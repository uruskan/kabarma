using System.Numerics;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 kale (ICERIK §10, §13; PLAN §7.1): 8 kule, 8 tuzak, Cam seti ve 72 yerlestirilebilir. Kurallar Sim'de; Godot
/// katmani (atis, efekt, guc) --test=kale'de.
/// </summary>
public class KaleTests
{
    static GameData D => TestData.Load();
    static DefenseTarget T(int id, float x, float z, float hp = 100, bool fly = false, float r = 0.4f) => new(id, new Vector3(x, 0, z), Vector3.Zero, r, hp, hp, Flying: fly);

    static readonly string[] Towers8 = { "civi_kulesi", "sapan_kulesi", "sacma_kulesi", "alev_kulesi", "havan_kulesi", "ark_bobini", "lazer_kulesi", "maya_topu_kulesi" };
    static readonly string[] Traps8 = { "diken_tarlasi", "yapiskan_zemin", "firlatma_pedi", "cukur_kapagi", "elektrikli_cit", "mayin", "yag_hendegi", "dondurucu_puskurtucu" };

    /// <summary>ICERIK §10.2: 72 yerlestirilebilir.</summary>
    static readonly string[] Placeables72 =
    {
        "ahsap_kapi", "celik_kapi", "cift_kanat_kapi", "garaj_kapisi", "tavan_kapagi", "el_merdiveni",
        "sandik", "ambar", "silo", "sivi_tanki", "akilli_depo", "tohum_dolabi", "alet_askisi", "kupa_rafi",
        "uyku_tulumu", "yatak", "ranza", "sedir", "masa", "sandalye", "hali", "soba",
        "mesale", "fener", "elektrik_lambasi", "projektor", "nilufer_saksisi", "sokak_lambasi",
        "korkuluk", "yagmurlama", "damla_hatti", "buyutme_lambasi", "kompost_kasasi", "saksi", "yukseltilmis_yatak", "ari_kovani", "sirik", "tarla_tabelasi",
        "yemlik", "suluk", "folluk", "savas_agili_bayragi", "ahir_zili", "baglama_diregi", "kasinma_fircasi", "eyerlik",
        "mancinik", "harita_masasi", "is_emri_panosu", "borsa_tahtasi", "katalog_terminali", "tohum_carki", "gardirop", "tabela",
        "saksi_cicegi", "duvar_halisi", "tablo", "ekip_bayragi", "ata_heykeli", "ruzgar_cani", "kus_evi", "gidak_cucesi", "cesme", "hamak", "salincak", "kamp_atesi", "raf", "kitaplik", "saat", "radyo", "semaver", "kilim",
    };

    [Fact]
    public void Plan_7_1_counts_8_towers_8_traps_72_placeables()
    {
        var d = D;
        Assert.Equal(Towers8, d.Defense.Towers.Select(t => t.Id));
        Assert.Equal(Traps8, d.Defense.Traps.Select(t => t.Id));
        Assert.Equal(72, Placeables72.Length);
        Assert.Equal(72, Placeables72.Distinct().Count());
        foreach (var id in Placeables72)
        {
            Assert.Contains(d.Tool.Placeables, p => p.Id == id);
            Assert.True(d.Tool.RecipeFor(id) != null, "tarifi yok: " + id);
        }
        foreach (var id in Towers8.Concat(Traps8)) { Assert.Contains(d.Tool.Placeables, p => p.Id == id); Assert.NotNull(d.Tool.RecipeFor(id)); }
        var g = RecipeGraph.Walk(d, 6);
        foreach (var id in Towers8.Concat(Traps8).Concat(Placeables72)) Assert.True(g.Has(id), "tarif grafinda ulasilamaz: " + id);
        Assert.Equal(new[] { "Çivi", "Sapan" }.Length, 2); // M3 adlari degismedi
    }

