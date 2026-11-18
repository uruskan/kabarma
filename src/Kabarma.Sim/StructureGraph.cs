using System.Text.Json.Serialization;

namespace Kabarma.Sim;

/// <summary>
/// Bir yapi parcasi (GDD §7). Duz ve serilestirilebilir (TEKNIK §9): Godot dugumu tutmaz, komsulara kimlikle baglanir.
/// Span ve Load turetilmis degerdir; kayittan sonra StructureGraph.Bind / RebuildAll yeniden hesaplar.
/// </summary>
public sealed class StructurePiece
{
    public const int Unsupported = int.MaxValue;

    public int Id { get; set; }
    /// <summary>Sekil kimligi (kare_temel, duvar...).</summary>
    public string Shape { get; set; } = "";
    /// <summary>build.json malzeme kimligi.</summary>
    public string Material { get; set; } = "wood";
    /// <summary>Sekil birimi (ICERIK §10.1 Odun adedi): bedel ve agirlik bundan.</summary>
    public int Units { get; set; } = 1;
    public float Hp { get; set; }
    /// <summary>Dunya konumu (parcanin kokeni), metre.</summary>
    public float[] Pos { get; set; } = new float[3];
    /// <summary>Y ekseni etrafinda donus, radyan (Godot ekseni).</summary>
    public float Yaw { get; set; }
    /// <summary>Parca merkezinin dunya yuksekligi: dusey destegin yonu bundan anlasilir.</summary>
    public float CenterY { get; set; }
    /// <summary>Araziye kendi basina oturuyor (temel, cit, kule tabani).</summary>
    public bool Grounded { get; set; }
    /// <summary>Yatay parca (zemin, cati, kiris): aciklik +1.</summary>
    public bool Horizontal { get; set; }
    /// <summary>Plan modu hayaleti: dengeye katilmaz, malzemesi gelince kurulur.</summary>
    public bool Planned { get; set; }
    /// <summary>Plan parcasina konmus malzeme.</summary>
    public Dictionary<string, int> Delivered { get; set; } = new();
    /// <summary>Bu parcayi tasiyabilen komsular (yuva kurali, GDD §7).</summary>
    public List<int> SupportedBy { get; set; } = new();
    /// <summary>Bu parcanin tasiyabildigi komsular.</summary>
    public List<int> Supports { get; set; } = new();

    /// <summary>Zemine en kisa destek yolunun acikligi; Unsupported = desteksiz.</summary>
    [JsonIgnore] public int Span { get; internal set; } = Unsupported;
    /// <summary>Tasidigi yuk (kendi agirligi dahil).</summary>
    [JsonIgnore] public float Load { get; internal set; }
    [JsonIgnore] public bool Stable => Span != Unsupported;
}

/// <summary>Hayalet ya da yukseltme icin denge onizlemesi.</summary>
public sealed class StabilityReport
{
    public bool Valid;
    public string Reason = "";
    /// <summary>Malzeme siniri uygulanmadan en kisa aciklik (desteksizse Unsupported).</summary>
    public int Span = StructurePiece.Unsupported;
    public int MaxSpan;
    /// <summary>Yukun en agir bastigi parca: yuk ve sinir.</summary>
    public float Load, Capacity;
    public int WorstId;
    /// <summary>0 rahat, 1 sinirda: aciklik ve yuk oranlarinin buyugu (hayalet rengi).</summary>
    public float Strain;
}

/// <summary>
/// Tasima dengesi grafi (GDD §7). Kenarlar yuva kuralindan gelir (Godot katmani verir): destekleyen -> desteklenen.
/// Aciklik: araziye oturan 0; yatay parca ve asagidan beslenmeyen dusey parca +1, ustune dikilen dusey parca +0;
/// parcanin malzeme siniri asilirsa desteksiz. Yuk: agirlik + desteklediklerinin payi; pay, en kisa yoldan tasiyan
/// parcalara (ebeveynlere) esit bolunur. Ekleme ve sokum artimli: aciklik yalniz etkilenen parcalarda yeniden kurulur,
/// yuk yalniz degisen bagli bilesende cozulur. Desteksiz ya da asiri yuklu parca coker (Settle), zincir surer.
/// </summary>
public sealed class StructureGraph
{
    public const float LiftEps = 0.25f;

    public Dictionary<int, StructurePiece> Pieces { get; set; } = new();
    public int NextId { get; set; } = 1;

    BuildFile _rules = new();
    static readonly BuildMaterialDef Fallback = new() { Id = "?", Hp = 400, MaxSpan = 3, WeightPerUnit = 1, Capacity = 30 };

