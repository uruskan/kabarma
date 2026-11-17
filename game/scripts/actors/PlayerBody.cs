using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncunun kodla animasyon katmani (SANAT-SES §3.1 "Kodla"): klibin ustune, ayni karede, sirayla:
///  1. kalkis harmani (ragdoll pozundan kalkis klibine),
///  2. kosarken one egilme ve donuste yana yatma,
///  3. bas ve govdenin bakis yonune donmesi (aci omurgaya paylastirilir; kalani LookAtModifier3D),
///  4. geri tepme (omurga geriye seker),
///  5. yurume ustune katmanlar: el arabasi itme (push_cart) ve suda yurume (wade),
///  6. canta yayi (Backpack kemigi, ivmeye gecikmeli tepki),
///  7. ayaklarin zemine oturmasi: ayak altindan isin, kalca inisi ve bacak IK hedefleri (TwoBoneIK3D sonra cozer).
/// Godot her karede degistirilmemis pozu geri yukler; buradaki donusler birikmez.
/// Girdiler PlayerRig.Update'ten gelir (fizik karesi), uygulama iskeletin guncelleme karesinde.
/// </summary>
public partial class PlayerBody : SkeletonModifier3D
{
    public PlayerRigDef Def = new();

    // --- Girdiler (PlayerRig doldurur) ---
    public float Speed, LookYaw, LookPitch, Recoil, YawRate;
    public Vector3 AccelModel;            // govde ivmesi, model uzayinda
    public bool Sprint, OnFloor, FootIkWanted;
    public float PushTarget, WadeTarget;
    public float FloorY;
    public Rid Exclude;
    public TwoBoneIK3D LegIk;
    public Node3D[] FootTargets = new Node3D[2], KneePoles = new Node3D[2];

    float _push, _wade, _foot, _pelvis, _lean, _bank;
    float _bagPitch, _bagRoll, _bagPitchV, _bagRollV;
    Vector3 _bagPrev, _bagVel, _bagAcc;
    bool _bagInit;
    int _hips = -1, _bag = -1;
    int[] _spine = new int[0];
    readonly int[] _feet = { -1, -1 }, _knees = { -1, -1 };
    readonly Dictionary<string, int> _idx = new();

    // Kalkis harmani
    Quaternion[] _snap;
    Vector3 _snapHips;
    float _snapT = -1f, _snapDur = 0.4f;

    static readonly (string bone, Vector3 dir)[] PushPose =
    {
        ("Spine02", new Vector3(0, 1, 0.35f)), ("Spine01", new Vector3(0, 1, 0.35f)),
        ("RightArm", new Vector3(-0.12f, -0.5f, 0.85f)), ("RightForeArm", new Vector3(0, -0.2f, 1)),
        ("LeftArm", new Vector3(0.12f, -0.5f, 0.85f)), ("LeftForeArm", new Vector3(0, -0.2f, 1)),
    };
    static readonly (string bone, Vector3 dir)[] WadePose =
    {
        ("Spine01", new Vector3(0, 1, 0.12f)),
        ("LeftArm", new Vector3(0.85f, -0.15f, 0.25f)), ("LeftForeArm", new Vector3(0.75f, 0.25f, 0.45f)),
        ("RightArm", new Vector3(-0.85f, -0.15f, 0.25f)), ("RightForeArm", new Vector3(-0.75f, 0.25f, 0.45f)),
    };

    int Bone(string n) => _idx.TryGetValue(n, out var i) ? i : -1;

    void Cache(Skeleton3D s)
    {
        if (_idx.Count > 0) return;
        for (int b = 0; b < s.GetBoneCount(); b++) _idx[s.GetBoneName(b)] = b;
        _hips = Bone("Hips"); _bag = Bone("Backpack");
        _spine = new[] { Bone("Spine02"), Bone("Spine01"), Bone("Spine") };
        _feet[0] = Bone("LeftFoot"); _feet[1] = Bone("RightFoot");
        _knees[0] = Bone("LeftLeg"); _knees[1] = Bone("RightLeg");
    }

    /// <summary>Ragdoll'un son pozundan (yerel donusler, kalca) kalkis klibine harmanlamayi baslatir.</summary>
    public void BlendFrom(Quaternion[] localRots, Vector3 hipsPos, float seconds)
    {
        _snap = localRots; _snapHips = hipsPos; _snapT = 0f; _snapDur = Mathf.Max(0.05f, seconds);
        _bagInit = false; _bagPitch = _bagRoll = _bagPitchV = _bagRollV = 0f; // govde yer degistirdi: canta ivmesi sifirdan
    }

