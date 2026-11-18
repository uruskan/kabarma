namespace Kabarma.Sim;

// Hayvancilik kurallari (GDD 9.1-9.4): evcillestirme yollari, genler ve kalitim, roller, komutlar, heybe.
// Durum PenAnimal'da (Coop.cs) ve Ranch'ta (Husbandry.Ranch.cs); sayilar husbandry.json'da. Motordan bagimsiz.

/// <summary>Hayvanin dort geni, 1-10 (GDD 9.3): B boyut, U uysallik, V verim, K kabarma gucu.</summary>
public sealed class AnimalGenes
{
    public int B { get; set; } = 1;
    public int U { get; set; } = 1;
    public int V { get; set; } = 1;
    public int K { get; set; } = 1;

    public override string ToString() => $"B{B} U{U} V{V} K{K}";
}

/// <summary>
/// Kaydedilebilir tohumlu rastgele sayi (xorshift64). Durumu duz alan: kayitla birlikte gider, ayni tohum ayni sonuc.
/// </summary>
public sealed class RanchRng
{
    public ulong State { get; set; } = 0x9E3779B97F4A7C15UL;
    public ulong Next() { var s = State; s ^= s << 13; s ^= s >> 7; s ^= s << 17; State = s; return s; }
    public float Float() => (Next() >> 40) / (float)(1UL << 24);
    public int Range(int minInclusive, int maxInclusive) => minInclusive + (int)(Next() % (ulong)Math.Max(1, maxInclusive - minInclusive + 1));
    public float Range(float min, float max) => min + Float() * (max - min);
}

public sealed class HusbandryRules
{
    public HusbandryFile Def { get; }
    /// <summary>Bir oyun gunu, oyun saniyesi (GDD 2: 22 dk gunduz + 8 dk gece).</summary>
    public float DaySeconds { get; }

    public HusbandryRules(HusbandryFile def, CalendarFile cal)
    {
        Def = def;
        DaySeconds = Math.Max(60f, (cal.DaylightMinutes + cal.NightMinutes) * 60f * Math.Max(0.01f, cal.LengthScale));
    }

    public SpeciesDef Species(string id) => Def.SpeciesOf(id) ?? throw new KeyNotFoundException($"husbandry.json: tur yok '{id}'");
    public bool Known(string id) => Def.SpeciesOf(id) != null;

    // ---------- genler (GDD 9.3, 8.3) ----------

    int Clamp(int g) => Math.Clamp(g, Def.Genes.Min, Def.Genes.Max);

    /// <summary>Yabani hayvanin genleri: her biri wildMin..wildMax.</summary>
    public AnimalGenes Wild(RanchRng rng) => new()
    {
        B = rng.Range(Def.Genes.WildMin, Def.Genes.WildMax),
        U = rng.Range(Def.Genes.WildMin, Def.Genes.WildMax),
        V = rng.Range(Def.Genes.WildMin, Def.Genes.WildMax),
        K = rng.Range(Def.Genes.WildMin, Def.Genes.WildMax),
    };

    /// <summary>Mutasyon adimi (GDD 8.3): %70 0, %20 +1, %8 -1, %2 +2.</summary>
    public int Mutation(RanchRng rng)
    {
        float r = rng.Float(), acc = 0;
        foreach (var m in Def.Genes.Mutation) { acc += m.P; if (r < acc) return m.Delta; }
        return 0;
    }

    /// <summary>
    /// Tek gen (GDD 9.3: kalitim tarimdakiyle ayni): tarim akisinin Genetics.Cross'u, ebeveyn ortalamasi (bucuk yari
    /// yariya asagi/yukari) + mutasyon, 1-10. Tarim kurali yuklu degilse (sinama verisi) ayni kural burada.
    /// </summary>
    public int InheritOne(int a, int b, RanchRng rng)
    {
        if (_crop != null)
        {
            var r = Borrow(rng);
            var g = Genetics.Cross(_crop, new Genes { V = a, H = a, D = a, M = a }, new Genes { V = b, H = b, D = b, M = b }, r);
            rng.State = r.State;
            return Clamp(g.V);
        }
        int s = a + b;
        return Clamp(s / 2 + (s % 2 != 0 && rng.Float() < 0.5f ? 1 : 0) + Mutation(rng));
    }

    public AnimalGenes Inherit(AnimalGenes m, AnimalGenes f, RanchRng rng) => new()
    {
        B = InheritOne(m.B, f.B, rng),
        U = InheritOne(m.U, f.U, rng),
        V = InheritOne(m.V, f.V, rng),
        K = InheritOne(m.K, f.K, rng),
    };

