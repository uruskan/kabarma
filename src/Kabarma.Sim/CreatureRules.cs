using System.Numerics;

namespace Kabarma.Sim;

/// <summary>
/// Koc Gidak'in tekmesi (PLAN §1.4: "birini citin ustunden tekmeliyor"): yatayda duz, dikeyde parabol.
/// Yukseklik, baslangic ve inis zemini arasindaki dogrunun ustune eklenir. Durum duz alanlardir (kayda hazir, TEKNIK §9).
/// </summary>
public readonly struct KickArc
{
    public Vector3 From { get; init; }
    public Vector3 To { get; init; }
    public float Apex { get; init; }
    public float Seconds { get; init; }

    /// <summary>Yatay yon (birim) ve veriden menzil, tepe, sure. Inis yuksekligi cagirandan (zemin) gelir.</summary>
    public static KickArc Toward(CreatureDef kicker, Vector3 from, Vector3 dirXZ, float landY)
    {
        var d = new Vector3(dirXZ.X, 0, dirXZ.Z);
        d = d.LengthSquared() > 1e-6f ? Vector3.Normalize(d) : Vector3.UnitZ;
        var to = from + d * kicker.KickDistance;
        to.Y = landY;
        return new KickArc { From = from, To = to, Apex = kicker.KickApex, Seconds = kicker.KickFlightSeconds };
    }

    /// <summary>Ucusun orani 0..1.</summary>
    public float Progress(float elapsed) => Seconds > 0 ? Math.Clamp(elapsed / Seconds, 0f, 1f) : 1f;

    public Vector3 At(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        var p = Vector3.Lerp(From, To, u);
        p.Y += 4f * Apex * u * (1f - u);
        return p;
    }

    /// <summary>Baslangictan yatayda <paramref name="dist"/> metre otedeki yukseklik (baslangic zeminine gore).</summary>
    public float HeightAtDistance(float dist)
    {
        var flat = new Vector2(To.X - From.X, To.Z - From.Z).Length();
        if (flat <= 1e-4f) return 0f;
        return At(dist / flat).Y - From.Y;
    }
}

/// <summary>Zipzip'in gunduz surusu kurallari (URETIM §6 M1, GDD §8.1): 2-3'luk urkek gruplar, olgun ekine gelir.</summary>
public static class Herds
{
    /// <summary>Her grubun boyu [GroupMin, GroupMax].</summary>
    public static List<int> GroupSizes(DayHerdDef d, Rng rng) =>
        Enumerable.Range(0, d.Groups).Select(_ => rng.Range(d.GroupMin, d.GroupMax + 1)).ToList();

    /// <summary>Urkek: oyuncu FleeRadius'tan yakinsa kacar.</summary>
    public static bool ShouldFlee(DayHerdDef d, float playerDistance) => playerDistance < d.FleeRadius;

    /// <summary>Gelecegi ekin: ekili, Olgun, kabarmamis (GDD §8.1: zararli olgun ekine gelir).</summary>
    public static bool Attracts(Plot p) => p.Planted && p.Stage == CropStage.Mature && p.Swollen == null;

    /// <summary>Surular yalnizca gunduz cayirdadir; Kabarma gecesi yabani yaratiklar dalga olarak gelir (GDD §14.2).</summary>
    public static bool Out(bool nightActive) => !nightActive;
}
