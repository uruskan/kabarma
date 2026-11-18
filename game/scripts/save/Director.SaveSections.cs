using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

// Godot katmaninda duran durumun kayit bolumleri (TEKNIK 9.1; progress/m2/kayit/durum_denetimi.md). Her bolum duz DTO
// yazar (Godot tipi yok). Sira: yapi parcalari -> yerlestirilebilirler -> agil -> dunya -> yuk nesneleri (Spawn), oyuncu
// ve Gidak (State), yonetmen (Progress).

public sealed class PlayerSave
{
    public string Player { get; set; } = GameState.LocalPlayer;
    public float[] Pos { get; set; } = new float[3];
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public float Hp { get; set; }
    public float[] Respawn { get; set; } = new float[3];
    public bool Visible { get; set; } = true;
    public string Head { get; set; } = "";
}

public sealed class PlayersSave { public List<PlayerSave> Players { get; set; } = new(); }

public sealed class PieceSave
{
    public string Id { get; set; } = "";
    public float[] Xf { get; set; } = new float[12];
    public float Hp { get; set; }
}

/// <summary>Istasyon, Ocak, mobilya (Sandik, Uyku Tulumu, Mesale), agil nesnesi (Yemlik, Folluk, Ahsap Kapi).</summary>
public sealed class PlacedSave
{
    public EntityRecord E { get; set; } = new();
    /// <summary>Sandik yuvalari.</summary>
    public List<StackSave> Items { get; set; }
    /// <summary>Yemlik yemi ya da Folluk yumurtasi.</summary>
    public int Amount { get; set; }
    public bool Open { get; set; }
}

public sealed class GateSave
{
    /// <summary>Parcanin yapi listesindeki sirasi (build.pieces ile ayni sira).</summary>
    public int Piece { get; set; }
    public bool Open { get; set; }
}

public sealed class PenGroundSave
{
    public List<GateSave> Gates { get; set; } = new();
    public List<EntityRecord> Eggs { get; set; } = new();
}

public sealed class WorldResSave
{
    /// <summary>Kaya adi -> kalan birim; kayitta olmayan kaya bitmisti.</summary>
    public SortedDictionary<string, int> Rocks { get; set; } = new(System.StringComparer.Ordinal);
    public SortedDictionary<string, float> Wear { get; set; } = new(System.StringComparer.Ordinal);
    /// <summary>Tutamlarin kalan verimi (dunya sirasiyla).</summary>
    public List<int> Tufts { get; set; } = new();
}

public sealed class CartSave
{
    public float[] Xf { get; set; } = new float[12];
    public List<StackSave> Items { get; set; } = new();
}

public sealed class LoadsSave
{
    public List<CartSave> Carts { get; set; } = new();
    /// <summary>Kargo kasalari: E.Id kasa kimligi; Params "ramp", "cart" (araba sirasi).</summary>
    public List<PlacedSave> Crates { get; set; } = new();
    /// <summary>Yerdeki kabarik urunler: Content urun, Params "size", "scale", "cart".</summary>
    public List<EntityRecord> Swollen { get; set; } = new();
}

public sealed class CreaturesSave
{
    public bool GidakVisible { get; set; }
    public float[] GidakPos { get; set; } = new float[3];
    public float GidakYaw { get; set; }
    public int GidakFed { get; set; }
}

public sealed class DirectorSave
{
    public string Phase { get; set; } = "Day";
    public bool ReportShown { get; set; }
    public List<int[]> Watered { get; set; } = new();
    public List<string> Made { get; set; } = new();
    public List<string> Placed { get; set; } = new();
    public int Harvests { get; set; }
    public bool PodRamp { get; set; }
}

public partial class Director
{
    static float[] V(Vector3 v) => new[] { v.X, v.Y, v.Z };
    static Vector3 V(float[] a) => a == null || a.Length < 3 ? Vector3.Zero : new Vector3(a[0], a[1], a[2]);

    static float[] Xf(Transform3D t) => new[]
    {
        t.Basis.Column0.X, t.Basis.Column0.Y, t.Basis.Column0.Z, t.Basis.Column1.X, t.Basis.Column1.Y, t.Basis.Column1.Z,
        t.Basis.Column2.X, t.Basis.Column2.Y, t.Basis.Column2.Z, t.Origin.X, t.Origin.Y, t.Origin.Z,
    };

