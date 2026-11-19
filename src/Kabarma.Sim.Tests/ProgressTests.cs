using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Is emri zincirleri (ilk 12), Ustalik ve basarimlar (GDD 10, 13; ICERIK 18, 19).</summary>
public class ProgressTests
{
    static GameData Data() => TestData.Load();
    static GameState New() => new(Data());

    // ---------- Veri ----------

    [Fact]
    public void First_twelve_chains_follow_icerik_18()
    {
        var o = Data().Orders;
        Assert.Equal(36, o.Chains.Count); // 12 (M2) + 24 (M4 ilerleme4: #13-24, #37-48); #25-36 M3 payi
        Assert.Equal(new[] { "ciraklik", "salgam_baronu", "yesil_parmak", "islahci", "alaca_bahce", "devler_ligi", "coban", "suru_sahibi", "mandira", "cins_hayvan", "suvari", "savas_agili" },
            o.Chains.Take(12).Select(c => c.Id));
        Assert.All(o.Chains, c => Assert.Equal(5, c.Steps.Count));
        Assert.All(o.Chains.SelectMany(c => c.Steps), s => Assert.True(o.Measures.ContainsKey(s.Measure), s.Measure + " olcusu measures'ta yok"));
        Assert.Equal(new[] { 4, 8, 12, 18, 24 }, o.Chains.Single(c => c.Id == "yesil_parmak").Steps.Select(s => s.Target));
        Assert.Equal(new[] { 4, 10, 20, 32, 48 }, o.Chains.Single(c => c.Id == "suru_sahibi").Steps.Select(s => s.Target));
        Assert.Equal(new[] { 20, 100, 500, 2000, 8000 }, o.Chains.Single(c => c.Id == "mandira").Steps.Select(s => s.Target));
        Assert.Equal(new[] { 10, 100, 1000, 5000, 20000 }, o.Chains.Single(c => c.Id == "savas_agili").Steps.Select(s => s.Target));
        Assert.Equal(new[] { 1000, 10000, 50000, 200000, 1000000 }, o.Chains.Single(c => c.Id == "suvari").Steps.Select(s => s.Target)); // metre
        // Cogu zincirin odulu son adimda; Coban uyesi 2. adimda
        Assert.Equal(2, o.Chains.Single(c => c.Id == "coban").Grants.Single(g => g.Kind == "member").Step);
        Assert.All(o.Chains, c => Assert.Contains(c.Grants, g => g.Step == 5));
    }

    [Fact]
    public void Mastery_data_has_eighty_perks_in_eight_branches_with_t1_t2_live()
    {
        var m = Data().Mastery;
        Assert.Equal(8, m.Branches.Count);
        Assert.Equal(80, m.Perks.Count);
        Assert.All(m.Branches, b => Assert.Equal(10, m.Perks.Count(p => p.Branch == b.Id)));
        Assert.All(m.Perks.GroupBy(p => (p.Branch, p.Tier)), g => Assert.Equal(new[] { "A", "B" }, g.Select(p => p.Slot).OrderBy(x => x)));
        Assert.Equal(m.Perks.Count, m.Perks.Select(p => p.Id).Distinct().Count());
        Assert.All(m.Perks.Where(p => p.Tier <= 20), p => Assert.True(p.Live && p.Effects.Count > 0, p.Id));
        Assert.All(m.Perks.Where(p => p.Tier > 20), p => Assert.False(p.Live, p.Id));
        Assert.All(m.Xp.Values, x => Assert.Contains(m.Branches, b => b.Id == x.Branch));
        Assert.Equal("Çift Damla", m.Perks.Single(p => p.Branch == "kazi" && p.Tier == 10 && p.Slot == "A").Name);
    }

    // ---------- Is emirleri ----------

    [Fact]
    public void Sum_distinct_and_peak_measures_count_per_their_mode()
    {
        var s = New();
        s.Orders.OpenDefter();
        s.Orders.ReportKey("crop_kind", "turnip");
        s.Orders.ReportKey("crop_kind", "turnip");   // ayni anahtar iki kez sayilmaz
        Assert.Equal(1, s.Orders.Chain("yesil_parmak")!.Count("crop_kind"));
        for (int i = 0; i < 3; i++) s.Orders.ReportKey("crop_kind", "k" + i);
        Assert.True(s.Orders.Chain("yesil_parmak")!.StepDone(0));        // 4 farkli ekin

        s.Orders.Level("herd_size", 3);
        Assert.False(s.Orders.Chain("suru_sahibi")!.StepDone(0));
        s.Orders.Level("herd_size", 11);
        s.Orders.Level("herd_size", 2);                                    // dusus tepeyi dusurmez
        var herd = s.Orders.Chain("suru_sahibi")!;
        Assert.Equal(11, herd.Count("herd_size"));
        Assert.True(herd.StepDone(0) && herd.StepDone(1) && !herd.StepDone(2)); // 4 ve 10 gecildi (atlayarak)
        Assert.Equal(2, herd.CurrentStep);

        s.Orders.Report("animal_product", 25);
        Assert.True(s.Orders.Chain("mandira")!.StepDone(0));
    }

