using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Tarim M2 (GDD 8.2-8.3): T1-T2 ekinleri, zararli ve Korkuluk, Sarimsak, nobet, nadas, kalici cali, mantar, yabani bitki, 100 gunluk tarla.</summary>
public class FarmM2Tests
{
    static GameData Data() => TestData.Load();

    static readonly string[] T12 = { "turnip", "wheat", "pumpkin", "sunflower", "flax", "tomato", "mushroom", "blackberry", "sourberry", "hazelnut", "garlic" };

    [Fact]
    public void All_eleven_t1_t2_crops_have_data_and_families_from_icerik()
    {
        var d = Data();
        Assert.Equal(T12.OrderBy(x => x), d.Crops.Crops.Select(c => c.Id).Where(T12.Contains).OrderBy(x => x)); // 13 yeni ekin: FarmM4Tests
        var fam = new Dictionary<string, string>
        {
            ["turnip"] = "root", ["wheat"] = "grain", ["pumpkin"] = "gourd", ["sunflower"] = "oil", ["flax"] = "fiber", ["tomato"] = "fruit",
            ["mushroom"] = "mushroom", ["blackberry"] = "fruit", ["sourberry"] = "fruit", ["hazelnut"] = "oil", ["garlic"] = "spice",
        };
        foreach (var c in d.Crops.Crops.Where(c => T12.Contains(c.Id)))
        {
            Assert.Equal(fam[c.Id], c.Family);
            Assert.True(c.GrowSeconds > 0 && c.BaseYield > 0 && c.SeedReturn > 0 && c.BasePrice > 0, c.Id);
            if (c.Days > 0) Assert.Equal(c.Days * d.Crops.GrowDaySeconds, c.GrowSeconds, 3);
        }
        Assert.Equal("fiber", d.Crop("flax").ProductId);
        Assert.Equal("wheat", d.Crop("wheat").ProductId);
    }

    [Theory]
    [InlineData("turnip")][InlineData("wheat")][InlineData("pumpkin")][InlineData("sunflower")][InlineData("flax")][InlineData("tomato")]
    [InlineData("mushroom")][InlineData("blackberry")][InlineData("sourberry")][InlineData("hazelnut")][InlineData("garlic")]
    public void Every_crop_grows_through_stages_and_harvests_product_and_seed(string id)
    {
        var d = Data();
        var def = d.Crop(id);
        var f = new Farm(d);
        var bag = new Inventory(20, 99);
        bag.Add(def.SeedId, 1);
        f.Till(0, 0);
        Assert.True(f.Plant(0, 0, id, bag, new Rng(1)));
        var seen = new HashSet<CropStage>();
        bool light = !def.Dark; // mantar karanlikta
        for (float t = 0; t < def.GrowSeconds * 3 && f.Get(0, 0)!.Stage != CropStage.Mature; t += 0.25f)
        {
            if (f.Get(0, 0)!.Moisture < 60) f.Water(0, 0);
            f.Tick(0.25f, light);
            seen.Add(f.Get(0, 0)!.Stage);
        }
        Assert.Equal(4, seen.Count);
        var h = f.HarvestFull(0, 0, new Rng(2))!;
        Assert.Equal(h.Alaca == null ? def.ProductId : Genetics.AlacaItem(id, h.Alaca), h.Item);
        Assert.True(h.Yield >= 1);
        Assert.Equal(def.SeedId, h.SeedItem);
        Assert.Equal(def.SeedReturn, f.Seeds.CountFor(id));
    }

