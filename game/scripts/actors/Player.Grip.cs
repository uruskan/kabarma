using Godot;

namespace Kabarma.Game;

/// <summary>
/// Capa'nin elde durusu. Iskeletin son pozu (IK, ragdoll sonrasi) yalnizca PlayerRig.PoseReady aninda okunabilir;
/// Capa o anda yerlesir. Uc durum: nisan (el + nisan yonu + geri tepme yayi), elde (ragdoll, poz, kalkis:
/// Capa ele bagli kalir ve elle birlikte serilir) ve sirtta (uyurken cantanin ustunde).
/// </summary>
public partial class Player
{
    Transform3D _capaGrip = Transform3D.Identity; // elin uzayinda Capa
    bool _capaInHand, _capaOnBack;
    Vector3 _aimCache;
    float _capaFromHand, _handHeight; // sinama: Capa'nin ele uzakligi, elin govde tabanina yuksekligi (son pozda)
    static readonly Transform3D CapaOnBack = new(new Basis(new Vector3(0, 0, 1), Mathf.DegToRad(80)), new Vector3(0.02f, -0.05f, 0.16f));

    /// <summary>Capa'nin su anki konumunu ele gore saklar: bundan sonra elle birlikte hareket eder.</summary>
    void CaptureCapaGrip()
    {
        if (Rig == null || _capa == null) return;
        _capaGrip = Rig.HandWorld.AffineInverse() * _capa.GlobalTransform;
        _capaInHand = true;
    }

    void OnPoseReady()
    {
        if (_capa == null || Rig == null) return;
        if (_capaOnBack && Rig.Body.HasBag)
        {
            _capa.GlobalTransform = Rig.BoneWorld("Backpack") * CapaOnBack;
            return;
        }
        var hand = Rig.HandWorld;
        _handHeight = hand.Origin.Y - GlobalPosition.Y;
        if (Down || Posed)
        {
            if (!_capaInHand) CaptureCapaGrip();
            _capa.GlobalTransform = hand * _capaGrip;
            _capaFromHand = _capa.GlobalPosition.DistanceTo(hand.Origin);
            return;
        }
        _capaInHand = false;
        // Nisan: konum elden (kol IK'si eli nisana goturur), yon nisandan; geri tepme yayi.
        var fwd = Facing || AimOverride != null ? (_aimCache - hand.Origin).Normalized() : Visual.GlobalBasis.Z;
        if (fwd.LengthSquared() < 0.01f) fwd = Visual.GlobalBasis.Z;
        _capa.GlobalPosition = hand.Origin + fwd * (0.12f - _recoil * RecoilPushBack) + Vector3.Up * _recoil * RecoilLift; // feel.json (Player.Feel.cs)
        var look = Basis.LookingAt(-fwd, Vector3.Up);
        _capa.GlobalBasis = look.Rotated(look.X, _recoil * RecoilTilt);
        _capaFromHand = _capa.GlobalPosition.DistanceTo(hand.Origin);
    }
}
