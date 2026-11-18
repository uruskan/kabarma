using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuk odasi (M3, oyuk akisi; TEKNIK §7.5): koddan kurulan hazir ic mekan. YER TUTUCU gorunus (MeshKit): taban diski,
/// kaya duvar halkasi, tavan, kapi kemeri, sandiklar; Kostebek odasinda delikler ve tikac kayalari, Yarasa odasinda
/// duvar kandilleri. Kural OyukSystem'de ve Sim'de (MoleFight, BatKnotFight).
/// </summary>
public partial class OyukRoom : Node3D
{
    public Poi Poi;
    public OyukGuardianDef Guardian;
    public int Tier, Index;
    public float Radius, Height;
    public Vector3 Center;
    public Vector3 DoorPos;
    public readonly List<OyukChest> Chests = new();
    public readonly List<OyukLamp> Lamps = new();
    public readonly List<PlugRock> Plugs = new();
    public readonly List<Node3D> HoleNodes = new();

    public static OyukRoom Build(Director d, Poi poi, OyukGuardianDef g, int index)
    {
        var def = d.Data.Oyuk.Interior;
        var (cx, cz) = OyukRules.RoomCenter(def, index);
        var room = new OyukRoom
        {
            Name = "Oyuk_" + poi.Id, Poi = poi, Guardian = g, Index = index, Tier = OyukRules.TierOf(poi),
            Radius = def.Radius, Height = g.RoomHeight > 0 ? g.RoomHeight : def.Height,
            Center = new Vector3(cx, def.Depth, cz),
        };
        room.DoorPos = room.Center + new Vector3(def.Door[0], 0, def.Door[1]);
        d.World.AddInterior(cx, cz, def.Radius, def.Depth);
        room.Shell(new Color(def.FloorColor), new Color(def.WallColor), g);
        return room;
    }

