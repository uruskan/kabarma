using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M4 (holding4): 2. ve 3. kamp, Genel Merkez ve Mudur (GDD §19.1, §19.5; ICERIK §3.7, §4.4).</summary>
public class Holding4Tests
{
    static readonly GameData D = TestData.Load();

    /// <summary>Ilk kamp kurulmus durum (gercek Kabarma sonrasi sabah).</summary>
    static GameState WithFirstCamp()
    {
        var s = new GameState(D);
        s.Holding.OnKabarmaEnded(true, 2);
        s.Holding.OnMorning(3, 2);
        Assert.Single(s.Holding.State.Camps);
        return s;
    }

    [Fact]
    public void Sites_data_four_slots_each_in_its_biome_and_hq_at_crater_foot()
    {
        Assert.Equal(new[] { 1, 2, 3, 4 }, D.Holding.Sites.Select(x => x.Slot));
        var s = new GameState(D);
        foreach (var site in D.Holding.Sites.Where(x => x.Slot is 2 or 3))
        {
            var (x, z) = s.Holding.SitePos(site);
            Assert.Equal(site.Biome, s.BiomeMap.BiomeAt(x, z));
            Assert.True(s.BiomeMap.Weight(site.Biome, x, z) >= HoldingSites.MinWeight);
            Assert.True(Math.Max(Math.Abs(x), Math.Abs(z)) < D.Biomes.World.Terrain.EdgeStart);
        }
        var hq = D.Holding.Site(4)!;
        var (hx, hz) = s.Holding.SitePos(hq);
        float dist = MathF.Sqrt((hx - s.BiomeMap.CraterX) * (hx - s.BiomeMap.CraterX) + (hz - s.BiomeMap.CraterZ) * (hz - s.BiomeMap.CraterZ));
        Assert.InRange(dist, s.BiomeMap.Crater.Radius, s.BiomeMap.Crater.Radius * 2.2f);
        Assert.Equal("hq", hq.Kind); Assert.Equal(10, hq.Level);
        // camplar birbirinden uzak
        var pts = D.Holding.Sites.Select(x => s.Holding.SitePos(x)).ToList();
        for (int i = 0; i < pts.Count; i++) for (int j = i + 1; j < pts.Count; j++)
            Assert.True(Math.Abs(pts[i].x - pts[j].x) + Math.Abs(pts[i].z - pts[j].z) > 150f);
    }

    [Fact]
    public void Second_third_camp_and_hq_found_at_ocak_t3_t4_t5_once_and_hq_is_fixed()
    {
        var s = WithFirstCamp();
        var h = s.Holding;
        h.OnMorning(4, 2);
        Assert.Single(h.State.Camps);                 // T2: yalniz 1. kamp
        h.OnMorning(5, 3);
        Assert.Equal(new[] { 1, 2 }, h.State.Camps.Select(c => c.Slot));
        h.OnMorning(6, 3);
        Assert.Equal(2, h.State.Camps.Count);         // ayni basamakta ikinci kez kurulmaz
        h.OnMorning(7, 4);
        Assert.Equal(3, h.State.Camps.Count);
        h.OnMorning(8, 5);
        var hq = h.State.Camps.Single(c => c.Slot == 4);
        Assert.Equal("hq", hq.Kind); Assert.Equal(10, hq.Level);
        Assert.Equal(4, h.State.Camps.Count(c => c.Kind == "camp") + 1);
        // 2. kamp kendi biyomunda, kervan teslim noktasi kendine gore
        var c2 = h.State.Camps.Single(c => c.Slot == 2);
        Assert.Equal("isil_sazlik", s.BiomeMap.BiomeAt(c2.X, c2.Z));
        var (dx, dz) = h.DepotOf(c2);
        Assert.Equal(c2.X - 90, dx, 1); Assert.Equal(c2.Z + 60, dz, 1);
        Assert.Equal(D.Holding.Caravan.Depot[0], h.DepotOf(h.State.Camps[0]).x);
        // Genel Merkez: seviye atlamaz, Husumet birikmez, baskin ve kervan yok
        for (int d = 9; d < 30; d++) h.OnMorning(d, 5);
        Assert.Equal(10, hq.Level); Assert.Equal(0, hq.Husumet);
        Assert.Null(h.DepartCaravan(hq.Id, 30));
        foreach (var c in h.State.Camps) c.Husumet = 90;
        var raids = h.OnNightStart(true);
        Assert.DoesNotContain(raids, r => r.camp.Kind == "hq");
        Assert.NotEmpty(raids);
        // yeni kamplar 1. seviyeden baslar ve 5 gunde seviye atlar (tavan Ocak x 2)
        var c3 = h.State.Camps.Single(c => c.Slot == 3);
        Assert.True(c3.Level > 1 && c3.Level <= 10);
    }

