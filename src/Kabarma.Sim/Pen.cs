namespace Kabarma.Sim;

// Agil (GDD 9.1): cit, bahce kapisi, duvar ya da kapi parcalarindan kapali bir halka, en az 1 hucre.
// Kurallar motordan bagimsiz: Godot katmani yapi setinin parcalarini duvar dogrularina (PenWall) cevirir,
// halkayi, icerdeki hayvani ve hareketin bir duvara takilip takilmadigini buradan sorar. Sayilar pen.json'da.

/// <summary>Yatay duzlemde nokta (Godot X ve Z ekseni, metre).</summary>
public readonly record struct P2(float X, float Z)
{
    public static P2 operator +(P2 a, P2 b) => new(a.X + b.X, a.Z + b.Z);
    public static P2 operator -(P2 a, P2 b) => new(a.X - b.X, a.Z - b.Z);
    public static P2 operator *(P2 a, float k) => new(a.X * k, a.Z * k);
    public float Length => MathF.Sqrt(X * X + Z * Z);
    public float Dist(P2 o) => (this - o).Length;
    public static float Cross(P2 a, P2 b) => a.X * b.Z - a.Z * b.X;
    public static float Dot(P2 a, P2 b) => a.X * b.X + a.Z * b.Z;
}

/// <summary>
/// Agil duvari olabilecek bir yapi parcasinin taban dogrusu. Gecit (kapi) acikken gecilir; kapali kapi duvar gibidir.
/// Id cagiranin parca kimligidir (Godot'ta parca dugumunun sirasi).
/// </summary>
public sealed class PenWall
{
    public int Id { get; init; }
    public P2 A { get; init; }
    public P2 B { get; init; }
    /// <summary>Kapi: bahce kapisi, sur kapisi ya da kapi kasasi.</summary>
    public bool Gate { get; init; }
    /// <summary>Kapi acik (kasada kapi yoksa da acik sayilir).</summary>
    public bool Open { get; set; }
    public bool Blocks => !(Gate && Open);
    public P2 Mid => (A + B) * 0.5f;
}

/// <summary>Bulunmus bir agil: kapali halkanin cokgeni, alani ve halkayi kuran duvarlar.</summary>
public sealed class PenShape
{
    public required IReadOnlyList<P2> Polygon { get; init; }
    public required IReadOnlyList<int> Walls { get; init; }
    public float Area { get; init; }
    /// <summary>Halkada en az bir kapi var (hayvan iceri kapidan girer).</summary>
    public bool HasGate { get; init; }

    /// <summary>Hucre sayisi: alan / hucre alani (kucuk yuvarlama payiyla).</summary>
    public int Cells(float cellArea) => cellArea <= 0 ? 0 : (int)MathF.Floor(Area / cellArea + 0.02f);

    /// <summary>Bu alana sigan hayvan sayisi (tur basina alan, GDD 9.1: Gidak 4 m²).</summary>
    public int Capacity(float areaPerAnimal) => areaPerAnimal <= 0 ? 0 : (int)MathF.Floor(Area / areaPerAnimal + 0.02f);

    public bool Contains(P2 p) => PenRules.InPolygon(Polygon, p);

    /// <summary>Cokgenin agirlik merkezi (gezinme ve dalga yonu icin).</summary>
    public P2 Centroid
    {
        get
        {
            float cx = 0, cz = 0, a = 0;
            for (int i = 0; i < Polygon.Count; i++)
            {
                var p = Polygon[i]; var q = Polygon[(i + 1) % Polygon.Count];
                float c = P2.Cross(p, q);
                a += c; cx += (p.X + q.X) * c; cz += (p.Z + q.Z) * c;
            }
            if (MathF.Abs(a) < 1e-5f) return new P2(Polygon.Average(p => p.X), Polygon.Average(p => p.Z));
            return new P2(cx / (3 * a), cz / (3 * a));
        }
    }
}

/// <summary>Bir hareketin takildigi duvar ve takildigi yer (0..1, hareket boyunca).</summary>
public readonly record struct WallHit(PenWall Wall, float T, P2 Point);

