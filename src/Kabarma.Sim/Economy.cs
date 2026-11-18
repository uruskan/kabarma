namespace Kabarma.Sim;

/// <summary>
/// Satis fiyati (GDD 13, ICERIK 18): fiyat = taban × kalite. Ham urunun tabani prices.json'da; islenmis mal ve yemegin
/// tabani tariften: girdilerin taban toplami × processedMult / cikan adet (yakit fiyatsizdir). Borsa ve doygunluk M2.
/// </summary>
public sealed class Pricing
{
    readonly GameData _d;
    public Pricing(GameData d) { _d = d; }

    /// <summary>Birim taban fiyat (kalite ★1). Satilmayan esya 0.</summary>
    public float Base(string id) => Base(id, 0);

    float Base(string id, int depth)
    {
        var t = _d.Tool;
        if (t.Prices.TryGetValue(id, out int p)) return p;
        if (Genetics.TryPrice(_d, id, out int cropPrice)) return cropPrice; // tarim: T1-T2 ekinleri ve Alaca urun (GDD 8.3, crops.json)
        if (depth > 8 || !t.Processed.Contains(id)) return 0;
        var r = t.Recipes.FirstOrDefault(x => x.Id == id && !x.Load);
        if (r == null || r.Out <= 0) return 0;
        float sum = r.In.Sum(kv => Base(kv.Key, depth + 1) * kv.Value);
        return sum * t.ProcessedMult / r.Out;
    }

    /// <summary>Kalite carpani: ★1, ★2, ★3 (GDD 13: 1,0 / 1,3 / 1,7).</summary>
    public float Quality(int stars)
    {
        var q = _d.Tool.Quality;
        return q.Count == 0 ? 1f : q[Math.Clamp(stars - 1, 0, q.Count - 1)];
    }

    /// <summary>Borsa (GameState.Market.cs baglar); null ise fiyat notr (Borsa ve doygunluk 1).</summary>
    public Market? Market { get; set; }

    /// <summary>Birim satis fiyati: taban × kalite × Borsa × doygunluk (GDD 13; Alaca carpani tarim akisiyla).</summary>
    public float Unit(string id, int stars = 1) => Base(id) * Quality(stars) * (Market?.FactorOf(id) ?? 1f);

    public bool Sellable(string id) => Base(id) > 0;

    /// <summary>Bir yuva kumesinin (kasa) toplam degeri: her yigin icin taban × kalite[★] × adet; toplam en yakin tam Krediye yuvarlanir.</summary>
    public int Value(Inventory items) =>
        (int)Math.Round(items.Stacks.Sum(s => Unit(s.id, s.stars) * s.count), MidpointRounding.AwayFromZero);
}

/// <summary>Kargo kasasi (GDD 13): 20 yigin alan yuk nesnesi (25 kg); degeri icindekilerin satis degerinin toplami.</summary>
public sealed class CargoCrate
{
    public Inventory Items { get; }
    public float Kg { get; }
    public int Id { get; }

    public CargoCrate(GameData d, int id)
    {
        Id = id;
        Items = new Inventory(d.Economy.Crate.Slots, d.Tool.StackOf);
        Kg = d.Economy.Crate.Kg;
    }
}

/// <summary>
/// Kargo: Paketleme'de kasa doldurma, Kargo Rampasi'na yukleme, firlatma ve gecikmeli odeme (GDD 13; ilk satis aninda, GDD 24).
/// Rampa 1-6 kasa ya da tek bir kabarik urun alir; ikisi birden degil.
/// Durum duz veridir (kayda hazir, TEKNIK 9): acik kasa, rampadaki kasalar, bekleyen odemeler, ilk satis bayragi.
/// </summary>
public sealed partial class Cargo
{
    readonly GameData _d;
    readonly Action<int, string> _credit;
    int _serial;
    readonly List<CargoCrate> _pad = new();
    readonly List<PendingPayout> _pending = new();

    public sealed class PendingPayout
    {
        public float Left;
        public int Credits;
        public string What = "";
    }

    public Pricing Pricing { get; }
    /// <summary>Paketleme Tezgahi'nda doldurulmakta olan kasa (yoksa null).</summary>
    public CargoCrate? Open { get; private set; }
    /// <summary>Kapatilip dunyaya cikmayi bekleyen kasalar (Paketleme sirasi bitince alinir).</summary>
    readonly Queue<CargoCrate> _closed = new();
    public IReadOnlyList<CargoCrate> Pad => _pad;
    public IReadOnlyList<PendingPayout> Pending => _pending;
    public bool FirstSaleDone { get; private set; }

    /// <summary>Odeme hesaba gecti (Kredi, ne satildi).</summary>
    public event Action<int, string>? Paid;

    /// <param name="credit">Kredi yazan (GameState): (tutar, ne satildi).</param>
    public Cargo(GameData d, Action<int, string> credit)
    {
        _d = d;
        _credit = credit;
        Pricing = new Pricing(d);
    }

    RampDef Ramp => _d.Economy.Ramp;

    // ---------- Paketleme ----------

    /// <summary>Esyayi cantadan acik kasaya koyar (kasa yoksa acar). Satilmayan esya kasaya girmez.</summary>
    /// <returns>Kasaya giren adet.</returns>
    public int Pack(Inventory from, string id, int amount)
    {
        if (!Pricing.Sellable(id)) return 0;
        Open ??= new CargoCrate(_d, ++_serial);
        return from.MoveTo(Open.Items, id, amount);
    }

