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

/// <summary>Sirt cantasi: sabit yuva sayisi, esya basina yigin siniri (GDD 5).</summary>
public sealed class Inventory
{
    readonly List<(string id, int count)> _slots = new();
    public int Slots { get; }
    public int StackSize { get; }
    public event Action<string, int>? Changed;

    public Inventory(int slots, int stackSize) { Slots = slots; StackSize = stackSize; }

    public IReadOnlyList<(string id, int count)> Contents => _slots;
    public int UsedSlots => _slots.Count;

    public int Count(string id) => _slots.Where(s => s.id == id).Sum(s => s.count);

    /// <returns>Gercekten eklenen adet; canta dolarsa artan kismi disarida kalir.</returns>
    public int Add(string id, int amount)
    {
        int left = amount;
        for (int i = 0; i < _slots.Count && left > 0; i++)
        {
            if (_slots[i].id != id || _slots[i].count >= StackSize) continue;
            int put = Math.Min(left, StackSize - _slots[i].count);
            _slots[i] = (id, _slots[i].count + put);
            left -= put;
        }
        while (left > 0 && _slots.Count < Slots)
        {
            int put = Math.Min(left, StackSize);
            _slots.Add((id, put));
            left -= put;
        }
        int added = amount - left;
        if (added > 0) Changed?.Invoke(id, Count(id));
        return added;
    }

    public bool Remove(string id, int amount)
    {
        if (Count(id) < amount) return false;
        int left = amount;
        for (int i = _slots.Count - 1; i >= 0 && left > 0; i--)
        {
            if (_slots[i].id != id) continue;
            int take = Math.Min(left, _slots[i].count);
            left -= take;
            if (_slots[i].count - take == 0) _slots.RemoveAt(i);
            else _slots[i] = (id, _slots[i].count - take);
        }
        Changed?.Invoke(id, Count(id));
        return true;
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
}
