namespace Kabarma.Sim;

/// <summary>
/// Hasat Raporu (GDD 14.1: safakta acilir). Alanlar GDD'nin gece kurallarindan: Maya (14.4), sondurulen kabarik,
/// Gosteri tepesi (14.5), Oz, seviye ve kartlar (15), kabaran ekin boy sinifina gore (8.4), yenen ekin (14.4),
/// kabaran hayvan (9.5), yere dusme (3.6), kargo satisi (13). Belge raporun alan listesini ayrica yazmaz.
/// </summary>
public sealed class HarvestReport
{
    public int NightNumber { get; init; }
    public int Ks { get; init; }
    public float Seconds { get; init; }
    public int WavesCleared { get; init; }
    public int WavesTotal { get; init; }
    public int Kills { get; init; }
    public float Maya { get; init; }
    public float Oz { get; init; }
    public int Level { get; init; }
    public int MaxLevel { get; init; }
    public IReadOnlyList<TakenCard> Cards { get; init; } = Array.Empty<TakenCard>();
    public float PeakGosteri { get; init; }
    public float DamageDealt { get; init; }
    public IReadOnlyDictionary<string, int> SwollenBySize { get; init; } = new Dictionary<string, int>();
    public int SwollenTotal => SwollenBySize.Values.Sum();
    public int CropsEaten { get; init; }
    public int AnimalsSwollen { get; init; }
    public int PlayerDowns { get; init; }
    public int SoldCredits { get; init; }

    public static HarvestReport From(KabarmaNight n, int nightNumber, int soldCredits) => new()
    {
        NightNumber = nightNumber,
        Ks = n.Ks,
        Seconds = n.TotalTime,
        WavesCleared = Math.Clamp(n.WaveIndex + 1, 0, n.Def.Waves.Count),
        WavesTotal = n.Def.Waves.Count,
        Kills = n.Kills,
        Maya = n.Maya,
        Oz = n.Oz,
        Level = n.Level,
        MaxLevel = n.Def.MaxCardLevels,
        Cards = n.CardsAtDawn,
        PeakGosteri = n.PeakGosteri,
        DamageDealt = n.DamageDealt,
        SwollenBySize = new Dictionary<string, int>(n.SwollenBySize),
        CropsEaten = n.CropsEaten,
        AnimalsSwollen = n.AnimalsSwollen,
        PlayerDowns = n.PlayerDowns,
        SoldCredits = soldCredits,
    };
}
