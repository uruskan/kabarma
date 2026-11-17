using Godot;

namespace Kabarma.Game;

/// <summary>Kazilabilir kaya: Kazma isiniyla kemirilir, her birim tas cantaya emilir (PLAN 1.4, 0:30).</summary>
public partial class Rock : Node3D
{
    public int Stones { get; private set; }
    readonly int _max;
    readonly float _size;
    float _wear;
    Node3D _mesh;
    ShaderMaterial _mat;
    float _flash;

    public Rock(int stones, float size, ulong seed)
    {
        Stones = _max = stones;
        _size = size;
        var r = new RandomNumberGenerator { Seed = seed };
        // M0 denemesi #1: Tripo'dan gelen dokulu kaya (uretim hattinda 3.879 ucgen, 1K doku). Yoksa koddan kit.
        var model = Assets.Load("res://assets/models/kaya/kaya.glb");
        if (model != null)
        {
            _mesh = new Node3D();
            model.Scale = Vector3.One * size;
            model.Rotation = new Vector3(0, r.Randf() * Mathf.Tau, 0);
            _mesh.AddChild(model);
            AddChild(_mesh);
        }
        else
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(1.8f, 1.3f, 1.5f) * size, MeshKit.At(new Vector3(0, 0.55f * size, 0), new Vector3(0, r.Randf() * 3f, 0.1f)), Palette.Rock, 0.5f, 10, 6);
            kit.Pillow(new Vector3(1.0f, 0.9f, 1.0f) * size, MeshKit.At(new Vector3(0.7f * size, 0.35f * size, 0.4f * size), new Vector3(0.2f, r.Randf() * 3f, 0)), Palette.RockDark, 0.5f, 8, 5);
            kit.Pillow(new Vector3(0.45f, 0.35f, 0.4f) * size, MeshKit.At(new Vector3(-0.3f * size, 1.15f * size, 0.1f)), Palette.Warning.Lerp(Palette.Rock, 0.4f), 0.5f, 8, 4);
            _mat = MeshKit.NewLiveMaterial();
            _mesh = kit.Instance(_mat);
            AddChild(_mesh);
        }
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.85f * size }, Position = new Vector3(0, 0.6f * size, 0) });
        AddChild(body);
        AddToGroup("rocks");
    }

    public Vector3 Center => GlobalPosition + new Vector3(0, 0.6f * _size, 0);
    public float Radius => 1.0f * _size;

    /// <returns>Bu karede kopan tas sayisi.</returns>
    public int Mine(float amount)
    {
        if (Stones <= 0) return 0;
        _wear += amount;
        _flash = 0.6f;
        int got = 0;
        while (_wear >= 1f && Stones > 0) { _wear -= 1f; Stones--; got++; }
        float k = 0.45f + 0.55f * Stones / (float)_max;
        _mesh.Scale = Vector3.One * k;
        return got;
    }

    public override void _Process(double delta)
    {
        if (_flash > 0) { _flash = Mathf.Max(0, _flash - (float)delta * 3f); _mat?.SetShaderParameter("flash", _flash * 0.4f); }
        if (Stones <= 0 && _flash <= 0) QueueFree();
        // titreme
        if (_flash > 0) _mesh.Position = new Vector3(Mathf.Sin(Time.GetTicksMsec() * 0.09f), 0, Mathf.Cos(Time.GetTicksMsec() * 0.11f)) * 0.04f * _flash;
    }
}

/// <summary>Ocak: ussun kalbi. Cevresine ilik bir isik kubbesi (tapu) yayar (PLAN 1.4, 2:00; GDD 6).</summary>
public partial class Ocak : Node3D
{
    MeshInstance3D _dome;
    OmniLight3D _light;
    float _t;
    public const float ClaimRadius = 40f; // T1 tapu yaricapi

