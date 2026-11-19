using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>GDD 22.2 Cekisme dunyasi: on kuralin her biri bir senaryo testi (M5 cekisme).</summary>
public class RivalryTests
{
    static GameData D() => TestData.Load();

    /// <summary>Iki takim: kuzey ve guney, 600 m arayla, 60 m tapu.</summary>
    static RivalryState Two(GameData d, bool dedicated = false)
    {
        var s = RivalryRules.New(d.Rivalry, dedicated, 1);
        RivalryRules.AddTeam(d.Rivalry, s, "kuzey", "Kuzey", 0, 0, 60, "p0", "p1");
        RivalryRules.AddTeam(d.Rivalry, s, "guney", "Guney", 0, 600, 60, "p2", "p3");
        return s;
    }

    static StructureGraph Graph(GameData d) { var g = new StructureGraph(); g.Bind(d.Build); return g; }

    static int Wall(StructureGraph g, float x, float z, string shape = "kare_temel", string mat = "wood")
        => g.Add(new StructurePiece { Shape = shape, Units = 1, CenterY = 0.5f, Grounded = true, Material = mat, Hp = g.Rules.Material(mat)!.Hp, Pos = new[] { x, 0f, z } });

    // 1. Takim sayisi, oyuncu ve Ocak araligi
    [Fact]
    public void Rule1_teams_players_and_ocak_distance()
    {
        var d = D();
        Assert.Null(RivalryRules.ValidateSetup(d.Rivalry, Two(d)));
        var close = Two(d);
        close.Teams[1].OcakZ = 300;
        Assert.Contains("500", RivalryRules.ValidateSetup(d.Rivalry, close));
        var solo = Two(d); solo.Teams.RemoveAt(1);
        Assert.NotNull(RivalryRules.ValidateSetup(d.Rivalry, solo));
        var big = Two(d); big.Teams[0].Players = new() { "a", "b", "c", "d", "e" };
        Assert.NotNull(RivalryRules.ValidateSetup(d.Rivalry, big));
        var dup = Two(d); dup.Teams[1].Players = new() { "p0" };
        Assert.Equal("bir oyuncu iki takimda", RivalryRules.ValidateSetup(d.Rivalry, dup));
    }

    // 2. Gunduz tapu dokunulmaz (oyuncu, hayvan, yapi); gece olmaz
    [Fact]
    public void Rule2_day_invulnerability_in_plot_and_night_lifts_it()
    {
        var d = D(); var s = Two(d);
        Assert.Equal(RivalryVerdict.DayProtected, RivalryRules.CanHit(d.Rivalry, s, false, 10, 10, "guney", targetIsPlayer: true));
        Assert.Equal(RivalryVerdict.DayProtected, RivalryRules.CanHit(d.Rivalry, s, false, 10, 10, "guney", "duvar"));
        Assert.Equal(RivalryVerdict.Allowed, RivalryRules.CanHit(d.Rivalry, s, true, 10, 10, "guney", "duvar"));
        // Kendi takimi vurulmaz.
        Assert.Equal(RivalryVerdict.Friendly, RivalryRules.CanHit(d.Rivalry, s, true, 10, 10, "kuzey"));
        // Cekisme degilse karar yok.
        s.Enabled = false;
        Assert.Equal(RivalryVerdict.NotRivalry, RivalryRules.CanHit(d.Rivalry, s, false, 10, 10, "guney"));
    }

    // 3. Acik arazi PvP: varsayilan kapali; acikken olduren 3 yigin alir
    [Fact]
    public void Rule3_open_pvp_closed_by_default_and_loot_three_stacks()
    {
        var d = D(); var s = Two(d);
        Assert.False(s.OpenPvp);
        Assert.Equal(RivalryVerdict.PvpClosed, RivalryRules.CanHit(d.Rivalry, s, true, 300, 300, "guney", targetIsPlayer: true));
        s.OpenPvp = true;
        Assert.Equal(RivalryVerdict.Allowed, RivalryRules.CanHit(d.Rivalry, s, false, 300, 300, "guney", targetIsPlayer: true));
        var victim = new Inventory(20, id => 50); var killer = new Inventory(20, id => 50);
        foreach (var id in new[] { "stone", "wood", "fiber", "clay", "wheat" }) victim.Add(id, 5);
        var moved = RivalryRules.PvpLoot(d.Rivalry, victim, killer, new Rng(7));
        Assert.Equal(3, moved.Count);
        Assert.Equal(2, victim.Stacks.Count);
        Assert.Equal(15, killer.Stacks.Sum(x => x.count));
    }

