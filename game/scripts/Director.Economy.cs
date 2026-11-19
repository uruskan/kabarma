using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, ekonomi akisi: yerlestirilebilirler (Sandik, Uyku Tulumu, Mesale), yuk urunleri (El Arabasi, kargo kasasi),
/// Paketleme Tezgahi'nda kasa doldurma, Kargo Rampasi'na yukleme ve gonderme, gecikmeli odeme (GDD 13), El Arabasi (ICERIK 14),
/// uyku (Events.RaiseSlept). Kurallar Sim'de (Cargo, Pricing, Structures); burasi dunyaya baglar.
/// </summary>
public partial class Director
{
    public readonly List<Furniture> Furniture = new();
    public readonly List<ElArabasi> Carts = new();
    readonly Dictionary<SwollenCrop, (bool held, Vector3 at, float t)> _dropWatch = new();
    /// <summary>El Arabasi tutamagina E ile gecis mesafesi (m, yatay).</summary>
    const float HandleReach = 1.3f;

    /// <summary>Director._Ready'den bir kez.</summary>
    void InitEconomy()
    {
        State.Cargo.Paid += (v, what) =>
        {
            Hud.Big(Loc.T("dir.big_credits_plus", v), Palette.Warning, 2.5f);
            Sfx.Play("pickup", null, 0f);
            GD.Print($"PAID {what} +{v} credits={State.Credits}");
            Events.RaisePaid(v, what);
        };
        if (Main.Args.ContainsKey("ekonomi")) AddChild(new EconomyTest(this));
        InitMarketTerminal(); // Director.EconomyMarket.cs: Borsa, Katalog, Tohum Carki, teslimat kapsulu (ekonomi2)
    }

    /// <summary>Director._Process'ten her karede.</summary>
    void TickEconomy(float dt)
    {
        State.Cargo.Tick(dt);
        State.Catalog.Tick(dt); // teslimatlar (Director.EconomyMarket.cs Arrived'i dinler)
        WatchCrates(dt);
        foreach (var c in Carts) if (c.Pushed) Hud.Hint(Loc.T("dir.hint_cart_pushing"), 0.2f);
    }

    // ---------- yerlestirme ----------

    /// <summary>Istasyon olmayan yerlestirilebilir (Sandik, Uyku Tulumu, Mesale).</summary>
    public Furniture PlaceFurniture(PlaceableDef def, Vector3 at, float yaw)
    {
        if (Defense?.Handles(def.Id) == true) return Defense.Place(def, at, yaw); // savunma M3: kule ve tuzak (ortak "placed" istemcisi)
        var f = new Furniture(this, def);
        AddChild(f);
        f.GlobalPosition = at;
        f.Rotation = new Vector3(0, yaw, 0);
        Furniture.Add(f);
        Sfx.Play("place", at, 2f);
        Fx.Burst(at + Vector3.Up * 0.3f, Palette.Wood, 20, 4f, 0.1f, 0.7f);
        if (def.Spawn) { Player.RespawnPoint = at + Vector3.Up * 0.3f; Hud.Hint(Loc.T("dir.hint_spawn_point"), 2f); }
        GD.Print($"PLACED {def.Id} at {at}");
        Events.RaiseStationPlaced(def.Id);
        return f;
    }

    /// <summary>Istasyonun yuk urununu dunyaya cikarir (Station.OnProduced).</summary>
    public void SpawnLoad(RecipeDef r, Station st)
    {
        var at = st.OutputPoint;
        if (r.Id == "el_arabasi")
        {
            var cart = new ElArabasi(this);
            AddChild(cart);
            cart.GlobalTransform = new Transform3D(new Basis(Vector3.Up, st.Rotation.Y), at + st.GlobalBasis.X * 0.6f);
            Carts.Add(cart);
            cart.TreeExiting += () => Carts.Remove(cart);
            Fx.Burst(at + Vector3.Up * 0.5f, Palette.Wood, 20, 4f, 0.1f, 0.7f);
            GD.Print($"CART spawned at {cart.GlobalPosition}");
        }
        else if (r.Id == "kargo_kasasi")
        {
            var crate = State.Cargo.TakeClosed();
            if (crate == null) { GD.PrintErr("kasa yok: Paketleme sirasi bos kasayla bitti"); return; }
            var n = new CargoCrateNode(this, crate);
            AddChild(n);
            n.GlobalPosition = at + Vector3.Up * CargoCrateNode.HalfHeight;
            Fx.Burst(n.GlobalPosition, Palette.Wood, 16, 3f, 0.08f, 0.6f);
            GD.Print($"CRATE {crate.Id} value={State.Cargo.Value(crate)} stacks={crate.Items.UsedSlots}");
        }
    }

