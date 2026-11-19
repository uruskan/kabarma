using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Guc ve tasima, Godot katmani (M4 tasima; GDD 11.1-11.3, ICERIK §12). Kurallar Sim'de (PowerNet, TransportState);
/// burasi dunyayi Sim'e baglar: depolar (Sandik) ITransportWorld olarak, gunes, gece, Kabarma, hareket, su, Cizbiz, depo
/// doluluğu; parcalarin 1 m karoya oturmasi; E panelinin tasima satirlari (kol filtresi, yakit, sayac, drone gorevleri,
/// teleferik hatti). Tasima yalniz host'ta ve tek oyuncuda islenir (ortak: Sandiklar host'ta; durum "power" bolumuyle gider).
/// </summary>
public partial class PowerSystem
{
    sealed class Depots : ITransportWorld
    {
        readonly Director _d;
        public Depots(Director d) { _d = d; }
        public IItemStore StoreAt(float x, float z, float radius)
        {
            Inventory best = null; float bd = radius * radius;
            foreach (var f in _d.Furniture)
            {
                if (!GodotObject.IsInstanceValid(f) || f.Storage == null) continue;
                float dx = f.GlobalPosition.X - x, dz = f.GlobalPosition.Z - z, d2 = dx * dx + dz * dz;
                if (d2 <= bd) { bd = d2; best = f.Storage; }
            }
            return best;
        }
    }

    public enum TransOp : byte { DroneOrder = 1, DroneClear = 2, RopeConnect = 3, RopeRemove = 4 }

    /// <summary>Drone gorevi ve teleferik hatti eylemleri: istemcide host'a gider; host ve tek oyuncuda yerel isler.</summary>
    void TransAct(TransOp op, int id, int other = 0, string item = "", float fx = 0, float fz = 0, float tx = 0, float tz = 0)
    {
        if (_d.Net?.Forward("ptrans", w => { w.U8((byte)op); w.VarU((uint)id); w.VarU((uint)other); w.Str(item); w.F32(fx); w.F32(fz); w.F32(tx); w.F32(tz); }) == true) return;
        ApplyTrans(op, id, other, item, fx, fz, tx, tz, true);
    }

    void HostTrans(NetActor actor, NetReader r)
    {
        var op = (TransOp)r.U8();
        int id = (int)r.VarU(), other = (int)r.VarU();
        string item = r.Str();
        float fx = r.F32(), fz = r.F32(), tx = r.F32(), tz = r.F32();
        var n = Net.Get(id);
        if (n == null || !NetRules.InRange(actor, n.X, n.Y, n.Z, 8f)) { GD.Print($"POWER net reject {op} {id}"); return; }
        ApplyTrans(op, id, other, item, fx, fz, tx, tz, false);
        _d.Net.Flush();
    }

    /// <summary>Eylemin kendisi. local: ipucu ve ses bu makinede. Donen: null ya da reddetme nedeni.</summary>
    string ApplyTrans(TransOp op, int id, int other, string item, float fx, float fz, float tx, float tz, bool local)
    {
        string err = null;
        switch (op)
        {
            case TransOp.DroneOrder: err = Net.Transport.Drones.AddOrder(Net, F, id, new DroneOrder { Item = item, Fx = fx, Fz = fz, Tx = tx, Tz = tz }); break;
            case TransOp.DroneClear: Net.Transport.Drones.ClearOrders(id); break;
            case TransOp.RopeConnect: err = Net.Transport.Ropeways.Connect(Net, F, id, other, item); break;
            case TransOp.RopeRemove: Net.Transport.Ropeways.Lines.RemoveAll(l => l.A == id); break;
        }
        if (local && op is TransOp.DroneOrder or TransOp.RopeConnect)
        {
            _d.Hud.Hint(err ?? (op == TransOp.DroneOrder ? "Görev eklendi" : "Hat kuruldu"), 2f);
            Sfx.Play(err == null ? "pickup" : "hit", null, -8f);
        }
        GD.Print($"POWER trans {op} {id} err={err}");
        return err;
    }

    Depots _depots;
    Vector3? _droneFrom, _droneTo;

    public (Vector3 at, float yaw) SnapToTileOf(Vector3 at, float yaw) => SnapToTile(at, yaw);

