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
    public void Slots_carry_quality_only_same_id_and_stars_stack_and_low_stars_are_spent_first()
    {
        var d = Data();
        var inv = new Inventory(6, d.Tool.StackOf);
        Assert.Equal(30, inv.Add("turnip", 30, 1));
        Assert.Equal(30, inv.Add("turnip", 30, 3));
        Assert.Equal(10, inv.Add("turnip", 10, 1));
        // ★1: 40 tek yiginda (50'lik), ★3: 30 ayri yiginda; ayni kimlik farkli ★ ust uste binmez
        Assert.Equal(2, inv.UsedSlots);
        Assert.Equal(70, inv.Count("turnip"));
        Assert.Equal(40, inv.Count("turnip", 1));
        Assert.Equal(30, inv.Count("turnip", 3));
        Assert.Equal(0, inv.Count("turnip", 2));
        // Kalite belirtilmeyen esya ★1
        inv.Add("turnip", 5);
        Assert.Equal(45, inv.Count("turnip", 1));
        Assert.Equal(2, inv.UsedSlots);
        // Harcama butun kalitelerden, once dusuk ★
        Assert.True(inv.Remove("turnip", 50));
        Assert.Equal(0, inv.Count("turnip", 1));
        Assert.Equal(25, inv.Count("turnip", 3));
        Assert.False(inv.Remove("turnip", 26));
        // Yalniz bir kaliteden dusurme
        inv.Add("turnip", 4, 2);
        Assert.True(inv.Remove("turnip", 4, 2));
        Assert.False(inv.Remove("turnip", 1, 2));
        // Tasima kaliteyi korur (Sandik, araba, kasa), once dusuk ★
        inv.Add("turnip", 3, 2);
        var chest = new Inventory(20, d.Tool.StackOf);
        Assert.Equal(10, inv.MoveTo(chest, "turnip", 10));
        Assert.Equal(3, chest.Count("turnip", 2));
        Assert.Equal(7, chest.Count("turnip", 3));
        Assert.Equal(18, inv.Count("turnip", 3));
        // Dolu kumede baska ★ icin yer yok, ayni ★'nin yarim yiginina yer var
        var tight = new Inventory(1, d.Tool.StackOf);
        tight.Add("turnip", 10, 1);
        Assert.Equal(0, tight.Room("turnip", 3));
        Assert.Equal(40, tight.Room("turnip", 1));
        Assert.Equal(0, inv.MoveTo(tight, "turnip", 5)); // cantada yalniz ★3 kaldi
    }

    [Fact]
    public void Crate_value_is_base_times_quality_per_stack()
    {
        var d = Data();
        var s = new GameState(d);
        var c = s.Cargo;
        s.Backpack.Add("turnip", 10, 1);
        s.Backpack.Add("turnip", 10, 2);
        s.Backpack.Add("turnip", 10, 3);
        s.Backpack.Add("egg", 2);
        Assert.Equal(30, c.Pack(s.Backpack, "turnip", 30));
        c.Pack(s.Backpack, "egg", 2);
        var crate = c.Open!;
        Assert.Equal(10, crate.Items.Count("turnip", 3)); // kalite kasaya da gecer
        // 10 × 15 × (1,0 + 1,3 + 1,7) + 2 × 15 = 600 + 30
        Assert.Equal(630, c.Value(crate));
        Assert.Same(crate, c.Close());
        c.TakeClosed();
        Assert.True(c.LoadCrate(crate));
        Assert.Equal(630, c.LaunchCrates());
    }

    [Fact]
    public void Harvest_stars_go_into_the_backpack()
    {
        var d = Data();
        var s = new GameState(d);
        // Hic kurumamis tarla en az ★2 verir (GDD 8.2); yildiz cantaya ayni yigin anahtariyla gecer.
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        s.Farm.Water(0, 0);
        while (s.Farm.Get(0, 0)!.Stage != CropStage.Mature) { s.Farm.Tick(0.5f, true); s.Farm.Water(0, 0); }
        var h = s.Farm.Harvest(0, 0, s.Rng);
        Assert.NotNull(h);
        var (yield, stars, _) = h!.Value;
        Assert.True(stars >= 2);
        s.Backpack.Add("turnip", yield, stars);
        Assert.Equal(yield, s.Backpack.Count("turnip", stars));
        Assert.Equal(15f * d.Tool.Quality[stars - 1], s.Cargo.Pricing.Unit("turnip", stars), 3);
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
        // Bos rampaya kabarik urun: 60 sn sonra. Ilk kabarik satis Kok kategorisini %3 doyurdu (GDD 13 doygunluk): 120 × 0,97.
        Assert.Equal(116, s.Sell("turnip", size));
        Assert.Equal(300, s.Credits);
        c.Tick(60.5f);
        Assert.Equal(416, s.Credits);
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
