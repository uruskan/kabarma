namespace Kabarma.Sim;

/// <summary>GameState parcasi: guc agi ve temel sinyal (GDD 11.1, 11.2; PowerNet.cs). Kayit bolumu: Power (duz, TEKNIK 9).</summary>
public sealed partial class GameState
{
    /// <summary>Guc agi: parcalar, kablolar, aku dolulugu, salter ve oncelikler. Kayit yuklenince yerine yenisi konur.</summary>
    public PowerNet Power { get; set; } = new();

    /// <summary>Agi bir adim isletir (Godot her kare; dt gercek saniye x oyun hizi).</summary>
    /// <summary>Kayit bolumu (TEKNIK 9.1): parcalar, kablolar, aku, salter, oncelik, ruzgar evresi. Gorunusu PowerSystem.Rebuild kurar.</summary>
    [SaveSection("power", 1, Order = SaveOrder.State, Covers = new[] { nameof(Power) })]
    PowerNet PowerSection { get => Power; set => Power = value ?? new PowerNet(); }

    public void TickPower(float dt) => Power.Tick(Data.Power, dt, PowerNet.GameHours(dt, Data.Calendar));
}
