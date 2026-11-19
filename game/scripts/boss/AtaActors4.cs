using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

// M4 (ata4): Kurbaga Aga, Ciyan Pasa, Buz Tosunu, Ana Maya. Kural ve sayilar Sim'de (AtaRules, atalar.json); bu dosya
// gorunus ve hareket. Modeller YER TUTUCUDUR (koddan; atalar.json "placeholder" Tripo isteminin ozeti).

/// <summary>Genel zayif nokta: kovan benzeri hedef (Kurbaga'nin karin kesesi, Ciyan'in bogumlari, Tosun'un karni, Ana Maya'nin cekirdegi). Indeks AtaFight.WeakPoints'a denk.</summary>
public partial class AtaPoint : Node3D, IAtaTarget
{
    readonly AtaSystem _sys;
    readonly AtaDef _def;
    readonly float _r;
    readonly Color _color;
    StandardMaterial3D _mat;
    MeshInstance3D _mesh;
    bool _active, _broken;
    float _t;
    public int Index { get; }
    public bool Broken => _broken;

    public AtaPoint(AtaSystem sys, AtaDef def, int index, float radius, Color color) { _sys = sys; _def = def; Index = index; _r = radius; _color = color; }

    public Vector3 Center => GlobalPosition;
    public float Radius => _r;
    public bool IsWeakPoint => true;
    public CombatBody Body => null;
    bool Open { get { var f = _sys.Fight; return f != null && !f.Defeated && AtaRules.WeakOpen(f, _def) && Index < f.WeakPoints.Count && f.WeakPoints[Index] > 0; } }
    public bool Alive => _active && !_broken && Open;

    public override void _Ready()
    {
        _mat = new StandardMaterial3D { AlbedoColor = _color, EmissionEnabled = true, Emission = _color, EmissionEnergyMultiplier = 0.4f };
        _mesh = new MeshInstance3D { Mesh = new SphereMesh { Radius = _r, Height = _r * 2f, RadialSegments = 12, Rings = 6 }, MaterialOverride = _mat };
        AddChild(_mesh);
    }

    public void SetActive(bool on)
    {
        _active = on && !_broken;
        Visible = _active || !_broken && on;
        if (_active) { if (!_sys.Targets.Contains(this)) _sys.Targets.Add(this); }
        else _sys.Remove(this);
    }

    /// <summary>Yeni asama: zayif nokta yeniden kurulur (Sim asama basinda canlari yeniler).</summary>
    public void Reset() { _broken = false; if (_mesh != null) _mesh.Visible = true; }

    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (!Alive) return default;
        _sys.DamageWeakPoint(Index, hit);
        Fx.DamageNumber(Center + Vector3.Up * 0.5f, hit.Amount, true);
        Fx.Burst(Center, _color, 8, 3f, 0.1f, 0.4f, true);
        Squash.Kick(this, 0.25f, 0.25f, 2f);
        return new CombatResult { Damage = hit.Amount };
    }

    public void Break()
    {
        if (_broken) return;
        _broken = true;
        Fx.Burst(Center, _color, 36, 6f, 0.16f, 0.9f, true, -2f);
        Sfx.Play("isabet_kritik", Center, 4f, 0.8f);
        if (_mesh != null) _mesh.Visible = false;
        SetActive(false);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_mat != null) _mat.EmissionEnergyMultiplier = Alive ? 3f + Mathf.Sin(_t * 8f) * 1.5f : _active ? 0.5f : 0.1f;
    }
}

/// <summary>Gecici bolge (kum firtinasi): Seconds sure, Radius icindeki oyuncu saniyede Dps hasar alir.</summary>
public partial class AtaZone : Node3D
{
    readonly Director _d;
    readonly float _r, _life, _dps;
    readonly Color _c;
    float _t, _fx;

    public AtaZone(Director d, float radius, float seconds, float dps, Color c) { _d = d; _r = radius; _life = seconds; _dps = dps; _c = c; AddToGroup("ata_fx"); AddToGroup("ata_zone"); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _fx -= dt;
        if (_fx <= 0) { _fx = 0.12f; Fx.Burst(GlobalPosition + new Vector3(GD.Randf() - 0.5f, 0.3f, GD.Randf() - 0.5f) * _r * 1.6f, _c, 6, 3f, 0.22f, 0.9f); }
        var p = _d.Player;
        if (!p.Down && new Vector2(p.GlobalPosition.X - GlobalPosition.X, p.GlobalPosition.Z - GlobalPosition.Z).Length() < _r && (int)(_t * 2) != (int)((_t - dt) * 2)) p.Hurt(_dps * 0.5f, GlobalPosition);
        if (_t >= _life) QueueFree();
    }
}

