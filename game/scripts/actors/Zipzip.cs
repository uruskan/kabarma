using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Zipzip'in davranis kipi: gece dalgasi, gunduz surusu (Zipzip.Day.cs) ya da sinama (yerinde durur).</summary>
public enum ZipzipMode { Wave, Day, Display }

/// <summary>
/// Zipzip: tarlaya dadanan zararli. Gunduz cayirda 2-3'luk urkek gruplar halinde dolasir (Zipzip.Day.cs, ZipzipMeadow.cs);
/// Kabarma'da plaj topu gibi sisip saldirir (ICERIK §3.1: Sicrayan, Sok zaafi).
/// Gorunus: kucuk sinif model (en cok 5.000 ucgen), ortak sisme shader'i (Swellable), 8 klip (CreatureRig: modelin
/// bekleme/kosu/ziplama/aci klipleri + PoseLibrary). Gidak tekmesiyle havada ucus: Zipzip.Launch.cs.
/// Cit hucumu (ilkgun, Zipzip.Fence.cs) FenceHop/FenceTick ile baglanir.
/// </summary>
public partial class Zipzip : Node3D
{
    public static readonly List<Zipzip> All = new();
    public CreatureDef Def;
    public float Hp, MaxHp;
    public bool Dead;
    public float Scale0 = 1f;
    public bool Display; // sinama: yerinde durur, klipler oynar
    /// <summary>Davranis kipi. Sinama icin Display de kullanilabilir (eski bayrak).</summary>
    public ZipzipMode Mode = ZipzipMode.Wave;
    /// <summary>Kabarmis mi (gece dalgasi). Gunduz Zipzip'i normal gorunur ve avlaninca ragdoll olur (SANAT-SES §4).</summary>
    public bool Swollen { get; }
    /// <summary>Sinamada oynatilacak klip yuvasi (Display).</summary>
    public string DisplaySlot = "run";
    Node3D _body;
    MeshInstance3D _sac;
    ShaderMaterial _mat;
    Swellable _sw;
    AnimationPlayer _anim;
    string _aRun, _aJump, _aHurt, _aIdle;
    CreatureRig _rig;
    float _hurtT;
    Director _dir;
    float _hopT, _hopDur = 0.55f;
    Vector3 _hopFrom, _hopTo;
    float _flash, _squash;
    Vector3 _knock;
    PlotView _target;
    float _eatT;
    float _attackCd;
    float _attackAnimT;
    public float BurnT, FreezeT;
    float _burnTick;
    float _spawnT;
    readonly float _damageMult;

