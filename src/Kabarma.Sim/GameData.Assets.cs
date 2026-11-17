namespace Kabarma.Sim;

// Varlik kaydi: assets.json (URETIM §3 yer tutucu isareti, §4 stil kilidi; SANAT-SES §9 sekiz olcut).
// Oyun kurali degil; uretim hattinin ve cikis kapisinin kaydi. pipeline/run_golden.py betik alanlarini yazar.

/// <summary>Varlik durumu. Cikis kapisinda <see cref="Placeholder"/> sayisi sifir olmali (URETIM §3).</summary>
public static class AssetStatus
{
    /// <summary>Sekiz olcut gecti ve sahip onayladi.</summary>
    public const string Golden = "altin";
    /// <summary>Sekiz olcut gecti; sahibin onayi bekleniyor.</summary>
    public const string AwaitingOwner = "onay_bekliyor";
    /// <summary>Oyunda duran ama olcutlerden gecmemis ya da koddan gecici bicim.</summary>
    public const string Placeholder = "yer_tutucu";
    public static readonly string[] All = { Golden, AwaitingOwner, Placeholder };
}

/// <summary>Sinif butcesi (SANAT-SES §4).</summary>
public sealed class AssetBudget
{
    public int MinTris { get; set; }
    public int MaxTris { get; set; }
    public int MaxTex { get; set; }
    public string Note { get; set; } = "";
}

public sealed class AssetSource
{
    /// <summary>tripo_api, tripo_web, genex, kod.</summary>
    public string Pool { get; set; } = "";
    public string Service { get; set; } = "";
    public int Credits { get; set; }
    public int Retries { get; set; }
    public string TaskId { get; set; } = "";
    public string Date { get; set; } = "";
}

/// <summary>SANAT-SES §9 olcutu. 1-5 betikle (Pass), 6-8 goruntuyle puan (Score / 10, gecme 8). Pass null = olculmedi.</summary>
public sealed class AssetCriterion
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool? Pass { get; set; }
    public float? Score { get; set; }
    public string Value { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class AssetLod
{
    public int Tris { get; set; }
    public int? Tex { get; set; }
    public string Path { get; set; } = "";
}

public sealed class AssetSocket
{
    public string Name { get; set; } = "";
    /// <summary>Godot ekseninde (Y yukari), model kokune gore, metre.</summary>
    public float[] Pos { get; set; } = Array.Empty<float>();
}

public sealed class AssetDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>URETIM §4 altin varlik sinifi ("Kaya", "Makine 2"...); bos = altin degil.</summary>
    public string GoldenClass { get; set; } = "";
    public string BudgetClass { get; set; } = "";
    /// <summary>Sahibi olan akis (CLAUDE.md).</summary>
    public string Stream { get; set; } = "";
    public string Model { get; set; } = "";
    public bool InGame { get; set; }
    public string UsedBy { get; set; } = "";
    public string Status { get; set; } = AssetStatus.Placeholder;
    public string StatusNote { get; set; } = "";
    public bool OwnerApproved { get; set; }
    public AssetSource Source { get; set; } = new();
    public string License { get; set; } = "";
    public string Forward { get; set; } = "";
    public int Tris { get; set; }
    public float[] SizeM { get; set; } = Array.Empty<float>();
    public List<AssetLod> Lods { get; set; } = new();
    public string Hull { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Card { get; set; } = "";
    public List<string> Parts { get; set; } = new();
    public List<AssetSocket> Sockets { get; set; } = new();
    public List<AssetCriterion> Criteria { get; set; } = new();
    public string Notes { get; set; } = "";

    /// <summary>Sekiz olcutun hepsi gecti mi (puanlanmamis olcut gecmis sayilmaz).</summary>
    public bool PassesAll => Criteria.Count == 8 && Criteria.All(c => c.Pass == true);
}

public sealed class AssetsFile
{
    public Dictionary<string, AssetBudget> Budgets { get; set; } = new();
    public List<AssetDef> Assets { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("assets.json")]
    public AssetsFile AssetRegistry { get; private set; } = new();

    public AssetDef? Asset(string id) => AssetRegistry.Assets.FirstOrDefault(a => a.Id == id);

    /// <summary>Yer tutucu sayisi: cikis kapisinda sifir olmali (URETIM §3).</summary>
    public int PlaceholderCount => AssetRegistry.Assets.Count(a => a.Status == AssetStatus.Placeholder);
}
