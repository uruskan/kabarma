namespace Kabarma.Sim;

/// <summary>Tohumlu, belirlenimci rastgele sayi (xorshift). Ayni tohum ayni oyunu verir.</summary>
public sealed class Rng
{
    ulong _s;
    public Rng(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
    /// <summary>Ic durum (kayit, TEKNIK 9.1): ayni durum ayni diziyi surdurur.</summary>
    public ulong State { get => _s; set => _s = value == 0 ? 0x9E3779B97F4A7C15UL : value; }
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
public sealed partial class Inventory
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

    /// <summary>Kayittan yuvalari aynen (sira dahil) yerine koyar; olay yaymaz (TEKNIK 9.1 sessiz yukleme).</summary>
    public void Load(IEnumerable<(string id, int count, int stars)> stacks)
    {
        _slots.Clear();
        foreach (var (id, count, stars) in stacks)
            if (!string.IsNullOrEmpty(id) && count > 0 && _slots.Count < Slots) _slots.Add((id, count, Norm(stars)));
    }

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
/// Kemer (GDD 4, 5; URETIM 6 M1 karari 23): 8 yuva. Secili yuvadaki esya eldedir; bos yuva secilirse eller bostur.
/// Aletler (Capa, Insa Plani) cantaya girmez, kemerde hep vardir. Obur yuvalar cantadaki esyalara kisayoldur: elde
/// tutulabilen (yerlestirilebilir, yemek) yeni bir esya cantaya girince bos yuva varsa kendiliginden eklenir; cantada
/// bitince yuvadan duser. Sayilar cantadan okunur; kemer kendi basina esya tutmaz.
/// </summary>
public sealed class Belt
{
    public const int Slots = 8;
    readonly string?[] _ids = new string?[Slots];
    readonly HashSet<string> _tools = new();
    readonly Func<string, bool> _auto;
    public IReadOnlyList<string?> Ids => _ids;
    /// <summary>Secili yuva (0..7); -1: hicbiri (eller bos).</summary>
    public int Selected { get; private set; } = -1;
    /// <summary>Elde tutulan esya: secili yuvanin kimligi; yuva bossa ya da esya bittiyse null.</summary>
    public string? Held => Selected >= 0 ? _ids[Selected] : null;
    /// <summary>Secim degisti: (eski yuva, yeni yuva).</summary>
    public event Action<int, int>? SelectionChanged;

    /// <param name="tools">Cantaya girmeyen aletler (craft.json beltTools).</param>
    /// <param name="start">Baslangic dizilisi (craft.json beltStart); ilk yuva secili baslar.</param>
    /// <param name="autoBelt">Kemere kendiliginden giren esya turu (ToolFile.IsHoldable); null: hepsi.</param>
    public Belt(Inventory inv, IEnumerable<string>? tools = null, IEnumerable<string>? start = null, Func<string, bool>? autoBelt = null)
    {
        foreach (var t in tools ?? Array.Empty<string>()) _tools.Add(t);
        _auto = autoBelt ?? (_ => true);
        int k = 0;
        foreach (var id in start ?? Array.Empty<string>()) if (k < Slots) _ids[k++] = id;
        if (k > 0) Selected = 0;
        foreach (var (id, _) in inv.Contents) AutoAssign(id);
        inv.Changed += (id, count) =>
        {
            if (count > 0) AutoAssign(id);
            else if (!IsTool(id)) { int i = Array.IndexOf(_ids, id); if (i >= 0) _ids[i] = null; } // bitti: yuvadan duser
        };
    }

    public bool IsTool(string id) => _tools.Contains(id);

    /// <summary>Kayittan yuvalari ve secimi aynen koyar; SelectionChanged yaymaz (TEKNIK 9.1 sessiz yukleme).</summary>
    public void Load(IReadOnlyList<string?> ids, int selected)
    {
        for (int i = 0; i < Slots; i++) _ids[i] = i < ids.Count && !string.IsNullOrEmpty(ids[i]) ? ids[i] : null;
        Selected = selected >= -1 && selected < Slots ? selected : -1;
    }

    public bool Contains(string id) => Array.IndexOf(_ids, id) >= 0;

    /// <summary>Yuva sec (1-8, tekerlek). Ayni yuvaya yeniden basmak secimi korur.</summary>
    public void Select(int slot)
    {
        if (slot < -1 || slot >= Slots || slot == Selected) return;
        int was = Selected;
        Selected = slot;
        SelectionChanged?.Invoke(was, slot);
    }

    /// <summary>Tekerlek: bir sonraki (+1) ya da onceki (-1) yuva; uclarda doner. Bos yuvalar da secilir (eller bos).</summary>
    public void Scroll(int dir)
    {
        if (dir == 0) return;
        int from = Selected < 0 ? (dir > 0 ? -1 : 0) : Selected;
        Select(((from + Math.Sign(dir)) % Slots + Slots) % Slots);
    }

    /// <summary>Esyanin kemerdeki yuvasi; yoksa -1.</summary>
    public int SlotOf(string id) => Array.IndexOf(_ids, id);

    public void AutoAssign(string id)
    {
        if (Contains(id) || !_auto(id)) return;
        int free = Array.IndexOf(_ids, null);
        if (free >= 0) _ids[free] = id;
    }

    /// <summary>Kemerdeyse cikarir (alet cikmaz), degilse ilk bos yuvaya koyar.</summary>
    public bool Toggle(string id)
    {
        int i = Array.IndexOf(_ids, id);
        if (i >= 0) { if (!IsTool(id)) _ids[i] = null; return IsTool(id); }
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
        var cur = _ids[slot];
        if (cur == id) return;
        int was = id != null ? Array.IndexOf(_ids, id) : -1;
        if (cur != null && IsTool(cur))
        {
            // Alet kemerden cikmaz: yerine gelen esyanin eski yuvasina ya da ilk bos yuvaya kayar.
            int to = was >= 0 ? was : Array.IndexOf(_ids, null);
            if (id == null || to < 0) return;
            _ids[to] = cur;
            _ids[slot] = id;
            return;
        }
        if (was >= 0) _ids[was] = null;
        _ids[slot] = id;
    }
}