    /// <summary>Veriyi baglar ve turetilmis degerleri bastan hesaplar (yeni oyun ya da kayittan sonra).</summary>
    public void Bind(BuildFile rules)
    {
        _rules = rules;
        RebuildAll();
    }

    [JsonIgnore] public BuildFile Rules => _rules;
    public StructurePiece? Get(int id) => Pieces.TryGetValue(id, out var p) ? p : null;
    public BuildMaterialDef Mat(StructurePiece p) => _rules.Material(p.Material) ?? Fallback;
    public int MaxSpan(StructurePiece p) => Mat(p).MaxSpan;
    public float Weight(StructurePiece p) => p.Units * Mat(p).WeightPerUnit;
    public float Capacity(StructurePiece p) => p.Grounded ? float.PositiveInfinity : Mat(p).Capacity;
    public float MaxHp(StructurePiece p) => Mat(p).Hp;
    public bool Overloaded(StructurePiece p) => Active(p) && p.Stable && p.Load > Capacity(p) + 1e-3f;

    static bool Active(StructurePiece p) => !p.Planned;

    /// <summary>s'den c'ye destek maliyeti: yatay +1, ustune dikilen dusey 0, yandan / ustten asilan dusey +1.</summary>
    public static int Cost(StructurePiece s, StructurePiece c) => c.Horizontal ? 1 : (s.CenterY < c.CenterY - LiftEps ? 0 : 1);

    // ------------------------------------------------------------------ ekleme, sokum

    /// <summary>
    /// Parcayi ekler. SupportedBy / Supports var olan kimlikleri adlandirir; ters kenarlar buradan kurulur. Plan degilse
    /// aciklik ve yuk artimli guncellenir (cokme denetimi yok: cagiran once Evaluate ile sinar). Kimligi dondurur.
    /// </summary>
    public int Add(StructurePiece p)
    {
        p.Id = NextId++;
        p.SupportedBy = p.SupportedBy.Where(id => id != p.Id && Pieces.ContainsKey(id)).Distinct().ToList();
        p.Supports = p.Supports.Where(id => id != p.Id && Pieces.ContainsKey(id)).Distinct().ToList();
        Pieces[p.Id] = p;
        foreach (var s in p.SupportedBy) AddOnce(Pieces[s].Supports, p.Id);
        foreach (var c in p.Supports) AddOnce(Pieces[c].SupportedBy, p.Id);
        p.Span = StructurePiece.Unsupported;
        p.Load = 0;
        if (Active(p)) Activate(p);
        return p.Id;
    }

    static void AddOnce(List<int> l, int id) { if (!l.Contains(id)) l.Add(id); }

    void Activate(StructurePiece p)
    {
        p.Span = SpanFromSupporters(p);
        if (p.Stable) Relax(new[] { p });
        RecomputeLoads(new[] { p.Id });
    }

    /// <summary>Parcayi kaldirir. collapse: desteksiz ya da asiri yuklu kalanlar coker ve dondurulur (sirayla).</summary>
    public List<StructurePiece> Remove(int id, bool collapse = true)
    {
        if (!Pieces.TryGetValue(id, out var r)) return new List<StructurePiece>();
        var touched = Detach(r);
        if (collapse) return Settle(touched);
        RecomputeLoads(touched);
        return new List<StructurePiece>();
    }

    HashSet<int> Detach(StructurePiece r)
    {
        var touched = r.SupportedBy.Concat(r.Supports).ToHashSet();
        var aff = Active(r) && r.Stable ? Dependents(r) : new HashSet<int>();
        foreach (var s in r.SupportedBy) Pieces[s].Supports.Remove(r.Id);
        foreach (var c in r.Supports) Pieces[c].SupportedBy.Remove(r.Id);
        Pieces.Remove(r.Id);
        r.Span = StructurePiece.Unsupported;
        Reseed(aff);
        touched.UnionWith(aff);
        touched.RemoveWhere(x => !Pieces.ContainsKey(x));
        return touched;
    }

    /// <summary>Plan parcasini kurulu yapar (malzemesi tamam). Dengeyi bozarsa geri alir ve nedeni dondurur.</summary>
    public StabilityReport Complete(int id)
    {
        var p = Pieces[id];
        if (!p.Planned) return new StabilityReport { Valid = true, Span = p.Span, MaxSpan = MaxSpan(p) };
        var rep = Evaluate(Clone(p));
        if (!rep.Valid) return rep;
        p.Planned = false;
        Activate(p);
        return rep;
    }

