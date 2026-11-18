namespace Kabarma.Sim;

/// <summary>Kayit bolumu "abilities": gucler, Kadim Tas ilerlemesi, fenerler (TEKNIK 9.1).</summary>
public sealed class AbilitiesSave
{
    public AbilityProgress Abilities { get; set; } = new();
    public KadimTasProgress KadimTas { get; set; } = new();
    public FenerProgress Fener { get; set; } = new();
}

/// <summary>GameState parcasi: oyuncu gucleri (GDD 17), Kadim Tas (ICERIK 17), Maya Feneri (GDD 14.6).</summary>
public sealed partial class GameState
{
    public AbilityProgress Abilities { get; set; } = new();
    public KadimTasProgress KadimTas { get; set; } = new();
    public FenerProgress Fener { get; set; } = new();

    [SaveSection("abilities", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Abilities), nameof(KadimTas), nameof(Fener) })]
    AbilitiesSave AbilitiesSection
    {
        get => new() { Abilities = Abilities, KadimTas = KadimTas, Fener = Fener };
        set
        {
            Abilities = value?.Abilities ?? new AbilityProgress();
            KadimTas = value?.KadimTas ?? new KadimTasProgress();
            Fener = value?.Fener ?? new FenerProgress();
        }
    }
}
