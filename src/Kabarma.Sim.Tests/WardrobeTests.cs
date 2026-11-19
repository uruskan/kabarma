using System.Linq;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Dolap (M4 dolap; ICERIK §9): 12 uye tipi, 72 kozmetik (6 slot × 12), pasifler, acilma, kayit.</summary>
public class WardrobeTests
{
    static GameData D => TestData.Load();

    [Fact]
    public void Counts_match_the_content_catalog()
    {
        Assert.Equal(12, D.Members.Members.Count);
        Assert.Equal(72, D.Cosmetics.Cosmetics.Count);
        foreach (var slot in D.Cosmetics.Slots) Assert.Equal(12, D.Cosmetics.Cosmetics.Count(c => c.Slot == slot));
        Assert.Equal(D.Members.Members.Count, D.Members.Members.Select(m => m.Id).Distinct().Count());
        Assert.Equal(D.Cosmetics.Cosmetics.Count, D.Cosmetics.Cosmetics.Select(c => c.Id).Distinct().Count());
    }

    [Fact]
    public void Member_start_cards_exist_and_unlock_chains_match_orders()
    {
        var d = D;
        var cardIds = d.Cards.Cards.Select(c => c.Id).ToHashSet();
        foreach (var m in d.Members.Members)
        {
            if (!string.IsNullOrEmpty(m.StartCard)) Assert.True(cardIds.Contains(m.StartCard), $"{m.Id}: baslangic karti {m.StartCard} yok");
            Assert.NotEmpty(m.Effects);
            Assert.False(string.IsNullOrEmpty(m.Unlock));
        }
        // orders.json'daki uye odulleri tanimli uyeleri acar
        foreach (var c in d.Orders.Chains)
            foreach (var g in c.Grants.Where(g => g.Kind == "member"))
            {
                var m = d.Members.Member(g.Id);
                Assert.NotNull(m);
                Assert.Equal(c.Id, m!.Chain);
                Assert.Equal(g.Step, m.Step);
            }
        foreach (var c in d.Orders.Chains)
            foreach (var g in c.Grants.Where(g => g.Kind == "cosmetic" && g.Id.StartsWith("kask_") || g.Kind == "cosmetic" && g.Id.Split('_')[0] is "tulum" or "vizor" or "canta" or "capa" or "jest"))
                Assert.NotNull(d.Cosmetics.Get(g.Id));
    }

    [Fact]
    public void Starting_wardrobe_has_the_default_member_and_one_cosmetic_per_default_slot()
    {
        var w = new Wardrobe(D);
        Assert.Equal("cirak", w.Active);
        Assert.True(w.HasMember("cirak"));
        Assert.False(w.HasMember("kacak"));
        foreach (var slot in new[] { "kask", "vizor", "tulum", "capa", "jest" }) Assert.NotEqual("", w.Equipped(slot));
        Assert.Equal("", w.Equipped("canta"));
        Assert.Equal(1.1f, w.Mult("mastery_gain"), 3);
    }

    [Fact]
    public void Unlock_equip_and_active_member_change_effects()
    {
        var w = new Wardrobe(D);
        int changed = 0; w.Changed += () => changed++;
        Assert.False(w.SetMember("kacak"));           // acik degil
        Assert.True(w.Unlock("member", "kacak"));
        Assert.False(w.Unlock("member", "kacak"));
        Assert.True(w.SetMember("kacak"));
        Assert.True(w.Flag("sprint_free"));
        Assert.Equal(0.08f, w.Effect("speed"), 3);
        Assert.Equal(0f, w.Effect("max_hp"));
        Assert.Equal("cevik", w.StartCard);
        Assert.False(w.Equip("kask_hasir"));          // sahip degil
        w.Unlock("cosmetic", "kask_hasir");
        Assert.True(w.Equip("kask_hasir"));
        Assert.False(w.Equip("kask_hasir"));
        Assert.Contains("kask=kask_hasir", w.Look());
        w.Unequip("canta");
        Assert.True(changed >= 4);
    }

    [Fact]
    public void Passives_match_the_catalog_numbers()
    {
        var w = new Wardrobe(D);
        void With(string id, System.Action a) { w.Unlock("member", id); w.SetMember(id); a(); }
        With("pehlivan", () => { Assert.Equal(25f, w.Effect("max_hp")); Assert.Equal(1.3f, w.Mult("scythe_damage"), 3); });
        With("muhtar", () => { Assert.Equal(1.1f, w.Mult("income"), 3); Assert.Equal(0.9f, w.Mult("catalog"), 3); });
        With("arici", () => Assert.Equal(1.5f, w.Mult("mutation"), 3));
        With("makinist", () => Assert.Equal(1.2f, w.Mult("craft_speed"), 3));
        With("holding_firarisi", () => Assert.Equal(0.25f, w.Effect("holding_parts"), 3));
        With("gidak_kostumlu", () => Assert.True(w.Flag("glide")));
    }

    [Fact]
    public void Wardrobe_round_trips_and_ignores_stale_equipment()
    {
        var a = new Wardrobe(D);
        a.Unlock("member", "coban"); a.SetMember("coban");
        a.Unlock("cosmetic", "tulum_altin"); a.Equip("tulum_altin");
        a.Unlock("cosmetic", "duduk_kaplama"); // bilinmeyen kimlik de saklanir
        var s = a.Export();
        var b = new Wardrobe(D);
        b.Import(s);
        Assert.Equal("coban", b.Active);
        Assert.Equal("tulum_altin", b.Equipped("tulum"));
        Assert.True(b.HasCosmetic("duduk_kaplama"));
        Assert.Equal(a.Look(), b.Look());
        // yanlis slota takili kimlik yuklemede yok sayilir
        var bad = a.Export(); bad.Equipped["kask"] = "tulum_altin";
        var c = new Wardrobe(D); c.Import(bad);
        Assert.NotEqual("tulum_altin", c.Equipped("kask"));
    }

    [Fact]
    public void Mutation_multiplier_raises_the_change_chance_and_keeps_step_ratios()
    {
        var r = D.Crops.Genetics;
        int Changes(float mult)
        {
            r.MutationMult = mult;
            var rng = new Rng(7);
            int n = 0;
            for (int i = 0; i < 20000; i++) if (Genetics.MutationDelta(r, rng) != 0) n++;
            r.MutationMult = 1f;
            return n;
        }
        int baseN = Changes(1f), up = Changes(1.5f);
        Assert.InRange(up / (double)baseN, 1.4, 1.6);
    }
}
