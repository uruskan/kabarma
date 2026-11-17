namespace Kabarma.Sim;

/// <summary>Ilk 10 dakikanin adim zinciri (PLAN 1.4). Godot katmani olaylari bildirir, senaryo ilerler.</summary>
public sealed class Scenario
{
    readonly ScenarioFile _def;
    public int StepIndex { get; private set; }
    public int Progress { get; private set; }
    public bool Finished => StepIndex >= _def.Steps.Count;
    public StepDef? Current => Finished ? null : _def.Steps[StepIndex];

    /// <summary>(biten adim, yeni adim). Yeni adim null ise senaryo bitti.</summary>
    public event Action<StepDef, StepDef?>? StepChanged;
    public event Action<StepDef, int>? ProgressChanged;

    public Scenario(ScenarioFile def) { _def = def; }

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
public sealed class GameState
{
    public GameData Data { get; }
    public Inventory Backpack { get; }
    public Farm Farm { get; }
    public Scenario Scenario { get; }
    public KabarmaNight? Night { get; private set; }
    public int Credits { get; private set; }
    public int Eggs { get; set; }
    public List<(string crop, string size, int value)> Sales { get; } = new();
    public ulong Seed { get; }

    public event Action<int, int>? Sold; // (kazanc, toplam)

    public GameState(GameData data, ulong seed = 4417)
    {
        Data = data;
        Seed = seed;
        Backpack = new Inventory(data.Tool.BackpackSlots, data.Tool.StackResource);
        foreach (var (id, n) in data.Tool.StartItems) Backpack.Add(id, n);
        Farm = new Farm(data);
        Scenario = new Scenario(data.Scenario);
    }

    public KabarmaNight StartNight()
    {
        Night = new KabarmaNight(Data, Data.Kabarma.FirstNight, Seed * 31 + 7);
        return Night;
    }

    public int Sell(string cropId, SizeClassDef size)
    {
        int v = Farm.SellValue(cropId, size);
        Credits += v;
        Sales.Add((cropId, size.Id, v));
        Sold?.Invoke(v, Credits);
        return v;
    }
}
