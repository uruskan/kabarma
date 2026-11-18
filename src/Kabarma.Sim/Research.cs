namespace Kabarma.Sim;

/// <summary>Arastirmanin neden yapilamadigi (Uretim paneli bunu yazar).</summary>
public enum ResearchBlock { Ok, NotResearch, Done, NeedsPrereq, NoTable, NoVeri }

/// <summary>
/// Arastirma ve Tarayici (GDD §4, §12; ICERIK §0, §11.1). Veri tarama, is emri ve enkazdan gelir (AddVeri); Arastirma
/// Masasi'nin yaninda harcanir. Arastirilan dugum GameState.Known'a "arastirma:&lt;id&gt;" yazar: kilitli tarifler
/// (recipes.json unlockOn) bu sette arandigi icin Uretim paneli ve kuyruklar baska bir kural bilmeden acilir.
/// Ocak basamagi da ayni sete "ocak:&lt;n&gt;" yazar (Ata istasyonlari). Durum duz (TEKNIK §9): Veri, basamak, taranan
/// turler; arastirilanlar Known'da.
/// </summary>
public sealed class ResearchState
{
    readonly ResearchFile _def;
    readonly ISet<string> _known;

    public int Veri { get; private set; }
    public int OcakTier { get; private set; }
    /// <summary>Ansiklopedi: ilk kez taranmis turler (ScanEntryDef.Id).</summary>
    public HashSet<string> Scanned { get; } = new();

    /// <summary>(degisim, neden): "tarama:gidak", "arastirma:bicki", "is_emri".</summary>
    public event Action<int, string>? VeriChanged;
    public event Action<ResearchNodeDef>? Researched;
    public event Action<int>? TierChanged;

    public ResearchState(ResearchFile def, ISet<string> known)
    {
        _def = def;
        _known = known;
        OcakTier = Math.Max(1, def.OcakTierStart); // baslangic basamagi Known'a yazilmaz (kayit gidis-donusu); 'ocak:1' kilidi kullanilmaz
    }

    public ResearchFile Def => _def;
    public static string Token(string nodeId) => ResearchFile.ResearchPrefix + nodeId;

    public bool IsDone(string nodeId) => _known.Contains(Token(nodeId));
    public IEnumerable<ResearchNodeDef> Done => _def.Nodes.Where(n => IsDone(n.Id));

    /// <summary>Veri ekler (is emri, enkaz kupu, baska akislar buradan verir).</summary>
    public void AddVeri(int amount, string reason)
    {
        if (amount <= 0) return;
        Veri += amount;
        VeriChanged?.Invoke(amount, reason);
    }

    /// <summary>Ocak basamagi (Ata Cekirdegi; GDD §6): 1..n arasi "ocak:k" Known'a girer. Yalniz yukselir.</summary>
    public void SetOcakTier(int tier)
    {
        if (tier <= OcakTier) return;
        OcakTier = tier;
        for (int k = 1; k <= tier; k++) _known.Add(ResearchFile.TierPrefix + k);
        TierChanged?.Invoke(tier);
    }

    /// <summary>Tapu yaricapi bu basamakta (m): ambardan uretimin Sandik yaricapi.</summary>
    public float ClaimRadius => _def.ClaimRadiusAt(OcakTier);

    public ResearchBlock Check(string nodeId, bool nearTable)
    {
        var n = _def.Node(nodeId);
        if (n == null) return ResearchBlock.NotResearch;
        if (IsDone(n.Id)) return ResearchBlock.Done;
        if (n.Requires.Any(r => !IsDone(r))) return ResearchBlock.NeedsPrereq;
        if (!nearTable) return ResearchBlock.NoTable;
        if (Veri < _def.CostOf(n)) return ResearchBlock.NoVeri;
        return ResearchBlock.Ok;
    }

