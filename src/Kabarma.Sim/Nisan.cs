namespace Kabarma.Sim;

/// <summary>Kart defterinin kaydi (karakter dosyasi): acik kartlar, Nisan puanlari, kart jetonlari.</summary>
public sealed class CardBookSave
{
    public List<string> Unlocked { get; set; } = new();
    public Dictionary<string, int> Points { get; set; } = new();
    public int Jetons { get; set; }
}

/// <summary>
/// Gecelerin otesinde kalan kart durumu (GDD 15): hangi kartlar acik, Nisan puanlari ve seviyeleri, kart jetonlari.
/// 20 kart bastan aciktir; digerleri Unlock ile (is emri, uye tipi, Ata) ya da jetonla acilir. Asi kartlari saklanmaz:
/// iki ebeveyni acikken kendiliginden aciktir. Karakter dosyasinda durur (dunyalar arasi tasinir).
/// </summary>
public sealed class CardBook
{
    readonly CardsFile _c;
    readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> _points = new(StringComparer.Ordinal);

    public int Jetons { get; private set; }
    public event Action<string, string>? Unlocked;
    public event Action<string, int>? NisanUp;

    public CardBook(CardsFile cards) { _c = cards; Reset(); }

    void Reset()
    {
        _unlocked.Clear(); _points.Clear(); Jetons = 0;
        foreach (var c in _c.Cards) if (c.Unlock == "start") _unlocked.Add(c.Id);
    }

    public bool IsUnlocked(string id)
    {
        var c = _c.Find(id);
        if (c == null) return false;
        if (c.IsAsi) return c.Parents.Count == 2 && c.Parents.All(IsUnlocked);
        return _unlocked.Contains(id);
    }

    public IEnumerable<CardDef> Open => _c.Cards.Where(c => IsUnlocked(c.Id));

    /// <summary>Bir karti acar (is emri odulu, uye tipi, Ata). source yalniz olay icin. Zaten aciksa false.</summary>
    public bool Unlock(string id, string source)
    {
        var c = _c.Card(id);
        if (c.IsAsi) throw new InvalidOperationException($"Asi kendiliginden acilir: {id}");
        if (!_unlocked.Add(id)) return false;
        Unlocked?.Invoke(id, source);
        return true;
    }

    public void AddJeton(int n = 1) => Jetons += n;

    /// <summary>Jeton havuzunda kapali kalan kartlar (ICERIK 8.3).</summary>
    public IEnumerable<string> JetonChoices => _c.JetonPool.Where(id => !_unlocked.Contains(id));

    /// <summary>
    /// Bir jeton harcar: havuzda kapali kart varsa onu acar; havuz bittiyse secilen acik karta 1 Nisan puani verir
    /// (ICERIK 8.3). Gecersiz secim ya da jeton yoksa false.
    /// </summary>
    public bool SpendJeton(string id)
    {
        if (Jetons <= 0) return false;
        if (JetonChoices.Any())
        {
            if (!JetonChoices.Contains(id)) return false;
            Jetons--;
            _unlocked.Add(id);
            Unlocked?.Invoke(id, "jeton");
            return true;
        }
        var c = _c.Find(id);
        if (c == null || c.IsAsi || !IsUnlocked(id)) return false;
        Jetons--;
        AddPoint(id);
        return true;
    }

    public int Points(string id) => _points.GetValueOrDefault(id);

    /// <summary>Nisan seviyesi 0-5: 3, 8, 15, 25, 40 puanda.</summary>
    public int Level(string id)
    {
        int p = Points(id), lv = 0;
        foreach (var t in _c.Nisan.Thresholds) if (p >= t) lv++;
        return lv;
    }

    /// <summary>Kesirli etki carpani: 1 + %4 x Nisan.</summary>
    public float Bonus(string id) => 1f + _c.Nisan.BonusPerLevel * Level(id);

    public void AddPoint(string id)
    {
        int before = Level(id);
        _points[id] = Points(id) + 1;
        if (Level(id) > before) NisanUp?.Invoke(id, Level(id));
    }

    /// <summary>Safak: geceyi tavan rutbesinde bitiren her kart 1 puan (GDD 15).</summary>
    public List<string> AwardNight(IReadOnlyDictionary<string, int> ranks)
    {
        var won = ranks.Where(kv => _c.Find(kv.Key) is { } c && kv.Value >= _c.RankCap(c)).Select(kv => kv.Key)
                       .OrderBy(x => x, StringComparer.Ordinal).ToList();
        foreach (var id in won) AddPoint(id);
        return won;
    }

    public CardBookSave Export() => new()
    {
        Unlocked = _unlocked.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        Points = _points.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value),
        Jetons = Jetons,
    };

    /// <summary>Sessiz yukleme: olay yaymaz. Bilinmeyen kimlikler atlanir; baslangic kartlari her zaman aciktir.</summary>
    public void Import(CardBookSave s)
    {
        Reset();
        foreach (var id in s.Unlocked ?? new()) if (_c.Find(id) is { IsAsi: false }) _unlocked.Add(id);
        foreach (var (id, p) in s.Points ?? new()) if (_c.Find(id) != null && p > 0) _points[id] = p;
        Jetons = Math.Max(0, s.Jetons);
    }
}
