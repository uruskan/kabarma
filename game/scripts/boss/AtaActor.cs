using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ata govdesi (gorunus ve hareket). Kural ve sayilar Sim'de (AtaFight, AtaRules, atalar.json); bu dugum yurur, sezdirir,
/// hucum eder, yumak atar, oyuncuya degerse hasar verir. Alt siniflar modeli ve ozel saldirilari kurar.
/// </summary>
public abstract partial class AtaActor : Node3D, IAtaTarget
{
    protected Director D;
    protected AtaSystem Sys;
    public AtaDef Def { get; private set; }
    public Node3D Visual { get; protected set; }
    protected AtaFight Fight => Sys.Fight;
    protected AtaPhaseDef Phase => Fight == null ? Def.Phases[0] : AtaRules.PhaseOf(Fight, Def);

    enum Move { Arrive, Walk, Telegraph, Charge, Stunned, Dead }
    Move _move = Move.Walk;
    float _arriveT, _contactCd, _flash, _t, _chargeT, _telegraphT;
    Vector3 _chargeDir;
    bool _chargeHitPlayer;
    string _telegraphKind = "";
    MeshInstance3D _arrow;
    public float Burning { get; private set; }

    public void Init(Director d, AtaSystem sys, AtaDef def) { D = d; Sys = sys; Def = def; }

    public Vector3 Center => GlobalPosition + Vector3.Up * Def.Height * 0.5f;
    public float Radius => Def.Radius;
    public bool Alive => _move != Move.Dead && Fight != null && !Fight.Defeated;
    public bool IsWeakPoint => false;
    public bool Charging => _move == Move.Charge;
    public bool Telegraphing => _move == Move.Telegraph;
    public string TelegraphKind => _telegraphKind;
    public bool Stunned => Fight?.StunT > 0;

    /// <summary>Model: Visual altina kurulur.</summary>
    protected abstract void BuildModel();

