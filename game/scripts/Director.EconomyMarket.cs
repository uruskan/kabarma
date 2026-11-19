using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director, ekonomi2: Kargo Rampasi terminali (Borsa, Katalog, Tohum Carki; GDD 13, ICERIK 18) ve katalog teslimatlari
/// (kapsul Ocak'in yanina iner, E ile acilir; arac govdesi rampaya iner). Kurallar Sim'de (Market, Catalog, Wheel);
/// burasi menu satirlarini kurar ve dunyaya baglar. Sol sutun cizimi Hud.Market.cs.
/// </summary>
public partial class Director
{
    public static string BorsaTitle => Loc.T("dir.borsa_title");
    public static string CatalogTitle => Loc.T("dir.catalog_title");
    public static string WheelTitle => Loc.T("dir.wheel_title");
    /// <summary>Rampa panelinde terminal satirlarinin ilki (Borsa); Katalog +1, Cark +2 (bot ve sinama icin).</summary>
    public int RampTerminalRow { get; private set; } = -1;

    /// <summary>InitEconomy'den bir kez.</summary>
    void InitMarketTerminal()
    {
        State.Catalog.Arrived += OnDeliveryArrived;
        State.Market.DayRolled += day =>
        {
            var hot = State.Market.State.Hot;
            GD.Print($"BORSA day={day} hot={hot}");
            if (hot.Length > 0) Hud.Hint(Loc.T("dir.hint_borsa_hot", State.Market.CategoryName(hot)), 4f);
        };
        // Kayittan gelen inmis teslimatlar: kapsulleri yerinde kur.
        foreach (var dl in State.Catalog.Deliveries.Where(d => d.Arrived).ToList()) OnDeliveryArrived(dl);
        if (Main.Args.ContainsKey("borsatest")) AddChild(new MarketTest(this));
    }

    // ---------- Rampa paneli: terminal satirlari ----------

    void AddTerminalRows(List<Hud.MenuItem> items)
    {
        Hud.SetMarketPanel("borsa", Loc.T("dir.ramp_title"));
        RampTerminalRow = items.Count;
        var m = State.Market;
        string hot = m.State.Hot.Length > 0 ? Loc.T("dir.borsa_hot_today", m.CategoryName(m.State.Hot)) : m.State.Open ? Loc.T("dir.borsa_no_hot") : Loc.T("dir.borsa_first_morning");
        items.Add(new Hud.MenuItem(Loc.T("dir.row_borsa", hot), () => true, () => OpenBorsaMenu()));
        int onWay = State.Catalog.Deliveries.Count;
        items.Add(new Hud.MenuItem(Loc.T("dir.row_catalog", State.Credits, onWay > 0 ? Loc.T("dir.row_catalog_deliveries", onWay) : ""), () => true, OpenCatalogMenu));
        items.Add(new Hud.MenuItem(Loc.T("dir.row_wheel", State.WheelFreeToday ? Loc.T("dir.wheel_free_today") : Loc.T("dir.credits_n", State.Wheel.Cost)), () => true, OpenWheelMenu));
    }

    Hud.MenuItem BackToRamp() => new(Loc.T("dir.back_to_ramp"), () => true, OpenRampMenu);

    // ---------- Borsa ----------

    public void OpenBorsaMenu(string select = null)
    {
        var m = State.Market;
        if (select != null) Hud.MarketCategory = select;
        var items = new List<Hud.MenuItem>();
        foreach (var c in m.Categories)
        {
            var id = c.Id;
            string tag = m.IsHot(id) ? Loc.T("dir.borsa_hot_tag") : "";
            items.Add(new Hud.MenuItem($"{c.Name}  ×{m.Factor(id):0.00}{tag}", () => true, () => { Hud.MarketCategory = id; OpenBorsaMenu(); }));
        }
        items.Add(BackToRamp());
        Hud.SetMarketPanel("borsa", BorsaTitle);
        Hud.ShowMenu(BorsaTitle, items, Loc.T("dir.borsa_foot"));
    }

    // ---------- Katalog ----------

