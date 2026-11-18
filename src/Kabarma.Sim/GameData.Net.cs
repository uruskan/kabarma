namespace Kabarma.Sim;

// Ortak oyun: net.json (GDD 22.1, 3.6; TEKNIK 5). Kurallar Net*.cs, Godot katmani game/scripts/net/.

public sealed class NetRangesDef
{
    /// <summary>Kazma istegi: istekte bulunanin konumundan kayanin merkezine en fazla, m.</summary>
    public float Mine { get; set; } = 9f;
    /// <summary>Bicme istegi: nisan noktasina en fazla, m.</summary>
    public float Mow { get; set; } = 9f;
    /// <summary>Yapi kurma, sokme, onarma, yerlestirme: hedefe en fazla, m.</summary>
    public float Build { get; set; } = 14f;
    /// <summary>E ile etkilesim (Sandik, istasyon): en fazla, m.</summary>
    public float Interact { get; set; } = 4.5f;
}

public sealed class NetCooldownsDef
{
    public float Mow { get; set; } = 0.15f;
    public float Place { get; set; } = 0.12f;
}

/// <summary>Bayilma ve sok tokadi (GDD 3.6).</summary>
public sealed class NetDownedDef
{
    public float Seconds { get; set; } = 30f;
    public float ReviveHold { get; set; } = 1.5f;
    public float ReviveHpFraction { get; set; } = 0.3f;
    public float SecondWindFraction { get; set; } = 0.4f;
    public float ReviveRange { get; set; } = 2.5f;
}

/// <summary>Sesli sohbet (GDD 22.1, TEKNIK 5.4).</summary>
public sealed class NetVoiceDef
{
    public int SampleRate { get; set; } = 16000;
    public int FrameMs { get; set; } = 20;
    public float FullVolumeDistance { get; set; } = 4f;
    public float MaxDistance { get; set; } = 35f;
    public float RadioGain { get; set; } = 0.85f;
    /// <summary>Bu tepe genliginin altindaki cerceve gonderilmez (sessizlik).</summary>
    public float Gate { get; set; } = 0.015f;
    public int FrameSamples => Math.Max(1, SampleRate * FrameMs / 1000);
}

public sealed class NetSteamDef
{
    public uint AppId { get; set; } = 480;
    /// <summary>"friends" ya da "private" ya da "public".</summary>
    public string LobbyType { get; set; } = "friends";
}

public sealed class NetFile
{
    public int Protocol { get; set; } = 1;
    public int Port { get; set; } = 27015;
    public int MaxPlayers { get; set; } = 8;
    public float FrameHz { get; set; } = 20f;
    public float CreatureNearHz { get; set; } = 10f;
    public float CreatureFarHz { get; set; } = 2f;
    public float CreatureNearRadius { get; set; } = 120f;
    public float SectionHz { get; set; } = 5f;
    public float DigestSeconds { get; set; } = 10f;
    public float InterpDelay { get; set; } = 0.1f;
    public int PosPerMetre { get; set; } = 256;
    public float TimeoutSeconds { get; set; } = 10f;
    public float ConnectSeconds { get; set; } = 8f;
    public float SpeedSlack { get; set; } = 1.5f;
    public float TeleportLimit { get; set; } = 6f;
    public NetRangesDef Ranges { get; set; } = new();
    public NetCooldownsDef Cooldowns { get; set; } = new();
    public NetDownedDef Downed { get; set; } = new();
    public NetVoiceDef Voice { get; set; } = new();
    public NetSteamDef Steam { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("net.json")]
    public NetFile Net { get; private set; } = new();
}
