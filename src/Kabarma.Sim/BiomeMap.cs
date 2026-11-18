namespace Kabarma.Sim;

/// <summary>
/// Motordan bagimsiz, tohumlu deger gurultusu (TEKNIK §3: dunya uretimi Sim'de). Godot'un FastNoiseLite'ina dayanmaz;
/// host ve istemci ayni tohumdan ayni haritayi uretir. Cikti yaklasik [-1, 1].
/// </summary>
public static class WorldNoise
{
    static float Hash(int seed, int x, int z)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)z * 0xC2B2AE3Du;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    public static float Value(int seed, float x, float z)
    {
        int x0 = (int)MathF.Floor(x), z0 = (int)MathF.Floor(z);
        float tx = Smooth(x - x0), tz = Smooth(z - z0);
        float a = Hash(seed, x0, z0), b = Hash(seed, x0 + 1, z0);
        float c = Hash(seed, x0, z0 + 1), d = Hash(seed, x0 + 1, z0 + 1);
        float ab = a + (b - a) * tx, cd = c + (d - c) * tx;
        return ab + (cd - ab) * tz;
    }

    /// <summary>Kesirli Brown hareketi: oktav basina frekans 2, genlik 0,5.</summary>
    public static float Fbm(int seed, float x, float z, float frequency, int octaves = 3)
    {
        float sum = 0, amp = 1, norm = 0, f = frequency;
        for (int i = 0; i < octaves; i++)
        {
            sum += Value(seed + i * 131, x * f, z * f) * amp;
            norm += amp; amp *= 0.5f; f *= 2f;
        }
        return sum / norm;
    }
}

/// <summary>
/// Biyom haritasi (TEKNIK §7 "M2 dunyasi"): Ocak'tan krater sutununa dogru olculen uzaklik, gurultuyle bukulmus
/// sinirlari gecince sonraki biyom baslar. Agirliklar yumusak (GDD §21 "gecisler yumusaktir"); toplami 1.
/// Duz ve durumsuz: yalniz veriden kurulur, kayda girmez.
/// </summary>
public sealed class BiomeMap
{
    readonly BiomeMapDef _def;
    readonly float _dx, _dz;

    public IReadOnlyList<string> Order => _def.Order;

    public BiomeMap(BiomesFile biomes)
    {
        _def = biomes.World.BiomeMap;
        var p = biomes.World.Pillar;
        float len = p.Count >= 2 ? MathF.Sqrt(p[0] * p[0] + p[1] * p[1]) : 0f;
        (_dx, _dz) = len > 0 ? (p[0] / len, p[1] / len) : (0f, 1f);
        if (_def.Order.Count == 0) _def.Order.Add(biomes.World.Biome);
    }

    /// <summary>Krater yonundeki uzaklik + sinir bukulmesi (m).</summary>
    public float Depth(float x, float z) =>
        x * _dx + z * _dz + WorldNoise.Fbm(_def.Seed, x, z, _def.WarpFrequency, 3) * _def.WarpAmp;

    static float SmoothStep(float a, float b, float v)
    {
        float t = Math.Clamp((v - a) / Math.Max(1e-4f, b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>i. sinirin otesinde olma payi (0..1).</summary>
    float Past(int i, float depth) => SmoothStep(_def.Boundaries[i] - _def.Blend * 0.5f, _def.Boundaries[i] + _def.Blend * 0.5f, depth);

    /// <summary>Biyomun bu noktadaki agirligi (0..1). Bilinmeyen biyom 0.</summary>
    public float Weight(string biome, float x, float z)
    {
        int i = _def.Order.IndexOf(biome);
        if (i < 0) return 0f;
        float d = Depth(x, z);
        float inside = i == 0 ? 1f : Past(i - 1, d);
        float beyond = i < _def.Boundaries.Count ? Past(i, d) : 0f;
        return Math.Clamp(inside - beyond, 0f, 1f);
    }

    /// <summary>Agirligi en buyuk biyom.</summary>
    public string BiomeAt(float x, float z)
    {
        float d = Depth(x, z);
        int i = 0;
        while (i < _def.Boundaries.Count && d >= _def.Boundaries[i]) i++;
        return _def.Order[Math.Min(i, _def.Order.Count - 1)];
    }

    /// <summary>Biyomun kademesi (T): siradaki konumu + 1 (Cayir T1, Koru T2).</summary>
    public int Tier(string biome) => _def.Order.IndexOf(biome) + 1;
}
