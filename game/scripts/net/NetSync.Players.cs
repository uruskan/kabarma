using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncular (TEKNIK 5.2: kendi hareketinde sahibi yetkili, host hiz ve konum tutarliligini denetler; 5.3: 16 B, 20 Hz).
/// Her surec kendi oyuncusunun karesini yollar; host bunlari dogrular ve anlik goruntuyle digerlerine dagitir. Uzak
/// oyuncular <see cref="NetAvatar"/> olarak gorunur (konum, govde yonu, klip, Capa basligi ve isini, eldeki esya).
/// </summary>
public partial class NetSync
{
    /// <summary>Uzak oyuncularin gorunusu (es → avatar). Host'ta butun istemciler; istemcide host ve oteki istemciler.</summary>
    public readonly Dictionary<byte, NetAvatar> Avatars = new();
    PlayerFrame _own;
    string _lastLook;
    readonly Dictionary<byte, (float, float, float)> _prevPos = new();
    readonly Dictionary<(byte, byte), float> _settleLeft = new();
    byte _lastHead = 255;

    void RegisterPlayers() { }

    /// <summary>Kendi oyuncumun karesi.</summary>
    public PlayerFrame LocalFrame()
    {
        var p = D.Player;
        var pos = p.GlobalPosition;
        var flags = NetFrameFlags.None;
        if (p.Down) flags |= NetFrameFlags.Down;
        if (p.IsOnFloor()) flags |= NetFrameFlags.OnFloor;
        if (D.Capa.UsingNow && D.Capa.InHand) flags |= NetFrameFlags.Firing;
        if (p.Carrying) flags |= NetFrameFlags.Carrying;
        if (p.PushingCart) flags |= NetFrameFlags.Pushing;
        if (new Vector2(p.Velocity.X, p.Velocity.Z).Length() > p.WalkSpeed * 1.1f) flags |= NetFrameFlags.Sprint;
        if (_talking) flags |= NetFrameFlags.Talking;
        if (_slapping) flags |= NetFrameFlags.Slapping;
        int clip = D.Data.PlayerAnims.Clips.FindIndex(c => c.Id == p.CurrentClip);
        return new PlayerFrame
        {
            Peer = LocalPeer, X = pos.X, Y = pos.Y, Z = pos.Z,
            Yaw = p.Visual.Rotation.Y, Pitch = p.Pitch.Rotation.X,
            Anim = (byte)(clip < 0 ? 255 : clip), Head = (byte)D.Capa.Current, Flags = flags,
        };
    }

    void TickFrame(double now)
    {
        if (!Joined) return;
        _own = LocalFrame();
        string look = D.State.Belt.Held ?? "";
        if (look != _lastLook || _own.Head != _lastHead)
        {
            _lastLook = look; _lastHead = _own.Head;
            var w = NetPackets.Begin(NetMsg.Look); w.U8(LocalPeer); w.Str(look);
            if (IsHost) Broadcast(w.ToArray()); else SendHost(w.ToArray());
        }
        if (IsClient) { SendHost(NetPackets.Frame(_own, Cfg.PosPerMetre), NetChannel.State); return; }

        // sekiz (M5): hareketsiz kalan oyuncunun son karesi, seyreltilmis olsa da her aliciya bir kez gider (donuk avatar kalmasin).
        var moved = new Dictionary<byte, bool>();
        void Track(in PlayerFrame f)
        {
            var pos = new Vector3(f.X, f.Y, f.Z);
            moved[f.Peer] = !_prevPos.TryGetValue(f.Peer, out var old) || System.Math.Abs(old.Item1 - pos.X) + System.Math.Abs(old.Item2 - pos.Y) + System.Math.Abs(old.Item3 - pos.Z) > 0.002f;
            _prevPos[f.Peer] = (pos.X, pos.Y, pos.Z);
        }
        Track(_own);
        foreach (var x in Actors.Values) if (x.HasFrame) Track(x.Last);
        // Host: her hazir istemciye kendisi disindaki oyuncular (host dahil) ve yakindaki yaratiklar.
        foreach (var (peer, a) in Actors)
        {
            if (!a.Ready) continue;
            var w = NetPackets.Begin(NetMsg.Snapshot, 16 + 17 * (Actors.Count + 1));
            w.U32(_tick);
            // sekiz (M5): ilgi alani. Alicinin yakinindaki oyuncular tam hizda, uzaktakiler seyreltilmis (NetInterest, net.json interest).
            float rx = a.HasFrame ? a.Last.X : _own.X, rz = a.HasFrame ? a.Last.Z : _own.Z;
            bool Want(in PlayerFrame f)
            {
                float d = Mathf.Sqrt((f.X - rx) * (f.X - rx) + (f.Z - rz) * (f.Z - rz));
                bool urgent = (f.Flags & (NetFrameFlags.Talking | NetFrameFlags.Down)) != 0;
                int div = NetInterest.Divisor(Cfg.Interest, d, urgent);
                var key = (peer, f.Peer);
                if (div <= 1) return true;
                if (moved.GetValueOrDefault(f.Peer, true)) { _settleLeft[key] = 1f; return NetInterest.SendThisTick(_tick, div, peer, f.Peer); }
                // durdu: bir saniye boyunca tam hizda (son kare kesin ulassin), sonra seyrek
                if (_settleLeft.TryGetValue(key, out var left) && left > 0f) { _settleLeft[key] = left - 1f / Cfg.FrameHz; return true; }
                return NetInterest.SendThisTick(_tick, div, peer, f.Peer);
            }
            var frames = new List<PlayerFrame>();
            if (Want(_own)) frames.Add(_own);
            foreach (var x in Actors.Values) if (x.Peer != peer && x.HasFrame && Want(x.Last)) frames.Add(x.Last);
            w.U8((byte)frames.Count);
            foreach (var f in frames) f.Write(w, Cfg.PosPerMetre);
            WriteCreatures(w, a, now); // NetSync.Creatures.cs
            SendTo(peer, w.ToArray(), NetChannel.State);
        }
    }

