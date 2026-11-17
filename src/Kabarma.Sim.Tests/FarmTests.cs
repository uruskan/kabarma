using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Tarla: nem, buyume, kabarma boyu, hasat (GDD 8).</summary>
public class FarmTests
{
    static GameData Data() => TestData.Load();

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
    public void Mature_turnip_harvests_with_quality_and_seeds_and_replants()
    {
        var d = Data();
        var s = new GameState(d);
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        s.Farm.Water(0, 0);
        while (s.Farm.Get(0, 0)!.Stage != CropStage.Mature) { s.Farm.Tick(0.5f, true); s.Farm.Water(0, 0); }
        var h = s.Farm.Harvest(0, 0, s.Rng);
        Assert.NotNull(h);
        Assert.Equal(2, h!.Value.stars); // nem hic dusmedi, bereket 60 < 70
        Assert.InRange(h.Value.yield, 3, 5);
        Assert.Null(s.Farm.Harvest(0, 0, s.Rng));
        Assert.True(s.Farm.Plant(0, 0, "turnip", s.Backpack)); // ayni kareye yeniden ekilir
    }
}
