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
}
