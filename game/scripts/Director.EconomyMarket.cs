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
    public const string BorsaTitle = "Borsa", CatalogTitle = "Katalog", WheelTitle = "Tohum Çarkı";
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
            if (hot.Length > 0) Hud.Hint($"Borsa: bugün {State.Market.CategoryName(hot)} sıcak (×2)", 4f);
        };
        // Kayittan gelen inmis teslimatlar: kapsulleri yerinde kur.
        foreach (var dl in State.Catalog.Deliveries.Where(d => d.Arrived).ToList()) OnDeliveryArrived(dl);
        if (Main.Args.ContainsKey("borsatest")) AddChild(new MarketTest(this));
    }

    // ---------- Rampa paneli: terminal satirlari ----------

    void AddTerminalRows(List<Hud.MenuItem> items)
    {
        Hud.SetMarketPanel("borsa", "Kargo Rampası");
        RampTerminalRow = items.Count;
        var m = State.Market;
        string hot = m.State.Hot.Length > 0 ? $"bugün sıcak: {m.CategoryName(m.State.Hot)}" : m.State.Open ? "bugün sıcak yok" : "fiyatlar ilk sabah oynar";
        items.Add(new Hud.MenuItem($"Borsa   ←   12 kategori · {hot}", () => true, () => OpenBorsaMenu()));
        int onWay = State.Catalog.Deliveries.Count;
        items.Add(new Hud.MenuItem($"Katalog   ←   {State.Credits} Kredi{(onWay > 0 ? $" · {onWay} teslimat" : "")}", () => true, OpenCatalogMenu));
        items.Add(new Hud.MenuItem($"Tohum Çarkı   ←   {(State.WheelFreeToday ? "bugün bedava" : $"{State.Wheel.Cost} Kredi")}", () => true, OpenWheelMenu));
    }

    Hud.MenuItem BackToRamp() => new("Geri: Kargo Rampası", () => true, OpenRampMenu);

    // ---------- Borsa ----------

    public void OpenBorsaMenu(string select = null)
    {
        var m = State.Market;
        if (select != null) Hud.MarketCategory = select;
        var items = new List<Hud.MenuItem>();
        foreach (var c in m.Categories)
        {
            var id = c.Id;
            string tag = m.IsHot(id) ? "  · SICAK" : "";
            items.Add(new Hud.MenuItem($"{c.Name}  ×{m.Factor(id):0.00}{tag}", () => true, () => { Hud.MarketCategory = id; OpenBorsaMenu(); }));
        }
        items.Add(BackToRamp());
        Hud.SetMarketPanel("borsa", BorsaTitle);
        Hud.ShowMenu(BorsaTitle, items, "Satırı seç: son 30 günü solda büyük gör. Satış Kargo Rampası'ndan; fiyat fırlatma anında kilitlenir.  ·  {act:interact} / {act:ui_cancel}: kapat");
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
            string via = e.Via == "rampa" ? $"rampaya {cat.Seconds(e.Via):0} sn" : $"kapsülle {cat.Seconds(e.Via):0} sn";
            items.Add(new Hud.MenuItem($"{e.Name}   ←   {price} Kredi · {what} · {via}", () => State.Credits >= price, () => BuyCatalog(entry.Id),
                e.Items.Keys.FirstOrDefault() ?? (e.Load.Length > 0 ? e.Load : null)));
        }
        items.Add(BackToRamp());
        Hud.SetMarketPanel("katalog", CatalogTitle);
        Hud.ShowMenu(CatalogTitle, items, $"Cüzdan: {State.Credits} Kredi (ekipçe ortak).  ·  {{act:interact}} / {{act:ui_cancel}}: kapat");
    }

    /// <summary>Katalog satiri: Krediyi duser, teslimat yola cikar.</summary>
    public bool BuyCatalog(string entryId)
    {
        var e = State.Catalog.Entry(entryId);
        int price = e == null ? 0 : State.Catalog.Price(e);
        var dl = State.BuyCatalog(entryId);
        if (dl == null) { Hud.Hint("Kredi yetmiyor", 1.5f); Sfx.Play("hit", null, -12f); return false; }
        Sfx.Play("pickup", null, -4f);
        Hud.Big($"-{price} Kredi", Palette.Warning, 2f);
        if (dl.Id == 0) Hud.Hint($"{e.Name}: dolapta (K)", 3f); else Hud.Hint(dl.Via == "rampa" ? $"{e.Name}: {dl.Left:0} sn sonra Kargo Rampası'na iner" : $"{e.Name}: kapsül {dl.Left:0} sn sonra Ocak'ın yanına iner", 3f);
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
            new(State.WheelFreeToday ? "Çevir   ←   bugün bedava (günün ilk çevirmesi)" : $"Çevir   ←   {State.Wheel.Cost} Kredi",
                () => State.WheelFreeToday || State.Credits >= State.Wheel.Cost, () => SpinWheel()),
            BackToRamp(),
        };
        Hud.SetMarketPanel("cark", WheelTitle);
        Hud.ShowMenu(WheelTitle, items, "Gerçek para yok: çark yalnız oyun içi Krediyle döner. Alaca ürünle çevirme tarım gelince.  ·  {act:interact} / {act:ui_cancel}: kapat");
    }

    public WheelResult SpinWheel()
    {
        var r = State.SpinWheel();
        if (r == null) { Hud.Hint("Kredi yetmiyor", 1.5f); Sfx.Play("hit", null, -12f); return null; }
        string got = r.Reward.Kind == "credits" ? $"+{r.Credits} Kredi" : r.Reward.Kind == "items" ? $"{r.Reward.Name}: kapsülle gelir" : $"{r.Reward.Name}: Kooperatif deposunda";
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
        Hud.Hint("Katalog kapsülü iniyor: Ocak'ın yanında, E ile aç", 3f);
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
        Hud.Hint($"{ItemNames.Of(dl.Load)} Kargo Rampası'na indi", 3f);
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
            Hud.Hint("Çanta dolu: kapsül yerinde bekler", 2f);
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
            Hud.Hint($"Kapsül açıldı: +{n} çantaya", 2f);
        }
        else Hud.Hint($"+{n} çantaya; çanta dolu, kalan kapsülde", 2.5f);
        return true;
    }
}
