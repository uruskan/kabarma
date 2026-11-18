using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Yabani yaratiklar (ICERIK §3.8, GDD §16.2 "Gunduz tavri"): veri, beyin, suru, dogma.</summary>
public class WildTests
{
    static readonly GameData D = TestData.Load();
    const float Dt = 1f / 30f;

    static readonly string[] Twelve = { "gidak", "pofuduk", "homur", "zipzip", "cekirge", "sinsi", "dalli", "hu", "vicik", "bozuk", "vizviz", "findikci" };

    [Fact]
    public void Twelve_meadow_and_grove_creatures_are_in_the_data()
    {
        foreach (var id in Twelve) Assert.Contains(D.Creatures.Creatures, c => c.Id == id);
        // Yeni dokuz tur: tavir (ya da suru) ve yerli biyom tanimli; tavirlar bilinen listede.
        foreach (var c in D.Creatures.Creatures.Where(c => c.Wild != null || c.Swarm != null))
        {
            Assert.Contains(c.Wild!.Stance, WildStance.All);
            Assert.Contains(c.Wild.Active, new[] { "day", "night", "always" });
            Assert.False(string.IsNullOrEmpty(c.Biome), c.Id);
            Assert.True(c.Speed > 0 && c.Hp > 0, c.Id);
        }
    }

