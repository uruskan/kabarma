using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: yabani yaratiklar (yaratik2, ICERIK §3.8). Wildlife dugumu ve gorunus sinamasi:
///   --creatureview=wild [--species=a,b] [--clip=yuva]   her yabani tur normal ve kabarmis esiyle yan yana, sabit kamera.
///   --creatureview=wildday [--species=id]                 turun gercek surusu oyuncunun 14 m onunde; kamera surude.
/// </summary>
public partial class Director
{
    public Wildlife Wildlife { get; private set; }

    void InitWildlife()
    {
        Wildlife = new Wildlife(this) { Name = "Wildlife" };
        AddChild(Wildlife);
        Save.Add(SaveSection.Json("wildlife", 1, Wildlife.Capture, Wildlife.Apply, SaveOrder.Spawn)); // kayit sozlesmesi (Wildlife.Save.cs)
        if (Main.Args.TryGetValue("creatureview", out var v) && v is "wild" or "normal" or "swollen" or "both" or "kick")
            Wildlife.ProcessMode = ProcessModeEnum.Disabled;
    }

    List<CreatureDef> WildSpecies()
    {
        var all = Data.Creatures.Creatures.Where(c => (c.Wild != null && c.Rig != null) || c.Swarm != null).ToList();
        if (Main.Args.TryGetValue("species", out var s)) { var want = s.Split(','); all = all.Where(c => want.Contains(c.Id)).ToList(); }
        return all;
    }

    /// <summary>Her yabani tur normal ve kabarmis yan yana (kabarmis arkada). Sutun araligi turun boyuna gore.</summary>
    void WildLineup(Camera3D cam, string clip)
    {
        var center = World.Ground(PodPos + new Vector3(-12f, 0, 14f));
        var species = WildSpecies();
        // --row=normal (varsayilan) | swollen | both: kabarmis sira ayri goruntude (boylari ust uste binmesin).
        string row = Main.Args.TryGetValue("row", out var rr) ? rr : "normal";
        bool swollenOnly = row == "swollen";
        var rows = row switch { "swollen" => new[] { true }, "both" => new[] { false, true }, _ => new[] { false } };
        float x = 0;
        var spots = new List<(CreatureDef def, float x, float w)>();
        foreach (var def in species)
        {
            float h = (def.Rig?.Height ?? 0.8f) * (swollenOnly ? def.SwollenScale * 0.8f : 1f);
            float wid = Mathf.Max(1.4f, h * 1.6f);
            spots.Add((def, x + wid * 0.5f, wid));
            x += wid + 0.4f;
        }
        float half = x * 0.5f;
        var camPos = center + new Vector3(0, Mathf.Max(2.2f, half * 0.35f), Mathf.Max(5f, half * 1.35f));
        foreach (var (def, sx, _) in spots)
        {
            foreach (bool sw in rows)
            {
                var p = center + new Vector3(sx - half, 0, sw && !swollenOnly ? -4.5f : 0f);
                var ns = Wildlife.Spawn(def, p, display: true);
                if (ns.Count == 0) continue;
                // Sergide tur basina bir tane: surunun digerleri kalkar.
                foreach (var extra in ns.Skip(1)) { if (extra is WildCreature ec) Wildlife.Creatures.Remove(ec); extra.QueueFree(); }
                var n = ns[0];
                n.GlobalPosition = World.Ground(p);
                n.RotationDegrees = new Vector3(0, Mathf.RadToDeg(Mathf.Atan2(camPos.X - p.X, camPos.Z - p.Z)) - 25f, 0);
                if (n is WildCreature c) { c.DisplaySlot = clip; if (sw) c.SnapSwell(true); }
                if (n is WildSwarm s && sw) s.SnapSwell(true);
                n.ProcessMode = ProcessModeEnum.Always;
            }
        }
        cam.GlobalPosition = camPos;
        cam.LookAt(center + new Vector3(0, 0.9f, -1.5f));
        cam.MakeCurrent();
        GD.Print($"CREATUREVIEW wild species={string.Join(",", species.Select(s => s.Id))} clip={clip}");
    }

    /// <summary>Turun gercek surusu oyuncunun onunde (davranis goruntusu): kamera surunun merkezine yandan bakar.</summary>
    void WildDay(Camera3D cam)
    {
        var def = WildSpecies().FirstOrDefault();
        if (def == null) return;
        var pp = Player.GlobalPosition;
        var at = World.Ground(pp + new Vector3(0, 0, 14f));
        var members = Wildlife.Spawn(def, at, count: 3);
        void Follow()
        {
            if (!IsInstanceValid(cam)) return;
            var live = members.Where(IsInstanceValid).ToList();
            var c = live.Count > 0 ? live.Aggregate(Vector3.Zero, (s, n) => s + n.GlobalPosition) / live.Count : at;
            cam.GlobalPosition = c + new Vector3(7f, 3.2f, 4f);
            cam.LookAt(c + Vector3.Up * 0.6f);
        }
        Follow();
        GetTree().ProcessFrame += Follow;
        cam.MakeCurrent();
        GD.Print($"CREATUREVIEW wildday {def.Id} members={members.Count}");
    }
}
