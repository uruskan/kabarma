using System.Text.Json;

namespace Kabarma.Sim;

// steam.json (M6 steam akisi): basarim, bulut ve zengin durum ayarlari. Godot katmani game/scripts/steam/.

public sealed class SteamAchievementsDef
{
    public string ApiPrefix { get; set; } = "";
    public float SyncSeconds { get; set; } = 3f;
    public float StatsWaitSeconds { get; set; } = 10f;
}

public sealed class SteamCloudDef
{
    public bool Enabled { get; set; } = true;
    public long MaxFileBytes { get; set; } = 31457280;
    public List<string> SkipNames { get; set; } = new();
    public float PullTimeoutSeconds { get; set; } = 8f;
}

public sealed class SteamPresenceDef
{
    public float Seconds { get; set; } = 15f;
    public string Format { get; set; } = "{biome} · KS {ks} · {players} oyuncu · {phase}";
    public string PhaseDay { get; set; } = "Gündüz";
    public string PhaseNight { get; set; } = "Kabarma gecesi";
    public Dictionary<string, string> BiomeNames { get; set; } = new();
}

public sealed class SteamFile
{
    public SteamAchievementsDef Achievements { get; set; } = new();
    public string LedgerFile { get; set; } = "user://steam_state.json";
    public SteamCloudDef Cloud { get; set; } = new();
    public SteamPresenceDef Presence { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("steam.json")]
    public SteamFile Steam { get; private set; } = new();
}

/// <summary>Yerel basarim defteri: Steam olsun olmasin her acilis yazilir; Steam'e iletilmeyenler bekler.</summary>
public sealed class SteamLedger
{
    public Dictionary<string, long> Unlocked { get; set; } = new();
    public List<string> Synced { get; set; } = new();
    /// <summary>Bulutta guncel olan yerel dosyalar: goreli yol -> yuklenen degisim zamani (unix sn).</summary>
    public Dictionary<string, long> CloudPushed { get; set; } = new();

    /// <summary>Yeni acilis: ilk kez ise true.</summary>
    public bool Record(string id, long nowUnix)
    {
        if (Unlocked.ContainsKey(id)) return false;
        Unlocked[id] = nowUnix;
        return true;
    }

    public IEnumerable<string> Pending() => Unlocked.Keys.Where(k => !Synced.Contains(k)).OrderBy(k => Unlocked[k]).ThenBy(k => k, StringComparer.Ordinal);

    public void MarkSynced(string id) { if (!Synced.Contains(id)) Synced.Add(id); }

    public string ToJson() => JsonSerializer.Serialize(this);
    public static SteamLedger FromJson(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<SteamLedger>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}

public static class SteamPresence
{
    public static string BiomeName(SteamFile f, string biomeId) => f.Presence.BiomeNames.GetValueOrDefault(biomeId, biomeId);

    /// <summary>Zengin durum satiri: biyom, KS, oyuncu sayisi, gunduz/gece.</summary>
    public static string Text(SteamFile f, string biomeId, int ks, int players, bool night) =>
        f.Presence.Format.Replace("{biome}", BiomeName(f, biomeId)).Replace("{ks}", Math.Max(1, ks).ToString())
            .Replace("{players}", Math.Max(1, players).ToString()).Replace("{phase}", night ? f.Presence.PhaseNight : f.Presence.PhaseDay);
}

/// <summary>Bulut karari (saf): yerel ve bulut degisim zamanina gore ne yapilir.</summary>
public enum CloudAction { None, Pull, Push }

public static class SteamCloudRules
{
    /// <summary>
    /// localTime/cloudTime unix sn, 0 = yok. Bulutta olup yerelde olmayan ya da bulutta daha yeni olan iner; yerelde olup
    /// bulutta olmayan ya da yerelde daha yeni (ve daha once yuklenmemis) olan cikar. pushedTime: son yuklenen yerel zaman.
    /// </summary>
    public static CloudAction Decide(long localTime, long cloudTime, long pushedTime)
    {
        if (localTime == 0 && cloudTime == 0) return CloudAction.None;
        if (localTime == 0) return CloudAction.Pull;
        if (cloudTime == 0) return CloudAction.Push;
        if (cloudTime > localTime + 1 && cloudTime > pushedTime) return CloudAction.Pull;
        if (localTime > pushedTime && localTime >= cloudTime - 1) return CloudAction.Push;
        return CloudAction.None;
    }

    /// <summary>Bulut adi: kayit kokune gore yol ("saves/" onekli); atlanacak ad parcalari yuklenmez.</summary>
    public static bool Eligible(string rel, long bytes, SteamCloudDef c) =>
        bytes > 0 && bytes <= c.MaxFileBytes && !c.SkipNames.Any(s => rel.Contains(s, StringComparison.OrdinalIgnoreCase));
}
