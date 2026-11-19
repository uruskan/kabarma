using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ata'nin yardimcisi (yer tutucu, atalar.json "adds"): Koca Pofuduk'un yumaklarindan cikan kucuk Pofuduklar
/// (olcekli pofuduk modeli) ve Dede'nin dallarindan dusen koruyucular (Findikci ve Hu yerine koddan "bekci").
/// Sicrayarak oyuncuya gelir, degerse hasar verir; silahla olur.
/// </summary>
public partial class AtaMinion : Node3D, IAtaTarget
{
    readonly Director _d;
    readonly AtaSystem _sys;
    readonly AtaAddDef _def;
    Node3D _body;
    CreatureRig _rig;
    float _cd, _hopT, _popT = -1, _t;
    Vector3 _hopFrom, _hopTo, _popFrom, _knock;
    bool _dead;

    public AtaMinion(Director d, AtaSystem sys, AtaAddDef def)
    {
        _d = d; _sys = sys; _def = def;
        Body = CombatBody.Of(def.Hp, CombatTeam.Wild, IsReal(def, d) ? def.Look : "", mass: def.Scale);
    }

    /// <summary>look "kod" degilse ve yaratik tablosunda varsa gercek yaratik modeli (M4: suluk_surusu, tosun, hamurcuk ...).</summary>
    static bool IsReal(AtaAddDef def, Director d) => def.Look != "kod" && !string.IsNullOrEmpty(def.Look) && d.Data.Creatures.Creatures.Any(c => c.Id == def.Look);

    public Vector3 Center => GlobalPosition + Vector3.Up * 0.45f * _def.Scale;
    public float Radius => 0.5f * _def.Scale;
    public bool Alive => !_dead;
    public bool IsWeakPoint => false;

    public override void _Ready()
    {
        _body = new Node3D();
        AddChild(_body);
        var aimArea = new Area3D { Name = "Nisan", CollisionLayer = 1, CollisionMask = 0, Monitoring = false };
        aimArea.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Radius }, Position = Vector3.Up * 0.45f * _def.Scale });
        AddChild(aimArea);
        if (IsReal(_def, _d))
        {
            _rig = CreatureRig.Load(_d.Data.Creature(_def.Look), _d.Poses, _body);
            _body.Scale = Vector3.One * _def.Scale;
            _rig?.Play("walk", 0f);
        }
        if (_rig == null)
        {
            // Koddan bekci: findik govde, yaprak kulak, isiyan gozler (Findikci / Hu yer tutucusu).
            var kit = new MeshKit();
            var shell = string.IsNullOrEmpty(_def.Color) ? Palette.Wood.Lerp(Palette.Shadow, 0.25f) : new Color(_def.Color);
            kit.Sphere(0.38f, MeshKit.At(new Vector3(0, 0.4f, 0), Vector3.Zero, new Vector3(1f, 1.15f, 1f)), shell, 12, 7);
            kit.Cylinder(0.08f, 0.3f, 0.22f, MeshKit.At(0, 0.86f, 0), Palette.Wood.Darkened(0.4f), 8);
            kit.Pillow(new Vector3(0.28f, 0.06f, 0.14f), MeshKit.At(new Vector3(-0.3f, 0.7f, 0), new Vector3(0, 0, 0.6f)), Palette.Plant, 0.5f, 8, 3);
            kit.Pillow(new Vector3(0.28f, 0.06f, 0.14f), MeshKit.At(new Vector3(0.3f, 0.7f, 0), new Vector3(0, 0, -0.6f)), Palette.Plant, 0.5f, 8, 3);
            kit.Sphere(0.07f, MeshKit.At(-0.12f, 0.5f, 0.33f), Palette.MayaTeal, 6, 4);
            kit.Sphere(0.07f, MeshKit.At(0.12f, 0.5f, 0.33f), Palette.MayaTeal, 6, 4);
            _body.AddChild(kit.Instance());
            _body.Scale = Vector3.One * _def.Scale;
        }
    }

    /// <summary>Dogus: Ata'dan yay cizerek yere iner.</summary>
    public void Pop(Vector3 from) { _popFrom = from; _popT = 0; }

    /// <summary>Savas govdesi: profil yardimcinin gorunus turu (pofuduk) ya da Ata'nin kendisi.</summary>
    public CombatBody Body { get; private set; }

    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (_dead) return default;
        var r = Combat.Apply(_d.Data.Combat, Body, hit);
        var dir = (GlobalPosition - from) with { Y = 0 };
        _knock += dir.Normalized() * r.Knock;
        if (r.Damage > 0) Fx.DamageNumber(Center + Vector3.Up * Radius, Mathf.Round(r.Damage), hit.Crit);
        Squash.Kick(_body, 0.2f, 0.25f, 2f);
        if (Body.Dead) Poof();
        return r;
    }

    /// <summary>Durum zamani (AtaSystem her kare): yanik ve zehir.</summary>
    public void TickStatus(float dt)
    {
        if (_dead) return;
        _d.Savas?.TickBody(this, dt);
        if (Body.Dead) Poof();
    }

    public void Poof()
    {
        if (_dead) return;
        _dead = true;
        _sys.Remove(this);
        Fx.Burst(Center, _def.Look == "pofuduk" || _def.Look == "hamurcuk" ? Palette.OffWhite : Palette.Wood, 16, 4f, 0.14f, 0.6f);
        Sfx.Play("zipzip_aci", Center, 0f, 0.8f);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (_dead) return;
        float dt = (float)delta;
        _t += dt;
        _cd -= dt;
        if (_popT >= 0)
        {
            _popT += dt;
            float k = Mathf.Clamp(_popT / 0.7f, 0, 1);
            var g = _d.World.Ground(GlobalPosition);
            var p0 = _popFrom.Lerp(g, k);
            GlobalPosition = p0 + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 1.5f;
            if (k >= 1) { _popT = -1; GlobalPosition = g; Squash.Kick(_body, 0.3f, 0.3f, 2f); }
            return;
        }
        dt *= Status.SpeedMult(_d.Data.Combat, Body); // savas: donma yavaslatir, kilit ve sok durdurur
        var pl = _d.Player;
        var d = (pl.GlobalPosition - GlobalPosition) with { Y = 0 };
        if (_hopT <= 0 && d.Length() > Radius + 0.5f)
        {
            _hopT = 0.45f;
            _hopFrom = GlobalPosition;
            _hopTo = _d.World.Ground(GlobalPosition + d.Normalized() * Mathf.Min(d.Length(), _def.Speed * 0.45f));
            _body.Rotation = new Vector3(0, Mathf.Atan2(d.X, d.Z), 0);
        }
        if (_hopT > 0)
        {
            _hopT -= dt;
            float k = 1f - Mathf.Clamp(_hopT / 0.45f, 0, 1);
            GlobalPosition = _hopFrom.Lerp(_hopTo, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 0.35f;
        }
        if (_knock.LengthSquared() > 0.01f)
        {
            GlobalPosition = _d.World.Ground(GlobalPosition + _knock * dt);
            _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 6f));
        }
        if (!pl.Down && _cd <= 0 && d.Length() < Radius + 0.7f)
        {
            _cd = 1.2f;
            pl.Hurt(_def.Damage, GlobalPosition);
            Squash.Kick(_body, 0.25f, 0.3f, 2f);
        }
    }
}
