using Godot;

namespace Kabarma.Game;

/// <summary>
/// Ata mermisi: Koca Pofuduk'un yun yumagi, Dede'nin fidan/findik atisi. Yay cizer, yere duserse seker ve soner;
/// oyuncuya degerse hasar ve itme ("sektirir", ICERIK §4.1).
/// </summary>
public partial class AtaProjectile : Node3D
{
    readonly Director _d;
    readonly float _damage, _knock, _size;
    readonly Color _color;
    Vector3 _v;
    float _life;
    int _bounces = 1;

    public AtaProjectile(Director d, float damage, float knock, Color color, float size)
    {
        _d = d; _damage = damage; _knock = knock; _color = color; _size = size;
    }

    public void Launch(Vector3 from, Vector3 dir, float speed, float dist)
    {
        GlobalPosition = from;
        // Hedef uzakligina gore yay: ucus suresi t = dist / speed, dikey hiz t boyunca yercekimini karsilar.
        float t = Mathf.Clamp(dist / Mathf.Max(1f, speed), 0.4f, 2.5f);
        _v = dir * speed + Vector3.Up * (9.8f * t * 0.5f - 0.5f);
        var kit = new MeshKit();
        kit.Sphere(_size, MeshKit.At(0, 0, 0), _color, 10, 6);
        kit.Sphere(_size * 0.55f, MeshKit.At(_size * 0.5f, _size * 0.3f, 0), _color.Lightened(0.15f), 8, 5);
        kit.Sphere(_size * 0.5f, MeshKit.At(-_size * 0.45f, -_size * 0.2f, _size * 0.3f), _color.Darkened(0.1f), 8, 5);
        AddChild(kit.Instance());
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _life += dt;
        _v += Vector3.Down * 9.8f * dt;
        GlobalPosition += _v * dt;
        Rotation += new Vector3(dt * 5f, dt * 3f, 0);
        var p = _d.Player;
        if (!p.Down && (p.GlobalPosition + Vector3.Up).DistanceTo(GlobalPosition) < _size + 0.6f)
        {
            p.Hurt(_damage, GlobalPosition - _v.Normalized());
            p.Velocity += (_v with { Y = 0 }).Normalized() * _knock + Vector3.Up * 2.5f;
            Fx.Burst(GlobalPosition, _color, 14, 4f, 0.12f, 0.5f);
            Sfx.Play("kabarik_gumleme", GlobalPosition, 0f, 1.6f);
            QueueFree();
            return;
        }
        float gy = _d.World.HeightAt(GlobalPosition.X, GlobalPosition.Z) + _size;
        if (GlobalPosition.Y < gy && _v.Y < 0)
        {
            GlobalPosition = GlobalPosition with { Y = gy };
            if (_bounces-- > 0) { _v = new Vector3(_v.X * 0.6f, -_v.Y * 0.45f, _v.Z * 0.6f); Squash.Kick(this, 0.3f, 0.3f, 2f); }
            else { Fx.Burst(GlobalPosition, _color, 8, 2.5f, 0.1f, 0.4f); QueueFree(); }
        }
        if (_life > 5f) QueueFree();
    }
}

/// <summary>
/// Kok dalgasi (Boynuzlu Dede asama 2): Dede'den yayilan halka. Halka oyuncunun ustunden gecerken oyuncu yerdeyse
/// hasar alir; zipliyorsa degmez (GDD §18.1).
/// </summary>
public partial class RootWave : Node3D
{
    readonly Director _d;
    readonly float _speed, _maxR, _width, _damage, _knock;
    float _r = 1f;
    bool _hit;
    MeshInstance3D _ring;
    StandardMaterial3D _mat;

    public RootWave(Director d, float speed, float maxR, float width, float damage, float knock)
    {
        _d = d; _speed = speed; _maxR = maxR; _width = width; _damage = damage; _knock = knock;
    }

    /// <summary>Oyuncu konumunda halkaya kalan mesafe (m); halka gectiyse negatif. Bot zipla zamanini buradan okur.</summary>
    public float GapToPlayer => new Vector2(_d.Player.GlobalPosition.X - GlobalPosition.X, _d.Player.GlobalPosition.Z - GlobalPosition.Z).Length() - _r;
    public float Speed => _speed;
    public bool Done => _hit || _r > _maxR;

    public override void _Ready()
    {
        _mat = new StandardMaterial3D { AlbedoColor = Palette.Wood.Darkened(0.35f), EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 0.6f };
        _ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 1f - _width * 0.5f, OuterRadius = 1f + _width * 0.5f, Rings = 48, RingSegments = 6 }, MaterialOverride = _mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_ring);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _r += _speed * dt;
        // Halka zemine oturur (yaklasik: merkezin yuksekligi), kalinligi sabit kalsin diye torus yeniden olceklenmez; yaricap olcegi.
        var tm = (TorusMesh)_ring.Mesh;
        tm.InnerRadius = Mathf.Max(0.1f, _r - _width * 0.5f);
        tm.OuterRadius = _r + _width * 0.5f;
        _ring.Scale = new Vector3(1f, 0.6f + 0.2f * Mathf.Sin(_r * 3f), 1f);
        _mat.AlbedoColor = new Color(_mat.AlbedoColor, Mathf.Clamp(1.2f - _r / _maxR, 0.2f, 1f));
        if ((int)(_r * 1.5f) != (int)((_r - _speed * dt) * 1.5f))
        {
            float a = GD.Randf() * Mathf.Tau;
            var at = GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * _r;
            Fx.Burst(_d.World.Ground(at) + Vector3.Up * 0.2f, Palette.Soil, 6, 3f, 0.14f, 0.5f);
        }
        var p = _d.Player;
        if (!_hit && !p.Down && Mathf.Abs(GapToPlayer) < _width * 0.5f + 0.3f)
        {
            float above = p.GlobalPosition.Y - _d.World.HeightAt(p.GlobalPosition.X, p.GlobalPosition.Z);
            if (p.IsOnFloor() && above < 0.5f)
            {
                _hit = true;
                p.Hurt(_damage, GlobalPosition);
                p.Velocity += ((p.GlobalPosition - GlobalPosition) with { Y = 0 }).Normalized() * _knock + Vector3.Up * 4f;
                Sfx.Play("kaya_kemirme", p.GlobalPosition, 2f, 0.6f);
            }
        }
        if (_r > _maxR) QueueFree();
    }
}
