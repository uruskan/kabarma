namespace Kabarma.Sim;

// Borsa, Katalog ve Tohum Carki'nin kayit bolumu (TEKNIK 9.1; kayit sozlesmesi). ekonomi2 akisi.

public sealed class PurchaseSave
{
    public string What { get; set; } = "";
    public int Credits { get; set; }
}

public sealed class MarketSave
{
    public MarketState Market { get; set; } = new();
    public CatalogState Catalog { get; set; } = new();
    public WheelState Wheel { get; set; } = new();
    public List<PurchaseSave> Purchases { get; set; } = new();
}

public sealed partial class GameState
{
    /// <summary>
    /// Borsa (carpanlar, doygunluk, sicak kategori, gecmis), yoldaki ve inmis katalog teslimatlari, cark sayaclari ve
    /// Kooperatif deposu, harcamalar. Durum siniflari zaten duz; kopyalanmadan JSON'a gider.
    /// </summary>
    [SaveSection("market", 1, Order = SaveOrder.State, Covers = new[] { nameof(Market), nameof(Catalog), nameof(Wheel), nameof(Purchases), nameof(WheelFreeToday) })]
    MarketSave MarketSection
    {
        get => new()
        {
            Market = Market.State,
            Catalog = Catalog.State,
            Wheel = Wheel.State,
            Purchases = Purchases.Select(p => new PurchaseSave { What = p.what, Credits = p.credits }).ToList(),
        };
        set
        {
            Market.Restore(value.Market ?? new MarketState());
            Catalog.Restore(value.Catalog ?? new CatalogState());
            Wheel.Restore(value.Wheel ?? new WheelState());
            Purchases.Clear();
            foreach (var p in value.Purchases ?? new List<PurchaseSave>()) Purchases.Add((p.What, p.Credits));
        }
    }
}