public static class PenRules
{
    /// <summary>
    /// Duvarlardan kapali halkalari (agillari) bulur. Uclar <paramref name="weld"/> icinde birlesir; bos uclu
    /// kollar (halkaya baglanmayan cit) budanir; kalan duzlemsel cizgenin ic yuzleri agildir. Alani
    /// <paramref name="minArea"/>'nin altindakiler sayilmaz (GDD 9.1: en az 1 hucre).
    /// </summary>
    public static List<PenShape> FindPens(IReadOnlyList<PenWall> walls, float minArea, float weld)
    {
        // 1) uclari birlestir
        var verts = new List<P2>();
        int VertexOf(P2 p)
        {
            for (int i = 0; i < verts.Count; i++) if (verts[i].Dist(p) <= weld) return i;
            verts.Add(p);
            return verts.Count - 1;
        }
        var edges = new Dictionary<(int, int), PenWall>();
        foreach (var w in walls)
        {
            int a = VertexOf(w.A), b = VertexOf(w.B);
            if (a == b) continue;
            var key = a < b ? (a, b) : (b, a);
            if (!edges.ContainsKey(key)) edges[key] = w; // ust kattaki ayni dogru: bir kez
        }
        // 2) bos uclu kollari buda
        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var (a, b) in edges.Keys)
        {
            (adj.TryGetValue(a, out var la) ? la : adj[a] = new()).Add(b);
            (adj.TryGetValue(b, out var lb) ? lb : adj[b] = new()).Add(a);
        }
        bool pruned = true;
        while (pruned)
        {
            pruned = false;
            foreach (var v in adj.Keys.ToList())
            {
                if (adj[v].Count >= 2) continue;
                foreach (var n in adj[v]) adj[n].Remove(v);
                adj.Remove(v);
                pruned = true;
            }
        }
        // 3) yuz gezintisi: her yarim kenarin solundaki yuz; saat yonu tersine (pozitif alanli) yuzler ictir
        var sorted = adj.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(n => MathF.Atan2(verts[n].Z - verts[kv.Key].Z, verts[n].X - verts[kv.Key].X)).ToList());
        var used = new HashSet<(int, int)>();
        var pens = new List<PenShape>();
        foreach (var (u0, ns) in sorted)
            foreach (var v0 in ns)
            {
                if (used.Contains((u0, v0))) continue;
                var cycle = new List<int>();
                int u = u0, v = v0, guard = 0;
                while (!used.Contains((u, v)) && guard++ < 4096)
                {
                    used.Add((u, v));
                    cycle.Add(u);
                    var around = sorted[v];
                    int i = around.IndexOf(u);
                    int w = around[(i - 1 + around.Count) % around.Count]; // v cevresinde saat yonunde bir sonraki
                    u = v; v = w;
                }
                if (cycle.Count < 3) continue;
                var poly = cycle.Select(i => verts[i]).ToList();
                float area = SignedArea(poly);
                if (area < minArea - 0.05f) continue;
                var wallIds = new List<int>();
                bool gate = false;
                for (int i = 0; i < cycle.Count; i++)
                {
                    int a = cycle[i], b = cycle[(i + 1) % cycle.Count];
                    var wall = edges[a < b ? (a, b) : (b, a)];
                    wallIds.Add(wall.Id);
                    gate |= wall.Gate;
                }
                pens.Add(new PenShape { Polygon = poly, Walls = wallIds, Area = area, HasGate = gate });
            }
        return pens;
    }

    /// <summary>Saat yonu tersine pozitif alan (X, Z duzleminde).</summary>
    public static float SignedArea(IReadOnlyList<P2> poly)
    {
        float s = 0;
        for (int i = 0; i < poly.Count; i++) s += P2.Cross(poly[i], poly[(i + 1) % poly.Count]);
        return s * 0.5f;
    }

    public static bool InPolygon(IReadOnlyList<P2> poly, P2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Z > p.Z) != (b.Z > p.Z) && p.X < (b.X - a.X) * (p.Z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// from → to hareketinin ilk kestigi gecilmez duvar (kapali kapi dahil, acik kapi haric). Yoksa null.
    /// Point, duvardan <paramref name="radius"/> kadar geride durulacak yerdir.
    /// </summary>
    public static WallHit? FirstHit(IEnumerable<PenWall> walls, P2 from, P2 to, float radius)
    {
        var d = to - from;
        float len = d.Length;
        if (len < 1e-5f) return null;
        WallHit? best = null;
        foreach (var w in walls)
        {
            if (!w.Blocks) continue;
            if (!Intersect(from, to, w.A, w.B, out float t)) continue;
            if (best != null && t >= best.Value.T) continue;
            float back = MathF.Max(0f, t - radius / len);
            best = new WallHit(w, t, from + d * back);
        }
        return best;
    }

    /// <summary>Iki dogru parcasi kesisiyor mu; t ilk parca boyunca (0..1).</summary>
    public static bool Intersect(P2 p, P2 p2, P2 q, P2 q2, out float t)
    {
        t = 0;
        var r = p2 - p; var s = q2 - q;
        float den = P2.Cross(r, s);
        if (MathF.Abs(den) < 1e-6f) return false;
        var qp = q - p;
        t = P2.Cross(qp, s) / den;
        float u = P2.Cross(qp, r) / den;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    /// <summary>Noktanin duvar parcasina en kisa uzakligi.</summary>
    public static float DistanceToWall(PenWall w, P2 p)
    {
        var ab = w.B - w.A;
        float l2 = P2.Dot(ab, ab);
        float t = l2 < 1e-6f ? 0f : Math.Clamp(P2.Dot(p - w.A, ab) / l2, 0f, 1f);
        return p.Dist(w.A + ab * t);
    }
}

/// <summary>Yapi parcasinin cani (URETIM 6 M1: hasarla yikilma yok, can %10'da durur; yikilma M3).</summary>
public static class StructureRules
{
    /// <summary>Hasar sonrasi can: tabanin (maxHp x floor) altina inmez. Zaten tabandaysa degismez.</summary>
    public static float Damage(float hp, float maxHp, float damage, float floorFraction)
    {
        float floor = maxHp * floorFraction;
        if (damage <= 0 || hp <= floor) return hp;
        return MathF.Max(floor, hp - damage);
    }
}
