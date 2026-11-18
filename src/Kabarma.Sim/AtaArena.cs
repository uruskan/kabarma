namespace Kabarma.Sim;

/// <summary>Bir Ata arenasinin dunyadaki yeri: merkez [x, z], yaricap ve nereden geldigi.</summary>
public readonly record struct AtaArenaSpot(float X, float Z, float Radius, bool FromSite, string SiteId);

/// <summary>
/// Arena yeri (GDD §18.1): dunya2'nin yerlestirdigi Ata Tasi kesif noktasi (biomes.json biyom "spawns.atalar" satiri ve
/// "pois" ata_tasi; SpawnTable.AtaSite) oynanabilir arazinin icindeyse oradadir; yoksa (Sis Korusu'nun noktasi henuz
/// arazinin disinda) atalar.json arena.center yedegi. Durumsuz: ayni veri ayni yeri verir.
/// </summary>
public static class AtaArena
{
    public static AtaArenaSpot Resolve(GameState s, AtaDef def)
    {
        var site = s.Spawns.AtaSite(def.Id, s.Pois);
        if (site is { } found && OnMap(s.Data.Biomes, found.site.X, found.site.Z, found.def.ArenaRadius))
            return new AtaArenaSpot(found.site.X, found.site.Z, found.def.ArenaRadius > 0 ? found.def.ArenaRadius : def.Arena.Radius, true, found.site.Id);
        return new AtaArenaSpot(def.Arena.Center[0], def.Arena.Center[1], def.Arena.Radius, false, "");
    }

    /// <summary>Arena, arazinin kenar sirti baslamadan (terrain.edgeStart) ve dunya kenarinin icinde mi.</summary>
    public static bool OnMap(BiomesFile b, float x, float z, float radius)
    {
        var t = b.World.Terrain;
        float half = t.Size * 0.5f;
        float edge = t.EdgeStart > 0 ? Math.Min(t.EdgeStart, half) : half;
        // Kenar yukselisi kare (World.BaseHeight: max(|x|,|z|) EdgeStart'tan sonra; dunya2), daire degil.
        return Math.Max(Math.Abs(x), Math.Abs(z)) + radius <= edge;
    }
}