    public void OpenCatalogMenu()
    {
        var cat = State.Catalog;
        var items = new List<Hud.MenuItem>();
        foreach (var e in cat.Visible)
        {
            var entry = e;
            int price = cat.Price(e);
            string what = e.Items.Count > 0 ? string.Join(", ", e.Items.Select(kv => $"{ItemNames.Of(kv.Key)} ×{kv.Value}")) : ItemNames.Of(e.Load);
            string via = e.Via == "rampa" ? Loc.T("dir.via_ramp", cat.Seconds(e.Via).ToString("0", System.Globalization.CultureInfo.InvariantCulture)) : Loc.T("dir.via_pod", cat.Seconds(e.Via).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
            items.Add(new Hud.MenuItem(Loc.T("dir.catalog_row", e.Name, price, what, via), () => State.Credits >= price, () => BuyCatalog(entry.Id),
                e.Items.Keys.FirstOrDefault() ?? (e.Load.Length > 0 ? e.Load : null)));
        }
        items.Add(BackToRamp());
        Hud.SetMarketPanel("katalog", CatalogTitle);
        Hud.ShowMenu(CatalogTitle, items, Loc.T("dir.catalog_foot", State.Credits));
    }

    /// <summary>Katalog satiri: Krediyi duser, teslimat yola cikar.</summary>
    public bool BuyCatalog(string entryId)
    {
        var e = State.Catalog.Entry(entryId);
        int price = e == null ? 0 : State.Catalog.Price(e);
        var dl = State.BuyCatalog(entryId);
        if (dl == null) { Hud.Hint(Loc.T("dir.hint_no_credits"), 1.5f); Sfx.Play("hit", null, -12f); return false; }
        Sfx.Play("pickup", null, -4f);
        Hud.Big(Loc.T("dir.big_credits_minus", price), Palette.Warning, 2f);
        if (dl.Id == 0) Hud.Hint(Loc.T("dir.hint_in_wardrobe", e.Name), 3f); else Hud.Hint(dl.Via == "rampa" ? Loc.T("dir.hint_arrives_ramp", e.Name, dl.Left.ToString("0", System.Globalization.CultureInfo.InvariantCulture)) : Loc.T("dir.hint_arrives_pod", e.Name, dl.Left.ToString("0", System.Globalization.CultureInfo.InvariantCulture)), 3f);
        GD.Print($"CATALOG buy {entryId} -{price} credits={State.Credits} via={dl.Via} id={dl.Id}");
        Events.RaiseCatalogBought(entryId, price);
        OpenCatalogMenu();
        return true;
    }

    // ---------- Tohum Carki ----------

    public void OpenWheelMenu()
    {
        var items = new List<Hud.MenuItem>
        {
            new(State.WheelFreeToday ? Loc.T("dir.wheel_spin_free") : Loc.T("dir.wheel_spin_cost", State.Wheel.Cost),
                () => State.WheelFreeToday || State.Credits >= State.Wheel.Cost, () => SpinWheel()),
            BackToRamp(),
        };
        Hud.SetMarketPanel("cark", WheelTitle);
        Hud.ShowMenu(WheelTitle, items, Loc.T("dir.wheel_foot"));
    }

    public WheelResult SpinWheel()
    {
        var r = State.SpinWheel();
        if (r == null) { Hud.Hint(Loc.T("dir.hint_no_credits"), 1.5f); Sfx.Play("hit", null, -12f); return null; }
        string got = r.Reward.Kind == "credits" ? Loc.T("dir.big_credits_plus", r.Credits) : r.Reward.Kind == "items" ? Loc.T("dir.wheel_reward_pod", r.Reward.Name) : Loc.T("dir.wheel_reward_depot", r.Reward.Name);
        Hud.Big(got, Palette.Warning, 2.5f);
        Sfx.Play("pickup", null, 0f);
        Fx.Burst(Pod.PadCenter + Vector3.Up * 1.5f, Palette.Warning, 24, 5f, 0.1f, 0.8f, true, 3f);
        GD.Print($"WHEEL spin reward={r.Reward.Id} credits={r.Credits} free={r.Free} paid={r.Paid} wallet={State.Credits}");
        Events.RaiseWheelSpun(r.Reward.Id, r.Credits);
        WheelCosmetic(r.Reward.Id); // actors/Director.Ilerleme4.cs: kozmetik odulu dolapta
        OpenWheelMenu();
        return r;
    }

    // ---------- Teslimat ----------

    void OnDeliveryArrived(Delivery dl)
    {
        if (dl.Via == "rampa" && dl.Load.Length > 0)
        {
            SpawnRampLoad(dl);
            return;
        }
        if (dl.Items.Count == 0) return;
        var pod = new EconomyDeliveryPod(dl, DeliverySpot(dl.Id));
        AddChild(pod);
        GD.Print($"DELIVERY {dl.Id} {dl.Source} landing at {DeliverySpot(dl.Id)}");
        Hud.Hint(Loc.T("dir.hint_catalog_pod_landing"), 3f);
    }

    /// <summary>Kapsulun inecegi yer: Ocak'in yaninda (Ocak yoksa rampanin yaninda), istasyon ve esyalardan uzak.</summary>
    Vector3 DeliverySpot(int id)
    {
        var basePos = Ocak != null ? OcakPos : Pod.PadCenter + Pod.GlobalBasis.Z * 6f;
        var dir = Pod.PadCenter - basePos; dir.Y = 0;
        dir = dir.Length() > 0.5f ? dir.Normalized() : Vector3.Right;
        var taken = GetTree().GetNodesInGroup("deliveries").OfType<Node3D>().Select(n => n.GlobalPosition).ToList();
        for (int k = 0; k < 24; k++)
        {
            float r = 3.4f + (k / 8) * 1.4f;
            var p = basePos + dir.Rotated(Vector3.Up, 0.7f + k * 0.785f) * r;
            bool clear = Stations.All(s => s.GlobalPosition.DistanceTo(p) > 2.4f)
                && Furniture.All(f => f.GlobalPosition.DistanceTo(p) > 1.8f)
                && new Vector2(p.X - Pod.PadCenter.X, p.Z - Pod.PadCenter.Z).Length() > 5.2f
                && Pod.GlobalPosition.DistanceTo(p) > 3f
                && taken.All(t => new Vector2(t.X - p.X, t.Z - p.Z).Length() > 1.6f);
            if (clear) return World.Ground(p);
        }
        return World.Ground(basePos + dir.Rotated(Vector3.Up, 1.2f) * 4f);
    }

    /// <summary>Arac govdesi rampaya iner (El Arabasi): rampanin yanina yuk urunu olarak.</summary>
    void SpawnRampLoad(Delivery dl)
    {
        if (dl.Load == "el_arabasi")
        {
            var cart = new ElArabasi(this);
            AddChild(cart);
            var at = World.Ground(Pod.PadCenter + Pod.GlobalBasis.X * 3.2f) + Vector3.Up * 0.6f;
            cart.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Pod.Rotation.Y), at);
            Carts.Add(cart);
            cart.TreeExiting += () => Carts.Remove(cart);
            Fx.Burst(at + Vector3.Up * 0.5f, Palette.Warning, 24, 5f, 0.1f, 0.8f, true, 3f);
            Sfx.Play("place", at, 0f);
        }
        GD.Print($"DELIVERY {dl.Id} {dl.Source} ramp load {dl.Load}");
        Hud.Hint(Loc.T("dir.hint_ramp_arrived", ItemNames.Of(dl.Load)), 3f);
        State.Catalog.LoadTaken(dl);
        Events.RaiseDelivered(dl.Source, 1);
    }

    /// <summary>Inmis kapsul erisimdeyse acar (E): icindekiler cantaya. Isledi ise true.</summary>
    bool OpenDeliveryInReach()
    {
        var p = Player.GlobalPosition;
        var pod = GetTree().GetNodesInGroup("deliveries").OfType<EconomyDeliveryPod>().Where(x => x.InReach(p))
            .OrderBy(x => x.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (pod == null) return false;
        var dl = pod.Delivery;
        int n = State.Catalog.Collect(dl, State.Backpack);
        if (n <= 0)
        {
            Hud.Hint(Loc.T("dir.hint_bag_full_pod"), 2f);
            Sfx.Play("hit", null, -12f);
            return true;
        }
        Sfx.Play("pickup", pod.GlobalPosition, 0f);
        GD.Print($"DELIVERED {dl.Id} {dl.Source} +{n} left={dl.Items.Values.Sum()}");
        Events.RaiseDelivered(dl.Source, n);
        if (dl.Empty)
        {
            Fx.Burst(pod.GlobalPosition + Vector3.Up * 0.5f, Palette.OffWhite, 20, 4f, 0.1f, 0.6f);
            pod.QueueFree();
            Hud.Hint(Loc.T("dir.hint_pod_opened", n), 2f);
        }
        else Hud.Hint(Loc.T("dir.hint_pod_partial", n), 2.5f);
        return true;
    }
}
