using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director: araclar (ICERIK §14, GDD §20). Cekirge, Traktor, Kamyon, Bicerdover, Hava Yastikli, Zeplin ve 10 eklenti.
/// E ile arac menusu (bin, yakit, eklenti, kasa, isik, park); binince W/S gaz ve geri, A/D direksiyon, Bosluk fren
/// (hava araci: Shift yuksel, Bosluk alcal). Kural Sim'de (Vehicles), kayit bolumu "vehicles".
/// Uretim: Arac Atolyesi (Cekirge, Traktor, Kamyon, Bicerdover, Hava Yastikli, eklentiler) ve Zeplin Hangari (Zeplin)
/// istasyon sirasindan yuk olarak cikar (Station.OnProduced -> SpawnProduced). Parcalar: Director.Vehicles.Menu.cs
/// (menu ve eylemler), .Work.cs (eklenti isi, biçme, carpma), .Net.cs (ortak oyun).
/// </summary>
public partial class Director
{
    public readonly List<VehicleNode> Vehicles = new();
    public VehicleNode Riding { get; private set; }
    int _nextVehicleId = 1;
    /// <summary>Oyuncunun arac koltuk kimligi (Sim Riders): tek oyuncuda ve host'ta 1; istemcide 100 + es.</summary>
    public int MyRiderId => Net != null && Net.IsClient ? 100 + Net.LocalPeer : 1;

    void InitVehicles()
    {
        var driver = new VehicleDriver(this) { Name = "VehicleDriver", ProcessPriority = 100 };
        AddChild(driver);
        Save.Add(SaveSection.Json("vehicles", 1, CaptureVehicles, ApplyVehicles, SaveOrder.Spawn));
        if (Main.Args.TryGetValue("vehicle", out var id) && Data.Vehicles.Vehicles.Any(v => v.Id == id))
            CallDeferred(nameof(SpawnTestVehicle), id);
        Callable.From(BindVehicleNet).CallDeferred(); // ortak oyun: InitNet'ten sonra (Director.Vehicles.Net.cs)
        if (Main.Args.ContainsKey("vehicletest")) AddChild(new VehicleTest(this)); // --test=vehicles (tests/VehicleTest.cs)
    }

    void SpawnTestVehicle(string id)
    {
        var def = Data.Vehicle(id);
        var p = PodPos + new Vector3(-8f, 0, 10f); // kapsulun onunde acik cayir
        var v = SpawnVehicle(def, Kabarma.Sim.Vehicles.New(def, _nextVehicleId++, p.X, p.Z, Mathf.Pi));
        Kabarma.Sim.Vehicles.Refuel(def, v.State, def.TankUnits); // yer tutucu: --vehicle ile dolu depo
        GD.Print($"VEHICLE {id}#{v.State.Id} at {p} fuel={v.State.Fuel}");
        if (Main.Args.ContainsKey("vehicleview"))
        {
            // Goruntu kaniti: arac yandan-onden, sabit kamera.
            var cam = new Camera3D { Fov = 50 };
            AddChild(cam);
            cam.GlobalPosition = v.GlobalPosition + new Vector3(3.2f, 1.8f, -3.0f) * (def.Id is "zeplin" ? 3f : def.Id is "kamyon" or "bicerdover" ? 1.6f : 1f);
            cam.LookAt(v.GlobalPosition + Vector3.Up * 0.6f);
            cam.MakeCurrent();
        }
    }

    /// <summary>Zemin turu: su, sonra biyom (Sazlik bataklik, Kanyon kum, Ayaz kar); aksi kara (vehicles.json surface).</summary>
    string VehicleSurface(float x, float z)
    {
        var at = World.Ground(new Vector3(x, 0, z));
        if (World.IsWater(at)) return "water";
        return World.Map.BiomeAt(x, z) switch { "isil_sazlik" => "swamp", "kizil_kanyon" => "sand", "ayaz_yaylasi" => "snow", _ => "" };
    }

    public VehicleNode SpawnVehicle(VehicleDef def, VehicleState st)
    {
        var v = new VehicleNode(def, st, Data.Vehicles) { Height = (x, z) => World.HeightAt(x, z), Surface = VehicleSurface };
        AddChild(v);
        v.Place();
        Vehicles.Add(v);
        // Yuklu kasalar: kasa dugumleri (cargo_crates grubu) aracin ustune oturur.
        foreach (var cid in st.Crates.ToList()) { var n = CrateNode(cid); if (n != null) SeatCrate(v, n); else st.Crates.Remove(cid); }
        return v;
    }

