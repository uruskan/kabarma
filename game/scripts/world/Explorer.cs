using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kesif (GDD §21, ICERIK §17; dunya2): harita acilmasi, kesif noktalarinin bulunmasi, Seyir Kulesi'ne tirmanma,
/// mancinik (duraktan duraga firlatma, parasutle inis), Yuva, Enkaz ve Yabani Bahce'nin tukenip yenilenmesi.
/// Durum Sim'de (GameState.Explore, kayit bolumu "explore"); bu dugum onu dunyaya baglar. Statik yok (TEKNIK §9).
/// </summary>
public partial class Explorer : Node
{
    readonly Director _d;
    World W => _d.World;
    Exploration X => _d.State.Explore;
    MapDef MapDef => _d.Data.Biomes.World.Map;
    CatapultDef Cat => _d.Data.Biomes.World.Catapult;
    int Day => _d.State.Calendar.Day;

    /// <summary>Harita degisti (acilan hucre, yeni nokta, isaret): harita paneli yeniden cizer.</summary>
    public event Action Changed;
    public int Version { get; private set; }
    void Bump() { Version++; Changed?.Invoke(); }

    float _tick;
    /// <summary>Tirmanma ya da ucus suruyor: oyuncu fizigi kapali, konum buradan.</summary>
    public bool Busy => _move != null;

    public Explorer(Director d) { _d = d; Name = "Explorer"; }

    Exploration _seen;

    public override void _Ready()
    {
        _d.State.Calendar.Morning += _ => Refill();
        _seen = X;
        Restore();
    }

    // ---------- Kayittan ya da yeni oyundan: dunyadaki tuketilen noktalar ----------

    /// <summary>Enkaz kayalari ve Bahce tutamlari Sim'deki kalana gore kurulur (kayit yuklenince de cagrilir).</summary>
    public void Restore()
    {
        foreach (var poi in W.Pois)
        {
            var type = W.PoiType(poi.Type);
            int left = X.Left(poi, type, Day);
            if (poi.Type == "enkaz")
            {
                int cur = W.PoiRocks.TryGetValue(poi.Id, out var r) && IsInstanceValid(r) ? r.Stones : 0;
                if (cur != left) W.SpawnPoiRock(poi, left);
            }
            if (poi.Type == "yabani_bahce") W.RefillTufts(poi.Id, Exploration.Split(poi, type, left));
        }
        Bump();
    }

    /// <summary>Safak: yenilenme gunu gelen noktalar doldurulur.</summary>
    void Refill()
    {
        foreach (var poi in W.Pois)
        {
            var type = W.PoiType(poi.Type);
            if (type.RespawnDays <= 0) continue;
            int before = X.Uses.TryGetValue(poi.Id, out var u) ? u.Left : Exploration.Loot(poi, type).Values.Sum();
            int left = X.Left(poi, type, Day);
            if (before == left) continue;
            if (poi.Type == "enkaz") W.SpawnPoiRock(poi, left);
            if (poi.Type == "yabani_bahce") W.RefillTufts(poi.Id, Exploration.Split(poi, type, left));
            GD.Print($"EXPLORE refill {poi.Id} left={left} day={Day}");
        }
    }

