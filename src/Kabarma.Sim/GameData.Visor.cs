namespace Kabarma.Sim;

// Vizor yuzu (SANAT-SES §3 "Vizor bir ekrandir", URETIM §2.2 G "Vizor yuzleri kodla cizilir"): visor.json.

/// <summary>Bir ifade: goz bicimi ve agiz egrisi. Cizim Visor.cs'teki golgelendiricide.</summary>
public sealed class VisorExprDef
{
    /// <summary>round, happy, wide, closed, x.</summary>
    public string Eye { get; set; } = "round";
    public float EyeScale { get; set; } = 1f;
    /// <summary>Goz kapagi acikligi (0 kapali, 1 acik).</summary>
    public float Open { get; set; } = 1f;
    /// <summary>Agiz egrisi: -1 asik, 0 duz, 1 gulumseme.</summary>
    public float MouthCurve { get; set; }
    /// <summary>Agiz acikligi (0 cizgi, 1 tam acik).</summary>
    public float MouthOpen { get; set; }
    public float MouthWidth { get; set; } = 0.5f;
    /// <summary>Titreme (korku).</summary>
    public float Shake { get; set; }
    /// <summary>Uyku harfleri.</summary>
    public bool Zzz { get; set; }
}

/// <summary>Olayin ifade sirasindaki bir adim.</summary>
public sealed class VisorStepDef
{
    public string Expr { get; set; } = "";
    public float Seconds { get; set; }
}

public sealed class VisorFile
{
    public string Color { get; set; } = "#EDE6D6";
    public float Emission { get; set; } = 1f;
    public float BlinkMin { get; set; } = 2f;
    public float BlinkMax { get; set; } = 5f;
    public float BlinkSeconds { get; set; } = 0.12f;
    /// <summary>Dayaniklilik bu oranin altindayken (ya da tukenmisken) yuz yorgun.</summary>
    public float TiredBelow { get; set; } = 0.25f;
    /// <summary>Ifadeler arasi gecis hizi (1/sn).</summary>
    public float Blend { get; set; } = 10f;
    public Dictionary<string, VisorExprDef> Expressions { get; set; } = new();
    /// <summary>Oyun olayi -> ifade sirasi (hasar, kart, satis, yemek, uyku...).</summary>
    public Dictionary<string, List<VisorStepDef>> Events { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("visor.json")]
    public VisorFile Visor { get; private set; } = new();
}
