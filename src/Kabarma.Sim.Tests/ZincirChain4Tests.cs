using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 zincir4: T3-T6 istasyonlari ve ara urunleri (ICERIK §2.3, §11, §20; PLAN §7.1). Yalniz Sim verisi: ham kaynak
/// dugumlerini biyom akisi kurar, bu testler onlara dayanmaz (kaynaklar research.json rawSources'tan).
/// </summary>
public class ZincirChain4Tests
{
    static GameData Data() => TestData.Load();

    /// <summary>ICERIK §11'deki 43 istasyon ve makine (PLAN §7.1). Kargo Rampasi Kapsul'den gelir (placeables.json'da yok).</summary>
    static readonly string[] Stations43 =
    {
        "sirt_yazicisi", "tezgah", "tas_firin", "komur_ocagi", "ocakbasi", "tabaklama_askisi", "harman_yeri", "paketleme", "kargo_rampasi", "arastirma_masasi",
        "demirci_ocagi", "bicki", "degirmen", "dokuma_tezgahi", "kasap_tezgahi", "kulucka", "arac_atolyesi", "oyma_iskelesi",
        "yuksek_firin", "kimya_tezgahi", "pres", "mutfak", "damitici", "tursu_ficisi", "hasat_robotu", "otomatik_yemlik", "yumurta_toplayici", "kompostor",
        "cam_ocagi", "elektronik_tezgahi", "montaj_makinesi", "rafineri", "sera_kontrol_unitesi", "ekim_robotu", "sagim_kirkim_kolu", "paketleyici", "sondaj_kulesi",
        "kristal_kesici", "batarya_tezgahi", "asi_tezgahi", "zeplin_hangari",
        "maya_dokumhanesi", "kabartma_odasi",
    };

    /// <summary>Henuz baska akisin isi olanlar: Asi Tezgahi tarim4'te (gen aktarma). tarim4 birlesince bu liste bosalir.</summary>
    static readonly string[] OtherStreams = { "asi_tezgahi" };

    /// <summary>zincir4'un yeni tarifleri (T3+): ResearchTests'in T2 testi bunlari "ulasilamayan" saymaz.</summary>
    public static readonly string[] T3Plus =
    {
        "yuksek_firin", "kimya_tezgahi", "pres", "mutfak", "damitici", "tursu_ficisi", "hasat_robotu", "otomatik_yemlik", "yumurta_toplayici", "kompostor",
        "cam_ocagi", "elektronik_tezgahi", "montaj_makinesi", "rafineri", "sera_kontrol_unitesi", "ekim_robotu", "sagim_kirkim_kolu", "paketleyici", "sondaj_kulesi",
        "kristal_kesici", "batarya_tezgahi", "zeplin_hangari", "maya_dokumhanesi", "kabartma_odasi",
        "ark_eki", "celik", "titan_ingot", "kaucuk", "bitkisel_yag", "barut", "kimyasal_gubre", "havan_mermisi", "maya_siringasi", "biyoyakit", "cam", "devre", "plastik",
        "termal_kumas", "kristal_lens", "aku_hucresi", "mayali_alasim", "mayali_beton", "reaktor_cubugu",
    };

    [Fact]
    public void Station_count_matches_plan_7_1()
    {
        var d = Data();
        Assert.Equal(43, Stations43.Length);
        Assert.Equal(43, Stations43.Distinct().Count());
        var present = d.Tool.Placeables.Where(p => p.Station != null).Select(p => p.Station!).ToHashSet();
        present.Add("kargo_rampasi"); // Kapsul'den (ItemNames, Pod)
        foreach (var id in Stations43.Except(OtherStreams)) Assert.True(present.Contains(id), "istasyon yok: " + id);
        foreach (var id in present) Assert.True(Stations43.Contains(id), "ICERIK §11'de olmayan istasyon: " + id);
        Assert.Equal(43 - OtherStreams.Count(id => !present.Contains(id)), present.Count);
    }

