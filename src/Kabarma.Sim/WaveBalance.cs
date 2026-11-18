namespace Kabarma.Sim;

/// <summary>
/// Onerilen donanim (URETIM M3 kabul: denge simulasyonu). GECICI: savas, kartlar ve savunma akislari sayilarini
/// getirene kadar kabarma.json "balance" bolumundeki yer tutucu donanimla kosar.
/// </summary>
public sealed class BalanceDef
{
    /// <summary>Capa'nin taban saniye hasari (Civi 22 / 0,24 sn ~ 92) ve isabet orani.</summary>
    public float BaseDps { get; set; } = 90f;
    public float Accuracy { get; set; } = 0.7f;
    /// <summary>Tirpan alan vurusu: ortalama ayni anda vurulan dusman.</summary>
    public float AreaTargets { get; set; } = 1.6f;
    /// <summary>Onerilen donanimin KS basina hasar artisi (basamak, cip, baslik; carpimsal).</summary>
    public float GearPerKs { get; set; } = 0.12f;
    /// <summary>Gece karti: seviye basina hasar artisi ve dalga basina ortalama seviye.</summary>
    public float CardPerLevel { get; set; } = 0.035f;
    public float LevelsPerWave { get; set; } = 2.6f;
    /// <summary>Kule/tuzak saniye hasari: KS basina (onerilen savunma).</summary>
    public float TowerDpsPerKs { get; set; } = 18f;
    /// <summary>Oyuncu beceri dagilimi (ortalama 1, sapma).</summary>
    public float SkillSd { get; set; } = 0.22f;
    /// <summary>Tapu sinirindan Kazan'a yurume suresi (sn) ve Kazan'a yuklenen dusmanin vurus araligi.</summary>
    public float TravelSeconds { get; set; } = 9f;
    public float AttackInterval { get; set; } = 1.3f;
    /// <summary>Onerilen Ocak basamagi: KS / bu (yukari yuvarlanir).</summary>
    public float KsPerOcakTier { get; set; } = 2f;
    public int Runs { get; set; } = 300;
    public float PassMin { get; set; } = 0.6f;
    public float PassMax { get; set; } = 0.8f;
}

/// <summary>
/// Denge simulasyonu (URETIM M3): planin dalgalari saniye saniye gelir; oyuncu (+ savunma) beceriyle olceklenen hasarla
/// one en yakin dusmani eritir; Kazan'a varan dusmanlar vurur. Kazan safaga kadar dayanirsa gecti.
/// </summary>
public static class WaveBalance
{
    public readonly record struct Result(bool Passed, float KazanLeft, int Killed, int Reached);

    public static Result Simulate(GameData data, BalanceDef b, int ks, int players, ulong seed)
    {
        var full = data.Kabarma.Full;
        var rng = new Rng(seed * 0x2545F4914F6CDD1DUL + 17);
        int tier = Math.Max(1, (int)MathF.Ceiling(ks / Math.Max(0.1f, b.KsPerOcakTier)));
        var spec = new KabarmaSpec { Kind = KabarmaKind.Full, Ks = ks, Players = players, Biome = "altin_cayir", OcakTier = tier, Seed = seed,
            PlantedFamilies = new() { ["root"] = 8, ["grain"] = 8, ["gourd"] = 4 } };
        var plan = Waves.Compose(data, spec);
        // Beceri: normal dagilim (Box-Muller), oyuncu basina.
        float skill = 0f;
        for (int p = 0; p < players; p++)
        {
            float u1 = Math.Max(1e-6f, rng.NextFloat()), u2 = rng.NextFloat();
            float z = MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);
            skill += Math.Clamp(1f + z * b.SkillSd, 0.3f, 1.8f);
        }
        float kazan = Waves.KazanHp(full, tier);
        int killed = 0, reached = 0;
        var alive = new List<(float hp, float age, float dps)>();
        float t = 0;
        int level = 0;
        foreach (var w in plan.Waves)
        {
            var slots = w.Slots.Where(s => !s.Azman).ToList();
            int spawned = 0;
            float spread = Math.Max(1f, w.Seconds * 0.6f);
            for (float wt = 0; wt < w.Seconds + w.GapAfter; wt += 1f)
            {
                int should = Math.Min(slots.Count, 1 + (int)(wt / spread * slots.Count));
                while (spawned < should && alive.Count < plan.MaxActive)
                {
                    var k = Waves.NewKabaran(data, plan, slots[spawned++], 0, w.Index, 0, 0);
                    alive.Add((k.MaxHp, 0f, k.Damage / b.AttackInterval));
                }
                float dmgMult = MathF.Pow(1f + b.GearPerKs, ks - 1) * (1f + b.CardPerLevel * level);
                float budget = (b.BaseDps * b.Accuracy * b.AreaTargets * skill * dmgMult + b.TowerDpsPerKs * ks) * 1f;
                // En eski (Kazan'a en yakin) dusman once.
                alive.Sort((a, c) => c.age.CompareTo(a.age));
                for (int i = 0; i < alive.Count && budget > 0; i++)
                {
                    var e = alive[i];
                    float d = Math.Min(budget, e.hp);
                    budget -= d;
                    alive[i] = (e.hp - d, e.age, e.dps);
                }
                killed += alive.RemoveAll(e => e.hp <= 0.01f);
                for (int i = 0; i < alive.Count; i++)
                {
                    var e = alive[i];
                    if (e.age >= b.TravelSeconds) { kazan -= e.dps; if (e.age - 1f < b.TravelSeconds) reached++; }
                    alive[i] = (e.hp, e.age + 1f, e.dps);
                }
                t += 1f;
                if (kazan <= 0) return new Result(false, 0, killed, reached);
            }
            level += (int)MathF.Round(b.LevelsPerWave);
        }
        return new Result(true, kazan, killed, reached);
    }

    /// <summary>Gecme orani: b.Runs tohumla.</summary>
    public static float PassRate(GameData data, BalanceDef b, int ks, int players = 1)
    {
        int pass = 0;
        for (int i = 0; i < b.Runs; i++) if (Simulate(data, b, ks, players, (ulong)(i * 7919 + ks * 104729 + players)).Passed) pass++;
        return pass / (float)b.Runs;
    }
}
