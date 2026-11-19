using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=creatures, M4 ozellikleri (yaratik4, ICERIK §3.3-3.6; WildCreature.Traits.cs):
///  - menzilli yirtici (Projectile) uzaktan vurur ve vurusu gorunur (mermi/dil dugumu dogar);
///  - Dehliz (Burrow) beklerken toprak altinda, oyuncu yaklasinca cikip hucum eder;
///  - Paytak (BellySlide) kacarken karninin ustune yatar.
/// Bolunme (SplitInto) Wildlife dugumu ister; oyun sahnesinde Wildlife.Strike -> OnDowned yolundan isler.
/// </summary>
public partial class CreatureTest
{
    void CheckTraits(GameData data, PoseLibrary poses)
    {
        int n = 0;
        foreach (var def in data.Creatures.Creatures.Where(c => c.Rig != null && c.Wild != null))
        {
            var w = def.Wild!;
            if (string.IsNullOrEmpty(w.Projectile) && !w.Burrow && !w.BellySlide) continue;
            n++;
            var world = new FlatWorld { IsNight = w.Active == "night" };
            var at = new Vector3(2000 + n * 40f, 0, 0);
            var c = NewWild(def, poses, world, at);
            if (!c.Loaded) { Fail(def.Id, "aktor yuklenemedi"); continue; }
            var vis = c.GetNode<Node3D>("Visual");

            if (w.Burrow)
            {
                world.Player = at + new Vector3(60, 0, 0);
                Run(c.Tick, 6f);
                if (vis.Position.Y > -0.2f) Fail(def.Id, $"pusuda gomulu degil (y={vis.Position.Y:0.00})");
                float buried = vis.Position.Y;
                world.Player = c.GlobalPosition + new Vector3(0, 0, w.ChargeRadius * 0.7f);
                float top = buried;
                for (float t = 0; t < 4f; t += Dt) { c.Tick(Dt); top = Mathf.Max(top, vis.Position.Y); }
                if (top < -0.1f) Fail(def.Id, $"hucumda cikmadi (y={top:0.00})");
                GD.Print($"TRAIT {def.Id} burrow gomulu={buried:0.00} cikis={top:0.00}");
            }

            if (!string.IsNullOrEmpty(w.Projectile))
            {
                int before = GetChildCount();
                world.Events.Clear();
                // Oyuncu menzilin icinde ama yakin vurus mesafesinin disinda durur.
                world.Player = c.GlobalPosition + new Vector3(0, 0, w.AttackRange * 0.8f);
                float minD = float.MaxValue;
                for (float t = 0; t < 10f && !world.Events.Contains(WildEvent.Struck); t += Dt)
                {
                    c.Tick(Dt);
                    minD = Mathf.Min(minD, c.GlobalPosition.DistanceTo(world.Player));
                }
                if (!world.Events.Contains(WildEvent.Struck)) Fail(def.Id, $"menzilli vurmadi ({w.Projectile})");
                else if (GetChildCount() <= before) Fail(def.Id, $"vurus gorunmuyor ({w.Projectile})");
                if (w.AttackRange > 3f && minD < 2.5f) Fail(def.Id, $"menzilli tur dibine girdi ({minD:0.0} m)");
                GD.Print($"TRAIT {def.Id} projectile={w.Projectile} range={w.AttackRange} enYakin={minD:0.0}m");
            }

            if (w.BellySlide)
            {
                world.Player = c.GlobalPosition + new Vector3(w.FleeRadius * 0.5f, 0, 0);
                float tilt = 0;
                for (float t = 0; t < 1.5f; t += Dt) { c.Tick(Dt); tilt = Mathf.Max(tilt, vis.Rotation.X); }
                if (tilt < 0.6f) Fail(def.Id, $"karin ustu kaymiyor (egim {Mathf.RadToDeg(tilt):0} derece)");
                GD.Print($"TRAIT {def.Id} slide egim={Mathf.RadToDeg(tilt):0}");
            }
            c.QueueFree();
        }
        if (n < 7) _fail.Add($"M4 ozellikli tur sayisi {n} < 7");
    }
}
