using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M2 zincir: T1-T2 uretim zinciri, arastirma, Tarayici, istasyon kuyruklari, ambardan uretim ve envanter duzeni
/// (GDD §4, §5, §12; ICERIK §0, §11.1, §20; TEKNIK §11 tarif grafi).
/// </summary>
public class ResearchTests
{
    static GameData Data() => TestData.Load();
    static RecipeDef R(GameData d, string id) => d.Tool.Recipes.Single(r => r.Id == id);
    static void Run(StationQueue q, IItemStore inv, float seconds) { for (float t = 0; t < seconds; t += 0.1f) q.Tick(0.1f, inv); }

    // ---------- Tarif grafi (ICERIK §20) ----------

    [Fact]
    public void Recipe_graph_reaches_the_end_of_t2_from_the_start()
    {
        var d = Data();
        var g = RecipeGraph.Walk(d, 2);
        var stuck = g.Unreached.Where(r => !ZincirChain4Tests.T3Plus.Contains(r.Id)).ToList(); // T3+ tarifleri T2'de beklenen sekilde ulasilmaz (zincir4)
        Assert.True(stuck.Count == 0, "T2'de ulasilamayan tarif: " + string.Join(", ", stuck.Select(r => r.Id)));
        foreach (var id in new[] { "arastirma_masasi", "komur_ocagi", "charcoal", "brick", "copper_wire", "demirci_ocagi", "iron_ingot", "bicki", "plank", "degirmen", "flour", "dokuma_tezgahi", "cloth", "arac_atolyesi", "filtre_maskesi" })
            Assert.True(g.Has(id), id);
        Assert.Contains("arastirma:arac_atolyesi", g.Tokens);
    }

    [Fact]
    public void At_t1_research_opens_cloth_but_iron_waits_for_the_ata_and_koru()
    {
        var g = RecipeGraph.Walk(Data(), 1);
        Assert.True(g.Has("arastirma_masasi"));
        Assert.True(g.Has("dokuma_tezgahi") && g.Has("cloth"), "Kumas T1'de arastirmayla yapilabilmeli (Cayir malzemesi)");
        Assert.False(g.Has("demirci_ocagi")); // Ata: Ocak T2
        Assert.False(g.Has("iron_ingot"));
        Assert.False(g.Has("bicki"));         // demir ister
        Assert.Contains(g.Unreached, r => r.Id == "filtre_maskesi");
    }

    [Fact]
    public void Recipe_graph_catches_a_cycle()
    {
        var d = Data();
        // Bicki Kereste isteseydi: Kereste Bicki'den cikar, ikisi de hic yapilamaz.
        var bad = d.Tool.Recipes.Select(r => r.Id != "bicki" ? r : new RecipeDef { Id = r.Id, Name = r.Name, Station = r.Station, Category = r.Category, Out = 1, Seconds = r.Seconds, UnlockOn = r.UnlockOn, In = new() { ["plank"] = 4, ["iron_ingot"] = 5 } }).ToList();
        var g = RecipeGraph.Walk(d, 2, bad);
        Assert.Contains(g.Unreached, r => r.Id == "bicki");
        Assert.Contains(g.Unreached, r => r.Id == "plank");
    }

    [Fact]
    public void Powered_stations_need_a_reachable_generator()
    {
        var d = Data();
        Assert.Contains(d.Power.Devices, g => g.Kind == "generator");
        // Ureteclerin tarifi olmasaydi Bicki ve Degirmen calismazdi: Kereste ve Un hic yapilamazdi.
        var gens = d.Power.Devices.Where(g => g.Kind == "generator").Select(g => g.Id).ToHashSet();
        var noGen = d.Tool.Recipes.Where(r => !gens.Contains(r.Id)).ToList();
        var g = RecipeGraph.Walk(d, 2, noGen);
        Assert.True(g.Has("bicki"));     // kurulur,
        Assert.False(g.Has("plank"));    // ama gucsuz calismaz (20 W)
        Assert.False(g.Has("flour"));
        Assert.True(g.Has("cloth"));     // Dokuma guc istemez
        Assert.True(RecipeGraph.Walk(d, 2).Has("plank"));
    }

    // ---------- Veri ve agac ----------

