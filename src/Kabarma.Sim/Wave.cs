namespace Kabarma.Sim;

// Kabarma dalga ve Kabaran sozlesmesi (kabarma3, M3; GDD 14.1-14.6). Motordan bagimsiz, duz, tohumla belirli.
// Baglananlar: kartlar (KabarmaNight), savunma (Kabaranlar + vurus girisi), gucler (KabarmaSpec: Tatbikat, Maya Feneri),
// savas-hayvan (Gosteri olayi, hayvan oldurmesi), savas (hasar sozlesmesi gelince IKabaranHit tek yerden degisir).

/// <summary>Kabarma turu: tam (takvim/Can), kucuk (Maya Feneri, F1), tatbikat (hayalet, odulsuz), ilk gece (senaryo).</summary>
public enum KabarmaKind { Full, Small, Drill, First }

/// <summary>Gecenin bitis nedeni (GDD 14.4).</summary>
public enum KabarmaEnd { None, Dawn, KazanToppled }

/// <summary>Kabaranin hedef sirasi (GDD 14.3): kendi ailesinin ekini, Maya Kazani, kendisine saldiran; yoldaki yapi.</summary>
public enum KabaranTarget { Crop, Kazan, Attacker, Structure }

/// <summary>Gosteri carpanini artiran olaylar (GDD 14.5). Oldurme serisi KabarmaNight.OnKill'de kendiliginden sayilir.</summary>
public enum GosteriEvent { AirKill, EnvironmentKill, AnimalKill, CloseCall, NoHitStreak }

/// <summary>
/// Bir Kabarma'nin baslatma tanimi. Tam Kabarma (takvim/Can) KS ve oyuncu sayisiyla; Kucuk Kabarma merkez ve yaricapla
/// (GDD 14.6); Tatbikat Drill ile (odul yok, onarim bedava; GDD 14.7). Ekili aileler: aile -> olgun ekin sayisi.
/// </summary>
public sealed class KabarmaSpec
{
    public KabarmaKind Kind { get; set; } = KabarmaKind.Full;
    public int Ks { get; set; } = 1;
    public int Players { get; set; } = 1;
    public string Biome { get; set; } = "altin_cayir";
    public Dictionary<string, int> PlantedFamilies { get; set; } = new();
    public float CenterX { get; set; }
    public float CenterZ { get; set; }
    /// <summary>0: tapu (Ocak) alani. Kucuk Kabarma: small.radius.</summary>
    public float Radius { get; set; }
    public int OcakTier { get; set; } = 1;
    public ulong Seed { get; set; } = 1;
    /// <summary>O gecenin tuzuk maddeleri (GDD 14.8); yoksa TuzukSet.None.</summary>
    public TuzukSet Tuzuk { get; set; } = TuzukSet.None;
}

/// <summary>Dalgadaki tek giris: tur, elit oneki (yoksa bos), suru mu, Azman yeri mi (savas akisi; yoksa elit dalga).</summary>
public sealed class WaveSlot
{
    public string Species { get; set; } = "";
    public string Elite { get; set; } = "";
    public bool Swarm { get; set; }
    public bool Azman { get; set; }
}

public sealed class PlannedWave
{
    public int Index { get; set; }
    public bool Doruk { get; set; }
    public float Seconds { get; set; }
    public float GapAfter { get; set; }
    public List<WaveSlot> Slots { get; set; } = new();
    public int Weight => Slots.Count == 0 ? 0 : Slots.Sum(s => s.Azman ? 0 : 1);
}

/// <summary>Bir gecenin bütün dalgalari (tohumla belirli) ve carpanlari.</summary>
public sealed class WavePlan
{
    public KabarmaSpec Spec { get; set; } = new();
    public int Ks { get; set; }
    public List<PlannedWave> Waves { get; set; } = new();
    public float HpMult { get; set; } = 1f;
    public float DamageMult { get; set; } = 1f;
    public float MayaMult { get; set; } = 1f;
    public bool NoCrops { get; set; }
    public bool Rewards { get; set; } = true;
    public float AlarmSeconds { get; set; }
    public float SwellSeconds { get; set; }
    public int MaxCardLevels { get; set; }
    public int MaxActive { get; set; }
    public int TotalSlots => Waves.Sum(w => w.Slots.Count);
}

