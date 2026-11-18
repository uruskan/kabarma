using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// URETIM M3 kabulu: her kartin ve her asinin senaryo testi. Her senaryo karti gercek gece yolundan alir
/// (Grant ya da teklif) ve kuralin sonucunu olcer: atis plani, isabet, oldurme, alinan hasar, zaman, dalga, safak.
/// Kapsam testi cards.json'daki her kimligin burada bir senaryosu oldugunu denetler.
/// </summary>
public class CardScenarioTests
{
    static readonly GameData D = TestData.Load();

    /// <summary>Butun kartlari acik bir gece; "id*3" uc rutbe verir.</summary>
    static KabarmaNight Night(params string[] grants)
    {
        var s = new GameState(D);
        foreach (var c in D.Cards.Cards.Where(c => !c.IsAsi)) s.CardBook.Unlock(c.Id, "test");
        var n = s.StartNight();
        foreach (var g in grants)
        {
            var parts = g.Split('*');
            int k = parts.Length > 1 ? int.Parse(parts[1]) : 1;
            for (int i = 0; i < k; i++) n.Grant(parts[0]);
        }
        return n;
    }

    static CardDef C(string id) => D.Cards.Card(id);
    static WeaponDef W => D.Tool.Weapon;

    static int Count(KabarmaNight n, int tries, Func<HitResult, bool> f) =>
        Enumerable.Range(0, tries).Count(_ => f(n.Hand.RollHit(W, false, 1f)));

    static void Chance(string id, Func<HitResult, bool> f)
    {
        Assert.Equal(0, Count(Night(), 1000, f));
        int hits = Count(Night(id + "*2"), 4000, f); // %30
        Assert.InRange(hits, 1000, 1400);
    }

    /// <summary>Yorunge silahi: 10 sn'de aralik basina bir ates, hasar ve yaricap veriden; 2. rutbe buyur.</summary>
    static void Orbital(string id, int rank = 1, float seconds = 10f)
    {
        var c = C(id);
        Assert.Empty(Night().Hand.Tick(seconds));
        var n = Night($"{id}*{rank}");
        var fires = new List<OrbitalFire>();
        for (float t = 0; t < seconds - 1e-3f; t += 0.1f) fires.AddRange(n.Hand.Tick(0.1f));
        int expected = (int)MathF.Floor(seconds / c.Param("interval") + 1e-3f);
        Assert.InRange(fires.Count, expected - 1, expected);
        Assert.All(fires, f => Assert.Equal(c.Param("damage") * (1 + c.Param("growth") * (rank - 1)), f.Damage, 3));
        Assert.All(fires, f => Assert.Equal((int)c.Param("count", 1), f.Count));
        Assert.All(fires, f => Assert.True(f.Radius > 0));
    }

    /// <summary>Asi: iki ebeveyn 5. rutbede, bir sonraki teklifin ilki asi; alininca iki yuva bir olur.</summary>
    static KabarmaNight Asi(string id)
    {
        var a = C(id);
        var n = Night(a.Parents.Select(p => $"{p}*5").ToArray());
        Assert.Equal(0, n.Rank(id));
        int slots = n.Hand.SlotsUsed;
        CardTests.LevelUp(n, D);
        Assert.NotNull(n.Offer);
        Assert.Equal(id, n.Offer!.Cards[0].Id);
        Assert.Equal(id, n.Choose(0)!.Id);
        Assert.Equal(1, n.Rank(id));
        Assert.Equal(slots - 1, n.Hand.SlotsUsed);
        Assert.All(a.Parents, p => Assert.True(n.Hand.MergedIntoAsi(p)));
        Assert.Null(n.Grant(id)); // tek rutbe
        return n;
    }

    static float Team(string id, int rank, string stat)
    {
        var n = Night($"{id}*{rank}");
        Assert.True(C(id).Team);
        return CardTeam.Mod(D, n.Hand, stat);
    }

    static float OzOf(KabarmaNight n) => n.OnKill(D.Creature("zipzip"), 0, 0).Value;