    [Fact]
    public void Code_bodies_exist_map_every_plan_role_and_are_marked_placeholders()
    {
        foreach (var c in D.Creatures.Creatures.Where(c => c.Rig != null && c.Rig.Model.StartsWith("code:")))
        {
            var body = D.Body(c.Rig!.Model["code:".Length..]);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrEmpty(body!.Placeholder), $"{c.Id}: yer tutucu isareti (URETIM §3)");
            var names = body.Bones.Select(b => b.Name).ToHashSet();
            foreach (var b in body.Bones.Where(b => b.Name.EndsWith("_l"))) names.Add(b.Name[..^2] + "_r");
            var plan = D.Plan(c.Rig.Plan);
            // Plan rolleri tur basina kismi olabilir (yaratik4: bacaksiz Cizbiz/Hamurcuk, kiskacsiz Billur); PoseLibrary olmayani atlar.
            Assert.True(c.Rig.Bones.ContainsKey(plan.MoveRole), $"{c.Id}: hareket rolu {plan.MoveRole}");
            foreach (var role in c.Rig.Bones.Keys) { Assert.Contains(role, plan.Roles); Assert.True(names.Contains(c.Rig.Bones[role]), $"{c.Id}: {role} kemigi yok"); }
            foreach (var (_, bone) in c.Rig.Bones) Assert.Contains(bone, names);
            foreach (var p in body.Parts) Assert.True(names.Contains(p.Bone), $"{c.Id}: parca kemigi {p.Bone}");
            Assert.True(body.Bones.Count(b => string.IsNullOrEmpty(b.Parent)) == 1, $"{c.Id}: tek kok");
        }
        foreach (var c in D.Creatures.Creatures.Where(c => c.Swarm != null))
        {
            Assert.NotNull(D.Body(c.Id));
            if (c.Swarm!.Hive) Assert.NotNull(D.Body(c.Id + "_hive"));
        }
    }

    [Fact]
    public void Content_has_all_27_icerik_species_with_canonical_ids()
    {
        // ICERIK §3: 27 tur (PLAN §7.1); kimlikler docs/kimlikler-m4.md §3.
        string[] ids = { "zipzip", "gidak", "pofuduk", "homur", "cekirge", "sinsi", "dalli", "hu", "vicik", "bozuk", "vizviz", "findikci",
            "vrak", "kiskac", "suluk_surusu", "tukurgen", "cizbiz", "kosar", "kivrik", "kizgin", "lescci", "dehliz",
            "tosun", "ayaz_kurdu", "paytak", "billur", "hamurcuk" };
        Assert.Equal(27, D.Creatures.Creatures.Count);
        foreach (var id in ids) Assert.Contains(D.Creatures.Creatures, c => c.Id == id);
        foreach (var c in D.Creatures.Creatures.Skip(12)) Assert.True(c.Wild != null && c.Weight > 0 && !string.IsNullOrEmpty(c.Biome), c.Id);
    }
    [Fact]
    public void Every_new_species_has_a_spawn_row_in_its_home_biome()
    {
        // Dogma semasi dunya2'nin (biomes.json spawns.creatures, SpawnTable); yaratik2 satir ekler.
        foreach (var c in D.Creatures.Creatures.Where(c => c.Wild != null || c.Swarm != null))
        {
            // GECICI (yaratik4, M4): yeni biyomlar (isil_sazlik, kizil_kanyon, ayaz_yaylasi, gobek) biyom akisiyla gelir; gelince satir zorunlu.
            if (!D.Biomes.Biomes.ContainsKey(c.Biome) && c.Biome is "isil_sazlik" or "kizil_kanyon" or "ayaz_yaylasi" or "gobek") continue;
            Assert.True(D.Biomes.Biomes.ContainsKey(c.Biome), $"{c.Id}: biyom {c.Biome}");
            var row = D.Biomes.Biomes[c.Biome].Spawns.Creatures.FirstOrDefault(r => r.Creature == c.Id);
            Assert.True(row != null, $"{c.Id}: {c.Biome} dogma satiri yok");
            Assert.True(row!.Group[0] >= 1 && row.MaxGroups >= 1 && row.Respawn > 0, c.Id);
        }
    }
    static (WildHerdState h, WildState s) Spawn(CreatureDef c, float x = 0, float z = 0) =>
        (new WildHerdState { HomeX = x, HomeZ = z, WanderX = x, WanderZ = z, Rand = 11 }, new WildState { X = x, Z = z, Hp = c.Hp, Rand = 3 });

    /// <summary>Basit tumlesik adim: beyin der, yaratik gider (zemin duz).</summary>
    static WildStep Tick(CreatureDef c, WildHerdState h, WildState s, WildSense sense)
    {
        WildBrain.StepHerd(c.Wild!, h, Dt);
        var st = WildBrain.Step(c, h, s, sense, Dt);
        float dx = st.GoalX - s.X, dz = st.GoalZ - s.Z, l = MathF.Sqrt(dx * dx + dz * dz);
        if (l > 0.01f && st.Speed > 0) { float m = MathF.Min(l, st.Speed * Dt); s.X += dx / l * m; s.Z += dz / l * m; }
        return st;
    }

    [Fact]
    public void Shy_herd_watches_then_flees_together()
    {
        var c = D.Creature("dalli");
        var (h, s) = Spawn(c);
        var other = new WildState { X = 2, Z = 0, Hp = c.Hp, Rand = 9 };
        var far = new WildSense { PlayerX = 100, PlayerZ = 0 };
        Assert.NotEqual(WildMode.Flee, Tick(c, h, s, far).Mode);
        // Alarm yaricapinda: bakar
        var alert = Tick(c, h, s, new WildSense { PlayerX = c.Wild!.AlertRadius - 1, PlayerZ = 0 });
        Assert.Equal(WildMode.Alert, alert.Mode);
        Assert.True(alert.Face);
        // Kacis yaricapinda: kacar, suru arkadasi da (ortak sayac)
        var flee = Tick(c, h, s, new WildSense { PlayerX = c.Wild.FleeRadius - 1, PlayerZ = 0 });
        Assert.Equal(WildMode.Flee, flee.Mode);
        Assert.Equal(WildEvent.Fled, flee.Event);
        Assert.True(flee.GoalX < s.X, "oyuncudan uzaga");
        Assert.Equal(WildMode.Flee, Tick(c, h, other, far).Mode);
        // Sure dolunca sakinlesir
        for (float t = 0; t < c.Wild.FleeSeconds + 1; t += Dt) Tick(c, h, s, far);
        Assert.NotEqual(WildMode.Flee, Tick(c, h, s, far).Mode);
    }

    [Fact]
    public void Predator_warns_before_it_strikes_and_gives_up_far_away()
    {
        var c = D.Creature("bozuk");
        var w = c.Wild!;
        var (h, s) = Spawn(c);
        var events = new List<WildEvent>();
        // Oyuncu 1 m'de duruyor: once uyari (warnSeconds), sonra vurus.
        for (float t = 0; t < w.WarnSeconds + w.AttackInterval + 1; t += Dt)
        {
            var st = Tick(c, h, s, new WildSense { PlayerX = s.X + 1f, PlayerZ = s.Z });
            if (st.Event != WildEvent.None) events.Add(st.Event);
        }
        int warn = events.IndexOf(WildEvent.Warned), strike = events.IndexOf(WildEvent.Struck);
        Assert.True(warn >= 0 && strike > warn, string.Join(",", events));
        // Hucum: hucum yaricapindaki oyuncuya kosar
        var (h2, s2) = Spawn(c);
        s2.Warned = true;
        var charge = Tick(c, h2, s2, new WildSense { PlayerX = w.ChargeRadius - 1, PlayerZ = 0 });
        Assert.Equal(WildMode.Charge, charge.Mode);
        Assert.Equal(c.Speed * w.RunSpeedMult, charge.Speed, 3);
        // Cok uzakta birakir
        Tick(c, h2, s2, new WildSense { PlayerX = w.GiveUpRadius + 5, PlayerZ = 0 });
        Assert.False(s2.Warned);
    }

    [Fact]
    public void Raider_goes_to_the_crop_stays_and_takes_then_flees()
    {
        var c = D.Creature("homur");
        var w = c.Wild!;
        var (h, s) = Spawn(c);
        var sense = new WildSense { PlayerX = 200, PlayerZ = 200, HasTarget = true, TargetX = 10, TargetZ = 0 };
        Assert.Equal(WildMode.Approach, Tick(c, h, s, sense).Mode);
        WildEvent last = WildEvent.None;
        float t = 0;
        for (; t < 30 && last != WildEvent.Took; t += Dt) last = Tick(c, h, s, sense).Event;
        Assert.Equal(WildEvent.Took, last);
        Assert.True(MathF.Abs(s.X - (10 - w.TargetStop)) < 0.5f, $"x={s.X}");
        Assert.True(h.FleeT > 0, "aldiktan sonra kacar");
    }

    [Fact]
    public void Nocturnal_sleeps_by_day_and_wakes_when_hit()
    {
        var c = D.Creature("hu");
        var (h, s) = Spawn(c);
        var day = new WildSense { PlayerX = 50, PlayerZ = 0, Night = false };
        var st = Tick(c, h, s, day);
        Assert.Equal(WildMode.Sleep, st.Mode);
        Assert.Equal(WildEvent.Slept, st.Event);
        Assert.Equal("rest", st.Clip);
        Assert.Equal(WildMode.Sleep, Tick(c, h, s, day).Mode);
        Assert.Equal(WildMode.Flee, Tick(c, h, s, day with { Hit = true }).Mode);
        // Gece uyanik
        var (h2, s2) = Spawn(c);
        Assert.NotEqual(WildMode.Sleep, Tick(c, h2, s2, day with { Night = true }).Mode);
        Assert.True(WildBrain.Awake(D.Creature("vicik").Wild!, true) && WildBrain.Awake(D.Creature("vicik").Wild!, false));
        Assert.False(WildBrain.Present(kabarmaNight: true));
    }

    [Fact]
    public void Calm_grazes_and_wanders_inside_its_radius()
    {
        var c = D.Creature("vicik");
        var (h, s) = Spawn(c, 5, 5);
        var modes = new HashSet<WildMode>();
        float maxD = 0;
        for (float t = 0; t < 120; t += Dt)
        {
            modes.Add(Tick(c, h, s, new WildSense { PlayerX = 200, PlayerZ = 200 }).Mode);
            maxD = MathF.Max(maxD, MathF.Sqrt((s.X - 5) * (s.X - 5) + (s.Z - 5) * (s.Z - 5)));
        }
        Assert.Contains(WildMode.Walk, modes);
        Assert.Contains(WildMode.Graze, modes);
        Assert.True(maxD <= c.Wild!.WanderRadius + 2f, $"{maxD}");
        // Calm: yaklasinca kacmaz (fleeRadius 0), vurulunca kacar
        Assert.NotEqual(WildMode.Flee, Tick(c, h, s, new WildSense { PlayerX = s.X + 0.5f, PlayerZ = s.Z }).Mode);
        Assert.Equal(WildMode.Flee, Tick(c, h, s, new WildSense { PlayerX = s.X + 0.5f, PlayerZ = s.Z, Hit = true }).Mode);
    }

    [Fact]
    public void Same_seed_same_brain_and_state_round_trips_as_json()
    {
        var c = D.Creature("findikci");
        WildState Run()
        {
            var (h, s) = Spawn(c);
            for (float t = 0; t < 30; t += Dt) Tick(c, h, s, new WildSense { PlayerX = 30, PlayerZ = 0, HasTarget = t > 10, TargetX = 4, TargetZ = 2 });
            return s;
        }
        var a = Run(); var b = Run();
        Assert.Equal(a.X, b.X); Assert.Equal(a.Z, b.Z); Assert.Equal(a.Mode, b.Mode);
        // Kayda hazir (TEKNIK §9): duz durum JSON'a gider ve geri gelir.
        var json = JsonSerializer.Serialize(a);
        var back = JsonSerializer.Deserialize<WildState>(json)!;
        Assert.Equal(a.X, back.X); Assert.Equal(a.Rand, back.Rand); Assert.Equal(a.Mode, back.Mode);
        var sw = new SwarmState(); Swarms.Init(D.Creature("cekirge"), sw, 1, 2, 77);
        var sback = JsonSerializer.Deserialize<SwarmState>(JsonSerializer.Serialize(sw))!;
        Assert.Equal(sw.Seed, sback.Seed); Assert.Equal(sw.Hp, sback.Hp);
    }

    [Fact]
    public void Swarm_members_come_from_the_seed_and_damage_scatters_the_pool()
    {
        var c = D.Creature("cekirge");
        var d = c.Swarm!;
        var a = new SwarmState(); Swarms.Init(c, a, 0, 0, 42);
        var b = new SwarmState(); Swarms.Init(c, b, 0, 0, 42);
        for (int i = 0; i < Swarms.Count(d, false); i++)
        {
            var p = Swarms.Member(d, a, i, 3.3f);
            Assert.Equal(p, Swarms.Member(d, b, i, 3.3f));
            Assert.True(new System.Numerics.Vector2(p.X, p.Z).Length() <= d.Radius + 0.01f);
            Assert.True(p.Y > 0);
        }
        Assert.True(Swarms.Count(d, true) > Swarms.Count(d, false));
        Assert.False(Swarms.Damage(c, a, d.HpPool * 0.5f, 5, 0));
        Assert.True(a.ScatterT > 0 && Swarms.Radius(d, a) > d.Radius);
        var st = Swarms.Step(c, a, new SwarmSense { PlayerX = 100, PlayerZ = 100 }, Dt);
        Assert.Equal(WildMode.Flee, st.Mode);
        Assert.True(Swarms.Damage(c, a, d.HpPool, 5, 0), "havuz biter");
    }

    [Fact]
    public void Swarm_flows_to_grain_and_hive_guards_sting()
    {
        var c = D.Creature("cekirge");
        var s = new SwarmState(); Swarms.Init(c, s, 0, 0, 9);
        var sense = new SwarmSense { PlayerX = 100, PlayerZ = 100, HasTarget = true, TargetX = 6, TargetZ = 0 };
        SwarmStep st = default;
        for (float t = 0; t < 30 && st.Event != WildEvent.Took; t += Dt) st = Swarms.Step(c, s, sense, Dt);
        Assert.Equal(WildEvent.Took, st.Event);

        var v = D.Creature("vizviz");
        var hive = new SwarmState(); Swarms.Init(v, hive, 0, 0, 3);
        bool stung = false;
        for (float t = 0; t < 8 && !stung; t += Dt) stung = Swarms.Step(v, hive, new SwarmSense { PlayerX = 1.5f, PlayerZ = 0 }, Dt).Sting;
        Assert.True(stung);
        // Gece kovanina doner
        var night = Swarms.Step(v, hive, new SwarmSense { PlayerX = 50, PlayerZ = 50, Night = true }, Dt);
        Assert.Equal(WildMode.Sleep, night.Mode);
    }

    [Fact]
    public void Wildlife_save_section_round_trips()
    {
        var c = D.Creature("dalli");
        var (h, s) = Spawn(c, 3, 4);
        for (float t = 0; t < 5; t += Dt) Tick(c, h, s, new WildSense { PlayerX = 100, PlayerZ = 0 });
        var sw = new SwarmState(); Swarms.Init(D.Creature("vizviz"), sw, 7, 8, 5);
        var save = new WildlifeSave { NextId = 12, Rand = 99, Respawn = { ["sis_korusu:dalli"] = 40f } };
        save.Groups.Add(new WildGroupSave { Biome = "sis_korusu", Creature = "dalli", Herd = h, Members = { new WildMemberSave { State = s, Y = 1.5f, Yaw = 0.3f } } });
        save.Groups.Add(new WildGroupSave { Biome = "sis_korusu", Creature = "vizviz", Swarm = sw });
        var json = JsonSerializer.Serialize(save);
        var back = JsonSerializer.Deserialize<WildlifeSave>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back));
        Assert.Equal(s.X, back.Groups[0].Members[0].State.X);
        Assert.Equal(h.WanderX, back.Groups[0].Herd!.WanderX);
        Assert.Equal(sw.Seed, back.Groups[1].Swarm!.Seed);
    }
}