    public Ocak()
    {
        // Tiknaz bir ocak: koyu taban, yuvarlak govde, isiyan agiz, kooperatif mavisi kubbe, kisa baca.
        var kit = new MeshKit();
        kit.Pillow(new Vector3(1.9f, 0.4f, 1.9f), MeshKit.At(0, 0.2f, 0), Palette.DarkGrey, 0.4f);
        kit.Pillow(new Vector3(1.55f, 1.0f, 1.55f), MeshKit.At(0, 0.85f, 0), Palette.OffWhite, 0.6f, 18, 10);
        kit.Sphere(0.82f, MeshKit.At(new Vector3(0, 1.35f, 0), Vector3.Zero, new Vector3(1f, 0.55f, 1f)), Palette.CoopBlue, 16, 8);
        kit.Pillow(new Vector3(1.62f, 0.14f, 1.62f), MeshKit.At(0, 1.32f, 0), Palette.Warning, 0.4f, 16, 6);
        kit.Pillow(new Vector3(0.75f, 0.55f, 0.2f), MeshKit.At(0, 0.78f, 0.72f), Palette.DarkGrey, 0.5f);
        kit.Pillow(new Vector3(0.55f, 0.38f, 0.12f), MeshKit.At(0, 0.76f, 0.8f), Palette.Accent.Lerp(Palette.Warning, 0.45f), 0.5f);
        kit.Cylinder(0.16f, 0.2f, 0.55f, MeshKit.At(0.38f, 1.75f, -0.25f), Palette.DarkGrey, 10);
        kit.Pillow(new Vector3(0.48f, 0.12f, 0.48f), MeshKit.At(0.38f, 2.05f, -0.25f), Palette.DarkGrey.Lerp(Palette.Shadow, 0.3f), 0.5f);
        kit.Sphere(0.1f, MeshKit.At(-0.5f, 1.05f, 0.62f), Palette.Warning, 8, 4);
        kit.Sphere(0.1f, MeshKit.At(0.5f, 1.05f, 0.62f), Palette.MayaTeal, 8, 4);
        AddChild(kit.Instance());
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.8f, Height = 2f }, Position = new Vector3(0, 1f, 0) });
        AddChild(body);

        _light = new OmniLight3D { LightColor = new Color("FFB066"), LightEnergy = 1.6f, OmniRange = 9f, Position = new Vector3(0, 1.2f, 0.9f) };
        AddChild(_light);
        var domeMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(Palette.Warning, 0.07f),
            CullMode = BaseMaterial3D.CullModeEnum.Front,
            NoDepthTest = false,
        };
        _dome = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 32, Rings = 16, IsHemisphere = true }, MaterialOverride = domeMat };
        _dome.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _dome.Scale = Vector3.One * 0.1f;
        AddChild(_dome);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        // kubbe acilisi: 1,5 sn'de tapu yaricapina (gorsel olarak 18 m; tam yaricap sisle kayboluyor)
        float k = Mathf.Min(1f, _t / 1.5f);
        float r = Mathf.Lerp(0.1f, 18f, 1f - Mathf.Pow(1f - k, 3f));
        _dome.Scale = new Vector3(r, r * 0.5f, r);
        _light.LightEnergy = 1.5f + Mathf.Sin(_t * 5f) * 0.12f + Mathf.Sin(_t * 13f) * 0.06f;
    }
}

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

    public Pod()
    {
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
        AddChild(kit.Instance());

        _hatch = new Node3D { Position = new Vector3(0, 0.5f, 1.25f) };
        var hk = new MeshKit();
        hk.Pillow(new Vector3(1.6f, 1.6f, 0.2f), MeshKit.At(0, 0.8f, 0), Palette.OffWhite.Lerp(Palette.Shadow, 0.08f), 0.4f);
        hk.Pillow(new Vector3(1.3f, 0.12f, 0.24f), MeshKit.At(0, 1.35f, 0), Palette.Warning, 0.4f);
        _hatch.AddChild(hk.Instance());
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