    [Fact]
    public void Peak_measures_start_from_the_peak_reached_before_the_defter_opens()
    {
        var s = New();
        s.Orders.Level("herd_size", 5);
        Assert.Equal(0, s.Orders.Chain("suru_sahibi")!.Count("herd_size")); // zincir henuz kapali
        s.Orders.OpenDefter();
        Assert.Equal(5, s.Orders.Chain("suru_sahibi")!.Count("herd_size"));
        Assert.True(s.Orders.Chain("suru_sahibi")!.StepDone(0));
    }

    [Fact]
    public void Boy_chain_counts_m_or_bigger_and_keeps_a_separate_xl_count()
    {
        var s = New();
        s.Orders.OpenDefter();
        var d = s.Data.Crops;
        s.Backpack.Add("turnip_seed", 12);
        s.Farm.Till(0, 0); s.Farm.Plant(0, 0, "turnip", s.Backpack);
        var p = s.Farm.Get(0, 0)!;
        s.Farm.Water(0, 0);
        for (int k = 0; k < 2000 && s.Farm.MatureCount == 0; k++) s.Farm.Tick(1f, true);
        s.Farm.SwellMature(1, 100f); // yuksek puan: XL
        Assert.Equal("XL", p.Swollen!.Id);
        var b = s.Orders.Chain("devler_ligi")!;
        Assert.True(b.StepDone(0) && b.StepDone(1) && b.StepDone(2)); // ilk M, L, XL birden
        Assert.Equal(1, b.Count("swollen_xl"));
        Assert.Equal(1, s.Orders.Chain("yesil_parmak")!.Count("crop_kind"));
    }

    [Fact]
    public void Rewards_pay_credits_and_veri_when_the_defter_is_open_and_not_through_sales()
    {
        var s = New();
        s.OrderPaidCheck(out var paid);
        s.Orders.OpenDefter();
        s.Orders.Report("swollen_crop", 5);            // Salgam Baronu adim 1
        Assert.Equal((100, 10), (s.Orders.CreditsPaid, s.Research.Veri));
        Assert.Equal(100, s.Credits);
        Assert.Empty(s.Sales);                          // Hasat Raporu'nu ve Sold'u etkilemez
        Assert.Equal(0, s.Orders.Chain("ciraklik")!.Count("cargo_sold"));
        s.Orders.Report("swollen_crop", 100);          // adim 2 (400) ve 3 (1500) birden
        Assert.Equal(100 + 400 + 1500, s.Credits);
        Assert.Equal(10 + 20 + 40, s.Research.Veri);
        Assert.Equal(3, paid.Count);
    }

    [Fact]
    public void Steps_done_before_the_defter_are_paid_when_it_opens()
    {
        var s = New();
        s.Backpack.Add("turnip_seed", 9);
        for (int x = 0; x < 9; x++) { s.Farm.Till(x, 0); s.Farm.Plant(x, 0, "turnip", s.Backpack); }
        Assert.True(s.Orders.Chain("ciraklik")!.StepDone(0));
        Assert.Equal(0, s.Credits);                    // Defter kapali: sessiz sayim
        Assert.Single(s.Orders.PendingRewards);
        s.Orders.OpenDefter();
        Assert.Equal(100, s.Credits);
        Assert.Equal(10, s.Research.Veri);
        Assert.Empty(s.Orders.PendingRewards);
    }

