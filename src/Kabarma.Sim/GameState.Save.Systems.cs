namespace Kabarma.Sim;

// M1 Sim sistemlerinin kayit bolumleri (TEKNIK 9.1). DTO'lar ve sessiz yukleyiciler SaveSystems.cs icinde.

public sealed class CoopSave
{
    public List<PenAnimalSave> Animals { get; set; } = new();
}

public sealed class PenAnimalSave
{
    public string Species { get; set; } = "";
    public bool Tutorial { get; set; }
    public float Trust { get; set; }
    public bool Registered { get; set; }
    public float LayTimer { get; set; }
    public int Laid { get; set; }
}

public sealed class NightSave
{
    public int NightsStarted { get; set; }
    public int SalesAtNightStart { get; set; }
}

public sealed partial class GameState
{
    [SaveSection("farm", 1, Order = SaveOrder.State, Covers = new[] { nameof(Farm) })]
    List<PlotSave> FarmSection { get => Farm.SavePlots(); set => Farm.LoadPlots(value); }

    /// <summary>
    /// Agildaki hayvanlarin kural durumu. Yemlik ve Folluk iceriklerini sahibi PenProp kaydi tasir (Godot "pen.props"),
    /// cunku Feeder/Nest nesneleri prop kurulunca dogar.
    /// </summary>
    [SaveSection("pen.animals", 1, Order = SaveOrder.State, Covers = new[] { nameof(Coop) })]
    CoopSave CoopSection
    {
        get => new()
        {
            Animals = Coop.Animals.Select(a => new PenAnimalSave
            {
                Species = a.Species, Tutorial = a.Tutorial, Trust = a.Trust, Registered = a.Registered, LayTimer = a.LayTimer, Laid = a.Laid,
            }).ToList(),
        };
        set
        {
            Coop.Animals.Clear();
            foreach (var a in value.Animals)
                Coop.Animals.Add(new PenAnimal { Species = a.Species, Tutorial = a.Tutorial, Trust = a.Trust, Registered = a.Registered, LayTimer = a.LayTimer, Laid = a.Laid });
        }
    }

    [SaveSection("stations", 1, Order = SaveOrder.State, Covers = new[] { nameof(Stations) })]
    List<StationQueueSave> StationsSection
    {
        get => Stations.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value.Save()).ToList();
        set { foreach (var s in value) if (Stations.TryGetValue(s.Station, out var q)) q.Load(s); }
    }

    [SaveSection("craft", 1, Order = SaveOrder.State, Covers = new[] { nameof(Craft) })]
    CraftSave CraftSection
    {
        get => Craft.Save();
        set => Craft.Load(value, id => Data.Tool.Recipes.FirstOrDefault(r => r.Id == id && CraftRules.IsHand(r)));
    }

    [SaveSection("cargo", 1, Order = SaveOrder.State, Covers = new[] { nameof(Cargo) })]
    CargoSave CargoSection { get => Cargo.Save(); set => Cargo.Load(value); }

    [SaveSection("capa", 1, Order = SaveOrder.State, Covers = new[] { nameof(Capa) })]
    List<string> CapaSection
    {
        get => Capa.Unlocked.OrderBy(x => x, StringComparer.Ordinal).ToList();
        set { Capa.Unlocked.Clear(); foreach (var w in value) Capa.Unlocked.Add(w); }
    }

    [SaveSection("calendar", 1, Order = SaveOrder.State, Covers = new[] { nameof(Calendar) })]
    CalendarSave CalendarSection { get => Calendar.Save(); set => Calendar.Load(value); }

    /// <summary>
    /// Gece sayaci. Gece surerken kayit alinmaz (progress/m2/kayit/durum_denetimi.md, bulgu 4): KabarmaNight kaydedilmez,
    /// yuklenince Night null'dir. Safakta acik kalan gece de birakilir; rapor ekrani bir kez gosterilir.
    /// </summary>
    [SaveSection("night", 1, Order = SaveOrder.State, Covers = new[] { nameof(NightsStarted), nameof(Night) })]
    NightSave NightSection
    {
        get => new() { NightsStarted = NightsStarted, SalesAtNightStart = _salesAtNightStart };
        set { NightsStarted = value.NightsStarted; _salesAtNightStart = value.SalesAtNightStart; Night = null; }
    }

    [SaveSection("scenario", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Scenario) })]
    ScenarioSave ScenarioSection { get => Scenario.Save(); set => Scenario.Load(value); }

    [SaveSection("orders", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Orders) })]
    OrdersSave OrdersSection { get => Orders.Save(); set => Orders.Load(value); }
}
