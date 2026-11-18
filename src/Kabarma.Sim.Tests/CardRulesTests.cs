using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>GDD 15.1: havuz, yenileme, surgun, 12 yuva, Nisan, jeton, acilma, ekip kartlari, kayit.</summary>
public class CardRulesTests
{
    static readonly GameData D = TestData.Load();

    [Fact]
    public void Catalog_matches_icerik_counts()
    {
        var by = D.Cards.Cards.GroupBy(c => c.Class).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(20, by["capa"]); Assert.Equal(12, by["yorunge"]); Assert.Equal(10, by["beden"]);
        Assert.Equal(12, by["ciftlik"]); Assert.Equal(6, by["risk"]); Assert.Equal(12, by["asi"]);
        var un = D.Cards.Cards.GroupBy(c => c.Unlock).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(20, un["start"]); Assert.Equal(22, un["is_emri"]); Assert.Equal(4, un["uye"]);
        Assert.Equal(6, un["ata"]); Assert.Equal(8, un["jeton"]); Assert.Equal(12, un["asi"]);
        Assert.All(D.Cards.JetonPool, id => Assert.Equal("jeton", D.Cards.Card(id).Unlock));
        Assert.All(D.Cards.Cards.Where(c => c.IsAsi), a =>
        {
            Assert.Equal(2, a.Parents.Count);
            Assert.All(a.Parents, p => Assert.False(D.Cards.Card(p).IsAsi));
        });
        Assert.All(D.Cards.Cards.Where(c => c.Class == "ciftlik"), c => Assert.True(c.Team));
    }

