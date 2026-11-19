namespace Kabarma.Sim;

public sealed partial class Genes
{
    public Genes Clone() => new() { V = V, H = H, D = D, M = M };
    public int Sum => V + H + D + M;
    public int this[int i] { get => i switch { 0 => V, 1 => H, 2 => D, _ => M }; set { switch (i) { case 0: V = value; break; case 1: H = value; break; case 2: D = value; break; default: M = value; break; } } }
    public const int Count = 4;
    public static readonly string[] Letters = { "V", "H", "D", "M" };
    public bool SameAs(Genes o) => V == o.V && H == o.H && D == o.D && M == o.M;
    public override string ToString() => $"V{V} H{H} D{D} M{M}";
}

/// <summary>
/// Kalitim, caprazlama, mutasyon ve Alaca zari (GDD 8.3). Durumsuz: butun sayilar crops.json "genetics".
/// </summary>
public static class Genetics
{
    public static int Clamp(GeneticsRules r, int g) => Math.Clamp(g, r.GeneMin, r.GeneMax);

    /// <summary>Mutasyon zari: %70 degismez, %20 +1, %8 -1, %2 +2 (veride).</summary>
    public static int MutationDelta(GeneticsRules r, Rng rng)
    {
        float x = rng.NextFloat(), acc = 0f;
        if (r.MutationMult != 1f)
        {
            // Degisme olasiligi (sifir disi adimlarin toplami) carpanla artar; adimlarin kendi orani korunur.
            float change = r.Mutation.Where(m => m.Delta != 0).Sum(m => m.P);
            float pc = Math.Clamp(change * r.MutationMult, 0f, 1f);
            if (change <= 0f || x >= pc) return 0;
            float y = x / pc * change;
            foreach (var m in r.Mutation.Where(m => m.Delta != 0)) { acc += m.P; if (y < acc) return m.Delta; }
            return r.Mutation.Last(m => m.Delta != 0).Delta;
        }
        foreach (var m in r.Mutation)
        {
            acc += m.P;
            if (x < acc) return m.Delta;
        }
        return 0;
    }

    /// <summary>Tek bitkiden tohum (caprazlama yok): her gen ayni + mutasyon, 1-10 arasinda.</summary>
    public static Genes Inherit(GeneticsRules r, Genes parent, Rng rng)
    {
        var g = new Genes();
        for (int i = 0; i < Genes.Count; i++) g[i] = Clamp(r, parent[i] + MutationDelta(r, rng));
        return g;
    }

    /// <summary>Caprazlama: her gen ebeveyn ortalamasi (bucuk yari yariya asagi/yukari) + mutasyon (GDD 8.3).</summary>
    public static Genes Cross(GeneticsRules r, Genes a, Genes b, Rng rng)
    {
        var g = new Genes();
        for (int i = 0; i < Genes.Count; i++)
        {
            int s = a[i] + b[i];
            int avg = s / 2 + (s % 2 != 0 && rng.Chance(0.5f) ? 1 : 0);
            g[i] = Clamp(r, avg + MutationDelta(r, rng));
        }
        return g;
    }

    /// <summary>Yabani tohum: her gen wildMin..wildMax (GDD 8.3 "1-3").</summary>
    public static Genes Wild(GeneticsRules r, Rng rng)
    {
        var g = new Genes();
        for (int i = 0; i < Genes.Count; i++) g[i] = rng.Range(r.WildMin, r.WildMax + 1);
        return g;
    }

    /// <summary>
    /// Alaca zari (ekimde): Alaca tohum alacaSeedKeep olasilikla ayni Alaca'yi verir; yoksa (ya da tutmazsa) en nadirden
    /// baslayarak tek zar: Aurora 1/2000, Altin 1/250, Benekli 1/40. Null: Alaca degil.
    /// </summary>
    public static string? RollAlaca(GeneticsRules r, string? seedAlaca, Rng rng)
    {
        if (seedAlaca != null && r.AlacaOf(seedAlaca) != null && rng.Chance(r.AlacaSeedKeep)) return seedAlaca;
        float x = rng.NextFloat(), acc = 0f;
        foreach (var a in r.Alaca.OrderByDescending(a => a.Odds))
        {
            acc += 1f / Math.Max(1, a.Odds);
            if (x < acc) return a.Id;
        }
        return null;
    }

    /// <summary>Alaca urunun esya kimligi: "turnip_altin".</summary>
    public static string AlacaItem(string cropId, string alaca) => $"{cropId}_{alaca}";

