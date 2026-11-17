using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Oyuncunun kodla animasyon katmanlari (SANAT-SES §3.1):
///  - Kol IK'si: sag kol Capa'yi nisan noktasina dogru tutar (TwoBoneIK3D).
///  - Bakis: bas nisan yonune doner, acisi sinirli (LookAtModifier3D).
///  - Ragdoll: bayilinca govde fizikle serilir, kalkinca klibe doner (PhysicalBoneSimulator3D).
/// </summary>
public sealed class PlayerRig
{
    readonly Skeleton3D _skel;
    readonly TwoBoneIK3D _arm;
    readonly LookAtModifier3D _look;
    readonly PhysicalBoneSimulator3D _rag;
    public readonly Node3D HandTarget, ElbowPole, LookTarget;
    readonly Dictionary<string, PhysicalBone3D> _bones = new();
    float _armW, _lookW;
    public bool Ragdolling { get; private set; }

    public PlayerRig(Node3D owner, Skeleton3D skel)
    {
        _skel = skel;
        HandTarget = new Node3D { Name = "HandTarget", TopLevel = true };
        ElbowPole = new Node3D { Name = "ElbowPole", TopLevel = true };
        LookTarget = new Node3D { Name = "LookTarget", TopLevel = true };
        owner.AddChild(HandTarget); owner.AddChild(ElbowPole); owner.AddChild(LookTarget);

        _arm = new TwoBoneIK3D { Name = "ArmIK", Influence = 0f };
        skel.AddChild(_arm);
        _arm.SettingCount = 1;
        _arm.SetRootBoneName(0, "RightArm");
        _arm.SetMiddleBoneName(0, "RightForeArm");
        _arm.SetEndBoneName(0, "RightHand");
        _arm.SetTargetNode(0, _arm.GetPathTo(HandTarget));
        _arm.SetPoleNode(0, _arm.GetPathTo(ElbowPole));

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

        _rag = new PhysicalBoneSimulator3D { Name = "Ragdoll" };
        skel.AddChild(_rag);
        // Iskelet 0,01 olcekli ama fizik govdeleri olcegi atar: sekil boyutlari metre (asagida /100).
        AddBone("Hips", 14f, 18f);
        AddBone("Spine01", 14f, 22f);
        AddBone("Head", 15f, 10f);
        AddBone("LeftUpLeg", 7f, 30f); AddBone("LeftLeg", 6f, 30f);
        AddBone("RightUpLeg", 7f, 30f); AddBone("RightLeg", 6f, 30f);
        AddBone("LeftArm", 5f, 18f); AddBone("LeftForeArm", 5f, 18f);
        AddBone("RightArm", 5f, 18f); AddBone("RightForeArm", 5f, 18f);
        _rag.Active = false;
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
        float r = radius / 100f, l = length / 100f;
        pb.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = r, Height = Mathf.Max(l, r * 2.1f) },
            Position = new Vector3(0, l * 0.5f, 0),
        });
        _rag.AddChild(pb);
        _bones[name] = pb;
    }

    public void Exclude(Rid body) => _rag.PhysicalBonesAddCollisionException(body);

    /// <summary>Kol, bas ve parmak ucu hedeflerini gunceller. aiming: Capa kullaniliyor ya da silah takili gece.</summary>
    public void Update(float dt, bool aiming, Vector3 aimPoint, Node3D body)
    {
        if (Ragdolling) { _arm.Influence = 0; _look.Influence = 0; return; }
        _armW = Mathf.MoveToward(_armW, aiming ? 1f : 0f, dt * 6f);
        _lookW = Mathf.MoveToward(_lookW, 1f, dt * 3f);
        _arm.Influence = _armW;
        _look.Influence = _lookW * 0.9f;
        var shoulder = _skel.GlobalTransform * _skel.GetBoneGlobalPose(_skel.FindBone("RightArm")).Origin;
        var dir = (aimPoint - shoulder).Normalized();
        // El omzun 0,45 m onunde, nisan yonunde; dirsek asagi ve disari.
        HandTarget.GlobalPosition = shoulder + dir * 0.45f + Vector3.Down * 0.05f;
        HandTarget.GlobalBasis = Basis.LookingAt(dir, Vector3.Up);
        ElbowPole.GlobalPosition = shoulder + body.GlobalBasis.X * -0.35f + Vector3.Down * 0.5f;
        LookTarget.GlobalPosition = aimPoint;
    }

    public void StartRagdoll(Vector3 impulse)
    {
        Ragdolling = true;
        _rag.Active = true;
        _rag.PhysicalBonesStartSimulation();
        if (_bones.TryGetValue("Spine01", out var spine)) spine.ApplyCentralImpulse(impulse);
        if (_bones.TryGetValue("Hips", out var hips)) hips.ApplyCentralImpulse(impulse * 0.6f);
    }

    public void StopRagdoll()
    {
        _rag.PhysicalBonesStopSimulation();
        _rag.Active = false;
        Ragdolling = false;
    }

    /// <summary>Ragdoll sirasinda kalcanin dunya konumu (kalkinca govde oraya tasinir).</summary>
    public Vector3 HipsPosition => _bones.TryGetValue("Hips", out var h) ? h.GlobalPosition : _skel.GlobalPosition;
}
