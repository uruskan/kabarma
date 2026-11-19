using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Bereket Holding (GDD §19, §19.4.1; ICERIK §3.7; holding akisi M3).</summary>
public class HoldingTests
{
    static GameData Data() => TestData.Load();

    static HoldingSystem Founded(GameData d, out HoldingCamp camp, int day = 3)
    {
        var h = new HoldingSystem(d);
        h.OnKabarmaEnded(true, day - 1);
        h.OnMorning(day, 2);
        camp = h.FirstCamp!;
        return h;
    }

    [Fact]
    public void Data_has_five_units_four_structures_and_a_far_anchor()
    {
        var d = Data();
        var h = d.Holding;
        Assert.Equal(new[] { "isci", "bekci", "ustabasi", "mekanik", "mudur" }, h.Units.Select(u => u.Id));
        Assert.Equal("M4", h.Unit("mudur").Milestone);
        Assert.Equal(new[] { "silo", "atolye", "ahir", "cekirdek" }, h.Camp.Structures.Select(s => s.Id));
        Assert.Single(h.Camp.Structures, s => s.Core);
        float dist = MathF.Sqrt(h.Camp.Anchor[0] * h.Camp.Anchor[0] + h.Camp.Anchor[1] * h.Camp.Anchor[1]);
        Assert.True(dist >= h.Camp.MinDistance, $"kamp Ocak'a {dist:0} m");
        Assert.True(MathF.Abs(h.Camp.Anchor[0]) < 300 && MathF.Abs(h.Camp.Anchor[1]) < 300, "kamp haritada");
        foreach (var l in h.Loot.Values) foreach (var id in l.Items.Keys) Assert.Matches("^[a-z_]+$", id);
    }

    [Fact]
    public void Camp_is_founded_the_morning_after_the_first_real_kabarma_only()
    {
        var d = Data();
        var h = new HoldingSystem(d);
        h.OnKabarmaEnded(false, 1); // Kucuk Kabarma sayilmaz
        h.OnMorning(2, 2);
        Assert.Empty(h.State.Camps);
        h.OnKabarmaEnded(true, 4);
        h.OnMorning(5, 2);
        var c = Assert.Single(h.State.Camps);
        Assert.Equal(1, c.Level);
        Assert.Equal(5, c.FoundedDay);
        Assert.Equal(d.Holding.Husumet.Start, c.Husumet);
        Assert.Equal(4, c.Structures.Count);
        h.OnKabarmaEnded(true, 8); // ikinci kamp yok (2. kamp Ocak T3)
        h.OnMorning(9, 2);
        Assert.Single(h.State.Camps);
    }

    [Theory]
    [InlineData(1, 3, 2, 0, 0)]
    [InlineData(2, 4, 3, 1, 0)]
    [InlineData(3, 5, 3, 1, 1)]
    [InlineData(4, 6, 4, 2, 1)]
    [InlineData(10, 12, 7, 5, 3)]
    public void Composition_follows_gdd_19_1(int level, int guards, int workers, int towers, int walls)
    {
        var h = new HoldingSystem(Data());
        Assert.Equal((guards, workers, towers, walls), h.Composition(level));
    }

    [Fact]
    public void Camp_levels_every_5_quiet_days_up_to_ocak_tier_times_2()
    {
        var d = Data();
        var h = Founded(d, out var c);
        var counts = new List<int>();
        for (int day = 4; day <= 40; day++) h.OnMorning(day, 2);
        Assert.Equal(4, c.Level); // M3: Ocak T2 -> tavan 4
        var (g, w, _, _) = h.Composition(4);
        Assert.Equal(g, c.Units.Count(u => u.Kind == "bekci" && u.Job == "camp"));
        Assert.Equal(w, c.Units.Count(u => u.Kind == "isci"));
        Assert.Equal(h.StructureMaxHp(d.Holding.Camp.Structures[0], 4), c.Structures["silo"]);
        h.OnMorning(41, 5); // Ocak T5: tavan 10
        for (int day = 42; day <= 80; day++) h.OnMorning(day, 5);
        Assert.Equal(10, c.Level);
        Assert.Equal(10, h.Cap(9));
    }

    [Fact]
    public void Husumet_grows_daily_plus_export_and_is_clamped()
    {
        var d = Data();
        var h = Founded(d, out var c);
        Assert.Equal(0, c.Husumet);
        h.RecordExport(2500); // 2 bin -> +2
        h.OnMorning(4, 2);
        Assert.Equal(2 + 2, c.Husumet);
        h.RecordExport(9000); // en fazla +3
        h.OnMorning(5, 2);
        Assert.Equal(4 + 2 + 3, c.Husumet);
        for (int day = 6; day <= 100; day++) h.OnMorning(day, 2);
        Assert.Equal(100, c.Husumet);
    }

