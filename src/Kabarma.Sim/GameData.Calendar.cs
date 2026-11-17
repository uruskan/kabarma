namespace Kabarma.Sim;

// Gun dongusu: calendar.json (GDD 2). Is emirleri: orders.json (GDD 13, 24; ICERIK 18). Pano: hud.json (SANAT-SES 8.1).

public sealed class DayPartDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Gunduzun hangi kesrine kadar surer (0..1).</summary>
    public float Until { get; set; } = 1f;
}

public sealed class CalendarFile
{
    public float DaylightMinutes { get; set; }
    public float NightMinutes { get; set; }
    /// <summary>Dunya ayari: gun uzunlugu carpani (GDD 2: x0,5 ile x2 arasi).</summary>
    public float LengthScale { get; set; } = 1f;
    public float LengthScaleMin { get; set; } = 0.5f;
    public float LengthScaleMax { get; set; } = 2f;
    /// <summary>Ilk Kabarma'nin safaginda saat bu gunle baslar.</summary>
    public int FirstDay { get; set; } = 2;
    public List<DayPartDef> Parts { get; set; } = new();
    public DayPartDef NightPart { get; set; } = new();
}

public sealed class OrderStepDef
{
    public string Goal { get; set; } = "";
    public string Measure { get; set; } = "";
    public int Target { get; set; } = 1;
}

public sealed class OrderChainDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public string Measure { get; set; } = "";
    /// <summary>"start": oyun basindan sayar; "defter": Defter acilinca (ilk Hasat Raporu) sayar.</summary>
    public string OpensAt { get; set; } = "defter";
    public string Reward { get; set; } = "";
    public List<OrderStepDef> Steps { get; set; } = new();
}

public sealed class OrderRewardsDef
{
    public List<int> Credits { get; set; } = new();
    public List<int> Veri { get; set; } = new();
}

public sealed class OrdersFile
{
    public int ChainsTotal { get; set; }
    public int StepsPerChain { get; set; }
    public OrderRewardsDef StepRewards { get; set; } = new();
    public List<OrderChainDef> Chains { get; set; } = new();
}

public sealed class HudFile
{
    /// <summary>Pusulanin gosterdigi yatay aci (derece).</summary>
    public float CompassFovDegrees { get; set; }
    /// <summary>Senaryo adimi -> pusuladaki etkin hedefin turu (Director.Compass.cs cozer).</summary>
    public Dictionary<string, string> StepTargets { get; set; } = new();
    /// <summary>Hedef turu -> pusulada adi.</summary>
    public Dictionary<string, string> TargetNames { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("calendar.json")]
    public CalendarFile Calendar { get; private set; } = new();

    [DataFile("orders.json")]
    public OrdersFile Orders { get; private set; } = new();

    [DataFile("hud.json")]
    public HudFile Hud { get; private set; } = new();
}
