using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Capa, silah basliklari: Tirpan Isini ve Civi Tabancasi (ICERIK §6.2 #1, #2) ve mermileri.</summary>
public partial class Capa
{
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