    // 4. Kabarma herkese ayni anda: gece tapu korumasi askida
    [Fact]
    public void Rule4_night_comes_to_everyone_and_suspends_plot_protection()
    {
        var d = D(); var s = Two(d);
        foreach (var (x, z) in new[] { (5f, 5f), (5f, 605f) })
        {
            Assert.Equal(RivalryVerdict.DayProtected, RivalryRules.CanHit(d.Rivalry, s, false, x, z, x < 100 && z < 100 ? "guney" : "kuzey"));
            Assert.Equal(RivalryVerdict.Allowed, RivalryRules.CanHit(d.Rivalry, s, true, x, z, x < 100 && z < 100 ? "guney" : "kuzey"));
        }
    }

    // 5. Acik Silo: gecenin %30'u + ihracata hazir mallar yagmalanir; ambar ve hayvan yagmalanmaz
    [Fact]
    public void Rule5_open_silo_holds_30_percent_and_export_goods_and_is_lootable()
    {
        var d = D(); var s = Two(d);
        var k = s.Team("kuzey")!;
        k.NightMaya = 100f;
        RivalryRules.MarkExportReady(k, "turnip", 8);
        RivalryRules.Dawn(d.Rivalry, s);
        Assert.Equal(30, k.SiloMaya);
        Assert.Equal(70, k.CollectedMaya);
        Assert.Equal(8, k.Silo["turnip"]);
        Assert.Equal(8, k.Exports);
        // Yagma
        var bag = new Inventory(20, id => 99);
        var own = RivalryRules.LootSilo(s, "kuzey", "kuzey", new Inventory(20, id => 99)); // kendi silosunu yagmalayamaz
        Assert.Equal((0, 0), own);
        var (items, maya) = RivalryRules.LootSilo(s, "kuzey", "guney", bag);
        Assert.Equal(8, items); Assert.Equal(30, maya);
        Assert.Equal(30, bag.Count("maya")); Assert.Equal(8, bag.Count("turnip"));
        Assert.Equal(30, s.Team("guney")!.CollectedMaya);
        Assert.Empty(k.Silo);
        Assert.Equal(0, k.SiloMaya);
        Assert.Equal((0, 0), RivalryRules.LootSilo(s, "guney", "guney", bag)); // baska takimin silosuna dokunmaz: guney'in silosu ayri
        // Yagmalanabilirlik
        Assert.True(RivalryRules.Lootable(d.Rivalry, s, true, "swollen_crop", 5, 5, "guney"));
        Assert.False(RivalryRules.Lootable(d.Rivalry, s, false, "swollen_crop", 5, 5, "guney"));
        Assert.False(RivalryRules.Lootable(d.Rivalry, s, true, "warehouse", 5, 5, "guney"));
        Assert.False(RivalryRules.Lootable(d.Rivalry, s, true, "animal", 5, 5, "guney"));
        Assert.False(RivalryRules.Lootable(d.Rivalry, s, true, "swollen_crop", 5, 5, "kuzey"));
    }

