using System.Globalization;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Dunya verisi (biomes.json, graphics.json) belgelerdeki kurallarla ayni mi: URETIM 6 M1 (300 m, gol 60-90 m, 8 bitki,
/// 4 kaya, kaynaklar), SANAT-SES §2 (gun saatleri) ve §9 (4 yer imi), TEKNIK §10 ("yuksek" on ayar).
/// Arazinin kendisi Godot'ta olculur (--test=world).
/// </summary>
public class BiomesDataTests
{
    static readonly GameData D = TestData.Load();

    [Fact]
    public void World_is_1800m_with_a_640m_core_and_lake_is_60_to_90m_from_ocak()
    {
        var w = D.Biomes.World;
        Assert.Equal(1800, w.Terrain.Size); // M4 ara adimi (TEKNIK 7, biyom akisi); M2 cekirdegi (coreSize 640) ve M1 kutusu ±146 m area ile korunur
        Assert.Equal(640, w.Terrain.CoreSize);
        var l = w.Lake;
        float c = MathF.Sqrt(l.Center[0] * l.Center[0] + l.Center[1] * l.Center[1]);
        float reach = l.Radius + l.ShoreNoise;
        Assert.Equal(new List<float> { 60, 90 }, l.OcakDistance);
        Assert.True(c - reach >= 60 && c + reach <= 90, $"gol {c - reach:0.0}-{c + reach:0.0} m");
        // Sig: oyuncu icinde yurur (SANAT-SES 3.1 "suda yurume" klibi).
        Assert.InRange(l.Depth, 0.3f, 1.2f);
    }

    [Fact]
    public void Four_times_of_day_plus_normal_night_match_the_art_table()
    {
        var t = D.Biomes.Current.Times;
        foreach (var k in new[] { "sabah", "ogle", "gunbatimi", "kabarma_gecesi", "normal_gece" }) Assert.True(t.ContainsKey(k), k);
        // SANAT-SES §2: gunes yuksekligi ve renkler.
        Assert.Equal(12, t["sabah"].LightElevation);
        Assert.Equal(62, t["ogle"].LightElevation);
        Assert.Equal(4, t["gunbatimi"].LightElevation);
        Assert.Equal(35, t["kabarma_gecesi"].MoonElevation);
        Assert.Equal(35, t["normal_gece"].MoonElevation);
        Assert.Equal(("5F98CF", "F4D2AE", "FFD6A6"), (t["sabah"].SkyTop, t["sabah"].SkyHorizon, t["sabah"].LightColor));
        Assert.Equal(("3E7DC2", "D7E2E2", "FFF5E4"), (t["ogle"].SkyTop, t["ogle"].SkyHorizon, t["ogle"].LightColor));
        Assert.Equal(("3A4A86", "F48A52", "FF8C4A", "C98466"), (t["gunbatimi"].SkyTop, t["gunbatimi"].SkyHorizon, t["gunbatimi"].LightColor, t["gunbatimi"].Fog));
        Assert.Equal(("1A2238", "5FF0C8", "A9B8E8", "2A3358"), (t["kabarma_gecesi"].SkyTop, t["kabarma_gecesi"].SkyHorizon, t["kabarma_gecesi"].LightColor, t["kabarma_gecesi"].Fog));
        Assert.Equal(("1A2238", "2E3E66"), (t["normal_gece"].SkyTop, t["normal_gece"].SkyHorizon));
        // Sabah sisi ogle sisinin 1,5 kati; gunesli saatlerde gunes diski var, gecelerde yok.
        Assert.Equal(t["ogle"].FogDensity * 1.5f, t["sabah"].FogDensity, 4);
        Assert.Equal(0, t["kabarma_gecesi"].SunDisk);
        Assert.True(t["kabarma_gecesi"].Aurora > 0 && t["normal_gece"].Aurora == 0);
        // Gece karanlik degil mavidir: en koyu deger #1A2238 (SANAT-SES §2).
        foreach (var (k, v) in t)
            foreach (var hex in new[] { v.SkyTop, v.SkyHorizon, v.Fog })
                Assert.True(Luma(hex) >= Luma("1A2238") - 0.001f, $"{k} {hex} #1A2238'den koyu");
    }

    [Fact]
    public void Four_bookmarks_in_order()
    {
        Assert.Equal(new[] { "kapsul", "us", "gol", "tepe", "gecis", "koru", "sazlik", "kanyon", "ayaz", "gobek" }, D.Biomes.World.Bookmarks.Select(b => b.Id).ToArray()); // M1 dordu + M2 iki biyom (dunya2) + M4 dort biyom (biyom akisi)
        Assert.All(D.Biomes.World.Bookmarks, b => Assert.Equal(68f, b.Fov)); // oyun kamerasi (Player.cs)
    }

    [Fact]
    public void Eight_plants_four_rocks_and_m1_resources()
    {
        var b = D.Biomes.Current;
        var decor = b.Decor.Select(d => d.Id).ToHashSet();
        // 8 bitki: cayir agaci ve Salgam (altin; Salgam tarlada) + 6 sus modeli. 4 kaya: cevherli kaya (altin) + 3.
        foreach (var id in new[] { "agac", "cali", "bugday", "cicek", "ot", "turuncu_agac", "egrelti", "kaya_duz", "kaya_dik" }) Assert.Contains(id, decor);
        var nodes = b.Nodes.ToDictionary(n => n.Id);
        Assert.Equal("stone", nodes["kaya"].Item);
        Assert.Equal("clay", nodes["kil"].Item);
        Assert.Equal("copper_ore", nodes["bakir"].Item);
        Assert.Contains("bakir", nodes["bakir"].Model);
        Assert.True(nodes["kil"].ShoreCount > 0, "kil gol kiyisinda");
        // Bicme: ot ve cali Lif, yabani bugday Bugday (ortak kimlikler, CLAUDE.md).
        Assert.Equal("fiber", b.Decor.First(d => d.Id == "ot").Harvest);
        Assert.Equal("fiber", b.Decor.First(d => d.Id == "cali").Harvest);
        Assert.Equal("wheat", b.Decor.First(d => d.Id == "bugday").Harvest);
        Assert.All(b.Decor.Where(d => d.Harvest != ""), d => Assert.True(d.Yield > 0, d.Id));
    }

    [Fact]
    public void High_preset_is_the_one_in_teknik_10()
    {
        var g = D.Graphics.Preset("yuksek");
        Assert.Equal((1920, 1080, 1.0f, 0, true), (g.Width, g.Height, g.Scale, g.Msaa, g.Smaa));
        Assert.Equal((4, 4096, 110f), (g.ShadowSplits, g.ShadowAtlas, g.ShadowDistance));
        Assert.True(g.Ssao && g.SsaoHalf && g.Glow && g.DistanceFog && g.HeightFog);
        Assert.False(g.Sdfgi || g.Ssil || g.Ssr || g.VolumetricFog || g.Vsync);
        Assert.Equal((1.0f, 22f, 50f, 0), (g.PlantDensity, g.GrassRadius, g.GrassFar, g.MaxFps));
        Assert.Equal(11.1f, D.Graphics.Perf.BudgetMs);
        Assert.Equal(60f, D.Graphics.Perf.NoonSeconds);
    }

    static float Luma(string hex)
    {
        int v = int.Parse(hex, NumberStyles.HexNumber);
        return (0.2126f * ((v >> 16) & 255) + 0.7152f * ((v >> 8) & 255) + 0.0722f * (v & 255)) / 255f;
    }
}
