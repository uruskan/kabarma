namespace Kabarma.Sim;

/// <summary>
/// Akis alani maliyet izgarasi (TEKNIK 8, 8.1): 1 m'lik hucreler, bir pencere (kenar GridSize, merkez parametre).
/// Hucre maliyeti = zemin (egim, su; ucurum gecilmez) + engel izi (kirma maliyeti). Iz, sahip kimligiyle (yapi parcasi,
/// yerlestirilebilir, kapi, tuzak) ve turuyle eklenir/kaldirilir; degisen hucreler degisim gunlugune yazilir, alanlar
/// (FlowField) oradan artimli onarilir. Duz ve motordan bagimsiz; turetilmis veridir, kayda yazilmaz.
/// </summary>
public sealed class FlowGrid
{
    public const int Impassable = -1;
    /// <summary>Komsu yonleri: 0 dogu, sonra saat yonunun tersine; tek indisler capraz.</summary>
    public static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    public static readonly int[] DZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

    readonly FlowFieldFile _f;
    readonly int[] _terrain;
    readonly int[] _break;
    readonly Dictionary<int, List<ulong>> _cellOwners = new();
    readonly Dictionary<ulong, (string kind, int[] cells)> _stamps = new();
    readonly List<int> _log = new();
    long _logBase;

    public int Size { get; }
    /// <summary>(0,0) hucresinin dunya koseleri (x, z).</summary>
    public float OriginX { get; }
    public float OriginZ { get; }
    public int OpenCost => _f.OpenCost;
    public FlowFieldFile Def => _f;

    public FlowGrid(FlowFieldFile def, float centerX, float centerZ, int? size = null)
    {
        _f = def;
        Size = Math.Max(4, size ?? def.GridSize);
        OriginX = MathF.Floor(centerX) - Size / 2;
        OriginZ = MathF.Floor(centerZ) - Size / 2;
        _terrain = new int[Size * Size];
        _break = new int[Size * Size];
        Array.Fill(_terrain, def.OpenCost);
    }

