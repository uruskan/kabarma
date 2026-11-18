using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa, silah basliklari: Tirpan Isini ve Civi Tabancasi (ICERIK 6.2 #1, #2) ve mermileri. Sayilar weapons.json'da,
/// vurus hissi feel.json'da (sources.tirpan, sources.civi; Capa.Feel.cs).
/// </summary>
public partial class Capa
{
    /// <summary>Tetigin urettigi atis sayisi (sinama: yari ve tam otomatik).</summary>
    public int ShotsFired { get; private set; }

    /// <summary>
    /// Tetik: tam otomatikte basili tutmak ateslemeye devam eder; yari otomatikte her basis bir atis (ICERIK 6.2 #2),
    /// bekleme bitmeden gelen basis inputBufferSeconds icinde saklanir. Beklemede gelen basis gecikme ornegi sayilmaz.
    /// </summary>
    void TickWeapon(WeaponDef w, bool fire, bool firePressed, float dt, Action shoot)
    {
        bool pressed = firePressed || _shotBuffer > 0f;
        bool trigger = w.SemiAuto ? pressed : fire;
        if (trigger && _fireCd <= 0f)
        {
            _fireCd = w.FireInterval / ((_d.Night?.Stats.FireRateMult ?? 1f) * ChipFireRate(w.Id)); // Capa.Chips.cs
            _shotBuffer = 0f;
            shoot();
            ShotsFired++;
            _reacted = true;
        }
        else if (firePressed)
        {
            if (w.SemiAuto) _shotBuffer = w.InputBufferSeconds;
            _d.FeelPanel?.CancelInput();
        }
        else _shotBuffer = Mathf.Max(0f, _shotBuffer - dt);
    }

    /// <summary>Civi Tabancasi: tek hedefli, isabetli (ICERIK 6.2 #2). Gunduz cephane harcar; Kabarma gecesi sinirsiz (GDD 4).</summary>
    public void FireNail(Vector3 aim)
    {
        var w = _d.Data.Tool.Nailgun;
        var muzzle = _d.Player.Muzzle.GlobalPosition;
        bool free = _d.NightActive;
        if (!free && !string.IsNullOrEmpty(w.Ammo) && !_d.State.Backpack.Remove(w.Ammo, w.AmmoPerShot))
        {
            _d.Hud.Hint("Çivi yok: Tezgâhta bakırdan çivi yap", 1.2f);
            FeelEmpty("civi", muzzle);
            return;
        }
        var stats = StatsFor(w.Id); // kart + cip (Capa.Chips.cs)
        var dir = (aim - muzzle).Normalized();
        int n = stats.Projectiles;
        float spread = Mathf.DegToRad(w.SpreadDegrees);
        var look = NailLook(w, stats, F.Source("civi").Trail > 0);
        for (int i = 0; i < n; i++)
        {
            float off = n == 1 ? 0 : Mathf.Lerp(-spread * (n - 1), spread * (n - 1), i / (float)(n - 1));
            var nail = new Nail(_d, dir.Rotated(Vector3.Up, off), w, stats, look);
            _d.AddChild(nail);
            nail.GlobalPosition = muzzle;
        }
        FeelFire("civi", muzzle, Palette.Warning);
        _d.OnCardShot(aim); // kartlar: Patlar Uc (Director.Cards.cs)
    }

    public void FireTirpan(Vector3 aim)
    {
        var w = _d.Data.Tool.Weapon;
        var stats = StatsFor(w.Id); // kart + cip (Capa.Chips.cs)
        var muzzle = _d.Player.Muzzle.GlobalPosition;
        var dir = (aim - muzzle);
        if (w.MaxPitch > 0) dir.Y = Mathf.Clamp(dir.Y, -w.MaxPitch * dir.Length(), w.MaxPitch * dir.Length());
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
        FeelFire("tirpan", muzzle, Palette.MayaTeal);
        _d.OnCardShot(aim); // kartlar: Patlar Uc (Director.Cards.cs)
    }

    /// <summary>Civinin gorunusu (malzeme, govde, iz parcaciginin malzemesi ve cizimi): her atista yeniden kurmak (ozellikle
    /// iz parcaciginin renk rampasi dokusu) tepki karesini bir kare geciktiriyordu (pencereli gecikme olcumu: ~34 ms -> ~20 ms).
    /// Kaynaklar paylasilir; dugumler (MeshInstance3D, GpuParticles3D) her civide yeni.</summary>
    NailLook _nailLook;

    NailLook NailLook(WeaponDef w, CombatStats s, bool trail)
    {
        if (_nailLook != null && _nailLook.Size == s.SizeMult && _nailLook.Glow == w.Glow && (_nailLook.TrailMesh != null) == trail) return _nailLook;
        var look = new NailLook { Size = s.SizeMult, Glow = w.Glow };
        look.Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.OffWhite, EmissionEnabled = true, Emission = Palette.Warning, EmissionEnergyMultiplier = w.Glow };
        look.Body = new CapsuleMesh { Radius = 0.035f * s.SizeMult, Height = 0.5f, RadialSegments = 6, Rings = 1 };
        if (trail)
        {
            var t = Fx.MakeTrail(new Color(Palette.Warning, 0.7f), Palette.OffWhite);
            ((SphereMesh)t.DrawPass1).Radius = 0.03f;
            ((SphereMesh)t.DrawPass1).Height = 0.06f;
            look.TrailProcess = t.ProcessMaterial;
            look.TrailMesh = t.DrawPass1;
            t.Free(); // agaca girmedi: yalnizca kaynaklari kullanilir
        }
        return _nailLook = look;
    }

    /// <summary>Mermi bir Zipzip'e degdi: hasar, durumlar ve isabet hissi (iki silahta ortak).</summary>
    internal void WeaponHit(string feel, WeaponDef w, CombatStats s, Zipzip z, Vector3 at, Vector3 dir, bool weak, Color color)
    {
        var night = _d.Night;
        float dmg; bool crit;
        if (night != null) (dmg, crit) = night.RollHit(w, weak, z.Def.WeakPointMult);
        else { dmg = w.Damage; crit = false; }
        dmg = ChipCombat.AdjustHit(_d.State.Chips, w.Id, dmg, ref crit, w.CritMult, _d.State.ChipRng); // Capa.Chips.cs: cip hasari ve kritigi
        if (_d.Net?.ForwardHit(z, dmg, crit, dir with { Y = 0 }, w.Knockback * s.KnockbackMult, weak, night != null && night.RollBurn() ? w.BurnSeconds : 0, night != null && night.RollFreeze() ? w.FreezeSeconds : 0) == true) { FeelHit(feel, at, crit ? Palette.Warning : color, crit, weak); return; } // ortak: istemcinin vurusu host'ta
        z.Hit(dmg, crit, dir with { Y = 0 }, w.Knockback * s.KnockbackMult, weak);
        _d.OnCardHit(z); // kartlar: can emme, Sok, Zehir (Director.Cards.cs)
        if (night != null && night.RollBurn()) z.BurnT = w.BurnSeconds;
        if (night != null && night.RollFreeze()) z.FreezeT = w.FreezeSeconds;
        if (ChipCombat.RollBurn(_d.State.Chips, w.Id, _d.State.ChipRng)) z.BurnT = w.BurnSeconds;     // cip: Yakici
        if (ChipCombat.RollFreeze(_d.State.Chips, w.Id, _d.State.ChipRng)) z.FreezeT = w.FreezeSeconds; // cip: Ayaz
        FeelHit(feel, at, crit ? Palette.Warning : color, crit, weak);
    }
}

