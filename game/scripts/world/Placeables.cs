using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Yerlestirilebilirlerin gorunusu (ICERIK §10.2, §11). Istasyonlar altin modelden; Ocakbasi koddan (kaya modelinden tas
/// halka + izgara + ates; URETIM §6 M1); Sandik, Uyku Tulumu ve Mesale yapi kitinin kalas dilinden (pipeline/build_placeables.py).
/// </summary>
public static class PlaceableVisuals
{
    public const string Dir = "res://assets/models/placeables";

    public static Node3D Make(PlaceableDef def)
    {
        if (PowerVisuals.Has(def.Id)) return PowerVisuals.Make(def.Id); // guc akisi: koddan modeller (power/PowerVisuals.cs)
        switch (def.Id)
        {
            case "ocak": return new Ocak();
            case "tas_firin": return new TasFirin();
            case "ocakbasi": return new OcakbasiVisual();
            case "mesale": return new MesaleVisual(def.LightRange);
            case "korkuluk": return KorkulukVisual.Make(); // tarim (M2), FarmView.Meshes.cs
        }
        var m = def.Model != null ? Assets.Load(def.Model) : null;
        m ??= Assets.Load($"{Dir}/{def.Id}.glb");
        if (m != null) return m;
        var m4 = DefenseVisuals4.Make(def) ?? PropVisuals4.Make(def); // kale (M4): kuleler, tuzaklar ve kalan yerlestirilebilirler (koddan yer tutucu)
        if (m4 != null) return m4;
        // Yedek: koddan kutu
        var kit = new MeshKit();
        kit.Pillow(new Vector3(1.4f, 0.9f, 0.8f), MeshKit.At(0, 0.45f, 0), Palette.Wood, 0.3f);
        return kit.Instance();
    }

    /// <summary>Gorselin kapsayan kutusu (yerel). Bos ise false.</summary>
    public static bool Bounds(Node3D vis, out Aabb aabb)
    {
        aabb = new Aabb();
        bool first = true;
        foreach (var mi in vis.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var b = Rel(vis, mi) * mi.GetAabb();
            aabb = first ? b : aabb.Merge(b);
            first = false;
        }
        return !first;
    }

    static Transform3D Rel(Node3D root, Node3D n)
    {
        var xf = Transform3D.Identity;
        Node cur = n;
        while (cur != null && cur != root) { if (cur is Node3D n3) xf = n3.Transform * xf; cur = cur.GetParent(); }
        return xf;
    }

    /// <summary>Titrek ates: isik + yukari ucan kor parcaciklari.</summary>
    public static (OmniLight3D light, GpuParticles3D flame) Fire(Node3D parent, Vector3 at, float range, float size)
    {
        var light = new OmniLight3D { LightColor = new Color("FFB066"), LightEnergy = 1.4f, OmniRange = range, Position = at + Vector3.Up * 0.2f, ShadowEnabled = false };
        parent.AddChild(light);
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up, Spread = 14f, InitialVelocityMin = 0.5f * size, InitialVelocityMax = 1.3f * size,
            Gravity = new Vector3(0, 0.6f, 0), ScaleMin = 0.6f, ScaleMax = 1.1f, DampingMin = 0.5f, DampingMax = 1f,
        };
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 1f)); curve.AddPoint(new Vector2(1, 0.1f));
        pm.ScaleCurve = new CurveTexture { Curve = curve };
        var grad = new Gradient();
        grad.SetColor(0, new Color("FFE08A"));
        grad.SetColor(1, new Color(Palette.Accent, 0f));
        pm.ColorRamp = new GradientTexture1D { Gradient = grad };
        var flame = new GpuParticles3D
        {
            Amount = 22, Lifetime = 0.55f, ProcessMaterial = pm, Position = at,
            DrawPass1 = new SphereMesh
            {
                Radius = 0.09f * size, Height = 0.18f * size, RadialSegments = 6, Rings = 3,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha, EmissionEnabled = true, Emission = new Color("FFB066"), EmissionEnergyMultiplier = 2f,
                },
            },
        };
        parent.AddChild(flame);
        return (light, flame);
    }
}

/// <summary>Ocakbasi: kaya modelinden tas halka, demir izgara, odun ve ates. Pisirirken ates buyur.</summary>
public partial class OcakbasiVisual : Node3D
{
    OmniLight3D _light;
    GpuParticles3D _flame;
    float _t;
    public bool Working;

