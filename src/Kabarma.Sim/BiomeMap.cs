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
    /// <summary>Derinlik zincirindeki biyomlar (Cayir, Koru); M2 dunyasinda Order'in kendisi.</summary>
    public IReadOnlyList<string> Home => _def.Home.Count > 0 ? _def.Home : _def.Order;
    readonly bool _craters;
    readonly float _cx, _cz;

    public BiomeMap(BiomesFile biomes)
    {
        _def = biomes.World.BiomeMap;
        var p = biomes.World.Pillar;
        float len = p.Count >= 2 ? MathF.Sqrt(p[0] * p[0] + p[1] * p[1]) : 0f;
        (_dx, _dz) = len > 0 ? (p[0] / len, p[1] / len) : (0f, 1f);
        if (_def.Order.Count == 0) _def.Order.Add(biomes.World.Biome);
        var t = biomes.World.Terrain;
        (_worldCx, _worldCz, _worldHalf) = (t.CenterX, t.CenterZ, t.Size / 2f);
        _craters = _def.Crater.Radius > 0 && _def.Crater.Center.Count >= 2 && _def.Slices.Count > 0;
        if (_craters) (_cx, _cz) = (_def.Crater.Center[0], _def.Crater.Center[1]);
    }

    readonly Dictionary<string, (float x0, float z0, float x1, float z1)> _bounds = new();
    readonly float _worldCx, _worldCz, _worldHalf;

    /// <summary>
    /// Biyomun agirliginin 0,02'yi astigi bolgenin sinir kutusu (32 m'lik tarama, dunya karesi icinde): kucuk biyomlarda
    /// (Gobek) dogma ve serpme reddetme ornekleme yeterli denemeye sahip olsun. Durumsuz, tohumdan.
    /// </summary>
    public (float x0, float z0, float x1, float z1) Bounds(string biome)
    {
        lock (_bounds)
        {
            if (_bounds.TryGetValue(biome, out var b)) return b;
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            for (float x = _worldCx - _worldHalf; x <= _worldCx + _worldHalf; x += 32f)
            for (float z = _worldCz - _worldHalf; z <= _worldCz + _worldHalf; z += 32f)
                if (Weight(biome, x, z) > 0.02f) { x0 = MathF.Min(x0, x); z0 = MathF.Min(z0, z); x1 = MathF.Max(x1, x); z1 = MathF.Max(z1, z); }
            b = x0 > x1 ? (_worldCx - _worldHalf, _worldCz - _worldHalf, _worldCx + _worldHalf, _worldCz + _worldHalf)
                        : (MathF.Max(x0 - 32f, _worldCx - _worldHalf), MathF.Max(z0 - 32f, _worldCz - _worldHalf), MathF.Min(x1 + 32f, _worldCx + _worldHalf), MathF.Min(z1 + 32f, _worldCz + _worldHalf));
            _bounds[biome] = b;
            return b;
        }
    }

    /// <summary>Kraterli dunya mi (M4); degilse yalniz derinlik zinciri.</summary>
    public bool HasCrater => _craters;
    public float CraterX => _cx;
    public float CraterZ => _cz;
    public CraterDef Crater => _def.Crater;

    /// <summary>Krater sirti yuksekligi (m): Radius*RimStart..Radius*RimEnd arasinda cos tumsegi; disinda tam 0.</summary>
    public float RimHeight(float x, float z)
    {
        if (!_craters || _def.Crater.RimHeight == 0) return 0f;
        float r = MathF.Sqrt((x - _cx) * (x - _cx) + (z - _cz) * (z - _cz));
        float a = _def.Crater.Radius * _def.Crater.RimStart, b = _def.Crater.Radius * _def.Crater.RimEnd;
        if (r <= a || r >= b) return 0f;
        float t = (r - a) / (b - a);
        return _def.Crater.RimHeight * 0.5f * (1f - MathF.Cos(t * MathF.Tau));
    }

    /// <summary>Kratere uzaklik (m) ve acisal konum (derece), bukulmus (sinirlar duz cizgi olmasin).</summary>
    (float r, float ang) Polar(float x, float z)
    {
        float wx = x + WorldNoise.Fbm(_def.Seed + 1000, x, z, _def.WarpFrequency, 3) * _def.WarpAmp;
        float wz = z + WorldNoise.Fbm(_def.Seed + 2000, x, z, _def.WarpFrequency, 3) * _def.WarpAmp;
        float dx = wx - _cx, dz = wz - _cz;
        return (MathF.Sqrt(dx * dx + dz * dz), MathF.Atan2(dz, dx) * (180f / MathF.PI));
    }

    static float AngDist(float a, float b)
    {
        float d = MathF.Abs(a - b) % 360f;
        return d > 180f ? 360f - d : d;
    }

    /// <summary>Dilimin agirligi (0..1): bitisik dilimlerle toplami 1 (simetrik yumusak sinir).</summary>
    float SliceWeight(SliceDef s, float ang, float r)
    {
        float tb = Math.Clamp(MathF.Atan(_def.Blend * 0.5f / Math.Max(r, 1f)) * (180f / MathF.PI), 1f, 25f);
        float d = AngDist(ang, s.Center);
        return 1f - SmoothStep(s.HalfWidth - tb, s.HalfWidth + tb, d);
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

    /// <summary>Derinlik zincirinde (Home) biyomun payi; zincir kendi icinde toplami 1.</summary>
    float HomeWeight(int i, float x, float z)
    {
        float d = Depth(x, z);
        float inside = i == 0 ? 1f : Past(i - 1, d);
        float beyond = i < _def.Boundaries.Count ? Past(i, d) : 0f;
        return Math.Clamp(inside - beyond, 0f, 1f);
    }

    /// <summary>
    /// Butun biyomlarin agirligi (Order sirasiyla; toplami 1) tek gecista: derinlik ve kutup hesaplari paylasilir
    /// (dunya yuksekligi ve doku dokulari her hucrede cagirir).
    /// </summary>
    public void Weights(float x, float z, Span<float> w)
    {
        var order = _def.Order;
        int n = order.Count;
        for (int i = 0; i < n && i < w.Length; i++) w[i] = 0f;
        if (!_craters)
        {
            float d = Depth(x, z);
            for (int i = 0; i < n && i < w.Length; i++)
            {
                float inside = i == 0 ? 1f : Past(i - 1, d);
                float beyond = i < _def.Boundaries.Count ? Past(i, d) : 0f;
                w[i] = Math.Clamp(inside - beyond, 0f, 1f);
            }
            return;
        }
        var (r, ang) = Polar(x, z);
        float g = 1f - SmoothStep(_def.Crater.Radius - _def.Crater.Blend * 0.5f, _def.Crater.Radius + _def.Crater.Blend * 0.5f, r);
        float total = 0, homeSlice = 0;
        Span<float> sw = stackalloc float[Math.Max(_def.Slices.Count, 1)];
        for (int k = 0; k < _def.Slices.Count; k++)
        {
            sw[k] = SliceWeight(_def.Slices[k], ang, r);
            total += sw[k];
            if (_def.Slices[k].Biome == "home") homeSlice += sw[k];
        }
        if (total <= 1e-6f) return;
        float rest = (1f - g) / total;
        for (int k = 0; k < _def.Slices.Count; k++)
        {
            if (_def.Slices[k].Biome == "home") continue;
            int i = order.IndexOf(_def.Slices[k].Biome);
            if (i >= 0 && i < w.Length) w[i] += sw[k] * rest;
        }
        if (homeSlice > 0)
        {
            float d = Depth(x, z);
            var home = _def.Home.Count > 0 ? _def.Home : order;
            for (int h = 0; h < home.Count; h++)
            {
                float inside = h == 0 ? 1f : Past(h - 1, d);
                float beyond = h < _def.Boundaries.Count ? Past(h, d) : 0f;
                int i = order.IndexOf(home[h]);
                if (i >= 0 && i < w.Length) w[i] += homeSlice * rest * Math.Clamp(inside - beyond, 0f, 1f);
            }
        }
        int gi = order.IndexOf(_def.Crater.Biome);
        if (gi >= 0 && gi < w.Length) w[gi] = g;
    }

    /// <summary>Biyomun bu noktadaki agirligi (0..1). Bilinmeyen biyom 0. Toplam 1.</summary>
    public float Weight(string biome, float x, float z)
    {
        int i = _def.Order.IndexOf(biome);
        if (i < 0) return 0f;
        Span<float> w = stackalloc float[_def.Order.Count];
        Weights(x, z, w);
        return w[i];
    }

    /// <summary>Agirligi en buyuk biyom.</summary>
    public string BiomeAt(float x, float z)
    {
        Span<float> w = stackalloc float[_def.Order.Count];
        Weights(x, z, w);
        int best = 0;
        for (int i = 1; i < w.Length; i++) if (w[i] > w[best]) best = i;
        return _def.Order[best];
    }
    /// <summary>Biyomun kademesi (T): siradaki konumu + 1 (Cayir T1 ... Gobek T6; Order kademe sirasiyla).</summary>
    public int Tier(string biome) => _def.Order.IndexOf(biome) + 1;
}
