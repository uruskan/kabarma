namespace Kabarma.Sim;

/// <summary>Oyuncunun haritaya koydugu isaret (GDD §21 harita; "isaretler").</summary>
public sealed class MapMarker
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>Kesif noktasinin tuketilen durumu: kalan birim ve yenilenecegi oyun gunu.</summary>
public sealed class PoiUse
{
    public int Left { get; set; }
    public int RefillDay { get; set; }
}

/// <summary>
/// Kesif durumu (GDD §21): gezilen yer acilir, Seyir Kulesi'ne tirmanmak yaricapi acar, kesif noktalari bulunur,
/// mancinik duraklari acilir, oyuncu isaret koyar, Enkaz/Yuva/Yabani Bahce tuketilip yenilenir.
/// Duz ve serilestirilebilir (TEKNIK §9): yalniz kimlik, sayi, dizi. Yerler tohumdan (PoiPlacer), burada degil.
/// Izgara satir satir bitlerdir: hucre (i, j) = Bits[(j * N + i) / 8] bit (j * N + i) % 8; dunyanin (-S/2, -S/2) kosesi (0, 0).
/// </summary>
public sealed class Exploration
{
    public float WorldSize { get; set; }
    public float Cell { get; set; }
    public int N { get; set; }
    public byte[] Bits { get; set; } = Array.Empty<byte>();
    public int Revealed { get; set; }
    public List<string> Found { get; set; } = new();
    public List<string> Climbed { get; set; } = new();
    public List<MapMarker> Markers { get; set; } = new();
    public int NextMarker { get; set; } = 1;
    public Dictionary<string, PoiUse> Uses { get; set; } = new();
    /// <summary>Mancinik ucuslari (ICERIK §18 "Ucan Uye" zinciri sayaci).</summary>
    public int Flights { get; set; }

    public Exploration() { }

    public Exploration(GameData data) : this(data.Biomes.World.Terrain.Size, data.Biomes.World.Map.Cell) { }

    public Exploration(float worldSize, float cell)
    {
        WorldSize = worldSize;
        Cell = Math.Max(1f, cell);
        N = (int)MathF.Ceiling(worldSize / Cell);
        Bits = new byte[(N * N + 7) / 8];
    }

    public int Total => N * N;
    public float Percent => Total == 0 ? 0 : 100f * Revealed / Total;

    public (int i, int j) CellOf(float x, float z) =>
        (Math.Clamp((int)MathF.Floor((x + WorldSize / 2f) / Cell), 0, N - 1),
         Math.Clamp((int)MathF.Floor((z + WorldSize / 2f) / Cell), 0, N - 1));

    public bool IsRevealed(int i, int j)
    {
        if (i < 0 || j < 0 || i >= N || j >= N) return false;
        int k = j * N + i;
        return (Bits[k >> 3] & (1 << (k & 7))) != 0;
    }

    public bool IsRevealedAt(float x, float z) { var (i, j) = CellOf(x, z); return IsRevealed(i, j); }

    /// <summary>Merkezi (x, z), yaricapi r olan daireyi acar; yeni acilan hucre sayisini dondurur.</summary>
    public int Reveal(float x, float z, float r)
    {
        int added = 0;
        var (i0, j0) = CellOf(x - r, z - r);
        var (i1, j1) = CellOf(x + r, z + r);
        float r2 = (r + Cell * 0.5f) * (r + Cell * 0.5f);
        for (int j = j0; j <= j1; j++)
        for (int i = i0; i <= i1; i++)
        {
            float cx = (i + 0.5f) * Cell - WorldSize / 2f, cz = (j + 0.5f) * Cell - WorldSize / 2f;
            if ((cx - x) * (cx - x) + (cz - z) * (cz - z) > r2) continue;
            int k = j * N + i;
            if ((Bits[k >> 3] & (1 << (k & 7))) != 0) continue;
            Bits[k >> 3] |= (byte)(1 << (k & 7));
            added++;
        }
        Revealed += added;
        return added;
    }

    /// <summary>Kesif noktasinin bulunmasi (ilk kez yakinina gelince). Yeniyse true.</summary>
    public bool Discover(string poiId)
    {
        if (Found.Contains(poiId)) return false;
        Found.Add(poiId);
        return true;
    }

    /// <summary>Seyir Kulesi'ne tirmanma: kule bulunur, duragi acilir, Reveal yaricapi acilir. Ilk tirmanista true.</summary>
    public bool Climb(Poi tower, PoiTypeDef type)
    {
        Discover(tower.Id);
        bool first = !Climbed.Contains(tower.Id);
        if (first) Climbed.Add(tower.Id);
        if (type.Reveal > 0) Reveal(tower.X, tower.Z, type.Reveal);
        return first;
    }

    public MapMarker AddMarker(float x, float z, string label)
    {
        var m = new MapMarker { Id = NextMarker++, X = x, Z = z, Label = label };
        Markers.Add(m);
        return m;
    }

    public bool RemoveMarker(int id) => Markers.RemoveAll(m => m.Id == id) > 0;

    /// <summary>Noktadan alinabilecek birim (bos ve yenilenme gunu geldiyse doldurur).</summary>
    public int Left(Poi poi, PoiTypeDef type, int day)
    {
        int full = Loot(poi, type).Values.Sum();
        if (!Uses.TryGetValue(poi.Id, out var u)) return full;
        if (u.Left <= 0 && type.RespawnDays > 0 && day >= u.RefillDay) { Uses.Remove(poi.Id); return full; }
        return u.Left;
    }

    public static Dictionary<string, int> Loot(Poi poi, PoiTypeDef type) =>
        type.LootByBiome.TryGetValue(poi.Biome, out var l) ? l : type.Loot;

    /// <summary>
    /// Bir vurus/etkilesim: en cok want birim alir; esyalar Loot sirasiyla dagitilir (once ilk esya). Bos kalinca
    /// RefillDay = bugun + RespawnDays. Alinanlari dondurur (bos = yok).
    /// </summary>
    public List<(string item, int count)> Take(Poi poi, PoiTypeDef type, int day, int want)
    {
        var got = new List<(string, int)>();
        int left = Left(poi, type, day);
        if (left <= 0 || want <= 0) return got;
        var loot = Loot(poi, type);
        int full = loot.Values.Sum();
        int taken = full - left; // once alinmis olanlari atla
        int n = Math.Min(want, left);
        foreach (var (item, count) in loot)
        {
            int skip = Math.Min(taken, count); taken -= skip;
            int can = Math.Min(count - skip, n);
            if (can > 0) { got.Add((item, can)); n -= can; }
            if (n <= 0) break;
        }
        int used = got.Sum(g => g.Item2);
        if (!Uses.TryGetValue(poi.Id, out var u)) Uses[poi.Id] = u = new PoiUse { Left = left };
        u.Left = left - used;
        if (u.Left <= 0) u.RefillDay = type.RespawnDays > 0 ? day + (int)MathF.Ceiling(type.RespawnDays) : int.MaxValue;
        return got;
    }
}
