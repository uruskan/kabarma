namespace Kabarma.Sim;

/// <summary>
/// Borsanin kayda hazir durumu (TEKNIK §9): duz alanlar, kimlikle basvuru. Rastgelelik durumsuzdur (tohum + gun + kategori
/// sirasinin ozeti), bu yuzden durumda RNG yok.
/// </summary>
public sealed class MarketState
{
    /// <summary>Borsa acildi mi (ilk Kabarma safagindan sonra takvimin ilk sabahi; GDD §13 "Borsanin acilisi").</summary>
    public bool Open { get; set; }
    /// <summary>Son islenen takvim gunu.</summary>
    public int Day { get; set; }
    /// <summary>Gunun sicak kategorisi (bos: yok).</summary>
    public string Hot { get; set; } = "";
    /// <summary>Kategori -> carpan (0,6-1,6).</summary>
    public Dictionary<string, float> Mult { get; set; } = new();
    /// <summary>Kategori -> doygunluk (0,4-1,0).</summary>
    public Dictionary<string, float> Saturation { get; set; } = new();
    /// <summary>Kategori -> gunluk Borsa carpani gecmisi (carpan × sicak), en eski once, en fazla historyDays.</summary>
    public Dictionary<string, List<float>> History { get; set; } = new();
    /// <summary>Kategori -> bugune kadar satilan kasa (kabarik urun dahil).</summary>
    public Dictionary<string, int> SoldCrates { get; set; } = new();
}

/// <summary>
/// Borsa (GDD §13, ICERIK §18): 12 kategorinin gunluk dalgalanan carpani, gunun sicak kategorisi (×2) ve satisla dusen,
/// sabahlari geri gelen doygunluk. Fiyat = taban × kalite × Borsa × doygunluk (Pricing.Unit). Kurallar burada, sayilar
/// market.json'da; durum MarketState'te.
/// </summary>
public sealed class Market
{
    readonly MarketFile _m;
    readonly GameData _d;
    readonly ulong _seed;

    public MarketState State { get; private set; }
    public IReadOnlyList<MarketCategoryDef> Categories => _m.Categories;

    /// <summary>Yeni gun islendi (gun).</summary>
    public event Action<int>? DayRolled;

    public Market(GameData d, ulong seed, MarketState? state = null)
    {
        _d = d;
        _m = d.Market;
        _seed = seed;
        State = state ?? new MarketState();
        foreach (var c in _m.Categories)
        {
            State.Mult.TryAdd(c.Id, 1f);
            State.Saturation.TryAdd(c.Id, 1f);
            State.History.TryAdd(c.Id, new List<float>());
            State.SoldCrates.TryAdd(c.Id, 0);
        }
    }

    /// <summary>Kayittan yuklenen durumu baglar (kayit akisi).</summary>
    public void Restore(MarketState s)
    {
        State = s;
        foreach (var c in _m.Categories)
        {
            s.Mult.TryAdd(c.Id, 1f);
            s.Saturation.TryAdd(c.Id, 1f);
            s.History.TryAdd(c.Id, new List<float>());
            s.SoldCrates.TryAdd(c.Id, 0);
        }
    }

    // ---------- Kategori ----------

    /// <summary>Urunun Borsa kategorisi: veride yazili; yoksa yemek "Yemek", digeri "Islenmis Mal" (asla hata vermez).</summary>
    public string Category(string itemId)
    {
        if (_m.Items.TryGetValue(itemId, out var c) && State.Mult.ContainsKey(c)) return c;
        return _d.Tool.IsFood(itemId) ? _m.FoodCategory : _m.DefaultCategory;
    }

    public string CategoryName(string category) => _m.Categories.FirstOrDefault(c => c.Id == category)?.Name ?? category;

    public bool IsHot(string category) => State.Hot == category;

