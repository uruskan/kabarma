namespace Kabarma.Sim;

/// <summary>
/// Bir oyuncunun guc donanimi (GDD 17, 17.1): yuvalar, ulti, Sarj, beklemeler, Maya olceri ve suren etkiler. Duz ve
/// serilestirilebilir. Yuva, Sarj, bekleme ve olcer oyuncu basinadir.
/// </summary>
public sealed class AbilityLoadout
{
    /// <summary>Etkin yuvalar (activeSlotsMax uzunlugunda; bos yuva "").</summary>
    public List<string> Slots { get; set; } = new();
    public string Ulti { get; set; } = "";
    public float Charge { get; set; } = 100f;
    /// <summary>Son Sarj harcamasindan beri gecen sure.</summary>
    public float SinceSpend { get; set; } = 99f;
    public Dictionary<string, float> Cooldowns { get; set; } = new();
    public float Meter { get; set; }
    /// <summary>Hasar alinmayan kalan sure (Atilma).</summary>
    public float InvulnLeft { get; set; }
    /// <summary>Dev Mod'un kalan suresi.</summary>
    public float GiantLeft { get; set; }

    public float CooldownOf(string id) => Cooldowns.TryGetValue(id, out var c) ? c : 0f;
}

/// <summary>Dunyanin guc ilerlemesi: Kadim Tas'tan acilan gucler (ekibin hepsine acik; ultiler AtaProgress.Ultis'te).</summary>
public sealed class AbilityProgress
{
    public List<string> Unlocked { get; set; } = new();
    /// <summary>Yerel oyuncunun (tek oyuncu, host) donanimi. Istemcilerinki host bellegindedir (M5: karakter dosyasi).</summary>
    public AbilityLoadout Local { get; set; } = new();
}

/// <summary>Guc kurallari: acma, takma, kullanma, Sarj ve olcer. Etkinin dunyadaki karsiligi Godot katmanindadir.</summary>
public static class AbilityRules
{
    /// <summary>Yuva listesini veri boyuna getirir (eksikse "" ekler).</summary>
    public static void Normalize(AbilitiesFile f, AbilityLoadout l)
    {
        while (l.Slots.Count < f.ActiveSlotsMax) l.Slots.Add("");
        if (l.Charge > f.Charge.Max) l.Charge = f.Charge.Max;
    }

    /// <summary>Gucu dunyaya acar; bos acik yuva varsa oraya takar. Yeni acildiysa true.</summary>
    public static bool Unlock(AbilitiesFile f, AbilityProgress p, string id)
    {
        var def = f.Def(id) ?? throw new ArgumentException($"Bilinmeyen guc '{id}'");
        if (def.IsUlti) throw new ArgumentException($"'{id}' ulti: ultiler Atalardan gelir");
        if (p.Unlocked.Contains(id)) return false;
        p.Unlocked.Add(id);
        AutoEquip(f, p, p.Local);
        return true;
    }

    /// <summary>Acik ama takilmamis gucu bos acik yuvaya, acik ultiyi bos ulti yuvasina takar.</summary>
    public static void AutoEquip(AbilitiesFile f, AbilityProgress p, AbilityLoadout l, IReadOnlyList<string>? ultis = null)
    {
        Normalize(f, l);
        foreach (var id in p.Unlocked)
        {
            if (l.Slots.Contains(id)) continue;
            int empty = -1;
            for (int i = 0; i < f.ActiveSlots && i < l.Slots.Count; i++) if (l.Slots[i] == "") { empty = i; break; }
            if (empty < 0) break;
            l.Slots[empty] = id;
        }
        if (l.Ulti == "" && ultis != null)
            foreach (var u in ultis) if (f.Def(u)?.IsUlti == true) { l.Ulti = u; break; }
    }

    /// <summary>Yuvaya guc takar (slot -1: ulti yuvasi). Olmuyorsa Turkce neden.</summary>
    public static string? Equip(AbilitiesFile f, AbilityProgress p, IReadOnlyList<string> ultis, AbilityLoadout l, int slot, string id)
    {
        Normalize(f, l);
        var def = f.Def(id);
        if (def == null) return "Bilinmeyen güç";
        if (slot < 0)
        {
            if (!def.IsUlti) return "Bu bir ulti değil";
            if (!ultis.Contains(id)) return "Bu ulti henüz açılmadı";
            l.Ulti = id;
            return null;
        }
        if (def.IsUlti) return "Ulti etkin yuvaya takılmaz";
        if (slot >= f.ActiveSlots) return "Bu yuva henüz kilitli";
        if (!p.Unlocked.Contains(id)) return "Bu güç henüz açılmadı";
        int other = l.Slots.IndexOf(id);
        if (other >= 0 && other != slot) l.Slots[other] = l.Slots[slot]; // iki yuva yer degistirir
        l.Slots[slot] = id;
        return null;
    }

