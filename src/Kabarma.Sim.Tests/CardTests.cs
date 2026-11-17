using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M1'in 10 karti (URETIM 6 M1 Kartlar, karar 5): her kartin etkisi ICERIK 8.1'deki degerle, safakta sonme (GDD 15),
/// ilk teklifte Catal ve ilk gecede uye karti verilmemesi (GDD 24).
/// </summary>
public class CardTests
{
    static GameData Data() => TestData.Load();

    /// <summary>ICERIK 8.1: rutbe basina etki. Veri bu tabloya uymali.</summary>
    public static readonly TheoryData<string, string, float> Icerik = new()
    {
        { "catal", "projectiles", 1f },
        { "seken", "bounces", 1f },
        { "tez_el", "fireRate", 0.12f },
        { "iri_tane", "size", 0.15f },
        { "kivilcim", "burnChance", 0.15f },
        { "kiragi", "freezeChance", 0.15f },
        { "keskin_goz", "critChance", 0.08f },
        { "tokmak", "knockback", 0.30f },
        { "miknatis", "pickupRange", 0.40f },
        { "cevik", "moveSpeed", 0.08f },
    };

    [Theory]
    [MemberData(nameof(Icerik))]
    public void Card_data_matches_icerik(string id, string stat, float perRank)
    {
        var c = Data().Cards.Cards.Single(x => x.Id == id);
        Assert.Equal(stat, c.Stat);
        Assert.Equal(perRank, c.PerRank, 3);
    }

