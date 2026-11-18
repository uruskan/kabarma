using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Hayvancilik (GDD 9.1-9.4): evcillestirme yollari, ihtiyac ve mutluluk, gen ve ureme, roller, kacma, 100 gunluk agil.</summary>
public class HusbandryTests
{
    static PenShape Square(float x0, float z0, float size) => new()
    {
        Polygon = new List<P2> { new(x0, z0), new(x0 + size, z0), new(x0 + size, z0 + size), new(x0, z0 + size) },
        Walls = new List<int>(), Area = size * size, HasGate = true,
    };

    static (GameState s, Ranch r) Fresh(ulong seed = 7)
    {
        var s = new GameState(TestData.Load(), seed);
        return (s, s.Ranch);
    }

    static PenAnimal TameIn(Ranch r, string species, PenShape pen, float x, float z)
    {
        var a = r.Wild(species, x, z);
        a.Trust = 1f;
        Assert.True(r.Register(a, pen));
        return a;
    }

    // ---------- veri ----------

    [Fact]
    public void Eight_species_with_food_roles_and_products_from_ICERIK()
    {
        var d = TestData.Load();
        var h = d.Husbandry;
        Assert.Equal(new[] { "gidak", "pofuduk", "homur", "dalli", "hu", "vicik", "bozuk", "vizviz" }, h.Species.Select(s => s.Id));
        var roleIds = h.Roles.Select(r => r.Id).ToHashSet();
        foreach (var s in h.Species)
        {
            Assert.False(string.IsNullOrEmpty(s.Food), s.Id);
            Assert.NotEmpty(s.Roles);
            Assert.All(s.Roles, r => Assert.Contains(r, roleIds));
            Assert.Contains(s.Tame, new[] { "feed", "subdue", "egg" });
            if (s.Tame == "egg") Assert.False(string.IsNullOrEmpty(s.Egg));
            if (s.Roles.Contains("isci")) Assert.NotNull(s.Work);
            if (s.Roles.Contains("yuk")) Assert.InRange(s.Pack, 8, 24); // GDD 9.4
        }
        Assert.Equal(16, h.SpeciesOf("dalli")!.Pack); // ICERIK 3.2
        Assert.Equal("subdue", h.SpeciesOf("bozuk")!.Tame);   // GDD 9.1 yirtici
        Assert.Equal("egg", h.SpeciesOf("hu")!.Tame);         // GDD 9.1 yumurtadan
        Assert.Equal("hive", h.SpeciesOf("vizviz")!.Home);
        Assert.Equal(12f, h.SpeciesOf("vizviz")!.PollinateRadius); // GDD 8.3
        // ilk gunun Gidak'i pen.json'da: ayni alan, yem, guven ve urun
        var g = d.Pen.Animals.Single(a => a.Id == "gidak");
        var gs = h.SpeciesOf("gidak")!;
        Assert.Equal(g.Area, gs.Area);
        Assert.Equal(g.Food, gs.Food);
        Assert.Equal(g.FoodToTrust, gs.FoodToTrust);
        var egg = gs.Products.Single(p => p.Nest);
        Assert.Equal(g.Product, egg.Item);
        Assert.Equal(g.LayInterval, egg.Seconds);
        Assert.Equal(60f, h.Breeding.MinHappiness); // GDD 9.3
        Assert.Equal(0.25f, h.Subdue.HpFraction);   // GDD 9.1
    }

    // ---------- evcillestirme ----------

    [Fact]
    public void Feed_path_needs_the_favourite_food()
    {
        var (_, r) = Fresh();
        var a = r.Wild("pofuduk", 0, 0);
        Assert.False(r.Rules.Feed(a, "wheat"));
        Assert.Equal(0f, a.Trust);
        Assert.False(r.Rules.Feed(a, "turnip"));
        Assert.False(r.Rules.Feed(a, "turnip"));
        Assert.True(r.Rules.Feed(a, "turnip"));
        Assert.True(a.Tame);
        Assert.Equal("uretici", a.Role);
    }