    static (Vector3, float) SnapToTile(Vector3 at, float yaw)
    {
        // Sim'in karosu 1 m (power.json tileSize); yon 90 derecenin kati.
        float q = Mathf.Pi / 2f;
        return (new Vector3(Mathf.Round(at.X), at.Y, Mathf.Round(at.Z)), Mathf.Round(yaw / q) * q);
    }

    /// <summary>Her kare, TickPower'dan once: Sim'in dis dunya girdileri.</summary>
    void FeedWorld()
    {
        var net = Net;
        _depots ??= new Depots(_d);
        net.World = _d.NetClient ? null : _depots;
        var cal = _d.State.Calendar;
        net.Night = cal.IsNight;
        net.Sun = cal.IsNight ? 0f : Mathf.Sin(cal.DaylightFraction * Mathf.Pi);
        net.KabarmaLevel = _d.State.Night == null ? 0 : _d.State.Night.Phase switch { NightPhase.Alarm => 25, NightPhase.Waves => 75, _ => 0 };
        var movers = new List<Vector3> { _d.Player.GlobalPosition };
        foreach (var g in GetTree().GetNodesInGroup("gidak").OfType<Node3D>()) if (g.Visible) movers.Add(g.GlobalPosition);
        foreach (var g in GetTree().GetNodesInGroup("power_pressers").OfType<Node3D>()) movers.Add(g.GlobalPosition);
        var cizbiz = GetTree().GetNodesInGroup("cizbiz").OfType<Node3D>().Select(g => g.GlobalPosition).ToList();
        foreach (var dev in Devices)
        {
            var n = Net.Get(dev.NodeId);
            if (n == null) continue;
            var p = dev.GlobalPosition;
            switch (dev.PDef.Kind)
            {
                case "motion":
                    n.Occupied = movers.Any(w => new Vector2(w.X - p.X, w.Z - p.Z).Length() < 8f);
                    break;
                case "generator" when dev.PDef.Source == "water":
                    n.Occupied = _d.World != null && (_d.World.IsWater(p) || _d.World.IsWater(p + Vector3.Right * 1.5f) || _d.World.IsWater(p - Vector3.Right * 1.5f));
                    break;
                case "generator" when dev.Def.Id == "cizbiz_tanki":
                    n.Occupied = cizbiz.Any(w => new Vector2(w.X - p.X, w.Z - p.Z).Length() < dev.PDef.AnimalRadius);
                    break;
                case "reading":
                    if (dev.Def.Id == "depo_sensoru")
                    {
                        var chest = _d.Furniture.Where(f => IsInstanceValid(f) && f.Storage != null && f.GlobalPosition.DistanceTo(p) < 3.5f).OrderBy(f => f.GlobalPosition.DistanceTo(p)).FirstOrDefault();
                        n.Reading = chest == null ? 0 : Mathf.RoundToInt(100f * chest.Storage.UsedSlots / Mathf.Max(1, chest.Storage.Slots));
                    }
                    else n.Reading = _d.World != null && (_d.World.IsWater(p) || _d.World.IsWater(p + Vector3.Forward * 3f) || _d.World.IsWater(p + Vector3.Back * 3f)) ? 100 : 0; // Nem: suya yakinlik (yer tutucu olcum)
                    break;
            }
        }
    }

    // ---------------------------------------------------------------- E paneli

    static string NextFilter(Inventory bag, string cur)
    {
        var ids = new List<string> { "" };
        ids.AddRange(bag.Contents.Select(c => c.id).Distinct().OrderBy(x => x, System.StringComparer.Ordinal));
        int i = ids.IndexOf(cur);
        return ids[(i + 1) % ids.Count];
    }

    void ApplyTransport(Op op, PowerNode n, Inventory bag)
    {
        switch (op)
        {
            case Op.Filter: n.Filter = NextFilter(bag, n.Filter); break;
            case Op.Reset: n.Count = 0; n.Mem = false; n.PrevIn = false; break;
            case Op.Refuel:
                var d = F.Def(n.Def);
                if (d?.FuelItem == null) break;
                int have = bag.Count(d.FuelItem);
                int use = System.Math.Min(have, 5);
                if (use > 0 && bag.Remove(d.FuelItem, use)) Net.Refuel(F, n.Id, use);
                break;
        }
    }

