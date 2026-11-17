namespace Kabarma.Sim;

// Ilk gunun kurgusu: scenario.json (PLAN 1.4, GDD 24, SANAT-SES 10.3).
// Suna'nin replikleri kimlikle durur (lines: kimlik -> tr, en); adimlar ve gece olaylari kimlik verir.
// Seslendirilen replikte tus adi yoktur; tuslar adimin hedef satirinda {act:eylem} yer tutucusuyla yazilir.

/// <summary>Suna'nin bir repligi, iki dilde (SANAT-SES 10.3). Ses dosyasi kimlikle adlanir.</summary>
public sealed class LineDef
{
    public string Tr { get; set; } = "";
    public string En { get; set; } = "";
}

public sealed class StepDef
{
    public string Id { get; set; } = "";
    public int Target { get; set; }
    /// <summary>Ekrandaki adim satiri (seslendirilmez). {act:eylem} yer tutuculari girdi glifiyle cizilir.</summary>
    public string Goal { get; set; } = "";
    /// <summary>Adim baslarken Suna'nin repligi (kimlik) ya da null.</summary>
    public string? Start { get; set; }
    /// <summary>Adim bitince Suna'nin repligi (kimlik) ya da null.</summary>
    public string? Done { get; set; }
    /// <summary>Adimin ilk 10 dakikadan sonraki ogretici adimlardan olup olmadigi (GDD 24: 10-16).</summary>
    public bool AfterTen { get; set; }
    /// <summary>
    /// Adimin dayandigi tarif ya da yerlestirilebilir kimlikleri. Biri veride yoksa adim yuklemede devre disi kalir
    /// (Scenario.Skipped); veri gelince kendiliginden acilir.
    /// </summary>
    public List<string> Requires { get; set; } = new();
}

/// <summary>Inis kapsulunun kapak acilisi (PLAN 1.4, 0:00): saniyeler ve aci.</summary>
public sealed class HatchDef
{
    public float Delay { get; set; }
    public float OpenSeconds { get; set; }
    public float OpenDegrees { get; set; }
    public float HoldSeconds { get; set; }
    public float ExitSeconds { get; set; }
    public float CloseSeconds { get; set; }
}

public sealed class ScenarioFile
{
    /// <summary>Kapak acilinca Suna'nin giris replikleri (kimlik).</summary>
    public List<string> Intro { get; set; } = new();
    public List<StepDef> Steps { get; set; } = new();
    /// <summary>Gece olayi -> replik kimligi (alarm, swell, wave, level, gidak).</summary>
    public Dictionary<string, string> NightLineIds { get; set; } = new();
    /// <summary>Hasat Raporu'ndaki sabit satir (ekranda; seslendirilmez).</summary>
    public string NextOrder { get; set; } = "";
    /// <summary>Defter acilirken Suna'nin repligi (kimlik).</summary>
    public string? NextOrderLine { get; set; }
    public int OrdersTotal { get; set; }
    public HatchDef Hatch { get; set; } = new();
    /// <summary>Ilk gece dalgalar agilin yonunden bu acinin icinden girer, derece (GDD 24).</summary>
    public float WaveSpreadDegrees { get; set; }
    /// <summary>Ilk gece Kucuk Kabarma ekin evresine bakmadan butun ekinleri kabartir (GDD 24, FirstDayRules).</summary>
    public bool FirstNightSwellsAll { get; set; }
    /// <summary>Butun replikler: kimlik -> Turkce ve Ingilizce metin.</summary>
    public Dictionary<string, LineDef> Lines { get; set; } = new();

    /// <summary>Replik metni; kimlik yoksa bos. lang: "tr" ya da "en".</summary>
    public string Text(string? id, string lang = "tr") =>
        id != null && Lines.TryGetValue(id, out var l) ? (lang == "en" ? l.En : l.Tr) : "";

    /// <summary>Gece olayi -> Turkce metin (gece akisi dogrudan metni okur).</summary>
    public Dictionary<string, string> NightLines => NightLineIds.ToDictionary(kv => kv.Key, kv => Text(kv.Value));

    /// <summary>Ekrandaki metnin replik kimligi (ses akisi, metinle cagrilan Hud.Say icin). Yoksa null.</summary>
    public string? IdOf(string text) => Lines.FirstOrDefault(kv => kv.Value.Tr == text || kv.Value.En == text).Key;
}

public sealed partial class GameData
{
    [DataFile("scenario.json")]
    public ScenarioFile Scenario { get; private set; } = new();

    /// <summary>Adimin Requires listesinden tariflerde ve yerlestirilebilirlerde olmayan kimlikler.</summary>
    public List<string> MissingFor(StepDef step) =>
        step.Requires.Where(id => Tool.Recipes.All(r => r.Id != id) && Tool.Placeables.All(p => p.Id != id)).ToList();
}