    public bool HasBag => _bag >= 0;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var s = GetSkeleton();
        if (s == null) return;
        Cache(s);
        float dt = Mathf.Max((float)delta, 1e-4f);

        // 1. Kalkis harmani
        if (_snap != null && _snapT >= 0f && _snapT < _snapDur)
        {
            float w = Mathf.SmoothStep(0f, 1f, _snapT / _snapDur);
            for (int b = 0; b < Mathf.Min(_snap.Length, s.GetBoneCount()); b++)
                s.SetBonePoseRotation(b, _snap[b].Slerp(s.GetBonePoseRotation(b), w));
            if (_hips >= 0) s.SetBonePosePosition(_hips, _snapHips.Lerp(s.GetBonePosePosition(_hips), w));
            _snapT += dt;
        }

        // 2. Egilme ve yana yatma
        float leanDeg = Sprint ? Def.LeanSprintDeg : Speed > 4.2f ? Def.LeanRunDeg : Speed / 4.2f * Def.LeanRunDeg * 0.5f;
        leanDeg += Mathf.Clamp(AccelModel.Z * Def.LeanAccelDeg, -Def.LeanSprintDeg, Def.LeanSprintDeg);
        _lean = Mathf.Lerp(_lean, OnFloor ? leanDeg : 0f, Mathf.Min(1f, dt * 6f));
        float bank = Mathf.Clamp(-YawRate * Speed * Def.BankGain, -Def.BankMaxDeg, Def.BankMaxDeg);
        _bank = Mathf.Lerp(_bank, OnFloor ? bank : 0f, Mathf.Min(1f, dt * 5f));
        if (_hips >= 0 && Mathf.Abs(_bank) > 0.01f) RotateGlobal(s, _hips, new Quaternion(new Vector3(0, 0, 1), Mathf.DegToRad(_bank)));
        if (_spine.Length > 0 && _spine[0] >= 0 && Mathf.Abs(_lean) > 0.01f) RotateGlobal(s, _spine[0], new Quaternion(Vector3.Right, Mathf.DegToRad(_lean)));

        // 3. Govde donusu: yatay aci omurgaya, dikey acinin bir payi gogse.
        float yaw = Mathf.Clamp(LookYaw, -Mathf.DegToRad(Def.TwistMaxDeg), Mathf.DegToRad(Def.TwistMaxDeg));
        for (int i = 0; i < _spine.Length; i++)
        {
            if (_spine[i] < 0) continue;
            float share = i < Def.TwistShare.Count ? Def.TwistShare[i] : 0f;
            var q = new Quaternion(Vector3.Up, yaw * share);
            if (i == _spine.Length - 1) q = new Quaternion(Vector3.Right, -LookPitch * Def.PitchShare) * q;
            RotateGlobal(s, _spine[i], q);
        }

        // 4. Geri tepme: ust govde geriye seker.
        if (Mathf.Abs(Recoil) > 1e-3f && _spine.Length > 2 && _spine[2] >= 0)
            RotateGlobal(s, _spine[2], new Quaternion(Vector3.Right, -Recoil * Mathf.DegToRad(Def.RecoilSpineDeg)));

        // 5. Katmanlar
        _push = Mathf.MoveToward(_push, PushTarget * Def.PushWeight, dt * 4f);
        _wade = Mathf.MoveToward(_wade, WadeTarget * Def.WadeWeight, dt * 3f);
        if (_push > 0.001f) foreach (var (b, d) in PushPose) OverrideDir(s, Bone(b), d, _push);
        if (_wade > 0.001f) foreach (var (b, d) in WadePose) OverrideDir(s, Bone(b), d, _wade);

        // 6. Canta yayi
        if (_bag >= 0) BagSpring(s, dt);

