using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuncu, vurus hissi sayilari (capa akisi): geri tepme yayi ve kamera sarsintisi (GDD 4, feel.json recoil ve shake).
/// Player.cs bunlari kullanir; degerler Director.WireFeel ile gelir, F1'de degisince yenilenir.
/// </summary>
public partial class Player
{
    public float RecoilImpulse, RecoilStiffness, RecoilDamping, RecoilPushBack, RecoilLift, RecoilTilt;
    public float ShakeDecay, ShakeOffset;

    public void ApplyFeel(FeelFile f)
    {
        var r = f.Recoil;
        RecoilImpulse = r.Impulse; RecoilStiffness = r.Stiffness; RecoilDamping = r.Damping;
        RecoilPushBack = r.PushBack; RecoilLift = r.Lift; RecoilTilt = r.Tilt;
        ShakeDecay = f.Shake.DecayPerSecond; ShakeOffset = f.Shake.Offset;
    }
}
