using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Borsa, Katalog, Tohum Carki (GDD §13, ICERIK §18; ekonomi2).</summary>
public class MarketTests
{
    static GameData Data() => TestData.Load();

    /// <summary>n XL kabarik Salgam satar ve odemeyi bekler (oyun yolu: rampa).</summary>
    static void Earn(GameState s, int n)
    {
        for (int i = 0; i < n; i++) { s.Sell("turnip", s.Data.Crops.SizeClasses[3]); s.Cargo.Tick(s.Data.Economy.Ramp.PayoutSeconds + 1); }
    }

    [Fact]
    public void Market_has_12_categories_and_every_priced_item_maps_without_throwing()
    {
        var d = Data();
        var m = new Market(d, 1);
        Assert.Equal(12, m.Categories.Count);
        Assert.Equal(12, m.Categories.Select(c => c.Id).Distinct().Count());
        foreach (var id in d.Tool.Prices.Keys.Concat(d.Tool.Processed))
            Assert.Contains(m.Category(id), m.Categories.Select(c => c.Id));
        Assert.Equal("kok", m.Category("turnip"));
        Assert.Equal("yemek", m.Category("sebze_corbasi"));
        Assert.Equal("islenmis", m.Category("hic_olmayan_urun")); // bilinmeyen urun: hata yok, Islenmis Mal
    }

    [Fact]
    public void Market_is_neutral_until_the_first_morning_after_it_opens()
    {
        var d = Data();
        var s = new GameState(d);
        Assert.False(s.Market.State.Open);
        Assert.Equal(1f, s.Market.FactorOf("turnip"));
        s.Calendar.Start(d.Calendar.FirstDay);          // ilk Kabarma safagi: Borsa acilir, fiyat notr
        Assert.True(s.Market.State.Open);
        Assert.Equal("", s.Market.State.Hot);
        Assert.All(s.Market.Categories, c => Assert.Equal(1f, s.Market.Factor(c.Id)));
        Assert.Equal(120, s.Sell("turnip", d.Crops.SizeClasses[0])); // PLAN 1.4: ilk satis 120 Kredi
        s.Market.OnMorning(d.Calendar.FirstDay + 1);    // ilk gun degisimi: fiyatlar oynar, bir kategori sicak
        Assert.NotEqual("", s.Market.State.Hot);
        Assert.Contains(s.Market.Categories, c => Math.Abs(s.Market.State.Mult[c.Id] - 1f) > 1e-4);
    }

    /// <summary>200 oyun gunu, 3 tohum, her gun satis baskisi: patlama yok, fiyatlar sinirlarda, gecmis sinirli.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(4417UL)]
    [InlineData(987654321UL)]
    public void Two_hundred_days_stay_within_bounds(ulong seed)
    {
        var d = Data();
        var mf = d.Market;
        var m = new Market(d, seed);
        m.OnMorning(1);
        var hot = new HashSet<string>();
        float lo = float.MaxValue, hi = float.MinValue;
        for (int day = 2; day <= 201; day++)
        {
            m.OnMorning(day);
            hot.Add(m.State.Hot);
            // Satis baskisi: her gun Kok'ten 8 kasa, Maden'den 2 kasa, karisik bir kasa (Tahil + Yemek).
            for (int k = 0; k < 8; k++) m.RecordSale(new[] { "kok" });
            for (int k = 0; k < 2; k++) m.RecordSale(new[] { "maden" });
            m.RecordSale(new[] { "tahil", "yemek", "tahil" });
            foreach (var c in m.Categories)
            {
                float mult = m.State.Mult[c.Id], sat = m.Saturation(c.Id), f = m.Factor(c.Id);
                Assert.False(float.IsNaN(f) || float.IsInfinity(f));
                Assert.InRange(mult, mf.Min - 1e-5f, mf.Max + 1e-5f);
                Assert.InRange(sat, mf.SaturationFloor - 1e-5f, 1f + 1e-5f);
                Assert.InRange(f, mf.Min * mf.SaturationFloor - 1e-5f, mf.Max * mf.HotMult + 1e-5f);
                Assert.True(m.State.History[c.Id].Count <= mf.HistoryDays);
                lo = Math.Min(lo, mult); hi = Math.Max(hi, mult);
            }
            // Fiyat: taban × kalite × Borsa × doygunluk sinirlarda (Salgam ★3)
            float p = 15f * 1.7f * m.FactorOf("turnip");
            Assert.InRange(p, 15f * 1.7f * mf.Min * mf.SaturationFloor - 1e-3f, 15f * 1.7f * mf.Max * mf.HotMult + 1e-3f);
        }
        Assert.Equal(201, m.State.Day);
        Assert.Equal(mf.HistoryDays, m.State.History["kok"].Count);
        Assert.True(hot.Count >= 8, $"200 gunde yalniz {hot.Count} farkli sicak kategori");
        Assert.True(hi - lo > 0.5f, $"carpan oynamiyor ({lo:0.00}-{hi:0.00})");
        // Tek urune yuklenmek cezalanir: Kok her gun 8 kasayla taban yakininda
        Assert.True(m.Saturation("kok") < 0.6f);
        Assert.True(m.Saturation("meyve") > 0.99f);
    }