    // ---------- etkilesim (E) ----------

    /// <summary>Director.Interact basinda: El Arabasi, Sandik, Uyku Tulumu. Isledi ise true.</summary>
    public bool InteractEconomy()
    {
        var p = Player.GlobalPosition;
        if (OpenDeliveryInReach()) return true; // Director.EconomyMarket.cs: inmis katalog kapsulu
        var pushed = Carts.FirstOrDefault(c => c.Pushed);
        if (pushed != null) { pushed.Release(); return true; }
        var cart = Carts.Where(c => c.InReach(p)).OrderBy(c => c.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        var furn = Furniture.Where(f => f.Def.LightRange <= 0 && f.InReach(p)).OrderBy(f => f.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        bool cartFirst = cart != null && (furn == null || cart.GlobalPosition.DistanceTo(p) <= furn.GlobalPosition.DistanceTo(p));
        // Istasyon daha yakinsa istasyon menusu (Director.Interact) acilsin.
        var st = Stations.Where(s => s.InReach(p)).OrderBy(s => s.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        float best = cartFirst ? cart.GlobalPosition.DistanceTo(p) : furn?.GlobalPosition.DistanceTo(p) ?? float.MaxValue;
        if (st != null && st.GlobalPosition.DistanceTo(p) < best) return false;
        if (cartFirst)
        {
            // Devrikse E duzeltir; tutamagin yanindaysa E dogrudan tutamaga gecer (URETIM 6 M1); yandan E yuva ve yuk menusu.
            if (cart.Tipped) cart.Right();
            else if (new Vector2(cart.Handle.X - p.X, cart.Handle.Z - p.Z).Length() < HandleReach) cart.Grab();
            else OpenCartMenu(cart);
            return true;
        }
        if (furn == null) return false;
        if (furn.Storage != null) { OpenChestMenu(furn); return true; }
        if (furn.Def.Spawn) { Sleep(furn); return true; }
        if (InteractProp(furn)) return true; // kale M4: hizmet parcalari (Director.Props.cs)
        return false;
    }

    /// <summary>Uyku Tulumu: dogma noktasi olur; uyuyunca normal gece atlanir (GDD 2; dinleyen gun dongusu). Kabarma'da uyunmaz.</summary>
    public bool Sleep(Furniture bag)
    {
        Player.RespawnPoint = bag.GlobalPosition + Vector3.Up * 0.3f;
        if (NightActive) { Hud.Hint(Loc.T("dir.hint_no_sleep_kabarma"), 1.5f); return false; }
        bag.Bounce();
        Sfx.Play("place", bag.GlobalPosition, -8f);
        Hud.Big("Zzz", Palette.CoopBlue, 1.5f);
        GD.Print("SLEPT");
        Events.RaiseSlept();
        return true;
    }

    // ---------- menuler ----------

    static string Stack(Inventory inv) => inv.IsEmpty ? Loc.T("dir.stack_empty") : string.Join("  ·  ", inv.Contents.GroupBy(c => c.id).Select(g => $"{ItemNames.Of(g.Key)} {g.Sum(c => c.count)}{Stars(inv, g.Key)}"));

    /// <summary>Kalite dokumu: hepsi ★1 ise bos, degilse " (★1 4 · ★3 2)".</summary>
    static string Stars(Inventory inv, string id)
    {
        var by = inv.Stacks.Where(s => s.id == id).GroupBy(s => s.stars).OrderBy(g => g.Key).Select(g => (stars: g.Key, n: g.Sum(s => s.count))).ToList();
        if (by.All(x => x.stars <= Inventory.BaseStars)) return "";
        return " (" + string.Join(" · ", by.Select(x => $"{new string('★', x.stars)} {x.n}")) + ")";
    }

    /// <summary>Iki yuva kumesi arasinda tasima menusu (Sandik, araba yuvalari): koy / al.</summary>
    void TransferItems(List<Hud.MenuItem> items, Inventory store, System.Func<bool> allowPut, System.Action reopen, int max = 9)
    {
        foreach (var id in store.Contents.Select(c => c.id).Distinct().ToList())
        {
            var iid = id;
            if (items.Count >= max) break;
            items.Add(new Hud.MenuItem(Loc.T("dir.chest_take", ItemNames.Of(iid), store.Count(iid)), () => store.Count(iid) > 0 && State.Backpack.Room(iid) > 0,
                () => { Net?.ForwardChest(store, iid, true, store.Count(iid)); store.MoveTo(State.Backpack, iid, store.Count(iid)); Sfx.Play("pickup", null, -6f); reopen(); }, iid));
        }
        foreach (var id in State.Backpack.Contents.Select(c => c.id).Distinct().ToList())
        {
            var iid = id;
            if (items.Count >= max) break;
            items.Add(new Hud.MenuItem(Loc.T("dir.chest_put", ItemNames.Of(iid), State.Backpack.Count(iid)), () => allowPut() && State.Backpack.Count(iid) > 0 && store.Room(iid) > 0,
                () => { Net?.ForwardChest(store, iid, false, State.Backpack.Count(iid)); State.Backpack.MoveTo(store, iid, State.Backpack.Count(iid)); Sfx.Play("place", null, -8f); reopen(); }, iid));
        }
    }

    public void OpenChestMenu(Furniture chest)
    {
        chest.Opened = true;
        var items = new List<Hud.MenuItem>();
        AddChestTools(items, chest); // zincir: hizli yigma ve siralama (Director.Zincir.cs, GDD 5)
        TransferItems(items, chest.Storage, () => true, () => OpenChestMenu(chest));
        Hud.ShowMenu(Loc.T("dir.chest_title", chest.Def.Name, chest.Storage.UsedSlots, chest.Storage.Slots), items, Stack(chest.Storage) + "\n" + Loc.T("dir.menu_close"));
    }

    public void OpenCartMenu(ElArabasi cart)
    {
        var items = new List<Hud.MenuItem>
        {
            new(Loc.T("dir.cart_grab"), () => !cart.Tipped, () => { Hud.HideMenu(); cart.Grab(); }),
        };
        if (cart.Load != null) items.Add(new Hud.MenuItem(Loc.T("dir.cart_unload"), () => true, () => { cart.DropLoad(); OpenCartMenu(cart); }));
        TransferItems(items, cart.Slots, () => cart.CanUseSlots, () => OpenCartMenu(cart));
        string what = cart.Load != null ? Loc.T("dir.cart_load_line", cart.Load is CargoCrateNode cc ? Loc.T("dir.cargo_crate_value", State.Cargo.Value(cc.Crate)) : ItemNames.Of(cart.Load.CropId), cart.Load.Size.Kg) : Stack(cart.Slots);
        Hud.ShowMenu(Loc.T("dir.cart_title", cart.Slots.UsedSlots, cart.Slots.Slots), items,
            what + Loc.T("dir.cart_foot", Data.Economy.Cart.MaxLoadKg.ToString("0", System.Globalization.CultureInfo.InvariantCulture), Data.Economy.Cart.Slots));
    }

    /// <summary>Paketleme Tezgahi: cantadan acik kasaya mal koy, kasayi kapat (kasa yuk nesnesi olarak cikar).</summary>
    public void OpenPackingMenu(Station st)
    {
        var cargo = State.Cargo;
        var items = new List<Hud.MenuItem>();
        var r = st.Queue.Recipes.First(x => x.Id == "kargo_kasasi");
        items.Add(new Hud.MenuItem(Loc.T("dir.pack_close", r.Seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)), () => cargo.Open != null && !cargo.Open.Items.IsEmpty && st.Queue.Active == null, () =>
        {
            if (cargo.Close() != null && st.Queue.Enqueue(r, State.Backpack)) { Sfx.Play("place", st.GlobalPosition, -4f); GD.Print($"QUEUED {r.Id}"); Hud.HideMenu(); }
        }));
        foreach (var id in State.Backpack.Contents.Select(c => c.id).Distinct().Where(cargo.Pricing.Sellable).Take(8).ToList())
        {
            var iid = id;
            items.Add(new Hud.MenuItem(Loc.T("dir.pack_put", ItemNames.Of(iid), State.Backpack.Count(iid), Stars(State.Backpack, iid), cargo.Pricing.Base(iid).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)),
                () => State.Backpack.Count(iid) > 0 && st.Queue.Active == null && (cargo.Open == null || cargo.Open.Items.Room(iid) > 0),
                () => { int n = cargo.Pack(State.Backpack, iid, State.Backpack.Count(iid)); if (n > 0) { Sfx.Play("place", st.GlobalPosition, -8f); GD.Print($"PACKED {iid} x{n}"); } OpenPackingMenu(st); }, iid));
        }
        var open = cargo.Open;
        string foot = open == null ? Loc.T("dir.pack_empty") : Loc.T("dir.pack_status", open.Items.UsedSlots, open.Items.Slots, cargo.Value(open), open.Kg.ToString("0", System.Globalization.CultureInfo.InvariantCulture), Stack(open.Items));
        Hud.ShowMenu(st.Def.Name, items, foot + Loc.T("dir.pack_foot"));
    }

    /// <summary>Kargo Rampasi menusu (Director.Input: rampanin dibinde E): rampadaki kasalari gonder.</summary>
    public void OpenRampMenu()
    {
        var cargo = State.Cargo;
        var items = new List<Hud.MenuItem>();
        int n = cargo.Pad.Count;
        int value = cargo.Pad.Sum(cargo.Value);
        string pay = cargo.FirstSaleDone ? Loc.T("dir.ramp_pay_later", Data.Economy.Ramp.PayoutSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)) : Loc.T("dir.ramp_pay_now");
        items.Add(new Hud.MenuItem(Loc.T("dir.ramp_send", n, value, pay), () => cargo.Pad.Count > 0, LaunchRamp));
        var cart = Carts.FirstOrDefault(c => c.Load is CargoCrateNode && c.GlobalPosition.DistanceTo(Pod.PadCenter) < 6f);
        if (cart != null) items.Add(new Hud.MenuItem(Loc.T("dir.ramp_load_cart"), () => cart.Load is CargoCrateNode && cargo.CanLoadCrate, () =>
        {
            var c = (CargoCrateNode)cart.Load;
            cart.Detach(c);
            LoadCrateOnRamp(c);
            OpenRampMenu();
        }));
        string pending = cargo.Pending.Count > 0 ? Loc.T("dir.ramp_pending", string.Join(", ", cargo.Pending.Select(x => Loc.T("dir.ramp_pending_item", x.Credits, x.Left.ToString("0", System.Globalization.CultureInfo.InvariantCulture))))) : "";
        AddTerminalRows(items); // Director.EconomyMarket.cs: Borsa, Katalog, Tohum Carki (satirlarin sonunda; Gonder hep 0)
        Hud.ShowMenu(Loc.T("dir.ramp_title"), items, Loc.T("dir.ramp_foot", n, Data.Economy.Ramp.MaxCrates, pending));
    }

