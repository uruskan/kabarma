namespace Kabarma.Sim;

/// <summary>Tohumlu, belirlenimci rastgele sayi (xorshift). Ayni tohum ayni oyunu verir.</summary>
public sealed class Rng
{
    ulong _s;
    public Rng(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
    public ulong NextU64() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return _s; }
    public float NextFloat() => (NextU64() >> 40) / (float)(1UL << 24);
    public int Range(int minInclusive, int maxExclusive) =>
        minInclusive + (int)(NextU64() % (ulong)Math.Max(1, maxExclusive - minInclusive));
    public float Range(float min, float max) => min + NextFloat() * (max - min);
    public bool Chance(float p) => NextFloat() < p;
}

/// <summary>
/// Esya yuvalari: sirt cantasi, Sandik, El Arabasi, kargo kasasi. Sabit yuva sayisi, esya basina yigin siniri
/// (GDD 5: kaynak 50, ara urun 20, yemek 10; ToolFile.StackOf).
/// Her yuva kaliteyi (★1-3, GDD 8 hasat, GDD 13 fiyat) tasir: yalniz ayni kimlik ve ayni ★ ust uste biner.
/// Kalitesi olmayan esya ★1'dir. Count ve Remove butun kaliteleri kapsar; harcama once dusuk ★'dan.
/// </summary>
public sealed class Inventory
{
    /// <summary>Kalitesi belirtilmeyen esya (kaynak, ara urun, yemek): ★1.</summary>
    public const int BaseStars = 1;

    readonly List<(string id, int count, int stars)> _slots = new();
    readonly Func<string, int> _stackOf;
    public int Slots { get; }
    /// <summary>Varsayilan (kaynak) yigin boyu.</summary>
    public int StackSize { get; }
    public event Action<string, int>? Changed;

    public Inventory(int slots, int stackSize) : this(slots, _ => stackSize) { StackSize = stackSize; }

    public Inventory(int slots, Func<string, int> stackOf)
    {
        Slots = slots;
        _stackOf = stackOf;
        StackSize = stackOf("");
    }

    public int StackOf(string id) => Math.Max(1, _stackOf(id));

    /// <summary>Yuvalar (kimlik, adet); kalite icin <see cref="Stacks"/>.</summary>
    public IReadOnlyList<(string id, int count)> Contents => _slots.Select(s => (s.id, s.count)).ToList();
    /// <summary>Yuvalar kaliteyle (kimlik, adet, ★).</summary>
    public IReadOnlyList<(string id, int count, int stars)> Stacks => _slots;
    public int UsedSlots => _slots.Count;
    public bool IsEmpty => _slots.Count == 0;

    static int Norm(int stars) => Math.Max(BaseStars, stars);

    /// <summary>Butun kaliteler.</summary>
    public int Count(string id) => _slots.Where(s => s.id == id).Sum(s => s.count);

    /// <summary>Yalniz bu kalite.</summary>
    public int Count(string id, int stars) => _slots.Where(s => s.id == id && s.stars == Norm(stars)).Sum(s => s.count);

    /// <summary>Bu esyadan ve bu kaliteden kac tane daha sigar (bos yuvalar ve ayni ★'li yarim yiginlar).</summary>
    public int Room(string id, int stars = BaseStars)
    {
        int stack = StackOf(id);
        stars = Norm(stars);
        return _slots.Where(s => s.id == id && s.stars == stars).Sum(s => stack - Math.Min(stack, s.count)) + (Slots - _slots.Count) * stack;
    }

    /// <returns>Gercekten eklenen adet; canta dolarsa artan kismi disarida kalir.</returns>
    public int Add(string id, int amount) => Add(id, amount, BaseStars);

    /// <summary>Kaliteyle ekler (hasat ★1-3): yalniz ayni kimlik ve ayni ★'li yigina biner.</summary>
    /// <returns>Gercekten eklenen adet.</returns>
    public int Add(string id, int amount, int stars)
    {
        stars = Norm(stars);
        int left = amount;
        int stack = StackOf(id);
        for (int i = 0; i < _slots.Count && left > 0; i++)
        {
            if (_slots[i].id != id || _slots[i].stars != stars || _slots[i].count >= stack) continue;
            int put = Math.Min(left, stack - _slots[i].count);
            _slots[i] = (id, _slots[i].count + put, stars);
            left -= put;
        }
        while (left > 0 && _slots.Count < Slots)
        {
            int put = Math.Min(left, stack);
            _slots.Add((id, put, stars));
            left -= put;
        }
        int added = amount - left;
        if (added > 0) Changed?.Invoke(id, Count(id));
        return added;
    }