    [Fact]
    public void Locked_cards_and_asi_never_roll_as_normal_offers()
    {
        var open = new CardBook(D.Cards).Open.Where(c => !c.IsAsi).Select(c => c.Id).ToHashSet();
        Assert.Equal(20, open.Count);
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var n2 = new KabarmaNight(D, D.Kabarma.FirstNight, seed, book: new CardBook(D.Cards));
            Assert.All(n2.RollOffer(), c => Assert.Contains(c.Id, open));
        }
    }

    [Fact]
    public void Unlocking_adds_to_the_pool_and_asi_opens_by_itself()
    {
        var b = new CardBook(D.Cards);
        Assert.False(b.IsUnlocked("agu"));
        Assert.False(b.IsUnlocked("esek_arisi")); // Ari Kovani acik, Agu kapali
        Assert.True(b.Unlock("agu", "ata"));
        Assert.False(b.Unlock("agu", "ata"));
        Assert.True(b.IsUnlocked("esek_arisi"));
        Assert.Throws<InvalidOperationException>(() => b.Unlock("esek_arisi", "x"));
        Assert.True(b.IsUnlocked("sacmalik")); // Catal + Seken bastan acik
    }

    [Fact]
    public void Reroll_redraws_and_is_limited_per_night()
    {
        var n = new GameState(D).StartNight();
        CardTests.LevelUp(n, D);
        Assert.Equal(2, n.Hand.RerollsLeft);
        n.Tick(10f);
        Assert.True(n.Hand.Reroll());
        Assert.Equal(0f, n.Offer!.Age);
        Assert.Equal(3, n.Offer.Cards.Count);
        Assert.True(n.Hand.Reroll());
        Assert.False(n.Hand.Reroll());
        Assert.Equal(0, n.Hand.RerollsLeft);
    }

    [Fact]
    public void Reroll_keeps_an_asi_offer()
    {
        var n = new GameState(D).StartNight();
        for (int i = 0; i < 5; i++) { n.Grant("catal"); n.Grant("seken"); }
        CardTests.LevelUp(n, D);
        Assert.Equal("sacmalik", n.Offer!.Cards[0].Id);
        n.Hand.Reroll();
        Assert.Equal("sacmalik", n.Offer!.Cards[0].Id);
        Assert.Equal(1, n.Offer.Cards.Count(c => c.Id == "sacmalik"));
    }

    [Fact]
    public void Banish_removes_the_card_for_the_night_only()
    {
        var s = new GameState(D);
        var n = s.StartNight();
        CardTests.LevelUp(n, D);
        n.Choose(0); // Catal
        CardTests.LevelUp(n, D);
        var o = n.Offer!;
        int heldIdx = o.Cards.ToList().FindIndex(c => n.Rank(c.Id) > 0);
        if (heldIdx >= 0) Assert.False(n.Hand.Banish(heldIdx)); // eldeki kart surgun edilmez
        int idx = o.Cards.ToList().FindIndex(c => n.Rank(c.Id) == 0);
        var gone = o.Cards[idx].Id;
        Assert.True(n.Hand.Banish(idx));
        Assert.DoesNotContain(n.Offer!.Cards, c => c.Id == gone);
        Assert.DoesNotContain(n.Hand.Pool(), c => c.Id == gone);
        for (int i = 0; i < 300; i++) Assert.DoesNotContain(n.RollOffer(), c => c.Id == gone);
        Assert.Equal(1, n.Hand.BanishesLeft);
        CardTests.RunToDawn(n, D);
        var next = s.StartNight();
        Assert.Contains(next.Hand.Pool(), c => c.Id == gone);
        Assert.Equal(2, next.Hand.BanishesLeft);
    }

    [Fact]
    public void Twelve_slots_then_only_held_cards_are_offered()
    {
        var s = new GameState(D);
        var n = s.StartNight();
        var twelve = s.CardBook.Open.Where(c => !c.IsAsi).Take(12).Select(c => c.Id).ToList();
        foreach (var id in twelve) n.Grant(id);
        Assert.Equal(12, n.Hand.SlotsUsed);
        for (int i = 0; i < 200; i++) Assert.All(n.RollOffer(), c => Assert.Contains(c.Id, twelve));
        foreach (var id in twelve) while (n.Grant(id) != null) { }
        Assert.Empty(n.Hand.Pool()); // hepsi tavanda: yalniz hazir asilar sunulur, yoksa seviye bosa
        int lv = n.Level;
        CardTests.LevelUp(n, D);
        Assert.True(n.Level > lv);
        Assert.True(n.Offer == null || n.Offer.Cards.All(c => c.IsAsi && n.Hand.AsiReady().Contains(c)));
    }

    [Fact]
    public void Nisan_points_levels_bonus_and_start_rank()
    {
        var s = new GameState(D);
        var b = s.CardBook;
        for (int night = 0; night < 3; night++)
        {
            var n = s.StartNight();
            for (int i = 0; i < 5; i++) n.Grant("tez_el");
            n.Grant("catal"); // tavanda degil: puan yok
            CardTests.RunToDawn(n, D);
        }
        Assert.Equal(3, b.Points("tez_el"));
        Assert.Equal(0, b.Points("catal"));
        Assert.Equal(1, b.Level("tez_el"));
        var n2 = s.StartNight();
        n2.Grant("tez_el");
        Assert.Equal(1f + 0.12f * 1.04f, n2.Stats.FireRateMult, 4); // %4 Nisan 1
        for (int i = 0; i < 37; i++) b.AddPoint("tez_el");
        Assert.Equal(5, b.Level("tez_el"));
        var n3 = s.StartNight();
        n3.Grant("tez_el");
        Assert.Equal(2, n3.Rank("tez_el")); // Nisan 5: 2. rutbeden baslar
        for (int i = 0; i < 40; i++) b.AddPoint("catal");
        var n4 = s.StartNight();
        n4.Grant("catal");
        Assert.Equal(1 + 2, n4.Stats.Projectiles); // tam sayi etki Nisan'la artmaz, rutbe 2
    }

    [Fact]
    public void Jetons_open_the_pool_then_give_nisan_points()
    {
        var b = new CardBook(D.Cards);
        Assert.False(b.SpendJeton("delici"));
        b.AddJeton(9);
        Assert.False(b.SpendJeton("agu")); // havuzda degil
        foreach (var id in D.Cards.JetonPool) Assert.True(b.SpendJeton(id));
        Assert.All(D.Cards.JetonPool, id => Assert.True(b.IsUnlocked(id)));
        Assert.Empty(b.JetonChoices);
        Assert.True(b.SpendJeton("catal"));
        Assert.Equal(1, b.Points("catal"));
        Assert.Equal(0, b.Jetons);
    }

    [Fact]
    public void Team_cards_take_the_highest_rank_and_do_not_stack()
    {
        var a = new Dictionary<string, int> { ["kule_asiri_yuku"] = 2, ["catal"] = 3 };
        var b = new Dictionary<string, int> { ["kule_asiri_yuku"] = 4 };
        var team = CardTeam.Ranks(D, new IReadOnlyDictionary<string, int>[] { a, b });
        Assert.Equal(4, team["kule_asiri_yuku"]);
        Assert.False(team.ContainsKey("catal"));
        Assert.Equal(0.48f, CardTeam.Mod(D, team, "towerFireRate"), 3);
    }

    [Fact]
    public void Card_book_survives_save_and_load()
    {
        var s = new GameState(D);
        s.CardBook.Unlock("agu", "ata");
        s.CardBook.AddJeton(2);
        s.CardBook.SpendJeton("delici");
        for (int i = 0; i < 9; i++) s.CardBook.AddPoint("catal");
        var save = s.CardBook.Export();
        var t = new GameState(D);
        t.CardBook.Import(save);
        Assert.True(t.CardBook.IsUnlocked("agu"));
        Assert.True(t.CardBook.IsUnlocked("delici"));
        Assert.True(t.CardBook.IsUnlocked("esek_arisi"));
        Assert.Equal(1, t.CardBook.Jetons);
        Assert.Equal(2, t.CardBook.Level("catal"));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(save), System.Text.Json.JsonSerializer.Serialize(t.CardBook.Export()));
    }
}