/// <summary>Sahnedeki bir Kabaran'in duz durumu (kayda ve aga hazir). Godot aktoru bunu tasir.</summary>
public sealed class KabaranState
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public string Elite { get; set; } = "";
    public int Wave { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public float Damage { get; set; }
    public float SpeedMult { get; set; } = 1f;
    public float Scale { get; set; } = 1f;
    public float Armor { get; set; }
    public KabaranTarget Target { get; set; } = KabaranTarget.Crop;
    /// <summary>Son vuran oyuncu (0: yerel/bilinmiyor). Oldurme ona yazilir (GDD 15 ortak oyunda Oz oyuncu basina).</summary>
    public int LastHitBy { get; set; }
    public bool Dead => Hp <= 0;
}

/// <summary>Maya Kazani (GDD 6, 14.4): Ocak'ta; dusmanin ikinci hedefi. Devrilirse gece biter.</summary>
public sealed class KazanState
{
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public bool Toppled { get; set; }
}

/// <summary>Dalga kurallari (GDD 14.3): sayi, guc, elit orani, bilesim. Hepsi tohumla belirli.</summary>
public static class Waves
{
    /// <summary>Oyuncu carpani: 1 + 0,6 x (n - 1)^0,85.</summary>
    public static float PlayerMult(FullKabarmaDef d, int players) =>
        1f + d.PlayerFactor * MathF.Pow(Math.Max(0, players - 1), d.PlayerExponent);

    /// <summary>Dalga basina toplam dusman (agirlik birimi): (30 + 8 x KS) x oyuncu carpani.</summary>
    public static int Count(FullKabarmaDef d, int ks, int players) =>
        (int)MathF.Round((d.CountBase + d.CountPerKs * ks) * PlayerMult(d, players));

    public static float EliteRate(FullKabarmaDef d, int ks) => Math.Min(d.EliteMax, d.ElitePerKs * ks);
    public static float HpMult(KabarmaFile k, int ks) => MathF.Pow(k.HpGrowth, ks - 1);
    public static float DamageMult(KabarmaFile k, int ks) => MathF.Pow(k.DamageGrowth, ks - 1);
    public static float MayaKsMult(KabarmaFile k, int ks) => 1f + k.MayaKsFactor * ks;

    /// <summary>Haritada bitisik biyom (biomeMap.order sirasi); yoksa null.</summary>
    public static string? Neighbor(BiomesFile b, string biome)
    {
        List<string> order = b.World.BiomeMap.Order;
        int i = order.IndexOf(biome);
        if (i < 0) return null;
        return i + 1 < order.Count ? order[i + 1] : i > 0 ? order[i - 1] : null;
    }

    /// <summary>Kucuk Kabarma'nin KS'si: secilenin yarisi (en az 1).</summary>
    public static int SmallKs(FullKabarmaDef d, int ks) => Math.Max(1, (int)MathF.Round(ks * d.Small.KsFactor));

