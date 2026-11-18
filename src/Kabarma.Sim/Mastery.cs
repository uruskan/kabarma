namespace Kabarma.Sim;

/// <summary>
/// Ustalik (GDD 10; ICERIK 19): sekiz dal, her dal 1-50. Yaparak gelisir (Earn); n. seviyenin toplam esigi
/// 100 × n^1,6 XP. Her seviye dalin etkisine +%1; 10/20/30/40/50. seviyelerde iki ozellikten biri secilir (A / B).
/// Secimler Kredi karsiligi sifirlanir. Ustalik karaktere aittir ve dunyalar arasinda tasinir (TEKNIK 9):
/// bu nesne dunya durumundan ayridir ve MasteryState olarak duz biçimde yazilir.
/// Etkiler stat kimligiyle sorulur: Sum("crop_yield") = seviye payi + secili ozelliklerin toplami.
/// </summary>
public sealed class Mastery
{
    readonly MasteryFile _def;
    readonly Dictionary<string, double> _xp = new();
    readonly Dictionary<string, string> _choices = new(); // "dal:kademe" -> ozellik kimligi

    public Mastery(MasteryFile def) { _def = def; }

    public IReadOnlyList<MasteryBranchDef> Branches => _def.Branches;
    public IReadOnlyList<MasteryPerkDef> Perks => _def.Perks;
    public MasteryFile Def => _def;

    /// <summary>(dal, yeni seviye).</summary>
    public event Action<string, int>? LevelUp;
    /// <summary>(dal, kademe): yeni bir A/B secimi acildi.</summary>
    public event Action<string, int>? PerkUnlocked;
    /// <summary>(dal, kademe, ozellik): secim yapildi.</summary>
    public event Action<string, int, MasteryPerkDef>? Chosen;

    /// <summary>n. seviyenin toplam XP esigi (GDD 10).</summary>
    public double Threshold(int level) => level <= 0 ? 0 : Math.Floor(_def.ThresholdBase * Math.Pow(level, _def.ThresholdExponent));

    public double Xp(string branch) => _xp.GetValueOrDefault(branch);

    public int Level(string branch)
    {
        double xp = Xp(branch);
        int n = 0;
        while (n < _def.MaxLevel && xp >= Threshold(n + 1)) n++;
        return n;
    }

    /// <summary>Siradaki seviyeye ilerleme (0..1); en ust seviyede 1.</summary>
    public float Fraction(string branch)
    {
        int n = Level(branch);
        if (n >= _def.MaxLevel) return 1f;
        double a = Threshold(n), b = Threshold(n + 1);
        return (float)((Xp(branch) - a) / (b - a));
    }

    /// <summary>XP kaynagi kimligiyle XP kazanir ("gather:stone", "plant"...); kaynak tanimsizsa 0. Donen: dal.</summary>
    public string? Earn(string source, int times = 1)
    {
        if (times <= 0 || !_def.Xp.TryGetValue(source, out var x)) return null;
        AddXp(x.Branch, x.Xp * times);
        return x.Branch;
    }

    public void AddXp(string branch, double xp)
    {
        if (xp <= 0 || _def.Branches.All(b => b.Id != branch)) return;
        int before = Level(branch);
        double cap = Threshold(_def.MaxLevel);
        _xp[branch] = Math.Min(cap, Xp(branch) + xp);
        int after = Level(branch);
        for (int n = before + 1; n <= after; n++)
        {
            LevelUp?.Invoke(branch, n);
            if (_def.PerkLevels.Contains(n)) PerkUnlocked?.Invoke(branch, n);
        }
    }

    /// <summary>Seviyesi yetmis ama secilmemis kademeler.</summary>
    public IEnumerable<int> PendingTiers(string branch)
    {
        int lv = Level(branch);
        return _def.PerkLevels.Where(t => t <= lv && !_choices.ContainsKey(Key(branch, t)));
    }

    public int PendingCount => _def.Branches.Sum(b => PendingTiers(b.Id).Count());

    public IEnumerable<MasteryPerkDef> PerksOf(string branch, int tier) => _def.Perks.Where(p => p.Branch == branch && p.Tier == tier);

