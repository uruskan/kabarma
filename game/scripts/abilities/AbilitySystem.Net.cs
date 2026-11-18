using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Gucler, ortak oyun (CLAUDE.md "Ortak oyuna baglanma"). Host yetkili:
/// - "abilities" bolumu: acik gucler, tamamlanan Kadim Taslar, kurulu fenerler ve yakildiklari gun (dunya durumu).
/// - "abil" istegi: istemci gucunu kullanir; host aktorun Sarj ve beklemesini kendi aynasinda denetler, etkiyi
///   dunyada (dusman, hayalet, hologram, sis) kurar. Atilma ve Dev Mod'un hareketi ve boyu kullananin makinesindedir.
/// - "fener" istegi: istemci feneri yakar (menzil host'ta).
/// Kadim Tas denemesi M3'te host'un oyuncusu icindir; istemci tasta ipucu gorur (M5: ekip denemesi).
/// </summary>
public partial class AbilitySystem
{
    readonly Dictionary<byte, AbilityLoadout> _peers = new();

    /// <summary>Director.InitNet'ten sonra (ertelenmis) bir kez.</summary>
    public void BindNet()
    {
        var net = D.Net;
        if (net == null) return;
        net.Register("abilities", false, WriteNet, ReadNet);
        net.On("abil", HostUse);
        net.On("fener", HostFener);
        GD.Print($"ABILITY net bound host={net.IsHost}");
    }

    /// <summary>Dunya durumu degisti (host her 5 Hz'te bolumu karsilastirir; burada yalniz isaret).</summary>
    public void SyncNet() { }

    /// <summary>Yuvaya takar (yerel donanim; istemcide de yereldir, host kullanimda aynasina yazar).</summary>
    public string Equip(int slot, string id) => AbilityRules.Equip(F, D.State.Abilities, D.State.Atalar.Ultis, L, slot, id);

    void WriteNet(NetWriter w, int peer)
    {
        var a = D.State.Abilities;
        var unlocked = a.Unlocked.OrderBy(x => x).ToList();
        w.VarU((uint)unlocked.Count);
        foreach (var u in unlocked) w.Str(u);
        var done = D.State.KadimTas.Done.OrderBy(x => x).ToList();
        w.VarU((uint)done.Count);
        foreach (var d in done) w.Str(d);
        var f = D.State.Fener;
        w.VarU((uint)f.NextId);
        var sites = f.Sites.OrderBy(s => s.Id).ToList();
        w.VarU((uint)sites.Count);
        foreach (var s in sites)
        {
            w.Str(s.Id); w.F32(s.X); w.F32(s.Y); w.F32(s.Z); w.F32(s.Yaw);
            w.VarU((uint)(f.LitDay.TryGetValue(s.Id, out var day) ? day + 1 : 0));
            w.VarU((uint)f.LitCount.GetValueOrDefault(s.Id));
        }
    }

    void ReadNet(NetReader r)
    {
        var a = D.State.Abilities;
        int n = (int)r.VarU();
        var unlocked = new List<string>();
        for (int i = 0; i < n; i++) unlocked.Add(r.Str());
        int dn = (int)r.VarU();
        var done = new List<string>();
        for (int i = 0; i < dn; i++) done.Add(r.Str());
        var f = D.State.Fener;
        int next = (int)r.VarU();
        int sn = (int)r.VarU();
        var sites = new List<FenerSite>();
        var lit = new Dictionary<string, int>();
        var count = new Dictionary<string, int>();
        for (int i = 0; i < sn; i++)
        {
            var s = new FenerSite { Id = r.Str(), X = r.F32(), Y = r.F32(), Z = r.F32(), Yaw = r.F32() };
            int day = (int)r.VarU() - 1;
            int c = (int)r.VarU();
            if (day >= 0) { lit[s.Id] = day; count[s.Id] = c; }
            sites.Add(s);
        }
        bool newPower = unlocked.Except(a.Unlocked).Any();
        bool stonesChanged = !done.OrderBy(x => x).SequenceEqual(D.State.KadimTas.Done.OrderBy(x => x));
        bool fenersChanged = !sites.Select(s => s.Id).SequenceEqual(f.Sites.OrderBy(s => s.Id).Select(s => s.Id));
        a.Unlocked = unlocked;
        D.State.KadimTas.Done = done;
        f.NextId = next; f.Sites = sites; f.LitDay = lit; f.LitCount = count;
        if (newPower) AbilityRules.AutoEquip(F, a, L, D.State.Atalar.Ultis);
        if (stonesChanged) Stones?.Rebuild();
        if (fenersChanged) Drill?.RebuildFeners();
    }

    /// <summary>Istemci: gucun etkisini host'a gonderir (true). Host ya da tek oyuncuda false.</summary>
    bool ForwardUse(string id, Vector3 at, Vector3 aim)
    {
        if (D.Net?.IsClient != true) return false;
        var def = F.Def(id);
        // Kullananin kendisindeki etki (hareket, boy) burada; dunyadaki etki host'ta.
        if (def.Id is "atilma" or "dev_mod") Apply(def, at, aim, local: true);
        return D.Net.Forward("abil", w => { w.Str(id); w.F32(at.X); w.F32(at.Y); w.F32(at.Z); w.F32(aim.X); w.F32(aim.Z); });
    }

    void HostUse(NetActor actor, NetReader r)
    {
        string id = r.Str();
        var at = new Vector3(r.F32(), r.F32(), r.F32());
        var aim = new Vector3(r.F32(), 0, r.F32());
        var def = F.Def(id);
        if (def == null || !NetRules.InRange(actor, at.X, at.Y, at.Z, 4f)) return;
        if (!_peers.TryGetValue(actor.Peer, out var l)) _peers[actor.Peer] = l = new AbilityLoadout();
        AbilityRules.Normalize(F, l);
        if (def.IsUlti) { if (!D.State.Atalar.Ultis.Contains(id)) return; l.Ulti = id; l.Meter = F.UltiMeter.Max; } // olcer istemcide dolar (M5: host olcer)
        else { if (!D.State.Abilities.Unlocked.Contains(id)) return; l.Slots[0] = id; }
        if (AbilityRules.Use(F, l, id) == null) { GD.Print($"ABILITY reject peer={actor.Peer} {id}"); return; }
        Apply(def, at, aim.LengthSquared() > 0.001f ? aim.Normalized() : Vector3.Forward, local: false);
        GD.Print($"ABILITY peer={actor.Peer} use {id}");
    }

    void HostFener(NetActor actor, NetReader r)
    {
        string id = r.Str();
        var s = D.State.Fener.Sites.FirstOrDefault(x => x.Id == id);
        if (s == null || !NetRules.InRange(actor, s.X, s.Y, s.Z, TatbikatSystem.FenerReach + 1.5f)) return;
        Drill.Light(id);
    }

    /// <summary>Host'ta istemci aynalarinin zamani (Sarj ve bekleme).</summary>
    void TickPeers(float dt)
    {
        foreach (var l in _peers.Values) AbilityRules.Tick(F, l, dt);
    }
}
