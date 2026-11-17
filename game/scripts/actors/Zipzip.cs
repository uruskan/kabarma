using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Zipzip: tarlaya dadanan zararli. Kabarma'da plaj topu gibi sisip saldirir (ICERIK §3.1: Sicrayan, Sok zaafi).
/// Govde, kulaklar ve gozler koddan; sisme, titreme, damar ve maya kesesi shader ile (SANAT-SES §4).
/// </summary>
public partial class Zipzip : Node3D
{
    public static readonly List<Zipzip> All = new();
    public CreatureDef Def;
    public float Hp, MaxHp;
    public bool Dead;
    public float Scale0 = 1f;
    public bool Display; // sinama: yerinde durur, klipler oynar
    Node3D _body;
    MeshInstance3D _sac;
    ShaderMaterial _mat;
    Swellable _sw;
    AnimationPlayer _anim;
    string _aRun, _aJump, _aHurt, _aIdle;
    float _hurtT;
    Director _dir;
    float _hopT, _hopDur = 0.55f;
    Vector3 _hopFrom, _hopTo;
    float _flash, _squash;
    Vector3 _knock;
    PlotView _target;
    float _eatT;
    float _attackCd;
    public float BurnT, FreezeT;
    float _burnTick;
    float _spawnT;
    readonly float _damageMult;