    /// <summary>
    /// Ekin urunlerinin satis tabani (Pricing'e baglanir): prices.json'da olmayan ekin urunu crops.json basePrice;
    /// Alaca urun basePrice x priceMult (GDD 8.3: x3, x10, x50).
    /// </summary>
    public static bool TryPrice(GameData d, string id, out int price)
    {
        price = 0;
        foreach (var c in d.Crops.Crops)
        {
            if (c.Product == null || c.Product == c.Id) { if (id == c.Id) { price = c.BasePrice; return true; } }
            foreach (var a in d.Crops.Genetics.Alaca)
                if (id == AlacaItem(c.Id, a.Id)) { price = c.BasePrice * a.PriceMult; return true; }
        }
        return false;
    }

    /// <summary>Kimlik bir Alaca urunuyse (ekin, Alaca).</summary>
    public static (CropDef crop, AlacaDef alaca)? ParseAlacaItem(GameData d, string id)
    {
        foreach (var c in d.Crops.Crops)
            foreach (var a in d.Crops.Genetics.Alaca)
                if (id == AlacaItem(c.Id, a.Id)) return (c, a);
        return null;
    }
}

/// <summary>Tohum Kaydi'nin bir satiri: ayni ekin, ayni genler, ayni Alaca'dan kac tohum (GDD 8.3 "Tohum soyu").</summary>
public sealed class SeedLot
{
    public string Crop { get; set; } = "";
    public Genes Genes { get; set; } = new();
    public string? Alaca { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Tapunun Tohum Kaydi (GDD 8.3): cantadaki tohumun genleri ve Alaca'si burada tutulur. Canta adet ve kimligin tek
/// kaynagidir; kaydi olmayan tohum taban genleriyle ekilir, tohumu olmayan kayit bekler. Duz ve serilestirilebilir.
/// </summary>
public sealed class SeedLedger
{
    public List<SeedLot> Lots { get; set; } = new();

    public void Add(string crop, Genes genes, string? alaca, int count = 1)
    {
        if (count <= 0) return;
        var lot = Lots.FirstOrDefault(l => l.Crop == crop && l.Alaca == alaca && l.Genes.SameAs(genes));
        if (lot != null) { lot.Count += count; return; }
        Lots.Add(new SeedLot { Crop = crop, Genes = genes.Clone(), Alaca = alaca, Count = count });
    }

    public int CountFor(string crop) => Lots.Where(l => l.Crop == crop).Sum(l => l.Count);

    /// <summary>Ekilecek ilk kayit: Alaca once (nadirlik), sonra gen toplami en yuksek.</summary>
    public SeedLot? Best(string crop, GeneticsRules r) => Lots.Where(l => l.Crop == crop && l.Count > 0)
        .OrderByDescending(l => r.AlacaOf(l.Alaca)?.Odds ?? 0).ThenByDescending(l => l.Genes.Sum).ThenByDescending(l => l.Genes.V).FirstOrDefault();

    /// <summary>En iyi kayittan bir tohum alir; kayit yoksa null.</summary>
    public SeedLot? Take(string crop, GeneticsRules r)
    {
        var lot = Best(crop, r);
        if (lot == null) return null;
        lot.Count--;
        if (lot.Count <= 0) Lots.Remove(lot);
        return new SeedLot { Crop = crop, Genes = lot.Genes.Clone(), Alaca = lot.Alaca, Count = 1 };
    }
}

/// <summary>Defter'in Islah notu (GDD 8.3). Kind: best (turun yeni en iyisi), jump (+2 mutasyon), alaca, cross.</summary>
public sealed class BreedingNote
{
    public string Kind { get; set; } = "";
    public string Crop { get; set; } = "";
    public Genes Genes { get; set; } = new();
    public string? Alaca { get; set; }
    /// <summary>Oyun zamani (Farm.Time, sn).</summary>
    public float Time { get; set; }
}

/// <summary>Islah notlari: her turun en iyi genleri ve son olaylar (en fazla maxNotes).</summary>
public sealed class BreedingLog
{
    public Dictionary<string, Genes> Best { get; set; } = new();
    public List<BreedingNote> Notes { get; set; } = new();
    public int Crosses { get; set; }
    public int AlacaFinds { get; set; }

    public void Note(GeneticsRules r, string kind, string crop, Genes g, string? alaca, float time)
    {
        Notes.Add(new BreedingNote { Kind = kind, Crop = crop, Genes = g.Clone(), Alaca = alaca, Time = time });
        while (Notes.Count > Math.Max(1, r.MaxNotes)) Notes.RemoveAt(0);
    }

    /// <summary>Yeni tohumu degerlendir: turun en iyisi mi?</summary>
    public bool Offer(GeneticsRules r, string crop, Genes g, float time)
    {
        if (Best.TryGetValue(crop, out var b) && b.Sum >= g.Sum) return false;
        Best[crop] = g.Clone();
        Note(r, "best", crop, g, null, time);
        return true;
    }
}
