namespace Kabarma.Sim;

/// <summary>
/// GameState'in Borsa, Katalog ve Tohum Carki parcasi (GDD §13, ICERIK §18; ekonomi2). Cuzdan ekipce ortaktir: Credits.
/// Butun durum MarketState / CatalogState / WheelState'te, duz ve serilestirilebilir (TEKNIK §9).
/// </summary>
public sealed partial class GameState
{
    public Market Market { get; private set; } = null!;
    public Catalog Catalog { get; private set; } = null!;
    public Wheel Wheel { get; private set; } = null!;
    /// <summary>Harcamalar (ne, tutar): katalog ve cark.</summary>
    public List<(string what, int credits)> Purchases { get; } = new();

    /// <summary>Kredi harcandi (tutar, ne, kalan).</summary>
    public event Action<int, string, int>? Spent;
    /// <summary>Cark cevrildi.</summary>
    public event Action<WheelResult>? WheelSpun;

    /// <summary>Kurucudan bir kez (InitNightAndDay'den sonra): Borsa, Katalog, Cark; Borsa takvimin sabahina baglanir.</summary>
    void InitMarket()
    {
        Market = new Market(Data, Seed * 7919 + 3);
        Cargo.Pricing.Market = Market;
        Catalog = new Catalog(Data, Cargo.Pricing);
        Wheel = new Wheel(Data, Seed * 104729 + 11);
        Calendar.Morning += Market.OnMorning;
    }

    /// <summary>Kredi harcar: yeterliyse duser, true. Cuzdan eksiye inmez.</summary>
    public bool Spend(int credits, string what)
    {
        if (credits < 0 || Credits < credits) return false;
        Credits -= credits;
        Purchases.Add((what, credits));
        Spent?.Invoke(credits, what, Credits);
        return true;
    }

    /// <summary>Katalogdan satin alir. Donen: yola cikan teslimat ya da null (Kredi yetmedi, kalem yok).</summary>
    public Delivery? BuyCatalog(string entryId) => Catalog.Buy(entryId, p => Spend(p, entryId));

    /// <summary>Takvim gunu (gun dongusu baslamadiysa 1, bedava cevirme icin).</summary>
    int WheelDay => Calendar.Running ? Calendar.Day : 0;

    public bool WheelFreeToday => Wheel.FreeAvailable(WheelDay);

    /// <summary>
    /// Tohum Carki'ni cevirir: bedava hak ya da 500 Kredi. Kredi odulu cuzdana, esya odulu kapsulle (Catalog), token odulu
    /// Kooperatif deposuna (Wheel.State.Tokens). Kredi yetmezse null.
    /// </summary>
    public WheelResult? SpinWheel()
    {
        var res = Wheel.Spin(WheelDay, p => Spend(p, "cark"));
        if (res == null) return null;
        if (res.Credits > 0) Credits += res.Credits;
        if (res.Reward.Kind == "items" && res.Reward.Items.Count > 0) Catalog.Send("cark:" + res.Reward.Id, "kapsul", res.Reward.Items);
        WheelSpun?.Invoke(res);
        return res;
    }

    /// <summary>Kabarik urunun Borsa fiyati (GameState.Sell): taban × Borsa × doygunluk.</summary>
    int SwollenPrice(string cropId, int value) =>
        (int)Math.Round(value * Market.FactorOf(cropId), MidpointRounding.AwayFromZero);

    /// <summary>Kabarik urun firlatildi: kategorisinin doygunlugu duser.</summary>
    void SwollenSold(string cropId) => Market.RecordSale(new[] { Market.Category(cropId) });
}
