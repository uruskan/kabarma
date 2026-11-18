using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Savunma, ortak oyun (CLAUDE.md "Ortak oyuna baglanma"). Yapinin kendisi ortak'in "placed" bolumuyle (Furniture)
/// cogalir; "defense" bolumu kural durumunu (can, cephane, oncelik, kapak) host'tan herkese yazar. Kurallar yalniz
/// host'ta isler; istemcide mermi gorunusu yok (M3 kisiti). E eylemleri "dact" istegiyle host'a, aktorun cantasindan.
/// </summary>
public partial class DefenseSystem
{
    /// <summary>Director.InitNet'ten sonra (ertelenmis) bir kez.</summary>
    public void BindNet()
    {
        var net = _d.Net;
        if (net == null) return;
        net.Register("defense", false, WriteNet, ReadNet);
        net.On("dact", HostAct);
        GD.Print($"DEFENSE net bound host={net.IsHost}");
    }

    void WriteNet(NetWriter w, int peer)
    {
        w.VarU((uint)S.NextId);
        var towers = S.Towers.OrderBy(t => t.Id).ToList();
        w.VarU((uint)towers.Count);
        foreach (var t in towers)
        {
            w.VarU((uint)t.Id); w.Str(t.Def); w.F32(t.Pos[0]); w.F32(t.Pos[1]); w.F32(t.Pos[2]); w.F32(t.Yaw);
            w.F32(t.Hp); w.U8((byte)System.Math.Max(0, F.Priorities.IndexOf(t.Priority)));
            var mag = t.Mag.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).ToList();
            w.VarU((uint)mag.Count);
            foreach (var (k, n) in mag) { w.Str(k); w.VarU((uint)n); }
        }
        var traps = S.Traps.OrderBy(t => t.Id).ToList();
        w.VarU((uint)traps.Count);
        foreach (var t in traps)
        {
            w.VarU((uint)t.Id); w.Str(t.Def); w.F32(t.Pos[0]); w.F32(t.Pos[1]); w.F32(t.Pos[2]); w.F32(t.Yaw);
            w.F32(t.Hp); w.Bool(t.Open); w.VarU((uint)t.Held.Count);
        }
    }

    void ReadNet(NetReader r)
    {
        int next = (int)r.VarU();
        var towers = new List<TowerState>();
        int n = (int)r.VarU();
        for (int i = 0; i < n; i++)
        {
            var t = new TowerState { Id = (int)r.VarU(), Def = r.Str(), Pos = new[] { r.F32(), r.F32(), r.F32() }, Yaw = r.F32(), Hp = r.F32() };
            int pi = r.U8();
            t.Priority = pi < F.Priorities.Count ? F.Priorities[pi] : "nearest";
            int m = (int)r.VarU();
            for (int k = 0; k < m; k++) t.Mag[r.Str()] = (int)r.VarU();
            towers.Add(t);
        }
        var traps = new List<TrapState>();
        n = (int)r.VarU();
        for (int i = 0; i < n; i++)
        {
            var t = new TrapState { Id = (int)r.VarU(), Def = r.Str(), Pos = new[] { r.F32(), r.F32(), r.F32() }, Yaw = r.F32(), Hp = r.F32(), Open = r.Bool() };
            int held = (int)r.VarU();
            for (int k = 0; k < held; k++) t.Held.Add(-1 - k); // istemcide yalniz sayi
            traps.Add(t);
        }
        bool sameShape = S.Towers.Select(t => t.Id).SequenceEqual(towers.Select(t => t.Id)) && S.Traps.Select(t => t.Id).SequenceEqual(traps.Select(t => t.Id));
        S.NextId = next; S.Towers = towers; S.Traps = traps;
        if (!sameShape) Rebind();
    }

    /// <summary>Istemci: dugumleri ("placed" ile gelir) yeni kimliklere baglar; dugum kurmaz, silmez.</summary>
    void Rebind()
    {
        _nodes.Clear();
        foreach (var node in _d.Furniture.OfType<DefenseNode>())
        {
            if (!IsInstanceValid(node)) continue;
            node.StateId = S.FindAt(node.Def.Id, ToN(node.GlobalPosition), 0.5f);
            if (node.StateId != 0) _nodes[node.StateId] = node;
        }
    }

    /// <summary>Istemcide eylemi host'a gonderir (true); host ve tek oyuncuda false.</summary>
    bool ForwardAct(Op op, DefenseNode n) =>
        _d.Net?.Forward("dact", w => { w.U8((byte)op); w.Str(n.Def.Id); w.F32(n.GlobalPosition.X); w.F32(n.GlobalPosition.Y); w.F32(n.GlobalPosition.Z); }) == true;

    void HostAct(NetActor actor, NetReader r)
    {
        var op = (Op)r.U8();
        string def = r.Str();
        var at = new Vector3(r.F32(), r.F32(), r.F32());
        int id = S.FindAt(def, ToN(at), 0.5f);
        if (id == 0 || !NetRules.InRange(actor, at.X, at.Y, at.Z, 6f)) { GD.Print($"DEFENSE net reject {op} {def}"); return; }
        Apply(op, id, actor.Bag, false);
        _d.Net.Flush();
    }
}
