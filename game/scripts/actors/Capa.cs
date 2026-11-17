using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa: tek el aleti, baslik takilir (GDD 4). Is basliklari: Kazma, Toprak, Sulama, Cekim, Insa.
/// Silah basligi: Tirpan Isini (baslangic setinde, ICERIK §6.2 #1).
/// </summary>
public partial class Capa : Node3D
{
    public enum Head { Kazma, Toprak, Sulama, Cekim, Insa, Tirpan }
    public static readonly string[] HeadNames = { "Kazma", "Toprak", "Sulama", "Çekim", "İnşa", "Tırpan Işını" };
    public static readonly Color[] HeadColors = { Palette.Warning, Palette.Wood.Lerp(Palette.Warning, 0.4f), new("8CC8E8"), Palette.MayaViolet, Palette.CoopBlue.Lerp(Palette.OffWhite, 0.4f), Palette.MayaTeal };

    public Head Current { get; private set; } = Head.Kazma;
    Director _d;
    ToolFile _t;
    MeshInstance3D _beam;
    StandardMaterial3D _beamMat;
    GpuParticles3D _spray;
    Node3D _ghost;
    float _actCd, _fireCd, _waterTick;
    public SwollenCrop Held { get; private set; }
    readonly HashSet<(int, int)> _wateredPlanted = new();
    public bool UsingNow { get; private set; }

    public void Init(Director d) { _d = d; _t = d.Data.Tool; }

