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
/// Hesap arka tamponda yapilir ve hucre butcesiyle karelere bolunebilir (Step); bitince on tamponla takas edilir.
/// Is surerken sorgular eski (on) alani okur. Is sirasinda gelen degisim bir sonraki ise kalir.
/// </summary>
public sealed class FlowField
{
    public const int Inf = int.MaxValue;
    readonly FlowGrid _g;
    int[] _dist, _parent;   // on tampon: sorgular
    int[] _bd, _bp;         // arka tampon: suren is
    readonly HashSet<int> _goals = new();
    readonly int[] _mark;
    int _markGen;
    readonly PriorityQueue<int, int> _pq = new();
    readonly Stack<int> _stack = new();
    readonly List<int> _affected = new();
    long _seen;
    bool _full = true;
    bool _job, _jobFull;
    int _jobTouched;
    readonly List<int> _goalsRemoved = new(), _goalsAdded = new();

    public string Key { get; }
    /// <summary>Son biten iste dokunulan hucre sayisi (olcum).</summary>
    public int LastTouched { get; private set; }
    public int Recomputes { get; private set; }
    public int Repairs { get; private set; }
    /// <summary>Karelere bolunmus bir is suruyor (sorgular eski alani okur).</summary>
    public bool Busy => _job;
    public IReadOnlyCollection<int> Goals => _goals;

    public FlowField(FlowGrid grid, string key)
    {
        _g = grid;
        Key = key;
        _dist = new int[grid.Count];
        _parent = new int[grid.Count];
        _bd = new int[grid.Count];
        _bp = new int[grid.Count];
        _mark = new int[grid.Count];
        Array.Fill(_dist, Inf);
        Array.Fill(_parent, -1);
        _seen = grid.LogEnd;
    }

    public int Dist(int i) => _dist[i];
    public int Parent(int i) => _parent[i];
    /// <summary>Gunlukte bu alanin okudugu yer (suren is baslarken okunanlar dahil).</summary>
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

    /// <summary>Izgara ya da hedef degisti mi ya da is suruyor mu (Update bir sey yapacak mi).</summary>
    public bool Dirty => _job || _full || _seen < _g.LogEnd || _goalsRemoved.Count > 0 || _goalsAdded.Count > 0;

    /// <summary>Bekleyen degisimi butcesiz, sonuna kadar uygular. Donen: bir sey yapildi mi.</summary>
    public bool Update(int maxRepairCells = 4096)
    {
        if (!Dirty) return false;
        while (Dirty) Step(int.MaxValue, maxRepairCells);
        return true;
    }

    /// <summary>
    /// En cok budget hucre kadar is yapar (en az bir adim). Is yoksa ve kirliyse yeni is baslar: ilk kez, gunluk
    /// cok gerideyse ya da kirpildiysa tam hesap, yoksa onarim. Donen: islenen hucre (0: is yoktu).
    /// </summary>
    public int Step(int budget, int maxRepairCells = 4096)
    {
        int used = 0;
        if (!_job)
        {
            if (!Dirty) return 0;
            if (_full || _seen < _g.LogStart || _g.LogEnd - _seen > maxRepairCells) BeginFull();
            else used += BeginRepair();
        }
        used += Run(Math.Max(1, budget - used));
        if (_pq.Count == 0) Finish();
        return Math.Max(1, used);
    }

    /// <summary>Bastan hesap, hemen ve sonuna kadar (suren is birakilir).</summary>
    public void Recompute()
    {
        BeginFull();
        Run(int.MaxValue);
        Finish();
    }

    void BeginFull()
    {
        Array.Fill(_bd, Inf);
        Array.Fill(_bp, -1);
        _pq.Clear();
        foreach (int g in _goals) { _bd[g] = 0; _pq.Enqueue(g, 0); }
        _seen = _g.LogEnd;
        _full = false;
        _goalsRemoved.Clear();
        _goalsAdded.Clear();
        _job = true; _jobFull = true; _jobTouched = 0;
    }

    void Finish()
    {
        (_dist, _bd) = (_bd, _dist);
        (_parent, _bp) = (_bp, _parent);
        _job = false;
        LastTouched = _jobTouched;
        if (_jobFull) Recomputes++; else Repairs++;
    }

