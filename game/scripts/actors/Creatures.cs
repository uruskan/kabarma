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
    Node3D _body;
    MeshInstance3D _sac;
    ShaderMaterial _mat;
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
        var kit = new MeshKit();
        var fur = Palette.Ground.Lerp(Palette.Accent, 0.35f);
        var belly = Palette.OffWhite;
        kit.Sphere(0.36f, MeshKit.At(new Vector3(0, 0.36f, 0), Vector3.Zero, new Vector3(1f, 0.92f, 1.05f)), fur, 14, 8);
        kit.Sphere(0.24f, MeshKit.At(new Vector3(0, 0.3f, 0.16f), Vector3.Zero, new Vector3(1f, 1f, 0.7f)), belly, 10, 6);
        kit.Pillow(new Vector3(0.13f, 0.42f, 0.08f), MeshKit.At(new Vector3(-0.13f, 0.8f, -0.02f), new Vector3(-0.2f, 0, -0.25f)), fur, 0.5f, 10, 6);
        kit.Pillow(new Vector3(0.13f, 0.42f, 0.08f), MeshKit.At(new Vector3(0.13f, 0.8f, -0.02f), new Vector3(-0.2f, 0, 0.25f)), fur, 0.5f, 10, 6);
        kit.Pillow(new Vector3(0.08f, 0.28f, 0.04f), MeshKit.At(new Vector3(-0.13f, 0.8f, 0.01f), new Vector3(-0.2f, 0, -0.25f)), Palette.Accent.Lerp(Palette.OffWhite, 0.4f), 0.5f, 8, 4);
        kit.Pillow(new Vector3(0.08f, 0.28f, 0.04f), MeshKit.At(new Vector3(0.13f, 0.8f, 0.01f), new Vector3(-0.2f, 0, 0.25f)), Palette.Accent.Lerp(Palette.OffWhite, 0.4f), 0.5f, 8, 4);
        kit.Sphere(0.1f, MeshKit.At(-0.13f, 0.47f, 0.27f), Palette.OffWhite, 8, 6);
        kit.Sphere(0.1f, MeshKit.At(0.13f, 0.47f, 0.27f), Palette.OffWhite, 8, 6);
        kit.Sphere(0.06f, MeshKit.At(-0.13f, 0.47f, 0.35f), Palette.NightDeep, 6, 4);
        kit.Sphere(0.06f, MeshKit.At(0.13f, 0.47f, 0.35f), Palette.NightDeep, 6, 4);
        kit.Sphere(0.045f, MeshKit.At(0, 0.39f, 0.36f), Palette.Accent, 6, 4);
        kit.Pillow(new Vector3(0.16f, 0.1f, 0.24f), MeshKit.At(-0.15f, 0.04f, 0.08f), fur.Lerp(Palette.Shadow, 0.2f), 0.5f, 8, 4);
        kit.Pillow(new Vector3(0.16f, 0.1f, 0.24f), MeshKit.At(0.15f, 0.04f, 0.08f), fur.Lerp(Palette.Shadow, 0.2f), 0.5f, 8, 4);
        kit.Sphere(0.11f, MeshKit.At(0, 0.3f, -0.36f), belly, 8, 5);
        _mat = MeshKit.NewLiveMaterial();
        _mat.SetShaderParameter("wobble_amp", 0.025f);
        _body.AddChild(kit.Instance(_mat));

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
            _mat.SetShaderParameter("swell", 1f);
            _mat.SetShaderParameter("glow", 0.9f);
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
        _mat.SetShaderParameter("flash", _flash * 0.8f);
        if (FreezeT > 0) _mat.SetShaderParameter("tint", new Color(0.7f, 0.85f, 1f));
        else _mat.SetShaderParameter("tint", Colors.White);
    }
}

