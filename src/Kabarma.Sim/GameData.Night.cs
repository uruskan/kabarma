namespace Kabarma.Sim;

// Kabarma gecesi: cards.json (GDD 15) ve kabarma.json (GDD 14-15).

public sealed partial class CardDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rarity { get; set; } = "S";
    public string Text { get; set; } = "";
    public string Stat { get; set; } = "";
    public float PerRank { get; set; }
}

public sealed partial class CardsFile
{
    public Dictionary<string, int> RarityWeights { get; set; } = new();
    public int MaxRank { get; set; }
    public int OfferSize { get; set; }
    public float AutoPickSeconds { get; set; }
    public float SlowMoScale { get; set; }
    public List<CardDef> Cards { get; set; } = new();
}

public sealed class WaveDef
{
    public int Count { get; set; }
    public float Seconds { get; set; }
    public float GapAfter { get; set; }
}

public sealed class NightDef
{
    public int Ks { get; set; }
    public float AlarmSeconds { get; set; }
    public float SwellSeconds { get; set; }
    public List<WaveDef> Waves { get; set; } = new();
    public int MaxCardLevels { get; set; }
    public string Enemy { get; set; } = "";
    /// <summary>GDD 24: ilk gecenin ilk teklifinde her zaman bulunan kart (bos: kural yok).</summary>
    public string? FirstOfferCard { get; set; }
    /// <summary>GDD 23-24: uye tipinin baslangic karti bu gece verilir mi (ilk gecede verilmez).</summary>
    public bool MemberStartCard { get; set; } = true;
}

public sealed class GosteriDef
{
    public float Min { get; set; }
    public float Max { get; set; }
    public int StreakKills { get; set; }
    public float StreakWindow { get; set; }
    public float StreakBonus { get; set; }
    public float DecayPerSecond { get; set; }
    /// <summary>GDD 14.5: GosteriEvent adi -> artis (kabarma3).</summary>
    public Dictionary<string, float> Events { get; set; } = new();
}

public sealed class KabarmaFile
{
    public NightDef FirstNight { get; set; } = new();
    public float LevelBase { get; set; }
    public float LevelExponent { get; set; }
    public GosteriDef Gosteri { get; set; } = new();
    public float OzPerWeight { get; set; } = 1f;
    /// <summary>Yerdeki Oz'un toplanma yaricapi, metre (URETIM 6 M1 karari 19); Miknatis karti carpar.</summary>
    public float OzPickupRadius { get; set; } = 4f;
    public float MayaPerWeight { get; set; }
    public float MayaKsFactor { get; set; }
    public float HpGrowth { get; set; }
    public float DamageGrowth { get; set; }
    /// <summary>denge (M6): LateFromKs'tan sonraki KS basina buyume (0 = ayni, tek us). Erken egri M3 kabulune (KS 1-10) baglidir; gec egri onerilen donanimin gercekci buyumesine uyar.</summary>
    public float HpGrowthLate { get; set; }
    public float DamageGrowthLate { get; set; }
    public int LateFromKs { get; set; } = 10;
    /// <summary>denge (M6): EndFromKs'tan sonra ucuncu us (0 = Late ile ayni). Toplam can buyumesi (can x dusman sayisi) sabit kalsin diye sayi artisi azaldikca can buyumesi de kisilir.</summary>
    public float HpGrowthEnd { get; set; }
    public float DamageGrowthEnd { get; set; }
    public int EndFromKs { get; set; } = 25;
    public float GrowthMult(float early, float late, int ks, float end = 0f)
    {
        if (late <= 0f || ks <= LateFromKs) return MathF.Pow(early, ks - 1);
        float mid = MathF.Pow(early, LateFromKs - 1);
        if (end <= 0f || ks <= EndFromKs) return mid * MathF.Pow(late, ks - LateFromKs);
        return mid * MathF.Pow(late, EndFromKs - LateFromKs) * MathF.Pow(end, ks - EndFromKs);
    }
    /// <summary>Tam Kabarma (GDD 14.1-14.6): GameData.Wave.cs.</summary>
    public FullKabarmaDef Full { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("cards.json")]
    public CardsFile Cards { get; private set; } = new();

    [DataFile("kabarma.json")]
    public KabarmaFile Kabarma { get; private set; } = new();
}
