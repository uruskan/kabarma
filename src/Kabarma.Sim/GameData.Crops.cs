namespace Kabarma.Sim;

// Ekinler: crops.json (GDD 8).

public sealed class Genes
{
    public int V { get; set; } = 1;
    public int H { get; set; } = 1;
    public int D { get; set; } = 1;
    public int M { get; set; } = 1;
}

public sealed class CropDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Family { get; set; } = "";
    public float GrowSeconds { get; set; }
    public int BasePrice { get; set; }
    public int BaseYield { get; set; } = 1;
    public int SeedReturn { get; set; }
    public Genes SeedGenes { get; set; } = new();
    public float MoistureDrainPerSecond { get; set; }
    public float WaterPerShot { get; set; }
}

public sealed class MoistureRules
{
    public float StopBelow { get; set; }
    public float HalfBelow { get; set; }
    public float WetAbove { get; set; }
    public float WetFactor { get; set; }
}

public sealed class SizeClassDef
{
    public string Id { get; set; } = "";
    public float MinScore { get; set; }
    public int Kg { get; set; }
    public int PriceMult { get; set; }
    public float Scale { get; set; }
}

public sealed class CropsFile
{
    public List<CropDef> Crops { get; set; } = new();
    public MoistureRules Moisture { get; set; } = new();
    public float StartFertility { get; set; }
    public List<float> QualityMult { get; set; } = new() { 1f, 1f, 1f };
    public float HarvestFertilityLoss { get; set; } = 10f;
    public List<SizeClassDef> SizeClasses { get; set; } = new();
    /// <summary>GDD 8.1: egim bundan az olmali (derece).</summary>
    public float MaxSlopeDegrees { get; set; }
    /// <summary>GDD 8.2: 3 yildiz icin en az Bereket.</summary>
    public float Star3MinFertility { get; set; }
    /// <summary>GDD 8.4: safaktan sonra bu kadar saniye toplanmayan kabarik urun soner (1 oyun gunu, GDD 2).</summary>
    public float DeflateAfterSeconds { get; set; }
    /// <summary>GDD 8.4: sonen (ya da bicilen) kabarik urun "iki kat normal urune doner".</summary>
    public float DeflateYieldMult { get; set; } = 1f;
}

public sealed partial class GameData
{
    [DataFile("crops.json")]
    public CropsFile Crops { get; private set; } = new();

    public CropDef Crop(string id) => Crops.Crops.First(c => c.Id == id);
}
