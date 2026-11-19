using System.Linq;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M4 ilerleme4: ICERIK §18 zincirleri #13-24 ve #37-48; olcu tanimlari, odul kimlikleri, her zincirin tamamlanabilmesi, katalog ve cark.</summary>
public class Ilerleme4Tests
{
    static readonly string[] New24 =
    {
        "usta", "elektrikci", "otomasyoncu", "kalfa", "mantikci", "mimar", "gozcu", "kasif", "magaraci", "ansiklopedist", "kadim_yol", "enkazci",
        "tuccar", "borsaci", "cesitci", "carkci", "gurme", "koleksiyoncu", "can_dostu", "ucan_uye", "ezilenler_kulubu", "nakliyeci", "sofor", "fotografci",
    };
    static readonly string[] ExtraCosmetics = { "duduk_kaplama", "altin_eyer", "altin_drone", "planor_deseni", "altin_cark", "altin_cerceve" };

    static GameData D => TestData.Load();

    [Fact]
    public void All_24_chains_exist_with_five_increasing_steps_and_known_measures()
    {
        var d = D;
        foreach (var id in New24)
        {
            var c = d.Orders.Chains.FirstOrDefault(x => x.Id == id);
            Assert.NotNull(c);
            Assert.Equal(5, c!.Steps.Count);
            Assert.Equal(c.Steps.Select(s => s.Target).OrderBy(x => x), c.Steps.Select(s => s.Target));
            Assert.Equal(5, c.Steps.Select(s => s.Target).Distinct().Count());
            Assert.All(c.Steps, s => Assert.True(d.Orders.Measures.ContainsKey(s.Measure), $"{id}: olcu {s.Measure} tanimsiz"));
            Assert.NotEmpty(c.Grants);
        }
    }

    [Fact]
    public void Chain_rewards_point_at_real_cards_members_and_cosmetics()
    {
        var d = D;
        var cards = d.Cards.Cards.Select(c => c.Id).ToHashSet();
        foreach (var c in d.Orders.Chains)
            foreach (var g in c.Grants)
                switch (g.Kind)
                {
                    case "card": case "nisan": Assert.True(cards.Contains(g.Id), $"{c.Id}: kart {g.Id} yok"); break;
                    case "member": Assert.NotNull(d.Members.Member(g.Id)); break;
                    case "cosmetic": Assert.True(d.Cosmetics.Get(g.Id) != null || ExtraCosmetics.Contains(g.Id), $"{c.Id}: kozmetik {g.Id} yok"); break;
                }
        // dolap uyeleri: zincirleri artik var (Nine ve Holding Firarisi M3 payi zincirlerinde)
        foreach (var m in d.Members.Members.Where(m => m.Chain != "start" && m.Chain is not ("gece_bekcisi" or "holding_belasi")))
        {
            var chain = d.Orders.Chains.FirstOrDefault(c => c.Id == m.Chain);
            Assert.NotNull(chain);
            Assert.Contains(chain!.Grants, g => g.Kind == "member" && g.Id == m.Id && g.Step == m.Step);
        }
    }

    [Fact]
    public void Every_new_chain_completes_when_its_measure_is_reported()
    {
        var d = D;
        foreach (var id in New24)
        {
            var s = new GameState(d);
            s.Orders.OpenDefter();
            var chain = s.Orders.Chain(id)!;
            foreach (var step in chain.Def.Steps)
            {
                string mode = d.Orders.Measures[step.Measure];
                if (mode == "sum") s.Orders.Report(step.Measure, step.Target - chain.Count(step.Measure));
                else if (mode == "peak") s.Orders.Level(step.Measure, step.Target);
                else for (int i = chain.Count(step.Measure); i < step.Target; i++) s.Orders.ReportKey(step.Measure, $"{step.Measure}_{i}");
            }
            Assert.True(chain.Done, $"{id} tamamlanmadi");
            foreach (var g in chain.Def.Grants) Assert.Contains($"{g.Kind}:{g.Id}", s.Orders.Granted);
        }
    }

