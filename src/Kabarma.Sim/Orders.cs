namespace Kabarma.Sim;

/// <summary>Bir is emri zincirinin ilerlemesi (ICERIK 18: 5 adim). Sayaclar zincir acildiktan sonra sayar.</summary>
public sealed class ChainState
{
    public OrderChainDef Def { get; }
    public bool Opened { get; internal set; }
    /// <summary>Olcu -> deger (sum: toplam, distinct: farkli anahtar sayisi, peak: en yuksek).</summary>
    public Dictionary<string, int> Counts { get; } = new();
    /// <summary>"distinct" olculerin gorulen anahtarlari (olcu -> anahtarlar). Duz, kayda hazir.</summary>
    public Dictionary<string, HashSet<string>> Keys { get; } = new();
    readonly bool[] _done;

    public ChainState(OrderChainDef def) { Def = def; _done = new bool[def.Steps.Count]; }

    public int Count(string measure) => Counts.GetValueOrDefault(measure);
    public bool StepDone(int i) => i >= 0 && i < _done.Length && _done[i];
    /// <summary>Ilk bitmemis adim (bitmisse adim sayisi).</summary>
    public int CurrentStep { get { int i = 0; while (i < _done.Length && _done[i]) i++; return i; } }
    public OrderStepDef? Current => CurrentStep < Def.Steps.Count ? Def.Steps[CurrentStep] : null;
    public bool Done => CurrentStep >= Def.Steps.Count;
    public int DoneCount => _done.Count(d => d);
    /// <summary>Adimin ilerlemesi (hedefte kesilir).</summary>
    public int Progress(int i) => Math.Min(Count(Def.Steps[i].Measure), Def.Steps[i].Target);
    public bool[] DoneFlags() => (bool[])_done.Clone();
    internal void SetDone(IList<bool> flags) { for (int i = 0; i < _done.Length && i < flags.Count; i++) _done[i] = flags[i]; }

    /// <summary>Yeni biten adimlarin sirasi (her adim bir kez).</summary>
    internal IEnumerable<int> Latch()
    {
        for (int i = 0; i < _done.Length; i++)
        {
            if (_done[i] || Count(Def.Steps[i].Measure) < Def.Steps[i].Target) continue;
            _done[i] = true;
            yield return i;
        }
    }
}

/// <summary>Odeme geri cagrisi (Kredi, Veri, ne icin): GameState kendi bakiyesine yazar.</summary>
public delegate void OrderPayout(int credits, int veri, string what);

/// <summary>
/// Defter: is emri zincirleri (GDD 13, 24; ICERIK 18). Oyun olaylari olcu adiyla bildirilir; uc kip (orders.json "measures"):
///   sum      Report(olcu, n): her olay toplanir ("planted", "swollen_crop"...).
///   distinct ReportKey(olcu, anahtar): farkli anahtar sayisi ("crop_kind", "tamed_species").
///   peak     Level(olcu, deger): en yuksek deger ("herd_size", "plant_gene"); Defter acilinca o ana kadarki tepe alinir.
/// "Ilk X" adimlari sirasindan bagimsiz isaretlenir (ilk gecede Kabarma kargodan once gelir); siradaki adim ilk bitmemis olandir.
/// Adim odulu (Kredi + Veri) Defter aciksa adim biter bitmez, degilse Defter acilinca odenir (sessiz sayim, GDD 24).
/// Genel sayaclar (Stats) zincirlerden bagimsizdir ve her zaman sayar: basarimlar bunlara bakar.
/// </summary>
public sealed class OrderBook
{
    readonly OrdersFile _def;
    readonly List<ChainState> _chains;
    readonly OrderPayout? _pay;
    readonly List<string> _pending = new();

    public OrderBook(OrdersFile def, OrderPayout? pay = null)
    {
        _def = def;
        _pay = pay;
        _chains = def.Chains.Select(c => new ChainState(c) { Opened = c.OpensAt == "start" }).ToList();
    }

