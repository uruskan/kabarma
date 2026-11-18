using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Genler ve ıslah (GDD 8.3): mutasyon, kalitim, caprazlama, Alaca, Tohum Kaydi. Istatistik testleri sabit tohumlu.</summary>
public class GeneticsTests
{
    static GameData Data() => TestData.Load();
    static GeneticsRules R(GameData d) => d.Crops.Genetics;

    [Fact]
    public void Mutation_table_matches_gdd_frequencies()
    {
        var r = R(Data());
        var rng = new Rng(11);
        const int n = 200_000;
        var counts = new Dictionary<int, int>();
        for (int i = 0; i < n; i++) { int k = Genetics.MutationDelta(r, rng); counts[k] = counts.GetValueOrDefault(k) + 1; }
        // %70 degismez, %20 +1, %8 -1, %2 +2; 200k cekimde her pay icin ~4 sigma pay
        Assert.InRange(counts[0] / (double)n, 0.695, 0.705);
        Assert.InRange(counts[1] / (double)n, 0.196, 0.204);
        Assert.InRange(counts[-1] / (double)n, 0.0765, 0.0835);
        Assert.InRange(counts[2] / (double)n, 0.0185, 0.0215);
        Assert.Equal(4, counts.Count);
    }

    [Fact]
    public void Inheritance_keeps_parent_genes_plus_mutation_mean()
    {
        var r = R(Data());
        var rng = new Rng(7);
        var parent = new Genes { V = 5, H = 5, D = 5, M = 5 };
        const int n = 50_000;
        double sum = 0;
        for (int i = 0; i < n; i++) sum += Genetics.Inherit(r, parent, rng).V;
        // beklenen kayma: 0,20 - 0,08 + 0,04 = +0,16
        Assert.InRange(sum / n, 5.16 - 0.01, 5.16 + 0.01);
    }

    [Fact]
    public void Genes_stay_within_1_and_10()
    {
        var r = R(Data());
        var rng = new Rng(3);
        var top = new Genes { V = 10, H = 10, D = 10, M = 10 };
        var low = new Genes { V = 1, H = 1, D = 1, M = 1 };
        for (int i = 0; i < 20_000; i++)
        {
            var a = Genetics.Inherit(r, top, rng);
            var b = Genetics.Inherit(r, low, rng);
            var c = Genetics.Cross(r, top, low, rng);
            for (int g = 0; g < Genes.Count; g++)
            {
                Assert.InRange(a[g], 9, 10);
                Assert.InRange(b[g], 1, 3);
                Assert.InRange(c[g], 4, 8);
            }
        }
    }

    [Fact]
    public void Crossing_averages_parents_and_rounds_halves_evenly()
    {
        var r = R(Data());
        var rng = new Rng(19);
        var a = new Genes { V = 2, H = 4, D = 9, M = 1 };
        var b = new Genes { V = 5, H = 6, D = 9, M = 1 };
        const int n = 60_000;
        double v = 0, h = 0, dd = 0;
        for (int i = 0; i < n; i++) { var c = Genetics.Cross(r, a, b, rng); v += c.V; h += c.H; dd += c.D; }
        Assert.InRange(v / n, 3.66 - 0.015, 3.66 + 0.015); // (2+5)/2 = 3,5 -> 3 ya da 4 yari yariya, +0,16
        Assert.InRange(h / n, 5.16 - 0.015, 5.16 + 0.015); // tam ortalama 5
        // 9 + mutasyon, 10'da kesilir: 0,70*9 + 0,20*10 + 0,08*8 + 0,02*10
        Assert.InRange(dd / n, 9.14 - 0.015, 9.14 + 0.015);
    }

