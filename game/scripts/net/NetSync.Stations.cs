using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Uretim, sandik ve yemek (GDD 5, 11; TEKNIK 5.2): istemcinin cantasi host'ta durur, bu yuzden cantayi degistiren bu
/// eylemler host'a gider. El kuyrugu oyuncu basinadir: host her uzak oyuncu icin bir CraftQueue isletir (urun onun
/// cantasina), durumu "craft" bolumuyle yalniz o oyuncuya doner (istemcide el kuyrugu islemez, gosterir). Istasyon
/// kuyrugu ortaktir: isi baslatan oyuncu sahibi olur, urun onun cantasina girer; sira bosalinca sahiplik duser, baska
/// oyuncunun suren isine eklenemez. Istasyon durumu istemciye kayit koprusuyle ("save:stations") gelir; istemcide
/// istasyon islemez. Sandiklar "chests" bolumuyle (tur + konum anahtari, icerik toplamlari) esitlenir; istemci yerelde
/// tasir (tahmin), host'un toplamlari duzeltir.
/// </summary>
public partial class NetSync
{
    readonly Dictionary<byte, CraftQueue> _hand = new();
    readonly Dictionary<StationQueue, byte> _stationOwner = new();
    const float ChestTol = 0.3f;

    void RegisterStations()
    {
        Register("craft", true, WriteCraft, ReadCraft);
        Register("chests", false, WriteChests, ReadChests);
        On("craft", HostCraft);
        On("craftcancel", HostCraftCancel);
        On("stcancel", HostStationCancel);
        On("chest", HostChest);
        On("eat", HostEat);
        On("graft", HostGraft); // tarim4: Asi Tezgahi (Director.Asi.cs DoGraft)
        RegisterSwellRoom(); // gece4: Kabartma Odasi (NetSync.SwellRoom.cs)
    }

    /// <summary>Asi Tezgahi: aktorun cantasi ve ortak Tohum Kaydi; aktor tezgahin erisiminde olmali.</summary>
    void HostGraft(NetActor a, NetReader r)
    {
        SeedLot Read()
        {
            string crop = r.Str();
            var g = new Genes { V = r.U8(), H = r.U8(), D = r.U8(), M = r.U8() };
            return D.State.Farm.Seeds.Lots.FirstOrDefault(l => l.Crop == crop && l.Genes.SameAs(g));
        }
        var recv = Read(); var donor = Read();
        int gene = r.U8();
        if (!StationsNear(a).Contains("asi_tezgahi")) { Reject(a, "graft station"); return; }
        if (recv == null || donor == null) { Reject(a, "graft lot"); return; }
        if (D.State.Farm.Graft(a.Bag, recv, donor, gene, out _) != GraftResult.Ok) Reject(a, "graft rule");
    }

    CraftQueue HandOf(NetActor a)
    {
        if (!_hand.TryGetValue(a.Peer, out var q)) _hand[a.Peer] = q = new CraftQueue(T.CraftQueueMax) { Known = D.State.Known };
        return q;
    }

    /// <summary>Host: uzak oyuncularin el kuyruklari kendi cantalarina isler (NetSync._Process).</summary>
    void TickStations(float dt)
    {
        if (!IsHost) return;
        foreach (var (peer, q) in _hand) if (Actors.TryGetValue(peer, out var a)) q.Tick(dt, a.Bag);
        foreach (var q in _stationOwner.Keys.Where(q => q.Active == null || !Actors.ContainsKey(_stationOwner[q])).ToList()) _stationOwner.Remove(q);
    }

    // ---------------------------------------------------------------- kancalar (baskasinin dosyasinda tek satir)

    /// <summary>
    /// Director.Craft: istemcide tarif host'a gider (true: cagiran donsun). Host'ta istasyon baska oyuncunun isiyle
    /// doluysa ipucu verir ve true doner (yerel siraya girmez); yoksa false.
    /// </summary>
    public bool ForwardCraft(RecipeDef r, int count)
    {
        if (IsHost)
        {
            if (CraftRules.IsHand(r) || D.NearStation(r.Station) is not { } st || !_stationOwner.ContainsKey(st.Queue)) return false;
            D.Hud.Hint($"{st.Def.Name}: başka oyuncunun işi sürüyor", 1.5f);
            return true;
        }
        Forward("craft", w => { w.Str(r.Id); w.VarU((uint)Mathf.Clamp(count, 1, 99)); });
        Sfx.Play("place", null, -6f);
        return true;
    }