    // 6. Yapi hasari gecede; enkaz plani %50 bedelle kurulur; Ocak yikilmaz; Kazan'i devrilen mühürlenir
    [Fact]
    public void Rule6_structure_damage_ruins_rebuild_half_cost_ocak_indestructible_and_toppled_seal()
    {
        var d = D(); var s = Two(d); var g = Graph(d);
        int id = Wall(g, 10, 10);
        // Gunduz hasar yok
        Assert.Equal(RivalryVerdict.DayProtected, RivalryRules.DamagePiece(d.Rivalry, s, g, id, 9999, false, "guney"));
        Assert.True(g.Pieces.ContainsKey(id));
        // Gece yikilir, enkaz plani kalir
        var gone = new List<StructurePiece>();
        Assert.Equal(RivalryVerdict.Allowed, RivalryRules.DamagePiece(d.Rivalry, s, g, id, 99999, true, "guney", gone));
        Assert.Empty(g.Pieces);
        Assert.Single(s.Ruins);
        var ruin = s.Ruins[0];
        Assert.Equal("kuzey", ruin.Team);
        var full = StructureCosts.Cost(d.Build, ruin.Material, ruin.Units);
        var half = RivalryRules.RebuildCost(d.Rivalry, d.Build, ruin);
        Assert.All(half, kv => Assert.True(kv.Value <= full[kv.Key] && kv.Value >= 1));
        // Gece kurulmaz, baska takim kuramaz, eksik malzemeyle olmaz
        var bag = new Inventory(20, i => 99);
        Assert.Equal("gece kurulmaz", RivalryRules.Rebuild(d.Rivalry, s, g, d.Build, ruin.Id, "kuzey", true, bag));
        Assert.Equal("baska takimin enkazi", RivalryRules.Rebuild(d.Rivalry, s, g, d.Build, ruin.Id, "guney", false, bag));
        Assert.StartsWith("eksik", RivalryRules.Rebuild(d.Rivalry, s, g, d.Build, ruin.Id, "kuzey", false, bag));
        foreach (var kv in half) bag.Add(kv.Key, kv.Value);
        Assert.Null(RivalryRules.Rebuild(d.Rivalry, s, g, d.Build, ruin.Id, "kuzey", false, bag));
        Assert.Single(g.Pieces);
        Assert.Empty(s.Ruins);
        Assert.Equal(0, bag.Stacks.Sum(x => x.count));
        // Ocak yikilmaz
        Assert.Equal(RivalryVerdict.Indestructible, RivalryRules.CanHit(d.Rivalry, s, true, 0, 0, "guney", "ocak"));
        // Kazan'i devrilen takim: gecesi biter, tapu muhurlu; safakta acilir
        RivalryRules.ToppleKazan(s, "kuzey");
        Assert.Equal(RivalryVerdict.Sealed, RivalryRules.CanHit(d.Rivalry, s, true, 10, 10, "guney"));
        RivalryRules.Dawn(d.Rivalry, s);
        Assert.Equal(RivalryVerdict.Sealed, RivalryRules.CanHit(d.Rivalry, s, true, 10, 10, "guney")); // muhur gece kalani boyunca
        RivalryRules.ClearSeals(s); // yeni gun
        Assert.Equal(RivalryVerdict.Allowed, RivalryRules.CanHit(d.Rivalry, s, true, 10, 10, "guney"));
    }

    // 7. Cevrimdisi koruma: ekipten kimse yoksa tapu muhurlu, ekinler sismez
    [Fact]
    public void Rule7_offline_protection_seals_plot_and_stops_swelling()
    {
        var d = D(); var s = Two(d);
        Assert.Equal(1, RivalryRules.UpdatePresence(s, new HashSet<string> { "p2" }));
        Assert.True(s.Team("kuzey")!.Offline);
        Assert.Equal(RivalryVerdict.Offline, RivalryRules.CanHit(d.Rivalry, s, true, 10, 10, "guney"));
        Assert.False(RivalryRules.SwellAllowed(s, 10, 10));
        Assert.True(RivalryRules.SwellAllowed(s, 10, 610));
        Assert.False(RivalryRules.Lootable(d.Rivalry, s, true, "swollen_crop", 10, 10, "guney"));
        // Biri gelince acilir
        Assert.Equal(1, RivalryRules.UpdatePresence(s, new HashSet<string> { "p1", "p2" }));
        Assert.False(s.Team("kuzey")!.Offline);
        Assert.True(RivalryRules.SwellAllowed(s, 10, 10));
        // Degisim yoksa 0
        Assert.Equal(0, RivalryRules.UpdatePresence(s, new HashSet<string> { "p1", "p2" }));
    }

