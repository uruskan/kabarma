namespace Kabarma.Sim;

// Cipler (chips.json; ICERIK 6.3 ve 6.3.1, GDD 4). Oyun kurali Chip.cs, Chip.Combat.cs.

public sealed class ChipRarityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Nadirlik etkisi (0,08 = +%8).</summary>
    public float Bonus { get; set; }
    public string Color { get; set; } = "FFFFFF";
}

public sealed class ChipLegendaryDef
{
    public string Stat { get; set; } = "";
    public float Value { get; set; }
    public string Text { get; set; } = "";
}

public sealed class ChipDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>general | weapon | work</summary>
    public string Group { get; set; } = "general";
    /// <summary>work cipleri icin tek uyumlu baslik.</summary>
    public string Host { get; set; } = "";
    public string Stat { get; set; } = "";
    /// <summary>Etki = nadirlik carpani x Scale.</summary>
    public float Scale { get; set; } = 1f;
    public string Text { get; set; } = "";
    public ChipLegendaryDef Legendary { get; set; } = new();
}

public sealed class ChipSourceDef
{
    /// <summary>Olay basina dusme olasiligi.</summary>
    public float Chance { get; set; } = 1f;
    public Dictionary<string, float> Weights { get; set; } = new();
    /// <summary>Kabarma odulunde KS basina eklenen agirlik.</summary>
    public float KsRarePerKs { get; set; }
    public float KsEpicPerKs { get; set; }
}

public sealed class ChipsFile
{
    public List<ChipRarityDef> Rarities { get; set; } = new();
    /// <summary>Capa Mk'sine (1-6) gore baslik basina yuva sayisi.</summary>
    public List<int> SlotsByMk { get; set; } = new();
    public int StorageMax { get; set; } = 200;
    public List<string> WeaponHeads { get; set; } = new();
    /// <summary>Is basliklari (ve Insa Plani): Genel cipler bunlara da takilir.</summary>
    public List<string> WorkHeads { get; set; } = new();
    public List<ChipDef> Chips { get; set; } = new();
    /// <summary>Nadirlik -> esya kimligi -> adet.</summary>
    public Dictionary<string, Dictionary<string, int>> Craft { get; set; } = new();
    public Dictionary<string, ChipSourceDef> Sources { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("chips.json")]
    public ChipsFile Chips { get; private set; } = new();
}
