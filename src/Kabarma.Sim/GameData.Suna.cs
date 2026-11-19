namespace Kabarma.Sim;

// suna.json: Suna'nin M6 ogretici replikleri (TR ve EN) ve ogretici tetikleyicileri (M6 ogretici). Metin ceviri anahtariyla
// ("key": suna.<kimlik>) durur; dil akisi anahtari kullanir, burada TR ve EN dogrudan bulunur (eksik dil TR'ye duser).

public sealed class SunaLineDef
{
    /// <summary>Ceviri anahtari: suna.&lt;kimlik&gt;.</summary>
    public string Key { get; set; } = "";
    /// <summary>Seslendirme tonu (ses uretim istegi icin).</summary>
    public string Tone { get; set; } = "";
    public string Tr { get; set; } = "";
    public string En { get; set; } = "";

    public string Text(string lang) => lang == "en" && En.Length > 0 ? En : Tr;
}

public sealed class TutorialDef
{
    public string Id { get; set; } = "";
    /// <summary>biome | station | automation | vehicle | ata | ks | tuzuk | husumet.</summary>
    public string Trigger { get; set; } = "";
    public string Arg { get; set; } = "";
    public string Line { get; set; } = "";
}

public sealed class SunaFile
{
    public float CooldownSeconds { get; set; } = 12f;
    public List<string> AutomationIds { get; set; } = new();
    public Dictionary<string, SunaLineDef> Lines { get; set; } = new();
    public List<TutorialDef> Tutorial { get; set; } = new();

    public string Text(string id, string lang) => Lines.TryGetValue(id, out var l) ? l.Text(lang) : "";
}

public sealed partial class GameData
{
    [DataFile("suna.json")]
    public SunaFile Suna { get; private set; } = new();
}