    public static readonly Dictionary<string, Action> Scenarios = new()
    {
        // ---- Capa ----
        ["catal"] = () => { Assert.Equal(1, Night().Hand.NextShot().Projectiles); Assert.Equal(3, Night("catal*2").Hand.NextShot().Projectiles); },
        ["seken"] = () => Assert.Equal(1, Night("seken").Hand.NextShot().Bounces),
        ["tez_el"] = () => Assert.Equal(W.FireInterval / 1.12f, Night("tez_el").Stats.FireInterval(W.FireInterval), 4),
        ["iri_tane"] = () =>
        {
            var n = Night("iri_tane");
            Assert.Equal(11.5f, n.Stats.Damage(10f), 3);
            Assert.Equal(W.Width * 1.15f, n.Stats.Size(W.Width), 3);
        },
        ["kivilcim"] = () => Chance("kivilcim", h => h.Burn),
        ["kiragi"] = () => Chance("kiragi", h => h.Freeze),
        ["keskin_goz"] = () =>
        {
            int a = Count(Night(), 4000, h => h.Crit), b = Count(Night("keskin_goz*3"), 4000, h => h.Crit);
            Assert.InRange(b - a, 700, 1250); // +%24
        },
        ["tokmak"] = () => Assert.Equal(13f, Night("tokmak").Stats.Knockback(10f), 3),
        ["miknatis"] = () =>
        {
            var n0 = Night(); n0.OnKill(D.Creature("zipzip"), 5f, 0); Assert.Equal(0, n0.CollectNear(0, 0));
            var n = Night("miknatis"); n.OnKill(D.Creature("zipzip"), 5f, 0); Assert.Equal(1, n.CollectNear(0, 0)); // 4 m x 1,4
        },
        ["cevik"] = () => Assert.Equal(1.08f, Night("cevik").Hand.SpeedMult, 3),
        ["delici"] = () => { Assert.Equal(0, Night().Hand.NextShot().Pierce); Assert.Equal(2, Night("delici*2").Hand.NextShot().Pierce); },
        ["uzun_namlu"] = () => Assert.Equal(1.2f, Night("uzun_namlu").Hand.NextShot().RangeMult, 3),
        ["cereyan"] = () => Chance("cereyan", h => h.Shock),
        ["agu"] = () => Chance("agu", h => h.Poison),
        ["patlar_uc"] = () =>
        {
            var n = Night("patlar_uc");
            var shots = Enumerable.Range(0, 10).Select(_ => n.Hand.NextShot()).ToList();
            Assert.Equal(new[] { 4, 9 }, shots.Select((s, i) => (s, i)).Where(x => x.s.Explodes).Select(x => x.i));
            Assert.Equal(0.5f, shots[4].ExplodeDamage, 3);
        },
        ["emici"] = () => { var h = Night("emici").Hand.RollHit(W, false, 1f); Assert.Equal(h.Damage * 0.02f, h.Heal, 3); },
        ["maya_ucu"] = () => { Assert.Equal(0f, Night().Hand.RollHit(W, false, 1f).Swell); Assert.Equal(0.3f, Night("maya_ucu*2").Hand.RollHit(W, false, 1f).Swell, 3); },
        ["geri_sayim"] = () =>
        {
            var n = Night("geri_sayim");
            n.OnKill(D.Creature("zipzip"));
            Assert.Equal(0.5f, n.LastKill.BlastDamage, 3);
            Assert.Equal(1f, n.LastKill.BlastDelay, 3);
        },
        ["ikiz_baslik"] = () => Assert.Equal(0.4f, Night("ikiz_baslik").Hand.NextShot().TwinPower, 3),
        ["yanki"] = () => { var s = Night("yanki").Hand.NextShot(); Assert.Equal(0.3f, s.EchoPower, 3); Assert.Equal(0.5f, s.EchoDelay, 3); },
        ["altin_tetik"] = () =>
        {
            var n = Night("altin_tetik");
            Assert.Equal(1f, n.Hand.DamageMult(), 4);
            for (int i = 0; i < 20; i++) n.OnKill(D.Creature("zipzip"));
            Assert.Equal(1.10f, n.Hand.DamageMult(), 3); // 20 x %0,5
            Assert.Equal(1f, Night().Hand.DamageMult(), 4);
        },

        // ---- Yorunge ----
        ["ari_kovani"] = () => { Orbital("ari_kovani"); Orbital("ari_kovani", 3); },
        ["tirpan_carki"] = () => Orbital("tirpan_carki", 2),
        ["simsek_bulutu"] = () => { Orbital("simsek_bulutu"); Assert.Equal(2f, C("simsek_bulutu").Param("chain")); },
        ["fiskiye"] = () => Orbital("fiskiye"),
        ["gubre_mayini"] = () => { Orbital("gubre_mayini"); Assert.Equal(1f, C("gubre_mayini").Param("behind")); },
        ["yumurta_topu"] = () => Orbital("yumurta_topu"),
        ["korkuluk_dronu"] = () => { Orbital("korkuluk_dronu"); Assert.Equal(1f, C("korkuluk_dronu").Param("taunt")); },
        ["minik_bicer"] = () => Orbital("minik_bicer"),
        ["sinek_kapan"] = () => { Orbital("sinek_kapan", 1, 16f); Assert.Equal(1f, C("sinek_kapan").Param("devour")); },
        ["ay_parcasi"] = () => Orbital("ay_parcasi", 2),
        ["sadik_golge"] = () => { Assert.Equal(0f, Night().Hand.ShadowPower); Assert.Equal(0.5f, Night("sadik_golge").Hand.ShadowPower, 3); },
        ["kooperatif_uydusu"] = () => Orbital("kooperatif_uydusu", 1, 30f),

        // ---- Beden ----
        ["yayli_taban"] = () => Assert.Equal(2, Night("yayli_taban*2").Hand.ExtraJumps),
        ["saglam"] = () => Assert.Equal(120f, Night("saglam").Hand.MaxHp(100f), 3),
        ["kabuk"] = () =>
        {
            var h = Night("kabuk").Hand;
            Assert.Equal(30f, h.Shield, 3);
            var g = h.Guard(20f); Assert.Equal(20f, g.Absorbed, 3); Assert.Equal(0f, g.Taken, 3);
            g = h.Guard(20f); Assert.Equal(10f, g.Absorbed, 3); Assert.Equal(10f, g.Taken, 3);
            h.Tick(9f); Assert.Equal(0f, h.Shield, 3);
            h.Tick(1.1f); Assert.Equal(30f, h.Shield, 3); // 10 sn'de yenilenir
        },
        ["yenilenme"] = () => Assert.Equal(2f, Night("yenilenme*2").Hand.RegenPerSecond, 3),
        ["kabarik_beden"] = () =>
        {
            var h = Night("kabarik_beden").Hand;
            Assert.Equal(1.15f, h.BodyScale, 3);
            Assert.Equal(110f, h.MaxHp(100f), 3);
            Assert.Equal(1.10f, h.DamageMult(), 3);
            Assert.Equal(0.96f, h.SpeedMult, 3);
        },
        ["ufak_tefek"] = () =>
        {
            var h = Night("ufak_tefek").Hand;
            Assert.Equal(0.88f, h.BodyScale, 3);
            Assert.Equal(1.10f, h.SpeedMult, 3);
            int dodged = Enumerable.Range(0, 4000).Count(_ => h.Guard(5f).Dodged);
            Assert.InRange(dodged, 220, 420); // %8
        },
        ["dikenli"] = () => { var g = Night("dikenli").Hand.Guard(10f); Assert.Equal(5f, g.Reflected, 3); Assert.Equal(10f, g.Taken, 3); },
        ["ikinci_nefes"] = () =>
        {
            Assert.False(Night().Hand.TryRevive());
            var h = Night("ikinci_nefes").Hand;
            Assert.True(h.TryRevive());
            Assert.False(h.TryRevive());
        },
        ["maya_sarhosu"] = () =>
        {
            var g = D.Kabarma.Gosteri;
            var n = Night("maya_sarhosu");
            for (int i = 0; i < 400; i++) n.OnKill(D.Creature("zipzip"));
            Assert.Equal(g.Max + 1f, n.Gosteri, 3);
            var b = Night(); for (int i = 0; i < 400; i++) b.OnKill(D.Creature("zipzip"));
            Assert.Equal(g.Max, b.Gosteri, 3);
            float n0 = n.Gosteri, b0 = b.Gosteri;
            n.Tick(1f); b.Tick(1f);
            Assert.Equal((b0 - b.Gosteri) * 0.5f, n0 - n.Gosteri, 3); // yari hizda duser
        },

        // ---- Ciftlik (ekip) ----
        ["suru_cobani"] = () => Assert.Equal(0.30f, Team("suru_cobani", 2, "animalDamage"), 3),
        ["kule_asiri_yuku"] = () => Assert.Equal(0.24f, Team("kule_asiri_yuku", 2, "towerFireRate"), 3),
        ["bereket"] = () => Assert.Equal(1f, Team("bereket", 1, "boyBonus"), 3),
        ["dikenli_cit"] = () => Assert.Equal(0.5f, Team("dikenli_cit", 1, "wallThorns"), 3),
        ["tamirci"] = () => Assert.Equal(0.01f, Team("tamirci", 2, "structureRegen"), 4),
        ["ortak_kasa"] = () =>
        {
            Assert.Equal(0.06f, Team("ortak_kasa", 1, "ozGain"), 3);
            Assert.Equal(OzOf(Night()) * 1.06f, OzOf(Night("ortak_kasa")), 3);
        },
        ["korkuluk_ordusu"] = () => Assert.Equal(1f, Team("korkuluk_ordusu", 1, "scarecrowsFight"), 3),
        ["gece_vardiyasi"] = () => Assert.Equal(1f, Team("gece_vardiyasi", 1, "workAnimalsFight"), 3),
        ["kazan_kapagi"] = () => Assert.Equal(0.40f, Team("kazan_kapagi", 2, "kazanHp"), 3),
        ["hasat_senligi"] = () =>
        {
            var h = Night("hasat_senligi").Hand;
            Assert.Equal(1f, h.DamageMult(), 4);
            h.OnCropSwell();
            Assert.Equal(1.10f, h.DamageMult(), 3);
            Assert.Equal(1.10f, h.SpeedMult, 3);
            h.Tick(5.1f);
            Assert.Equal(1f, h.DamageMult(), 4);
        },
        ["yedek_aku"] = () => Assert.Equal(30f, Team("yedek_aku", 1, "towerBackup"), 3),
        ["alaca_sansi"] = () =>
        {
            var n = Night("alaca_sansi*3");
            Assert.Equal(1, n.Rank("alaca_sansi")); // tavan 1
            Assert.Equal(1f, CardTeam.Mod(D, n.Hand, "alacaMult"), 3);
        },

        // ---- Risk ----
        ["cam_top"] = () => { var h = Night("cam_top").Hand; Assert.Equal(1.6f, h.DamageMult(), 3); Assert.Equal(60f, h.MaxHp(100f), 3); },
        ["acgozlu"] = () =>
        {
            Assert.Equal(OzOf(Night()) * 1.25f, OzOf(Night("acgozlu")), 3);
            Assert.Equal(Night().HpMult * 1.15f, Night("acgozlu").HpMult, 3);
        },
        ["kumarbaz"] = () =>
        {
            var n = Night("catal*2", "seken*2", "kumarbaz");
            int before = n.Rank("catal") + n.Rank("seken");
            var (up, down) = n.Hand.OnWaveStart();
            Assert.NotNull(up); Assert.NotNull(down); Assert.NotEqual(up, down);
            Assert.Equal(before, n.Rank("catal") + n.Rank("seken"));
            Assert.Equal(3, n.Rank(up!)); Assert.Equal(1, n.Rank(down!));
            Assert.Equal(1, n.Rank("kumarbaz"));
            Assert.Equal((null, null), Night("catal*2", "seken*2").Hand.OnWaveStart());
        },
        ["yem"] = () =>
        {
            var n = Night("yem");
            Assert.True(n.Hand.Bait);
            var b = Night();
            n.OnKill(D.Creature("zipzip")); b.OnKill(D.Creature("zipzip"));
            Assert.Equal(b.Maya * 1.15f, n.Maya, 3);
        },
        ["son_kursun"] = () =>
        {
            var h = Night("son_kursun").Hand;
            h.PlayerHpFrac = 0.5f; Assert.Equal(1f, h.DamageMult(), 4);
            h.PlayerHpFrac = 0.2f; Assert.Equal(2f, h.DamageMult(), 4);
        },
        ["borc"] = () =>
        {
            var n = Night();
            int lv = n.Level;
            n.Grant("borc");
            Assert.Equal(lv + 3, n.Level);
            Assert.NotNull(n.Offer);
            for (int i = 0; i < 30; i++) n.OnKill(D.Creature("zipzip"));
            float maya = n.Maya;
            CardTests.RunToDawn(n, D);
            Assert.Equal(maya * 0.8f, n.Maya, 2);
            Assert.Equal(maya * 0.2f, n.MayaToKooperatif, 2);
        },

        // ---- Asilar ----
        ["sacmalik"] = () => Assert.True(Asi("sacmalik").Hand.NextShot().ForkOnBounce),
        ["dolu"] = () => Assert.Equal(2.5f, Asi("dolu").Hand.NextShot().FreezeArea, 3),
        ["gok_gurultusu"] = () =>
        {
            var n = Asi("gok_gurultusu");
            Assert.True(n.Hand.RollHit(W, false, 1f).UnlimitedChain);
            Assert.Equal(C("simsek_bulutu").Param("radius") * 1.5f, n.Hand.OrbitalAt(C("simsek_bulutu")).Radius, 3);
        },
        ["esek_arisi"] = () =>
        {
            var n = Asi("esek_arisi");
            var f = n.Hand.Tick(1f).Where(x => x.Card == "esek_arisi").ToList();
            Assert.Single(f);
            Assert.Equal(6, f[0].Count);
            Assert.Equal(1f, f[0].Def.Param("poison"));
        },
        ["bicerdover_modu"] = () => Assert.Contains(Asi("bicerdover_modu").Hand.Tick(1f), f => f.Card == "bicerdover_modu"),
        ["omlet_yagmuru"] = () => Assert.Contains(Asi("omlet_yagmuru").Hand.Tick(2.05f), f => f.Card == "omlet_yagmuru" && f.Count == 4),
        ["biyogaz"] = () => Assert.Equal(3f, Asi("biyogaz").Hand.Mod("fireWall"), 3),
        ["kara_delik"] = () => Assert.Contains(Asi("kara_delik").Hand.Tick(6.05f), f => f.Card == "kara_delik" && f.Def.Param("pull") == 1f),
        ["maya_bombasi"] = () => { var n = Asi("maya_bombasi"); n.OnKill(D.Creature("zipzip")); Assert.True(n.LastKill.ChainSwell); },
        ["kisla"] = () => Assert.Equal(1f, CardTeam.Mod(D, Asi("kisla").Hand, "animalCopies"), 3),
        ["kale"] = () =>
        {
            var h = Asi("kale").Hand;
            Assert.Equal(1f, CardTeam.Mod(D, h, "towerDouble"), 3);
            Assert.Equal(0.01f, CardTeam.Mod(D, h, "towerRegen"), 4);
        },
        ["hokkabaz"] = () =>
        {
            var h = Asi("hokkabaz").Hand;
            Assert.True(h.UnlimitedJumps);
            h.Airborne = false; float ground = h.DamageMult();
            h.Airborne = true; Assert.Equal(ground * 1.5f, h.DamageMult(), 3);
        },
    };

    public static IEnumerable<object[]> Ids => D.Cards.Cards.Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(Ids))]
    public void Card_scenario(string id) => Scenarios[id]();

    [Fact]
    public void Every_card_and_asi_has_a_scenario()
    {
        var ids = D.Cards.Cards.Select(c => c.Id).ToHashSet();
        Assert.Equal(72, ids.Count);
        Assert.Empty(ids.Except(Scenarios.Keys));
        Assert.Empty(Scenarios.Keys.Except(ids));
    }
}
