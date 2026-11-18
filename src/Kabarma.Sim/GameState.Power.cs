namespace Kabarma.Sim;

/// <summary>GameState parcasi: guc agi ve temel sinyal (GDD 11.1, 11.2; PowerNet.cs). Kayit bolumu: Power (duz, TEKNIK 9).</summary>
public sealed partial class GameState
{
    /// <summary>Guc agi: parcalar, kablolar, aku dolulugu, salter ve oncelikler. Kayit yuklenince yerine yenisi konur.</summary>
    public PowerNet Power { get; set; } = new();

    /// <summary>Agi bir adim isletir (Godot her kare; dt gercek saniye x oyun hizi).</summary>
    public void TickPower(float dt) => Power.Tick(Data.Power, dt, PowerNet.GameHours(dt, Data.Calendar));
}
