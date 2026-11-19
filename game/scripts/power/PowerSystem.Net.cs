using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Guc agi, ortak oyun (CLAUDE.md "Ortak oyuna baglanma"; TEKNIK 5.3, 5.4). Host yetkili: "power" bolumu agin kalici
/// durumunu (parcalar, kablolar, aku, salter, oncelik, dugme) herkese yazar; istemci uzlastirir ve gorunusu yeniden kurar.
/// Istemcinin eylemleri host'a istek olur: "pcable" (kablo cek; aktorun cantasindan 1 Kablo) ve "pact" (ac/kapat,
/// oncelik, bas, kablolarini sok, sok; iade aktorun cantasina). Kurma, ortak'in "placeitem" istegiyle (HostPlaceItem ->
/// Director.PlacePower). Tek oyuncuda _d.Net null: her sey yerel.
/// </summary>
public partial class PowerSystem
{
    public enum Op : byte { Toggle = 1, Priority = 2, Press = 3, Unplug = 4, Dismantle = 5, Filter = 6, Reset = 7, Refuel = 8 }

    /// <summary>Director.InitNet'ten sonra (ertelenmis) bir kez.</summary>
    public void BindNet()
    {
        var net = _d.Net;
        if (net == null) return;
        net.Register("power", false, WriteNet, ReadNet);
        net.On("pcable", HostCable);
        net.On("pact", HostAct);
        net.On("ptrans", HostTrans); // M4 tasima: drone gorevi, teleferik hatti (PowerSystem.Transport.cs)
        _d.BindMayaNet();            // Maya Kazani (Director.Maya.cs)
        GD.Print($"POWER net bound host={net.IsHost}");
    }

    // ---------------------------------------------------------------- bolum

    void WriteNet(NetWriter w, int peer)
    {
        var n = Net;
        w.VarU((uint)n.NextId);
        w.VarU((uint)(n.Clock / 10.0)); // ruzgar evresi 10 sn adimla (her karede degismesin)
        var nodes = n.Nodes.OrderBy(x => x.Id).ToList();
        w.VarU((uint)nodes.Count);
        foreach (var x in nodes)
        {
            w.VarU((uint)x.Id); w.Str(x.Def);
            w.F32(x.X); w.F32(x.Y); w.F32(x.Z); w.F32(x.Yaw);
            w.U8((byte)x.Priority); w.Bool(x.On);
            w.Str(x.Filter); w.Bool(x.Mem); w.VarU((uint)x.Count); w.F32(x.Fuel); // M4 tasima: filtre, bellek, sayac, yakit
            w.VarU((uint)Mathf.RoundToInt(x.StoredWh));
            w.Bool(x.PressLeft > 0);
        }
        var links = n.Links.OrderBy(l => l.A).ThenBy(l => l.B).ThenBy(l => l.Signal).ToList();
        w.VarU((uint)links.Count);
        foreach (var l in links) { w.VarU((uint)l.A); w.VarU((uint)l.B); w.Bool(l.Signal); w.F32(l.Length); }
        TransportCodec.Write(w, n.Transport); // M4 tasima: bant seritleri, drone'lar, teleferik hatlari (TransportCodec.cs)
    }

    void ReadNet(NetReader r)
    {
        var n = Net;
        int nextId = (int)r.VarU();
        double clock = r.VarU() * 10.0;
        int count = (int)r.VarU();
        var nodes = new System.Collections.Generic.List<PowerNode>();
        for (int i = 0; i < count; i++)
        {
            var x = new PowerNode { Id = (int)r.VarU(), Def = r.Str(), X = r.F32(), Y = r.F32(), Z = r.F32(), Yaw = r.F32(), Priority = r.U8(), On = r.Bool() };
            x.Filter = r.Str(); x.Mem = r.Bool(); x.Count = (int)r.VarU(); x.Fuel = r.F32();
            x.StoredWh = r.VarU();
            bool pressed = r.Bool();
            var old = n.Get(x.Id);
            x.PressLeft = pressed ? (old?.PressLeft > 0 ? old.PressLeft : F.ButtonSeconds) : 0f;
            if (old != null) { x.Wants = old.Wants; x.Occupied = old.Occupied; }
            nodes.Add(x);
        }
        int lc = (int)r.VarU();
        var links = new System.Collections.Generic.List<PowerLink>();
        for (int i = 0; i < lc; i++) links.Add(new PowerLink { A = (int)r.VarU(), B = (int)r.VarU(), Signal = r.Bool(), Length = r.F32() });
        TransportCodec.Read(r, n.Transport);
        bool sameShape = n.Nodes.Count == nodes.Count && n.Nodes.OrderBy(x => x.Id).Zip(nodes, (a, b) => a.Id == b.Id && a.Def == b.Def).All(s => s);
        n.NextId = nextId;
        if (System.Math.Abs(n.Clock - clock) > 15) n.Clock = clock;
        n.Nodes = nodes;
        n.Links = links;
        if (!sameShape) Rebuild();
    }

