namespace Kabarma.Sim;

/// <summary>
/// GameState'in gece, takvim ve Defter parcasi: gece sayaci, gun dongusu (GDD 2), is emirleri (GDD 13, 24; ICERIK 18)
/// ve Hasat Raporu (GDD 14.1). Butun durum burada, duz alanlarda: kayda hazir (PLAN 7.2, TEKNIK 9).
/// </summary>
public sealed partial class GameState
{
    /// <summary>Baslatilan Kabarma gecesi sayisi (ilk gece 1).</summary>
    public int NightsStarted { get; private set; }
    /// <summary>Gun dongusu: ilk Kabarma'nin safagindan sonra isler (URETIM 6 M1 Takvim).</summary>
    public Calendar Calendar { get; private set; } = null!;
    /// <summary>Defter: is emri zincirleri ve ilerlemesi.</summary>
    public OrderBook Orders { get; private set; } = null!;
    /// <summary>Son gecenin raporu (safaktan sonra kurulur).</summary>
    public HarvestReport? LastReport { get; private set; }
    int _salesAtNightStart;

    /// <summary>Kurucudan bir kez: takvim, Defter ve tarla olaylarinin sayaclara baglanmasi.</summary>
    void InitNightAndDay()
    {
        Calendar = new Calendar(Data.Calendar);
        Orders = new OrderBook(Data.Orders, GrantOrderReward);
        Farm.Planted += _ => Orders.Report("planted");
        Farm.Swelled += p =>
        {
            Orders.Report("swollen_crop");
            if (Night is { Phase: not NightPhase.Dawn } n && p.Swollen != null) { n.RecordSwell(p.Swollen); n.Hand.OnCropSwell(); } // Hand: Hasat Senligi (kartlar)
        };
        Farm.Eaten += _ => Night?.RecordEaten();
        Sold += (_, _) => Orders.Report("cargo_sold");
        InitProgress(); // GameState.Progress.cs: Ustalik, basarimlar, boy olculeri
    }

    void OnNightStarted(KabarmaNight n)
    {
        NightsStarted++;
        _salesAtNightStart = Sales.Count;
        Calendar.Paused = true; // Kabarma suresince saat durur; safaktan sonra devam eder
        n.DawnNow += () =>
        {
            Orders.Report("kabarma_survived");
            // M1: ilk Kabarma'nin safagindan sonra gun dongusu baslar; F1'den yeniden oynanan gece saati yalnizca durdurur.
            if (!Calendar.Running) Calendar.Start(Data.Calendar.FirstDay);
            Calendar.Paused = false;
        };
    }

    /// <summary>Son gecenin Hasat Raporu: gecenin sayaclari + geceden beri yapilan satis.</summary>
    public HarvestReport BuildReport()
    {
        var n = Night ?? throw new InvalidOperationException("Gece yok");
        int sold = 0;
        for (int i = _salesAtNightStart; i < Sales.Count; i++) sold += Sales[i].value;
        LastReport = HarvestReport.From(n, NightsStarted, sold);
        return LastReport;
    }

    /// <summary>Uyku (GDD 2): normal gece atlanir, Kabarma gecesi atlanamaz. Atlandiysa true.</summary>
    public bool Sleep() => Calendar.Sleep(kabarmaActive: Night != null && Night.Phase != NightPhase.Dawn);
}