    [Fact]
    public void M1_pool_is_exactly_the_ten_cards()
    {
        var ids = Data().Cards.Cards.Select(c => c.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "catal", "cevik", "iri_tane", "keskin_goz", "kiragi", "kivilcim", "miknatis", "seken", "tez_el", "tokmak" }, ids);
    }

    /// <summary>Her kart iki rutbeyle: silah ve bedenin kullandigi sonuc deger ICERIK'teki gibi degisir.</summary>
    [Fact]
    public void Each_card_changes_its_rule_per_rank()
    {
        var d = Data();
        var w = d.Tool.Weapon;
        CombatStats Two(string id) { var n = new GameState(d).StartNight(); n.Grant(id); n.Grant(id); return n.Stats; }

        Assert.Equal(3, Two("catal").Projectiles);                                      // +1 mermi / isin kolu
        Assert.Equal(2, Two("seken").Bounces);                                          // 1 kez seker
        Assert.Equal(w.FireInterval / 1.24f, Two("tez_el").FireInterval(w.FireInterval), 4); // ates hizi +%12
        Assert.Equal(w.Damage * 1.30f, Two("iri_tane").Damage(w.Damage), 3);            // boy ve hasar +%15
        Assert.Equal(w.Width * 1.30f, Two("iri_tane").Size(w.Width), 3);
        Assert.Equal(0.30f, Two("kivilcim").BurnChance, 3);                             // %15 Yanik
        Assert.Equal(0.30f, Two("kiragi").FreezeChance, 3);                             // %15 Donma
        Assert.Equal(w.CritChance + 0.16f, Two("keskin_goz").Crit(w.CritChance), 3);    // kritik +%8
        Assert.Equal(w.Knockback * 1.60f, Two("tokmak").Knockback(w.Knockback), 3);     // itme +%30
        Assert.Equal(4f * 1.80f, Two("miknatis").PickupRadius(4f), 3);                  // toplama menzili +%40
        Assert.Equal(5f * 1.16f, Two("cevik").MoveSpeed(5f), 3);                        // hiz +%8
    }

    [Fact]
    public void Cards_do_not_touch_other_stats()
    {
        var d = Data();
        foreach (var c in d.Cards.Cards)
        {
            var n = new GameState(d).StartNight();
            n.Grant(c.Id);
            var s = n.Stats;
            int changed = new[]
            {
                s.Projectiles != 1, s.Bounces != 0, s.FireRateMult != 1f, s.SizeMult != 1f, s.BurnChance != 0f,
                s.FreezeChance != 0f, s.CritChance != 0f, s.KnockbackMult != 1f, s.PickupRangeMult != 1f, s.MoveSpeedMult != 1f,
            }.Count(x => x);
            Assert.True(changed == 1, $"{c.Id} {changed} degeri degistirdi");
        }
    }

    [Fact]
    public void Burn_and_freeze_rolls_follow_the_card_chance()
    {
        var d = Data();
        var n = new GameState(d).StartNight();
        Assert.DoesNotContain(true, Enumerable.Range(0, 200).Select(_ => n.RollBurn() || n.RollFreeze())); // kartsiz: hic
        for (int i = 0; i < 5; i++) { n.Grant("kivilcim"); n.Grant("kiragi"); }                           // 5. rutbe: %75
        int burns = Enumerable.Range(0, 2000).Count(_ => n.RollBurn());
        int freezes = Enumerable.Range(0, 2000).Count(_ => n.RollFreeze());
        Assert.InRange(burns, 1350, 1650);
        Assert.InRange(freezes, 1350, 1650);
    }

    [Fact]
    public void Rank_is_capped_at_five()
    {
        var n = new GameState(Data()).StartNight();
        for (int i = 0; i < 7; i++) n.Grant("catal");
        Assert.Equal(5, n.Rank("catal"));
        Assert.Equal(6, n.Stats.Projectiles);
        Assert.DoesNotContain(n.RollOffer(), c => c.Id == "catal");
    }

    /// <summary>Denetimde bulunan hata: kart etkileri safakta sifirlanmiyordu (gunduz Capa'si kartli atiyordu).</summary>
    [Fact]
    public void Cards_fade_at_dawn()
    {
        var d = Data();
        var s = new GameState(d);
        var n = s.StartNight();
        foreach (var c in d.Cards.Cards) n.Grant(c.Id);
        Assert.False(n.Stats.IsBase);
        RunToDawn(n, d);
        Assert.True(n.Stats.IsBase);
        Assert.All(d.Cards.Cards, c => Assert.Equal(0, n.Rank(c.Id)));
        // Rapor ve sonuc satiri safaktan sonra da okunur: liste ve sayac korunur.
        Assert.Equal(d.Cards.Cards.Count, n.CardsAtDawn.Count);
        Assert.True(n.CardsCount >= d.Cards.Cards.Count);
        // Safaktan sonra isabet tabandir (gunduz).
        var w = d.Tool.Weapon;
        var (dmg, _) = n.RollHit(w, false, 1f);
        Assert.True(dmg == MathF.Round(w.Damage) || dmg == MathF.Round(w.Damage * w.CritMult));
        Assert.Null(n.Grant("catal"));
    }

    [Fact]
    public void First_offer_always_has_catal_first()
    {
        var d = Data();
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var n = new KabarmaNight(d, d.Kabarma.FirstNight, seed);
            LevelUp(n, d);
            Assert.NotNull(n.Offer);
            Assert.Equal("catal", n.Offer!.Cards[0].Id);
            Assert.Equal(3, n.Offer.Cards.Select(c => c.Id).Distinct().Count());
        }
    }

    [Fact]
    public void Only_the_first_offer_is_forced()
    {
        var d = Data();
        int catalLater = 0, offers = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var n = new KabarmaNight(d, d.Kabarma.FirstNight, seed);
            LevelUp(n, d);
            n.Choose(1); // Catal'i alma
            LevelUp(n, d);
            if (n.Offer == null) continue;
            offers++;
            if (n.Offer.Cards.Any(c => c.Id == "catal")) catalLater++;
        }
        Assert.True(offers > 0);
        Assert.True(catalLater < offers, "ikinci teklif de hep Catal iceriyor: kural yalniz ilk teklif icin");
    }

    /// <summary>Teklifler havuzun basina yigilmamali: ilk sira 10 kart arasinda dagilir (kucuk tohum hatasi).</summary>
    [Fact]
    public void Offers_are_spread_over_the_pool()
    {
        var d = Data();
        var firsts = new Dictionary<string, int>();
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var n = new KabarmaNight(d, d.Kabarma.FirstNight, seed);
            var o = n.RollOffer();
            firsts[o[0].Id] = firsts.GetValueOrDefault(o[0].Id) + 1;
        }
        Assert.Equal(d.Cards.Cards.Count, firsts.Count);
        Assert.True(firsts.Values.Max() < 70, "ilk sira tek karta yigiliyor: " + string.Join(", ", firsts.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    [Fact]
    public void First_night_does_not_grant_the_member_start_card()
    {
        var d = Data();
        Assert.False(d.Kabarma.FirstNight.MemberStartCard);
        var n = new GameState(d).StartNight(memberCard: "tokmak");
        Assert.Equal(0, n.Rank("tokmak"));
        Assert.True(n.Stats.IsBase);
        Assert.Equal(0, n.CardsCount);
    }

    [Fact]
    public void A_night_that_allows_it_grants_the_member_card()
    {
        var d = Data();
        var def = new NightDef { Ks = 1, AlarmSeconds = 1, Waves = d.Kabarma.FirstNight.Waves, MaxCardLevels = 3, Enemy = "zipzip", MemberStartCard = true };
        var n = new KabarmaNight(d, def, 7, memberCard: "tokmak");
        Assert.Equal(1, n.Rank("tokmak"));
        Assert.Equal(1.30f, n.Stats.KnockbackMult, 3);
    }

    [Fact]
    public void Auto_pick_takes_catal_on_the_first_offer()
    {
        var d = Data();
        var n = new KabarmaNight(d, d.Kabarma.FirstNight, 99);
        LevelUp(n, d);
        n.Tick(d.Cards.AutoPickSeconds + 0.1f);
        Assert.True(n.ShouldAutoPick);
        Assert.Equal("catal", n.Choose(0)!.Id);
        Assert.Equal(2, n.Stats.Projectiles);
    }

    /// <summary>Bir seviye atlatacak kadar Oz verir (oldurmeyle).</summary>
    internal static void LevelUp(KabarmaNight n, GameData d)
    {
        var zip = d.Creature("zipzip");
        int lv = n.Level;
        for (int i = 0; i < 200 && n.Level == lv; i++) n.OnKill(zip);
    }

    internal static void RunToDawn(KabarmaNight n, GameData d)
    {
        for (float t = 0; t < 900 && n.Phase != NightPhase.Dawn; t += 0.5f)
        {
            n.Tick(0.5f);
            n.SpawnDue();
            n.WaveCleared();
        }
        Assert.Equal(NightPhase.Dawn, n.Phase);
    }
}
