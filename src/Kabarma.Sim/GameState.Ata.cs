namespace Kabarma.Sim;

/// <summary>GameState parcasi: Atalarin ilerlemesi (GDD §18). Duz ve serilestirilebilir; kayit bolumu "atalar" (TEKNIK 9.1).</summary>
public sealed partial class GameState
{
    AtaProgress? _atalar;

    public AtaProgress Atalar
    {
        get => _atalar ??= new AtaProgress();
        set => _atalar = value;
    }

    /// <summary>
    /// Kayit: adak ilerlemesi, yenilgiler, muhurler, ultiler, acilanlar ve suren dovus. Suren dovus yuklenince Godot
    /// katmani (AtaSystem) Ata'yi arenada yeniden kurar.
    /// </summary>
    [SaveSection("atalar", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Atalar) })]
    AtaProgress AtalarSection
    {
        get => Atalar;
        set => _atalar = value ?? new AtaProgress();
    }
}