    [Fact]
    public void Perde_blocks_damage_by_day_and_drops_on_a_real_kabarma_night()
    {
        var d = Data();
        var h = Founded(d, out var c);
        var guard = c.Units.First(u => u.Kind == "bekci");
        Assert.True(h.DamageBlockedAt(c.X + 3, c.Z));
        Assert.False(h.DamageBlockedAt(c.X + 100, c.Z));
        Assert.Equal(HoldingHit.Blocked, h.HitStructure(c.Id, "silo", 50, out _));
        Assert.Equal(0, h.HitUnit(guard.Id, 50));
        h.OnNightStart(false); // Kucuk Kabarma: Perde kalkmaz
        Assert.True(h.PerdeUp(c));
        h.OnNightStart(true);
        Assert.False(h.PerdeUp(c));
        Assert.Equal(HoldingHit.Hit, h.HitStructure(c.Id, "silo", 50, out _));
        Assert.True(h.HitUnit(guard.Id, 10) > 0);
        h.OnDawn();
        Assert.True(h.PerdeUp(c));
    }

    [Fact]
    public void Player_raid_adds_25_once_resets_level_timer_and_drops_loot()
    {
        var d = Data();
        var h = Founded(d, out var c);
        for (int day = 4; day <= 6; day++) h.OnMorning(day, 2);
        Assert.Equal(3, c.QuietDays);
        float hus = c.Husumet;
        h.OnNightStart(true);
        Assert.Equal(HoldingHit.Locked, h.HitStructure(c.Id, "cekirdek", 10, out _)); // once oteki yapilar
        Assert.Equal(hus + 25, c.Husumet);
        Assert.Equal(0, c.QuietDays);
        h.HitStructure(c.Id, "silo", 1, out _);
        Assert.Equal(hus + 25, c.Husumet); // gece basina bir kez
        HoldingLoot? loot = null;
        Assert.Equal(HoldingHit.Destroyed, h.HitStructure(c.Id, "atolye", 99999, out loot));
        Assert.Equal(2, loot!.Items["holding_parcasi"]);
        Assert.Equal(1, loot.Items["holding_plani"]);
        h.HitStructure(c.Id, "ahir", 99999, out loot);
        Assert.Equal("nadir_hayvan", loot!.Token);
        h.HitStructure(c.Id, "silo", 99999, out loot);
        Assert.Equal(d.Holding.Loot["silo"].Credits * c.Level, loot!.Credits);
    }

    [Fact]
    public void Destroying_the_core_resets_husumet_and_rebuilds_at_level_1_after_3_days()
    {
        var d = Data();
        var h = Founded(d, out var c);
        for (int day = 4; day <= 14; day++) h.OnMorning(day, 2);
        Assert.Equal(3, c.Level);
        h.OnNightStart(true);
        foreach (var s in new[] { "silo", "atolye", "ahir" }) h.HitStructure(c.Id, s, 99999, out _);
        bool core = false;
        h.CoreDestroyed += _ => core = true;
        Assert.Equal(HoldingHit.Destroyed, h.HitStructure(c.Id, "cekirdek", 99999, out var loot));
        Assert.True(core && c.Destroyed);
        Assert.Equal(0, c.Husumet);
        Assert.Empty(c.Units);
        Assert.Equal(4, loot!.Items["holding_parcasi"]);
        h.OnDawn();
        h.OnMorning(15, 2);
        h.OnMorning(16, 2);
        Assert.True(c.Destroyed);
        h.OnMorning(17, 2); // 14 + 3
        Assert.False(c.Destroyed);
        Assert.Equal(1, c.Level);
        Assert.All(c.Structures.Values, hp => Assert.True(hp > 0));
    }

    [Fact]
    public void Caravan_leaves_once_a_day_and_an_ambush_adds_15_and_drops_cargo()
    {
        var d = Data();
        var h = Founded(d, out var c);
        for (int day = 4; day <= 8; day++) h.OnMorning(day, 2); // seviye 2 -> 3 bekci
        Assert.Equal(2, c.Level);
        var k = h.DepartCaravan(c.Id, 8);
        Assert.NotNull(k);
        Assert.Null(h.DepartCaravan(c.Id, 8));
        var guards = c.Units.Where(u => u.Job == "caravan").ToList();
        Assert.Equal(3, guards.Count);
        foreach (var g in guards) { g.X = c.X + 120; g.Z = c.Z; } // Perde disinda (acik arazi)
        k!.X = c.X + 120;
        HoldingLoot? got = null;
        h.Ambushed += (_, l) => got = l;
        float hus = c.Husumet;
        foreach (var g in guards) h.HitUnit(g.Id, 9999);
        Assert.Null(got); // arac duruyor
        h.HitCaravan(c.Id, 9999);
        Assert.NotNull(got);
        Assert.Equal(hus + 15, c.Husumet);
        Assert.Equal(d.Holding.Caravan.CreditsPerLevel * 2, got!.Credits);
        Assert.Equal(d.Holding.Caravan.Cargo, got.Items);
        Assert.Equal(1, h.State.Ambushes);
        Assert.DoesNotContain(c.Units, u => u.Job == "caravan");
    }