    [Fact]
    public void Research_data_is_consistent()
    {
        var d = Data();
        var rs = d.Research;
        Assert.Equal(new[] { 0, 10, 25, 60, 120, 220, 400 }, rs.CostByTier); // ICERIK §0
        Assert.Contains(d.Tool.Placeables, p => p.Station == rs.Table);
        var ids = rs.Nodes.Select(n => n.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var n in rs.Nodes)
        {
            foreach (var q in n.Requires) Assert.Contains(q, ids);
            Assert.Contains(d.Tool.Recipes, r => r.UnlockOn == ResearchState.Token(n.Id)); // her dugum bir tarifi acar
        }
        foreach (var r in d.Tool.Recipes.Where(r => r.UnlockOn?.StartsWith(ResearchFile.ResearchPrefix) == true))
            Assert.NotNull(rs.NodeFor(r));
        // Agacta dongu yok: siralanabilir.
        var done = new HashSet<string>();
        for (int pass = 0; pass < ids.Count && done.Count < ids.Count; pass++)
            foreach (var n in rs.Nodes) if (n.Requires.All(done.Contains)) done.Add(n.Id);
        Assert.Equal(ids.Count, done.Count);
        // Yeni ara urunler 20'lik yigin (GDD §5).
        foreach (var id in new[] { "copper_wire", "brick", "charcoal", "iron_ingot", "plank", "cloth", "flour" }) Assert.Equal(20, d.Tool.StackOf(id));
        // Her tarifi olan istasyon yerlestirilebilir.
        foreach (var st in d.Tool.Recipes.Select(r => r.Station).Where(s => s.Length > 0).Distinct()) Assert.Contains(d.Tool.Placeables, p => p.Station == st);
    }

    [Fact]
    public void Research_needs_table_veri_and_prereqs_then_unlocks_the_recipe()
    {
        var d = Data();
        var s = new GameState(d);
        var tez = s.QueueFor("tezgah");
        var rs = s.Research;
        Assert.Equal(1, rs.OcakTier);
        Assert.False(tez.IsUnlocked(R(d, "dokuma_tezgahi")));
        Assert.Equal(ResearchBlock.NoTable, rs.Check("dokuma_tezgahi", false));
        Assert.Equal(ResearchBlock.NoVeri, rs.Check("dokuma_tezgahi", true));
        Assert.Equal(ResearchBlock.NeedsPrereq, rs.Check("degirmen", true));
        Assert.Equal(ResearchBlock.NotResearch, rs.Check("tezgah", true));
        rs.AddVeri(30, "test");
        Assert.True(rs.Research("dokuma_tezgahi", true));
        Assert.Equal(5, rs.Veri);                      // T2: 25 Veri
        Assert.True(tez.IsUnlocked(R(d, "dokuma_tezgahi")));
        Assert.Equal(ResearchBlock.Done, rs.Check("dokuma_tezgahi", true));
        Assert.False(rs.Research("filtre_maskesi", true)); // 5 Veri yetmez
        Assert.Contains("arastirma:dokuma_tezgahi", s.Known); // kayit: arastirilanlar Known'da
    }

    [Fact]
    public void Ocak_tier_opens_ata_stations()
    {
        var d = Data();
        var s = new GameState(d);
        var tez = s.QueueFor("tezgah");
        Assert.False(tez.IsUnlocked(R(d, "demirci_ocagi")));
        s.Research.SetOcakTier(2);
        Assert.True(tez.IsUnlocked(R(d, "demirci_ocagi")));
        Assert.Equal(60f, s.Research.ClaimRadius); // GDD §6: T2 tapu 60 m
        s.Research.SetOcakTier(1);                 // geri inmez
        Assert.Equal(2, s.Research.OcakTier);
    }

    [Fact]
    public void Scanner_gives_veri_once_per_kind()
    {
        var s = new GameState(Data());
        var rs = s.Research;
        Assert.Equal(15, rs.Scan("creature", "gidak"));
        Assert.Equal(0, rs.Scan("creature", "gidak"));  // ikinci kez Veri yok
        Assert.Equal(6, rs.Scan("mineral", "clay"));
        Assert.Equal(-1, rs.Scan("mineral", "gidak"));  // tur yanlis
        Assert.Equal(21, rs.Veri);
        Assert.Equal(new[] { "clay", "gidak" }, rs.Scanned.OrderBy(x => x));
        // Cayir'in taranabilirleri en az bir T2 arastirmasini karsilar (bot yolu).
        int cayir = d().Research.Scan.Entries.Where(e => e.Id != "iron_ore").Sum(e => e.Veri);
        Assert.True(cayir >= d().Research.CostByTier[2], $"Cayir taramasi {cayir} Veri");
        static GameData d() => TestData.Load();
    }