    public Zipzip(CreatureDef def, Director dir, float hpMult, float dmgMult, bool swollen)
    {
        Def = def; _dir = dir; _damageMult = dmgMult;
        MaxHp = Hp = def.Hp * hpMult;
        _body = new Node3D();
        AddChild(_body);
        // M1 altin varlik: Tripo'dan iskeletli, dokulu Zipzip (idle, run, jump, hurt). Yoksa koddan govde.
        var model = Assets.Load("res://assets/models/zipzip/zipzip.glb");
        if (model != null)
        {
            _body.AddChild(model);
            _sw = new Swellable(model, 4f);
            _anim = model.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
            if (_anim != null)
            {
                foreach (var n in _anim.GetAnimationList())
                {
                    var l = n.ToLower();
                    if (l.Contains("run")) _aRun = n; else if (l.Contains("jump")) _aJump = n; else if (l.Contains("hurt")) _aHurt = n; else if (l.Contains("idle")) _aIdle = n;
                }
                foreach (var n in new[] { _aRun, _aIdle }) if (n != null) _anim.GetAnimation(n).LoopMode = Animation.LoopModeEnum.Linear;
                _anim.Play(_aIdle ?? _aRun);
                _anim.Seek((float)GD.RandRange(0.0, 0.8), true);
            }
        }
        else
        {
            var kit = new MeshKit();
            var fur = Palette.Ground.Lerp(Palette.Accent, 0.35f);
            kit.Sphere(0.36f, MeshKit.At(new Vector3(0, 0.36f, 0), Vector3.Zero, new Vector3(1f, 0.92f, 1.05f)), fur, 14, 8);
            kit.Pillow(new Vector3(0.13f, 0.42f, 0.08f), MeshKit.At(new Vector3(-0.13f, 0.8f, -0.02f), new Vector3(-0.2f, 0, -0.25f)), fur, 0.5f, 10, 6);
            kit.Pillow(new Vector3(0.13f, 0.42f, 0.08f), MeshKit.At(new Vector3(0.13f, 0.8f, -0.02f), new Vector3(-0.2f, 0, 0.25f)), fur, 0.5f, 10, 6);
            kit.Sphere(0.1f, MeshKit.At(-0.13f, 0.47f, 0.27f), Palette.OffWhite, 8, 6);
            kit.Sphere(0.1f, MeshKit.At(0.13f, 0.47f, 0.27f), Palette.OffWhite, 8, 6);
            _mat = MeshKit.NewLiveMaterial();
            _mat.SetShaderParameter("wobble_amp", 0.025f);
            _body.AddChild(kit.Instance(_mat));
        }

        // Zayif nokta: parlayan maya kesesi (sirtta)
        _sac = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 3f },
            Position = new Vector3(0, 0.62f, -0.2f),
            Visible = swollen,
        };
        _body.AddChild(_sac);
        if (swollen)
        {
            Scale0 = def.SwollenScale;
            _mat?.SetShaderParameter("swell", 1f);
            _mat?.SetShaderParameter("glow", 0.9f);
            _sw?.Set(swell: 1f, glow: 0.9f, push: 0.04f, wobble: 0.02f);
        }
        _body.Scale = Vector3.One * 0.05f;
        All.Add(this);
    }

    public override void _ExitTree() => All.Remove(this);

    public Vector3 Center => GlobalPosition + Vector3.Up * 0.38f * Scale0;
    public Vector3 SacPos => _sac.GlobalPosition;
    public float Radius => 0.42f * Scale0;
    public bool HasSac => _sac.Visible;

    public void Hit(float dmg, bool crit, Vector3 knockDir, float knock, bool weak)
    {
        if (Dead) return;
        Hp -= dmg;
        _flash = 1f;
        _squash = 1f;
        if (_anim != null && _aHurt != null && _hurtT <= 0) { _anim.Play(_aHurt, 0.05); _hurtT = 0.35f; }
        _knock += knockDir.Normalized() * knock / Mathf.Max(1f, Scale0 * 0.6f);
        Fx.DamageNumber(Center + Vector3.Up * Radius, dmg, crit || weak);
        if (Hp <= 0) Die();
    }

    void Die()
    {
        Dead = true;
        if (_target != null) { _target.Claimed = false; _target = null; }
        // Balon gibi patlar; Maya ve parca sacilir (SANAT-SES §4.5)
        Fx.Burst(Center, Palette.MayaTeal, 26, 7f, 0.13f, 0.7f, true, 2f);
        Fx.Burst(Center, Palette.MayaViolet, 14, 5f, 0.1f, 0.6f, true, 2f);
        Fx.Burst(Center, Palette.Ground.Lerp(Palette.Accent, 0.35f), 16, 6f, 0.12f, 0.6f);
        Sfx.Play("pop", Center, 0f);
        _dir.OnZipzipKilled(this);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Dead) return;
        _spawnT += dt;
        float slow = FreezeT > 0 ? 0.6f : 1f;
        if (FreezeT > 0) FreezeT -= dt;
        if (BurnT > 0)
        {
            BurnT -= dt; _burnTick -= dt;
            if (_burnTick <= 0) { _burnTick = 0.5f; Hit(Mathf.Max(1f, MaxHp * 0.05f), false, Vector3.Zero, 0, false); Fx.Burst(Center, Palette.Accent, 4, 2f, 0.08f, 0.4f, true, -3f); }
            if (Dead) return;
        }
        _attackCd -= dt;

        // dogus: yerden fiskirir
        float grow = Mathf.Min(1f, _spawnT / 0.5f);
        float baseScale = Scale0 * (1f - Mathf.Pow(1f - grow, 3f));

        if (Display)
        {
            float g0 = Mathf.Min(1f, _spawnT / 0.5f);
            _body.Scale = Vector3.One * Scale0 * g0;
            if (_anim != null && _aRun != null && _anim.CurrentAnimation != _aRun) _anim.Play(_aRun);
            _sw?.Set(flash: 0f);
            return;
        }
        var player = _dir.Player;
        var pos = GlobalPosition;
        // Hedef secimi: kendi ailesinin ekini -> saldirani (GDD 14.3). Oyuncu yakinsa ona dalar.
        bool chasePlayer = !player.Down && player.GlobalPosition.DistanceTo(pos) < 3.5f + Radius;
        if (!chasePlayer && (_target == null || !_target.Plot.Planted))
        {
            if (_target != null) _target.Claimed = false;
            _target = _dir.FarmView.NearestPlanted(pos);
            if (_target != null) _target.Claimed = true;
            _eatT = 0;
        }

        Vector3 goal = chasePlayer ? player.GlobalPosition : _target?.GlobalPosition ?? _dir.OcakPos;
        float dist = new Vector2(goal.X - pos.X, goal.Z - pos.Z).Length();

        if (!chasePlayer && _target != null && dist < 0.6f + Radius * 0.8f && _hopT <= 0)
        {
            // Yeme: kafa sallama
            _eatT += dt * slow;
            _body.Rotation = new Vector3(Mathf.Sin(_eatT * 18f) * 0.25f, _body.Rotation.Y, 0);
            if (_eatT >= Def.EatSeconds)
            {
                _dir.State.Farm.Eat(_target.Plot.X, _target.Plot.Y);
                _target.Claimed = false;
                _target = null;
                _eatT = 0;
            }
        }
        else if (chasePlayer && dist < Radius + 0.8f && _attackCd <= 0)
        {
            _attackCd = 1.3f;
            player.Hurt(Mathf.Round(Def.Damage * _damageMult * (Scale0 > 1.5f ? 1.6f : 1f)), pos);
            _squash = 1f;
        }

        // Ziplama hareketi
        if (_hopT <= 0 && dist > 0.5f + Radius * 0.7f)
        {
            _hopT = _hopDur / slow;
            _hopFrom = pos;
            var dir = new Vector3(goal.X - pos.X, 0, goal.Z - pos.Z).Normalized();
            float step = Mathf.Min(dist - Radius * 0.5f, Def.Speed * _hopDur * (0.8f + Scale0 * 0.1f));
            _hopTo = _dir.World.Ground(pos + dir * Mathf.Max(0.2f, step));
            _body.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        }
        float hopH = 0;
        if (_hopT > 0)
        {
            _hopT -= dt;
            float k = 1f - Mathf.Clamp(_hopT / (_hopDur / slow), 0, 1);
            var p = _hopFrom.Lerp(_hopTo, k);
            hopH = Mathf.Sin(k * Mathf.Pi) * (0.5f + Scale0 * 0.25f);
            GlobalPosition = p;
            if (_hopT <= 0) _squash = Mathf.Max(_squash, 0.6f);
        }
        if (_knock.LengthSquared() > 0.001f)
        {
            GlobalPosition = _dir.World.Ground(GlobalPosition + _knock * dt);
            _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 6f));
        }
        _body.Position = new Vector3(0, hopH, 0);

        // Ezilip esneme (yay) ve isabet parlamasi
        _squash = Mathf.Max(0, _squash - dt * 4f);
        float sq = Mathf.Sin(_squash * Mathf.Pi) * 0.25f;
        _body.Scale = new Vector3(baseScale * (1 + sq), baseScale * (1 - sq), baseScale * (1 + sq));
        _flash = Mathf.Max(0, _flash - dt * 6f);
        _mat?.SetShaderParameter("flash", _flash * 0.8f);
        _mat?.SetShaderParameter("tint", FreezeT > 0 ? new Color(0.7f, 0.85f, 1f) : Colors.White);
        _sw?.Set(flash: _flash * 0.6f);
        _sw?.Tint(FreezeT > 0 ? new Color(0.7f, 0.85f, 1f) : Colors.White);
        // Klip secimi: vurulunca acı, hareket halinde kosu, yerken bekleme.
        if (_anim != null)
        {
            _hurtT -= dt;
            if (_hurtT <= 0)
            {
                string want = _hopT > 0 || _knock.LengthSquared() > 0.5f ? (_aRun ?? _aIdle) : (_aIdle ?? _aRun);
                if (want != null && _anim.CurrentAnimation != want) _anim.Play(want, 0.15);
                _anim.SpeedScale = (FreezeT > 0 ? 0.6f : 1f) * (want == _aRun ? 1.3f : 1f);
            }
        }
    }
}
