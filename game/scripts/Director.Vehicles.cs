using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: araclar (Cekirge ATV; yaratik2, URETIM §6 M2). E ile binilir ve inilir; binince W/S gaz ve geri,
/// A/D direksiyon, Bosluk fren. Kural Sim'de (Vehicles), kayit bolumu "vehicles".
/// M2'de Cekirge yalniz --vehicle=cekirge ile oyuncunun onunde dogar (depo dolu): tarifin girdileri (Recine, Bitkisel Yag)
/// henuz yok; zincir/tarim onlari ekleyince Arac Atolyesi tarifi recipes.json'a satir olarak gelir.
/// </summary>
public partial class Director
{
    public readonly List<CekirgeAtv> Vehicles = new();
    public CekirgeAtv Riding { get; private set; }
    int _nextVehicleId = 1;

    void InitVehicles()
    {
        var driver = new VehicleDriver(this) { Name = "VehicleDriver", ProcessPriority = 100 };
        AddChild(driver);
        Save.Add(SaveSection.Json("vehicles", 1, CaptureVehicles, ApplyVehicles, SaveOrder.Spawn));
        if (Main.Args.TryGetValue("vehicle", out var id) && Data.Vehicles.Vehicles.Any(v => v.Id == id))
            CallDeferred(nameof(SpawnTestVehicle), id);
    }

    void SpawnTestVehicle(string id)
    {
        var def = Data.Vehicle(id);
        var p = PodPos + new Vector3(-8f, 0, 10f); // kapsulun onunde acik cayir
        var v = SpawnVehicle(def, Kabarma.Sim.Vehicles.New(def, _nextVehicleId++, p.X, p.Z, Mathf.Pi));
        Kabarma.Sim.Vehicles.Refuel(def, v.State, def.TankUnits); // yer tutucu: yakit esyasi yokken dolu depo
        GD.Print($"VEHICLE {id}#{v.State.Id} at {p} fuel={v.State.Fuel}");
        if (Main.Args.ContainsKey("vehicleview"))
        {
            // Goruntu kaniti: arac yandan-onden, sabit kamera.
            var cam = new Camera3D { Fov = 50 };
            AddChild(cam);
            cam.GlobalPosition = v.GlobalPosition + new Vector3(3.2f, 1.8f, -3.0f);
            cam.LookAt(v.GlobalPosition + Vector3.Up * 0.6f);
            cam.MakeCurrent();
        }
    }

    public CekirgeAtv SpawnVehicle(VehicleDef def, VehicleState st)
    {
        var v = new CekirgeAtv(def, st) { Height = (x, z) => World.HeightAt(x, z) };
        AddChild(v);
        v.Place();
        Vehicles.Add(v);
        return v;
    }

    /// <summary>Director.Interact basinda: binili ise iner, yakindaki araca biner. Isledi ise true.</summary>
    public bool InteractVehicle()
    {
        if (Riding != null) { LeaveVehicle(); return true; }
        var p = Player.GlobalPosition;
        var v = Vehicles.Where(x => IsInstanceValid(x) && x.InReach(p)).OrderBy(x => x.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (v == null) return false;
        if (v.State.Flipped) { Kabarma.Sim.Vehicles.Right(v.State); v.Place(); Hud.Hint("Çekirge doğrultuldu", 1.5f); return true; }
        if (!Kabarma.Sim.Vehicles.Board(v.Def, v.State, 1)) { Hud.Hint("Koltuk dolu", 1.5f); return true; }
        Riding = v;
        Player.MoveOverride = Vector3.Zero;
        Hud.Hint(v.State.Fuel > 0 ? "W/S: gaz · A/D: direksiyon · Boşluk: fren · E: in" : "Depo boş (Bitkisel Yağ)", 2.5f);
        GD.Print($"VEHICLE board {v.Name}");
        return true;
    }

    public void LeaveVehicle()
    {
        var v = Riding;
        if (v == null) return;
        Riding = null;
        Kabarma.Sim.Vehicles.Leave(v.State, 1);
        Player.MoveOverride = null;
        var side = v.GlobalBasis.X.Normalized();
        Player.GlobalPosition = World.Ground(v.GlobalPosition + side * 1.4f) + Vector3.Up * 0.2f;
        GD.Print($"VEHICLE leave {v.Name} travelled={v.Travelled:0}m fuel={v.State.Fuel:0}");
    }

    VehiclesSave CaptureVehicles() => new() { NextId = _nextVehicleId, Vehicles = Vehicles.Where(IsInstanceValid).Select(v => v.State).ToList() };

    void ApplyVehicles(VehiclesSave s)
    {
        LeaveVehicle();
        foreach (var v in Vehicles) if (IsInstanceValid(v)) v.QueueFree();
        Vehicles.Clear();
        _nextVehicleId = s.NextId;
        foreach (var st in s.Vehicles)
        {
            st.Riders.Clear();
            if (Data.Vehicles.Vehicles.FirstOrDefault(d => d.Id == st.Vehicle) is { } def) SpawnVehicle(def, st);
        }
    }

    /// <summary>Binili araci her fizik karesinde surer; oyuncuyu koltukta tutar (oyuncudan sonra calisir).</summary>
    sealed partial class VehicleDriver : Node
    {
        readonly Director _d;
        public VehicleDriver(Director d) => _d = d;

        public override void _PhysicsProcess(double delta)
        {
            var v = _d.Riding;
            if (v == null) return;
            if (!IsInstanceValid(v) || _d.Player.Down) { _d.LeaveVehicle(); return; }
            float dt = (float)delta;
            var input = new VehicleInput
            {
                Throttle = Input.GetAxis("move_back", "move_forward"),
                Steer = Input.GetAxis("move_right", "move_left"),
                Brake = Input.IsActionPressed("jump"),
            };
            v.Drive(input, dt);
            _d.Player.MoveOverride = Vector3.Zero;
            _d.Player.GlobalPosition = v.Seat;
            _d.Player.Velocity = Vector3.Zero;
            _d.Player.Visual.Rotation = new Vector3(0, v.State.Yaw, 0);
            if (v.State.Flipped) { _d.Hud.Hint("Çekirge devrildi. E: doğrult", 2f); _d.LeaveVehicle(); }
        }
    }
}