    static StructurePiece Clone(StructurePiece p) => new()
    {
        Shape = p.Shape, Material = p.Material, Units = p.Units, Hp = p.Hp, Pos = (float[])p.Pos.Clone(), Yaw = p.Yaw,
        CenterY = p.CenterY, Grounded = p.Grounded, Horizontal = p.Horizontal,
        SupportedBy = p.SupportedBy.ToList(), Supports = p.Supports.ToList(),
    };

    /// <summary>
    /// Plan bayragini yetkili kaynaktan (ortak oyunda host) uygular: denetim yok. true: dengeden cikar (altindakiler
    /// yeniden hesaplanir, cokme yok); false: dengeye girer.
    /// </summary>
    public void SetPlanned(int id, bool planned)
    {
        var p = Pieces[id];
        if (p.Planned == planned) return;
        if (!planned) { p.Planned = false; Activate(p); return; }
        var touched = p.SupportedBy.Concat(p.Supports).ToHashSet();
        var aff = p.Stable ? Dependents(p) : new HashSet<int>();
        p.Planned = true;
        p.Span = StructurePiece.Unsupported;
        p.Load = 0;
        Reseed(aff);
        touched.UnionWith(aff);
        RecomputeLoads(touched);
    }

    /// <summary>Malzemeyi degistirir (yukseltme). Yuk sinirini asacaksa (ya da commit=false ise) geri alir; force: yetkili kaynaktan, denetimsiz.</summary>
    public StabilityReport TrySetMaterial(int id, string material, bool commit = true, bool force = false)
    {
        var p = Pieces[id];
        string old = p.Material;
        p.Material = material;
        var rep = new StabilityReport { Span = p.Span, MaxSpan = MaxSpan(p), Valid = true };
        if (Active(p))
        {
            var comp = RecomputeLoads(new[] { id });
            Worst(comp.Select(x => Pieces[x]), rep);
            if (!force && rep.Load > rep.Capacity + 1e-3f) { rep.Valid = false; rep.Reason = $"aşırı yük {rep.Load:0}/{rep.Capacity:0}"; }
        }
        if (!commit || !rep.Valid)
        {
            p.Material = old;
            if (Active(p)) RecomputeLoads(new[] { id });
        }
        return rep;
    }

    // ------------------------------------------------------------------ onizleme

    /// <summary>
    /// Aday parcanin dengesi: gecici eklenir, olculur, geri alinir (graf degismez). assumePlanned: plan parcalari kurulu
    /// sayilir (plan modu yerlesimi; yalniz aciklik denetlenir).
    /// </summary>
    public StabilityReport Evaluate(StructurePiece cand, bool assumePlanned = false)
    {
        if (assumePlanned) return EvaluatePlan(cand);
        int next = NextId;
        cand.Planned = false;
        int id = Add(cand);
        var rep = new StabilityReport { MaxSpan = MaxSpan(cand), Span = RawSpan(cand, null) };
        if (cand.Stable)
        {
            var anc = Ancestors(cand);
            Worst(anc, rep);
            float spanRatio = rep.MaxSpan > 0 ? (float)cand.Span / rep.MaxSpan : 0f;
            float loadRatio = float.IsInfinity(rep.Capacity) || rep.Capacity <= 0 ? 0f : rep.Load / rep.Capacity;
            rep.Strain = Math.Clamp(Math.Max(spanRatio, loadRatio), 0f, 1f);
            if (rep.Load > rep.Capacity + 1e-3f) rep.Reason = $"aşırı yük {rep.Load:0}/{rep.Capacity:0}";
            else rep.Valid = true;
        }
        else rep.Reason = rep.Span == StructurePiece.Unsupported ? "destek yok" : $"açıklık {rep.Span} > {rep.MaxSpan}";
        Remove(id, collapse: false);
        NextId = next;
        cand.Id = 0;
        return rep;
    }

    StabilityReport EvaluatePlan(StructurePiece cand)
    {
        var comp = Component(cand.SupportedBy.Concat(cand.Supports), includePlanned: true);
        var nodes = comp.Select(x => Pieces[x]).Append(cand).ToList();
        cand.Id = -1;
        var spans = SolveSpans(nodes, _ => true, raw: cand);
        int raw = spans.Raw;
        var rep = new StabilityReport { MaxSpan = MaxSpan(cand), Span = raw };
        bool ok = raw <= rep.MaxSpan;
        rep.Valid = ok;
        rep.Strain = ok && rep.MaxSpan > 0 ? Math.Clamp((float)raw / rep.MaxSpan, 0f, 1f) : 0f;
        if (!ok) rep.Reason = raw == StructurePiece.Unsupported ? "destek yok" : $"açıklık {raw} > {rep.MaxSpan}";
        cand.Id = 0;
        return rep;
    }

