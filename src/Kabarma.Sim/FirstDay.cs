namespace Kabarma.Sim;

/// <summary>
/// Ilk gunun ogretici kurallari (GDD 24, yalniz ilk gun). Agil ve Gidak kurallari CoopRules'ta (Coop.cs).
/// </summary>
public static class FirstDayRules
{
    /// <summary>
    /// GDD 24: "Gece 7. adim bitince baslar; ekinin olgun olmasi gerekmez. Kucuk Kabarma evresine bakmadan butun ekinleri
    /// ... kabartir." Gece basinda ekili butun kareleri olgun sayar; Kabarma ani (Farm.SwellMature) hepsini kabartir.
    /// Donus: olgunlastirilan kare sayisi.
    /// </summary>
    public static int RipenForFirstNight(Farm farm)
    {
        int n = 0;
        foreach (var p in farm.Plots)
        {
            if (!p.Planted || p.Growth >= 1f) continue;
            p.Growth = 1f;
            n++;
        }
        return n;
    }
}
