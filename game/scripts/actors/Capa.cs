using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa: tek el aleti, baslik takilir (GDD 4). Is basliklari: Kazma, Toprak, Sulama, Cekim, Insa.
/// Silah basligi: Tirpan Isini (baslangic setinde, ICERIK §6.2 #1).
/// Parcalar: baslik basina bir dosya (Capa.Kazma.cs, Capa.Bicme.cs, Capa.Toprak.cs, Capa.Sulama.cs, Capa.Cekim.cs,
/// Capa.Insa.cs, Capa.Weapons.cs). Burada: secim, kare isi ve isin.
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
    float _actCd, _fireCd, _waterTick;
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
                    _d.Hud.Hint(atRamp ? "Bırak: kargoya yükle ({act:fire})" : "Kargo Rampası'na taşı");
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
}
