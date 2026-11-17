using System.Text.RegularExpressions;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Ilk gunun adim zinciri (GDD 24, PLAN 1.4) ve Suna'nin replikleri (SANAT-SES 10.3), kurallar duzeyinde.</summary>
public class ScenarioTests
{
    static GameData Data() => TestData.Load();

    /// <summary>GDD 24 sirasi: 9 adim (ilk 10 dakika) + 7 adim (ilk 10 dakikadan sonra).</summary>
    static readonly string[] Order = { "mine", "odun", "ocak", "tezgah", "tarla", "agil", "gidak", "night", "sell", "hasat", "yemek", "bakir_kil", "firin", "civi", "kasa", "barinak" };

    [Fact]
    public void Steps_follow_gdd_24_order_and_targets()
    {
        var steps = Data().Scenario.Steps;
        Assert.Equal(Order, steps.Select(s => s.Id));
        Assert.Equal(9, steps.Count(s => !s.AfterTen));
        Assert.Equal(7, steps.Count(s => s.AfterTen));
        int T(string id) => steps.Single(s => s.Id == id).Target;
        Assert.Equal(10, T("mine"));   // Kazma ile 10 Tas
        Assert.Equal(25, T("odun"));   // Tezgah 10 + agil 13 + 2
        Assert.Equal(9, T("tarla"));   // 3x3
        Assert.Equal(2, T("agil"));    // en az 1x2 hucre
        Assert.Equal(5, T("hasat"));   // 5 salgam
    }

    [Fact]
    public void Suna_has_thirty_lines_in_two_languages_and_every_reference_resolves()
    {
        var sc = Data().Scenario;
        Assert.Equal(30, sc.Lines.Count);
        Assert.All(sc.Lines, kv => { Assert.False(string.IsNullOrWhiteSpace(kv.Value.Tr), kv.Key); Assert.False(string.IsNullOrWhiteSpace(kv.Value.En), kv.Key); });
        var used = sc.Intro.Concat(sc.Steps.SelectMany(s => new[] { s.Start, s.Done })).Concat(sc.NightLineIds.Values).Append(sc.NextOrderLine)
            .Where(x => x != null).Select(x => x!).ToList();
        Assert.All(used, id => Assert.True(sc.Lines.ContainsKey(id), $"replik yok: {id}"));
        Assert.Equal(used.Count, used.Distinct().Count()); // her replik bir kez
        Assert.Equal(sc.Lines.Keys.OrderBy(x => x), used.OrderBy(x => x)); // kullanilmayan replik yok
        // SANAT-SES 10.3 dagilimi: giris 2, dokuz adim 14, Kucuk Kabarma ve safak 6, sonraki yedi adim 7, siradaki is emri 1.
        Assert.Equal(2, sc.Intro.Count);
        var first9 = sc.Steps.Where(s => !s.AfterTen && s.Id != "night").SelectMany(s => new[] { s.Start, s.Done }).Append(sc.Steps.Single(s => s.Id == "night").Start).Count(x => x != null);
        Assert.Equal(14, first9);
        Assert.Equal(6, sc.NightLineIds.Count + 1); // + safak (gece adiminin bitis repligi)
        Assert.Equal(7, sc.Steps.Where(s => s.AfterTen).SelectMany(s => new[] { s.Start, s.Done }).Count(x => x != null));
        Assert.NotNull(sc.NextOrderLine);
        // PLAN 1.4'teki iki replik aynen.
        Assert.Equal("Kooperatife hoş geldin, üye 4417. Tarla senin, borç bizim.", sc.Text("giris_1"));
        Assert.Equal("Küçük bir ayrıntı kaldı. Bu ayın bir huyu var.", sc.Text(sc.Steps.Single(s => s.Id == "night").Start));
    }

    /// <summary>GDD 24 / SANAT-SES 10.3: seslendirilen replik tus adi icermez; tuslar adim satirinda {act:eylem} ile.</summary>
    [Fact]
    public void Voiced_lines_name_no_keys_and_goals_use_action_placeholders()
    {
        var sc = Data().Scenario;
        var keyish = new Regex(@"\{act:|\(\d\)|\b[EQRFZXC]\b|\bTab\b|\bShift\b|\bAlt\b|\bCtrl\b|sol tık|sağ tık|left click|right click|\bmouse\b|\bfare\b|\btuş|\bkey\b|\bbutton\b", RegexOptions.IgnoreCase);
        foreach (var (id, l) in sc.Lines)
        {
            Assert.False(keyish.IsMatch(l.Tr), $"{id} (tr): {l.Tr}");
            Assert.False(keyish.IsMatch(l.En), $"{id} (en): {l.En}");
        }
        var known = new HashSet<string> { "interact", "fire", "bag", "build_next", "build_prev", "build_rotate", "head_1", "head_2", "head_3", "head_4", "head_5", "head_6", "head_7", "head_8" };
        foreach (var s in sc.Steps)
        {
            foreach (Match m in Regex.Matches(s.Goal, @"\{act:([a-z0-9_]+)\}")) Assert.Contains(m.Groups[1].Value, known);
            var bare = Regex.Replace(s.Goal, @"\{act:[a-z0-9_]+\}", "");
            Assert.False(Regex.IsMatch(bare, @"\(\d\)|\b[EQR]\b|\bTab\b"), $"{s.Id} hedef satirinda cig tus: {s.Goal}");
        }
    }

