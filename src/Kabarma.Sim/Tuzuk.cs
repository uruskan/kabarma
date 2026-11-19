namespace Kabarma.Sim;

/// <summary>
/// O gecenin gecerli tuzuk maddeleri ve etkileri (GDD 14.8). Duz ve degismez: madde kimligi -> kat (1 ya da 2).
/// Etkiler tuzuk.json'daki 1 kat degerinden kat sayisiyla olceklenir (dosya basligi). Oyun yalniz bu ozelliklere bakar.
/// </summary>
public sealed class TuzukSet
{
    public static readonly TuzukSet None = new(new Dictionary<string, int>(), null, 0);

    readonly TuzukFile? _f;
    public IReadOnlyDictionary<string, int> Stacks { get; }
    /// <summary>Yeni Ay'da secilen madde sayisi (Maya zammi).</summary>
    public int NewMoonCount { get; }

    public TuzukSet(IReadOnlyDictionary<string, int> stacks, TuzukFile? f, int newMoonCount)
    {
        Stacks = stacks; _f = f; NewMoonCount = newMoonCount;
    }

    public bool Any => Stacks.Count > 0;
    public bool Has(string id) => Stacks.ContainsKey(id);
    public int Stack(string id) => Stacks.GetValueOrDefault(id);

    IEnumerable<(TuzukClauseDef c, int s)> Active() =>
        _f == null ? Enumerable.Empty<(TuzukClauseDef, int)>() : _f.Clauses.Where(c => Stacks.ContainsKey(c.Id)).Select(c => (c, Stacks[c.Id]));

    /// <summary>Carpan maddeleri: 1 + (v - 1) x kat; birlesik carpim.</summary>
    float Mult(Func<TuzukClauseDef, float?> v)
    {
        float m = 1f;
        foreach (var (c, s) in Active())
            if (v(c) is { } x) m *= Math.Max(0f, 1f + (x - 1f) * s);
        return m;
    }

    /// <summary>Hasar turune gore tuzuk carpani (Saganak: Ates etkisiz, Sok guclu); diger turler 1.</summary>
    public float DamageMult(DamageType type) => type == DamageType.Ates ? FireMult : type == DamageType.Sok ? ShockMult : 1f;

    /// <summary>Gorus mesafesi (m); 0: sinirsiz.</summary>
    public float VisionM => Active().Where(a => a.c.VisionM > 0).Select(a => Math.Max(10f, a.c.VisionM - 10f * (a.s - 1))).DefaultIfEmpty(0f).Min();
    public float EatSpeed => Mult(c => c.EatSpeed > 0 ? c.EatSpeed : null);
    public float EliteAdd => Active().Sum(a => a.c.EliteAdd * a.s);
    public int AzmanAdd => Active().Sum(a => a.c.AzmanAdd * a.s);
    public int ExtraWaves => Active().Sum(a => a.c.ExtraWaves * a.s);
    public bool NoGap => Active().Any(a => a.c.NoGap);
    public bool HoldingAllRaid => Active().Any(a => a.c.HoldingAllRaid);
    public bool AnyBiomeMix => Active().Any(a => a.c.AnyBiomeMix);
    public float FireMult => Mult(c => c.FireMult);
    public float ShockMult => Mult(c => c.ShockMult);
    public float TowerSpeed => Mult(c => c.TowerSpeed > 0 ? c.TowerSpeed : null);
    public float RangeMult => Mult(c => c.RangeMult > 0 ? c.RangeMult : null);
    /// <summary>Seviye basina sunulan kart sayisi siniri; 0: sinir yok.</summary>
    public int OfferSize => Active().Where(a => a.c.OfferSize > 0).Select(a => Math.Max(1, a.c.OfferSize - (a.s - 1))).DefaultIfEmpty(0).Min();
}

/// <summary>Tuzuk sirasi ve katlari (GDD 14.8): KS'ye, dunya tohumuna ve Yeni Ay secimine gore.</summary>
public static class Tuzuk
{
    /// <summary>12 maddenin dunya tohumuyla karistirilmis sirasi (her zaman ayni).</summary>
    public static List<string> Order(GameData d, ulong worldSeed)
    {
        var ids = d.Tuzuk.Clauses.Select(c => c.Id).ToList();
        var rng = new Rng(worldSeed * 0xA24BAED4963EE407UL + 0x9FB21C651E98DF25UL);
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        return ids;
    }

    /// <summary>KS'ye gore madde katlari. chosen: Yeni Ay secimi (KS 1'den tek kat).</summary>
    public static Dictionary<string, int> Stacks(GameData d, ulong worldSeed, int ks, IEnumerable<string> chosen)
    {
        var f = d.Tuzuk;
        var res = new Dictionary<string, int>();
        var valid = f.Clauses.Select(c => c.Id).ToHashSet();
        foreach (var id in chosen) if (valid.Contains(id)) res[id] = 1;
        var order = Order(d, worldSeed);
        int round1 = Math.Clamp(ks - f.FirstKs + 1, 0, order.Count);
        int round2 = Math.Clamp(ks - f.SecondRoundKs + 1, 0, order.Count);
        for (int i = 0; i < order.Count; i++)
        {
            int add = (i < round1 ? 1 : 0) + (i < round2 ? 1 : 0);
            if (add > 0) res[order[i]] = Math.Min(f.MaxStack, res.GetValueOrDefault(order[i]) + add);
        }
        return res;
    }

    /// <summary>
    /// O gecenin tuzuku: tam Kabarma'da KS sirasi + Yeni Ay secimi; Kucuk Kabarma ve Tatbikat'ta yalniz Yeni Ay secimi (GDD 14.8).
    /// </summary>
    public static TuzukSet For(GameData d, ulong worldSeed, int ks, IEnumerable<string> chosen, KabarmaKind kind)
    {
        var picked = chosen.Where(id => d.Tuzuk.Clauses.Any(c => c.Id == id)).Distinct().ToList();
        var stacks = kind == KabarmaKind.Full || kind == KabarmaKind.First
            ? Stacks(d, worldSeed, kind == KabarmaKind.First ? 0 : ks, picked)
            : picked.ToDictionary(id => id, _ => 1);
        return stacks.Count == 0 ? TuzukSet.None : new TuzukSet(stacks, d.Tuzuk, picked.Count);
    }
}