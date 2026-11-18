namespace Kabarma.Sim;

// Arastirma, Tarayici ve ambardan uretim (GDD §5, §12; ICERIK §0, §11.1, §20). Veri: research.json.

/// <summary>Arastirma agacinin bir dugumu: arastirilinca Known'a "arastirma:&lt;id&gt;" girer ve o kilitli tarifler acilir.</summary>
public sealed class ResearchNodeDef
{
    public string Id { get; set; } = "";
    /// <summary>Basamak (bedel costByTier[tier]).</summary>
    public int Tier { get; set; } = 1;
    /// <summary>Once arastirilmasi gereken dugumler.</summary>
    public List<string> Requires { get; set; } = new();
}

/// <summary>Taranabilir tur (GDD §4 Tarayici): kind creature / plant / mineral.</summary>
public sealed class ScanEntryDef
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public int Veri { get; set; }
}

public sealed class ScanDef
{
    public float Range { get; set; } = 14f;
    public float Pick { get; set; } = 3f;
    public List<ScanEntryDef> Entries { get; set; } = new();
}

/// <summary>Tarifi olmayan ham kaynak ve elde edildigi basamak (RecipeGraph).</summary>
public sealed class RawSourceDef
{
    public string Id { get; set; } = "";
    public int Tier { get; set; } = 1;
    public string By { get; set; } = "";
}

public sealed class ResearchFile
{
    /// <summary>Arastirmanin yapildigi istasyon (Arastirma Masasi).</summary>
    public string Table { get; set; } = "";
    /// <summary>Basamaga gore Veri bedeli; indeks basamaktir (ICERIK §0).</summary>
    public List<int> CostByTier { get; set; } = new();
    public List<ResearchNodeDef> Nodes { get; set; } = new();
    public int OcakTierStart { get; set; } = 1;
    /// <summary>Basamaga cikaran Cekirdek esyasi (indeks basamak; Ata odulu, atalar.json).</summary>
    public List<string> TierCores { get; set; } = new();
    /// <summary>Tapu yaricapi (m), basamak 1'den baslayarak (GDD §6).</summary>
    public List<float> ClaimRadius { get; set; } = new();
    /// <summary>El tarifleri de Sandiktan alir mi (GDD §5: hayir).</summary>
    public bool HandFromStorage { get; set; }
    public int StationQueueMax { get; set; } = 8;
    /// <summary>Guc isteyen istasyonlar (W).</summary>
    public Dictionary<string, int> Watts { get; set; } = new();
    public ScanDef Scan { get; set; } = new();
    public List<RawSourceDef> RawSources { get; set; } = new();

    public const string ResearchPrefix = "arastirma:";
    public const string TierPrefix = "ocak:";

    public ResearchNodeDef? Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public int CostOf(ResearchNodeDef n) => n.Tier >= 0 && n.Tier < CostByTier.Count ? CostByTier[n.Tier] : int.MaxValue;
    public float ClaimRadiusAt(int tier) => ClaimRadius.Count == 0 ? 40f : ClaimRadius[Math.Clamp(tier - 1, 0, ClaimRadius.Count - 1)];

    /// <summary>Tarifin arastirma dugumu (unlockOn "arastirma:x"); yoksa null.</summary>
    public ResearchNodeDef? NodeFor(RecipeDef r) =>
        r.UnlockOn != null && r.UnlockOn.StartsWith(ResearchPrefix) ? Node(r.UnlockOn[ResearchPrefix.Length..]) : null;

    /// <summary>Tarifin Ocak basamagi kilidi (unlockOn "ocak:n"); yoksa 0.</summary>
    public static int TierLock(RecipeDef r) =>
        r.UnlockOn != null && r.UnlockOn.StartsWith(TierPrefix) && int.TryParse(r.UnlockOn[TierPrefix.Length..], out int t) ? t : 0;
}

public sealed partial class GameData
{
    [DataFile("research.json")]
    public ResearchFile Research { get; private set; } = new();
}