    [Fact]
    public void Market_is_deterministic_per_seed_and_state_is_plain()
    {
        var d = Data();
        var a = new Market(d, 42);
        var b = new Market(d, 42);
        for (int day = 1; day <= 30; day++) { a.OnMorning(day); b.OnMorning(day); }
        Assert.Equal(a.State.Hot, b.State.Hot);
        Assert.Equal(a.State.Mult, b.State.Mult);
        // Kayda hazir: durum JSON'a gidip geri gelince ayni fiyat (TEKNIK §9)
        var json = System.Text.Json.JsonSerializer.Serialize(a.State);
        var c = new Market(d, 42, System.Text.Json.JsonSerializer.Deserialize<MarketState>(json));
        foreach (var cat in a.Categories) Assert.Equal(a.Factor(cat.Id), c.Factor(cat.Id), 5);
        a.OnMorning(31); c.OnMorning(31);
        Assert.Equal(a.State.Mult, c.State.Mult);
    }

    [Fact]
    public void Saturation_drops_3_percent_per_crate_to_40_and_recovers_15_each_morning()
    {
        var d = Data();
        var m = new Market(d, 3);
        m.OnMorning(1);
        m.RecordSale(new[] { "kok" });
        Assert.Equal(0.97f, m.Saturation("kok"), 4);
        for (int i = 0; i < 40; i++) m.RecordSale(new[] { "kok" });
        Assert.Equal(0.4f, m.Saturation("kok"), 4);
        m.OnMorning(2);
        Assert.Equal(0.55f, m.Saturation("kok"), 4);
    }

    [Fact]
    public void Crate_price_is_locked_at_launch_and_saturates_its_categories()
    {
        var d = Data();
        var s = new GameState(d);
        s.Calendar.Start(d.Calendar.FirstDay);
        s.Market.OnMorning(d.Calendar.FirstDay + 1);
        s.Sell("turnip", d.Crops.SizeClasses[0]); // ilk satis (aninda) bitsin: kasa 60 sn sonra odenir
        s.Backpack.Add("turnip", 10);
        s.Backpack.Add("egg", 4);
        var c = s.Cargo;
        c.Pack(s.Backpack, "turnip", 10);
        c.Pack(s.Backpack, "egg", 4);
        var crate = c.Close()!;
        c.TakeClosed();
        int expect = (int)Math.Round(10 * 15 * s.Market.FactorOf("turnip") + 4 * 15 * s.Market.FactorOf("egg"), MidpointRounding.AwayFromZero);
        Assert.Equal(expect, c.Value(crate));
        c.LoadCrate(crate);
        int before = s.Credits;
        Assert.Equal(expect, c.LaunchCrates());
        Assert.Equal(0.94f, s.Market.Saturation("kok"), 4); // %3 eksiltme toplamsal
        Assert.Equal(0.97f, s.Market.Saturation("sut"), 4);
        Assert.Equal(before, s.Credits); // henuz odenmedi
        // Ertesi sabah fiyat degisir ama odeme firlatma anindaki fiyattir (kilit; ilk satis aninda odenir)
        s.Market.OnMorning(d.Calendar.FirstDay + 2);
        c.Tick(d.Economy.Ramp.PayoutSeconds + 1);
        Assert.Equal(before + expect, s.Credits);
    }

    // ---------- Katalog ----------

    [Fact]
    public void Catalog_prices_follow_icerik_and_buying_spends_credits_and_delivers_by_capsule()
    {
        var d = Data();
        var s = new GameState(d);
        var seed = s.Catalog.Entry("tohum_salgam")!;
        Assert.Equal(15 * 20, s.Catalog.Price(seed)); // tohum paketi: taban × 20 (ICERIK §18)
        Assert.Equal(400, s.Catalog.Price(s.Catalog.Entry("govde_el_arabasi")!));
        Assert.Null(s.BuyCatalog("tohum_salgam"));     // Kredi yok
        Assert.Empty(s.Catalog.Deliveries);
        s.Sell("turnip", d.Crops.SizeClasses[0]);       // 120
        s.Sell("turnip", d.Crops.SizeClasses[2]);       // L: rampa bos, 60 sn sonra
        s.Cargo.Tick(61);
        int credits = s.Credits;
        Assert.True(credits >= 300);
        var dl = s.BuyCatalog("tohum_salgam");
        Assert.NotNull(dl);
        Assert.Equal(credits - 300, s.Credits);
        Assert.Equal("kapsul", dl!.Via);
        Assert.False(dl.Arrived);
        int seeds = s.Backpack.Count("turnip_seed");
        Assert.Equal(0, s.Catalog.Collect(dl, s.Backpack)); // inmeden acilmaz
        Delivery? landed = null;
        s.Catalog.Arrived += x => landed = x;
        s.Catalog.Tick(d.Catalog.CapsuleSeconds + 0.1f);
        Assert.Same(dl, landed);
        Assert.Equal(10, s.Catalog.Collect(dl, s.Backpack));
        Assert.Equal(seeds + 10, s.Backpack.Count("turnip_seed"));
        Assert.Empty(s.Catalog.Deliveries);
        Assert.Contains(("tohum_salgam", 300), s.Purchases);
    }

