using System.Linq;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M6 ogretici: suna.json replikleri (TR ve EN, anahtarli) ve ogretici tetikleyicileri.</summary>
public class SunaTests
{
    static readonly string[] Triggers = { "biome", "station", "automation", "vehicle", "ata", "ks", "tuzuk", "husumet" };

    [Fact]
    public void Every_line_has_tr_and_en_text_a_translation_key_and_a_tone()
    {
        var s = TestData.Load().Suna;
        Assert.True(s.Lines.Count >= 12);
        foreach (var (id, l) in s.Lines)
        {
            Assert.False(string.IsNullOrWhiteSpace(l.Tr), $"{id}: TR yok");
            Assert.False(string.IsNullOrWhiteSpace(l.En), $"{id}: EN yok");
            Assert.NotEqual(l.Tr, l.En);
            Assert.Equal("suna." + id, l.Key);
            Assert.False(string.IsNullOrWhiteSpace(l.Tone), $"{id}: ton yok");
            Assert.True(l.Tr.Length < 200 && l.En.Length < 200, $"{id}: kisa yonlendirme olmali");
        }
        Assert.Equal("Hello", new SunaFile { Lines = { ["a"] = new SunaLineDef { Tr = "Merhaba", En = "Hello" } } }.Text("a", "en"));
        Assert.Equal("Merhaba", new SunaFile { Lines = { ["a"] = new SunaLineDef { Tr = "Merhaba" } } }.Text("a", "en")); // eksik dil TR'ye duser
    }

    [Fact]
    public void Tutorial_covers_the_eight_systems_with_known_triggers_and_lines()
    {
        var d = TestData.Load();
        var s = d.Suna;
        Assert.Equal(s.Tutorial.Count, s.Tutorial.Select(t => t.Id).Distinct().Count());
        foreach (var t in s.Tutorial)
        {
            Assert.Contains(t.Trigger, Triggers);
            Assert.True(s.Lines.ContainsKey(t.Line), $"{t.Id}: replik {t.Line} yok");
            if (t.Trigger == "biome") Assert.True(d.Biomes.Biomes.ContainsKey(t.Arg), $"{t.Id}: biyom {t.Arg} yok");
            if (t.Trigger == "ks") Assert.True(int.TryParse(t.Arg, out var n) && n >= 1);
        }
        foreach (var trig in Triggers) Assert.Contains(s.Tutorial, t => t.Trigger == trig);
        Assert.True(s.Tutorial.Count(t => t.Trigger == "biome") >= 5);   // Sis Korusu + 4 yeni biyom
        Assert.True(s.CooldownSeconds >= 5);
        Assert.NotEmpty(s.AutomationIds);
    }

    [Fact]
    public void Tutorial_lines_do_not_clash_with_scenario_line_ids()
    {
        var d = TestData.Load();
        foreach (var id in d.Suna.Lines.Keys) Assert.True(string.IsNullOrEmpty(d.Scenario.Text(id)), $"{id}: scenario.json ile cakisiyor");
    }
}