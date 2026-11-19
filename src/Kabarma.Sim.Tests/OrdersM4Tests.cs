using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M4 ilerleme4: ICERIK 18 kalan 24 is emri zinciri (#13-24, #37-48): hedefler, olcu kipleri, oduller, basarimlar, baglanan olculer.</summary>
public class OrdersM4Tests
{
    static GameData Data() => TestData.Load();

    // ICERIK 18 tablosu: zincir -> (grup, 5 hedef)
    static readonly (string id, string group, int[] targets)[] Table =
    {
        ("usta", "Yapı ve mühendislik", new[] { 20, 100, 500, 2000, 8000 }),
        ("elektrikci", "Yapı ve mühendislik", new[] { 100, 500, 2000, 10000, 50000 }),
        ("otomasyoncu", "Yapı ve mühendislik", new[] { 50, 500, 5000, 50000, 500000 }),
        ("kalfa", "Yapı ve mühendislik", new[] { 5, 12, 20, 30, 43 }),
        ("mantikci", "Yapı ve mühendislik", new[] { 1, 5, 20, 50, 150 }),
        ("mimar", "Yapı ve mühendislik", new[] { 4, 8, 12, 16, 20 }),
        ("gozcu", "Keşif", new[] { 1, 4, 8, 13, 18 }),
        ("kasif", "Keşif", new[] { 10, 25, 50, 75, 100 }),
        ("magaraci", "Keşif", new[] { 1, 4, 8, 12, 15 }),
        ("ansiklopedist", "Keşif", new[] { 20, 60, 120, 200, 300 }),
        ("kadim_yol", "Keşif", new[] { 1, 3, 6, 9, 12 }),
        ("enkazci", "Keşif", new[] { 3, 10, 25, 60, 150 }),
        ("tuccar", "Ticaret", new[] { 1000, 10000, 100000, 1000000, 10000000 }),
        ("borsaci", "Ticaret", new[] { 1, 5, 20, 60, 200 }),
        ("cesitci", "Ticaret", new[] { 10, 25, 50, 80, 120 }),
        ("carkci", "Ticaret", new[] { 1, 10, 50, 150, 500 }),
        ("gurme", "Ticaret", new[] { 5, 12, 20, 26, 32 }),
        ("koleksiyoncu", "Ticaret", new[] { 5, 20, 50, 90, 120 }),
        ("can_dostu", "Ekip ve şamata", new[] { 1, 10, 50, 150, 500 }),
        ("ucan_uye", "Ekip ve şamata", new[] { 5, 25, 100, 300, 1000 }),
        ("ezilenler_kulubu", "Ekip ve şamata", new[] { 5, 25, 100, 300, 1000 }),
        ("nakliyeci", "Ekip ve şamata", new[] { 1000, 10000, 100000, 1000000, 10000000 }), // kg (ton x 1000)
        ("sofor", "Ekip ve şamata", new[] { 5000, 50000, 250000, 1000000, 5000000 }),     // metre
        ("fotografci", "Ekip ve şamata", new[] { 1, 5, 15, 30, 60 }),
    };

    // Zincir -> beklenen odul (ICERIK 18 "Zincir odulu" sutunu): (adim, tur, kimlik)
    static readonly Dictionary<string, (int step, string kind, string id)[]> Grants = new()
    {
        ["usta"] = new[] { (5, "item", "plan_defteri_yuva") },
        ["elektrikci"] = new[] { (5, "card", "yedek_aku") },
        ["otomasyoncu"] = new[] { (4, "member", "robot_kahya"), (5, "cosmetic", "altin_drone") },
        ["kalfa"] = new[] { (3, "member", "makinist"), (5, "cosmetic", "kaplama_krom") },
        ["mantikci"] = new[] { (5, "card", "tamirci") },
        ["mimar"] = new[] { (5, "item", "ata_heykeli") },
        ["gozcu"] = new[] { (5, "item", "yonlu_parasut") },
        ["kasif"] = new[] { (5, "card", "miknatis") },
        ["magaraci"] = new[] { (5, "card", "ay_parcasi") },
        ["ansiklopedist"] = new[] { (5, "cosmetic", "vizor_gozluklu") },
        ["kadim_yol"] = new[] { (5, "card", "sadik_golge") },
        ["enkazci"] = new[] { (5, "card", "kooperatif_uydusu") },
        ["tuccar"] = new[] { (3, "member", "muhtar"), (5, "cosmetic", "tulum_altin") },
        ["borsaci"] = new[] { (5, "card", "acgozlu") },
        ["cesitci"] = new[] { (5, "card", "borc") },
        ["carkci"] = new[] { (5, "cosmetic", "altin_cark") },
        ["gurme"] = new[] { (2, "member", "asci"), (5, "cosmetic", "canta_caydanlik") },
        ["koleksiyoncu"] = new[] { (5, "cosmetic", "tulum_aurora") },
        ["can_dostu"] = new[] { (5, "card", "ikinci_nefes") },
        ["ucan_uye"] = new[] { (2, "member", "kacak"), (5, "cosmetic", "planor_deseni") },
        ["ezilenler_kulubu"] = new[] { (5, "card", "dikenli"), (5, "cosmetic", "kask_yumurta_kabugu") },
        ["nakliyeci"] = new[] { (3, "member", "pehlivan"), (5, "card", "tokmak") },
        ["sofor"] = new[] { (5, "card", "minik_bicer") },
        ["fotografci"] = new[] { (5, "cosmetic", "altin_cerceve") },
    };

    static readonly string[] Ids = Table.Select(t => t.id).ToArray();

    public static IEnumerable<object[]> ChainIds() => Ids.Select(i => new object[] { i });

    [Fact]
    public void Twenty_four_new_chains_follow_icerik_18_table()
    {
        var o = Data().Orders;
        Assert.Equal(24, Table.Length);
        foreach (var (id, group, targets) in Table)
        {
            var c = o.Chains.SingleOrDefault(x => x.Id == id);
            Assert.True(c != null, id + " zinciri yok");
            Assert.Equal(5, c!.Steps.Count);
            Assert.Equal(targets, c.Steps.Select(s => s.Target));
            Assert.Equal(group, c.Group);
            Assert.Equal("defter", c.OpensAt);
            Assert.Single(c.Steps.Select(x => x.Measure).Distinct()); // bir zincir tek olcu
            Assert.All(c.Steps, s => Assert.True(o.Measures.ContainsKey(s.Measure), id + ": " + s.Measure + " measures'ta yok"));
        }
        Assert.Equal(o.Chains.Count, o.Chains.Select(c => c.Id).Distinct().Count());
        // Hedefler artar
        Assert.All(o.Chains.Where(c => Ids.Contains(c.Id)), c => Assert.True(c.Steps.Zip(c.Steps.Skip(1), (a, b) => a.Target < b.Target).All(x => x), c.Id));
    }

    [Fact]
    public void Chain_grants_match_the_reward_column_and_cards_exist()
    {
        var d = Data();
        foreach (var (id, expected) in Grants)
        {
            var c = d.Orders.Chains.Single(x => x.Id == id);
            Assert.Equal(expected.Select(g => (g.step, g.kind, g.id)), c.Grants.Select(g => (g.Step, g.Kind, g.Id)));
            Assert.All(c.Grants, g => { if (g.Kind == "card") Assert.NotNull(d.Cards.Cards.FirstOrDefault(k => k.Id == g.Id)); });
            Assert.Contains(c.Grants, g => g.Step == 5);
        }
        // Uye ve kozmetik kimlikleri kimlikler-m4.md 9. bolumden (dolap akisi ayni kimlikleri yazar)
        var members = new HashSet<string> { "robot_kahya", "makinist", "muhtar", "asci", "kacak", "pehlivan" };
        Assert.Equal(members, d.Orders.Chains.SelectMany(c => c.Grants).Where(g => g.Kind == "member" && Ids.Contains(d.Orders.Chains.First(c => c.Grants.Contains(g)).Id)).Select(g => g.Id).ToHashSet());
    }

    [Theory]
    [MemberData(nameof(ChainIds))]
    public void Every_chain_completes_by_its_measure_mode_and_grants_its_rewards(string id)
    {
        var s = new GameState(Data());
        s.Orders.OpenDefter();
        var def = s.Orders.Chain(id)!.Def;
        Assert.False(s.Orders.Chain(id)!.Done);
        var granted = new List<(int step, string key)>();
        s.Orders.StepDone += (c, i) => { if (c.Def.Id == id) granted.Add((i, "")); };
        for (int i = 0; i < 5; i++)
        {
            var st = def.Steps[i];
            Feed(s, st.Measure, st.Target);
            Assert.True(s.Orders.Chain(id)!.StepDone(i), $"{id} adim {i + 1} ({st.Measure} {st.Target}) bitmedi");
            // Adim i+1'e kadar olan oduller acildi, sonrakiler henuz degil
            foreach (var g in def.Grants)
                Assert.Equal(g.Step <= i + 1, s.Orders.Granted.Contains($"{g.Kind}:{g.Id}"));
        }
        Assert.True(s.Orders.Chain(id)!.Done);
        Assert.Equal(5, granted.Count);
        Assert.Equal(new[] { 100 + 400 + 1500 + 6000 + 25000 }, new[] { s.Orders.CreditsPaid });
        Assert.Equal(10 + 20 + 40 + 70 + 100, s.Orders.VeriPaid);
        Assert.True(s.Achievements.IsUnlocked("chain_" + id));
    }

    static void Feed(GameState s, string measure, int target)
    {
        var mode = s.Data.Orders.Measures[measure];
        if (mode == "peak") s.Orders.Level(measure, target);
        else if (mode == "distinct") { int have = s.Orders.StatKeys.TryGetValue(measure, out var k) ? k.Count : 0; for (int i = have; i < target; i++) s.Orders.ReportKey(measure, measure + "#" + i); }
        else { int have = s.Orders.Stats.GetValueOrDefault(measure); if (target > have) s.Orders.Report(measure, target - have); }
    }

    [Fact]
    public void Distinct_measures_ignore_repeats_and_peak_measures_ignore_drops()
    {
        var s = new GameState(Data());
        s.Orders.OpenDefter();
        for (int i = 0; i < 4; i++) { s.Orders.ReportKey("machine_kind", "ocak"); s.Orders.ReportKey("dish_kind", "corba"); }
        Assert.Equal(1, s.Orders.Chain("kalfa")!.Count("machine_kind"));
        Assert.Equal(1, s.Orders.Chain("gurme")!.Count("dish_kind"));
        s.Orders.Level("comfort", 9);
        s.Orders.Level("comfort", 3);
        Assert.Equal(9, s.Orders.Chain("mimar")!.Count("comfort"));
        Assert.True(s.Orders.Chain("mimar")!.StepDone(1)); // 4 ve 8
        s.Orders.Level("map_pct", 30);
        Assert.True(s.Orders.Chain("kasif")!.StepDone(0) && s.Orders.Chain("kasif")!.StepDone(1) && !s.Orders.Chain("kasif")!.StepDone(2));
    }

    [Fact]
    public void Chains_stay_closed_until_the_defter_opens_and_peaks_carry_over()
    {
        var s = new GameState(Data());
        s.Orders.Level("power_w", 600);
        Assert.Equal(0, s.Orders.Chain("elektrikci")!.Count("power_w"));
        s.Orders.OpenDefter();
        Assert.Equal(600, s.Orders.Chain("elektrikci")!.Count("power_w"));
        Assert.True(s.Orders.Chain("elektrikci")!.StepDone(1)); // 100 ve 500
        // Odul Defter acilinca toplu odendi
        Assert.Equal(100 + 400, s.Orders.CreditsPaid);
    }

    [Fact]
    public void Achievements_cover_every_new_chain()
    {
        var s = new GameState(Data());
        foreach (var id in Ids) Assert.Contains(s.Achievements.All, a => a.Id == "chain_" + id && a.IsChain);
        Assert.Equal(36 + 2, s.Achievements.Count);
        Assert.Equal(50, s.Achievements.Total);
    }

    [Fact]
    public void Wheel_spin_reports_to_the_carkci_chain()
    {
        var s = new GameState(Data());
        s.Orders.OpenDefter();
        Assert.NotNull(s.SpinWheel()); // gunun ilk cevirmesi bedava
        Assert.Equal(1, s.Orders.Chain("carkci")!.Count("wheel_spin"));
        Assert.True(s.Orders.Chain("carkci")!.StepDone(0));
    }
}
