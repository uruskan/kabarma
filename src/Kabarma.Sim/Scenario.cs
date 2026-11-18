namespace Kabarma.Sim;

/// <summary>Ilk 10 dakikanin adim zinciri (PLAN 1.4). Godot katmani olaylari bildirir, senaryo ilerler.</summary>
public sealed partial class Scenario
{
    readonly List<StepDef> _steps;
    public int StepIndex { get; private set; }
    public int Progress { get; private set; }
    public bool Finished => StepIndex >= _steps.Count;
    public StepDef? Current => Finished ? null : _steps[StepIndex];
    /// <summary>Oynanacak adimlar (veride on kosulu olmayanlar cikarilmis).</summary>
    public IReadOnlyList<StepDef> Steps => _steps;
    /// <summary>Yuklemede devre disi kalan adimlar ve eksik kimlikleri (StepDef.Requires).</summary>
    public List<(StepDef step, List<string> missing)> Skipped { get; } = new();

    /// <summary>(biten adim, yeni adim). Yeni adim null ise senaryo bitti.</summary>
    public event Action<StepDef, StepDef?>? StepChanged;
    public event Action<StepDef, int>? ProgressChanged;

    /// <param name="missing">Adimin veride olmayan on kosullari; bos degilse adim atlanir. Null: hepsi oynanir.</param>
    public Scenario(ScenarioFile def, Func<StepDef, List<string>>? missing = null)
    {
        _steps = new List<StepDef>();
        foreach (var s in def.Steps)
        {
            var miss = missing?.Invoke(s) ?? new List<string>();
            if (miss.Count > 0) Skipped.Add((s, miss));
            else _steps.Add(s);
        }
    }

    public bool Is(string id) => Current?.Id == id;

    /// <summary>Su anki adim bu olaysa sayaci ilerletir. Baska adimin olayi yok sayilir.</summary>
    public void Report(string id, int amount = 1) => SetProgress(id, Progress + amount);

    /// <summary>Sayaci mutlak degere esitler (ornegin olgun ekin sayisi).</summary>
    public void SetProgress(string id, int value)
    {
        if (!Is(id)) return;
        Progress = Math.Max(Progress, value);
        ProgressChanged?.Invoke(Current!, Progress);
        if (Progress >= Current!.Target) Advance();
    }

    void Advance()
    {
        var done = Current!;
        StepIndex++;
        Progress = 0;
        StepChanged?.Invoke(done, Current);
    }
}

/// <summary>Bir oyunun butun kural durumu. Godot katmani bunu tutar ve gorur.</summary>
public sealed partial class GameState
{
    public GameData Data { get; }
    public Inventory Backpack { get; }
    public Farm Farm { get; }
    public Scenario Scenario { get; }
    public KabarmaNight? Night { get; private set; }
    public int Credits { get; private set; }
    /// <summary>Gidak yumurtasi canta esyasidir ("egg", ICERIK 15); sayi cantadan okunur.</summary>
    public int Eggs => Backpack.Count("egg");
    public List<(string crop, string size, int value)> Sales { get; } = new();
    public ulong Seed { get; }
    public Rng Rng { get; }
    public FoodBuffs Food { get; }
    public Belt Belt { get; }
    /// <summary>Sulama basliginin su deposu (WaterTank.cs).</summary>
    public WaterTank Tank { get; }
    public Dictionary<string, StationQueue> Stations { get; } = new();
    /// <summary>Oyuncunun dayanikliligi (GDD §3.2); Stamina.cs.</summary>
    public Stamina Stamina { get; }
    /// <summary>Kargo: Paketleme, rampa, gecikmeli odeme (GDD 13).</summary>
    public Cargo Cargo { get; }
    /// <summary>Oyuncunun eline bir kez gecmis esyalar: kilitli tarifleri acar (GDD 12 Kesif).</summary>
    public HashSet<string> Known { get; } = new();
    /// <summary>Agil, evcil hayvanlar, Yemlik ve Folluk (GDD 9.1).</summary>
    public Coop Coop { get; }

    public event Action<int, int>? Sold; // (kazanc, toplam)

    public GameState(GameData data, ulong seed = 4417)
    {
        Data = data;
        Seed = seed;
        Backpack = new Inventory(data.Tool.BackpackSlots, data.Tool.StackOf);
        Backpack.Changed += (id, count) => { if (count > 0) Known.Add(id); };
        foreach (var (id, n) in data.Tool.StartItems) Backpack.Add(id, n);
        Farm = new Farm(data);
        Scenario = new Scenario(data.Scenario, data.MissingFor);
        Rng = new Rng(seed * 13 + 1);
        Belt = new Belt(Backpack, data.Tool.BeltTools, data.Tool.BeltStart, data.Tool.IsHoldable); // karar 23: Capa ve Insa Plani kemerde
        Tank = new WaterTank(data.Tool.Sulama);
        Stamina = new Stamina(data.Tool.Player.Stamina);
        Food = new FoodBuffs(data.Tool.FoodSlots);
        foreach (var st in data.Tool.Recipes.Select(r => r.Station).Where(s => !string.IsNullOrEmpty(s)).Distinct()) Stations[st] =new StationQueue(data, st) { Known = Known };
        Cargo = new Cargo(data, Credit);
        Coop = new Coop(data.Pen);
        InitNightAndDay(); // GameState.Night.cs: takvim, Defter (is emirleri), gece sayaclari
        InitMarket(); // GameState.Market.cs: Borsa, Katalog, Tohum Carki (ekonomi2)
    }

    /// <summary>Odeme hesaba gecer (Kargo Rampasi; ilk satis aninda, sonrakiler 60 sn sonra).</summary>
    void Credit(int v, string what)
    {
        Credits += v;
        Sales.Add((what, "", v));
        Sold?.Invoke(v, Credits);
    }

    public KabarmaNight StartNight(string? memberCard = null)
    {
        Night = new KabarmaNight(Data, Data.Kabarma.FirstNight, Seed * 31 + 7 + (ulong)NightsStarted, memberCard);
        OnNightStarted(Night); // GameState.Night.cs: gece sayaci, tarla olaylari
        return Night;
    }

    /// <summary>
    /// Kabarik urunu Kargo Rampasi'ndan firlatir (rampa bos olmali). Donen: urunun degeri (rampa doluysa -1).
    /// Odeme ilk satista aninda, sonra 60 sn sonra gelir (Cargo.Tick).
    /// </summary>
    public int Sell(string cropId, SizeClassDef size)
    {
        int v = SwollenPrice(cropId, Farm.SellValue(cropId, size)); // Borsa × doygunluk (GameState.Market.cs)
        int shipped = Cargo.ShipSwollen(v, $"{cropId} {size.Id}");
        if (shipped >= 0) SwollenSold(cropId);
        return shipped;
    }
}
