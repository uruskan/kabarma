using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa: tek el aleti, baslik takilir (GDD 4). Is basliklari: Kazma, Biçme, Toprak, Sulama, Cekim (karar 23: Insa basligi kalkti;
/// yapi parcalari Insa Plani esyasiyla, yerlestirilebilirler kemerden kurulur: Capa.Hand.cs, Capa.Insa.cs). Capa kemerdeki bir
/// esyadir: yalniz elde (secili kemer yuvasi) iken calisir.
/// Silah basligi: Tirpan Isini (baslangic setinde, ICERIK §6.2 #1).
/// Parcalar: baslik basina bir dosya (Capa.Kazma.cs, Capa.Bicme.cs, Capa.Toprak.cs, Capa.Sulama.cs, Capa.Cekim.cs,
/// Capa.Insa.cs, Capa.Weapons.cs). Burada: secim, kare isi ve isin.
/// </summary>
public partial class Capa : Node3D
{
    // GDD 4: bes is basligi + silah yuvalari (Mk.I: 2). Karar 23: Insa basligi kalkti (Insa Plani esyasi).
    public enum Head { Kazma, Bicme, Toprak, Sulama, Cekim, Tirpan, Civi, Alev, Havan, Ark, Tohum, Ayaz, Yercekimi, MayaTopu } // 7-13: kusam (M4), Capa.Heads4.cs
    public static readonly string[] HeadNames = { "Kazma", "Biçme", "Toprak", "Sulama", "Çekim", "Tırpan Işını", "Çivi Tab.", "Alev Püskürteci", "Havan", "Ark", "Tohum Makinalısı", "Ayaz Lazeri", "Yerçekimi", "Maya Topu" };
    public static readonly Color[] HeadColors = { Palette.Warning, Palette.Plant.Lerp(Palette.Warning, 0.3f), Palette.Wood.Lerp(Palette.Warning, 0.4f), new("8CC8E8"), Palette.MayaViolet, Palette.MayaTeal, Palette.OffWhite, Palette.Warning, new("3A3A3A"), new("BFE6F5"), new("D9C089"), new("8CC8E8"), Palette.MayaViolet, Palette.MayaViolet };
    public static bool IsWeapon(Head h) => h >= Head.Tirpan;

    public Head Current { get; private set; } = Head.Kazma;
    Director _d;
    ToolFile _t;
    MeshInstance3D _beam;
    StandardMaterial3D _beamMat;
    GpuParticles3D _spray;
    float _actCd, _fireCd, _waterTick;
    float _shotBuffer;   // yari otomatik: beklemede gelen basis (Capa.Weapons.cs)
    bool _reacted;       // bu karede girdiye gorsel tepki verildi (gecikme olcumu)
    public bool UsingNow { get; private set; }
    /// <summary>Girise tepki verilen son kare ve baslik (gecikme sinamasi).</summary>
    public ulong LastReactionFrame { get; private set; }

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

    /// <summary>Baslik takili olabilir mi: is basliklari hep; silahlar CapaState'e gore (Civi Tabancasi tarifle acilir).</summary>
    public bool IsAvailable(Head h) => h switch
    {
        Head.Tirpan => _d.State.Capa.IsUnlocked(_t.Weapon.Id),
        Head.Civi => _d.State.Capa.IsUnlocked(_t.Nailgun.Id),
        >= Head.Alev => Head4Available(h), // kusam (M4): tarifin urunu cantada
        _ => true,
    };

    public void Select(Head h)
    {
        if (Current == h) return;
        if (!IsAvailable(h)) { _d.Hud.Hint($"{HeadNames[(int)h]} henüz yok: Tezgâh'ta yapılır", 1.5f); return; }
        if (Current == Head.Cekim && Held != null) Drop();
        Current = h;
        _shotBuffer = 0f;
        if (h == Head.Civi) NailLook(_t.Nailgun, _d.Night?.Stats ?? new CombatStats(), F.Source("civi").Trail > 0); // ilk atista kurulmasin
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
        _reacted = false;
        TickFlash(); // Capa.Feel.cs
        // F1 paneli acikken tiklama panele gider, aleti tetiklemez.
        if (_d.FeelPanel?.Open == true) { fire = false; firePressed = false; }
        bool spraying = false;
        bool beaming = false;
        UsingNow = fire || Held != null;
        p.Carrying = Held != null;
        p.Facing = UsingNow || IsWeapon(Current) && _d.NightActive;

        var aim = p.AimPoint(out var hitNode, 60f);
        // Karar 23: Capa elde degilse (kemerde baska yuva secili) elde tutulan esya isler (Capa.Hand.cs).
        if (!InHand)
        {
            _spray.Emitting = false; Sfx.Beam(false);
            if (Held != null) Drop();
            UsingNow = false; p.Carrying = false; p.Facing = false;
            if (!p.Down && TickHand(dt, fire, firePressed, aim)) { LastReactionFrame = Engine.GetProcessFrames(); _d.MarkReaction("insa"); }
            return;
        }
        HideHandGhosts(); // Capa eldeyken hayalet ve yapi seti gizli (Capa.Insa.cs)

        if (p.Down) { _spray.Emitting = false; Sfx.Beam(false); return; }

        switch (Current)
        {
            case Head.Kazma:
                if (fire)
                {
                    var rock = (hitNode as Node)?.GetParent() as Rock ?? NearestRockTo(aim, _t.MineAimSlack);
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
                if (fire && _actCd <= 0) { _actCd = _t.MowInterval; Mow(aim); }
                if (fire && muzzle.DistanceTo(aim) <= _t.MowRange) { ShowBeam(muzzle, aim, HeadColors[1], 0.5f); beaming = true; }
                break;
            case Head.Toprak:
                if (fire && _actCd <= 0) { _actCd = _t.TillInterval; TillOrPlant(aim); }
                if (fire && muzzle.DistanceTo(aim) <= _t.TillRange) { ShowBeam(muzzle, aim, HeadColors[1], 0.35f); beaming = true; }
                break;
            case Head.Sulama:
                if (fire)
                {
                    spraying = true;
                    var dir = (aim - muzzle).Normalized();
                    _spray.GlobalBasis = Basis.LookingAt(dir, Vector3.Up);
                    if (_waterTick <= 0) { _waterTick = _t.WaterInterval; WaterAt(aim); PushSmallEnemies(muzzle, aim); } // Capa.Itme.cs
                }
                break;
            case Head.Cekim:
                beaming |= TickCekim(dt, fire, firePressed, aim, muzzle); // Capa.Cekim.cs: tut, tasi, birak, firlat
                break;
            case Head.Tirpan:
                TickWeapon(_d.Data.Tool.Weapon, fire, firePressed, dt, () => FireTirpan(aim)); // Capa.Weapons.cs
                break;
            case Head.Civi:
                TickWeapon(_d.Data.Tool.Nailgun, fire, firePressed, dt, () => FireNail(aim)); // yari otomatik
                break;
            default: // kusam (M4): Alev, Havan, Ark, Tohum, Ayaz Lazeri, Yercekimi, Maya Topu
                beaming |= TickHead4(Current, dt, fire, firePressed, aim, muzzle);
                break;
        }
        _spray.Emitting = spraying;
        Sfx.Beam(beaming && Current is Head.Kazma or Head.Cekim);
        // Girdi -> ilk gorsel tepki (GDD 4: en fazla 50 ms): isin, puskurtme, mermi, tutma, yerlestirme.
        if (_reacted || ((fire || firePressed) && (beaming || spraying)))
        {
            LastReactionFrame = Engine.GetProcessFrames();
            _d.MarkReaction(HeadId(Current));
        }
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