    /// <summary>Alaca zari (GDD 9.3 = 8.3): Alaca ebeveyn alacaSeedKeep ile aktarir; yoksa Aurora 1/2000, Altin 1/250, Benekli 1/40.</summary>
    public string Alaca(string mother, string father, RanchRng rng)
    {
        if (_crop == null || _crop.Alaca.Count == 0) return "";
        var r = Borrow(rng);
        string parent = mother != "" ? mother : father;
        if (mother != "" && father != "" && rng.Float() < 0.5f) parent = father;
        var a = Genetics.RollAlaca(_crop, parent == "" ? null : parent, r);
        rng.State = r.State;
        return a ?? "";
    }

    GeneticsRules? _crop;
    /// <summary>Tarim akisinin gen kurallari (crops.json "genetics"): kalitim ve Alaca ortak.</summary>
    public void UseCropGenetics(GeneticsRules r) => _crop = r;
    static Rng Borrow(RanchRng r) => new(1) { State = r.State };

    /// <summary>B geninin cana carpani.</summary>
    public float HpMult(PenAnimal a) => 1f + (a.Genes.B - 1) * Def.Genes.BHpPerPoint;
    /// <summary>Binek hizi (GDD 9.4), m/sn.</summary>
    public float MountSpeed(PenAnimal a) => Species(a.Species).Speed * (1f + (a.Genes.B - 1) * Def.Genes.BSpeedPerPoint);
    /// <summary>Heybe yuvasi (GDD 9.4: 8-24): tur tabani + B geni.</summary>
    public int PackSlots(PenAnimal a)
    {
        var s = Species(a.Species);
        return s.Pack <= 0 ? 0 : Math.Clamp(s.Pack + (a.Genes.B - 1) * Def.Genes.BPackPerPoint, 8, 24);
    }

    // ---------- evcillestirme (GDD 9.1) ----------

    /// <summary>
    /// Sevdigi yemi yedirir: guven 1/foodToTrust artar. Baska yem, yumurtadan cikan tur ya da tutulmamis yirtici
    /// guven kazanmaz. Bu yemle evcillestiyse true.
    /// </summary>
    public bool Feed(PenAnimal a, string food)
    {
        if (a.Tame) return false;
        var s = Species(a.Species);
        if (food != s.Food || s.Tame == "egg") return false;
        if (s.Tame == "subdue" && !a.Bound) return false;
        a.Trust = Math.Min(1f, a.Trust + 1f / Math.Max(1, s.FoodToTrust));
        if (a.Tame && a.Role == "") a.Role = s.Roles.FirstOrDefault() ?? "";
        return a.Tame;
    }

    /// <summary>Bu yem bu turun guvenini doldurur mu (arayuz ve E ipucu).</summary>
    public bool Wants(PenAnimal a, string food)
    {
        var s = Species(a.Species);
        return !a.Tame && food == s.Food && s.Tame != "egg" && (s.Tame != "subdue" || a.Bound);
    }

    /// <summary>Yirtici yolu (GDD 9.1): can %25'in altindayken Cekim ya da Zipkin ile tutulur. Tutulduysa true.</summary>
    public bool TryBind(PenAnimal a, float hpFraction, string tool)
    {
        var s = Species(a.Species);
        if (s.Tame != "subdue" || a.Bound || a.Tame) return false;
        if (hpFraction >= Def.Subdue.HpFraction || !Def.Subdue.Tools.Contains(tool)) return false;
        a.Bound = true;
        return true;
    }

    /// <summary>Kosmak ve ates etmek dolmamis cubugu sifirlar (GDD 9.1).</summary>
    public void Scare(PenAnimal a) { if (!a.Tame) a.Trust = 0f; }

    // ---------- roller ve komutlar (GDD 9.4) ----------

    public bool CanRole(PenAnimal a, string role) => Species(a.Species).Roles.Contains(role);

    public bool SetRole(PenAnimal a, string role)
    {
        if (!a.Tame || !CanRole(a, role)) return false;
        if (a.Role == "yuk" && role != "yuk" && a.Pack.Count > 0) return false; // heybe doluyken yuk rolu birakilmaz
        a.Role = role;
        return true;
    }

    /// <summary>Komutu dinleme olasiligi: obeyBase + U x obeyPerPoint (GDD 9.4: uysalligi dusuk bazen dinlemez).</summary>
    public float ObeyChance(PenAnimal a) => Math.Clamp(Def.Genes.ObeyBase + a.Genes.U * Def.Genes.ObeyPerPoint, 0f, 1f);

    /// <summary>Komut verir (izle, bekle, saldir, don). Dinlediyse komut gecer ve true.</summary>
    public bool Command(PenAnimal a, string cmd, RanchRng rng)
    {
        if (!a.Tame || Def.Commands.All(c => c.Id != cmd)) return false;
        if (rng.Float() >= ObeyChance(a)) return false;
        a.Command = cmd;
        return true;
    }