    [Fact]
    public void Alaca_rates_match_gdd()
    {
        var r = R(Data());
        var rng = new Rng(2024);
        const int n = 400_000;
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < n; i++) { var a = Genetics.RollAlaca(r, null, rng); if (a != null) counts[a] = counts.GetValueOrDefault(a) + 1; }
        // beklenen: Benekli 10.000, Altin 1.600, Aurora 200 (yaklasik 4 sigma)
        Assert.InRange(counts["benekli"], 10_000 - 400, 10_000 + 400);
        Assert.InRange(counts["altin"], 1_600 - 160, 1_600 + 160);
        Assert.InRange(counts["aurora"], 200 - 57, 200 + 57);
    }

    [Fact]
    public void Alaca_seed_keeps_its_alaca_half_the_time()
    {
        var r = R(Data());
        var rng = new Rng(5);
        const int n = 40_000;
        int keep = 0;
        for (int i = 0; i < n; i++) if (Genetics.RollAlaca(r, "altin", rng) == "altin") keep++;
        // 0,5 + 0,5 x 1/250
        Assert.InRange(keep / (double)n, 0.502 - 0.01, 0.502 + 0.01);
    }

    [Fact]
    public void Seed_without_record_uses_base_genes_and_record_without_seed_waits()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(20, 99);
        f.Till(0, 0); f.Till(1, 0); f.Till(2, 0);
        // Kayit var, tohum yok: ekilmez, kayit durur
        f.Seeds.Add("tomato", new Genes { V = 7, H = 7, D = 7, M = 7 }, null, 1);
        Assert.False(f.Plant(0, 0, "tomato", bag));
        Assert.Equal(1, f.Seeds.CountFor("tomato"));
        // Tohum gelince once kayit kullanilir, sonra taban genleri
        bag.Add("tomato_seed", 2);
        Assert.True(f.Plant(0, 0, "tomato", bag));
        Assert.Equal(7, f.Get(0, 0)!.Genes.V);
        Assert.True(f.Plant(1, 0, "tomato", bag));
        Assert.Equal(d.Crop("tomato").SeedGenes.V, f.Get(1, 0)!.Genes.V);
        Assert.Equal(0, f.Seeds.CountFor("tomato"));
        Assert.False(f.Plant(2, 0, "tomato", bag));
    }

    [Fact]
    public void Ledger_plants_alaca_first_then_highest_gene_sum()
    {
        var d = Data();
        var led = new SeedLedger();
        led.Add("turnip", new Genes { V = 9, H = 9, D = 9, M = 9 }, null, 1);
        led.Add("turnip", new Genes { V = 1, H = 1, D = 1, M = 1 }, "benekli", 1);
        led.Add("turnip", new Genes { V = 5, H = 5, D = 5, M = 5 }, null, 2);
        led.Add("turnip", new Genes { V = 5, H = 5, D = 5, M = 5 }, null, 1); // ayni satira biner
        Assert.Equal(3, led.Lots.Count);
        Assert.Equal("benekli", led.Take("turnip", R(d))!.Alaca);
        Assert.Equal(36, led.Take("turnip", R(d))!.Genes.Sum);
        Assert.Equal(20, led.Take("turnip", R(d))!.Genes.Sum);
        Assert.Equal(2, led.CountFor("turnip"));
    }

    [Fact]
    public void Harvest_writes_inherited_seeds_to_ledger_and_logs_best()
    {
        var d = Data();
        var f = new Farm(d);
        var bag = new Inventory(20, 99);
        var rng = new Rng(1);
        bag.Add("wheat_seed", 1);
        f.Till(0, 0);
        Assert.True(f.Plant(0, 0, "wheat", bag, rng));
        GrowAll(f, d);
        var h = f.HarvestFull(0, 0, rng)!;
        Assert.Equal(h.Alaca == null ? "wheat" : Genetics.AlacaItem("wheat", h.Alaca), h.Item);
        Assert.Equal(d.Crop("wheat").SeedReturn, h.SeedLots.Sum(l => l.Count));
        Assert.Equal(h.Seeds, f.Seeds.CountFor("wheat"));
        Assert.False(h.Crossed);
        Assert.True(f.Log.Best.ContainsKey("wheat"));
        foreach (var l in h.SeedLots) for (int g = 0; g < Genes.Count; g++) Assert.InRange(l.Genes[g], 1, 4);
    }

    [Fact]
    public void Crossing_needs_same_family_within_2m_and_a_pollinator()
    {
        var d = Data();
        var rng = new Rng(9);
        (Farm f, Inventory bag) Setup(string a, string b, int dist, bool hive)
        {
            var f = new Farm(d);
            var bag = new Inventory(20, 99);
            bag.Add(a + "_seed", 1); bag.Add(b + "_seed", 1);
            f.Till(0, 0); f.Till(dist, 0);
            f.Plant(0, 0, a, bag); f.Plant(dist, 0, b, bag);
            if (hive) f.SetPollinators(new[] { (5f, 5f) });
            GrowAll(f, d);
            return (f, bag);
        }
        Assert.True(Setup("tomato", "blackberry", 1, true).f.HarvestFull(0, 0, rng)!.Crossed);   // Meyve x Meyve, 1 m
        Assert.True(Setup("tomato", "tomato", 2, true).f.HarvestFull(0, 0, rng)!.Crossed);       // tam 2 m
        Assert.False(Setup("tomato", "tomato", 3, true).f.HarvestFull(0, 0, rng)!.Crossed);      // 3 m: uzak
        Assert.False(Setup("tomato", "tomato", 1, false).f.HarvestFull(0, 0, rng)!.Crossed);     // kovan yok
        Assert.False(Setup("tomato", "garlic", 1, true).f.HarvestFull(0, 0, rng)!.Crossed);      // baska aile
        var far = Setup("tomato", "tomato", 1, false);
        far.f.SetPollinators(new[] { (40f, 0f) });
        Assert.False(far.f.HarvestFull(0, 0, rng)!.Crossed);                                     // kovan 12 m disinda
    }

    [Fact]
    public void Alaca_produce_has_its_own_item_and_price()
    {
        var d = Data();
        var price = new Pricing(d);
        Assert.Equal(15f * 10f, price.Base(Genetics.AlacaItem("turnip", "altin")));
        Assert.Equal(30f * 50f, price.Base(Genetics.AlacaItem("pumpkin", "aurora")));
        Assert.Equal(30f, price.Base("pumpkin"));
        Assert.Equal(15f, price.Base("turnip")); // prices.json once
        Assert.Equal(("tomato", "benekli"), Genetics.ParseAlacaItem(d, "tomato_benekli") is { } x ? (x.crop.Id, x.alaca.Id) : default);
    }

    internal static void GrowAll(Farm f, GameData d, float seconds = 400f)
    {
        for (float t = 0; t < seconds; t += 0.5f)
        {
            foreach (var p in f.Plots) if (!p.Wild && p.Planted && p.Moisture < 60) f.Water(p.X, p.Y);
            f.Tick(0.5f, light: true);
        }
    }
}
