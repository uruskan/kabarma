namespace Kabarma.Sim;

// M1 Sim sistemlerinin kayit DTO'lari ve sessiz yukleyicileri (TEKNIK 9.1). Siniflar partial: sahibi akisin dosyasina
// dokunmadan ozel alanlara buradan erisilir. Load yontemleri olay yaymaz, sayac ilerletmez.
// Bolumler GameState.Save.Systems.cs icinde.

public sealed class PlotSave
{
    public int X { get; set; }
    public int Y { get; set; }
    public string? Crop { get; set; }
    public Genes Genes { get; set; } = new();
    public float Growth { get; set; }
    public float Moisture { get; set; }
    public float Fertility { get; set; }
    public bool NeverDry { get; set; } = true;
    /// <summary>Kabarik urunun boy kimligi (crops.json sizeClasses); null: kabarik degil.</summary>
    public string? Swollen { get; set; }
    public bool Eaten { get; set; }
    public bool Harvested { get; set; }
    public float SinceDawn { get; set; } = -1f;
    public bool Deflated { get; set; }
}

public sealed partial class Farm
{
    public List<PlotSave> SavePlots() => _plots.Values.OrderBy(p => p.X).ThenBy(p => p.Y).Select(p => new PlotSave
    {
        X = p.X, Y = p.Y, Crop = p.Crop,
        Genes = new Genes { V = p.Genes.V, H = p.Genes.H, D = p.Genes.D, M = p.Genes.M },
        Growth = p.Growth, Moisture = p.Moisture, Fertility = p.Fertility, NeverDry = p.NeverDry,
        Swollen = p.Swollen?.Id, Eaten = p.Eaten, Harvested = p.Harvested, SinceDawn = p.SinceDawn, Deflated = p.Deflated,
    }).ToList();

    /// <summary>Kareleri aynen kurar; Tilled/Planted yaymaz (gorunumu FarmView.Rebuild kurar).</summary>
    public void LoadPlots(IEnumerable<PlotSave> plots)
    {
        _plots.Clear();
        foreach (var s in plots)
        {
            _plots[(s.X, s.Y)] = new Plot
            {
                X = s.X, Y = s.Y, Crop = s.Crop, Genes = s.Genes ?? new Genes(),
                Growth = s.Growth, Moisture = s.Moisture, Fertility = s.Fertility, NeverDry = s.NeverDry,
                Swollen = s.Swollen == null ? null : _data.Crops.SizeClasses.FirstOrDefault(c => c.Id == s.Swollen),
                Eaten = s.Eaten, Harvested = s.Harvested, SinceDawn = s.SinceDawn, Deflated = s.Deflated,
            };
        }
    }
}

public sealed class StaminaSave
{
    public float Value { get; set; }
    public bool Exhausted { get; set; }
    public float SinceSprint { get; set; }
}

public sealed partial class Stamina
{
    public StaminaSave Save() => new() { Value = Value, Exhausted = Exhausted, SinceSprint = SinceSprint };
    public void Load(StaminaSave s) { Value = Math.Clamp(s.Value, 0f, Math.Max(Max, s.Value)); Exhausted = s.Exhausted; SinceSprint = s.SinceSprint; Sprinting = false; }
}

public sealed class FoodSave
{
    public string Id { get; set; } = "";
    public float Left { get; set; }
}

public sealed partial class FoodBuffs
{
    public List<FoodSave> Save() => _active.Select(a => new FoodSave { Id = a.food.Id, Left = a.left }).ToList();
    public void Load(IEnumerable<FoodSave> list, Func<string, FoodDef?> food)
    {
        _active.Clear();
        foreach (var f in list) if (food(f.Id) is FoodDef d && _active.Count < Slots) _active.Add((d, f.Left));
    }
}

/// <summary>Istasyon kuyrugunun bir isi (zincir M2: istasyon basina cok is).</summary>
public sealed class StationJobSave
{
    public string Recipe { get; set; } = "";
    public int Left { get; set; }
    public List<string?> Fuel { get; set; } = new();
}

public sealed class StationQueueSave
{
    public string Station { get; set; } = "";
    /// <summary>Ilk isin tarifi (M1 bicimi; Jobs bossa tek is olarak okunur).</summary>
    public string? Active { get; set; }
    public float Progress { get; set; }
    public int Queued { get; set; }
    public List<string?> Fuel { get; set; } = new();
    /// <summary>Butun isler, sirasiyla (zincir M2).</summary>
    public List<StationJobSave> Jobs { get; set; } = new();
}

public sealed partial class StationQueue
{
    public StationQueueSave Save() => new()
    {
        Station = Station, Active = Active?.Id, Progress = Progress, Queued = Queued, Fuel = _jobs.Count > 0 ? _jobs[0].Fuel.ToList() : new(),
        Jobs = _jobs.Select(j => new StationJobSave { Recipe = j.Recipe.Id, Left = j.Left, Fuel = j.Fuel.ToList() }).ToList(),
    };