    public bool ForwardCraftCancel(int index) => IsClient && Forward("craftcancel", w => w.VarU((uint)Mathf.Max(0, index)));

    public bool ForwardStationCancel(Station st) => IsClient && st != null && Forward("stcancel", w => w.Str(st.Def.Station ?? ""));

    /// <summary>Station._Process: true ise istasyon kendisi islemez (istemci; host'ta uzak sahibin isi onun cantasina isler).</summary>
    public bool TickStation(StationQueue q, float dt)
    {
        if (IsClient) return true;
        if (!_stationOwner.TryGetValue(q, out var peer) || !Actors.TryGetValue(peer, out var a)) return false;
        q.Tick(dt, a.Bag);
        return true;
    }

    /// <summary>Director.Economy TransferItems: sandik tasimasi host'a da gider (yerel tasima tahmin). Araba yuvalari yerel kalir.</summary>
    public void ForwardChest(Inventory store, string item, bool take, int n)
    {
        if (!IsClient || n <= 0) return;
        var f = D.Furniture.FirstOrDefault(x => GodotObject.IsInstanceValid(x) && x.Storage == store);
        if (f == null) return;
        var p = f.GlobalPosition;
        Forward("chest", w => { w.Str(f.Def.Id); w.Pos(p.X, p.Y, p.Z, Cfg.PosPerMetre); w.U8(take ? (byte)1 : (byte)0); w.Str(item); w.VarU((uint)n); });
    }

    /// <summary>Director.Menus EatFood: yemek istemcinin kendi yuvasina girer; host'taki cantadan bir adet duser.</summary>
    public void ForwardEat(string food)
    {
        if (IsClient) Forward("eat", w => w.Str(food));
    }

    // ---------------------------------------------------------------- host istekleri

    HashSet<string> StationsNear(NetActor a)
    {
        var p = ActorPos(a);
        float reach = T.StationReach + 1.5f; // gecikme payi
        return D.Stations.Where(s => GodotObject.IsInstanceValid(s) && s.Def.Station != null && new Vector2(s.GlobalPosition.X - p.X, s.GlobalPosition.Z - p.Z).Length() < reach)
            .Select(s => s.Def.Station).ToHashSet();
    }

