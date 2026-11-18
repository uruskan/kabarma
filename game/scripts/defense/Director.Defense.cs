using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Director, savunma akisi (GDD §16.4 M3): kuleler ve tuzaklar (defense/DefenseSystem.cs).</summary>
public partial class Director
{
    public DefenseSystem Defense { get; private set; }

    /// <summary>Director._Ready'den bir kez.</summary>
    void InitDefense()
    {
        Defense = new DefenseSystem(this);
        AddChild(Defense);
        // Kayit (TEKNIK 9.1): kural durumu Sim'in "defense" bolumunde; yuklemeden sonra dugumler yeniden kurulur.
        Save.Add(SaveSection.Json("defense.view", 1, () => 0, _ => { }, SaveOrder.Spawn, afterRestore: Defense.Rebuild));
        Callable.From(Defense.BindNet).CallDeferred(); // ortak oyun: InitNet'ten sonra (DefenseSystem.Net.cs)
        if (Main.Args.ContainsKey("savunmatest")) AddChild(new DefenseTest(this)); // --test=savunma (defense/DefenseTest.cs)
    }

    /// <summary>Capa.Insa ve ortak HostPlaceItem: kemerdeki kule ya da tuzagi kurar. Savunma yapisi degilse false.</summary>
    public bool PlaceDefense(PlaceableDef def, Vector3 at, float yaw)
    {
        if (Defense == null || !Defense.Handles(def.Id)) return false;
        Defense.Place(def, at, yaw);
        return true;
    }

    /// <summary>Director.Interact: kule (doldur / onar) ya da tuzak (kapak / onar). Isledi ise true.</summary>
    public bool InteractDefense() => Defense?.Interact() ?? false;
}
