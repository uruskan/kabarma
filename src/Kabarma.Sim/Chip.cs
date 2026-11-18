namespace Kabarma.Sim;

/// <summary>Bir cip ornegi (Cip Deposu'nda ya da takili). Duz veri: kayda hazir.</summary>
public sealed class ChipInstance
{
    public int Id { get; set; }
    public string Type { get; set; } = "";
    public string Rarity { get; set; } = "common";
}

public enum ChipResult { Ok, NoSuchChip, AlreadyInstalled, NoSuchHead, Incompatible, Duplicate, NoSlot, Locked, StorageFull, Missing }

/// <summary>
/// Cipler (ICERIK 6.3, 6.3.1; GDD 4): Cip Deposu, baslik yuvalari, etki toplami, dusme ve uretim. Duz durum (TEKNIK 9).
/// Yuva sayisi Capa Mk'sinden (Ocak basamagi) gelir. Kabarma gecesi takma/cikarma kilitlidir (Locked).
/// Etkiler stat kimligiyle sorulur: Stat("tirpan_isini", "crit_chance").
/// </summary>
public sealed class ChipBook
{
    readonly ChipsFile _def;
    readonly Func<int> _mk;
    readonly List<ChipInstance> _owned = new();
    readonly Dictionary<string, List<int>> _slots = new(); // baslik -> yuva basina ornek kimligi (0 = bos)

    public ChipBook(ChipsFile def, Func<int> mk) { _def = def; _mk = mk; }

    public ChipsFile Def => _def;
    /// <summary>Verilecek sonraki ornek kimligi (1'den).</summary>
    public int NextId { get; private set; } = 1;
    /// <summary>true: takma ve cikarma yapilamaz (Kabarma gecesi). Godot katmani ayarlar; kayda girmez.</summary>
    public bool Locked { get; set; }

    public IReadOnlyList<ChipInstance> Owned => _owned;
    public event Action<ChipInstance>? Added;
    public event Action<string, int, ChipInstance>? Installed; // baslik, yuva, cip
    public event Action<string, int, ChipInstance>? Removed;

    public ChipDef? TypeOf(string id) => _def.Chips.FirstOrDefault(c => c.Id == id);
    public ChipRarityDef? RarityOf(string id) => _def.Rarities.FirstOrDefault(r => r.Id == id);
    public ChipInstance? Get(int id) => _owned.FirstOrDefault(c => c.Id == id);

    public bool IsHead(string head) => _def.WeaponHeads.Contains(head) || _def.WorkHeads.Contains(head);
    public bool IsWeaponHead(string head) => _def.WeaponHeads.Contains(head);

    /// <summary>Baslik basina yuva sayisi (Capa Mk'sine gore).</summary>
    public int Capacity()
    {
        var t = _def.SlotsByMk;
        return t.Count == 0 ? 0 : t[Math.Clamp(_mk() - 1, 0, t.Count - 1)];
    }

    /// <summary>Cip turu bu basliga takilabilir mi (grup kurali)?</summary>
    public bool Compatible(ChipDef chip, string head) => chip.Group switch
    {
        "weapon" => IsWeaponHead(head),
        "work" => chip.Host == head,
        _ => IsHead(head),
    };

    /// <summary>Baslik yuvalari (Capacity kadar; 0 = bos).</summary>
    public IReadOnlyList<int> SlotsOf(string head)
    {
        int cap = Capacity();
        var l = _slots.TryGetValue(head, out var s) ? s : null;
        var res = new List<int>(cap);
        for (int i = 0; i < cap; i++) res.Add(l != null && i < l.Count ? l[i] : 0);
        return res;
    }

    public bool IsInstalled(int chipId) => _slots.Values.Any(l => l.Contains(chipId));
    public IEnumerable<ChipInstance> InStorage => _owned.Where(c => !IsInstalled(c.Id));
    public IEnumerable<ChipInstance> On(string head) => SlotsOf(head).Where(i => i != 0).Select(i => Get(i)!);

    /// <summary>Depoya yeni cip ekler (dusme, uretim, cark). Doluysa null.</summary>
    public ChipInstance? Add(string type, string rarity)
    {
        if (TypeOf(type) == null || RarityOf(rarity) == null || _owned.Count >= _def.StorageMax) return null;
        var c = new ChipInstance { Id = NextId++, Type = type, Rarity = rarity };
        _owned.Add(c);
        Added?.Invoke(c);
        return c;
    }

    public ChipResult CanInstall(string head, int chipId, int slot = -1)
    {
        var c = Get(chipId);
        if (c == null) return ChipResult.NoSuchChip;
        if (IsInstalled(chipId)) return ChipResult.AlreadyInstalled;
        if (!IsHead(head)) return ChipResult.NoSuchHead;
        if (Locked) return ChipResult.Locked;
        var def = TypeOf(c.Type)!;
        if (!Compatible(def, head)) return ChipResult.Incompatible;
        if (On(head).Any(o => o.Type == c.Type)) return ChipResult.Duplicate;
        var slots = SlotsOf(head);
        if (slot >= slots.Count) return ChipResult.NoSlot;
        if (slot < 0 ? !slots.Contains(0) : slots[slot] != 0) return ChipResult.NoSlot;
        return ChipResult.Ok;
    }

    /// <summary>Cipi basligin yuvasina takar (slot -1: ilk bos).</summary>
    public ChipResult Install(string head, int chipId, int slot = -1)
    {
        var r = CanInstall(head, chipId, slot);
        if (r != ChipResult.Ok) return r;
        var list = _slots.TryGetValue(head, out var l) ? l : _slots[head] = new();
        int cap = Capacity();
        while (list.Count < cap) list.Add(0);
        if (slot < 0) slot = list.IndexOf(0);
        list[slot] = chipId;
        Installed?.Invoke(head, slot, Get(chipId)!);
        return ChipResult.Ok;
    }