    void Worst(IEnumerable<StructurePiece> nodes, StabilityReport rep)
    {
        float best = -1;
        foreach (var p in nodes)
        {
            if (!Active(p) || !p.Stable || p.Grounded) continue;
            float cap = Capacity(p);
            float r = cap <= 0 ? 1f : p.Load / cap;
            if (r > best) { best = r; rep.Load = p.Load; rep.Capacity = cap; rep.WorstId = p.Id; }
        }
        if (best < 0) { rep.Load = 0; rep.Capacity = float.PositiveInfinity; }
    }

    /// <summary>Parcanin yukunu tasiyan zincir (kendisi dahil, ebeveynler boyunca).</summary>
    public List<StructurePiece> Ancestors(StructurePiece p)
    {
        var seen = new HashSet<int> { p.Id };
        var res = new List<StructurePiece> { p };
        var q = new Queue<StructurePiece>();
        q.Enqueue(p);
        while (q.Count > 0)
            foreach (var s in Parents(q.Dequeue()))
                if (seen.Add(s.Id)) { res.Add(s); q.Enqueue(s); }
        return res;
    }

    /// <summary>En kisa destek yolunda c'yi tasiyan parcalar (yuk bunlara esit bolunur).</summary>
    public List<StructurePiece> Parents(StructurePiece c)
    {
        var res = new List<StructurePiece>();
        if (c.Grounded || !c.Stable) return res;
        foreach (var sid in c.SupportedBy)
        {
            var s = Pieces[sid];
            if (Active(s) && s.Stable && s.Span + Cost(s, c) == c.Span) res.Add(s);
        }
        return res;
    }

    // ------------------------------------------------------------------ aciklik (artimli)

    int RawSpan(StructurePiece p, HashSet<int>? exclude)
    {
        if (p.Grounded) return 0;
        int best = StructurePiece.Unsupported;
        foreach (var sid in p.SupportedBy)
        {
            var s = Pieces[sid];
            if (!Active(s) || !s.Stable || (exclude != null && exclude.Contains(sid))) continue;
            int ns = s.Span + Cost(s, p);
            if (ns < best) best = ns;
        }
        return best;
    }

    int SpanFromSupporters(StructurePiece p)
    {
        int s = RawSpan(p, null);
        return s <= MaxSpan(p) ? s : StructurePiece.Unsupported;
    }

    /// <summary>Tohumlardan acikliklari asagi dogru yayar (Dijkstra; kenar 0 ya da 1, dugum siniri malzemeden).</summary>
    void Relax(IEnumerable<StructurePiece> seeds)
    {
        var pq = new PriorityQueue<StructurePiece, int>();
        foreach (var s in seeds) if (s.Stable) pq.Enqueue(s, s.Span);
        while (pq.TryDequeue(out var p, out int d))
        {
            if (d != p.Span) continue;
            foreach (var cid in p.Supports)
            {
                var c = Pieces[cid];
                if (!Active(c) || c.Grounded) continue;
                int ns = d + Cost(p, c);
                if (ns > MaxSpan(c) || ns >= c.Span) continue;
                c.Span = ns;
                pq.Enqueue(c, ns);
            }
        }
    }

