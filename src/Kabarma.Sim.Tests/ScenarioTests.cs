using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Ilk 10 dakikanin adim zinciri, kurallar duzeyinde (PLAN 1.4).</summary>
public class ScenarioTests
{
    static GameData Data() => TestData.Load();

    /// <summary>Ilk 10 dakikanin butun zinciri, kurallar duzeyinde: kaz, Ocak, sur, ek, sula, buyut, gece, sat.</summary>
    [Fact]
    public void First_ten_minutes_full_chain()
    {
        var d = Data();
        var s = new GameState(d);
        var sc = s.Scenario;
        Assert.True(sc.Is("mine"));
        for (int i = 0; i < 10; i++) { s.Backpack.Add("stone", 1); sc.Report("mine"); }
        sc.Report("ocak");
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) { s.Farm.Till(x, y); sc.Report("till"); }
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) { Assert.True(s.Farm.Plant(x, y, "turnip", s.Backpack)); sc.Report("plant"); }
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) { s.Farm.Water(x, y); sc.Report("water"); }
        Assert.True(sc.Is("grow"));
        float t = 0;
        while (sc.Is("grow") && t < 300) { s.Farm.Tick(0.25f, true); sc.SetProgress("grow", s.Farm.MatureCount); t += 0.25f; }
        Assert.True(sc.Is("night"));
        Assert.InRange(t, 30, 200);

        var n = s.StartNight();
        n.SwellNow += () => s.Farm.SwellMature(n.Ks, 0);
        var zip = d.Creature("zipzip");
        while (n.Phase != NightPhase.Dawn)
        {
            n.Tick(0.1f);
            int due = n.SpawnDue();
            for (int i = 0; i < due; i++) n.OnKill(zip);
            if (n.Offer != null) n.Choose(0);
            if (due > 0) n.WaveCleared();
        }
        sc.Report("night");
        Assert.Equal(9, s.Farm.Plots.Count(p => p.Swollen != null));

        var size = s.Farm.Uproot(1, 1)!;
        s.Sell("turnip", size);
        sc.Report("sell");
        // M1 makine zinciri (kurallar duzeyinde): Tezgah, civi, hasat, firin, corba, paketleme, kasa satisi.
        Assert.True(sc.Is("tezgah_kur"));
        s.Backpack.Add("stone", 30);
        sc.Report("tezgah_kur");
        var tez = s.Stations["tezgah"];
        Assert.True(tez.Enqueue(tez.Recipes.First(), s.Backpack));
        tez.Produced += (r, n) => sc.Report("civi");
        for (int i = 0; i < 30; i++) tez.Tick(0.1f, s.Backpack);
        Assert.True(sc.Is("hasat"));
        int got = 0;
        foreach (var p in s.Farm.Plots.ToList()) { var h = s.Farm.Harvest(p.X, p.Y, s.Rng); if (h != null) { s.Backpack.Add("turnip", h.Value.yield); got += h.Value.yield; } }
        Assert.True(got >= 8 * 6, $"kabarik salgamlar iki kat verir, alinan {got}");
        sc.SetProgress("hasat", s.Backpack.Count("turnip"));
        sc.Report("firin_kur");
        var firin = s.Stations["tas_firin"];
        firin.Enqueue(firin.Recipes.First(), s.Backpack);
        for (int i = 0; i < 50; i++) firin.Tick(0.1f, s.Backpack);
        Assert.True(s.Food.Eat(d.Tool.Foods.First()) && s.Backpack.Remove("sebze_corbasi", 1));
        sc.Report("corba");
        var pak = s.Stations["paketleme"];
        pak.Enqueue(pak.Recipes.First(), s.Backpack);
        for (int i = 0; i < 40; i++) pak.Tick(0.1f, s.Backpack);
        sc.Report("paketle");
        Assert.Equal(90, s.SellItem("kargo_kasasi", 1));
        sc.Report("kasa_sat");
        sc.SetProgress("odun", 18);
        sc.Report("yapi", 4);
        Assert.True(sc.Finished);
        Assert.Equal(120 + 90, s.Credits); // kabarik salgam + kasa
        Assert.Equal(3, s.Backpack.Count("turnip_seed"));
    }
}
