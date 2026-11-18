using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=creatures, yabani turler (yaratik2, ICERIK §3.8): her tur oyundaki aktoruyle (WildCreature / WildSwarm) duz bir
/// sinama dunyasinda dogar, hareket eder ve kabarir; tavrinin cekirdegi de sinanir:
///  - dolasir (20 sn'de en az 2 m), uyurken (gunduzcu gece, gececi gunduz) yerinde uyur;
///  - kabarir: sisme orani 1'e varir, govde turun boyuna buyur, Maya kesesi gorunur (suruda uye sayisi artar);
///  - kacan turler oyuncu yaklasinca uzaklasir, yirtici once uyarir sonra vurur, yagmaci ve gececi hedefini alip kacar,
///    kovan surusu kovana yaklasani sokar.
/// Kareler elle ilerletilir (Tick); gercek zaman beklenmez.
/// </summary>
public partial class CreatureTest
{
    int _wild;

    sealed class FlatWorld : IWildWorld
    {
        public Vector3 Player = new(200, 0, 200);
        public bool IsNight;
        public Vector3? Target;
        public readonly List<WildEvent> Events = new();
        public Vector3 Ground(Vector3 p) => p with { Y = 0 };
        public Vector3 PlayerPos => Player;
        public bool Night => IsNight;
        public bool FindTarget(string kind, Vector3 from, float range, out Vector3 at)
        {
            at = Target ?? default;
            return Target.HasValue && Target.Value.DistanceTo(from) <= range;
        }
        public void Report(WildCreature c, WildEvent e) => Events.Add(e);
        public void ReportSwarm(WildSwarm s, WildEvent e, bool sting) => Events.Add(e);
    }

    const float Dt = 1f / 30f;

    static void Run(System.Action<float> tick, float seconds) { for (float t = 0; t < seconds; t += Dt) tick(Dt); }

    void CheckWild(GameData data, PoseLibrary poses)
    {
        foreach (var def in data.Creatures.Creatures.Where(c => c.Wild != null || c.Swarm != null))
        {
            _wild++;
            if (def.Swarm != null) CheckSwarm(def, data);
            else CheckWildCreature(def, poses);
        }
        if (_wild < 9) _fail.Add($"yabani tur sayisi {_wild} < 9");
    }

    WildCreature NewWild(CreatureDef def, PoseLibrary poses, FlatWorld w, Vector3 at)
    {
        var herd = new WildHerdState { Id = 1, Species = def.Id, HomeX = at.X, HomeZ = at.Z, WanderX = at.X, WanderZ = at.Z, Rand = 99 };
        var c = new WildCreature(def, poses, w, herd, new WildState { Id = 2, Herd = 1, Rand = 7 }) { LeadsHerd = true, ProcessMode = ProcessModeEnum.Disabled };
        AddChild(c);
        c.GlobalPosition = at;
        return c;
    }

