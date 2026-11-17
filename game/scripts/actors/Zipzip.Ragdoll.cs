using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Gunduz avlanan Zipzip ragdoll olur (SANAT-SES §4: "Olum animasyonu yoktur. Kabarmis yaratik patlar; gunduz avlanan
/// yaratik ragdoll olur"). Kemikler creatures.json rig.ragdoll'dan (rol -> kapsul yaricapi, iskelet boyu orani);
/// kapsul boyu kemikten cocuguna olan uzakliktir. Oyuncu ragdoll'u (PlayerRig) ile ayni yontem. Sure sonunda soner.
/// </summary>
public partial class Zipzip
{
    PhysicalBoneSimulator3D _ragSim;
    float _ragT = -1f;
    readonly List<PhysicalBone3D> _ragBones = new();

    bool StartRagdoll(Vector3 knockDir)
    {
        var skel = _rig?.Skeleton;
        var rig = Def.Rig;
        if (skel == null || rig == null || rig.Ragdoll.Count == 0 || !IsInsideTree()) return false;
        _rig.Player?.Stop(true);
        _ragSim = new PhysicalBoneSimulator3D { Name = "Ragdoll" };
        skel.AddChild(_ragSim);
        float size = (rig.Height > 0 ? rig.Height : 0.75f) * _body.Scale.Y;
        var xf = skel.GlobalTransform;
        foreach (var (role, radiusFrac) in rig.Ragdoll)
        {
            if (!rig.Bones.TryGetValue(role, out var bn)) continue;
            int bi = PoseLibrary.FindBone(skel, bn);
            if (bi < 0) continue;
            var pose = skel.GetBoneGlobalPose(bi);
            // Kemik yonu ve boyu: ilk cocuga dogru (yoksa yaricapin iki kati).
            int child = -1;
            foreach (int c in skel.GetBoneChildren(bi)) { child = c; break; }
            var a = xf * pose.Origin;
            var b = child >= 0 ? xf * skel.GetBoneGlobalPose(child).Origin : a + Vector3.Up * radiusFrac * size * 2f;
            float len = Mathf.Max(a.DistanceTo(b), radiusFrac * size * 2.1f);
            var dirLocal = (pose.Basis.Orthonormalized().Inverse() * (skel.GetBoneGlobalPose(child >= 0 ? child : bi).Origin - pose.Origin)).Normalized();
            if (dirLocal.LengthSquared() < 0.5f) dirLocal = Vector3.Up;
            var pb = new PhysicalBone3D
            {
                Name = "PB_" + role, Mass = 0.6f,
                JointType = PhysicalBone3D.JointTypeEnum.Cone, LinearDamp = 0.4f, AngularDamp = 2f, Friction = 0.9f,
                CollisionLayer = 1u << 2, CollisionMask = 1u,
            };
            pb.Set("bone_name", skel.GetBoneName(bi));
            var shape = new CollisionShape3D { Shape = new CapsuleShape3D { Radius = radiusFrac * size, Height = len } };
            shape.Position = dirLocal * len * 0.5f;
            // Kapsulun Y ekseni kemik yonune
            var up = Vector3.Up;
            if (Mathf.Abs(dirLocal.Dot(up)) < 0.999f) shape.Basis = new Basis(new Quaternion(up, dirLocal));
            pb.AddChild(shape);
            _ragSim.AddChild(pb);
            _ragBones.Add(pb);
        }
        if (_ragBones.Count == 0) { _ragSim.QueueFree(); _ragSim = null; return false; }
        _ragSim.Active = true;
        _ragSim.PhysicalBonesStartSimulation();
        var push = (knockDir.LengthSquared() > 1e-4f ? knockDir.Normalized() : -GlobalBasis.Z) * 1.2f + Vector3.Up * 1.4f;
        foreach (var pb in _ragBones) pb.ApplyCentralImpulse(push * pb.Mass);
        _ragT = Def.Day?.RagdollSeconds ?? 5f;
        All.Remove(this);
        return true;
    }

    /// <summary>Ragdoll suruyorsa true: serili kalir, son saniyede kuculup kaybolur.</summary>
    bool TickRagdoll(float dt)
    {
        if (_ragT < 0f) return false;
        _ragT -= dt;
        if (_ragT < 1f) _body.Scale = Vector3.One * Mathf.Max(0.01f, _ragT) * Scale0;
        if (_ragT <= 0f)
        {
            Fx.Burst(_ragBones.Count > 0 ? _ragBones[0].GlobalPosition : GlobalPosition, Palette.Ground.Lerp(Palette.Accent, 0.35f), 8, 2f, 0.08f, 0.5f);
            QueueFree();
        }
        return true;
    }
}
