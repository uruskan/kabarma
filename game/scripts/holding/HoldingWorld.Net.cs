using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Holding'in ortak oyuna baglanmasi (CLAUDE.md M2 notu). Host yetkili:
///   "save:holding" : HoldingState'in tamami (kayit kopru, NetSync.Save.cs); istemci uygular, birimleri kendisi yurutmez.
///   "holding_hurt" : es basina biriken birim hasari (sira, toplam, kaynak); istemci farki kendi oyuncusuna uygular.
///   "holding_hit"  : istemcinin mermisi Holding'e degdi; host menzili ve hasar sinirini dogrular, ganimet aktorun cantasina.
/// </summary>
public partial class HoldingWorld
{
    /// <summary>Bir istemci vurusunun kabul edilen en buyuk hasari ve aktor-hedef uzakligi (hile siniri).</summary>
    const float MaxNetDamage = 400f, MaxNetReach = 160f;
    readonly Dictionary<byte, (uint seq, float total, float x, float z)> _hurt = new();
    bool _hurtInit;
    float _hurtApplied;

    void InitNet()
    {
        var net = D.Net;
        if (net == null) return;
        // HoldingState (kamplar, birim konumlari, kervan) kayit kopru ile gelir: "save:holding" (NetSync.Save.cs, saniyede
        // bir, ozet disi). Istemci birimleri o konumlara yumusak yurutur (HoldingUnitActor.Step).
        net.Register("holding_hurt", true,
            (w, peer) => { var h = _hurt.GetValueOrDefault((byte)peer); w.VarU(h.seq); w.F32(h.total); w.F32(h.x); w.F32(h.z); },
            r =>
            {
                uint seq = r.VarU(); float total = r.F32(), x = r.F32(), z = r.F32();
                if (Host) return;
                _hurt[D.Net.LocalPeer] = (seq, total, x, z); // istemci son aldigini yazar: ozet esit kalir
                if (!_hurtInit) { _hurtInit = true; _hurtApplied = total; return; } // katilmadan onceki hasar sayilmaz
                if (total > _hurtApplied + 0.01f) { D.Player.Hurt(total - _hurtApplied, new Vector3(x, D.Player.GlobalPosition.Y, z)); }
                _hurtApplied = Mathf.Max(_hurtApplied, total);
            });
        net.On("holding_hit", (actor, r) =>
        {
            var kind = (HitKind)r.U8();
            int camp = r.VarI();
            string structure = r.Str();
            int unit = r.VarI();
            float dmg = r.F32();
            var target = TargetPoint(kind, camp, structure, unit);
            var me = new Vector3(actor.Last.X, actor.Last.Y, actor.Last.Z);
            if (target == null || dmg <= 0 || dmg > MaxNetDamage || me.DistanceTo(target.Value) > MaxNetReach) { actor.Rejected++; return; }
            Apply(new HitFound(kind, camp, structure, unit, target.Value), dmg, actor.Bag);
        });
    }

    /// <summary>Istemcide vurus host'a gider (true); host'ta ve tek oyuncuda false.</summary>
    bool ForwardHit(HitFound f, float damage) =>
        D.Net?.Forward("holding_hit", w => { w.U8((byte)f.Kind); w.VarI(f.Camp); w.Str(f.Structure); w.VarI(f.Unit); w.F32(damage); }) == true;

    Vector3? TargetPoint(HitKind kind, int camp, string structure, int unit)
    {
        switch (kind)
        {
            case HitKind.Unit: return _units.TryGetValue(unit, out var a) && IsInstanceValid(a) ? a.Center : null;
            case HitKind.Caravan: return _trucks.TryGetValue(camp, out var t) && IsInstanceValid(t) ? t.GlobalPosition : null;
            case HitKind.Structure:
                var c = H.Camp(camp);
                return c != null && c.Structures.ContainsKey(structure) ? new Vector3(c.X, D.World.HeightAt(c.X, c.Z), c.Z) : null;
            default: return null;
        }
    }

    /// <summary>Host: es numarasina gore hasar (host oyuncusu yerel, uzak oyuncu "holding_hurt" bolumuyle); Mudur dovusu kullanir.</summary>
    public void HurtPeer(byte peer, float dmg, Vector3 from)
    {
        if (D.Net == null || (peer == D.Net.LocalPeer && !D.Net.Dedicated)) D.Player.Hurt(dmg, from);
        else HurtRemote(peer, dmg, from);
    }

    /// <summary>Host: uzak oyuncuya birim hasari (bolumle gider).</summary>
    void HurtRemote(byte peer, float dmg, Vector3 from)
    {
        var h = _hurt.GetValueOrDefault(peer);
        _hurt[peer] = (h.seq + 1, h.total + dmg, from.X, from.Z);
    }
}
