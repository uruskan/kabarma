using System;

namespace Kabarma.Game;

/// <summary>
/// Oyun olaylari (duz C# olaylari; Godot sinyali degil, agdan gitmez). Oyun kodu olayi Raise... ile bildirir,
/// ilgilenen += ile abone olur: senaryo sayaclari (Director.Scenario.cs WireScenario), ileride gorevler, ses, istatistik.
/// Yeni bir senaryo adimi icin cogu zaman kod degil, mevcut olaya bir abonelik yeter.
/// Durum tutmaz; Director acilista ve kapanista Clear() cagirir (eski sahneden abone kalmasin).
/// </summary>
public static class Events
{
    /// <summary>Ocak ya da bir istasyon yere kondu (yerlestirilebilir kimligi: "ocak", "tezgah", "tas_firin"...).</summary>
    public static event Action<string> StationPlaced;
    /// <summary>Yapi seti parcasi kondu (parca kimligi: "kare_temel", "duvar"...).</summary>
    public static event Action<string> PiecePlaced;
    /// <summary>Istasyon bir tarifi bitirdi (tarif/urun kimligi, cikan adet).</summary>
    public static event Action<string, int> Produced;
    /// <summary>Kargo Rampasi menusunden satis (urun kimligi, adet, kazanilan Kredi).</summary>
    public static event Action<string, int, int> Sold;
    /// <summary>Kabarik urun Cekim ile rampaya birakilip satildi (ekin kimligi, boy sinifi, kazanilan Kredi).</summary>
    public static event Action<string, string, int> CropShipped;
    /// <summary>Bicme vurusu olgun ekin hasat etti (bu vurusta hasat edilen kare sayisi).</summary>
    public static event Action<int> Harvested;
    /// <summary>Kaynak cantaya girdi (esya kimligi: "stone", "wood"...; adet).</summary>
    public static event Action<string, int> Gathered;
    /// <summary>Yemek yendi (yemek kimligi).</summary>
    public static event Action<string> Ate;
    /// <summary>Yaratik oldu (yaratik kimligi, kabarik miydi).</summary>
    public static event Action<string, bool> CreatureKilled;
    /// <summary>Gece bir kart secildi (kart kimligi).</summary>
    public static event Action<string> CardPicked;
    /// <summary>Senaryo adimi degisti (biten adim, yeni adim; zincir bittiyse "end").</summary>
    public static event Action<string, string> StepChanged;
    /// <summary>Kabarma gecesi basladi (alarm).</summary>
    public static event Action NightStarted;
    /// <summary>Safak: gece bitti, kalan dusmanlar sondu.</summary>
    public static event Action Dawn;
    /// <summary>Oyuncu Uyku Tulumu'nda uyudu (normal gece atlanir, GDD 2; ICERIK 10.2). Yayan: ekonomi, dinleyen: gun dongusu.</summary>
    public static event Action Slept;
    /// <summary>Kargo odemesi hesaba gecti (Kredi, ne satildi): ilk satista hemen, sonra firlatmadan 60 sn sonra (GDD 13).</summary>
    public static event Action<int, string> Paid;
    /// <summary>Insa'nin Sok kipiyle bir parca ya da yerlestirilebilir sokuldu (kimlik).</summary>
    public static event Action<string> Deconstructed;
    /// <summary>Agil degisti: kapisi olan en buyuk agilin hucre sayisi (0 = agil yok). Yayan: PenSystem.</summary>
    public static event Action<int> PenChanged;
    /// <summary>Evcil hayvan agila girip kayit oldu (tur kimligi: "gidak"). Yayan: Gidak.Pen.cs.</summary>
    public static event Action<string> AnimalPenned;
    /// <summary>Agildaki hayvan urun verdi (urun kimligi "egg", yer "nest" ya da "ground"). Yayan: PenSystem.</summary>
    public static event Action<string, string> Laid;
    /// <summary>Suna bir repligi soyledi (scenario.json "lines" kimligi). Seslendirme buna baglanir.</summary>
    public static event Action<string> SunaSaid;
    /// <summary>Ata yenildi (atalar.json kimligi). Yayan: AtaSystem (boss akisi).</summary>
    public static event Action<string> AtaDefeated;
    /// <summary>Ata'nin asamasi degisti (kimlik, 0'dan asama). Yayan: AtaSystem.</summary>
    public static event Action<string, int> AtaPhase;
    public static void RaiseAtaDefeated(string id) => AtaDefeated?.Invoke(id);
    public static void RaiseAtaPhase(string id, int phase) => AtaPhase?.Invoke(id, phase);
    /// <summary>Yabani yaratik davranisi (tur, olay: Alerted/Fled/Warned/Struck/Took/Slept/Woke, x, z). Yayan: Wildlife (yaratik2). Etkiyi sahibi uygular: ekin tarim, yumurta hayvan (ICERIK §3.8).</summary>
    public static event Action<string, string, float, float> WildActed;
    public static void RaiseWildActed(string species, string what, float x, float z) => WildActed?.Invoke(species, what, x, z);

    public static void RaisePenChanged(int cells) => PenChanged?.Invoke(cells);
    public static void RaiseAnimalPenned(string species) => AnimalPenned?.Invoke(species);
    public static void RaiseLaid(string product, string where) => Laid?.Invoke(product, where);
    public static void RaiseSunaSaid(string lineId) => SunaSaid?.Invoke(lineId);

    public static void RaiseStationPlaced(string id) => StationPlaced?.Invoke(id);
    public static void RaisePiecePlaced(string id) => PiecePlaced?.Invoke(id);
    public static void RaiseProduced(string item, int n) => Produced?.Invoke(item, n);
    public static void RaiseSold(string item, int n, int credits) => Sold?.Invoke(item, n, credits);
    public static void RaiseCropShipped(string crop, string size, int credits) => CropShipped?.Invoke(crop, size, credits);
    public static void RaiseHarvested(int n) => Harvested?.Invoke(n);
    public static void RaiseGathered(string item, int n) => Gathered?.Invoke(item, n);
    public static void RaiseAte(string food) => Ate?.Invoke(food);
    public static void RaiseCreatureKilled(string id, bool swollen) => CreatureKilled?.Invoke(id, swollen);
    public static void RaiseCardPicked(string id) => CardPicked?.Invoke(id);
    public static void RaiseStepChanged(string from, string to) => StepChanged?.Invoke(from, to);
    public static void RaiseNightStarted() => NightStarted?.Invoke();
    public static void RaiseDawn() => Dawn?.Invoke();
    public static void RaiseSlept() => Slept?.Invoke();
    public static void RaisePaid(int credits, string what) => Paid?.Invoke(credits, what);
    public static void RaiseDeconstructed(string id) => Deconstructed?.Invoke(id);

    /// <summary>Butun aboneleri birakir.</summary>
    public static void Clear()
    {
        WildActed = null;
        StationPlaced = null; PiecePlaced = null; Produced = null; Sold = null; CropShipped = null;
        Harvested = null; Gathered = null; Ate = null; CreatureKilled = null; CardPicked = null;
        StepChanged = null; NightStarted = null; Dawn = null;
        Slept = null;
        Paid = null;
        Deconstructed = null;
        PenChanged = null; AnimalPenned = null; Laid = null; SunaSaid = null;
        AtaDefeated = null; AtaPhase = null;
    }
}