/// <summary>Katran varili (Ciyan Pasa asama 3): vurulunca patlar; Pasa yakindaysa buyuk hasar ve sersemler.</summary>
public partial class AtaBarrel : Node3D, IAtaTarget
{
    readonly AtaSystem _sys;
    readonly CiyanPasa _owner;
    bool _gone;
    public AtaBarrel(AtaSystem sys, CiyanPasa owner) { _sys = sys; _owner = owner; }

    public Vector3 Center => GlobalPosition + Vector3.Up * 0.7f;
    public float Radius => 0.9f;
    public bool IsWeakPoint => false;
    public CombatBody Body => null;
    public bool Alive => !_gone && _owner != null && IsInstanceValid(_owner) && _owner.Alive;

    public override void _Ready()
    {
        var kit = new MeshKit();
        kit.Cylinder(0.8f, 0.8f, 1.4f, MeshKit.At(0, 0.7f, 0), Palette.Shadow, 10);
        kit.Cylinder(0.82f, 0.82f, 0.12f, MeshKit.At(0, 1.2f, 0), Palette.Warning.Darkened(0.4f), 10);
        AddChild(kit.Instance());
    }

    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (!Alive) return default;
        _gone = true;
        float d = _owner.GlobalPosition.DistanceTo(GlobalPosition);
        var ph = _sys.Fight != null ? AtaRules.PhaseOf(_sys.Fight, _owner.Def) : null;
        Fx.Burst(Center, Palette.Accent, 60, 8f, 0.25f, 1.1f, true);
        Fx.Burst(Center, Palette.Shadow, 40, 6f, 0.3f, 1.2f);
        Sfx.Play("kabarmis_patlama", Center, 4f, 0.7f);
        if (ph != null && d < 11f)
        {
            float done = _sys.Damage(ph.ShardDamage, AtaHitKind.Thrown);
            _sys.PillarHit(); // patlama sersemletir (asama sersemleme suresi)
            _sys.ShardHits++;
            GD.Print($"ATA barrel_hit dmg={done:0} dist={d:0.0}");
            Fx.DamageNumber(_owner.Center + Vector3.Up * 1.5f, done, true);
        }
        else GD.Print($"ATA barrel_miss dist={d:0.0}");
        _sys.Remove(this);
        QueueFree();
        return new CombatResult { Damage = hit.Amount };
    }
}

/// <summary>Kurbaga Aga (ICERIK §4.1): dil-yutma, dalis (nilufer yapragi guvenli), sisme ve karin kesesi.</summary>
public partial class KurbagaAga : AtaActor
{
    Node3D _body;
    MeshInstance3D _mouth;
    AtaPoint _sac;
    bool _swallowed, _wasSub;
    float _bobT;

    protected override AtaHitKind HitKind => Fight != null && Fight.SwallowT > 0 ? AtaHitKind.Inside : AtaHitKind.Weapon;

