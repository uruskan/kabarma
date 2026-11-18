using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Katalog teslimat kapsulu (GDD 13 "Katalog"; ekonomi2): gokten iner, yere oturur, E ile acilir; icindekiler cantaya
/// gecer (Catalog.Collect), sigmayan kapsulde kalir. Gorunus: Kooperatif kapsulunun kucugu (altin varlik kapsul.glb,
/// yoksa MeshKit). YER TUTUCU inis efekti: duz dusus + toz; parasut ve ses M6 cilasinda.
/// </summary>
public partial class EconomyDeliveryPod : Node3D
{
    public const float PodScale = 0.36f;
    const float DropHeight = 38f, DropSeconds = 2.4f;
    public Delivery Delivery { get; }
    public bool Landed { get; private set; }
    Vector3 _ground;
    float _t;
    Node3D _body;
    OmniLight3D _beacon;

    public EconomyDeliveryPod(Delivery dl, Vector3 ground, bool instant = false)
    {
        Delivery = dl;
        _ground = ground;
        Name = $"Teslimat{dl.Id}";
        AddToGroup("deliveries");
        _body = new Node3D();
        var golden = Assets.Load("res://assets/models/kapsul/kapsul.glb");
        if (golden != null)
        {
            golden.RotationDegrees = new Vector3(0, -90, 0);
            golden.Scale = Vector3.One * PodScale;
            _body.AddChild(golden);
        }
        else
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(0.95f, 0.8f, 0.95f), MeshKit.At(0, 0.48f, 0), Palette.OffWhite, 0.55f, 14, 8);
            kit.Pillow(new Vector3(1.0f, 0.13f, 1.0f), MeshKit.At(0, 0.2f, 0), Palette.CoopBlue, 0.4f);
            kit.Pillow(new Vector3(1.0f, 0.1f, 1.0f), MeshKit.At(0, 0.7f, 0), Palette.Warning, 0.4f);
            _body.AddChild(kit.Instance());
        }
        // Isaret: ustte turuncu fener (uzaktan bulunsun)
        var top = new MeshKit();
        top.Cylinder(0.06f, 0.08f, 0.5f, MeshKit.At(0, 1.15f, 0), Palette.DarkGrey, 6);
        top.Sphere(0.13f, MeshKit.At(0, 1.45f, 0), Palette.Warning, 10, 6);
        _body.AddChild(top.Instance());
        AddChild(_body);
        _beacon = new OmniLight3D { LightColor = Palette.Warning, OmniRange = 4f, LightEnergy = 1.2f, Position = new Vector3(0, 1.5f, 0) };
        AddChild(_beacon);
        if (instant) Land();
    }

    public override void _Ready()
    {
        if (Landed) { GlobalPosition = _ground; return; }
        GlobalPosition = _ground + Vector3.Up * DropHeight;
    }

    public void LandNow() { if (!Landed) Land(); }

    void Land()
    {
        Landed = true;
        if (IsInsideTree())
        {
            GlobalPosition = _ground;
            Fx.Burst(_ground + Vector3.Up * 0.2f, Palette.Ground, 26, 5f, 0.14f, 0.8f);
            Fx.Burst(_ground + Vector3.Up * 0.6f, Palette.Warning, 10, 3f, 0.08f, 0.5f, true, 2f);
            Sfx.Play("place", _ground, 2f);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Landed)
        {
            _t += dt;
            _beacon.LightEnergy = 0.8f + 0.5f * Mathf.Sin(_t * 4f);
            return;
        }
        _t += dt;
        float k = Mathf.Clamp(_t / DropSeconds, 0f, 1f);
        GlobalPosition = _ground + Vector3.Up * DropHeight * (1f - k * k);
        _body.RotationDegrees = new Vector3(0, _t * 90f, 0);
        if (k >= 1f) Land();
    }

    /// <summary>Oyuncu acabilecek kadar yakin mi (yatay).</summary>
    public bool InReach(Vector3 p) => Landed && new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < 2.2f;
}
