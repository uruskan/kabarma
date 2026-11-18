using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Cipler (ICERIK 6.3, 6.3.1; GDD 4): veri, yuva ve uyum kurallari, etki, dusme, uretim, kayit.</summary>
public class ChipTests
{
    static GameState New() => new(TestData.Load());
    static ChipsFile Def => TestData.Load().Chips;

    // ---------- Veri ----------

    [Fact]
    public void Data_has_24_chips_in_three_groups_and_four_rarities()
    {
        var d = Def;
        Assert.Equal(24, d.Chips.Count);
        Assert.Equal(new[] { 8, 12, 4 }, new[] { "general", "weapon", "work" }.Select(g => d.Chips.Count(c => c.Group == g)));
        Assert.Equal(d.Chips.Count, d.Chips.Select(c => c.Id).Distinct().Count());
        Assert.Equal(new[] { 0.08f, 0.15f, 0.25f, 0.40f }, d.Rarities.Select(r => r.Bonus));
        Assert.Equal(new[] { "common", "rare", "epic", "legendary" }, d.Rarities.Select(r => r.Id));
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 4 }, d.SlotsByMk); // GDD 4
        Assert.All(d.Chips, c => { Assert.NotEmpty(c.Stat); Assert.NotEmpty(c.Legendary.Stat); Assert.True(c.Scale > 0); });
        Assert.All(d.Chips.Where(c => c.Group == "work"), c => Assert.Contains(c.Host, d.WorkHeads));
        Assert.Equal(new[] { "kazma", "bicme", "sulama", "insa_plani" }, d.Chips.Where(c => c.Group == "work").Select(c => c.Host));
    }

    [Fact]
    public void Sources_and_craft_cost_are_valid()
    {
        var d = Def;
        Assert.NotEmpty(d.Sources);
        Assert.All(d.Sources.Values, s =>
        {
            Assert.All(s.Weights.Keys, k => Assert.Contains(d.Rarities, r => r.Id == k));
            Assert.DoesNotContain("legendary", s.Weights.Keys); // Efsane dusmez, yalniz uretilir
        });
        Assert.Contains("holding_parcasi", d.Craft["legendary"].Keys);   // Efsane yalniz Holding Parcasi ile (GDD 4)
        Assert.DoesNotContain("holding_parcasi", d.Craft["epic"].Keys);
        Assert.Contains("ata:koca_pofuduk", d.Sources.Keys);
        Assert.Contains("kabarma_reward", d.Sources.Keys);
    }

    // ---------- Yuva ve uyum ----------

    [Fact]
    public void Slot_count_follows_the_ocak_tier()
    {
        var s = New();
        Assert.Equal(1, s.Chips.Capacity());                // Mk.I
        s.Research.SetOcakTier(3);
        Assert.Equal(2, s.Chips.Capacity());                // Mk.III
        s.Research.SetOcakTier(6);
        Assert.Equal(4, s.Chips.Capacity());                // Mk.VI
    }

    [Fact]
    public void Group_rules_decide_which_head_takes_which_chip()
    {
        var s = New();
        var b = s.Chips;
        var hiz = b.Add("hiz", "common")!;
        var kritik = b.Add("kritik", "common")!;
        var damar = b.Add("damar_goren", "common")!;
        Assert.Equal(ChipResult.Ok, b.CanInstall("kazma", hiz.Id));                // Genel: her baslik
        Assert.Equal(ChipResult.Ok, b.CanInstall("tirpan_isini", hiz.Id));
        Assert.Equal(ChipResult.Incompatible, b.CanInstall("kazma", kritik.Id));   // Silah: yalniz silah basligi
        Assert.Equal(ChipResult.Ok, b.CanInstall("civi_tabancasi", kritik.Id));
        Assert.Equal(ChipResult.Ok, b.CanInstall("kazma", damar.Id));              // Is: kendi basligi
        Assert.Equal(ChipResult.Incompatible, b.CanInstall("bicme", damar.Id));
        Assert.Equal(ChipResult.NoSuchHead, b.CanInstall("yok", hiz.Id));
        Assert.Equal(ChipResult.NoSuchChip, b.CanInstall("kazma", 999));
    }

    [Fact]
    public void Install_fills_slots_refuses_duplicates_and_returns_chips_to_storage()
    {
        var s = New();
        var b = s.Chips;
        var a = b.Add("guc", "common")!; var a2 = b.Add("guc", "rare")!; var c = b.Add("menzil", "common")!;
        Assert.Equal(ChipResult.Ok, b.Install("tirpan_isini", a.Id));
        Assert.Equal(ChipResult.AlreadyInstalled, b.CanInstall("tirpan_isini", a.Id));
        Assert.Equal(ChipResult.AlreadyInstalled, b.CanInstall("civi_tabancasi", a.Id));  // ornek tek yerde
        Assert.Equal(ChipResult.Duplicate, b.Install("tirpan_isini", a2.Id));      // ayni tur bir kez
        Assert.Equal(ChipResult.NoSlot, b.Install("tirpan_isini", c.Id));          // Mk.I: 1 yuva
        s.Research.SetOcakTier(3);                                                // 2 yuva
        Assert.Equal(ChipResult.Ok, b.Install("tirpan_isini", c.Id));
        Assert.Equal(new[] { a.Id, c.Id }, b.SlotsOf("tirpan_isini"));
        Assert.Equal(ChipResult.Ok, b.Remove("tirpan_isini", 0));
        Assert.Equal(new[] { 0, c.Id }, b.SlotsOf("tirpan_isini"));
        Assert.Contains(a, b.InStorage);
        Assert.Equal(ChipResult.NoSlot, b.Remove("tirpan_isini", 0));              // bos yuva
        Assert.Equal(ChipResult.Ok, b.Install("tirpan_isini", a2.Id, 0));          // belirli yuva, ayni tur simdi bos
    }

    [Fact]
    public void Locked_book_refuses_install_and_remove_during_a_kabarma_night()
    {
        var s = New();
        var c = s.Chips.Add("hiz", "common")!;
        Assert.Equal(ChipResult.Ok, s.Chips.Install("kazma", c.Id));
        s.Chips.Locked = true;
        Assert.Equal(ChipResult.Locked, s.Chips.Remove("kazma", 0));
        var d = s.Chips.Add("guc", "common")!;
        Assert.Equal(ChipResult.Locked, s.Chips.Install("bicme", d.Id));
        s.Chips.Locked = false;
        Assert.Equal(ChipResult.Ok, s.Chips.Remove("kazma", 0));
    }

    // ---------- Etki ----------

    [Fact]
    public void Effect_is_rarity_times_scale_and_stacks_per_stat()
    {
        var s = New();
        s.Research.SetOcakTier(3);
        var b = s.Chips;
        var kritik = b.Add("kritik", "rare")!;          // 0,15 x 0,6
        var guc = b.Add("guc", "epic")!;                // 0,25 x 1
        var guc2 = b.Add("hafif", "common")!;
        b.Install("tirpan_isini", kritik.Id); b.Install("tirpan_isini", guc.Id);
        Assert.Equal(0.09f, b.Stat("tirpan_isini", "crit_chance"), 4);
        Assert.Equal(0.25f, b.Stat("tirpan_isini", "damage"), 4);
        Assert.Equal(0f, b.Stat("civi_tabancasi", "damage"));          // baska baslik
        Assert.Equal(0f, b.Stat("tirpan_isini", "carry_capacity"));    // depoda: sayilmaz
    }

    [Fact]
    public void Legendary_adds_its_specific_extra_effect()
    {
        var s = New();
        s.Research.SetOcakTier(3);
        var b = s.Chips;
        var seken = b.Add("seken", "legendary")!;
        var epic = b.Add("akim", "epic")!;
        b.Install("civi_tabancasi", seken.Id); b.Install("civi_tabancasi", epic.Id);
        Assert.Equal(0.40f * 1.5f, b.Stat("civi_tabancasi", "bounce_chance"), 4);
        Assert.Equal(1f, b.Stat("civi_tabancasi", "bounce_count"));    // +1 sekme: yalniz Efsane
        Assert.Equal(0f, b.Stat("civi_tabancasi", "chain_count"));     // Destansi Akim'da ek etki yok
        // Kritik'in Efsane ek etkisi ayni stat'a eklenir (Guc'un crit_damage'i ile toplanir)
        var kr = b.Add("kritik", "legendary")!;
        s.Research.SetOcakTier(5);
        b.Install("tirpan_isini", kr.Id);
        Assert.Equal(0.40f * 0.6f, b.Stat("tirpan_isini", "crit_chance"), 4);
        Assert.Equal(0.3f, b.Stat("tirpan_isini", "crit_damage"), 4);
    }

    // ---------- Dusme ----------

    [Fact]
    public void Drops_follow_the_source_table_and_never_give_legendary()
    {
        var s = New();
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < 200; i++)
        {
            var c = s.DropChip("ata:boynuzlu_dede");      // Nadir 70, Destansi 30, olasilik 1
            Assert.NotNull(c);
            counts[c!.Rarity] = counts.GetValueOrDefault(c.Rarity) + 1;
        }
        Assert.Equal(new[] { "epic", "rare" }, counts.Keys.OrderBy(x => x));
        Assert.InRange(counts["epic"], 35, 85);           // ~%30 (200 dusme)
        Assert.Null(s.DropChip("yok_boyle_bir_kaynak"));
        Assert.Equal(200, s.Chips.Owned.Count);           // depo siniri 200: sonrasi dusmez
        Assert.Null(s.DropChip("ata:boynuzlu_dede"));
    }

    [Fact]
    public void Kabarma_reward_rarity_rises_with_ks_and_the_roll_is_deterministic()
    {
        int Epics(int ks)
        {
            var s = New();
            int e = 0;
            for (int i = 0; i < 600 && s.Chips.Owned.Count < 200; i++)
            {
                var c = s.DropChip("kabarma_reward", ks);
                if (c?.Rarity == "epic") e++;
            }
            return e;
        }
        Assert.True(Epics(30) > Epics(1) + 5);
        var a = New(); var b = New();
        for (int i = 0; i < 20; i++)
            Assert.Equal(a.DropChip("holding:kamp")?.Type, b.DropChip("holding:kamp")?.Type);
    }

    // ---------- Uretim ----------

    [Fact]
    public void Craft_spends_materials_and_legendary_needs_a_holding_part()
    {
        var s = New();
        var cost = s.Chips.CraftCost("common");
        Assert.False(s.Chips.CanCraft("hiz", "common", s.Backpack));
        Assert.Equal(ChipResult.Missing, s.Chips.Craft("hiz", "common", s.Backpack).result);
        foreach (var (item, n) in cost) s.Backpack.Add(item, n + 2);
        var (r, chip) = s.Chips.Craft("hiz", "common", s.Backpack);
        Assert.Equal(ChipResult.Ok, r);
        Assert.Equal("common", chip!.Rarity);
        foreach (var (item, n) in cost) Assert.Equal(2, s.Backpack.Count(item));

        foreach (var (item, n) in s.Chips.CraftCost("legendary")) if (item != "holding_parcasi") s.Backpack.Add(item, n);
        Assert.Equal(ChipResult.Missing, s.Chips.Craft("seken", "legendary", s.Backpack).result);   // Holding Parcasi yok
        s.Backpack.Add("holding_parcasi", s.Chips.CraftCost("legendary")["holding_parcasi"]);
        Assert.Equal(ChipResult.Ok, s.Chips.Craft("seken", "legendary", s.Backpack).result);
        Assert.Equal(ChipResult.NoSuchChip, s.Chips.Craft("yok", "common", s.Backpack).result);
    }

    // ---------- Gecici savas arayuzu ----------

    [Fact]
    public void Interim_combat_adapter_merges_chip_stats_into_combat_stats()
    {
        var s = New();
        s.Research.SetOcakTier(3);
        var b = s.Chips;
        b.Install("tirpan_isini", b.Add("hiz", "epic")!.Id);
        b.Install("tirpan_isini", b.Add("itici", "rare")!.Id);
        var cards = new CombatStats { Projectiles = 2, FireRateMult = 1.2f };
        var m = ChipCombat.Merge(cards, b, "tirpan_isini", new Rng(1));
        Assert.Equal(1.2f * 1.25f, m.FireRateMult, 4);
        Assert.Equal(1f + 0.15f * 2f, m.KnockbackMult, 4);
        Assert.Equal(2, m.Projectiles);
        Assert.Equal(1.2f, cards.FireRateMult);          // kart istatistigi degismez
        var none = ChipCombat.Merge(null, b, "civi_tabancasi", new Rng(1));
        Assert.True(none.IsBase);
        bool crit = false;
        Assert.Equal(10f, ChipCombat.AdjustHit(b, "civi_tabancasi", 10f, ref crit, 2f, new Rng(1))); // takili cip yok
        b.Install("civi_tabancasi", b.Add("guc", "rare")!.Id);
        Assert.Equal(12f, ChipCombat.AdjustHit(b, "civi_tabancasi", 10f, ref crit, 2f, new Rng(1))); // 10 x 1,15 = 11,5 -> 12
    }

    // ---------- Kayit ----------

    [Fact]
    public void Chip_section_round_trips_and_restores_silently()
    {
        var a = New();
        a.Research.SetOcakTier(3);
        var x = a.Chips.Add("hiz", "epic")!; var y = a.Chips.Add("kritik", "legendary")!; a.Chips.Add("guc", "common");
        a.Chips.Install("tirpan_isini", x.Id); a.Chips.Install("tirpan_isini", y.Id);
        a.DropChip("ata:koca_pofuduk");
        var reg = new SaveRegistry().AddRange(a.SaveSections());
        var g = SaveCodec.Decode(SaveCodec.Encode(reg.Capture(SaveScope.World, a.Manifest("c"))));
        Assert.Contains("chips", g.Sections.Keys);

        var b = New();
        b.Research.SetOcakTier(3);
        int added = 0;
        b.Chips.Added += _ => added++;
        new SaveRegistry().AddRange(b.SaveSections()).Restore(g);
        Assert.Equal(0, added);
        Assert.Equal(JsonSerializer.Serialize(a.Chips.Save()), JsonSerializer.Serialize(b.Chips.Save()));
        Assert.Equal(a.Chips.Stat("tirpan_isini", "crit_chance"), b.Chips.Stat("tirpan_isini", "crit_chance"));
        Assert.Equal(a.Chips.NextId, b.Chips.NextId);
        for (int i = 0; i < 5; i++) Assert.Equal(a.DropChip("holding:kamp")?.Type, b.DropChip("holding:kamp")?.Type);
    }

    [Fact]
    public void Load_drops_unknown_types_and_broken_slots()
    {
        var s = New();
        s.Chips.Load(new ChipsSave
        {
            NextId = 1,
            Owned = { new ChipInstance { Id = 5, Type = "hiz", Rarity = "rare" }, new ChipInstance { Id = 6, Type = "yok", Rarity = "rare" } },
            Slots = { ["kazma"] = new() { 5 }, ["bicme"] = new() { 5 }, ["sulama"] = new() { 6 } },
        });
        Assert.Single(s.Chips.Owned);
        Assert.Equal(new[] { 5 }, s.Chips.SlotsOf("bicme"));   // siralamada ilk gelen alir
        Assert.Equal(new[] { 0 }, s.Chips.SlotsOf("kazma"));   // ayni ornek iki yuvada olamaz
        Assert.Equal(new[] { 0 }, s.Chips.SlotsOf("sulama"));  // tanimsiz tur atildi
        Assert.True(s.Chips.NextId > 5);
    }
}
