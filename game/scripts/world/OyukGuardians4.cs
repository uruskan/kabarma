using System;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

// M4 (ata4): Camur Devi, Kuvars Akrebi, Buz Aynasi gorunumleri. YER TUTUCU, koddan (oyuk.json "placeholder" Tripo isteminin ozeti).

/// <summary>Silahla vurulabilen yardimci hedef (sarkit, buz kopya): Node degil duz nesne (OyukTarget gibi AtaSystem temizliginden etkilenmez).</summary>
public sealed class OyukPoint : IAtaTarget
{
    readonly Func<Vector3> _center;
    readonly Func<bool> _alive;
    readonly Action<CombatHit> _onHit;
    public float Radius { get; }

    public OyukPoint(Func<Vector3> center, float radius, Func<bool> alive, Action<CombatHit> onHit) { _center = center; Radius = radius; _alive = alive; _onHit = onHit; }

    public Vector3 Center => _center();
    public bool Alive => _alive();
    public bool IsWeakPoint => false;
    public CombatBody Body => null;
    public CombatResult TakeHit(CombatHit hit, Vector3 from) { _onHit(hit); return new CombatResult { Damage = hit.Amount }; }
}

/// <summary>Camur Devi (YER TUTUCU): ıslak parlak camur govde, yosun, kuruyunca catlak ve acik renk.</summary>
public partial class MudVisual : Node3D
{
    MeshInstance3D _body;
    StandardMaterial3D _mat;
    float _t, _flash;

    public override void _Ready()
    {
        _mat = new StandardMaterial3D { AlbedoColor = new Color("4A3A26"), Roughness = 0.15f, Metallic = 0.1f };
        _body = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.7f, Height = 3.8f, RadialSegments = 14, Rings = 8 }, MaterialOverride = _mat, Position = new Vector3(0, 1.9f, 0) };
        AddChild(_body);
        AddChild(new MeshKit()
            .Sphere(0.7f, MeshKit.At(-1.7f, 1.7f, 0.3f), new Color("4A3A26"), 10, 6).Sphere(0.7f, MeshKit.At(1.7f, 1.7f, 0.3f), new Color("4A3A26"), 10, 6)
            .Sphere(0.6f, MeshKit.At(0, 3.7f, 0.2f), new Color("4A3A26"), 10, 6)
            .Sphere(0.35f, MeshKit.At(0.9f, 2.9f, -0.3f), Palette.Plant, 8, 5).Sphere(0.3f, MeshKit.At(-0.8f, 1.1f, -0.9f), Palette.Plant, 8, 5)
            .Sphere(0.1f, MeshKit.At(-0.25f, 3.8f, 0.75f), Palette.Warning, 6, 4).Sphere(0.1f, MeshKit.At(0.25f, 3.8f, 0.75f), Palette.Warning, 6, 4)
            .Instance());
    }

    public void Flash() => _flash = 0.15f;

    public void Show(MudFight f, float dt)
    {
        _t += dt;
        Position = new Vector3(Position.X, Position.Y, Position.Z);
        bool dry = f.Dry && !f.Dead;
        var wet = new Color("4A3A26"); var dried = new Color("B59A72");
        _mat.AlbedoColor = _mat.AlbedoColor.Lerp(dry ? dried : wet.Lerp(dried, Mathf.Clamp(f.Dryness / 3f, 0f, 1f) * 0.6f), Mathf.Min(1f, dt * 4f));
        _mat.Roughness = dry ? 0.95f : 0.15f;
        _body.Rotation = new Vector3(0, 0, dry ? Mathf.Sin(_t * 6f) * 0.04f : 0f);
        if (_flash > 0) { _flash -= dt; _body.Scale = Vector3.One * (1f + _flash * 0.8f); } else _body.Scale = Vector3.One;
        if (f.Dead) Visible = false;
    }
}

/// <summary>Kuvars Akrebi (YER TUTUCU): kuvars zirh kabugu, kiskaclar, kuyruk. Catlakta zirh parcalari acilir ve parlar.</summary>
public partial class ScorpionVisual : Node3D
{
    MeshInstance3D _shell;
    StandardMaterial3D _mat;
    Node3D _tail;
    float _t, _flash;