    public void Load(StationQueueSave s)
    {
        _jobs.Clear();
        Blocked = false;
        var jobs = s.Jobs.Count > 0 ? s.Jobs
            : s.Active != null ? new List<StationJobSave> { new() { Recipe = s.Active, Left = s.Queued, Fuel = s.Fuel } } : new();
        foreach (var j in jobs)
        {
            var r = _data.Tool.Recipes.FirstOrDefault(x => x.Id == j.Recipe && x.Station == Station);
            if (r == null || j.Left <= 0) continue;
            var job = new Job { Recipe = r, Left = j.Left };
            job.Fuel.AddRange(j.Fuel);
            _jobs.Add(job);
        }
        Progress = _jobs.Count == 0 ? 0 : s.Progress;
    }
}

public sealed class CraftJobSave
{
    public string Recipe { get; set; } = "";
    public int Left { get; set; }
    public float Progress { get; set; }
}

public sealed class CraftSave
{
    public List<CraftJobSave> Jobs { get; set; } = new();
    public bool Blocked { get; set; }
}

public sealed partial class CraftQueue
{
    public CraftSave Save() => new() { Blocked = Blocked, Jobs = _jobs.Select(j => new CraftJobSave { Recipe = j.Recipe.Id, Left = j.Left, Progress = j.Progress }).ToList() };
    public void Load(CraftSave s, Func<string, RecipeDef?> recipe)
    {
        _jobs.Clear();
        foreach (var j in s.Jobs) if (recipe(j.Recipe) is RecipeDef r && j.Left > 0) _jobs.Add(new Job { Recipe = r, Left = j.Left, Progress = j.Progress });
        Blocked = s.Blocked;
    }
}

public sealed class CrateSave
{
    public int Id { get; set; }
    public List<StackSave> Items { get; set; } = new();
}

public sealed class PayoutSave
{
    public float Left { get; set; }
    public int Credits { get; set; }
    public string What { get; set; } = "";
}

public sealed class CargoSave
{
    public int Serial { get; set; }
    public bool FirstSaleDone { get; set; }
    public CrateSave? Open { get; set; }
    public List<CrateSave> Closed { get; set; } = new();
    public List<CrateSave> Pad { get; set; } = new();
    public List<PayoutSave> Pending { get; set; } = new();
}

public sealed partial class Cargo
{
    static CrateSave Of(CargoCrate c) => new() { Id = c.Id, Items = StackSave.Of(c.Items) };

    CargoCrate Make(CrateSave s)
    {
        var c = new CargoCrate(_d, s.Id);
        StackSave.Into(c.Items, s.Items);
        return c;
    }

    public CargoSave Save() => new()
    {
        Serial = _serial,
        FirstSaleDone = FirstSaleDone,
        Open = Open == null ? null : Of(Open),
        Closed = _closed.Select(Of).ToList(),
        Pad = _pad.Select(Of).ToList(),
        Pending = _pending.Select(p => new PayoutSave { Left = p.Left, Credits = p.Credits, What = p.What }).ToList(),
    };

    public void Load(CargoSave s)
    {
        _serial = s.Serial;
        FirstSaleDone = s.FirstSaleDone;
        Open = s.Open == null ? null : Make(s.Open);
        _closed.Clear();
        foreach (var c in s.Closed) _closed.Enqueue(Make(c));
        _pad.Clear();
        _pad.AddRange(s.Pad.Select(Make));
        _pending.Clear();
        _pending.AddRange(s.Pending.Select(p => new PendingPayout { Left = p.Left, Credits = p.Credits, What = p.What }));
    }

    /// <summary>
    /// Dunyadaki bir kasa dugumu kayittan kendi CargoCrate'iyle kuruldu: rampadaki ayni kimlikli kasanin yerine o nesne
    /// gecer (ikisi ayni kasa; tek nesne kalmali).
    /// </summary>
    public void Adopt(CargoCrate c)
    {
        int i = _pad.FindIndex(x => x.Id == c.Id);
        if (i >= 0) _pad[i] = c;
        if (Open?.Id == c.Id) Open = c;
        if (_closed.Any(x => x.Id == c.Id))
        {
            var list = _closed.Select(x => x.Id == c.Id ? c : x).ToList();
            _closed.Clear();
            foreach (var x in list) _closed.Enqueue(x);
        }
    }

    /// <summary>Rampada ya da kapali kasa sirasinda bu kimlikli kasa.</summary>
    public CargoCrate? FindCrate(int id) => _pad.FirstOrDefault(c => c.Id == id) ?? _closed.FirstOrDefault(c => c.Id == id) ?? (Open?.Id == id ? Open : null);
}

