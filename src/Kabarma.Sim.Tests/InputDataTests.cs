using System.Text.RegularExpressions;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Girdi eslemesi (input.json) ve metindeki tus yer tutuculari ({act:x}). PLAN §7.2 kosul 4: her eylem klavye-fare ve
/// oyun koluyla yapilabilmeli. Belirteclerin Godot'ta cozulmesini --test=input denetler; burada yapi ve gonderimler.
/// </summary>
public class InputDataTests
{
    static InputFile Input() => TestData.Load().Input;

    static readonly HashSet<string> PadTokens = new()
    {
        "A", "B", "X", "Y", "LB", "RB", "L3", "R3", "Back", "Start", "Guide", "LT", "RT",
        "DPad:Up", "DPad:Down", "DPad:Left", "DPad:Right",
        "LS:Up", "LS:Down", "LS:Left", "LS:Right", "RS:Up", "RS:Down", "RS:Left", "RS:Right",
    };

    static readonly Regex KbToken = new(@"^(Mouse:(Left|Right|Middle)|Wheel:(Up|Down)|[A-Za-z0-9][A-Za-z0-9 ]*)$");

    [Fact]
    public void Every_action_is_reachable_with_keyboard_mouse_and_with_gamepad()
    {
        var inp = Input();
        Assert.NotEmpty(inp.Actions);
        var problems = new List<string>();
        foreach (var (id, a) in inp.Actions)
        {
            if (string.IsNullOrWhiteSpace(a.Name)) problems.Add($"{id}: ad yok");
            if (a.Kb.Count == 0 && string.IsNullOrEmpty(a.KbGlyph)) problems.Add($"{id}: klavye-fare baglamasi yok");
            if (a.Pad.Count == 0 && a.PadVia == null) problems.Add($"{id}: oyun kolu baglamasi yok");
            if (a.PadVia != null && (!inp.Actions.TryGetValue(a.PadVia, out var via) || via.Pad.Count == 0))
                problems.Add($"{id}: padVia '{a.PadVia}' kolda baglanmis bir eylem degil");
            foreach (var t in a.Kb) if (!KbToken.IsMatch(t)) problems.Add($"{id}: klavye belirteci '{t}'");
            foreach (var t in a.Pad)
            {
                if (!PadTokens.Contains(t)) problems.Add($"{id}: kol belirteci '{t}'");
                else if (!inp.PadGlyphs.ContainsKey(t)) problems.Add($"{id}: '{t}' icin kol glifi yok");
            }
        }
        foreach (var (id, h) in inp.Hints)
            if (string.IsNullOrEmpty(h.Kb) || string.IsNullOrEmpty(h.Pad)) problems.Add($"ipucu {id}: iki aygitin glifi olmali");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Game_actions_used_by_code_are_mapped()
    {
        // Director.Input.cs ve Player.cs bu eylemleri okur; eksik eylem Godot'ta sessizce "basilmadi" doner.
        string[] used =
        {
            "move_forward", "move_back", "move_left", "move_right", "look_left", "look_right", "look_up", "look_down",
            "jump", "sprint", "interact", "bag", "fire", "head_wheel", "build_next", "build_prev", "build_rotate",
            "card_1", "card_2", "card_3", "ui_accept", "ui_cancel", "ui_up", "ui_down", "controls_panel",
        };
        var inp = Input();
        var missing = used.Where(u => !inp.Actions.ContainsKey(u)).ToList();
        Assert.True(missing.Count == 0, "Eksik eylem: " + string.Join(", ", missing));
        for (int i = 1; i <= 9; i++) Assert.True(inp.Actions.ContainsKey($"head_{i}"), $"head_{i}");
    }

    [Fact]
    public void Scenario_placeholders_name_known_actions()
    {
        var d = TestData.Load();
        var known = d.Input.Actions.Keys.Concat(d.Input.Hints.Keys).ToHashSet();
        var texts = new List<(string where, string text)>();
        // Entegrasyon: ilkgun'un senaryo bicimi. Adim satirlari (goal) ekranda; start/done/intro replik kimligi, metinleri "lines" icinde.
        foreach (var s in d.Scenario.Steps) texts.Add(($"{s.Id}.goal", s.Goal));
        foreach (var (id, l) in d.Scenario.Lines) { texts.Add(($"lines.{id}.tr", l.Tr)); texts.Add(($"lines.{id}.en", l.En)); }
        texts.Add(("nextOrder", d.Scenario.NextOrder));
        var problems = new List<string>();
        foreach (var (where, text) in texts)
        {
            foreach (var a in InputText.Actions(text)) if (!known.Contains(a)) problems.Add($"{where}: bilinmeyen eylem '{a}'");
            foreach (var m in InputText.Malformed(text)) problems.Add($"{where}: bozuk yer tutucu '{m}'");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Expand_replaces_each_placeholder_with_the_glyph()
    {
        string g(string a) => a == "interact" ? "[E]" : a == "bag" ? "Ⓨ" : "?";
        Assert.Equal("Tezgâh'ın yanında [E] ile aç, sonra Ⓨ", InputText.Expand("Tezgâh'ın yanında {act:interact} ile aç, sonra {act:bag}", g));
        Assert.Equal("yer tutucusuz metin", InputText.Expand("yer tutucusuz metin", g));
        Assert.Equal("", InputText.Expand("", g));
    }

    [Fact]
    public void Strip_leaves_a_clean_voice_line()
    {
        Assert.Equal("İnşa başlığına geç.", InputText.Strip("İnşa başlığına geç ({act:head_6})."));
        Assert.Equal("Çantayı aç ve ye.", InputText.Strip("{act:bag}: Çantayı aç ve ye."));
        Assert.Equal("Tuş yok, aynen kalır.", InputText.Strip("Tuş yok, aynen kalır."));
    }

    [Fact]
    public void Malformed_placeholders_are_found()
    {
        Assert.Single(InputText.Malformed("bas {act:Interact} şimdi"));
        Assert.Single(InputText.Malformed("bas {act:interact şimdi"));
        Assert.Empty(InputText.Malformed("bas {act:interact} şimdi"));
        Assert.Equal(new[] { "interact", "bag" }, InputText.Actions("{act:interact} ve {act:bag}"));
    }
}