    [Fact]
    public void Every_new_station_has_recipe_cost_and_watts_from_the_table()
    {
        var d = Data();
        // ICERIK §11 "Guc" sutunu.
        var watts = new Dictionary<string, int>
        {
            ["yuksek_firin"] = 100, ["kimya_tezgahi"] = 40, ["pres"] = 60, ["mutfak"] = 40, ["damitici"] = 60, ["hasat_robotu"] = 60, ["otomatik_yemlik"] = 10, ["yumurta_toplayici"] = 10, ["kompostor"] = 20,
            ["elektronik_tezgahi"] = 80, ["montaj_makinesi"] = 150, ["rafineri"] = 200, ["sera_kontrol_unitesi"] = 100, ["ekim_robotu"] = 60, ["sagim_kirkim_kolu"] = 40, ["paketleyici"] = 60, ["sondaj_kulesi"] = 200,
            ["kristal_kesici"] = 150, ["batarya_tezgahi"] = 150, ["zeplin_hangari"] = 200, ["maya_dokumhanesi"] = 400, ["kabartma_odasi"] = 800,
        };
        var mine = Stations43.Except(OtherStreams).Where(id => T3Plus.Contains(id) || id is "sirt_yazicisi" or "tabaklama_askisi" or "harman_yeri");
        foreach (var id in mine)
        {
            var p = d.Tool.Placeables.SingleOrDefault(x => x.Id == id);
            Assert.NotNull(p);
            Assert.Equal(id, p!.Station);
            Assert.True(d.Tool.Recipes.Count(r => r.Id == id) == 1, "istasyonun uretim tarifi: " + id);
            Assert.Equal(watts.GetValueOrDefault(id), d.Research.Watts.GetValueOrDefault(id));
        }
        Assert.Equal(25 + 3, mine.Count() + OtherStreams.Length); // 25 yeni T3-T6 (Asi Tezgahi tarim4'te) + 3 eksik T1
        // Yakitli ocaklar (ICERIK §11 Guc: "Yakit").
        foreach (var id in new[] { "yuksek_firin", "cam_ocagi" }) Assert.True(d.Tool.Placeables.Single(p => p.Id == id).FuelPerJob > 0, id);
    }

    [Fact]
    public void Recipe_graph_reaches_the_kabartma_odasi_from_the_start()
    {
        var d = Data();
        var g = RecipeGraph.Walk(d, 6);
        Assert.True(g.Unreached.Count == 0, "T6'da ulasilamayan tarif: " + string.Join(", ", g.Unreached.Select(r => r.Id)));
        Assert.True(g.Has("kabartma_odasi"));
        foreach (var id in T3Plus) Assert.True(g.Has(id), id);
        // Basamak basamak: Ocak basamagi yetmeyince ulasilmaz.
        Assert.False(RecipeGraph.Walk(d, 5).Has("kabartma_odasi"));
        Assert.False(RecipeGraph.Walk(d, 2).Has("celik"));
        Assert.True(RecipeGraph.Walk(d, 3).Has("celik") && RecipeGraph.Walk(d, 3).Has("kaucuk"));
        Assert.True(RecipeGraph.Walk(d, 4).Has("devre") && RecipeGraph.Walk(d, 4).Has("titan_ingot"));
        Assert.True(RecipeGraph.Walk(d, 5).Has("kristal_lens") && RecipeGraph.Walk(d, 5).Has("aku_hucresi"));
        Assert.True(RecipeGraph.Walk(d, 6).Has("reaktor_cubugu"));
        // Kabartma Odasi, Ana Maya'nin plani (rawSources 'kabartma_plani', ata4) olmadan yapilamaz.
        var noPlan = d.Research.RawSources.Where(s => s.Id != "kabartma_plani").ToList();
        Assert.True(noPlan.Count < d.Research.RawSources.Count);
        d.Research.RawSources.Clear();
        d.Research.RawSources.AddRange(noPlan);
        Assert.False(RecipeGraph.Walk(d, 6).Has("kabartma_odasi"));
    }

    [Fact]
    public void Recipe_graph_has_no_cycle()
    {
        var d = Data();
        var byId = d.Tool.Recipes.ToDictionary(r => r.Id);
        var state = new Dictionary<string, int>(); // 1 yolda, 2 bitti
        var stack = new List<string>();
        void Visit(string id)
        {
            if (state.GetValueOrDefault(id) == 2) return;
            Assert.True(state.GetValueOrDefault(id) != 1, "tarif dongusu: " + string.Join(" -> ", stack.Append(id)));
            state[id] = 1; stack.Add(id);
            var r = byId[id];
            foreach (var dep in r.In.Keys.Append(r.Station).Append(r.UnlockOn ?? "")) if (dep.Length > 0 && byId.ContainsKey(dep)) Visit(dep);
            stack.RemoveAt(stack.Count - 1); state[id] = 2;
        }
        foreach (var id in byId.Keys) Visit(id);
    }

    [Fact]
    public void Titan_needs_the_ark_attachment_and_the_attachment_needs_circuits()
    {
        var d = Data();
        var titan = d.Tool.Recipes.Single(r => r.Id == "titan_ingot");
        Assert.Equal("yuksek_firin", titan.Station);
        Assert.Equal("ark_eki", titan.UnlockOn);
        Assert.Equal(new[] { ("celik", 10), ("devre", 4) }, d.Tool.Recipes.Single(r => r.Id == "ark_eki").In.Select(kv => (kv.Key, kv.Value)).OrderBy(x => x.Key).ToArray()); // ICERIK §20
        Assert.Equal(("titan_ore", 2), (titan.In.Keys.First(k => k == "titan_ore"), titan.In["titan_ore"]));
    }

