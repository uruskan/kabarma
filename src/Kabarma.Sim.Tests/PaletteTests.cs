using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// palette.json (uretim hatti, stil foyu) ile oyunun Palette.cs'i ayni renkleri tasir (SANAT-SES §2).
/// Palette.cs metin olarak okunur: taban renkler new("RRGGBB"), turetilmis tonlar A.Lerp(B, t).
/// </summary>
public class PaletteTests
{
    static readonly Dictionary<string, string> CsToJson = new()
    {
        ["Ground"] = "altin_cayir.ground", ["Plant"] = "altin_cayir.plant", ["Accent"] = "altin_cayir.accent",
        ["Shadow"] = "altin_cayir.shadow", ["Fog"] = "altin_cayir.fog", ["SkyNoon"] = "altin_cayir.sky",
        ["SkySunset"] = "altin_cayir.sky_sunset",
        ["OffWhite"] = "neutral.off_white", ["CoopBlue"] = "neutral.coop_blue", ["Warning"] = "neutral.warning",
        ["DarkGrey"] = "neutral.dark_grey", ["Wood"] = "neutral.wood", ["HoldingRed"] = "neutral.holding_red",
        ["MayaTeal"] = "maya.teal", ["MayaViolet"] = "maya.violet", ["NightDeep"] = "maya.night",
        ["Soil"] = "tones.soil", ["SoilWet"] = "tones.soil_wet", ["TurnipBody"] = "tones.turnip_body",
        ["TurnipTop"] = "tones.turnip_top", ["Rock"] = "tones.rock", ["RockDark"] = "tones.rock_dark",
    };

    [Fact]
    public void Palette_json_matches_Palette_cs()
    {
        var cs = File.ReadAllText(Path.Combine(RepoDir(), "game", "scripts", "fx", "Palette.cs"));
        var colors = new Dictionary<string, double[]>();
        // Taban: Name = new("RRGGBB")  /  Turetilmis: Name = A.Lerp(B, 0.55f) ya da new Color("RRGGBB").Lerp(B, 0.15f)
        foreach (Match m in Regex.Matches(cs, @"Color (\w+) = (?:new(?: Color)?\(""([0-9A-Fa-f]{6})""\)|(\w+))(?:\.Lerp\((\w+), ([0-9.]+)f\))?;"))
        {
            var name = m.Groups[1].Value;
            var a = m.Groups[2].Success ? Hex(m.Groups[2].Value) : colors[m.Groups[3].Value];
            if (m.Groups[4].Success)
            {
                var b = colors[m.Groups[4].Value]; var t = double.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture);
                a = a.Zip(b, (x, y) => x + (y - x) * t).ToArray();
            }
            colors[name] = a;
        }
        using var doc = JsonDocument.Parse(TestData.Read("palette.json"));
        var sets = doc.RootElement.GetProperty("sets");
        Assert.Equal(CsToJson.Keys.OrderBy(k => k), colors.Keys.Where(CsToJson.ContainsKey).OrderBy(k => k));
        foreach (var (csName, path) in CsToJson)
        {
            var parts = path.Split('.');
            var json = Hex(sets.GetProperty(parts[0]).GetProperty(parts[1]).GetString()!);
            var diff = json.Zip(colors[csName], (x, y) => Math.Abs(x - y) * 255).Max();
            Assert.True(diff <= 1.0, $"{csName} ({path}): palette.json {Fmt(json)} / Palette.cs {Fmt(colors[csName])}");
        }
    }

    static double[] Hex(string h) => new[] { 0, 2, 4 }.Select(i => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber) / 255.0).ToArray();
    static string Fmt(double[] c) => string.Concat(c.Select(x => ((int)Math.Round(x * 255)).ToString("X2")));

    static string RepoDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "game", "project.godot"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }
}