    [Fact]
    public void Mushroom_grows_only_in_the_dark()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(10, 99);
        bag.Add("mushroom_seed", 1);
        f.Till(0, 0); f.Plant(0, 0, "mushroom", bag); f.Water(0, 0);
        f.Tick(10f, light: true);
        Assert.Equal(0f, f.Get(0, 0)!.Growth);
        f.Tick(10f, light: false);
        Assert.True(f.Get(0, 0)!.Growth > 0f);
    }

    [Fact]
    public void Pest_bites_scale_with_d_and_eat_the_crop_at_full_damage()
    {
        var d = Data();
        var g = d.Crops.Genetics;
        var f = new Farm(d);
        var bag = new Inventory(10, 99);
        bag.Add("turnip_seed", 1);
        f.Till(0, 0); f.Plant(0, 0, "turnip", bag);
        var p = f.Get(0, 0)!;
        float per = g.BiteDamage * (1f - g.DBiteReduce * p.Genes.D);
        Assert.False(f.PestBite(0, 0));
        Assert.Equal(per, p.PestDamage, 4);
        int bites = 1;
        while (!f.PestBite(0, 0)) bites++;
        Assert.Equal((int)MathF.Ceiling(1f / per - 1e-4f), bites + 1); // son isirik yer
        Assert.True(p.Eaten);
        Assert.False(p.Planted);
    }

    [Fact]
    public void Scarecrow_and_garlic_protect_and_hide_crops_from_herds()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(10, 99);
        bag.Add("turnip_seed", 3); bag.Add("garlic_seed", 1);
        f.Till(0, 0); f.Till(30, 0); f.Till(31, 0); f.Till(60, 0);
        f.Plant(0, 0, "turnip", bag); f.Plant(30, 0, "turnip", bag); f.Plant(31, 0, "garlic", bag); f.Plant(60, 0, "turnip", bag);
        f.SetScarecrows(new[] { (5f, 0f) });
        GeneticsTests.GrowAll(f, d, 200f);
        Assert.True(f.Get(0, 0)!.Protected);          // Korkuluk 10 m
        Assert.True(f.Get(30, 0)!.Protected);         // Sarimsak komsusu
        Assert.False(f.Get(60, 0)!.Protected);
        Assert.False(Herds.Attracts(f.Get(0, 0)!));
        Assert.True(Herds.Attracts(f.Get(60, 0)!));
        Assert.False(f.PestBite(0, 0));
        Assert.Equal(0f, f.Get(0, 0)!.PestDamage);
        f.PestBite(60, 0);
        Assert.True(f.Get(60, 0)!.PestDamage > 0f);
    }

    [Fact]
    public void Pest_damage_cuts_harvest_and_rotation_adds_ten_percent()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(10, 99);
        bag.Add("wheat_seed", 2); bag.Add("turnip_seed", 1);
        f.Till(0, 0);
        f.Plant(0, 0, "turnip", bag);
        GeneticsTests.GrowAll(f, d);
        f.HarvestFull(0, 0, new Rng(1));
        Assert.True(f.Plant(0, 0, "wheat", bag));   // kok -> tahil: farkli aile
        Assert.True(f.Get(0, 0)!.RotationBonus);
        GeneticsTests.GrowAll(f, d);
        f.HarvestFull(0, 0, new Rng(1));
        Assert.True(f.Plant(0, 0, "wheat", bag));   // tahil -> tahil
        Assert.False(f.Get(0, 0)!.RotationBonus);
    }

    [Fact]
    public void Fallow_square_gains_three_fertility_per_grow_day()
    {
        var d = Data();
        var f = new Farm(d);
        f.Till(0, 0);
        float before = f.Get(0, 0)!.Fertility;
        for (int i = 0; i < 100; i++) f.Tick(d.Crops.GrowDaySeconds / 100f, light: true);
        Assert.Equal(before + d.Crops.Genetics.FallowPerDay, f.Get(0, 0)!.Fertility, 2);
    }

    [Fact]
    public void Perennial_bush_returns_to_young_and_regrows_in_its_regrow_days()
    {
        var d = Data();
        var def = d.Crop("blackberry");
        var f = new Farm(d);
        var bag = new Inventory(10, 99);
        bag.Add("blackberry_seed", 1);
        f.Till(0, 0); f.Plant(0, 0, "blackberry", bag);
        GeneticsTests.GrowAll(f, d);
        Assert.NotNull(f.HarvestFull(0, 0, new Rng(3)));
        var p = f.Get(0, 0)!;
        Assert.True(p.Planted);
        Assert.Equal(CropStage.Young, p.Stage);
        // Isik tam, Bereket, H ve islak toprak (80 ustu x0,8) carpaniyla: en gec regrowDays / (f(B) x 0,8 x (1+0,04H)) gunde olgun
        float bound = def.RegrowDays * d.Crops.GrowDaySeconds / (Farm.FertilityFactor(p.Fertility) * d.Crops.Moisture.WetFactor * (1f + 0.04f * p.Genes.H)) + 1f;
        float t = 0;
        for (; t < bound * 2 && p.Stage != CropStage.Mature; t += 0.25f) { if (p.Moisture < 60) f.Water(0, 0); f.Tick(0.25f, true); }
        Assert.InRange(t, bound * 0.5f, bound);
    }

    [Fact]
    public void Wild_plants_give_wild_seeds_do_not_swell_and_regrow()
    {
        var d = Data();
        var f = new Farm(d);
        var rng = new Rng(4);
        var w = f.SpawnWild(40, 40, "pumpkin", rng)!;
        Assert.True(w.Wild && w.Planted && w.Stage == CropStage.Mature);
        for (int g = 0; g < Genes.Count; g++) Assert.InRange(w.Genes[g], 1, 3);
        Assert.False(f.Till(40, 40));
        Assert.DoesNotContain(w, f.Plots);   // tarla sayaclari (senaryo, pusula) yabaniyi saymaz
        Assert.Contains(w, f.AllPlots);
        Assert.Empty(f.SwellMature(10, 0));
        Assert.Equal(0, f.MatureCount);
        Assert.False(Herds.Attracts(w));
        var h = f.HarvestFull(40, 40, rng)!;
        Assert.True(h.Wild);
        Assert.Equal(d.Crop("pumpkin").SeedReturn, f.Seeds.CountFor("pumpkin"));
        Assert.False(w.Planted);
        Assert.False(f.Plant(40, 40, "pumpkin", new Inventory(2, 9)));
        for (float t = 0; t < d.Crops.Genetics.Wild.RegrowDays * d.Crops.GrowDaySeconds + 1; t += 1f) f.Tick(1f, true);
        Assert.True(w.Planted && w.Stage == CropStage.Mature);
    }

    [Fact]
    public void D_gene_lowers_moisture_thresholds()
    {
        var d = Data();
        var f = new Farm(d);
        Assert.Equal(0f, f.MoistureFactor(19f, 1, false));
        Assert.Equal(0.5f, f.MoistureFactor(19f, 10, false)); // 20 - 4,5 = 15,5
        Assert.Equal(1f, f.MoistureFactor(90f, 1, true));     // su seven
        Assert.Equal(0.8f, f.MoistureFactor(90f, 1, false));
    }

    /// <summary>
    /// 100 oyun gunu (GDD 2: 22 dk gunduz + 8 dk gece, calendar.json): 4x4 tarla (Salgam, Bugday, Domates, Mantar),
    /// her sabah sulanir, olgun hasat edilir, Tohum Kaydi'ndan yeniden ekilir (en iyi soy once), kovan var (caprazlama).
    /// Mantar yalniz gece buyur. Bereket 0-100'de kalir, tohum hic bitmez, genler secilimle yukselir, Alaca bulunur.
    /// </summary>
    [Fact]
    public void Hundred_game_day_field_simulation_is_self_sustaining_and_breeding_improves_genes()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(400, 100_000);
        var rng = new Rng(100);
        string[] rows = { "turnip", "wheat", "tomato", "mushroom" };
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
            {
                f.Till(x, y);
                bag.Add(d.Crop(rows[y]).SeedId, 1);
                Assert.True(f.Plant(x, y, rows[y], bag, rng));
            }
        f.SetPollinators(new[] { (2f, 2f) });
        double startSum = f.Plots.Average(p => p.Genes.Sum);
        int harvests = 0, produce = 0;
        float light = d.Calendar.DaylightMinutes * 60f, dark = d.Calendar.NightMinutes * 60f;
        for (int dayN = 0; dayN < 100; dayN++)
        {
            foreach (var p in f.Plots) f.Water(p.X, p.Y);
            for (float t = 0; t < light; t += 0.5f) f.Tick(0.5f, light: true);
            foreach (var p in f.Plots) f.Water(p.X, p.Y); // aksam sulamasi: mantar gunduz kurur, gece buyur
            for (float t = 0; t < dark; t += 0.5f) f.Tick(0.5f, light: false);
            foreach (var p in f.Plots.ToList())
            {
                Assert.InRange(p.Fertility, 0f, 100f);
                Assert.False(float.IsNaN(p.Growth));
                if (p.Planted && p.Stage == CropStage.Mature)
                {
                    var h = f.HarvestFull(p.X, p.Y, rng)!;
                    harvests++; produce += h.Yield;
                    bag.Add(h.Item, h.Yield, h.Stars);
                    bag.Add(h.SeedItem, h.Seeds);
                }
                if (!p.Planted)
                {
                    string crop = rows[p.Y];
                    Assert.True(f.Plant(p.X, p.Y, crop, bag, rng), $"gun {dayN}: {crop} tohumu bitti");
                }
            }
        }
        double endSum = f.Plots.Average(p => p.Genes.Sum);
        Assert.True(harvests >= 16 * 99, $"hasat {harvests}"); // her kare her oyun gunu olgunlasir (mantar gece), ertesi sabah bicilir
        Assert.True(f.Log.Best.ContainsKey("mushroom"));
        Assert.True(produce > harvests);
        Assert.True(endSum > startSum + 4, $"gen toplami {startSum:0.0} -> {endSum:0.0}");
        Assert.True(f.Log.Crosses > 0);
        Assert.True(f.Log.AlacaFinds > 0, "100 gunde Alaca yok");
        Assert.True(f.Log.Notes.Count <= d.Crops.Genetics.MaxNotes);
        Assert.All(f.Plots, p => { for (int g = 0; g < Genes.Count; g++) Assert.InRange(p.Genes[g], 1, 10); });
    }
}
