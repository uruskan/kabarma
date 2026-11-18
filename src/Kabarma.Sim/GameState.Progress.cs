namespace Kabarma.Sim;

/// <summary>
/// GameState'in ilerleme parcasi (M2 ilerleme akisi): is emri odulleri (Kredi, Veri), Ustalik ve basarimlar.
/// Ustalik ve basarimlar karakter durumudur (dunyalar arasinda tasinir, TEKNIK 9); is emirleri dunya durumudur.
/// Hepsi duz siniflarda: Orders.Export / Mastery.Export / Achievements.Export.
/// </summary>
public sealed partial class GameState
{
    /// <summary>Araştırma Veri'si (iş emirleri, tarama ve enkaz verir; ICERIK 12). Harcama araştırma akışının (zincir).</summary>
    public int Veri { get; private set; }
    /// <summary>Is emri odemelerinden gelen toplam Kredi (Sales'e girmez: Hasat Raporu'nu ve "cargo_sold"u etkilemez).</summary>
    public int OrderCredits { get; private set; }
    public Mastery Mastery { get; private set; } = null!;
    public AchievementBook Achievements { get; private set; } = null!;
    /// <summary>Ustalik sansi (kesir yuvarlama) icin ayri akis: oyunun Rng'ini kaydirmaz.</summary>
    public Rng MasteryRng { get; private set; } = null!;

    /// <summary>Odeme (is emri): Kredi ve Veri hesaba gecer.</summary>
    public event Action<int, int, string>? OrderPaid;

    void InitProgress()
    {
        Mastery = new Mastery(Data.Mastery);
        Achievements = new AchievementBook(Data.Achievements, Orders);
        MasteryRng = new Rng(Seed * 17 + 5);
        Farm.Planted += _ => Mastery.Earn("plant");
        Farm.Harvested += p => { Mastery.Earn("harvest"); Orders.ReportKey("crop_kind", p.Crop ?? ""); };
        Farm.Swelled += p =>
        {
            Mastery.Earn("swell");
            Orders.ReportKey("crop_kind", p.Crop ?? "");
            // Devler Ligi: "ilk M" = M ya da buyuk (Defter'den once kabaran boy sayilmaz: zincir acik degilse sayac durur).
            int rank = p.Swollen == null ? -1 : Data.Crops.SizeClasses.FindIndex(c => c.Id == p.Swollen.Id);
            int m = Data.Crops.SizeClasses.FindIndex(c => c.Id == "M"), l = Data.Crops.SizeClasses.FindIndex(c => c.Id == "L"), xl = Data.Crops.SizeClasses.FindIndex(c => c.Id == "XL");
            if (rank >= 0 && rank >= m) Orders.Report("swollen_m");
            if (rank >= 0 && rank >= l) Orders.Report("swollen_l");
            if (rank >= 0 && rank >= xl) Orders.Report("swollen_xl");
        };
    }

    void GrantOrderReward(int credits, int veri, string what)
    {
        Credits += credits;
        Veri += veri;
        OrderCredits += credits;
        OrderPaid?.Invoke(credits, veri, what);
    }

    /// <summary>Veri harcar (araştırma). Yetmiyorsa false.</summary>
    public bool SpendVeri(int amount)
    {
        if (amount < 0 || Veri < amount) return false;
        Veri -= amount;
        return true;
    }

    /// <summary>Kredi harcar (katalog, Ustalik sifirlama). Yetmiyorsa false.</summary>
    public bool SpendCredits(int amount)
    {
        if (amount < 0 || Credits < amount) return false;
        Credits -= amount;
        return true;
    }

    /// <summary>Ustalik dalinin secimlerini Kredi karsiligi sifirlar (ICERIK 18). Donen: odenen Kredi, olmazsa -1.</summary>
    public int RespecMastery(string branch)
    {
        int cost = Mastery.RespecCost(branch);
        if (cost == 0 && Mastery.ChoiceCount(branch) == 0) return -1;
        if (!SpendCredits(cost)) return -1;
        Mastery.ClearChoices(branch);
        return cost;
    }

    /// <summary>
    /// Toplanan esyaya Ustalik'in verdigi ek adet (Cift Damla, Oduncu, Toplayici ve dal seviyesi payi). Toplama olayindan
    /// sonra cantaya ek olarak eklenir; ek yoksa 0.
    /// </summary>
    public int GatherBonus(string item, int amount)
    {
        string[] stats = item switch
        {
            "copper_ore" or "stone" or "clay" => new[] { "ore_yield" },
            "wood" => new[] { "wood_yield", "gather_yield" },
            "fiber" or "wheat" => new[] { "gather_yield", "wild_plant_yield" },
            _ => Array.Empty<string>(),
        };
        float add = 0;
        foreach (var s in stats) add += Mastery.Sum(s);
        if (add <= 0 || amount <= 0) return 0;
        float raw = amount * add;
        int whole = (int)MathF.Floor(raw);
        return whole + (MasteryRng.Chance(raw - whole) ? 1 : 0);
    }
}
