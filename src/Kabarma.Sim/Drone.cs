namespace Kabarma.Sim;

/// <summary>Drone gorevi: Kaynak deposundan Hedef deposuna bir esya (Item). Koordinatlar dunya (x, z).</summary>
public sealed class DroneOrder
{
    public string Item { get; set; } = "";
    public float Fx { get; set; }
    public float Fz { get; set; }
    public float Tx { get; set; }
    public float Tz { get; set; }
}

/// <summary>Tek drone: 0 bosta (istasyonda), 1 kaynaga ucuyor, 2 hedefe ucuyor, 3 donuyor.</summary>
public sealed class Drone
{
    public int Phase { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public string Item { get; set; } = "";
    public int Qty { get; set; }
    public int Order { get; set; }
}

public sealed class DroneStation
{
    public List<DroneOrder> Orders { get; set; } = new();
    public List<Drone> Fleet { get; set; } = new();
    public int Next { get; set; }
}

/// <summary>
/// Drone Istasyonu (ICERIK §12: 6 drone, 80 m; GDD 11.3): gorev listesi (esya, kaynak depo, hedef depo) sirayla
/// dagitilir; bosta drone kaynakta esya ve hedefte yer varsa kalkar, kapasite kadar alir, hedefe birakir, doner.
/// Kalkis icin istasyon beslenmis olmali. Ucarken konumu Drone.X/Z'de durur (Godot iz cizer). Duz durum.
/// </summary>
public sealed class DroneState
{
    public Dictionary<int, DroneStation> Stations { get; set; } = new();

    public DroneStation Station(int id)
    {
        if (!Stations.TryGetValue(id, out var s)) Stations[id] = s = new DroneStation();
        return s;
    }

    /// <summary>Gorev ekler. Donen null: eklendi; degilse neden (menzil disi, ayni nokta, gorev dolu).</summary>
    public string? AddOrder(PowerNet net, PowerFile f, int stationId, DroneOrder o)
    {
        var n = net.Get(stationId);
        var d = n == null ? null : f.Def(n.Def);
        if (n == null || d == null || d.Kind != "drone_station") return "Drone İstasyonu değil";
        if (Dist(n.X, n.Z, o.Fx, o.Fz) > d.Range || Dist(n.X, n.Z, o.Tx, o.Tz) > d.Range) return $"Menzil dışı (en fazla {d.Range:0} m)";
        if (Dist(o.Fx, o.Fz, o.Tx, o.Tz) < 1.5f) return "Kaynak ve hedef aynı yer";
        var st = Station(stationId);
        if (st.Orders.Count >= 8) return "Görev listesi dolu (8)";
        st.Orders.Add(o);
        return null;
    }

    public void ClearOrders(int stationId) => Station(stationId).Orders.Clear();

    static float Dist(float ax, float az, float bx, float bz) => MathF.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    public void Tick(PowerNet net, PowerFile f, ITransportWorld world, float dt)
    {
        foreach (var k in Stations.Keys.Where(k => net.Get(k) == null).ToList()) Stations.Remove(k);
        foreach (var n in net.Nodes)
        {
            var d = f.Def(n.Def);
            if (d == null || d.Kind != "drone_station") continue;
            var st = Station(n.Id);
            while (st.Fleet.Count < d.Drones) st.Fleet.Add(new Drone { X = n.X, Z = n.Z });
            bool live = n.Powered && n.On && (!n.SignalWired || n.Signal > 0);
            foreach (var dr in st.Fleet) Step(n, d, st, dr, world, dt, live);
        }
    }

    void Step(PowerNode home, PowerDeviceDef d, DroneStation st, Drone dr, ITransportWorld world, float dt, bool live)
    {
        float speed = Math.Max(1f, d.Speed);
        if (dr.Phase == 0)
        {
            if (!live || st.Orders.Count == 0) { dr.X = home.X; dr.Z = home.Z; return; }
            for (int k = 0; k < st.Orders.Count; k++)
            {
                int idx = (st.Next + k) % st.Orders.Count;
                var o = st.Orders[idx];
                var src = world.StoreAt(o.Fx, o.Fz, 1.5f); var dst = world.StoreAt(o.Tx, o.Tz, 1.5f);
                if (src == null || dst == null || src.Count(o.Item) < 1 || dst.Room(o.Item) < 1) continue;
                dr.Order = idx; dr.Phase = 1; st.Next = (idx + 1) % st.Orders.Count;
                break;
            }
            return;
        }
        if (dr.Order >= st.Orders.Count) { dr.Phase = 3; dr.Qty = 0; return; } // gorev silindi: eve don
        var ord = st.Orders[dr.Order];
        (float tx, float tz) = dr.Phase switch { 1 => (ord.Fx, ord.Fz), 2 => (ord.Tx, ord.Tz), _ => (home.X, home.Z) };
        float dist = Dist(dr.X, dr.Z, tx, tz), step = speed * dt;
        if (dist > step) { dr.X += (tx - dr.X) / dist * step; dr.Z += (tz - dr.Z) / dist * step; return; }
        dr.X = tx; dr.Z = tz;
        switch (dr.Phase)
        {
            case 1: // kaynakta: kapasite, depodaki esya ve hedefteki yer kadar al
                {
                    var src = world.StoreAt(ord.Fx, ord.Fz, 1.5f); var dst = world.StoreAt(ord.Tx, ord.Tz, 1.5f);
                    int q = src == null || dst == null ? 0 : Math.Min(Math.Min(d.Capacity, src.Count(ord.Item)), dst.Room(ord.Item));
                    if (q > 0 && src!.Remove(ord.Item, q)) { dr.Item = ord.Item; dr.Qty = q; dr.Phase = 2; } else dr.Phase = 3;
                    break;
                }
            case 2: // hedefte: birak; sigmayan kaynaga doner (kaybolmaz)
                {
                    var dst = world.StoreAt(ord.Tx, ord.Tz, 1.5f);
                    int put = dst == null ? 0 : dst.Add(dr.Item, dr.Qty);
                    if (put < dr.Qty) world.StoreAt(ord.Fx, ord.Fz, 1.5f)?.Add(dr.Item, dr.Qty - put);
                    dr.Qty = 0; dr.Item = ""; dr.Phase = 3;
                    break;
                }
            default: dr.Phase = 0; break;
        }
    }
}
