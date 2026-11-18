namespace Kabarma.Sim;

// Kayit: save.json (TEKNIK 9, 9.1).

public sealed class SaveFile
{
    /// <summary>Kapsayici dosyanin bicim surumu (bolum surumlerinden ayri).</summary>
    public int FormatVersion { get; set; } = 1;
    public string Root { get; set; } = "user://saves";
    /// <summary>Sinamalarin tek yazabildigi kok (sahibin kaydina dokunulmaz).</summary>
    public string TestRoot { get; set; } = "user://test";
    public string WorldFile { get; set; } = "world.kbs";
    public string ManifestFile { get; set; } = "manifest.json";
    public string CharacterFile { get; set; } = "character.kbs";
    /// <summary>Donen yedek sayisi (world.kbs.1 en yeni).</summary>
    public int Backups { get; set; } = 5;
    /// <summary>Dunya kaydinin ust siniri (TEKNIK 9: 30 MB).</summary>
    public long MaxWorldBytes { get; set; } = 30L * 1024 * 1024;
    public float AutosaveMinutes { get; set; } = 5f;
    /// <summary>Menudeki yuva sayisi.</summary>
    public int Slots { get; set; } = 6;
    public string DefaultSlot { get; set; } = "yuva1";
}

public sealed partial class GameData
{
    [DataFile("save.json")]
    public SaveFile Save { get; private set; } = new();
}