    protected override void BuildModel()
    {
        var skin = new Color("5E6B3A"); var belly = new Color("C8C27A");
        var kit = new MeshKit();
        kit.Pillow(new Vector3(3.2f, 2.4f, 3.6f), MeshKit.At(0, 1.3f, 0), skin, 0.6f, 14, 8);
        kit.Pillow(new Vector3(2.4f, 0.8f, 2.6f), MeshKit.At(0, 0.65f, 0.3f), belly, 0.6f, 12, 6);
        kit.Sphere(0.5f, MeshKit.At(-0.9f, 2.55f, 1.1f), skin, 10, 6); kit.Sphere(0.5f, MeshKit.At(0.9f, 2.55f, 1.1f), skin, 10, 6);
        kit.Sphere(0.2f, MeshKit.At(-0.9f, 2.7f, 1.45f), Palette.Warning, 8, 5); kit.Sphere(0.2f, MeshKit.At(0.9f, 2.7f, 1.45f), Palette.Warning, 8, 5);
        foreach (float sx in new[] { -1f, 1f })
        {
            kit.Cylinder(0.3f, 0.45f, 1.4f, MeshKit.At(sx * 1.5f, 0.5f, 1.0f), skin.Darkened(0.2f), 8);
            kit.Pillow(new Vector3(0.9f, 0.3f, 1.2f), MeshKit.At(sx * 1.5f, 0.15f, 1.5f), skin.Darkened(0.2f), 0.5f, 8, 4);
        }
        _body = kit.Instance();
        Visual.AddChild(_body);
        var mouthMat = new StandardMaterial3D { AlbedoColor = new Color("7A2A2A") };
        _mouth = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.9f, Height = 0.5f, RadialSegments = 12, Rings = 4 }, MaterialOverride = mouthMat, Position = new Vector3(0, 1.3f, 1.85f), Scale = new Vector3(1.4f, 0.5f, 0.4f) };
        Visual.AddChild(_mouth);
        // Karin kesesi (maya kesesi): asama 3'te isiyan zayif nokta.
        _sac = new AtaPoint(Sys, Def, 0, 0.7f, Palette.MayaTeal) { Name = "KarinKesesi", Position = new Vector3(0, 0.9f, 1.9f) };
        Visual.AddChild(_sac);
        _sac.Visible = false;
    }

    protected override Color VolleyColor => new Color("5B3A4A");

    public override void OnPhase(int phase, bool quiet = false)
    {
        base.OnPhase(phase, quiet);
        if (phase == 2)
        {
            _sac.Reset(); _sac.Visible = true; _sac.SetActive(true);
            if (!quiet) { D.Hud.Say(Loc.T("ata.say_swelled")); Squash.Kick(Visual, 0.3f, 0.8f, 3f); }
        }
        if (!quiet && phase == 1) D.Hud.Say(Loc.T("ata.say_dive"));
    }

    protected override void SpecialAttack(string kind, int count)
    {
        var p = D.Player;
        switch (kind)
        {
            case "swallow":
                // Dil: yakindaki oyuncuyu yakalar, SwallowSeconds iceride tutar.
                float d = new Vector2(p.GlobalPosition.X - GlobalPosition.X, p.GlobalPosition.Z - GlobalPosition.Z).Length();
                Sfx.Play("firlatma", Center, 2f, 0.5f);
                if (d < 13f && !p.Down && Sys.BeginSwallow())
                {
                    _swallowed = true;
                    Fx.Burst(p.GlobalPosition + Vector3.Up, Palette.MayaTeal, 20, 4f, 0.14f, 0.6f, true);
                    D.Hud.Say(Loc.T("ata.say_swallowed"));
                    GD.Print("ATA swallow caught");
                }
                else { Squash.Kick(Visual, 0.15f, 0.3f, 2f); GD.Print("ATA swallow miss"); }
                break;
            case "dive":
                Fx.Burst(GlobalPosition + Vector3.Up * 0.3f, Palette.MayaTeal, 40, 6f, 0.2f, 0.9f);
                Sfx.Play("water", GlobalPosition, 4f, 0.7f);
                break;
            case "swell":
                Squash.Kick(Visual, 0.45f, 0.7f, 3f);
                Ring(GlobalPosition, Phase.LungeRadius, Palette.MayaTeal);
                if (AreaHit(GlobalPosition, Phase.LungeRadius, Phase.AttackDamage, Phase.Knockback)) GD.Print("ATA swell hit");
                break;
        }
    }

    protected override void ExtraTick(float dt)
    {
        var p = D.Player;
        _bobT += dt;
        if (_swallowed)
        {
            if (Fight.SwallowT > 0 && !Fight.Defeated)
            {
                p.GlobalPosition = Center + Vector3.Up * 0.3f;
                p.Velocity = Vector3.Zero;
            }
            else
            {
                _swallowed = false;
                var dir = FaceDir;
                p.GlobalPosition = Center + dir * (Def.Radius + 1.2f);
                p.Velocity = dir * 14f + Vector3.Up * 6f;
                p.Hurt(Phase.AttackDamage * DmgMult, GlobalPosition);
                Fx.Burst(Center + dir * Def.Radius, Palette.MayaTeal, 30, 6f, 0.16f, 0.7f, true);
                Sfx.Play("firlatma", Center, 4f, 0.6f);
                GD.Print("ATA swallow spit");
            }
        }
        // Dalis: gorunus yerin altina iner; cikista oyuncunun yerinde alan vurusu (nilufer yapragindakine degmez).
        bool sub = Fight.SubmergedT > 0;
        Visual.Position = Visual.Position.Lerp(sub ? Vector3.Down * 4.5f : Vector3.Zero, Mathf.Min(1f, dt * 6f));
        if (_wasSub && !sub)
        {
            if (!p.Down) GlobalPosition = D.World.Ground(p.GlobalPosition);
            Ring(GlobalPosition, Phase.LungeRadius, Palette.MayaTeal);
            bool safe = Sys.Props(Def, "pad").Any(pad => new Vector2(p.GlobalPosition.X - pad.pos.X, p.GlobalPosition.Z - pad.pos.Z).Length() <= pad.r);
            if (safe) { Fx.Text3D(p.GlobalPosition + Vector3.Up * 2f, "YAPRAK", Palette.Plant, 56, 0.9f, 0.9f); GD.Print("ATA dive safe_on_pad"); }
            else if (AreaHit(GlobalPosition, Phase.LungeRadius, Phase.AttackDamage, Phase.Knockback)) GD.Print("ATA dive hit");
            Squash.Kick(Visual, 0.35f, 0.6f, 3f);
        }
        _wasSub = sub;
        if (_mouth != null) _mouth.Scale = new Vector3(1.4f, 0.5f + (Telegraphing && TelegraphKind == "swallow" ? 0.5f : 0f), 0.4f);
    }

    public override void OnWeakPointBroken(int i) => _sac.Break();
    protected override void WalkAnim(float dt, float speed) => _body.Position = Vector3.Up * Mathf.Abs(Mathf.Sin(_bobT * 3f)) * 0.12f;
    protected override void IdleAnim(float dt, bool tense) => _body.Scale = Vector3.One * (1f + Mathf.Sin(_bobT * 2f) * 0.02f);
}

