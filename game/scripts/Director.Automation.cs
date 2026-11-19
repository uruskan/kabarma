namespace Kabarma.Game;

/// <summary>Director, otomasyon makineleri (M4 tasima): Paketleyici'nin rampayi yollamasi.</summary>
public partial class Director
{
    /// <summary>Paketleyici: rampadaki kasalari firlatir (oyuncunun satis menusuyle ayni yol; LaunchRamp).</summary>
    public void MachineLaunchRamp() => LaunchRamp();
}
