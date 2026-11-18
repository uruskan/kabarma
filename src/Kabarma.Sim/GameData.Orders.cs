namespace Kabarma.Sim;

// Is emirleri (orders.json; GDD 13, 24; ICERIK 18), Ustalik (mastery.json; GDD 10; ICERIK 19) ve basarimlar
// (achievements.json; ICERIK 18). Oyun kurali Orders.cs, Mastery.cs, Achievements.cs icinde.

public sealed class OrderStepDef
{
    public string Goal { get; set; } = "";
    public string Measure { get; set; } = "";
    public int Target { get; set; } = 1;
}

/// <summary>Adim ya da zincir sonunda acilan sey (plan, kart, uye, kozmetik, esya). Sahibi sistem OrderBook.Granted'i okur.</summary>
public sealed class OrderGrantDef
{
    /// <summary>1 tabanli adim (5 = zincir sonu).</summary>
    public int Step { get; set; } = 5;
    /// <summary>plan | card | member | cosmetic | item</summary>
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
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
    public List<OrderGrantDef> Grants { get; set; } = new();
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
    /// <summary>Olcu kimligi -> kip: "sum" | "distinct" | "peak".</summary>
    public Dictionary<string, string> Measures { get; set; } = new();
    public List<OrderChainDef> Chains { get; set; } = new();
}

// ---- Ustalik ----

public sealed class MasteryBranchDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Her seviyenin +%1'inin gittigi etki (GDD 10: "ilgili ise +%1 hiz ya da verim").</summary>
    public string LevelStat { get; set; } = "";
}

public sealed class MasteryEffectDef
{
    public string Stat { get; set; } = "";
    /// <summary>"add" (toplanir; yuzde ya da sayi) | "set" (deger olur; en buyugu gecerli).</summary>
    public string Op { get; set; } = "add";
    public float Value { get; set; }
}

public sealed class MasteryPerkDef
{
    public string Id { get; set; } = "";
    public string Branch { get; set; } = "";
    public int Tier { get; set; }
    /// <summary>"A" ya da "B".</summary>
    public string Slot { get; set; } = "A";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>true: etkisi M2'de oyunda calisir (T1-T2: 10. ve 20. seviye); false: secilir, etkisi ilgili sistemle gelir.</summary>
    public bool Live { get; set; }
    public List<MasteryEffectDef> Effects { get; set; } = new();
}

public sealed class MasteryXpDef
{
    public string Branch { get; set; } = "";
    public float Xp { get; set; }
}

public sealed class MasteryFile
{
    public int MaxLevel { get; set; } = 50;
    /// <summary>n. seviyenin toplam XP esigi = Base × n^Exponent (GDD 10).</summary>
    public float ThresholdBase { get; set; } = 100f;
    public float ThresholdExponent { get; set; } = 1.6f;
    /// <summary>Seviye basina etki (0,01 = %1).</summary>
    public float PerLevel { get; set; } = 0.01f;
    public List<int> PerkLevels { get; set; } = new();
    /// <summary>Sifirlama bedeli: Kredi × (dalin seviyesi / 10) (ICERIK 18 katalog).</summary>
    public int RespecCreditsPer10Levels { get; set; } = 1000;
    public List<MasteryBranchDef> Branches { get; set; } = new();
    /// <summary>XP kaynagi kimligi ("gather:stone", "plant", "kill"...) -> dal ve miktar.</summary>
    public Dictionary<string, MasteryXpDef> Xp { get; set; } = new();
    public List<MasteryPerkDef> Perks { get; set; } = new();
}

// ---- Basarimlar ----

public sealed class AchievementDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    /// <summary>Genel sayac (OrderBook.Stats) bu degere ulasinca acilir.</summary>
    public string Stat { get; set; } = "";
    public int Target { get; set; } = 1;
}

public sealed class AchievementsFile
{
    /// <summary>Toplam basarim: 48 zincirin tamamlanmasi (kimlik chain_ + zincir kimligi) + Extras.</summary>
    public int Total { get; set; } = 50;
    public string ChainPrefix { get; set; } = "chain_";
    public string ChainText { get; set; } = "";
    public List<AchievementDef> Extras { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("orders.json")]
    public OrdersFile Orders { get; private set; } = new();

    [DataFile("mastery.json")]
    public MasteryFile Mastery { get; private set; } = new();

    [DataFile("achievements.json")]
    public AchievementsFile Achievements { get; private set; } = new();
}