    /// <summary>Ilk gunun butun zinciri, kurallar duzeyinde: her adim kendi sayaciyla ilerler, baska adimin olayi sayilmaz.</summary>
    [Fact]
    public void First_day_full_chain()
    {
        var d = Data();
        var s = new GameState(d);
        var sc = s.Scenario;
        Assert.True(sc.Is("mine"));
        sc.Report("ocak"); // yanlis adimin olayi yok sayilir
        Assert.True(sc.Is("mine"));
        for (int i = 0; i < 10; i++) { s.Backpack.Add("stone", 1); sc.Report("mine"); }
        s.Backpack.Add("wood", 25); sc.SetProgress("odun", s.Backpack.Count("wood"));
        sc.Report("ocak");
        sc.Report("tezgah");
        Assert.True(sc.Is("tarla"));
        int watered = 0;
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) { s.Farm.Till(x, y); Assert.True(s.Farm.Plant(x, y, "turnip", s.Backpack)); s.Farm.Water(x, y); sc.SetProgress("tarla", ++watered); }
        Assert.True(sc.Is("agil"));
        // Agil: 5 cit + 1 bahce kapisi = 1x2 hucre.
        var walls = new List<PenWall>
        {
            new() { Id = 1, A = new(0, 0), B = new(3, 0) }, new() { Id = 2, A = new(3, 0), B = new(3, 3) }, new() { Id = 3, A = new(3, 3), B = new(3, 6) },
            new() { Id = 4, A = new(3, 6), B = new(0, 6) }, new() { Id = 5, A = new(0, 6), B = new(0, 3) }, new() { Id = 6, A = new(0, 3), B = new(0, 0), Gate = true },
        };
        var pen = PenRules.FindPens(walls, d.Pen.CellArea, d.Pen.Weld).Single();
        sc.SetProgress("agil", pen.Cells(d.Pen.CellArea));
        Assert.True(sc.Is("gidak"));
        // Gidak: tek bugday (ogretici), agila girince kayit ve hemen yumurta.
        var g = new PenAnimal { Species = "gidak", Tutorial = true };
        Assert.True(s.Coop.Rules.Feed(g));
        Assert.True(s.Coop.Rules.Register(g, pen, 0, out bool egg));
        Assert.True(egg);
        sc.Report("gidak");
        Assert.True(sc.Is("night"));
        // Ogretici hizinda buyume: gece basladiginda ekin olgunlasmis olur (gece olgunluga bakmaz).
        float t = 0;
        while (s.Farm.MatureCount < 9 && t < 300) { s.Farm.Tick(0.25f, true); t += 0.25f; }
        Assert.InRange(t, 10, 200);

        var n = s.StartNight();
        n.SwellNow += () => s.Farm.SwellMature(n.Ks, 0);
        var zip = d.Creature("zipzip");
        while (n.Phase != NightPhase.Dawn)
        {
            n.Tick(0.1f);
            int due = n.SpawnDue();
            for (int i = 0; i < due; i++) n.OnKill(zip);
            if (n.Offer != null) n.Choose(0);
            if (due > 0) n.WaveCleared();
        }
        sc.Report("night");
        Assert.Equal(9, s.Farm.Plots.Count(p => p.Swollen != null));
        var size = s.Farm.Uproot(1, 1)!;
        s.Sell("turnip", size);
        sc.Report("sell");
        Assert.True(sc.Is("hasat"));
        Assert.Equal(120, s.Credits);

