using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya cogaltmasi (TEKNIK 4, 5.3): kaynak dugumleri (kaya, tutam), yapi parcalari, yerlestirilebilirler (Ocak,
/// istasyon, esya) ve kisisel canta. Kimlik: kaya ve tutam ayni tohumdan kurulan dunyadaki ilk sirasi; yapi ve
/// yerlestirilebilir ise (tur, nicemlenmis donusum): sonradan katilan istemci ayni anahtarla uzlasir.
/// Istekler (istemci → host): mine, mow, place, remove, repair, placeitem. Host dogrular (menzil, bekleme, canta),
/// sonra oyunun kendi genel girisini cagirir (Rock.Mine, World.TakeTuft, Farm.Harvest, BuildSystem.PlaceAt, Director.Place*).
/// </summary>
public partial class NetSync
{
    /// <summary>Kaya: dunyanin ilk listesindeki sirasi ve ilk birim sayisi.</summary>
    sealed record RockRef(Rock Rock, Vector3 InitPos, int Init);

    readonly List<RockRef> _rocks = new();
    readonly List<World.Tuft> _tufts = new();
    readonly List<int> _tuftInit = new();
    readonly Dictionary<int, float> _mineAcc = new();
    float _mineFlush;
    const int PieceQ = 1024;   // yapi parcasi konumu: 1 mm
    const int ItemQ = 256;     // yerlestirilebilir konumu

    ToolFile T => D.Data.Tool;

    void RegisterWorld()
    {
        foreach (var r in D.World.Rocks) _rocks.Add(new RockRef(r, r.GlobalPosition, r.Stones));
        foreach (var t in D.World.Tufts) { _tufts.Add(t); _tuftInit.Add(t.Left); }

        Register(new NetInventory.Section("bag", p => IsHost ? (p == 0 ? D.State.Backpack : Actors.GetValueOrDefault((byte)p)?.Bag) : D.State.Backpack, () => D.State.Backpack));
        Register("rocks", false, WriteRocks, ReadRocks);
        Register("tufts", false, WriteTufts, ReadTufts);
        Register("placed", false, WritePlaced, ReadPlaced);
        if (D.Build != null) Register("pieces", false, WritePieces, ReadPieces);
        if (D.Build != null) RegisterYapi(); // NetSync.Yapi.cs: malzeme, plan, yukselt

        On("mine", HostMine);
        On("mow", HostMow);
        On("place", HostPlace);
        On("remove", HostRemove);
        On("repair", HostRepair);
        On("placeitem", HostPlaceItem);
    }

    // ---------------------------------------------------------------- yardimcilar

    static void WriteXf(NetWriter w, Transform3D xf, int q)
    {
        w.Pos(xf.Origin.X, xf.Origin.Y, xf.Origin.Z, q);
        var r = xf.Basis.GetRotationQuaternion().Normalized();
        if (r.W < 0) r = -r; // kanonik isaret
        w.U16((ushort)(short)Mathf.RoundToInt(r.X * 32767)); w.U16((ushort)(short)Mathf.RoundToInt(r.Y * 32767));
        w.U16((ushort)(short)Mathf.RoundToInt(r.Z * 32767)); w.U16((ushort)(short)Mathf.RoundToInt(r.W * 32767));
    }

    static Transform3D ReadXf(NetReader r, int q)
    {
        var (x, y, z) = r.Pos(q);
        var rot = new Quaternion((short)r.U16() / 32767f, (short)r.U16() / 32767f, (short)r.U16() / 32767f, (short)r.U16() / 32767f).Normalized();
        return new Transform3D(new Basis(rot), new Vector3(x, y, z));
    }

    static string Key(string id, Transform3D xf, int q)
    {
        var w = new NetWriter(48);
        WriteXf(w, xf, q);
        return id + "|" + System.Convert.ToHexString(w.Span);
    }

    Vector3 ActorPos(NetActor a) => new(a.Last.X, a.Last.Y, a.Last.Z);

    bool Reject(NetActor a, string what)
    {
        a.Rejected++;
        if (Main.Args.ContainsKey("netdebug")) GD.Print($"NET reject peer={a.Peer} {what}");
        return false;
    }

    // ---------------------------------------------------------------- kaya ve tutam

    void WriteRocks(NetWriter w, int peer)
    {
        var l = new List<(int i, int s)>();
        for (int i = 0; i < _rocks.Count; i++)
        {
            var rk = _rocks[i];
            int s = GodotObject.IsInstanceValid(rk.Rock) ? rk.Rock.Stones : 0;
            if (s != rk.Init) l.Add((i, s));
        }
        w.VarU((uint)l.Count);
        foreach (var (i, s) in l) { w.VarU((uint)i); w.VarU((uint)s); }
    }

    void ReadRocks(NetReader r)
    {
        int n = (int)r.VarU();
        for (int k = 0; k < n; k++)
        {
            int i = (int)r.VarU(), s = (int)r.VarU();
            if (i >= _rocks.Count) continue;
            var rock = _rocks[i].Rock;
            if (!GodotObject.IsInstanceValid(rock) || rock.Stones <= s) continue;
            rock.Mine(rock.Stones - s);
            if (rock.Stones <= 0) D.World.RemoveRock(rock);
        }
    }

    void WriteTufts(NetWriter w, int peer)
    {
        var l = new List<(int i, int s)>();
        for (int i = 0; i < _tufts.Count; i++) if (_tufts[i].Left != _tuftInit[i]) l.Add((i, _tufts[i].Left));
        w.VarU((uint)l.Count);
        foreach (var (i, s) in l) { w.VarU((uint)i); w.VarU((uint)s); }
    }

    void ReadTufts(NetReader r)
    {
        int n = (int)r.VarU();
        for (int k = 0; k < n; k++)
        {
            int i = (int)r.VarU(), s = (int)r.VarU();
            if (i < _tufts.Count && _tufts[i].Left > s) D.World.TakeTuft(_tufts[i], _tufts[i].Left - s);
        }
    }

    // ---------------------------------------------------------------- yapi parcalari

    IEnumerable<BuildPiece> LivePieces => D.Build.System.Pieces.Where(p => GodotObject.IsInstanceValid(p) && !p.IsQueuedForDeletion());

    /// <summary>Kanonik sira: alttan uste (destek once kurulsun), sonra x, z, tur.</summary>
    static IOrderedEnumerable<(string id, Transform3D xf, float hp, BuildPiece p)> Canon(IEnumerable<(string id, Transform3D xf, float hp, BuildPiece p)> l) =>
        l.OrderBy(e => NetQuant.Pos(e.xf.Origin.Y, PieceQ)).ThenBy(e => NetQuant.Pos(e.xf.Origin.X, PieceQ)).ThenBy(e => NetQuant.Pos(e.xf.Origin.Z, PieceQ)).ThenBy(e => e.id, System.StringComparer.Ordinal);

    void WritePieces(NetWriter w, int peer)
    {
        var l = Canon(LivePieces.Select(p => (p.Def.Id, p.GlobalTransform, p.Hp, p))).ToList();
        w.VarU((uint)l.Count);
        foreach (var (id, xf, hp, _) in l) { w.Str(id); WriteXf(w, xf, PieceQ); w.U16((ushort)Mathf.Clamp(Mathf.RoundToInt(hp * 10f), 0, 65535)); }
    }

    void ReadPieces(NetReader r)
    {
        int n = (int)r.VarU();
        var want = new List<(string id, Transform3D xf, float hp, string key)>(n);
        for (int i = 0; i < n; i++)
        {
            string id = r.Str();
            var xf = ReadXf(r, PieceQ);
            float hp = r.U16() / 10f;
            want.Add((id, xf, hp, Key(id, xf, PieceQ)));
        }
        var sys = D.Build.System;
        var have = new Dictionary<string, BuildPiece>();
        foreach (var p in LivePieces.ToList())
        {
            var k = Key(p.Def.Id, p.GlobalTransform, PieceQ);
            if (want.Any(x => x.key == k) && !have.ContainsKey(k)) have[k] = p;
            else sys.Remove(p);
        }
        foreach (var (id, xf, hp, key) in want)
        {
            if (!have.TryGetValue(key, out var p))
            {
                p = sys.PlaceAt(id, xf, out var why);
                if (p == null) { GD.PushWarning($"NET parca kurulamadi {id} {xf.Origin}: {why}"); continue; }
                p.GlobalTransform = xf; // yakalama ayni yere oturtmazsa host'un donusumu gecerli
            }
            p.Hp = hp;
        }
    }

    // ---------------------------------------------------------------- yerlestirilebilirler

    enum PlacedKind : byte { Ocak, Station, Furniture }

    IEnumerable<(PlacedKind kind, string id, Transform3D xf, Node3D node)> LivePlaced()
    {
        if (D.Ocak != null && GodotObject.IsInstanceValid(D.Ocak)) yield return (PlacedKind.Ocak, "ocak", D.Ocak.GlobalTransform, D.Ocak);
        foreach (var st in D.Stations.Where(GodotObject.IsInstanceValid)) yield return (PlacedKind.Station, st.Def.Id, st.GlobalTransform, st);
        foreach (var f in D.Furniture.Where(GodotObject.IsInstanceValid)) yield return (PlacedKind.Furniture, f.Def.Id, f.GlobalTransform, f);
    }

    static Transform3D Flat(Transform3D xf) => new(new Basis(Vector3.Up, xf.Basis.GetEuler().Y), xf.Origin);

    void WritePlaced(NetWriter w, int peer)
    {
        var l = LivePlaced().Select(e => (e.kind, e.id, xf: Flat(e.xf))).OrderBy(e => e.kind).ThenBy(e => Key(e.id, e.xf, ItemQ), System.StringComparer.Ordinal).ToList();
        w.VarU((uint)l.Count);
        foreach (var (kind, id, xf) in l) { w.U8((byte)kind); w.Str(id); w.Pos(xf.Origin.X, xf.Origin.Y, xf.Origin.Z, ItemQ); w.Angle16(xf.Basis.GetEuler().Y); }
    }

    void ReadPlaced(NetReader r)
    {
        int n = (int)r.VarU();
        var want = new List<(PlacedKind kind, string id, Vector3 at, float yaw, string key)>();
        for (int i = 0; i < n; i++)
        {
            var kind = (PlacedKind)r.U8();
            string id = r.Str();
            var (x, y, z) = r.Pos(ItemQ);
            float yaw = r.Angle16();
            var at = new Vector3(x, y, z);
            want.Add((kind, id, at, yaw, Key(id, new Transform3D(new Basis(Vector3.Up, yaw), at), ItemQ)));
        }
        var have = new HashSet<string>();
        foreach (var (kind, id, xf, node) in LivePlaced().ToList())
        {
            var k = Key(id, Flat(xf), ItemQ);
            if (want.Any(x => x.key == k)) { have.Add(k); continue; }
            if (kind == PlacedKind.Station) D.Stations.Remove((Station)node);
            else if (kind == PlacedKind.Furniture) D.Furniture.Remove((Furniture)node);
            else continue; // Ocak sokulmez
            node.QueueFree();
        }
        foreach (var (kind, id, at, yaw, key) in want)
        {
            if (have.Contains(key)) continue;
            var def = T.Placeables.FirstOrDefault(p => p.Id == id);
            switch (kind)
            {
                case PlacedKind.Ocak: if (D.Ocak == null) D.PlaceOcak(at); break;
                case PlacedKind.Station: if (def != null) D.PlaceStation(def, at, yaw); break;
                case PlacedKind.Furniture: if (def != null) D.PlaceFurniture(def, at, yaw); break;
            }
        }
    }

    // ---------------------------------------------------------------- istemci kancalari (Forward)

    /// <summary>Kazma (Capa.Kazma.cs): istemcide kemirme birikir, 10 Hz host'a gider; kaya ve canta host'tan doner.</summary>
    public bool ForwardMine(Rock rock, float amount, Vector3 hit)
    {
        if (IsHost) return false;
        int i = _rocks.FindIndex(x => x.Rock == rock);
        if (i < 0 || !Joined) return true;
        _mineAcc[i] = _mineAcc.GetValueOrDefault(i) + amount;
        if (_mineFx <= 0) { _mineFx = 0.12f; Fx.Burst(hit, Palette.Rock, 4, 2.5f, 0.05f, 0.35f); }
        return true;
    }

    float _mineFx;

    void TickWorld(float dt)
    {
        if (IsHost) return;
        _mineFx -= dt;
        _mineFlush -= dt;
        if (_mineFlush > 0 || _mineAcc.Count == 0) return;
        _mineFlush = 0.1f;
        foreach (var (i, amount) in _mineAcc) Forward("mine", w => { w.U16((ushort)i); w.F32(amount); });
        _mineAcc.Clear();
    }

    /// <summary>Bicme (Capa.Bicme.cs): ekin, agac, tutam host'ta.</summary>
    public bool ForwardMow(Vector3 aim) => Forward("mow", w => w.Pos(aim.X, aim.Y, aim.Z, Cfg.PosPerMetre));

    /// <summary>Insa Plani ile parca kurma (BuildBridge.Impl.cs): hayaletin donusumu host'a.</summary>
    public bool ForwardPlace(BuildSystem sys)
    {
        if (IsHost) return false;
        var cur = sys.Current;
        if (cur == null || !cur.Valid) return true;
        return Forward("place", w => { w.Str(cur.Def.Id); w.F32(cur.Xf.Origin.X); w.F32(cur.Xf.Origin.Y); w.F32(cur.Xf.Origin.Z); var q = cur.Xf.Basis.GetRotationQuaternion(); w.F32(q.X); w.F32(q.Y); w.F32(q.Z); w.F32(q.W); });
    }

    /// <summary>Sok (BuildBridge.Deconstruct) ve Onar (BuildBridge.Repair): hedefin anahtari host'a.</summary>
    public bool ForwardRemove(Node3D t) => ForwardTarget("remove", t);
    public bool ForwardRepair(Node3D t) => ForwardTarget("repair", t);

    bool ForwardTarget(string req, Node3D t)
    {
        if (IsHost) return false;
        if (t == null) return true;
        return Forward(req, w =>
        {
            byte kind = t switch { BuildPiece => 0, Station => 1, Furniture => 2, _ => 9 };
            w.U8(kind);
            w.Str(t switch { BuildPiece bp => bp.Def.Id, Station st => st.Def.Id, Furniture f => f.Def.Id, _ => "" });
            w.F32(t.GlobalPosition.X); w.F32(t.GlobalPosition.Y); w.F32(t.GlobalPosition.Z);
        });
    }

    /// <summary>Kemerden yerlestirme (Capa.Insa.cs TryPlace): Ocak, istasyon, Sandik, Uyku Tulumu, Mesale.</summary>
    public bool ForwardPlaceItem(string id, Vector3 at, float yaw) =>
        Forward("placeitem", w => { w.Str(id); w.F32(at.X); w.F32(at.Y); w.F32(at.Z); w.F32(yaw); });

    // ---------------------------------------------------------------- host isleyicileri

    void HostMine(NetActor a, NetReader r)
    {
        int i = r.U16();
        float amount = r.F32();
        if (i >= _rocks.Count) { Reject(a, "mine idx"); return; }
        var rock = _rocks[i].Rock;
        if (!GodotObject.IsInstanceValid(rock) || rock.Stones <= 0) return;
        if (!NetRules.InRange(a, rock.Center.X, rock.Center.Y, rock.Center.Z, Cfg.Ranges.Mine + rock.Radius)) { Reject(a, "mine range"); return; }
        // 10 Hz'te bir paket; kemirme hizinin bir buckuk katindan fazlasi sayilmaz (TEKNIK 5.5).
        amount = Mathf.Clamp(amount, 0f, T.MineRate * 0.15f * Cfg.SpeedSlack);
        int got = rock.Mine(amount);
        if (got > 0) a.Bag.Add(rock.Item, got);
        if (rock.Stones <= 0) D.World.RemoveRock(rock);
    }

    void HostMow(NetActor a, NetReader r)
    {
        var (x, y, z) = r.Pos(Cfg.PosPerMetre);
        var aim = new Vector3(x, y, z);
        if (!NetRules.InRange(a, x, y, z, Cfg.Ranges.Mow)) { Reject(a, "mow range"); return; }
        if (!a.Cooldown(NetReq.Mow, Now, Cfg.Cooldowns.Mow)) { Reject(a, "mow cooldown"); return; }
        // Capa.Mow ile ayni hedefler ve ayni sayilar (tool.json), canta istekte bulununinki.
        foreach (var plot in D.State.Farm.AllPlots.ToList()) // tarim: yabani bitkiler dahil
        {
            var c = D.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > T.MowRadius) continue;
            if (plot.Wild && new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > D.Data.Crops.Genetics.Wild.MowRadius) continue; // tarim: Capa.Bicme ile ayni
            var h = D.State.Farm.HarvestFull(plot.X, plot.Y, D.State.Rng); // Capa.Bicme ile ayni (tarim: urun ekine gore)
            if (h == null) continue;
            D.FarmView.OnHarvestedBy(h, c);
            a.Bag.Add(h.Item, h.Yield, h.Stars); // Ustalik carpani kisisel, host'ta uzak oyuncu icin yok (bilinen eksik)
            a.Bag.Add(h.SeedItem, h.Seeds);
        }
        foreach (var tp in D.World.TreePositions.Where(t => new Vector2(t.X - aim.X, t.Z - aim.Z).Length() < T.TreeRadius))
            a.Bag.Add("wood", T.WoodPerChop);
        foreach (var tuft in D.World.Tufts.Where(t => t.Left > 0 && new Vector2(t.Position.X - aim.X, t.Position.Z - aim.Z).Length() < T.MowRadius).ToList())
        {
            int got = D.World.TakeTuft(tuft, tuft.PerHit);
            if (got > 0) a.Bag.Add(tuft.Item, got);
        }
    }

    bool InClaim(Vector3 p) => D.Ocak != null && Structures.InClaim(p.X - D.OcakPos.X, p.Z - D.OcakPos.Z, Ocak.ClaimRadius);

    void HostPlace(NetActor a, NetReader r)
    {
        string id = r.Str();
        var o = new Vector3(r.F32(), r.F32(), r.F32());
        var q = new Quaternion(r.F32(), r.F32(), r.F32(), r.F32());
        if (D.Build == null || !D.Build.Handles(id)) { Reject(a, "place id"); return; }
        if (!q.IsNormalized()) q = q.Normalized();
        if (!NetRules.InRange(a, o.X, o.Y, o.Z, Cfg.Ranges.Build)) { Reject(a, "place range"); return; }
        if (!a.Cooldown(NetReq.Place, Now, Cfg.Cooldowns.Place)) { Reject(a, "place cooldown"); return; }
        // M2 (GDD §7): yapi parcasi tapu disinda da kurulur; sokumde iadesi %50.
        int cost = D.Build.CostOf(id);
        if (a.Bag.Count("wood") < cost) { Reject(a, "place wood"); return; }
        var piece = D.Build.System.PlaceAt(id, new Transform3D(new Basis(q), o), out var why);
        if (piece == null) { Reject(a, "place invalid " + why); return; }
        a.Bag.Remove("wood", cost);
        Flush();
    }

    /// <summary>Hedef anahtari: tur + konum; 5 cm icindeki ayni turden nesne.</summary>
    Node3D FindTarget(NetReader r)
    {
        byte kind = r.U8();
        string id = r.Str();
        var at = new Vector3(r.F32(), r.F32(), r.F32());
        const float Tol = 0.05f;
        return kind switch
        {
            0 => D.Build == null ? null : LivePieces.FirstOrDefault(p => p.Def.Id == id && p.GlobalPosition.DistanceTo(at) < Tol),
            1 => D.Stations.FirstOrDefault(s => GodotObject.IsInstanceValid(s) && s.Def.Id == id && s.GlobalPosition.DistanceTo(at) < Tol),
            2 => D.Furniture.FirstOrDefault(f => GodotObject.IsInstanceValid(f) && f.Def.Id == id && f.GlobalPosition.DistanceTo(at) < Tol),
            _ => null,
        };
    }

    void HostRemove(NetActor a, NetReader r)
    {
        var t = FindTarget(r);
        if (t == null) { Reject(a, "remove target"); return; }
        if (!NetRules.InRange(a, t.GlobalPosition.X, t.GlobalPosition.Y, t.GlobalPosition.Z, Cfg.Ranges.Build)) { Reject(a, "remove range"); return; }
        // BuildBridge.Deconstruct ile ayni kural: iade %100 (tapu icinde), uretim suruyorsa ya da Sandik doluysa sokulmez.
        var rules = D.Data.Economy.Build;
        var cost = t switch
        {
            BuildPiece bp => D.Build.PieceCost(bp), // malzemesine gore (GDD §7)
            Station st => new Dictionary<string, int> { [st.Def.Id] = 1 },
            Furniture f => new Dictionary<string, int> { [f.Def.Id] = 1 },
            _ => new Dictionary<string, int>(),
        };
        if (t is Station s2 && s2.Queue.Active != null) { Reject(a, "remove busy"); return; }
        if (t is Furniture f2 && f2.Storage != null && !f2.Storage.IsEmpty) { Reject(a, "remove chest"); return; }
        var refund = t is BuildPiece bpr ? D.Build.RefundOf(bpr, cost) : Structures.Refund(cost, rules);
        if (!NetRules.Fits(a.Bag, refund, T.StackOf)) { Reject(a, "remove bag full"); return; }
        switch (t)
        {
            case BuildPiece bp: D.Build.System.Remove(bp); break;
            case Station st: D.Stations.Remove(st); st.QueueFree(); break;
            case Furniture f: D.Furniture.Remove(f); f.QueueFree(); break;
        }
        foreach (var (item, n) in refund) a.Bag.Add(item, n);
        Flush();
    }

    void HostRepair(NetActor a, NetReader r)
    {
        if (FindTarget(r) is not BuildPiece bp) { Reject(a, "repair target"); return; }
        if (!NetRules.InRange(a, bp.GlobalPosition.X, bp.GlobalPosition.Y, bp.GlobalPosition.Z, Cfg.Ranges.Build)) { Reject(a, "repair range"); return; }
        float max = D.Build.System.MaxHp(bp);
        var cost = Structures.RepairCost(D.Build.PieceCost(bp), bp.Hp, max, D.Data.Economy.Build);
        if (cost.Count == 0 || !NetRules.CanPay(a.Bag, cost)) { Reject(a, "repair cost"); return; }
        foreach (var (item, n) in cost) a.Bag.Remove(item, n);
        bp.Hp = max;
    }

    void HostPlaceItem(NetActor a, NetReader r)
    {
        string id = r.Str();
        var at = new Vector3(r.F32(), r.F32(), r.F32());
        float yaw = r.F32();
        var def = T.Placeables.FirstOrDefault(p => p.Id == id);
        if (def == null || a.Bag.Count(id) < 1) { Reject(a, "placeitem bag"); return; }
        if (!NetRules.InRange(a, at.X, at.Y, at.Z, Cfg.Ranges.Build)) { Reject(a, "placeitem range"); return; }
        if (!a.Cooldown(NetReq.PlaceItem, Now, Cfg.Cooldowns.Place)) { Reject(a, "placeitem cooldown"); return; }
        // Capa.Insa.cs UpdateGhost ile ayni tapu kurali: Ocak yalniz bir tane; digerleri tapu icinde, Ocak'a cok yakin degil.
        bool ok = id == "ocak" ? D.Ocak == null : D.Ocak != null && at.DistanceTo(D.OcakPos) <= Ocak.ClaimRadius && at.DistanceTo(D.OcakPos) > 2.2f;
        if (!ok) { Reject(a, "placeitem claim"); return; }
        at = D.World.Ground(at);
        a.Bag.Remove(id, 1);
        // Capa.Insa.cs TryPlace ile ayni dallanma.
        if (D.PlacePower(def, at, yaw)) { Flush(); return; } // guc akisi: guc parcasi "power" bolumuyle cogalir (PowerSystem.Net.cs)
        if (D.PlaceAbility(def, at, yaw)) { Flush(); return; } // gucler akisi: Maya Feneri "abilities" bolumuyle cogalir
        bool furniture = def.Slots > 0 || def.Spawn || def.LightRange > 0;
        if (PenSystem.IsPenProp(def.Id) || (def.Station == null && def.Id != "ocak" && !furniture)) D.PlaceProp(def, at, yaw);
        else if (def.Id == "ocak") D.PlaceOcak(at);
        else if (def.Station != null) D.PlaceStation(def, at, yaw);
        else D.PlaceFurniture(def, at, yaw);
        Flush();
    }
}
