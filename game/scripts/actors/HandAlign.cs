using Godot;

namespace Kabarma.Game;

/// <summary>
/// El kemiginin dunya yonunu hedefe ceviren iskelet degistiricisi. TwoBoneIK3D bilegi hedef noktaya goturur ama
/// elin yonunu hedefe gore cevirmez; tutulan nesnenin eli izlemesi icin el yonu de hedeften gelmeli
/// (Player.Grip.cs). Kol IK'sinin hemen ardindan calisir; agirlik IK'ninkiyle ayni.
/// </summary>
public partial class HandAlign : SkeletonModifier3D
{
    public string BoneName = "RightHand";
    /// <summary>Elin istenen dunya yonu (dik birim eksenler).</summary>
    public Basis Target = Basis.Identity;
    public float Weight;
    int _bone = -2;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var s = GetSkeleton();
        if (s == null || Weight <= 1e-3f) return;
        if (_bone == -2) _bone = s.FindBone(BoneName);
        if (_bone < 0) return;
        var cur = s.GetBoneGlobalPose(_bone).Basis.Orthonormalized();
        // Iskelet uzayinda istenen yon (iskeletin dunya olcegi ayiklanir).
        var want = s.GlobalBasis.Orthonormalized().Inverse() * Target;
        var q = cur.GetRotationQuaternion().Slerp(want.Orthonormalized().GetRotationQuaternion(), Mathf.Clamp(Weight, 0f, 1f));
        int p = s.GetBoneParent(_bone);
        var pg = p >= 0 ? s.GetBoneGlobalPose(p).Basis.Orthonormalized() : Basis.Identity;
        s.SetBonePoseRotation(_bone, (pg.Inverse() * new Basis(q)).GetRotationQuaternion());
    }
}