    [Fact]
    public void Member_rewards_unlock_the_member_in_the_wardrobe()
    {
        var d = D;
        var s = new GameState(d);
        s.Orders.GrantMade += (_, g) => { if (g.Kind is "member" or "cosmetic") s.Wardrobe.Unlock(g.Kind, g.Id); };
        s.Orders.OpenDefter();
        s.Orders.Report("machine_items", 500_000);            // otomasyoncu: 4. adimda Robot Kahya
        Assert.True(s.Wardrobe.HasMember("robot_kahya"));
        Assert.True(s.Wardrobe.HasCosmetic("altin_drone"));
        for (int i = 0; i < 12; i++) s.Orders.ReportKey("food_kind", $"yemek{i}"); // gurme: 2. adimda Asci
        Assert.True(s.Wardrobe.HasMember("asci"));
        s.Orders.Report("credits_earned", 100_000);           // tuccar: 3. adimda Muhtar
        Assert.True(s.Wardrobe.HasMember("muhtar"));
        Assert.True(s.Wardrobe.SetMember("muhtar"));
        Assert.Equal(1.1f, s.Wardrobe.Mult("income"), 3);
    }

    [Fact]
    public void Catalog_sells_thirty_six_cosmetics_priced_500_to_5000_and_unlocks_them()
    {
        var d = D;
        var cosEntries = d.Catalog.Entries.Where(e => e.Cosmetic.Length > 0).ToList();
        Assert.Equal(36, cosEntries.Count);
        Assert.All(cosEntries, e => { Assert.InRange(e.Price, 500, 5000); Assert.NotNull(d.Cosmetics.Get(e.Cosmetic)); Assert.Equal("catalog", d.Cosmetics.Get(e.Cosmetic)!.Source); });
        Assert.Equal(36, d.Cosmetics.Cosmetics.Count(c => c.Source == "catalog"));
        // her slotta en az bir katalog parcasi, 72'nin kalani baska yoldan gelir
        Assert.All(d.Cosmetics.Slots, slot => Assert.Contains(d.Cosmetics.Cosmetics, c => c.Slot == slot && c.Source == "catalog"));

        var s = new GameState(d);
        var e0 = cosEntries[0];
        string got = "";
        s.Catalog.CosmeticBought += id => { got = id; s.Wardrobe.Unlock("cosmetic", id); };
        s.Catalog.OwnsCosmetic = id => s.Wardrobe.HasCosmetic(id);
        var dl = s.Catalog.Buy(e0.Id, _ => true);
        Assert.NotNull(dl);
        Assert.Equal(e0.Cosmetic, got);
        Assert.True(s.Wardrobe.HasCosmetic(e0.Cosmetic));
        Assert.DoesNotContain(e0, s.Catalog.Visible);          // sahip olunan kalem gizlenir
        Assert.Empty(s.Catalog.Deliveries);                    // teslimat yok
    }

    [Fact]
    public void Wheel_cosmetic_pick_returns_an_unowned_wheel_or_catalog_piece()
    {
        var d = D;
        var w = new Wardrobe(d);
        var rng = new Rng(3);
        for (int i = 0; i < 20; i++)
        {
            var id = w.WheelPick(rng);
            Assert.NotNull(id);
            Assert.False(w.HasCosmetic(id!));
            Assert.Contains(d.Cosmetics.Get(id!)!.Source, new[] { "wheel", "catalog" });
            w.Unlock("cosmetic", id!);
        }
    }

    [Fact]
    public void Wheel_expected_value_still_matches_the_design_target()
    {
        var w = D.Wheel;
        double total = w.Rewards.Sum(r => r.Weight);
        double ev = w.Rewards.Sum(r => r.Weight / total * (r.Kind == "credits" ? r.Amounts.Average() : r.Value));
        Assert.InRange(ev, 410, 440);   // ICERIK §18: 425 Kredi = 500'un %85'i
    }
}