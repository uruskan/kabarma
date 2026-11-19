namespace Kabarma.Sim;

/// <summary>Hatta giden bir yuk: A'dan B'ye, T yolun 0..1'i.</summary>
public sealed class RopewayCar
{
    public string Item { get; set; } = "";
    public int Qty { get; set; }
    public float T { get; set; }
}

/// <summary>Iki Teleferik Istasyonu arasindaki tek yonlu hat (A -> B); ayni cifti ters yonde ikinci hat tasir.</summary>
public sealed class RopewayLine
{
    public int A { get; set; }
    public int B { get; set; }
    /// <summary>Bos: her esya.</summary>
    public string Item { get; set; } = "";
    public float Acc { get; set; }
    public List<RopewayCar> Cars { get; set; } = new();
}

/// <summary>
/// Teleferik (GDD 11.3, ICERIK §12): karakollar arasi yuk hatti. Hat iki Teleferik Istasyonu arasindadir (en fazla Range
/// m) ve araya iki direk arasi en cok Span m kalacak kadar Teleferik Diregi gerekir. Her istasyonun yanindaki depodan
/// (Sandik) yuk alinir, hedefin deposuna birakilir; istasyon beslenmis olmali. Duz durum.
/// </summary>
public sealed class RopewayState
{
    public List<RopewayLine> Lines { get; set; } = new();

    static float Dist(PowerNode a, PowerNode b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>Hatta gereken direk adedi (ara noktalar).</summary>
    public static int PostsNeeded(float length, float span) => span <= 0 ? 0 : Math.Max(0, (int)MathF.Ceiling(length / span) - 1);

    /// <summary>Hatta durmus direkler: dogruya 6 m'den yakin olan Teleferik Direkleri.</summary>
    public static int PostsOn(PowerNet net, PowerFile f, PowerNode a, PowerNode b)
    {
        float dx = b.X - a.X, dz = b.Z - a.Z, len2 = Math.Max(1e-4f, dx * dx + dz * dz);
        int c = 0;
        foreach (var p in net.Nodes)
        {
            if (f.Def(p.Def) is not { Kind: "ropeway_post" }) continue;
            float t = ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len2;
            if (t <= 0.02f || t >= 0.98f) continue;
            float px = a.X + dx * t - p.X, pz = a.Z + dz * t - p.Z;
            if (px * px + pz * pz <= 36f) c++;
        }
        return c;
    }

    /// <summary>Hat kurar. Donen null: kuruldu; degilse neden (menzil, direk eksik, ayni hat).</summary>
    public string? Connect(PowerNet net, PowerFile f, int a, int b, string item = "")
    {
        var na = net.Get(a); var nb = net.Get(b);
        var da = na == null ? null : f.Def(na.Def); var db = nb == null ? null : f.Def(nb.Def);
        if (na == null || nb == null || da?.Kind != "ropeway_station" || db?.Kind != "ropeway_station" || a == b) return "İki Teleferik İstasyonu gerekir";
        float len = Dist(na, nb);
        if (len > da.Range) return $"Hat en fazla {da.Range:0} m";
        var post = f.Devices.FirstOrDefault(d => d.Kind == "ropeway_post");
        int need = PostsNeeded(len, post?.Span ?? 60f), have = PostsOn(net, f, na, nb);
        if (have < need) return $"Direk eksik ({have}/{need}): {post?.Span ?? 60f:0} m'de bir Teleferik Direği";
        if (Lines.Any(l => l.A == a && l.B == b)) return "Zaten bağlı";
        Lines.Add(new RopewayLine { A = a, B = b, Item = item });
        return null;
    }

    public void Tick(PowerNet net, PowerFile f, ITransportWorld world, float dt)
    {
        Lines.RemoveAll(l => net.Get(l.A) == null || net.Get(l.B) == null);
        foreach (var l in Lines)
        {
            var a = net.Get(l.A)!; var b = net.Get(l.B)!;
            var d = f.Def(a.Def)!;
            float len = Math.Max(1f, Dist(a, b)), speed = Math.Max(0.5f, d.Speed);
            // Yukler ilerler; varinca hedef depoya iner (sigmayan hatta bekler).
            foreach (var c in l.Cars.ToList())
            {
                c.T = MathF.Min(1f, c.T + speed * dt / len);
                if (c.T < 1f) continue;
                var dst = world.StoreAt(b.X, b.Z, 4f);
                int put = dst == null ? 0 : dst.Add(c.Item, c.Qty);
                c.Qty -= put;
                if (c.Qty <= 0) l.Cars.Remove(c);
            }
            bool live = a.Powered && a.On && b.Powered && b.On && (!a.SignalWired || a.Signal > 0);
            if (!live || l.Cars.Count >= 4) { l.Acc = 0; continue; }
            l.Acc += dt * Math.Max(0.01f, d.Rate);
            if (l.Acc < 1f) continue;
            var src = world.StoreAt(a.X, a.Z, 4f);
            if (src == null) continue;
            string item = !string.IsNullOrEmpty(l.Item) ? l.Item : !string.IsNullOrEmpty(a.Filter) ? a.Filter
                : src is Inventory inv ? inv.Contents.Select(x => x.id).FirstOrDefault() ?? "" : "";
            int q = item.Length == 0 ? 0 : Math.Min(d.Capacity, src.Count(item));
            if (q > 0 && src.Remove(item, q)) { l.Cars.Add(new RopewayCar { Item = item, Qty = q }); l.Acc = 0; }
        }
    }
}
