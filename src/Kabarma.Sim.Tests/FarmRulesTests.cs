using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Salgam tarlasinin kucuk kurallari (URETIM 6 M1): egim siniri, kalite carpanlari, kabarik urunun sonmesi.</summary>
public class FarmRulesTests
{
    static (GameData d, GameState s) New() { var d = TestData.Load(); return (d, new GameState(d)); }

    static void GrowToMature(GameState s, int x, int y)
    {
        s.Farm.Water(x, y);
        while (s.Farm.Get(x, y)!.Stage != CropStage.Mature) { s.Farm.Tick(0.5f, true); s.Farm.Water(x, y); }
    }

    [Fact]
    public void Slope_limit_is_15_degrees_and_strict()
    {
        var (d, s) = New();
        Assert.Equal(15f, d.Crops.MaxSlopeDegrees); // GDD 8.1
        Assert.True(s.Farm.Till(0, 0, 14.9f));
        Assert.False(s.Farm.Till(1, 0, 15f));      // "15 dereceden az olmali"
        Assert.False(s.Farm.Till(2, 0, 30f));
        Assert.Null(s.Farm.Get(1, 0));
        Assert.True(s.Farm.Till(3, 0));            // egimsiz eski yol degismedi
    }

    [Fact]
    public void Quality_multipliers_follow_gdd_13()
    {
        var (_, s) = New();
        Assert.Equal(1.0f, s.Farm.QualityMult(1));
        Assert.Equal(1.3f, s.Farm.QualityMult(2));
        Assert.Equal(1.7f, s.Farm.QualityMult(3));
    }

    [Fact]
    public void Three_stars_need_fertility_70_and_never_dry()
    {
        var d = TestData.Load();
        Assert.Equal(70f, d.Crops.Star3MinFertility); // GDD 8.2
        d.Crops.StartFertility = 70;
        var s = new GameState(d);
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        Assert.Equal(3, s.Farm.Stars(s.Farm.Get(0, 0)!));

        var (_, s2) = New();                       // baslangic bereketi 60 < 70
        s2.Farm.Till(0, 0);
        s2.Farm.Plant(0, 0, "turnip", s2.Backpack);
        Assert.Equal(2, s2.Farm.Stars(s2.Farm.Get(0, 0)!));
        s2.Farm.Tick(1f, true);                    // sulanmamis: nem 0, durma esiginin altina dustu
        Assert.Equal(1, s2.Farm.Stars(s2.Farm.Get(0, 0)!));
    }

    [Fact]
    public void Three_star_yield_uses_quality_17()
    {
        var d = TestData.Load();
        d.Crops.StartFertility = 80;
        var s = new GameState(d);
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        GrowToMature(s, 0, 0);
        var h = s.Farm.Harvest(0, 0, s.Rng)!.Value;
        Assert.Equal(3, h.stars);
        // GDD 8.2: taban x (1 + 0,08 x V) x kalite = 3 x 1,16 x 1,7 = 5,916 -> 5 ya da 6
        var def = d.Crop("turnip");
        float raw = def.BaseYield * (1f + 0.08f * def.SeedGenes.V) * 1.7f;
        Assert.InRange(h.yield, (int)MathF.Floor(raw), (int)MathF.Ceiling(raw));
    }

    [Fact]
    public void Swollen_crop_deflates_one_day_after_dawn_into_double_yield()
    {
        var (d, s) = New();
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        GrowToMature(s, 0, 0);
        s.Farm.SwellMature(ks: 1, bonus: 0);
        var p = s.Farm.Get(0, 0)!;
        Plot? deflated = null;
        s.Farm.Deflated += x => deflated = x;

        // Safaktan once saat islemez.
        s.Farm.Tick(d.Crops.DeflateAfterSeconds * 2, true);
        Assert.NotNull(p.Swollen);

        s.Farm.OnDawn();
        Assert.Equal(1800f, d.Crops.DeflateAfterSeconds); // GDD 8.4 "1 gun", GDD 2: 30 dk
        s.Farm.Tick(d.Crops.DeflateAfterSeconds - 1f, true);
        Assert.NotNull(p.Swollen);
        s.Farm.Tick(2f, true);
        Assert.Null(p.Swollen);
        Assert.True(p.Deflated);
        Assert.Same(p, deflated);
        Assert.Equal(1, s.Farm.MatureCount);       // yeniden olgun ekin, bicilebilir

        var h = s.Farm.Harvest(0, 0, s.Rng)!.Value;
        var def = d.Crop("turnip");
        float raw = def.BaseYield * (1f + 0.08f * def.SeedGenes.V) * s.Farm.QualityMult(h.stars) * 2f;
        Assert.InRange(h.yield, (int)MathF.Floor(raw), (int)MathF.Ceiling(raw));
    }

    [Fact]
    public void Lifted_swollen_crop_does_not_deflate_and_replant_resets()
    {
        var (_, s) = New();
        s.Farm.Till(0, 0);
        s.Farm.Plant(0, 0, "turnip", s.Backpack);
        GrowToMature(s, 0, 0);
        s.Farm.SwellMature(1, 0);
        s.Farm.OnDawn();
        Assert.NotNull(s.Farm.Uproot(0, 0));
        bool any = false;
        s.Farm.Deflated += _ => any = true;
        s.Farm.Tick(5000f, true);
        Assert.False(any);
        Assert.True(s.Farm.Plant(0, 0, "turnip", s.Backpack));
        var p = s.Farm.Get(0, 0)!;
        Assert.False(p.Deflated);
        Assert.Equal(-1f, p.SinceDawn);
    }
}
