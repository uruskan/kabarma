namespace Kabarma.Sim;

/// <summary>
/// Tasima dunyasi (Godot yazar; testler sahte yazar): bir noktadaki depo (Sandik, istasyon deposu). Null: orada depo yok.
/// Sim yalniz bu kapidan depoya dokunur; Godot dugumu tutmaz (TEKNIK 9).
/// </summary>
public interface ITransportWorld
{
    IItemStore? StoreAt(float x, float z, float radius);
}

/// <summary>Bantta giden bir nesne: esya ve parca uzerindeki yol (m).</summary>
public sealed class BeltItem
{
    public string Item { get; set; } = "";
    public float T { get; set; }
}

/// <summary>Bir bant/boru/ayirici/birlestirici/kol parcasinin tasima durumu (PowerNode kimligiyle).</summary>
public sealed class LaneState
{
    public List<BeltItem> Items { get; set; } = new();
    /// <summary>Parcaya en son nesne girdigi an (TransportState.Clock): bant araligi.</summary>
    public double LastIn { get; set; } = -99;
    /// <summary>Ayirici: siradaki cikis. Kol: biriken is.</summary>
    public int Toggle { get; set; }
    public float Acc { get; set; }
}

/// <summary>
/// Tasima (GDD 11.3, ICERIK Â§12): bant, boru, ayirici, birlestirici ve kol/pompa. Parcalar guc agiyla ayni PowerNode
/// listesindedir (konum, donus, sinyal, guc); burasi yalniz nesnelerin yolculugunu tutar. Baglanti kablosuz ve turetilir:
/// parcanin onundeki nokta (yon = Yaw: (sin, cos)) bir sonraki parcadir; parca yoksa orada bir depo (Sandik) aranir.
/// Kol arkasindaki depodan alir, onune verir. Saniyede en cok 1/Gap nesne (bant: 2). Sinyal telli parca sinyal 0 iken durur.
/// Duz durum (TEKNIK 9): Lanes, Clock, Drones, Ropeways; PowerNet ile kaydolur ve aga gider.
/// </summary>
public sealed class TransportState
{
    public double Clock { get; set; }
    public Dictionary<int, LaneState> Lanes { get; set; } = new();
    public DroneState Drones { get; set; } = new();
    public RopewayState Ropeways { get; set; } = new();
    /// <summary>Toplam teslim edilen nesne (test ve basarim).</summary>
    public int Delivered { get; set; }

    public LaneState Lane(int id)
    {
        if (!Lanes.TryGetValue(id, out var l)) Lanes[id] = l = new LaneState();
        return l;
    }

    public static (float x, float z) Dir(float yaw) => (MathF.Sin(yaw), MathF.Cos(yaw));

    public void Tick(PowerNet net, PowerFile f, ITransportWorld world, float dt)
    {
        Clock += dt;
        // Silinen parcalarin seridi kalmasin.
        foreach (var k in Lanes.Keys.Where(k => net.Get(k) == null).ToList()) Lanes.Remove(k);
        var conv = net.Nodes.Where(n => f.Def(n.Def) is { IsConveyor: true }).ToList();
        foreach (var n in conv) MoveConveyor(net, f, world, n, dt);
        foreach (var n in net.Nodes.Where(n => f.Def(n.Def) is { IsMover: true })) RunMover(net, f, world, n, dt);
        Drones.Tick(net, f, world, dt);
        Ropeways.Tick(net, f, world, dt);
    }

    static bool Active(PowerNode n) => !n.SignalWired || n.Signal > 0;

    /// <summary>Parcanin onundeki (k=1), solundaki (+90) ya da saginda (-90) nokta; tile kadar uzak.</summary>
    static (float x, float z) Probe(PowerFile f, PowerNode n, float quarterTurns, float dist = 1f)
    {
        var (dx, dz) = Dir(n.Yaw + quarterTurns * MathF.PI / 2f);
        float d = f.TileSize * dist;
        return (n.X + dx * d, n.Z + dz * d);
    }