    public IReadOnlyList<ChainState> Chains => _chains;
    public bool DefterOpen { get; private set; }
    public int OrdersTotal => _def.ChainsTotal * _def.StepsPerChain;
    public int ChainsTotal => _def.ChainsTotal;
    /// <summary>Oyuncunun elle sectigi takip zinciri (bos: otomatik).</summary>
    public string PinnedId { get; private set; } = "";
    /// <summary>Genel sayaclar: zincirden bagimsiz, her zaman sayar (basarimlar).</summary>
    public Dictionary<string, int> Stats { get; } = new();
    public Dictionary<string, HashSet<string>> StatKeys { get; } = new();
    /// <summary>Acilan seyler ("kind:id"): sahibi sistem (kart, uye, kozmetik, plan, esya) buradan okur.</summary>
    public HashSet<string> Granted { get; } = new();
    /// <summary>Defter kapaliyken biten, odulu bekleyen adimlar ("zincir:adim").</summary>
    public IReadOnlyList<string> PendingRewards => _pending;

    /// <summary>(zincir, biten adim indeksi).</summary>
    public event Action<ChainState, int>? StepDone;
    /// <summary>Zincirin 5 adimi da bitti.</summary>
    public event Action<ChainState>? ChainDone;
    /// <summary>(zincir, adim indeksi, Kredi, Veri): adim odulu hesaba gecti.</summary>
    public event Action<ChainState, int, int, int>? RewardPaid;
    public event Action<ChainState, OrderGrantDef>? GrantMade;
    /// <summary>(sayac, yeni deger): genel sayac degisti.</summary>
    public event Action<string, int>? StatChanged;
    public event Action? DefterOpened;

    public ChainState? Chain(string id) => _chains.FirstOrDefault(c => c.Def.Id == id);

    string Mode(string measure) => _def.Measures.TryGetValue(measure, out var m) ? m : "sum";

    /// <summary>PLAN 1.4: ilk Hasat Raporu'nda Defter acilir; "defter" zincirleri bundan sonra sayar.</summary>
    public void OpenDefter()
    {
        if (DefterOpen) return;
        DefterOpen = true;
        foreach (var c in _chains)
        {
            if (c.Def.OpensAt != "defter") continue;
            c.Opened = true;
            // Tepe olculer (aynı anda hayvan, gen puani) simdiye dek ulasilan en yuksek degerle baslar.
            foreach (var s in c.Def.Steps)
                if (Mode(s.Measure) == "peak" && Stats.TryGetValue(s.Measure, out int v)) c.Counts[s.Measure] = v;
            foreach (int i in c.Latch().ToList()) AfterLatch(c, i);
        }
        FlushPending();
        DefterOpened?.Invoke();
    }

    /// <summary>sum kipi: olcuyu amount kadar artirir.</summary>
    public void Report(string measure, int amount = 1)
    {
        if (amount <= 0) return;
        SetStat(measure, Stats.GetValueOrDefault(measure) + amount);
        foreach (var c in Watching(measure))
        {
            c.Counts[measure] = c.Count(measure) + amount;
            LatchAll(c);
        }
    }

    /// <summary>distinct kipi: anahtar daha once gorulmediyse sayiyi bir artirir.</summary>
    public void ReportKey(string measure, string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var set = StatKeys.TryGetValue(measure, out var s0) ? s0 : StatKeys[measure] = new();
        if (set.Add(key)) SetStat(measure, set.Count);
        foreach (var c in Watching(measure))
        {
            var keys = c.Keys.TryGetValue(measure, out var k0) ? k0 : c.Keys[measure] = new();
            if (!keys.Add(key)) continue;
            c.Counts[measure] = keys.Count;
            LatchAll(c);
        }
    }

    /// <summary>peak kipi: deger o ana kadarki en yuksekten buyukse tepeyi gunceller.</summary>
    public void Level(string measure, int value)
    {
        if (value > Stats.GetValueOrDefault(measure)) SetStat(measure, value);
        foreach (var c in Watching(measure))
        {
            if (value <= c.Count(measure)) continue;
            c.Counts[measure] = value;
            LatchAll(c);
        }
    }

    IEnumerable<ChainState> Watching(string measure) =>
        _chains.Where(c => c.Opened && !c.Done && c.Def.Steps.Any(s => s.Measure == measure)).ToList();

    void SetStat(string stat, int value)
    {
        Stats[stat] = value;
        StatChanged?.Invoke(stat, value);
    }

    void LatchAll(ChainState c)
    {
        foreach (int i in c.Latch().ToList()) AfterLatch(c, i);
    }

    void AfterLatch(ChainState c, int i)
    {
        StepDone?.Invoke(c, i);
        if (DefterOpen) Pay(c, i); else _pending.Add($"{c.Def.Id}:{i}");
        if (c.Done) ChainDone?.Invoke(c);
    }