    [Fact]
    public void Chain_grants_land_in_granted_and_complete_the_chain()
    {
        var s = New();
        s.Orders.OpenDefter();
        var made = new List<string>();
        s.Orders.GrantMade += (_, g) => made.Add($"{g.Kind}:{g.Id}");
        int done = 0;
        s.Orders.ChainDone += _ => done++;
        s.Orders.Report("animal_product", 20);         // Mandira 1
        s.Orders.Report("animal_product", 80);         // 2
        s.Orders.Report("animal_product", 400);        // 3
        s.Orders.Report("animal_product", 1500);       // 4
        Assert.Empty(made);
        s.Orders.Report("animal_product", 6000);       // 5: Semaver
        Assert.Equal(new[] { "item:semaver" }, made);
        Assert.Contains("item:semaver", s.Orders.Granted);
        Assert.Equal(1, done);
        Assert.True(s.Orders.Chain("mandira")!.Done);
        for (int i = 0; i < 4; i++) s.Orders.ReportKey("tamed_species", "t" + i);
        Assert.DoesNotContain("member:coban", s.Orders.Granted);
        s.Orders.ReportKey("tamed_species", "t4");      // Coban 2. adim (5 tur): uye
        Assert.Contains("member:coban", s.Orders.Granted);
    }

    [Fact]
    public void Tracked_is_ciraklik_then_first_open_unfinished_chain_unless_pinned()
    {
        var s = New();
        Assert.Equal("ciraklik", s.Orders.Tracked!.Def.Id);
        s.Orders.OpenDefter();
        Assert.Equal("salgam_baronu", s.Orders.Tracked!.Def.Id);
        Assert.True(s.Orders.Pin("mandira"));
        Assert.Equal("mandira", s.Orders.Tracked!.Def.Id);
        Assert.False(s.Orders.Pin("yok_boyle_bir_zincir"));
        s.Orders.Report("animal_product", 8000);        // mandira biter: sabitleme duser
        Assert.Equal("salgam_baronu", s.Orders.Tracked!.Def.Id);
    }

    [Fact]
    public void Order_book_save_round_trips_through_json()
    {
        var s = New();
        s.Orders.Report("planted", 3);
        s.Orders.OpenDefter();
        s.Orders.ReportKey("crop_kind", "a"); s.Orders.ReportKey("crop_kind", "b");
        s.Orders.Level("herd_size", 7);
        s.Orders.Report("swollen_crop", 30);
        s.Orders.Pin("devler_ligi");
        var json = JsonSerializer.Serialize(s.Orders.Save());

        var t = New();
        t.Orders.Load(JsonSerializer.Deserialize<OrdersSave>(json)!);
        Assert.True(t.Orders.DefterOpen);
        Assert.Equal("devler_ligi", t.Orders.PinnedId);
        Assert.Equal(2, t.Orders.Chain("yesil_parmak")!.Count("crop_kind"));
        Assert.Equal(7, t.Orders.Chain("suru_sahibi")!.Count("herd_size"));
        Assert.True(t.Orders.Chain("salgam_baronu")!.StepDone(0) && t.Orders.Chain("salgam_baronu")!.StepDone(1) && !t.Orders.Chain("salgam_baronu")!.StepDone(2));
        Assert.Equal(JsonSerializer.Serialize(s.Orders.Save()), JsonSerializer.Serialize(t.Orders.Save()));
        t.Orders.ReportKey("crop_kind", "a");           // yuklenen anahtar kumesi korunur: tekrar sayilmaz
        Assert.Equal(2, t.Orders.Chain("yesil_parmak")!.Count("crop_kind"));
    }

    // ---------- Ustalik ----------

    [Fact]
    public void Mastery_levels_follow_the_threshold_curve_and_cap_at_fifty()
    {
        var m = New().Mastery;
        Assert.Equal(100.0, m.Threshold(1));
        Assert.Equal(Math.Floor(100 * Math.Pow(10, 1.6)), m.Threshold(10));
        Assert.Equal(0, m.Level("kazi"));
        m.AddXp("kazi", 99);
        Assert.Equal(0, m.Level("kazi"));
        m.AddXp("kazi", 1);
        Assert.Equal(1, m.Level("kazi"));
        m.AddXp("kazi", 10_000_000);
        Assert.Equal(50, m.Level("kazi"));
        Assert.Equal(1f, m.Fraction("kazi"));
        Assert.Equal(0, m.Level("hasat"));
    }

    [Fact]
    public void Earn_gives_xp_by_source_and_unknown_sources_give_none()
    {
        var m = New().Mastery;
        Assert.Equal("kazi", m.Earn("gather:copper_ore", 3));
        Assert.Equal(12, m.Xp("kazi"));
        Assert.Null(m.Earn("gather:yok"));
        m.Earn("plant", 2);
        Assert.Equal(4, m.Xp("ciftcilik"));
    }

