namespace Kabarma.Sim;

// Cekisme dunyasi sayilari: rivalry.json (GDD 22.2; M5 cekisme).

public sealed class RivalryFile
{
    public int TeamsMin { get; set; } = 2;
    public int TeamsMax { get; set; } = 4;
    public int PlayersPerTeamMax { get; set; } = 4;
    /// <summary>Iki takimin Ocak'lari arasi en az (m).</summary>
    public float OcakMinDistance { get; set; } = 500f;
    public int ListenMaxTeams { get; set; } = 2;
    public int ListenMaxPlayers { get; set; } = 8;
    public int DedicatedMaxTeams { get; set; } = 4;
    public int DedicatedMaxPlayers { get; set; } = 16;
    /// <summary>Tapu yaricapi verilmediginde (m).</summary>
    public float PlotRadiusFallback { get; set; } = 60f;
    public bool OpenPvpDefault { get; set; }
    /// <summary>Acik arazide oldurulenden dusen rastgele yigin sayisi.</summary>
    public int PvpLootStacks { get; set; } = 3;
    /// <summary>Gecenin Maya'sinin Acik Silo'ya giren payi.</summary>
    public float SiloMayaShare { get; set; } = 0.3f;
    /// <summary>Enkaz planini tek tusla kurmanin bedel orani.</summary>
    public float RebuildCostFraction { get; set; } = 0.5f;
    public List<string> IndestructibleShapes { get; set; } = new();
    /// <summary>Sezon gun secenekleri; 0 sonsuz.</summary>
    public List<int> SeasonOptions { get; set; } = new();
    public int SeasonDefault { get; set; } = 14;
    public bool HoldingCampsDefault { get; set; }
    /// <summary>Ihracata hazir bir esyanin puan degeri (adet basina).</summary>
    public int ExportMayaValue { get; set; } = 1;
    public List<string> LootableKinds { get; set; } = new();
    public List<string> ProtectedKinds { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("rivalry.json")]
    public RivalryFile Rivalry { get; private set; } = new();
}
