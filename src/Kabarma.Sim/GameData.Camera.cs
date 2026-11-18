namespace Kabarma.Sim;

// Oyuncu kamerasi (oyuncu akisi): camera.json. PLAN §4 Kamera, URETIM §6 karar 21: birinci sahis yok; V iki kip
// arasinda gecer: normal (arkada ve ustte, fare tekerlegi yakinlastirir) ve yakin omuz (Astroneer tarzi).

/// <summary>
/// Bir kamera kipinin durusu. Pivot oyuncunun ayaklarindan Height kadar yukarida; kamera pivottan Side kadar saga,
/// oradan Distance kadar geriye (bakis yonunun tersine) gider. Fov dikey gorus acisi (derece).
/// </summary>
public sealed class CameraPoseDef
{
    public float Height { get; set; } = 1.55f;
    public float Side { get; set; } = 0.7f;
    public float Distance { get; set; } = 4.2f;
    public float Fov { get; set; } = 68f;
}

public sealed class CameraFile
{
    /// <summary>Normal kip; Distance tekerlekle ZoomMin..ZoomMax arasinda degisir, verideki deger baslangic.</summary>
    public CameraPoseDef Normal { get; set; } = new();
    /// <summary>Yakin omuz kipi; uzaklik sabit (tekerlek bu kipte yakinlastirmaz).</summary>
    public CameraPoseDef Shoulder { get; set; } = new() { Height = 1.62f, Side = 0.62f, Distance = 1.15f, Fov = 70f };
    public float ZoomMin { get; set; } = 2.2f;
    public float ZoomMax { get; set; } = 7.5f;
    /// <summary>Tekerlegin bir tiki (m).</summary>
    public float WheelStep { get; set; } = 0.5f;
    /// <summary>Yakinlastirmanin hedefe yaklasma hizi (1/sn, ustel).</summary>
    public float ZoomSmooth { get; set; } = 12f;
    /// <summary>Iki kip arasi gecis suresi (sn).</summary>
    public float TransitionSeconds { get; set; } = 0.35f;
    /// <summary>Carpisma kuresi yaricapi (m): kamera duvar ve kayanin arkasina gecmez, one cekilir.</summary>
    public float CollisionRadius { get; set; } = 0.2f;
    /// <summary>Engel kalkinca kameranin geri acilma hizi (1/sn, ustel). Engele girerken hemen cekilir.</summary>
    public float ReturnRate { get; set; } = 8f;
    /// <summary>Oyun basinda bakisin yataydan egimi (derece, eksi asagi).</summary>
    public float StartPitchDeg { get; set; } = -12.6f;

    /// <summary>Tekerlek: notches > 0 uzaklastirir, < 0 yakinlastirir; sonuc ZoomMin..ZoomMax.</summary>
    public float StepZoom(float distance, int notches) => Math.Clamp(distance + notches * WheelStep, ZoomMin, ZoomMax);

    /// <summary>Kayitli (ya da verideki) uzakligi gecerli araliga ceker.</summary>
    public float ClampZoom(float distance) => Math.Clamp(distance, ZoomMin, ZoomMax);

    /// <summary>Gecis egrisi: 0..1 dogrusal ilerleme -> yumusak baslayip yumusak biten agirlik.</summary>
    public static float Ease(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

public sealed partial class GameData
{
    [DataFile("camera.json")]
    public CameraFile Camera { get; private set; } = new();
}
