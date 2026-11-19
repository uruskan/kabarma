using System.Numerics;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Savunma yapilari (GDD §16.4 M3, ICERIK §13): kule hedefi, nisan, cephane, asinma, onarim; dort tuzak; kayit.</summary>
public class DefenseTests
{
    static readonly GameData D = TestData.Load();
    static TowerDef Tw(string id) => D.Defense.Tower(id)!;
    static TrapDef Tp(string id) => D.Defense.Trap(id)!;
    static DefenseTarget T(int id, float x, float z, float hp = 30, bool fly = false, bool thief = false, bool player = false, float vx = 0, float vz = 0) =>
        new(id, new Vector3(x, 0, z), new Vector3(vx, 0, vz), 0.4f, hp, 30, fly, thief, player);

    [Fact]
    public void Data_has_m3_towers_and_traps_with_recipes_placeables_and_known_ammo()
    {
        var towers = new[] { "civi_kulesi", "sapan_kulesi", "sacma_kulesi" };
        var traps = new[] { "diken_tarlasi", "yapiskan_zemin", "firlatma_pedi", "cukur_kapagi" };
        Assert.Equal(towers, D.Defense.Towers.Select(t => t.Id).Take(towers.Length)); // M4: 5 kule daha (KaleTests)
        Assert.Equal(traps, D.Defense.Traps.Select(t => t.Id).Take(traps.Length));
        var items = D.Tool.Recipes.Select(r => r.Id).ToHashSet();
        foreach (var id in towers.Concat(traps))
        {
            Assert.Contains(id, items);
            Assert.Contains(D.Tool.Placeables, p => p.Id == id);
        }
        Assert.Contains("civi", items);
        Assert.Contains("sacma", items);
        Assert.Equal("off", D.Defense.Priorities.Last());
    }

    [Fact]
    public void Priority_picks_nearest_strongest_flying_thief_and_falls_back_to_nearest()
    {
        var d = Tw("civi_kulesi");
        var ts = new List<DefenseTarget> { T(1, 3, 0, hp: 10), T(2, 6, 0, hp: 50), T(3, 8, 0, fly: true), T(4, 9, 0, thief: true), T(5, 40, 0, hp: 500), T(6, 1, 0, player: true) };
        Assert.Equal(0, TowerRules.Pick(d, Vector3.Zero, ts, "nearest"));
        Assert.Equal(1, TowerRules.Pick(d, Vector3.Zero, ts, "strongest")); // 500'luk menzil disinda
        Assert.Equal(2, TowerRules.Pick(d, Vector3.Zero, ts, "flying"));
        Assert.Equal(3, TowerRules.Pick(d, Vector3.Zero, ts, "thief"));
        Assert.Equal(-1, TowerRules.Pick(d, Vector3.Zero, ts, "off"));
        var noFly = ts.Where(t => !t.Flying).ToList();
        Assert.Equal(0, TowerRules.Pick(d, Vector3.Zero, noFly, "flying")); // aranan yok: en yakin
        // Sapan'in olu bolgesi: minRange icindeki hedefe atmaz
        Assert.Equal(1, TowerRules.Pick(Tw("sapan_kulesi"), Vector3.Zero, new[] { T(1, 2, 0), T(2, 6, 0) }, "nearest"));
    }

    [Fact]
    public void Lead_hits_a_moving_target_and_arc_lands_on_the_aim_point()
    {
        var d = Tw("civi_kulesi");
        var from = new Vector3(0, 2, 0);
        var pos = new Vector3(8, 0.4f, 0); var vel = new Vector3(0, 0, 4);
        var aim = TowerRules.Lead(from, pos, vel, d.Speed);
        float t = Vector3.Distance(from, aim) / d.Speed;
        Assert.True(Vector3.Distance(pos + vel * t, aim) < 0.05f);

        var s = Tw("sapan_kulesi");
        var to = new Vector3(12, 0.3f, -5);
        var v0 = TowerRules.ArcVelocity(from, to, s.FlightSeconds, s.Gravity);
        Assert.True(Vector3.Distance(TowerRules.ArcAt(from, v0, s.Gravity, s.FlightSeconds), to) < 1e-3f);
        Assert.True(v0.Y > 0, "kavisle yukari atar");
        var hit = TowerRules.Splash(to, s.Splash, new[] { T(1, 12, -5), T(2, 13.5f, -5), T(3, 18, -5) });
        Assert.Equal(new[] { 0, 1 }, hit);
    }