    public override void _Ready()
    {
        var q = new Color("B9A38A");
        _mat = new StandardMaterial3D { AlbedoColor = q, Roughness = 0.25f, Metallic = 0.3f };
        _shell = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.4f, Height = 1.8f, RadialSegments = 12, Rings = 6 }, MaterialOverride = _mat, Position = new Vector3(0, 1.0f, 0), Scale = new Vector3(1f, 0.8f, 1.5f) };
        AddChild(_shell);
        var kit = new MeshKit();
        foreach (float sx in new[] { -1f, 1f })
        {
            kit.Cylinder(0.1f, 0.25f, 1.6f, MeshKit.At(new Vector3(sx * 1.1f, 0.9f, -1.9f), new Vector3(1.2f, 0, sx * -0.3f)), q.Darkened(0.15f), 8);
            kit.Sphere(0.35f, MeshKit.At(sx * 1.35f, 0.9f, -2.6f), q.Darkened(0.2f), 8, 5);
            for (int i = 0; i < 3; i++) kit.Cylinder(0.05f, 0.12f, 1.4f, MeshKit.At(new Vector3(sx * 1.5f, 0.5f, -0.6f + i * 0.9f), new Vector3(0, 0, sx * 0.9f)), q.Darkened(0.3f), 6);
        }
        AddChild(kit.Instance());
        _tail = new Node3D { Position = new Vector3(0, 1.4f, 1.6f) };
        _tail.AddChild(new MeshKit().Cylinder(0.25f, 0.4f, 1.8f, MeshKit.At(0, 0.9f, 0.6f), q, 8).Sphere(0.45f, MeshKit.At(0, 2.1f, 0.3f), q.Darkened(0.1f), 8, 5)
            .Cylinder(0f, 0.18f, 0.8f, MeshKit.At(new Vector3(0, 2.6f, -0.2f), new Vector3(1.0f, 0, 0)), new Color("6A3A5A"), 6).Instance());
        AddChild(_tail);
    }

    public void Flash() => _flash = 0.15f;

    public void Show(ScorpionFight f, float dt, Vector3 player)
    {
        _t += dt;
        _mat.EmissionEnabled = f.Cracked;
        _mat.Emission = Palette.Warning; _mat.EmissionEnergyMultiplier = f.Cracked ? 1.2f + Mathf.Sin(_t * 9f) * 0.6f : 0f;
        _mat.AlbedoColor = f.Cracked ? new Color("8A6A50") : new Color("B9A38A");
        _tail.Rotation = new Vector3(Mathf.Sin(_t * 2f) * 0.2f, 0, 0);
        var d = player - GlobalPosition; d.Y = 0;
        if (d.LengthSquared() > 0.01f) Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(-d.X, -d.Z) + Mathf.Pi, Mathf.Min(1f, dt * 3f)), 0);
        if (_flash > 0) { _flash -= dt; _shell.Scale = new Vector3(1f, 0.8f, 1.5f) * (1f + _flash); } else _shell.Scale = new Vector3(1f, 0.8f, 1.5f);
        if (f.Dead) Visible = false;
    }
}

/// <summary>Buz Aynasi (YER TUTUCU): duvarda buz cerceveli ayna ve oyuncu boyunda buz kopya silindirleri.</summary>
public partial class MirrorVisual : Node3D
{
    MeshInstance3D _glass;
    StandardMaterial3D _mat;
    readonly System.Collections.Generic.List<Node3D> _copies = new();
    float _t, _flash;

    public override void _Ready()
    {
        var ice = new Color("BFE3F2");
        _mat = new StandardMaterial3D { AlbedoColor = new Color(ice, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.05f, Metallic = 0.6f, EmissionEnabled = true, Emission = ice, EmissionEnergyMultiplier = 0.3f };
        _glass = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3.6f, 5f, 0.25f) }, MaterialOverride = _mat, Position = new Vector3(0, 3f, 0) };
        AddChild(_glass);
        AddChild(new MeshKit()
            .Pillow(new Vector3(4.2f, 0.4f, 0.5f), MeshKit.At(0, 5.6f, 0), ice.Darkened(0.2f), 0.4f).Pillow(new Vector3(4.2f, 0.4f, 0.5f), MeshKit.At(0, 0.4f, 0), ice.Darkened(0.2f), 0.4f)
            .Pillow(new Vector3(0.4f, 5.4f, 0.5f), MeshKit.At(-2f, 3f, 0), ice.Darkened(0.2f), 0.4f).Pillow(new Vector3(0.4f, 5.4f, 0.5f), MeshKit.At(2f, 3f, 0), ice.Darkened(0.2f), 0.4f)
            .Instance());
    }

    public void Flash() => _flash = 0.15f;

    public void Show(MirrorFight f, Vector3 roomCenter, float dt)
    {
        _t += dt;
        _mat.EmissionEnergyMultiplier = f.Vulnerable ? 1.4f + Mathf.Sin(_t * 8f) * 0.5f : 0.3f; // kopya yokken ayna parlar: vur
        if (_flash > 0) { _flash -= dt; _glass.Scale = Vector3.One * (1f + _flash * 0.4f); } else _glass.Scale = Vector3.One;
        while (_copies.Count < f.Copies.Count)
        {
            var c = new Node3D();
            c.AddChild(new MeshKit().Cylinder(0.45f, 0.5f, 1.5f, MeshKit.At(0, 0.9f, 0), new Color("BFE3F2"), 10).Sphere(0.4f, MeshKit.At(0, 2.0f, 0), new Color("D6EEF8"), 10, 6)
                .Cylinder(0.12f, 0.12f, 1.0f, MeshKit.At(new Vector3(0.55f, 1.3f, 0.35f), new Vector3(1.2f, 0, 0)), new Color("9CCFE6"), 6).Instance());
            GetParent().AddChild(c);
            _copies.Add(c);
        }
        while (_copies.Count > f.Copies.Count) { var c = _copies[^1]; _copies.RemoveAt(_copies.Count - 1); Fx.Burst(c.GlobalPosition + Vector3.Up, new Color("BFE3F2"), 24, 4f, 0.14f, 0.6f, true); c.QueueFree(); }
        for (int i = 0; i < f.Copies.Count; i++) _copies[i].GlobalPosition = roomCenter + new Vector3(f.Copies[i].X, 0, f.Copies[i].Z);
        if (f.Dead) { Visible = false; foreach (var c in _copies) c.Visible = false; }
    }

    public void Clear() { foreach (var c in _copies) if (IsInstanceValid(c)) c.QueueFree(); _copies.Clear(); }
    public Vector3 CopyPos(int i) => _copies[i].GlobalPosition + Vector3.Up * 1.1f;
}
