using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Kusam M4 (ICERIK 6.2 #6-#12, 7, 16): yedi silah basligi, yedi guc ve iki ulti, zirh takimlari, moduller, Capa Mk.</summary>
public class KusamTests
{
    static GameData Data() => TestData.Load();
    static readonly string[] Heads7 = { "alev_puskurtecu", "havan", "ark", "tohum_makinalisi", "ayaz_lazeri", "yercekimi", "maya_topu" };

    [Fact]
    public void Seven_weapon_heads_have_data_types_and_recipes()
    {
        var d = Data();
        Assert.Equal(Heads7, d.Tool.WeaponHeads.Select(h => h.Id));
        var chips = d.Chips.WeaponHeads;
        foreach (var id in Heads7) Assert.Contains(id, chips); // chips.json weaponHeads
        foreach (var h in d.Tool.WeaponHeads)
        {
            Assert.True(h.Damage > 0 && h.Range > 0, h.Id);
            Assert.Equal(h.DamageType, d.Combat.WeaponTypes[h.Id]); // combat.json ile ayni tur
            Assert.Contains(d.Tool.Recipes, r => r.Id == h.Id && r.Category == "alet");
        }
        Assert.Equal(new[] { "cone", "mortar", "chain", "seed", "beam", "gravity", "orb" }, d.Tool.WeaponHeads.Select(h => h.Kind));
        var g = RecipeGraph.Walk(d, 6);
        foreach (var id in Heads7) Assert.True(g.Has(id), "ulasilamayan baslik tarifi: " + id);
    }

    [Fact]
    public void Cone_mortar_chain_and_seed_rules()
    {
        var d = Data();
        var alev = d.Tool.Head4("alev_puskurtecu")!;
        Assert.True(HeadRules.InCone(0, 0, 0, 1, alev.ConeDegrees, alev.Range, 0, 5));
        Assert.False(HeadRules.InCone(0, 0, 0, 1, alev.ConeDegrees, alev.Range, 5, 5));   // koni disi (45 derece)
        Assert.False(HeadRules.InCone(0, 0, 0, 1, alev.ConeDegrees, alev.Range, 0, 9));   // menzil disi
        Assert.False(HeadRules.InCone(0, 0, 0, 1, alev.ConeDegrees, alev.Range, 0, -3));  // arkada
        var havan = d.Tool.Head4("havan")!;
        Assert.Equal(havan.MinRange, HeadRules.MortarDistance(havan, 0f), 3);
        Assert.Equal(havan.Range, HeadRules.MortarDistance(havan, 5f), 3);
        Assert.Equal((havan.MinRange + havan.Range) / 2, HeadRules.MortarDistance(havan, havan.ChargeSeconds / 2), 3);
        Assert.True(HeadRules.MortarFlight(havan, 44f) > HeadRules.MortarFlight(havan, 11f));
        var ark = d.Tool.Head4("ark")!;
        var order = HeadRules.ChainOrder(0, 0, new (float, float)[] { (0, 5), (0, 40), (3, 9), (6, 12), (0, 6.5f) }, ark.ChainCount, ark.ChainRange);
        Assert.Equal(new[] { 0, 4, 2, 3 }, order);                  // 40 m'deki hic secilmez
        Assert.Equal(ark.Damage * 0.8f * 0.8f, HeadRules.ChainDamage(ark, 2), 3);
        var seed = d.Tool.Head4("tohum_makinalisi")!;
        Assert.True(HeadRules.SeedEffect(seed, "chili").Burn > 0);                 // biber yakar
        Assert.True(HeadRules.SeedEffect(seed, "prickly_pear").Pierce > 0);        // kaktus deler
        Assert.True(HeadRules.SeedEffect(seed, "garlic").Slow > 0);                // sarimsak yavaslatir
        Assert.Equal(1f, HeadRules.SeedEffect(seed, "turnip").DamageMult);
        Assert.Equal("chili_seed", HeadRules.AmmoItem(d, seed, "chili"));
        Assert.Equal("biyoyakit", HeadRules.AmmoItem(d, d.Tool.Head4("alev_puskurtecu")!, "turnip"));
        var maya = d.Tool.Head4("maya_topu")!;
        Assert.False(HeadRules.SwellDone(maya, 0.5f)); Assert.True(HeadRules.SwellDone(maya, 1.0f));
    }

    [Fact]
    public void Twelve_powers_and_four_ultis_with_the_new_rules()
    {
        var d = Data();
        var f = d.Abilities;
        Assert.Equal(12, f.Abilities.Count(a => !a.IsUlti)); // PLAN 7.1: Guc 12
        Assert.Equal(4, f.Abilities.Count(a => a.IsUlti));   // Ulti 4
        foreach (var id in new[] { "zaman_bali", "cekim_kuyusu", "roket_ziplama", "alev_cemberi", "isinlan", "buz_duvari", "gorunmez_pelerin", "yorunge_sulamasi", "zaman_durdu" })
            Assert.NotNull(f.Def(id));
        Assert.Equal((40f, 25f), (f.Def("zaman_bali")!.Charge, f.Def("zaman_bali")!.Cooldown)); // ICERIK 7: Sarj . bekleme
        Assert.Equal((50f, 30f), (f.Def("isinlan")!.Charge, f.Def("isinlan")!.Cooldown));
        Assert.Equal(8f, f.Def("gorunmez_pelerin")!.Duration);
        // kullanim: pelerin, isinlan isareti, zaman durdu
        var p = new AbilityProgress();
        var l = p.Local; AbilityRules.Normalize(f, l);
        foreach (var id in new[] { "gorunmez_pelerin", "isinlan" }) AbilityRules.Unlock(f, p, id);
        Assert.NotNull(AbilityRules.Use(f, l, "gorunmez_pelerin"));
        Assert.Equal(8f, l.CloakLeft);
        l.Charge = 100;
        l.Slots[0] = "isinlan"; // aktif yuva 1 (Kurbaga Aga ikinciyi acar)
        Assert.NotNull(AbilityRules.Use(f, l, "isinlan"));
        Assert.Equal(4f, l.MarkLeft);
        AbilityRules.Tick(f, l, 3f);
        Assert.Equal((5f, 1f), (l.CloakLeft, l.MarkLeft));
        l.Ulti = "zaman_durdu"; l.Meter = f.UltiMeter.Max;
        Assert.NotNull(AbilityRules.Use(f, l, "zaman_durdu"));
        Assert.Equal(5f, l.TimeStopLeft);
        Assert.Equal(0f, l.Meter);
        // alan kurallari
        var alev = f.Def("alev_cemberi")!;
        Assert.True(AbilityRules.InRing(0, 0, alev.Inner, alev.Radius, 5, 0));
        Assert.False(AbilityRules.InRing(0, 0, alev.Inner, alev.Radius, 2, 0)); // ic bosluk
        Assert.False(AbilityRules.InRing(0, 0, alev.Inner, alev.Radius, 7, 0));
        var kuyu = f.Def("cekim_kuyusu")!;
        var (x, z) = AbilityRules.PullToward(kuyu, 0, 0, 10, 0, 0.5f);
        Assert.Equal(10f - kuyu.Pull * 0.5f, x, 3); Assert.Equal(0f, z, 3);
        Assert.Equal((0f, 0f), AbilityRules.PullToward(kuyu, 0, 0, 1, 0, 5f)); // merkezde durur
        var bal = f.Def("zaman_bali")!;
        Assert.Equal(0.3f, AbilityRules.SlowFactor(bal, 0, 0, 2, 0), 3);
        Assert.Equal(1f, AbilityRules.SlowFactor(bal, 0, 0, 9, 0));
        var (a, b) = AbilityRules.WallEnds(f.Def("buz_duvari")!, 0, 0, 0, 1);
        Assert.Equal(8f, MathF.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)), 3);
        Assert.Equal(4f, a.z, 3); // onunde 4 m
    }

    [Fact]
    public void Seven_armor_sets_18_modules_and_capa_mk_follow_icerik()
    {
        var d = Data();
        var f = d.Armor;
        Assert.Equal(7, f.Sets.Count);                                    // PLAN 7.1: zirh takimi 7
        Assert.Equal(new float[] { 0, 15, 35, 60, 90, 125, 170 }, f.Sets.Select(s => s.Armor));
        Assert.Equal(new[] { 3, 3, 4, 4, 5, 5, 6 }, f.Sets.Select(s => s.ModuleSlots));
        Assert.Equal(18, f.Modules.Count);                                // kusam modulu 18
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 4 }, f.CapaMk.Select(m => m.ChipSlots));
        Assert.Contains(f.Modules, m => m.Id == "filtre_maskesi");
        // cantada giyilen: en yuksek zirh, o takimin yuvasi kadar modul
        var bag = new Dictionary<string, int> { ["deri_yelek"] = 1, ["celik_zirh"] = 1, ["filtre_maskesi"] = 1, ["serin_yelek"] = 1, ["genis_canta"] = 1, ["far"] = 1, ["hayvan_dudugu"] = 1, ["planor"] = 1 };
        int Count(string id) => bag.GetValueOrDefault(id);
        Assert.Equal("celik_zirh", ArmorRules.Worn(f, Count)!.Id);
        Assert.Equal(60f, ArmorRules.ArmorPoints(f, Count));
        Assert.Equal(4, ArmorRules.ActiveModules(f, Count).Count);        // 6 modul var, 4 yuva
        Assert.Equal(6f, ArmorRules.Effect(f, Count, "backpackSlots"));
        Assert.Equal(0f, ArmorRules.Effect(f, new Dictionary<string, int>().GetValueOrDefault, "shield"));
        Assert.Equal(1, ArmorRules.ChipSlots(f, Count));
        bag["capa_mk5"] = 1; bag["capa_mk3"] = 1;
        Assert.Equal(5, ArmorRules.Mk(f, Count)!.Mk);
        Assert.Equal(3, ArmorRules.ChipSlots(f, Count));
        // hasar: zirh puani GDD 16.1; kalkan jeneratoru once tuketilir
        var (dmg, sh) = ArmorRules.Absorb(100f, 100f, 20f);
        Assert.Equal(30f, dmg, 3); Assert.Equal(0f, sh, 3);               // 100 -> %50 -> 50, kalkan 20 yer
        Assert.Equal((100f, 0f), ArmorRules.Absorb(100f, 0f, 0f));
        // her uretilebilir takim/modul/Mk tarifi var ve T6'da ulasilir
        var g = RecipeGraph.Walk(d, 6);
        foreach (var id in f.Sets.Where(s => s.Cost.Count > 0).Select(s => s.Id).Concat(f.Modules.Select(m => m.Id)).Concat(f.CapaMk.Where(c => c.Mk > 1).Select(c => c.Id)))
            Assert.True(g.Has(id), "ulasilamayan: " + id);
        foreach (var s in f.Sets.Where(s => s.Cost.Count > 0)) foreach (var (k, n) in s.Cost) Assert.Equal(n, d.Tool.Recipes.Single(r => r.Id == s.Id).In[k]);
    }
}