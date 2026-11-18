namespace Kabarma.Sim;

/// <summary>
/// Ortak oyunda cogaltilan bir durum parcasi (TEKNIK 5.3 "Sim durumu: degisince fark, 10 sn'de bir ozet"; 5.4 sonradan
/// katilma). Host <see cref="Write"/> ile yazar; istemci <see cref="Read"/> ile kendi durumunu buna uydurur (uzlastirma:
/// eksik olani kurar, fazlayi kaldirir; ayni girdiyle iki kez okumak ayni sonucu verir). Istemci de ayni
/// <see cref="Write"/> ile kendi durumunu yazar; iki tarafin ozeti (<see cref="NetHash"/>) esitse durum aynidir.
/// Yazim kanoniktir: sira, yuva duzeni gibi oyuna etkisiz farklar yaziya girmez (sirala).
/// Yeni sistem baglamak: CLAUDE.md "M2 akislari" altindaki ortak oyun rehberi.
/// </summary>
public interface INetSection
{
    /// <summary>Kisa ve kalici kimlik ("bag", "pieces", "rocks"...).</summary>
    string Id { get; }
    /// <summary>true: her es kendi icerigini alir (canta). false: herkese ayni.</summary>
    bool PerPeer { get; }
    /// <param name="peer">PerPeer ise hangi esin icerigi (host 0; istemcide kendi kimligi).</param>
    void Write(NetWriter w, int peer);
    void Read(NetReader r);
}

/// <summary>
/// Bolum kumesi: kayit, degisim denetimi (ozet), ozet listesi. Host her bolum ve es icin son gonderilen ozeti tutar;
/// <see cref="Changed"/> yalniz degisen bolumu verir.
/// </summary>
public sealed class NetSectionSet
{
    readonly List<INetSection> _list = new();
    readonly Dictionary<(string id, int peer), ulong> _sent = new();
    readonly NetWriter _w = new(1024);

    public IReadOnlyList<INetSection> All => _list;

    public void Register(INetSection s)
    {
        if (_list.Any(x => x.Id == s.Id)) throw new InvalidOperationException($"net section '{s.Id}' iki kez");
        _list.Add(s);
    }

    public INetSection? Get(string id) => _list.FirstOrDefault(x => x.Id == id);

    /// <summary>Bolumu yazar; (yuk, ozet).</summary>
    public (byte[] payload, ulong hash) Build(INetSection s, int peer)
    {
        _w.Reset();
        s.Write(_w, peer);
        return (_w.ToArray(), NetHash.Of(_w.Span));
    }

    public ulong Hash(INetSection s, int peer)
    {
        _w.Reset();
        s.Write(_w, peer);
        return NetHash.Of(_w.Span);
    }

    /// <summary>Bu ese son gonderilenden farkliysa yukunu verir ve gonderilmis sayar.</summary>
    public bool Changed(INetSection s, int peer, out byte[] payload, out ulong hash)
    {
        (payload, hash) = Build(s, peer);
        var key = (s.Id, peer); // herkese ayni bolum de es basina izlenir: her es bir kez almali
        if (_sent.TryGetValue(key, out var h) && h == hash) return false;
        _sent[key] = hash;
        return true;
    }

    /// <summary>Esin bolumlerini "gonderilmedi" sayar (katilma, yeniden esleme).</summary>
    public void Forget(int peer, string? id = null)
    {
        foreach (var k in _sent.Keys.Where(k => k.peer == peer && (id == null || k.id == id)).ToList()) _sent.Remove(k);
    }

    /// <summary>Bir esin butun bolum ozetleri (Digest).</summary>
    public List<(string id, ulong hash)> Digest(int peer) => _list.Select(s => (s.Id, Hash(s, peer))).ToList();
}

/// <summary>Canta (Inventory) bolumu yazimi ve uzlastirmasi. Yuva sirasi yaziya girmez: (kimlik, ★) basina toplam.</summary>
public static class NetInventory
{
    public static SortedDictionary<(string id, int stars), int> Totals(Inventory inv)
    {
        var d = new SortedDictionary<(string, int), int>(Comparer<(string id, int stars)>.Create((a, b) =>
        {
            int c = string.CompareOrdinal(a.id, b.id);
            return c != 0 ? c : a.stars.CompareTo(b.stars);
        }));
        foreach (var (id, n, stars) in inv.Stacks) d[(id, stars)] = d.GetValueOrDefault((id, stars)) + n;
        return d;
    }

    public static void Write(NetWriter w, Inventory inv)
    {
        var t = Totals(inv);
        w.VarU((uint)t.Count);
        foreach (var ((id, stars), n) in t) { w.Str(id); w.U8((byte)stars); w.VarU((uint)n); }
    }

    public static Dictionary<(string id, int stars), int> ReadTotals(NetReader r)
    {
        int c = (int)r.VarU();
        var d = new Dictionary<(string, int), int>(c);
        for (int i = 0; i < c; i++) { var id = r.Str(); int st = r.U8(); d[(id, st)] = (int)r.VarU(); }
        return d;
    }

    /// <summary>Cantayi hedef toplamlara uydurur: yalniz farki ekler ya da dusurur (kemer ve olaylar gereksiz calkalanmaz).</summary>
    public static void Apply(Inventory inv, IReadOnlyDictionary<(string id, int stars), int> target)
    {
        var cur = Totals(inv);
        foreach (var ((id, stars), n) in cur)
        {
            int want = target.TryGetValue((id, stars), out var t) ? t : 0;
            if (n > want) inv.Remove(id, n - want, stars);
        }
        foreach (var ((id, stars), want) in target)
        {
            int have = inv.Count(id, stars);
            if (want > have) inv.Add(id, want - have, stars);
        }
    }

    /// <summary>Hazir bolum: tek bir cantayi (ya da es basina bir cantayi) cogaltir.</summary>
    public sealed class Section : INetSection
    {
        readonly Func<int, Inventory?> _of;
        readonly Func<Inventory?> _local;
        public string Id { get; }
        public bool PerPeer => true;

        /// <param name="of">Host'ta: es kimligi → o esin cantasi.</param>
        /// <param name="local">Istemcide: kendi cantasi (Read buraya uygular).</param>
        public Section(string id, Func<int, Inventory?> of, Func<Inventory?> local)
        {
            Id = id; _of = of; _local = local;
        }

        public void Write(NetWriter w, int peer)
        {
            var inv = _of(peer);
            if (inv == null) { w.VarU(0); return; }
            NetInventory.Write(w, inv);
        }

        public void Read(NetReader r)
        {
            var t = ReadTotals(r);
            var inv = _local();
            if (inv != null) Apply(inv, t);
        }
    }
}
