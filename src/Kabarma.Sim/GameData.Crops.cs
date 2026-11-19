namespace Kabarma.Sim;

// Ekinler: crops.json (GDD 8).

public sealed partial class Genes
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
    // M2 (tarim): ICERIK 5 sutunlari, GDD 8.2
    /// <summary>Olgunlasma (buyume gunu); growSeconds = days x growDaySeconds.</summary>
    public float Days { get; set; }
    public string Band { get; set; } = "cayir";
    /// <summary>az / orta / cok (ICERIK 5 Su). "cok": su sever, 80 ustu nemde x1.</summary>
    public string Water { get; set; } = "orta";
    /// <summary>Hasat urunu; bos: ekin kimligi.</summary>
    public string? Product { get; set; }
    /// <summary>Hasatta Bereket kaybi; bos: harvestFertilityLoss.</summary>
    public float? FertilityLoss { get; set; }
    public bool Perennial { get; set; }
    public float RegrowDays { get; set; }
    public bool Dark { get; set; }
    public bool WardsPests { get; set; }
    /// <summary>Su basmis ekin (Pirinc, Kamis, Isil Nilufer): sig su karesi ister (Farm.SetFlooded).</summary>
    public bool Flooded { get; set; }
    /// <summary>Kabarik halinin ozelligi (ICERIK 5 son sutun); davranis Godot katmaninda, kimlik burada.</summary>
    public string? Trait { get; set; }
    /// <summary>Kabarik tek bitkinin verdigi urun (Pirinc 30, Kamis 20); 0: kuralsiz.</summary>
    public int SwollenYield { get; set; }
    /// <summary>Hasat basina Maya (Maya Cicegi 5).</summary>
    public int MayaPerHarvest { get; set; }

    public string ProductId => string.IsNullOrEmpty(Product) ? Id : Product!;
    public string SeedId => Id + "_seed";
    public bool WetLoving => Water == "cok";
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
    /// <summary>GDD 8.2 "Gun birimi": buyume gunu (sn).</summary>
    public float GrowDaySeconds { get; set; } = 37.5f;
    /// <summary>GDD 8.3 genler, ıslah, Alaca, zararli (GameData.Genetics.cs).</summary>
    public GeneticsRules Genetics { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("crops.json")]
    public CropsFile Crops { get; private set; } = new();

    public CropDef Crop(string id) => FindCrop(id) ?? throw new InvalidOperationException($"bilinmeyen ekin '{id}'");

    Dictionary<string, CropDef>? _cropIndex;
    List<CropDef>? _cropIndexOf;
    /// <summary>Ekin tanimi (sozluk onbellegi; liste degisirse yeniden kurulur). Tarla her tikte cagirir.</summary>
    public CropDef? FindCrop(string id)
    {
        var list = Crops.Crops;
        if (_cropIndex == null || !ReferenceEquals(_cropIndexOf, list) || _cropIndex.Count != list.Count)
        {
            _cropIndex = new Dictionary<string, CropDef>();
            foreach (var c in list) _cropIndex[c.Id] = c;
            _cropIndexOf = list;
        }
        return _cropIndex.TryGetValue(id, out var d) ? d : null;
    }
}
