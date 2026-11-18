namespace Kabarma.Sim;

// Uretim paneli, kemer ve elde tutulan esya (GDD 4, 5, 7; URETIM 6 M1 karari 23). Veri: craft.json (kemer, kuyruk,
// kategoriler, eksik malzemenin kaynagi) ve placeables.json (planWheel). Tarifler recipes.json: "station" bos ise el tarifi.

/// <summary>Uretim panelinin kategorisi (sol sutun). "all" butun tarifleri gosterir.</summary>
public sealed class CraftCategoryDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>
/// Eksik malzemenin nereden bulunacagi (GDD 5 "Eksik malzeme"): satirda yazan metin ve pusulanin cozdugu hedef turu
/// ("rock:clay", "tree", "tuft:fiber", "farm", "station:tas_firin", "gidak").
/// </summary>
public sealed class ItemSourceDef
{
    public string Text { get; set; } = "";
    public string Target { get; set; } = "";
}

public sealed partial class ToolFile
{
    /// <summary>Cantaya girmeyen, hep elde olan kemer esyalari (Capa, Insa Plani; GDD 3.6: olumde oyuncuda kalir).</summary>
    public List<string> BeltTools { get; set; } = new();
    /// <summary>Kemerin baslangic dizilisi (GDD 5: 1. yuvada Capa, 2. yuvada Insa Plani).</summary>
    public List<string> BeltStart { get; set; } = new();
    /// <summary>El uretim kuyrugunda en cok bu kadar is (satir).</summary>
    public int CraftQueueMax { get; set; } = 8;
    /// <summary>Panelde tek seferde secilebilen en buyuk adet.</summary>
    public int CraftCountMax { get; set; } = 10;
    /// <summary>Istasyon tarifi bu yaricap (m) icindeki istasyonda uretilir; ayni yaricap E menusunu acar.</summary>
    public float StationReach { get; set; } = 3.2f;
    /// <summary>Erisimde birden cok istasyon varsa E nisangahin bu aci (derece) icinde baktigini acar; yoksa en yakini.</summary>
    public float StationFocusDegrees { get; set; } = 30f;
    /// <summary>Insa Plani eldeyken parcaya bakip E bu kadar basili tutulunca yukselt / onar / sok carki acilir.</summary>
    public float PlanHoldSeconds { get; set; } = 0.35f;
    public List<CraftCategoryDef> CraftCategories { get; set; } = new();
    /// <summary>Esya -> kaynagi (eksik malzeme satiri ve "Pusulada goster").</summary>
    public Dictionary<string, ItemSourceDef> Sources { get; set; } = new();
    /// <summary>Insa Plani'nin parca carki (sag tik basili): yapi seti parca kimlikleri, carktaki sirayla.</summary>
    public List<string> PlanWheel { get; set; } = new();

    /// <summary>Bu esyayi ureten tarif (tarifin kimligi urunun kimligidir); yoksa null.</summary>
    public RecipeDef? RecipeFor(string item) => Recipes.FirstOrDefault(r => r.Id == item);

    public bool IsPlaceable(string id) => Placeables.Any(p => p.Id == id);
    public bool IsTool(string id) => BeltTools.Contains(id);
    public bool IsFood(string id) => Foods.Any(f => f.Id == id);

    /// <summary>Elde tutulabilen esya: kemer aleti, yerlestirilebilir ya da yemek. Kemere kendiliginden yalniz bunlar girer.</summary>
    public bool IsHoldable(string id) => IsTool(id) || IsPlaceable(id) || IsFood(id) || HeldItems.Contains(id);

    /// <summary>Cantada duran ama elde kullanilan obur esyalar (guc: Kablo, sokete elle cekilir; GDD 11.1).</summary>
    public List<string> HeldItems { get; set; } = new();

    /// <summary>
    /// Yerlestirilebilirin bedeli: onu ureten tarifin girdisi (karar 23: bedel artik tariftedir). Tarif yoksa placeables.json
    /// "cost" (bos). Soku iadesi ve bot bunu kullanir.
    /// </summary>
    public IReadOnlyDictionary<string, int> CostOf(string placeable) =>
        RecipeFor(placeable)?.In ?? (IReadOnlyDictionary<string, int>?)Placeables.FirstOrDefault(p => p.Id == placeable)?.Cost ?? new Dictionary<string, int>();
}