    /// <summary>Dugumu arastirir: Veri duser, kilit acilir. Yapilamazsa false.</summary>
    public bool Research(string nodeId, bool nearTable)
    {
        if (Check(nodeId, nearTable) != ResearchBlock.Ok) return false;
        var n = _def.Node(nodeId)!;
        int cost = _def.CostOf(n);
        Veri -= cost;
        _known.Add(Token(n.Id));
        VeriChanged?.Invoke(-cost, Token(n.Id));
        Researched?.Invoke(n);
        return true;
    }

    /// <summary>
    /// Ocak basamak yukseltme (GDD §6): bir sonraki basamagin Cekirdegi (research.json tierCores; Ata odulu) depodaysa
    /// harcanir ve basamak yukselir. Donen: yeni basamak (yukselmediyse 0).
    /// </summary>
    public int TryUpgradeOcak(IItemStore store)
    {
        int next = OcakTier + 1;
        if (next >= _def.TierCores.Count) return 0;
        var core = _def.TierCores[next];
        if (string.IsNullOrEmpty(core) || store.Count(core) < 1 || !store.Remove(core, 1)) return 0;
        SetOcakTier(next);
        return next;
    }

    public ResearchSave Save() => new() { Veri = Veri, OcakTier = OcakTier, Scanned = Scanned.OrderBy(x => x, StringComparer.Ordinal).ToList() };

    /// <summary>Sessiz yukleme (olay yaymaz); arastirilanlar Known ile ("core" bolumu) gelir.</summary>
    public void Load(ResearchSave s)
    {
        Veri = Math.Max(0, s.Veri);
        OcakTier = Math.Max(1, s.OcakTier);
        Scanned.Clear(); // basamak kilitleri Known ile ('core' bolumu) gelir
        foreach (var x in s.Scanned) Scanned.Add(x);
    }

    /// <summary>Taranabilir tur mu (kind + id: "creature"/"gidak", "mineral"/"clay").</summary>
    public ScanEntryDef? ScanEntry(string kind, string id) => _def.Scan.Entries.FirstOrDefault(e => e.Kind == kind && e.Id == id);

    /// <summary>Tarayici: tur ilk kez taraninca Veri verir ve Ansiklopedi'ye yazilir. Donen: kazanilan Veri (onceden taranmissa 0, bilinmiyorsa -1).</summary>
    public int Scan(string kind, string id)
    {
        var e = ScanEntry(kind, id);
        if (e == null) return -1;
        if (!Scanned.Add(e.Id)) return 0;
        AddVeri(e.Veri, "tarama:" + e.Id);
        return e.Veri;
    }
}

/// <summary>Arastirma kaydi (TEKNIK 9.1): Veri, Ocak basamagi, Ansiklopedi. Arastirilan dugumler Known'da.</summary>
public sealed class ResearchSave
{
    public int Veri { get; set; }
    public int OcakTier { get; set; } = 1;
    public List<string> Scanned { get; set; } = new();
}

/// <summary>GameState parcasi: arastirma, Ocak basamagi, istasyon kuyruklari ve ambardan uretim deposu.</summary>
public sealed partial class GameState
{
    ResearchState? _research;

    [SaveSection("research", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Research) })]
    ResearchSave ResearchSection { get => Research.Save(); set => Research.Load(value ?? new ResearchSave()); }

    /// <summary>Ilk eriside kurulur; Known'u paylasir (arastirilanlar ve basamak orada).</summary>
    public ResearchState Research => _research ??= new ResearchState(Data.Research, Known);

    /// <summary>Istasyonun kuyrugu; tarifi olmayan istasyon (Kulucka, Arastirma Masasi) icin ilk istekte kurulur.</summary>
    public StationQueue QueueFor(string station)
    {
        if (!Stations.TryGetValue(station, out var q)) Stations[station] = q = new StationQueue(Data, station) { Known = Known };
        return q;
    }

    /// <summary>Ambardan uretim (GDD §5): istasyon tarifinin deposu, once canta sonra verilen Sandiklar.</summary>
    public ItemPool StationStore(IEnumerable<Inventory> chests) => new(new IItemStore[] { Backpack }.Concat(chests));
}