/// <summary>Ciyan Pasa (ICERIK §4.1): 14 bogumlu govde (zayif noktalar), kopan bogumlar, kum firtinasi, mesa duvarindan ziplama ve katran varilleri.</summary>
public partial class CiyanPasa : AtaActor
{
    const int Segs = 14;
    readonly List<AtaPoint> _segs = new();
    readonly List<Vector3> _trail = new();
    readonly List<AtaBarrel> _barrels = new();
    Node3D _head;
    float _leapT = -1;
    Vector3 _leapFrom, _leapTo;
    float _walkT;

    protected override void BuildModel()
    {
        var shell = new Color("9A4B2A"); var plate = new Color("C98A4B");
        var kit = new MeshKit();
        kit.Pillow(new Vector3(2.4f, 1.6f, 2.8f), MeshKit.At(0, 1.0f, 0), shell, 0.6f, 12, 6);
        foreach (float sx in new[] { -1f, 1f }) kit.Cylinder(0.12f, 0.35f, 1.8f, MeshKit.At(new Vector3(sx * 0.9f, 0.6f, 1.6f), new Vector3(1.2f, 0, sx * -0.4f)), plate, 8); // kiskac
        kit.Sphere(0.2f, MeshKit.At(-0.55f, 1.5f, 1.3f), Palette.Warning, 8, 5); kit.Sphere(0.2f, MeshKit.At(0.55f, 1.5f, 1.3f), Palette.Warning, 8, 5);
        _head = kit.Instance();
        Visual.AddChild(_head);
        // Bogumlar: ust duzey dugumler (Sys altinda), govdenin izini surer.
        for (int i = 0; i < Segs; i++)
        {
            var s = new AtaPoint(Sys, Def, i, 0.95f - i * 0.02f, i % 2 == 0 ? shell : plate) { Name = "Bogum" + i };
            s.AddToGroup("ata_fx");
            Sys.AddChild(s);
            s.GlobalPosition = GlobalPosition - FaceDir * (2.2f + i * 1.5f);
            _segs.Add(s);
        }
    }

