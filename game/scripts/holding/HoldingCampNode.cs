using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bereket Holding kampinin gorunusu (GDD §19.4; holding, M3). Durum Sim'de (<see cref="HoldingCamp"/>); bu dugum her
/// kare ondan cizer: Silo, Atolye, Ahir, Sube Cekirdegi, seviyeye gore kule ve duvar halkalari, gunduz Perde kubbesi,
/// tabela ve can etiketleri. YER TUTUCU: MeshKit govdeleri (gercek yapi modelleri uretim isteginde).
/// </summary>
public partial class HoldingCampNode : Node3D
{
    readonly HoldingWorld _w;
    public int CampId { get; }
    readonly Dictionary<string, (Node3D whole, Node3D ruin, Label3D label, Vector3 center, float radius)> _parts = new();
    Node3D _rings, _ruins;
    MeshInstance3D _perde;
    StandardMaterial3D _perdeMat;
    Label3D _sign;
    int _builtLevel = -1;
    bool _builtDestroyed;

    public HoldingCampNode(HoldingWorld w, int campId) { _w = w; CampId = campId; Name = $"HoldingCamp_{campId}"; }

    public override void _Ready()
    {
        var def = _w.Def.Camp;
        foreach (var s in def.Structures)
        {
            var off = s.Offset.Count >= 2 ? new Vector3(s.Offset[0], 0, s.Offset[^1]) : Vector3.Zero;
            var root = new Node3D { Name = s.Id, Position = off };
            AddChild(root);
            var (whole, h, r) = BuildStructure(s.Id);
            root.AddChild(whole);
            var ruinKit = new MeshKit();
            ruinKit.Pillow(new Vector3(r * 1.6f, 0.7f, r * 1.4f), MeshKit.At(0, 0.3f, 0), Palette.RockDark, 0.4f);
            ruinKit.Pillow(new Vector3(r * 0.7f, 0.5f, r * 0.6f), MeshKit.At(new Vector3(r * 0.4f, 0.7f, -r * 0.2f), new Vector3(0.2f, 0.6f, 0.1f)), Palette.HoldingRed.Darkened(0.5f), 0.3f);
            var ruin = ruinKit.Instance();
            ruin.Visible = false;
            root.AddChild(ruin);
            var label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, h + 1.2f, 0),
                FontSize = 40, OutlineSize = 10, Modulate = Palette.OffWhite, PixelSize = 0.008f,
            };
            root.AddChild(label);
            _parts[s.Id] = (whole, ruin, label, off + Vector3.Up * h * 0.5f, r);
        }
        _rings = new Node3D { Name = "Rings" };
        AddChild(_rings);
        _ruins = new Node3D { Name = "Ruins", Visible = false };
        AddChild(_ruins);
        _perdeMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(Palette.HoldingRed.Lightened(0.3f), 0.10f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true, Emission = Palette.HoldingRed, EmissionEnergyMultiplier = 0.4f,
        };
        _perde = new MeshInstance3D
        {
            Name = "Perde", Mesh = new SphereMesh { Radius = def.PerdeRadius, Height = def.PerdeRadius * 2f, RadialSegments = 48, Rings = 24 },
            MaterialOverride = _perdeMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_perde);
        _sign = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 9f, 0), FontSize = 64, OutlineSize = 14,
            Modulate = Palette.HoldingRed.Lightened(0.45f), PixelSize = 0.012f,
        };
        AddChild(_sign);
    }

    /// <summary>Yapi govdesi: (dugum, yukseklik, yaricap).</summary>
    static (Node3D, float, float) BuildStructure(string id)
    {
        var k = new MeshKit();
        var red = Palette.HoldingRed;
        switch (id)
        {
            case "silo":
                k.Cylinder(2.2f, 2.4f, 6.5f, MeshKit.At(0, 3.25f, 0), Palette.Rock);
                k.Cylinder(2.45f, 2.45f, 0.7f, MeshKit.At(0, 4.6f, 0), red);
                k.Sphere(2.2f, MeshKit.At(new Vector3(0, 6.5f, 0), Vector3.Zero, new Vector3(1, 0.5f, 1)), Palette.DarkGrey);
                return (k.Instance(), 7.6f, 2.6f);
            case "atolye":
                k.Pillow(new Vector3(6f, 3.4f, 5f), MeshKit.At(0, 1.7f, 0), Palette.Wood, 0.08f);
                k.Pillow(new Vector3(6.6f, 0.5f, 5.6f), MeshKit.At(0, 3.6f, 0), red, 0.1f);
                k.Cylinder(0.3f, 0.35f, 2.2f, MeshKit.At(2f, 4.6f, -1.2f), Palette.DarkGrey);
                return (k.Instance(), 4.8f, 3.4f);
            case "ahir":
                k.Pillow(new Vector3(7f, 3f, 5f), MeshKit.At(0, 1.5f, 0), Palette.Wood.Darkened(0.15f), 0.08f);
                k.Pillow(new Vector3(7.4f, 0.6f, 3.2f), MeshKit.At(new Vector3(0, 3.3f, 1.2f), new Vector3(0.45f, 0, 0)), red.Darkened(0.2f), 0.1f);
                k.Pillow(new Vector3(7.4f, 0.6f, 3.2f), MeshKit.At(new Vector3(0, 3.3f, -1.2f), new Vector3(-0.45f, 0, 0)), red.Darkened(0.2f), 0.1f);
                return (k.Instance(), 4.2f, 3.8f);
            default: // cekirdek
                k.Cylinder(1.4f, 1.8f, 1f, MeshKit.At(0, 0.5f, 0), Palette.DarkGrey);
                k.Cylinder(0.8f, 1.1f, 3.6f, MeshKit.At(0, 2.8f, 0), Palette.Rock.Darkened(0.2f));
                k.Sphere(0.9f, MeshKit.At(0, 5.2f, 0), red.Lightened(0.2f), 16, 8);
                var node = k.Instance();
                var glow = new OmniLight3D { Position = new Vector3(0, 5.2f, 0), LightColor = red, LightEnergy = 1.6f, OmniRange = 9f };
                node.AddChild(glow);
                return (node, 6f, 1.8f);
        }
    }

    /// <summary>Yapi vurus noktasi (dunya), yaricapi ve ayakta mi.</summary>
    public IEnumerable<(string id, Vector3 center, float radius, Node3D node)> Targets(HoldingCamp c)
    {
        foreach (var (id, p) in _parts)
            if (c.Structures.GetValueOrDefault(id) > 0) yield return (id, GlobalPosition + p.center, p.radius, p.whole);
    }

    public void Sync(HoldingCamp c, bool perdeUp)
    {
        GlobalPosition = _w.D.World.Ground(new Vector3(c.X, 0, c.Z));
        if (_builtLevel != c.Level || _builtDestroyed != c.Destroyed) Rebuild(c);
        foreach (var s in _w.Def.Camp.Structures)
        {
            var p = _parts[s.Id];
            float hp = c.Structures.GetValueOrDefault(s.Id);
            bool up = !c.Destroyed && hp > 0;
            p.whole.Visible = up;
            p.ruin.Visible = !up;
            float max = _w.H.StructureMaxHp(s, c.Level);
            bool locked = s.Core && up && System.Linq.Enumerable.Any(c.Structures, kv => kv.Key != s.Id && kv.Value > 0);
            p.label.Text = up ? $"{s.Name}  {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(max)}{(locked ? "  (kilitli)" : "")}" : $"{s.Name} (yıkık)";
            p.label.Modulate = up ? (hp < max ? Palette.Warning : Palette.OffWhite) : Palette.DarkGrey;
        }
        _perde.Visible = perdeUp;
        _sign.Text = c.Kind == "hq" ? (c.Cleared ? "Bereket Holding Genel Merkez (ele geçirildi)" : $"BEREKET HOLDING GENEL MERKEZ · Sv. {c.Level} · yalnız Kabarma gecesi") : c.Destroyed
            ? $"Bereket Holding (yıkık, gün {c.RebuildDay} yeniden kurulur)"
            : $"Bereket Holding · Kamp Sv. {c.Level} · Husumet {Mathf.RoundToInt(c.Husumet)}";
    }

    /// <summary>Seviyeye gore kule ve duvar halkalari; yikik kampta enkaz.</summary>
    void Rebuild(HoldingCamp c)
    {
        _builtLevel = c.Level;
        _builtDestroyed = c.Destroyed;
        foreach (var n in _rings.GetChildren()) n.QueueFree();
        _ruins.Visible = c.Destroyed;
        _rings.Visible = !c.Destroyed;
        if (c.Destroyed) return;
        var (_, _, towers, walls) = _w.H.Composition(c.Level);
        var k = new MeshKit();
        for (int i = 0; i < towers; i++)
        {
            float a = Mathf.Tau * i / Mathf.Max(1, towers) + 0.4f;
            var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 19f;
            p.Y = _w.D.World.HeightAt(GlobalPosition.X + p.X, GlobalPosition.Z + p.Z) - GlobalPosition.Y;
            k.Cylinder(0.7f, 0.9f, 5f, MeshKit.At(p + Vector3.Up * 2.5f), Palette.Rock);
            k.Pillow(new Vector3(2f, 1f, 2f), MeshKit.At(p + Vector3.Up * 5.3f), Palette.HoldingRed, 0.15f);
        }
        for (int r = 0; r < walls; r++)
        {
            float rad = 22f + r * 2.2f;
            const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                if (i % 5 == 0) continue; // kapi bosluklari
                float a = Mathf.Tau * i / seg;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rad;
                p.Y = _w.D.World.HeightAt(GlobalPosition.X + p.X, GlobalPosition.Z + p.Z) - GlobalPosition.Y;
                float len = Mathf.Tau * rad / seg * 0.95f;
                k.Pillow(new Vector3(len, 1.6f, 0.5f), MeshKit.At(p + Vector3.Up * 0.8f, new Vector3(0, -a + Mathf.Pi * 0.5f, 0)), Palette.Wood.Darkened(0.1f), 0.1f);
            }
        }
        _rings.AddChild(k.Instance());
        GD.Print($"HOLDING camp#{c.Id} level={c.Level} towers={towers} walls={walls}");
    }
}
