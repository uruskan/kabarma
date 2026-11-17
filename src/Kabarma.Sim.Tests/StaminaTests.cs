using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Dayaniklilik kurali (GDD §3.2) ve verisi (tool.json player.stamina).</summary>
public class StaminaTests
{
    static Stamina Make() => new(TestData.Load().Tool.Player.Stamina);

    static void Run(Stamina s, float seconds, bool sprint, float dt = 0.05f)
    {
        for (float t = 0; t < seconds - 1e-4f; t += dt) s.Tick(dt, sprint);
    }

    [Fact]
    public void Data_matches_gdd()
    {
        var d = TestData.Load().Tool.Player.Stamina;
        Assert.Equal(100f, d.Max);      // GDD §3.1
        Assert.Equal(8f, d.SprintDrain); // GDD §3.2: saniyede 8
        Assert.Equal(15f, d.Regen);      // 1 sn sonra saniyede 15
        Assert.Equal(1f, d.RegenDelay);
        Assert.Equal(20f, d.UnlockAt);   // sifirda depar 20'ye kadar kapali
        Assert.InRange(d.MinSpeed, 0.01f, 2f); // GDD'de yok: durdugu halde depar harcamasin
    }

    [Fact]
    public void Sprint_drains_eight_per_second()
    {
        var s = Make();
        Run(s, 2.5f, true);
        Assert.Equal(80f, s.Value, 2);
        Assert.True(s.Sprinting);
    }

    [Fact]
    public void Regen_waits_one_second_then_fills_fifteen_per_second()
    {
        var s = Make();
        Run(s, 5f, true);                 // 60
        Run(s, 1f, false);
        Assert.Equal(60f, s.Value, 2);    // 1 sn bekleme
        Run(s, 2f, false);
        Assert.Equal(90f, s.Value, 2);    // 2 sn x 15
        Run(s, 5f, false);
        Assert.Equal(100f, s.Value, 2);   // tavan
    }

    [Fact]
    public void Walking_without_sprint_never_drains()
    {
        var s = Make();
        Run(s, 30f, false);
        Assert.Equal(100f, s.Value);
        Assert.False(s.Exhausted);
    }

    [Fact]
    public void Empty_locks_sprint_until_twenty()
    {
        var s = Make();
        int ticks = 0;
        while (!s.Exhausted && ticks++ < 1000) s.Tick(0.05f, true);
        Assert.True(s.Exhausted);
        Assert.Equal(0f, s.Value, 2);
        Assert.InRange(ticks * 0.05f, 12.4f, 12.6f); // 100 / 8 = 12,5 sn
        // Tus basili kalsa da depar yok; 1 sn sonra dolmaya baslar.
        Run(s, 1f, true);
        Assert.False(s.Sprinting);
        Assert.Equal(0f, s.Value, 2);
        Run(s, 1f, true);                 // 15
        Assert.True(s.Exhausted);
        Assert.False(s.Sprinting);
        Run(s, 0.4f, false);              // 21 -> kilit acilir
        Assert.False(s.Exhausted);
        s.Tick(0.05f, true);
        Assert.True(s.Sprinting);
    }

    [Fact]
    public void Short_taps_restart_the_regen_delay()
    {
        var s = Make();
        Run(s, 1f, true);                 // 92
        Run(s, 0.8f, false);              // bekleme bitmedi
        Run(s, 0.5f, true);               // 88, bekleme sifirlanir
        Run(s, 0.8f, false);
        Assert.Equal(88f, s.Value, 2);
    }

    [Fact]
    public void Regen_multiplier_and_bonus_max_apply()
    {
        var s = Make();
        s.BonusMax = 20f;
        s.RegenMult = 1.25f;              // Dinlenmis (GDD §3.4)
        Run(s, 5f, true);                 // 100 - 40 = 60
        Run(s, 3f, false);                // 2 sn x 15 x 1,25 = 37,5
        Assert.Equal(97.5f, s.Value, 2);
        Run(s, 5f, false);
        Assert.Equal(120f, s.Value, 2);
    }
}
