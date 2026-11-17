using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Kabarma gecesi: seviye esikleri, kart teklifi, alarm-kabarma-dalga-safak (GDD 14-15).</summary>
public class NightTests
{
    static GameData Data() => TestData.Load();

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
            for (int i = 0; i < due; i++) { n.OnKill(zip, 0, 0); n.CollectNear(0, 0); } // her dogan oyuncunun dibinde olur
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

    // ---- Oz parcaciklari ve Miknatis (URETIM 6 M1 karari 19) ----

    [Fact]
    public void Oz_drops_where_the_enemy_dies_and_is_collected_only_within_the_pickup_radius()
    {
        var d = Data();
        var n = new GameState(d).StartNight();
        var zip = d.Creature("zipzip");
        float r = d.Kabarma.OzPickupRadius;
        Assert.Equal(r, n.PickupRadius, 3);

        var near = n.OnKill(zip, r - 0.1f, 0);
        var far = n.OnKill(zip, 0, r + 0.1f);
        Assert.Equal(0f, n.Oz);
        Assert.Equal(2, n.Motes.Count);
        Assert.Equal(zip.Weight * d.Kabarma.OzPerWeight * n.Gosteri, near.Value, 3);

        Assert.Equal(1, n.CollectNear(0, 0));
        Assert.Equal(near.Value, n.Oz, 3);
        Assert.Same(far, Assert.Single(n.Motes));
        Assert.Equal(0, n.CollectNear(0, 0)); // uzaktaki yerinde bekler
        Assert.Equal(1, n.CollectNear(0, r)); // yanina gidilince toplanir
        Assert.Empty(n.Motes);
        Assert.Equal(near.Value + far.Value, n.Oz, 3);
    }

    [Fact]
    public void Oz_value_is_fixed_at_kill_time_even_if_gosteri_decays_before_pickup()
    {
        var d = Data();
        var g = d.Kabarma.Gosteri;
        var n = new GameState(d).StartNight();
        var zip = d.Creature("zipzip");
        OzMote? last = null;
        for (int i = 0; i < g.StreakKills; i++) last = n.OnKill(zip, 50, 50);
        float boosted = g.Min + g.StreakBonus;
        Assert.Equal(zip.Weight * d.Kabarma.OzPerWeight * boosted, last!.Value, 3);
        n.Tick(30f); // Gosteri tabana duser
        Assert.Equal(g.Min, n.Gosteri);
        n.CollectNear(50, 50);
        float expected = zip.Weight * d.Kabarma.OzPerWeight * (g.Min * (g.StreakKills - 1) + boosted);
        Assert.Equal(expected, n.Oz, 2);
    }

    [Fact]
    public void Miknatis_widens_the_pickup_radius_by_40_percent_per_rank()
    {
        var d = Data();
        var n = new GameState(d).StartNight();
        var zip = d.Creature("zipzip");
        float r = d.Kabarma.OzPickupRadius;
        float per = d.Cards.Cards.First(c => c.Id == "miknatis").PerRank;
        Assert.Equal(0.40f, per, 3); // ICERIK 8.3: toplama menzili +%40

        n.OnKill(zip, r * 1.3f, 0);
        Assert.Equal(0, n.CollectNear(0, 0));
        n.Grant("miknatis");
        Assert.Equal(r * (1 + per), n.PickupRadius, 3);
        Assert.Equal(1, n.CollectNear(0, 0));

        n.Grant("miknatis");
        Assert.Equal(r * (1 + 2 * per), n.PickupRadius, 3);
        n.OnKill(zip, r * 1.7f, 0);
        Assert.Equal(1, n.CollectNear(0, 0));
    }

    [Fact]
    public void Oz_left_on_the_ground_flies_to_the_player_when_the_wave_ends_and_at_dawn()
    {
        var d = Data();
        var s = new GameState(d);
        var n = s.StartNight();
        var zip = d.Creature("zipzip");
        int auto = 0;
        n.OzCollected += (_, a) => { if (a) auto++; };
        while (n.Phase != NightPhase.Waves) n.Tick(0.5f);

        // 1. dalga: hepsi uzakta olur, kimse toplamaz; dalga temizlenince hepsi sayilir
        var w = n.CurrentWave!;
        float dropped = 0;
        for (int k = 0; k < 2000 && n.SpawnedThisWave < w.Count; k++)
        {
            n.Tick(0.1f);
            for (int i = n.SpawnDue(); i > 0; i--) dropped += n.OnKill(zip, 80, 80).Value;
        }
        Assert.Equal(w.Count, n.Motes.Count);
        Assert.Equal(0f, n.Oz);
        n.WaveCleared();
        Assert.Empty(n.Motes);
        Assert.Equal(dropped, n.Oz, 2);
        Assert.Equal(w.Count, auto);

        // Son dalgada yerde kalan Oz, safak suresiyle gelse de kaybolmaz
        int lastWave = d.Kabarma.FirstNight.Waves.Count - 1;
        while (n.WaveIndex < lastWave) { n.Tick(0.5f); if (n.WaveIndex < lastWave) n.SpawnDue(); if (n.Offer != null) n.Choose(0); }
        int last = 0;
        for (int k = 0; k < 100 && last == 0; k++) { n.Tick(0.1f); last = n.SpawnDue(); }
        Assert.True(last > 0);
        float before = n.Oz, lastDrop = 0;
        for (int i = 0; i < last; i++) lastDrop += n.OnKill(zip, -80, 0).Value;
        CardTests.RunToDawn(n, d);
        Assert.Empty(n.Motes);
        Assert.Equal(before + lastDrop, n.Oz, 2);
        Assert.Equal(n.Oz, s.BuildReport().Oz, 2);
    }
}