    static Transform3D Xf(float[] a) => a == null || a.Length < 12 ? Transform3D.Identity
        : new Transform3D(new Vector3(a[0], a[1], a[2]), new Vector3(a[3], a[4], a[5]), new Vector3(a[6], a[7], a[8]), new Vector3(a[9], a[10], a[11]));

    static EntityRecord Rec(string kind, string content, Node3D n) => new()
    {
        Kind = kind, Content = content, Pos = V(n.GlobalPosition),
        Rot = new[] { n.Quaternion.X, n.Quaternion.Y, n.Quaternion.Z, n.Quaternion.W },
    };

    static float YawOf(EntityRecord e) => e.Rot is { Length: 4 } r ? new Quaternion(r[0], r[1], r[2], r[3]).GetEuler().Y : 0f;

    PlaceableDef Placeable(string id) => Data.Tool.Placeables.FirstOrDefault(p => p.Id == id);

    /// <summary>Godot katmaninin bolumleri (InitSave'den bir kez).</summary>
    void RegisterGodotSections()
    {
        Save.Add(SaveSection.Json("build.pieces", 1, CapturePieces, ApplyPieces, SaveOrder.Spawn));
        Save.Add(SaveSection.Json("placeables", 1, CapturePlaced, ApplyPlaced, SaveOrder.Spawn + 10));
        Save.Add(SaveSection.Json("pen.props", 1, CaptureProps, ApplyProps, SaveOrder.Spawn + 20));
        Save.Add(SaveSection.Json("pen.ground", 1, CapturePenGround, ApplyPenGround, SaveOrder.Spawn + 30));
        Save.Add(SaveSection.Json("world.resources", 1, CaptureWorldRes, ApplyWorldRes, SaveOrder.Spawn + 40));
        Save.Add(SaveSection.Json("loads", 1, CaptureLoads, ApplyLoads, SaveOrder.Spawn + 50, afterRestore: AdoptRampCrates));
        Save.Add(SaveSection.Json("player", 1, CapturePlayer, ApplyPlayer, SaveOrder.State + 10));
        Save.Add(SaveSection.Json("creatures", 1, CaptureCreatures, ApplyCreatures, SaveOrder.State + 20, afterRestore: BindPenAnimals));
        Save.Add(SaveSection.Json("director", 1, CaptureDirector, ApplyDirector, SaveOrder.Progress + 10));
    }

    // ---------- yapi parcalari ----------

    List<BuildPiece> LivePieces() => Build?.System?.Pieces.Where(p => IsInstanceValid(p) && !p.IsQueuedForDeletion()).ToList() ?? new();

    List<PieceSave> CapturePieces() => LivePieces().Select(p => new PieceSave { Id = p.Def.Id, Xf = Xf(p.GlobalTransform), Hp = p.Hp }).ToList();

    void ApplyPieces(List<PieceSave> list)
    {
        var sys = Build?.System;
        if (sys == null) { if (list.Count > 0) GD.PushWarning($"LOAD build.pieces: yapi sistemi yok, {list.Count} parca atlandi"); return; }
        foreach (var p in list)
            if (sys.LoadPiece(p.Id, Xf(p.Xf), p.Hp) == null) GD.PushWarning($"LOAD parca kurulamadi {p.Id}");
    }

    // ---------- yerlestirilebilirler ----------

    List<PlacedSave> CapturePlaced()
    {
        var l = new List<PlacedSave>();
        if (Ocak != null && IsInstanceValid(Ocak)) l.Add(new PlacedSave { E = Rec("ocak", "ocak", Ocak) });
        foreach (var s in Stations.Where(IsInstanceValid)) l.Add(new PlacedSave { E = Rec("station", s.Def.Id, s) });
        foreach (var f in Furniture.Where(IsInstanceValid))
            l.Add(new PlacedSave { E = Rec("furniture", f.Def.Id, f), Items = f.Storage == null ? null : StackSave.Of(f.Storage) });
        return l;
    }

