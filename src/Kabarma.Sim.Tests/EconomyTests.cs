using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Kargo (GDD 13; URETIM 6 M1 karari 14), fiyat (ICERIK 18), insa kurallari (GDD 7; M1 karari 3).</summary>
public class EconomyTests
{
    static GameData Data() => TestData.Load();

    [Fact]
    public void Prices_are_base_times_quality_and_processed_goods_from_inputs()
    {
        var p = new Pricing(Data());
        Assert.Equal(15f, p.Base("turnip"));
        Assert.Equal(15f, p.Base("egg"));
        Assert.Equal(0f, p.Base("stone"));           // satilmaz
        Assert.Equal(45f, p.Base("copper_ingot"));   // 2 × 15 × 1,5 (yakit fiyatsiz)
        Assert.Equal(45f, p.Base("sebze_corbasi"));  // 2 × 15 × 1,5
        Assert.Equal(22.5f, p.Base("kozde_salgam"));
        Assert.Equal(45f * 1.5f / 20f, p.Base("civi"), 3);
        Assert.Equal(0f, p.Base("kargo_kasasi"));    // kasanin kendi fiyati yok: degeri icerigi
        Assert.Equal(15f * 1.3f, p.Unit("turnip", 2), 3);
        Assert.Equal(15f * 1.7f, p.Unit("turnip", 3), 3);
    }

    [Fact]
    public void Crate_holds_20_stacks_weighs_25_kg_and_is_worth_its_contents()
    {
        var d = Data();
        var s = new GameState(d);
        var c = s.Cargo;
        s.Backpack.Add("turnip", 40);
        s.Backpack.Add("stone", 10);
        Assert.Equal(0, c.Pack(s.Backpack, "stone", 10)); // satilmayan mal kasaya girmez
        Assert.Equal(40, c.Pack(s.Backpack, "turnip", 40));
        Assert.Equal(0, s.Backpack.Count("turnip"));
        var crate = c.Open!;
        Assert.Equal(25f, crate.Kg);
        Assert.Equal(20, crate.Items.Slots);
        Assert.Equal(600, c.Value(crate));
        // 20 yigin dolunca fazlasi cantada kalir
        var big = new Inventory(40, d.Tool.StackOf);
        big.Add("turnip", 50 * 21);
        c.Pack(big, "turnip", 50 * 21);
        Assert.Equal(20, crate.Items.UsedSlots);
        Assert.Equal(1050 - (10 + 19 * 50), big.Count("turnip")); // 20 yigindan fazlasi disarida kalir
        Assert.Same(crate, c.Close());
        Assert.Null(c.Open);
        Assert.Same(crate, c.TakeClosed());
        Assert.Null(c.Close()); // bos kasa kapanmaz
    }

    [Fact]
    public void Ramp_takes_1_to_6_crates_or_one_swollen_crop_and_pays_after_60s_except_the_first_sale()
    {
        var d = Data();
        var s = new GameState(d);
        var c = s.Cargo;
        var size = d.Crops.SizeClasses[0];
        // Ilk satis (ogretici): kabarik S Salgam 15 × 8 = 120, aninda.
        Assert.Equal(120, s.Sell("turnip", size));
        Assert.Equal(120, s.Credits);
        // Rampa: en fazla 6 kasa
        var crates = new List<CargoCrate>();
        for (int i = 0; i < 7; i++)
        {
            s.Backpack.Add("egg", 2);
            c.Pack(s.Backpack, "egg", 2);
            crates.Add(c.Close()!);
            c.TakeClosed();
        }
        for (int i = 0; i < 6; i++) Assert.True(c.LoadCrate(crates[i]));
        Assert.False(c.LoadCrate(crates[6]));
        // Rampada kasa varken kabarik urun alinmaz (ikisi birden degil)
        Assert.False(c.CanShipSwollen);
        Assert.Equal(-1, s.Sell("turnip", size));
        Assert.Equal(6 * 30, c.LaunchCrates());
        Assert.Empty(c.Pad);
        Assert.Equal(120, s.Credits); // henuz odenmedi
        Assert.Equal(0, c.Tick(59f));
        Assert.Equal(120, s.Credits);
        Assert.Equal(180, c.Tick(1.01f));
        Assert.Equal(300, s.Credits);
        // Bos rampaya kabarik urun: 60 sn sonra
        Assert.Equal(120, s.Sell("turnip", size));
        Assert.Equal(300, s.Credits);
        c.Tick(60.5f);
        Assert.Equal(420, s.Credits);
        Assert.Equal(0, c.LaunchCrates()); // bos rampa firlatmaz
    }

    [Fact]
    public void Deconstruct_refunds_all_repair_costs_a_quarter_of_the_damage_and_hp_stops_at_10_percent()
    {
        var r = Data().Economy.Build;
        var wall = new Dictionary<string, int> { ["wood"] = 4 };
        Assert.Equal(4, Structures.Refund(wall, r)["wood"]);
        var tezgah = new Dictionary<string, int> { ["wood"] = 10, ["stone"] = 5 };
        Assert.Equal(tezgah, Structures.Refund(tezgah, r));
        Assert.Empty(Structures.RepairCost(wall, 400, 400, r));
        Assert.Equal(1, Structures.RepairCost(wall, 200, 400, r)["wood"]);  // 4 × %25 × %50 = 0,5 -> 1
        Assert.Equal(1, Structures.RepairCost(wall, 40, 400, r)["wood"]);   // 4 × %25 × %90 = 0,9 -> 1
        var temel = new Dictionary<string, int> { ["wood"] = 54 };
        Assert.Equal(13, Structures.RepairCost(temel, 40, 400, r)["wood"]); // 54 × %25 × %90 = 12,15 -> 13
        // Hasarla yikilma yok: %10'da durur
        Assert.Equal(300f, Structures.ApplyDamage(400, 400, 100, r));
        Assert.Equal(40f, Structures.ApplyDamage(400, 400, 1000, r));
        Assert.Equal(40f, Structures.ApplyDamage(40, 400, 10, r));
        Assert.True(Structures.InClaim(30, 20, 40));
        Assert.False(Structures.InClaim(30, 30, 40));
    }
}
