using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Tarim M4 (ICERIK 5 #12-#24, 11 Asi Tezgahi, 15): 13 yeni ekin, sera, su basmis ekin, Asi Tezgahi, 32 yemek.</summary>
public class FarmM4Tests
{
    static GameData Data() => TestData.Load();

    static readonly string[] New13 = { "rice", "reed", "glow_lily", "chili", "corn", "watermelon", "prickly_pear", "tar_bean", "frost_beet", "snow_tea", "ice_grape", "maya_flower", "leaven_mushroom" };

    [Fact]
    public void Crops_total_24_with_icerik_families_and_bands()
    {
        var d = Data();
        Assert.Equal(24, d.Crops.Crops.Count); // PLAN 7.1
        var want = new Dictionary<string, (string fam, string band, float days)>
        {
            ["rice"] = ("grain", "sazlik", 2.5f), ["reed"] = ("fiber", "sazlik", 2f), ["glow_lily"] = ("flower", "sazlik", 3f), ["chili"] = ("spice", "sazlik", 2.5f),
            ["corn"] = ("grain", "kanyon", 3f), ["watermelon"] = ("gourd", "kanyon", 3.5f), ["prickly_pear"] = ("fruit", "kanyon", 3f), ["tar_bean"] = ("oil", "kanyon", 2.5f),
            ["frost_beet"] = ("root", "ayaz", 3f), ["snow_tea"] = ("spice", "ayaz", 3f), ["ice_grape"] = ("fruit", "ayaz", 4f),
            ["maya_flower"] = ("flower", "gobek", 4f), ["leaven_mushroom"] = ("mushroom", "gobek", 4f),
        };
        foreach (var id in New13)
        {
            var c = d.Crop(id);
            Assert.Equal(want[id], (c.Family, c.Band, c.Days));
            Assert.Equal(c.Days * d.Crops.GrowDaySeconds, c.GrowSeconds, 3);
            Assert.True(c.BasePrice > 0 && c.BaseYield > 0 && c.SeedReturn > 0, id);
            Assert.False(string.IsNullOrEmpty(c.Trait), id);
            for (int i = 0; i < Genes.Count; i++) Assert.InRange(c.SeedGenes[i], d.Crops.Genetics.GeneMin, d.Crops.Genetics.GeneMax);
            Assert.True(d.Crops.Genetics.Bands.ContainsKey(c.Band), id); // biyom bandi tanimli
        }
        Assert.All(new[] { "rice", "reed", "glow_lily" }, id => Assert.True(d.Crop(id).Flooded));
        Assert.True(d.Crop("ice_grape").Perennial && d.Crop("ice_grape").RegrowDays == 2);
        Assert.True(d.Crop("leaven_mushroom").Dark);
        Assert.Equal(30, d.Crop("rice").SwollenYield);
        Assert.Equal(20, d.Crop("reed").SwollenYield);
        Assert.Equal(5, d.Crop("maya_flower").MayaPerHarvest);
        Assert.True(d.Crop("tar_bean").FertilityLoss < 0); // hasat Bereket +5
    }

    [Theory]
    [InlineData("rice")][InlineData("reed")][InlineData("glow_lily")][InlineData("chili")][InlineData("corn")][InlineData("watermelon")][InlineData("prickly_pear")]
    [InlineData("tar_bean")][InlineData("frost_beet")][InlineData("snow_tea")][InlineData("ice_grape")][InlineData("maya_flower")][InlineData("leaven_mushroom")]
    public void New_crop_grows_in_the_greenhouse_and_harvests_product_and_seed(string id)
    {
        var d = Data();
        var def = d.Crop(id);
        var f = new Farm(d);
        var bag = new Inventory(20, 99);
        bag.Add(def.SeedId, 1);
        f.Till(0, 0);
        f.SetGreenhouse(0, 0, true);
        if (def.Flooded) f.SetFlooded(0, 0, true);
        Assert.True(f.Plant(0, 0, id, bag, new Rng(1)));
        for (float t = 0; t < def.GrowSeconds * 2 && f.Get(0, 0)!.Stage != CropStage.Mature; t += 0.25f)
        {
            if (f.Get(0, 0)!.Moisture < 60) f.Water(0, 0);
            f.Tick(0.25f, !def.Dark);
        }
        Assert.Equal(CropStage.Mature, f.Get(0, 0)!.Stage);
        var h = f.HarvestFull(0, 0, new Rng(2))!;
        Assert.True(h.Yield >= 1);
        Assert.Equal(def.SeedId, h.SeedItem);
        Assert.Equal(def.SeedReturn, f.Seeds.CountFor(id));
    }

    [Fact]
    public void Greenhouse_fixes_the_band_and_flooded_crops_need_shallow_water()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(20, 99);
        bag.Add("corn_seed", 2); bag.Add("rice_seed", 2); bag.Add("wheat_seed", 1);
        for (int x = 0; x < 5; x++) { f.Till(x, 0); f.Water(x, 0); }
        f.Plant(0, 0, "corn", bag); f.Plant(1, 0, "corn", bag); f.Plant(2, 0, "rice", bag); f.Plant(3, 0, "rice", bag); f.Plant(4, 0, "wheat", bag);
        f.SetGreenhouse(1, 0, true);
        f.SetFlooded(3, 0, true);
        float home = f.GrowthFactor(f.Get(0, 0)!, true);
        float sera = f.GrowthFactor(f.Get(1, 0)!, true);
        Assert.True(home < 0.3f && sera > 0.6f && sera > home * 3f, $"ev {home}, sera {sera}"); // bant disi: dortte bir
        Assert.False(f.InBand(d.Crop("corn"), false));
        Assert.True(f.InBand(d.Crop("corn"), true));
        float dryRice = f.GrowthFactor(f.Get(2, 0)!, true), wetRice = f.GrowthFactor(f.Get(3, 0)!, true);
        Assert.True(wetRice > dryRice * 3f - 0.01f); // sig suda: x1 / sudan uzakta: x0,25 (ayrica bant)
        Assert.True(f.InBand(d.Crop("wheat"), false)); // Cayir ekini ana tapuda bantta
        // seradaki su basmis ekin: bant duzelir, su yine sart
        f.SetGreenhouse(2, 0, true);
        Assert.True(f.GrowthFactor(f.Get(2, 0)!, true) > dryRice * 3f);
    }

    [Fact]
    public void Greenhouse_and_flooded_squares_survive_save_and_load()
    {
        var d = Data();
        var f = new Farm(d);
        f.Till(0, 0); f.Till(1, 0); f.Till(2, 0);
        f.SetGreenhouse(0, 0, true); f.SetFlooded(1, 0, true);
        var g = new Farm(d);
        g.Till(0, 0); g.Till(1, 0); g.Till(2, 0);
        g.LoadGenetics(f.SaveGenetics());
        Assert.True(g.Get(0, 0)!.Greenhouse && !g.Get(0, 0)!.Flooded);
        Assert.True(g.Get(1, 0)!.Flooded && !g.Get(1, 0)!.Greenhouse);
        Assert.False(g.Get(2, 0)!.Greenhouse);
    }

    static (Farm f, Inventory bag) Graft_setup(out SeedLot recv, out SeedLot donor, string rc = "turnip", string dc = "turnip", int rv = 2, int dv = 8)
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(20, 99);
        f.Seeds.Add(rc, new Genes { V = rv, H = 3, D = 3, M = 3 }, null, 2);
        f.Seeds.Add(dc, new Genes { V = dv, H = 1, D = 1, M = 1 }, null, 2);
        bag.Add(d.Crop(rc).SeedId, 2); if (rc == dc) bag.Add(d.Crop(dc).SeedId, 2); else bag.Add(d.Crop(dc).SeedId, 2);
        recv = f.Seeds.Lots.First(l => l.Crop == rc && l.Genes.V == rv);
        donor = f.Seeds.Lots.First(l => l.Crop == dc && l.Genes.V == dv);
        return (f, bag);
    }

    [Fact]
    public void Graft_moves_a_gene_up_to_max_step_and_spends_one_seed_of_each()
    {
        var (f, bag) = Graft_setup(out var r, out var dn);
        Assert.Equal(GraftResult.Ok, f.Graft(bag, r, dn, 0, out var res)); // V: 2 <- 8, en fazla +2
        Assert.Equal(4, res!.Genes.V);
        Assert.Equal((3, 3, 3), (res.Genes.H, res.Genes.D, res.Genes.M)); // alicinin kalan genleri
        Assert.Equal(1, r.Count);
        Assert.Equal(1, dn.Count);
        Assert.Contains(f.Seeds.Lots, l => l.Crop == "turnip" && l.Genes.V == 4 && l.Count == 1);
        Assert.Equal(3, bag.Count("turnip_seed")); // 4 - 2 + 1
        Assert.Contains(f.Log.Notes, n => n.Kind == "graft");
    }

    [Fact]
    public void Graft_refuses_other_families_and_no_gain()
    {
        var (f, bag) = Graft_setup(out var r, out var dn, "turnip", "wheat");
        Assert.Equal(GraftResult.FamilyMismatch, f.Graft(bag, r, dn, 0, out _)); // Kok / Tahil
        var (f2, bag2) = Graft_setup(out var r2, out var d2, "turnip", "turnip", rv: 6, dv: 3);
        Assert.Equal(GraftResult.NoGain, f2.Graft(bag2, r2, d2, 0, out _));       // verici daha dusuk
        Assert.Equal(GraftResult.BadGene, f2.Graft(bag2, r2, d2, 9, out _));
        Assert.Equal(GraftResult.SameLot, f2.Graft(bag2, r2, r2, 0, out _));
        Assert.Equal(2, r2.Count); // hicbir sey harcanmadi
    }

    [Fact]
    public void Asi_tezgahi_is_a_200w_station_placeable_and_recipe()
    {
        var d = Data();
        Assert.Equal(200, d.Research.Watts["asi_tezgahi"]);
        Assert.Contains(d.Tool.Placeables, p => p.Id == "asi_tezgahi" && p.Station == "asi_tezgahi");
        Assert.Contains(d.Tool.Recipes, r => r.Id == "asi_tezgahi" && r.Station == "tezgah");
        Assert.Equal(200, d.Power.ConsumerWatts("asi_tezgahi"));
        Assert.True(d.Crops.Genetics.Graft.Seconds > 0);
    }

    [Fact]
    public void Foods_total_32_with_icerik_values_and_a_recipe_each_reachable_at_t6()
    {
        var d = Data();
        var foods = d.Tool.Foods;
        Assert.Equal(32, foods.Count); // PLAN 7.1 Yemek tarifi
        Assert.Equal(foods.Count, foods.Select(x => x.Id).Distinct().Count());
        var want = new (string id, float hp, float st)[]
        {
            ("menemen", 25, 25), ("bazlama", 10, 30), ("izgara_et", 30, 10), ("salgam_suyu", 5, 15), ("mantar_sote", 30, 20), ("bogurtlen_receli", 15, 35),
            ("gozleme", 30, 30), ("sarimsakli_yogurt", 20, 25), ("findik_ezmesi", 15, 40), ("kavurma", 45, 15), ("ayran", 10, 25),
            ("pilav", 35, 40), ("acili_ezme", 20, 30), ("tursu", 20, 20), ("sucuk", 55, 25), ("lahmacun", 50, 40), ("bal_kaymak", 30, 50),
            ("misir_ekmegi", 40, 50), ("karpuz_peynir", 35, 40), ("kaktus_serbeti", 20, 55), ("kozde_misir", 35, 45), ("guvec", 75, 35),
            ("kar_cayi", 15, 60), ("pekmez", 30, 60), ("pancar_tatlisi", 45, 55), ("baklava", 60, 70),
            ("eksi_maya_ekmegi", 70, 70), ("maya_corbasi", 90, 50), ("kooperatif_ziyafeti", 100, 100),
        };
        foreach (var (id, hp, st) in want)
        {
            var f = foods.Single(x => x.Id == id);
            Assert.Equal((hp, st), (f.MaxHp, f.Stamina));
            Assert.InRange(f.Seconds, 15 * 60, id == "kooperatif_ziyafeti" ? 30 * 60 : 25 * 60); // GDD 3.3; Ziyafet 30 dk
        }
        Assert.Equal(1800f, foods.Single(x => x.Id == "kooperatif_ziyafeti").Seconds); // 30 dk
        Assert.Equal(1f, foods.Single(x => x.Id == "kooperatif_ziyafeti").Effect("table"));
        Assert.Equal(-0.2f, foods.Single(x => x.Id == "salgam_suyu").Effect("sprintCost"));
        Assert.Equal(25f, foods.Single(x => x.Id == "pancar_tatlisi").Effect("chargeAdd"));
        Assert.Equal(2f, foods.Single(x => x.Id == "tursu").Effect("durationMult"));
        Assert.True(foods.Single(x => x.Id == "guvec").Regen > 0);   // Can yenilenmesi
        Assert.True(foods.Single(x => x.Id == "sebze_corbasi").Regen > 0);
        // her yemegin tarifi var ve T6'da ulasilir; yemek tarifi sayisi 32
        var recipes = d.Tool.Recipes.Where(r => r.Category == "yemek").ToList();
        Assert.Equal(32, recipes.Count);
        Assert.Equal(foods.Select(x => x.Id).OrderBy(x => x), recipes.Select(r => r.Id).OrderBy(x => x));
        var g = RecipeGraph.Walk(d, 6);
        foreach (var r in recipes) Assert.True(g.Has(r.Id), "ulasilamayan yemek: " + r.Id);
        Assert.True(g.Has("asi_tezgahi"));
        // ekin urunlerinden yemek yapilir: tarifin girdileri ekin kimligi ya da hayvansal/yag ham kaynagi
        foreach (var r in recipes) foreach (var k in r.In.Keys)
            Assert.True(d.FindCrop(k) != null || d.Research.RawSources.Any(s => s.Id == k) || k == "flour" || k == "egg", $"{r.Id}: {k}");
    }

    [Fact]
    public void Foods_are_cooked_and_eaten_through_the_station()
    {
        var d = Data();
        var s = new GameState(d);
        var q = s.Stations["ocakbasi"];
        var inv = s.Backpack;
        inv.Add("tomato", 1); inv.Add("egg", 1); inv.Add("wood", 2);
        var r = d.Tool.Recipes.Single(x => x.Id == "menemen");
        Assert.True(q.Enqueue(r, inv));
        for (float t = 0; t < 10f; t += 0.5f) q.Tick(0.5f, inv);
        Assert.Equal(1, inv.Count("menemen"));
        Assert.True(s.Food.Eat(d.Tool.Foods.Single(x => x.Id == "menemen")));
        Assert.Equal(25f, s.Food.BonusMaxHp);
        Assert.Equal(25f, s.Food.BonusStamina);
    }
}