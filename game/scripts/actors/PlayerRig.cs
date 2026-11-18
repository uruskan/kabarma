using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncunun kodla ve fizikle animasyon katmanlari (SANAT-SES §3.1). Iskeletin degistirici sirasi:
///  1. PlayerBody: egilme, govde donusu, geri tepme, itme ve suda yurume katmanlari, ayak IK hedefleri.
///  2. LegIK (TwoBoneIK3D, iki ayak): ayaklar zemine oturur.
///  3. ArmIK + AlignR: sag bilek RightGoal'a gider, el RightGoal'in yonune doner (nisanda Capa'nin kabzasi).
///  4. ArmIKL + AlignL: sol el LeftGoal'a (iki elle tutulan nesnenin ikinci tutamagi; El Arabasi).
///  5. HeadLook (LookAtModifier3D): bas nisan yonune doner, acisi sinirli.
///  6. Ragdoll (PhysicalBoneSimulator3D): yere serilme; kalkista PlayerBody ragdoll pozundan kalkis klibine harmanlar.
/// Godot degistirilmis pozu her karede geri yukler; son poz yalnizca SkeletonUpdated sinyalinde okunur.
/// Tutulan nesne ve vizor bu yuzden orada yerlestirilir (PoseReady, Player.Grip.cs).
/// </summary>
public sealed class PlayerRig
{
    readonly Skeleton3D _skel;
    readonly TwoBoneIK3D _arm, _armL, _legs;
    readonly HandAlign _alignR, _alignL;
    readonly LookAtModifier3D _look;
    readonly PhysicalBoneSimulator3D _rag;
    public readonly PlayerBody Body;
    public readonly Node3D HandTarget, ElbowPole, HandTargetL, ElbowPoleL, LookTarget;
    readonly Node3D[] _footTargets = new Node3D[2], _kneePoles = new Node3D[2];
    readonly Dictionary<string, PhysicalBone3D> _bones = new();
    readonly int _hips, _hand;
    float _armW, _armLW, _lookW;
    public bool Ragdolling { get; private set; }

    /// <summary>Bileklerin istenen dunya donusumu (Player.Grip.cs her fizik karesinde verir); null: el animasyondan.</summary>
    public Transform3D? RightGoal, LeftGoal;
    /// <summary>Hedef yokken sag elin istenen dunya yonu (alcak hazir tutus: kol animasyondan, yalniz bilek doner).</summary>
    public Basis? RightCarry;
    public float CarryWeight = 1f;
    float _carryW;
    Basis _alignBasis = Basis.Identity;
    /// <summary>Kol IK'sinin acilip kapanma hizi (grips.json armBlend).</summary>
    public float ArmBlend = 6f;
    /// <summary>Sag kol IK'sinin su anki agirligi (0 animasyon, 1 tam hedefte).</summary>
    public float RightWeight => _armW;
    public float LeftWeight => _armLW;

    /// <summary>Iskeletin son pozu hazir (degistiriciler uygulandi): Capa ve vizor burada yerlesir.</summary>
    public event System.Action PoseReady;

    // Ragdoll'un son pozu (kalkis harmani icin), dunya uzayinda yakalanir.
    Quaternion[] _lastRots;
    Transform3D _lastHipsWorld, _lastTorsoWorld;
    Vector3 _lastHeadPos;
    readonly int _torso, _head;