    [Fact]
    public void Subdue_path_binds_below_quarter_hp_with_cekim_then_feeds()
    {
        var (_, r) = Fresh();
        var b = r.Wild("bozuk", 0, 0);
        Assert.False(r.Rules.Feed(b, "bal"));                 // tutulmadan yem islemez
        Assert.False(r.Rules.TryBind(b, 0.5f, "cekim"));      // can yuksek
        Assert.False(r.Rules.TryBind(b, 0.2f, "kazma"));      // yanlis baslik
        Assert.True(r.Rules.TryBind(b, 0.2f, "zipkin"));
        Assert.False(r.Rules.Feed(b, "bal"));
        Assert.True(r.Rules.Feed(b, "bal"));
        Assert.True(b.Tame);
    }

    [Fact]
    public void Egg_path_hatches_a_tame_chick_in_the_incubator()
    {
        var (s, r) = Fresh();
        var inc = r.AddIncubator(0, 0);
        Assert.False(r.PutEgg(inc, s.Backpack, "hu_yumurtasi")); // cantada yok
        s.Backpack.Add("hu_yumurtasi", 1);
        Assert.True(r.PutEgg(inc, s.Backpack, "hu_yumurtasi"));
        var hu = r.Wild("hu", 5, 5);
        Assert.False(r.Rules.Feed(hu, "et")); // yumurtadan cikan tur yemle evcillesmez
        var ev = r.Tick(r.Rules.Def.Incubator.Seconds + 1, Array.Empty<PenShape>());
        var hatched = Assert.Single(ev, e => e.Kind == RanchEventKind.Hatched);
        var chick = r.ById(hatched.AnimalId)!;
        Assert.Equal("hu", chick.Species);
        Assert.True(chick.Tame);
        Assert.False(chick.Adult(r.Rules.Def.Breeding.AdultDays));
    }

    [Fact]
    public void Hive_species_settles_in_a_hive_not_a_pen()
    {
        var (_, r) = Fresh();
        var pen = Square(0, 0, 6);
        var v = r.Wild("vizviz", 1, 1);
        v.Trust = 1;
        Assert.False(r.Register(v, pen));
        var h = r.AddHive(10, 10);
        Assert.True(r.Settle(v, h));
        Assert.True(v.Registered);
        // kovanda yem olmadan bal birikir
        var ev = r.Tick(r.Rules.Def.Species.Single(x => x.Id == "vizviz").Products[0].Seconds * 1.5f, new[] { pen });
        Assert.Contains(ev, e => e.Kind == RanchEventKind.Product && e.Item == "bal");
    }

    // ---------- agil alani ----------

    [Fact]
    public void Pen_space_is_the_sum_of_species_areas()
    {
        var (_, r) = Fresh();
        var pen = Square(0, 0, MathF.Sqrt(18)); // 1x2 hucre = 18 m²
        TameIn(r, "gidak", pen, 1, 1);             // 4
        TameIn(r, "pofuduk", pen, 1.5f, 1.5f);     // 9 -> 13
        var third = r.Wild("pofuduk", 2, 2); third.Trust = 1;
        Assert.False(r.Register(third, pen));       // 22 > 18
        var g2 = r.Wild("gidak", 2, 2); g2.Trust = 1;
        Assert.True(r.Register(g2, pen));           // 17 <= 18
    }

    // ---------- ihtiyac ve mutluluk ----------

