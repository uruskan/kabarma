using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director, guc akisi (GDD 11.1, 11.2): guc sistemi dugumu, kemerden kurma, E ve Kablo eldeyken R.</summary>
public partial class Director
{
    /// <summary>Guc agi (power/PowerSystem.cs). Guc isteyen istasyonlar: Power?.Attach(this, Def.Id) (PowerPort).</summary>
    public PowerSystem Power { get; private set; }

    /// <summary>Director._Ready'den bir kez.</summary>
    void InitPower()
    {
        Power = new PowerSystem(this);
        AddChild(Power);
        // Kayit (TEKNIK 9.1): durum Sim'in "power" bolumunde; yuklemeden sonra gorunus yeniden kurulur.
        Save.Add(SaveSection.Json("power.view", 1, () => 0, _ => { }, SaveOrder.Spawn, afterRestore: Power.Rebuild));
        if (Main.Args.ContainsKey("power")) GD.Print("POWER test: bot zinciri bitince guc adimi (Bot.Power.cs)");
        if (Main.Args.ContainsKey("powershots")) AddChild(new PowerShots(this)); // gorsel kanit (power/PowerShots.cs)
        Callable.From(Power.BindNet).CallDeferred(); // ortak oyun: InitNet'ten sonra "power" bolumu ve istekler (PowerSystem.Net.cs)
    }

    /// <summary>Capa.Insa: kemerdeki guc parcasini kurar. Guc parcasi degilse false.</summary>
    public bool PlacePower(PlaceableDef def, Vector3 at, float yaw)
    {
        if (Power == null || !Power.IsDevice(def.Id)) return false;
        Power.Place(def, at, yaw);
        return true;
    }

    /// <summary>Director.Interact: guc parcasi (E: ayar, dugmeye bas). Isledi ise true.</summary>
    public bool InteractPower() => Power?.Interact() ?? false;
}
