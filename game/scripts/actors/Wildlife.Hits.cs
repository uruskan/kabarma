using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Gunduz vuruslari (GDD §16.3): Capa'nin mermileri yabani yaratiklara ve surulere de deger. M2'de darbe yaratigi
/// urkutur (kacar, yirtici ofkelenir) ve can dusurur; can biterse yaratik "Down" olayini yayar ve sahneden kalkar
/// (ganimet, ragdoll ve leş M3). Yakinda vurulmasi butun suruyu urkutur.
/// </summary>
public partial class Wildlife
{
    /// <summary>Mermi bu karede from->to dogrusunda bir yabaniye degdi mi; degdiyse hasar verir (ilk degen).</summary>
    public bool ProjectileHit(Vector3 from, Vector3 to, float radius, float damage, ISet<Node3D> already = null, Kabarma.Sim.DamageType type = Kabarma.Sim.DamageType.Darbe) =>
        _d.Net?.ForwardWildHit(from, to, radius, damage, already) // ortak: istemcide vurus host'ta (NetSync.Spawns.cs)
        ?? Strike(Creatures, Swarms, from, to, radius, damage, already, type) != null;

    /// <summary>Dunya dugumunden bagimsiz vurus: verilen listelerde ilk degeni bulur, vurur, cevresini urkutur.</summary>
    public static Node3D Strike(IList<WildCreature> creatures, IList<WildSwarm> swarms, Vector3 from, Vector3 to, float radius, float damage, ISet<Node3D> already = null, Kabarma.Sim.DamageType type = Kabarma.Sim.DamageType.Darbe)
    {
        var seg = to - from;
        float len2 = Mathf.Max(0.0001f, seg.LengthSquared());
        foreach (var c in creatures)
        {
            if (!IsInstanceValid(c) || c.Dead || c.Display || (already?.Contains(c) ?? false)) continue;
            float h = c.Def.Rig?.Height ?? 0.8f;
            var center = c.GlobalPosition + Vector3.Up * h * 0.5f;
            float t = Mathf.Clamp((center - from).Dot(seg) / len2, 0, 1);
            if ((from + seg * t).DistanceTo(center) > radius + h * 0.45f) continue;
            already?.Add(c);
            c.TakeHit(new Kabarma.Sim.CombatHit(damage, type, Kabarma.Sim.CombatTeam.Player), from); // savas: tur profili ve durumlar (combat/WildCreature.Combat.cs)
            if (c.IsInsideTree()) Fx.Burst(center, Palette.OffWhite, 6, 2f, 0.06f, 0.4f, true, -4f);
            Startle(creatures, center, 12f);
            if (c.Dead) { GD.Print($"WILD {c.Def.Id}#{c.State.Id} down"); Events.RaiseWildActed(c.Def.Id, "Down", center.X, center.Z); c.OnDowned(); c.QueueFree(); } // OnDowned: bolunen tur (WildCreature.Traits.cs)
            return c;
        }
        foreach (var s in swarms)
        {
            if (!IsInstanceValid(s) || s.Dead || s.Display || (already?.Contains(s) ?? false)) continue;
            var center = s.GlobalPosition + Vector3.Up * (s.Def.Swarm?.Height ?? 0.5f);
            float t = Mathf.Clamp((center - from).Dot(seg) / len2, 0, 1);
            if ((from + seg * t).DistanceTo(center) > radius + (s.Def.Swarm?.Radius ?? 1f)) continue;
            already?.Add(s);
            s.Hit(damage, from);
            if (s.Dead) { GD.Print($"WILD {s.Def.Id}#{s.State.Id} down"); Events.RaiseWildActed(s.Def.Id, "Down", center.X, center.Z); }
            return s;
        }
        return null;
    }

    /// <summary>Yakindaki yabani suruler urker (ates, darbe).</summary>
    public void Startle(Vector3 at, float radius) => Startle(Creatures, at, radius);

    static void Startle(IList<WildCreature> creatures, Vector3 at, float radius)
    {
        foreach (var c in creatures)
            if (IsInstanceValid(c) && !c.Dead && !c.Display && c.GlobalPosition.DistanceTo(at) < radius) c.Startle();
    }
}
