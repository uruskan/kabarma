using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Gun dongusu (GDD 2; URETIM 6 M1 Takvim): ilk geceden sonra isler, normal gece Kabarma'sizdir, uyku geceyi atlatir.</summary>
public class CalendarTests
{
    static GameData Data() => TestData.Load();

    [Fact]
    public void Day_is_30_minutes_22_day_8_night()
    {
        var c = new Calendar(Data().Calendar);
        Assert.Equal(22 * 60f, c.DaylightSeconds, 1);
        Assert.Equal(8 * 60f, c.NightSeconds, 1);
        Assert.Equal(30 * 60f, c.DayLength, 1);
    }

    [Fact]
    public void Clock_does_not_run_before_the_first_kabarma()
    {
        var s = new GameState(Data());
        s.Calendar.Tick(5000f);
        Assert.False(s.Calendar.Running);
        Assert.False(s.Calendar.IsNight);
        Assert.Equal(0f, s.Calendar.Seconds);
    }

    [Fact]
    public void Clock_starts_at_first_dawn_and_pauses_during_a_replayed_kabarma()
    {
        var d = Data();
        var s = new GameState(d);
        var n = s.StartNight();
        CardTests.RunToDawn(n, d);
        Assert.True(s.Calendar.Running);
        Assert.Equal(d.Calendar.FirstDay, s.Calendar.Day);
        Assert.Equal("sabah", s.Calendar.Part.Id);

        s.Calendar.Tick(100f);
        Assert.Equal(100f, s.Calendar.Seconds, 2);
        var replay = s.StartNight(); // F1: Kucuk Kabarma yeniden
        s.Calendar.Tick(100f);
        Assert.Equal(100f, s.Calendar.Seconds, 2); // gece suresince durur
        CardTests.RunToDawn(replay, d);
        s.Calendar.Tick(50f);
        Assert.Equal(150f, s.Calendar.Seconds, 2);
        Assert.Equal(d.Calendar.FirstDay, s.Calendar.Day); // yeniden oynanan gece takvimi ilerletmez
    }

    [Fact]
    public void Normal_night_comes_after_daylight_and_a_new_day_after_it()
    {
        var c = new Calendar(Data().Calendar);
        int dusks = 0, mornings = 0;
        c.Dusk += _ => dusks++;
        c.Morning += _ => mornings++;
        c.Start(2);
        Assert.Equal(1, mornings);
        c.Tick(c.DaylightSeconds - 1f);
        Assert.False(c.IsNight);
        Assert.Equal("aksam", c.Part.Id);
        c.Tick(2f);
        Assert.True(c.IsNight);
        Assert.Equal("gece", c.Part.Id);
        Assert.Equal(1, dusks);
        c.Tick(c.NightSeconds);
        Assert.False(c.IsNight);
        Assert.Equal(3, c.Day);
        Assert.Equal(2, mornings);
    }

    [Fact]
    public void Day_parts_cover_morning_noon_sunset_in_order()
    {
        var c = new Calendar(Data().Calendar);
        c.Start(2);
        var seen = new List<string>();
        for (int i = 0; i < 300; i++)
        {
            c.Tick(c.DayLength / 300f);
            if (seen.Count == 0 || seen[^1] != c.Part.Id) seen.Add(c.Part.Id);
        }
        Assert.Equal(new[] { "sabah", "ogle", "aksam", "gece", "sabah" }, seen.Take(5).ToArray());
    }

    [Fact]
    public void Sleeping_skips_a_normal_night_only()
    {
        var d = Data();
        var s = new GameState(d);
        CardTests.RunToDawn(s.StartNight(), d);
        Assert.False(s.Sleep()); // gunduz uyunmaz
        s.Calendar.Tick(s.Calendar.DaylightSeconds + 10f);
        Assert.True(s.Calendar.IsNight);
        int day = s.Calendar.Day;
        Assert.True(s.Sleep());
        Assert.False(s.Calendar.IsNight);
        Assert.Equal(day + 1, s.Calendar.Day);
        Assert.Equal(0f, s.Calendar.Seconds);
        Assert.Equal(1, s.Calendar.NightsSkipped);
    }

    [Fact]
    public void Kabarma_night_cannot_be_slept_through()
    {
        var c = new Calendar(Data().Calendar);
        c.Start(2);
        c.Tick(c.DaylightSeconds + 5f);
        Assert.False(c.Sleep(kabarmaActive: true));
        Assert.True(c.IsNight);
    }

    [Fact]
    public void Length_scale_is_clamped_to_world_setting_range()
    {
        var def = Data().Calendar;
        def.LengthScale = 5f;
        Assert.Equal(60f * 60f, new Calendar(def).DayLength, 1); // x2 tavan
        def.LengthScale = 0.1f;
        Assert.Equal(15f * 60f, new Calendar(def).DayLength, 1); // x0,5 taban
    }
}
