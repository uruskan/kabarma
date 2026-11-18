using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Arayuz tasarim sistemi (ui.json; SANAT-SES 8.1, URETIM 6 M1 karari 20): olcek, canta izgarasi, cark, palet.</summary>
public class UiTests
{
    [Theory]
    [InlineData(1600, 900, 1.0f)]      // tasarim tuvali
    [InlineData(1920, 1080, 1.0f)]     // 1080p: 1
    [InlineData(2560, 1440, 1.35f)]    // 1440p: yukseklikle buyur (4/3, yirmide bire yuvarli)
    [InlineData(1280, 720, 0.8f)]      // 720p: tuval sigsin diye kuculur
    [InlineData(3840, 2160, 2.0f)]     // 4K: 2
    public void Scale_follows_the_window_height_and_shrinks_small_windows_so_the_canvas_fits(int w, int h, float expected)
    {
        var d = TestData.Load().Ui;
        Assert.Equal(expected, UiScale.For(d, w, h), 3);
    }

    [Fact]
    public void Player_preference_never_scales_panels_past_what_the_window_can_show()
    {
        var d = TestData.Load().Ui;
        // 720p'de tercih 2: en genis satir (1440 tasarim pikseli) 1280'e sigmali
        float s = UiScale.For(d, 1280, 720, 2f);
        Assert.True(d.Scale.FitWidth * s <= 1280 + 0.01f, $"olcek {s} 1280 genislige sigmiyor");
        Assert.True(UiScale.For(d, 1920, 1080, 1.1f) > UiScale.For(d, 1920, 1080), "Ctrl + arti buyutmeli");
    }

    [Theory]
    [InlineData(20, 5)]  // GDD 5: canta 20 yuva -> 5 x 4
    [InlineData(24, 6)]  // Capa basamagiyla 24, 30, 36, 42, 48 -> 6 sutun, tam satirlar
    [InlineData(30, 6)]
    [InlineData(36, 6)]
    [InlineData(42, 6)]
    [InlineData(48, 6)]
    public void Bag_grid_has_only_full_rows_for_every_bag_size_in_gdd(int slots, int columns)
    {
        var d = TestData.Load().Ui;
        Assert.Equal(columns, UiScale.BagColumns(d, slots));
        Assert.Equal(0, slots % UiScale.BagColumns(d, slots));
    }

    [Fact]
    public void Wheel_lists_six_work_heads_and_two_weapons_and_the_palette_has_every_colour_the_ui_uses()
    {
        var d = TestData.Load().Ui;
        Assert.Equal(8, d.Heads.Count); // Capa.Head: Kazma, Bicme, Toprak, Sulama, Cekim, Insa, Tirpan, Civi
        Assert.All(d.Heads, h => { Assert.False(string.IsNullOrWhiteSpace(h.Title)); Assert.False(string.IsNullOrWhiteSpace(h.Text)); });
        foreach (var key in new[] { "ink", "muted", "red", "green", "panel", "panelHover", "hudSlot", "selected", "selectedEdge", "health", "stamina",
                     "ivory", "brick", "short", "warning", "tooltip", "star", "dialog", "toast", "shade", "locked", "oz", "teal" })
        {
            Assert.True(d.Palette.TryGetValue(key, out var hex), $"palette: {key} yok");
            Assert.Matches("^[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", hex);
        }
        Assert.Equal(8, Belt.Slots); // kemer 8 yuva (GDD 5)
    }
}
