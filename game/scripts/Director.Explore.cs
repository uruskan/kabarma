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

    void InitExplore()
    {
        Explorer = new Explorer(this);
        AddChild(Explorer);
        MapPanel = new MapPanel(this);
        Hud.AddChild(MapPanel);
    }

    /// <summary>E: kesif etkilesimi (kule, mancinik, yuva) yaptiysa true.</summary>
    bool ExploreInteract() => Explorer != null && Explorer.Interact();
}