    void Shell(Color floor, Color wall, OyukGuardianDef g)
    {
        // Konum Build'den sonra AddChild ile verilir: parcalar yerel (merkez 0, taban 0).
        var body = new StaticBody3D { Name = "Shell" };
        AddChild(body);
        var floorKit = new MeshKit().Cylinder(Radius + 1.5f, Radius + 1.5f, 0.6f, MeshKit.At(0, -0.3f, 0), floor, 32);
        var rng = new RandomNumberGenerator { Seed = (ulong)(Index * 977 + 13) };
        for (int i = 0; i < 26; i++) // tabanda topak ve cakil
        {
            float a = rng.Randf() * Mathf.Tau, r = Mathf.Sqrt(rng.Randf()) * (Radius - 2f);
            floorKit.Pillow(new Vector3(rng.RandfRange(0.4f, 1.1f), rng.RandfRange(0.12f, 0.3f), rng.RandfRange(0.4f, 1.0f)),
                MeshKit.At(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), new Vector3(0, a, 0)), floor.Darkened(rng.RandfRange(0.05f, 0.25f)));
        }
        AddChild(floorKit.Instance());
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = Radius + 1.5f, Height = 0.6f }, Position = new Vector3(0, -0.3f, 0) });

        // Duvar: kaya bloklari halkasi (iceri bakar) ve carpisma kutulari.
        var wallKit = new MeshKit();
        int n = 28;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float w = Mathf.Tau * (Radius + 1f) / n * 1.35f;
            for (int k = 0; k < 3; k++)
            {
                float hy = Height * (0.2f + k * 0.33f);
                var p = dir * (Radius + 1.2f + rng.RandfRange(-0.3f, 0.4f)) + Vector3.Up * hy;
                wallKit.Pillow(new Vector3(w * rng.RandfRange(0.9f, 1.2f), Height * 0.42f, 2.2f), MeshKit.At(p, new Vector3(rng.RandfRange(-0.1f, 0.1f), -a + Mathf.Pi / 2f, 0)), wall.Darkened(rng.RandfRange(0f, 0.3f)), 0.5f);
            }
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(w, Height + 2f, 1.2f) },
                Transform = new Transform3D(new Basis(Vector3.Up, -a + Mathf.Pi / 2f), dir * (Radius + 0.9f) + Vector3.Up * (Height / 2f)),
            });
        }
        // Tavan: basik kubbe ve sarkitlar.
        wallKit.Cylinder(Radius + 2f, Radius * 0.5f, 2.4f, MeshKit.At(0, Height + 1f, 0), wall.Darkened(0.35f), 28);
        for (int i = 0; i < 18; i++)
        {
            float a = rng.Randf() * Mathf.Tau, r = Mathf.Sqrt(rng.Randf()) * (Radius - 1f), h = rng.RandfRange(0.6f, 2f);
            wallKit.Cylinder(0.02f, rng.RandfRange(0.25f, 0.5f), h, MeshKit.At(Mathf.Cos(a) * r, Height - h / 2f + 0.2f, Mathf.Sin(a) * r), wall.Darkened(0.2f), 6);
        }
        AddChild(wallKit.Instance());
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = Radius + 2f, Height = 1f }, Position = new Vector3(0, Height + 0.6f, 0) });

        // Kapi: ahsap kemer ve sicak isik (cikis).
        var door = new Vector3(DoorPos.X - Center.X, 0, DoorPos.Z - Center.Z) * 1.08f;
        var arch = new MeshKit()
            .Cylinder(0.18f, 0.22f, 3.2f, MeshKit.At(door + new Vector3(-1.3f, 1.6f, 0)), Palette.Wood, 8)
            .Cylinder(0.18f, 0.22f, 3.2f, MeshKit.At(door + new Vector3(1.3f, 1.6f, 0)), Palette.Wood, 8)
            .Pillow(new Vector3(3.2f, 0.35f, 0.4f), MeshKit.At(door + new Vector3(0, 3.25f, 0)), Palette.Wood)
            .Pillow(new Vector3(2.4f, 3.0f, 0.1f), MeshKit.At(door + new Vector3(0, 1.5f, 0.35f)), new Color(0.95f, 0.75f, 0.45f));
        AddChild(arch.Instance());
        AddChild(new OmniLight3D { Position = door + new Vector3(0, 2.2f, -1f), LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 1.4f, OmniRange = 9f, ShadowEnabled = false });
        // Zayif dolgu: oda tamamen kor olmasin (yarasa odasinda daha da zayif; karanlik dovusun parcasi).
        AddChild(new OmniLight3D { Position = new Vector3(0, Height * 0.7f, 0), LightColor = new Color(0.6f, 0.62f, 0.8f), LightEnergy = g.Kind == "yarasa" ? 0.15f : 0.6f, OmniRange = Radius * 1.6f });
    }

    /// <summary>Oda merkezine gore yerel xz → dunya.</summary>
    public Vector3 World(float x, float z, float y = 0) => Center + new Vector3(x, y, z);
    public (float x, float z) Local(Vector3 p) => (p.X - Center.X, p.Z - Center.Z);
    public bool Contains(Vector3 p) => p.Y < Center.Y + Height + 5f && p.Y > Center.Y - 20f && new Vector2(p.X - Center.X, p.Z - Center.Z).Length() < Radius + 3f;
}

/// <summary>Kostebek deligi: koyu agiz ve toprak tumsek; tikaninca uzerinde kaya.</summary>
public static class OyukHole
{
    public static Node3D Make(Color soil)
    {
        var kit = new MeshKit()
            .Cylinder(1.25f, 1.45f, 0.35f, MeshKit.At(0, 0.12f, 0), soil.Darkened(0.1f), 16)
            .Cylinder(0.9f, 0.9f, 0.06f, MeshKit.At(0, 0.3f, 0), new Color(0.08f, 0.05f, 0.04f), 16);
        var n = new Node3D { Name = "Hole" };
        n.AddChild(kit.Instance());
        return n;
    }

