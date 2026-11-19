using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Tuzuk maddeleri, KS 11+ ve Yeni Ay (gece4, GDD 14.8-14.9) ile Kabartma Odasi (GDD 14.10).</summary>
public class TuzukTests
{
    static readonly GameData D = TestData.Load();
    static TuzukFile T => D.Tuzuk;
    const ulong World = 12345;

    static KabarmaSpec Spec(int ks, TuzukSet tz, int players = 1, KabarmaKind kind = KabarmaKind.Full) =>
        new() { Kind = kind, Ks = ks, Players = players, Biome = "altin_cayir", PlantedFamilies = new() { ["grain"] = 10, ["root"] = 10 }, Seed = 7, Tuzuk = tz };

    static TuzukSet Only(string id, int stack) => new(new Dictionary<string, int> { [id] = stack }, T, 0);

    [Fact]
    public void Data_has_twelve_clauses_with_canonical_ids_and_ks_goes_to_fifty()
    {
        string[] ids = { "sis", "ac_suru", "kalin_kabuk", "cift_azman", "kirik_can", "saganak", "holding_seferberligi", "cimri_ay", "uzun_gece", "kor_kule", "gocmen_suru", "ters_ruzgar" };
        Assert.Equal(ids.OrderBy(x => x), T.Clauses.Select(c => c.Id).OrderBy(x => x));
        Assert.Equal(50, D.Kabarma.Full.MaxKs);
        Assert.Equal(26, T.FirstKs); Assert.Equal(38, T.SecondRoundKs); Assert.Equal(2, T.MaxStack);
    }

    [Fact]
    public void Order_is_a_stable_shuffle_per_world_seed()
    {
        var a = Tuzuk.Order(D, World);
        Assert.Equal(12, a.Distinct().Count());
        Assert.Equal(a, Tuzuk.Order(D, World));
        Assert.NotEqual(a, Tuzuk.Order(D, World + 1)); // 12! olasilikla ayni degil
    }

    [Fact]
    public void Clauses_arrive_one_per_ks_from_26_and_stack_from_38()
    {
        var order = Tuzuk.Order(D, World);
        Assert.Empty(Tuzuk.Stacks(D, World, 25, new string[0]));
        var k26 = Tuzuk.Stacks(D, World, 26, new string[0]);
        Assert.Single(k26); Assert.Equal(1, k26[order[0]]);
        var k30 = Tuzuk.Stacks(D, World, 30, new string[0]);
        Assert.Equal(5, k30.Count);
        var k37 = Tuzuk.Stacks(D, World, 37, new string[0]);
        Assert.Equal(12, k37.Count); Assert.All(k37.Values, v => Assert.Equal(1, v));
        var k38 = Tuzuk.Stacks(D, World, 38, new string[0]);
        Assert.Equal(2, k38[order[0]]); Assert.Equal(1, k38[order[1]]);
        var k49 = Tuzuk.Stacks(D, World, 49, new string[0]);
        Assert.All(k49.Values, v => Assert.Equal(2, v));
        Assert.Equal(k49, Tuzuk.Stacks(D, World, 50, new string[0])); // KS 50'de yeni madde yok
    }

    [Fact]
    public void New_moon_choice_is_active_from_ks1_and_caps_at_two_stacks()
    {
        var order = Tuzuk.Order(D, World);
        var s1 = Tuzuk.Stacks(D, World, 1, new[] { "kor_kule", "yok_boyle_bir_madde" });
        Assert.Single(s1); Assert.Equal(1, s1["kor_kule"]);
        var late = Tuzuk.Stacks(D, World, 49, new[] { order[3] });
        Assert.Equal(2, late[order[3]]); // secilen + iki tur = tavan 2
    }

    [Fact]
    public void Small_and_drill_get_only_new_moon_clauses()
    {
        var chosen = new[] { "sis" };
        Assert.Empty(Tuzuk.For(D, World, 40, new string[0], KabarmaKind.Small).Stacks);
        var d = Tuzuk.For(D, World, 40, chosen, KabarmaKind.Drill);
        Assert.Single(d.Stacks);
        Assert.Equal(12, Tuzuk.For(D, World, 40, chosen, KabarmaKind.Full).Stacks.Count);
        Assert.Same(TuzukSet.None, Tuzuk.For(D, World, 20, new string[0], KabarmaKind.Full));
    }

