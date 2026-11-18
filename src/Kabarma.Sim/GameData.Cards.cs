namespace Kabarma.Sim;

// Kabarma kartlari, M3 (GDD 15, 15.1; ICERIK 8): 60 kart + 12 asi, Nisan, acilma, yenileme ve surgun.

public sealed class CardEffect
{
    public string Stat { get; set; } = "";
    public float PerRank { get; set; }
    /// <summary>Rutbeyle carpilmaz (ac/kapa ya da tek seferlik etki).</summary>
    public bool Once { get; set; }
}

public sealed partial class CardDef
{
    /// <summary>capa, yorunge, beden, ciftlik, risk, asi.</summary>
    public string Class { get; set; } = "capa";
    /// <summary>start (bastan acik), is_emri, uye, ata, jeton, asi.</summary>
    public string Unlock { get; set; } = "start";
    /// <summary>Ekip karti: ekibin en yuksek rutbesi gecerli (GDD 15).</summary>
    public bool Team { get; set; }
    /// <summary>Kartin rutbe tavani; yoksa cards.json maxRank (asilar 1).</summary>
    public int? MaxRank { get; set; }
    /// <summary>Asi kartinin iki ebeveyni.</summary>
    public List<string> Parents { get; set; } = new();
    public List<CardEffect> Effects { get; set; } = new();
    public Dictionary<string, float> Params { get; set; } = new();

    public bool IsAsi => Class == "asi";
    public float Param(string name, float fallback = 0f) => Params.TryGetValue(name, out var v) ? v : fallback;

    /// <summary>Ana etki ve ek etkiler birlikte.</summary>
    public IEnumerable<CardEffect> AllEffects()
    {
        if (!string.IsNullOrEmpty(Stat)) yield return new CardEffect { Stat = Stat, PerRank = PerRank };
        foreach (var e in Effects) yield return e;
    }
}

public sealed class NisanDef
{
    /// <summary>Nisan 1-5 icin gereken toplam puan.</summary>
    public List<int> Thresholds { get; set; } = new();
    /// <summary>Her Nisan seviyesinin kesirli etkilere eklenen payi (%4).</summary>
    public float BonusPerLevel { get; set; }
    /// <summary>Nisan 5'te kartin ilk alinista basladigi rutbe.</summary>
    public int StartRankAtMax { get; set; } = 2;
}

public sealed partial class CardsFile
{
    public int MaxSlots { get; set; } = 12;
    public int Rerolls { get; set; }
    public int Banishes { get; set; }
    public NisanDef Nisan { get; set; } = new();
    /// <summary>Kart jetonuyla acilan kartlar (ICERIK 8.3).</summary>
    public List<string> JetonPool { get; set; } = new();

    public CardDef? Find(string id) => Cards.FirstOrDefault(c => c.Id == id);
    public CardDef Card(string id) => Find(id) ?? throw new InvalidDataException($"Bilinmeyen kart: {id}");
    public int RankCap(CardDef c) => c.MaxRank ?? (c.IsAsi ? 1 : MaxRank);

    /// <summary>Sayi ya da ac/kapa etkileri: Nisan bunlari artirmaz (GDD 15.1).</summary>
    public static readonly HashSet<string> CountStats = new(StringComparer.Ordinal)
    {
        "projectiles", "bounces", "pierce", "jumps", "revives", "orbital", "gamble", "debtLevels", "boyBonus",
        "scarecrowsFight", "workAnimalsFight", "alacaMult", "baitTarget", "forkOnBounce", "unlimitedChain", "chainSwell",
        "animalCopies", "towerDouble", "unlimitedJumps",
    };
}