    void ApplyPlaced(List<PlacedSave> list)
    {
        foreach (var p in list)
        {
            var at = V(p.E.Pos);
            float yaw = YawOf(p.E);
            if (p.E.Kind == "ocak")
            {
                Ocak = new Ocak { Name = "Ocak" };
                AddChild(Ocak);
                Ocak.GlobalPosition = at;
                Ocak.Rotation = new Vector3(0, yaw, 0);
                continue;
            }
            var def = Placeable(p.E.Content);
            if (def == null) { GD.PushWarning($"LOAD yerlestirilebilir yok {p.E.Content}"); continue; }
            if (p.E.Kind == "station")
            {
                var st = new Station(this, def);
                AddChild(st);
                st.GlobalPosition = at;
                st.Rotation = new Vector3(0, yaw, 0);
                Stations.Add(st);
            }
            else
            {
                var f = new Furniture(this, def);
                AddChild(f);
                f.GlobalPosition = at;
                f.Rotation = new Vector3(0, yaw, 0);
                Furniture.Add(f);
                if (f.Storage != null) StackSave.Into(f.Storage, p.Items);
            }
        }
    }

    // ---------- agil ----------

    List<PlacedSave> CaptureProps() => Pens == null ? new() : Pens.Props.Where(IsInstanceValid).Select(p => new PlacedSave
    {
        E = Rec("pen_prop", p.Def.Id, p),
        Amount = p.Feeder?.Amount ?? p.Nest?.Eggs ?? 0,
        Open = p.DoorOpen,
    }).ToList();

    void ApplyProps(List<PlacedSave> list)
    {
        if (Pens == null) return;
        foreach (var p in list)
        {
            var def = Placeable(p.E.Content);
            if (def == null) continue;
            var prop = Pens.LoadProp(def, V(p.E.Pos), YawOf(p.E));
            if (prop.Feeder != null) prop.Feeder.Amount = p.Amount;
            if (prop.Nest != null) prop.Nest.Eggs = p.Amount;
            if (p.Open) prop.LoadDoor(true);
        }
    }

    PenGroundSave CapturePenGround()
    {
        var s = new PenGroundSave();
        if (Pens == null) return s;
        var pieces = LivePieces();
        foreach (var (piece, open) in Pens.GateStates)
        {
            int i = pieces.IndexOf(piece);
            if (i >= 0 && open) s.Gates.Add(new GateSave { Piece = i, Open = true });
        }
        s.Gates.Sort((a, b) => a.Piece.CompareTo(b.Piece));
        foreach (var e in Pens.GroundEggs.Where(IsInstanceValid)) s.Eggs.Add(Rec("egg", e.Product, e));
        return s;
    }

    void ApplyPenGround(PenGroundSave s)
    {
        if (Pens == null) return;
        Pens.RebuildNow(); // halkalar ve duvar-parca eslemesi (kapi durumlari ona yazilir)
        var pieces = LivePieces();
        foreach (var g in s.Gates) if (g.Piece >= 0 && g.Piece < pieces.Count) Pens.LoadGate(pieces[g.Piece], g.Open);
        foreach (var e in s.Eggs) Pens.LoadEgg(e.Content, V(e.Pos));
    }

    // ---------- dunya: kaya ve tutam ----------

