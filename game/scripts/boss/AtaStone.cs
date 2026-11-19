using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ata Tasi ve sunagi (GDD §18, ICERIK §17): arenanin kenarinda dikili tas, onunde adak sunagi, ustunde Ata'nin adi ve
/// adak durumu. Koddan model (yer tutucu; uretim istegi raporda). Yenilen Ata'nin tasi soner.
/// </summary>
public partial class AtaStone : Node3D
{
    readonly Director _d;
    readonly AtaDef _def;
    Label3D _label;
    MeshInstance3D _rune;
    StandardMaterial3D _runeMat;
    float _pulse, _t;
    public Vector3 Altar { get; private set; }

    readonly Vector3 _center;

    public AtaStone(Director d, AtaDef def, Vector3 center) { _d = d; _def = def; _center = center; }

    public override void _Ready()
    {
        var center = _center;
        var pos = _d.World.Ground(center + new Vector3(_def.Arena.Stone[0], 0, _def.Arena.Stone[1]));
        GlobalPosition = pos;
        // Tas arenaya bakar; sunak tasin onunde (arena tarafinda).
        var toArena = (center - pos) with { Y = 0 };
        Rotation = new Vector3(0, Mathf.Atan2(toArena.X, toArena.Z), 0);

        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.9f, 3.4f, 0.55f), MeshKit.At(new Vector3(0, 1.5f, -0.6f), new Vector3(0.04f, 0, -0.03f)), Palette.Rock, 0.3f, 10, 8);
        kit.Pillow(new Vector3(1.3f, 0.5f, 0.9f), MeshKit.At(0, 0.1f, -0.6f), Palette.RockDark, 0.4f, 10, 4);
        // Sunak: alcak tas tekne (Dev Yemlik / Orman Cingiragi burada toplanir)
        kit.Pillow(new Vector3(1.6f, 0.55f, 1.0f), MeshKit.At(0, 0.28f, 1.2f), Palette.Rock.Lerp(Palette.Wood, 0.3f), 0.35f, 12, 5);
        kit.Pillow(new Vector3(1.3f, 0.12f, 0.75f), MeshKit.At(0, 0.58f, 1.2f), Palette.Soil, 0.3f, 10, 3);
        var mi = kit.Instance();
        AddChild(mi);
        var body = new StaticBody3D();
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.0f, 3.2f, 0.7f) }, Position = new Vector3(0, 1.6f, -0.6f) });
        AddChild(body);

        // Isiyan oyma (Maya rengi): adak doldukca parlar.
        _runeMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 1.5f };
        _rune = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.22f, OuterRadius = 0.3f, Rings = 18, RingSegments = 6 }, MaterialOverride = _runeMat, Position = new Vector3(0, 2.2f, -0.3f), RotationDegrees = new Vector3(90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_rune);

        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 64, OutlineSize = 12, PixelSize = 0.006f,
            Modulate = Palette.OffWhite, OutlineModulate = new Color(0, 0, 0, 0.8f), Position = new Vector3(0, 3.8f, -0.4f),
            Font = UiTheme.Condensed, NoDepthTest = false,
        };
        AddChild(_label);
        Altar = ToGlobal(new Vector3(0, 0.6f, 1.2f));
        Refresh();
    }

    public void Refresh()
    {
        if (_label == null) return;
        var p = _d.State.Atalar;
        bool beaten = p.IsDefeated(_def.Id);
        float have = _def.Offering.Sum(o => o.Count - AtaRules.Missing(p, _def, o));
        float need = _def.Offering.Sum(o => o.Count);
        _label.Text = beaten ? Loc.T("ata.stone_beaten", UiTheme.Upper(_def.Name)) : Loc.T("ata.stone_label", UiTheme.Upper(_def.Name), _def.OfferingName, have.ToString("0", System.Globalization.CultureInfo.InvariantCulture), need.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        _runeMat.EmissionEnergyMultiplier = beaten ? 0.2f : 1f + 3f * (need > 0 ? have / need : 0);
    }

    /// <summary>Ata gelirken tas parlar.</summary>
    public void Pulse() { _pulse = 3f; Fx.Burst(GlobalPosition + Vector3.Up * 2.2f, Palette.MayaTeal, 30, 6f, 0.16f, 1.2f, true, -2f); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        if (_pulse > 0) _pulse -= dt;
        if (_rune != null) _rune.Rotation = new Vector3(Mathf.Pi / 2, _t * (_pulse > 0 ? 6f : 0.6f), 0);
    }
}