    [Fact]
    public void Each_level_adds_one_percent_and_perks_choose_one_of_two_every_ten_levels()
    {
        var m = New().Mastery;
        var raised = new List<(string, int)>();
        var unlocked = new List<(string, int)>();
        m.LevelUp += (b, n) => raised.Add((b, n));
        m.PerkUnlocked += (b, n) => unlocked.Add((b, n));
        m.AddXp("ciftcilik", m.Threshold(9));
        Assert.Equal(0.09f, m.Sum("crop_yield"), 4);
        Assert.False(m.Choose("ciftcilik", 10, 'A'));             // seviye 10 degil
        m.AddXp("ciftcilik", m.Threshold(10) - m.Xp("ciftcilik"));
        Assert.Equal(new[] { ("ciftcilik", 10) }, unlocked);
        Assert.Equal(10, raised.Count);
        Assert.Equal(new[] { 10 }, m.PendingTiers("ciftcilik"));
        Assert.True(m.Choose("ciftcilik", 10, 'A'));              // Bol Hasat: +%10 verim
        Assert.False(m.Choose("ciftcilik", 10, 'B'));             // kademe bir kez
        Assert.Equal(0.10f + 0.10f, m.Sum("crop_yield"), 4);
        Assert.Equal(0f, m.Sum("crop_growth"));                   // B secilmedi
        Assert.True(m.Has("ciftcilik_10a_bol_hasat"));
        Assert.Empty(m.PendingTiers("ciftcilik"));
    }

    [Fact]
    public void Set_effects_and_scaled_amounts()
    {
        var s = New();
        var m = s.Mastery;
        Assert.Equal(12f, m.Set("cable_len", 12f));
        m.AddXp("muhendislik", m.Threshold(20));
        Assert.True(m.Choose("muhendislik", 20, 'A'));            // Uzun Kablo: 16 m
        Assert.Equal(16f, m.Set("cable_len", 12f));
        // 100 birim, +%20: kesinlikle 120 (Scale tam sayida kesirsiz)
        m.AddXp("hasat", m.Threshold(10));
        Assert.True(m.Choose("hasat", 10, 'A'));                  // Oduncu: odun +%20
        Assert.Equal(0.30f, m.Sum("wood_yield") + m.Sum("gather_yield"), 3); // %20 ozellik + 10 seviye x %1
        int extra = s.GatherBonus("wood", 10);                    // 10 x 0,30 = 3
        Assert.Equal(3, extra);
        Assert.Equal(0, s.GatherBonus("stone", 10));              // Kazi seviyesi 0
    }

    [Fact]
    public void Respec_costs_credits_by_level_and_keeps_xp()
    {
        var s = New();
        var m = s.Mastery;
        m.AddXp("savas", m.Threshold(25));
        Assert.True(m.Choose("savas", 10, 'B'));
        Assert.True(m.Choose("savas", 20, 'A'));
        Assert.Equal(2500, m.RespecCost("savas"));               // 1000 x 25/10
        Assert.Equal(-1, s.RespecMastery("savas"));              // Kredi yok
        Assert.Equal(2, m.ChoiceCount("savas"));
        s.Orders.OpenDefter();
        s.Orders.Report("swollen_crop", 100);                    // 100 + 400 + 1500 Kredi
        Assert.Equal(2000, s.Credits);
        s.Orders.Report("swollen_crop", 300);                    // 4. adim 6000
        Assert.Equal(2500, s.RespecMastery("savas"));
        Assert.Equal(0, m.ChoiceCount("savas"));
        Assert.Equal(25, m.Level("savas"));
        Assert.Equal(new[] { 10, 20 }, m.PendingTiers("savas"));
        Assert.Equal(-1, s.RespecMastery("savas"));              // secim yok
    }

    [Fact]
    public void Mastery_state_round_trips_through_json()
    {
        var m = New().Mastery;
        m.AddXp("surus", m.Threshold(12));
        m.Choose("surus", 10, 'B');
        var json = JsonSerializer.Serialize(m.Export());
        var n = New().Mastery;
        n.Import(JsonSerializer.Deserialize<MasteryState>(json)!);
        Assert.Equal(12, n.Level("surus"));
        Assert.True(n.Has("surus_10b_saglam_kasa"));
        Assert.Equal(0.12f + 0.25f, n.Sum("vehicle_speed") + n.Sum("vehicle_hp"), 4);
    }

    [Fact]
    public void Farm_events_feed_mastery_xp()
    {
        var s = New();
        s.Backpack.Add("turnip_seed", 3);
        s.Farm.Till(0, 0); s.Farm.Plant(0, 0, "turnip", s.Backpack);
        Assert.Equal(Data().Mastery.Xp["plant"].Xp, s.Mastery.Xp("ciftcilik"));
    }

    // ---------- Kayit (TEKNIK 9.1) ----------