public sealed class CalendarSave
{
    public bool Running { get; set; }
    public bool Paused { get; set; }
    public int Day { get; set; } = 1;
    public float Seconds { get; set; }
    public int NightsSkipped { get; set; }
}

public sealed partial class Calendar
{
    public CalendarSave Save() => new() { Running = Running, Paused = Paused, Day = Day, Seconds = Seconds, NightsSkipped = NightsSkipped };
    public void Load(CalendarSave s) { Running = s.Running; Paused = s.Paused; Day = s.Day; Seconds = s.Seconds; NightsSkipped = s.NightsSkipped; }
}

public sealed class ScenarioSave
{
    /// <summary>Su anki adimin kimligi (veri degisse de dogru adim bulunsun); null: senaryo bitti.</summary>
    public string? Step { get; set; }
    public int Index { get; set; }
    public int Progress { get; set; }
}

public sealed partial class Scenario
{
    public ScenarioSave Save() => new() { Step = Current?.Id, Index = StepIndex, Progress = Progress };
    public void Load(ScenarioSave s)
    {
        int i = s.Step == null ? _steps.Count : _steps.FindIndex(x => x.Id == s.Step);
        StepIndex = i >= 0 ? i : Math.Clamp(s.Index, 0, _steps.Count);
        Progress = s.Progress;
    }
}

public sealed class ChainSave
{
    public string Id { get; set; } = "";
    public bool Opened { get; set; }
    public SortedDictionary<string, int> Counts { get; set; } = new(StringComparer.Ordinal);
    public List<bool> Done { get; set; } = new();
    /// <summary>"farkli" olculerin gorulen anahtarlari (ilerleme akisi).</summary>
    public SortedDictionary<string, List<string>> Keys { get; set; } = new(StringComparer.Ordinal);
}

public sealed class OrdersSave
{
    public bool DefterOpen { get; set; }
    public List<ChainSave> Chains { get; set; } = new();
    // ilerleme akisi
    public string PinnedId { get; set; } = "";
    public List<string> Pending { get; set; } = new();
    public List<string> Granted { get; set; } = new();
    public SortedDictionary<string, int> Stats { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, List<string>> StatKeys { get; set; } = new(StringComparer.Ordinal);
    public int CreditsPaid { get; set; }
    public int VeriPaid { get; set; }
}

public sealed partial class ChainState
{
    public ChainSave Save() => new()
    {
        Id = Def.Id, Opened = Opened,
        Counts = new SortedDictionary<string, int>(Counts, StringComparer.Ordinal),
        Done = _done.ToList(),
        Keys = new SortedDictionary<string, List<string>>(Keys.ToDictionary(k => k.Key, k => k.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()), StringComparer.Ordinal),
    };

    public void Load(ChainSave s)
    {
        Opened = s.Opened;
        Counts.Clear();
        foreach (var (k, v) in s.Counts) Counts[k] = v;
        for (int i = 0; i < _done.Length; i++) _done[i] = i < s.Done.Count && s.Done[i];
        Keys.Clear();
        foreach (var (k, v) in s.Keys) Keys[k] = new HashSet<string>(v);
    }
}

public sealed partial class OrderBook
{
    public OrdersSave Save() => new()
    {
        DefterOpen = DefterOpen, Chains = _chains.Select(c => c.Save()).ToList(),
        PinnedId = PinnedId, Pending = _pending.ToList(), Granted = Granted.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        Stats = new SortedDictionary<string, int>(Stats, StringComparer.Ordinal),
        StatKeys = new SortedDictionary<string, List<string>>(StatKeys.ToDictionary(k => k.Key, k => k.Value.OrderBy(x => x, StringComparer.Ordinal).ToList()), StringComparer.Ordinal),
        CreditsPaid = CreditsPaid, VeriPaid = VeriPaid,
    };

    /// <summary>Sessiz yukleme: olay yaymaz, odeme yapmaz.</summary>
    public void Load(OrdersSave s)
    {
        DefterOpen = s.DefterOpen;
        PinnedId = s.PinnedId ?? "";
        _pending.Clear(); _pending.AddRange(s.Pending ?? new());
        Granted.Clear(); foreach (var g in s.Granted ?? new()) Granted.Add(g);
        Stats.Clear(); foreach (var (k, v) in s.Stats ?? new()) Stats[k] = v;
        StatKeys.Clear(); foreach (var (k, v) in s.StatKeys ?? new()) StatKeys[k] = new HashSet<string>(v);
        CreditsPaid = s.CreditsPaid; VeriPaid = s.VeriPaid;
        foreach (var c in s.Chains) Chain(c.Id)?.Load(c);
    }
}
