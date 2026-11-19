namespace Kabarma.Sim;

/// <summary>
/// Tasima durumunun ag kodu (ortak oyun; TEKNIK 5.3): "power" bolumunun sonuna yazilir. Yazim kanoniktir (kimlige gore
/// sirali, sozluk sirasi yok), okuma durumu bastan kurar: istemci bant seritlerini, drone'lari ve teleferik hatlarini
/// host'un yazdigi gibi gorur. Ayni kod testte ve oyunda (Godot tipi yok).
/// </summary>
public static class TransportCodec
{
    public static void Write(NetWriter w, TransportState t)
    {
        w.VarU((uint)t.Delivered);
        var lanes = t.Lanes.Where(kv => kv.Value.Items.Count > 0 || kv.Value.Toggle != 0).OrderBy(kv => kv.Key).ToList();
        w.VarU((uint)lanes.Count);
        foreach (var (id, l) in lanes)
        {
            w.VarU((uint)id); w.VarU((uint)l.Toggle); w.VarU((uint)l.Items.Count);
            foreach (var it in l.Items) { w.Str(it.Item); w.F32(it.T); }
        }
        var stations = t.Drones.Stations.OrderBy(kv => kv.Key).ToList();
        w.VarU((uint)stations.Count);
        foreach (var (id, s) in stations)
        {
            w.VarU((uint)id); w.VarU((uint)s.Next);
            w.VarU((uint)s.Orders.Count);
            foreach (var o in s.Orders) { w.Str(o.Item); w.F32(o.Fx); w.F32(o.Fz); w.F32(o.Tx); w.F32(o.Tz); }
            w.VarU((uint)s.Fleet.Count);
            foreach (var d in s.Fleet) { w.U8((byte)d.Phase); w.F32(d.X); w.F32(d.Z); w.Str(d.Item); w.VarU((uint)d.Qty); w.VarU((uint)d.Order); }
        }
        var lines = t.Ropeways.Lines.OrderBy(l => l.A).ThenBy(l => l.B).ToList();
        w.VarU((uint)lines.Count);
        foreach (var l in lines)
        {
            w.VarU((uint)l.A); w.VarU((uint)l.B); w.Str(l.Item);
            w.VarU((uint)l.Cars.Count);
            foreach (var c in l.Cars) { w.Str(c.Item); w.VarU((uint)c.Qty); w.F32(c.T); }
        }
    }

    public static void Read(NetReader r, TransportState t)
    {
        t.Delivered = (int)r.VarU();
        t.Lanes = new Dictionary<int, LaneState>();
        int nl = (int)r.VarU();
        for (int i = 0; i < nl; i++)
        {
            int id = (int)r.VarU();
            var l = new LaneState { Toggle = (int)r.VarU() };
            int n = (int)r.VarU();
            for (int k = 0; k < n; k++) l.Items.Add(new BeltItem { Item = r.Str(), T = r.F32() });
            t.Lanes[id] = l;
        }
        t.Drones.Stations = new Dictionary<int, DroneStation>();
        int ns = (int)r.VarU();
        for (int i = 0; i < ns; i++)
        {
            int id = (int)r.VarU();
            var s = new DroneStation { Next = (int)r.VarU() };
            int no = (int)r.VarU();
            for (int k = 0; k < no; k++) s.Orders.Add(new DroneOrder { Item = r.Str(), Fx = r.F32(), Fz = r.F32(), Tx = r.F32(), Tz = r.F32() });
            int nf = (int)r.VarU();
            for (int k = 0; k < nf; k++) s.Fleet.Add(new Drone { Phase = r.U8(), X = r.F32(), Z = r.F32(), Item = r.Str(), Qty = (int)r.VarU(), Order = (int)r.VarU() });
            t.Drones.Stations[id] = s;
        }
        t.Ropeways.Lines = new List<RopewayLine>();
        int nr = (int)r.VarU();
        for (int i = 0; i < nr; i++)
        {
            var l = new RopewayLine { A = (int)r.VarU(), B = (int)r.VarU(), Item = r.Str() };
            int nc = (int)r.VarU();
            for (int k = 0; k < nc; k++) l.Cars.Add(new RopewayCar { Item = r.Str(), Qty = (int)r.VarU(), T = r.F32() });
            t.Ropeways.Lines.Add(l);
        }
    }
}
