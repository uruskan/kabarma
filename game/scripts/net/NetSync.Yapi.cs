using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Yapi akisi (M2, GDD §7) ile ortak oyunun baglantisi. "pieces" bolumu parcanin turunu, yerini ve canini tasir; bu dosya
/// yaninda "pieces.state" bolumunu (malzeme, plan bayragi, plan parcasina konan malzeme; ayni kanonik sirayla) ve
/// istemci isteklerini (yukselt, plan birak, malzeme koy) ekler. Host yetkili: istemcide BuildSystem.RemoteAuthority acik,
/// durum host'tan gelir. Plan Defteri yerlestirmesi M2'de yalniz host'ta.
/// </summary>
public partial class NetSync
{
    void RegisterYapi()
    {
        if (!IsHost) D.Build.System.RemoteAuthority = true;
        Register("pieces.state", false, WritePieceState, ReadPieceState);
        On("upgrade", HostUpgrade);
        On("plan", HostPlan);
        On("deliver", HostDeliver);
    }

    List<BuildPiece> CanonPieces() => Canon(LivePieces.Select(p => (p.Def.Id, p.GlobalTransform, p.Hp, p))).Select(e => e.p).ToList();

    void WritePieceState(NetWriter w, int peer)
    {
        var l = CanonPieces();
        w.VarU((uint)l.Count);
        foreach (var p in l)
        {
            w.Str(p.Material);
            w.U8((byte)(p.Planned ? 1 : 0));
            var d = p.S?.Delivered ?? new Dictionary<string, int>();
            w.VarU((uint)d.Count);
            foreach (var (k, n) in d.OrderBy(kv => kv.Key, System.StringComparer.Ordinal)) { w.Str(k); w.VarU((uint)n); }
        }
    }

    void ReadPieceState(NetReader r)
    {
        int n = (int)r.VarU();
        var states = new List<(string mat, bool plan, Dictionary<string, int> d)>(n);
        for (int i = 0; i < n; i++)
        {
            string mat = r.Str();
            bool plan = r.U8() != 0;
            int m = (int)r.VarU();
            var d = new Dictionary<string, int>();
            for (int k = 0; k < m; k++) d[r.Str()] = (int)r.VarU();
            states.Add((mat, plan, d));
        }
        var l = CanonPieces();
        if (l.Count != states.Count) return; // "pieces" ayni anda geldi; bir sonraki paket esitler
        for (int i = 0; i < l.Count; i++) D.Build.System.ApplyRemote(l[i], states[i].mat, states[i].plan, states[i].d);
    }

    /// <summary>Yukselt (BuildBridge.Yapi.cs Upgrade): hedef anahtari host'a.</summary>
    public bool ForwardUpgrade(Node3D t) => ForwardTarget("upgrade", t);
    /// <summary>Plan parcasina malzeme koy (BuildBridge.Yapi.cs DeliverTick).</summary>
    public bool ForwardDeliver(Node3D t) => ForwardTarget("deliver", t);

    /// <summary>Plan modunda birakma (BuildBridge.Yapi.cs PlanTick): hayaletin donusumu host'a; host plan olarak kurar.</summary>
    public bool ForwardPlan(BuildSystem sys)
    {
        if (IsHost) return false;
        var cur = sys.Current;
        if (cur == null || !cur.Valid) return true;
        return Forward("plan", w => { w.Str(cur.Def.Id); w.F32(cur.Xf.Origin.X); w.F32(cur.Xf.Origin.Y); w.F32(cur.Xf.Origin.Z); var q = cur.Xf.Basis.GetRotationQuaternion(); w.F32(q.X); w.F32(q.Y); w.F32(q.Z); w.F32(q.W); });
    }

    void HostUpgrade(NetActor a, NetReader r)
    {
        if (FindTarget(r) is not BuildPiece bp || bp.Planned) { Reject(a, "upgrade target"); return; }
        if (!NetRules.InRange(a, bp.GlobalPosition.X, bp.GlobalPosition.Y, bp.GlobalPosition.Z, Cfg.Ranges.Build)) { Reject(a, "upgrade range"); return; }
        var up = StructureCosts.Upgrade(D.Data.Build, bp.Material, D.Build.System.UnitsOf(bp.Def.Id), D.Build.InClaimAt(bp.GlobalPosition));
        if (up == null) { Reject(a, "upgrade top"); return; }
        var (to, pay, back) = up.Value;
        if (!NetRules.CanPay(a.Bag, pay)) { Reject(a, "upgrade cost"); return; }
        if (!D.Build.System.Upgrade(bp, to.Id, out var rep)) { Reject(a, "upgrade " + rep?.Reason); return; }
        foreach (var (item, k) in pay) a.Bag.Remove(item, k);
        foreach (var (item, k) in back) a.Bag.Add(item, k);
        Events.RaisePieceUpgraded(bp.Def.Id, to.Id);
        Flush();
    }

    void HostPlan(NetActor a, NetReader r)
    {
        string id = r.Str();
        var o = new Vector3(r.F32(), r.F32(), r.F32());
        var q = new Quaternion(r.F32(), r.F32(), r.F32(), r.F32());
        if (D.Build == null || !D.Build.Handles(id)) { Reject(a, "plan id"); return; }
        if (!q.IsNormalized()) q = q.Normalized();
        if (!NetRules.InRange(a, o.X, o.Y, o.Z, Cfg.Ranges.Build)) { Reject(a, "plan range"); return; }
        if (!a.Cooldown(NetReq.Place, Now, Cfg.Cooldowns.Place)) { Reject(a, "plan cooldown"); return; }
        var sys = D.Build.System;
        bool was = sys.PlanMode;
        sys.PlanMode = true;
        var piece = sys.PlaceAt(id, new Transform3D(new Basis(q), o), out var why);
        sys.PlanMode = was;
        if (piece == null) { Reject(a, "plan invalid " + why); return; }
        Flush();
    }

    void HostDeliver(NetActor a, NetReader r)
    {
        if (FindTarget(r) is not BuildPiece bp || !bp.Planned) { Reject(a, "deliver target"); return; }
        if (!NetRules.InRange(a, bp.GlobalPosition.X, bp.GlobalPosition.Y, bp.GlobalPosition.Z, Cfg.Ranges.Build)) { Reject(a, "deliver range"); return; }
        var cost = StructureCosts.Cost(D.Data.Build, bp.Material, D.Build.System.UnitsOf(bp.Def.Id));
        StructureCosts.Deliver(cost, bp.S.Delivered, a.Bag);
        if (StructureCosts.Missing(cost, bp.S.Delivered).Count == 0 && D.Build.System.CompletePlan(bp, out _))
        {
            Events.RaisePlanCompleted(bp.Def.Id);
            Events.RaisePiecePlaced(bp.Def.Id);
        }
        Flush();
    }
}