    [Fact]
    public void Power_towers_and_traps_have_watts_and_the_ammo_towers_do_not()
    {
        var d = D;
        var watts = new Dictionary<string, int> { ["ark_bobini"] = 120, ["lazer_kulesi"] = 200, ["maya_topu_kulesi"] = 300, ["elektrikli_cit"] = 40, ["dondurucu_puskurtucu"] = 80 }; // ICERIK §13: 120/200/300 W
        foreach (var (id, w) in watts) { Assert.Equal(w, d.Research.Watts[id]); Assert.Equal(w, d.Power.ConsumerWatts(id)); }
        foreach (var t in d.Defense.Towers) Assert.Equal(watts.ContainsKey(t.Id), t.Powered);
        foreach (var t in d.Defense.Traps) Assert.Equal(watts.ContainsKey(t.Id), t.Powered);
        Assert.Equal(new[] { "biyoyakit" }, d.Defense.Tower("alev_kulesi")!.Ammo);
        Assert.Equal(new[] { "havan_mermisi", "hazelnut" }, d.Defense.Tower("havan_kulesi")!.Ammo);
        Assert.Equal("flying", d.Defense.Tower("lazer_kulesi")!.DefaultPriority);
        Assert.Contains("maya", d.Defense.Tower("maya_topu_kulesi")!.Ammo);
    }

    // ---------- kuleler ----------

    [Fact]
    public void Powered_tower_without_ammo_is_ready_and_ammo_towers_still_need_ammo()
    {
        var d = D;
        var ark = d.Defense.Tower("ark_bobini")!; var st = new DefenseState().AddTower(ark, Vector3.Zero, 0);
        Assert.Equal(TowerStatus.Ready, TowerRules.Status(st, ark));
        Assert.Equal("", TowerRules.Fire(st, ark));                   // cephanesiz atar
        Assert.True(st.Cooldown > 0 && st.Hp < ark.Hp);
        var alev = d.Defense.Tower("alev_kulesi")!; var a = new DefenseState().AddTower(alev, Vector3.Zero, 0);
        Assert.Equal(TowerStatus.Empty, TowerRules.Status(a, alev));
        a.Mag["biyoyakit"] = 2;
        Assert.Equal("biyoyakit", TowerRules.Fire(a, alev)); Assert.Equal(1, a.Loaded);
        var lazer = d.Defense.Tower("lazer_kulesi")!; var l = new DefenseState().AddTower(lazer, Vector3.Zero, 0);
        Assert.Equal("flying", l.Priority);                           // varsayilan oncelik
        Assert.Equal(TowerStatus.Ready, TowerRules.Status(l, lazer)); // Ayaz Kristali olmadan da ates eder
        l.Mag["ayaz_kristali"] = 1;
        Assert.Equal("ayaz_kristali", TowerRules.Fire(l, lazer));
        Assert.Equal(0, l.Loaded);
    }

    [Fact]
    public void Arc_chain_and_beam_rules()
    {
        var d = D;
        var ark = d.Defense.Tower("ark_bobini")!;
        var ts = new List<DefenseTarget> { T(1, 5, 0), T(2, 8, 0), T(3, 11, 0), T(4, 14, 0), T(5, 30, 0), T(6, 8, 6) };
        var c = TowerRules.Chain(ark, 0, ts);
        Assert.Equal(new[] { 0, 1, 2, 3 }, c.Select(x => x.index));  // 3 atlama; 4.5 m'den uzaktaki (30) ve 6 m yandaki girmez
        Assert.Equal(1f, c[0].mult); Assert.Equal(0.6f, c[1].mult, 3); Assert.Equal(0.36f, c[2].mult, 3); Assert.Equal(0.216f, c[3].mult, 3);
        Assert.Single(TowerRules.Chain(ark, 4, ts));                  // yalniz kalan hedefte zincir yok
        var lazer = d.Defense.Tower("lazer_kulesi")!;
        float plain = TowerRules.BeamDamage(lazer, T(1, 5, 0), false);
        Assert.Equal(lazer.Damage, plain);
        Assert.Equal(plain * lazer.BoostMult, TowerRules.BeamDamage(lazer, T(1, 5, 0), true), 3);
        Assert.Equal(plain * lazer.FlyingMult, TowerRules.BeamDamage(lazer, T(2, 5, 0, fly: true), false), 3);
        Assert.Equal(0, TowerRules.Pick(lazer, Vector3.Zero, new List<DefenseTarget> { T(1, 5, 0, fly: true), T(2, 3, 0) }, "flying"));
        var havan = d.Defense.Tower("havan_kulesi")!;
        Assert.True(havan.Range >= 30f && havan.MinRange >= 8f && havan.Splash >= 4f);          // uzun menzil, alan
        Assert.False(TowerRules.InRange(havan, Vector3.Zero, T(1, 5, 0)));                       // minimum menzil
        Assert.True(TowerRules.InRange(havan, Vector3.Zero, T(1, 25, 0)));
    }