    [Fact]
    public void Happiness_follows_met_needs_pet_and_manure()
    {
        var (s, r) = Fresh();
        var pen = Square(0, 0, 6);
        var a = TameIn(r, "pofuduk", pen, 3, 3);
        var day = r.Rules.DaySeconds;
        // Hicbir sey yok: yalniz alan karsilanir -> base + space
        for (int i = 0; i < 48; i++) r.Tick(day / 24, new[] { pen });
        var w = r.Rules.Def.Happiness.Weights;
        // Hedef her tikte gubre dusmeden once hesaplanir: son yigin bir tik geriden gelir.
        float noFood = r.Rules.Def.Happiness.Base + w["space"];
        Assert.InRange(a.Happiness, noFood - r.Rules.Def.Happiness.ManurePenaltyMax, noFood - Math.Min(r.Rules.Def.Happiness.ManurePenaltyMax, (r.ManureIn(pen) - 1) * r.Rules.Def.Happiness.ManurePenalty) + 0.01f);

        // Yem, su, firca, cati: hedef yukselir
        var f = r.AddFeeder(1, 1); s.Backpack.Add("turnip", 20); HusbandryRules.Fill(f, s.Backpack, "turnip");
        r.AddTrough(5, 5); r.AddBrush(3, 4);
        foreach (var m in r.Manure.ToList()) r.CollectManure(m, s.Backpack);
        Assert.True(s.Backpack.Count("gubre") > 0);
        var needs = r.NeedsOf(a, pen, roof: true);
        Assert.True(needs.Food, "food");
        Assert.True(needs.Water, "water");
        Assert.True(needs.Roof, "roof");
        Assert.True(needs.Space, "space");
        Assert.True(needs.Brush, "brush");
        Assert.Equal(0, needs.Manure);
        float full = r.Rules.Target(needs);
        Assert.Equal(Math.Min(100f, r.Rules.Def.Happiness.Base + w.Values.Sum()), full, 1);
        float noRoof = r.Rules.Target(r.NeedsOf(a, pen, false));
        Assert.True(noRoof < full);
        Assert.True(r.Rules.Pet(a, r.Day));
        Assert.False(r.Rules.Pet(a, r.Day)); // gunde bir
        Assert.Equal(Math.Min(100f, noRoof + r.Rules.Def.Happiness.PetBonus), r.Rules.Target(r.NeedsOf(a, pen, false)), 1);
        // manure cezasi
        var dirty = new Needs(true, true, true, true, true, false, 100);
        Assert.Equal(full - r.Rules.Def.Happiness.ManurePenaltyMax, r.Rules.Target(dirty), 1);
    }

    [Fact]
    public void Production_needs_the_species_food_and_happiness_sets_stars()
    {
        var (s, r) = Fresh();
        var pen = Square(0, 0, 6);
        var a = TameIn(r, "pofuduk", pen, 3, 3);
        var sut = r.Rules.Species("pofuduk").Products.Single(p => p.Item == "sut");
        r.Tick(sut.Seconds * 3, new[] { pen });
        Assert.Empty(a.Pending); // yem yok
        var f = r.AddFeeder(1, 1);
        s.Backpack.Add("wheat", 5);
        Assert.Equal(0, HusbandryRules.Fill(f, s.Backpack, "turnip")); // cantada yok
        HusbandryRules.Fill(f, s.Backpack, "wheat");                   // yanlis yem: Yemlik bugday olur
        r.Tick(sut.Seconds * 2, new[] { pen });
        Assert.Empty(a.Pending);
        f.Amount = 0;
        s.Backpack.Add("turnip", 20);
        Assert.Equal(20, HusbandryRules.Fill(f, s.Backpack, "turnip"));
        Assert.Equal("turnip", f.Food);
        r.Tick(1f, new[] { pen }); // bekleyen urun hemen
        Assert.Contains(a.Pending, p => p.Item == "sut");
        Assert.Equal(r.Rules.Tier(a.Happiness).Stars, a.Pending.First(p => p.Item == "sut").Stars);
        int got = HusbandryRules.Collect(a, s.Backpack);
        Assert.True(got >= 1);
        Assert.True(s.Backpack.Count("sut") >= 1);
        Assert.Empty(a.Pending);
    }

    [Fact]
    public void Gidak_egg_goes_to_the_folluk_in_its_pen()
    {
        var (s, r) = Fresh();
        var pen = Square(0, 0, 6);
        var g = TameIn(r, "gidak", pen, 3, 3);
        var f = r.AddFeeder(1, 1); s.Backpack.Add("wheat", 10); HusbandryRules.Fill(f, s.Backpack, "wheat");
        var nest = r.AddNest(5, 1);
        r.AddNest(20, 20); // agil disindaki Folluk sayilmaz
        var ev = r.Tick(r.Rules.Species("gidak").Products[0].Seconds + 1, new[] { pen });
        Assert.Contains(ev, e => e.Kind == RanchEventKind.Nested && e.Item == "egg");
        Assert.Equal(1, nest.Eggs);
    }

