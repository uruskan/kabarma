namespace Kabarma.Sim;

/// <summary>Bir akis alani sorgusunun sonucu (TEKNIK 8.1).</summary>
public readonly struct FlowSample
{
    /// <summary>Konum pencere icinde ve hedefe yol var.</summary>
    public bool Valid { get; init; }
    public bool AtGoal { get; init; }
    /// <summary>Sonraki hucreye birim yon (dunya x, z).</summary>
    public float DirX { get; init; }
    public float DirZ { get; init; }
    /// <summary>Hedefe kalan maliyet (birim; OpenCost = 1 m duz yol).</summary>
    public int Cost { get; init; }
    /// <summary>Sonraki hucrenin merkezi (dunya).</summary>
    public float NextX { get; init; }
    public float NextZ { get; init; }
    /// <summary>Sonraki hucre bir engel izinin icinde: kirilacak engelin sahibi ve turu (yoksa 0 / null).</summary>
    public ulong Blocker { get; init; }
    public string? BlockerKind { get; init; }
    public bool Blocked => Blocker != 0;
}

/// <summary>
/// Tek hedef kumesine akis alani (TEKNIK 8): her hucrenin hedefe en ucuz yol maliyeti ve sonraki hucresi (Dijkstra,
/// 8 komsu, capraz adim x1,4, koseden capraz gecis yok: iki dik komsudan biri gecilmez ya da izliyse capraz kapali).
/// Degisimden sonra artimli onarilir: degisen hucreden gecen en kisa yollarin alt agaci sifirlanip sinirindan yeniden
/// yayilir; iyilesme degisen hucrelerin cevresinden yayilir. Sonuc tam hesapla ayni maliyeti verir (FlowFieldTests).
/// </summary>
public sealed class FlowField
{
    public const int Inf = int.MaxValue;
    readonly FlowGrid _g;
    readonly int[] _dist;
    readonly int[] _parent;
    readonly HashSet<int> _goals = new();
    readonly int[] _mark;
    int _markGen;
    readonly PriorityQueue<int, int> _pq = new();
    long _seen;
    bool _full = true;
    readonly List<int> _goalsRemoved = new(), _goalsAdded = new();

    public string Key { get; }
    /// <summary>Son hesapta dokunulan hucre sayisi (olcum).</summary>
    public int LastTouched { get; private set; }
    public int Recomputes { get; private set; }
    public int Repairs { get; private set; }
    public IReadOnlyCollection<int> Goals => _goals;

    public FlowField(FlowGrid grid, string key)
    {
        _g = grid;
        Key = key;
        _dist = new int[grid.Count];
        _parent = new int[grid.Count];
        _mark = new int[grid.Count];
        Array.Fill(_dist, Inf);
        Array.Fill(_parent, -1);
        _seen = grid.LogEnd;
    }

    public int Dist(int i) => _dist[i];
    public int Parent(int i) => _parent[i];
    public long Seen => _seen;

    /// <summary>Hedef kumesini degistirir (hesap bir sonraki Update'te).</summary>
    public void SetGoals(IEnumerable<int> cells)
    {
        var want = new HashSet<int>(cells.Where(i => i >= 0 && i < _g.Count));
        foreach (int g in _goals) if (!want.Contains(g)) _goalsRemoved.Add(g);
        foreach (int g in want) if (!_goals.Contains(g)) _goalsAdded.Add(g);
        _goals.Clear();
        _goals.UnionWith(want);
    }

    /// <summary>Izgara ya da hedef degisti mi (Update bir sey yapacak mi).</summary>
    public bool Dirty => _full || _seen < _g.LogEnd || _goalsRemoved.Count > 0 || _goalsAdded.Count > 0;

    /// <summary>Bekleyen degisimi uygular. Gunluk cok gerideyse ya da ilk kezse tam hesap. Donen: bir sey yapildi mi.</summary>
    public bool Update(int maxRepairCells = 4096)
    {
        if (!Dirty) return false;
        if (_full || _seen < _g.LogStart || _g.LogEnd - _seen > maxRepairCells) Recompute();
        else Repair();
        return true;
    }

    /// <summary>Bastan hesap.</summary>
    public void Recompute()
    {
        Array.Fill(_dist, Inf);
        Array.Fill(_parent, -1);
        _pq.Clear();
        foreach (int g in _goals) { _dist[g] = 0; _pq.Enqueue(g, 0); }
        LastTouched = Run();
        _seen = _g.LogEnd;
        _full = false;
        _goalsRemoved.Clear();
        _goalsAdded.Clear();
        Recomputes++;
    }