    /// <summary>Yuvadaki cipi depoya geri koyar.</summary>
    public ChipResult Remove(string head, int slot)
    {
        if (Locked) return ChipResult.Locked;
        if (!_slots.TryGetValue(head, out var l) || slot < 0 || slot >= l.Count || l[slot] == 0) return ChipResult.NoSlot;
        var c = Get(l[slot])!;
        l[slot] = 0;
        Removed?.Invoke(head, slot, c);
        return ChipResult.Ok;
    }

    // ---- Etki ----

    /// <summary>Bir cipin bir stat'a verdigi deger: nadirlik x scale (+ Efsane ek etkisi).</summary>
    public float Contribution(ChipInstance c, string stat)
    {
        var def = TypeOf(c.Type);
        var rar = RarityOf(c.Rarity);
        if (def == null || rar == null) return 0f;
        float v = def.Stat == stat ? rar.Bonus * def.Scale : 0f;
        if (c.Rarity == "legendary" && def.Legendary.Stat == stat) v += def.Legendary.Value;
        return v;
    }

    /// <summary>Basliga takili cipler stat'a toplam ne verir (0,15 = +%15).</summary>
    public float Stat(string head, string stat) => On(head).Sum(c => Contribution(c, stat));

    // ---- Dusme (ICERIK 6.3.1) ----

    /// <summary>
    /// Kaynaktan dusme: olasilik tutarsa depoya bir cip eklenir (Efsane dusmez). ks: Kabarma odulunde seviye.
    /// Donen: eklenen cip ya da null (dusmedi, bilinmeyen kaynak, depo dolu).
    /// </summary>
    public ChipInstance? Drop(string source, Rng rng, int ks = 0)
    {
        if (!_def.Sources.TryGetValue(source, out var s) || !rng.Chance(s.Chance)) return null;
        var w = new SortedDictionary<string, float>(s.Weights, StringComparer.Ordinal);
        if (ks > 0)
        {
            w["rare"] = w.GetValueOrDefault("rare") + ks * s.KsRarePerKs;
            w["epic"] = w.GetValueOrDefault("epic") + ks * s.KsEpicPerKs;
        }
        float total = w.Values.Sum();
        if (total <= 0) return null;
        float roll = rng.NextFloat() * total;
        string rarity = w.Keys.Last();
        foreach (var (k, v) in w) { if (roll < v) { rarity = k; break; } roll -= v; }
        if (rarity == "legendary") rarity = "epic";
        var type = _def.Chips[rng.Range(0, _def.Chips.Count)];
        return Add(type.Id, rarity);
    }

    // ---- Uretim ----

    /// <summary>Uretim bedeli (esya -> adet).</summary>
    public IReadOnlyDictionary<string, int> CraftCost(string rarity) =>
        _def.Craft.TryGetValue(rarity, out var c) ? c : new Dictionary<string, int>();

    public bool CanCraft(string type, string rarity, Inventory bag) =>
        TypeOf(type) != null && _def.Craft.ContainsKey(rarity) && _owned.Count < _def.StorageMax &&
        CraftCost(rarity).All(kv => bag.Count(kv.Key) >= kv.Value);

    /// <summary>Malzemeyi harcayip cipi uretir (Efsane yalniz buradan). Donen: sonuc ve cip.</summary>
    public (ChipResult result, ChipInstance? chip) Craft(string type, string rarity, Inventory bag)
    {
        if (TypeOf(type) == null || RarityOf(rarity) == null || !_def.Craft.ContainsKey(rarity)) return (ChipResult.NoSuchChip, null);
        if (_owned.Count >= _def.StorageMax) return (ChipResult.StorageFull, null);
        var cost = CraftCost(rarity);
        if (cost.Any(kv => bag.Count(kv.Key) < kv.Value)) return (ChipResult.Missing, null);
        foreach (var (item, n) in cost) bag.Remove(item, n);
        return (ChipResult.Ok, Add(type, rarity));
    }

    // ---- Kayit (TEKNIK 9.1) ----

    public ChipsSave Save() => new()
    {
        NextId = NextId,
        Owned = _owned.Select(c => new ChipInstance { Id = c.Id, Type = c.Type, Rarity = c.Rarity }).ToList(),
        Slots = new SortedDictionary<string, List<int>>(_slots.Where(kv => kv.Value.Any(i => i != 0)).ToDictionary(kv => kv.Key, kv => kv.Value.ToList()), StringComparer.Ordinal),
    };

    /// <summary>Sessiz yukleme: olay yaymaz. Tanimsiz tur ya da bozuk yuva atilir.</summary>
    public void Load(ChipsSave s)
    {
        _owned.Clear(); _slots.Clear();
        foreach (var c in s.Owned) if (TypeOf(c.Type) != null && RarityOf(c.Rarity) != null) _owned.Add(new ChipInstance { Id = c.Id, Type = c.Type, Rarity = c.Rarity });
        NextId = Math.Max(Math.Max(1, s.NextId), _owned.Select(c => c.Id).DefaultIfEmpty(0).Max() + 1);
        var used = new HashSet<int>();
        foreach (var (head, list) in s.Slots)
            _slots[head] = list.Select(i => i != 0 && Get(i) != null && used.Add(i) ? i : 0).ToList();
    }
}

public sealed class ChipsSave
{
    public int NextId { get; set; } = 1;
    public List<ChipInstance> Owned { get; set; } = new();
    /// <summary>Baslik -> yuva basina ornek kimligi (0 bos).</summary>
    public SortedDictionary<string, List<int>> Slots { get; set; } = new(StringComparer.Ordinal);
}
