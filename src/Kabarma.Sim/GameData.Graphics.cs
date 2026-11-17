namespace Kabarma.Sim;

// Grafik on ayarlari ve performans olcumu: graphics.json (TEKNIK §10 "Yuksek" on ayari ve M1 olcumu, SANAT-SES §8.3).

public sealed class GraphicsPreset
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public float Scale { get; set; } = 1f;
    /// <summary>MSAA ornek sayisi: 0, 2, 4, 8.</summary>
    public int Msaa { get; set; }
    public int ShadowSplits { get; set; }
    public int ShadowAtlas { get; set; }
    public float ShadowDistance { get; set; }
    public bool Ssao { get; set; }
    public bool SsaoHalf { get; set; }
    public bool Glow { get; set; }
    public bool Sdfgi { get; set; }
    public bool Ssil { get; set; }
    public bool Ssr { get; set; }
    public bool VolumetricFog { get; set; }
    public bool DistanceFog { get; set; }
    public bool HeightFog { get; set; }
    /// <summary>Bitki ornek yogunlugu (1 = %100) ve ot gorunme yaricapi (m).</summary>
    public float PlantDensity { get; set; } = 1f;
    public float GrassRadius { get; set; }
    public bool Vsync { get; set; }
    public int MaxFps { get; set; }
}

/// <summary>M1 olcumu (TEKNIK §10): her yer iminde ogle N sn ve ilk gecenin son dalgasi; ortalama kare suresi butcesi.</summary>
public sealed class PerfDef
{
    public string Preset { get; set; } = "";
    public float WarmupSeconds { get; set; }
    public float NoonSeconds { get; set; }
    /// <summary>Son dalga yer imi basina en fazla bu kadar sn olculur (dalga kisaysa dalga boyunca).</summary>
    public float NightSeconds { get; set; }
    public float BudgetMs { get; set; }
    public int DrawCallBudget { get; set; }
}

public sealed class GraphicsFile
{
    public string Default { get; set; } = "";
    public Dictionary<string, GraphicsPreset> Presets { get; set; } = new();
    public PerfDef Perf { get; set; } = new();

    public GraphicsPreset Preset(string? id) => Presets[string.IsNullOrEmpty(id) ? Default : id];
}

public sealed partial class GameData
{
    [DataFile("graphics.json")]
    public GraphicsFile Graphics { get; private set; } = new();
}