    public int Count => Size * Size;
    public int Index(int x, int z) => z * Size + x;
    public int X(int i) => i % Size;
    public int Z(int i) => i / Size;
    public bool In(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;

    /// <summary>Dunya konumunun hucresi; pencere disindaysa -1.</summary>
    public int CellAt(float wx, float wz)
    {
        int x = (int)MathF.Floor(wx - OriginX), z = (int)MathF.Floor(wz - OriginZ);
        return In(x, z) ? Index(x, z) : -1;
    }

    public (float x, float z) Center(int i) => (OriginX + X(i) + 0.5f, OriginZ + Z(i) + 0.5f);

    /// <summary>Hucreye girme maliyeti; gecilmezse Impassable.</summary>
    public int Cost(int i) => _terrain[i] < 0 ? Impassable : _terrain[i] + _break[i];
    public bool Passable(int i) => _terrain[i] >= 0;
    /// <summary>Hucrede kirma maliyetli bir iz var mi (capraz gecis yasagi da buna bakar).</summary>
    public bool Stamped(int i) => _break[i] > 0;
    public int BreakAt(int i) => _break[i];

    /// <summary>
    /// (ux, uz) hucresinden k yonundeki komsuya yurumenin maliyeti (komsuya girme; capraz x1,4); gecilmezse -1.
    /// Capraz adim, iki dik komsudan biri gecilmez ya da izliyse kapalidir (eğik duvar sizdirmaz).
    /// </summary>
    public int WalkCost(int ux, int uz, int k, out int n)
    {
        int nx = ux + DX[k], nz = uz + DZ[k];
        n = -1;
        if (!In(nx, nz)) return -1;
        n = Index(nx, nz);
        int c = Cost(n);
        if (c < 0) return -1;
        if ((k & 1) == 0) return c;
        int a = Index(nx, uz), b = Index(ux, nz);
        if (!Passable(a) || !Passable(b) || Stamped(a) || Stamped(b)) return -1;
        return c * 14 / 10;
    }

    // ---------------------------------------------------------------- zemin

    /// <summary>
    /// Zemini kurar: egim (hucre merkezinin iki yanindaki yukseklik farkindan, Capa.Toprak gibi) ve su. Butun hucreler
    /// degisim gunlugune girer (alanlar bastan hesaplanir).
    /// </summary>
    public void BuildTerrain(Func<float, float, float> height, Func<float, float, bool> water)
    {
        for (int i = 0; i < Count; i++)
        {
            var (cx, cz) = Center(i);
            float dx = height(cx + 0.5f, cz) - height(cx - 0.5f, cz);
            float dz = height(cx, cz + 0.5f) - height(cx, cz - 0.5f);
            float slope = MathF.Atan(MathF.Sqrt(dx * dx + dz * dz)) * 180f / MathF.PI;
            _terrain[i] = TerrainCost(slope, water(cx, cz));
        }
        for (int i = 0; i < Count; i++) _log.Add(i);
    }

    public int TerrainCost(float slopeDeg, bool water)
    {
        if (slopeDeg > _f.MaxSlopeDegrees) return Impassable;
        int c = _f.OpenCost + (int)MathF.Round(slopeDeg * _f.SlopeCostPerDegree);
        return water ? c + _f.WaterCost : c;
    }

    /// <summary>Sinama ve elle duzenleme: tek hucrenin zemin maliyeti.</summary>
    public void SetTerrain(int i, int cost)
    {
        if (_terrain[i] == cost) return;
        _terrain[i] = cost;
        _log.Add(i);
    }

    // ---------------------------------------------------------------- izler

    public bool HasStamp(ulong owner) => _stamps.ContainsKey(owner);
    public IReadOnlyCollection<ulong> Owners => _stamps.Keys;
    public string? KindOf(ulong owner) => _stamps.TryGetValue(owner, out var s) ? s.kind : null;
    public IReadOnlyList<int> CellsOf(ulong owner) => _stamps.TryGetValue(owner, out var s) ? s.cells : Array.Empty<int>();

    /// <summary>Sahibin izini koyar ya da degistirir (ayni sahibin eski izi kalkar). Bos hucre listesi izi kaldirir.</summary>
    public void Stamp(ulong owner, string kind, IEnumerable<int> cells)
    {
        var list = cells.Where(i => i >= 0 && i < Count).Distinct().OrderBy(i => i).ToArray();
        if (_stamps.TryGetValue(owner, out var old) && old.kind == kind && old.cells.SequenceEqual(list)) return;
        Unstamp(owner);
        if (list.Length == 0) return;
        _stamps[owner] = (kind, list);
        foreach (int i in list)
        {
            if (!_cellOwners.TryGetValue(i, out var o)) _cellOwners[i] = o = new List<ulong>(1);
            o.Add(owner);
            Refresh(i);
        }
    }

    public void Unstamp(ulong owner)
    {
        if (!_stamps.Remove(owner, out var s)) return;
        foreach (int i in s.cells)
        {
            if (_cellOwners.TryGetValue(i, out var o)) { o.Remove(owner); if (o.Count == 0) _cellOwners.Remove(i); }
            Refresh(i);
        }
    }

    void Refresh(int i)
    {
        int b = 0;
        if (_cellOwners.TryGetValue(i, out var o)) foreach (var ow in o) b = Math.Max(b, _f.BreakOf(_stamps[ow].kind));
        if (b == _break[i]) return;
        _break[i] = b;
        _log.Add(i);
    }

    /// <summary>Hucredeki en pahali izin sahibi ve turu (kirilacak engel); iz yoksa null.</summary>
    public (ulong owner, string kind)? BlockerAt(int i)
    {
        if (i < 0 || !_cellOwners.TryGetValue(i, out var o) || o.Count == 0) return null;
        ulong best = o[0]; int bc = -1;
        foreach (var ow in o)
        {
            int c = _f.BreakOf(_stamps[ow].kind);
            if (c > bc || (c == bc && ow < best)) { bc = c; best = ow; }
        }
        return bc > 0 ? (best, _stamps[best].kind) : null;
    }

    // ---------------------------------------------------------------- degisim gunlugu

    public long LogEnd => _logBase + _log.Count;
    public long LogStart => _logBase;

    /// <summary>[from, LogEnd) araligindaki degisen hucreler.</summary>
    public IEnumerable<int> ChangesSince(long from)
    {
        for (long k = Math.Max(from, _logBase); k < LogEnd; k++) yield return _log[(int)(k - _logBase)];
    }

    /// <summary>Butun alanlar okuduktan sonra gunlugun basini atar.</summary>
    public void TrimLog(long upTo)
    {
        int n = (int)Math.Clamp(upTo - _logBase, 0, _log.Count);
        if (n == 0) return;
        _log.RemoveRange(0, n);
        _logBase += n;
    }

    // ---------------------------------------------------------------- tarama

    /// <summary>
    /// Donuk dikdortgenin (merkez, yari genislikler, Y ekseni etrafinda donus, radyan) degdigi butun hucreler (tasan
    /// tarama: kenara degen hucre de girer). Duvar, cit, kapi izleri bununla kurulur.
    /// </summary>
    public IEnumerable<int> CellsUnderBox(float cx, float cz, float hx, float hz, float yaw)
    {
        float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
        // Kutunun eksenleri (dunya): yerel x -> (c, -s), yerel z -> (s, c) (Godot Y donusu).
        float ax = c, az = -s, bx = s, bz = c;
        float ex = MathF.Abs(ax) * hx + MathF.Abs(bx) * hz, ez = MathF.Abs(az) * hx + MathF.Abs(bz) * hz;
        int x0 = (int)MathF.Floor(cx - ex - OriginX), x1 = (int)MathF.Floor(cx + ex - OriginX);
        int z0 = (int)MathF.Floor(cz - ez - OriginZ), z1 = (int)MathF.Floor(cz + ez - OriginZ);
        for (int z = Math.Max(0, z0); z <= Math.Min(Size - 1, z1); z++)
        for (int x = Math.Max(0, x0); x <= Math.Min(Size - 1, x1); x++)
        {
            // Ayirici eksen sinamasi: hucre karesi (yari 0,5) ile donuk kutu.
            float px = OriginX + x + 0.5f - cx, pz = OriginZ + z + 0.5f - cz;
            const float eps = 1e-4f;
            // Dunya eksenleri
            if (MathF.Abs(px) > ex + 0.5f - eps || MathF.Abs(pz) > ez + 0.5f - eps) continue;
            // Kutunun eksenleri
            float pa = px * ax + pz * az, pb = px * bx + pz * bz;
            float ra = 0.5f * (MathF.Abs(ax) + MathF.Abs(az)), rb = 0.5f * (MathF.Abs(bx) + MathF.Abs(bz));
            if (MathF.Abs(pa) > hx + ra - eps || MathF.Abs(pb) > hz + rb - eps) continue;
            yield return Index(x, z);
        }
    }
}