    /// <summary>Acik kasayi kapatir (bos kasa kapanmaz). Paketleme tarifi bitince <see cref="TakeClosed"/> ile dunyaya cikar.</summary>
    public CargoCrate? Close()
    {
        if (Open == null || Open.Items.IsEmpty) return null;
        var c = Open;
        Open = null;
        _closed.Enqueue(c);
        return c;
    }

    /// <summary>Kapanmis bir kasayi dunyaya cikarmak icin alir (yoksa null).</summary>
    public CargoCrate? TakeClosed() => _closed.Count > 0 ? _closed.Dequeue() : null;

    public int Value(CargoCrate c) => Pricing.Value(c.Items);

    // ---------- Rampa ----------

    public bool CanLoadCrate => _pad.Count < Ramp.MaxCrates;

    /// <summary>Kabarik urun yalniz bos rampaya konur (rampa ya kasa ya tek kabarik urun alir).</summary>
    public bool CanShipSwollen => _pad.Count == 0;

    public bool LoadCrate(CargoCrate c)
    {
        if (!CanLoadCrate || _pad.Contains(c)) return false;
        _pad.Add(c);
        return true;
    }

    public bool UnloadCrate(CargoCrate c) => _pad.Remove(c);

    /// <summary>Rampadaki kasalari firlatir. Donen: vaat edilen Kredi (0: rampa bos).</summary>
    public int LaunchCrates()
    {
        if (_pad.Count == 0) return 0;
        int v = _pad.Sum(Value); // fiyat firlatma aninda kilitlenir (GDD 13)
        string what = $"kargo_kasasi x{_pad.Count}";
        if (Pricing.Market is Market m) foreach (var c in _pad) m.RecordSale(m.CategoriesOf(c.Items)); // doygunluk: kasa basina
        _pad.Clear();
        Schedule(v, what);
        return v;
    }

    /// <summary>Tek bir kabarik urunu firlatir (rampa bossa). Donen: vaat edilen Kredi; rampa doluysa -1.</summary>
    public int ShipSwollen(int value, string what)
    {
        if (!CanShipSwollen) return -1;
        Schedule(value, what);
        return value;
    }

    void Schedule(int credits, string what)
    {
        if (!FirstSaleDone && Ramp.FirstSaleInstant)
        {
            FirstSaleDone = true;
            Pay(credits, what);
            return;
        }
        FirstSaleDone = true;
        _pending.Add(new PendingPayout { Left = Ramp.PayoutSeconds, Credits = credits, What = what });
    }

    void Pay(int credits, string what)
    {
        _credit(credits, what);
        Paid?.Invoke(credits, what);
    }

    /// <returns>Bu tikte hesaba gecen Kredi.</returns>
    public int Tick(float dt)
    {
        int paid = 0;
        for (int i = 0; i < _pending.Count; i++)
        {
            var p = _pending[i];
            p.Left -= dt;
            if (p.Left > 0) continue;
            _pending.RemoveAt(i--);
            Pay(p.Credits, p.What);
            paid += p.Credits;
        }
        return paid;
    }
}

/// <summary>Insa kurallari (GDD 7; URETIM 6 M1 "Yapi"): sokum iadesi, onarim bedeli, hasar tabani.</summary>
public static class Structures
{
    /// <summary>Sokum iadesi: tapu icinde bedelin RefundInClaim kati (M1: %100). Tapu disi kurma M2'de.</summary>
    public static Dictionary<string, int> Refund(IReadOnlyDictionary<string, int> cost, BuildRulesDef r) =>
        cost.ToDictionary(kv => kv.Key, kv => (int)Math.Floor(kv.Value * r.RefundInClaim + 1e-4));

    /// <summary>Onarim bedeli: hasar oraninda malzemenin %25'i, malzeme basina yukari yuvarlanir. Hasarsizsa bos.</summary>
    public static Dictionary<string, int> RepairCost(IReadOnlyDictionary<string, int> cost, float hp, float maxHp, BuildRulesDef r)
    {
        var res = new Dictionary<string, int>();
        if (maxHp <= 0 || hp >= maxHp) return res;
        float damage = Math.Clamp(1f - hp / maxHp, 0f, 1f);
        foreach (var kv in cost)
        {
            int n = (int)Math.Ceiling(kv.Value * r.RepairFraction * damage - 1e-4);
            if (n > 0) res[kv.Key] = n;
        }
        return res;
    }

    /// <summary>Hasar uygular; can azaminin HpFloor katinin altina inmez (M1: yikilma yok, %10'da durur).</summary>
    public static float ApplyDamage(float hp, float maxHp, float damage, BuildRulesDef r)
    {
        float floor = maxHp * r.HpFloor;
        if (hp <= floor) return hp;
        return Math.Max(floor, hp - Math.Max(0, damage));
    }

    /// <summary>Kurma yalniz tapu icinde (M1): nokta Ocak'in tapu yaricapinda mi (yatay uzaklik).</summary>
    public static bool InClaim(float dx, float dz, float claimRadius) => dx * dx + dz * dz <= claimRadius * claimRadius;
}
