namespace Kabarma.Sim;

// market.json: Borsa (GDD §13, ICERIK §18). catalog.json: Katalog. wheel.json: Tohum Carki. ekonomi2 akisi.

public sealed class MarketCategoryDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class MarketFile
{
    /// <summary>Kategori carpaninin gunluk oynama genligi (GDD §13: ±0,15).</summary>
    public float DailyStep { get; set; } = 0.15f;
    /// <summary>Her gun 1,0'a dogru cekim orani (carpanin sinira yapismamasi icin).</summary>
    public float Revert { get; set; } = 0.1f;
    public float Min { get; set; } = 0.6f;
    public float Max { get; set; } = 1.6f;
    /// <summary>Gunun sicak kategorisinin carpani (×2; sinirin ustune biner).</summary>
    public float HotMult { get; set; } = 2f;
    /// <summary>Satilan her kasanin kategorisini dusurme orani (%3).</summary>
    public float SaturationPerCrate { get; set; } = 0.03f;
    /// <summary>Doygunluk tabani (%40).</summary>
    public float SaturationFloor { get; set; } = 0.4f;
    /// <summary>Her sabah doygunluga eklenen (0,15; en fazla 1,0).</summary>
    public float SaturationRecovery { get; set; } = 0.15f;
    /// <summary>Kategori basina tutulan gunluk fiyat gecmisi.</summary>
    public int HistoryDays { get; set; } = 30;
    public string DefaultCategory { get; set; } = "islenmis";
    public string FoodCategory { get; set; } = "yemek";
    public List<MarketCategoryDef> Categories { get; set; } = new();
    /// <summary>Urun kimligi -> kategori kimligi.</summary>
    public Dictionary<string, string> Items { get; set; } = new();
}

public sealed class CatalogEntryDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>tohum, yumurta, arac, sus, kozmetik, yuva...</summary>
    public string Kind { get; set; } = "";
    /// <summary>Sabit fiyat (Kredi); 0 ise <see cref="SeedPackOf"/> ile hesaplanir.</summary>
    public int Price { get; set; }
    /// <summary>Tohum paketi: bu urunun taban fiyati × seedPackMult (ICERIK §18).</summary>
    public string SeedPackOf { get; set; } = "";
    /// <summary>Teslimatin icindekiler (kimlik -> adet), cantaya gecer.</summary>
    public Dictionary<string, int> Items { get; set; } = new();
    /// <summary>Rampaya inen yuk urunu (tarif kimligi, ornegin el_arabasi).</summary>
    public string Load { get; set; } = "";
    /// <summary>kapsul ya da rampa.</summary>
    public string Via { get; set; } = "kapsul";
    /// <summary>Veride bulunmasi gereken kimlikler; biri yoksa kalem gizlenir.</summary>
    public List<string> Requires { get; set; } = new();
}

public sealed class CatalogFile
{
    public int SeedPackMult { get; set; } = 20;
    public float CapsuleSeconds { get; set; } = 20f;
    public float RampSeconds { get; set; } = 40f;
    public List<CatalogEntryDef> Entries { get; set; } = new();
}

public sealed class WheelRewardDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>credits, items ya da token.</summary>
    public string Kind { get; set; } = "";
    public float Weight { get; set; }
    /// <summary>Beklenen deger icin Kredi karsiligi (credits turunde amounts ortalamasi kullanilir).</summary>
    public int Value { get; set; }
    public List<int> Amounts { get; set; } = new();
    public Dictionary<string, int> Items { get; set; } = new();
}

public sealed class WheelFile
{
    public int Cost { get; set; } = 500;
    public int AlacaCost { get; set; } = 3;
    public int FreePerDay { get; set; } = 1;
    public float TargetEv { get; set; } = 0.85f;
    public List<WheelRewardDef> Rewards { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("market.json")]
    public MarketFile Market { get; private set; } = new();

    [DataFile("catalog.json")]
    public CatalogFile Catalog { get; private set; } = new();

    [DataFile("wheel.json")]
    public WheelFile Wheel { get; private set; } = new();
}