    // ---------------------------------------------------------------- istekler

    /// <summary>Kablo cekme: istemcide host'a gider (true), host ya da tek oyuncuda false (yerel isle).</summary>
    bool ForwardCable(int a, int b, bool signal) =>
        _d.Net?.Forward("pcable", w => { w.VarU((uint)a); w.VarU((uint)b); w.Bool(signal); }) == true;

    void HostCable(NetActor actor, NetReader r)
    {
        int a = (int)r.VarU(), b = (int)r.VarU();
        bool signal = r.Bool();
        var na = Net.Get(a);
        if (na == null || actor.Bag.Count(F.CableItem) < 1 || !NetRules.InRange(actor, na.X, na.Y, na.Z, 12f)) { GD.Print($"POWER net reject cable {a}->{b}"); return; }
        if (Net.Connect(F, a, b, signal) == null) return;
        actor.Bag.Remove(F.CableItem, 1);
        _d.Net.Flush();
    }

    /// <summary>Panel ve dugme eylemi: istemcide host'a gider; host ya da tek oyuncuda yerel cantayla uygulanir.</summary>
    void Act(Op op, int id)
    {
        if (_d.Net?.Forward("pact", w => { w.U8((byte)op); w.VarU((uint)id); }) == true) { if (op == Op.Dismantle) _d.Hud.HideMenu(); return; }
        Apply(op, id, _d.State.Backpack);
    }

    void HostAct(NetActor actor, NetReader r)
    {
        var op = (Op)r.U8();
        int id = (int)r.VarU();
        var n = Net.Get(id);
        if (n == null || !NetRules.InRange(actor, n.X, n.Y, n.Z, 6f)) { GD.Print($"POWER net reject {op} {id}"); return; }
        Apply(op, id, actor.Bag);
        _d.Net.Flush();
    }

    /// <summary>Eylemin kendisi; iade <paramref name="bag"/>'e.</summary>
    void Apply(Op op, int id, Inventory bag)
    {
        var n = Net.Get(id);
        if (n == null) return;
        switch (op)
        {
            case Op.Toggle: n.On = !n.On; break;
            case Op.Filter: case Op.Reset: case Op.Refuel: ApplyTransport(op, n, bag); break; // M4 tasima (PowerSystem.Transport.cs)
            case Op.Priority: n.Priority = n.Priority >= F.PriorityMax ? F.PriorityMin : n.Priority + 1; break;
            case Op.Press:
                Net.Press(F, id);
                if (_owners.TryGetValue(id, out var o) && o is PowerDevice pd) pd.Bounce();
                break;
            case Op.Unplug:
                int k = Net.Disconnect(id);
                if (k > 0) bag.Add(F.CableItem, k);
                break;
            case Op.Dismantle:
                if (_owners.TryGetValue(id, out var od) && od is PowerDevice dev)
                {
                    int freed = Net.Remove(id);
                    _owners.Remove(id);
                    if (_pick == id) _pick = -1;
                    if (freed > 0) bag.Add(F.CableItem, freed);
                    bag.Add(dev.Def.Id, 1);
                    Fx.Burst(dev.GlobalPosition + Vector3.Up * 0.4f, Palette.Wood, 16, 3f, 0.08f, 0.6f);
                    Sfx.Play("pickup", dev.GlobalPosition, -4f);
                    dev.QueueFree();
                }
                break;
        }
        GD.Print($"POWER {op} {n.Def}({id})");
    }
}
