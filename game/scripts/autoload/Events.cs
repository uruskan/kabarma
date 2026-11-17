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

    /// <summary>Butun aboneleri birakir.</summary>
    public static void Clear()
    {
        StationPlaced = null; PiecePlaced = null; Produced = null; Sold = null; CropShipped = null;
        Harvested = null; Gathered = null; Ate = null; CreatureKilled = null; CardPicked = null;
        StepChanged = null; NightStarted = null; Dawn = null;
        Slept = null;
    }
}