    WorldResSave CaptureWorldRes()
    {
        var s = new WorldResSave();
        foreach (var r in World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0))
        {
            s.Rocks[r.Name] = r.Stones;
            if (r.Wear != 0f) s.Wear[r.Name] = r.Wear;
        }
        s.Tufts = World.Tufts.Select(t => t.Left).ToList();
        return s;
    }

    void ApplyWorldRes(WorldResSave s)
    {
        foreach (var r in World.Rocks.ToList())
        {
            if (!IsInstanceValid(r)) continue;
            if (s.Rocks.TryGetValue(r.Name, out int n)) r.LoadState(n, s.Wear.GetValueOrDefault(r.Name));
            else { World.RemoveRock(r); r.QueueFree(); }
        }
        var tufts = World.Tufts;
        for (int i = 0; i < tufts.Count && i < s.Tufts.Count; i++)
            if (s.Tufts[i] < tufts[i].Left) World.TakeTuft(tufts[i], tufts[i].Left - s.Tufts[i]);
    }

    // ---------- yuk nesneleri: El Arabasi, kargo kasasi, kabarik urun ----------

    List<ElArabasi> LiveCarts() => Carts.Where(c => IsInstanceValid(c) && !c.IsQueuedForDeletion()).ToList();

    LoadsSave CaptureLoads()
    {
        var s = new LoadsSave();
        var carts = LiveCarts();
        foreach (var c in carts) s.Carts.Add(new CartSave { Xf = Xf(c.GlobalTransform), Items = StackSave.Of(c.Slots) });
        foreach (var n in GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>().Where(n => IsInstanceValid(n) && !n.Gone))
        {
            int cart = carts.FindIndex(c => c.Load == n);
            if (n is CargoCrateNode k)
            {
                var p = new PlacedSave { E = Rec("crate", "kargo_kasasi", k), Items = StackSave.Of(k.Crate.Items) };
                p.E.Id = (ulong)k.Crate.Id;
                if (k.OnRamp) p.E.Params["ramp"] = "1";
                if (cart >= 0) p.E.Params["cart"] = cart.ToString();
                s.Crates.Add(p);
            }
            else
            {
                var e = Rec("swollen", n.CropId, n);
                e.Params["size"] = n.Size.Id;
                e.Params["scale"] = n.PivotScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (cart >= 0) e.Params["cart"] = cart.ToString();
                s.Swollen.Add(e);
            }
        }
        s.Crates.Sort((a, b) => a.E.Id.CompareTo(b.E.Id));
        return s;
    }

    void ApplyLoads(LoadsSave s)
    {
        var carts = new List<ElArabasi>();
        foreach (var c in s.Carts)
        {
            var cart = new ElArabasi(this);
            AddChild(cart);
            cart.GlobalTransform = Xf(c.Xf);
            Carts.Add(cart);
            cart.TreeExiting += () => Carts.Remove(cart);
            StackSave.Into(cart.Slots, c.Items);
            carts.Add(cart);
        }
        ElArabasi CartOf(EntityRecord e) => e.Params.TryGetValue("cart", out var ci) && int.TryParse(ci, out int i) && i >= 0 && i < carts.Count ? carts[i] : null;
        foreach (var p in s.Crates)
        {
            var crate = new CargoCrate(Data, (int)p.E.Id);
            StackSave.Into(crate.Items, p.Items);
            var n = new CargoCrateNode(this, crate);
            AddChild(n);
            n.GlobalPosition = V(p.E.Pos);
            n.Quaternion = QuatOf(p.E);
            n.OnRamp = p.E.Params.ContainsKey("ramp");
            if (CartOf(p.E) is ElArabasi cart) cart.TryLoad(n, out _);
        }
        foreach (var e in s.Swollen)
        {
            var size = Data.Crops.SizeClasses.FirstOrDefault(c => c.Id == e.Params.GetValueOrDefault("size"));
            if (size == null) continue;
            float scale = float.TryParse(e.Params.GetValueOrDefault("scale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sc) ? sc : 1f;
            var crop = MakeSwollen(e.Content, size, scale);
            AddChild(crop);
            crop.GlobalPosition = V(e.Pos);
            crop.Quaternion = QuatOf(e);
            if (CartOf(e) is ElArabasi cart) cart.TryLoad(crop, out _);
        }
    }

    static Quaternion QuatOf(EntityRecord e) => e.Rot is { Length: 4 } r ? new Quaternion(r[0], r[1], r[2], r[3]).Normalized() : Quaternion.Identity;

    /// <summary>Tarladan sokulmus kabarik urunun yeniden kurulumu (PlotView.Lift ile ayni model ve isilti).</summary>
    SwollenCrop MakeSwollen(string cropId, SizeClassDef size, float scale)
    {
        var model = Assets.Load("res://assets/models/salgam/salgam.glb");
        if (model != null)
        {
            model.Position = new Vector3(0, -0.1f, 0);
            var sw = new Swellable(model, 6f);
            return new SwollenCrop(cropId, size, model, g => sw.Set(glow: g), scale);
        }
        var kit = new MeshKit();
        kit.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.18f, 0), Vector3.Zero, new Vector3(1f, 0.95f, 1f)), Palette.TurnipBody, 12, 8);
        var mat = MeshKit.NewLiveMaterial();
        return new SwollenCrop(cropId, size, kit.Instance(mat), g => mat.SetShaderParameter("glow", g), scale);
    }

    /// <summary>Sinama (SaveTest): yerde duran kabarik urun.</summary>
    internal SwollenCrop MakeSwollenForTest(string cropId, SizeClassDef size, float scale) => MakeSwollen(cropId, size, scale);

    /// <summary>Rampadaki kasa dugumleri Cargo'daki ayni kimlikli kasanin yerine gecer (tek nesne); rampa dizilir.</summary>
    void AdoptRampCrates()
    {
        foreach (var n in GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().Where(c => c.OnRamp)) State.Cargo.Adopt(n.Crate);
        LayoutRamp();
    }

    // ---------- oyuncu ----------

    PlayersSave CapturePlayer() => new()
    {
        Players = new()
        {
            new PlayerSave
            {
                Pos = V(Player.GlobalPosition), Yaw = Player.Yaw.Rotation.Y, Pitch = Player.Pitch.Rotation.X, Hp = Player.Hp,
                Respawn = V(Player.RespawnPoint), Visible = Player.Visible, Head = Capa.Current.ToString(),
            },
        },
    };

    void ApplyPlayer(PlayersSave s)
    {
        var p = s.Players.FirstOrDefault(x => x.Player == GameState.LocalPlayer) ?? s.Players.FirstOrDefault();
        if (p == null) return;
        Player.GlobalPosition = V(p.Pos);
        Player.Velocity = Vector3.Zero;
        Player.Yaw.Rotation = new Vector3(0, p.Yaw, 0);
        Player.Pitch.Rotation = new Vector3(p.Pitch, 0, 0);
        Player.Hp = p.Hp;
        Player.RespawnPoint = V(p.Respawn);
        Player.Visible = p.Visible;
        if (System.Enum.TryParse<Capa.Head>(p.Head, out var h)) Capa.LoadHead(h);
    }

    // ---------- yaratiklar ----------

    CreaturesSave CaptureCreatures() => new()
    {
        GidakVisible = Gidak.Visible, GidakPos = V(Gidak.GlobalPosition), GidakYaw = Gidak.Rotation.Y, GidakFed = Gidak.Fed,
    };

    void ApplyCreatures(CreaturesSave s)
    {
        Gidak.Visible = s.GidakVisible;
        Gidak.GlobalPosition = V(s.GidakPos);
        Gidak.Rotation = new Vector3(0, s.GidakYaw, 0);
        Gidak.Fed = s.GidakFed;
    }

    /// <summary>Gidak dugumu Coop'taki ilk Gidak kaydina baglanir (Coop bolumu hayvanlari yeniden kurdu).</summary>
    void BindPenAnimals()
    {
        // Kayitta Gidak yoksa (adimdan once) bag kopar: Animal ozelligi gerektiginde yenisini Coop'a ekler.
        Gidak.BindAnimal(State.Coop.Animals.FirstOrDefault(x => x.Species == Gidak.Def.Id));
    }

    // ---------- yonetmen: evre, senaryo yardimci sayaclari, rampa ----------

    DirectorSave CaptureDirector() => new()
    {
        Phase = Phase.ToString(),
        ReportShown = _reportShown,
        Watered = _wateredPlots.OrderBy(w => w.Item1).ThenBy(w => w.Item2).Select(w => new[] { w.Item1, w.Item2 }).ToList(),
        Made = _madeThisStep.OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
        Placed = _placedIds.OrderBy(x => x, System.StringComparer.Ordinal).ToList(),
        Harvests = Harvests,
        PodRamp = Pod.IsRamp,
    };

    void ApplyDirector(DirectorSave s)
    {
        Phase = System.Enum.TryParse<Act>(s.Phase, out var a) && a is Act.Day or Act.Free ? a : Act.Free;
        _actT = 0;
        _reportShown = s.ReportShown;
        _wateredPlots.Clear();
        foreach (var w in s.Watered) if (w.Length >= 2) _wateredPlots.Add((w[0], w[1]));
        _madeThisStep.Clear(); foreach (var m in s.Made) _madeThisStep.Add(m);
        _placedIds.Clear(); foreach (var p in s.Placed) _placedIds.Add(p);
        Harvests = s.Harvests;
        if (s.PodRamp && !Pod.IsRamp) Pod.BecomeRamp();
    }
}
