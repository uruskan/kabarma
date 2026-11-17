using System;
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
    // GDD 4: alti is basligi + silah yuvalari (Mk.I: 2). 1-6 is, 7-8 silah.
    public enum Head { Kazma, Bicme, Toprak, Sulama, Cekim, Insa, Tirpan, Civi }
    public static readonly string[] HeadNames = { "Kazma", "Biçme", "Toprak", "Sulama", "Çekim", "İnşa", "Tırpan Işını", "Çivi Tab." };
    public static readonly Color[] HeadColors = { Palette.Warning, Palette.Plant.Lerp(Palette.Warning, 0.3f), Palette.Wood.Lerp(Palette.Warning, 0.4f), new("8CC8E8"), Palette.MayaViolet, Palette.CoopBlue.Lerp(Palette.OffWhite, 0.4f), Palette.MayaTeal, Palette.OffWhite };
    public static bool IsWeapon(Head h) => h is Head.Tirpan or Head.Civi;

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
        p.Carrying = Held != null;
        p.Facing = UsingNow || IsWeapon(Current) && _d.NightActive;

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
            case Head.Bicme:
                if (fire && _actCd <= 0) { _actCd = 0.35f; Mow(aim); }
                if (fire && muzzle.DistanceTo(aim) <= _t.MowRange) { ShowBeam(muzzle, aim, HeadColors[1], 0.5f); beaming = true; }
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
                    var padP = _d.Pod.PadCenter;
                    bool atRamp = _d.Pod.IsRamp && (OverPad(Held.GlobalPosition) || new Vector2(p.GlobalPosition.X - padP.X, p.GlobalPosition.Z - padP.Z).Length() < 4.5f);
                    _d.Hud.Hint(atRamp ? "Bırak: kargoya yükle (sol tık)" : "Kargo Rampası'na taşı");
                }
                break;
            case Head.Insa:
                if (_d.Build != null && _d.Build.Handles(CurrentPlaceable)) { _d.Build.Tick(p, aim, firePressed); break; }
                if (firePressed && _ghost != null && _ghost.Visible) TryPlace();
                break;
            case Head.Tirpan:
                float interval = _d.Data.Tool.Weapon.FireInterval / (_d.Night?.Stats.FireRateMult ?? 1f);
                if (fire && _fireCd <= 0) { _fireCd = interval; FireTirpan(aim); }
                break;
            case Head.Civi:
                float ni = _d.Data.Tool.Nailgun.FireInterval / (_d.Night?.Stats.FireRateMult ?? 1f);
                if ((firePressed || fire) && _fireCd <= 0) { _fireCd = ni; FireNail(aim); }
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

    // ---------- Bicme ----------

    /// <summary>Bicme: nisan noktasi cevresindeki olgun ekinleri biçer; urun ve tohum cantaya ucar (GDD 8.2).</summary>
    public int Mow(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.MowRange) return 0;
        int n = 0;
        foreach (var plot in _d.State.Farm.Plots.ToList())
        {
            var c = _d.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > _t.MowRadius) continue;
            var h = _d.State.Farm.Harvest(plot.X, plot.Y, _d.State.Rng);
            if (h == null) continue;
            n++;
            var (yield, stars, seeds) = h.Value;
            Fx.Burst(c + Vector3.Up * 0.4f, Palette.Plant, 18, 4f, 0.08f, 0.5f);
            Fx.Text3D(c + Vector3.Up * 1.0f, $"+{yield} Şalgam {new string('★', stars)}", Palette.Warning, 48, 1.2f, 1.3f);
            for (int i = 0; i < Math.Min(yield, 4); i++)
                Fx.FlyTo(c + Vector3.Up * 0.3f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.TurnipBody, 0.12f);
            _d.State.Backpack.Add("turnip", yield);
            _d.State.Backpack.Add("turnip_seed", seeds);
            Sfx.Play("till", c, 0f);
        }
        if (n > 0) _d.OnHarvest(n);
        // Agac biçilirse odun verir (GDD 8: Biçme agac, cali ve ekin biçer). Agac yerinde kalir.
        if (n == 0)
        {
            var tree = _d.World.TreePositions.Where(t => new Vector2(t.X - aim.X, t.Z - aim.Z).Length() < 1.8f).OrderBy(t => t.DistanceTo(aim)).Cast<Vector3?>().FirstOrDefault();
            if (tree is Vector3 tp)
            {
                int w = _d.Data.Tool.WoodPerChop;
                _d.State.Backpack.Add("wood", w);
                Fx.Burst(aim, Palette.Plant, 14, 4f, 0.07f, 0.6f);
                Fx.Burst(aim, Palette.Wood, 6, 3f, 0.06f, 0.4f);
                Fx.FlyTo(aim, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Wood, 0.12f);
                Sfx.Play("hit", aim, -4f);
                _d.State.Scenario.SetProgress("odun", _d.State.Backpack.Count("wood"));
            }
        }
        return n;
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
        // Rampanin ustunde ya da oyuncu rampanin dibindeyken birakilan yuk kargoya gider.
        var pad = _d.Pod.PadCenter;
        bool nearPad = new Vector2(_d.Player.GlobalPosition.X - pad.X, _d.Player.GlobalPosition.Z - pad.Z).Length() < 4.5f;
        if (_d.Pod.IsRamp && (OverPad(s.GlobalPosition) || nearPad)) { _d.SellCrop(s); return; }
        var g = _d.World.Ground(s.GlobalPosition);
        var tw = s.CreateTween();
        tw.TweenProperty(s, "global_position", g + Vector3.Up * s.Radius * 0.6f, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        Sfx.Play("place", g, -4f);
    }

    // ---------- Insa ----------

    // ---------- Insa: yerlestirilebilirler ----------

    int _placeIndex;
    StandardMaterial3D _okOverlay, _badOverlay;
    string _ghostId;
    float _ghostYaw;
    bool _ghostOk;

    /// <summary>Su an secilebilen yerlestirilebilirler: Ocak kurulmadan once yalnizca Ocak (tapu yoksa yapi yok).</summary>
    public System.Collections.Generic.List<string> Placeables
    {
        get
        {
            if (_d.Ocak == null) return new() { "ocak" };
            var l = _d.Data.Tool.Placeables.Where(p => !p.Once).Select(p => p.Id).ToList();
            if (_d.Build != null) l.AddRange(_d.Build.PieceIds);
            return l;
        }
    }

    public string CurrentPlaceable { get { var l = Placeables; return l[Mathf.PosMod(_placeIndex, l.Count)]; } }

    public void CyclePlaceable(int dir) { _placeIndex += dir; Sfx.Play("place", null, -16f); if (_d.Build != null) _d.Build.Select(CurrentPlaceable); }
    public void RotateGhost() { _ghostYaw += Mathf.Pi / 12f; if (_d.Build != null && _d.Build.Handles(CurrentPlaceable)) _d.Build.Rotate(); }

    PlaceableDef DefOf(string id) => _d.Data.Tool.Placeables.FirstOrDefault(p => p.Id == id);

    string CostText(PlaceableDef def) => def.Cost.Count == 0 ? "bedava" : string.Join(", ", def.Cost.Select(kv => $"{kv.Value} {ItemNames.Of(kv.Key)}"));

    void TryPlace()
    {
        var def = DefOf(_ghostId);
        if (def == null) return;
        if (!_ghostOk) { _d.Hud.Hint("Buraya konmaz", 1f); Sfx.Play("hit", null, -10f); return; }
        if (def.Cost.Any(kv => _d.State.Backpack.Count(kv.Key) < kv.Value)) { _d.Hud.Hint($"Yetmiyor: {CostText(def)}", 1.5f); Sfx.Play("hit", null, -10f); return; }
        foreach (var kv in def.Cost) _d.State.Backpack.Remove(kv.Key, kv.Value);
        var at = _ghost.GlobalPosition; float yaw = _ghostYaw;
        _ghost.QueueFree(); _ghost = null; _ghostId = null;
        if (def.Id == "ocak") _d.PlaceOcak(at);
        else _d.PlaceStation(def, at, yaw);
    }

    void UpdateGhost(Vector3 aim)
    {
        bool show = Current == Head.Insa && !(_d.Build != null && _d.Build.Handles(CurrentPlaceable));
        if (_d.Build != null && (Current != Head.Insa || !_d.Build.Handles(CurrentPlaceable))) _d.Build.Hide();
        if (!show) { if (_ghost != null) _ghost.Visible = false; return; }
        var id = CurrentPlaceable;
        if (_ghost != null && _ghostId != id) { _ghost.QueueFree(); _ghost = null; }
        if (_ghost == null)
        {
            _ghostId = id;
            _ghost = Station.MakeVisual(DefOf(id));
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
        var at = _d.World.Ground(p + flat);
        _ghost.GlobalPosition = at;
        _ghost.Rotation = new Vector3(0, _ghostYaw, 0);
        _ghost.Visible = true;
        // Gecerlilik: tapu icinde, tarla karelerinin, istasyonlarin ve kapsulun uzerinde degil.
        _ghostOk = id == "ocak" || (_d.Ocak != null && at.DistanceTo(_d.OcakPos) <= Ocak.ClaimRadius && at.DistanceTo(_d.OcakPos) > 2.2f);
        if (_ghostOk && id != "ocak")
        {
            if (_d.Stations.Any(st => st.GlobalPosition.DistanceTo(at) < 2.2f)) _ghostOk = false;
            if (_d.State.Farm.Plots.Any(pl => _d.FarmView.CellCenter(pl.X, pl.Y).DistanceTo(at) < 1.6f)) _ghostOk = false;
            if (at.DistanceTo(_d.Pod.GlobalPosition) < 3.5f) _ghostOk = false;
        }
        var def = DefOf(id);
        _okOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.4f, 1f, 0.55f, 0.35f) };
        _badOverlay ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(1f, 0.3f, 0.25f, 0.4f) };
        foreach (var mi in _ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()) { mi.Transparency = 0.4f; mi.MaterialOverlay = _ghostOk ? _okOverlay : _badOverlay; mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
        _d.Hud.Hint($"İnşa: {def?.Name} ({(def != null ? CostText(def) : "")})  ·  tekerlek: değiştir  ·  R: döndür", 0.2f);
    }

    public void PlaceOcak(Vector3 at)
    {
        if (_d.Ocak != null) return;
        if (_ghost != null) _ghost.Visible = false;
        _d.PlaceOcak(at);
    }

    // ---------- Tirpan Isini ----------

    /// <summary>Civi Tabancasi: tek hedefli, isabetli (ICERIK §6.2 #2). Gunduz cephane harcar; Kabarma gecesi sinirsiz (GDD 4).</summary>
    public void FireNail(Vector3 aim)
    {
        var w = _d.Data.Tool.Nailgun;
        bool free = _d.NightActive;
        if (!free && !string.IsNullOrEmpty(w.Ammo) && !_d.State.Backpack.Remove(w.Ammo, w.AmmoPerShot))
        {
            _d.Hud.Hint("Çivi yok: Tezgâhta taştan çivi yap", 1.2f);
            Sfx.Play("place", null, -12f);
            return;
        }
        var stats = _d.Night?.Stats ?? new CombatStats();
        var muzzle = _d.Player.Muzzle.GlobalPosition;
        var dir = (aim - muzzle).Normalized();
        int n = stats.Projectiles;
        float spread = Mathf.DegToRad(w.SpreadDegrees);
        for (int i = 0; i < n; i++)
        {
            float off = n == 1 ? 0 : Mathf.Lerp(-spread * (n - 1), spread * (n - 1), i / (float)(n - 1));
            var nail = new Nail(_d, dir.Rotated(Vector3.Up, off), w, stats);
            _d.AddChild(nail);
            nail.GlobalPosition = muzzle;
        }
        Sfx.Play("nailgun", muzzle, -2f);
        Fx.Burst(muzzle, Palette.Warning, 5, 3f, 0.04f, 0.15f, true, 0f);
        _d.Player.Recoil(0.7f * Feel.Recoil);
        _d.Player.Shake(0.08f * Feel.Shake);
        _d.MarkReaction();
    }

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
        _d.Player.Shake(0.12f * Feel.Shake);
        _d.Player.Recoil(0.45f * Feel.Recoil);
        _d.MarkReaction();
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
            _d.HitStop((crit ? 0.07f : 0.04f) * Feel.HitStop);
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