    /// <summary>Islaklik: delik agzinda parlak su lekesi (gorunurlugu OyukSystem acar).</summary>
    public static MeshInstance3D Puddle()
    {
        var mi = new MeshKit().Cylinder(1.1f, 1.1f, 0.04f, MeshKit.At(0, 0.36f, 0), new Color(0.35f, 0.55f, 0.8f), 16).Instance();
        mi.Name = "Wet";
        mi.Visible = false;
        return mi;
    }
}

/// <summary>Tikac kayasi: Cekim'le tasinir ("cekim" grubu), delige birakilinca tikar.</summary>
public partial class PlugRock : Node3D, ICarryable
{
    public Node3D Body => this;
    public float Kg { get; set; } = 40f;
    public float Radius => 0.65f;
    public bool Held { get; set; }
    public bool Gone => Used;
    /// <summary>Delige oturdu: artik tasinmaz.</summary>
    public bool Used;

    public override void _Ready()
    {
        AddToGroup("cekim");
        var kit = new MeshKit().Pillow(new Vector3(1.3f, 1.0f, 1.2f), MeshKit.At(0, 0.1f, 0), new Color(0.55f, 0.52f, 0.5f), 0.6f);
        AddChild(kit.Instance());
    }
}

/// <summary>Odul sandigi: koruyucu yenilince E ile acilir (bir kez).</summary>
public partial class OyukChest : Node3D
{
    internal static readonly Color CopperColor = new("C77B4A");
    public int Slot;
    public bool Opened;
    Node3D _lid;

    public override void _Ready()
    {
        var kit = new MeshKit().Pillow(new Vector3(1.3f, 0.75f, 0.85f), MeshKit.At(0, 0.38f, 0), Palette.Wood, 0.25f)
            .Pillow(new Vector3(1.36f, 0.12f, 0.9f), MeshKit.At(0, 0.2f, 0), CopperColor, 0.2f);
        AddChild(kit.Instance());
        _lid = new MeshKit().Pillow(new Vector3(1.36f, 0.3f, 0.9f), MeshKit.At(0, 0.15f, 0), Palette.Wood.Darkened(0.15f), 0.4f).Instance();
        _lid.Position = new Vector3(0, 0.75f, 0);
        AddChild(_lid);
    }

    public void Open()
    {
        if (Opened) return;
        Opened = true;
        CreateTween().TweenProperty(_lid, "rotation", new Vector3(-1.9f, 0, 0), 0.35f).SetTrans(Tween.TransitionType.Back);
    }
}

/// <summary>Duvar kandili (Yarasa odasi): E ile yakilir; yaninca isik kaynagi.</summary>
public partial class OyukLamp : Node3D
{
    public bool Lit;
    public float Left;
    OmniLight3D _light;
    MeshInstance3D _flame;

    public override void _Ready()
    {
        AddChild(new MeshKit().Cylinder(0.25f, 0.35f, 1.3f, MeshKit.At(0, 0.65f, 0), new Color(0.35f, 0.3f, 0.28f), 8)
            .Cylinder(0.45f, 0.3f, 0.25f, MeshKit.At(0, 1.4f, 0), OyukChest.CopperColor, 10).Instance());
        _flame = new MeshKit().Sphere(0.22f, MeshKit.At(0, 1.7f, 0), new Color(1f, 0.75f, 0.3f), 8, 4).Instance(new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.75f, 0.3f), EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0.2f) });
        _flame.Visible = false;
        AddChild(_flame);
        _light = new OmniLight3D { Position = new Vector3(0, 1.9f, 0), LightColor = new Color(1f, 0.72f, 0.4f), LightEnergy = 2.2f, OmniRange = 9f, Visible = false };
        AddChild(_light);
    }

    public void SetLit(bool on, float seconds = 0)
    {
        Lit = on; Left = seconds;
        _flame.Visible = on; _light.Visible = on;
    }
}