    void HostCraft(NetActor a, NetReader r)
    {
        string id = r.Str();
        int count = Mathf.Clamp((int)r.VarU(), 1, 99);
        var rec = T.Recipes.FirstOrDefault(x => x.Id == id);
        if (rec == null) { Reject(a, "craft recipe"); return; }
        if (CraftRules.NeedStation(rec, StationsNear(a)) != null) { Reject(a, "craft station"); return; }
        if (CraftRules.IsHand(rec))
        {
            if (!HandOf(a).Enqueue(rec, count, a.Bag)) Reject(a, "craft hand");
            return;
        }
        var p = ActorPos(a);
        var st = D.Stations.Where(s => GodotObject.IsInstanceValid(s) && s.Def.Station == rec.Station).OrderBy(s => s.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (st == null) { Reject(a, "craft no station"); return; }
        var q = st.Queue;
        bool mine = _stationOwner.TryGetValue(q, out var owner) && owner == a.Peer;
        if (q.Active != null && !mine) { Reject(a, "craft station busy"); return; }
        int queued = 0;
        for (int i = 0; i < count && q.Enqueue(rec, a.Bag); i++) queued++;
        if (queued == 0) { Reject(a, "craft station"); return; }
        _stationOwner[q] = a.Peer;
    }

    void HostCraftCancel(NetActor a, NetReader r)
    {
        int i = (int)r.VarU();
        if (_hand.TryGetValue(a.Peer, out var q)) q.Cancel(i, a.Bag);
    }

    void HostStationCancel(NetActor a, NetReader r)
    {
        string station = r.Str();
        var q = D.Stations.FirstOrDefault(s => GodotObject.IsInstanceValid(s) && s.Def.Station == station)?.Queue;
        if (q == null || !_stationOwner.TryGetValue(q, out var owner) || owner != a.Peer) { Reject(a, "stcancel owner"); return; }
        q.Cancel(a.Bag);
        _stationOwner.Remove(q);
    }

    void HostChest(NetActor a, NetReader r)
    {
        string def = r.Str();
        var (x, y, z) = r.Pos(Cfg.PosPerMetre);
        bool take = r.U8() == 1;
        string item = r.Str();
        int n = (int)r.VarU();
        var at = new Vector3(x, y, z);
        var f = D.Furniture.FirstOrDefault(c => GodotObject.IsInstanceValid(c) && c.Storage != null && c.Def.Id == def && c.GlobalPosition.DistanceTo(at) < ChestTol);
        if (f == null) { Reject(a, "chest missing"); return; }
        if (!NetRules.InRange(a, x, y, z, Cfg.Ranges.Interact + 2f)) { Reject(a, "chest range"); return; }
        if (take) f.Storage.MoveTo(a.Bag, item, n);
        else a.Bag.MoveTo(f.Storage, item, n);
    }

    void HostEat(NetActor a, NetReader r)
    {
        string food = r.Str();
        if (!T.IsFood(food) || !a.Bag.Remove(food, 1)) Reject(a, "eat");
    }

    // ---------------------------------------------------------------- bolumler

    void WriteCraft(NetWriter w, int peer)
    {
        var q = IsClient || peer == 0 ? D.State.Craft : _hand.GetValueOrDefault((byte)peer); // istemcide kendi (host'tan yuklenen) kuyrugu
        var s = q?.Save() ?? new CraftSave();
        w.U8(s.Blocked ? (byte)1 : (byte)0);
        w.VarU((uint)s.Jobs.Count);
        foreach (var j in s.Jobs) { w.Str(j.Recipe); w.VarU((uint)j.Left); w.U8((byte)Mathf.Clamp(Mathf.RoundToInt(j.Progress * 250f), 0, 250)); }
    }

    void ReadCraft(NetReader r)
    {
        var s = new CraftSave { Blocked = r.U8() == 1 };
        int n = (int)r.VarU();
        for (int i = 0; i < n; i++) s.Jobs.Add(new CraftJobSave { Recipe = r.Str(), Left = (int)r.VarU(), Progress = r.U8() / 250f });
        D.State.Craft.Load(s, id => T.Recipes.FirstOrDefault(x => x.Id == id && CraftRules.IsHand(x)));
    }

    IEnumerable<Furniture> Chests => D.Furniture.Where(f => GodotObject.IsInstanceValid(f) && f.Storage != null)
        .OrderBy(f => NetQuant.Pos(f.GlobalPosition.X, Cfg.PosPerMetre)).ThenBy(f => NetQuant.Pos(f.GlobalPosition.Z, Cfg.PosPerMetre))
        .ThenBy(f => NetQuant.Pos(f.GlobalPosition.Y, Cfg.PosPerMetre)).ThenBy(f => f.Def.Id, System.StringComparer.Ordinal);

    void WriteChests(NetWriter w, int peer)
    {
        var l = Chests.ToList();
        w.VarU((uint)l.Count);
        foreach (var f in l)
        {
            var p = f.GlobalPosition;
            w.Str(f.Def.Id);
            w.Pos(p.X, p.Y, p.Z, Cfg.PosPerMetre);
            NetInventory.Write(w, f.Storage);
        }
    }

    void ReadChests(NetReader r)
    {
        int n = (int)r.VarU();
        for (int i = 0; i < n; i++)
        {
            string def = r.Str();
            var (x, y, z) = r.Pos(Cfg.PosPerMetre);
            var totals = NetInventory.ReadTotals(r);
            var at = new Vector3(x, y, z);
            var f = D.Furniture.FirstOrDefault(c => GodotObject.IsInstanceValid(c) && c.Storage != null && c.Def.Id == def && c.GlobalPosition.DistanceTo(at) < ChestTol);
            if (f != null) NetInventory.Apply(f.Storage, totals);
        }
    }
}
