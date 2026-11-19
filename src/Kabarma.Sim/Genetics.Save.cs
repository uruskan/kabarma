namespace Kabarma.Sim;

// tarim (M2): genler, Tohum Kaydi, Islah notlari ve karelerin M2 alanlari icin kayit bolumu (TEKNIK 9.1).
// "farm" bolumu (kayit akisi) kareleri kurar; bu bolum ondan sonra okunur ve ayni karelere M2 alanlarini yazar.
// Korkuluk ve kovan konumlari kaydedilmez: Godot katmani yerlestirilebilirlerden saniyede bir yeniden yazar.

public sealed class PlotGeneticsSave
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool Wild { get; set; }
    public string? Alaca { get; set; }
    public float PestDamage { get; set; }
    public string? PrevFamily { get; set; }
    public bool RotationBonus { get; set; }
    public bool Regrowing { get; set; }
    public float RegrowT { get; set; }
    public bool Greenhouse { get; set; }
    public bool Flooded { get; set; }
}

/// <summary>
/// Secili tohum (Farm.Selected) kaydedilmez: oyuncu basinadir ve bu bolum ortak oyunda kayit koprusuyle host'tan her
/// istemciye gider (NetSync.Save.cs); kaydedilse host'un secimi istemcininkini ezerdi.
/// </summary>
public sealed class FarmGeneticsSave
{
    public SeedLedger Seeds { get; set; } = new();
    public BreedingLog Log { get; set; } = new();
    public float Time { get; set; }
    public List<PlotGeneticsSave> Plots { get; set; } = new();
}

public sealed partial class Farm
{
    public FarmGeneticsSave SaveGenetics() => new()
    {
        Seeds = Seeds, Log = Log, Time = Time,
        Plots = _plots.Values.OrderBy(p => p.X).ThenBy(p => p.Y)
            .Where(p => p.Wild || p.Alaca != null || p.PestDamage > 0 || p.PrevFamily != null || p.RotationBonus || p.Regrowing || p.RegrowT > 0 || p.Greenhouse || p.Flooded)
            .Select(p => new PlotGeneticsSave
            {
                X = p.X, Y = p.Y, Wild = p.Wild, Alaca = p.Alaca, PestDamage = p.PestDamage, PrevFamily = p.PrevFamily,
                RotationBonus = p.RotationBonus, Regrowing = p.Regrowing, RegrowT = p.RegrowT, Greenhouse = p.Greenhouse, Flooded = p.Flooded,
            }).ToList(),
    };

    /// <summary>Sessiz yukleme: olay yaymaz. Kareler "farm" bolumunden once kurulmus olmali.</summary>
    public void LoadGenetics(FarmGeneticsSave s)
    {
        Seeds = s.Seeds ?? new SeedLedger();
        Log = s.Log ?? new BreedingLog();
        Time = s.Time;
        foreach (var e in s.Plots ?? new())
        {
            var p = Get(e.X, e.Y);
            if (p == null) continue;
            p.Wild = e.Wild; p.Alaca = e.Alaca; p.PestDamage = e.PestDamage; p.PrevFamily = e.PrevFamily;
            p.RotationBonus = e.RotationBonus; p.Regrowing = e.Regrowing; p.RegrowT = e.RegrowT; p.Greenhouse = e.Greenhouse; p.Flooded = e.Flooded;
        }
        _protT = float.MaxValue; // koruma bir sonraki tikte yeniden hesaplanir
    }
}

public sealed partial class GameState
{
    [SaveSection("farm.genetics", 1, Order = SaveOrder.State + 10)]
    FarmGeneticsSave FarmGeneticsSection { get => Farm.SaveGenetics(); set => Farm.LoadGenetics(value); }
}
