using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Defter ve is emirleri (GDD 13, 24; ICERIK 18; PLAN 1.4): Ciraklik, siradaki is emri, Defter'in acilisi.</summary>
public class OrdersTests
{
    static GameData Data() => TestData.Load();

    [Fact]
    public void Data_follows_icerik_18()
    {
        var o = Data().Orders;
        Assert.Equal(240, o.ChainsTotal * o.StepsPerChain);
        Assert.Equal(new[] { 100, 400, 1500, 6000, 25000 }, o.StepRewards.Credits);
        Assert.Equal(new[] { 10, 20, 40, 70, 100 }, o.StepRewards.Veri);
        Assert.Equal("ciraklik", o.Chains[0].Id);
        Assert.All(o.Chains, c => Assert.Equal(o.StepsPerChain, c.Steps.Count));
        Assert.Equal(new[] { 5, 25, 100, 400, 1500 }, o.Chains.Single(c => c.Id == "salgam_baronu").Steps.Select(s => s.Target));
    }

    [Fact]
    public void Ciraklik_counts_from_the_start_and_marks_steps_out_of_order()
    {
        var d = Data();
        var s = new GameState(d);
        var c = s.Orders.Chain("ciraklik")!;
        var done = new List<int>();
        s.Orders.StepDone += (ch, i) => { if (ch == c) done.Add(i); };
        Assert.True(c.Opened);

        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) { s.Farm.Till(x, y); s.Farm.Plant(x, y, "turnip", s.Backpack); }
        Assert.True(c.StepDone(0)); // ilk tarla: 9 ekim

        CardTests.RunToDawn(s.StartNight(), d); // ilk Kabarma, ilk kargodan once
        Assert.True(c.StepDone(3));
        Assert.Equal(1, c.CurrentStep);  // siradaki: ilk hayvan
        Assert.Equal(new[] { 0, 3 }, done);
    }

    [Fact]
    public void First_sale_marks_ilk_kargo()
    {
        var d = Data();
        var s = new GameState(d);
        var item = d.Tool.Prices.Keys.First();
        s.Backpack.Add(item, 1);
        Assert.False(s.Orders.Chain("ciraklik")!.StepDone(2));
        Assert.True(s.SellItem(item, 1) > 0);
        Assert.True(s.Orders.Chain("ciraklik")!.StepDone(2));
    }

    [Fact]
    public void Salgam_baronu_counts_only_after_the_defter_opens()
    {
        var d = Data();
        var s = new GameState(d);
        var b = s.Orders.Chain("salgam_baronu")!;
        Assert.False(b.Opened);
        GrowAndSwell(s, 9, 0);              // ilk gecenin 9 kabarik salgami: Defter henuz yok
        Assert.Equal(0, b.Count("swollen_crop"));
        Assert.Equal("ciraklik", s.Orders.Tracked!.Def.Id);

        s.Orders.OpenDefter();             // ilk Hasat Raporu
        Assert.Equal("salgam_baronu", s.Orders.Tracked!.Def.Id);
        Assert.Equal("5 kabarık ürün yetiştir", s.Orders.Tracked.Current!.Goal); // PLAN 1.4: "Siradaki"
        GrowAndSwell(s, 5, 10);
        Assert.True(b.StepDone(0));
        Assert.Equal(1, b.CurrentStep);
        Assert.Equal(5, b.Progress(0));
    }

    [Fact]
    public void Night_counts_feed_the_harvest_report()
    {
        var d = Data();
        var s = new GameState(d);
        var n = s.StartNight();
        GrowAndSwell(s, 4, 0);
        s.Farm.Eat(0, 0);
        n.RecordPlayerDown();
        n.RecordAnimalSwell();
        var zip = d.Creature("zipzip");
        for (int i = 0; i < 12; i++) n.OnKill(zip, 0, 0);
        n.CollectNear(0, 0);
        while (n.Offer != null) n.Choose(0);
        var (hit, _) = n.RollHit(d.Tool.Weapon, false, 1f);
        CardTests.RunToDawn(n, d);
        var item = d.Tool.Prices.Keys.First();
        s.Backpack.Add(item, 2);
        int sold = s.SellItem(item, 2);

        var r = s.BuildReport();
        Assert.Equal(1, r.NightNumber);
        Assert.Equal(d.Kabarma.FirstNight.Ks, r.Ks);
        Assert.Equal(12, r.Kills);
        Assert.Equal(n.Maya, r.Maya);
        Assert.Equal(n.Level, r.Level);
        Assert.Equal(d.Kabarma.FirstNight.MaxCardLevels, r.MaxLevel);
        Assert.Equal(n.CardsCount, r.Cards.Sum(c => c.Rank));
        Assert.Contains(r.Cards, c => c.Id == "catal"); // ilk teklifin ilki Catal, Choose(0)
        Assert.True(sold > 0);
        Assert.Equal(4, r.SwollenTotal);
        Assert.Equal(1, r.CropsEaten);
        Assert.Equal(1, r.PlayerDowns);
        Assert.Equal(1, r.AnimalsSwollen);
        Assert.True(r.DamageDealt >= hit);
        Assert.Equal(sold, r.SoldCredits);
        Assert.Equal(r.WavesTotal, r.WavesCleared);
        Assert.Same(r, s.LastReport);
    }

    [Fact]
    public void Gosteri_rises_on_a_kill_streak_decays_and_its_peak_reaches_the_report()
    {
        // GDD 14.5: 3 saniyede 5 oldurme carpani artirir, zamanla duser; Maya ve Oz carpanla carpilir (GDD 14.4, 15).
        var d = Data();
        var g = d.Kabarma.Gosteri;
        var s = new GameState(d);
        var n = s.StartNight();
        var zip = d.Creature("zipzip");
        Assert.Equal(g.Min, n.Gosteri);
        for (int i = 0; i < g.StreakKills - 1; i++) n.OnKill(zip);
        Assert.Equal(g.Min, n.Gosteri);
        float mayaBefore = n.Maya;
        n.OnKill(zip); // seri tamam: bu oldurme artmis carpanla sayilir
        float boosted = g.Min + g.StreakBonus;
        Assert.Equal(boosted, n.Gosteri, 3);
        float perKill = zip.Weight * d.Kabarma.MayaPerWeight * (1f + d.Kabarma.MayaKsFactor * n.Ks);
        Assert.Equal(perKill * boosted, n.Maya - mayaBefore, 2);

        n.Tick(2f);
        Assert.Equal(boosted - g.DecayPerSecond * 2f, n.Gosteri, 3);
        n.Tick(60f);
        Assert.Equal(g.Min, n.Gosteri);

        CardTests.RunToDawn(n, d);
        Assert.Equal(boosted, s.BuildReport().PeakGosteri, 3);
    }

    /// <summary>n kareyi ekip olgunlastirir ve kabartir (x0 sutunundan baslayarak).</summary>
    static void GrowAndSwell(GameState s, int n, int x0)
    {
        var f = s.Farm;
        s.Backpack.Add("turnip_seed", n);
        for (int i = 0; i < n; i++) { f.Till(x0 + i, 0); f.Plant(x0 + i, 0, "turnip", s.Backpack); f.Water(x0 + i, 0); }
        for (int k = 0; k < 2000 && f.MatureCount < f.Plots.Count(p => p.Planted && p.Swollen == null); k++) f.Tick(1f, true);
        f.SwellMature(1, 0);
    }
}