    /// <summary>Butun kalitelerden dusurur, once dusuk ★ (tarif ve yakit iyi mali sonra harcar).</summary>
    public bool Remove(string id, int amount)
    {
        if (Count(id) < amount) return false;
        TakeLowFirst(id, amount);
        Changed?.Invoke(id, Count(id));
        return true;
    }

    /// <summary>Yalniz bu kaliteden dusurur.</summary>
    public bool Remove(string id, int amount, int stars)
    {
        stars = Norm(stars);
        if (Count(id, stars) < amount) return false;
        int left = amount;
        for (int i = _slots.Count - 1; i >= 0 && left > 0; i--)
        {
            if (_slots[i].id != id || _slots[i].stars != stars) continue;
            int take = Math.Min(left, _slots[i].count);
            left -= take;
            if (_slots[i].count - take == 0) _slots.RemoveAt(i);
            else _slots[i] = (id, _slots[i].count - take, stars);
        }
        Changed?.Invoke(id, Count(id));
        return true;
    }

    /// <summary>Dusuk ★'dan baslayarak alir (yeterli oldugu denetlenmis olmali).</summary>
    void TakeLowFirst(string id, int amount)
    {
        int left = amount;
        foreach (int stars in _slots.Where(s => s.id == id).Select(s => s.stars).Distinct().OrderBy(x => x).ToList())
        {
            for (int i = _slots.Count - 1; i >= 0 && left > 0; i--)
            {
                if (_slots[i].id != id || _slots[i].stars != stars) continue;
                int take = Math.Min(left, _slots[i].count);
                left -= take;
                if (_slots[i].count - take == 0) _slots.RemoveAt(i);
                else _slots[i] = (id, _slots[i].count - take, stars);
            }
            if (left <= 0) break;
        }
    }

    /// <summary>
    /// Bu esyadan en fazla <paramref name="amount"/> adedi baska bir yuva kumesine tasir (Sandik, araba, kasa), kaliteyi koruyarak;
    /// once dusuk ★. Hedefte yer yoksa o kalite atlanir.
    /// </summary>
    /// <returns>Tasinan adet (hedefte yer kadar).</returns>
    public int MoveTo(Inventory to, string id, int amount)
    {
        int moved = 0;
        foreach (int stars in _slots.Where(s => s.id == id).Select(s => s.stars).Distinct().OrderBy(x => x).ToList())
        {
            int n = Math.Min(Math.Min(amount - moved, Count(id, stars)), to.Room(id, stars));
            if (n <= 0) continue;
            Remove(id, n, stars);
            to.Add(id, n, stars);
            moved += n;
            if (moved >= amount) break;
        }
        return moved;
    }
}

/// <summary>
/// Kemer (GDD 5): 8 yuva, cantadaki esyalara kisayol. Yeni bir esya turu cantaya girince bos yuva varsa kendiliginden eklenir.
/// Sayilar cantadan okunur; kemer kendi basina esya tutmaz.
/// </summary>
public sealed class Belt
{
    public const int Slots = 8;
    readonly string?[] _ids = new string?[Slots];
    public IReadOnlyList<string?> Ids => _ids;

    public Belt(Inventory inv)
    {
        foreach (var (id, _) in inv.Contents) AutoAssign(id);
        inv.Changed += (id, count) => { if (count > 0) AutoAssign(id); };
    }

    public bool Contains(string id) => Array.IndexOf(_ids, id) >= 0;

    public void AutoAssign(string id)
    {
        if (Contains(id)) return;
        int free = Array.IndexOf(_ids, null);
        if (free >= 0) _ids[free] = id;
    }

    /// <summary>Kemerdeyse cikarir, degilse ilk bos yuvaya koyar.</summary>
    public bool Toggle(string id)
    {
        int i = Array.IndexOf(_ids, id);
        if (i >= 0) { _ids[i] = null; return false; }
        int free = Array.IndexOf(_ids, null);
        if (free < 0) return false;
        _ids[free] = id;
        return true;
    }

    public string? At(int slot) => slot >= 0 && slot < Slots ? _ids[slot] : null;

    /// <summary>Envanter izgarasi: esyayi belirli kemer yuvasina koyar (baska yuvadaysa oradan alinir). null yuvayi bosaltir.</summary>
    public void Assign(int slot, string? id)
    {
        if (slot < 0 || slot >= Slots) return;
        if (id != null) { int was = Array.IndexOf(_ids, id); if (was >= 0) _ids[was] = null; }
        _ids[slot] = id;
    }
}
