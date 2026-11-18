namespace Kabarma.Sim;

/// <summary>Yerlesmis kesif noktasi. Kimlik tohumdan kararlidir ("seyir_kulesi_sis_korusu_0"); kayit ona baglanir.</summary>
public sealed class Poi
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Biome { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
}

/// <summary>
/// Kesif noktasi yerlesimi (TEKNIK §7.3: kuralli Poisson). Tur basina asgari aralik, biyom agirligi, Ocak'a uzaklik,
/// gol ve dunya kenari disi; turler arasi da en az CrossSpacing. Once elle konmus yerler, sonra tohumdan.
/// Durumsuz: ayni veri ayni listeyi verir (kayda yalniz oyuncunun bu noktalardaki durumu gider: Exploration).
/// </summary>
public static class PoiPlacer
{
    /// <summary>Farkli turden iki nokta arasi en az (m).</summary>
    public const float CrossSpacing = 22f;
    /// <summary>Dunya kenarindan ic pay (m).</summary>
    public const float EdgeMargin = 24f;

    public static List<Poi> Place(BiomesFile biomes, BiomeMap map)
    {
        var w = biomes.World;
        float half = w.Terrain.Size / 2f - EdgeMargin;
        var placed = new List<Poi>();
        foreach (var biomeId in map.Order)
        {
            if (!biomes.Biomes.TryGetValue(biomeId, out var biome)) continue;
            var rng = new Rng((ulong)(w.Terrain.Seed * 7919 + biome.Seed * 104729 + 17));
            foreach (var rule in biome.Pois)
            {
                int made = 0;
                bool Ok(float x, float z, bool strict)
                {
                    float d = MathF.Sqrt(x * x + z * z);
                    if (MathF.Abs(x) > half || MathF.Abs(z) > half) return false;
                    if (strict && (d < rule.BaseDistance[0] || d > rule.BaseDistance[1])) return false;
                    if (strict && map.Weight(biomeId, x, z) < rule.MinWeight) return false;
                    if (InLake(w, x, z)) return false;
                    foreach (var p in placed)
                    {
                        float dd = MathF.Sqrt((p.X - x) * (p.X - x) + (p.Z - z) * (p.Z - z));
                        if (dd < CrossSpacing) return false;
                        if (p.Type == rule.Type && dd < rule.MinSpacing) return false;
                    }
                    return true;
                }
                void Add(float x, float z) =>
                    placed.Add(new Poi { Id = $"{rule.Type}_{biomeId}_{made++}", Type = rule.Type, Biome = biomeId, X = x, Z = z });

                foreach (var f in rule.Fixed)
                    if (made < rule.Count && f.Count >= 2 && Ok(f[0], f[1], strict: false)) Add(f[0], f[1]);
                for (int tries = 0; tries < 4000 && made < rule.Count; tries++)
                {
                    float x = rng.Range(-half, half), z = rng.Range(-half, half);
                    if (Ok(x, z, strict: true)) Add(x, z);
                }
            }
        }
        return placed;
    }

    /// <summary>Gol ve kiyi payi (veriden: merkez, yaricap + kiyi gurultusu + 6 m).</summary>
    public static bool InLake(WorldDef w, float x, float z)
    {
        var l = w.Lake;
        if (l.Center.Count < 2 || l.Radius <= 0) return false;
        float dx = x - l.Center[0], dz = z - l.Center[1];
        return MathF.Sqrt(dx * dx + dz * dz) < l.Radius + l.ShoreNoise + 6f;
    }
}