    public Zipzip(CreatureDef def, Director dir, float hpMult, float dmgMult, bool swollen)
    {
        Def = def; _dir = dir; _damageMult = dmgMult; Swollen = swollen;
        MaxHp = Hp = def.Hp * hpMult;
        _body = new Node3D();
        AddChild(_body);
        // M1: kucuk sinif model (zipzip_5k.glb, 4.830 ucgen), iskelet ve dort klip Tripo'dan; eksik klipler PoseLibrary'den.
        _rig = CreatureRig.Load(def, dir?.Poses, _body, "res://assets/models/zipzip/zipzip.glb");
        if (_rig != null)
        {
            _sw = new Swellable(_rig.Model, def.Swell);
            _anim = _rig.Player;
            if (_rig.Animated)
            {
                _aIdle = _rig.SlotAnim["idle"]; _aRun = _rig.SlotAnim["run"]; _aJump = _rig.SlotAnim["attack1"]; _aHurt = _rig.SlotAnim["hit"];
                _rig.Play(swollen ? "run" : "idle", 0f);
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

        // Zayif nokta: parlayan maya kesesi (sirtta), yalniz kabarmis Zipzip'te (SANAT-SES §4.4)
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
            _sw?.Snap(true);
        }
        _body.Scale = Vector3.One * 0.05f;
        All.Add(this);
    }

    public override void _ExitTree() => All.Remove(this);

    public Vector3 Center => GlobalPosition + Vector3.Up * 0.38f * Scale0;
    public Vector3 SacPos => _sac.GlobalPosition;
    public float Radius => 0.42f * Scale0;
    public bool HasSac => _sac.Visible;
    /// <summary>Gorunus ve klipler (sinama ve baglanan partial'lar icin).</summary>
    public CreatureRig Rig => _rig;
    public Swellable Look => _sw;

    // ---------- ilkgun baglantisi: cit hucumu (Zipzip.Fence.cs) ----------
    /// <summary>Gece dalgasinda her yeni sicrayis secilirken: yolda cit varsa to'yu citin onune cek, blocked = true.</summary>
    partial void FenceHop(Vector3 from, ref Vector3 to, ref bool blocked);
    /// <summary>Her kare, hareketten once: handled = true ise o kare Zipzip yerinden kipirdamaz (cite yuklenir).</summary>
    partial void FenceTick(float dt, ref bool handled);

    /// <summary>Saldiri klibini oynatir (oyuncuya, cite ya da hayvana yuklenirken).</summary>
    public void PlayAttack()
    {
        _attackAnimT = 0.5f;
        _rig?.Play(GD.Randf() < 0.5f ? "attack1" : "attack2", 0.06f, 1.2f, restart: true);
        _squash = 1f;
        _sw?.HitJiggle();
    }

    public void Hit(float dmg, bool crit, Vector3 knockDir, float knock, bool weak)
    {
        if (Dead) return;
        Hp -= dmg;
        _flash = 1f;
        _squash = 1f;
        _sw?.HitJiggle();
        if (_rig != null && _hurtT <= 0) { _rig.Play("hit", 0.05f, 1f, restart: true); _hurtT = 0.35f; }
        _knock += knockDir.Normalized() * knock / Mathf.Max(1f, Scale0 * 0.6f);
        Fx.DamageNumber(Center + Vector3.Up * Radius, dmg, crit || weak);
        if (Mode == ZipzipMode.Day) DayStartled(); // Zipzip.Day.cs: surusu kacar
        if (Hp <= 0) Die(knockDir);
    }

    void Die() => Die(Vector3.Zero);

    void Die(Vector3 knockDir)
    {
        Dead = true;
        if (_target != null) { _target.Claimed = false; _target = null; }
        _dir.OnZipzipKilled(this);
        // Gunduz avlanan yaratik ragdoll olur; kabarmis olan balon gibi patlar (SANAT-SES §4).
        if (!Swollen && StartRagdoll(knockDir)) return; // Zipzip.Ragdoll.cs
        Fx.Burst(Center, Palette.MayaTeal, 26, 7f, 0.13f, 0.7f, true, 2f);
        Fx.Burst(Center, Palette.MayaViolet, 14, 5f, 0.1f, 0.6f, true, 2f);
        Fx.Burst(Center, Palette.Ground.Lerp(Palette.Accent, 0.35f), 16, 6f, 0.12f, 0.6f);
        Sfx.Play("pop", Center, 0f);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (TickRagdoll(dt)) return; // Zipzip.Ragdoll.cs
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
        _attackAnimT -= dt;
        _hurtT -= dt;

        // dogus: yerden fiskirir
        float grow = Mathf.Min(1f, _spawnT / 0.5f);
        float baseScale = Scale0 * (1f - Mathf.Pow(1f - grow, 3f));

        if (TickLaunch(dt, baseScale)) return; // Zipzip.Launch.cs: Gidak tekmesiyle havada
        if (Display || Mode == ZipzipMode.Display)
        {
            _body.Scale = Vector3.One * Scale0 * grow;
            if (_hurtT <= 0) _rig?.PlayRepeat(DisplaySlot);
            _sw?.Tick(dt);
            _sw?.Set(flash: 0f);
            return;
        }
        bool fenced = false;
        FenceTick(dt, ref fenced);
        if (fenced) { TickLook(dt, baseScale, 0f); return; }
        if (Mode == ZipzipMode.Day) { TickDay(dt, baseScale, slow); return; } // Zipzip.Day.cs

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
        bool eating = false;

        if (!chasePlayer && _target != null && dist < 0.6f + Radius * 0.8f && _hopT <= 0)
        {
            // Yeme klibi (kafa sallama)
            eating = true;
            _eatT += dt * slow;
            if (_rig == null) _body.Rotation = new Vector3(Mathf.Sin(_eatT * 18f) * 0.25f, _body.Rotation.Y, 0);
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
            PlayAttack();
        }

        // Ziplama hareketi
        if (_hopT <= 0 && dist > 0.5f + Radius * 0.7f)
        {
            _hopT = _hopDur / slow;
            _hopFrom = pos;
            var dir = new Vector3(goal.X - pos.X, 0, goal.Z - pos.Z).Normalized();
            if (!chasePlayer && dist > 3f && _dir.Flow?.Steer(pos, _target != null ? "crop" : "kazan") is { } flow) dir = flow; // yolbulma: akis alani (TEKNIK 8.1); yakinda dogrudan
            float step = Mathf.Min(dist - Radius * 0.5f, Def.Speed * _hopDur * (0.8f + Scale0 * 0.1f));
            _hopTo = _dir.World.Ground(pos + dir * Mathf.Max(0.2f, step));
            bool blocked = false;
            FenceHop(pos, ref _hopTo, ref blocked);
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
            if (_hopT <= 0) { _squash = Mathf.Max(_squash, 0.6f); _sw?.StepJiggle(); }
        }
        if (_knock.LengthSquared() > 0.001f)
        {
            GlobalPosition = _dir.World.Ground(GlobalPosition + _knock * dt);
            _knock = _knock.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 6f));
        }
        TickLook(dt, baseScale, hopH);
        // Klip secimi: vurulunca aci, saldiriyorsa saldiri, yerken yeme, ziplarken kosu, yoksa bekleme.
        if (_rig != null && _hurtT <= 0 && _attackAnimT <= 0)
        {
            string want = eating ? "eat" : _hopT > 0 || _knock.LengthSquared() > 0.5f ? "run" : "idle";
            _rig.Play(want, 0.15f, (FreezeT > 0 ? 0.6f : 1f) * (want == "run" ? 1.3f : 1f));
        }
    }

    /// <summary>Ezilip esneme (yay), ziplama yuksekligi, isabet parlamasi, sisme shader'i.</summary>
    void TickLook(float dt, float baseScale, float hopH)
    {
        _body.Position = new Vector3(0, hopH, 0);
        _squash = Mathf.Max(0, _squash - dt * 4f);
        float sq = Mathf.Sin(_squash * Mathf.Pi) * 0.25f;
        _body.Scale = new Vector3(baseScale * (1 + sq), baseScale * (1 - sq), baseScale * (1 + sq));
        _flash = Mathf.Max(0, _flash - dt * 6f);
        _mat?.SetShaderParameter("flash", _flash * 0.8f);
        _mat?.SetShaderParameter("tint", FreezeT > 0 ? new Color(0.7f, 0.85f, 1f) : Colors.White);
        _sw?.Tick(dt);
        _sw?.Set(flash: _flash * 0.6f);
        _sw?.Tint(FreezeT > 0 ? new Color(0.7f, 0.85f, 1f) : Colors.White);
    }
}