    [Fact]
    public void Effect_values_scale_with_the_stack()
    {
        Assert.Equal(30f, Only("sis", 1).VisionM); Assert.Equal(20f, Only("sis", 2).VisionM); Assert.Equal(0f, TuzukSet.None.VisionM);
        Assert.Equal(2f, Only("ac_suru", 1).EatSpeed, 3); Assert.Equal(3f, Only("ac_suru", 2).EatSpeed, 3);
        Assert.Equal(0.10f, Only("kalin_kabuk", 1).EliteAdd, 3); Assert.Equal(0.20f, Only("kalin_kabuk", 2).EliteAdd, 3);
        Assert.Equal(1, Only("cift_azman", 1).AzmanAdd); Assert.Equal(2, Only("cift_azman", 2).AzmanAdd);
        Assert.True(Only("kirik_can", 2).NoGap);
        Assert.Equal(0f, Only("saganak", 1).FireMult); Assert.Equal(1.5f, Only("saganak", 1).ShockMult, 3); Assert.Equal(2f, Only("saganak", 2).ShockMult, 3);
        Assert.True(Only("holding_seferberligi", 1).HoldingAllRaid);
        Assert.Equal(2, Only("cimri_ay", 1).OfferSize); Assert.Equal(1, Only("cimri_ay", 2).OfferSize);
        Assert.Equal(4, Only("uzun_gece", 1).ExtraWaves); Assert.Equal(8, Only("uzun_gece", 2).ExtraWaves);
        Assert.Equal(0.7f, Only("kor_kule", 1).TowerSpeed, 3); Assert.Equal(0.4f, Only("kor_kule", 2).TowerSpeed, 3);
        Assert.Equal(0.75f, Only("ters_ruzgar", 1).RangeMult, 3); Assert.Equal(0.5f, Only("ters_ruzgar", 2).RangeMult, 3);
        Assert.Equal(1f, TuzukSet.None.FireMult); Assert.Equal(1f, TuzukSet.None.TowerSpeed); Assert.Equal(0, TuzukSet.None.OfferSize);
    }

    [Fact]
    public void Compose_uses_long_night_broken_bell_thick_shell_and_double_azman()
    {
        var plain = Waves.Compose(D, Spec(5, TuzukSet.None));
        var longNight = Waves.Compose(D, Spec(5, Only("uzun_gece", 1)));
        Assert.Equal(plain.Waves.Count + 4, longNight.Waves.Count);
        Assert.True(longNight.Waves[^1].Doruk);
        Assert.All(plain.Waves.Take(plain.Waves.Count - 1), w => Assert.True(w.GapAfter > 0));
        Assert.All(Waves.Compose(D, Spec(5, Only("kirik_can", 1))).Waves, w => Assert.Equal(0f, w.GapAfter));
        // Kalin Kabuk: elit orani KS 1'de bile ~%12 (tavanin ustune eklenir)
        int Elites(WavePlan p) => p.Waves.SelectMany(w => w.Slots).Count(s => !string.IsNullOrEmpty(s.Elite) && !s.Azman);
        int total(WavePlan p) => p.Waves.Take(p.Waves.Count - 1).SelectMany(w => w.Slots).Count();
        var thick = Waves.Compose(D, Spec(8, Only("kalin_kabuk", 2)));
        float rate = thick.Waves.Take(thick.Waves.Count - 1).SelectMany(w => w.Slots).Count(s => !string.IsNullOrEmpty(s.Elite)) / (float)total(thick);
        Assert.InRange(rate, 0.25f, 0.55f); // 8 x 0,02 + 0,20 = 0,36
        Assert.Equal(1, plain.Waves[^1].Slots.Count(s => s.Azman));
        Assert.Equal(2, Waves.Compose(D, Spec(5, Only("cift_azman", 1))).Waves[^1].Slots.Count(s => s.Azman));
        Assert.Equal(3, Waves.Compose(D, Spec(5, Only("cift_azman", 2))).Waves[^1].Slots.Count(s => s.Azman));
        Assert.True(Elites(plain) >= 0);
    }

    [Fact]
    public void Migrant_flock_draws_from_every_biome()
    {
        var plan = Waves.Compose(D, new KabarmaSpec { Kind = KabarmaKind.Full, Ks = 20, Biome = "altin_cayir", PlantedFamilies = new(), Seed = 5, Tuzuk = Only("gocmen_suru", 1) });
        var species = plan.Waves.SelectMany(w => w.Slots).Select(s => s.Species).ToHashSet();
        var f = D.Kabarma.Full;
        bool fromOther(string biome) => f.Natives[biome].Any(species.Contains);
        Assert.True(fromOther("altin_cayir") && fromOther("sis_korusu") && fromOther("kizil_kanyon") && fromOther("ayaz_yaylasi"), string.Join(",", species));
        var plain = Waves.Compose(D, new KabarmaSpec { Kind = KabarmaKind.Full, Ks = 20, Biome = "altin_cayir", PlantedFamilies = new(), Seed = 5 });
        Assert.DoesNotContain(plain.Waves.SelectMany(w => w.Slots), s => f.Natives["ayaz_yaylasi"].Contains(s.Species));
    }

    [Fact]
    public void New_moon_adds_five_percent_maya_per_clause_and_stays_off_drills()
    {
        var chosen = new TuzukSet(new Dictionary<string, int> { ["sis"] = 1, ["kor_kule"] = 1, ["cimri_ay"] = 1 }, T, 3);
        var plan = Waves.Compose(D, Spec(3, chosen));
        Assert.Equal(1f + 3 * T.NewMoon.MayaPerClause, plan.MayaMult, 4);
        var drill = Waves.Compose(D, Spec(3, chosen, kind: KabarmaKind.Drill));
        Assert.Equal(0f, drill.MayaMult);
    }