    /// <summary>Gunlukteki degisen hucrelerden ve hedef farkindan artimli onarim.</summary>
    void Repair()
    {
        var changed = _g.ChangesSince(_seen).Distinct().ToList();
        _seen = _g.LogEnd;
        int gen = ++_markGen;
        // 1. Kokler: degisen hucre, cevresinde capraz geldigi yolu o hucrenin kosesinden gecenler, kalkan hedefler.
        var stack = new Stack<int>();
        void Root(int i) { if (_mark[i] != gen) { _mark[i] = gen; stack.Push(i); } }
        foreach (int c in changed)
        {
            Root(c);
            ForNeighbours(c, n => { int p = _parent[n]; if (p >= 0 && _g.X(p) != _g.X(n) && _g.Z(p) != _g.Z(n)) Root(n); });
        }
        foreach (int g in _goalsRemoved) Root(g);
        // 2. Alt agac: ebeveyni etkilenen olan her hucre etkilenir.
        var affected = new List<int>();
        while (stack.Count > 0)
        {
            int u = stack.Pop();
            affected.Add(u);
            ForNeighbours(u, n => { if (_parent[n] == u && _mark[n] != gen) { _mark[n] = gen; stack.Push(n); } });
        }
        foreach (int a in affected) { _dist[a] = Inf; _parent[a] = -1; }
        // 3. Tohumlar: etkilenenlerin etkilenmemis komsulari, degisen hucrelerin cevresi, hedefler.
        _pq.Clear();
        foreach (int a in affected)
        {
            if (_goals.Contains(a)) { _dist[a] = 0; _pq.Enqueue(a, 0); continue; }
            ForNeighbours(a, n => { if (_mark[n] != gen && _dist[n] != Inf) _pq.Enqueue(n, _dist[n]); });
        }
        foreach (int c in changed)
        {
            if (_dist[c] != Inf) _pq.Enqueue(c, _dist[c]);
            ForNeighbours(c, n => { if (_dist[n] != Inf) _pq.Enqueue(n, _dist[n]); });
        }
        foreach (int g in _goalsAdded) { if (_goals.Contains(g)) { _dist[g] = 0; _parent[g] = -1; _pq.Enqueue(g, 0); } }
        _goalsRemoved.Clear();
        _goalsAdded.Clear();
        LastTouched = affected.Count + Run();
        Repairs++;
    }

    void ForNeighbours(int i, Action<int> f)
    {
        int x = _g.X(i), z = _g.Z(i);
        for (int k = 0; k < 8; k++)
        {
            int nx = x + FlowGrid.DX[k], nz = z + FlowGrid.DZ[k];
            if (_g.In(nx, nz)) f(_g.Index(nx, nz));
        }
    }

    /// <summary>
    /// Kuyruktan Dijkstra. Akis alani hedeften disa yayilir; u'dan n'ye adim, n'den u'ya yurumenin maliyetidir, yani
    /// u'ya girmenin maliyeti (n zaten oradadir). Bu yuzden kenar agirligi hedef yonundeki hucre (u) uzerinden okunur.
    /// </summary>
    int Run()
    {
        int touched = 0;
        while (_pq.TryDequeue(out int u, out int d))
        {
            if (d != _dist[u]) continue;
            touched++;
            int ux = _g.X(u), uz = _g.Z(u);
            int enter = _g.Cost(u);
            if (enter < 0) { if (!_goals.Contains(u)) continue; enter = _g.OpenCost; } // hedef gecilmez zeminde olabilir (kazan, ekin)
            for (int k = 0; k < 8; k++)
            {
                // n -> u yurume: u'ya girme maliyeti; capraz kurali ayni kose hucreleri icin simetrik.
                int nx = ux + FlowGrid.DX[k], nz = uz + FlowGrid.DZ[k];
                if (!_g.In(nx, nz)) continue;
                int n = _g.Index(nx, nz);
                if (!_g.Passable(n)) continue;
                if ((k & 1) == 1)
                {
                    int a = _g.Index(nx, uz), b = _g.Index(ux, nz);
                    if (!_g.Passable(a) || !_g.Passable(b) || _g.Stamped(a) || _g.Stamped(b)) continue;
                }
                int w = (k & 1) == 1 ? enter * 14 / 10 : enter;
                long nd = (long)d + w;
                if (nd >= _dist[n]) continue;
                _dist[n] = (int)nd;
                _parent[n] = u;
                _pq.Enqueue(n, (int)nd);
            }
        }
        return touched;
    }

    /// <summary>Dunya konumunda yon ve sonraki hucre.</summary>
    public FlowSample Sample(float wx, float wz)
    {
        int i = _g.CellAt(wx, wz);
        if (i < 0 || _dist[i] == Inf) return default;
        if (_goals.Contains(i)) { var (gx, gz) = _g.Center(i); return new FlowSample { Valid = true, AtGoal = true, Cost = 0, NextX = gx, NextZ = gz }; }
        int p = _parent[i];
        if (p < 0) return default;
        var (px, pz) = _g.Center(p);
        float dx = px - wx, dz = pz - wz, len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 1e-4f) { var (cx, cz) = _g.Center(i); dx = px - cx; dz = pz - cz; len = MathF.Sqrt(dx * dx + dz * dz); }
        var blk = _goals.Contains(p) ? null : _g.BlockerAt(p);
        return new FlowSample
        {
            Valid = true, Cost = _dist[i], DirX = dx / len, DirZ = dz / len, NextX = px, NextZ = pz,
            Blocker = blk?.owner ?? 0, BlockerKind = blk?.kind,
        };
    }
}

