namespace Kabarma.Sim;

/// <summary>
/// Host'ta uzak bir oyuncunun yetkili durumu (GDD 22.1: canta kisiseldir, kayit host'ta). Duz, serilestirilebilir
/// (TEKNIK 9): Godot tipi ya da dugum tutmaz. Host kendi oyuncusu icin bunu tutmaz; onun cantasi GameState.Backpack.
/// </summary>
public sealed class NetActor
{
    public byte Peer { get; }
    public string Name { get; set; } = "";
    /// <summary>Kisisel canta: istemcinin gordugu canta bunun kopyasidir (NetInventory bolumu).</summary>
    public Inventory Bag { get; }
    /// <summary>Son kabul edilen kare ve zamani (host saati, sn).</summary>
    public PlayerFrame Last;
    public double LastTime = double.NaN;
    public bool HasFrame => !double.IsNaN(LastTime);
    /// <summary>Eldeki esya (kemer), baslik.</summary>
    public string Held { get; set; } = "";
    public bool Ready { get; set; }
    /// <summary>Sok tokadi: bu oyuncuyu kaldirmak icin biriken basili tutma (GDD 3.6).</summary>
    public DownedState Downed { get; } = new();
    readonly Dictionary<NetReq, double> _next = new();
    /// <summary>Reddedilen istek ve konum duzeltmesi sayilari (sinama ve gunluk).</summary>
    public int Rejected, Corrections;

    public NetActor(byte peer, GameData data)
    {
        Peer = peer;
        Bag = new Inventory(data.Tool.BackpackSlots, data.Tool.StackOf);
        foreach (var (id, n) in data.Tool.StartItems) Bag.Add(id, n);
    }

    public (float x, float y, float z) Pos => (Last.X, Last.Y, Last.Z);

    /// <summary>Bekleme suresi denetimi: izinliyse bir sonrakini <paramref name="interval"/> sonraya kurar.</summary>
    public bool Cooldown(NetReq req, double now, float interval)
    {
        if (interval <= 0) return true;
        if (_next.TryGetValue(req, out var t) && now < t - 1e-6) return false;
        _next[req] = now + interval;
        return true;
    }
}

/// <summary>Host'un istemci isteklerini ve konumlarini denetleme kurallari (TEKNIK 5.5).</summary>
public static class NetRules
{
    public static float Dist(float ax, float ay, float az, float bx, float by, float bz)
    {
        float dx = ax - bx, dy = ay - by, dz = az - bz;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static float FlatDist(float ax, float az, float bx, float bz)
    {
        float dx = ax - bx, dz = az - bz;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>
    /// Yeni kare kabul edilir mi: yatay yol, en hizli kosunun <see cref="NetFile.SpeedSlack"/> kati arti 1 m pay
    /// (ag titremesi, itilme). Dusme (dikey) serbest. Ilk kare ve baygin oyuncu (ragdoll savrulur) her zaman kabul edilir.
    /// </summary>
    public static bool MovementOk(in PlayerFrame prev, double prevTime, in PlayerFrame next, double now, float maxSpeed, NetFile net)
    {
        if (double.IsNaN(prevTime)) return true;
        if (next.Has(NetFrameFlags.Down) || prev.Has(NetFrameFlags.Down)) return true;
        float d = FlatDist(prev.X, prev.Z, next.X, next.Z);
        float dt = (float)Math.Max(0.0, now - prevTime);
        return d <= maxSpeed * net.SpeedSlack * dt + 1f;
    }

    /// <summary>Gorunus: uzak oyuncu ya da yaratik bu kadar sicrarsa ara degerleme yerine aninda yerine konur.</summary>
    public static bool Snap(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, NetFile net) =>
        Dist(fromX, fromY, fromZ, toX, toY, toZ) > net.TeleportLimit;

    /// <summary>Istek menzil denetimi: oyuncunun son kabul edilen konumundan hedefe.</summary>
    public static bool InRange(NetActor a, float x, float y, float z, float range) =>
        a.HasFrame && Dist(a.Last.X, a.Last.Y, a.Last.Z, x, y, z) <= range;

    /// <summary>Envanter denetimi: bedelin hepsi cantada mi.</summary>
    public static bool CanPay(Inventory bag, IReadOnlyDictionary<string, int> cost) =>
        cost.All(kv => bag.Count(kv.Key) >= kv.Value);

    /// <summary>Iade ya da kazanc cantaya sigiyor mu (tasan esya kaybolmasin).</summary>
    public static bool Fits(Inventory bag, IReadOnlyDictionary<string, int> items, Func<string, int> stackOf)
    {
        var probe = new Inventory(bag.Slots, stackOf);
        foreach (var (id, n, stars) in bag.Stacks) probe.Add(id, n, stars);
        return items.All(kv => probe.Add(kv.Key, kv.Value) == kv.Value);
    }
}
