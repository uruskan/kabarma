using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Yaratiklar (TEKNIK 5.2: host yetkili; 5.3: 120 m icinde 10 Hz, uzakta 2 Hz). Host'ta yaratiklar kendi yapay zekasiyla
/// yasar; istemcide ayni dugumler kukladir: islemleri kapatilir, konum ve yon anlik goruntuden gelir.
/// Kimlik: "creatures" grubundaki dugumlerin kurulus sirasi (ayni tohum, ayni dunya) + gece dalgalari icin host'un
/// verdigi kimlik (ileride: tanimlayiciyla dogurma). Yeni yaratik turu: dugumu "net_creature" grubuna ekle.
/// </summary>
public partial class NetSync
{
    public const string CreatureGroup = "net_creature";
    readonly List<Node3D> _creatures = new();
    readonly Dictionary<(byte peer, int id), double> _creatureNext = new();
    readonly Dictionary<ushort, (Vector3 from, Vector3 to, float yawFrom, float yawTo, double t0, double t1)> _creatureLerp = new();
    double _creatureScan;

    void RegisterCreatures() { }

    /// <summary>Kimlik listesini kurar: yeni gelenler sona eklenir (iki tarafta ayni sirayla kurulanlar ayni kimligi alir).</summary>
    void ScanCreatures()
    {
        foreach (var n in GetTree().GetNodesInGroup(CreatureGroup).OfType<Node3D>().Concat(KnownCreatures()))
        {
            if (!GodotObject.IsInstanceValid(n) || _creatures.Contains(n)) continue;
            _creatures.Add(n);
            if (IsClient) Puppet(n);
        }
    }

    /// <summary>M1 yaratiklari (gruba henuz girmeyenler): Gidak, Pofuduk. Zipzip surusu NetSync'e grupla baglanir.</summary>
    IEnumerable<Node3D> KnownCreatures()
    {
        if (D.Gidak != null) yield return D.Gidak;
        if (D.Pofuduk != null) yield return D.Pofuduk;
    }

    /// <summary>Istemcide yaratigin kendi islemi kapanir; animasyon oynatici calismaya devam eder.</summary>
    static void Puppet(Node3D n)
    {
        n.ProcessMode = ProcessModeEnum.Disabled;
        foreach (var ap in n.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>()) ap.ProcessMode = ProcessModeEnum.Always;
    }

    void TickCreatures(double now)
    {
        if (now >= _creatureScan) { _creatureScan = now + 1.0; ScanCreatures(); ScanDynamic(); }
    }

    /// <summary>Kimlikten dugum: 0x8000 biti sonradan doganlar (NetSync.Spawns.cs), digerleri kurulus sirasi.</summary>
    Node3D CreatureNode(ushort id)
    {
        if ((id & DynBit) != 0) return DynNode(id);
        return id < _creatures.Count && GodotObject.IsInstanceValid(_creatures[id]) ? _creatures[id] : null;
    }

    void WriteCreatures(NetWriter w, NetActor to, double now)
    {
        var me = new Vector3(to.Last.X, to.Last.Y, to.Last.Z);
        var list = new List<CreatureFrame>();
        for (int i = 0; i < _creatures.Count; i++)
        {
            var n = _creatures[i];
            if (!GodotObject.IsInstanceValid(n) || !n.IsInsideTree()) continue;
            var key = (to.Peer, i);
            float hz = n.GlobalPosition.DistanceTo(me) <= Cfg.CreatureNearRadius ? Cfg.CreatureNearHz : Cfg.CreatureFarHz;
            if (_creatureNext.TryGetValue(key, out var next) && now < next - 1e-3) continue;
            _creatureNext[key] = now + 1.0 / hz;
            var p = n.GlobalPosition;
            list.Add(new CreatureFrame { Id = (ushort)i, X = p.X, Y = p.Y, Z = p.Z, Yaw = n.GlobalRotation.Y, State = (byte)(n.Visible ? 1 : 0) });
        }
        WriteDynamicFrames(list, to, now); // NetSync.Spawns.cs
        // Paket MTU altinda kalir: sigmayan kareler ayri "Creatures" paketleriyle gider (ENet buyuk guvenilmez paketi
        // guvenilir parcalara boler; sira tikanmasin).
        int first = Mathf.Clamp((MaxPacket - w.Length - 2) / CreatureFrame.Size, 0, list.Count);
        w.U16((ushort)first);
        for (int i = 0; i < first; i++) list[i].Write(w, Cfg.PosPerMetre);
        int per = (MaxPacket - 3) / CreatureFrame.Size;
        for (int i = first; i < list.Count; i += per)
        {
            int n = System.Math.Min(per, list.Count - i);
            var c = NetPackets.Begin(NetMsg.Creatures, 3 + n * CreatureFrame.Size);
            c.U16((ushort)n);
            for (int k = 0; k < n; k++) list[i + k].Write(c, Cfg.PosPerMetre);
            SendTo(to.Peer, c.ToArray(), NetChannel.State);
            CreaturePackets++;
        }
    }

    /// <summary>Tek paketin en buyuk boyu (bayt): ENet MTU'su 1400, baslik payi.</summary>
    const int MaxPacket = 1200;
    /// <summary>Ek yaratik paketi sayisi (olcum).</summary>
    public int CreaturePackets { get; private set; }

    void ReadCreatures(NetReader r, double now)
    {
        if (r.AtEnd) return;
        if (now >= _creatureScan) { _creatureScan = now + 1.0; ScanCreatures(); }
        int n = r.U16();
        for (int i = 0; i < n; i++)
        {
            var f = CreatureFrame.Read(r, Cfg.PosPerMetre);
            var node = CreatureNode(f.Id);
            if (node == null) continue;
            node.Visible = (f.State & 1) != 0;
            var to = new Vector3(f.X, f.Y, f.Z);
            var from = _creatureLerp.TryGetValue(f.Id, out var l) ? node.GlobalPosition : to;
            _creatureLerp[f.Id] = (from, to, node.GlobalRotation.Y, f.Yaw, now, now + 1.0 / Cfg.CreatureNearHz);
        }
    }

    /// <summary>Istemci: kukla yaratiklar iki kare arasinda yumusak ilerler.</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (IsHost || _creatureLerp.Count == 0) return;
        double now = Now;
        foreach (var (id, l) in _creatureLerp)
        {
            var n = CreatureNode(id);
            if (n == null) continue;
            float k = l.t1 > l.t0 ? Mathf.Clamp((float)((now - l.t0) / (l.t1 - l.t0)), 0f, 1f) : 1f;
            n.GlobalPosition = l.from.Lerp(l.to, k);
            n.GlobalRotation = new Vector3(n.GlobalRotation.X, Mathf.LerpAngle(l.yawFrom, l.yawTo, k), n.GlobalRotation.Z);
        }
    }
}