    void FlushPending()
    {
        var todo = _pending.ToList();
        _pending.Clear();
        foreach (var key in todo)
        {
            var p = key.Split(':');
            var c = Chain(p[0]);
            if (c != null) Pay(c, int.Parse(p[1]));
        }
    }

    void Pay(ChainState c, int i)
    {
        int cr = i < _def.StepRewards.Credits.Count ? _def.StepRewards.Credits[i] : 0;
        int veri = i < _def.StepRewards.Veri.Count ? _def.StepRewards.Veri[i] : 0;
        _pay?.Invoke(cr, veri, $"{c.Def.Id} {i + 1}");
        RewardPaid?.Invoke(c, i, cr, veri);
        foreach (var g in c.Def.Grants.Where(g => g.Step == i + 1))
            if (Granted.Add($"{g.Kind}:{g.Id}")) GrantMade?.Invoke(c, g);
    }

    /// <summary>
    /// Takip edilen zincir (Defter satiri, pusula). Oyuncu sectiyse o; degilse Defter acilana dek Ciraklik, sonra
    /// Ciraklik'tan sonraki ilk bitmemis acik zincir (Ciraklik'in sonraki adimlari ogretici olarak gozlenir).
    /// </summary>
    public ChainState? Tracked
    {
        get
        {
            if (PinnedId != "" && Chain(PinnedId) is { Opened: true, Done: false } pin) return pin;
            var first = _chains.FirstOrDefault();
            if (!DefterOpen) return first is { Opened: true, Done: false } ? first : null;
            return _chains.Skip(1).FirstOrDefault(c => c.Opened && !c.Done) ?? (first is { Opened: true, Done: false } ? first : null);
        }
    }

    /// <summary>Takip zincirini secer ("" otomatik). Acik ve bitmemis degilse false.</summary>
    public bool Pin(string id)
    {
        if (id != "" && Chain(id) is not { Opened: true, Done: false }) return false;
        PinnedId = id;
        return true;
    }

    // ---- Kayit: duz, serilestirilebilir (TEKNIK 9) ----

    public OrderBookState Export() => new()
    {
        DefterOpen = DefterOpen,
        PinnedId = PinnedId,
        Pending = _pending.ToList(),
        Granted = Granted.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        Stats = new(Stats),
        StatKeys = StatKeys.ToDictionary(k => k.Key, k => k.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()),
        Chains = _chains.Select(c => new ChainSave
        {
            Id = c.Def.Id, Opened = c.Opened, Done = c.DoneFlags().ToList(), Counts = new(c.Counts),
            Keys = c.Keys.ToDictionary(k => k.Key, k => k.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()),
        }).ToList(),
    };

    /// <summary>Kaydi yukler (olay tetiklemez, odeme yapmaz). Tanimsiz zincir yok sayilir.</summary>
    public void Import(OrderBookState s)
    {
        DefterOpen = s.DefterOpen;
        PinnedId = s.PinnedId;
        _pending.Clear(); _pending.AddRange(s.Pending);
        Granted.Clear(); foreach (var g in s.Granted) Granted.Add(g);
        Stats.Clear(); foreach (var (k, v) in s.Stats) Stats[k] = v;
        StatKeys.Clear(); foreach (var (k, v) in s.StatKeys) StatKeys[k] = new(v);
        foreach (var cs in s.Chains)
        {
            var c = Chain(cs.Id);
            if (c == null) continue;
            c.Opened = cs.Opened;
            c.SetDone(cs.Done);
            c.Counts.Clear(); foreach (var (k, v) in cs.Counts) c.Counts[k] = v;
            c.Keys.Clear(); foreach (var (k, v) in cs.Keys) c.Keys[k] = new(v);
        }
    }
}

public sealed class ChainSave
{
    public string Id { get; set; } = "";
    public bool Opened { get; set; }
    public List<bool> Done { get; set; } = new();
    public Dictionary<string, int> Counts { get; set; } = new();
    public Dictionary<string, List<string>> Keys { get; set; } = new();
}

public sealed class OrderBookState
{
    public bool DefterOpen { get; set; }
    public string PinnedId { get; set; } = "";
    public List<string> Pending { get; set; } = new();
    public List<string> Granted { get; set; } = new();
    public Dictionary<string, int> Stats { get; set; } = new();
    public Dictionary<string, List<string>> StatKeys { get; set; } = new();
    public List<ChainSave> Chains { get; set; } = new();
}