    [Fact]
    public void Their_raid_comes_at_husumet_50_with_L_over_2_plus_2_and_entering_costs_40()
    {
        var d = Data();
        var h = Founded(d, out var c);
        for (int day = 4; day <= 12; day++) h.OnMorning(day, 2);
        Assert.True(c.Husumet < 50);
        Assert.Empty(h.OnNightStart(true));
        h.OnDawn();
        for (int day = 13; day <= 30; day++) h.OnMorning(day, 2);
        Assert.True(c.Husumet >= 50);
        var raids = h.OnNightStart(true);
        var (camp, team) = Assert.Single(raids);
        Assert.Equal(2 + c.Level / 2, team.Count);
        Assert.Equal("ustabasi", team[0].Kind);
        float hus = c.Husumet;
        h.OnRaidEntered(c.Id);
        h.OnRaidEntered(c.Id);
        Assert.Equal(hus - 40, c.Husumet);
        h.OnDawn();
        Assert.DoesNotContain(c.Units, u => u.Job == "raid");
    }

    [Fact]
    public void Armor_reduces_damage_and_dead_units_are_replaced_next_morning()
    {
        var d = Data();
        var h = Founded(d, out var c);
        h.OnNightStart(true);
        var team = h.OnNightStart(true);
        var guard = c.Units.First(u => u.Kind == "bekci" && u.Job == "camp");
        Assert.Equal(10f, h.HitUnit(guard.Id, 10), 3);
        h.HitUnit(guard.Id, 999);
        Assert.False(guard.Alive);
        h.OnDawn();
        int before = c.Units.Count(u => u.Alive && u.Job == "camp");
        h.OnMorning(4, 2);
        Assert.Equal(before + 1, c.Units.Count(u => u.Alive && u.Job == "camp"));
        Assert.True(d.Holding.Unit("ustabasi").Armor > 0);
    }

    [Fact]
    public void Long_run_200_days_stays_consistent()
    {
        var d = Data();
        var h = Founded(d, out var c);
        int raids = 0;
        for (int day = 4; day <= 203; day++)
        {
            h.RecordExport(day * 37 % 4000);
            h.OnMorning(day, 2);
            h.DepartCaravan(c.Id, day);
            h.CaravanHome(c.Id);
            if (day % 5 == 0)
            {
                raids += h.OnNightStart(true).Count;
                if (day % 20 == 0) foreach (var s in c.Structures.Keys.ToList()) h.HitStructure(c.Id, s, 99999, out _);
                h.OnRaidEntered(c.Id);
                h.OnDawn();
            }
            Assert.InRange(c.Husumet, 0, 100);
            Assert.InRange(c.Level, 1, 4);
            Assert.Equal(c.Units.Select(u => u.Id).Distinct().Count(), c.Units.Count);
            Assert.True(c.Units.Count < 40, $"gun {day}: {c.Units.Count} birim");
        }
        Assert.True(raids > 0);
        Assert.True(h.State.CoresDestroyed > 0);
    }

    [Fact]
    public void Loot_goes_to_bag_wallet_and_depot_and_state_round_trips()
    {
        var d = Data();
        var s = new GameState(d);
        s.Holding.OnKabarmaEnded(true, 2);
        s.Calendar.Start(3);
        Assert.Single(s.Holding.State.Camps);
        int credits = s.Credits;
        s.ApplyLoot(new HoldingLoot("atolye", new() { ["holding_parcasi"] = 2 }, 300, "nadir_hayvan"), s.Backpack);
        Assert.Equal(2, s.Backpack.Count("holding_parcasi"));
        Assert.Equal(credits + 300, s.Credits);
        Assert.Equal(1, s.Holding.State.Depot["nadir_hayvan"]);
        var g = new SaveRegistry().AddRange(s.SaveSections()).Capture(SaveScope.World);
        var b = new GameState(d);
        new SaveRegistry().AddRange(b.SaveSections()).Restore(g);
        var c0 = s.Holding.FirstCamp!;
        var c1 = b.Holding.FirstCamp!;
        Assert.Equal(c0.Level, c1.Level);
        Assert.Equal(c0.Units.Count, c1.Units.Count);
        Assert.Equal(c0.Structures, c1.Structures);
        Assert.Equal(1, b.Holding.State.Depot["nadir_hayvan"]);
    }

    [Fact]
    public void Cargo_sales_feed_export_husumet()
    {
        var d = Data();
        var s = new GameState(d);
        s.Sell("turnip", d.Crops.SizeClasses[3]); // 360, aninda
        Assert.Equal(360, s.Holding.State.ExportToday);
    }
}
