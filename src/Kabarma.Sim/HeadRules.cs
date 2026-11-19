namespace Kabarma.Sim;

// kusam (M4): Capa silah basliklari #6-#12 (ICERIK 6.2). Sayilar weapons.json "weaponHeads"; davranisin dunyadaki karsiligi Godot'ta
// (Capa.Heads4.cs), kural burada duz ve test edilebilir.

/// <summary>Tohum Makinalisi: tohumun turu etkiyi belirler (ICERIK 6.2 #9). Anahtar ekin kimligi.</summary>
public sealed class SeedEffectDef
{
    public float Burn { get; set; }
    public int Pierce { get; set; }
    public float Slow { get; set; }
    public float SlowSeconds { get; set; }
    public float DamageMult { get; set; } = 1f;
}

public sealed class HeadDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    /// <summary>cone (Alev), mortar (Havan), chain (Ark), seed (Tohum), beam (Ayaz Lazeri), gravity (Yercekimi), orb (Maya Topu).</summary>
    public string Kind { get; set; } = "";
    public string DamageType { get; set; } = "darbe";
    public float Damage { get; set; }
    public float FireInterval { get; set; }
    public float Range { get; set; }
    public float MinRange { get; set; }
    public float ConeDegrees { get; set; }
    public float Splash { get; set; }
    public int ChainCount { get; set; }
    public float ChainRange { get; set; }
    public float ChainDecay { get; set; } = 1f;
    public float ChargeSeconds { get; set; }
    public float FlightSpeed { get; set; }
    public float BurnSeconds { get; set; }
    public float FreezeSeconds { get; set; }
    public float SwellSeconds { get; set; }
    public float HoldDistance { get; set; }
    public float ThrowSpeed { get; set; }
    public float GrabRadius { get; set; }
    /// <summary>Cephane esyasi; bos: cephanesiz. "{tohum}": secili ekinin tohumu.</summary>
    public string Ammo { get; set; } = "";
    public float AmmoPerShot { get; set; }
    public float AmmoPerSecond { get; set; }
    /// <summary>Acilma (ICERIK 6.2): A arastirma, G Genel Merkez. Tarif kimligi bu kimlikle ayni.</summary>
    public string Unlock { get; set; } = "A";
    public Dictionary<string, SeedEffectDef> Seeds { get; set; } = new();
}

public sealed partial class ToolFile
{
    public List<HeadDef> WeaponHeads { get; set; } = new();
    public HeadDef? Head4(string id) => WeaponHeads.FirstOrDefault(h => h.Id == id);
}

/// <summary>Silah baslik kurallari: koni, havan inisi, zincir, tohum etkisi, cephane (durumsuz, duz).</summary>
public static class HeadRules
{
    /// <summary>Hedef, namlunun onunde, menzil ve koni icinde mi (yatay).</summary>
    public static bool InCone(float ox, float oz, float dx, float dz, float coneDeg, float range, float tx, float tz, float targetRadius = 0f)
    {
        float vx = tx - ox, vz = tz - oz;
        float dist = MathF.Sqrt(vx * vx + vz * vz);
        if (dist > range + targetRadius) return false;
        if (dist < 0.001f) return true;
        float dl = MathF.Sqrt(dx * dx + dz * dz);
        if (dl < 0.001f) return false;
        float cos = (vx * dx + vz * dz) / (dist * dl);
        float half = coneDeg * 0.5f * MathF.PI / 180f;
        // yaricapli hedef: kenar acisi payi
        float slack = targetRadius > 0 && dist > 0.01f ? MathF.Atan2(targetRadius, dist) : 0f;
        return MathF.Acos(Math.Clamp(cos, -1f, 1f)) <= half + slack;
    }

    /// <summary>Havan: yuklenme oranina gore inis mesafesi (MinRange..Range).</summary>
    public static float MortarDistance(HeadDef h, float heldSeconds)
    {
        float t = h.ChargeSeconds <= 0 ? 1f : Math.Clamp(heldSeconds / h.ChargeSeconds, 0f, 1f);
        return h.MinRange + (h.Range - h.MinRange) * t;
    }

    /// <summary>Havan: ucus suresi (sn).</summary>
    public static float MortarFlight(HeadDef h, float distance) => h.FlightSpeed <= 0 ? 0.5f : distance / h.FlightSpeed;

    /// <summary>Ark: baslangic noktasindan, her adimda en yakin kullanilmamis hedefe sicrayan sira. Aday konumlari (x,z).</summary>
    public static List<int> ChainOrder(float sx, float sz, IReadOnlyList<(float x, float z)> cands, int count, float hopRange)
    {
        var used = new List<int>();
        float cx = sx, cz = sz;
        for (int step = 0; step < count; step++)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < cands.Count; i++)
            {
                if (used.Contains(i)) continue;
                float dx = cands[i].x - cx, dz = cands[i].z - cz, d = dx * dx + dz * dz;
                if (d <= hopRange * hopRange && d < bd) { bd = d; best = i; }
            }
            if (best < 0) break;
            used.Add(best);
            cx = cands[best].x; cz = cands[best].z;
        }
        return used;
    }

    /// <summary>Ark: n'inci sicramanin hasari (her sicramada decay carpani).</summary>
    public static float ChainDamage(HeadDef h, int hop) => h.Damage * MathF.Pow(h.ChainDecay, hop);

    /// <summary>Tohum Makinalisi: tohumun etkisi (yoksa etkisiz, damageMult 1).</summary>
    public static SeedEffectDef SeedEffect(HeadDef h, string cropId) => h.Seeds.TryGetValue(cropId, out var e) ? e : new SeedEffectDef();

    /// <summary>Cephane esya kimligi: "{tohum}" secili ekinin tohumu olur.</summary>
    public static string AmmoItem(GameData d, HeadDef h, string selectedCrop)
    {
        if (h.Ammo != "{tohum}") return h.Ammo;
        return d.FindCrop(selectedCrop)?.SeedId ?? "";
    }

    /// <summary>Ayaz Lazeri ve Alev: sureklilik; dt boyunca hasar ve harcanan cephane.</summary>
    public static (float damage, float ammo) Continuous(HeadDef h, float dt) => (h.Damage / MathF.Max(0.01f, h.FireInterval) * dt, h.AmmoPerSecond * dt);

    /// <summary>Maya Topu: sisme suresi bitince patlama hasari (hedef sisme suresinde isaretli kalir).</summary>
    public static bool SwellDone(HeadDef h, float elapsed) => elapsed >= h.SwellSeconds;
}