    Furniture ChestAtAim()
    {
        var aim = _d.Player.AimPoint(out _);
        return _d.Furniture.Where(f => IsInstanceValid(f) && f.Storage != null && f.GlobalPosition.DistanceTo(aim) < 2.5f).OrderBy(f => f.GlobalPosition.DistanceTo(aim)).FirstOrDefault();
    }

    void TransportPanel(List<Hud.MenuItem> items, PowerNode n, PowerDeviceDef pdef, PowerDevice dev, System.Action re)
    {
        int id = n.Id;
        var bag = _d.State.Backpack;
        if (pdef.IsMover || pdef.Kind == "ropeway_station" || pdef.Kind == "drone_station")
            items.Add(new Hud.MenuItem($"Eşya: {(n.Filter.Length > 0 ? ItemNames.Of(n.Filter) : "hepsi")}  (çantandaki eşyalar arasında değiştir)", () => true, () => { Act(Op.Filter, id); Sfx.Play("place", null, -12f); re(); }));
        if (pdef.Kind is "counter" or "memory")
            items.Add(new Hud.MenuItem("Sıfırla", () => true, () => { Act(Op.Reset, id); Sfx.Play("place", null, -12f); re(); }));
        if (pdef.Source == "fuel" && pdef.FuelItem != null)
            items.Add(new Hud.MenuItem($"Yakıt ekle: {ItemNames.Of(pdef.FuelItem)} ×{System.Math.Min(5, bag.Count(pdef.FuelItem))} (çantada {bag.Count(pdef.FuelItem)})", () => bag.Count(pdef.FuelItem) > 0, () => { Act(Op.Refuel, id); Sfx.Play("pickup", null, -8f); re(); }, pdef.FuelItem));
        if (pdef.Kind == "drone_station")
        {
            var ds = Net.Transport.Drones.Station(id);
            items.Add(new Hud.MenuItem(_droneFrom == null ? "Kaynak: bakılan Sandık" : "Kaynak seçildi ✓ (yeniden: bakılan Sandık)", () => ChestAtAim() != null, () => { _droneFrom = ChestAtAim().GlobalPosition; Sfx.Play("place", null, -10f); re(); }));
            items.Add(new Hud.MenuItem(_droneTo == null ? "Hedef: bakılan Sandık" : "Hedef seçildi ✓ (yeniden: bakılan Sandık)", () => ChestAtAim() != null, () => { _droneTo = ChestAtAim().GlobalPosition; Sfx.Play("place", null, -10f); re(); }));
            items.Add(new Hud.MenuItem($"Görev ekle ({ds.Orders.Count}/8): {(n.Filter.Length > 0 ? ItemNames.Of(n.Filter) : "?")}", () => _droneFrom != null && _droneTo != null && n.Filter.Length > 0, () =>
            {
                TransAct(TransOp.DroneOrder, id, 0, n.Filter, _droneFrom.Value.X, _droneFrom.Value.Z, _droneTo.Value.X, _droneTo.Value.Z);
                re();
            }));
            items.Add(new Hud.MenuItem("Görevleri temizle", () => ds.Orders.Count > 0, () => { TransAct(TransOp.DroneClear, id); re(); }));
        }
        if (pdef.Kind == "ropeway_station")
        {
            var lines = Net.Transport.Ropeways.Lines;
            var aim = _d.Player.AimPoint(out _);
            int other = Net.Nodes.Where(x => x.Id != id && F.Def(x.Def)?.Kind == "ropeway_station").OrderBy(x => new Vector3(x.X, x.Y, x.Z).DistanceTo(aim)).Select(x => x.Id).FirstOrDefault();
            items.Add(new Hud.MenuItem($"Hat kur: bakılan en yakın başka istasyona ({lines.Count(l => l.A == id)} hat)", () => other > 0, () =>
            {
                TransAct(TransOp.RopeConnect, id, other, n.Filter);
                re();
            }));
            items.Add(new Hud.MenuItem("Hatlarını kaldır", () => lines.Any(l => l.A == id), () => { TransAct(TransOp.RopeRemove, id); re(); }));
        }
    }
}
