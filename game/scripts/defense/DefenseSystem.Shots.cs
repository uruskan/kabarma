using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Kule atislari: Civi mermisi (one nisanli, dogrusal), Sapan tasi (kavis, alan), Sacma (koni, aninda).</summary>
public partial class DefenseSystem
{
    sealed class Shot
    {
        public MeshInstance3D Mesh;
        public Vector3 From, V0, To;
        public float T, Dur, Gravity, Damage, Splash;
        public int Target;
        public bool Arc, Swell;
        public string Tower;
    }

    sealed class Beam { public MeshInstance3D Mesh; public float Life; }
    readonly List<Beam> _beams = new();

    static string ShotVfx(TowerDef d) => "tower_" + d.Id.Replace("_kulesi", "").Replace("_bobini", "") + "_shot";

    /// <summary>Anlik isin (Ark, Lazer): iki nokta arasi ince ve kisa omurlu kutu.</summary>
    void BeamBetween(Vector3 a, Vector3 b, Color c, float life, float width = 0.06f)
    {
        float len = a.DistanceTo(b);
        if (len < 0.05f) return;
        var m = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, width, len) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = c, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 2f },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(m);
        m.GlobalPosition = (a + b) / 2f;
        m.LookAt(b, Mathf.Abs((b - a).Normalized().Y) > 0.95f ? Vector3.Right : Vector3.Up);
        _beams.Add(new Beam { Mesh = m, Life = life });
    }

    /// <summary>Ark Bobini ve Elektrikli Cit: hedefe Sok (durdurur), yakindakilere zincir (combat.json shock).</summary>
    void ShockChain(Zipzip z, TrapDef d, TrapEffect e)
    {
        z.DefenseHold(e.Seconds);
        Vfx.Play("shock_chain", z.GlobalPosition + Vector3.Up * 0.5f);
        int hops = (int)e.Value;
        if (hops <= 0) return;
        var from = z.GlobalPosition;
        int self = _zips.FirstOrDefault(kv => kv.Value == z).Key;
        foreach (var t in _targets.Where(t => !t.IsPlayer && t.Hp > 0 && t.Id != self && G(t.Pos).DistanceTo(from) <= d.ChainRadius + t.Radius).OrderBy(t => G(t.Pos).DistanceTo(from)).Take(hops))
        {
            if (!_zips.TryGetValue(t.Id, out var o) || !IsInstanceValid(o)) continue;
            Hurt(t.Id, d.Dps * d.TriggerEvery * 0.6f, from, 0f);
            o.DefenseHold(e.Seconds * 0.6f);
            BeamBetween(from + Vector3.Up * 0.5f, G(t.Pos) + Vector3.Up * 0.5f, Palette.Warning.Lerp(Palette.OffWhite, 0.5f), 0.12f, 0.04f);
        }
    }

    readonly List<Shot> _shots = new();

    static Color AmmoColor(string item) => item switch
    {
        "stone" => Palette.Rock, "turnip" => new Color("C9A0DC"), "pumpkin" => Palette.Accent, "tomato" => new Color("D9442E"),
        _ => Palette.DarkGrey.Lerp(Palette.Accent, 0.3f),
    };

    void Shoot(DefenseNode n, TowerDef d, string item, DefenseTarget x, Vector3 aimPt)
    {
        var from = n.Muzzle;
        switch (d.Kind)
        {
            case "single":
            {
                float dur = from.DistanceTo(aimPt) / Mathf.Max(1f, d.Speed);
                var m = Bullet(new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.28f) }, AmmoColor(item), from);
                m.LookAt(aimPt == from ? from + Vector3.Forward : aimPt);
                _shots.Add(new Shot { Mesh = m, From = from, To = aimPt, Dur = Mathf.Max(0.02f, dur), Damage = d.Damage, Target = x.Id, Tower = d.Id });
                Sfx.Play("hit", from, -10f, 1.8f);
                Vfx.Play("tower_civi_shot", from, aimPt - from);
                break;
            }
            case "arc":
            {
                var to = aimPt with { Y = _d.World.Ground(aimPt).Y + 0.1f };
                var v0 = G(TowerRules.ArcVelocity(ToN(from), ToN(to), d.FlightSeconds, d.Gravity));
                var m = Bullet(new SphereMesh { Radius = 0.14f, Height = 0.28f, RadialSegments = 8, Rings = 4 }, AmmoColor(item), from);
                _shots.Add(new Shot { Mesh = m, From = from, V0 = v0, To = to, Dur = d.FlightSeconds, Gravity = d.Gravity, Damage = d.Damage, Splash = d.Splash, Arc = true, Target = x.Id, Tower = d.Id });
                Sfx.Play("firlatma", from, -6f, d.Id == "havan_kulesi" ? 0.7f : 1.1f);
                Vfx.Play(Vfx.Has(ShotVfx(d)) ? ShotVfx(d) : "tower_sapan_shot", from, Vector3.Up);
                break;
            }
            case "cone":
            {
                var dir = aimPt - from; dir.Y = 0;
                var hits = TowerRules.ConeHits(d, ToN(from), ToN(dir), _targets);
                foreach (var u in TowerRules.ConeDirs(ToN(dir), d.Pellets, d.ConeDegrees))
                    Fx.Burst(from + G(u) * 0.6f, Palette.OffWhite.Lerp(Palette.Accent, 0.4f), 3, 9f, 0.05f, 0.18f, true, 0f);
                Fx.Burst(from, Palette.DarkGrey.Lerp(Palette.OffWhite, 0.5f), 10, 2f, 0.14f, 0.5f, false, -1f);
                Sfx.Play("pop", from, -2f, 0.7f);
                Vfx.Play(Vfx.Has(ShotVfx(d)) ? ShotVfx(d) : "tower_sacma_shot", from, dir);
                foreach (var (i, k) in hits.ToList())
                {
                    var t = _targets[i];
                    Hurt(t.Id, d.Damage * k, from, 1.5f * k);
                    if (d.Status == "burn") _st.AddBurn(t.Id, d.StatusValue, d.StatusSeconds); // Alev Kulesi (M4)
                    Fx.Burst(G(t.Pos), d.Status == "burn" ? Palette.Warning : Palette.Accent, 4 + k, 3f, 0.06f, 0.3f);
                }
                break;
            }
            case "chain": // Ark Bobini (M4): ilk hedef + zincirleme Sok
            {
                int first = _targets.FindIndex(t => t.Id == x.Id);
                if (first < 0) break;
                Vfx.Play(ShotVfx(d), from, aimPt - from);
                Sfx.Play("hit", from, -4f, 1.6f);
                var prev = from;
                foreach (var (i, mult) in TowerRules.Chain(d, first, _targets))
                {
                    var t = _targets[i];
                    Hurt(t.Id, d.Damage * mult, prev, 1f);
                    if (_zips.TryGetValue(t.Id, out var zz) && IsInstanceValid(zz)) { zz.DefenseHold(d.StatusSeconds); Vfx.Play("shock_chain", G(t.Pos) + Vector3.Up * 0.5f); }
                    BeamBetween(prev, G(t.Pos) + Vector3.Up * 0.5f, Palette.Warning.Lerp(Palette.OffWhite, 0.6f), 0.14f);
                    prev = G(t.Pos) + Vector3.Up * 0.5f;
                }
                break;
            }
            case "beam": // Lazer Kulesi (M4): anlik isin; Ayaz Kristali varsa guclenir, ucana cift
            {
                bool boosted = item.Length > 0;
                Hurt(x.Id, TowerRules.BeamDamage(d, x, boosted), from, 0.5f);
                BeamBetween(from, G(x.Pos) + Vector3.Up * 0.4f, boosted ? Palette.MayaTeal : Palette.HoldingRed, 0.1f, boosted ? 0.09f : 0.06f);
                Vfx.Play(ShotVfx(d), from, aimPt - from);
                Sfx.Play("hit", from, -8f, 2.0f);
                break;
            }
            case "swell": // Maya Topu Kulesi (M4): Maya mermisi, sisirir ve patlatir
            {
                float dur = from.DistanceTo(aimPt) / Mathf.Max(1f, d.Speed);
                var m = Bullet(new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 10, Rings = 5 }, Palette.MayaTeal.Lerp(Palette.MayaViolet, 0.4f), from);
                _shots.Add(new Shot { Mesh = m, From = from, To = aimPt, Dur = Mathf.Max(0.05f, dur), Damage = d.Damage, Target = x.Id, Tower = d.Id, Swell = true });
                Vfx.Play(ShotVfx(d), from, aimPt - from);
                Sfx.Play("firlatma", from, -4f, 0.8f);
                break;
            }
        }
    }

    MeshInstance3D Bullet(Mesh mesh, Color c, Vector3 at)
    {
        var m = new MeshInstance3D { Mesh = mesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.6f }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(m);
        m.GlobalPosition = at;
        return m;
    }

    void TickProjectiles(float dt)
    {
        for (int b = _beams.Count - 1; b >= 0; b--)
        {
            _beams[b].Life -= dt;
            if (_beams[b].Life > 0f) continue;
            _beams[b].Mesh.QueueFree();
            _beams.RemoveAt(b);
        }
        for (int i = _shots.Count - 1; i >= 0; i--)
        {
            var s = _shots[i];
            s.T += dt;
            float u = Mathf.Min(1f, s.T / s.Dur);
            if (s.Arc) s.Mesh.GlobalPosition = G(TowerRules.ArcAt(ToN(s.From), ToN(s.V0), s.Gravity, Mathf.Min(s.T, s.Dur)));
            else s.Mesh.GlobalPosition = s.From.Lerp(s.To, u);
            if (u < 1f) continue;
            if (!_d.NetClient) Land(s);
            s.Mesh.QueueFree();
            _shots.RemoveAt(i);
        }
    }

    void Land(Shot s)
    {
        if (s.Arc)
        {
            Fx.Burst(s.To + Vector3.Up * 0.1f, Palette.Soil.Lerp(Palette.Rock, 0.4f), 16, 4f, 0.1f, 0.5f);
            Sfx.Play("hit", s.To, -4f, 0.8f);
            foreach (int i in TowerRules.Splash(ToN(s.To), s.Splash, _targets)) Hurt(_targets[i].Id, s.Damage, s.To, 3f);
            return;
        }
        // Civi: hedef inis noktasinin yakinindaysa isabet (one nisan tuttu mu)
        int k = _targets.FindIndex(t => t.Id == s.Target);
        if (k < 0) return;
        var t = _targets[k];
        if (s.Swell)
        {
            if (G(t.Pos).DistanceTo(s.To) > t.Radius + 1.0f) return;
            Hurt(t.Id, s.Damage * 0.25f, s.From, 1f);
            var (burst, aoe, self, radius) = _st.AddSwell(t.Id, s.Damage, t.MaxHp, _d.Data.Combat.Swell);
            Vfx.Play("hit_weak", G(t.Pos) + Vector3.Up * 0.5f);
            if (!burst) return;
            Vfx.Play("swell_burst", G(t.Pos) + Vector3.Up * 0.5f);
            Hurt(t.Id, self, s.To, 3f);
            foreach (int i in TowerRules.Splash(t.Pos, radius, _targets).Where(i => _targets[i].Id != t.Id).ToList()) Hurt(_targets[i].Id, aoe, G(t.Pos), 4f);
            SwellBursts++;
            GD.Print($"DEFENSE swell burst target={t.Id} aoe={aoe:0}");
            return;
        }
        if (G(t.Pos).DistanceTo(s.To) <= t.Radius + 0.7f) Hurt(t.Id, s.Damage, s.From, 1f);
    }

    /// <summary>Yanik ilerler ve yayilir (combat.json burn); olenlerin kabarigi silinir.</summary>
    void TickStatuses(float dt)
    {
        var alive = new HashSet<int>(_targets.Where(t => t.Hp > 0).Select(t => t.Id));
        var b = _d.Data.Combat.Burn;
        foreach (var (id, dmg) in _st.StepBurn(dt, 0.5f, alive))
        {
            Hurt(id, dmg, Vector3.Zero, 0f);
            BurnTicks++;
            if (_zips.TryGetValue(id, out var z) && IsInstanceValid(z)) Vfx.Play("burn_spread", z.GlobalPosition + Vector3.Up * 0.5f);
        }
        _spreadT += dt;
        if (_spreadT >= b.SpreadEvery)
        {
            _spreadT = 0f;
            foreach (int id in _st.Spread(b, _targets)) _st.AddBurn(id, Mathf.Max(b.MinDps, 2f), b.Seconds);
        }
        _st.Prune(alive);
    }
}