    // ---------- gen ve ureme ----------

    [Fact]
    public void Inheritance_is_rounded_average_plus_mutation_clamped()
    {
        var (_, r) = Fresh(3);
        var rng = new RanchRng { State = 12345 };
        int[] counts = new int[5];
        for (int i = 0; i < 20000; i++)
        {
            int g = r.Rules.InheritOne(4, 5, rng); // ortalama 4,5 -> 5
            Assert.InRange(g, 4, 7);
            counts[g - 4]++;
        }
        Assert.InRange(counts[1] / 20000f, 0.66f, 0.74f); // %70 degismez
        Assert.InRange(counts[2] / 20000f, 0.17f, 0.23f); // %20 +1
        Assert.InRange(counts[0] / 20000f, 0.06f, 0.10f); // %8 -1
        for (int i = 0; i < 2000; i++) Assert.InRange(r.Rules.InheritOne(10, 10, rng), 1, 10);
        for (int i = 0; i < 2000; i++) Assert.InRange(r.Rules.InheritOne(1, 1, rng), 1, 10);
    }

    static (Ranch r, GameState s, PenShape pen, PenAnimal a, PenAnimal b) HappyPair(ulong seed = 11, float size = 6)
    {
        var (s, r) = Fresh(seed);
        var pen = Square(0, 0, size);
        var a = TameIn(r, "pofuduk", pen, 2, 2);
        var b = TameIn(r, "pofuduk", pen, 4, 4);
        var f = r.AddFeeder(1, 1); f.Food = "turnip"; f.Amount = 20;
        r.AddTrough(5, 1); r.AddBrush(3, 3);
        return (r, s, pen, a, b);
    }

    [Fact]
    public void Two_happy_adults_with_room_have_a_young_in_two_to_four_days()
    {
        var (r, s, pen, a, b) = HappyPair();
        float day = r.Rules.DaySeconds;
        RanchEvent? born = null;
        float t = 0;
        while (t < 6 * day && born == null)
        {
            foreach (var f in r.Feeders) f.Amount = f.Capacity;
            foreach (var m in r.Manure.ToList()) r.CollectManure(m, s.Backpack);
            var ev = r.Tick(60, new[] { pen }, _ => true);
            t += 60;
            born = ev.Cast<RanchEvent?>().FirstOrDefault(e => e!.Value.Kind == RanchEventKind.Born);
        }
        Assert.NotNull(born);
        // mutluluk 60'a ulasmasi + 2-4 gun
        Assert.InRange(t / day, 2f, 4f + 1f);
        var child = r.ById(born!.Value.AnimalId)!;
        Assert.True(child.Registered && child.Tame);
        Assert.Contains(child.MotherId, new[] { a.Id, b.Id });
        Assert.Contains(child.FatherId, new[] { a.Id, b.Id });
        foreach (var g in new[] { child.Genes.B, child.Genes.U, child.Genes.V, child.Genes.K }) Assert.InRange(g, 1, 10);
        // yavru urun vermez
        Assert.False(child.Adult(r.Rules.Def.Breeding.AdultDays));
    }

    [Fact]
    public void No_young_when_unhappy_or_no_room()
    {
        // yer yok: 9 + 9 = 18 m², agil 18 m²
        var (r, s, pen, _, _) = HappyPair(size: MathF.Sqrt(18));
        float day = r.Rules.DaySeconds;
        for (float t = 0; t < 8 * day; t += 120)
        {
            foreach (var f in r.Feeders) f.Amount = f.Capacity;
            Assert.DoesNotContain(r.Tick(120, new[] { pen }, _ => true), e => e.Kind == RanchEventKind.Born);
        }
        // mutsuz: yem, su, firca yok
        var (s2, r2) = Fresh(5);
        var pen2 = Square(0, 0, 8);
        TameIn(r2, "pofuduk", pen2, 2, 2); TameIn(r2, "pofuduk", pen2, 3, 3);
        for (float t = 0; t < 8 * day; t += 120)
            Assert.DoesNotContain(r2.Tick(120, new[] { pen2 }), e => e.Kind == RanchEventKind.Born);
    }

    // ---------- roller ----------

