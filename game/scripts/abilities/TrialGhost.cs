using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Hayalet dusman (GDD 17.1 Kadim Tas denemeleri, GDD 14.7 Tatbikat): yari saydam, ziplayarak gelen Maya golgesi.
/// Hasari gercektir; ganimet, Oz, Maya, oldurme sayaci vermez (Events.CreatureKilled yaymaz). Hedef: yakindaysa
/// Yem Hologrami, sonra korunan Gidak (koru denemesi), sonra oyuncu. Gorunus koddan (YER TUTUCU).
/// GECICI: savas akisinin Combat* hasar arayuzu main'e girince Hit onunla degisir.
/// </summary>
public partial class TrialGhost : Node3D
{
    readonly AbilitySystem _sys;
    readonly GhostDef _g;
    public float Hp, MaxHp;
    public bool Dead;
    readonly float _damage;
    float _attackCd, _hopT, _spawnT;
    Vector3 _hopFrom, _hopTo, _knock;
    Node3D _body;
    StandardMaterial3D _mat;
    float _flash;
    /// <summary>Korunan hedef (koru denemesi); null: oyuncu.</summary>
    public Node3D Guard;
    /// <summary>Korunan hedefe verdigi hasar (deneme bunu okuyup sifirlar).</summary>
    public float GuardDamage;

    public const float Radius = 0.45f;
    public Vector3 Center => GlobalPosition + Vector3.Up * 0.45f;

    public TrialGhost(AbilitySystem sys, GhostDef g, float hpMult = 1f, float dmgMult = 1f)
    {
        _sys = sys; _g = g;
        MaxHp = Hp = g.Hp * hpMult;
        _damage = g.Damage * dmgMult;
        _attackCd = 1f;
    }

    public override void _Ready()
    {
        _mat = AbilitySystem.GhostMaterial(Palette.MayaViolet, 0.55f);
        var kit = new MeshKit();
        kit.Pillow(new Vector3(0.8f, 0.7f, 0.8f), MeshKit.At(0, 0.42f, 0), Palette.MayaViolet, 0.6f);
        kit.Sphere(0.09f, MeshKit.At(-0.16f, 0.6f, 0.36f), Palette.OffWhite, 8, 4);
        kit.Sphere(0.09f, MeshKit.At(0.16f, 0.6f, 0.36f), Palette.OffWhite, 8, 4);
        _body = kit.Instance(_mat);
        _body.Scale = Vector3.One * 0.05f;
        AddChild(_body);
        _sys.Ghosts.Add(this);
    }

    public override void _ExitTree() => _sys.Ghosts.Remove(this);

    /// <summary>Hasar ve itme (Capa silahlari, Sok Dalgasi, ultiler).</summary>
    public void Hit(float dmg, Vector3 knock)
    {
        if (Dead) return;
        Hp -= dmg;
        _flash = 1f;
        _knock += knock with { Y = 0 };
        Fx.DamageNumber(Center + Vector3.Up * 0.5f, dmg, false);
        if (Hp <= 0) Die();
    }

    public void Die()
    {
        if (Dead) return;
        Dead = true;
        Fx.Burst(Center, Palette.MayaViolet, 18, 5f, 0.12f, 0.6f, true, -1f);
        Sfx.Play("pop", Center, -4f);
        QueueFree();
    }

    /// <summary>Iz birakmadan soner (deneme bitince, Tatbikat sonunda).</summary>
    public void Fade()
    {
        if (Dead) return;
        Dead = true;
        Fx.Burst(Center, Palette.MayaTeal, 8, 2.5f, 0.1f, 0.5f, true, -1f);
        QueueFree();
    }

    Node3D PickTarget(out bool isPlayer, out bool isGuard)
    {
        var p = _sys.D.Player;
        var pos = GlobalPosition;
        isPlayer = false; isGuard = false;
        if (_sys.DecoyNear(pos) is Node3D decoy) return decoy;
        bool playerClose = !p.Down && p.GlobalPosition.DistanceTo(pos) < 3f;
        if (Guard != null && IsInstanceValid(Guard) && !playerClose) { isGuard = true; return Guard; }
        isPlayer = !p.Down;
        return p.Down ? null : p;
    }

    public override void _Process(double delta)
    {
        if (Dead) return;
        float dt = (float)delta;
        _spawnT += dt;
        _attackCd -= dt;
        var target = PickTarget(out bool isPlayer, out bool isGuard);
        var pos = GlobalPosition;
        var goal = target?.GlobalPosition ?? pos;
        var flat = (goal - pos) with { Y = 0 };
        float dist = flat.Length();
        if (target != null && dist < _g.Reach + Radius && _attackCd <= 0)
        {
            _attackCd = _g.AttackSeconds;
            _flash = 0.5f;
            if (isPlayer) _sys.D.Player.Hurt(_damage, pos);
            else if (isGuard) { GuardDamage += _damage; Fx.Burst(goal + Vector3.Up * 0.6f, Palette.Warning, 6, 3f, 0.08f, 0.4f); }
            else _sys.HitDecoy(_damage);
        }
        if (_hopT <= 0 && dist > _g.Reach * 0.8f)
        {
            _hopT = 0.42f;
            _hopFrom = pos;
            var dir = flat.Normalized();
            _hopTo = _sys.D.World.Ground(pos + dir * Mathf.Min(dist - _g.Reach * 0.6f, _g.Speed * 0.42f));
            _body.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        }
        float hopH = 0;
        if (_hopT > 0)
        {
            _hopT -= dt;
            float k = 1f - Mathf.Clamp(_hopT / 0.42f, 0, 1);
            GlobalPosition = _hopFrom.Lerp(_hopTo, k);
            hopH = Mathf.Sin(k * Mathf.Pi) * 0.45f;
        }
        if (_knock.LengthSquared() > 0.01f)
        {
            GlobalPosition = _sys.D.World.Ground(GlobalPosition + _knock * dt);
            _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 5f));
        }
        float grow = Mathf.Min(1f, _spawnT / 0.5f);
        _body.Position = new Vector3(0, hopH, 0);
        _body.Scale = Vector3.One * Mathf.Max(0.05f, grow);
        _flash = Mathf.Max(0, _flash - dt * 5f);
        _mat.EmissionEnergyMultiplier = 0.8f + _flash * 2.5f;
    }
}
