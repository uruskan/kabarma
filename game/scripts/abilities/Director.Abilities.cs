using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director, gucler akisi (GDD 14.6, 14.7, 17): guc sistemi, Kadim Taslar, Maya Feneri, Tatbikat.</summary>
public partial class Director
{
    /// <summary>Oyuncu gucleri (abilities/AbilitySystem.cs). Elektrik agi Power'dir.</summary>
    public AbilitySystem Abilities { get; private set; }

    /// <summary>Director._Ready'den bir kez.</summary>
    void InitAbilities()
    {
        Abilities = new AbilitySystem(this);
        AddChild(Abilities);
        // Kayit (TEKNIK 9.1): durum Sim'in "abilities" bolumunde; yuklemeden sonra taslar ve fenerler yeniden kurulur.
        Save.Add(SaveSection.Json("abilities.view", 1, () => 0, _ => { }, SaveOrder.Spawn, afterRestore: () => { Abilities.AfterRestore(); Abilities.Drill.RebuildFeners(); }));
        if (Main.Args.ContainsKey("guclertest")) AddChild(new GuclerTest(this)); // --test=gucler (abilities/GuclerTest.cs)
        Callable.From(Abilities.BindNet).CallDeferred(); // ortak oyun: InitNet'ten sonra "abilities" bolumu ve istekler
    }

    /// <summary>Capa.Insa: kemerdeki Maya Feneri'ni kurar. Fener degilse false.</summary>
    public bool PlaceAbility(PlaceableDef def, Vector3 at, float yaw) => Abilities?.Drill.Place(def, at, yaw) ?? false;

    /// <summary>Director.Interact (E): Kadim Tas ya da Maya Feneri. Isledi ise true.</summary>
    public bool InteractAbilities()
    {
        if (Abilities == null) return false;
        if (Abilities.Stones.Interact() || Abilities.Drill.Interact()) return true;
        // Ocak'in E menusu: Tatbikat (GDD 14.7). Erisimde istasyon varsa E istasyona gider.
        if (Ocak == null || Ocak.GlobalPosition.DistanceTo(Player.GlobalPosition) > 2.6f || StationInFocus() != null || NetClient) return false;
        Hud.ShowMenu("Ocak", new System.Collections.Generic.List<Hud.MenuItem>(Abilities.Drill.OcakMenuItems()), "Tatbikat: seçilen KS'nin dalgaları hayalet gelir; hasar gerçek, onarım bedava, ödül yok.\n{act:interact} / {act:ui_cancel}: kapat");
        return true;
    }
}