    public PlayerRig(Node3D owner, Skeleton3D skel, PlayerRigDef def)
    {
        _skel = skel;
        _hips = skel.FindBone("Hips");
        _hand = skel.FindBone("RightHand");
        _torso = skel.FindBone("Spine01");
        _head = skel.FindBone("Head");
        HandTarget = Marker(owner, "HandTarget");
        ElbowPole = Marker(owner, "ElbowPole");
        HandTargetL = Marker(owner, "HandTargetL");
        ElbowPoleL = Marker(owner, "ElbowPoleL");
        LookTarget = Marker(owner, "LookTarget");
        for (int i = 0; i < 2; i++) { _footTargets[i] = Marker(owner, $"FootTarget{i}"); _kneePoles[i] = Marker(owner, $"KneePole{i}"); }

        // 1. Kodla katman
        Body = new PlayerBody { Name = "Body", Def = def, FootTargets = _footTargets, KneePoles = _kneePoles };
        skel.AddChild(Body);

        // 2. Bacak IK'si
        _legs = new TwoBoneIK3D { Name = "LegIK", Influence = 0f };
        skel.AddChild(_legs);
        _legs.SettingCount = 2;
        string[] side = { "Left", "Right" };
        for (int i = 0; i < 2; i++)
        {
            _legs.SetRootBoneName(i, side[i] + "UpLeg");
            _legs.SetMiddleBoneName(i, side[i] + "Leg");
            _legs.SetEndBoneName(i, side[i] + "Foot");
            _legs.SetTargetNode(i, _legs.GetPathTo(_footTargets[i]));
            _legs.SetPoleNode(i, _legs.GetPathTo(_kneePoles[i]));
        }
        Body.LegIk = _legs;

        // 3. Kol IK'si
        _arm = new TwoBoneIK3D { Name = "ArmIK", Influence = 0f };
        skel.AddChild(_arm);
        _arm.SettingCount = 1;
        _arm.SetRootBoneName(0, "RightArm");
        _arm.SetMiddleBoneName(0, "RightForeArm");
        _arm.SetEndBoneName(0, "RightHand");
        _arm.SetTargetNode(0, _arm.GetPathTo(HandTarget));
        _arm.SetPoleNode(0, _arm.GetPathTo(ElbowPole));
        _alignR = new HandAlign { Name = "AlignR", BoneName = "RightHand" };
        skel.AddChild(_alignR);

        // 4. Sol kol IK'si (ikinci tutamak)
        _armL = new TwoBoneIK3D { Name = "ArmIKL", Influence = 0f };
        skel.AddChild(_armL);
        _armL.SettingCount = 1;
        _armL.SetRootBoneName(0, "LeftArm");
        _armL.SetMiddleBoneName(0, "LeftForeArm");
        _armL.SetEndBoneName(0, "LeftHand");
        _armL.SetTargetNode(0, _armL.GetPathTo(HandTargetL));
        _armL.SetPoleNode(0, _armL.GetPathTo(ElbowPoleL));
        _alignL = new HandAlign { Name = "AlignL", BoneName = "LeftHand" };
        skel.AddChild(_alignL);

        // 5. Bakis
        _look = new LookAtModifier3D
        {
            Name = "HeadLook", Influence = 0f, BoneName = "Head",
            ForwardAxis = SkeletonModifier3D.BoneAxis.PlusZ, PrimaryRotationAxis = Vector3.Axis.Y,
            UseAngleLimitation = true, SymmetryLimitation = true,
            PrimaryLimitAngle = Mathf.DegToRad(140), SecondaryLimitAngle = Mathf.DegToRad(70),
            Duration = 0.18f,
        };
        skel.AddChild(_look);
        _look.TargetNode = _look.GetPathTo(LookTarget);

        // 6. Ragdoll
        _rag = new PhysicalBoneSimulator3D { Name = "Ragdoll" };
        skel.AddChild(_rag);
        // Iskelet metre biriminde (merge_player.py olcegi uygular); sekil boyutlari cm, asagida /100.
        AddBone("Hips", 14f, 18f);
        AddBone("Spine01", 14f, 22f);
        AddBone("Head", 15f, 10f);
        AddBone("LeftUpLeg", 7f, 30f); AddBone("LeftLeg", 6f, 30f);
        AddBone("RightUpLeg", 7f, 30f); AddBone("RightLeg", 6f, 30f);
        AddBone("LeftArm", 5f, 18f); AddBone("LeftForeArm", 5f, 18f);
        AddBone("RightArm", 5f, 18f); AddBone("RightForeArm", 5f, 18f);
        _rag.Active = false;

        skel.SkeletonUpdated += OnSkeletonUpdated;
    }

    static Node3D Marker(Node3D owner, string name)
    {
        var n = new Node3D { Name = name, TopLevel = true };
        owner.AddChild(n);
        return n;
    }