    /// <summary>Istasyonun arac urununu dunyaya cikarir (Station.OnProduced): Atolyede istasyonun onune, Zeplin Hangari'nda hangarda park halinde.</summary>
    public void SpawnProduced(RecipeDef r, Station st)
    {
        var def = Data.Vehicle(r.Id);
        var at = st.OutputPoint + st.GlobalBasis.Z * (def.Id == "zeplin" ? 6f : 3.2f);
        var state = Kabarma.Sim.Vehicles.New(def, _nextVehicleId++, at.X, at.Z, st.Rotation.Y);
        state.Y = at.Y;
        state.Parked = def.Kind == "air";
        var v = SpawnVehicle(def, state);
        Fx.Burst(v.GlobalPosition + Vector3.Up * 0.8f, Palette.Warning, 24, 4f, 0.1f, 0.8f);
        Hud.Hint(Loc.T("dir.hint_vehicle_ready", def.Name), 4f);
        GD.Print($"VEHICLE produced {def.Id}#{state.Id} at {at}");
    }

    /// <summary>Director.Interact: binili ise iner (hava aracinda yerdeyken), yakindaki aracin menusunu acar. Isledi ise true.</summary>
    public bool InteractVehicle()
    {
        if (Riding != null) { LeaveVehicle(); return true; }
        var p = Player.GlobalPosition;
        float reach = Data.Vehicles.Rules.ReachMeters;
        var v = Vehicles.Where(x => IsInstanceValid(x) && x.InReach(p, reach)).OrderBy(x => x.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (v == null) return false;
        OpenVehicleMenu(v);
        return true;
    }

    /// <summary>Binmeyi dener (menu ya da test): koltuk bos, devrik degil. Surucu ilk binen.</summary>
    public bool BoardVehicle(VehicleNode v)
    {
        if (Riding != null) return false;
        if (VehicleForward("board", v, "")) return true; // ortak: istemcide host koltuk verir
        int me = MyRiderId;
        if (v.State.Flipped) { Kabarma.Sim.Vehicles.Right(v.State); v.Place(); Hud.Hint(Loc.T("dir.hint_vehicle_righted", v.Def.Name), 1.5f); return true; }
        if (!Kabarma.Sim.Vehicles.Board(v.Def, v.State, me)) { Hud.Hint(Loc.T("dir.hint_seat_full"), 1.5f); return true; }
        EnterVehicle(v);
        return true;
    }

    void EnterVehicle(VehicleNode v)
    {
        Riding = v;
        Player.MoveOverride = Vector3.Zero;
        string how = v.Def.Kind == "air" ? Loc.T("dir.vehicle_controls_air") : Loc.T("dir.vehicle_controls_ground");
        Hud.Hint(v.State.Fuel > 0 ? how : Loc.T("dir.hint_tank_empty", ItemNames.Of(v.Def.FuelItem)), 2.5f);
        GD.Print($"VEHICLE board {v.Name} seat={v.State.Riders.IndexOf(MyRiderId)}");
    }

    public void LeaveVehicle()
    {
        var v = Riding;
        if (v == null) return;
        if (v.State.Altitude > 2.5f) { Hud.Hint(Loc.T("dir.hint_land_first"), 1.5f); return; }
        if (VehicleForward("leave", v, "")) { /* istemci: host koltugu birakir, Riding geri okunur */ }
        else Kabarma.Sim.Vehicles.Leave(v.State, MyRiderId);
        Riding = null;
        Player.MoveOverride = null;
        var side = v.GlobalBasis.X.Normalized();
        Player.GlobalPosition = World.Ground(v.GlobalPosition + side * (v.Def.Id == "zeplin" ? 3.5f : 1.4f + v.Def.Seats * 0.1f)) + Vector3.Up * 0.2f;
        ParkIfInHangar(v);
        GD.Print($"VEHICLE leave {v.Name} travelled={v.Travelled:0}m fuel={v.State.Fuel:0}");
    }

    /// <summary>Zeplin Hangari'nin park yaricapinda inen hava araci park sayilir (can onarilir).</summary>
    void ParkIfInHangar(VehicleNode v)
    {
        if (v.Def.Kind != "air" || v.State.Altitude > 2.5f) return;
        var h = Stations.FirstOrDefault(s => s.Def.Station == "zeplin_hangari" && IsInstanceValid(s));
        if (h == null || !Kabarma.Sim.Vehicles.InHangar(v.State, h.GlobalPosition.X, h.GlobalPosition.Z, Data.Vehicles.Rules)) return;
        v.State.Parked = true; v.State.Altitude = 0; v.State.Speed = 0; v.State.Hp = v.Def.Hp;
        Hud.Hint(Loc.T("dir.hint_zeppelin_parked"), 2.5f);
    }

    VehiclesSave CaptureVehicles() => new() { NextId = _nextVehicleId, Vehicles = Vehicles.Where(IsInstanceValid).Select(v => v.State).ToList() };

    void ApplyVehicles(VehiclesSave s)
    {
        if (Riding != null) { Kabarma.Sim.Vehicles.Leave(Riding.State, MyRiderId); Riding = null; Player.MoveOverride = null; }
        foreach (var v in Vehicles) if (IsInstanceValid(v)) v.QueueFree();
        Vehicles.Clear();
        _nextVehicleId = s.NextId;
        foreach (var st in s.Vehicles)
        {
            st.Riders.Clear();
            if (Data.Vehicles.Vehicles.FirstOrDefault(d => d.Id == st.Vehicle) is { } def) SpawnVehicle(def, st);
        }
    }

    /// <summary>Binili araci her fizik karesinde surer; oyuncuyu koltukta tutar (oyuncudan sonra calisir). Surucusuz araclar duser ve zemine oturur.</summary>
    sealed partial class VehicleDriver : Node
    {
        readonly Director _d;
        public VehicleDriver(Director d) => _d = d;

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;
            _d.TickVehicles(dt);
        }
    }

