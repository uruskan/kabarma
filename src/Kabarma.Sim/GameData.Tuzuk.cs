namespace Kabarma.Sim;

// Tuzuk maddeleri (GDD 14.8, ICERIK 8.4) ve Kabartma Odasi (GDD 14.10) verisi: data/tuzuk.json.

/// <summary>Bir tuzuk maddesi. Etki alanlari 1 kat degerini verir; 0 / bos = bu maddenin etkisi degil (FireMult ve ShockMult null = yok).</summary>
public sealed class TuzukClauseDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Menude gosterilen kisa etki yazisi (ICERIK 8.4).</summary>
    public string Note { get; set; } = "";
    /// <summary>Gorus mesafesi (m).</summary>
    public float VisionM { get; set; }
    /// <summary>Dusmanin ekini yeme hizi carpani.</summary>
    public float EatSpeed { get; set; }
    /// <summary>Elit oranina eklenir (tavanin ustune).</summary>
    public float EliteAdd { get; set; }
    /// <summary>Doruk'ta ek Azman sayisi.</summary>
    public int AzmanAdd { get; set; }
    public bool NoGap { get; set; }
    public float? FireMult { get; set; }
    public float? ShockMult { get; set; }
    public bool HoldingAllRaid { get; set; }
    /// <summary>Seviye basina sunulan kart sayisi.</summary>
    public int OfferSize { get; set; }
    public int ExtraWaves { get; set; }
    public float TowerSpeed { get; set; }
    public bool AnyBiomeMix { get; set; }
    /// <summary>Mermi menzili carpani.</summary>
    public float RangeMult { get; set; }
}

public sealed class NewMoonDef { public float MayaPerClause { get; set; } }

public sealed class SwellRoomDef
{
    public int MayaCost { get; set; } = 50;
    public float Radius { get; set; } = 12f;
    public float AnimalSeconds { get; set; } = 120f;
}

public sealed class TuzukFile
{
    public int FirstKs { get; set; } = 26;
    public int SecondRoundKs { get; set; } = 38;
    public int MaxStack { get; set; } = 2;
    public NewMoonDef NewMoon { get; set; } = new();
    public List<TuzukClauseDef> Clauses { get; set; } = new();
    public SwellRoomDef SwellRoom { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("tuzuk.json")]
    public TuzukFile Tuzuk { get; private set; } = new();
}