        // 10-16: ilk 10 dakikadan sonra (sayaclar; istasyon ve tarifler veride).
        int got = 0;
        foreach (var p in s.Farm.Plots.ToList()) { var h = s.Farm.Harvest(p.X, p.Y, s.Rng); if (h != null) { s.Backpack.Add("turnip", h.Value.yield, h.Value.stars); got += h.Value.yield; } }
        Assert.True(got >= 8 * 6, $"kabarik salgamlar iki kat verir, alinan {got}");
        sc.SetProgress("hasat", s.Backpack.Count("turnip"));
        // Kalan adimlar (veride on kosulu olanlar; olmayanlar yuklemede atlandi): her biri kendi hedefine.
        Assert.Equal(sc.Steps.Count - 10, sc.Steps.Skip(10).Count());
        while (sc.Current is StepDef cur) { int at = sc.StepIndex; sc.SetProgress(cur.Id, cur.Target); Assert.Equal(at + 1, sc.StepIndex); }
        Assert.True(sc.Finished);
        Assert.Equal(16, sc.Steps.Count + sc.Skipped.Count);
    }

    /// <summary>GDD 24: ilk gece ekinin olgun olmasi gerekmez; Kucuk Kabarma evreye bakmadan butun ekinleri kabartir.</summary>
    [Fact]
    public void First_night_swells_every_crop_whatever_its_stage()
    {
        var d = Data();
        Assert.True(d.Scenario.FirstNightSwellsAll);
        var s = new GameState(d);
        for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) { s.Farm.Till(x, y); s.Farm.Plant(x, y, "turnip", s.Backpack); s.Farm.Water(x, y); }
        s.Farm.Till(5, 5); // surulmus ama ekilmemis kare sayilmaz
        s.Farm.Tick(1f, true);
        Assert.Equal(0, s.Farm.MatureCount);
        Assert.Equal(9, FirstDayRules.RipenForFirstNight(s.Farm));
        Assert.Equal(9, s.Farm.SwellMature(0, 0).Count);
        Assert.Equal(0, FirstDayRules.RipenForFirstNight(s.Farm));
    }

    /// <summary>
    /// Ilk 10 dakikadan sonraki adimlar baska akislarin tarif ve yerlestirilebilirlerine dayanir (Ocakbasi, Bakir Kulce...).
    /// Veride olmayan on kosul adimi devre disi birakir; sira korunur; ilk dokuz adim hic atlanmaz.
    /// </summary>
    [Fact]
    public void Steps_whose_requirements_are_missing_are_skipped_in_order()
    {
        var d = Data();
        Assert.All(d.Scenario.Steps.Where(s => !s.AfterTen), s => Assert.Empty(s.Requires));
        Assert.All(d.Scenario.Steps.SelectMany(s => s.Requires), id => Assert.Matches("^[a-z_]+$", id));
        Assert.Equal(new[] { "nope_x" }, d.MissingFor(new StepDef { Requires = { "tezgah", "civi", "nope_x" } }));

        var sc = new Scenario(d.Scenario, s => s.Id is "yemek" or "kasa" ? new List<string> { "x" } : new List<string>());
        Assert.Equal(Order.Where(id => id is not ("yemek" or "kasa")), sc.Steps.Select(s => s.Id));
        Assert.Equal(new[] { "yemek", "kasa" }, sc.Skipped.Select(x => x.step.Id));
        var all = new Scenario(d.Scenario);
        Assert.Equal(Order, all.Steps.Select(s => s.Id));

        var real = new GameState(d).Scenario;
        Assert.Equal(Order.Take(9), real.Steps.Take(9).Select(s => s.Id));
        Assert.All(real.Skipped, x => Assert.NotEmpty(d.MissingFor(x.step)));
    }

    /// <summary>Gercek veriyle: on kosulu veride olan adim atlanmaz, olmayan atlanir; on kosulsuz adim hep oynanir.</summary>
    [Fact]
    public void Met_requirement_keeps_step_unmet_requirement_skips_it()
    {
        var d = Data();
        var file = new ScenarioFile
        {
            Steps =
            {
                new StepDef { Id = "a", Target = 1, Requires = { "tezgah", "civi" } }, // ikisi de veride
                new StepDef { Id = "b", Target = 1, Requires = { "tezgah", "yok_boyle_bir_sey" } },
                new StepDef { Id = "c", Target = 1 },
            }
        };
        var sc = new Scenario(file, d.MissingFor);
        Assert.Equal(new[] { "a", "c" }, sc.Steps.Select(s => s.Id));
        var skipped = Assert.Single(sc.Skipped);
        Assert.Equal("b", skipped.step.Id);
        Assert.Equal(new[] { "yok_boyle_bir_sey" }, skipped.missing);
        sc.Report("b"); // atlanan adimin olayi yok sayilir
        Assert.True(sc.Is("a"));
    }

    /// <summary>
    /// requires yalniz CLAUDE.md'deki ortak kimlikleri kullanir: birlestirmeden sonra ad tutmazsa adim sessizce kapali
    /// kalmasin diye uydurma kimlik yok.
    /// </summary>
    [Fact]
    public void Requires_use_only_shared_ids()
    {
        var shared = new HashSet<string>
        {
            "stone", "wood", "fiber", "clay", "copper_ore", "copper_ingot", "wheat", "egg", "turnip", "turnip_seed", "civi",
            "civi_tabancasi", "el_arabasi", "kargo_kasasi", "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi",
            "ocak", "tezgah", "tas_firin", "paketleme", "ocakbasi", "kargo_rampasi", "ahsap_kapi", "sandik", "uyku_tulumu",
            "mesale", "yemlik", "folluk",
        };
        foreach (var s in Data().Scenario.Steps)
            foreach (var id in s.Requires) Assert.True(shared.Contains(id), $"{s.Id}: '{id}' ortak kimlik degil");
    }
}