    [Fact]
    public void Catalog_hides_entries_whose_requirements_are_missing_and_items_exist()
    {
        var d = Data();
        var s = new GameState(d);
        foreach (var e in d.Catalog.Entries)
        {
            Assert.True(s.Catalog.Price(e) > 0, e.Id);
            Assert.Contains(e.Via, new[] { "kapsul", "rampa" });
            foreach (var id in e.Items.Keys) Assert.True(d.Tool.StackOf(id) > 0, $"{e.Id}: {id}");
            if (e.Load.Length > 0) Assert.Contains(d.Tool.Recipes, r => r.Id == e.Load && r.Load);
        }
        var hidden = new CatalogEntryDef { Id = "x", Price = 10, Requires = { "gidak_yumurtasi_yok" } };
        Assert.False(s.Catalog.Available(hidden));
        Assert.True(s.Catalog.Visible.Any());
    }

    [Fact]
    public void Spend_never_goes_negative()
    {
        var s = new GameState(Data());
        Assert.False(s.Spend(1, "x"));
        Assert.Equal(0, s.Credits);
    }

    // ---------- Tohum Carki ----------

    [Fact]
    public void Wheel_expected_value_is_85_percent_of_the_cost()
    {
        var d = Data();
        var w = new Wheel(d, 1);
        Assert.Equal(500, w.Cost);
        Assert.Equal(100f, w.TotalWeight, 3);
        Assert.InRange(w.ExpectedValue / w.Cost, d.Wheel.TargetEv - 0.01f, d.Wheel.TargetEv + 0.01f);
        Assert.Equal(0.40f, w.Chance(w.Rewards.Single(r => r.Id == "kredi")), 3);
        Assert.Equal(0.005f, w.Chance(w.Rewards.Single(r => r.Id == "alaca_tohum")), 4);
    }

    [Fact]
    public void Wheel_draws_match_the_weights_over_many_spins()
    {
        var d = Data();
        var w = new Wheel(d, 99);
        var n = new Dictionary<string, int>();
        const int N = 40000;
        double value = 0;
        for (int i = 0; i < N; i++)
        {
            var r = w.Pick(i);
            n[r.Id] = n.GetValueOrDefault(r.Id) + 1;
            value += Wheel.ValueOf(r);
        }
        foreach (var r in w.Rewards) Assert.InRange(n.GetValueOrDefault(r.Id) / (double)N, w.Chance(r) * 0.85 - 0.002, w.Chance(r) * 1.15 + 0.002);
        Assert.InRange(value / N / w.Cost, 0.80, 0.90);
    }

    [Fact]
    public void Wheel_first_spin_of_the_day_is_free_then_costs_500()
    {
        var d = Data();
        var s = new GameState(d);
        Assert.True(s.WheelFreeToday);
        var free = s.SpinWheel();
        Assert.NotNull(free);
        Assert.True(free!.Free);
        Assert.False(s.WheelFreeToday);
        int c0 = s.Credits;
        Assert.Null(s.SpinWheel());                       // Kredi yok
        // 500 Kredi kazan
        Earn(s, 2);                                       // 2 XL kabarik Salgam (360 + ~349)
        Assert.True(s.Credits >= 500);
        int before = s.Credits;
        var paid = s.SpinWheel();
        Assert.NotNull(paid);
        Assert.False(paid!.Free);
        Assert.Equal(before - 500 + paid.Credits, s.Credits);
        // Ertesi gun yine bedava
        s.Calendar.Start(d.Calendar.FirstDay);
        Assert.True(s.WheelFreeToday);
        Assert.True(c0 >= 0);
    }

    [Fact]
    public void Wheel_rewards_go_to_wallet_capsule_or_depot()
    {
        var d = Data();
        var s = new GameState(d);
        Earn(s, 6);
        int credited = 0, capsules = 0, tokens = 0;
        for (int i = 0; i < 60 && s.Credits >= 500; i++)
        {
            int before = s.Credits;
            var r = s.SpinWheel()!;
            if (r.Reward.Kind == "credits") { Assert.Contains(r.Credits, r.Reward.Amounts); credited++; }
            if (r.Reward.Kind == "items") capsules++;
            if (r.Reward.Kind == "token") tokens++;
            Assert.Equal(before - r.Paid + r.Credits, s.Credits);
        }
        Assert.Equal(capsules, s.Catalog.Deliveries.Count(x => x.Source.StartsWith("cark:")));
        Assert.Equal(tokens, s.Wheel.State.Tokens.Values.Sum());
        Assert.Equal(s.Wheel.State.Spins, credited + capsules + tokens);
    }
}