    void AddBone(string name, float radius, float length)
    {
        int idx = _skel.FindBone(name);
        if (idx < 0) return;
        var pb = new PhysicalBone3D
        {
            Name = "PB_" + name, Mass = 2f,
            JointType = PhysicalBone3D.JointTypeEnum.Cone, LinearDamp = 0.4f, AngularDamp = 2f, Friction = 0.9f,
        };
        // Ragdoll kemikleri birbirine carpmaz (eklemde ust uste binerler); yalnizca dunyaya (katman 1) carpar.
        pb.CollisionLayer = 1u << 2;
        pb.CollisionMask = 1u;
        pb.Set("bone_name", name); // C# baglayicisinda ozellik yok; motor ozelligi adiyla ayarlanir
        // Varsayilan burulma 180 derece: govde belden ters donebiliyordu (kalca sirtustu, gogus yuzustu).
        pb.Set("joint_constraints/swing_span", 50f);
        pb.Set("joint_constraints/twist_span", 25f);
        float r = radius / 100f, l = length / 100f;
        pb.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = r, Height = Mathf.Max(l, r * 2.1f) },
            Position = new Vector3(0, l * 0.5f, 0),
        });
        _rag.AddChild(pb);
        _bones[name] = pb;
    }

    public void Exclude(Rid body) { _rag.PhysicalBonesAddCollisionException(body); Body.Exclude = body; }

    void OnSkeletonUpdated()
    {
        if (Ragdolling)
        {
            int n = _skel.GetBoneCount();
            if (_lastRots == null || _lastRots.Length != n) _lastRots = new Quaternion[n];
            for (int b = 0; b < n; b++) _lastRots[b] = _skel.GetBonePoseRotation(b);
            if (_hips >= 0) _lastHipsWorld = _skel.GlobalTransform * _skel.GetBoneGlobalPose(_hips);
            if (_torso >= 0) _lastTorsoWorld = _skel.GlobalTransform * _skel.GetBoneGlobalPose(_torso);
            if (_head >= 0) _lastHeadPos = _skel.GlobalTransform * _skel.GetBoneGlobalPose(_head).Origin;
        }
        PoseReady?.Invoke();
    }

    /// <summary>Elin son (IK ve ragdoll sonrasi) dunya donusumu.</summary>
    public Transform3D HandWorld => _hand >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(_hand) : _skel.GlobalTransform;

    /// <summary>Kemigin son dunya donusumu (yalnizca PoseReady icinde gecerli son poz).</summary>
    public Transform3D BoneWorld(string bone)
    {
        int b = _skel.FindBone(bone);
        return b >= 0 ? _skel.GlobalTransform * _skel.GetBoneGlobalPose(b) : _skel.GlobalTransform;
    }

    /// <summary>Kemigin dunya konumu (fizik karesinde: onceki karenin pozu).</summary>
    public Vector3 BoneOrigin(string bone) => BoneWorld(bone).Origin;

    /// <summary>Iskeletin dunya olcegi (model 1,6 m'ye olceklenir; el soketi uzunluklari bununla carpilir).</summary>
    public float Scale => _skel.GlobalBasis.Scale.X;

    /// <summary>Fizik karesinde katman girdileri. aiming: Capa kullaniliyor ya da silah takili gece (govde donusu icin).
    /// Kollar RightGoal/LeftGoal'dan gelir (Player.Grip.cs).</summary>
    public void Update(float dt, bool aiming, Vector3 aimPoint, Node3D visual, in BodyState st)
    {
        if (Ragdolling)
        {
            _arm.Influence = 0; _armL.Influence = 0; _alignR.Weight = 0; _alignL.Weight = 0; _look.Influence = 0;
            _armW = _armLW = _carryW = 0;
            Body.FootIkWanted = false; Body.Speed = 0; Body.PushTarget = 0; Body.WadeTarget = 0;
            return;
        }
        bool posed = st.Posed;
        _armW = Mathf.MoveToward(_armW, RightGoal != null && !posed ? 1f : 0f, dt * ArmBlend);
        _armLW = Mathf.MoveToward(_armLW, LeftGoal != null && !posed ? 1f : 0f, dt * ArmBlend);
        _lookW = Mathf.MoveToward(_lookW, posed ? 0f : 1f, dt * 3f);
        _carryW = Mathf.MoveToward(_carryW, RightCarry != null && !posed ? CarryWeight : 0f, dt * ArmBlend);
        _arm.Influence = _armW; _alignR.Weight = Mathf.Max(_armW, _carryW);
        _armL.Influence = _armLW; _alignL.Weight = _armLW;
        _look.Influence = _lookW * 0.9f;
        // Dirsekler asagi ve disari (sag kolda modelin -X'i, solda +X'i).
        var side = visual.GlobalBasis.X;
        // Elin yonu: nisan hedefinden, yoksa alcak hazir tutustan; gecis IK agirligiyla harmanlanir.
        if (RightCarry is Basis c) _alignBasis = c.Orthonormalized();
        if (RightGoal is Transform3D r)
        {
            HandTarget.GlobalTransform = r;
            var gq = r.Basis.Orthonormalized().GetRotationQuaternion();
            _alignBasis = RightCarry is Basis c2
                ? new Basis(c2.Orthonormalized().GetRotationQuaternion().Slerp(gq, _armW))
                : new Basis(gq);
        }
        _alignR.Target = _alignBasis;
        ElbowPole.GlobalPosition = BoneOrigin("RightArm") - side * 0.35f + Vector3.Down * 0.5f;
        if (LeftGoal is Transform3D l)
        {
            HandTargetL.GlobalTransform = l;
            _alignL.Target = l.Basis.Orthonormalized();
        }
        ElbowPoleL.GlobalPosition = BoneOrigin("LeftArm") + side * 0.35f + Vector3.Down * 0.5f;
        LookTarget.GlobalPosition = aimPoint;

        // Govde katmani girdileri, model uzayinda.
        var inv = visual.GlobalBasis.Orthonormalized().Inverse();
        var look = inv * (aimPoint - (visual.GlobalPosition + Vector3.Up * 1.4f));
        Body.LookYaw = posed || aiming ? 0f : Mathf.Atan2(look.X, look.Z);
        Body.LookPitch = posed ? 0f : Mathf.Atan2(look.Y, new Vector2(look.X, look.Z).Length());
        Body.Speed = st.Speed; Body.Sprint = st.Sprint; Body.OnFloor = st.OnFloor; Body.YawRate = st.YawRate;
        Body.AccelModel = inv * st.Accel;
        Body.Recoil = st.Recoil;
        Body.FootIkWanted = st.FootIk && !posed;
        Body.FloorY = st.FloorY;
        Body.PushTarget = st.Push; Body.WadeTarget = st.Wade;
    }

    /// <summary>Govdenin bu karedeki durumu (Player doldurur).</summary>
    public struct BodyState
    {
        public float Speed, YawRate, Recoil, FloorY, Push, Wade;
        public Vector3 Accel;
        public bool Sprint, OnFloor, FootIk, Posed;
    }

    public void StartRagdoll(Vector3 impulse)
    {
        Ragdolling = true;
        _rag.Active = true;
        _rag.PhysicalBonesStartSimulation();
        if (_bones.TryGetValue("Spine01", out var spine)) spine.ApplyCentralImpulse(impulse);
        if (_bones.TryGetValue("Hips", out var hips)) hips.ApplyCentralImpulse(impulse * 0.6f);
    }

    /// <summary>Kalkis bilgisi: kalcanin yerdeki noktasi, yuzustu mu, govdenin bakacagi yatay yon.</summary>
    public readonly struct Lying
    {
        public readonly Vector3 Hips; public readonly bool FaceDown; public readonly Vector3 Forward;
        public Lying(Vector3 hips, bool faceDown, Vector3 forward) { Hips = hips; FaceDown = faceDown; Forward = forward; }
    }

    /// <summary>
    /// Ragdoll'u bitirir ve yatis bilgisini verir. Gogus (Spine01) yere bakiyorsa yuzustu: bas yonune bakarak kalkar;
    /// sirtustuyse ayak yonune (kalkis kliplerinde yuzustu bas +Z'de, sirtustu bas -Z'de yatar; PlayerPoses).
    /// </summary>
    public Lying StopRagdoll()
    {
        _rag.PhysicalBonesStopSimulation();
        _rag.Active = false;
        Ragdolling = false;
        int seg = _torso >= 0 ? _torso : _hips;
        var segWorld = _torso >= 0 ? _lastTorsoWorld : _lastHipsWorld;
        var hw = segWorld.Basis.Orthonormalized() * _skel.GetBoneGlobalRest(seg).Basis.Orthonormalized().Inverse();
        var front = hw * new Vector3(0, 0, 1);
        var head = _head >= 0 ? (_lastHeadPos - _lastHipsWorld.Origin).Normalized() : hw * Vector3.Up;
        bool down = front.Y < 0f;
        if (Main.Args.ContainsKey("rigtest")) GD.Print($"GETUP front={front} head={head} faceDown={down}");
        var fwd = down ? head : -head;
        fwd.Y = 0;
        if (fwd.LengthSquared() < 1e-4f) fwd = _skel.GlobalBasis * new Vector3(0, 0, 1);
        return new Lying(_lastHipsWorld.Origin, down, fwd.Normalized());
    }

    /// <summary>Govde yeni yere ve yone tasindiktan sonra: ragdoll pozundan kalkis klibine harmanlar.</summary>
    public void BlendFromRagdoll(float seconds)
    {
        if (_lastRots == null || _hips < 0) return;
        var rots = (Quaternion[])_lastRots.Clone();
        // Kok kemik (kalca) iskelet uzayinda: yeni govde donusumune gore yeniden hesapla.
        var hipsLocal = _skel.GlobalTransform.AffineInverse() * _lastHipsWorld;
        rots[_hips] = hipsLocal.Basis.GetRotationQuaternion();
        Body.BlendFrom(rots, hipsLocal.Origin, seconds);
    }

    /// <summary>Ragdoll sirasinda kalcanin dunya konumu.</summary>
    public Vector3 HipsPosition => _bones.TryGetValue("Hips", out var h) ? h.GlobalPosition : _skel.GlobalPosition;
}