    [Fact]
    public void Orders_mastery_and_achievements_survive_capture_restore_with_character_scope_apart()
    {
        var a = New();
        a.Orders.Report("planted", 3);
        a.Orders.OpenDefter();
        a.Orders.ReportKey("crop_kind", "a"); a.Orders.ReportKey("crop_kind", "b");
        a.Orders.Level("herd_size", 7);
        a.Orders.Report("swollen_crop", 30);
        a.Orders.Report("kabarma_survived");
        a.Orders.Pin("devler_ligi");
        a.Mastery.AddXp("ciftcilik", a.Mastery.Threshold(10));
        a.Mastery.Choose("ciftcilik", 10, 'B');
        a.Mastery.Scale("crop_yield", 3, a.MasteryRng);

        var ra = new SaveRegistry().AddRange(a.SaveSections());
        var world = SaveCodec.Decode(SaveCodec.Encode(ra.Capture(SaveScope.World, a.Manifest("w"))));
        var character = SaveCodec.Decode(SaveCodec.Encode(ra.Capture(SaveScope.Character, a.Manifest("c"))));
        Assert.Contains("mastery", character.Sections.Keys);
        Assert.Contains("achievements", character.Sections.Keys);
        Assert.DoesNotContain("mastery", world.Sections.Keys);
        Assert.Contains("orders", world.Sections.Keys);

        var b = New();
        var rb = new SaveRegistry().AddRange(b.SaveSections());
        int paid = 0;
        b.OrderPaid += (_, _, _) => paid++;
        rb.Restore(world);
        rb.Restore(character, SaveScope.Character);
        Assert.Equal(0, paid);                                    // yukleme odeme yapmaz
        Assert.Equal(JsonSerializer.Serialize(a.Orders.Save()), JsonSerializer.Serialize(b.Orders.Save()));
        Assert.True(b.Orders.DefterOpen);
        Assert.Equal("devler_ligi", b.Orders.PinnedId);
        Assert.Equal(2, b.Orders.Chain("yesil_parmak")!.Count("crop_kind"));
        Assert.True(b.Mastery.Has("ciftcilik_10b_cabuk_filiz"));
        Assert.Equal(a.Mastery.Xp("ciftcilik"), b.Mastery.Xp("ciftcilik"));
        Assert.True(b.Achievements.IsUnlocked("ilk_gece"));
        // ayni komut, ayni sonuc: sans akisi da yuklendi
        for (int i = 0; i < 6; i++) Assert.Equal(a.Mastery.Scale("crop_yield", 3, a.MasteryRng), b.Mastery.Scale("crop_yield", 3, b.MasteryRng));
        b.Orders.ReportKey("crop_kind", "a");                     // yuklenen anahtar kumesi: tekrar sayilmaz
        Assert.Equal(2, b.Orders.Chain("yesil_parmak")!.Count("crop_kind"));
    }
    // ---------- Basarimlar ----------

    [Fact]
    public void Achievements_unlock_on_chain_completion_and_first_night()
    {
        var s = New();
        var got = new List<string>();
        s.Achievements.UnlockedEvent += a => got.Add(a.Id);
        Assert.Equal(36 + 2, s.Achievements.Count);              // 48 zincir tamamlaninca 50
        Assert.Equal(50, s.Achievements.Total);
        s.Orders.Report("kabarma_survived");
        Assert.Equal(new[] { "ilk_gece" }, got);
        s.Orders.OpenDefter();
        s.Orders.Report("animal_product", 8000);
        Assert.Contains("chain_mandira", got);
        Assert.True(s.Achievements.IsUnlocked("chain_mandira"));
        s.Orders.Report("kabarma_survived");                     // iki kez acilmaz
        Assert.Equal(2, got.Count);
        var n = New();
        n.Achievements.Import(s.Achievements.Export());
        Assert.True(n.Achievements.IsUnlocked("ilk_gece") && n.Achievements.IsUnlocked("chain_mandira"));
    }

    [Fact]
    public void First_night_dawn_unlocks_ilk_gece_through_the_game_path()
    {
        var d = Data();
        var s = new GameState(d);
        CardTests.RunToDawn(s.StartNight(), d);
        Assert.True(s.Achievements.IsUnlocked("ilk_gece"));
    }
}

static class ProgressTestHelpers
{
    public static void OrderPaidCheck(this GameState s, out List<(int, int, string)> paid)
    {
        var list = new List<(int, int, string)>();
        s.OrderPaid += (c, v, w) => list.Add((c, v, w));
        paid = list;
    }
}
