namespace Kabarma.Sim;

/// <summary>
/// Dogma tablolari (biomes.json "spawns", TEKNIK §7 "M2 dunyasi"): biyomdaki yaratik suruleri ve Atalar.
/// Durumsuz kurallar: hangi satir secilebilir, suru boyu, dogma yeri. Yasayan surulerin sayimi cagiranda
/// (Godot katmaninda aktor sayisi; kayitta tur basina sayac). Yaratik akislari satir ekler, kod degismez.
/// </summary>
public sealed class SpawnTable
{
    readonly BiomesFile _biomes;
    readonly BiomeMap _map;

    public SpawnTable(BiomesFile biomes, BiomeMap map) { _biomes = biomes; _map = map; }

    public static bool TimeMatches(string time, bool night) =>
        time switch { "any" => true, "night" => night, _ => !night };

    /// <summary>Biyomun bu saatte dogabilecek satirlari.</summary>
    public IEnumerable<CreatureSpawnDef> Candidates(string biome, bool night) =>
        _biomes.Biomes.TryGetValue(biome, out var b)
            ? b.Spawns.Creatures.Where(c => c.Weight > 0 && TimeMatches(c.Time, night))
            : Enumerable.Empty<CreatureSpawnDef>();

    /// <summary>
    /// Agirlikli secim. alive(satir) o turun yasayan suru sayisi; MaxGroups dolu satirlar elenir.
    /// known(kimlik) false donen tur (aktoru henuz yok) atlanir.
    /// </summary>
    public CreatureSpawnDef? Pick(string biome, bool night, Rng rng, Func<CreatureSpawnDef, int> alive, Func<string, bool>? known = null)
    {
        var list = Candidates(biome, night).Where(c => alive(c) < c.MaxGroups && (known?.Invoke(c.Creature) ?? true)).ToList();
        float total = list.Sum(c => c.Weight);
        if (total <= 0) return null;
        float r = rng.NextFloat() * total;
        foreach (var c in list) { r -= c.Weight; if (r <= 0) return c; }
        return list[^1];
    }

    public static int GroupSize(CreatureSpawnDef def, Rng rng)
    {
        int lo = def.Group.Count > 0 ? def.Group[0] : 1;
        int hi = def.Group.Count > 1 ? def.Group[1] : lo;
        return rng.Range(lo, hi + 1);
    }

    /// <summary>
    /// Dogma yeri: biyom agirligi en az MinWeight, Ocak'a uzaklik BaseDistance icinde, dunya kenarinin icinde, golde degil;
    /// NearPoi doluysa o turden bir kesif noktasinin 25 m'sinde. Bulunamazsa null.
    /// </summary>
    public (float x, float z)? Place(string biome, CreatureSpawnDef def, Rng rng, float baseX = 0, float baseZ = 0, IReadOnlyList<Poi>? pois = null, int tries = 200)
    {
        var w = _biomes.World;
        float half = w.Terrain.Size / 2f - 10f;
        var near = string.IsNullOrEmpty(def.NearPoi) || pois == null ? null : pois.Where(p => p.Type == def.NearPoi && p.Biome == biome).ToList();
        for (int i = 0; i < tries; i++)
        {
            float x, z;
            if (near is { Count: > 0 })
            {
                var p = near[rng.Range(0, near.Count)];
                float a = rng.Range(0f, MathF.Tau), d = rng.Range(4f, 25f);
                x = p.X + MathF.Cos(a) * d; z = p.Z + MathF.Sin(a) * d;
            }
            else { x = rng.Range(-half, half); z = rng.Range(-half, half); }
            if (MathF.Abs(x) > half || MathF.Abs(z) > half) continue;
            float bd = MathF.Sqrt((x - baseX) * (x - baseX) + (z - baseZ) * (z - baseZ));
            if (bd < def.BaseDistance[0] || bd > def.BaseDistance[1]) continue;
            if (_map.Weight(biome, x, z) < def.MinWeight) continue;
            if (PoiPlacer.InLake(w, x, z)) continue;
            return (x, z);
        }
        return null;
    }

    /// <summary>Ata'nin cagrildigi kesif noktasi (boss akisi): biyomun Ata satiri ve onun Ata Tasi.</summary>
    public (AtaSpawnDef def, Poi site)? AtaSite(string ata, IReadOnlyList<Poi> pois)
    {
        foreach (var (id, b) in _biomes.Biomes)
            foreach (var a in b.Spawns.Atalar.Where(a => a.Ata == ata))
            {
                var site = pois.FirstOrDefault(p => p.Type == a.Site && p.Biome == id);
                if (site != null) return (a, site);
            }
        return null;
    }
}
