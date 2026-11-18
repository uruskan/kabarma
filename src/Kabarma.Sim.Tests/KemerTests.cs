using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// URETIM 6 M1 karari 23 (GDD 4, 5, 7): Capa kemerde bir esya; Uretim paneli (el kuyrugu, iptal iadesi, istasyon
/// yakinligi); yerlestirilebilirler uretilir; eksik malzemenin kaynagi veriden.
/// </summary>
public class KemerTests
{
    static GameData Data() => TestData.Load();
    static RecipeDef R(GameData d, string id) => d.Tool.Recipes.Single(r => r.Id == id);

    // ---------- Kemer secimi ----------

    [Fact]
    public void Belt_starts_with_capa_then_plan_and_capa_in_hand()
    {
        var s = new GameState(Data());
        Assert.Equal("capa", s.Belt.At(0));
        Assert.Equal("insa_plani", s.Belt.At(1));
        Assert.Equal(0, s.Belt.Selected);
        Assert.Equal("capa", s.Belt.Held);
        // Aletler cantaya girmez (GDD 3.6: Capa olumde oyuncuda kalir).
        Assert.Equal(0, s.Backpack.Count("capa"));
        Assert.Equal(0, s.Backpack.Count("insa_plani"));
    }

    [Fact]
    public void Selecting_a_slot_puts_its_item_in_hand_and_empty_slot_means_empty_hands()
    {
        var s = new GameState(Data());
        s.Belt.Select(1);
        Assert.Equal("insa_plani", s.Belt.Held);
        int empty = Enumerable.Range(0, Belt.Slots).First(i => s.Belt.At(i) == null);
        s.Belt.Select(empty);
        Assert.Null(s.Belt.Held);
        s.Belt.Select(0);
        Assert.Equal("capa", s.Belt.Held);
    }

    [Fact]
    public void Scroll_wraps_around_all_eight_slots()
    {
        var s = new GameState(Data());
        s.Belt.Select(Belt.Slots - 1);
        s.Belt.Scroll(1);
        Assert.Equal(0, s.Belt.Selected);
        s.Belt.Scroll(-1);
        Assert.Equal(Belt.Slots - 1, s.Belt.Selected);
        s.Belt.Scroll(-1);
        Assert.Equal(Belt.Slots - 2, s.Belt.Selected);
    }

    [Fact]
    public void Only_holdable_items_join_the_belt_and_spent_items_leave_it()
    {
        var d = Data();
        var s = new GameState(d);
        // Kaynak kemere girmez (kemer 8 yuva: tas ve odun yuvalari doldurmasin).
        s.Backpack.Add("stone", 10);
        s.Backpack.Add("wood", 10);
        Assert.False(s.Belt.Contains("stone"));
        Assert.False(s.Belt.Contains("wood"));
        // Ocak kapsulden gelir ve kemerdedir (kemerden kurulur).
        Assert.True(s.Belt.Contains("ocak"));
        // Uretilen yerlestirilebilir kemere girer; kurulup bitince yuvadan duser.
        s.Backpack.Add("tezgah", 1);
        int slot = s.Belt.SlotOf("tezgah");
        Assert.True(slot >= 2);
        s.Belt.Select(slot);
        Assert.Equal("tezgah", s.Belt.Held);
        s.Backpack.Remove("tezgah", 1);
        Assert.False(s.Belt.Contains("tezgah"));
        Assert.Null(s.Belt.Held);
    }

    [Fact]
    public void Tools_never_leave_the_belt()
    {
        var s = new GameState(Data());
        s.Belt.Toggle("capa");
        Assert.Equal("capa", s.Belt.At(0));
        s.Belt.Assign(0, null);
        Assert.Equal("capa", s.Belt.At(0));
        // Capa'nin yuvasina baska esya konursa Capa bos yuvaya kayar.
        s.Backpack.Add("mesale", 1);
        s.Belt.Assign(0, "mesale");
        Assert.Equal("mesale", s.Belt.At(0));
        Assert.True(s.Belt.Contains("capa"));
    }

    // ---------- Kuyruk ve iptal ----------

    [Fact]
    public void Hand_queue_takes_materials_produces_in_time_and_cancel_refunds()
    {
        var d = Data();
        var inv = new Inventory(20, d.Tool.StackOf);
        inv.Add("wood", 30); inv.Add("stone", 10);
        var q = new CraftQueue(d.Tool.CraftQueueMax);
        var tezgah = R(d, "tezgah");
        Assert.True(CraftRules.IsHand(tezgah));
        Assert.True(q.Enqueue(tezgah, 2, inv));
        Assert.Equal(30 - 20, inv.Count("wood"));
        Assert.Equal(0, inv.Count("stone"));
        Assert.False(q.Enqueue(tezgah, 1, inv)); // malzeme yetmiyor
        // Ilk adet suresinde biter, ikincisi surerken iptal: kalan adedin malzemesi geri doner.
        q.Tick(tezgah.Seconds * 0.5f, inv);
        Assert.Equal(0, inv.Count("tezgah"));
        q.Tick(tezgah.Seconds * 0.6f, inv);
        Assert.Equal(1, inv.Count("tezgah"));
        Assert.Single(q.Jobs);
        Assert.Equal(1, q.Jobs[0].Left);
        var back = q.Cancel(0, inv);
        Assert.Empty(q.Jobs);
        Assert.Equal(10, back["wood"]);
        Assert.Equal(5, back["stone"]);
        Assert.Equal(20, inv.Count("wood"));
        Assert.Equal(5, inv.Count("stone"));
        Assert.Equal(1, inv.Count("tezgah"));
    }

