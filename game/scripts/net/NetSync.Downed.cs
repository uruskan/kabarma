using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bayilma ve sok tokadi (GDD 3.6): ortak oyunda can sifirlaninca oyuncu 30 sn baygin kalir; bir arkadasi yaninda E'yi
/// 1,5 sn basili tutarak (sok tokadi) %30 canla kaldirir; sure dolarsa olur ve dogma noktasinda kalkar.
/// Baygin kalma yerel oyuncuda (Player.Net.cs); tokat host'ta dogrulanir: istemci basili tutarken 0,1 sn'de bir
/// "slap" istegi yollar, host birikimi DownedRules.Revive ile sayar ve kalkani bildirir.
/// </summary>
public partial class NetSync
{
    bool _slapping;
    float _slapSend, _slapLocal;
    /// <summary>Host'ta hedef es basina tokat durumu (host'un kendisi 0).</summary>
    readonly System.Collections.Generic.Dictionary<byte, DownedState> _downed = new();
    readonly System.Collections.Generic.Dictionary<byte, double> _slapSeen = new();
    public int Revives;

    void RegisterDowned()
    {
        On("slap", HostSlap);
    }

    /// <summary>Yakindaki baygin oyuncu (tokat hedefi).</summary>
    public NetAvatar DownedNear(Vector3 from) =>
        Avatars.Values.Where(a => a.Down && a.GlobalPosition.DistanceTo(from) <= Cfg.Downed.ReviveRange).OrderBy(a => a.GlobalPosition.DistanceTo(from)).FirstOrDefault();

    /// <summary>E basili mi (oyuncu girdisi; bot SlapHeld'i kurar).</summary>
    public bool SlapHeld;

    void TickDowned(float dt, double now)
    {
        if (!Joined) return;
        bool held = SlapHeld || (!Main.Args.ContainsKey("bot") && Input.IsActionPressed("interact"));
        var target = !D.Player.Down && held ? DownedNear(D.Player.GlobalPosition) : null;
        _slapping = target != null;
        if (_slapping)
        {
            _slapLocal += dt;
            D.Hud?.Hint(Loc.T("net.slap", target.PlayerName, Mathf.Min(100, (int)(_slapLocal / Cfg.Downed.ReviveHold * 100))), 0.2f);
            _slapSend -= dt;
            if (_slapSend <= 0)
            {
                _slapSend = 0.1f;
                if (IsHost) SlapTick(target.Peer, 0.1f, true, now);
                else Forward("slap", w => w.U8(target.Peer));
            }
        }
        else _slapLocal = 0;

        if (!IsHost) return;
        // Host: tokat gelmeyen hedefin birikimi sifirlanir (birakti ya da uzaklasti).
        foreach (var (peer, s) in _downed)
            if (s.Hold > 0 && (!_slapSeen.TryGetValue(peer, out var seen) || now - seen > 0.35)) s.Hold = 0;
    }

    void HostSlap(NetActor a, NetReader r)
    {
        byte target = r.U8();
        var tpos = target == 0 ? D.Player.GlobalPosition : Avatars.TryGetValue(target, out var av) ? av.GlobalPosition : Vector3.Inf;
        bool inRange = NetRules.InRange(a, tpos.X, tpos.Y, tpos.Z, Cfg.Downed.ReviveRange + 0.75f);
        if (!inRange) { Reject(a, "slap range"); return; }
        SlapTick(target, 0.1f, true, Now);
    }

    /// <summary>Host: bir tokat dilimi. Hedef baygin degilse yok sayilir.</summary>
    void SlapTick(byte target, float dt, bool inRange, double now)
    {
        bool down = target == 0 ? D.Player.Down : Avatars.TryGetValue(target, out var av) && av.Down;
        if (!down) { _downed.Remove(target); return; }
        if (!_downed.TryGetValue(target, out var s)) _downed[target] = s = new DownedState { Down = true, Left = Cfg.Downed.Seconds };
        s.Down = true;
        _slapSeen[target] = now;
        if (!DownedRules.Revive(s, dt, holding: true, inRange, Cfg.Downed)) return;
        _downed.Remove(target);
        Revives++;
        GD.Print($"NET revive peer={target}");
        if (target == 0) D.Player.NetRevive(DownedRules.ReviveHp(D.Player.MaxHp, Cfg.Downed));
        else { var w = NetPackets.Begin(NetMsg.Revived); w.U8(target); SendTo(target, w.ToArray()); }
    }

    void OnRevived(NetReader r)
    {
        if (r.U8() != LocalPeer) return;
        Revives++;
        D.Player.NetRevive(DownedRules.ReviveHp(D.Player.MaxHp, Cfg.Downed));
    }
}