/// <summary>Tirpan Isini mermisi: hilal bicimli enerji supurmesi. Ayni dusmana bir kez vurur; Seken karti ile seker.</summary>
public partial class Crescent : Node3D
{
    readonly Director _d;
    Vector3 _dir;
    readonly WeaponDef _w;
    readonly CombatStats _s;
    float _traveled;
    int _bounces;
    readonly HashSet<Zipzip> _hit = new();
    readonly HashSet<Node3D> _hitWild = new(); // yaratik2
    readonly HashSet<Node3D> _hitGhost = new(); // gucler: deneme ve Tatbikat hayaletleri
    readonly HashSet<IAtaTarget> _hitAta = new(); // boss akisi: Ata hedefleri (boss/Capa.Ata.cs)
    readonly MeshInstance3D _mesh;
    readonly float _width;

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
            EmissionEnabled = true, Emission = Palette.MayaTeal, EmissionEnergyMultiplier = w.Glow,
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
        _mesh.RotateObjectLocal(Vector3.Forward, dt * _w.Spin);
        float life = _traveled / (_w.Range * _s.SizeMult);
        _mesh.Transparency = Mathf.Clamp((life - _w.FadeStart) / Mathf.Max(0.01f, 1f - _w.FadeStart), 0, 1);

        // Zemine gomulmesin
        float gy = _d.World.HeightAt(GlobalPosition.X, GlobalPosition.Z) + _w.GroundOffset;
        if (GlobalPosition.Y < gy) GlobalPosition = GlobalPosition with { Y = gy };

