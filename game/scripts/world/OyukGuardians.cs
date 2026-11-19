using System;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// GECICI SAVAS KOPRUSU (oyuk akisi, M3): savas akisinin ortak hasar arayuzu (Combat*) main'e girene dek koruyucular
/// silah isabetini boss akisinin IAtaTarget yolundan alir (Director.AtaTargetsNear / AtaSegmentHit). Duz nesne (Node degil):
/// AtaSystem.Cleanup Node hedefleri siler, bu nesneyi silmez; listeden dusurulurse OyukSystem her kare geri ekler.
/// Combat* gelince bu dosya o arayuze tasinir.
/// </summary>
public sealed class OyukTarget : IAtaTarget
{
    readonly Func<Vector3> _center;
    readonly IOyukFight _fight;
    readonly Func<bool> _weak;
    public float Radius { get; }
    public Action<float, bool> OnHit;
    /// <summary>M4: ham isabet (tur ve zehir/ates bilgisi); Camur Devi kurutma ve Akrep catlatma icin once cagrilir.</summary>
    public Action<CombatHit> OnRaw;

    public OyukTarget(Func<Vector3> center, float radius, IOyukFight fight, Func<bool> weak)
    { _center = center; Radius = radius; _fight = fight; _weak = weak; }

    public Vector3 Center => _center();
    public bool Alive => !_fight.Dead && _fight.Vulnerable;
    public bool IsWeakPoint => _weak();
    public void TakeWeapon(float dmg, bool crit, Vector3 dir, float knock)
    {
        float got = _fight.Hit(dmg);
        OnHit?.Invoke(got, crit);
    }

    // savas sozlesmesi (M3, ICombatTarget): IAtaTarget artik isabeti TakeHit'ten verir. Yerel kopru, savas akisi ekledi:
    // koruyucunun kendi govdesi yok (alan etkisi almaz), isabet eski yola iner. Gercek gecis oyuk akisinin isi.
    public CombatBody Body => null;
    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        OnRaw?.Invoke(hit);
        float got = _fight.Hit(hit.Amount, hit.Type); // M4: tur bilgili (Mole ve Bat eski yola duser)
        OnHit?.Invoke(got, hit.Crit);
        return new CombatResult { Damage = got };
    }
}

/// <summary>Kostebek Bey gorunusu (YER TUTUCU, koddan): govde, pembe burun, pence, bere. Sersemken basinda yildizlar.</summary>
public partial class MoleVisual : Node3D
{
    Node3D _body, _stars;
    float _t, _flash;
    public float Rise; // 0 yerin altinda, 1 ustte

    public override void _Ready()
    {
        var fur = new Color("6E5644");
        _body = new MeshKit()
            .Pillow(new Vector3(1.9f, 2.1f, 1.7f), MeshKit.At(0, 1.05f, 0), fur, 0.7f)
            .Sphere(0.32f, MeshKit.At(0, 1.35f, -0.95f), new Color("F2A3A8"), 10, 6)
            .Sphere(0.11f, MeshKit.At(-0.35f, 1.65f, -0.78f), new Color("1A1412"), 6, 4)
            .Sphere(0.11f, MeshKit.At(0.35f, 1.65f, -0.78f), new Color("1A1412"), 6, 4)
            .Pillow(new Vector3(0.75f, 0.25f, 0.55f), MeshKit.At(new Vector3(-0.85f, 0.75f, -0.6f), new Vector3(0.3f, 0.4f, 0)), new Color("E8D3B0"), 0.5f)
            .Pillow(new Vector3(0.75f, 0.25f, 0.55f), MeshKit.At(new Vector3(0.85f, 0.75f, -0.6f), new Vector3(0.3f, -0.4f, 0)), new Color("E8D3B0"), 0.5f)
            .Cylinder(0.55f, 0.6f, 0.45f, MeshKit.At(0, 2.2f, 0.05f), new Color("2B2230"), 14) // bere (Bey)
            .Cylinder(0.85f, 0.85f, 0.06f, MeshKit.At(0, 2.0f, 0.05f), new Color("2B2230"), 14)
            .Instance();
        AddChild(_body);
        _stars = new Node3D { Name = "Stars", Visible = false };
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4;
            _stars.AddChild(new MeshKit().Sphere(0.12f, MeshKit.At(Mathf.Cos(a) * 0.8f, 0, Mathf.Sin(a) * 0.8f), Palette.Warning, 6, 3)
                .Instance(new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Palette.Warning }));
        }
        _stars.Position = new Vector3(0, 2.8f, 0);
        AddChild(_stars);
    }

    public void Flash() => _flash = 0.15f;

    public void Show(MoleFight f, float dt)
    {
        _t += dt;
        float target = f.Dead ? 0f : f.State == MoleFight.Phase.Hidden ? 0f : 1f;
        Rise = Mathf.MoveToward(Rise, target, dt * (target > Rise ? 5f : 3f));
        _body.Position = new Vector3(0, Mathf.Lerp(-2.6f, 0f, Rise), 0);
        bool weak = f.Weak && !f.Dead;
        _stars.Visible = weak && Rise > 0.8f;
        _stars.Rotation = new Vector3(0, _t * 4f, 0);
        _body.Rotation = new Vector3(weak ? Mathf.Sin(_t * 9f) * 0.12f : 0f, 0, weak ? 0.2f : 0f);
        if (_flash > 0) { _flash -= dt; _body.Scale = Vector3.One * (1f + _flash * 1.2f); } else _body.Scale = Vector3.One;
        Visible = Rise > 0.01f;
    }
}

