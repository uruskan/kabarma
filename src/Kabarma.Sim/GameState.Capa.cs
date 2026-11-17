namespace Kabarma.Sim;

/// <summary>GameState parcasi: Capa durumu (acik silah basliklari).</summary>
public sealed partial class GameState
{
    CapaState? _capa;

    /// <summary>Ilk eriside veriden kurulur (weapons.json unlockedAtStart).</summary>
    public CapaState Capa => _capa ??= CapaState.FromData(Data);
}