    PowerNode? ConveyorAt(PowerNet net, PowerFile f, PowerNode from, (float x, float z) p)
    {
        PowerNode? best = null; float bd = 0.45f;
        foreach (var n in net.Nodes)
        {
            if (n.Id == from.Id || f.Def(n.Def) is not { IsConveyor: true }) continue;
            float d = MathF.Sqrt((n.X - p.x) * (n.X - p.x) + (n.Z - p.z) * (n.Z - p.z));
            if (d >= bd) continue;
            // Karsi karsiya bakan parca nesne almaz (onu bize cevirmis).
            var (fx, fz) = Probe(f, n, 0);
            if (MathF.Abs(fx - from.X) < 0.45f && MathF.Abs(fz - from.Z) < 0.45f) continue;
            bd = d; best = n;
        }
        return best;
    }

    bool Accepts(PowerFile f, PowerNode target, string item)
    {
        var d = f.Def(target.Def)!;
        var lane = Lane(target.Id);
        if (d.Fluid && !f.Fluids.Contains(item)) return false;
        if (!d.Fluid && f.Fluids.Contains(item) && d.Kind != "merger" && d.Kind != "splitter") return false;
        return lane.Items.Count < Math.Max(1, d.Capacity) && Clock - lane.LastIn >= d.Gap - 1e-6;
    }

    void Push(PowerNode target, string item)
    {
        var lane = Lane(target.Id);
        lane.Items.Add(new BeltItem { Item = item, T = 0 });
        lane.LastIn = Clock;
    }

    /// <summary>Nesneyi (x,z) noktasindaki parcaya ya da depoya verir. Verildiyse true.</summary>
    bool Deliver(PowerNet net, PowerFile f, ITransportWorld world, PowerNode from, string item, (float x, float z) p)
    {
        var tn = ConveyorAt(net, f, from, p);
        if (tn != null)
        {
            if (!Accepts(f, tn, item)) return false;
            Push(tn, item);
            return true;
        }
        var store = world.StoreAt(p.x, p.z, 0.8f);
        if (store == null || store.Add(item, 1) < 1) return false;
        Delivered++;
        return true;
    }

    void MoveConveyor(PowerNet net, PowerFile f, ITransportWorld world, PowerNode n, float dt)
    {
        var d = f.Def(n.Def)!;
        var lane = Lane(n.Id);
        if (lane.Items.Count == 0) return;
        bool active = Active(n);
        float len = d.Range > 0 ? d.Range : f.TileSize;
        float speed = Math.Max(0.1f, d.Speed);
        if (active)
            for (int i = 0; i < lane.Items.Count; i++)
            {
                float ahead = i == 0 ? len : lane.Items[i - 1].T - 0.25f; // nesneler ust uste binmez
                lane.Items[i].T = MathF.Min(MathF.Max(lane.Items[i].T, 0f) + speed * dt, MathF.Max(0f, ahead));
            }
        var head = lane.Items[0];
        if (!active || head.T < len - 1e-4f) return;
        var outs = new List<(float x, float z)> { Probe(f, n, 0) };
        if (d.Kind == "splitter") { outs.Add(Probe(f, n, 1)); outs.Add(Probe(f, n, -1)); }
        for (int k = 0; k < outs.Count; k++)
        {
            int idx = d.Kind == "splitter" ? (lane.Toggle + k) % outs.Count : k;
            if (!Deliver(net, f, world, n, head.Item, outs[idx])) continue;
            lane.Items.RemoveAt(0);
            lane.Toggle = (idx + 1) % outs.Count;
            return;
        }
    }

    void RunMover(PowerNet net, PowerFile f, ITransportWorld world, PowerNode n, float dt)
    {
        var d = f.Def(n.Def)!;
        var lane = Lane(n.Id);
        if (!Active(n) || !n.Powered || !n.On) { lane.Acc = 0; return; }
        lane.Acc = MathF.Min(lane.Acc + dt * Math.Max(0.01f, d.Rate), 2f);
        if (lane.Acc < 1f) return;
        var back = Probe(f, n, 2);
        var src = world.StoreAt(back.x, back.z, 0.8f);
        if (src == null) return;
        string? item = string.IsNullOrEmpty(n.Filter) ? null : n.Filter;
        if (item == null && src is Inventory inv) item = inv.Contents.Select(c => c.id).FirstOrDefault(i => f.Fluids.Contains(i) == d.Fluid);
        if (item == null || (d.Fluid && !f.Fluids.Contains(item)) || src.Count(item) < 1) return;
        if (!src.Remove(item, 1)) return;
        if (!Deliver(net, f, world, n, item, Probe(f, n, 0))) { src.Add(item, 1); return; } // hedef dolu: nesne yerine doner
        lane.Acc -= 1f;
    }
}
