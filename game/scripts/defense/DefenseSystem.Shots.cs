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
        public bool Arc;
        public string Tower;
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
                Sfx.Play("firlatma", from, -6f, 1.1f);
                Vfx.Play("tower_sapan_shot", from, Vector3.Up);
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
                Vfx.Play("tower_sacma_shot", from, dir);
                foreach (var (i, k) in hits.ToList())
                {
                    var t = _targets[i];
                    Hurt(t.Id, d.Damage * k, from, 1.5f * k);
                    Fx.Burst(G(t.Pos), Palette.Accent, 4 + k, 3f, 0.06f, 0.3f);
                }
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
        if (G(t.Pos).DistanceTo(s.To) <= t.Radius + 0.7f) Hurt(t.Id, s.Damage, s.From, 1f);
    }
}