        // 7. Ayak IK'si
        FootIk(s, dt);
    }

    /// <summary>Kemigi model uzayindaki q donusuyle, kendi eklemi etrafinda cevirir.</summary>
    public static void RotateGlobal(Skeleton3D s, int b, Quaternion q)
    {
        if (b < 0) return;
        var g = s.GetBoneGlobalPose(b).Basis;
        int p = s.GetBoneParent(b);
        var pg = p >= 0 ? s.GetBoneGlobalPose(p).Basis : Basis.Identity;
        var local = pg.Orthonormalized().Inverse() * (new Basis(q) * g.Orthonormalized());
        s.SetBonePoseRotation(b, local.GetRotationQuaternion());
    }

    /// <summary>Kemigin yonunu model uzayinda istenen yone, w agirligiyla ceker.</summary>
    static void OverrideDir(Skeleton3D s, int b, Vector3 want, float w)
    {
        if (b < 0) return;
        var cur = (s.GetBoneGlobalPose(b).Basis.Orthonormalized() * PlayerPoses.LocalDir(s, b)).Normalized();
        var arc = PlayerPoses.Arc(cur, want.Normalized());
        RotateGlobal(s, b, Quaternion.Identity.Slerp(arc, Mathf.Clamp(w, 0f, 1f)));
    }

    void BagSpring(Skeleton3D s, float dt)
    {
        var xf = s.GlobalTransform;
        var p = xf * s.GetBoneGlobalPose(_bag).Origin;
        if (!_bagInit || p.DistanceTo(_bagPrev) > 1.5f) { _bagPrev = p; _bagVel = Vector3.Zero; _bagAcc = Vector3.Zero; _bagInit = true; }
        var v = (p - _bagPrev) / dt;
        var a = ((v - _bagVel) / dt).LimitLength(40f);
        _bagAcc = _bagAcc.Lerp(a, Mathf.Min(1f, dt * 20f)); // gurultuyu suz
        _bagPrev = p; _bagVel = v;
        var am = xf.Basis.Orthonormalized().Inverse() * _bagAcc;
        float tp = Mathf.Clamp(Def.BagGain * am.Z, -Def.BagMaxPitch, Def.BagMaxPitch);
        float tr = Mathf.Clamp(-Def.BagGain * am.X, -Def.BagMaxRoll, Def.BagMaxRoll);
        _bagPitchV += (-Def.BagStiffness * (_bagPitch - tp) - Def.BagDamping * _bagPitchV) * dt;
        _bagRollV += (-Def.BagStiffness * (_bagRoll - tr) - Def.BagDamping * _bagRollV) * dt;
        _bagPitch = Mathf.Clamp(_bagPitch + _bagPitchV * dt, -Def.BagMaxPitch, Def.BagMaxPitch);
        _bagRoll = Mathf.Clamp(_bagRoll + _bagRollV * dt, -Def.BagMaxRoll, Def.BagMaxRoll);
        RotateGlobal(s, _bag, new Quaternion(Vector3.Right, _bagPitch) * new Quaternion(new Vector3(0, 0, 1), _bagRoll));
    }

    /// <summary>Son karedeki canta acisi (sinama ve goruntu icin).</summary>
    public Vector2 BagAngles => new(_bagPitch, _bagRoll);

    void FootIk(Skeleton3D s, float dt)
    {
        _foot = Mathf.MoveToward(_foot, FootIkWanted && OnFloor ? 1f : 0f, dt * Def.FootBlend);
        if (LegIk != null) LegIk.Influence = _foot;
        if (_foot <= 0.001f || _feet[0] < 0 || _feet[1] < 0 || !IsInsideTree()) { _pelvis = 0f; return; }
        var xf = s.GlobalTransform;
        var space = s.GetWorld3D().DirectSpaceState;
        var dy = new float[2];
        var footW = new Vector3[2];
        for (int i = 0; i < 2; i++)
        {
            footW[i] = xf * s.GetBoneGlobalPose(_feet[i]).Origin;
            var q = PhysicsRayQueryParameters3D.Create(footW[i] + Vector3.Up * Def.FootRayUp, footW[i] + Vector3.Down * Def.FootRayDown, 1u);
            q.Exclude = new Godot.Collections.Array<Rid> { Exclude };
            var hit = space.IntersectRay(q);
            dy[i] = hit.Count > 0 ? Mathf.Clamp(((Vector3)hit["position"]).Y - FloorY, -Def.FootMaxDrop, Def.FootMaxRise) : 0f;
        }
        float drop = Mathf.Min(0f, Mathf.Min(dy[0], dy[1]));
        _pelvis = Mathf.Lerp(_pelvis, drop, Mathf.Min(1f, dt * Def.FootBlend));
        if (_hips >= 0 && Mathf.Abs(_pelvis) > 1e-4f)
        {
            var off = xf.Basis.Inverse() * new Vector3(0, _pelvis * _foot, 0);
            s.SetBonePosePosition(_hips, s.GetBonePosePosition(_hips) + off);
        }
        var fwd = xf.Basis.Orthonormalized() * new Vector3(0, 0, 1);
        for (int i = 0; i < 2; i++)
        {
            if (FootTargets[i] == null) continue;
            FootTargets[i].GlobalPosition = footW[i] + new Vector3(0, dy[i], 0);
            FootTargets[i].GlobalBasis = (xf.Basis * s.GetBoneGlobalPose(_feet[i]).Basis).Orthonormalized();
            if (KneePoles[i] != null) KneePoles[i].GlobalPosition = xf * s.GetBoneGlobalPose(_knees[i]).Origin + fwd * 0.6f;
        }
    }
}