/// <summary>
/// Pencere basina alan kumesi (TEKNIK 8.1): ortak maliyet izgarasi, hedef anahtari basina bir alan (yalniz sorgulanan
/// anahtarlar). Tick'te en cok MaxRecomputesPerTick alan guncellenir; guncellenmeyen eski haliyle gecerli kalir.
/// Holding icin ayni izgarada kirma maliyetli A* (FindPath).
/// </summary>
public sealed class FlowFieldSet
{
    readonly Dictionary<string, FlowField> _fields = new(StringComparer.Ordinal);
    int _rr;
    public FlowGrid Grid { get; }
    public IReadOnlyDictionary<string, FlowField> Fields => _fields;

    public FlowFieldSet(FlowGrid grid) { Grid = grid; }

    /// <summary>Anahtarin alani (yoksa kurulur ve hemen hesaplanir).</summary>
    public FlowField Field(string key)
    {
        if (_fields.TryGetValue(key, out var f)) return f;
        f = new FlowField(Grid, key);
        _fields[key] = f;
        return f;
    }

    public bool Has(string key) => _fields.ContainsKey(key);

    /// <summary>Anahtarin hedef hucreleri (dunya konumlari; pencere disindakiler atilir).</summary>
    public void SetGoals(string key, IEnumerable<(float x, float z)> world) =>
        Field(key).SetGoals(world.Select(p => Grid.CellAt(p.x, p.z)).Where(i => i >= 0));

    public FlowSample Sample(string key, float x, float z)
    {
        if (!_fields.TryGetValue(key, out var f)) return default;
        if (f.Recomputes == 0) f.Update(); // ilk sorgu: hemen hesapla
        return f.Sample(x, z);
    }

    /// <summary>Kirli alanlari sirayla gunceller (en cok max tane). Donen: guncellenen sayisi.</summary>
    public int Tick(int? max = null)
    {
        int budget = max ?? Grid.Def.MaxRecomputesPerTick, done = 0;
        var keys = _fields.Keys.ToList();
        for (int k = 0; k < keys.Count && done < budget; k++)
        {
            var f = _fields[keys[(_rr + k) % keys.Count]];
            if (f.Update()) done++;
        }
        if (keys.Count > 0) _rr = (_rr + 1) % keys.Count;
        // Butun alanlarin okudugu gunluk kismi atilir.
        if (_fields.Count > 0) Grid.TrimLog(_fields.Values.Min(f => f.Seen));
        return done;
    }

    /// <summary>
    /// Kirma maliyetli A* (Holding ekipleri, TEKNIK 8): baslangictan hedefe hucre listesi (baslangic dahil). Engel
    /// hucreleri yolda kalabilir; cagiran Grid.BlockerAt ile kiracagini bulur. Yol yoksa bos.
    /// </summary>
    public List<int> FindPath(float fromX, float fromZ, float toX, float toZ, int maxExpand = 40000)
    {
        int s = Grid.CellAt(fromX, fromZ), t = Grid.CellAt(toX, toZ);
        var path = new List<int>();
        if (s < 0 || t < 0) return path;
        var g = new Dictionary<int, int> { [s] = 0 };
        var from = new Dictionary<int, int>();
        var open = new PriorityQueue<int, int>();
        int tx = Grid.X(t), tz = Grid.Z(t);
        int H(int i) { int dx = Math.Abs(Grid.X(i) - tx), dz = Math.Abs(Grid.Z(i) - tz); return Grid.OpenCost * (Math.Max(dx, dz) * 10 + Math.Min(dx, dz) * 4) / 10; }
        open.Enqueue(s, H(s));
        int expanded = 0;
        while (open.TryDequeue(out int u, out int pr))
        {
            if (u == t) break;
            if (pr - H(u) > g[u]) continue;
            if (++expanded > maxExpand) return path;
            int ux = Grid.X(u), uz = Grid.Z(u);
            for (int k = 0; k < 8; k++)
            {
                int w = Grid.WalkCost(ux, uz, k, out int n);
                if (w < 0) continue;
                int nd = g[u] + w;
                if (g.TryGetValue(n, out int old) && old <= nd) continue;
                g[n] = nd; from[n] = u;
                open.Enqueue(n, nd + H(n));
            }
        }
        if (!g.ContainsKey(t)) return path;
        for (int c = t; ; c = from[c]) { path.Add(c); if (c == s) break; }
        path.Reverse();
        return path;
    }
}
