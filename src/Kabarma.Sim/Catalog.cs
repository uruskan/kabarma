namespace Kabarma.Sim;

/// <summary>Yoldaki ya da inmis bir katalog teslimati (kayda hazir, TEKNIK §9).</summary>
public sealed class Delivery
{
    public int Id { get; set; }
    /// <summary>Katalog kalemi ya da "cark:odul".</summary>
    public string Source { get; set; } = "";
    /// <summary>kapsul ya da rampa.</summary>
    public string Via { get; set; } = "kapsul";
    /// <summary>Inmesine kalan sn.</summary>
    public float Left { get; set; }
    public bool Arrived { get; set; }
    /// <summary>Kapsulde kalan esyalar (kimlik -> adet).</summary>
    public Dictionary<string, int> Items { get; set; } = new();
    /// <summary>Rampaya inecek yuk urunu (bos: yok ya da indirildi).</summary>
    public string Load { get; set; } = "";
    public bool Empty => Items.Count == 0 && Load.Length == 0;
}

public sealed class CatalogState
{
    public int Serial { get; set; }
    public List<Delivery> Deliveries { get; set; } = new();
    /// <summary>Kalem -> alinma sayisi.</summary>
    public Dictionary<string, int> Bought { get; set; } = new();
}

/// <summary>
/// Katalog (GDD §12-13, ICERIK §18): Krediyle tohum, yumurta, arac govdesi, sus, kozmetik. Satin alinan teslimat kapsulle
/// (Ocak'in yanina) ya da rampayla iner; kapsul acilinca icindekiler cantaya gecer. Tohum Carki'nin esya odulleri de
/// kapsulle gelir. Durum CatalogState'te.
/// </summary>
public sealed class Catalog
{
    readonly GameData _d;
    readonly CatalogFile _c;
    readonly Pricing _pricing;
    public CatalogState State { get; private set; }

    /// <summary>Teslimat indi (Godot kapsulu ya da rampa yukunu kurar).</summary>
    public event Action<Delivery>? Arrived;

    public Catalog(GameData d, Pricing pricing, CatalogState? state = null)
    {
        _d = d;
        _c = d.Catalog;
        _pricing = pricing;
        State = state ?? new CatalogState();
    }

    public void Restore(CatalogState s) => State = s;

    /// <summary>Kalemin gerektirdigi kimlikler veride var mi (tarif, yerlestirilebilir, fiyat ya da ekin).</summary>
    /// <summary>Kozmetik zaten dolapta mi (dolap): sahip olunan kalem gizlenir. Director baglar.</summary>
    public Func<string, bool>? OwnsCosmetic { get; set; }
    /// <summary>Kozmetik satin alindi (kimlik): dolaba acilir.</summary>
    public event Action<string>? CosmeticBought;

    public bool Available(CatalogEntryDef e) => e.Requires.All(Known) && !(e.Cosmetic.Length > 0 && (OwnsCosmetic?.Invoke(e.Cosmetic) ?? false));

    bool Known(string id) =>
        _d.Tool.Recipes.Any(r => r.Id == id) || _d.Tool.Placeables.Any(p => p.Id == id) || _d.Tool.Prices.ContainsKey(id) || _d.Crops.Crops.Any(c => c.Id == id);

    /// <summary>Gorunen kalemler (veri sirasi).</summary>
    public IEnumerable<CatalogEntryDef> Visible => _c.Entries.Where(Available);

    public CatalogEntryDef? Entry(string id) => _c.Entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Fiyat: sabit fiyat ya da tohum paketi (urunun taban fiyati × 20, ICERIK §18). Borsadan etkilenmez.</summary>
    public Func<float>? PriceMult { get; set; } // dolap: Muhtar katalog -%10
    public int Price(CatalogEntryDef e) => (int)Math.Round(BasePrice(e) * (PriceMult?.Invoke() ?? 1f), MidpointRounding.AwayFromZero);

    int BasePrice(CatalogEntryDef e) =>
        e.Price > 0 ? e.Price : e.SeedPackOf.Length > 0 ? (int)Math.Round(_pricing.Base(e.SeedPackOf) * _c.SeedPackMult, MidpointRounding.AwayFromZero) : 0;

    public float Seconds(string via) => via == "rampa" ? _c.RampSeconds : _c.CapsuleSeconds;

    /// <summary>
    /// Satin alir: <paramref name="spend"/> Krediyi duserse (yeterliyse true) teslimat yola cikar. Donen: teslimat ya da null
    /// (kalem yok, gizli, fiyatsiz ya da Kredi yetmedi).
    /// </summary>
    public Delivery? Buy(string id, Func<int, bool> spend)
    {
        var e = Entry(id);
        if (e == null || !Available(e)) return null;
        int price = Price(e);
        if (price <= 0 || !spend(price)) return null;
        State.Bought[e.Id] = State.Bought.GetValueOrDefault(e.Id) + 1;
        if (e.Cosmetic.Length > 0)
        {
            CosmeticBought?.Invoke(e.Cosmetic); // teslimat yok: aninda dolapta
            return new Delivery { Id = 0, Source = e.Id, Via = "kapsul", Left = 0, Arrived = true };
        }
        return Send(e.Id, e.Via, e.Items, e.Load);
    }

    /// <summary>Bedelsiz teslimat (Tohum Carki'nin esya odulu).</summary>
    public Delivery Send(string source, string via, IReadOnlyDictionary<string, int> items, string load = "")
    {
        var dl = new Delivery
        {
            Id = ++State.Serial,
            Source = source,
            Via = via,
            Left = Seconds(via),
            Items = items.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value),
            Load = load ?? "",
        };
        State.Deliveries.Add(dl);
        return dl;
    }

    public void Tick(float dt)
    {
        foreach (var dl in State.Deliveries)
        {
            if (dl.Arrived) continue;
            dl.Left -= dt;
            if (dl.Left > 0) continue;
            dl.Left = 0;
            dl.Arrived = true;
            Arrived?.Invoke(dl);
        }
    }

    /// <summary>Inmis kapsulu acar: icindekiler cantaya (sigdigi kadar). Donen: cantaya giren toplam adet. Bosalan teslimat silinir.</summary>
    public int Collect(Delivery dl, Inventory to)
    {
        if (!dl.Arrived) return 0;
        int moved = 0;
        foreach (var id in dl.Items.Keys.ToList())
        {
            int n = to.Add(id, dl.Items[id]);
            moved += n;
            dl.Items[id] -= n;
            if (dl.Items[id] <= 0) dl.Items.Remove(id);
        }
        if (dl.Empty) State.Deliveries.Remove(dl);
        return moved;
    }

    /// <summary>Rampaya inen yuk dunyaya cikti (Godot kurdu).</summary>
    public void LoadTaken(Delivery dl)
    {
        dl.Load = "";
        if (dl.Empty) State.Deliveries.Remove(dl);
    }

    public IReadOnlyList<Delivery> Deliveries => State.Deliveries;
}
