using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Tam Kabarma (kabarma3): Zipzip sinifi gece dalgasinin genel Kabaran aktorudur. Ilk gecenin Zipzip'i aynen kalir;
/// plandan dogan her tur (Homur, Dalli, Bozuk...) ayni aktorle gelir: govdesi turun kodu/modeli, sicrayan olmayan
/// turler yurur, hedef sirasi GDD 14.3 (kendi ailesinin ekini -> Maya Kazani -> saldiran), elit oneki cani, hizi,
/// boyu ve hasari carpar. Sozlesme: IKabaran (game/scripts/night/Kabarans.cs).
/// </summary>
public partial class Zipzip : IKabaran
{
    KabaranState _kab;
    HashSet<string> _families;
    float _kazanCd;
    float _walkPhase;

    /// <summary>Plandan dogan Kabaran mi (ilk gecenin Zipzip'i degil).</summary>
    public bool Planned => _kab != null;
    /// <summary>Sicramayan turler yurur (Zipzip ve sicrayan arketipi ziplar).</summary>
    public bool Walks => _kab != null && Def.Archetype != "jumper";

    public KabaranState State
    {
        get
        {
            _kab ??= new KabaranState { Species = Def.Id, Hp = Hp, MaxHp = MaxHp, Damage = Def.Damage * _damageMult };
            _kab.Hp = Hp; _kab.MaxHp = MaxHp;
            _kab.X = GlobalPosition.X; _kab.Z = GlobalPosition.Z;
            return _kab;
        }
    }
    public Node3D Node => this;
    bool IKabaran.Dead => Dead;
    public bool Flying => Def.Archetype == "flyer";

    float AttackDamage => _kab != null ? _kab.Damage : Def.Damage * _damageMult;
    float MoveSpeed => Def.Speed * (_kab?.SpeedMult ?? 1f);

    /// <summary>Plandan gelen durumu baglar: can, hasar, hiz, boy, ekin aileleri (ICERIK 5: turu ceken aileler).</summary>
    public void BindKabaran(KabaranState s, FullKabarmaDef full)
    {
        _kab = s;
        MaxHp = Hp = s.MaxHp;
        // Zipzip disi Tripo govdesi (Gidak, Pofuduk): rig boyuna olceklenir (WildCreature ile ayni); kod govdesi gercek boyda.
        if (Def.Id != "zipzip" && _rig != null && _rig.Body == null) WildCreature.FitHeight(_rig.Model, Def.Rig?.Height ?? 0.75f);
        Scale0 *= s.Scale;
        _families = full.Families.Where(kv => kv.Value.Contains(Def.Id)).Select(kv => kv.Key).ToHashSet();
        Kabarans.Add(this);
    }

    public void ApplyHit(float damage, Vector3 from, float knock, int attacker)
    {
        if (Dead) return;
        if (_kab != null) _kab.LastHitBy = attacker;
        var dir = GlobalPosition - from; dir.Y = 0;
        Hit(damage, false, dir.LengthSquared() > 0.001f ? dir.Normalized() : Vector3.Forward, knock, false);
    }

    /// <summary>GDD 14.3 hedef: kendi ailesinin olgun ekini (ilk gecede: herhangi ekin).</summary>
    PlotView FindCropTarget(Vector3 pos)
    {
        // Olcum (KS 10, 200 Kabaran): hedefsiz olan her karede butun tarlayi tariyordu; arama 0.4-0.7 sn'de bir, kaydirmali.
        ulong now = Time.GetTicksMsec();
        if (now < _nextFind) return null;
        _nextFind = now + 400 + GetInstanceId() % 300;
        if (_families == null) return _dir.FarmView.NearestPlanted(pos);
        if (_families.Count == 0) return null; // ailesiz tur dogrudan Kazan'a gider
        var data = _dir.Data;
        return _dir.FarmView.NearestPlanted(pos, p => data.FindCrop(p.Crop ?? "")?.Family is { } f && _families.Contains(f));
    }

    /// <summary>Hedefsiz Kabaran Ocak'taki Maya Kazani'na yuklenir (GDD 14.4). true: bu kare Kazan'a vurdu.</summary>
    bool TickKazan(float dt, Vector3 pos, float dist)
    {
        _kazanCd -= dt;
        var n = _dir.Night;
        if (_kab == null || n?.Kazan == null || n.Phase == NightPhase.Dawn || dist > 1.6f + Radius) return false;
        if (_kazanCd > 0) return true;
        _kazanCd = 1.3f;
        PlayAttack();
        _dir.OnKazanHit(AttackDamage, pos); // Director.Kabarma.cs
        return true;
    }

    /// <summary>Yuruyen turun adimi: dogrudan hedefe, egime oturur. Klip "run".</summary>
    ulong _nextFind;

    void WalkStep(float dt, Vector3 goal, float dist, float slow, string flowKey)
    {
        var pos = GlobalPosition;
        var dir = new Vector3(goal.X - pos.X, 0, goal.Z - pos.Z);
        if (dist < 0.4f + Radius * 0.6f || dir.LengthSquared() < 1e-4f) { _walkPhase = 0; return; }
        dir = dir.Normalized();
        if (flowKey != null && dist > 3f && _dir.Flow?.Steer(pos, flowKey) is { } flow) dir = flow; // yolbulma: akis alani (TEKNIK 8.1), sicrayanlarla ayni
        float step = Mathf.Min(dist, MoveSpeed * slow * dt);
        GlobalPosition = _dir.World.Ground(pos + dir * step);
        _body.Rotation = new Vector3(0, Mathf.LerpAngle(_body.Rotation.Y, Mathf.Atan2(dir.X, dir.Z), Mathf.Min(1f, dt * 8f)), 0);
        _walkPhase += dt;
    }

    bool Walking => _walkPhase > 0;

    /// <summary>Tuzuk Ac Suru: gece dalgasi ekini daha hizli yer (GDD 14.8).</summary>
    float EatMult => Planned ? _dir.TuzukNow.EatSpeed : 1f;

    partial void KabaranExit();
    partial void KabaranExit() => Kabarans.Remove(this);
}
