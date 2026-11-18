namespace Kabarma.Sim;

// Tam Kabarma (GDD 14.1-14.6, kabarma3): kabarma.json "full" bolumu. Ilk gecenin elle yazili NightDef'i (firstNight) ayri kalir.

/// <summary>Elit oneki (GDD 14.3): carpanlar ve ozel etki. Sifir/bos alan etkisizdir.</summary>
public sealed class EliteDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Hp { get; set; } = 1f;
    public float Speed { get; set; } = 1f;
    public float Damage { get; set; } = 1f;
    public float Scale { get; set; } = 1f;
    public float Armor { get; set; }
    public int SplitInto { get; set; }
    public float DeathBlast { get; set; }
    public float HealRadius { get; set; }
    public float HealPerSecond { get; set; }
    public float PullRadius { get; set; }
}

public sealed class KazanDef
{
    public List<float> HpByTier { get; set; } = new();
    public float RoutDamage { get; set; }
    public float ToppleMayaLoss { get; set; }
    public bool RepairAtDawn { get; set; } = true;
}

public sealed class SmallKabarmaDef
{
    public float Radius { get; set; }
    public float Seconds { get; set; }
    public float KsFactor { get; set; }
    public int MaxCardLevels { get; set; }
    public int WaveCount { get; set; }
}

public sealed class DrillDef
{
    public bool Rewards { get; set; }
    public bool FreeRepair { get; set; }
}

public sealed class FullKabarmaDef
{
    public float AlarmSeconds { get; set; }
    public float SwellSeconds { get; set; }
    public int WaveCount { get; set; }
    public float WaveSeconds { get; set; }
    public float GapSeconds { get; set; }
    public float DorukSeconds { get; set; }
    public float CountBase { get; set; }
    public float CountPerKs { get; set; }
    public float PlayerFactor { get; set; }
    public float PlayerExponent { get; set; }
    public int MaxActive { get; set; }
    public float EliteMax { get; set; }
    public float ElitePerKs { get; set; }
    public float MixPlanted { get; set; }
    public float MixNative { get; set; }
    public float MixNeighbor { get; set; }
    public float NoCropCountMult { get; set; }
    public float NoCropMayaMult { get; set; }
    public int DorukEvery { get; set; }
    public float DorukEliteShare { get; set; }
    public int FirstClearCardTokens { get; set; }
    public int FirstClearCredits { get; set; }
    public int MaxKs { get; set; }
    /// <summary>GDD 2: varsayilan her N. gece Kabarma (Can ayrica).</summary>
    public int EveryDays { get; set; } = 4;
    /// <summary>Ekin ailesi -> o ailenin cektigi turler (ICERIK 5). Bos liste: pay yerlilere.</summary>
    public Dictionary<string, List<string>> Families { get; set; } = new();
    /// <summary>Biyom -> yerli turler (ICERIK 3).</summary>
    public Dictionary<string, List<string>> Natives { get; set; } = new();
    public List<EliteDef> Elites { get; set; } = new();
    public KazanDef Kazan { get; set; } = new();
    public SmallKabarmaDef Small { get; set; } = new();
    public DrillDef Drill { get; set; } = new();
    /// <summary>Denge simulasyonunun onerilen donanimi (GECICI yer tutucu, WaveBalance.cs).</summary>
    public BalanceDef Balance { get; set; } = new();
}