    /// <summary>Gunlukteki degisen hucrelerden ve hedef farkindan artimli onarimi kurar (arka tamponda). Donen: etkilenen.</summary>
    int BeginRepair()
    {
        Array.Copy(_dist, _bd, _dist.Length);
        Array.Copy(_parent, _bp, _parent.Length);
        var changed = new HashSet<int>(_g.ChangesSince(_seen));
        _seen = _g.LogEnd;
        int gen = ++_markGen;
        int S = _g.Size;
        // 1. Kokler: degisen hucre, cevresinde capraz geldigi yolu o hucrenin kosesinden gecenler, kalkan hedefler.
        _stack.Clear();
        void Root(int i) { if (_mark[i] != gen) { _mark[i] = gen; _stack.Push(i); } }
        foreach (int c in changed)
        {
            Root(c);
            int cx = c % S, cz = c / S;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + FlowGrid.DX[k], nz = cz + FlowGrid.DZ[k];
                if (nx < 0 || nz < 0 || nx >= S || nz >= S) continue;
                int n = nz * S + nx, p = _bp[n];
                if (p >= 0 && p % S != nx && p / S != nz) Root(n);
            }
        }
        foreach (int g in _goalsRemoved) Root(g);
        // 2. Alt agac: ebeveyni etkilenen olan her hucre etkilenir.
        _affected.Clear();
        while (_stack.Count > 0)
        {
            int u = _stack.Pop();
            _affected.Add(u);
            int ux = u % S, uz = u / S;
            for (int k = 0; k < 8; k++)
            {
                int nx = ux + FlowGrid.DX[k], nz = uz + FlowGrid.DZ[k];
                if (nx < 0 || nz < 0 || nx >= S || nz >= S) continue;
                int n = nz * S + nx;
                if (_bp[n] == u && _mark[n] != gen) { _mark[n] = gen; _stack.Push(n); }
            }
        }
        foreach (int a in _affected) { _bd[a] = Inf; _bp[a] = -1; }
        // 3. Tohumlar: etkilenenlerin etkilenmemis komsulari, degisen hucrelerin cevresi, hedefler.
        _pq.Clear();
        foreach (int a in _affected)
        {
            if (_goals.Contains(a)) { _bd[a] = 0; _pq.Enqueue(a, 0); continue; }
            int ax = a % S, az = a / S;
            for (int k = 0; k < 8; k++)
            {
                int nx = ax + FlowGrid.DX[k], nz = az + FlowGrid.DZ[k];
                if (nx < 0 || nz < 0 || nx >= S || nz >= S) continue;
                int n = nz * S + nx;
                if (_mark[n] != gen && _bd[n] != Inf) _pq.Enqueue(n, _bd[n]);
            }
        }
        foreach (int c in changed)
        {
            if (_bd[c] != Inf) _pq.Enqueue(c, _bd[c]);
            int cx = c % S, cz = c / S;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + FlowGrid.DX[k], nz = cz + FlowGrid.DZ[k];
                if (nx < 0 || nz < 0 || nx >= S || nz >= S) continue;
                int n = nz * S + nx;
                if (_bd[n] != Inf) _pq.Enqueue(n, _bd[n]);
            }
        }
        foreach (int g in _goalsAdded) { if (_goals.Contains(g)) { _bd[g] = 0; _bp[g] = -1; _pq.Enqueue(g, 0); } }
        _goalsRemoved.Clear();
        _goalsAdded.Clear();
        _job = true; _jobFull = false; _jobTouched = _affected.Count;
        return _affected.Count;
    }

    /// <summary>
    /// Kuyruktan Dijkstra (arka tampon), en cok budget hucre. Akis alani hedeften disa yayilir; u'dan n'ye adim, n'den
    /// u'ya yurumenin maliyetidir, yani u'ya girmenin maliyeti (n zaten oradadir). Bu yuzden kenar agirligi hedef
    /// yonundeki hucre (u) uzerinden okunur.
    /// </summary>
    int Run(int budget)
    {
        int touched = 0, S = _g.Size;
        while (touched < budget && _pq.TryDequeue(out int u, out int d))
        {
            if (d != _bd[u]) continue;
            touched++;
            int ux = u % S, uz = u / S;
            int enter = _g.Cost(u);
            if (enter < 0) { if (!_goals.Contains(u)) continue; enter = _g.OpenCost; } // hedef gecilmez zeminde olabilir (kazan, ekin)
            int diag = enter * 14 / 10;
            for (int k = 0; k < 8; k++)
            {
                // n -> u yurume: u'ya girme maliyeti; capraz kurali ayni kose hucreleri icin simetrik.
                int nx = ux + FlowGrid.DX[k], nz = uz + FlowGrid.DZ[k];
                if (nx < 0 || nz < 0 || nx >= S || nz >= S) continue;
                int n = nz * S + nx;
                if (!_g.Passable(n)) continue;
                if ((k & 1) == 1)
                {
                    int a = uz * S + nx, b = nz * S + ux;
                    if (!_g.Passable(a) || !_g.Passable(b) || _g.Stamped(a) || _g.Stamped(b)) continue;
                }
                long nd = (long)d + ((k & 1) == 1 ? diag : enter);
                if (nd >= _bd[n]) continue;
                _bd[n] = (int)nd;
                _bp[n] = u;
                _pq.Enqueue(n, (int)nd);
            }
        }
        _jobTouched += touched;
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
/// anahtarlar). Tick'te en cok MaxRecomputesPerTick alan, toplam en cok MaxCellsPerTick hucre islenir; yarim kalan is
/// sonraki tike surer, o arada eski alan gecerli kalir. Holding icin ayni izgarada kirma maliyetli A* (FindPath).
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
        if (f.Recomputes == 0) f.Update(); // ilk sorgu: hemen, sonuna kadar hesapla (anahtar basina bir kez)
        return f.Sample(x, z);
    }

    /// <summary>
    /// Kirli alanlari sirayla ilerletir: en cok max alan, toplam en cok cells hucre. Donen: is yapilan alan sayisi.
    /// </summary>
    public int Tick(int? max = null, int? cells = null)
    {
        int fieldsMax = max ?? Grid.Def.MaxRecomputesPerTick, budget = cells ?? Grid.Def.MaxCellsPerTick, done = 0;
        var keys = _fields.Keys.ToList();
        for (int k = 0; k < keys.Count && done < fieldsMax && budget > 0; k++)
        {
            var f = _fields[keys[(_rr + k) % keys.Count]];
            if (!f.Dirty) continue;
            budget -= f.Step(budget);
            done++;
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
