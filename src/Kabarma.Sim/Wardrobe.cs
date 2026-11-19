namespace Kabarma.Sim;

/// <summary>Karakter dosyasina yazilan dolap durumu (SaveScope.Character, bolum "wardrobe").</summary>
public sealed class WardrobeSave
{
    public List<string> Members { get; set; } = new();
    public List<string> Cosmetics { get; set; } = new();
    public string Active { get; set; } = "";
    public Dictionary<string, string> Equipped { get; set; } = new();
}

/// <summary>
/// Dolap (GDD 23, ICERIK §9): sahip olunan uye tipleri ve kozmetikler, secili uye (pasifi ve baslangic karti) ve takili kozmetik
/// (slot basina bir). Karakter durumudur: dunyalar arasinda tasinir. Kozmetigin oynanisa etkisi yoktur; uye pasifi etkilidir:
/// <see cref="Effect"/> anahtarini sistemler okur (Mastery.Mult ile ayni kalip).
/// </summary>
public sealed class Wardrobe
{
    readonly GameData _d;
    readonly HashSet<string> _members = new();
    readonly HashSet<string> _cosmetics = new();
    readonly Dictionary<string, string> _equipped = new();

    public Wardrobe(GameData d)
    {
        _d = d;
        Reset();
    }

    public string Active { get; private set; } = "";
    /// <summary>Uye ya da kozmetik acildi/degisti (arayuz, gorunus, ag).</summary>
    public event Action? Changed;
    /// <summary>Yeni bir sey acildi: tur ("member" ya da "cosmetic") ve kimlik.</summary>
    public event Action<string, string>? Unlocked;

    public MemberDef? ActiveDef => _d.Members.Member(Active);
    public IReadOnlyCollection<string> Members => _members;
    public IReadOnlyCollection<string> Cosmetics => _cosmetics;
    public bool HasMember(string id) => _members.Contains(id);
    public bool HasCosmetic(string id) => _cosmetics.Contains(id);
    public string Equipped(string slot) => _equipped.GetValueOrDefault(slot, "");

    void Reset()
    {
        _members.Clear(); _cosmetics.Clear(); _equipped.Clear();
        foreach (var m in _d.Members.Members.Where(m => m.Chain == "start" || m.Id == _d.Members.DefaultMember)) _members.Add(m.Id);
        Active = _d.Members.DefaultMember;
        foreach (var c in _d.Cosmetics.Cosmetics.Where(c => c.Source == "start")) { _cosmetics.Add(c.Id); if (!_equipped.ContainsKey(c.Slot)) _equipped[c.Slot] = c.Id; }
    }

    /// <summary>Is emri odulu, katalog, cark ya da Genel Merkez: kimlik bilinmiyorsa da saklanir (sonradan gelen veri tanir). Yeni ise true.</summary>
    public bool Unlock(string kind, string id)
    {
        bool added = kind == "member" ? _members.Add(id) : kind == "cosmetic" && _cosmetics.Add(id);
        if (added) { Unlocked?.Invoke(kind, id); Changed?.Invoke(); }
        return added;
    }

    public bool SetMember(string id)
    {
        if (!_members.Contains(id) || _d.Members.Member(id) == null || Active == id) return false;
        Active = id;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Kozmetigi slotuna takar; sahip degilse ya da zaten takiliysa false. Bos slot icin <see cref="Unequip"/>.</summary>
    public bool Equip(string id)
    {
        var c = _d.Cosmetics.Get(id);
        if (c == null || !_cosmetics.Contains(id) || _equipped.GetValueOrDefault(c.Slot) == id) return false;
        _equipped[c.Slot] = id;
        Changed?.Invoke();
        return true;
    }

    public void Unequip(string slot)
    {
        if (_equipped.Remove(slot)) Changed?.Invoke();
    }

    /// <summary>Tohum Carki'nin kozmetik odulu: sahip olunmayan cark ya da katalog kozmetiklerinden biri (kimlik sirasi sabit, secim Rng ile); yoksa null.</summary>
    public string? WheelPick(Rng rng)
    {
        var pool = _d.Cosmetics.Cosmetics.Where(c => c.Source is "wheel" or "catalog" && !_cosmetics.Contains(c.Id)).Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
        return pool.Count == 0 ? null : pool[(int)(rng.NextFloat() * pool.Count) % pool.Count];
    }

    // ---------------- pasif ----------------

    /// <summary>Secili uyenin pasif etkisi (yok: 0).</summary>
    public float Effect(string key) => ActiveDef != null && ActiveDef.Effects.TryGetValue(key, out var v) ? v : 0f;
    /// <summary>1 + Effect: carpan (0,10 -> 1,10).</summary>
    public float Mult(string key) => 1f + Effect(key);
    public bool Flag(string key) => Effect(key) > 0f;
    /// <summary>Kabarma'ya baslarken verilecek kart (bos: yok ya da rastgele Siradan).</summary>
    public string StartCard => ActiveDef?.StartCard ?? "";

    /// <summary>Ag gorunumu: takili kozmetikler, "slot=kimlik;..." (siraya bagimsiz degil, slot sirasi sabit).</summary>
    public string Look() => string.Join(";", _d.Cosmetics.Slots.Select(s => $"{s}={Equipped(s)}"));

    public static Dictionary<string, string> ParseLook(string look)
    {
        var d = new Dictionary<string, string>();
        foreach (var part in (look ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int i = part.IndexOf('=');
            if (i > 0) d[part[..i]] = part[(i + 1)..];
        }
        return d;
    }

    // ---------------- kayit ----------------

    public WardrobeSave Export() => new()
    {
        Members = _members.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        Cosmetics = _cosmetics.OrderBy(x => x, StringComparer.Ordinal).ToList(),
        Active = Active,
        Equipped = _equipped.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => k.Value),
    };

    /// <summary>Sessiz yukleme (olay yaymaz); varsayilanlar (baştan acik olanlar) her zaman korunur.</summary>
    public void Import(WardrobeSave s)
    {
        Reset();
        foreach (var m in s.Members ?? new()) _members.Add(m);
        foreach (var c in s.Cosmetics ?? new()) _cosmetics.Add(c);
        if (!string.IsNullOrEmpty(s.Active) && _members.Contains(s.Active)) Active = s.Active;
        foreach (var kv in s.Equipped ?? new())
            if (_cosmetics.Contains(kv.Value) && _d.Cosmetics.Get(kv.Value)?.Slot == kv.Key) _equipped[kv.Key] = kv.Value;
        Changed?.Invoke();
    }
}