    /// <summary>Borsa carpani: kategori carpani × (sicaksa ×2).</summary>
    public float Borsa(string category) =>
        (State.Mult.TryGetValue(category, out var m) ? m : 1f) * (IsHot(category) ? _m.HotMult : 1f);

    public float Saturation(string category) => State.Saturation.TryGetValue(category, out var s) ? s : 1f;

    /// <summary>Fiyat carpani: Borsa × doygunluk.</summary>
    public float Factor(string category) => Borsa(category) * Saturation(category);

    /// <summary>Urunun fiyat carpani (kategorisinden).</summary>
    public float FactorOf(string itemId) => Factor(Category(itemId));

    // ---------- Gun ----------

    /// <summary>
    /// Takvimin sabahi (Calendar.Morning). Ilk cagri Borsayi acar ve fiyatlari notr birakir (ogretici satislar 120 Kredi);
    /// sonraki her yeni gun icin bir kez <see cref="Roll"/>. Ayni ya da eski gun yok sayilir.
    /// </summary>
    public void OnMorning(int day)
    {
        if (!State.Open)
        {
            State.Open = true;
            State.Day = day;
            Record();
            return;
        }
        while (State.Day < day) Roll(State.Day + 1);
    }

    /// <summary>Bir gunu isler: carpanlar oynar ve cekilir, sinira kirpilir; doygunluk geri gelir; yeni sicak kategori.</summary>
    public void Roll(int day)
    {
        State.Open = true;
        State.Day = day;
        var cats = _m.Categories;
        for (int i = 0; i < cats.Count; i++)
        {
            var id = cats[i].Id;
            float m = State.Mult[id];
            float u = Unit(day, i) * 2f - 1f;
            m += u * _m.DailyStep + _m.Revert * (1f - m);
            if (float.IsNaN(m) || float.IsInfinity(m)) m = 1f;
            State.Mult[id] = Math.Clamp(m, _m.Min, _m.Max);
            State.Saturation[id] = Math.Clamp(State.Saturation[id] + _m.SaturationRecovery, _m.SaturationFloor, 1f);
        }
        State.Hot = cats.Count == 0 ? "" : cats[(int)(Hash(_seed, (ulong)day, 977UL) % (ulong)cats.Count)].Id;
        Record();
        DayRolled?.Invoke(day);
    }

    void Record()
    {
        foreach (var c in _m.Categories)
        {
            var h = State.History[c.Id];
            h.Add(Borsa(c.Id));
            while (h.Count > Math.Max(1, _m.HistoryDays)) h.RemoveAt(0);
        }
    }

    // ---------- Satis ----------

    /// <summary>Bir kasa (ya da kabarik urun) satildi: icindeki her kategori bir kez %3 duser (taban %40).</summary>
    public void RecordSale(IEnumerable<string> categories)
    {
        foreach (var c in categories.Distinct())
        {
            if (!State.Saturation.ContainsKey(c)) continue;
            State.Saturation[c] = Math.Max(_m.SaturationFloor, State.Saturation[c] - _m.SaturationPerCrate);
            State.SoldCrates[c]++;
        }
    }

    /// <summary>Kasanin kategorileri (yigin basina).</summary>
    public IEnumerable<string> CategoriesOf(Inventory items) => items.Stacks.Select(s => Category(s.id)).Distinct();

    // ---------- Rastgelelik (durumsuz) ----------

    float Unit(int day, int cat) => (Hash(_seed, (ulong)day, (ulong)cat + 1) >> 40) / (float)(1UL << 24);

    static ulong Hash(ulong a, ulong b, ulong c)
    {
        ulong x = a * 0x9E3779B97F4A7C15UL ^ (b + 0x632BE59BD9B4E019UL) * 0xBF58476D1CE4E5B9UL ^ (c + 0x85EBCA77C2B2AE63UL) * 0x94D049BB133111EBUL;
        x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27; x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    internal static ulong Mix(ulong a, ulong b, ulong c) => Hash(a, b, c);
}