    [Fact]
    public void Roles_follow_species_and_shape_the_work()
    {
        var (s, r) = Fresh();
        var pen = Square(0, 0, 8);
        var dalli = TameIn(r, "dalli", pen, 2, 2);
        Assert.False(r.Rules.SetRole(dalli, "binek"));
        Assert.True(r.Rules.SetRole(dalli, "yuk"));
        Assert.Equal(16 + (dalli.Genes.B - 1) * r.Rules.Def.Genes.BPackPerPoint, r.Rules.PackSlots(dalli));
        s.Backpack.Add("stone", 40);
        Assert.Equal(40, r.Rules.PackLoad(dalli, s.Backpack, "stone", 40));
        Assert.Equal(0, s.Backpack.Count("stone"));
        Assert.False(r.Rules.SetRole(dalli, "uretici")); // heybe dolu
        Assert.Equal(40, r.Rules.PackUnload(dalli, s.Backpack));
        Assert.Equal(40, s.Backpack.Count("stone"));

        // Homur isci: eseler (cevher ya da truf)
        var homur = TameIn(r, "homur", pen, 6, 6);
        Assert.True(r.Rules.SetRole(homur, "isci"));
        var ev = r.Tick(r.Rules.Species("homur").Work!.Seconds * 2f, new[] { pen });
        Assert.Contains(ev, e => e.Kind == RanchEventKind.Worked && e.AnimalId == homur.Id && (e.Item == "copper_ore" || e.Item == "truf"));
        // Vicik isci: gubreden kompost
        var (s2, r2) = Fresh();
        var v = TameIn(r2, "vicik", pen, 3, 3);
        Assert.Equal("isci", v.Role);
        r2.Manure.Add(new ManurePile { Id = r2.NewId(), Count = 1, X = 3, Z = 3 });
        ev = r2.Tick(r2.Rules.Species("vicik").Work!.Seconds * 2f, new[] { pen });
        Assert.Contains(ev, e => e.Kind == RanchEventKind.Worked && e.Item == "kompost");
        // komut: uysal hayvan cogunlukla dinler
        int ok = 0;
        dalli.Genes.U = 10;
        for (int i = 0; i < 200; i++) if (r.Rules.Command(dalli, "bekle", r.Rng)) ok++;
        Assert.Equal(200, ok);
        dalli.Genes.U = 1;
        ok = 0;
        for (int i = 0; i < 2000; i++) if (r.Rules.Command(dalli, "izle", r.Rng)) ok++;
        Assert.InRange(ok / 2000f, r.Rules.ObeyChance(dalli) - 0.05f, r.Rules.ObeyChance(dalli) + 0.05f);
        Assert.True(r.Rules.ObeyChance(dalli) < 1f);
    }

    // ---------- kacma ----------

    [Fact]
    public void Long_unhappy_animal_wants_out_and_escapes_through_the_gate()
    {
        var (_, r) = Fresh();
        var pen = Square(0, 0, 6);
        var a = TameIn(r, "pofuduk", pen, 3, 3);
        a.Happiness = 5;
        r.Rules.Def.Happiness.Base = 0;
        r.Rules.Def.Happiness.Weights["space"] = 0;
        var ev = r.Tick(r.Rules.Def.Escape.Seconds + 1, new[] { pen });
        Assert.Contains(ev, e => e.Kind == RanchEventKind.WantsOut);
        Assert.True(a.WantsOut);
        Assert.True(a.Registered);
        // Godot katmani kapidan cikarir: agilin disinda kayit duser
        a.X = 20; a.Z = 20;
        ev = r.Tick(1, new[] { pen });
        Assert.Contains(ev, e => e.Kind == RanchEventKind.Escaped);
        Assert.False(a.Registered);
        Assert.Equal(r.Rules.Def.Escape.TrustAfter, a.Trust);
        Assert.False(a.Tame);
    }

    // ---------- 100 gunluk agil ----------

