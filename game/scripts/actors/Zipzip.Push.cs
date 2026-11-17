using Godot;

namespace Kabarma.Game;

/// <summary>
/// Zipzip parcasi (capa akisi ekledi; yaratik akisinin sinifi): hasarsiz itme. Sulama basligi kucuk dusmani iter
/// (ICERIK 6.1). Itme, vurustaki geri itmeyle ayni yoldan (_knock) isler.
/// </summary>
public partial class Zipzip
{
    public void Push(Vector3 dir, float speed)
    {
        if (Dead || dir.LengthSquared() < 0.0001f) return;
        _knock += dir.Normalized() * speed;
    }
}