    protected override void ExtraTick(float dt)
    {
        _walkT += dt;
        var pos = GlobalPosition;
        if (_trail.Count == 0 || _trail[0].DistanceTo(pos) > 0.45f) _trail.Insert(0, pos);
        if (_trail.Count > 400) _trail.RemoveRange(400, _trail.Count - 400);
        for (int i = 0; i < _segs.Count; i++)
        {
            int idx = Mathf.Min(_trail.Count - 1, (i + 1) * 3 + 2);
            var at = _trail.Count > 0 ? _trail[idx] : pos - FaceDir * (i + 1) * 1.5f;
            if (_trail.Count < (i + 1) * 3 + 3) at = pos - FaceDir * (2.2f + i * 1.5f);
            _segs[i].GlobalPosition = _segs[i].GlobalPosition.Lerp(D.World.Ground(at) + Vector3.Up * (0.9f + Mathf.Sin(_walkT * 5f + i) * 0.08f), Mathf.Min(1f, dt * 10f));
        }
        // Asama 3: mesa duvarina tirmanip ziplama
        if (Fight.Phase == 2 && Fight.ShieldT <= 0) LeapTick(dt);
    }

    void LeapTick(float dt)
    {
        var wall = Sys.Props(Def, "wall").FirstOrDefault();
        if (_leapT >= 0)
        {
            _leapT += dt;
            float k = Mathf.Clamp(_leapT / 0.8f, 0, 1);
            GlobalPosition = _leapFrom.Lerp(_leapTo, k) + Vector3.Up * Mathf.Sin(k * Mathf.Pi) * 5f;
            Frozen = true;
            if (k >= 1)
            {
                _leapT = -1;
                GlobalPosition = D.World.Ground(_leapTo);
                Ring(GlobalPosition, Phase.LungeRadius, Palette.Soil);
                Sfx.Play("kabarik_gumleme", GlobalPosition, 6f, 0.5f);
                AreaHit(GlobalPosition, Phase.LungeRadius, Phase.AttackDamage, Phase.Knockback);
                Visual.Position = Vector3.Zero;
                GD.Print("ATA leap land");
            }
            return;
        }
        if (Telegraphing && TelegraphKind == "leap" && wall.r > 0)
        {
            // Sezdirme: duvara tirmanir (yerden 4 m).
            var to = new Vector3(wall.pos.X, 0, wall.pos.Z + 2.5f);
            GlobalPosition = GlobalPosition.Lerp(D.World.Ground(to), Mathf.Min(1f, dt * 2.5f));
            Visual.Position = Visual.Position.Lerp(Vector3.Up * 4f, Mathf.Min(1f, dt * 3f));
            Frozen = true;
        }
        else Visual.Position = Visual.Position.Lerp(Vector3.Zero, Mathf.Min(1f, dt * 4f));
    }

    protected override void SpecialAttack(string kind, int count)
    {
        var p = D.Player;
        switch (kind)
        {
            case "sandstorm":
                var z = new AtaZone(D, Phase.LungeRadius, 7f, Phase.AttackDamage * 0.35f, Palette.Soil.Lerp(Palette.Warning, 0.3f)) { Name = "KumFirtinasi" };
                Sys.AddChild(z);
                z.GlobalPosition = D.World.Ground(p.GlobalPosition);
                Sfx.Play("ruzgar", z.GlobalPosition, 2f);
                GD.Print("ATA sandstorm");
                break;
            case "leap":
                _leapFrom = GlobalPosition; _leapTo = D.World.Ground(p.GlobalPosition); _leapT = 0;
                Sfx.Play("kabarik_gumleme", Center, 4f, 0.8f);
                break;
        }
    }

    public override void OnPhase(int phase, bool quiet = false)
    {
        base.OnPhase(phase, quiet);
        foreach (var s in _segs)
        {
            if (phase < 2) { s.Reset(); s.SetActive(true); s.Visible = true; }
            else { s.SetActive(false); s.Visible = false; }
        }
        if (phase == 2)
        {
            foreach (var b in _barrels) if (IsInstanceValid(b)) b.QueueFree();
            _barrels.Clear();
            foreach (var (pos, _) in Sys.Props(Def, "barrel"))
            {
                var b = new AtaBarrel(Sys, this) { Name = "KatranVarili" };
                b.AddToGroup("ata_fx");
                Sys.AddChild(b);
                b.GlobalPosition = pos;
                Sys.Targets.Add(b);
                _barrels.Add(b);
            }
            if (!quiet) D.Hud.Say(Loc.T("ata.say_mesa"));
        }
        if (!quiet && phase == 1) D.Hud.Say(Loc.T("ata.say_sandstorm"));
    }

