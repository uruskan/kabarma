using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Istasyonlar, tarifler, bedeller ve yemek (GDD 3.3, 5, 11; ICERIK 10, 11, 15, 20; URETIM 6 M1 kararlari 2, 4, 15).
/// </summary>
public class CraftingTests
{
    static GameData Data() => TestData.Load();

    static RecipeDef R(GameData d, string id) => d.Tool.Recipes.Single(r => r.Id == id);
    static PlaceableDef P(GameData d, string id) => d.Tool.Placeables.Single(p => p.Id == id);

    static void Run(StationQueue q, Inventory inv, float seconds) { for (float t = 0; t < seconds; t += 0.1f) q.Tick(0.1f, inv); }

    [Fact]
    public void Costs_and_recipes_follow_icerik_20()
    {
        var d = Data();
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 10, ["stone"] = 5 }, P(d, "tezgah").Cost);
        Assert.Equal(new Dictionary<string, int> { ["stone"] = 15, ["clay"] = 5 }, P(d, "tas_firin").Cost);
        Assert.Equal(new Dictionary<string, int> { ["stone"] = 8, ["wood"] = 4 }, P(d, "ocakbasi").Cost);
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 8 }, P(d, "paketleme").Cost);
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 12, ["stone"] = 4 }, R(d, "el_arabasi").In);
        Assert.Equal("tezgah", R(d, "el_arabasi").Station);
        Assert.True(R(d, "el_arabasi").Load);
        Assert.Equal(new Dictionary<string, int> { ["copper_ingot"] = 2, ["wood"] = 4 }, R(d, "civi_tabancasi").In);
        Assert.Equal("copper_ingot", R(d, "civi_tabancasi").UnlockOn);
        Assert.Equal(new Dictionary<string, int> { ["copper_ingot"] = 1 }, R(d, "civi").In);
        Assert.Equal(20, R(d, "civi").Out);
        Assert.Equal(new Dictionary<string, int> { ["copper_ore"] = 2 }, R(d, "copper_ingot").In);
        Assert.Equal("tas_firin", R(d, "copper_ingot").Station);
        foreach (var food in new[] { "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi" }) Assert.Equal("ocakbasi", R(d, food).Station);
        Assert.Equal(new Dictionary<string, int> { ["turnip"] = 2 }, R(d, "sebze_corbasi").In);
        Assert.Equal("paketleme", R(d, "kargo_kasasi").Station);
        Assert.True(R(d, "kargo_kasasi").Load);
        // Yakit: Tas Firin ve Ocakbasi is basina 1 Odun (ICERIK 20).
        foreach (var st in new[] { "tas_firin", "ocakbasi" }) { Assert.Equal(new List<string> { "wood" }, P(d, st).Fuel); Assert.Equal(1, P(d, st).FuelPerJob); }
        Assert.Equal(0, P(d, "tezgah").FuelPerJob);
        // M1'in 5 istasyonu: dordu yerlestirilir, rampa kapsulden (URETIM 6).
        Assert.Equal(new[] { "ocakbasi", "paketleme", "tas_firin", "tezgah" }, d.Tool.Placeables.Where(p => p.Station != null).Select(p => p.Id).OrderBy(x => x));
    }

    [Fact]
    public void Placeables_follow_icerik_10_2()
    {
        var d = Data();
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 10 }, P(d, "sandik").Cost);
        Assert.Equal(20, P(d, "sandik").Slots);
        Assert.Equal(new Dictionary<string, int> { ["fiber"] = 6 }, P(d, "uyku_tulumu").Cost);
        Assert.True(P(d, "uyku_tulumu").Spawn);
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 2 }, P(d, "mesale").Cost);
        Assert.Equal(6f, P(d, "mesale").LightRange);
    }

    [Fact]
    public void Build_costs_follow_icerik_10_1()
    {
        var c = Data().Tool.BuildCosts;
        var want = new Dictionary<string, int>
        {
            ["kare_temel"] = 6, ["kazik_temel"] = 6, ["ucgen_temel"] = 3,
            ["duvar"] = 4, ["kapi_kasasi"] = 4, ["pencereli_duvar"] = 4, ["kare_zemin"] = 4, ["egimli_cati"] = 4, ["cati_kosesi"] = 4, ["cati_tepesi"] = 4, ["duz_cati"] = 4,
            ["yarim_duvar"] = 2, ["alcak_duvar"] = 2, ["ucgen_duvar"] = 2, ["ucgen_zemin"] = 2,
            ["merdiven"] = 5, ["rampa"] = 5, ["sutun"] = 2, ["kiris"] = 2, ["parmaklik"] = 2, ["cit"] = 2, ["bahce_kapisi"] = 3,
            ["sur_duvari"] = 8, ["sur_kapisi"] = 10, ["kule_tabani"] = 12,
            ["makine_platformu_kucuk"] = 6, ["makine_platformu_orta"] = 24, ["makine_platformu_buyuk"] = 54,
        };
        Assert.Equal(want.OrderBy(kv => kv.Key), c.OrderBy(kv => kv.Key));
    }

    [Fact]
    public void Smelting_burns_fuel_and_first_ingot_unlocks_the_nailgun()
    {
        var d = Data();
        var s = new GameState(d);
        var inv = s.Backpack;
        var firin = s.Stations["tas_firin"];
        var tez = s.Stations["tezgah"];
        inv.Add("copper_ore", 4);
        Assert.False(firin.CanMake(R(d, "copper_ingot"), inv)); // yakit yok
        inv.Add("wood", 5);
        Assert.False(tez.IsUnlocked(R(d, "civi_tabancasi")));
        Assert.False(tez.Enqueue(R(d, "civi_tabancasi"), inv));
        Assert.True(firin.Enqueue(R(d, "copper_ingot"), inv));
        Assert.True(firin.Enqueue(R(d, "copper_ingot"), inv));
        Assert.Equal(0, inv.Count("copper_ore"));
        Assert.Equal(3, inv.Count("wood")); // is basina 1 Odun
        Run(firin, inv, 9f);
        Assert.Equal(2, inv.Count("copper_ingot"));
        Assert.True(tez.IsUnlocked(R(d, "civi_tabancasi")));
        // Civi: 1 Bakir Kulce -> 20 Civi (yakitsiz)
        Assert.True(tez.Enqueue(R(d, "civi"), inv));
        Run(tez, inv, 3f);
        Assert.Equal(40 + 20, inv.Count("civi"));
        Assert.Equal(1, inv.Count("copper_ingot"));
        Assert.False(tez.CanMake(R(d, "civi_tabancasi"), inv)); // 2 kulce ister
        inv.Add("copper_ingot", 1);
        inv.Add("wood", 1);
        Assert.True(tez.Enqueue(R(d, "civi_tabancasi"), inv));
        Run(tez, inv, 5f);
        Assert.Equal(1, inv.Count("civi_tabancasi"));
    }

    [Fact]
    public void Ocakbasi_cooks_three_foods_with_fuel()
    {
        var d = Data();
        var s = new GameState(d);
        var inv = s.Backpack;
        var q = s.Stations["ocakbasi"];
        inv.Add("turnip", 3);
        inv.Add("egg", 1);
        inv.Add("wood", 3);
        foreach (var id in new[] { "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi" })
        {
            Assert.True(q.Enqueue(R(d, id), inv), id);
            Run(q, inv, 5f);
            Assert.Equal(1, inv.Count(id));
        }
        Assert.Equal(0, inv.Count("turnip"));
        Assert.Equal(0, inv.Count("egg"));
        Assert.Equal(0, inv.Count("wood"));
        inv.Add("turnip", 2);
        Assert.False(q.Enqueue(R(d, "sebze_corbasi"), inv)); // odun bitti
    }

    [Fact]
    public void Load_products_leave_the_station_as_world_objects()
    {
        var d = Data();
        var s = new GameState(d);
        s.Backpack.Add("wood", 12);
        s.Backpack.Add("stone", 4);
        var tez = s.Stations["tezgah"];
        RecipeDef? made = null;
        tez.Produced += (r, _) => made = r;
        Assert.True(tez.Enqueue(R(d, "el_arabasi"), s.Backpack));
        Run(tez, s.Backpack, 5f);
        Assert.Equal("el_arabasi", made?.Id);
        Assert.Equal(0, s.Backpack.Count("el_arabasi")); // cantaya girmez: yuk nesnesi
        Assert.Equal(0, s.Backpack.Count("wood"));
    }

    [Fact]
    public void Stack_sizes_follow_gdd_5()
    {
        var d = Data();
        var s = new GameState(d);
        Assert.Equal(50, s.Backpack.StackOf("turnip"));
        Assert.Equal(50, s.Backpack.StackOf("stone"));
        Assert.Equal(20, s.Backpack.StackOf("copper_ingot"));
        Assert.Equal(10, s.Backpack.StackOf("sebze_corbasi"));
        var inv = new Inventory(2, d.Tool.StackOf);
        Assert.Equal(20, inv.Add("kozde_salgam", 25)); // 2 yuva × 10
        Assert.Equal(0, inv.Room("kozde_salgam"));
    }

    [Fact]
    public void Food_three_slots_hp_stamina_and_expiry()
    {
        var d = Data();
        var s = new GameState(d);
        var f = d.Tool.Foods;
        Assert.Equal(3, s.Food.Slots);
        Assert.Equal(3, f.Count);
        foreach (var x in f) Assert.InRange(x.Seconds, 15 * 60, 25 * 60); // GDD 3.3: 15-25 dk
        var soup = f.Single(x => x.Id == "sebze_corbasi");
        Assert.Equal((25f, 10f), (soup.MaxHp, soup.Stamina)); // ICERIK 15
        Assert.True(soup.Regen > 0);                          // "Can yenilenmesi"
        Assert.Equal((15f, 10f), (f.Single(x => x.Id == "kozde_salgam").MaxHp, f.Single(x => x.Id == "kozde_salgam").Stamina));
        Assert.Equal((10f, 20f), (f.Single(x => x.Id == "haslanmis_yumurta").MaxHp, f.Single(x => x.Id == "haslanmis_yumurta").Stamina));
        foreach (var x in f) Assert.True(s.Food.Eat(x));
        Assert.Equal(50f, s.Food.BonusMaxHp);
        Assert.Equal(40f, s.Food.BonusStamina);
        Assert.True(s.Food.Eat(soup));             // ayni yemek: suresi tazelenir,
        Assert.Equal(3, s.Food.Active.Count);      // ikinci yuvaya girmez
        var extra = new FoodDef { Id = "deneme", MaxHp = 1, Seconds = 60 };
        Assert.False(s.Food.Eat(extra));           // uc yuva dolu
        s.Food.Tick(901f);
        Assert.Single(s.Food.Active);              // 15 dk'liklar bitti, corba (20 dk) suruyor
        Assert.Equal(25f, s.Food.BonusMaxHp);
        s.Food.Tick(300f);
        Assert.Equal(0f, s.Food.BonusMaxHp);
    }
}