    public override void _Ready()
    {
        _beamMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1, 1, 1, 0.75f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            EmissionEnergyMultiplier = 2.5f,
        };
        _beam = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.09f, Height = 1f, RadialSegments = 8, Rings = 1 },
            MaterialOverride = _beamMat,
            TopLevel = true,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_beam);

        var pm = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, 0, -1), Spread = 6f, InitialVelocityMin = 11f, InitialVelocityMax = 14f,
            Gravity = new Vector3(0, -9, 0), ScaleMin = 0.6f, ScaleMax = 1.3f, Color = new Color("BFE6F5"),
        };
        _spray = new GpuParticles3D
        {
            Amount = 80, Lifetime = 0.7f, ProcessMaterial = pm, Emitting = false, LocalCoords = false,
            DrawPass1 = new SphereMesh { Radius = 0.06f, Height = 0.12f, RadialSegments = 6, Rings = 3, Material = new StandardMaterial3D { AlbedoColor = new Color("BFE6F5"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
        };
        AddChild(_spray);
    }

    public void Select(Head h)
    {
        if (Current == h) return;
        if (Current == Head.Cekim && Held != null) Drop();
        Current = h;
        Sfx.Play("place", null, -14f);
        _d.Hud.OnHead((int)h);
    }

    public void Process(float dt, bool fire, bool firePressed)
    {
        var p = _d.Player;
        var muzzle = p.Muzzle.GlobalPosition;
        GlobalPosition = muzzle;
        _spray.GlobalPosition = muzzle;
        _actCd -= dt; _fireCd -= dt; _waterTick -= dt;
        _beam.Visible = false;
        bool spraying = false;
        bool beaming = false;
        UsingNow = fire || Held != null;
        p.Facing = UsingNow || Current == Head.Tirpan && _d.NightActive;

        var aim = p.AimPoint(out var hitNode, 60f);
        UpdateGhost(aim);

        if (p.Down) { _spray.Emitting = false; Sfx.Beam(false); return; }

        switch (Current)
        {
            case Head.Kazma:
                if (fire)
                {
                    var rock = (hitNode as Node)?.GetParent() as Rock ?? NearestRockTo(aim, 1.2f);
                    float dist = muzzle.DistanceTo(aim);
                    if (rock != null && IsInstanceValid(rock) && dist <= _t.MineRange)
                    {
                        beaming = true;
                        ShowBeam(muzzle, aim, HeadColors[0], 0.6f + Mathf.Sin(Time.GetTicksMsec() * 0.05f) * 0.3f);
                        MineRock(rock, dt, aim);
                    }
                    else if (dist <= _t.MineRange) { ShowBeam(muzzle, aim, HeadColors[0], 0.25f); beaming = true; }
                }
                break;
            case Head.Toprak:
                if (fire && _actCd <= 0) { _actCd = 0.16f; TillOrPlant(aim); }
                if (fire && muzzle.DistanceTo(aim) <= _t.TillRange) { ShowBeam(muzzle, aim, HeadColors[1], 0.35f); beaming = true; }
                break;
            case Head.Sulama:
                if (fire)
                {
                    spraying = true;
                    var dir = (aim - muzzle).Normalized();
                    _spray.GlobalBasis = Basis.LookingAt(dir, Vector3.Up);
                    if (_waterTick <= 0) { _waterTick = 0.08f; WaterAt(aim); }
                }
                break;
            case Head.Cekim:
                if (firePressed)
                {
                    if (Held != null) Drop();
                    else TryGrab(aim);
                }
                if (Held != null)
                {
                    var holdAt = muzzle + p.AimDir * (2.2f + Held.Radius) + Vector3.Up * (0.3f + Held.Radius * 0.4f);
                    var floor = _d.World.HeightAt(holdAt.X, holdAt.Z) + Held.Radius;
                    if (holdAt.Y < floor) holdAt.Y = floor;
                    Held.GlobalPosition = Held.GlobalPosition.Lerp(holdAt, Mathf.Min(1f, dt * 8f));
                    ShowBeam(muzzle, Held.GlobalPosition, HeadColors[3], 0.5f);
                    beaming = true;
                    _d.Hud.Hint(_d.Pod.IsRamp && OverPad(Held.GlobalPosition) ? "Bırak: kargoya yükle (sol tık)" : "Kargo Rampası'na taşı");
                }
                break;
            case Head.Insa:
                if (firePressed && _ghost != null && _ghost.Visible && _d.Ocak == null) PlaceOcak(_ghost.GlobalPosition);
                break;
            case Head.Tirpan:
                float interval = _d.Data.Tool.Weapon.FireInterval / (_d.Night?.Stats.FireRateMult ?? 1f);
                if (fire && _fireCd <= 0) { _fireCd = interval; FireTirpan(aim); }
                break;
        }
        _spray.Emitting = spraying;
        Sfx.Beam(beaming && Current is Head.Kazma or Head.Cekim);
    }

    void ShowBeam(Vector3 from, Vector3 to, Color color, float intensity)
    {
        var d = to - from;
        float len = d.Length();
        if (len < 0.05f) return;
        _beam.Visible = true;
        _beam.GlobalPosition = from + d * 0.5f;
        var up = d / len;
        var side = Mathf.Abs(up.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up.Cross(up).Normalized();
        var fwd = side.Cross(up);
        float w = 0.7f + intensity * 0.8f + Mathf.Sin(Time.GetTicksMsec() * 0.04f) * 0.15f;
        _beam.GlobalBasis = new Basis(side * w, up * len, fwd * w);
        _beamMat.AlbedoColor = new Color(color, 0.55f + intensity * 0.3f);
        _beamMat.Emission = color;
        _beamMat.EmissionEnergyMultiplier = 1.5f + intensity * 2f;
    }

    // ---------- Kazma ----------

    Rock NearestRockTo(Vector3 point, float within) =>
        _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Center.DistanceTo(point) < r.Radius + within)
            .OrderBy(r => r.Center.DistanceTo(point)).FirstOrDefault();

    /// <summary>Kazma isini kayayi kemirir; parcalar cantaya ucar (bot da bunu cagirir).</summary>
    public void MineRock(Rock rock, float dt, Vector3 hitPoint)
    {
        int got = rock.Mine(_t.MineRate * dt);
        if (Time.GetTicksMsec() % 6 == 0) Fx.Burst(hitPoint, Palette.Rock, 3, 3f, 0.06f, 0.3f);
        for (int i = 0; i < got; i++)
        {
            Fx.FlyTo(hitPoint, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Rock, 0.14f, () =>
            {
                _d.State.Backpack.Add("stone", 1);
                Sfx.Play("pickup", null, -6f);
                _d.State.Scenario.Report("mine");
            });
        }
        if (rock.Stones <= 0) _d.World.RemoveRock(rock);
    }

    // ---------- Toprak ----------

    public bool TillOrPlant(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.TillRange) return false;
        if (_d.Ocak == null) { _d.Hud.Hint("Önce Ocak'ı kur: tapu dışında tarla açılmaz"); return false; }
        if (aim.DistanceTo(_d.OcakPos) > Ocak.ClaimRadius) { _d.Hud.Hint("Tapunun dışı"); return false; }
        if (aim.DistanceTo(_d.OcakPos) < 1.6f || aim.DistanceTo(_d.Pod.GlobalPosition) < 2.2f) return false;
        var (x, y) = FarmView.Cell(aim);
        var farm = _d.State.Farm;
        var plot = farm.Get(x, y);
        if (plot == null)
        {
            if (!farm.Till(x, y)) return false;
            Sfx.Play("till", _d.FarmView.CellCenter(x, y), -2f);
            _d.State.Scenario.SetProgress("till", farm.Plots.Count());
            return true;
        }
        if (plot.Crop == null)
        {
            if (!farm.Plant(x, y, "turnip", _d.State.Backpack)) { _d.Hud.Hint("Tohum kalmadı"); return false; }
            Sfx.Play("place", _d.FarmView.CellCenter(x, y), -10f);
            _d.State.Scenario.SetProgress("plant", farm.Plots.Count(q => q.Crop != null));
            return true;
        }
        return false;
    }

    // ---------- Sulama ----------

    public void WaterAt(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.WaterRange) return;
        var farm = _d.State.Farm;
        bool any = false;
        foreach (var plot in farm.Plots)
        {
            var c = _d.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > 1.1f) continue;
            if (plot.Moisture > 92f) continue;
            farm.Water(plot.X, plot.Y);
            any = true;
            if (plot.Planted && _wateredPlanted.Add((plot.X, plot.Y)))
                _d.State.Scenario.SetProgress("water", _wateredPlanted.Count);
        }
        if (any && GD.Randf() < 0.35f) Sfx.Play("water", aim, -8f);
    }

    // ---------- Cekim ----------

    public bool TryGrab(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.CarryRange + 2f) return false;
        // Yerde duran kabarik urun
        var loose = GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>()
            .Where(s => !s.Sold && s.GlobalPosition.DistanceTo(aim) < s.Radius + 1.2f)
            .OrderBy(s => s.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
        if (loose != null) { Grab(loose); return true; }
        // Tarladaki kabarik urun
        var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted)
            .OrderBy(v => v.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
        if (view != null && view.GlobalPosition.DistanceTo(aim) < 1.2f + view.CropScale * 0.4f)
        {
            var sc = view.Lift();
            if (sc != null) { Grab(sc); Fx.Burst(view.GlobalPosition, Palette.Soil, 18, 4f, 0.1f, 0.6f); return true; }
        }
        return false;
    }

    void Grab(SwollenCrop s)
    {
        Held = s;
        s.Held = true;
        Sfx.Play("place", s.GlobalPosition, -6f);
    }

    public bool OverPad(Vector3 p)
    {
        var pad = _d.Pod.PadCenter;
        return new Vector2(p.X - pad.X, p.Z - pad.Z).Length() < 2.4f;
    }

    public void Drop()
    {
        if (Held == null) return;
        var s = Held;
        Held = null;
        s.Held = false;
        if (_d.Pod.IsRamp && OverPad(s.GlobalPosition)) { _d.SellCrop(s); return; }
        var g = _d.World.Ground(s.GlobalPosition);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", g + Vector3.Up * s.Radius * 0.6f, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        Sfx.Play("place", g, -4f);
    }

    // ---------- Insa ----------

    void UpdateGhost(Vector3 aim)
    {
        bool show = Current == Head.Insa && _d.Ocak == null;
        if (!show) { if (_ghost != null) _ghost.Visible = false; return; }
        if (_ghost == null)
        {
            _ghost = new Ocak();
            foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
                mi.Transparency = 0.55f;
            foreach (var c in _ghost.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>()) c.Disabled = true;
            foreach (var l in _ghost.FindChildren("*", "OmniLight3D", true, false).OfType<OmniLight3D>()) l.Visible = false;
            _ghost.TopLevel = true;
            AddChild(_ghost);
        }
        var p = _d.Player.GlobalPosition;
        var flat = aim - p; flat.Y = 0;
        if (flat.Length() > _t.BuildRange) flat = flat.Normalized() * _t.BuildRange;
        if (flat.Length() < 2f) flat = flat.Normalized() * 2f;
        _ghost.GlobalPosition = _d.World.Ground(p + flat);
        _ghost.Visible = true;
    }

    public void PlaceOcak(Vector3 at)
    {
        if (_d.Ocak != null) return;
        if (_ghost != null) _ghost.Visible = false;
        _d.PlaceOcak(at);
    }

    // ---------- Tirpan Isini ----------

    public void FireTirpan(Vector3 aim)
    {
        var w = _d.Data.Tool.Weapon;
        var stats = _d.Night?.Stats ?? new CombatStats();
        var muzzle = _d.Player.Muzzle.GlobalPosition;
        var dir = (aim - muzzle); dir.Y = Mathf.Clamp(dir.Y, -0.3f * dir.Length(), 0.3f * dir.Length());
        dir = dir.Normalized();
        int n = stats.Projectiles;
        float spread = Mathf.DegToRad(w.SpreadDegrees);
        for (int i = 0; i < n; i++)
        {
            float off = n == 1 ? 0 : Mathf.Lerp(-spread * (n - 1) * 0.5f, spread * (n - 1) * 0.5f, i / (float)(n - 1));
            var d = dir.Rotated(Vector3.Up, off);
            var proj = new Crescent(_d, d, w, stats);
            _d.AddChild(proj);
            proj.GlobalPosition = muzzle;
        }
        Sfx.Play("nailgun", muzzle, -5f);
        _d.Player.Shake(0.12f);
    }
}