    // ---------- rampa ----------

    /// <summary>Kasayi rampaya koyar (en fazla 6). Rampa doluysa yanina birakir.</summary>
    public bool LoadCrateOnRamp(CargoCrateNode c)
    {
        if (c.OnRamp) return true;
        if (c.OnCart != null) c.OnCart.Detach(c);
        if (!State.Cargo.LoadCrate(c.Crate))
        {
            Hud.Hint(Loc.T("dir.hint_ramp_full", Data.Economy.Ramp.MaxCrates), 2f);
            var g = World.Ground(Pod.PadCenter + (Player.GlobalPosition - Pod.PadCenter).Normalized() * 3.5f);
            c.CreateTween().TweenProperty(c, "global_position", g + Vector3.Up * c.Radius, 0.35f);
            return false;
        }
        c.OnRamp = true;
        LayoutRamp();
        Sfx.Play("place", Pod.PadCenter, -2f);
        GD.Print($"RAMP load crate {c.Crate.Id} pad={State.Cargo.Pad.Count}");
        return true;
    }

    /// <summary>Rampadaki kasalar 2 × 3 duzende dizilir.</summary>
    void LayoutRamp()
    {
        var crates = GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().Where(c => c.OnRamp && !c.Sold).ToList();
        var b = Pod.GlobalBasis;
        for (int i = 0; i < crates.Count; i++)
        {
            var off = b.X * ((i % 3) - 1) * 0.95f + b.Z * ((i / 3) - 0.5f) * 0.95f;
            var to = Pod.PadCenter + off + Vector3.Up * (CargoCrateNode.HalfHeight + 0.02f);
            crates[i].CreateTween().TweenProperty(crates[i], "global_position", to, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    void LaunchRamp()
    {
        var cargo = State.Cargo;
        int n = cargo.Pad.Count;
        var nodes = GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().Where(c => c.OnRamp && !c.Sold).ToList();
        bool instant = !cargo.FirstSaleDone && Data.Economy.Ramp.FirstSaleInstant;
        int v = cargo.LaunchCrates();
        if (v <= 0 && n == 0) return;
        foreach (var c in nodes)
        {
            c.Sold = true;
            c.OnRamp = false;
            var tw = c.CreateTween();
            tw.TweenProperty(c, "global_position", c.GlobalPosition + Vector3.Up * 30f, 2.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(c.QueueFree));
        }
        Pod.Launch();
        Sfx.Play("rocket", Pod.PadCenter, 0f);
        Fx.Burst(Pod.PadCenter + Vector3.Up, Palette.Warning, 40, 8f, 0.12f, 1.2f, true, 6f);
        if (!instant) Hud.Big(Loc.T("dir.big_sent", v, Data.Economy.Ramp.PayoutSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)), Palette.Warning, 2.5f);
        Hud.HideMenu();
        GD.Print($"SOLD kargo_kasasi x{n} +{v} instant={instant}");
        Events.RaiseSold("kargo_kasasi", n, v);
    }

    /// <summary>
    /// Cekim'le birakilan kasayi izler: El Arabasi'nin kasasina birakildiysa arabaya alir; Cekim'le tutulan kasa
    /// rampadan ya da arabadan ayrilir.
    /// </summary>
    void WatchCrates(float dt)
    {
        foreach (var s in GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>())
        {
            if (s.Sold) continue;
            var c = s as CargoCrateNode;
            if (s.Held && c != null && c.OnRamp) { State.Cargo.UnloadCrate(c.Crate); c.OnRamp = false; LayoutRamp(); }
            bool was = _dropWatch.TryGetValue(s, out var w) && w.held;
            if (s.Held) { _dropWatch[s] = (true, s.GlobalPosition, 0); continue; }
            if (!was) continue;
            // Birakildi: tutuldugu son nokta bir arabanin kasasinin ustundeyse arabaya yukle.
            var at = w.at;
            _dropWatch.Remove(s);
            var cart = Carts.Where(k => !k.Pushed && !k.Tipped && new Vector2(k.Bed.X - at.X, k.Bed.Z - at.Z).Length() < k.Length * 0.6f)
                .OrderBy(k => k.Bed.DistanceTo(at)).FirstOrDefault();
            if (cart == null) continue;
            if (cart.TryLoad(s, out var why)) Hud.Hint(Loc.T("dir.hint_load_on_cart"), 1.5f);
            else Hud.Hint(Loc.T("dir.hint_cant_load", why), 1.5f);
        }
        foreach (var k in _dropWatch.Keys.Where(x => !IsInstanceValid(x)).ToList()) _dropWatch.Remove(k);
    }
}