    void OnFrame(NetActor a, PlayerFrame f, double now)
    {
        f.Peer = a.Peer;
        float max = D.Data.Tool.Player.SprintSpeed * Mathf.Max(1f, D.Player.SpeedMult) * 1.15f;
        if (!NetRules.MovementOk(a.Last, a.LastTime, f, now, max, Cfg))
        {
            // Kabul edilmeyen kare: istemci son kabul edilen yere doner (TEKNIK 5.5).
            a.Corrections++; Corrections++;
            var w = NetPackets.Begin(NetMsg.Correction); w.Pos(a.Last.X, a.Last.Y, a.Last.Z, Cfg.PosPerMetre);
            SendTo(a.Peer, w.ToArray());
            a.LastTime = now;
            return;
        }
        a.Last = f;
        a.LastTime = now;
        if (Avatars.TryGetValue(a.Peer, out var av)) av.Push(f, now);
    }

    void OnSnapshot(NetReader r, double now)
    {
        r.U32();
        int n = r.U8();
        for (int i = 0; i < n; i++)
        {
            var f = PlayerFrame.Read(r, Cfg.PosPerMetre);
            if (f.Peer == LocalPeer) continue;
            if (!Avatars.TryGetValue(f.Peer, out var av)) av = SpawnAvatar(f.Peer, Roster.FirstOrDefault(e => e.Peer == f.Peer)?.Name ?? $"Oyuncu {f.Peer}");
            av.Push(f, now);
        }
        ReadCreatures(r, now); // NetSync.Creatures.cs
    }

    void OnLook(NetActor from, NetReader r)
    {
        byte peer = r.U8();
        string held = r.Str();
        if (from != null) { peer = from.Peer; from.Held = held; var w = NetPackets.Begin(NetMsg.Look); w.U8(peer); w.Str(held); Broadcast(w.ToArray(), except: peer); }
        if (Avatars.TryGetValue(peer, out var av)) av.Held = held;
    }

    void OnCorrection(NetReader r)
    {
        var (x, y, z) = r.Pos(Cfg.PosPerMetre);
        D.Player.GlobalPosition = new Vector3(x, y, z);
        D.Player.Velocity = Vector3.Zero;
        Corrections++;
        GD.Print($"NET correction -> {x:0.00},{y:0.00},{z:0.00}");
    }

    NetAvatar SpawnAvatar(byte peer, string name)
    {
        if (Avatars.TryGetValue(peer, out var old)) { old.PlayerName = name; return old; }
        var av = new NetAvatar(this, peer, name) { Name = $"Avatar{peer}" };
        D.AddChild(av);
        Avatars[peer] = av;
        return av;
    }

    void DespawnAvatar(byte peer)
    {
        if (Avatars.Remove(peer, out var av)) av.QueueFree();
    }

    /// <summary>Istemci: oyuncu listesine gore avatarlari kur ya da kaldir.</summary>
    void SyncAvatars()
    {
        foreach (var e in Roster) if (e.Peer != LocalPeer) SpawnAvatar(e.Peer, e.Name);
        foreach (var peer in Avatars.Keys.Where(p => Roster.All(e => e.Peer != p)).ToList()) DespawnAvatar(peer);
    }
}