/// <summary>Gidak: urkek otcul. Bugday verilince yumurtlar; Kabarma'da midilli boyuna gelip Zipzip tekmeler (PLAN 1.4).</summary>
public partial class Gidak : Node3D
{
    public CreatureDef Def;
    Node3D _visual;
    Director _dir;
    Vector3 _wanderTo;
    float _wanderT, _kickT, _layT = -1, _t;
    float _scale = 1f, _targetScale = 1f;
    public bool Swollen;
    readonly List<ShaderMaterial> _liveMats = new();
    public int Fed;

    public Gidak(CreatureDef def, Director dir)
    {
        Def = def; _dir = dir;
        _visual = new Node3D();
        AddChild(_visual);
        const string path = "res://assets/models/gidak/gidak.glb";
        Node3D model = null;
        if (ResourceLoader.Exists(path)) model = GD.Load<PackedScene>(path)?.Instantiate<Node3D>();
        if (model != null)
        {
            _visual.AddChild(model);
            var meshes = model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList();
            Aabb box = new(); bool first = true;
            foreach (var mi in meshes)
            {
                var b = mi.Transform * mi.GetAabb();
                box = first ? b : box.Merge(b); first = false;
            }
            float h = Mathf.Max(0.01f, box.Size.Y);
            float s = 0.75f / h; // Gidak ~75 cm
            model.Scale = Vector3.One * s;
            model.Position = new Vector3(-box.GetCenter().X * s, -box.Position.Y * s, -box.GetCenter().Z * s);
        }
        else
        {
            var kit = new MeshKit();
            kit.Sphere(0.32f, MeshKit.At(new Vector3(0, 0.38f, 0), Vector3.Zero, new Vector3(1f, 1.15f, 1.1f)), Palette.OffWhite, 12, 8);
            kit.Pillow(new Vector3(0.12f, 0.2f, 0.12f), MeshKit.At(0, 0.8f, 0.05f), Palette.Accent, 0.5f);
            kit.Cylinder(0.0f, 0.07f, 0.14f, MeshKit.At(new Vector3(0, 0.55f, 0.33f), new Vector3(Mathf.Pi / 2, 0, 0)), Palette.Warning, 6);
            _visual.AddChild(kit.Instance());
        }
        AddToGroup("gidak");
    }

    public override void _Ready()
    {
        _wanderTo = GlobalPosition;
    }

    public void Feed()
    {
        Fed++;
        Fx.Burst(GlobalPosition + Vector3.Up * 1f, Palette.Accent, 6, 1.5f, 0.1f, 0.8f, true, -2f);
        Fx.Text3D(GlobalPosition + Vector3.Up * 1.2f, "♥", Palette.Accent, 96, 1.2f, 1.2f);
        _layT = 2.5f;
    }

