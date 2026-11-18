using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=creatures, Cekirge ATV aktoru (yaratik2): kod govdesi kurulur, binilir, gazla yol alir ve dugum Sim durumunu izler,
/// direksiyon doner, dik yokusta hizli gidince devrilir.
/// </summary>
public partial class CreatureTest
{
    void CheckVehicle(GameData data)
    {
        var def = data.Vehicle("cekirge");
        var v = new CekirgeAtv(def, Vehicles.New(def, 1, 3000, 0)) { ProcessMode = ProcessModeEnum.Disabled };
        AddChild(v);
        v.Place();
        if (v.FindChild("Visual", true, false) == null) Fail("cekirge_atv", "govde yok");
        if (!Vehicles.Board(def, v.State, 1)) Fail("cekirge_atv", "binilmedi");
        Vehicles.Refuel(def, v.State, def.TankUnits);
        Run(dt => v.Drive(new VehicleInput { Throttle = 1 }, dt), 5f);
        if (v.Travelled < 20f) Fail("cekirge_atv", $"yol almadi ({v.Travelled:0.0} m)");
        if (v.GlobalPosition.DistanceTo(new Vector3(v.State.X, 0, v.State.Z)) > 0.01f) Fail("cekirge_atv", "dugum durumu izlemiyor");
        float yaw = v.State.Yaw;
        Run(dt => v.Drive(new VehicleInput { Throttle = 1, Steer = 1 }, dt), 1f);
        if (Mathf.Abs(v.State.Yaw - yaw) < 0.5f) Fail("cekirge_atv", "donmuyor");
        v.Height = (x, z) => x; // 45 derece yokus
        Run(dt => v.Drive(new VehicleInput { Throttle = 1 }, dt), 1f);
        if (!v.State.Flipped) Fail("cekirge_atv", "dik yokusta devrilmedi");
        GD.Print($"VEHICLE cekirge yol={v.Travelled:0}m hiz<={def.MaxSpeed} yakit={v.State.Fuel:0}/{Vehicles.TankSeconds(def):0} devrik={v.State.Flipped}");
    }
}
