namespace Kabarma.Sim;

// Bereket Holding'in kayda hazir durumu (TEKNIK §9): duz siniflar, kimlikle basvuru. Kurallar HoldingSystem'de.

public sealed class HoldingUnit
{
    public int Id { get; set; }
    /// <summary>holding.json units kimligi: isci, bekci, ustabasi, mekanik.</summary>
    public string Kind { get; set; } = "";
    /// <summary>camp (kamp ve devriye), caravan (kervan korumasi), raid (onlarin baskini).</summary>
    public string Job { get; set; } = "camp";
    public float Hp { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public bool Alive { get; set; } = true;
    /// <summary>Baskinda kabarik urun tasiyor.</summary>
    public bool Carrying { get; set; }
}

public sealed class HoldingCaravan
{
    public int Day { get; set; }
    /// <summary>out (depoya), wait, back (kampa), done.</summary>
    public string Phase { get; set; } = "out";
    public float X { get; set; }
    public float Z { get; set; }
    public float VehicleHp { get; set; }
    public bool Ambushed { get; set; }
    public Dictionary<string, int> Cargo { get; set; } = new();
    public int Credits { get; set; }
}

public sealed class HoldingCamp
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public int Level { get; set; } = 1;
    /// <summary>1, 2, 3: kamp sirasi; 4: Genel Merkez (holding.json sites).</summary>
    public int Slot { get; set; } = 1;
    /// <summary>"camp" ya da "hq" (Genel Merkez: seviye sabit, baskin ve kervan yok, Cekirdek dusunce Mudur gelir).</summary>
    public string Kind { get; set; } = "camp";
    /// <summary>Genel Merkez temizlendi (Mudur yenildi): yeniden kurulmaz.</summary>
    public bool Cleared { get; set; }
    public int FoundedDay { get; set; }
    /// <summary>Son seviye atlamadan ya da son baskindan beri gecen gun.</summary>
    public int QuietDays { get; set; }
    public float Husumet { get; set; }
    public bool Destroyed { get; set; }
    public int RebuildDay { get; set; }
    /// <summary>Yapi kimligi -> can (0: yikildi).</summary>
    public Dictionary<string, float> Structures { get; set; } = new();
    public List<HoldingUnit> Units { get; set; } = new();
    public HoldingCaravan? Caravan { get; set; }
    public int LastCaravanDay { get; set; }
    /// <summary>Bu gece oyuncu kampa isabet ettirdi (Husumet +25 bir kez, seviye sayaci sifirlanir).</summary>
    public bool RaidedTonight { get; set; }
    /// <summary>Bu gece baskin ekibi gonderildi / oyuncunun tapusuna girdi.</summary>
    public bool RaidSent { get; set; }
    public bool RaidEntered { get; set; }
}

public sealed class HoldingState
{
    public int Serial { get; set; }
    public List<HoldingCamp> Camps { get; set; } = new();
    /// <summary>Kampin kurulacagi gun (0: bekleyen yok).</summary>
    public int FoundOnDay { get; set; }
    /// <summary>Gercek Kabarma gecesi suruyor: Perde kalkik.</summary>
    public bool NightOpen { get; set; }
    /// <summary>Bugunku kargo geliri (ihracat; ertesi sabah Husumet'e).</summary>
    public int ExportToday { get; set; }
    public int Day { get; set; }
    /// <summary>Sistemi olmayan oduller: Kooperatif deposu (nadir_hayvan...).</summary>
    public Dictionary<string, int> Depot { get; set; } = new();
    public int Ambushes { get; set; }
    public int PlayerRaids { get; set; }
    public int CoresDestroyed { get; set; }
    /// <summary>Suren Mudur dovusu (yoksa null) ve toplam yenilgi; acilanlar (head:..., cosmetic:...).</summary>
    public MudurFight? Mudur { get; set; }
    public int MudurDefeats { get; set; }
    public List<string> Unlocks { get; set; } = new();
}

/// <summary>Yikilan hedefin ya da basarili pusunun ganimeti.</summary>
public sealed record HoldingLoot(string Source, Dictionary<string, int> Items, int Credits, string Token);

/// <summary>Bir yapiya vurusun sonucu.</summary>
public enum HoldingHit { Blocked, Locked, Hit, Destroyed }
