using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arac eklentilerinin ve Bicerdover'in isi (ICERIK §14), yalniz host'ta ve tek oyuncuda calisir (surucu varken):
///  - Pulluk: arkasindaki seritte tapuda tarla surer; Ekim Makinesi: surulmus kareye secili tohumu eker; Su Tankeri: kareyi sular;
///  - Kepce: oniindeki kayalari kazar (cantaya); Bicerdover: oniindeki olgun ekini bicer (cantaya, depolar), yaratigi biçer;
///  - Zirh ve Tampon: hizliyken oniindeki yaratiga carpma hasari (Darbe), Bicerdover da yaratik biçer.
/// Tarla kurallari Farm'dadir (egim siniri, tapu); burada yalniz arac yolunu kareye cevirir.
/// </summary>
public partial class Director
{
    readonly Dictionary<ulong, double> _rammed = new();
    float _workAcc;

    void RunVehicleWork(VehicleNode v, float dt)
    {
        var s = v.State; var d = v.Def; var f = Data.Vehicles;
        if (s.Flipped || s.Hp <= 0) return;
        bool moving = Mathf.Abs(s.Speed) > 0.4f;
        _workAcc += dt;
        bool tick = _workAcc >= 0.2f;
        if (tick) _workAcc = 0f;
        if (moving && tick)
        {
            foreach (var id in s.Attachments)
            {
                var a = f.Attachment(id);
                if (a == null) continue;
                if (id == "pulluk") WorkTill(v, a);
                else if (id == "ekim_makinesi") WorkSow(v, a);
                else if (id == "su_tankeri") WorkWater(v, a);
            }
            if (v.HasAttachment("kepce")) WorkScoop(v, 4f);
        }
        if (d.HarvestRadius > 0 && s.Riders.Count > 0 && tick) WorkHarvest(v);
        Ram(v, dt);
    }

    bool Tapu(Vector3 p) => Ocak != null && p.DistanceTo(OcakPos) <= Ocak.ClaimRadius && p.DistanceTo(OcakPos) >= Data.Tool.TillClearOcak && p.DistanceTo(Pod.GlobalPosition) >= Data.Tool.TillClearPod;