/// <summary>Yarasa Yumagi gorunusu (YER TUTUCU, koddan): yarasalar MultiMesh; toplu iken tavanda top, dagilinca odada daire
/// cizer ve ana yarasa (buyuk, kizil gozlu) ortada asagi iner.</summary>
public partial class BatVisual : Node3D
{
    MultiMesh _mm;
    Node3D _main;
    Vector3[] _seed;
    float _t, _spread, _flash, _swoop;
    public Vector3 Hang;
    public float RoomRadius = 14f;

    public void Init(int count, Vector3 hang)
    {
        Hang = hang;
        var bat = new MeshKit()
            .Sphere(0.16f, MeshKit.At(0, 0, 0), new Color("6A5C86"), 8, 4)
            .Pillow(new Vector3(0.55f, 0.04f, 0.28f), MeshKit.At(new Vector3(-0.3f, 0.02f, 0), new Vector3(0, 0, 0.35f)), new Color("54496E"), 0.2f)
            .Pillow(new Vector3(0.55f, 0.04f, 0.28f), MeshKit.At(new Vector3(0.3f, 0.02f, 0), new Vector3(0, 0, -0.35f)), new Color("54496E"), 0.2f)
            .Build();
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = bat, InstanceCount = count };
        AddChild(new MultiMeshInstance3D { Multimesh = _mm, MaterialOverride = MeshKit.StaticMaterial });
        var rng = new RandomNumberGenerator { Seed = 42 };
        _seed = new Vector3[count];
        for (int i = 0; i < count; i++) _seed[i] = new Vector3(rng.Randf(), rng.Randf(), rng.Randf());
        _main = new MeshKit()
            .Sphere(0.45f, MeshKit.At(0, 0, 0), new Color("4A2E3A"), 12, 6)
            .Pillow(new Vector3(1.5f, 0.08f, 0.7f), MeshKit.At(new Vector3(-0.85f, 0.05f, 0), new Vector3(0, 0, 0.3f)), new Color("33202C"), 0.2f)
            .Pillow(new Vector3(1.5f, 0.08f, 0.7f), MeshKit.At(new Vector3(0.85f, 0.05f, 0), new Vector3(0, 0, -0.3f)), new Color("33202C"), 0.2f)
            .Sphere(0.08f, MeshKit.At(-0.16f, 0.12f, -0.4f), new Color("FF3B30"), 6, 3)
            .Sphere(0.08f, MeshKit.At(0.16f, 0.12f, -0.4f), new Color("FF3B30"), 6, 3)
            .Instance();
        _main.Visible = false;
        AddChild(_main);
    }

    /// <summary>Ana yarasanin dunyadaki yeri (vurus hedefi).</summary>
    public Vector3 MainPos => _main.GlobalPosition;
    public void Flash() => _flash = 0.15f;
    public void Swoop() => _swoop = 0.6f;

    public void Show(BatKnotFight f, float dt, Vector3 player)
    {
        _t += dt;
        _spread = Mathf.MoveToward(_spread, f.Scattered && !f.Dead ? 1f : 0f, dt * (f.Scattered ? 2.5f : 0.8f));
        if (_swoop > 0) _swoop -= dt;
        var t = new Transform3D();
        for (int i = 0; i < _seed.Length; i++)
        {
            var s = _seed[i];
            // Toplu: tavanda ~1,1 m'lik top; dagilmis: odada farkli yaricap ve yukseklikte daireler.
            float a0 = s.X * Mathf.Tau + _t * (0.4f + s.Y);
            var ball = Hang + new Vector3(Mathf.Cos(a0) * 0.9f * s.Z, -s.Y * 0.9f, Mathf.Sin(a0) * 0.9f * s.Z);
            float a1 = s.X * Mathf.Tau + _t * (1.2f + s.Y * 1.4f) * (i % 2 == 0 ? 1 : -1);
            float r = 3f + s.Z * (RoomRadius - 5f);
            var fly = new Vector3(Hang.X + Mathf.Cos(a1) * r, Hang.Y - 2.5f - s.Y * 4f + Mathf.Sin(_t * 3f + i) * 0.4f, Hang.Z + Mathf.Sin(a1) * r);
            var p = ball.Lerp(fly, _spread);
            if (_swoop > 0 && i % 4 == 0) p = p.Lerp(player + Vector3.Up * 1.4f, Mathf.Sin(_swoop / 0.6f * Mathf.Pi) * 0.85f);
            float flap = Mathf.Sin(_t * 22f + i) * 0.5f * Mathf.Max(_spread, _swoop > 0 ? 1 : 0.15f);
            t = new Transform3D(new Basis(Vector3.Up, a1 + Mathf.Pi / 2f) * new Basis(Vector3.Back, flap), p);
            _mm.SetInstanceTransform(i, t);
        }
        _main.Visible = _spread > 0.6f && !f.Dead;
        _main.Position = Hang + new Vector3(Mathf.Sin(_t * 0.7f) * 2.5f, -4.5f + Mathf.Sin(_t * 2.1f) * 0.4f, Mathf.Cos(_t * 0.5f) * 2.5f);
        _main.Rotation = new Vector3(0, _t * 0.7f, Mathf.Sin(_t * 10f) * 0.25f);
        if (_flash > 0) { _flash -= dt; _main.Scale = Vector3.One * (1f + _flash * 1.5f); } else _main.Scale = Vector3.One;
        if (f.Dead) Visible = false;
    }
}