    [Fact]
    public void Cone_spreads_pellets_and_near_targets_take_more()
    {
        var d = Tw("sacma_kulesi");
        var ts = new[] { T(1, 0, -2.5f), T(2, 0, -7), T(3, 6, -1) };
        var hits = TowerRules.ConeHits(d, Vector3.Zero, -Vector3.UnitZ, ts);
        Assert.True(hits.GetValueOrDefault(0) >= 3, $"yakindaki hedef {hits.GetValueOrDefault(0)} tane");
        Assert.False(hits.ContainsKey(2), "koni disindaki hedef");
        Assert.Equal(d.Pellets, TowerRules.ConeDirs(-Vector3.UnitZ, d.Pellets, d.ConeDegrees).Count);
        Assert.True(hits.Values.Sum() <= d.Pellets);
    }

    [Fact]
    public void Ammo_loads_from_bag_fires_and_tower_stops_when_empty_or_worn()
    {
        var d = Tw("civi_kulesi");
        var t = new DefenseState().AddTower(d, Vector3.Zero, 0);
        var bag = new Inventory(20, D.Tool.StackOf);
        bag.Add("civi", 100);
        Assert.Equal(d.Magazine, TowerRules.Load(t, d, bag));
        Assert.Equal(100 - d.Magazine, bag.Count("civi"));
        int shots = 0;
        while (TowerRules.Tick(t, d.Interval) && TowerRules.Fire(t, d) == "civi") shots++;
        Assert.Equal(d.Magazine, shots);
        Assert.Equal(TowerStatus.Empty, TowerRules.Status(t));
        Assert.Equal(d.Hp - d.Magazine * d.WearPerShot, t.Hp, 3);

        t.Hp = 0.1f; TowerRules.Load(t, d, bag);
        Assert.True(TowerRules.Tick(t, 1f));
        Assert.Equal("civi", TowerRules.Fire(t, d));
        Assert.Equal(TowerStatus.Broken, TowerRules.Status(t));
        Assert.False(TowerRules.Tick(t, 1f));
        Assert.Null(TowerRules.Fire(t, d));

        // Onarim: tarifin %25'i eksik can oraninda; can tamsa bedel yok.
        var recipe = D.Tool.Recipes.Single(r => r.Id == "civi_kulesi").In;
        var full = TowerRules.RepairCost(recipe, 0, d.Hp, D.Defense.RepairFraction);
        Assert.Equal(5, full["wood"]); // 20 * 0.25
        Assert.Empty(TowerRules.RepairCost(recipe, d.Hp, d.Hp, D.Defense.RepairFraction));
        Assert.Equal("strongest", TowerRules.NextPriority(D.Defense, "nearest"));
        Assert.Equal("nearest", TowerRules.NextPriority(D.Defense, "off"));
    }

    [Fact]
    public void Sapan_takes_stone_or_produce_in_listed_order()
    {
        var d = Tw("sapan_kulesi");
        var t = new DefenseState().AddTower(d, Vector3.Zero, 0);
        var bag = new Inventory(20, D.Tool.StackOf);
        bag.Add("turnip", 5); bag.Add("stone", 3);
        Assert.Equal(8, TowerRules.Load(t, d, bag));
        Assert.True(TowerRules.Tick(t, 5f));
        Assert.Equal("stone", TowerRules.Fire(t, d));
    }

    [Fact]
    public void Spikes_hurt_and_slow_on_their_beat()
    {
        var d = Tp("diken_tarlasi");
        var s = new DefenseState().AddTrap(d, Vector3.Zero, 0.7f);
        var ts = new[] { T(1, 0.3f, 0.2f), T(2, 3, 0), T(9, 0, 0, player: true) };
        var fx = TrapRules.Step(s, d, ts, 0.016f);
        Assert.Contains(fx, e => e.Target == 1 && e.Kind == TrapFx.Damage && Math.Abs(e.Value - d.Dps * d.TriggerEvery) < 1e-4f);
        Assert.Contains(fx, e => e.Target == 1 && e.Kind == TrapFx.Slow && e.Value == d.Slow);
        Assert.DoesNotContain(fx, e => e.Target == 2 || e.Target == 9);
        Assert.Empty(TrapRules.Step(s, d, ts, 0.1f)); // vurus araligi dolmadi
        Assert.NotEmpty(TrapRules.Step(s, d, ts, d.TriggerEvery));
        Assert.Equal(2, s.Triggers);
    }

    [Fact]
    public void Sticky_holds_once_then_immune_for_a_while()
    {
        var d = Tp("yapiskan_zemin");
        var s = new DefenseState().AddTrap(d, Vector3.Zero, 0);
        var ts = new[] { T(1, 0, 0) };
        Assert.Contains(TrapRules.Step(s, d, ts, 0.016f), e => e.Kind == TrapFx.Hold && e.Seconds == d.HoldSeconds);
        Assert.Empty(TrapRules.Step(s, d, ts, d.HoldSeconds));
        Assert.Contains(TrapRules.Step(s, d, ts, d.ImmuneSeconds + 0.01f), e => e.Kind == TrapFx.Hold);
    }