    /// <summary>Tek giris: yerel surucu (girdi tuslardan), uzak suruculer (host: son gelen girdi), surucusuz araclar (dusme, park). Istemcide arac host'tan gelir.</summary>
    void TickVehicles(float dt)
    {
        bool client = Net != null && Net.IsClient;
        SendVehicleInput(dt); // ortak: istemci surucu girdisini host'a gonderir
        foreach (var v in Vehicles.ToList())
        {
            if (!IsInstanceValid(v)) continue;
            if (client) { v.Place(); continue; } // istemci: durum host'tan (ReadNet), yalniz gorunum
            int driver = v.State.Riders.Count > 0 ? v.State.Riders[0] : 0;
            if (driver == 1 && Riding == v)
            {
                if (Player.Down) { LeaveVehicle(); continue; }
                v.Drive(LocalVehicleInput(v), dt);
                RunVehicleWork(v, dt);
            }
            else if (driver >= 100 && _remoteInput.TryGetValue(v.State.Id, out var ri) && Time.GetTicksMsec() - ri.at < 600) { v.Drive(ri.input, dt); RunVehicleWork(v, dt); }
            else { v.Drive(default, dt); }
            if (v.State.Flipped && Riding == v) { Hud.Hint(Loc.T("dir.hint_vehicle_flipped", v.Def.Name), 2f); LeaveVehicle(); }
        }
        // Oyuncu koltukta: konum ve yon (yolcular surucuyle birlikte).
        var me = Riding;
        if (me != null && IsInstanceValid(me))
        {
            Player.MoveOverride = Vector3.Zero;
            int seat = Mathf.Max(0, me.State.Riders.IndexOf(MyRiderId));
            Player.GlobalPosition = me.SeatAt(seat);
            Player.Velocity = Vector3.Zero;
            Player.Visual.Rotation = new Vector3(0, me.State.Yaw, 0);
        }
        else if (Riding != null) { Riding = null; Player.MoveOverride = null; }
    }

    /// <summary>Surucu girdisi: W/S gaz, A/D direksiyon, Bosluk fren (hava: Shift yuksel, Bosluk alcal), L isik.</summary>
    VehicleInput LocalVehicleInput(VehicleNode v)
    {
        bool air = v.Def.Kind == "air";
        return new VehicleInput
        {
            Throttle = Input.GetAxis("move_back", "move_forward"),
            Steer = Input.GetAxis("move_right", "move_left"),
            Brake = !air && Input.IsActionPressed("jump"),
            Lift = air ? (Input.IsActionPressed("sprint") ? 1f : 0f) - (Input.IsActionPressed("jump") ? 1f : 0f) : 0f,
        };
    }
}
