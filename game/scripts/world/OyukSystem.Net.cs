using System;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Oyuk, ortak oyun (CLAUDE.md "Ortak oyuna baglanma"). Kalici durum (temizlenen Oyuk, acilan sandik) "oyuk" bolumuyle
/// host'tan herkese yazilir. Dovus her makinede yerel isler (M3 kisiti): istemci koruyucuyu yenince "oyukclear" istegi
/// gider; sandik "oyukchest" istegiyle host'ta acilir, odul aktorun cantasina girer (istemci cantasi host'ta durur).
/// </summary>
public partial class OyukSystem
{
    /// <summary>Director.InitOyuk'tan ertelenmis (InitNet'ten sonra) bir kez.</summary>
    public void BindNet()
    {
        var net = _d.Net;
        if (net == null) return;
        net.Register("oyuk", false, WriteNet, ReadNet);
        net.On("oyukclear", HostClear);
        net.On("oyukchest", HostChest);
        GD.Print($"OYUK net bound host={net.IsHost}");
    }

    void WriteNet(NetWriter w, int peer)
    {
        var recs = P.Records.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        w.VarU((uint)recs.Count);
        foreach (var (id, r) in recs)
        {
            w.Str(id); w.Bool(r.Cleared); w.VarU((uint)Math.Max(0, r.Entered));
            var looted = r.Looted.OrderBy(x => x).ToList();
            w.VarU((uint)looted.Count);
            foreach (var s in looted) w.VarU((uint)s);
        }
    }

    void ReadNet(NetReader r)
    {
        var next = new OyukProgress();
        int n = (int)r.VarU();
        for (int i = 0; i < n; i++)
        {
            var rec = new OyukRecord();
            string id = r.Str();
            rec.Cleared = r.Bool(); rec.Entered = (int)r.VarU();
            int m = (int)r.VarU();
            for (int k = 0; k < m; k++) rec.Looted.Add((int)r.VarU());
            next.Records[id] = rec;
        }
        P.LoadFrom(next);
        // Acik odadaki sandik kapaklari host'un kaydina uyar.
        if (Inside is { } room && P.Records.TryGetValue(room.Poi.Id, out var cur))
            foreach (var c in room.Chests.Where(c => !c.Opened && cur.Looted.Contains(c.Slot))) c.Open();
    }

    OyukRoom RoomOf(string poiId) => Rooms.FirstOrDefault(r => r.Poi.Id == poiId);

    /// <summary>Aktor odanin icinde mi (oda oyun karesinin disinda, TEKNIK §7.5).</summary>
    static bool InRoom(NetActor a, OyukRoom room) =>
        NetRules.InRange(a, room.Center.X, room.Center.Y, room.Center.Z, room.Radius + 4f);

    void HostClear(NetActor actor, NetReader r)
    {
        var room = RoomOf(r.Str());
        if (room == null || !InRoom(actor, room)) { GD.Print("OYUK net reject clear"); return; }
        var rec = P.Of(room.Poi.Id);
        if (rec.Cleared) return;
        rec.Cleared = true;
        Events.RaiseOyukCleared(room.Poi.Id);
        GD.Print($"OYUK net cleared {room.Poi.Id} by peer {actor.Peer}");
        _d.Net.Flush();
    }

    void HostChest(NetActor actor, NetReader r)
    {
        var room = RoomOf(r.Str());
        int slot = (int)r.VarU();
        var chest = room?.Chests.FirstOrDefault(c => c.Slot == slot);
        if (room == null || chest == null || !NetRules.InRange(actor, chest.GlobalPosition.X, chest.GlobalPosition.Y, chest.GlobalPosition.Z, 4f))
        { GD.Print("OYUK net reject chest"); return; }
        var loot = P.OpenChest(room.Poi.Id, room.Guardian, room.Tier, slot);
        foreach (var (item, n) in loot) actor.Bag.Add(item, n);
        if (loot.Count > 0) GD.Print($"OYUK net chest {room.Poi.Id}#{slot} peer {actor.Peer}");
        _d.Net.Flush();
    }

    /// <summary>Istemci: koruyucu yerelde yenildi, host'a bildir (kayit host'ta).</summary>
    void ForwardClear(OyukRoom room) => _d.Net?.Forward("oyukclear", w => w.Str(room.Poi.Id));

    /// <summary>Istemcide sandigi host'a gonderir (true); host ve tek oyuncuda false.</summary>
    bool ForwardChest(OyukRoom room, OyukChest chest)
    {
        if (_d.Net?.Forward("oyukchest", w => { w.Str(room.Poi.Id); w.VarU((uint)chest.Slot); }) != true) return false;
        if (P.IsCleared(room.Poi.Id)) { chest.Open(); _d.Hud.Hint(Loc.T("world.oyuk_chest_opened"), 1.5f); }
        else _d.Hud.Hint(Loc.T("world.oyuk_chest_locked_hint"), 1.5f);
        return true;
    }
}