    // ---------- Istasyon kuyrugu ve ambardan uretim ----------

    [Fact]
    public void Station_queue_runs_different_recipes_in_order_and_cancels_one()
    {
        var d = Data();
        var s = new GameState(d);
        var inv = s.Backpack;
        var firin = s.QueueFor("tas_firin");
        inv.Add("copper_ore", 4); inv.Add("clay", 2); inv.Add("fiber", 1); inv.Add("wood", 10);
        Assert.True(firin.Enqueue(R(d, "copper_ingot"), inv));
        Assert.True(firin.Enqueue(R(d, "brick"), inv));         // M1'de "baska bir sey uretiyor" idi
        Assert.True(firin.Enqueue(R(d, "copper_ingot"), inv));
        Assert.Equal(3, firin.Jobs.Count);
        Assert.Equal(7, inv.Count("wood"));                      // is basina 1 Odun
        var back = firin.Cancel(2, inv);                         // son kulce isi
        Assert.Equal(2, back["copper_ore"]);
        Assert.Equal(1, back["wood"]);
        Run(firin, inv, 4.1f + 5.1f);
        Assert.Equal(1, inv.Count("copper_ingot"));
        Assert.Equal(1, inv.Count("brick"));
        Assert.Null(firin.Active);
    }

    [Fact]
    public void Station_crafts_from_nearby_chests_and_overflows_into_them()
    {
        var d = Data();
        var s = new GameState(d);
        var chest = new Inventory(20, d.Tool.StackOf);
        s.Backpack.Add("copper_ingot", 1);
        chest.Add("copper_ingot", 5);
        var store = s.StationStore(new[] { chest });
        var tez = s.QueueFor("tezgah");
        s.Known.Add("copper_ingot");
        Assert.Equal(6, store.Count("copper_ingot"));
        for (int i = 0; i < 3; i++) Assert.True(tez.Enqueue(R(d, "copper_wire"), store));
        Assert.Equal(0, s.Backpack.Count("copper_ingot"));      // once canta,
        Assert.Equal(3, chest.Count("copper_ingot"));           // eksigi Sandik'tan
        Run(tez, store, 10f);
        Assert.Equal(6, s.Backpack.Count("copper_wire"));
        // Canta doluysa urun Sandik'a; ikisi de doluysa is bekler.
        var full = new GameState(d);
        for (int i = 0; full.Backpack.UsedSlots < full.Backpack.Slots; i++) full.Backpack.Add("stone", 50);
        var box = new Inventory(1, d.Tool.StackOf);
        box.Add("copper_ingot", 1);
        full.Known.Add("copper_ingot");
        var st = full.QueueFor("tezgah");
        var pool = full.StationStore(new[] { box });
        Assert.True(st.Enqueue(R(d, "copper_wire"), pool));      // kulce kutudan, kutu bosaldi
        Run(st, pool, 4f);
        Assert.Equal(2, box.Count("copper_wire"));
        Assert.True(st.Enqueue(R(d, "copper_wire"), pool) == false); // kulce yok
        var tiny = new Inventory(1, d.Tool.StackOf);
        tiny.Add("copper_ingot", 2); // kulce alininca yuvada 1 kulce kalir: tele yer yok
        var blocked = full.StationStore(new[] { tiny });
        Assert.True(st.Enqueue(R(d, "copper_wire"), blocked));
        Run(st, blocked, 4f);
        Assert.True(st.Blocked);                                 // yer yok: bekler, urun kaybolmaz
        Assert.NotNull(st.Active);
    }

    [Fact]
    public void Power_hook_stops_a_station_and_watts_come_from_data()
    {
        var d = Data();
        var s = new GameState(d);
        var bicki = s.QueueFor("bicki");
        Assert.Equal(20, bicki.Watts);
        Assert.Equal(0, s.QueueFor("dokuma_tezgahi").Watts);
        s.Backpack.Add("wood", 2);
        Assert.True(bicki.Enqueue(R(d, "plank"), s.Backpack));
        bool on = false;
        bicki.PowerOk = () => on;
        Run(bicki, s.Backpack, 5f);
        Assert.Equal(0, s.Backpack.Count("plank"));
        on = true;
        Run(bicki, s.Backpack, 5f);
        Assert.Equal(1, s.Backpack.Count("plank"));
    }