    // 8. Sezon tablosu: Maya + ihracat, 7/14/30/sonsuz, sezon sonunda donar
    [Fact]
    public void Rule8_season_leaderboard_and_freeze()
    {
        var d = D(); var s = Two(d);
        Assert.False(RivalryRules.SetSeason(d.Rivalry, s, 5, 1));
        Assert.True(RivalryRules.SetSeason(d.Rivalry, s, 7, 1));
        RivalryRules.AddMaya(s, "kuzey", 40); RivalryRules.AddMaya(s, "guney", 90);
        RivalryRules.Dawn(d.Rivalry, s);
        Assert.Equal("guney", RivalryRules.Leaderboard(s)[0].Id);
        Assert.False(RivalryRules.TickSeason(s, 7));
        Assert.True(RivalryRules.TickSeason(s, 8));
        Assert.True(s.Season.Frozen);
        Assert.Equal("guney", s.Season.Winner);
        // Donunca puan islemez
        RivalryRules.AddMaya(s, "kuzey", 9999); RivalryRules.Dawn(d.Rivalry, s);
        Assert.Equal("guney", RivalryRules.Leaderboard(s)[0].Id);
        // Sonsuz sezon donmaz
        Assert.True(RivalryRules.SetSeason(d.Rivalry, s, 0, 1));
        Assert.False(RivalryRules.TickSeason(s, 9999));
    }

    // 9. Holding kamplari sunucu ayariyla
    [Fact]
    public void Rule9_holding_camps_follow_the_server_setting()
    {
        var d = D(); var s = Two(d);
        Assert.False(RivalryRules.HoldingAllowed(s));
        s.HoldingCamps = true;
        Assert.True(RivalryRules.HoldingAllowed(s));
        Assert.True(RivalryRules.HoldingAllowed(new RivalryState())); // Cekisme degil: olagan
    }

    // 10. Dinleme sunucusu 2 takim / 8 oyuncu, adanmis 4 takim / 16 oyuncu
    [Fact]
    public void Rule10_listen_two_teams_dedicated_four_teams()
    {
        var d = D();
        var listen = RivalryRules.New(d.Rivalry, false, 1);
        for (int i = 0; i < 3; i++) RivalryRules.AddTeam(d.Rivalry, listen, "t" + i, "T", 0, i * 600, 60, $"p{i}");
        Assert.Contains("en cok 2", RivalryRules.ValidateSetup(d.Rivalry, listen));
        var ded = RivalryRules.New(d.Rivalry, true, 1);
        for (int i = 0; i < 4; i++) RivalryRules.AddTeam(d.Rivalry, ded, "t" + i, "T", 0, i * 600, 60, $"p{i}a", $"p{i}b", $"p{i}c", $"p{i}d");
        Assert.Null(RivalryRules.ValidateSetup(d.Rivalry, ded));
        ded.Teams[0].Players.Add("fazla");
        Assert.NotNull(RivalryRules.ValidateSetup(d.Rivalry, ded));
        var lis8 = RivalryRules.New(d.Rivalry, false, 1);
        RivalryRules.AddTeam(d.Rivalry, lis8, "a", "A", 0, 0, 60, "1", "2", "3", "4");
        RivalryRules.AddTeam(d.Rivalry, lis8, "b", "B", 0, 600, 60, "5", "6", "7", "8");
        Assert.Null(RivalryRules.ValidateSetup(d.Rivalry, lis8));
    }

    // Kayit sozlesmesi: durum duz DTO, gidis-donus ayni
    [Fact]
    public void State_round_trips_through_save_section()
    {
        var d = D();
        var rv = Two(d);
        rv.Team("kuzey")!.Silo["turnip"] = 3; rv.Team("kuzey")!.SiloMaya = 12;
        rv.Ruins.Add(new RuinPlan { Id = 4, Team = "kuzey", Shape = "kare_temel" });
        rv.Team("kuzey")!.ExportReady["egg"] = 2;
        var json = JsonSerializer.Serialize(rv);
        var back = JsonSerializer.Deserialize<RivalryState>(json)!;
        Assert.Equal(JsonSerializer.Serialize(back), json);
        Assert.Equal(12, back.Team("kuzey")!.SiloMaya);
        Assert.Equal(2, back.Team("kuzey")!.ExportReady["egg"]);
    }
}
