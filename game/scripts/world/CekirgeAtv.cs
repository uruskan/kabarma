using System;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Cekirge (ATV, ICERIK §14, GDD §20; URETIM §6 M2): iki kisilik, hizli ve hafif arac. Kural Sim'de (Vehicles.Step);
/// bu dugum yalniz govdeyi kurar, zemine oturtur ve surucunun girdisini kurala verir.
/// Govde koddan yer tutucudur (vehicles.json placeholder); Tripo modeli gelince Assets.Load ile degisir.
/// </summary>
public partial class CekirgeAtv : Node3D
{
    public VehicleDef Def { get; }
    public VehicleState State { get; }
    public float Travelled { get; private set; }
    /// <summary>Zemin yuksekligi (x, z) -> y. Dunyada World.HeightAt; testte duz zemin.</summary>
    public Func<float, float, float> Height = (_, _) => 0f;
    Node3D _body;
    readonly Node3D[] _wheels = new Node3D[4];
    const float Wheelbase = 1.3f, WheelR = 0.32f;

    public CekirgeAtv(VehicleDef def, VehicleState state)
    {
        Def = def;
        State = state;
        Name = "Cekirge_" + state.Id;
        AddToGroup("vehicles");
        BuildBody();
    }

    void BuildBody()
    {
        _body = new Node3D { Name = "Visual" };
        AddChild(_body);
        var olive = Palette.Plant.Lerp(Palette.Shadow, 0.35f);
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.95f, 0.32f, 1.7f), MeshKit.At(0, 0.62f, 0), olive, 0.4f);              // govde
        kit.Pillow(new Vector3(1.1f, 0.12f, 0.5f), MeshKit.At(0, 0.8f, 0.72f), olive.Lerp(Palette.Warning, 0.3f), 0.3f); // on camurluk
        kit.Pillow(new Vector3(1.1f, 0.12f, 0.5f), MeshKit.At(0, 0.8f, -0.72f), olive.Lerp(Palette.Warning, 0.3f), 0.3f); // arka camurluk
        kit.Pillow(new Vector3(0.5f, 0.16f, 0.95f), MeshKit.At(0, 0.88f, -0.2f), Palette.DarkGrey, 0.5f);   // iki kisilik sele
        kit.Cylinder(0.04f, 0.05f, 0.5f, MeshKit.At(new Vector3(0, 1.0f, 0.55f), new Vector3(-0.4f, 0, 0)), Palette.DarkGrey, 8); // gidon kolonu
        kit.Cylinder(0.03f, 0.03f, 0.8f, MeshKit.At(new Vector3(0, 1.22f, 0.48f), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 8); // gidon
        kit.Sphere(0.09f, MeshKit.At(0, 0.78f, 0.9f), Palette.Warning, 10, 6); // far
        kit.Pillow(new Vector3(0.6f, 0.25f, 0.4f), MeshKit.At(0, 0.98f, -0.95f), Palette.Wood, 0.3f); // yuk yuvasi (1 yuva)
        _body.AddChild(kit.Instance());
        int i = 0;
        foreach (var z in new[] { Wheelbase * 0.5f, -Wheelbase * 0.5f })
            foreach (var x in new[] { -0.55f, 0.55f })
            {
                var w = new Node3D { Position = new Vector3(x, WheelR, z) };
                var wk = new MeshKit();
                wk.Cylinder(WheelR, WheelR, 0.26f, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 14);
                wk.Cylinder(0.14f, 0.14f, 0.28f, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2f)), Palette.OffWhite, 10);
                w.AddChild(wk.Instance());
                _body.AddChild(w);
                _wheels[i++] = w;
            }
    }

    /// <summary>Surucu koltugu (dunya): binen oyuncu buraya oturur.</summary>
    public Vector3 Seat => GlobalPosition + GlobalBasis.Y * 0.95f - GlobalBasis.Z * 0.1f;
    public bool InReach(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < 2.4f;

    /// <summary>Bir adim: girdi Sim kuralina gider, dugum yeni konum ve egime oturur.</summary>
    public void Drive(VehicleInput input, float dt)
    {
        float yaw = State.Yaw;
        var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        float hf = Height(State.X + fwd.X * Wheelbase * 0.5f, State.Z + fwd.Z * Wheelbase * 0.5f);
        float hb = Height(State.X - fwd.X * Wheelbase * 0.5f, State.Z - fwd.Z * Wheelbase * 0.5f);
        input.SlopeDeg = Mathf.RadToDeg(Mathf.Abs(Mathf.Atan2(hf - hb, Wheelbase)));
        float x0 = State.X, z0 = State.Z;
        Vehicles.Step(Def, State, input, dt);
        Travelled += new Vector2(State.X - x0, State.Z - z0).Length();
        Place(hf, hb);
        foreach (var w in _wheels) w.RotateX(State.Speed * dt / WheelR);
    }

    /// <summary>Durumu dugume yansitir (yukleme, dogma).</summary>
    public void Place(float? hf = null, float? hb = null)
    {
        float yaw = State.Yaw;
        var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        float f = hf ?? Height(State.X + fwd.X * Wheelbase * 0.5f, State.Z + fwd.Z * Wheelbase * 0.5f);
        float b = hb ?? Height(State.X - fwd.X * Wheelbase * 0.5f, State.Z - fwd.Z * Wheelbase * 0.5f);
        float pitch = Mathf.Atan2(f - b, Wheelbase);
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, -pitch);
        if (State.Flipped) basis *= new Basis(Vector3.Forward, Mathf.Pi * 0.5f);
        var pos = new Vector3(State.X, (f + b) * 0.5f, State.Z);
        if (IsInsideTree()) GlobalTransform = new Transform3D(basis, pos);
        else Transform = new Transform3D(basis, pos);
    }
}
