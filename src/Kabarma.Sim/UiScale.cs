namespace Kabarma.Sim;

/// <summary>
/// Arayuz olcegi kurallari (ui.json "scale"; SANAT-SES 8.1, M1 karari 20; basvuru Hollow-Universe UiScaler). Motordan
/// bagimsiz: Godot katmani (UiScaler) pencere boyunu verir, olcegi buradan alir.
/// </summary>
public static class UiScale
{
    /// <summary>
    /// Pencerenin olcegi: tasarim tuvalinden (1600 x 900) taban yukseklige (1080) dek 1, sonra yukseklikle buyur;
    /// tuvalden kucuk pencerede yerlesim sigsin diye kuculur.
    /// </summary>
    public static float Automatic(UiFile d, float width, float height)
    {
        var s = d.Scale;
        float fit = Math.Min(1f, Math.Min(height / d.Design.Height, width / d.Design.Width));
        return Math.Clamp(Math.Max(height / s.BaseHeight, fit), s.Min, s.Max);
    }

    /// <summary>Her panelin sigdigi en buyuk olcek (en genis satir, en uzun sutun).</summary>
    public static float Largest(UiFile d, float width, float height) =>
        Math.Max(d.Scale.Min, Math.Min(height / d.Scale.FitHeight, width / d.Scale.FitWidth));

    /// <summary>
    /// Kullanilan olcek: otomatik x oyuncu tercihi, kesirlere (ui.json "steps") yuvarlanir; ust sinir asagi yuvarlanir ki
    /// paneller hep sigsin.
    /// </summary>
    public static float For(UiFile d, float width, float height, float preference = 1f)
    {
        float steps = Math.Max(1f, d.Scale.Steps);
        float wanted = MathF.Round(Automatic(d, width, height) * preference * steps) / steps;
        float largest = MathF.Floor(Largest(d, width, height) * steps) / steps;
        return Math.Min(wanted, largest);
    }

    /// <summary>Canta izgarasinin sutun sayisi: secenekler icinde yuva sayisini tam bolen ilki; yoksa sonuncusu.</summary>
    public static int BagColumns(UiFile d, int slots)
    {
        var opts = d.Inventory.BagColumns;
        foreach (int c in opts) if (c > 0 && slots % c == 0) return c;
        return opts.Count > 0 ? opts[^1] : 6;
    }
}
