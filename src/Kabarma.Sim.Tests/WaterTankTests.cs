using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Sulama deposu (URETIM §6 M1; ana oturum karari 18 Kasim 2026): 100 L, dolu baslar, kare basina 5 L, golden 25 L/sn.</summary>
public class WaterTankTests
{
    static SulamaDef Def => TestData.Load().Tool.Sulama;

    [Fact]
    public void Data_matches_the_decision()
    {
        var d = Def;
        Assert.Equal(100f, d.TankLiters);
        Assert.Equal(5f, d.LitersPerCell);
        Assert.Equal(25f, d.FillPerSecond);
        Assert.True(d.StartFull);
    }

    [Fact]
    public void Starts_full_and_waters_twenty_cells_then_stops()
    {
        var t = new WaterTank(Def);
        Assert.True(t.Full);
        int n = 0;
        while (t.TryWaterCell()) n++;
        Assert.Equal(20, n);
        Assert.False(t.CanWater);
        Assert.Equal(0f, t.Liters);
    }

    [Fact]
    public void Fills_from_the_lake_and_clamps_at_capacity()
    {
        var t = new WaterTank(Def);
        while (t.TryWaterCell()) { }
        Assert.Equal(50f, t.Fill(2f), 3);
        Assert.Equal(50f, t.Fill(10f), 3);
        Assert.True(t.Full);
        Assert.Equal(0f, t.Fill(1f));
        Assert.Equal(0f, t.Fill(-1f));
    }

    [Fact]
    public void First_day_field_fits_one_tank_and_state_starts_full()
    {
        var data = TestData.Load();
        var s = new GameState(data);
        Assert.Equal(s.Tank.Capacity, s.Tank.Liters);
        // Ilk gunun 3 x 3 tarlasi tek depoyla sulanir (bot da oyuncu da gole gitmeden ilk sulamayi yapar).
        Assert.True(9 * s.Tank.PerCell <= s.Tank.Capacity);
        s.Tank.Restore(42f);
        Assert.Equal(42f, s.Tank.Liters);
        s.Tank.Restore(500f);
        Assert.Equal(s.Tank.Capacity, s.Tank.Liters);
    }
}