    public OcakbasiVisual()
    {
        // gorsel-model: Blender'da kurulan model (pipeline/build_props.py); yoksa asagidaki koddan kit.
        if (Assets.Load("res://assets/models/props/ocakbasi.glb") is Node3D prop)
        {
            AddChild(prop);
            (_light, _flame) = PlaceableVisuals.Fire(this, new Vector3(0, 0.16f, 0), 7f, 1.6f);
            return;
        }
        const int stones = 8;
        var rng = new RandomNumberGenerator { Seed = 4417 };
        for (int i = 0; i < stones; i++)
        {
            float a = i * Mathf.Tau / stones;
            var p = new Vector3(Mathf.Cos(a) * 0.62f, 0, Mathf.Sin(a) * 0.62f);
            var rock = Assets.Load("res://assets/models/kaya/kaya.glb");
            if (rock != null)
            {
                rock.Scale = Vector3.One * rng.RandfRange(0.2f, 0.26f);
                rock.Rotation = new Vector3(0, rng.Randf() * Mathf.Tau, 0);
                rock.Position = p + Vector3.Down * 0.04f;
                AddChild(rock);
            }
            else
            {
                var k = new MeshKit();
                k.Pillow(new Vector3(0.36f, 0.3f, 0.32f), MeshKit.At(p + Vector3.Up * 0.14f, new Vector3(0, a, 0)), Palette.Rock.Lerp(Palette.RockDark, rng.Randf() * 0.5f), 0.5f, 8, 5);
                AddChild(k.Instance());
            }
        }
        var kit = new MeshKit();
        // kor yatagi ve odunlar
        kit.Cylinder(0.48f, 0.5f, 0.06f, MeshKit.At(0, 0.03f, 0), Palette.DarkGrey.Lerp(Palette.Shadow, 0.4f), 14);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f;
            kit.Cylinder(0.055f, 0.06f, 0.62f, MeshKit.At(new Vector3(0, 0.11f, 0), new Vector3(Mathf.Pi / 2f, a, 0.25f)), Palette.Wood.Lerp(Palette.Shadow, 0.35f), 7);
        }
        // izgara: dort ayak, cerceve ve cubuklar
        var iron = Palette.DarkGrey;
        foreach (var (x, z) in new[] { (-0.42f, -0.42f), (0.42f, -0.42f), (-0.42f, 0.42f), (0.42f, 0.42f) })
            kit.Cylinder(0.025f, 0.025f, 0.5f, MeshKit.At(x, 0.25f, z), iron, 6);
        foreach (var s in new[] { -1f, 1f })
        {
            kit.Cylinder(0.022f, 0.022f, 0.9f, MeshKit.At(new Vector3(s * 0.42f, 0.5f, 0), new Vector3(Mathf.Pi / 2f, 0, 0)), iron, 6);
            kit.Cylinder(0.022f, 0.022f, 0.9f, MeshKit.At(new Vector3(0, 0.5f, s * 0.42f), new Vector3(0, 0, Mathf.Pi / 2f)), iron, 6);
        }
        for (int i = -3; i <= 3; i++)
            kit.Cylinder(0.014f, 0.014f, 0.84f, MeshKit.At(new Vector3(i * 0.11f, 0.51f, 0), new Vector3(Mathf.Pi / 2f, 0, 0)), iron.Lerp(Palette.Accent, 0.12f), 5);
        // izgarada bir tencere (yemek istasyonu oldugu belli olsun)
        kit.Cylinder(0.2f, 0.17f, 0.22f, MeshKit.At(0.12f, 0.64f, 0.05f), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.35f), 12);
        kit.Cylinder(0.205f, 0.205f, 0.03f, MeshKit.At(0.12f, 0.76f, 0.05f), Palette.OffWhite.Lerp(Palette.Shadow, 0.2f), 12);
        AddChild(kit.Instance());
        (_light, _flame) = PlaceableVisuals.Fire(this, new Vector3(0, 0.16f, 0), 7f, 1.6f);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float k = Working ? 1f : 0.45f;
        _light.LightEnergy = (1.2f + Mathf.Sin(_t * 7f) * 0.15f + Mathf.Sin(_t * 17f) * 0.08f) * k;
        _flame.AmountRatio = Working ? 1f : 0.4f;
    }
}