    public override void OnWeakPointBroken(int i) { if (i >= 0 && i < _segs.Count) _segs[i].Break(); }

    public override void _ExitTree()
    {
        base._ExitTree();
        foreach (var s in _segs) if (IsInstanceValid(s)) s.QueueFree();
    }

    protected override void WalkAnim(float dt, float speed) { _head.Rotation = new Vector3(0, Mathf.Sin(_walkT * 6f) * 0.08f, 0); }
}

/// <summary>Buz Tosunu (ICERIK §4.1): dis ve buz sutunu, donmus gol, sirtta buzul (ates eritir), karin kesesi.</summary>
public partial class BuzTosunu : AtaActor
{
    Node3D _glacier;
    AtaPoint _belly;
    float _walkT;

    protected override Color VolleyColor => new Color("CFE6F2");
    protected override float VolleySize => 0.7f;

    protected override void BuildModel()
    {
        var fur = new Color("E8EEF2"); var ice = new Color("AEDCF0"); var tusk = new Color("F4EFE0");
        var kit = new MeshKit();
        kit.Pillow(new Vector3(3.0f, 2.2f, 4.2f), MeshKit.At(0, 1.9f, 0), fur, 0.6f, 14, 8);
        kit.Pillow(new Vector3(1.6f, 1.5f, 1.8f), MeshKit.At(0, 2.2f, 2.3f), fur, 0.6f, 12, 6); // bas
        foreach (float sx in new[] { -1f, 1f })
        {
            kit.Cylinder(0.05f, 0.22f, 2.2f, MeshKit.At(new Vector3(sx * 0.7f, 1.4f, 3.1f), new Vector3(1.0f, 0, sx * -0.2f)), tusk, 8); // dis
            kit.Cylinder(0.35f, 0.4f, 1.6f, MeshKit.At(sx * 1.0f, 0.8f, 1.4f), fur.Darkened(0.15f), 8);
            kit.Cylinder(0.35f, 0.4f, 1.6f, MeshKit.At(sx * 1.0f, 0.8f, -1.4f), fur.Darkened(0.15f), 8);
            kit.Sphere(0.12f, MeshKit.At(sx * 0.5f, 2.5f, 3.0f), Palette.MayaTeal, 6, 4);
        }
        Visual.AddChild(kit.Instance());
        var gk = new MeshKit();
        gk.Pillow(new Vector3(2.4f, 1.8f, 3.0f), MeshKit.At(0, 3.5f, -0.2f), ice, 0.25f, 10, 5);
        gk.Cylinder(0f, 0.5f, 1.6f, MeshKit.At(-0.7f, 4.8f, 0.2f), ice, 6); gk.Cylinder(0f, 0.4f, 1.3f, MeshKit.At(0.6f, 4.6f, -0.6f), ice, 6);
        _glacier = gk.Instance(new StandardMaterial3D { AlbedoColor = new Color(ice, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f });
        Visual.AddChild(_glacier);
        _glacier.Visible = false;
        _belly = new AtaPoint(Sys, Def, 0, 0.65f, Palette.MayaTeal) { Name = "KarinKesesi", Position = new Vector3(0, 0.9f, 0.3f) };
        Visual.AddChild(_belly);
        _belly.Visible = false;
    }

    public override void OnPhase(int phase, bool quiet = false)
    {
        base.OnPhase(phase, quiet);
        if (phase == 2)
        {
            _glacier.Visible = true; _belly.Reset(); _belly.Visible = true; _belly.SetActive(true);
            if (!quiet) D.Hud.Say(Loc.T("ata.say_glacier"));
        }
        if (!quiet && phase == 1) D.Hud.Say(Loc.T("ata.say_blizzard"));
    }

    protected override void SpecialAttack(string kind, int count)
    {
        switch (kind)
        {
            case "icepillar": Volley(Mathf.Max(1, count)); break;
            case "icebreak":
                Squash.Kick(Visual, 0.35f, 0.6f, 3f);
                Ring(GlobalPosition, Phase.LungeRadius, new Color("CFE6F2"));
                Sfx.Play("donma_citirti", Center, 6f, 0.5f);
                if (AreaHit(GlobalPosition, Phase.LungeRadius, Phase.AttackDamage, Phase.Knockback)) GD.Print("ATA icebreak hit");
                break;
        }
    }

    protected override void ExtraTick(float dt)
    {
        _walkT += dt;
        if (!_glacier.Visible) return;
        float k = Phase.Armor > 0 ? Mathf.Clamp(Fight.Armor / Phase.Armor, 0f, 1f) : 0f;
        _glacier.Scale = new Vector3(1f, 0.25f + 0.75f * k, 1f);
        if (Burning > 0 && (int)(_walkT * 6) != (int)((_walkT - dt) * 6)) Fx.Burst(_glacier.GlobalPosition, new Color("CFE6F2"), 4, 3f, 0.12f, 0.6f);
        if (k <= 0.01f && _glacier.Visible) { _glacier.Visible = false; Fx.Burst(Center + Vector3.Up * 2f, new Color("CFE6F2"), 50, 7f, 0.2f, 1f); D.Hud.Say(Loc.T("ata.say_melted")); }
    }

    public override void OnWeakPointBroken(int i) => _belly.Break();
    protected override void WalkAnim(float dt, float speed) => Visual.Position = Vector3.Up * Mathf.Abs(Mathf.Sin(_walkT * 3f)) * 0.08f;
}

/// <summary>Ana Maya (ICERIK §4.1): Atalarin hamurdan kopyalari, kabaran zemin ve Hamurcuk seli, herkesin dev boyu ve cekirdek.</summary>
public partial class AnaMaya : AtaActor
{
    AtaPoint _core;
    Node3D _blob;
    float _t2;

