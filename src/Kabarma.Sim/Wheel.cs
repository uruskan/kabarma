namespace Kabarma.Sim;

public sealed class WheelState
{
    /// <summary>Toplam cevirme (rastgeleligin sirasi da budur).</summary>
    public int Spins { get; set; }
    /// <summary>Bedava cevirmenin son kullanildigi takvim gunu (0: hic).</summary>
    public int FreeDay { get; set; }
    public int FreeUsedToday { get; set; }
    /// <summary>Sistemi henuz olmayan oduller: Kooperatif deposu (odul kimligi -> adet).</summary>
    public Dictionary<string, int> Tokens { get; set; } = new();
    /// <summary>Son odulun kimligi ve Kredi tutari (panel icin).</summary>
    public string Last { get; set; } = "";
    public int LastCredits { get; set; }
}

/// <summary>Bir cevirmenin sonucu.</summary>
public sealed record WheelResult(WheelRewardDef Reward, int Credits, bool Free, int Paid);

/// <summary>
/// Tohum Carki (GDD §13, ICERIK §18): 500 Kredi (ya da 3 Alaca urun, tarim akisi gelince); gunun ilk cevirmesi bedava.
/// Odul agirlikla secilir; beklenen deger harcananin %85'i (wheel.json value alanlari). Gercek para yok (PLAN).
/// Rastgelelik durumsuz: tohum + cevirme sirasi. Durum WheelState'te.
/// </summary>
public sealed class Wheel
{
    readonly WheelFile _w;
    readonly ulong _seed;
    public WheelState State { get; private set; }

    public Wheel(GameData d, ulong seed, WheelState? state = null)
    {
        _w = d.Wheel;
        _seed = seed;
        State = state ?? new WheelState();
    }

    public void Restore(WheelState s) => State = s;

    public int Cost => _w.Cost;
    public IReadOnlyList<WheelRewardDef> Rewards => _w.Rewards;
    public float TotalWeight => _w.Rewards.Sum(r => r.Weight);

    /// <summary>Bu gun bedava cevirme var mi.</summary>
    public bool FreeAvailable(int day) => State.FreeDay != day || State.FreeUsedToday < _w.FreePerDay;

    /// <summary>Odulun Kredi karsiligi (credits: tutarlarin ortalamasi).</summary>
    public static float ValueOf(WheelRewardDef r) => r.Kind == "credits" && r.Amounts.Count > 0 ? (float)r.Amounts.Average() : r.Value;

    /// <summary>Bir cevirmenin beklenen degeri (Kredi).</summary>
    public float ExpectedValue => TotalWeight <= 0 ? 0 : _w.Rewards.Sum(r => r.Weight * ValueOf(r)) / TotalWeight;

    public float Chance(WheelRewardDef r) => TotalWeight <= 0 ? 0 : r.Weight / TotalWeight;

    /// <summary>
    /// Cevirir. Bugun bedava hak varsa o kullanilir; yoksa <paramref name="spend"/> 500 Krediyi duserse. Odul secilir; Kredi
    /// ve token odulleri burada yazilir (Kredi'yi cagiran ekler), esya odulu cagiranin teslimatina kalir. Kredi yetmezse null.
    /// </summary>
    public WheelResult? Spin(int day, Func<int, bool> spend)
    {
        bool free = FreeAvailable(day);
        if (!free && !spend(_w.Cost)) return null;
        if (free)
        {
            if (State.FreeDay != day) { State.FreeDay = day; State.FreeUsedToday = 0; }
            State.FreeUsedToday++;
        }
        var r = Pick(State.Spins);
        int credits = 0;
        if (r.Kind == "credits" && r.Amounts.Count > 0)
            credits = r.Amounts[(int)(Market.Mix(_seed, (ulong)State.Spins, 31UL) % (ulong)r.Amounts.Count)];
        else if (r.Kind == "token")
            State.Tokens[r.Id] = State.Tokens.GetValueOrDefault(r.Id) + 1;
        State.Spins++;
        State.Last = r.Id;
        State.LastCredits = credits;
        return new WheelResult(r, credits, free, free ? 0 : _w.Cost);
    }

    /// <summary>Cevirme sirasi i icin odul (agirlikli, durumsuz).</summary>
    public WheelRewardDef Pick(int spin)
    {
        float u = (Market.Mix(_seed, (ulong)spin, 7UL) >> 40) / (float)(1UL << 24) * TotalWeight;
        foreach (var r in _w.Rewards)
        {
            if (u < r.Weight) return r;
            u -= r.Weight;
        }
        return _w.Rewards[^1];
    }
}
