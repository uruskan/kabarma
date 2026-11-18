using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: kesif (dunya2; GDD §21). Explorer (harita acilmasi, kesif noktalari, Seyir Kulesi, mancinik) ve M haritasi
/// (MapPanel). Director._Ready bir satirla InitExplore cagirir; Interact ilk is ExploreInteract'a sorar.
/// </summary>
public partial class Director
{
    public Explorer Explorer { get; private set; }
    public MapPanel MapPanel { get; private set; }

    /// <summary>Biyom dogma tablolari: yaratik akislari aktor fabrikasini Register ile kaydeder (BiomeSpawner.cs).</summary>
    /// <remarks>Ilk eriste kurulur: kayit Director._Ready'nin hangi satirinda olursa olsun calisir.</remarks>
    public BiomeSpawner Spawner
    {
        get
        {
            if (_spawner == null) { _spawner = new BiomeSpawner(this); AddChild(_spawner); }
            return _spawner;
        }
    }
    BiomeSpawner _spawner;

    void InitExplore()
    {
        Explorer = new Explorer(this);
        AddChild(Explorer);
        _ = Spawner;
        MapPanel = new MapPanel(this);
        Hud.AddChild(MapPanel);
        if (Main.Args.ContainsKey("explore")) AddChild(new ExploreTest(this)); // --test=explore
    }

    /// <summary>E: kesif etkilesimi (kule, mancinik, yuva) yaptiysa true.</summary>
    bool ExploreInteract() => Explorer != null && Explorer.Interact();
}