    public MasteryPerkDef? ChosenPerk(string branch, int tier) =>
        _choices.TryGetValue(Key(branch, tier), out var id) ? _def.Perks.FirstOrDefault(p => p.Id == id) : null;

    /// <summary>Kademede A ya da B'yi secer. Seviye yetmiyorsa ya da kademe zaten secildiyse false.</summary>
    public bool Choose(string branch, int tier, char slot)
    {
        if (tier > Level(branch) || _choices.ContainsKey(Key(branch, tier))) return false;
        var perk = PerksOf(branch, tier).FirstOrDefault(p => p.Slot == char.ToUpperInvariant(slot).ToString());
        if (perk == null) return false;
        _choices[Key(branch, tier)] = perk.Id;
        Chosen?.Invoke(branch, tier, perk);
        return true;
    }

    public bool Has(string perkId) => _choices.ContainsValue(perkId);

    public int ChoiceCount(string branch) => _def.PerkLevels.Count(t => _choices.ContainsKey(Key(branch, t)));

    /// <summary>Dalin sifirlama bedeli (Kredi): 1000 × (seviye / 10). Hic secim yoksa 0.</summary>
    public int RespecCost(string branch) =>
        ChoiceCount(branch) == 0 ? 0 : (int)Math.Ceiling(_def.RespecCreditsPer10Levels * Level(branch) / 10.0);

    /// <summary>Dalin secimlerini siler (odemeyi GameState.RespecMastery yapar). XP ve seviye kalir.</summary>
    public void ClearChoices(string branch)
    {
        foreach (var t in _def.PerkLevels) _choices.Remove(Key(branch, t));
    }

    /// <summary>Stat'in toplam etkisi: dal seviyelerinin payi + secili ozelliklerin "add" etkileri (kesir, +%15 = 0,15).</summary>
    public float Sum(string stat)
    {
        float v = 0;
        foreach (var b in _def.Branches.Where(b => b.LevelStat == stat)) v += Level(b.Id) * _def.PerLevel;
        foreach (var id in _choices.Values)
            foreach (var e in _def.Perks.First(p => p.Id == id).Effects.Where(e => e.Stat == stat && e.Op == "add")) v += e.Value;
        return v;
    }

    /// <summary>Stat'in "set" degeri (secili ozelliklerin en buyugu); yoksa fallback.</summary>
    public float Set(string stat, float fallback)
    {
        float best = fallback;
        bool any = false;
        foreach (var id in _choices.Values)
            foreach (var e in _def.Perks.First(p => p.Id == id).Effects.Where(e => e.Stat == stat && e.Op == "set"))
            { best = any ? Math.Max(best, e.Value) : e.Value; any = true; }
        return best;
    }

    /// <summary>1 + Sum(stat): hiz ya da verim carpani.</summary>
    public float Mult(string stat) => 1f + Sum(stat);

    /// <summary>Tam sayi miktari Mult ile olcekler; kesir kismi sansla yuvarlanir (ayni seed ayni sonuc).</summary>
    public int Scale(string stat, int amount, Rng rng)
    {
        float raw = amount * Mult(stat);
        int whole = (int)MathF.Floor(raw);
        return whole + (rng.Chance(raw - whole) ? 1 : 0);
    }

    static string Key(string branch, int tier) => branch + ":" + tier;

    // ---- Kayit (karakter dosyasi) ----

    public MasteryState Export() => new()
    {
        Xp = _xp.ToDictionary(k => k.Key, k => k.Value),
        Choices = new(_choices),
    };

    public void Import(MasteryState s)
    {
        _xp.Clear(); foreach (var (k, v) in s.Xp) if (_def.Branches.Any(b => b.Id == k)) _xp[k] = v;
        _choices.Clear();
        foreach (var (k, v) in s.Choices) if (_def.Perks.Any(p => p.Id == v)) _choices[k] = v;
    }
}

public sealed class MasteryState
{
    public Dictionary<string, double> Xp { get; set; } = new();
    public Dictionary<string, string> Choices { get; set; } = new();
}
