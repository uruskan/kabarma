using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Agil (GDD 9.1): yapi setinin cit, kapi ve duvar parcalarindan kapali halkayi bulur (kural Sim'de: PenRules),
/// kapi kanatlarini E ile acip kapar, Ahsap Kapi / Yemlik / Folluk'u ve yerdeki yumurtalari tutar,
/// yaratiklarin duvara takilmasini (Gidak.Pen.cs, Zipzip.Fence.cs) yaratiklardan sonra isler.
/// Sayilar pen.json'da. Durum (hayvan, yemlik, folluk) Sim'in Coop'unda; burasi gorur ve bildirir.
/// </summary>
public partial class PenSystem : Node
{
    readonly Director _d;
    readonly PenFile _def;
    readonly HashSet<string> _wallIds, _gateIds;
    readonly List<PenWall> _walls = new();
    readonly Dictionary<int, BuildPiece> _pieceOf = new();
    readonly Dictionary<BuildPiece, PenWall> _wallOf = new();
    readonly Dictionary<BuildPiece, Gate> _gates = new();
    readonly List<PenProp> _props = new();
    readonly List<EggPickup> _eggs = new();
    bool _dirty;
    int _lastCells = -1;
    float _hintT;

    public IReadOnlyList<PenShape> Pens { get; private set; } = new List<PenShape>();
    public IReadOnlyList<PenWall> Walls => _walls;
    public IReadOnlyList<PenProp> Props => _props;
    public IReadOnlyList<EggPickup> GroundEggs => _eggs;
    public PenFile Def => _def;
    public int FenceHits { get; private set; }
    /// <summary>Itmeyle (Gidak tekmesi, vurus) cit ustunden gecen Zipzip sayisi.</summary>
    public int KnockedOver { get; set; }

    /// <summary>Kapi kanadi: yapi setindeki "Kanat" dugumleri (menteşe ekseninde) ve kanat carpismalari.</summary>
    public sealed class Gate
    {
        public BuildPiece Piece;
        public readonly List<(Node3D leaf, float sign)> Leaves = new();
        public readonly List<CollisionShape3D> Cols = new();
        public bool Open;
        public PenProp Door; // kapi kasasina oturan Ahsap Kapi
    }

    public PenSystem(Director d)
    {
        _d = d;
        _def = d.Data.Pen;
        _wallIds = _def.WallPieces.ToHashSet();
        _gateIds = _def.GatePieces.ToHashSet();
        Name = "PenSystem";
        ProcessPriority = 100; // yaratiklardan sonra: takilmayi ayni karede duzeltir
    }

    public override void _Ready()
    {
        Events.Dawn += () =>
        {
            var hp = _walls.Select(PieceOf).Where(p => p != null && IsInstanceValid(p)).Select(p => p.Hp).DefaultIfEmpty(0).Min();
            GD.Print($"FENCE night hits={FenceHits} lowest_hp={hp:0}/{_d.Build?.System?.Kit.PieceHp ?? 0:0} knocked_over={KnockedOver}");
        };
        BindSave(); // PenSystem.Save.cs: "pen.props" kayit bolumu
        var sys = _d.Build?.System;
        if (sys == null) return;
        sys.Placed += OnPlaced;
        sys.Removed += p => Forget(p);
        sys.Fell += (p, _) => Forget(p);
    }

    void OnPlaced(BuildPiece p)
    {
        if (!_wallIds.Contains(p.Def.Id)) return;
        if (_gateIds.Contains(p.Def.Id)) { _gates[p] = MakeGate(p); GD.Print($"GATE add {p.Def.Id} at {p.GlobalPosition} n={_gates.Count}"); }
        _dirty = true;
    }

    void Forget(BuildPiece p)
    {
        if (_gates.TryGetValue(p, out var g) && g.Door != null) g.Door.Kasa = null;
        _gates.Remove(p);
        _dirty = true;
    }

    Gate MakeGate(BuildPiece p)
    {
        var g = new Gate { Piece = p };
        foreach (var (name, sign) in new[] { ("Kanat", 1f), ("KanatSol", 1f), ("KanatSag", -1f) })
            if (p.Model?.FindChild(name, true, false) is Node3D leaf) g.Leaves.Add((leaf, sign));
        foreach (var c in p.GetChildren().OfType<CollisionShape3D>())
            if (c.Name.ToString().StartsWith("Col_Kanat")) g.Cols.Add(c);
        // Kapi kasasi: kanadi yok, icine Ahsap Kapi oturur. Bos kasa acik gecittir.
        g.Open = p.Def.Id == "kapi_kasasi";
        return g;
    }

    // ---------- Halka ----------

    void Rebuild()
    {
        _dirty = false;
        _walls.Clear(); _pieceOf.Clear(); _wallOf.Clear();
        var sys = _d.Build?.System;
        if (sys == null) return;
        int id = 0;
        foreach (var p in sys.Pieces)
        {
            if (!IsInstanceValid(p) || !_wallIds.Contains(p.Def.Id)) continue;
            float half = p.Def.Sockets.Where(s => s.Type == "wall_end").Select(s => Mathf.Abs(s.Pos.X)).DefaultIfEmpty(sys.Kit.Grid / 2f).Max();
            var a = p.GlobalTransform * new Vector3(-half, 0, 0);
            var b = p.GlobalTransform * new Vector3(half, 0, 0);
            bool gate = _gates.TryGetValue(p, out var g);
            var w = new PenWall { Id = ++id, A = new P2(a.X, a.Z), B = new P2(b.X, b.Z), Gate = gate, Open = gate && g.Open };
            _walls.Add(w); _pieceOf[w.Id] = p; _wallOf[p] = w;
        }
        Pens = PenRules.FindPens(_walls, _def.CellArea * _def.MinCells, _def.Weld);
        int cells = Pens.Where(x => x.HasGate).Select(x => x.Cells(_def.CellArea)).DefaultIfEmpty(0).Max();
        foreach (var pen in Pens) GD.Print($"PEN area={pen.Area:0.0} cells={pen.Cells(_def.CellArea)} walls={pen.Walls.Count} gate={pen.HasGate} gidak={pen.Capacity(_def.Animals[0].Area)}");
        if (cells != _lastCells) { _lastCells = cells; Events.RaisePenChanged(cells); }
    }

    public PenShape PenAt(Vector3 p) => Pens.FirstOrDefault(x => x.Contains(new P2(p.X, p.Z)));

    /// <summary>from → to hareketinin takildigi ilk duvar (kapali kapi dahil).</summary>
    public WallHit? FirstHit(Vector3 from, Vector3 to, float radius) =>
        PenRules.FirstHit(_walls, new P2(from.X, from.Z), new P2(to.X, to.Z), radius);

    public BuildPiece PieceOf(PenWall w) => w != null && _pieceOf.TryGetValue(w.Id, out var p) ? p : null;

    /// <summary>Yakindaki acik kapi (Gidak'in kapidan gecmesi icin).</summary>
    public PenWall NearestOpenGate(Vector3 p, float within) =>
        _walls.Where(w => w.Gate && w.Open && PenRules.DistanceToWall(w, new P2(p.X, p.Z)) < within)
            .OrderBy(w => PenRules.DistanceToWall(w, new P2(p.X, p.Z))).FirstOrDefault();

    /// <summary>Yasayan kapilar: Forget'siz silinen parca (yapi yukseltmesi, ayni yere ikinci yerlestirme) eski kaydi birakir.</summary>
    public IEnumerable<Gate> Gates
    {
        get
        {
            foreach (var k in _gates.Keys.Where(k => !IsInstanceValid(k) || k.IsQueuedForDeletion()).ToList()) { _gates.Remove(k); _dirty = true; }
            if (_dirty) Rebuild();
            // Agil halkasina ait kapilar once: halkanin disinda kalmis basi bos kapi (kaymis yerlestirme) ilk sayilmaz.
            return _gates.Values.OrderBy(g => InPen(g) ? 0 : 1).ToList();
        }
    }

    bool InPen(Gate g) => _wallOf.TryGetValue(g.Piece, out var w) && Pens.Any(p => p.Walls.Contains(w.Id));

    /// <summary>
    /// Duvarlarin arasindan yol: dogrudan gidilebiliyorsa hedefin kendisi, degilse agil koselerinin disindan (ve icinden)
    /// ve acik kapilarin iki yanindan gecen en kisa yolun ilk ara noktasi. Yol yoksa hedef.
    /// </summary>
    public Vector3 Route(Vector3 start, Vector3 to, float radius)
    {
        if (FirstHit(start, to, radius) == null) return to;
        var nodes = new List<Vector3> { start with { Y = 0 } };
        foreach (var pen in Pens)
        {
            var c = new Vector3(pen.Centroid.X, 0, pen.Centroid.Z);
            foreach (var v in pen.Polygon)
            {
                var p = new Vector3(v.X, 0, v.Z);
                var o = (p - c).Normalized();
                nodes.Add(p + o * (1.0f + radius));
                nodes.Add(p - o * (0.7f + radius));
            }
        }
        foreach (var w in _walls.Where(w => w.Gate && w.Open))
        {
            var mid = new Vector3(w.Mid.X, 0, w.Mid.Z);
            var n = new Vector3(w.B.X - w.A.X, 0, w.B.Z - w.A.Z).Normalized().Cross(Vector3.Up);
            nodes.Add(mid + n * 1.0f);
            nodes.Add(mid - n * 1.0f);
        }
        nodes.Add(to with { Y = 0 });
        int N = nodes.Count, goal = N - 1;
        var dist = Enumerable.Repeat(float.MaxValue, N).ToArray();
        var prev = Enumerable.Repeat(-1, N).ToArray();
        var done = new bool[N];
        dist[0] = 0;
        for (int it = 0; it < N; it++)
        {
            int u = -1;
            for (int i = 0; i < N; i++) if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0 || u == goal) break;
            done[u] = true;
            for (int v = 0; v < N; v++)
            {
                if (done[v] || v == u) continue;
                float d = nodes[u].DistanceTo(nodes[v]);
                if (dist[u] + d >= dist[v] || FirstHit(nodes[u], nodes[v], radius * 0.5f) != null) continue;
                dist[v] = dist[u] + d; prev[v] = u;
            }
        }
        if (prev[goal] < 0) return to;
        var path = new List<int>();
        for (int s = goal; s > 0; s = prev[s]) path.Add(s);
        path.Reverse();
        // Bir ara noktanin ustundeyse bir sonrakine gec (yoksa yerinde sayar).
        foreach (int s in path)
            if (s == goal || nodes[s].DistanceTo(nodes[0]) > 0.45f) return nodes[s] with { Y = to.Y };
        return to;
    }

    // ---------- Kapilar ----------

    /// <summary>
    /// E: kapiyi ve 2 m icindeki (ust uste ya da yan yana yanlis kurulmus) kapilari birlikte acar ya da kapatir. Ust uste iki kapi (ikinci
    /// yerlestirme) ayri durursa biri hep kapali kalir, duvar gecilmez ve en yakini secen E kapiyi titretir.
    /// </summary>
    void ToggleGateGroup(Gate g)
    {
        bool open = !g.Open;
        foreach (var x in Gates.Where(x => x.Leaves.Count > 0 && x.Piece.GlobalPosition.DistanceTo(g.Piece.GlobalPosition) < 2.5f).ToList())
            SetGate(x, open);
        SetGate(g, open);
    }

    public void SetGate(Gate g, bool open)
    {
        if (g.Open == open) return;
        g.Open = open;
        if (_wallOf.TryGetValue(g.Piece, out var w)) w.Open = open;
        float ang = Mathf.DegToRad(_def.GateOpenDegrees);
        foreach (var (leaf, sign) in g.Leaves)
            leaf.CreateTween().TweenProperty(leaf, "rotation:y", open ? ang * sign : 0f, _def.GateSwingSeconds).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        foreach (var c in g.Cols) c.SetDeferred(CollisionShape3D.PropertyName.Disabled, open);
        Sfx.Play("place", g.Piece.GlobalPosition + Vector3.Up, -4f);
        GD.Print($"GATE {g.Piece.Def.Id} {(open ? "open" : "closed")}");
    }

    /// <summary>Kapi kasasinin gecidi: kasadaki Ahsap Kapi acik ya da kasa bossa acik.</summary>
    internal void SyncDoor(BuildPiece kasa)
    {
        if (!_gates.TryGetValue(kasa, out var g)) return;
        g.Open = g.Door == null || g.Door.DoorOpen;
        if (_wallOf.TryGetValue(kasa, out var w)) w.Open = g.Open;
    }

    // ---------- Cit hasari ----------

    /// <summary>Zipzip cite yuklendi: parca hasar alir, can %10'un altina inmez (URETIM 6 M1).</summary>
    public void DamageWall(PenWall w, float dmg, Vector3 at)
    {
        var p = PieceOf(w);
        if (p == null || !IsInstanceValid(p)) return;
        float max = _d.Build.System.Kit.PieceHp;
        p.Hp = StructureRules.Damage(p.Hp, max, dmg, _def.StructureHpFloor);
        FenceHits++;
        Fx.Burst(at, Palette.Wood, 10, 4f, 0.07f, 0.5f);
        Sfx.Play("hit", at, -3f);
        if (p.Model != null)
        {
            var tw = p.Model.CreateTween();
            tw.TweenProperty(p.Model, "position", new Vector3(0, 0, 0.06f), 0.05f);
            tw.TweenProperty(p.Model, "position", Vector3.Zero, 0.12f);
        }
        if (FenceHits % 10 == 1) GD.Print($"FENCE hit {p.Name} hp={p.Hp:0}/{max:0} hits={FenceHits}");
    }

    // ---------- Yerlestirilebilirler ----------

    /// <summary>Agil sisteminin kurdugu yerlestirilebilirler (Capa.Insa bunlari PlaceProp'a yollar; Sandik, Uyku Tulumu, Mesale ekonomi'nin).</summary>
    public static bool IsPenProp(string id) => id is "ahsap_kapi" or "yemlik" or "folluk" or "suluk" or "kasinma_fircasi" or "ari_kovani" // M2 hayvan: Suluk, Firca, Kovan
        or "savas_agili_bayragi" or "ahir_zili" or "baglama_diregi" or "eyerlik"; // M3 hayvan (GDD 9.5)

    public PenProp PlaceProp(PlaceableDef def, Vector3 at, float yaw)
    {
        var prop = new PenProp(_d, this, def);
        _d.AddChild(prop);
        prop.GlobalPosition = at;
        prop.Rotation = new Vector3(0, yaw, 0);
        prop.SyncSim(); // Sim nesnesinin konumu (agil uyeligi konumdan)
        if (def.Id == "ahsap_kapi" && FreeKasaNear(at) is BuildPiece kasa) prop.FitInto(kasa, _gates[kasa]);
        _props.Add(prop);
        Sfx.Play("place", at, 0f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Wood, 18, 4f, 0.08f, 0.6f);
        GD.Print($"PLACED {def.Id} at {prop.GlobalPosition}");
        Events.RaiseStationPlaced(def.Id);
        return prop;
    }

    /// <summary>Ahsap Kapi oturacak bos Kapi Kasasi (nisan noktasina en yakin).</summary>
    public BuildPiece FreeKasaNear(Vector3 at) =>
        _gates.Values.Where(g => g.Piece.Def.Id == "kapi_kasasi" && g.Door == null && IsInstanceValid(g.Piece)
                && new Vector2(g.Piece.GlobalPosition.X - at.X, g.Piece.GlobalPosition.Z - at.Z).Length() < _def.DoorSnapRadius)
            .OrderBy(g => g.Piece.GlobalPosition.DistanceTo(at)).Select(g => g.Piece).FirstOrDefault();

    /// <summary>Insa hayaleti Ahsap Kapi ise: bos kasaya oturt (hayaletin yeri kasaya gecer). Kasa yoksa gecersiz.</summary>
    public bool SnapGhost(string id, Node3D ghost, Vector3 at)
    {
        if (id != "ahsap_kapi") return true;
        var kasa = FreeKasaNear(at);
        if (kasa == null) { _d.Hud.Hint(Loc.T("ranch.gate_needs_frame"), 0.25f); return false; }
        ghost.GlobalTransform = kasa.GlobalTransform;
        return true;
    }

    public IEnumerable<PenProp> PropsIn(PenShape pen, string id) => _props.Where(p => IsInstanceValid(p) && p.Def.Id == id && pen.Contains(new P2(p.GlobalPosition.X, p.GlobalPosition.Z)));

    // ---------- Yumurta ----------

    /// <summary>Yumurta Folluk'a (bos yuva varsa) ya da yere (ICERIK 10.2).</summary>
    public void Lay(string product, Vector3 at, PenShape pen)
    {
        var nests = pen == null ? new List<PenProp>() : PropsIn(pen, "folluk").ToList();
        var target = CoopRules.Place(nests.Select(n => n.Nest), out var nest);
        if (target == LayTarget.Nest)
        {
            var prop = nests.First(n => n.Nest == nest);
            prop.Bounce();
            Fx.Text3D(prop.GlobalPosition + Vector3.Up * 1.1f, "+1 Yumurta", Palette.OffWhite, 56, 1f, 1.4f);
        }
        else
        {
            var egg = new EggPickup { Product = product };
            _d.AddChild(egg);
            egg.GlobalPosition = _d.World.Ground(at);
            _eggs.Add(egg);
            Fx.Text3D(at + Vector3.Up * 1.0f, "Yumurta", Palette.OffWhite, 48, 1f, 1.2f);
        }
        Sfx.Play("pickup", at, -4f);
        GD.Print($"EGG laid {(target == LayTarget.Nest ? "nest" : "ground")} at {at}");
        Events.RaiseLaid(product, target == LayTarget.Nest ? "nest" : "ground");
    }

    // ---------- E ----------

    /// <summary>Menzildeki en yakin agil nesnesi: yerdeki yumurta, Folluk, Yemlik, kapi. Isledigini dondurur.</summary>
    public bool Interact(Vector3 p)
    {
        var best = Nearest(p, out _);
        best?.Invoke();
        return best != null;
    }

    System.Action Nearest(Vector3 p, out string label)
    {
        label = null;
        float range = _def.InteractRange;
        float bestD = range;
        System.Action act = null;
        string lab = null;
        float D(Vector3 q) => new Vector2(q.X - p.X, q.Z - p.Z).Length();
        foreach (var e in _eggs.Where(e => IsInstanceValid(e)))
            if (D(e.GlobalPosition) < bestD) { bestD = D(e.GlobalPosition); var ee = e; act = () => PickEgg(ee); lab = Loc.T("ranch.take_egg"); }
        foreach (var pr in _props.Where(x => IsInstanceValid(x)))
        {
            float d = D(pr.GlobalPosition);
            if (d >= bestD + 0.01f) continue;
            if (pr.Nest != null && pr.Nest.Eggs > 0) { bestD = d; var pp = pr; act = () => pp.Collect(); lab = Loc.T("ranch.take_eggs", pr.Nest.Eggs); }
            else if (pr.Feeder != null && pr.Feeder.Room > 0 && FoodFor(pr) is string food) { bestD = d; var pp = pr; act = () => pp.Fill(); lab = Loc.T("ranch.fill_feeder", ItemNames.Of(food)); }
            else if (pr.Kasa != null) { bestD = d; var pp = pr; act = () => pp.ToggleDoor(); lab = pr.DoorOpen ? Loc.T("ranch.close_gate") : Loc.T("ranch.open_gate"); }
            else if (_d.Ranch?.PropAction(pr) is var (pa, pl) && pa != null) { bestD = d; act = pa; lab = pl; } // M3: Ahir Zili, Eyerlik (Husbandry.Combat)
        }
        foreach (var g in _gates.Values.Where(g => g.Leaves.Count > 0 && IsInstanceValid(g.Piece)))
        {
            // Agil halkasindaki kapi, halkanin disinda kalmis basi bos kapidan once (1 m pay): kosede kaymis kapi E'yi calmaz.
            float d = D(g.Piece.GlobalPosition) + (InPen(g) ? 0f : 1f);
            if (d >= bestD) continue;
            bestD = d; var gg = g; act = () => ToggleGateGroup(gg); lab = g.Open ? Loc.T("ranch.close_gate") : Loc.T("ranch.open_gate");
        }
        label = lab;
        _nearD = bestD;
        return act;
    }

    /// <summary>Menzildeki en yakin agil nesnesinin uzakligi (yoksa sonsuz): RanchSystem hayvanla kiyaslar.</summary>
    public float NearestDistance(Vector3 p)
    {
        return Nearest(p, out _) == null ? float.MaxValue : _nearD;
    }

    float _nearD = float.MaxValue;

    /// <summary>
    /// Yemlik'e konacak yem (M2): doluysa kendi yemi; bossa agilindaki kayitli hayvanlarin (ya da izleyen evcillerin)
    /// sevdigi ve cantada olan ilk yem. Yoksa null.
    /// </summary>
    public string FoodFor(PenProp pr)
    {
        var f = pr.Feeder;
        var bag = _d.State.Backpack;
        if (f.Amount > 0) return bag.Count(f.Food) > 0 ? f.Food : null;
        var ranch = _d.State.Ranch;
        var pen = PenAt(pr.GlobalPosition);
        var wants = ranch.Animals.Where(a => a.Tame && ranch.Rules.Known(a.Species) && (pen == null || !a.Registered || pen.Contains(new P2(a.X, a.Z))))
            .OrderByDescending(a => a.Registered).Select(a => ranch.Rules.Species(a.Species).Food).Distinct();
        // Bu agilda baska Yemlik ayni yemi zaten tasiyorsa digerini sec (Gidak bugdayi + Pofuduk salgami).
        var taken = pen == null ? new HashSet<string>() : PropsIn(pen, "yemlik").Where(y => y != pr && y.Feeder.Amount > 0).Select(y => y.Feeder.Food).ToHashSet();
        return wants.Where(w => bag.Count(w) > 0).OrderBy(w => taken.Contains(w)).FirstOrDefault()
            ?? (bag.Count(f.Food) > 0 ? f.Food : null);
    }

    /// <summary>Yere dusen urun (Folluk yoksa ya da doluysa; Ranch olayi).</summary>
    public void DropOnGround(string product, Vector3 at)
    {
        var egg = new EggPickup { Product = product };
        _d.AddChild(egg);
        egg.GlobalPosition = _d.World.Ground(at);
        _eggs.Add(egg);
        Fx.Text3D(at + Vector3.Up * 1.0f, ItemNames.Of(product), Palette.OffWhite, 48, 1f, 1.2f);
        Sfx.Play("pickup", at, -4f);
        GD.Print($"EGG laid ground at {at}");
    }

    void PickEgg(EggPickup e)
    {
        _eggs.Remove(e);
        _d.State.Backpack.Add(e.Product, 1);
        Fx.FlyTo(e.GlobalPosition + Vector3.Up * 0.2f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.OffWhite, 0.12f);
        Sfx.Play("pickup", e.GlobalPosition, -2f);
        Events.RaiseGathered(e.Product, 1);
        GD.Print($"EGG picked eggs={_d.State.Eggs}");
        e.QueueFree();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_dirty) Rebuild();
        foreach (var z in Zipzip.All.ToArray()) z.FenceTick(dt, this);
        _d.Gidak?.PenTick(dt, this);
        // E ipucu: yakindaki agil nesnesi (tus glifle, ActText)
        _hintT -= dt;
        if (_hintT <= 0 && !_d.Hud.MenuOpen && _d.Capa.CurrentPlaceable == "")
        {
            _hintT = 0.2f;
            if (Nearest(_d.Player.GlobalPosition, out var label) != null) _d.Hud.Hint("{act:interact}: " + label, 0.3f); // Hint glifi genisletir (girdi akisi)
        }
    }
}

/// <summary>Yerde duran yumurta (Folluk yoksa ya da doluysa). E ile alinir.</summary>
public partial class EggPickup : Node3D
{
    public string Product = "egg";
    float _t;

    public override void _Ready()
    {
        AddChild(new MeshKit().Sphere(0.12f, MeshKit.At(new Vector3(0, 0.12f, 0), Vector3.Zero, new Vector3(1, 1.3f, 1)), Palette.OffWhite, 10, 6).Instance());
        AddToGroup("egg_pickup");
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        Rotation = new Vector3(0, _t * 0.8f, Mathf.Sin(_t * 2f) * 0.08f);
    }
}
