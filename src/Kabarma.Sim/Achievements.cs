namespace Kabarma.Sim;

/// <summary>Bir basarim (ICERIK 18): kimlik Steam API adina da temel olur (M6).</summary>
public sealed record Achievement(string Id, string Name, string Text, bool IsChain);

/// <summary>
/// Basarim altyapisi: olaydan basarima baglanti. Iki kaynak: bir is emri zincirinin tamamlanmasi (kimlik
/// "chain_" + zincir) ve genel sayac esigi (achievements.json extras; OrderBook.Stats). Acilan basarimlar duz bir
/// kumede durur (karakter dosyasi; TEKNIK 9). Steam'e baglama M6: Unlocked olayina abone olunur.
/// </summary>
public sealed class AchievementBook
{
    readonly AchievementsFile _def;
    readonly OrderBook _orders;
    readonly List<Achievement> _all = new();
    readonly HashSet<string> _unlocked = new();

    public AchievementBook(AchievementsFile def, OrderBook orders)
    {
        _def = def;
        _orders = orders;
        foreach (var e in def.Extras) _all.Add(new Achievement(e.Id, e.Name, e.Text, false));
        foreach (var c in orders.Chains)
            _all.Add(new Achievement(def.ChainPrefix + c.Def.Id, c.Def.Name, def.ChainText, true));
        orders.ChainDone += c => Unlock(def.ChainPrefix + c.Def.Id);
        orders.StatChanged += (stat, value) =>
        {
            foreach (var e in def.Extras) if (e.Stat == stat && value >= e.Target) Unlock(e.Id);
        };
    }

    public IReadOnlyList<Achievement> All => _all;
    public IReadOnlyCollection<string> Unlocked => _unlocked;
    /// <summary>Oyundaki basarim sayisi (zincirler 48'e tamamlaninca 50).</summary>
    public int Count => _all.Count;
    public int Total => _def.Total;

    public bool IsUnlocked(string id) => _unlocked.Contains(id);

    public event Action<Achievement>? UnlockedEvent;

    bool Unlock(string id)
    {
        if (!_unlocked.Add(id)) return false;
        var a = _all.FirstOrDefault(x => x.Id == id);
        if (a != null) UnlockedEvent?.Invoke(a);
        return true;
    }

    public List<string> Export() => _unlocked.OrderBy(x => x, StringComparer.Ordinal).ToList();

    public void Import(IEnumerable<string> ids)
    {
        _unlocked.Clear();
        foreach (var id in ids) if (_all.Any(a => a.Id == id)) _unlocked.Add(id);
    }
}