    public override void _Ready()
    {
        Visual = new Node3D { Name = "Visual" };
        AddChild(Visual);
        BuildModel();
        // Nisan isini (Player.AimPoint alanlara da carpar) Ata'nin govdesinde dursun: Bicme menzili ve mermi yonu.
        var aimArea = new Area3D { Name = "Nisan", CollisionLayer = 1, CollisionMask = 0, Monitoring = false };
        aimArea.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = Def.Radius, Height = Mathf.Max(Def.Height, Def.Radius * 2.1f) }, Position = Vector3.Up * Def.Height * 0.5f, RotationDegrees = new Vector3(90, 0, 0) });
        AddChild(aimArea);
        var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(Palette.HoldingRed, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _arrow = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(Def.Radius * 1.6f, 1f) }, MaterialOverride = mat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _arrow.AddToGroup("ata_fx");
        Sys.AddChild(_arrow);
    }

    public override void _ExitTree() { if (IsInstanceValid(_arrow)) _arrow.QueueFree(); }

    public void Begin(bool arriving)
    {
        if (arriving) { _move = Move.Arrive; _arriveT = 0; Visual.Position = Vector3.Up * 25f; }
        else _move = Move.Walk;
        OnPhase(Fight?.Phase ?? 0, quiet: true);
    }

    // ---------- Hasar ----------

    public void TakeWeapon(float dmg, bool crit, Vector3 dir, float knock)
    {
        if (!Alive) return;
        float done = Sys.Damage(dmg, AtaHitKind.Weapon);
        Feedback(done, crit || Stunned);
    }

    public void Burn(float seconds) => Burning = Mathf.Max(Burning, seconds);

    /// <summary>Isabet geri bildirimi: hasar sayisi (zirhta gri "zirh" yazisi), esneme, beyaz parlama.</summary>
    protected void Feedback(float done, bool big)
    {
        _flash = 1f;
        Squash.Kick(Visual, big ? 0.12f : 0.06f, 0.25f, 2f);
        if (done > 0) Fx.DamageNumber(Center + Vector3.Up * Def.Height * 0.45f + new Vector3(GD.Randf() - 0.5f, 0, GD.Randf() - 0.5f), Mathf.Round(done), big);
        else if (Fight?.ShieldT > 0) Fx.Text3D(Center + Vector3.Up * Def.Height * 0.5f, "—", Palette.OffWhite, 48, 0.8f, 0.5f);
        if (Fight != null && Fight.Armor > 0) Fx.Burst(Center + Vector3.Up * 0.5f, Palette.OffWhite, 4, 3f, 0.12f, 0.5f);
        OnHurt();
    }

    // ---------- Sim komutlari ----------

    public virtual void Telegraph(string kind)
    {
        if (_move is Move.Dead or Move.Arrive) return;
        _telegraphKind = kind;
        _telegraphT = Phase.Telegraph;
        _move = Move.Telegraph;
        if (kind == "charge")
        {
            _chargeDir = ((D.Player.GlobalPosition - GlobalPosition) with { Y = 0 }).Normalized();
            _arrow.Visible = true;
        }
        Sfx.Play("sisme", Center, -2f, 0.55f);
        Squash.Kick(Visual, 0.08f, Phase.Telegraph, 3f);
    }

    public virtual void Attack(string kind, int count)
    {
        if (_move is Move.Dead or Move.Arrive) return;
        _arrow.Visible = false;
        switch (kind)
        {
            case "charge":
                _move = Move.Charge; _chargeT = 0; _chargeHitPlayer = false;
                Sfx.Play("kabarik_gumleme", Center, 2f, 0.7f);
                break;
            case "volley":
                _move = Move.Walk;
                Volley(count);
                break;
            default:
                _move = Move.Walk;
                SpecialAttack(kind, count);
                break;
        }
    }

    protected virtual void SpecialAttack(string kind, int count) { }

    /// <summary>Yumak / fidan atisi: oyuncuya dogru yay; degerse hasar ve itme.</summary>
    protected void Volley(int count)
    {
        var from = Center + Vector3.Up * Def.Height * 0.3f;
        var target = D.Player.GlobalPosition + Vector3.Up * 1f;
        var dir = (target - from) with { Y = 0 };
        float dist = dir.Length();
        dir = dir.Normalized();
        for (int i = 0; i < count; i++)
        {
            float off = count == 1 ? 0 : Mathf.Lerp(-0.35f, 0.35f, i / (float)(count - 1));
            var p = new AtaProjectile(D, Phase.VolleyDamage, Phase.Knockback, VolleyColor, VolleySize) { Name = "AtaYumak" };
            p.AddToGroup("ata_fx");
            Sys.AddChild(p);
            p.Launch(from, dir.Rotated(Vector3.Up, off), Phase.VolleySpeed, dist);
        }
        Sfx.Play("firlatma", from, 2f, 0.6f);
        Squash.Kick(Visual, 0.1f, 0.3f, 2f);
    }

    protected virtual Color VolleyColor => Palette.OffWhite;
    protected virtual float VolleySize => 0.45f;

    public virtual void OnPhase(int phase, bool quiet = false)
    {
        if (quiet) return;
        Sfx.Play("sisme", Center, 4f, 0.5f);
        Fx.Burst(Center, Palette.MayaTeal, 40, 7f, 0.2f, 1.0f, true, -1f);
        Squash.Kick(Visual, 0.25f, 0.8f, 3f);
        _move = Move.Walk; _arrow.Visible = false;
        Sys.Bar.Phase(phase);
    }

    public virtual void OnStunned()
    {
        _move = Move.Stunned; _arrow.Visible = false;
        Sfx.Play("donma_citirti", Center, 2f, 0.6f);
        Fx.Text3D(Center + Vector3.Up * Def.Height * 0.6f, "SERSEM!", Palette.Warning, 80, 1.5f, 1.4f);
    }

    public virtual void OnWeakPointBroken(int i) { }
    public virtual void OnSheared(float before, float after, Vector3 at) { }
    protected virtual void OnHurt() { }

    public virtual void OnDefeated()
    {
        _move = Move.Dead;
        _arrow.Visible = false;
        Fx.Burst(Center, Palette.MayaTeal, 80, 9f, 0.25f, 1.6f, true, -1f);
        Fx.Burst(Center, Palette.OffWhite, 40, 6f, 0.3f, 1.2f);
        var tw = CreateTween();
        tw.TweenProperty(Visual, "scale", Visual.Scale * 1.25f, 0.35f).SetTrans(Tween.TransitionType.Back);
        tw.TweenProperty(Visual, "scale", Vector3.One * 0.01f, 0.6f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>Yardimcilarin dogdugu nokta (Ata'nin cevresi; Dede'de dallarin alti).</summary>
    public virtual Vector3 SpawnPoint(int i)
    {
        float a = GD.Randf() * Mathf.Tau;
        return GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (Def.Radius + 1.5f);
    }

    // ---------- Kare ----------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _contactCd -= dt;
        Burning = Mathf.Max(0, Burning - dt);
        if (_flash > 0) _flash = Mathf.Max(0, _flash - dt * 5f);
        if (Fight == null) return;
        var p = D.Player;
        var center = Sys.ArenaCenter(Def);
        if (Stunned && _move != Move.Stunned && _move != Move.Dead) _move = Move.Stunned;

        switch (_move)
        {
            case Move.Dead: return;
            case Move.Arrive:
                _arriveT += dt;
                float k = Mathf.Clamp(_arriveT / 1.2f, 0, 1);
                Visual.Position = Vector3.Up * 25f * (1f - k * k);
                if (k >= 1f)
                {
                    Visual.Position = Vector3.Zero;
                    _move = Move.Walk;
                    Squash.Kick(Visual, 0.3f, 0.7f, 3f);
                    Fx.Burst(GlobalPosition + Vector3.Up * 0.3f, Palette.Soil, 50, 8f, 0.25f, 1.0f);
                    Sfx.Play("kabarik_gumleme", GlobalPosition, 6f, 0.5f);
                    p.Shake(0.6f);
                    if (p.GlobalPosition.DistanceTo(GlobalPosition) < Def.Radius + 1.5f) p.Hurt(Phase.ContactDamage, GlobalPosition);
                }
                return;
            case Move.Stunned:
                if (!Stunned) _move = Move.Walk;
                Visual.Rotation = new Vector3(Mathf.Sin(_t * 9f) * 0.08f, Visual.Rotation.Y, Mathf.Cos(_t * 7f) * 0.08f);
                if ((int)(_t * 3) != (int)((_t - dt) * 3)) Fx.Burst(Center + Vector3.Up * Def.Height * 0.55f, Palette.Warning, 3, 1.5f, 0.1f, 0.5f, true, -1f);
                IdleAnim(dt, false);
                return;
            case Move.Telegraph:
                _telegraphT -= dt;
                if (_telegraphKind == "charge")
                {
                    // Sezdirme: yere kirmizi ok, toprak esmesi; yon oyuncuyu yavasca izler.
                    var want = ((p.GlobalPosition - GlobalPosition) with { Y = 0 }).Normalized();
                    _chargeDir = _chargeDir.Slerp(want, Mathf.Min(1f, dt * 1.5f)).Normalized();
                    Face(_chargeDir, dt * 8f);
                    float len = Phase.ChargeSpeed * Phase.ChargeSeconds;
                    _arrow.GlobalPosition = D.World.Ground(GlobalPosition + _chargeDir * len * 0.5f) + Vector3.Up * 0.08f;
                    _arrow.GlobalBasis = Basis.LookingAt(_chargeDir, Vector3.Up);
                    _arrow.Scale = new Vector3(1, 1, len);
                    if ((int)(_t * 6) != (int)((_t - dt) * 6)) Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Soil, 5, 3f, 0.12f, 0.4f);
                }
                else Face(((p.GlobalPosition - GlobalPosition) with { Y = 0 }).Normalized(), dt * 4f);
                IdleAnim(dt, true);
                return;
            case Move.Charge:
                ChargeTick(dt, center);
                return;
        }

        // Yurume: oyuncuya yaklas (arenada kal); dovus alani disindaysa merkeze don.
        var target = p.GlobalPosition;
        if (p.Down || target.DistanceTo(center) > Def.Arena.Leash) target = center;
        var d = (target - GlobalPosition) with { Y = 0 };
        float stop = Def.Radius + 0.6f;
        if (d.Length() > stop)
        {
            var step = d.Normalized() * Mathf.Min(d.Length() - stop, Phase.MoveSpeed * dt);
            var next = GlobalPosition + step;
            if ((next - center).Length() > Def.Arena.Radius + 2f) next = center + ((next - center) with { Y = 0 }).Normalized() * (Def.Arena.Radius + 2f);
            GlobalPosition = D.World.Ground(next);
            Face(d.Normalized(), dt * 3f);
            WalkAnim(dt, Phase.MoveSpeed);
        }
        else IdleAnim(dt, false);
        Contact(p, Phase.ContactDamage);
    }

    void ChargeTick(float dt, Vector3 center)
    {
        var p = D.Player;
        _chargeT += dt;
        var next = GlobalPosition + _chargeDir * Phase.ChargeSpeed * dt;
        Face(_chargeDir, dt * 10f);
        WalkAnim(dt, Phase.ChargeSpeed);
        if ((int)(_t * 10) != (int)((_t - dt) * 10)) Fx.Burst(GlobalPosition + Vector3.Up * 0.2f, Palette.Soil, 6, 4f, 0.15f, 0.5f);
        // Sutuna tosladi mi (Koca Pofuduk asama 3: sersemler)
        foreach (var pil in Sys.Pillars(Def))
        {
            if (new Vector2(next.X - pil.X, next.Z - pil.Z).Length() > Def.Radius + Def.Arena.PillarRadius) continue;
            Fx.Burst(pil + Vector3.Up * 1.5f, Palette.Rock, 40, 7f, 0.22f, 0.9f);
            Sfx.Play("parca_darbe", pil, 6f, 0.5f);
            p.Shake(0.5f);
            Squash.Kick(Visual, 0.3f, 0.5f, 3f);
            GD.Print($"ATA pillar_hit {Def.Id}");
            if (!Sys.PillarHit()) _move = Move.Walk;
            return;
        }
        if (!_chargeHitPlayer && !p.Down && p.GlobalPosition.DistanceTo(next) < Def.Radius + 0.9f)
        {
            _chargeHitPlayer = true;
            p.Hurt(Phase.AttackDamage, GlobalPosition);
            p.Velocity += _chargeDir * Phase.Knockback + Vector3.Up * 3f;
        }
        bool outside = (next - center).Length() > Def.Arena.Radius + 3f;
        if (_chargeT >= Phase.ChargeSeconds || outside) { _move = Move.Walk; Squash.Kick(Visual, 0.15f, 0.4f, 2f); return; }
        GlobalPosition = D.World.Ground(next);
    }

    protected void Contact(Player p, float dmg)
    {
        if (_contactCd > 0 || p.Down || dmg <= 0) return;
        if (p.GlobalPosition.DistanceTo(GlobalPosition) > Def.Radius + 0.8f) return;
        _contactCd = 1.2f;
        p.Hurt(dmg, GlobalPosition);
        Squash.Kick(Visual, 0.1f, 0.3f, 2f);
    }

    protected void Face(Vector3 dir, float k)
    {
        if (dir.LengthSquared() < 0.001f) return;
        float yaw = Mathf.Atan2(dir.X, dir.Z);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, k)), 0);
    }

    /// <summary>Yurume ve bekleme hareketi (alt sinif: klip ya da koddan sallanma).</summary>
    protected virtual void WalkAnim(float dt, float speed) { }
    protected virtual void IdleAnim(float dt, bool tense) { }
    protected float FlashAmount => _flash;
}
