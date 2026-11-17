namespace Kabarma.Sim;

/// <summary>Bir is emri zincirinin ilerlemesi (ICERIK 18: 5 adim). Sayaclar zincir acildiktan sonra sayar.</summary>
public sealed class ChainState
{
    public OrderChainDef Def { get; }
    public bool Opened { get; internal set; }
    public Dictionary<string, int> Counts { get; } = new();
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

/// <summary>
/// Defter: is emri zincirleri (GDD 13, 24; ICERIK 18). Oyun olaylari olcu adiyla bildirilir ("planted", "cargo_sold"...).
/// "Ilk X" adimlari sirasindan bagimsiz isaretlenir (ilk gecede Kabarma kargodan once gelir); siradaki adim ilk bitmemis olandir.
/// M1: odul odenmez (is emri altyapisi M2); Defter'in ilk sayfasi ve takip satiri.
/// </summary>
public sealed class OrderBook
{
    readonly OrdersFile _def;
    readonly List<ChainState> _chains;

    public OrderBook(OrdersFile def)
    {
        _def = def;
        _chains = def.Chains.Select(c => new ChainState(c) { Opened = c.OpensAt == "start" }).ToList();
    }

    public IReadOnlyList<ChainState> Chains => _chains;
    public bool DefterOpen { get; private set; }
    public int OrdersTotal => _def.ChainsTotal * _def.StepsPerChain;
    public int ChainsTotal => _def.ChainsTotal;

    /// <summary>(zincir, biten adim indeksi).</summary>
    public event Action<ChainState, int>? StepDone;
    public event Action? DefterOpened;

    public ChainState? Chain(string id) => _chains.FirstOrDefault(c => c.Def.Id == id);

    /// <summary>PLAN 1.4: ilk Hasat Raporu'nda Defter acilir; "defter" zincirleri bundan sonra sayar.</summary>
    public void OpenDefter()
    {
        if (DefterOpen) return;
        DefterOpen = true;
        foreach (var c in _chains) if (c.Def.OpensAt == "defter") c.Opened = true;
        DefterOpened?.Invoke();
    }

    public void Report(string measure, int amount = 1)
    {
        if (amount <= 0) return;
        foreach (var c in _chains)
        {
            if (!c.Opened || c.Def.Steps.All(s => s.Measure != measure)) continue;
            c.Counts[measure] = c.Count(measure) + amount;
            foreach (int i in c.Latch().ToList()) StepDone?.Invoke(c, i);
        }
    }

    /// <summary>Takip edilen zincir: acik ve bitmemis zincirlerin sonuncusu (Defter acilinca "siradaki is emri").</summary>
    public ChainState? Tracked => _chains.LastOrDefault(c => c.Opened && !c.Done);
}
