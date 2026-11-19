namespace Kabarma.Sim;

// Genler, ıslah, Alaca, gunduz zararlisi: crops.json "genetics" (GDD 8.2, 8.3).

public sealed class CropMutationStep
{
    public int Delta { get; set; }
    public float P { get; set; }
}

public sealed class AlacaDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Olasilik 1/Odds.</summary>
    public int Odds { get; set; }
    public int PriceMult { get; set; } = 1;
}

public sealed class WildRules
{
    public int PerCrop { get; set; }
    public float RingMin { get; set; }
    public float RingMax { get; set; }
    public float RegrowDays { get; set; }
    /// <summary>Agac ve ot tutamindan en az bu kadar uzakta (m): odun ve lif bicen yanlislikla yabani bitkiyi bicmez.</summary>
    public float Clearance { get; set; }
    /// <summary>Bicme yabani bitkiyi yalniz nisan bu kadar yakinsa alir (m): bilerek toplanir.</summary>
    public float MowRadius { get; set; }
}

/// <summary>Sera (ICERIK 5 bant kurali, 11 Sera Kontrol Unitesi): seradaki kare biyom bandindan bagimsiz buyur. Su basmis ekin yine sig su ister.</summary>
public sealed class GreenhouseRules
{
    public float BandFactor { get; set; } = 1f;
    /// <summary>Su basmis (flooded) ekin sig su olmayan karede bu carpanla buyur.</summary>
    public float FloodedOutOfWater { get; set; } = 0.25f;
    /// <summary>Sera Kontrol Unitesinin etkiledigi tarla yaricapi (m).</summary>
    public float Radius { get; set; } = 5f;
}

/// <summary>Asi Tezgahi (ICERIK 11, 200 W): iki tohum kaydi arasinda gen aktarma.</summary>
public sealed class GraftRules
{
    public float Seconds { get; set; } = 8f;
    /// <summary>Bir asida alicinin geni en fazla bu kadar artar (vericinin genini asmaz).</summary>
    public int MaxStep { get; set; } = 2;
    public bool SameFamily { get; set; } = true;
    public int MinGene { get; set; } = 1;
}

public sealed class GeneticsRules
{
    public int GeneMin { get; set; } = 1;
    public int GeneMax { get; set; } = 10;
    public List<CropMutationStep> Mutation { get; set; } = new();
    /// <summary>Mutasyon sansi carpani (dolap: Arici x1,5). Oyun kurulurken Director ayarlar; veri dosyasinda yok.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public float MutationMult { get; set; } = 1f;
    public int WildMin { get; set; } = 1;
    public int WildMax { get; set; } = 3;
    public float CrossRadius { get; set; }
    public float PollinatorRadius { get; set; }
    public List<AlacaDef> Alaca { get; set; } = new();
    public float AlacaSeedKeep { get; set; }
    public float DMoistureShift { get; set; }
    public float OutOfBand { get; set; } = 0.25f;
    public float DBandBonus { get; set; }
    public Dictionary<string, string> Bands { get; set; } = new();
    public string FarmBand { get; set; } = "";
    public float BiteDamage { get; set; }
    public float DBiteReduce { get; set; }
    public float ScarecrowRadius { get; set; }
    public int WardCells { get; set; }
    public float RotationBonus { get; set; }
    public float FallowPerDay { get; set; }
    public float RegrowFrom { get; set; } = 0.6f;
    public WildRules Wild { get; set; } = new();
    public GreenhouseRules Greenhouse { get; set; } = new();
    public GraftRules Graft { get; set; } = new();
    public int MaxNotes { get; set; } = 30;

    public AlacaDef? AlacaOf(string? id) => id == null ? null : Alaca.FirstOrDefault(a => a.Id == id);
}