    void CheckWildCreature(CreatureDef def, PoseLibrary poses)
    {
        var w = def.Wild!;
        bool nightOwl = w.Active == "night";
        var notes = new List<string>();

        // Dogar ve dolasir (uyanik oldugu saatte).
        var world = new FlatWorld { IsNight = nightOwl };
        var c = NewWild(def, poses, world, new Vector3(_wild * 40f, 0, 0));
        if (!c.Loaded) { Fail(def.Id, "aktor yuklenemedi"); return; }
        Run(c.Tick, 20f);
        if (c.Travelled < 2f) Fail(def.Id, $"dolasmiyor ({c.Travelled:0.0} m)");
        notes.Add($"dolasma={c.Travelled:0}m");

        // Uyku: uyanik olmadigi saatte yerinde uyur.
        if (w.Active != "always")
        {
            world.IsNight = !nightOwl;
            Run(c.Tick, 1f);
            if (c.State.Mode != WildMode.Sleep) Fail(def.Id, $"uyumuyor (mod {c.State.Mode})");
            world.IsNight = nightOwl;
            notes.Add("uyku");
        }

        // Tavir
        switch (w.Stance)
        {
            case WildStance.Predator:
                {
                    world.Events.Clear();
                    world.Player = c.GlobalPosition + new Vector3(0, 0, 6f);
                    for (float t = 0; t < 8f && !world.Events.Contains(WildEvent.Struck); t += Dt) { world.Player = c.GlobalPosition + (world.Player - c.GlobalPosition).Normalized() * Mathf.Max(1.2f, (world.Player - c.GlobalPosition).Length()); c.Tick(Dt); }
                    int warn = world.Events.IndexOf(WildEvent.Warned), hit = world.Events.IndexOf(WildEvent.Struck);
                    if (warn < 0 || hit < 0 || hit < warn) Fail(def.Id, $"yirtici: uyari {warn}, vurus {hit}");
                    notes.Add("uyari>vurus");
                    break;
                }
            default:
                if (w.FleeRadius > 0)
                {
                    var p = c.GlobalPosition;
                    world.Player = p + new Vector3(w.FleeRadius * 0.5f, 0, 0);
                    Run(c.Tick, 1.5f);
                    float d0 = p.DistanceTo(world.Player), d1 = c.GlobalPosition.DistanceTo(world.Player);
                    if (d1 < d0 + 1.5f) Fail(def.Id, $"kacmiyor ({d0:0.0} -> {d1:0.0} m)");
                    notes.Add($"kacis={d1 - d0:0.0}m");
                    world.Player = new Vector3(500, 0, 500);
                    Run(c.Tick, w.FleeSeconds + 0.5f);
                }
                if (!string.IsNullOrEmpty(w.Target))
                {
                    world.Events.Clear();
                    world.Target = c.GlobalPosition + new Vector3(6f, 0, 0);
                    for (float t = 0; t < 40f && !world.Events.Contains(WildEvent.Took); t += Dt) c.Tick(Dt);
                    if (!world.Events.Contains(WildEvent.Took)) Fail(def.Id, $"hedef ({w.Target}) alinmadi, mod {c.State.Mode}");
                    notes.Add($"hedef={w.Target}");
                    world.Target = null;
                }
                break;
        }

        // Darbe: kacan tur suruyle kacar (darbe klibi oynar).
        if (w.FleeOnHit && w.Stance != WildStance.Predator && WildBrain.Awake(w, world.IsNight))
        {
            Run(c.Tick, w.FleeSeconds + 0.5f);
            world.Player = c.GlobalPosition + new Vector3(10f, 0, 0);
            c.Hit(1f);
            Run(c.Tick, 0.2f);
            if (c.State.Mode != WildMode.Flee) Fail(def.Id, $"vurulunca kacmiyor (mod {c.State.Mode})");
            world.Player = new Vector3(500, 0, 500);
        }

        // Kabarma: sisme shader'i, govde olcegi, Maya kesesi.
        c.Swell(true);
        Run(c.Tick, def.Swell.Seconds * 2f);
        var vis = c.GetNode<Node3D>("Visual");
        if (c.Look == null || Mathf.Abs(c.Look.Amount - 1f) > 0.03f) Fail(def.Id, $"kabarmiyor ({c.Look?.Amount:0.00})");
        if (vis.Scale.X < def.SwollenScale * 0.95f) Fail(def.Id, $"govde buyumedi ({vis.Scale.X:0.00} < {def.SwollenScale})");
        var sac = c.FindChild("MayaSac", true, false) as MeshInstance3D;
        if (c.Rig.Body != null && (sac == null || !sac.Visible)) Fail(def.Id, "Maya kesesi gorunmuyor");
        c.Swell(false);
        Run(c.Tick, def.Swell.Seconds * 2f);
        if (c.Look.Amount > 0.03f) Fail(def.Id, "sonmedi");
        notes.Add($"kabarma={def.SwollenScale}x kese={(sac != null ? "var" : "-")}");
        GD.Print($"WILD {def.Id} stance={w.Stance} active={w.Active} {string.Join(" ", notes)}");
    }

    void CheckSwarm(CreatureDef def, GameData data)
    {
        var d = def.Swarm!;
        var world = new FlatWorld();
        var s = new WildSwarm(def, data, world, 1, new Vector3(_wild * 40f, 0, 0), 12345) { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(s);
        Run(s.Tick, 20f);
        if (s.Travelled < 2f) Fail(def.Id, $"suru dolasmiyor ({s.Travelled:0.0} m)");
        int n0 = s.MembersShown;
        if (n0 != Swarms.Count(d, false)) Fail(def.Id, $"uye {n0} != {Swarms.Count(d, false)}");
        var notes = new List<string> { $"dolasma={s.Travelled:0}m uye={n0}" };

        // Hedef: Cekirge tahila, Vizviz cicege (kovan menzilinde) akar.
        world.Events.Clear();
        world.Target = new Vector3(s.State.HomeX + 5f, 0, s.State.HomeZ);
        for (float t = 0; t < 40f && !world.Events.Contains(WildEvent.Took); t += Dt) s.Tick(Dt);
        if (!world.Events.Contains(WildEvent.Took)) Fail(def.Id, $"suru hedefe ({def.Wild?.Target}) varmadi");
        world.Target = null;
        notes.Add($"hedef={def.Wild?.Target}");

        if (d.Hive)
        {
            if (s.Hive == null) Fail(def.Id, "kovan yok");
            world.Events.Clear();
            world.Player = new Vector3(s.State.HomeX + d.GuardRadius * 0.5f, 0, s.State.HomeZ);
            for (float t = 0; t < 10f && !world.Events.Contains(WildEvent.Struck); t += Dt) s.Tick(Dt);
            if (!world.Events.Contains(WildEvent.Struck)) Fail(def.Id, "kovan bekcisi sokmadi");
            world.Player = new Vector3(500, 0, 500);
            notes.Add("sokma");
        }

        // Darbe: can havuzu ve dagilma
        float hp = s.State.Hp;
        s.Hit(5f, s.GlobalPosition + Vector3.Right);
        if (s.State.Hp >= hp || s.State.ScatterT <= 0) Fail(def.Id, "darbe havuzdan dusmedi / dagilmadi");

        // Kabarma: uye sayisi ve isima
        s.Swell(true);
        Run(s.Tick, def.Swell.Seconds * 2f);
        int n1 = s.MembersShown;
        if (n1 < Swarms.Count(d, true) - 1) Fail(def.Id, $"kabarinca uye {n1} < {Swarms.Count(d, true)}");
        if (Mathf.Abs(s.Look.Amount - 1f) > 0.03f) Fail(def.Id, $"suru kabarmiyor ({s.Look.Amount:0.00})");
        notes.Add($"kabarma uye={n1}");
        GD.Print($"WILD {def.Id} swarm {string.Join(" ", notes)}");
    }
}