    float SlopeDeg(float cx, float cz)
    {
        float dx = World.HeightAt(cx + 0.5f, cz) - World.HeightAt(cx - 0.5f, cz), dz = World.HeightAt(cx, cz + 0.5f) - World.HeightAt(cx, cz - 0.5f);
        return Mathf.RadToDeg(Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)));
    }

    /// <summary>Tarla kareleri 1 m; aracin seridindeki kareler (Vehicles.WorkCells).</summary>
    IEnumerable<(int x, int y, Vector3 c)> Strip(VehicleNode v, AttachmentDef a) =>
        Kabarma.Sim.Vehicles.WorkCells(v.State, a).Select(c => (c.x, c.y, FarmView.CellCenter(c.x, c.y)));

    public int VehicleTilled, VehicleSown, VehicleWatered, VehicleHarvested, VehicleScooped;

    void WorkTill(VehicleNode v, AttachmentDef a)
    {
        var farm = State.Farm;
        foreach (var (x, y, c) in Strip(v, a))
        {
            if (!Tapu(c) || farm.Get(x, y) != null) continue;
            if (!farm.Till(x, y, SlopeDeg(x + 0.5f, y + 0.5f))) continue;
            VehicleTilled++;
            Fx.Burst(c + Vector3.Up * 0.1f, Palette.Soil, 6, 2f, 0.05f, 0.4f);
            State.Scenario.SetProgress("till", farm.Plots.Count());
        }
    }

    void WorkSow(VehicleNode v, AttachmentDef a)
    {
        var farm = State.Farm;
        foreach (var (x, y, c) in Strip(v, a))
        {
            var plot = farm.Get(x, y);
            if (plot == null || plot.Crop != null || !Tapu(c)) continue;
            string seed = FarmView.PlantableSeed();
            if (seed == null || !farm.Plant(x, y, seed, State.Backpack, State.Rng)) { Hud.Hint(Loc.T("dir.hint_no_seed_left"), 1f); return; }
            VehicleSown++;
            Fx.Burst(c + Vector3.Up * 0.2f, Palette.Plant, 5, 2f, 0.05f, 0.4f);
            State.Scenario.SetProgress("plant", farm.Plots.Count(q => q.Crop != null));
        }
    }

    void WorkWater(VehicleNode v, AttachmentDef a)
    {
        var farm = State.Farm;
        foreach (var (x, y, c) in Strip(v, a))
        {
            var plot = farm.Get(x, y);
            if (plot == null || plot.Moisture > 92f) continue;
            if (v.State.Water <= 0) { Hud.Hint(Loc.T("dir.hint_out_of_water"), 1.2f); return; }
            v.State.Water--;
            farm.Water(x, y);
            VehicleWatered++;
            Fx.Burst(c + Vector3.Up * 0.3f, new Color(0.4f, 0.7f, 0.95f), 5, 2f, 0.05f, 0.4f);
        }
    }

    void WorkScoop(VehicleNode v, float reach)
    {
        var front = v.GlobalPosition + new Vector3(Mathf.Sin(v.State.Yaw), 0, Mathf.Cos(v.State.Yaw)) * (VehicleBodies.Wheelbase(v.Def) * 0.5f + 1.2f);
        foreach (var rock in World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && new Vector2(r.GlobalPosition.X - front.X, r.GlobalPosition.Z - front.Z).Length() < reach).Take(2).ToList())
        {
            int got = rock.Mine(2.5f);
            if (got > 0) { State.Backpack.Add(rock.Item, got); VehicleScooped += got; }
            if (rock.Stones <= 0) { World.RemoveRock(rock); rock.QueueFree(); }
        }
    }

    /// <summary>Bicerdover: olgun ekin (tarla) ve tutamlar (Lif, Bugday) onunden toplanir, cantaya girer.</summary>
    void WorkHarvest(VehicleNode v)
    {
        var s = v.State; var d = v.Def;
        int n = 0;
        foreach (var plot in State.Farm.Plots.Where(p => p.Planted && p.Stage == CropStage.Mature && !p.Wild).ToList())
        {
            var c = FarmView.CellCenter(plot.X, plot.Y);
            if (!Kabarma.Sim.Vehicles.InHarvestCone(d, s, c.X, c.Z)) continue;
            var h = State.Farm.HarvestFull(plot.X, plot.Y, State.Rng);
            if (h == null) continue;
            n++;
            FarmView.OnHarvestedBy(h, c);
            State.Backpack.Add(h.Item, h.Yield, h.Stars);
            State.Backpack.Add(h.SeedItem, h.Seeds);
        }
        foreach (var t in World.Tufts.Where(t => t.Left > 0 && Kabarma.Sim.Vehicles.InHarvestCone(d, s, t.Position.X, t.Position.Z)).Take(8).ToList())
        {
            int got = World.TakeTuft(t, t.PerHit);
            if (got > 0) { State.Backpack.Add(t.Item, got); n++; }
        }
        if (n > 0) { VehicleHarvested += n; OnHarvest(n); }
    }

    /// <summary>Tampon (Darbe) ve Bicerdover (Kesme): onundeki yaratiga hizli carpinca hasar; ayni yaratiga yarim saniyede bir.</summary>
    void Ram(VehicleNode v, float dt)
    {
        var s = v.State; var d = v.Def;
        float dmg = Kabarma.Sim.Vehicles.RamDamage(s, v.Mods, Data.Vehicles.Rules);
        bool reaper = d.HarvestRadius > 0 && s.Riders.Count > 0 && Mathf.Abs(s.Speed) > 0.5f;
        if (dmg <= 0 && !reaper) return;
        var fwd = new Vector3(Mathf.Sin(s.Yaw), 0, Mathf.Cos(s.Yaw));
        var head = v.GlobalPosition + fwd * (VehicleBodies.Wheelbase(d) * 0.5f + (reaper ? d.HarvestRadius * 0.6f : 0.8f)) + Vector3.Up * 0.6f;
        float reach = reaper ? d.HarvestRadius : 1.6f;
        double now = Time.GetTicksMsec() / 1000.0;
        foreach (var node in GetTree().GetNodesInGroup(NetSync.CreatureGroup))
        {
            if (node is not ICombatTarget t || !t.Alive || t.Center.DistanceTo(head) > reach + t.Radius) continue;
            ulong key = node.GetInstanceId();
            if (_rammed.TryGetValue(key, out var last) && now - last < 0.5) continue;
            _rammed[key] = now;
            float amount = reaper ? 18f : dmg;
            t.TakeHit(new CombatHit(amount, reaper ? DamageType.Kesme : DamageType.Darbe, CombatTeam.Player) { Knock = reaper ? 0f : 4f }, v.GlobalPosition);
            VehicleRams++;
            if (dmg > 0) Fx.Burst(t.Center, Palette.Warning, 8, 3f, 0.06f, 0.4f);
        }
    }

    public int VehicleRams;
}
