using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

public class SimTests
{
    static GameData Data() => GameData.Load(f => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", f)));

    [Fact]
    public void Data_loads_and_cards_all_have_known_effects()
    {
        var d = Data();
        Assert.Single(d.Crops.Crops);
        Assert.Equal(10, d.Cards.Cards.Count);
        var night = new GameState(d).StartNight();
        // Her kart uygulanabilmeli: bilinmeyen etki istisna atar.
        foreach (var _ in d.Cards.Cards)
        {
            night.PendingLevels.Enqueue(1);
            night.TryOpenOffer();
            Assert.NotNull(night.Choose(0));
        }
    }

    [Fact]
    public void Inventory_respects_slots_and_stacks()
    {
        var inv = new Inventory(2, 50);
        Assert.Equal(100, inv.Add("stone", 130));
        Assert.Equal(0, inv.Add("wood", 1));
        Assert.True(inv.Remove("stone", 60));
        Assert.Equal(40, inv.Count("stone"));
        Assert.False(inv.Remove("stone", 41));
    }

    [Fact]
    public void Dry_crop_does_not_grow_and_watered_crop_matures()
    {
        var d = Data();
        var s = new GameState(d);
        s.Farm.Till(0, 0);
        s.Farm.Till(1, 0);
        Assert.True(s.Farm.Plant(0, 0, "turnip", s.Backpack));
        Assert.True(s.Farm.Plant(1, 0, "turnip", s.Backpack));
        s.Farm.Water(0, 0);
        for (int i = 0; i < 60 * 200; i++) s.Farm.Tick(1f / 60f, light: true);
        Assert.Equal(CropStage.Mature, s.Farm.Get(0, 0)!.Stage);
        Assert.Equal(CropStage.Seed, s.Farm.Get(1, 0)!.Stage);
    }

    [Fact]
    public void Moisture_factor_follows_gdd_bands()
    {
        var f = new Farm(Data());
        Assert.Equal(0f, f.MoistureFactor(10));
        Assert.Equal(0.5f, f.MoistureFactor(30));
        Assert.Equal(1f, f.MoistureFactor(60));
        Assert.Equal(0.8f, f.MoistureFactor(90));
    }

    [Fact]
    public void Swollen_turnip_at_ks1_is_small_and_sells_for_120()
    {
        var d = Data();
        var s = new GameState(d);
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        s.Farm.Water(0, 0);
        while (s.Farm.Get(0, 0)!.Stage != CropStage.Mature) s.Farm.Tick(0.5f, true);
        var swollen = s.Farm.SwellMature(ks: 1, bonus: 0);
        Assert.Single(swollen);
        Assert.Equal("S", swollen[0].Swollen!.Id);
        var size = s.Farm.Uproot(0, 0)!;
        Assert.Equal(120, s.Sell("turnip", size)); // PLAN 1.4: "+120 Kredi"
    }

    [Fact]
    public void Level_thresholds_follow_20_n_pow_1_5()
    {
        var n = new GameState(Data()).StartNight();
        Assert.Equal(20f, n.Threshold(1), 3);
        Assert.Equal(56.57f, n.Threshold(2), 1);
    }

    [Fact]
    public void Card_offer_has_three_distinct_cards()
    {
        var n = new GameState(Data()).StartNight();
        for (int i = 0; i < 50; i++)
        {
            var o = n.RollOffer();
            Assert.Equal(3, o.Count);
            Assert.Equal(3, o.Select(c => c.Id).Distinct().Count());
        }
    }

    [Fact]
    public void Night_runs_alarm_swell_waves_dawn()
    {
        var d = Data();
        var s = new GameState(d);
        var n = s.StartNight();
        bool swelled = false, dawn = false;
        int waves = 0, spawned = 0;
        n.SwellNow += () => swelled = true;
        n.DawnNow += () => dawn = true;
        n.WaveStarted += (_, _) => waves++;
        var zip = d.Creature("zipzip");
        for (float t = 0; t < 600 && !dawn; t += 0.1f)
        {
            n.Tick(0.1f);
            int due = n.SpawnDue();
            spawned += due;
            for (int i = 0; i < due; i++) n.OnKill(zip); // her dogan aninda olur (bot)
            if (n.Offer != null) n.Choose(0);
            if (due > 0) n.WaveCleared();
        }
        Assert.True(swelled);
        Assert.True(dawn);
        Assert.Equal(3, waves);
        Assert.Equal(d.Kabarma.FirstNight.Waves.Sum(w => w.Count), spawned);
        Assert.Equal(d.Kabarma.FirstNight.MaxCardLevels, n.Level);
        Assert.Equal(n.Level, n.CardsCount);
        Assert.True(n.Maya > 0);
    }

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
        Assert.True(sc.Finished);
        Assert.Equal(120, s.Credits);
        Assert.Equal(3, s.Backpack.Count("turnip_seed"));
    }
}
