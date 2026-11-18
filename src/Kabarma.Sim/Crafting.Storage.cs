namespace Kabarma.Sim;

/// <summary>
/// Uretimin malzeme aldigi ve urun koydugu yer (GDD §5): tek canta ya da canta + Sandiklar (ItemPool).
/// Inventory bunu uygular; tarif kurallari (CraftRules, StationQueue, CraftQueue) bunu ister.
/// </summary>
public interface IItemStore
{
    int Count(string id);
    /// <summary>Yetmiyorsa hic almaz, false.</summary>
    bool Remove(string id, int amount);
    /// <returns>Gercekten eklenen.</returns>
    int Add(string id, int amount);
    /// <summary>Bu esyadan kac tane daha sigar (★1).</summary>
    int Room(string id);
}

public sealed partial class Inventory : IItemStore
{
    int IItemStore.Room(string id) => Room(id);

    /// <summary>
    /// Envanter duzeni (GDD §5): ayni kimlik ve ★'li yarim yiginlari birlestirir, yuvalari siraya dizer (once
    /// <paramref name="order"/>, sonra kimlik, sonra yuksek ★). Adetler degismez; Changed atilmaz (kemer yuvasi dusmez).
    /// </summary>
    public void Sort(Func<string, int>? order = null)
    {
        var totals = _slots.GroupBy(s => (s.id, s.stars)).Select(g => (g.Key.id, g.Key.stars, n: g.Sum(s => s.count)))
            .OrderBy(x => order?.Invoke(x.id) ?? 0).ThenBy(x => x.id, StringComparer.Ordinal).ThenByDescending(x => x.stars).ToList();
        _slots.Clear();
        foreach (var (id, stars, n) in totals)
        {
            int stack = StackOf(id), left = n;
            while (left > 0) { int put = Math.Min(stack, left); _slots.Add((id, put, stars)); left -= put; }
        }
    }

    /// <summary>
    /// Hizli yigma (Rust "quick stack"): hedefte zaten bulunan her esyanin hepsini bu yuva kumesinden hedefe tasir,
    /// kaliteyi koruyarak. Donen: tasinan toplam adet.
    /// </summary>
    public int QuickStackTo(Inventory to)
    {
        int moved = 0;
        foreach (var id in to.Contents.Select(c => c.id).Distinct().ToList()) moved += MoveTo(to, id, Count(id));
        return moved;
    }
}

/// <summary>
/// Ambardan uretim (GDD §5): sirali depolar; malzeme once ilkinden (canta), eksigi siradakilerden (tapu icindeki Sandiklar)
/// alinir, urun ayni sirayla konur. Duz: depolara yalniz baglanti tutar, kendi durumu yok.
/// </summary>
public sealed class ItemPool : IItemStore
{
    readonly List<IItemStore> _stores;
    public IReadOnlyList<IItemStore> Stores => _stores;

    public ItemPool(IEnumerable<IItemStore> stores) { _stores = stores.Where(s => s != null).Distinct().ToList(); }
    public ItemPool(params IItemStore[] stores) : this((IEnumerable<IItemStore>)stores) { }

    public int Count(string id) => _stores.Sum(s => s.Count(id));

    public bool Remove(string id, int amount)
    {
        if (amount <= 0) return true;
        if (Count(id) < amount) return false;
        int left = amount;
        foreach (var s in _stores)
        {
            if (left <= 0) break;
            int take = Math.Min(left, s.Count(id));
            if (take > 0 && s.Remove(id, take)) left -= take;
        }
        return true;
    }

    public int Add(string id, int amount)
    {
        int left = amount;
        foreach (var s in _stores) { if (left <= 0) break; left -= s.Add(id, left); }
        return amount - left;
    }

    public int Room(string id) => _stores.Sum(s => s.Room(id));
}