    /// <summary>r'nin en kisa yol agacinda altinda kalanlar (r cikinca acikliklari buyuyebilecek olanlar).</summary>
    HashSet<int> Dependents(StructurePiece r)
    {
        var set = new HashSet<int>();
        var q = new Queue<StructurePiece>();
        q.Enqueue(r);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var cid in p.Supports)
            {
                var c = Pieces[cid];
                if (!Active(c) || c.Grounded || !c.Stable || set.Contains(cid)) continue;
                if (p.Span + Cost(p, c) != c.Span) continue;
                set.Add(cid);
                q.Enqueue(c);
            }
        }
        return set;
    }

    void Reseed(HashSet<int> affected)
    {
        foreach (var id in affected) Pieces[id].Span = StructurePiece.Unsupported;
        var seeds = new List<StructurePiece>();
        foreach (var id in affected)
        {
            var p = Pieces[id];
            int s = SpanFromSupporters(p);
            if (s == StructurePiece.Unsupported) continue;
            p.Span = s;
            seeds.Add(p);
        }
        Relax(seeds);
    }

    // ------------------------------------------------------------------ yuk

    /// <summary>Tohumlarin bagli bilesenlerinde (kurulu parcalar) yuku bastan cozer; bilesenin kimliklerini dondurur.</summary>
    HashSet<int> RecomputeLoads(IEnumerable<int> seeds)
    {
        var comp = Component(seeds, includePlanned: false);
        SolveLoads(comp.Select(x => Pieces[x]).ToList());
        return comp;
    }

    HashSet<int> Component(IEnumerable<int> seeds, bool includePlanned)
    {
        var comp = new HashSet<int>();
        var q = new Queue<int>();
        foreach (var s in seeds)
            if (Pieces.TryGetValue(s, out var p) && (includePlanned || Active(p)) && comp.Add(s)) q.Enqueue(s);
        while (q.Count > 0)
        {
            var p = Pieces[q.Dequeue()];
            foreach (var n in p.SupportedBy.Concat(p.Supports))
                if ((includePlanned || Active(Pieces[n])) && comp.Add(n)) q.Enqueue(n);
        }
        return comp;
    }

    void SolveLoads(List<StructurePiece> nodes)
    {
        foreach (var p in nodes) p.Load = Weight(p);
        // Cocuk once: aciklik buyukten kucuge, esitse yuksekten alcaga (0 maliyetli kenar hep yukari gider).
        foreach (var c in nodes.Where(p => p.Stable).OrderByDescending(p => p.Span).ThenByDescending(p => p.CenterY).ThenByDescending(p => p.Id))
        {
            var parents = Parents(c);
            if (parents.Count == 0) continue;
            float share = c.Load / parents.Count;
            foreach (var s in parents) s.Load += share;
        }
    }

    // ------------------------------------------------------------------ cokme

    /// <summary>Desteksiz ya da asiri yuklu parcalari cokertir; yeni bozulan kalmayana dek surer. Cokenleri sirayla dondurur.</summary>
    List<StructurePiece> Settle(HashSet<int> touched)
    {
        var fallen = new List<StructurePiece>();
        for (int guard = 0; guard < 10000; guard++)
        {
            var comp = RecomputeLoads(touched);
            var bad = comp.Select(x => Pieces[x]).Where(p => !p.Stable || Overloaded(p)).OrderBy(p => p.Id).ToList();
            if (bad.Count == 0) break;
            touched = new HashSet<int>();
            foreach (var b in bad)
            {
                if (!Pieces.ContainsKey(b.Id)) continue;
                fallen.Add(b);
                touched.UnionWith(Detach(b));
            }
            touched.RemoveWhere(x => !Pieces.ContainsKey(x));
        }
        return fallen;
    }

    /// <summary>Butun parcalarin acikligi ve yuku bastan (kayittan sonra; testlerde artimliyla karsilastirma).</summary>
    public void RebuildAll()
    {
        var all = Pieces.Values.ToList();
        var res = SolveSpans(all, Active, null);
        foreach (var p in all) p.Span = res.Spans.TryGetValue(p.Id, out int s) ? s : StructurePiece.Unsupported;
        SolveLoads(all.Where(Active).ToList());
        foreach (var p in all.Where(p => !Active(p))) p.Load = 0;
    }

    /// <summary>Tam cozum: verilen dugumler arasinda, active olanlar uzerinden aciklik. raw: bu dugumun sinirsiz en kisa acikligi.</summary>
    public (Dictionary<int, int> Spans, int Raw) SolveSpans(List<StructurePiece> nodes, Func<StructurePiece, bool> active, StructurePiece? raw)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var outs = nodes.ToDictionary(n => n.Id, _ => new HashSet<int>());
        foreach (var n in nodes)
        {
            foreach (var s in n.SupportedBy) if (byId.ContainsKey(s)) outs[s].Add(n.Id);
            foreach (var c in n.Supports) if (byId.ContainsKey(c)) outs[n.Id].Add(c);
        }
        var dist = new Dictionary<int, int>();
        var rawBest = StructurePiece.Unsupported;
        var pq = new PriorityQueue<int, int>();
        foreach (var n in nodes) if (active(n) && n.Grounded) { dist[n.Id] = 0; pq.Enqueue(n.Id, 0); }
        if (raw != null && raw.Grounded) rawBest = 0;
        while (pq.TryDequeue(out int id, out int d))
        {
            if (dist[id] != d) continue;
            var p = byId[id];
            foreach (var cid in outs[id])
            {
                var c = byId[cid];
                if (!active(c) || c.Grounded) continue;
                int ns = d + Cost(p, c);
                if (c == raw && ns < rawBest) rawBest = ns;
                if (ns > MaxSpan(c)) continue;
                if (dist.TryGetValue(cid, out int old) && old <= ns) continue;
                dist[cid] = ns;
                pq.Enqueue(cid, ns);
            }
        }
        return (dist, rawBest);
    }
}