    [Fact]
    public void Launch_pad_throws_enemies_and_players_forward_then_rearms()
    {
        var d = Tp("firlatma_pedi");
        float yaw = MathF.PI / 2; // ileri = -X
        var s = new DefenseState().AddTrap(d, Vector3.Zero, yaw);
        var ts = new[] { T(1, 0.2f, 0), T(9, -0.3f, 0.1f, player: true) };
        var fx = TrapRules.Step(s, d, ts, 0.016f);
        Assert.Equal(2, fx.Count(e => e.Kind == TrapFx.Launch));
        var e1 = fx.Single(e => e.Target == 1);
        Assert.True(e1.Dir.X < -0.99f, $"yon {e1.Dir}");
        Assert.Equal(d.Damage, e1.Value);
        Assert.Equal(0, fx.Single(e => e.Target == 9).Value); // oyuncuya hasar yok
        Assert.Empty(TrapRules.Step(s, d, ts, d.RearmSeconds * 0.5f));
        Assert.NotEmpty(TrapRules.Step(s, d, ts, d.RearmSeconds));
    }

    [Fact]
    public void Pit_takes_up_to_capacity_while_open_holds_them_and_releases_on_close()
    {
        var d = Tp("cukur_kapagi");
        var s = new DefenseState().AddTrap(d, Vector3.Zero, 0);
        var ts = Enumerable.Range(1, d.Capacity + 2).Select(i => T(i, 0.1f * i, 0)).Append(T(99, 0, 0, player: true)).ToList();
        Assert.Empty(TrapRules.Step(s, d, ts, 0.016f)); // kapali: kimse dusmez
        TrapRules.Toggle(s);
        var fx = TrapRules.Step(s, d, ts, 0.016f);
        Assert.Equal(d.Capacity, fx.Count(e => e.Kind == TrapFx.Fall));
        Assert.DoesNotContain(fx, e => e.Target == 99);
        Assert.Equal(d.Capacity, TrapRules.Step(s, d, ts, 0.5f).Count(e => e.Kind == TrapFx.Hold));
        // Olen yer acar
        var fewer = ts.Where(t => t.Id != 1).ToList();
        Assert.Contains(TrapRules.Step(s, d, fewer, 0.1f), e => e.Kind == TrapFx.Fall);
        TrapRules.Toggle(s);
        var rel = TrapRules.Step(s, d, fewer, 0.1f);
        Assert.Equal(d.Capacity, rel.Count(e => e.Kind == TrapFx.Release));
        Assert.Empty(s.Held);
    }

    [Fact]
    public void Worn_out_trap_stops_and_defense_section_round_trips()
    {
        var d = Tp("diken_tarlasi");
        var a = new GameState(D);
        var s = a.Defense.AddTrap(d, new Vector3(1, 2, 3), 0.5f);
        s.Hp = 0;
        Assert.Empty(TrapRules.Step(s, d, new[] { T(1, 1, 3) }, 1f));
        var tw = a.Defense.AddTower(Tw("sapan_kulesi"), new Vector3(4, 0, 4), 1f);
        tw.Mag["stone"] = 7; tw.Priority = "flying";
        var pit = a.Defense.AddTrap(Tp("cukur_kapagi"), new Vector3(-3, 0, 2), 0);
        pit.Open = true; pit.Held.Add(42);

        var reg = new SaveRegistry().AddRange(a.SaveSections());
        var save = SaveCodec.Decode(SaveCodec.Encode(reg.Capture(SaveScope.World, a.Manifest("savunma"))));
        var b = new GameState(D);
        var before = b.Defense;
        Assert.Empty(new SaveRegistry().AddRange(b.SaveSections()).Restore(save).Missing);
        Assert.Same(before, b.Defense);
        Assert.Equal(7, b.Defense.Tower(tw.Id)!.Mag["stone"]);
        Assert.Equal("flying", b.Defense.Tower(tw.Id)!.Priority);
        Assert.True(b.Defense.Trap(pit.Id)!.Open);
        Assert.Equal(new[] { 42 }, b.Defense.Trap(pit.Id)!.Held);
        Assert.Equal(tw.Id, b.Defense.FindAt("sapan_kulesi", new Vector3(4.1f, 0, 4)));
        Assert.Equal(a.Defense.NextId, b.Defense.NextId);
    }
}
