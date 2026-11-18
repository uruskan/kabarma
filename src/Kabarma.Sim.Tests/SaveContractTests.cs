using System.Reflection;
using System.Text.Json.Nodes;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Kayit sozlesmesi (TEKNIK 9.1): bolum kimlikleri, kapsama, goc adimlari, gidis-donus, siralama, bilinmeyen ve
/// gelecekteki bolumler.
/// </summary>
public class SaveContractTests
{
    /// <summary>
    /// Kayda girmeyen GameState ozellikleri: veri, olay yayini ya da baska bir ozellikten turetilen. Yeni bir ozellik
    /// eklediysen ya bir [SaveSection] Covers'ina yaz ya da gercekten gecici ise buraya (nedeniyle).
    /// </summary>
    static readonly HashSet<string> Transient = new()
    {
        nameof(GameState.Data),       // veri dosyalari, kayit degil
        nameof(GameState.LastReport), // son gecenin raporu: ekranda bir kez gosterilir, safaktan sonra yeniden kurulur
    };

    static GameState NewState() => new(TestData.Load());

    static SaveRegistry Registry(GameState s) => new SaveRegistry().AddRange(s.SaveSections());

    [Fact]
    public void Every_GameState_property_is_saved_or_listed_transient()
    {
        var s = NewState();
        var covered = s.SaveSections().SelectMany(SaveSection.CoveredBy).ToHashSet();
        var missing = typeof(GameState).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => !covered.Contains(n) && !Transient.Contains(n))
            .ToList();
        Assert.True(missing.Count == 0,
            "Kayda girmeyen GameState ozellikleri: " + string.Join(", ", missing) +
            ". Ya bir [SaveSection(..., Covers = ...)] bolumu yaz (TEKNIK 9.1) ya da SaveContractTests.Transient'e nedeniyle ekle.");
    }

    [Fact]
    public void Section_ids_are_unique_and_valid()
    {
        var ids = NewState().SaveSections().Select(x => x.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var id in ids) SaveSection.CheckId(id);
        Assert.Contains("core", ids);
        Assert.Contains("player.pack", ids);
    }

    [Fact]
    public void Every_section_above_v1_has_a_step_for_each_older_version()
    {
        foreach (var sec in NewState().SaveSections())
            for (int v = 1; v < sec.Version; v++)
                Assert.True(SaveSection.HasStep(sec, v), $"'{sec.Id}' v{sec.Version}: v{v}->v{v + 1} goc adimi yok ([SaveMigration(\"{sec.Id}\", {v})])");
    }

    [Fact]
    public void Capture_restore_capture_is_byte_identical()
    {
        var a = NewState();
        a.Backpack.Add("stone", 37);
        a.Backpack.Add("turnip", 4, 3);
        a.Backpack.Add("wood", 120);
        a.Backpack.Remove("wood", 70);
        a.Belt.Select(2);
        a.Tank.TryWaterCell();
        a.Ids.New(); a.Ids.New();
        a.Rng.NextU64();
        var g = Registry(a).Capture(SaveScope.World, a.Manifest("deneme"));

        var b = NewState();
        var rep = Registry(b).Restore(SaveCodec.Decode(SaveCodec.Encode(g)));
        Assert.Contains("core", rep.Loaded);
        var g2 = Registry(b).Capture(SaveScope.World, b.Manifest("deneme"));
        foreach (var (id, blob) in g.Sections) Assert.Equal(blob.Data, g2.Sections[id].Data);

        Assert.Equal(a.Backpack.Stacks, b.Backpack.Stacks);
        Assert.Equal(a.Belt.Ids, b.Belt.Ids);
        Assert.Equal(a.Belt.Selected, b.Belt.Selected);
        Assert.Equal(a.Tank.Liters, b.Tank.Liters);
        Assert.Equal(a.Rng.NextU64(), b.Rng.NextU64()); // ayni rastgele dizi surer
        Assert.Equal(a.Ids.New(), b.Ids.New());
        Assert.Equal(a.Known.OrderBy(x => x), b.Known.OrderBy(x => x));
    }

    [Fact]
    public void Restore_is_silent_no_events()
    {
        var a = NewState();
        a.Backpack.Add("egg", 3);
        var g = Registry(a).Capture(SaveScope.World);
        var b = NewState();
        int changed = 0, sel = 0;
        b.Backpack.Changed += (_, _) => changed++;
        b.Belt.SelectionChanged += (_, _) => sel++;
        Registry(b).Restore(g);
        Assert.Equal(0, changed);
        Assert.Equal(0, sel);
        Assert.Equal(3, b.Eggs);
    }

    [Fact]
    public void Restored_state_runs_on_identically()
    {
        // Gidis-donus esitligi bir alanin unutulmasini yakalamaz; ayni komutlarla ikisi de ayni yere varmali.
        var a = NewState();
        a.Backpack.Add("wheat", 5);
        a.Rng.NextU64();
        var b = NewState();
        Registry(b).Restore(Registry(a).Capture(SaveScope.World));
        foreach (var s in new[] { a, b })
        {
            s.Backpack.Add("stone", 12);
            s.Backpack.Remove("wheat", 2);
            s.Tank.Fill(3f);
            s.Belt.Scroll(1);
        }
        Assert.Equal(a.Backpack.Stacks, b.Backpack.Stacks);
        Assert.Equal(a.Belt.Selected, b.Belt.Selected);
        Assert.Equal(a.Tank.Liters, b.Tank.Liters);
        for (int i = 0; i < 5; i++) Assert.Equal(a.Rng.Range(0, 1000), b.Rng.Range(0, 1000));
    }

    [Fact]
    public void Wrong_seed_is_refused()
    {
        var a = new GameState(TestData.Load(), seed: 1);
        var b = new GameState(TestData.Load(), seed: 2);
        Assert.Throws<SaveException>(() => Registry(b).Restore(Registry(a).Capture(SaveScope.World)));
    }

    sealed class V2 { public string Name { get; set; } = ""; public int Count { get; set; } }

    [Fact]
    public void Json_section_migrates_v1_to_v2()
    {
        // v1'de "n" vardi; v2'de "name" ve "count" oldu.
        V2? got = null;
        var sec = SaveSection.Json("ornek", 2, () => new V2(), v => got = v)
            .Step(1, o => { o["name"] = o["n"]!.GetValue<string>(); o.Remove("n"); o["count"] = 1; });
        var g = new SaveGame();
        g.Sections["ornek"] = new SaveBlob { Version = 1, Data = "{\"n\":\"gidak\"}"u8.ToArray() };
        var rep = new SaveRegistry().Add(sec).Restore(g);
        Assert.Equal("gidak", got!.Name);
        Assert.Equal(1, got.Count);
        Assert.Equal(("ornek", 1, 2), rep.Migrated.Single());
        Assert.True(SaveSection.HasStep(sec, 1));
        Assert.False(SaveSection.HasStep(sec, 2));
    }

    [Fact]
    public void Missing_step_and_future_version_are_refused_before_any_read()
    {
        int reads = 0;
        var early = SaveSection.Json("a", 1, () => 1, _ => reads++, order: SaveOrder.Spawn);
        var late = SaveSection.Json("b", 2, () => 1, _ => reads++);
        var g = new SaveGame();
        g.Sections["a"] = new SaveBlob { Version = 1, Data = "1"u8.ToArray() };
        g.Sections["b"] = new SaveBlob { Version = 1, Data = "1"u8.ToArray() };
        Assert.Throws<SaveException>(() => new SaveRegistry().Add(early).Add(late).Restore(g));
        Assert.Equal(0, reads); // yarim yukleme yok

        g.Sections["b"] = new SaveBlob { Version = 3, Data = "1"u8.ToArray() };
        Assert.Throws<SaveException>(() => new SaveRegistry().Add(early).Add(late).Restore(g));
    }

    [Fact]
    public void Restore_follows_order_then_after_restore()
    {
        var log = new List<string>();
        var r = new SaveRegistry()
            .Add(SaveSection.Json("p", 1, () => 0, _ => log.Add("p"), SaveOrder.Progress, afterRestore: () => log.Add("p+")))
            .Add(SaveSection.Json("s", 1, () => 0, _ => log.Add("s"), SaveOrder.Spawn, afterRestore: () => log.Add("s+")))
            .Add(SaveSection.Json("m", 1, () => 0, _ => log.Add("m"), SaveOrder.State));
        r.Restore(r.Capture(SaveScope.World));
        Assert.Equal(new[] { "s", "m", "p", "s+", "p+" }, log);
    }

    [Fact]
    public void Unknown_sections_are_carried_and_missing_keep_new_game_values()
    {
        var g = new SaveGame();
        g.Sections["gelecek.akis"] = new SaveBlob { Version = 4, Data = "{\"x\":1}"u8.ToArray() };
        int value = 7;
        var r = new SaveRegistry().Add(SaveSection.Json("benim", 1, () => value, v => value = v));
        var rep = r.Restore(g);
        Assert.Equal(new[] { "benim" }, rep.Missing);
        Assert.Equal(new[] { "gelecek.akis" }, rep.Unknown);
        Assert.Equal(7, value);
        var again = r.Capture(SaveScope.World);
        Assert.Equal(g.Sections["gelecek.akis"].Data, again.Sections["gelecek.akis"].Data);
    }

    [Fact]
    public void Duplicate_or_bad_ids_are_refused()
    {
        var r = new SaveRegistry().Add(SaveSection.Json("x", 1, () => 0, _ => { }));
        Assert.Throws<SaveException>(() => r.Add(SaveSection.Json("x", 1, () => 0, _ => { })));
        Assert.Throws<ArgumentException>(() => SaveSection.Json("Buyuk Harf", 1, () => 0, _ => { }));
    }

    [Fact]
    public void Character_scope_is_separate()
    {
        int mastery = 5, world = 9;
        var r = new SaveRegistry()
            .Add(SaveSection.Json("mastery", 1, () => mastery, v => mastery = v, scope: SaveScope.Character))
            .Add(SaveSection.Json("w", 1, () => world, v => world = v));
        var c = r.Capture(SaveScope.Character);
        var w = r.Capture(SaveScope.World);
        Assert.Equal(new[] { "mastery" }, c.Sections.Keys);
        Assert.Equal(new[] { "w" }, w.Sections.Keys);
        Assert.Equal("character", c.Manifest.Scope);
    }

    [Fact]
    public void Migration_attribute_on_property_sections_is_found()
    {
        var o = new Owner();
        var sec = SaveSection.FromAttributes(o).Single();
        Assert.True(SaveSection.HasStep(sec, 1));
        var g = new SaveGame();
        g.Sections["sahip"] = new SaveBlob { Version = 1, Data = "{\"old\":3}"u8.ToArray() };
        new SaveRegistry().Add(sec).Restore(g);
        Assert.Equal(3, o.Value);
    }

    sealed class OwnerSave { public int Value { get; set; } }

    sealed class Owner
    {
        public int Value;
        [SaveSection("sahip", 2)]
        OwnerSave Section { get => new() { Value = Value }; set => Value = value.Value; }

        [SaveMigration("sahip", 1)]
        static void From1(JsonObject o) { o["value"] = o["old"]!.GetValue<int>(); o.Remove("old"); }
    }
}
