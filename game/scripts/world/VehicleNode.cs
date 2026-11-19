using System;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arac dugumu (ICERIK §14, GDD §20; URETIM §6 M2/M4): Cekirge, Traktor, Kamyon, Bicerdover, Hava Yastikli, Zeplin.
/// Kural Sim'de (Vehicles.Step / Settle); bu dugum govdeyi kurar (VehicleBodies), eklenti gorunumlerini ve far isigini tasir,
/// zemine ya da havaya oturtur ve surucunun girdisini kurala verir. Govdeler koddan yer tutucudur (vehicles.json placeholder).
/// </summary>
public partial class VehicleNode : Node3D
{
    public VehicleDef Def { get; }
    public VehicleState State { get; }
    public VehiclesFile Config { get; }
    public float Travelled { get; private set; }
    /// <summary>Zemin yuksekligi (x, z) -> y. Dunyada World.HeightAt; testte duz zemin.</summary>
    public Func<float, float, float> Height = (_, _) => 0f;
    /// <summary>Zemin turu (x, z) -> "", "water", "swamp", "snow", "sand" (Director: biyom ve su).</summary>
    public Func<float, float, string> Surface = (_, _) => "";
    /// <summary>Aracin bulundugu yerin biyomu degisince ya da hasar alinca (isteyen dinler).</summary>
    public VehicleMods Mods { get; private set; } = VehicleMods.None;
    public string LastSurface { get; private set; } = "";
    Node3D _body;
    Node3D _attachRoot;
    SpotLight3D _light;
    readonly Node3D[] _wheels = new Node3D[4];
    readonly float _wheelbase;
    float _spin;

    public VehicleNode(VehicleDef def, VehicleState state, VehiclesFile config = null)
    {
        Def = def; State = state; Config = config ?? new VehiclesFile();
        Name = $"{char.ToUpper(def.Id[0])}{def.Id[1..]}_{state.Id}";
        AddToGroup("vehicles");
        _wheelbase = VehicleBodies.Wheelbase(def);
        _body = VehicleBodies.Build(def, _wheels);
        AddChild(_body);
        _attachRoot = new Node3D { Name = "Attachments" };
        _body.AddChild(_attachRoot);
        RefreshAttachments();
    }

    /// <summary>Takili eklentilerin gorunumu, far ve toplam etki (takma, cikarma, yukleme sonrasi).</summary>
    public void RefreshAttachments()
    {
        foreach (var c in _attachRoot.GetChildren()) c.QueueFree();
        foreach (var id in State.Attachments) _attachRoot.AddChild(VehicleBodies.Attachment(id, Def));
        Mods = Vehicles.ModsOf(Config, State);
        if (Mods.HasLight)
        {
            if (_light == null)
            {
                _light = new SpotLight3D { SpotRange = 40f, SpotAngle = 32f, LightEnergy = 4f, ShadowEnabled = false, Name = "Projektor", Position = new Vector3(0, 1.4f, _wheelbase * 0.5f + 0.5f) };
                _light.Rotation = new Vector3(Mathf.DegToRad(-6f), Mathf.Pi, 0);
                _body.AddChild(_light);
            }
            _light.Visible = State.Light;
        }
        else if (_light != null) { _light.QueueFree(); _light = null; }
    }

    public bool HasAttachment(string id) => State.Attachments.Contains(id);

    /// <summary>Surucu koltugu (dunya): binen oyuncu buraya oturur; index 0 surucu, sonrakiler yolcu.</summary>
    public Vector3 SeatAt(int index = 0) => GlobalTransform * VehicleBodies.SeatFor(Def, index);
    public Vector3 Seat => SeatAt(0);
    public bool InReach(Vector3 p, float reach = 2.6f)
    {
        float r = reach + (Def.Id == "zeplin" ? 3f : _wheelbase * 0.3f);
        return new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < r && Mathf.Abs(p.Y - GlobalPosition.Y) < (Def.Kind == "air" ? 6f : 3.5f);
    }

    /// <summary>Bir adim: girdi Sim kuralina gider, dugum yeni konum ve egime oturur.</summary>
    public void Drive(VehicleInput input, float dt)
    {
        float yaw = State.Yaw;
        var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        float hf = Height(State.X + fwd.X * _wheelbase * 0.5f, State.Z + fwd.Z * _wheelbase * 0.5f);
        float hb = Height(State.X - fwd.X * _wheelbase * 0.5f, State.Z - fwd.Z * _wheelbase * 0.5f);
        input.SlopeDeg = Mathf.RadToDeg(Mathf.Abs(Mathf.Atan2(hf - hb, _wheelbase)));
        LastSurface = Surface(State.X, State.Z);
        input.Surface = LastSurface;
        float x0 = State.X, z0 = State.Z;
        Vehicles.Step(Def, State, input, dt, Mods);
        Travelled += new Vector2(State.X - x0, State.Z - z0).Length();
        Fall(dt);
        Place(hf, hb);
        _spin += State.Speed * dt;
        foreach (var w in _wheels) w?.RotateX(State.Speed * dt / 0.4f);
    }

    /// <summary>Surucusuz ya da gazsiz kareler: dusme ve zemine oturma (park halindeki arac da ucurumdan duser).</summary>
    public void Fall(float dt)
    {
        float g = Height(State.X, State.Z);
        Vehicles.Settle(Def, State, g, dt, Mods, Config.Rules);
    }

    /// <summary>Durumu dugume yansitir (yukleme, dogma).</summary>
    public void Place(float? hf = null, float? hb = null)
    {
        float yaw = State.Yaw;
        var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        float f = hf ?? Height(State.X + fwd.X * _wheelbase * 0.5f, State.Z + fwd.Z * _wheelbase * 0.5f);
        float b = hb ?? Height(State.X - fwd.X * _wheelbase * 0.5f, State.Z - fwd.Z * _wheelbase * 0.5f);
        bool airborne = Def.Kind == "air" || State.Y > (f + b) * 0.5f + 0.35f;
        float pitch = airborne ? 0f : Mathf.Atan2(f - b, _wheelbase);
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, -pitch);
        if (State.Flipped) basis *= new Basis(Vector3.Forward, Mathf.Pi * 0.5f);
        float y = airborne ? State.Y : (f + b) * 0.5f;
        if (!airborne) State.Y = y;
        var pos = new Vector3(State.X, y, State.Z);
        if (IsInsideTree()) GlobalTransform = new Transform3D(basis, pos);
        else Transform = new Transform3D(basis, pos);
    }

    public override void _Process(double delta)
    {
        // Hava yastikli ve zeplin: hafif sallanma (yer tutucu canlilik).
        if (Def.Kind != "ground" && _body != null)
        {
            float t = (float)Time.GetTicksMsec() / 1000f;
            _body.Position = new Vector3(0, Mathf.Sin(t * 1.6f + State.Id) * (Def.Kind == "air" ? 0.12f : 0.04f), 0);
        }
        if (_light != null) _light.Visible = State.Light;
    }
}
