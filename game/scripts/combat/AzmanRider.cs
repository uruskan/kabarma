using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Azman Homur'un sirtindan dusen kucuk Homur (azman.json riders). Tabandaki yabani turun govdesi kucuk olcekte;
/// sicrayarak en yakin oyuncuya gelir, degerse hasar verir. Saglik ortak savas govdesinde (profil: tur kimligi).
/// Yalniz host'ta doger (istemcide kukla yok: bilinen ortak oyun boslugu, rapor).
/// </summary>
public partial class AzmanRider : Node3D, IAtaTarget
{
    readonly Director _d;
    readonly AzmanRidersDef _def;
    Node3D _body;
    CreatureRig _rig;
    float _cd, _hopT, _popT = -1;
    Vector3 _hopFrom, _hopTo, _popFrom, _knock;
    bool _dead;

    public AzmanRider(Director d, AzmanRidersDef def)
    {
        _d = d; _def = def;
        Body = CombatBody.Of(def.Hp, CombatTeam.Wild, def.Creature, mass: 2f);
    }

    public CombatBody Body { get; }
    public Vector3 Center => GlobalPosition + Vector3.Up * 0.5f * _def.Scale;
    public float Radius => 0.5f * _def.Scale;
    public bool Alive => !_dead;
    public bool IsWeakPoint => false;

    public override void _Ready()
    {
        _body = new Node3D();
        AddChild(_body);
        var aim = new Area3D { Name = "Nisan", CollisionLayer = 1, CollisionMask = 0, Monitoring = false };
        aim.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = Radius }, Position = Vector3.Up * 0.5f * _def.Scale });
        AddChild(aim);
        var cdef = _d.Data.Creature(_def.Creature);
        _rig = cdef != null ? CreatureRig.Load(cdef, _d.Poses, _body) : null;
        if (_rig == null)
        {
            var kit = new MeshKit();
            kit.Pillow(new Vector3(0.6f, 0.5f, 0.8f), MeshKit.At(0, 0.35f, 0), Palette.Wood.Lerp(Palette.Soil, 0.4f), 0.5f);
            _body.AddChild(kit.Instance());
        }
        _body.Scale = Vector3.One * _def.Scale;
        _rig?.Play("run", 0f);
        AddToGroup("azman_rider");
    }

    public void Pop(Vector3 from) { _popFrom = from; _popT = 0; }

    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (_dead) return default;
        var r = Combat.Apply(_d.Data.Combat, Body, hit);
        _knock += ((GlobalPosition - from) with { Y = 0 }).Normalized() * r.Knock;
        if (r.Damage > 0) Fx.DamageNumber(Center + Vector3.Up * Radius, Mathf.Round(r.Damage), hit.Crit);
        Squash.Kick(_body, 0.2f, 0.25f, 2f);
        if (Body.Dead) Poof();
        return r;
    }

    void Poof()
    {
        if (_dead) return;
        _dead = true;
        _d.Savas.Remove(this);
        Fx.Burst(Center, Palette.Wood, 16, 4f, 0.14f, 0.6f);
        Sfx.Play("zipzip_aci", Center, 0f, 0.6f);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (_dead) return;
        float dt = (float)delta;
        _d.Savas.TickBody(this, dt);
        if (Body.Dead) { Poof(); return; }
        _cd -= dt;
        if (_popT >= 0)
        {
            _popT += dt;
            float k = Mathf.Clamp(_popT / 0.7f, 0, 1);
            var g = _d.World.Ground(GlobalPosition);
            GlobalPosition = _popFrom.Lerp(g, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 1.5f;
            if (k >= 1) { _popT = -1; GlobalPosition = g; }
            return;
        }
        dt *= Status.SpeedMult(_d.Data.Combat, Body);
        var pl = _d.Player;
        var d = (pl.GlobalPosition - GlobalPosition) with { Y = 0 };
        if (_hopT <= 0 && d.Length() > Radius + 0.5f)
        {
            _hopT = 0.4f;
            _hopFrom = GlobalPosition;
            _hopTo = _d.World.Ground(GlobalPosition + d.Normalized() * Mathf.Min(d.Length(), _def.Speed * 0.4f));
            _body.Rotation = new Vector3(0, Mathf.Atan2(d.X, d.Z), 0);
        }
        if (_hopT > 0)
        {
            _hopT -= dt;
            float k = 1f - Mathf.Clamp(_hopT / 0.4f, 0, 1);
            GlobalPosition = _hopFrom.Lerp(_hopTo, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 0.3f;
        }
        if (_knock.LengthSquared() > 0.01f)
        {
            GlobalPosition = _d.World.Ground(GlobalPosition + _knock * dt);
            _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 6f));
        }
        if (!pl.Down && _cd <= 0 && d.Length() < Radius + 0.7f && Status.CanAct(Body))
        {
            _cd = 1.2f;
            pl.Hurt(_def.Damage, GlobalPosition);
            Squash.Kick(_body, 0.25f, 0.3f, 2f);
        }
    }
}