    protected override Color VolleyColor => new Color("F0E2C4");

    protected override void BuildModel()
    {
        var dough = new Color("EBDDB8");
        var kit = new MeshKit();
        kit.Sphere(3.2f, MeshKit.At(new Vector3(0, 3.0f, 0), Vector3.Zero, new Vector3(1f, 1.1f, 1f)), dough, 18, 10);
        kit.Sphere(1.6f, MeshKit.At(-2.4f, 2.2f, 1.0f), dough.Darkened(0.05f), 12, 7);
        kit.Sphere(1.6f, MeshKit.At(2.4f, 2.0f, 1.2f), dough.Darkened(0.05f), 12, 7);
        kit.Sphere(1.2f, MeshKit.At(0, 5.4f, 0.4f), dough.Lightened(0.05f), 12, 7);
        _blob = kit.Instance();
        Visual.AddChild(_blob);
        var eyeMat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.MayaTeal, EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 3f };
        foreach (float sx in new[] { -1f, 1f }) Visual.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f }, MaterialOverride = eyeMat, Position = new Vector3(sx * 0.9f, 3.6f, 2.9f) });
        _core = new AtaPoint(Sys, Def, 0, 0.9f, Palette.MayaTeal) { Name = "Cekirdek", Position = new Vector3(0, 2.4f, 2.4f) };
        Visual.AddChild(_core);
        _core.Visible = false;
    }

    public override void OnPhase(int phase, bool quiet = false)
    {
        base.OnPhase(phase, quiet);
        if (phase == 2)
        {
            _core.Reset(); _core.Visible = true; _core.SetActive(true);
            if (!quiet) D.Hud.Say(Loc.T("ata.say_giants"));
        }
        if (!quiet && phase == 1) D.Hud.Say(Loc.T("ata.say_floor"));
    }

    protected override void SpecialAttack(string kind, int count)
    {
        if (kind != "surge") return;
        var w = new RootWave(D, Phase.WaveSpeed, Phase.WaveRadius, Phase.WaveWidth, Phase.AttackDamage * DmgMult, Phase.Knockback) { Name = "KabaranZemin" };
        w.AddToGroup("ata_fx");
        w.AddToGroup("ata_rootwave");
        Sys.AddChild(w);
        w.GlobalPosition = D.World.Ground(GlobalPosition) + Vector3.Up * 0.15f;
        Sfx.Play("kaya_kemirme", GlobalPosition, 6f, 0.4f);
        Squash.Kick(Visual, 0.25f, 0.5f, 2f);
        D.Player.Shake(0.5f);
    }

    protected override void ExtraTick(float dt) { _t2 += dt; _blob.Scale = Vector3.One * (1f + Mathf.Sin(_t2 * 1.6f) * 0.03f); }
    public override void OnWeakPointBroken(int i) => _core.Break();
    protected override void WalkAnim(float dt, float speed) { }
}
