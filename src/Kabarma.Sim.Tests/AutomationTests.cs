using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 tasima: otomasyon makineleri, istasyon islevleri, Maya Kazani (GDD 11.4, 14.4; ICERIK §2.1 #26, §11). Hepsi Sim.
/// </summary>
public class AutomationTests
{
    static GameData Data() => TestData.Load();
    static List<Inventory> Chest(params (string id, int n)[] items)
    {
        var c = new Inventory(20, 99);
        foreach (var (id, n) in items) c.Add(id, n);
        return new List<Inventory> { c };
    }

    // ---------- istasyon islevleri ----------

    [Fact]
    public void Every_station_has_a_function_or_is_explicitly_marked_for_a_later_stream()
    {
        var d = Data();
        var allowedNext = new[] { "next:tarim4", "next:arac" };
        foreach (var id in ZincirChain4Tests.AllStations)
        {
            Assert.True(d.Automation.Functions.TryGetValue(id, out var fn), "istasyonun islevi yazilmamis: " + id);
            switch (fn)
            {
                case "recipes": Assert.True(d.Tool.Recipes.Any(r => r.Station == id), "tarifi yok: " + id); break;
                case "machine": Assert.True(d.Automation.Machines.ContainsKey(id), "makine tanimi yok: " + id); break;
                case "hand": Assert.Contains(d.Tool.Recipes, r => r.Station == ""); break;
                case "special": break; // kendi menusu ya da baska sistem (arastirma, paketleme, rampa, hayvan, arac, yapi)
                default: Assert.Contains(fn!, allowedNext); break;
            }
        }
        // "next:" yalniz o akisin kendi istasyonlari icin; bu istasyonlar M4'te bos kalir ve PROGRESS.md'de satiri var.
        Assert.Equal(new[] { "mutfak", "zeplin_hangari" },
            d.Automation.Functions.Where(kv => kv.Value.StartsWith("next:")).Select(kv => kv.Key).OrderBy(x => x).ToArray());
        foreach (var m in d.Automation.Machines.Keys) Assert.True(d.Research.Watts.ContainsKey(m), "makine guc ister (watts): " + m);
        Assert.Contains(d.Tool.Recipes, r => r.Id == "tabaklanmis_deri" && r.Station == "tabaklama_askisi");
        Assert.Contains(d.Tool.Recipes, r => r.Id == "kompost" && r.Station == "kompostor");
        Assert.Contains(d.Tool.Recipes, r => r.Id == "sira" && r.Station == "tursu_ficisi");
        Assert.Contains(d.Tool.Recipes, r => r.Id == "wheat_seed" && r.Station == "harman_yeri");
    }

    // ---------- Hasat ve Ekim Robotu, Sera ----------

    [Fact]
    public void Planting_and_harvesting_robots_work_within_their_radius_and_use_the_chests()
    {
        var d = Data();
        var farm = new Farm(d); var rng = new Rng(7);
        farm.Till(0, 0); farm.Till(1, 0); farm.Till(30, 30);
        var chests = Chest(("turnip_seed", 3));
        Assert.Equal(2, AutomationRules.Plant(d, farm, rng, 0.5f, 0.5f, 8f, chests)); // uzaktaki kare (30,30) ekilmez
        Assert.True(farm.Get(0, 0)!.Planted && farm.Get(1, 0)!.Planted && !farm.Get(30, 30)!.Planted);
        Assert.Equal(1, AutomationRules.Count(chests, "turnip_seed"));
        Assert.Equal(0, AutomationRules.Harvest(d, farm, rng, 0.5f, 0.5f, 8f, chests)); // henuz olgun degil
        GeneticsTests.GrowAll(farm, d);
        int h = AutomationRules.Harvest(d, farm, rng, 0.5f, 0.5f, 8f, chests);
        Assert.Equal(2, h);
        Assert.True(AutomationRules.Count(chests, "turnip") >= 2);
        Assert.True(AutomationRules.Count(chests, "turnip_seed") >= 1);
        // hasat edilmis kare yeniden ekilir (ayni tohum turu)
        Assert.Equal(2, AutomationRules.Plant(d, farm, rng, 0.5f, 0.5f, 8f, chests));
        // urune yer yoksa hasat beklemez de kaybetmez
        GeneticsTests.GrowAll(farm, d);
        var full = new List<Inventory> { new Inventory(1, 5) };
        full[0].Add("stone", 5);
        Assert.Equal(0, AutomationRules.Harvest(d, farm, rng, 0.5f, 0.5f, 8f, full));
        Assert.True(farm.Get(0, 0)!.Planted);
    }

    [Fact]
    public void Greenhouse_unit_sets_only_its_own_squares_and_releases_them()
    {
        var farm = new Farm(Data());
        farm.Till(0, 0); farm.Till(2, 0); farm.Till(40, 0);
        var mine = new HashSet<(int, int)>();
        Assert.Equal(2, AutomationRules.Greenhouse(farm, 1f, 0.5f, 5f, true, mine));
        Assert.True(farm.Get(0, 0)!.Greenhouse && farm.Get(2, 0)!.Greenhouse && !farm.Get(40, 0)!.Greenhouse);
        farm.SetGreenhouse(40, 0, true); // baska yolla (Cam Cati) acilmis sera: unite dokunmaz
        AutomationRules.Greenhouse(farm, 1f, 0.5f, 5f, false, mine);
        Assert.False(farm.Get(0, 0)!.Greenhouse);
        Assert.True(farm.Get(40, 0)!.Greenhouse);
        Assert.Empty(mine);
    }

    // ---------- Yemlik, Yumurta, Sagim ----------

    [Fact]
    public void Feeder_nest_and_milking_arm_move_goods_to_the_chests()
    {
        var feeder = new Feeder { Capacity = 10 };
        var chests = Chest(("wheat", 4), ("sunflower", 2));
        Assert.Equal(4, AutomationRules.Feed(new[] { feeder }, chests, new[] { "wheat" }));
        Assert.Equal(4, feeder.Amount);
        Assert.Equal(0, AutomationRules.Feed(new[] { feeder }, chests, new[] { "wheat" })); // yem bitti
        var nest = new Nest { Eggs = 3, Slots = 6 };
        var eggs = Chest();
        Assert.Equal(3, AutomationRules.CollectEggs(new[] { nest }, eggs, "egg"));
        Assert.Equal(3, eggs[0].Count("egg")); Assert.Equal(0, nest.Eggs);
        var cow = new PenAnimal { Species = "pofuduk", Trust = 1f, X = 3, Z = 0 };
        cow.Pending.Add(new ProductStack { Item = "sut", Count = 2 }); cow.Pending.Add(new ProductStack { Item = "gubre", Count = 1 });
        var far = new PenAnimal { Species = "pofuduk", Trust = 1f, X = 50, Z = 0 };
        far.Pending.Add(new ProductStack { Item = "sut", Count = 5 });
        var milk = Chest();
        Assert.Equal(2, AutomationRules.Milk(new[] { cow, far }, 0, 0, 12f, new[] { "sut", "yun" }, milk));
        Assert.Equal(2, milk[0].Count("sut"));
        Assert.Equal("gubre", Assert.Single(cow.Pending).Item);       // sagim yalniz sut ve yun alir
        Assert.Equal(5, Assert.Single(far.Pending).Count);            // menzil disi
    }

    // ---------- Paketleyici ----------

    [Fact]
    public void Packer_fills_a_crate_from_its_chest_and_puts_it_on_the_ramp()
    {
        var d = Data();
        var cargo = new Cargo(d, (_, _) => { });
        var chests = Chest(("turnip", 12), ("tezgah", 1));
        Assert.True(cargo.Pricing.Sellable("turnip")); Assert.False(cargo.Pricing.Sellable("tezgah"));
        int n = AutomationRules.Pack(cargo, chests, flush: false, out bool ready);
        Assert.Equal(12, n); Assert.False(ready);                    // kasa dolmadi: bekler
        Assert.Equal(1, chests[0].Count("tezgah"));                  // satilmayan esya kasaya girmez
        AutomationRules.Pack(cargo, chests, flush: true, out ready);
        Assert.True(ready);
        Assert.Single(cargo.Pad);
        Assert.Equal(12, cargo.Pad[0].Items.Count("turnip"));
        Assert.True(cargo.LaunchCrates() > 0);
    }

    // ---------- Sondaj Kulesi ----------

    [Fact]
    public void Drill_gives_the_ore_of_the_biome_the_rich_vein_lies_in()
    {
        var d = Data();
        Assert.Equal("copper_ore", AutomationRules.DrillOre(d, "altin_cayir"));
        Assert.Equal("iron_ore", AutomationRules.DrillOre(d, "sis_korusu"));
        Assert.Equal("titan_ore", AutomationRules.DrillOre(d, "kizil_kanyon"));
        Assert.Null(AutomationRules.DrillOre(d, "bilinmeyen"));
        foreach (var ore in d.Automation.Machines["sondaj_kulesi"].Ores.Values) Assert.True(d.Research.RawSources.Any(s => s.Id == ore), "cevher ham kaynak degil: " + ore);
        Assert.Contains("zengin_yatak", d.Biomes.World.PoiTypes.Keys); // dunya tanimi var; dugumlerini biyom akisi yerlestirir
    }

    // ---------- Montaj Makinesi ----------

    [Fact]
    public void Assembler_makes_the_selected_recipe_in_its_own_queue()
    {
        var d = Data();
        var a = new AssemblerState();
        Assert.Equal("", a.Recipe);
        string first = a.Next(d, null);
        Assert.NotEqual("", first);
        a.Recipe = "sandik";
        var proxy = AssemblerState.Proxy(d.Tool.RecipeFor("sandik")!);
        Assert.Equal("montaj_makinesi", proxy.Station); Assert.Equal("sandik", proxy.Id);
        var q = new StationQueue(d, "montaj_makinesi");
        var inv = new Inventory(20, 99); inv.Add("wood", 25);
        Assert.True(q.Enqueue(proxy, inv)); Assert.True(q.Enqueue(proxy, inv)); Assert.False(q.Enqueue(proxy, inv));
        for (float t = 0; t < 30; t += 0.1f) q.Tick(0.1f, inv);
        Assert.Equal(2, inv.Count("sandik"));
        Assert.True(AssemblerState.Candidates(d, null).All(r => (r.Station == "tezgah" || r.Station == "") && !r.Load));
    }

    // ---------- Maya Kazani ----------

    [Fact]
    public void Night_maya_reaches_the_vault_at_dawn_and_can_be_taken_as_an_item()
    {
        var d = Data();
        var s = new GameState(d);
        var n = s.StartNight();
        var zip = d.Creature("zipzip");
        for (int i = 0; i < 30; i++) { n.OnKill(zip, 0, 0); n.CollectNear(0, 0); }
        CardTests.RunToDawn(n, d);
        Assert.True(n.Maya > 0);
        Assert.Equal((int)Math.Floor(n.Maya), s.MayaStock.Stock);   // borc dusulmus Maya, tam sayi
        var bag = new Inventory(20, 50);
        int got = s.MayaStock.Withdraw(bag);
        Assert.Equal(got, bag.Count("maya"));
        Assert.Equal(0, s.MayaStock.Stock);
        Assert.Equal(got, s.MayaStock.TotalDeposited);
        // Sigmayan Maya depoda kalir (kaybolmaz)
        var tiny = new Inventory(1, 5); tiny.Add("stone", 5);
        s.MayaStock.Deposit(40f);
        Assert.Equal(0, s.MayaStock.Withdraw(tiny)); Assert.Equal(40, s.MayaStock.Stock);
    }

    [Fact]
    public void Raw_sources_need_an_in_game_producer_or_are_listed_as_pending()
    {
        var d = Data();
        var pending = d.Research.RawSources.Where(s => s.Via.StartsWith("pending:")).Select(s => s.Via).Distinct().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "pending:ata4", "pending:biyom" }, pending); // kabul akisi bunlari bosaltmak zorunda
        foreach (var s in d.Research.RawSources.Where(s => s.By is "biyom" or "kabarma3" or "ata4"))
            Assert.True(s.Via.Length > 0, $"{s.Id}: kaynagin oyun yolu (via) yazilmamis");
        // "mayavault": Maya'nin gercek yolu yukaridaki gece testiyle sinanir
        Assert.Equal("mayavault", d.Research.RawSources.Single(s => s.Id == "maya").Via);
        Assert.Equal("maya", MayaVault.Item);
        foreach (var r in d.Tool.Recipes.Where(r => r.In.ContainsKey("maya"))) Assert.True(d.Research.RawSources.Any(s => s.Id == "maya"), r.Id);
    }
}