/// <summary>Civi: ince, hizli, tek hedef. Seken karti ile bir sonrakine seker.</summary>
public partial class Nail : Node3D
{
    readonly Director _d;
    Vector3 _dir;
    readonly WeaponDef _w;
    readonly CombatStats _s;
    float _traveled;
    int _bounces;
    Zipzip _last;

    public Nail(Director d, Vector3 dir, WeaponDef w, CombatStats s)
    {
        _d = d; _dir = dir; _w = w; _s = s; _bounces = s.Bounces;
        var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.OffWhite, EmissionEnabled = true, Emission = Palette.Warning, EmissionEnergyMultiplier = 1.5f };
        AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.035f * s.SizeMult, Height = 0.5f, RadialSegments = 6, Rings = 1 }, MaterialOverride = mat, RotationDegrees = new Vector3(90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public override void _Ready() { if (_dir.LengthSquared() > 0.001f) GlobalBasis = Basis.LookingAt(_dir, Vector3.Up); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float step = _w.Speed * dt;
        var from = GlobalPosition;
        GlobalPosition += _dir * step;
        _traveled += step;
        // Sureklilik: bu kare boyunca gecilen dogru parcasina degen dusman
        foreach (var z in Zipzip.All.ToArray())
        {
            if (z.Dead || z == _last) continue;
            var c = z.Center;
            var seg = GlobalPosition - from;
            float t = Mathf.Clamp((c - from).Dot(seg) / Mathf.Max(0.0001f, seg.LengthSquared()), 0, 1);
            var hp = from + seg * t;
            if (hp.DistanceTo(c) > z.Radius + 0.1f * _s.SizeMult) continue;
            bool weak = z.HasSac && z.SacPos.DistanceTo(hp) < z.Radius * 0.7f;
            var night = _d.Night;
            float dmg; bool crit;
            if (night != null) (dmg, crit) = night.RollHit(_w, weak, z.Def.WeakPointMult);
            else { dmg = _w.Damage; crit = false; }
            z.Hit(dmg, crit, _dir with { Y = 0 }, _w.Knockback * _s.KnockbackMult, weak);
            if (night != null && night.RollBurn()) z.BurnT = _w.BurnSeconds;
            if (night != null && night.RollFreeze()) z.FreezeT = _w.FreezeSeconds;
            Sfx.Play("hit", c, -1f);
            Fx.Burst(c, crit ? Palette.Warning : Palette.OffWhite, crit ? 12 : 6, 4f, 0.06f, 0.3f, true, 0f);
            _d.HitStop((crit ? 0.06f : 0.03f) * Feel.HitStop);
            _last = z;
            if (_bounces-- > 0)
            {
                var next = Zipzip.All.Where(e => !e.Dead && e != z).OrderBy(e => e.Center.DistanceTo(c)).FirstOrDefault();
                if (next != null && next.Center.DistanceTo(c) < 14f)
                {
                    _dir = (next.Center - c).Normalized();
                    GlobalPosition = c;
                    GlobalBasis = Basis.LookingAt(_dir, Vector3.Up);
                    _traveled = Mathf.Max(0, _traveled - 6f);
                    return;
                }
            }
            QueueFree();
            return;
        }
        if (GlobalPosition.Y < _d.World.HeightAt(GlobalPosition.X, GlobalPosition.Z)) { Fx.Burst(GlobalPosition, Palette.Soil, 4, 2f, 0.05f, 0.3f); QueueFree(); return; }
        if (_traveled > _w.Range * _s.SizeMult) QueueFree();
    }
}