    public void Swell(bool on)
    {
        Swollen = on;
        _targetScale = on ? Def.SwollenScale : 1f;
        if (on) { Sfx.Play("swell", GlobalPosition, 0f); Fx.Burst(GlobalPosition + Vector3.Up, Palette.MayaTeal, 20, 4f, 0.12f, 0.8f, true, 0f); }
        foreach (var mi in _visual.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            for (int i = 0; i < mi.GetSurfaceOverrideMaterialCount(); i++)
            {
                if (mi.Mesh.SurfaceGetMaterial(i) is StandardMaterial3D sm)
                {
                    var m = (StandardMaterial3D)sm.Duplicate();
                    m.EmissionEnabled = on;
                    m.Emission = Palette.MayaTeal;
                    m.EmissionEnergyMultiplier = 0.35f;
                    mi.SetSurfaceOverrideMaterial(i, on ? m : null);
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _scale = Mathf.Lerp(_scale, _targetScale, Mathf.Min(1f, dt * 2.5f));
        float wob = Swollen ? 1f + Mathf.Sin(_t * 8f) * 0.03f : 1f;
        _visual.Scale = new Vector3(_scale * wob, _scale / wob, _scale * wob);

        if (_layT > 0)
        {
            _layT -= dt;
            if (_layT <= 0)
            {
                _dir.State.Eggs++;
                var egg = new MeshKit().Sphere(0.12f, MeshKit.At(new Vector3(0, 0.12f, 0), Vector3.Zero, new Vector3(1, 1.3f, 1)), Palette.OffWhite, 10, 6).Instance();
                GetParent().AddChild(egg);
                egg.GlobalPosition = GlobalPosition - GlobalBasis.Z * 0.35f;
                Fx.Text3D(GlobalPosition + Vector3.Up * 1.1f, "+1 Yumurta", Palette.OffWhite, 56, 1f, 1.4f);
                Sfx.Play("pickup", GlobalPosition, -4f);
            }
        }

        Vector3 goal;
        float speed = Def.Speed;
        if (Swollen)
        {
            // Koc Gidak: en yakin Zipzip'e kosar ve tekmeler.
            var z = Zipzip.All.Where(e => !e.Dead).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(GlobalPosition)).FirstOrDefault();
            goal = z?.GlobalPosition ?? _dir.FarmCenter() + new Vector3(3, 0, 3);
            // Oyuncunun kamerasini kapatmasin: oyuncuya 3 m'den fazla yaklasmaz.
            var toP = GlobalPosition - _dir.Player.GlobalPosition; toP.Y = 0;
            if (toP.Length() < 3.5f) goal = GlobalPosition + toP.Normalized() * 2f;
            speed *= 2.6f;
            _kickT -= dt;
            if (z != null && z.GlobalPosition.DistanceTo(GlobalPosition) < Def.KickRange + z.Radius && _kickT <= 0)
            {
                _kickT = Def.KickInterval;
                var dir = (z.GlobalPosition - GlobalPosition) with { Y = 0 };
                z.Hit(Def.Damage, false, dir + Vector3.Up * 0.3f, 14f, false);
                _dir.OnAnimalHit();
                Fx.Burst(z.Center, Palette.Warning, 10, 5f, 0.1f, 0.4f, true);
                Sfx.Play("hit", GlobalPosition, 2f);
                _visual.Rotation = new Vector3(-0.5f, _visual.Rotation.Y, 0);
            }
        }
        else
        {
            _wanderT -= dt;
            var p = _dir.Player;
            // Bugdayin varsa ve henuz beslemediysen pesinden gelir (PLAN 1.4, 6:00).
            bool lured = Fed == 0 && _layT < 0 && _dir.State.Backpack.Count("wheat") > 0 && p.GlobalPosition.DistanceTo(GlobalPosition) < 7f;
            if (lured) _wanderTo = p.GlobalPosition + (GlobalPosition - p.GlobalPosition).Normalized() * 2.4f;
            else if (_wanderT <= 0)
            {
                _wanderT = (float)GD.RandRange(2.5, 5.0);
                var home = _dir.GidakHome;
                _wanderTo = home + new Vector3((float)GD.RandRange(-5, 5), 0, (float)GD.RandRange(-5, 5));
            }
            goal = _wanderTo;
        }
        var d = goal - GlobalPosition; d.Y = 0;
        if (d.Length() > 0.4f)
        {
            var step = d.Normalized() * Mathf.Min(d.Length(), speed * dt);
            GlobalPosition = _dir.World.Ground(GlobalPosition + step);
            float yaw = Mathf.Atan2(d.X, d.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, yaw, Mathf.Min(1f, dt * 8f)), 0);
            // Paytak yuruyus: kodla salinim
            _visual.Rotation = new Vector3(Mathf.Lerp(_visual.Rotation.X, 0, dt * 6f), 0, Mathf.Sin(_t * 12f) * 0.12f);
            _visual.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_t * 12f)) * 0.05f * _scale, 0);
        }
        else
        {
            _visual.Rotation = new Vector3(Mathf.Lerp(_visual.Rotation.X, Mathf.Sin(_t * 2f) > 0.8f ? 0.5f : 0f, dt * 6f), 0, 0); // gagalama
            _visual.Position = Vector3.Zero;
        }
    }
}