    [Fact]
    public void Chain_costs_follow_icerik_section_20()
    {
        var d = Data();
        int N(string id, string item) => d.Tool.Recipes.Single(r => r.Id == id).In.GetValueOrDefault(item);
        Assert.Equal(30, N("yuksek_firin", "brick")); Assert.Equal(15, N("yuksek_firin", "iron_ingot")); Assert.Equal(10, N("yuksek_firin", "clay"));
        Assert.Equal(10, N("kimya_tezgahi", "celik")); Assert.Equal(10, N("pres", "plank"));
        Assert.Equal(20, N("cam_ocagi", "celik")); Assert.Equal(20, N("cam_ocagi", "brick"));
        Assert.Equal(15, N("elektronik_tezgahi", "celik")); Assert.Equal(10, N("elektronik_tezgahi", "cam"));
        Assert.Equal(30, N("rafineri", "celik")); Assert.Equal(5, N("rafineri", "devre"));
        Assert.Equal(20, N("kristal_kesici", "titan_ingot")); Assert.Equal(20, N("batarya_tezgahi", "titan_ingot")); Assert.Equal(10, N("batarya_tezgahi", "plastik"));
        Assert.Equal(30, N("maya_dokumhanesi", "titan_ingot")); Assert.Equal(20, N("maya_dokumhanesi", "obsidyen")); Assert.Equal(100, N("maya_dokumhanesi", "maya"));
        Assert.Equal(40, N("kabartma_odasi", "mayali_alasim")); Assert.Equal(20, N("kabartma_odasi", "devre")); Assert.Equal(10, N("kabartma_odasi", "kristal_lens")); Assert.Equal(10, N("kabartma_odasi", "reaktor_cubugu"));
        // §2.3 ara urunler
        Assert.Equal(2, N("kaucuk", "kaucuk_sutu")); Assert.Equal(2, N("cam", "kum")); Assert.Equal(1, N("cam", "turba"));
        Assert.Equal(1, N("devre", "kuvars")); Assert.Equal(2, N("devre", "copper_wire")); Assert.Equal(2, N("plastik", "katran"));
        Assert.Equal(2, N("kristal_lens", "ayaz_kristali")); Assert.Equal(1, N("aku_hucresi", "tuz"));
        Assert.Equal(2, N("mayali_alasim", "titan_ingot")); Assert.Equal(1, N("mayali_alasim", "ham_maya"));
        Assert.Equal(2, N("mayali_beton", "balcik")); Assert.Equal(2, N("reaktor_cubugu", "isil_cevher")); Assert.Equal(1, N("reaktor_cubugu", "mayali_alasim"));
    }

    [Fact]
    public void Every_raw_input_of_the_chain_has_a_source_with_a_tier()
    {
        var d = Data();
        var made = d.Tool.Recipes.Select(r => r.Id).ToHashSet();
        var raw = d.Research.RawSources.ToDictionary(s => s.Id, s => s.Tier);
        var start = d.Tool.StartItems.Keys.ToHashSet();
        foreach (var r in d.Tool.Recipes.Where(r => T3Plus.Contains(r.Id)))
            foreach (var k in r.In.Keys) Assert.True(made.Contains(k) || raw.ContainsKey(k) || start.Contains(k), $"{r.Id}: girdi '{k}' ne tarif ne ham kaynak");
        // ICERIK §2.1 / kimlikler-m4 §1: Sazlik T3, Kanyon T4, Ayaz T5, Gobek T6.
        foreach (var id in new[] { "turba", "kukurt", "kaucuk_sutu", "balcik" }) Assert.Equal(3, raw[id]);
        foreach (var id in new[] { "kum", "kuvars", "titan_ore", "katran", "tuz" }) Assert.Equal(4, raw[id]);
        foreach (var id in new[] { "buz", "ayaz_kristali" }) Assert.Equal(5, raw[id]);
        foreach (var id in new[] { "ham_maya", "obsidyen", "isil_cevher" }) Assert.Equal(6, raw[id]);
    }

    [Fact]
    public void Research_nodes_of_t3_to_t5_follow_the_station_tiers()
    {
        var d = Data();
        foreach (var n in d.Research.Nodes.Where(n => n.Tier >= 3))
        {
            var r = d.Tool.Recipes.Single(r => r.UnlockOn == ResearchState.Token(n.Id));
            Assert.True(d.Research.CostOf(n) >= d.Research.CostByTier[3]);
            foreach (var q in n.Requires) Assert.True(d.Research.Node(q)!.Tier <= n.Tier, $"{n.Id} oncul {q} daha ust basamakta");
        }
        Assert.Equal(220, d.Research.CostOf(d.Research.Node("zeplin_hangari")!)); // costByTier[5]
    }
}