    [Fact]
    public void Hq_enters_only_at_night_core_unlocks_after_others_and_starts_mudur()
    {
        var s = WithFirstCamp(); var h = s.Holding;
        h.OnMorning(4, 5); h.OnMorning(5, 5); h.OnMorning(6, 5);
        var hq = h.State.Camps.Single(c => c.Slot == 4);
        Assert.True(h.PerdeUp(hq));                                   // gunduz Perde: girilmez
        Assert.Equal(HoldingHit.Blocked, h.HitStructure(hq.Id, "silo", 99999, out _));
        h.OnNightStart(true);
        Assert.False(h.PerdeUp(hq));
        Assert.Equal(HoldingHit.Locked, h.HitStructure(hq.Id, "cekirdek", 99999, out _));
        foreach (var id in new[] { "silo", "atolye", "ahir" }) Assert.Equal(HoldingHit.Destroyed, h.HitStructure(hq.Id, id, 99999, out _));
        Assert.Null(h.State.Mudur);
        Assert.Equal(HoldingHit.Destroyed, h.HitStructure(hq.Id, "cekirdek", 99999, out var loot));
        Assert.NotNull(h.State.Mudur);
        Assert.False(hq.Destroyed);                                   // Mudur yenilene dek GM ayakta
        Assert.Equal(0, h.State.Mudur!.Stage);
        Assert.Equal(D.Holding.Mudur.Truck.Hp, h.State.Mudur.TruckHp);
    }

    static (HoldingSystem h, MudurFight f) Fight(int team = 1)
    {
        var h = new HoldingSystem(D) { Team = () => team };
        var f = MudurRules.Start(D.Holding, 1, team);
        h.State.Mudur = f;
        h.State.Camps.Add(new HoldingCamp { Id = 1, Slot = 4, Kind = "hq", Level = 10 });
        f.ShieldT = 0;
        return (h, f);
    }

    [Fact]
    public void Mudur_yard_truck_body_armored_motor_weak_mechanics_repair()
    {
        var (h, f) = Fight();
        float body = h.MudurHit("kasa", 100), motor = h.MudurHit("motor", 100);
        Assert.Equal(30f, body, 3);                                   // %70 zirh
        Assert.Equal(300f, motor, 3);                                 // motor ×3, zirhsiz
        Assert.Equal(0f, h.MudurHit("jenerator:0", 100));             // yanlis asama
        Assert.Equal(0f, h.MudurHit("mudur", 100));
        float hp = f.TruckHp;
        h.MudurTick(1f, 2);                                           // iki Mekanik onarir: 30 can/sn
        Assert.Equal(hp + 30f, f.TruckHp, 3);
        h.MudurTick(1f, 0);
        Assert.Equal(hp + 30f, f.TruckHp, 3);                         // Mekanik yoksa onarim yok
        // onarim tam cani gecmez
        f.TruckHp = f.TruckMax - 5; h.MudurTick(5f, 2);
        Assert.Equal(f.TruckMax, f.TruckHp, 3);
    }