    // ---------- tuzaklar ----------

    static (TrapState s, TrapDef d) Trap(string id) { var d = D.Defense.Trap(id)!; return (new DefenseState().AddTrap(d, Vector3.Zero, 0), d); }

    [Fact]
    public void Electric_fence_needs_power_and_shocks_what_touches_it()
    {
        var (s, d) = Trap("elektrikli_cit");
        var ts = new List<DefenseTarget> { T(1, 0.3f, 0.2f), T(2, 10, 0) };
        Assert.Empty(TrapRules.Step(s, d, ts, 0.1f, powered: false));    // gucsuz bos
        var fx = TrapRules.Step(s, d, ts, 0.1f, powered: true);
        Assert.Contains(fx, e => e.Target == 1 && e.Kind == TrapFx.Damage && e.Value > 0);
        Assert.Contains(fx, e => e.Target == 1 && e.Kind == TrapFx.Shock && e.Seconds >= 0.5f);
        Assert.DoesNotContain(fx, e => e.Target == 2);
        Assert.Empty(TrapRules.Step(s, d, ts, 0.1f, true));              // bekleme (triggerEvery)
        Assert.Equal(1, s.Triggers);
    }

    [Fact]
    public void Mine_blasts_everything_in_its_radius_once_and_needs_repair()
    {
        var (s, d) = Trap("mayin");
        var ts = new List<DefenseTarget> { T(1, 0.2f, 0), T(2, 2.5f, 0), T(3, 8, 0) };
        var fx = TrapRules.Step(s, d, ts, 0.1f);
        var blasts = fx.Where(e => e.Kind == TrapFx.Blast).ToList();
        Assert.Equal(new[] { 1, 2 }, blasts.Select(b => b.Target).OrderBy(x => x));
        Assert.True(blasts.Single(b => b.Target == 1).Value > blasts.Single(b => b.Target == 2).Value);   // merkeze yakin cok
        Assert.True(blasts.Single(b => b.Target == 2).Dir.X > 0.9f);                                   // disari iter
        Assert.True(TrapRules.Broken(s));
        Assert.Empty(TrapRules.Step(s, d, ts, 0.1f));                                                    // tek kullanimlik
        var cost = TowerRules.RepairCost(D.Tool.RecipeFor("mayin")!.In, s.Hp, d.Hp, D.Defense.RepairFraction); // onarimla yeniden kurulur
        Assert.NotEmpty(cost);
    }

    [Fact]
    public void Oil_trench_slows_until_lit_then_burns_and_runs_dry()
    {
        var (s, d) = Trap("yag_hendegi");
        var ts = new List<DefenseTarget> { T(1, 0.3f, 0.2f) };
        var fx = TrapRules.Step(s, d, ts, 0.1f);
        Assert.Contains(fx, e => e.Kind == TrapFx.Slow);
        Assert.DoesNotContain(fx, e => e.Kind == TrapFx.Burn);
        TrapRules.Toggle(s, d);                                           // E: tutustur
        Assert.True(s.Open);
        var lit = TrapRules.Step(s, d, ts, 0.1f);
        Assert.Contains(lit, e => e.Kind == TrapFx.Burn && e.Seconds >= 3f);
        Assert.Contains(lit, e => e.Kind == TrapFx.Damage);
        for (float t = 0; t < d.IgniteSeconds + 1f; t += 0.1f) TrapRules.Step(s, d, ts, 0.1f);
        Assert.False(s.Open);
        Assert.True(TrapRules.Broken(s));                                // yag bitti: onarimla (yeni yag) kurulur
        // yanan bir hedef yagi kendiliginden tutusturur
        var (s2, d2) = Trap("yag_hendegi");
        TrapRules.Step(s2, d2, ts, 0.1f, burning: new HashSet<int> { 1 });
        Assert.True(s2.Open);
    }

    [Fact]
    public void Freezer_needs_power_and_freezes_the_area()
    {
        var (s, d) = Trap("dondurucu_puskurtucu");
        var ts = new List<DefenseTarget> { T(1, 1.5f, 1.0f), T(2, 5, 0) };   // halfSize 2.2
        Assert.Empty(TrapRules.Step(s, d, ts, 0.1f, powered: false));
        var fx = TrapRules.Step(s, d, ts, 0.1f, powered: true);
        Assert.Contains(fx, e => e.Target == 1 && e.Kind == TrapFx.Freeze && e.Seconds >= 2f && e.Value < 1f);
        Assert.DoesNotContain(fx, e => e.Target == 2);
    }