    [Fact]
    public void Hand_queue_runs_jobs_in_order()
    {
        var d = Data();
        var inv = new Inventory(20, d.Tool.StackOf);
        inv.Add("wood", 20); inv.Add("fiber", 10);
        var q = new CraftQueue(d.Tool.CraftQueueMax);
        Assert.True(q.Enqueue(R(d, "mesale"), 1, inv));
        Assert.True(q.Enqueue(R(d, "uyku_tulumu"), 1, inv));
        q.Tick(R(d, "mesale").Seconds + 0.01f, inv);
        Assert.Equal(1, inv.Count("mesale"));
        Assert.Equal(0, inv.Count("uyku_tulumu"));
        q.Tick(R(d, "uyku_tulumu").Seconds, inv);
        Assert.Equal(1, inv.Count("uyku_tulumu"));
        Assert.Empty(q.Jobs);
    }

    [Fact]
    public void Station_queue_cancel_returns_inputs_and_fuel()
    {
        var d = Data();
        var inv = new Inventory(20, d.Tool.StackOf);
        inv.Add("copper_ore", 4); inv.Add("wood", 2);
        var q = new StationQueue(d, "tas_firin");
        var ingot = R(d, "copper_ingot");
        Assert.True(q.Enqueue(ingot, inv));
        Assert.True(q.Enqueue(ingot, inv));
        Assert.Equal(0, inv.Count("copper_ore"));
        Assert.Equal(0, inv.Count("wood"));
        var back = q.Cancel(inv);
        Assert.Null(q.Active);
        Assert.Equal(4, inv.Count("copper_ore"));
        Assert.Equal(2, inv.Count("wood"));
        Assert.Equal(4, back["copper_ore"]);
    }

    // ---------- Istasyon yakinligi ----------

    [Fact]
    public void Station_recipes_need_their_station_nearby_hand_recipes_do_not()
    {
        var d = Data();
        var none = Array.Empty<string>();
        Assert.Null(CraftRules.NeedStation(R(d, "tezgah"), none));
        Assert.Equal("tezgah", CraftRules.NeedStation(R(d, "tas_firin"), none));
        Assert.Null(CraftRules.NeedStation(R(d, "tas_firin"), new[] { "tezgah" }));
        Assert.Equal("tas_firin", CraftRules.NeedStation(R(d, "copper_ingot"), new[] { "tezgah" }));
    }

    // ---------- Yerlestirilebilirler uretilir ----------

    [Fact]
    public void Every_placeable_except_ocak_has_a_recipe_with_the_old_cost()
    {
        var d = Data();
        foreach (var p in d.Tool.Placeables.Where(p => !p.Once))
        {
            var r = d.Tool.RecipeFor(p.Id);
            Assert.True(r != null, $"{p.Id}: tarifi yok");
            Assert.NotEmpty(r!.In);
            Assert.Empty(p.Cost); // bedel tarifte, iki yerde degil
        }
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 10, ["stone"] = 5 }, d.Tool.CostOf("tezgah"));
        Assert.Equal(new Dictionary<string, int> { ["stone"] = 15, ["clay"] = 5 }, d.Tool.CostOf("tas_firin"));
        Assert.Contains("ocak", d.Tool.StartItems.Keys); // Ocak kapsulden gelir, kemerden kurulur
    }

    [Fact]
    public void Plan_wheel_names_build_pieces_with_costs()
    {
        var d = Data();
        Assert.NotEmpty(d.Tool.PlanWheel);
        foreach (var id in d.Tool.PlanWheel) Assert.True(d.Tool.BuildCosts.ContainsKey(id), $"{id}: buildCosts yok");
        foreach (var id in new[] { "kare_temel", "duvar", "kapi_kasasi", "duz_cati", "cit", "bahce_kapisi" }) Assert.Contains(id, d.Tool.PlanWheel);
    }

    // ---------- Eksik malzeme ----------

    [Fact]
    public void Missing_material_lists_shortfall_and_where_to_find_it()
    {
        var d = Data();
        var inv = new Inventory(20, d.Tool.StackOf);
        inv.Add("stone", 15);
        var miss = CraftRules.Missing(R(d, "tas_firin"), 1, inv);
        var clay = Assert.Single(miss);
        Assert.Equal(("clay", 0, 5), clay);
        var src = CraftRules.Source(d.Tool, "clay");
        Assert.NotNull(src);
        Assert.Contains("kil yatak", src!.Text);
        Assert.Contains("Kazma", src.Text);
        Assert.Equal("rock:clay", src.Target);
        Assert.Contains("Kazma", CraftRules.Source(d.Tool, "copper_ore")!.Text);
        Assert.Contains("Biçme", CraftRules.Source(d.Tool, "fiber")!.Text);
    }

    [Fact]
    public void Every_recipe_input_has_a_source()
    {
        var d = Data();
        var inputs = d.Tool.Recipes.SelectMany(r => r.In.Keys).Distinct();
        var missing = inputs.Where(i => CraftRules.Source(d.Tool, i) is not { Text.Length: > 0, Target.Length: > 0 }).ToList();
        Assert.True(missing.Count == 0, "kaynagi yazilmamis girdi: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_recipe_category_is_listed()
    {
        var d = Data();
        var cats = d.Tool.CraftCategories.Select(c => c.Id).ToHashSet();
        Assert.Contains("all", cats);
        foreach (var r in d.Tool.Recipes) Assert.True(cats.Contains(r.Category), $"{r.Id}: kategori '{r.Category}' craft.json'da yok");
    }
}