/// <summary>Tirpan Isini mermisi: hilal bicimli enerji suprumesi. Ayni dusmana bir kez vurur; Seken karti ile seker.</summary>
public partial class Crescent : Node3D
{
    readonly Director _d;
    Vector3 _dir;
    readonly WeaponDef _w;
    readonly CombatStats _s;
    float _traveled;
    int _bounces;
    readonly HashSet<Zipzip> _hit = new();
    readonly MeshInstance3D _mesh;
    float _width;

    public Crescent(Director d, Vector3 dir, WeaponDef w, CombatStats s)
    {
        _d = d; _dir = dir; _w = w; _s = s;
        _bounces = s.Bounces;
        _width = w.Width * s.SizeMult;
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(Palette.MayaTeal, 0.85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = 3f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _mesh = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = _width * 0.42f, OuterRadius = _width * 0.5f, Rings = 16, RingSegments = 6 },
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = new Vector3(1f, 0.6f, 0.45f),
        };
        AddChild(_mesh);
    }

    public override void _Ready() => LookDir();

    void LookDir()
    {
        if (_dir.LengthSquared() > 0.001f) GlobalBasis = Basis.LookingAt(_dir, Vector3.Up);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float step = _w.Speed * dt;
        GlobalPosition += _dir * step;
        _traveled += step;
        _mesh.RotateObjectLocal(Vector3.Forward, dt * 18f);
        float life = _traveled / (_w.Range * _s.SizeMult);
        _mesh.Transparency = Mathf.Clamp((life - 0.7f) / 0.3f, 0, 1);

        // Zemine gomulmesin
        float gy = _d.World.HeightAt(GlobalPosition.X, GlobalPosition.Z) + 0.35f;
        if (GlobalPosition.Y < gy) GlobalPosition = GlobalPosition with { Y = gy };

        foreach (var z in Zipzip.All.ToArray())
        {
            if (z.Dead || _hit.Contains(z)) continue;
            if (z.Center.DistanceTo(GlobalPosition) > z.Radius + _width * 0.5f) continue;
            _hit.Add(z);
            bool weak = z.HasSac && z.SacPos.DistanceTo(GlobalPosition) < z.Radius * 0.9f;
            var night = _d.Night;
            float dmg; bool crit;
            if (night != null) (dmg, crit) = night.RollHit(_w, weak, z.Def.WeakPointMult);
            else { dmg = _w.Damage; crit = false; }
            z.Hit(dmg, crit, _dir with { Y = 0 }, _w.Knockback * _s.KnockbackMult, weak);
            if (night != null && night.RollBurn()) z.BurnT = _w.BurnSeconds;
            if (night != null && night.RollFreeze()) z.FreezeT = _w.FreezeSeconds;
            Sfx.Play("hit", z.Center, -3f);
            Fx.Burst(z.Center, crit ? Palette.Warning : Palette.MayaTeal, crit ? 14 : 8, 5f, 0.08f, 0.35f, true, 0f);
            _d.HitStop(crit ? 0.07f : 0.04f);
            if (_bounces > 0)
            {
                _bounces--;
                var next = Zipzip.All.Where(e => !e.Dead && !_hit.Contains(e)).OrderBy(e => e.Center.DistanceTo(GlobalPosition)).FirstOrDefault();
                if (next != null && next.Center.DistanceTo(GlobalPosition) < 12f)
                {
                    _dir = (next.Center - GlobalPosition).Normalized();
                    _traveled = Mathf.Max(0, _traveled - 4f);
                    LookDir();
                }
            }
        }
        if (_traveled > _w.Range * _s.SizeMult) QueueFree();
    }
}
