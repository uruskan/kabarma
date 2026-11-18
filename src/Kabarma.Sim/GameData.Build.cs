namespace Kabarma.Sim;

// build.json: yapi malzemeleri, tasima dengesi sayilari, iade, hayalet renkleri, Plan Defteri (GDD §7, ICERIK §10.1).

public sealed class BuildMaterialDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Ocak basamagi (GDD §7 tablosu); kilit malzemenin kendisidir, bu bilgi icindir.</summary>
    public int Tier { get; set; } = 1;
    public float Hp { get; set; } = 400f;
    /// <summary>En fazla yatay aciklik (parca).</summary>
    public int MaxSpan { get; set; } = 3;
    /// <summary>Sekil birimi basina agirlik.</summary>
    public float WeightPerUnit { get; set; } = 1f;
    /// <summary>Parcanin tasiyabildigi toplam yuk (kendi agirligi dahil).</summary>
    public float Capacity { get; set; } = 30f;
    /// <summary>Esya -> sekil birimi basina oran (Kerpic-Tas: yari Kerpic yari Tas).</summary>
    public Dictionary<string, float> Cost { get; set; } = new();
    /// <summary>Model yoksa ahsap modelinin boyanacagi renk (sRGB, yer tutucu).</summary>
    public float[] Tint { get; set; } = { 1f, 1f, 1f };
}

public sealed class BuildGhostDef
{
    public float[] Ok { get; set; } = { 0.45f, 1f, 0.55f, 0.42f };
    public float[] Strained { get; set; } = { 1f, 0.85f, 0.3f, 0.45f };
    public float[] Bad { get; set; } = { 1f, 0.35f, 0.3f, 0.42f };
    public float[] Plan { get; set; } = { 0.45f, 0.7f, 1f, 0.38f };
    public float[] PlanPiece { get; set; } = { 0.45f, 0.7f, 1f, 0.28f };
}

public sealed class BuildCollapseDef
{
    public float Lifetime { get; set; } = 7f;
    public int Dust { get; set; } = 22;
    public string Sfx { get; set; } = "yapi_cokme";
}

public sealed class PlanBookDef
{
    public int Slots { get; set; } = 12;
    public int MaxPieces { get; set; } = 200;
}

public sealed class BuildFile
{
    public List<BuildMaterialDef> Materials { get; set; } = new();
    public float RefundInClaim { get; set; } = 1f;
    public float RefundOutClaim { get; set; } = 0.5f;
    public float RepairFraction { get; set; } = 0.25f;
    public BuildGhostDef Ghost { get; set; } = new();
    public BuildCollapseDef Collapse { get; set; } = new();
    public PlanBookDef PlanBook { get; set; } = new();

    public BuildMaterialDef? Material(string id) => Materials.FirstOrDefault(m => m.Id == id);
    public int IndexOf(string id) => Materials.FindIndex(m => m.Id == id);
    /// <summary>Bir ust malzeme (yoksa null).</summary>
    public BuildMaterialDef? Next(string id)
    {
        int i = IndexOf(id);
        return i >= 0 && i + 1 < Materials.Count ? Materials[i + 1] : null;
    }
}

public sealed partial class GameData
{
    [DataFile("build.json")]
    public BuildFile Build { get; private set; } = new();
}