    // ---------- Her kare ----------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        // Kayit yuklenince Sim'deki Exploration nesnesi degisir (kayit bolumu "explore"): dunyadaki noktalar yeniden kurulur.
        if (!ReferenceEquals(_seen, X)) { _seen = X; Restore(); }
        if (_move != null) { StepMove(dt); return; }
        _tick -= dt;
        if (_tick > 0) return;
        _tick = 0.25f;
        if (W.Indoor) return; // Oyuk odasi haritanin disinda (World.Oyuk.cs): acilma yok
        var p = _d.Player.GlobalPosition;
        bool changed = X.Reveal(p.X, p.Z, MapDef.WalkReveal) > 0;
        foreach (var poi in W.Pois)
        {
            if (X.Found.Contains(poi.Id)) continue;
            var type = W.PoiType(poi.Type);
            if (new Vector2(p.X - poi.X, p.Z - poi.Z).Length() > type.DiscoverRadius) continue;
            X.Discover(poi.Id);
            changed = true;
            _d.Hud.Big($"Keşfedildi: {type.Name}", Palette.Warning, 2f);
            GD.Print($"EXPLORE found {poi.Id} at={poi.X:0},{poi.Z:0}");
        }
        SyncUses();
        if (changed) Bump();
    }

    /// <summary>Kazma ve Bicme'nin tukettigini Sim'e yazar (Enkaz kayasi, Bahce tutamlari).</summary>
    void SyncUses()
    {
        foreach (var (id, rock) in W.PoiRocks.ToList())
        {
            var poi = W.Pois.First(q => q.Id == id);
            int left = IsInstanceValid(rock) && rock.Stones > 0 ? rock.Stones : 0;
            X.SetLeft(poi, W.PoiType(poi.Type), Day, left);
            if (left == 0) W.PoiRocks.Remove(id);
        }
        foreach (var (id, tufts) in W.PoiTufts)
        {
            var poi = W.Pois.First(q => q.Id == id);
            X.SetLeft(poi, W.PoiType(poi.Type), Day, tufts.Sum(t => t.Left));
        }
    }

    // ---------- Etkilesim (Director.Interact ve Hud istemi) ----------

    const float Reach = 3.2f;

    enum Kind { None, Climb, Descend, Catapult, Nest }

    (Kind kind, Poi poi, CatapultStop stop) Nearest(Vector3 p)
    {
        foreach (var poi in W.Pois.Where(q => q.Type == "seyir_kulesi"))
        {
            var top = W.TowerTop(poi);
            if (p.Y > top.Y - 1.5f && new Vector2(p.X - top.X, p.Z - top.Z).Length() < 3f) return (Kind.Descend, poi, null);
            var bottom = W.PoiPos(poi);
            if (p.Y < bottom.Y + 3f && new Vector2(p.X - bottom.X, p.Z - bottom.Z).Length() < Reach + 1.2f) return (Kind.Climb, poi, null);
        }
        var stop = Stops().Where(s => new Vector2(p.X - s.X, p.Z - s.Z).Length() < Cat.UseRadius + 0.6f).OrderBy(s => new Vector2(p.X - s.X, p.Z - s.Z).Length()).FirstOrDefault();
        if (stop != null) return (Kind.Catapult, null, stop);
        foreach (var poi in W.Pois.Where(q => q.Type == "yuva"))
            if (new Vector2(p.X - poi.X, p.Z - poi.Z).Length() < 2.2f && X.Left(poi, W.PoiType("yuva"), Day) > 0) return (Kind.Nest, poi, null);
        return (Kind.None, null, null);
    }

    /// <summary>Hud istemi: (fiil, nesne) ya da null.</summary>
    public (string verb, string obj)? Prompt(Vector3 p)
    {
        if (Busy) return null;
        var (k, poi, stop) = Nearest(p);
        return k switch
        {
            Kind.Climb => ("Tırman", "Seyir Kulesi"),
            Kind.Descend => ("İn", "Seyir Kulesi"),
            Kind.Catapult => stop.Open ? ("Fırlat", stop.Name) : ("Kapalı", stop.Name),
            Kind.Nest => ("Al", "Yuva"),
            _ => null,
        };
    }

    /// <summary>E: kesif etkilesimi yaptiysa true.</summary>
    public bool Interact()
    {
        if (Busy) return true;
        var p = _d.Player.GlobalPosition;
        var (k, poi, stop) = Nearest(p);
        switch (k)
        {
            case Kind.Climb: Climb(poi); return true;
            case Kind.Descend: Descend(poi); return true;
            case Kind.Catapult: OpenCatapult(stop); return true;
            case Kind.Nest: TakeNest(poi); return true;
        }
        return false;
    }

    void TakeNest(Poi poi)
    {
        var got = X.Take(poi, W.PoiType(poi.Type), Day, 1);
        foreach (var (item, n) in got)
        {
            _d.State.Backpack.Add(item, n);
            Events.RaiseGathered(item, n);
            Fx.FlyTo(W.PoiPos(poi) + Vector3.Up * 0.3f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Warning, 0.12f);
            GD.Print($"EXPLORE nest {poi.Id} +{n} {item}");
        }
        Bump();
    }

    // ---------- Seyir Kulesi ----------

    /// <summary>Hareket: noktalar arasinda sureli yol (tirmanma, inme, ucus). Fizik kapali.</summary>
    sealed class Move
    {
        public Func<float, Vector3> At;
        public float T, Seconds;
        public Action Done;
        public Node3D Chute;
    }
    Move _move;

    void Begin(Move m)
    {
        _move = m;
        _d.Player.SetPhysicsProcess(false);
        _d.Player.Velocity = Vector3.Zero;
    }

    void StepMove(float dt)
    {
        var m = _move;
        m.T = Mathf.Min(m.Seconds, m.T + dt);
        _d.Player.GlobalPosition = m.At(m.T);
        if (m.T < m.Seconds) return;
        _move = null;
        if (m.Chute != null && IsInstanceValid(m.Chute)) m.Chute.QueueFree();
        _d.Player.Velocity = Vector3.Zero;
        _d.Player.SetPhysicsProcess(true);
        m.Done?.Invoke();
    }

    public void Climb(Poi tower)
    {
        var bottom = W.TowerLadder(tower) + Vector3.Up * 0.1f;
        var start = _d.Player.GlobalPosition;
        var top = W.TowerTop(tower) + new Vector3(0, 0.1f, -0.6f);
        float up = 2.4f;
        Begin(new Move
        {
            Seconds = up + 0.6f,
            At = t => t < 0.6f ? start.Lerp(bottom, t / 0.6f)
                : (t < up ? bottom.Lerp(new Vector3(bottom.X, top.Y, bottom.Z), (t - 0.6f) / (up - 0.6f)) : new Vector3(bottom.X, top.Y, bottom.Z).Lerp(top, (t - up) / 0.6f)),
            Done = () =>
            {
                var type = W.PoiType(tower.Type);
                int before = X.Revealed;
                bool first = X.Climb(tower, type);
                _d.Hud.Big(first ? $"HARİTA AÇILDI · %{X.Percent:0}" : "Seyir Kulesi", Palette.Warning, 2.5f);
                if (first) Sfx.Play("pickup", top, 0f);
                GD.Print($"EXPLORE climb {tower.Id} first={first} revealed={X.Revealed - before} percent={X.Percent:0.0}");
                Bump();
            },
        });
    }

    void Descend(Poi tower)
    {
        var start = _d.Player.GlobalPosition;
        var bottom = W.TowerLadder(tower) + new Vector3(0, 0.15f, 0.8f);
        Begin(new Move { Seconds = 2f, At = t => start.Lerp(bottom, Mathf.SmoothStep(0, 1, t / 2f)) });
    }

    // ---------- Mancinik ----------

    /// <summary>Duraklar: kurulu Mancinik'lar (us, karakol) ve Seyir Kulesi'nin dibindekiler (kule tirmanilinca acik).</summary>
    public List<CatapultStop> Stops()
    {
        var list = new List<CatapultStop>();
        int k = 0;
        foreach (var f in _d.Furniture.Where(f => IsInstanceValid(f) && f.Def.Id == "mancinik"))
        {
            var at = f.GlobalPosition;
            list.Add(new CatapultStop { Id = $"mancinik_{k++}", Name = k == 1 ? "Mancınık · Üs" : $"Mancınık {k}", X = at.X, Y = at.Y, Z = at.Z, Open = true });
        }
        foreach (var poi in W.Pois.Where(q => q.Type == "seyir_kulesi" && W.PoiType(q.Type).Catapult))
        {
            var at = W.TowerCatapult(poi);
            var biome = _d.Data.Biomes.Biomes.TryGetValue(poi.Biome, out var b) ? b.Name : poi.Biome;
            list.Add(new CatapultStop { Id = poi.Id, Name = $"Seyir Kulesi · {biome}", X = at.X, Y = at.Y, Z = at.Z, Open = X.Climbed.Contains(poi.Id) });
        }
        return list;
    }

    bool Carrying => _d.Player.Carrying || _d.Player.PushingCart || _d.Carts.Any(c => c.Pushed);

    void OpenCatapult(CatapultStop from)
    {
        if (!from.Open) { _d.Hud.Hint(Catapult.Text(Catapult.Refusal.Closed), 2.5f); return; }
        var items = new List<Hud.MenuItem>();
        foreach (var to in Stops().Where(s => s.Id != from.Id))
        {
            var target = to;
            float d = Catapult.Distance(from, target);
            items.Add(new Hud.MenuItem($"{target.Name} · {d:0} m",
                () => Catapult.Check(Cat, from, target, Carrying) == Catapult.Refusal.None,
                () => { _d.Hud.HideMenu(); Launch(from, target); }));
        }
        if (items.Count == 0) { _d.Hud.Hint("Başka açık durak yok: Seyir Kulesi'ne tırman ya da Mancınık kur", 3f); return; }
        string why = Carrying ? Catapult.Text(Catapult.Refusal.Carrying) : "Paraşütle inilir";
        _d.Hud.ShowMenu("MANCINIK", items, why);
    }

    /// <summary>Firlat: kural Sim'de (Catapult.Check, Plan). Yay boyunca ucar, parasut chuteHeight'ta acilir.</summary>
    public bool Launch(CatapultStop from, CatapultStop to)
    {
        var why = Catapult.Check(Cat, from, to, Carrying);
        if (why != Catapult.Refusal.None) { _d.Hud.Hint(Catapult.Text(why), 2.5f); GD.Print($"EXPLORE catapult refused {why}"); return false; }
        var plan = Catapult.Plan(Cat, from, to, (x, z) => W.HeightAt(x, z));
        // Yay: parasut acilana dek parabol; sonra yavas inis (chuteSpeed) ve yatayda kalan yolu tamamlar.
        float tChute = plan.Seconds;
        for (float t = plan.Seconds * 0.5f; t < plan.Seconds; t += 0.05f)
        {
            var (sx, sy, sz) = plan.Sample(t);
            if (sy - W.HeightAt(sx, sz) < Cat.ChuteHeight) { tChute = t; break; }
        }
        var (cx, cy, cz) = plan.Sample(tChute);
        var chutePos = new Vector3(cx, cy, cz);
        var land = new Vector3(plan.X1, plan.Y1 + 0.2f, plan.Z1);
        float drop = Mathf.Max(0.5f, (chutePos.Y - land.Y) / Mathf.Max(0.5f, Cat.ChuteSpeed));
        Node3D chute = null;
        var m = new Move
        {
            Seconds = tChute + drop,
            At = t =>
            {
                if (t <= tChute) { var (x, y, z) = plan.Sample(t); return new Vector3(x, y, z); }
                if (chute == null) { chute = MakeChute(); _move.Chute = chute; }
                float u = (t - tChute) / drop;
                return new Vector3(Mathf.Lerp(chutePos.X, land.X, u), Mathf.Lerp(chutePos.Y, land.Y, u), Mathf.Lerp(chutePos.Z, land.Z, u));
            },
            Done = () =>
            {
                X.Flights++;
                GD.Print($"EXPLORE landed {to.Id} at={land.X:0.0},{land.Z:0.0} flights={X.Flights}");
                Bump();
            },
        };
        Begin(m);
        Sfx.Play("place", _d.Player.GlobalPosition, 0f);
        GD.Print($"EXPLORE launch {from.Id}->{to.Id} seconds={m.Seconds:0.0} apex={plan.Apex:0}");
        return true;
    }

    /// <summary>Parasut: yari kure kubbe ve dort ip (koddan; yer tutucu).</summary>
    Node3D MakeChute()
    {
        var root = new Node3D { Name = "Chute" };
        var canopy = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.7f, Height = 1.7f, IsHemisphere = true, RadialSegments = 16, Rings = 6 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Warning, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 0.7f },
            Position = new Vector3(0, 3.0f, 0),
            Scale = new Vector3(1, 0.5f, 1),
        };
        root.AddChild(canopy);
        foreach (var (x, z) in new[] { (1.2f, 0f), (-1.2f, 0f), (0f, 1.2f), (0f, -1.2f) })
        {
            var a = new Vector3(x, 3.0f, z); var b = new Vector3(0, 1.3f, 0);
            var line = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = a.DistanceTo(b), RadialSegments = 4 } };
            root.AddChild(line);
            line.Position = (a + b) / 2f;
            line.LookAtFromPosition(line.Position, b, Vector3.Right);
            line.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
        }
        _d.Player.AddChild(root);
        return root;
    }
}