    /// <summary>
    /// 100 gun: iki agil. Bakilan agil (yem, su, firca, cati, gunde okşama, gubre toplama) urer ve urun verir; bakimsiz
    /// agil (yemsiz, kapisi acik) mutsuzlasir ve hayvanlari kacar. Her an: nufus agil alanina sigar, mutluluk 0-100,
    /// genler 1-10.
    /// </summary>
    static (int born, int escaped, int products, int final, string sig) HundredDays(ulong seed)
    {
        var s = new GameState(TestData.Load(), seed);
        var r = s.Ranch;
        var cared = Square(0, 0, 9);      // 81 m²
        var neglected = Square(30, 0, 6); // 36 m²
        var pens = new[] { cared, neglected };
        foreach (var (x, z) in new[] { (2f, 2f), (6f, 6f) }) TameIn(r, "pofuduk", cared, x, z);
        foreach (var (x, z) in new[] { (3f, 3f), (5f, 5f) }) TameIn(r, "gidak", cared, x, z);
        foreach (var (x, z) in new[] { (32f, 2f), (34f, 4f) }) TameIn(r, "pofuduk", neglected, x, z);
        var turnip = r.AddFeeder(1, 1); var wheat = r.AddFeeder(8, 1);
        r.AddTrough(1, 8); r.AddBrush(4.5f, 4.5f); r.AddNest(8, 8);
        float day = r.Rules.DaySeconds, dt = 120;
        int born = 0, escaped = 0, products = 0;
        for (int d = 0; d < 100; d++)
        {
            r.Dawn();
            turnip.Food = "turnip"; turnip.Amount = turnip.Capacity;
            wheat.Food = "wheat"; wheat.Amount = wheat.Capacity;
            foreach (var a in r.Animals.Where(a => a.Registered && Ranch.In(cared, a.X, a.Z))) r.Rules.Pet(a, r.Day);
            for (float t = 0; t < day; t += dt)
            {
                var ev = r.Tick(dt, pens, p => p == cared);
                born += ev.Count(e => e.Kind == RanchEventKind.Born);
                products += ev.Count(e => e.Kind is RanchEventKind.Product or RanchEventKind.Nested or RanchEventKind.Grounded);
                // kapisi acik bakimsiz agil: cikmak isteyen cikar (Godot katmaninda kapidan yuruyerek)
                foreach (var a in r.Animals.Where(a => a.WantsOut)) { a.X += 40; }
                escaped += ev.Count(e => e.Kind == RanchEventKind.Escaped);
                foreach (var m in r.Manure.Where(m => Ranch.In(cared, m.X, m.Z)).ToList()) r.CollectManure(m, s.Backpack);
                foreach (var a in r.Animals) HusbandryRules.Collect(a, s.Backpack);
                foreach (var n in r.Nests) CoopRules.Collect(n, s.Backpack, "egg");
                foreach (var pen in pens) Assert.True(r.UsedArea(pen) <= pen.Area + 0.01f, $"gun {d}: agil tasti");
                foreach (var a in r.Animals)
                {
                    Assert.InRange(a.Happiness, 0f, 100f);
                    foreach (var g in new[] { a.Genes.B, a.Genes.U, a.Genes.V, a.Genes.K }) Assert.InRange(g, 1, 10);
                }
            }
        }
        string sig = string.Join(";", r.Animals.Select(a => $"{a.Id}:{a.Species}:{a.Genes}:{a.Happiness:0.0}:{a.Registered}")) + $"|{s.Backpack.Count("sut")},{s.Backpack.Count("egg")}";
        return (born, escaped, products, r.Animals.Count(a => a.Registered), sig);
    }

    [Fact]
    public void Hundred_day_pen_breeds_produces_and_the_neglected_pen_escapes()
    {
        var (born, escaped, products, final, _) = HundredDays(2026);
        Assert.True(born >= 2, $"born={born}");          // bakilan agilda yavru
        Assert.Equal(2, escaped);                         // bakimsiz agilin iki Pofuduk'u
        Assert.True(products > 200, $"products={products}");
        Assert.True(final >= 4 + born - 0, $"final={final}");
    }

    [Fact]
    public void Hundred_day_pen_is_deterministic_for_a_seed()
    {
        Assert.Equal(HundredDays(77).sig, HundredDays(77).sig);
        Assert.NotEqual(HundredDays(77).sig, HundredDays(78).sig);
    }
}