    [Fact]
    public void Mudur_floor_generators_shield_speakers_speed_calls_then_roof_balloons_then_defeat_and_reward()
    {
        var (h, f) = Fight();
        var cmds = new List<MudurCommand>();
        h.MudurHit("motor", 100000, cmds);
        Assert.Contains(cmds, c => c.Cue == MudurCue.StageChanged && c.Stage == 1);
        Assert.Equal(1, f.Stage); f.ShieldT = 0;
        Assert.Equal(4, f.Generators.Count); Assert.Equal(2, f.Speakers.Count);
        // jeneratorler kapanmadan Mudur hasar almaz
        Assert.Equal(0f, h.MudurHit("mudur", 500));
        Assert.Equal(1.4f, MudurRules.UnitSpeedMult(D.Holding, f), 3);
        h.MudurHit("hoparlor:0", 1000); Assert.Equal(1.4f, MudurRules.UnitSpeedMult(D.Holding, f), 3); // biri calisiyor
        h.MudurHit("hoparlor:1", 1000); Assert.Equal(1f, MudurRules.UnitSpeedMult(D.Holding, f), 3);   // ikisi de susturuldu
        // Ustabasi cagrisi 14 sn'de bir (ilk 7 sn)
        var calls = 0;
        for (int i = 0; i < 300; i++) calls += h.MudurTick(0.1f, 0).Count(c => c.Cue == MudurCue.CallUstabasi);
        Assert.Equal(2, calls);
        for (int i = 0; i < 4; i++) h.MudurHit($"jenerator:{i}", 1000);
        Assert.True(MudurRules.GeneratorsDown(f));
        float before = f.BossHp;
        Assert.True(h.MudurHit("mudur", 400) > 0);
        Assert.Equal(before - 400, f.BossHp, 3);
        // cati asamasi: can orani %40'in altina inince
        h.MudurHit("mudur", f.BossHp - f.BossMax * 0.4f + 1f, cmds);
        Assert.Equal(2, f.Stage); f.ShieldT = 0;
        Assert.Equal(6, f.Balloons.Count);
        Assert.Equal(0f, h.MudurHit("mudur", 100));                   // balonlar inmeden duser degil
        for (int i = 0; i < 6; i++) { Assert.Equal(6 - i, MudurRules.BalloonsLeft(f)); Assert.True(h.MudurHit($"balon:{i}", 1000) > 0); }
        Assert.Equal(0, MudurRules.BalloonsLeft(f));
        var done = new List<MudurCommand>();
        h.MudurHit("mudur", 99999, done);
        Assert.True(f.Defeated); Assert.Contains(done, c => c.Cue == MudurCue.Defeated);
        var hq = h.State.Camps[0];
        Assert.True(hq.Cleared && hq.Destroyed);
        Assert.Equal(1, h.State.MudurDefeats);
        Assert.Contains("head:yercekimi", h.State.Unlocks); Assert.Contains("cosmetic:kask_holding_muduru", h.State.Unlocks);
        Assert.Contains("cosmetic:kaplama_holding", h.State.Unlocks); Assert.Contains("plan:efsane_cip", h.State.Unlocks);
        var bag = new Inventory(30, 100);
        var loot = h.MudurReward(bag, out var left);
        Assert.NotNull(loot); Assert.Empty(left);
        Assert.Equal(3, bag.Count("holding_plani")); Assert.Equal(10, bag.Count("holding_parcasi"));
        Assert.Null(h.State.Mudur);
        Assert.Null(h.MudurReward(bag, out _));                       // odul bir kez
        // Genel Merkez yeniden kurulmaz
        h.OnMorning(50, 5);
        Assert.True(hq.Destroyed && hq.Cleared);
    }

    [Fact]
    public void Mudur_team_scales_hp_and_state_round_trips_through_json()
    {
        var (_, f1) = Fight(1); var (_, f4) = Fight(4);
        Assert.Equal(f1.TruckMax * 2.5f, f4.TruckMax, 2);
        Assert.Equal(f1.BossMax * 2.5f, f4.BossMax, 2);
        var s = new HoldingState();
        s.Mudur = f4; f4.Stage = 1; f4.Generators = new() { 10, 0, 5, 160 }; s.Unlocks.Add("head:yercekimi");
        s.Camps.Add(new HoldingCamp { Id = 3, Slot = 4, Kind = "hq", Level = 10, Cleared = true });
        var back = JsonSerializer.Deserialize<HoldingState>(JsonSerializer.Serialize(s))!;
        Assert.Equal(1, back.Mudur!.Stage);
        Assert.Equal(new[] { 10f, 0f, 5f, 160f }, back.Mudur.Generators);
        Assert.Equal("hq", back.Camps[0].Kind); Assert.True(back.Camps[0].Cleared);
        Assert.Contains("head:yercekimi", back.Unlocks);
    }

    [Fact]
    public void Dawn_during_mudur_fight_makes_him_flee_and_hq_is_rebuilt_for_next_night()
    {
        var s = WithFirstCamp(); var h = s.Holding;
        h.OnMorning(4, 5); h.OnMorning(5, 5); h.OnMorning(6, 5);
        var hq = h.State.Camps.Single(c => c.Slot == 4);
        h.OnNightStart(true);
        foreach (var id in new[] { "silo", "atolye", "ahir", "cekirdek" }) h.HitStructure(hq.Id, id, 99999, out _);
        Assert.NotNull(h.State.Mudur);
        h.OnDawn();
        Assert.Null(h.State.Mudur);
        Assert.False(hq.Cleared);
        Assert.All(hq.Structures.Values, hp => Assert.True(hp > 0));  // yapilar geri geldi
        h.OnNightStart(true);                                        // ertesi gece yeniden denenir
        Assert.Equal(HoldingHit.Locked, h.HitStructure(hq.Id, "cekirdek", 99999, out _));
    }}