/// <summary>Mesale: kalas dilinden sap, tepesinde ates; 6 m gece isigi (ICERIK §10.2).</summary>
public partial class MesaleVisual : Node3D
{
    OmniLight3D _light;
    float _t;

    public MesaleVisual(float range)
    {
        var m = Assets.Load($"{PlaceableVisuals.Dir}/mesale.glb");
        if (m != null) AddChild(m);
        else
        {
            var kit = new MeshKit();
            kit.Cylinder(0.05f, 0.06f, 1.5f, MeshKit.At(0, 0.75f, 0), Palette.Wood, 7);
            kit.Cylinder(0.1f, 0.09f, 0.26f, MeshKit.At(0, 1.55f, 0), Palette.Wood.Lerp(Palette.Shadow, 0.6f), 8);
            AddChild(kit.Instance());
        }
        (_light, _) = PlaceableVisuals.Fire(this, new Vector3(0, 1.72f, 0), Mathf.Max(1f, range), 0.9f);
        _light.Name = "Light";
        _light.OmniAttenuation = 1.2f;
    }

    public float Range => _light.OmniRange;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        _light.LightEnergy = 1.6f + Mathf.Sin(_t * 9f) * 0.18f + Mathf.Sin(_t * 23f) * 0.1f;
    }
}

/// <summary>
/// Istasyon olmayan yerlestirilebilir (Sandik, Uyku Tulumu, Mesale): Insa ile kurulur, Insa'nin Sok kipiyle sokulur.
/// Sandik 20 yuvalik depo (E: ac), Uyku Tulumu dogma noktasi ve uyku yeri (E: uyu).
/// </summary>
public partial class Furniture : Node3D
{
    public PlaceableDef Def { get; }
    /// <summary>Sandik: depo yuvalari (yoksa null).</summary>
    public Inventory Storage { get; }
    readonly Director _d;
    Label3D _label;
    Node3D _lid;
    float _open, _bounce;
    public bool Opened;

    public Furniture(Director d, PlaceableDef def)
    {
        _d = d; Def = def; Name = def.Id;
        if (def.Slots > 0) Storage = def.Stack > 0 ? new Inventory(def.Slots, _ => def.Stack) : new Inventory(def.Slots, d.Data.Tool.StackOf);
    }

    public override void _Ready()
    {
        Callable.From(() => { if (IsInstanceValid(this) && IsInsideTree()) _d.World?.ClearGrass(GlobalPosition, Def.Radius + 0.2f); }).CallDeferred(); // altindaki ot kalkar (gorsel-dunya; konum AddChild'dan sonra verilir)
        var vis = PlaceableVisuals.Make(Def);
        AddChild(vis);
        _lid = vis.FindChild("lid", true, false) as Node3D;
        bool any = PlaceableVisuals.Bounds(vis, out var aabb);
        if (any && Def.Id != "uyku_tulumu") // tulumun ustune basilir
        {
            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = aabb.Size * new Vector3(0.9f, 1f, 0.9f) }, Position = aabb.GetCenter() });
            AddChild(body);
        }
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 36, OutlineSize = 12, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = Palette.NightDeep, NoDepthTest = true,
            Position = new Vector3(0, (any ? aabb.End.Y : 1f) + 0.4f, 0), Visible = false,
        };
        AddChild(_label);
    }

    public bool InReach(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() < Def.Radius + 1.8f;

    public void Bounce() => _bounce = 1f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool near = Def.LightRange <= 0 && InReach(_d.Player.GlobalPosition);
        _label.Visible = near;
        if (near)
            _label.Text = Storage != null ? $"{Def.Name}  {Storage.UsedSlots}/{Storage.Slots}\nE: aç" : Def.Spawn ? $"{Def.Name}\nE: uyu" : Def.Name;
        if (_lid != null)
        {
            _open = Mathf.MoveToward(_open, Opened && _d.Hud.MenuOpen ? 1f : 0f, dt * 4f);
            _lid.Rotation = new Vector3(-Mathf.SmoothStep(0, 1, _open) * 1.6f, 0, 0);
        }
        if (_bounce > 0)
        {
            _bounce = Mathf.Max(0, _bounce - dt * 4f);
            float k = Mathf.Sin(_bounce * Mathf.Pi) * 0.06f;
            GetChild<Node3D>(0).Scale = new Vector3(1 + k, 1 - k, 1 + k);
        }
        if (!(_d.Hud.MenuOpen)) Opened = false;
    }
}