    /// <summary>Bir gecenin planini kurar (GDD 14.1, 14.3, 14.6, 14.7).</summary>
    public static WavePlan Compose(GameData data, KabarmaSpec spec)
    {
        var k = data.Kabarma;
        var d = k.Full;
        var rng = new Rng(spec.Seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL);
        int ks = spec.Kind == KabarmaKind.Small ? SmallKs(d, spec.Ks) : Math.Max(1, spec.Ks);
        bool noCrops = spec.PlantedFamilies.Values.Sum() <= 0;
        var plan = new WavePlan
        {
            Spec = spec,
            Ks = ks,
            HpMult = HpMult(k, ks),
            DamageMult = DamageMult(k, ks),
            NoCrops = noCrops,
            Rewards = spec.Kind != KabarmaKind.Drill || d.Drill.Rewards,
            AlarmSeconds = spec.Kind == KabarmaKind.Small ? Math.Min(d.AlarmSeconds, 20f) : d.AlarmSeconds,
            SwellSeconds = spec.Kind == KabarmaKind.Small ? Math.Min(d.SwellSeconds, 15f) : d.SwellSeconds,
            MaxCardLevels = spec.Kind == KabarmaKind.Small ? d.Small.MaxCardLevels : 999,
            MaxActive = d.MaxActive,
        };
        plan.MayaMult = !plan.Rewards ? 0f : noCrops ? d.NoCropMayaMult : 1f;
        var tz = spec.Tuzuk;
        if (plan.Rewards && tz.NewMoonCount > 0) plan.MayaMult *= 1f + data.Tuzuk.NewMoon.MayaPerClause * tz.NewMoonCount; // Yeni Ay: secilen madde basina Maya zammi

        int waveCount = spec.Kind == KabarmaKind.Small ? d.Small.WaveCount : d.WaveCount + tz.ExtraWaves; // Uzun Gece
        float waveSeconds = spec.Kind == KabarmaKind.Small ? d.Small.Seconds / Math.Max(1, waveCount) - d.GapSeconds * 0.5f : d.WaveSeconds;
        float gap = tz.NoGap ? 0f : spec.Kind == KabarmaKind.Small ? d.GapSeconds * 0.5f : d.GapSeconds; // Kirik Can: ara yok
        int budget = (int)MathF.Round(Count(d, ks, spec.Players) * (noCrops ? d.NoCropCountMult : 1f));
        if (spec.Kind == KabarmaKind.Small) budget = Math.Max(4, budget / 2);
        float elite = EliteRate(d, ks) + tz.EliteAdd; // Kalin Kabuk: tavanin ustune
        var buckets = Buckets(data, spec, noCrops);

        for (int i = 0; i < waveCount; i++)
        {
            var w = new PlannedWave { Index = i, Seconds = waveSeconds, GapAfter = i == waveCount - 1 && spec.Kind == KabarmaKind.Small ? 0 : gap };
            Fill(data, w, buckets, budget, elite, rng, allElite: false);
            plan.Waves.Add(w);
        }
        if (spec.Kind != KabarmaKind.Small)
        {
            // Doruk (GDD 14.1): her 5. KS'de Azman (savas akisi), oteki KS'lerde elit dalga. Azman yeri isaretli; Azman
            // sozlesmesi gelene kadar o dalga da elit dalgadir.
            var dk = new PlannedWave { Index = waveCount, Doruk = true, Seconds = d.DorukSeconds, GapAfter = 0 };
            if (d.DorukEvery > 0 && ks % d.DorukEvery == 0)
                for (int az = 0; az <= tz.AzmanAdd; az++) // Cift Azman: Doruk'ta ek Azman
                    dk.Slots.Add(new WaveSlot { Azman = true, Species = ks % (2 * d.DorukEvery) == 0 ? "bozuk" : "homur" });
            Fill(data, dk, buckets, (int)MathF.Round(budget * d.DorukEliteShare), 1f, rng, allElite: true);
            plan.Waves.Add(dk);
        }
        return plan;
    }

    sealed record Bucket(float Share, List<(string id, float w)> Species);

    /// <summary>%50 ekili ailelerin turleri (ekili alan oraninda), %30 yerli, %20 komsu; bos pay yerlilere (GDD 14.3).</summary>
    static List<Bucket> Buckets(GameData data, KabarmaSpec spec, bool noCrops)
    {
        var d = data.Kabarma.Full;
        bool Known(string id) => data.Creatures.Creatures.Any(c => c.Id == id && c.Weight > 0);
        var natives = (d.Natives.GetValueOrDefault(spec.Biome) ?? new()).Where(Known).ToList();
        var nb = Neighbor(data.Biomes, spec.Biome);
        var neighbors = nb == null ? new List<string>() : (d.Natives.GetValueOrDefault(nb) ?? new()).Where(Known).ToList();
        if (spec.Tuzuk.AnyBiomeMix) // Gocmen Suru: butun biyomlardan rastgele; yerli ve komsu payi bir havuzda
        {
            natives = d.Natives.OrderBy(kv => kv.Key, StringComparer.Ordinal).SelectMany(kv => kv.Value).Where(Known).Distinct().ToList();
            neighbors = new();
        }
        float nativeShare = d.MixNative, neighborShare = d.MixNeighbor, plantedShare = d.MixPlanted;
        if (noCrops) { nativeShare = 1f; neighborShare = 0f; plantedShare = 0f; }
        var planted = new List<(string, float)>();
        int totalPlots = spec.PlantedFamilies.Values.Sum();
        foreach (var (fam, n) in spec.PlantedFamilies.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var sp = (d.Families.GetValueOrDefault(fam) ?? new()).Where(Known).ToList();
            float famShare = totalPlots > 0 ? (float)n / totalPlots : 0f;
            if (sp.Count == 0) { nativeShare += plantedShare * famShare; continue; }
            foreach (var s in sp) planted.Add((s, famShare / sp.Count));
        }
        if (neighbors.Count == 0) { nativeShare += neighborShare; neighborShare = 0f; }
        var list = new List<Bucket>();
        if (planted.Count > 0) list.Add(new Bucket(plantedShare * planted.Sum(p => p.Item2), planted));
        if (natives.Count > 0) list.Add(new Bucket(nativeShare, natives.Select(n => (n, 1f)).ToList()));
        if (neighbors.Count > 0) list.Add(new Bucket(neighborShare, neighbors.Select(n => (n, 1f)).ToList()));
        return list;
    }