    /// <summary>Yuvadaki gucun ya da ultinin kullanilip kullanilamayacagi. Olmuyorsa Turkce neden.</summary>
    public static string? CanUse(AbilitiesFile f, AbilityLoadout l, string id)
    {
        var def = f.Def(id);
        if (def == null || id == "") return "Yuva boş";
        if (def.IsUlti)
        {
            if (l.Ulti != id) return "Ulti takılı değil";
            if (l.Meter < f.UltiMeter.Max) return "Maya ölçeri dolmadı";
            if (l.GiantLeft > 0) return "Ulti sürüyor";
            return null;
        }
        if (!l.Slots.Take(f.ActiveSlots).Contains(id)) return "Güç takılı değil";
        if (l.CooldownOf(id) > 0) return "Bekleniyor";
        if (l.Charge < def.Charge) return "Şarj yetmiyor";
        return null;
    }

    /// <summary>Gucu kullanir: Sarj ya da olcer harcanir, bekleme baslar, suren etki kurulur. Olmuyorsa null.</summary>
    public static AbilityDef? Use(AbilitiesFile f, AbilityLoadout l, string id)
    {
        if (CanUse(f, l, id) != null) return null;
        var def = f.Def(id)!;
        if (def.IsUlti)
        {
            l.Meter = 0;
            if (def.Id == "dev_mod") l.GiantLeft = def.Duration;
            return def;
        }
        l.Charge -= def.Charge;
        l.SinceSpend = 0;
        l.Cooldowns[id] = def.Cooldown;
        if (def.Invulnerable > 0) l.InvulnLeft = def.Invulnerable;
        return def;
    }

    /// <summary>Zaman: beklemeler iner, Sarj gecikmeden sonra dolar, suren etkiler biter.</summary>
    public static void Tick(AbilitiesFile f, AbilityLoadout l, float dt)
    {
        if (dt <= 0) return;
        foreach (var k in l.Cooldowns.Keys.ToList())
        {
            float v = l.Cooldowns[k] - dt;
            if (v <= 0) l.Cooldowns.Remove(k); else l.Cooldowns[k] = v;
        }
        l.SinceSpend += dt;
        float regen = MathF.Min(dt, l.SinceSpend - f.Charge.RegenDelay); // gecikmenin bittigi andan itibaren
        if (regen > 0 && l.Charge < f.Charge.Max)
            l.Charge = MathF.Min(f.Charge.Max, l.Charge + f.Charge.RegenPerSecond * regen);
        l.InvulnLeft = MathF.Max(0, l.InvulnLeft - dt);
        l.GiantLeft = MathF.Max(0, l.GiantLeft - dt);
    }

    /// <summary>Kabarma'da oldurme: olcer dolar (GDD 17.1). Kabarma ya da Ata dovusu disinda cagrilmaz.</summary>
    public static void OnKill(AbilitiesFile f, AbilityLoadout l, bool elite) =>
        AddMeter(f, l, elite ? f.UltiMeter.PerEliteKill : f.UltiMeter.PerKill);

    /// <summary>Ata dovusu: Ata'nin canindaki dusus (0..1 oran) olcere yazilir.</summary>
    public static void OnAtaDamage(AbilitiesFile f, AbilityLoadout l, float hpFraction) =>
        AddMeter(f, l, MathF.Max(0, hpFraction) * 100f * f.UltiMeter.PerAtaPercent);

    public static void AddMeter(AbilitiesFile f, AbilityLoadout l, float amount) =>
        l.Meter = Math.Clamp(l.Meter + amount, 0, f.UltiMeter.Max);

    /// <summary>Gelen hasar: Atilma aninda 0, Dev Mod'da azalir.</summary>
    public static float FilterDamage(AbilitiesFile f, AbilityLoadout l, float dmg)
    {
        if (l.InvulnLeft > 0) return 0;
        if (l.GiantLeft > 0) dmg *= f.Def("dev_mod")?.DamageTaken ?? 1f;
        return dmg;
    }

    /// <summary>Alan etkisinin bir hedefe dusen payi: yaricap icinde tam, disinda 0.</summary>
    public static bool InRadius(float cx, float cz, float r, float x, float z)
    {
        float dx = x - cx, dz = z - cz;
        return dx * dx + dz * dz <= r * r;
    }

    /// <summary>Sok Dalgasi itmesi: merkezden disa, kutleye gore (kutle 1 = tam).</summary>
    public static (float x, float z) Knock(AbilityDef def, float cx, float cz, float x, float z, float mass = 1f)
    {
        float dx = x - cx, dz = z - cz;
        float len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 0.001f) { dx = 0; dz = 1; len = 1; }
        float k = def.Knock / MathF.Max(0.5f, mass);
        return (dx / len * k, dz / len * k);
    }
}