    [Fact]
    public void Stations_without_recipes_get_a_queue()
    {
        var s = new GameState(Data());
        Assert.False(s.Stations.ContainsKey("kulucka"));
        var q = s.QueueFor("kulucka");
        Assert.Same(q, s.QueueFor("kulucka"));
        Assert.Empty(q.Recipes);
    }

    [Fact]
    public void Ata_core_raises_the_ocak_tier()
    {
        var s = new GameState(Data());
        Assert.Equal(0, s.Research.TryUpgradeOcak(s.Backpack));
        s.Backpack.Add("cayir_cekirdegi", 1);
        Assert.Equal(2, s.Research.TryUpgradeOcak(s.Backpack));
        Assert.Equal(0, s.Backpack.Count("cayir_cekirdegi"));
        Assert.Contains("ocak:2", s.Known);
    }

    [Fact]
    public void Research_and_multi_job_stations_survive_save_and_load()
    {
        var d = Data();
        var a = new GameState(d);
        a.Research.AddVeri(40, "test");
        a.Research.Scan("creature", "zipzip");
        a.Research.Research("bicki", true);
        a.Research.SetOcakTier(2);
        a.Backpack.Add("copper_ore", 2); a.Backpack.Add("clay", 2); a.Backpack.Add("fiber", 1); a.Backpack.Add("wood", 5);
        var f = a.QueueFor("tas_firin");
        f.Enqueue(R(d, "copper_ingot"), a.Backpack);
        f.Enqueue(R(d, "brick"), a.Backpack);
        f.Tick(1.5f, a.Backpack);
        var rs = a.Research.Save();
        var st = f.Save();
        var b = new GameState(d);
        foreach (var k in a.Known) b.Known.Add(k); // "core" bolumu
        b.Research.Load(rs);
        b.QueueFor("tas_firin").Load(st);
        Assert.Equal(a.Research.Veri, b.Research.Veri);
        Assert.Equal(2, b.Research.OcakTier);
        Assert.Contains("ocak:2", b.Known);
        Assert.Equal(new[] { "zipzip" }, b.Research.Scanned);
        var g = b.QueueFor("tas_firin");
        Assert.Equal(new[] { "copper_ingot", "brick" }, g.Jobs.Select(j => j.Recipe.Id));
        Assert.Equal(f.Progress, g.Progress, 3);
        var back = g.Cancel(b.Backpack);                 // yakit da kayittan gelir
        Assert.Equal(2, back["wood"]);
    }

    // ---------- Envanter ----------

    [Fact]
    public void Sort_merges_half_stacks_and_keeps_counts()
    {
        var d = Data();
        var s = new GameState(d);
        var inv = new Inventory(10, d.Tool.StackOf);
        inv.Add("wood", 30); inv.Add("stone", 10); inv.Add("turnip", 3, 2);
        var other = new Inventory(10, d.Tool.StackOf);
        other.Add("wood", 40);
        other.MoveTo(inv, "wood", 40); // 30 + 40: 50'lik ve 20'lik
        inv.Remove("stone", 5);
        inv.Add("stone", 5);
        inv.Add("wood", 0);
        int changes = 0;
        inv.Changed += (_, _) => changes++;
        inv.Sort();
        Assert.Equal(0, changes);
        Assert.Equal(70, inv.Count("wood"));
        Assert.Equal(10, inv.Count("stone"));
        Assert.Equal(3, inv.Count("turnip", 2));
        Assert.Equal(new[] { ("stone", 10, 1), ("turnip", 3, 2), ("wood", 50, 1), ("wood", 20, 1) }, inv.Stacks);
    }

    [Fact]
    public void Quick_stack_moves_only_what_the_chest_already_holds()
    {
        var d = Data();
        var bag = new Inventory(20, d.Tool.StackOf);
        var chest = new Inventory(20, d.Tool.StackOf);
        bag.Add("wood", 30); bag.Add("stone", 12); bag.Add("clay", 4);
        chest.Add("wood", 1); chest.Add("clay", 1);
        Assert.Equal(34, bag.QuickStackTo(chest));
        Assert.Equal(0, bag.Count("wood"));
        Assert.Equal(12, bag.Count("stone"));
        Assert.Equal(31, chest.Count("wood"));
        Assert.Equal(5, chest.Count("clay"));
    }
}