    static void Fill(GameData data, PlannedWave w, List<Bucket> buckets, int budget, float eliteRate, Rng rng, bool allElite)
    {
        var d = data.Kabarma.Full;
        float total = buckets.Sum(b => b.Share);
        if (total <= 0) return;
        foreach (var b in buckets)
        {
            float left = budget * b.Share / total;
            int guard = 0;
            while (left > 0.5f && guard++ < 10000)
            {
                var id = Pick(b.Species, rng);
                var c = data.Creature(id);
                bool swarm = c.Swarm != null;
                var slot = new WaveSlot { Species = id, Swarm = swarm };
                if (!swarm && d.Elites.Count > 0 && (allElite || rng.Chance(eliteRate)))
                    slot.Elite = d.Elites[rng.Range(0, d.Elites.Count)].Id;
                w.Slots.Add(slot);
                left -= Math.Max(1, c.Weight);
            }
        }
        // Dalga ici sira: turler karisik gelsin (tohumla).
        for (int i = w.Slots.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            (w.Slots[i], w.Slots[j]) = (w.Slots[j], w.Slots[i]);
        }
    }

    static string Pick(List<(string id, float w)> l, Rng rng)
    {
        float t = l.Sum(x => x.w), r = rng.NextFloat() * t;
        foreach (var (id, wt) in l) { r -= wt; if (r <= 0) return id; }
        return l[^1].id;
    }

    /// <summary>Bir giris icin Kabaran durumu: tur degerleri x KS carpanlari x elit oneki.</summary>
    public static KabaranState NewKabaran(GameData data, WavePlan plan, WaveSlot slot, int id, int wave, float x, float z)
    {
        var c = data.Creature(slot.Species);
        var e = string.IsNullOrEmpty(slot.Elite) ? null : data.Kabarma.Full.Elites.FirstOrDefault(el => el.Id == slot.Elite);
        float hp = c.Hp * plan.HpMult * (e?.Hp ?? 1f);
        return new KabaranState
        {
            Id = id, Species = c.Id, Elite = slot.Elite, Wave = wave, X = x, Z = z,
            Hp = hp, MaxHp = hp,
            Damage = c.Damage * plan.DamageMult * (e?.Damage ?? 1f),
            SpeedMult = e?.Speed ?? 1f,
            Scale = e?.Scale ?? 1f,
            Armor = e?.Armor ?? 0f,
        };
    }

    /// <summary>Zirh azaltmasi (GDD 16.1): zirh / (zirh + 100). savas sozlesmesi gelene kadar burada (gecici).</summary>
    public static float AfterArmor(float dmg, float armor) => armor <= 0 ? dmg : dmg * (1f - armor / (armor + 100f));

    /// <summary>Kazan'in cani (Ocak basamagina gore).</summary>
    public static float KazanHp(FullKabarmaDef d, int tier) =>
        d.Kazan.HpByTier.Count == 0 ? 1000f : d.Kazan.HpByTier[Math.Clamp(tier - 1, 0, d.Kazan.HpByTier.Count - 1)];

    /// <summary>Plani mevcut gece akisina (KabarmaNight) verilecek NightDef'e cevirir: dalga sayisi = giris sayisi.</summary>
    public static NightDef ToNightDef(WavePlan plan, NightDef first) => new()
    {
        Ks = plan.Ks,
        AlarmSeconds = plan.AlarmSeconds,
        SwellSeconds = plan.SwellSeconds,
        Waves = plan.Waves.Select(w => new WaveDef { Count = w.Slots.Count(s => !s.Azman), Seconds = w.Seconds, GapAfter = w.GapAfter }).ToList(),
        MaxCardLevels = plan.MaxCardLevels,
        Enemy = first.Enemy,
        FirstOfferCard = null,
        MemberStartCard = true,
    };
}