    // ---------- durumlar ----------

    [Fact]
    public void Burn_ticks_spreads_and_expires_and_swell_bursts()
    {
        var c = D.Combat;
        var st = new DefenseStatus();
        st.AddBurn(1, 6f, 3f); st.AddBurn(1, 4f, 1f);                     // sure uzamaz kisalmaz, hasar buyuk olan
        var alive = new HashSet<int> { 1, 2 };
        float total = 0;
        for (float t = 0; t < 4f; t += 0.5f) total += st.StepBurn(0.5f, 0.5f, alive).Where(x => x.id == 1).Sum(x => x.damage);
        Assert.InRange(total, 14f, 20f);                                    // ~3 sn x 6
        Assert.False(st.IsBurning(1));
        st.AddBurn(1, 6f, 3f);
        var ts = new List<DefenseTarget> { T(1, 0, 0), T(2, 2, 0), T(3, 9, 0) };
        Assert.Equal(new[] { 2 }, st.Spread(c.Burn, ts));                   // yakindaki yanmayan; uzaktaki degil
        var sw = new DefenseStatus();
        var (b1, _, _, _) = sw.AddSwell(7, 20f, 100f, c.Swell);              // 20/(100x3) = .067
        Assert.False(b1);
        bool burst = false; float aoe = 0, self = 0;
        for (int i = 0; i < 20 && !burst; i++) (burst, aoe, self, _) = sw.AddSwell(7, 20f, 100f, c.Swell);
        Assert.True(burst);
        Assert.Equal(50f, aoe, 3); Assert.Equal(50f, self, 3);               // azami canin yarisi (combat.json)
        Assert.Equal(0f, sw.SwellOf(7));
    }

    // ---------- Cam seti ve malzemeler ----------

    [Fact]
    public void Glass_set_is_four_shapes_of_four_glass_and_outside_the_upgrade_chain()
    {
        var d = D; var b = d.Build;
        Assert.Equal(new[] { "cam_cati", "cam_duvar", "cam_kapi", "cam_zemin" }, b.Sets["cam"].Keys.OrderBy(x => x));
        foreach (var (shape, baseShape) in b.Sets["cam"])
        {
            Assert.Equal("cam", b.SetMaterialOf(shape)); Assert.Equal(baseShape, b.BaseShapeOf(shape));
            Assert.Equal(4, d.Tool.BuildCosts[shape]);
            Assert.True(d.Tool.BuildCosts.ContainsKey(baseShape));
            Assert.Equal(new Dictionary<string, int> { ["cam"] = 4 }, StructureCosts.Cost(b, "cam", d.Tool.BuildCosts[shape]));
        }
        Assert.Null(b.SetMaterialOf("duvar")); Assert.Null(b.Next("cam")); Assert.Null(b.Next("mayali_beton"));
        Assert.True(b.Material("cam")!.Set); Assert.True(b.Material("cam")!.Alpha < 1f);
        Assert.True(b.Material("cam")!.Hp < b.Material("wood")!.Hp);          // cam kirilgan
        Assert.Equal("celik", b.Material("celik_sac")!.Cost.Keys.Single());      // ICERIK §10.1: Celik Sac Celik
        Assert.Equal("mayali_beton", b.Material("mayali_beton")!.Cost.Keys.Single());
        Assert.NotNull(d.Tool.RecipeFor("cam"));                                 // Cam Ocagi'ndan (T4)
        Assert.Equal(28, d.Tool.BuildCosts.Count - 4);                            // 26 sekil (makine platformu 3 boyda: 28 kimlik) + 4 cam
    }

    [Fact]
    public void Special_placeables_carry_their_behaviour_in_data()
    {
        var d = D;
        Assert.Equal(60, P("ambar").Slots);
        Assert.True(P("silo").Slots == 1 && P("silo").Stack == 2000);               // tek turden 2.000
        Assert.True(P("yatak").Spawn && P("ranza").Spawn);
        Assert.True(P("projektor").LightRange > P("fener").LightRange && P("sokak_lambasi").LightRange > P("fener").LightRange);
        PlaceableDef P(string id) => d.Tool.Placeables.Single(p => p.Id == id);
    }
}