    // ---------- heybe (Yuk) ----------

    public int PackUsed(PenAnimal a) => a.Pack.Values.Sum(n => (n + 49) / 50); // 50'lik yigin (GDD 5 kaynak yigini)

    /// <summary>Cantadan heybeye esya tasir (Yuk rolu). Tasinan adet.</summary>
    public int PackLoad(PenAnimal a, Inventory bag, string id, int n, int stack = 50)
    {
        if (a.Role != "yuk" || n <= 0) return 0;
        int have = bag.Count(id);
        int cur = a.Pack.GetValueOrDefault(id);
        int freeSlots = PackSlots(a) - PackUsed(a);
        int roomInStack = cur == 0 ? 0 : (stack - cur % stack) % stack;
        int room = roomInStack + Math.Max(0, freeSlots) * stack;
        int k = Math.Min(Math.Min(n, have), room);
        if (k <= 0 || !bag.Remove(id, k)) return 0;
        a.Pack[id] = cur + k;
        return k;
    }

    /// <summary>Heybedeki her seyi cantaya bosaltir (sigdigi kadar). Tasinan toplam.</summary>
    public int PackUnload(PenAnimal a, Inventory bag)
    {
        int moved = 0;
        foreach (var id in a.Pack.Keys.ToList())
        {
            int n = a.Pack[id];
            int put = bag.Add(id, n);
            moved += put;
            if (put >= n) a.Pack.Remove(id); else a.Pack[id] = n - put;
        }
        return moved;
    }

    // ---------- mutluluk (GDD 9.2) ----------

    public HappinessTier Tier(float happiness) =>
        Def.Happiness.Tiers.Where(t => happiness >= t.Min).OrderByDescending(t => t.Min).FirstOrDefault() ?? new HappinessTier();

    /// <summary>Mutluluk hedefi: taban + karsilanan ihtiyaclarin agirliklari + o gunku okşama - gubre cezasi; 0-100.</summary>
    public float Target(Needs n)
    {
        var h = Def.Happiness;
        float W(string k) => h.Weights.GetValueOrDefault(k);
        float t = h.Base;
        if (n.Food) t += W("food");
        if (n.Water) t += W("water");
        if (n.Roof) t += W("roof");
        if (n.Space) t += W("space");
        if (n.Brush) t += W("brush");
        if (n.Petted) t += h.PetBonus;
        t -= Math.Min(h.ManurePenaltyMax, n.Manure * h.ManurePenalty);
        return Math.Clamp(t, 0f, 100f);
    }

    /// <summary>Urun sikligi: kalite basamagi x (1 + (U-1) x uSpeedPerPoint).</summary>
    public float ProductSpeed(PenAnimal a) => Tier(a.Happiness).Speed * (1f + (a.Genes.U - 1) * Def.Genes.USpeedPerPoint);

    /// <summary>Urun miktari: temel x kalite x (1 + (V-1) x vYieldPerPoint), en az 1.</summary>
    public int ProductAmount(PenAnimal a, AnimalProductDef p) =>
        Math.Max(1, (int)MathF.Round(p.Amount * Tier(a.Happiness).Yield * (1f + (a.Genes.V - 1) * Def.Genes.VYieldPerPoint)));

    /// <summary>Okşar: gunde petsPerDay kez (GDD 9.2). Okşandiysa true.</summary>
    public bool Pet(PenAnimal a, int day)
    {
        if (!a.Tame || a.LastPetDay == day) return false;
        a.LastPetDay = day;
        return true;
    }

    /// <summary>Hayvanin ustundeki urunleri cantaya alir (sigdigi kadar). Alinan toplam.</summary>
    public static int Collect(PenAnimal a, Inventory bag)
    {
        int got = 0;
        foreach (var s in a.Pending.ToList())
        {
            int put = bag.Add(s.Item, s.Count, s.Stars);
            got += put;
            s.Count -= put;
            if (s.Count <= 0) a.Pending.Remove(s);
        }
        return got;
    }

    /// <summary>Yemlik'e cantadan yem koyar: bos Yemlik ilk konan yemin turunu alir. Konan miktar.</summary>
    public static int Fill(Feeder f, Inventory bag, string food)
    {
        if (f.Amount > 0 && f.Food != food) return 0;
        int n = Math.Min(f.Room, bag.Count(food));
        if (n <= 0 || !bag.Remove(food, n)) return 0;
        f.Food = food;
        f.Amount += n;
        return n;
    }
}

/// <summary>Bir hayvanin o anki ihtiyaclari (GDD 9.2): agildan turetilir.</summary>
public readonly record struct Needs(bool Food, bool Water, bool Roof, bool Space, bool Brush, bool Petted, int Manure);
