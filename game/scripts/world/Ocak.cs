using Godot;

namespace Kabarma.Game;

/// <summary>Ocak: ussun kalbi. Cevresine ilik bir isik kubbesi (tapu) yayar (PLAN 1.4, 2:00; GDD 6).</summary>
public partial class Ocak : Node3D
{
    MeshInstance3D _dome;
    OmniLight3D _light;
    float _t;
    public const float ClaimRadius = 40f; // T1 tapu yaricapi

    public Ocak()
    {
        // M1 altin varlik: Tripo'dan dokulu Ocak (2 m). Yoksa koddan kit.
        var golden = Assets.Load("res://assets/models/ocak/ocak.glb");
        if (golden != null) AddChild(golden);
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
        if (golden == null) AddChild(kit.Instance());
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