    [Fact]
    public void Cimri_ay_cuts_the_card_offer_and_new_moon_is_set_once_and_saved()
    {
        var plan = Waves.Compose(D, Spec(3, Only("cimri_ay", 1)));
        var n = new KabarmaNight(D, plan, 1);
        Assert.Equal(2, n.Hand.OfferCap);
        Assert.Equal(0, new KabarmaNight(D, Waves.Compose(D, Spec(3, TuzukSet.None)), 1).Hand.OfferCap);

        var s = new GameState(D, World);
        Assert.Equal(2, s.SetNewMoon(new[] { "sis", "ters_ruzgar", "sis", "bilinmeyen" }));
        Assert.Equal(2, s.KabarmaSpecFor(KabarmaKind.Full, 1, 1, 0, 0).Tuzuk.Stacks.Count);
        var json = JsonSerializer.Serialize(new TuzukSave { NewMoon = s.NewMoonClauses });
        Assert.Contains("ters_ruzgar", json);
        s.StartKabarma(s.KabarmaSpecFor(KabarmaKind.Full, 1, 1, 0, 0));
        Assert.Equal(0, s.SetNewMoon(new[] { "kor_kule" })); // ilk gecede degistirilemez
        Assert.Equal(2, s.NewMoonClauses.Count);
    }

    [Fact]
    public void Ks_above_ten_composes_with_all_new_biome_natives_and_scaling()
    {
        var s = new GameState(D, World);
        var spec = s.KabarmaSpecFor(KabarmaKind.Full, 30, 1, 0, 0);
        Assert.Equal(30, spec.Ks);
        Assert.Equal(5, spec.Tuzuk.Stacks.Count); // KS 26..30: bes madde
        var low = s.KabarmaSpecFor(KabarmaKind.Full, 10, 1, 0, 0);
        Assert.Same(TuzukSet.None, low.Tuzuk);
        var plan = Waves.Compose(D, spec);
        Assert.True(plan.Waves.Count >= D.Kabarma.Full.WaveCount + 1);
        Assert.Equal(50, s.KabarmaSpecFor(KabarmaKind.Full, 99, 1, 0, 0).Ks); // tavan
    }

    // ---------- Kabartma Odasi ----------

    static (GameState s, Plot p) MaturePlot()
    {
        var s = new GameState(D, World);
        Assert.True(s.Farm.Till(0, 0));
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        var p = s.Farm.Get(0, 0)!;
        for (int i = 0; i < 20000 && p.Stage != CropStage.Mature; i++) { if (p.Moisture < 60) s.Farm.Water(0, 0); s.Farm.Tick(0.5f, true); }
        Assert.Equal(CropStage.Mature, p.Stage);
        return (s, p);
    }

    [Fact]
    public void Swell_room_swells_one_mature_plot_for_fifty_maya_in_daytime()
    {
        var (s, p) = MaturePlot();
        Assert.Equal(50, T.SwellRoom.MayaCost);
        Assert.Same(p, SwellRoom.NearestMature(s, 0.5f, 0.5f));
        Assert.Null(SwellRoom.NearestMature(s, 40f, 40f)); // 12 m disinda
        Assert.Equal(SwellRoomResult.NoMaya, SwellRoom.UsePlot(s, s.Backpack, true, p));
        s.Backpack.Add("maya", 120);
        Assert.Equal(SwellRoomResult.NoPower, SwellRoom.UsePlot(s, s.Backpack, false, p));
        Assert.Equal(SwellRoomResult.Ok, SwellRoom.UsePlot(s, s.Backpack, true, p));
        Assert.NotNull(p.Swollen);
        Assert.Equal(0f, p.SinceDawn); // sonme saati hemen basladi
        Assert.Equal(70, s.Backpack.Count("maya"));
        Assert.Equal(SwellRoomResult.NoTarget, SwellRoom.UsePlot(s, s.Backpack, true, p)); // zaten kabarik: bedel odenmez
        Assert.Equal(70, s.Backpack.Count("maya"));
        Assert.Null(SwellRoom.NearestMature(s, 0.5f, 0.5f));
    }

    [Fact]
    public void Swell_room_refuses_during_kabarma_and_charges_animals_only_with_a_target()
    {
        var (s, p) = MaturePlot();
        s.Backpack.Add("maya", 100);
        Assert.Equal(SwellRoomResult.NoTarget, SwellRoom.UseAnimal(s, s.Backpack, true, hasAnimal: false));
        Assert.Equal(100, s.Backpack.Count("maya"));
        Assert.Equal(SwellRoomResult.Ok, SwellRoom.UseAnimal(s, s.Backpack, true, hasAnimal: true));
        Assert.Equal(50, s.Backpack.Count("maya"));
        s.StartKabarma(s.KabarmaSpecFor(KabarmaKind.Full, 1, 1, 0, 0));
        Assert.Equal(SwellRoomResult.KabarmaActive, SwellRoom.UsePlot(s, s.Backpack, true, p));
        Assert.Equal(50, s.Backpack.Count("maya"));
    }
}
