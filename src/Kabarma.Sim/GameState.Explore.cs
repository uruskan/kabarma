namespace Kabarma.Sim;

/// <summary>GameState parcasi: dunya (biyom haritasi, kesif noktalari, dogma tablolari) ve kesif durumu (dunya2 akisi).</summary>
public sealed partial class GameState
{
    BiomeMap? _biomeMap;
    List<Poi>? _pois;
    SpawnTable? _spawns;
    Exploration? _explore;

    /// <summary>Tohumdan; kayda girmez.</summary>
    public BiomeMap BiomeMap => _biomeMap ??= new BiomeMap(Data.Biomes);
    public IReadOnlyList<Poi> Pois => _pois ??= PoiPlacer.Place(Data.Biomes, BiomeMap);
    public SpawnTable Spawns => _spawns ??= new SpawnTable(Data.Biomes, BiomeMap);

    /// <summary>Oyuncunun kesif durumu (kayit bolumu: kayit akisinin sozlesmesi main'e girince baglanir).</summary>
    public Exploration Explore
    {
        get => _explore ??= new Exploration(Data);
        set => _explore = value;
    }

    public Poi? PoiById(string id) => Pois.FirstOrDefault(p => p.Id == id);

    /// <summary>
    /// Kayit (TEKNIK 9.1): acilan harita bitleri, bulunan noktalar, tirmanilan kuleler, isaretler, tuketilen noktalar,
    /// ucus sayaci. BiomeMap, Pois ve Spawns tohumdan yeniden uretilir (kayda yer yazilmaz); bu bolum onlari da kapsar.
    /// </summary>
    [SaveSection("explore", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Explore), nameof(BiomeMap), nameof(Pois), nameof(Spawns) })]
    Exploration ExploreSection
    {
        get => Explore;
        set => _explore = value ?? new Exploration(Data);
    }
}
