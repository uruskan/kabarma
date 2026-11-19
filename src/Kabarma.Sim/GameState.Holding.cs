namespace Kabarma.Sim;

/// <summary>
/// GameState'in Bereket Holding parcasi (GDD §19; holding akisi). Sabah ve satis olaylari baglanir; gece ve safak Godot
/// katmanindan gelir (gercek Kabarma isareti kabarma3 akisinda; o gelene kadar gece sayisi, holding.json firstRealNight).
/// </summary>
public sealed partial class GameState
{
    public HoldingSystem Holding { get; private set; } = null!;

    /// <summary>Kurucudan bir kez (InitNightAndDay'den sonra).</summary>
    void InitHolding()
    {
        Holding = new HoldingSystem(Data) { Biomes = () => BiomeMap, WorldEdge = Math.Min(Data.Biomes.World.Terrain.EdgeStart > 0 ? Data.Biomes.World.Terrain.EdgeStart : 800f, 800f), Team = () => 1 };
        Calendar.Morning += day => Holding.OnMorning(day, Research.OcakTier);
        Sold += (v, _) => Holding.RecordExport(v);
    }

    /// <summary>
    /// GECICI (kabarma3 gercek Kabarma isaretini getirene kadar): bu gece gercek Kabarma mi. Ilk gece Kucuk Kabarma'dir.
    /// </summary>
    public bool RealKabarma => NightsStarted >= Data.Holding.Camp.FirstRealNight;

    /// <summary>Ganimeti uygular: esyalar cantaya (sigmayan geri doner), Kredi cuzdana, token Kooperatif deposuna.</summary>
    public Dictionary<string, int> ApplyLoot(HoldingLoot loot, Inventory to)
    {
        var left = new Dictionary<string, int>();
        foreach (var (id, n) in loot.Items)
        {
            int got = to.Add(id, n);
            if (got < n) left[id] = n - got;
        }
        if (loot.Credits > 0) Credits += loot.Credits;
        if (loot.Token.Length > 0) Holding.State.Depot[loot.Token] = Holding.State.Depot.GetValueOrDefault(loot.Token) + 1;
        return left;
    }

    /// <summary>Kayit bolumu (TEKNIK 9.1): HoldingState duz; sessizce geri yuklenir.</summary>
    [SaveSection("holding", 1, Order = SaveOrder.State, Covers = new[] { nameof(Holding), nameof(RealKabarma) })]
    HoldingState HoldingSection { get => Holding.State; set => Holding.Restore(value ?? new HoldingState()); }
}