        foreach (var z in Zipzip.All.ToArray())
        {
            if (z.Dead || _hit.Contains(z)) continue;
            if (z.Center.DistanceTo(GlobalPosition) > z.Radius + _width * 0.5f + _w.HitRadius) continue;
            _hit.Add(z);
            bool weak = z.HasSac && z.SacPos.DistanceTo(GlobalPosition) < z.Radius * _w.WeakRadius;
            _d.Capa.WeaponHit("tirpan", _w, _s, z, z.Center, _dir, weak, Palette.MayaTeal);
            if (_bounces > 0)
            {
                _bounces--;
                var next = Zipzip.All.Where(e => !e.Dead && !_hit.Contains(e)).OrderBy(e => e.Center.DistanceTo(GlobalPosition)).FirstOrDefault();
                if (next != null && next.Center.DistanceTo(GlobalPosition) < _w.BounceSearch)
                {
                    _dir = (next.Center - GlobalPosition).Normalized();
                    _traveled = Mathf.Max(0, _traveled - _w.BounceRefund);
                    LookDir();
                }
            }
        }
        _d.Wildlife?.ProjectileHit(GlobalPosition - _dir * step, GlobalPosition, _width * 0.5f + _w.HitRadius, _w.Damage * _s.SizeMult, _hitWild, _d.Data.Combat.WeaponType(_w.Id)); // yaratik2: gunduz yabanileri (Wildlife.Hits.cs)
        foreach (var t in _d.AtaTargetsNear(GlobalPosition, _width * 0.5f + _w.HitRadius)) if (_hitAta.Add(t)) _d.Capa.AtaWeaponHit("tirpan", _w, _s, t, t.Center, _dir, Palette.MayaTeal); // boss akisi
        while (_d.Abilities?.GhostHitSegment(GlobalPosition - _dir * step, GlobalPosition, _width * 0.5f + _w.HitRadius, _w.Damage * _s.SizeMult, _hitGhost) == true) { } // gucler: hayaletler (abilities/AbilitySystem.cs)
        if (_traveled > _w.Range * _s.SizeMult) QueueFree();
    }
}

/// <summary>Civilerin paylastigi kaynaklar (Capa.NailLook).</summary>
public sealed class NailLook
{
    public float Size, Glow;
    public Material Material;
    public Mesh Body;
    public Material TrailProcess;
    public Mesh TrailMesh;
}

/// <summary>Civi: ince, hizli, tek hedef. Seken karti ile bir sonrakine seker. Arkasinda kisa iz (feel.json civi.trail).</summary>
public partial class Nail : Node3D
{
    readonly Director _d;
    Vector3 _dir;
    readonly WeaponDef _w;
    readonly CombatStats _s;
    float _traveled;
    int _bounces;
    Zipzip _last;
    readonly GpuParticles3D _trail;

    public Nail(Director d, Vector3 dir, WeaponDef w, CombatStats s, NailLook look)
    {
        _d = d; _dir = dir; _w = w; _s = s; _bounces = s.Bounces;
        AddChild(new MeshInstance3D { Mesh = look.Body, MaterialOverride = look.Material, RotationDegrees = new Vector3(90, 0, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (look.TrailMesh != null)
        {
            _trail = new GpuParticles3D { Amount = 12, Lifetime = 0.12f, LocalCoords = false, ProcessMaterial = look.TrailProcess, DrawPass1 = look.TrailMesh };
            AddChild(_trail);
        }
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
            if (hp.DistanceTo(c) > z.Radius + _w.HitRadius * _s.SizeMult) continue;
            bool weak = z.HasSac && z.SacPos.DistanceTo(hp) < z.Radius * _w.WeakRadius;
            _d.Capa.WeaponHit("civi", _w, _s, z, c, _dir, weak, Palette.OffWhite);
            _last = z;
            if (_bounces-- > 0)
            {
                var next = Zipzip.All.Where(e => !e.Dead && e != z).OrderBy(e => e.Center.DistanceTo(c)).FirstOrDefault();
                if (next != null && next.Center.DistanceTo(c) < _w.BounceSearch)
                {
                    _dir = (next.Center - c).Normalized();
                    GlobalPosition = c;
                    GlobalBasis = Basis.LookingAt(_dir, Vector3.Up);
                    _traveled = Mathf.Max(0, _traveled - _w.BounceRefund);
                    return;
                }
            }
            QueueFree();
            return;
        }
        if (_d.Wildlife != null && _d.Wildlife.ProjectileHit(from, GlobalPosition, _w.HitRadius * _s.SizeMult, _w.Damage * _s.SizeMult, type: _d.Data.Combat.WeaponType(_w.Id))) { QueueFree(); return; } // yaratik2: gunduz yabanileri
        if (_d.AtaSegmentHit(from, GlobalPosition, _w.HitRadius * _s.SizeMult) is IAtaTarget ata) { _d.Capa.AtaWeaponHit("civi", _w, _s, ata, ata.Center, _dir, Palette.OffWhite); QueueFree(); return; } // boss akisi
        if (_d.Abilities?.GhostHitSegment(from, GlobalPosition, _w.HitRadius * _s.SizeMult, _w.Damage * _s.SizeMult) == true) { QueueFree(); return; } // gucler: hayaletler
        if (GlobalPosition.Y < _d.World.HeightAt(GlobalPosition.X, GlobalPosition.Z) + _w.GroundOffset)
        {
            _d.Capa.FeelDebris("civi_zemin", GlobalPosition, Palette.Soil);
            QueueFree();
            return;
        }
        if (_traveled > _w.Range * _s.SizeMult) QueueFree();
    }
}
