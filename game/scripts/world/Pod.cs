using Godot;

namespace Kabarma.Game;

/// <summary>Inis kapsulu; safakta Kargo Rampasi'na doner (ICERIK: "Kapsulun kendisi ilk Kargo Rampasi'na donusur").</summary>
public partial class Pod : Node3D
{
    Node3D _hatch;
    MeshInstance3D _pad;
    Node3D _rocket;
    public bool IsRamp { get; private set; }
    float _rampT = -1, _launchT = -1;
    GpuParticles3D _flame;
    public Vector3 PadCenter => GlobalTransform * new Vector3(0, 0.3f, 3.2f); // rampa kapsulle birlikte doner

    Node3D _golden;

    public override void _Ready()
    {
        if (_golden == null) return;
        // Kapak parcasi (Tripo parcalama): alt kenarindan menteseli; acilinca rampa olur.
        var hatch = _golden.FindChild("hatch", true, false) as MeshInstance3D;
        if (hatch == null) return;
        var box = hatch.GlobalTransform * hatch.GetAabb();
        var local = GlobalTransform.AffineInverse();
        var lo = local * box.Position; var hi = local * box.End;
        var pivot = new Node3D { Name = "HatchHinge" };
        AddChild(pivot);
        pivot.Position = new Vector3((lo.X + hi.X) / 2f, Mathf.Min(lo.Y, hi.Y), Mathf.Max(lo.Z, hi.Z));
        hatch.Reparent(pivot, true);
        _hatch.QueueFree();
        _hatch = pivot;
    }

    public Pod()
    {
        _golden = Assets.Load("res://assets/models/kapsul/kapsul.glb");
        if (_golden != null)
        {
            _golden.RotationDegrees = new Vector3(0, -90, 0); // Tripo +X'e bakar; kapak +Z'ye (rampa yonu)
            AddChild(_golden);
        }
        var kit = new MeshKit();
        kit.Pillow(new Vector3(2.6f, 2.2f, 2.6f), MeshKit.At(0, 1.3f, 0), Palette.OffWhite, 0.55f, 18, 10);
        kit.Pillow(new Vector3(2.75f, 0.35f, 2.75f), MeshKit.At(0, 0.55f, 0), Palette.CoopBlue, 0.4f);
        kit.Pillow(new Vector3(2.75f, 0.25f, 2.75f), MeshKit.At(0, 1.9f, 0), Palette.Warning, 0.4f);
        kit.Cylinder(0.25f, 0.45f, 0.6f, MeshKit.At(0, 2.6f, 0), Palette.DarkGrey);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi / 2f + Mathf.Pi / 4f;
            kit.Cylinder(0.14f, 0.2f, 1.0f, MeshKit.At(new Vector3(Mathf.Cos(a) * 1.3f, 0.35f, Mathf.Sin(a) * 1.3f), new Vector3(Mathf.Sin(a) * 0.5f, 0, -Mathf.Cos(a) * 0.5f)), Palette.DarkGrey, 6);
        }
        // Kooperatif logosu: mavi daire
        kit.Cylinder(0.45f, 0.45f, 0.06f, MeshKit.At(new Vector3(0, 1.25f, 1.28f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.CoopBlue, 16);
        kit.Cylinder(0.28f, 0.28f, 0.07f, MeshKit.At(new Vector3(0, 1.25f, 1.3f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.Warning, 12);
        if (_golden == null) AddChild(kit.Instance());

        _hatch = new Node3D { Position = new Vector3(0, 0.5f, 1.25f) };
        var hk = new MeshKit();
        hk.Pillow(new Vector3(1.6f, 1.6f, 0.2f), MeshKit.At(0, 0.8f, 0), Palette.OffWhite.Lerp(Palette.Shadow, 0.08f), 0.4f);
        hk.Pillow(new Vector3(1.3f, 0.12f, 0.24f), MeshKit.At(0, 1.35f, 0), Palette.Warning, 0.4f);
        if (_golden == null) _hatch.AddChild(hk.Instance());
        AddChild(_hatch);

        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.35f, Height = 2.6f }, Position = new Vector3(0, 1.3f, 0) });
        AddChild(body);

        var padKit = new MeshKit();
        padKit.Pillow(new Vector3(3.2f, 0.3f, 3.2f), MeshKit.At(0, 0.15f, 0), Palette.DarkGrey, 0.35f);
        padKit.Pillow(new Vector3(2.6f, 0.12f, 2.6f), MeshKit.At(0, 0.32f, 0), Palette.Warning, 0.35f);
        padKit.Pillow(new Vector3(1.6f, 0.13f, 1.6f), MeshKit.At(0, 0.36f, 0), Palette.DarkGrey.Lerp(Palette.Warning, 0.2f), 0.35f);
        _pad = padKit.Instance();
        _pad.Position = new Vector3(0, -0.6f, 3.2f);
        _pad.Visible = false;
        AddChild(_pad);

        _rocket = new Node3D();
        var rk = new MeshKit();
        rk.Pillow(new Vector3(0.7f, 1.4f, 0.7f), MeshKit.At(0, 0.9f, 0), Palette.OffWhite, 0.6f);
        rk.Cylinder(0.0f, 0.36f, 0.6f, MeshKit.At(0, 1.85f, 0), Palette.Accent);
        rk.Pillow(new Vector3(0.9f, 0.3f, 0.25f), MeshKit.At(0, 0.35f, 0), Palette.CoopBlue, 0.4f);
        rk.Pillow(new Vector3(0.25f, 0.3f, 0.9f), MeshKit.At(0, 0.35f, 0), Palette.CoopBlue, 0.4f);
        _rocket.AddChild(rk.Instance());
        _flame = Fx.MakeTrail(Palette.Warning, Palette.Accent);
        _flame.Position = new Vector3(0, 0.1f, 0);
        _flame.Emitting = false;
        _rocket.AddChild(_flame);
        _rocket.Visible = false;
        AddChild(_rocket);
    }

    public void BecomeRamp() { if (_rampT < 0) _rampT = 0; IsRamp = true; _pad.Visible = true; }

    /// <summary>Yuku roketle gonderir; roket kalkar.</summary>
    public void Launch()
    {
        _rocket.Visible = true;
        _rocket.Position = new Vector3(0, 0.4f, 3.2f);
        _flame.Emitting = true;
        _launchT = 0;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_rampT >= 0 && _rampT < 1.2f)
        {
            _rampT += dt;
            float k = Mathf.Clamp(_rampT / 1.2f, 0, 1);
            _hatch.Rotation = new Vector3(Mathf.Lerp(0, 1.45f, Mathf.SmoothStep(0, 1, k)), 0, 0);
            _pad.Position = new Vector3(0, Mathf.Lerp(-0.6f, 0f, k), 3.2f);
        }
        if (_launchT >= 0)
        {
            _launchT += dt;
            float up = _launchT < 0.6f ? _launchT * 0.5f : 0.3f + Mathf.Pow(_launchT - 0.6f, 2f) * 14f;
            _rocket.Position = new Vector3(Mathf.Sin(_launchT * 9f) * 0.03f, 0.4f + up, 3.2f);
            if (_launchT > 6f) { _rocket.Visible = false; _flame.Emitting = false; _launchT = -1; }
        }
    }
}
