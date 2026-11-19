using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 (ata4) senaryolari: dort yeni Ata (Kurbaga Aga, Ciyan Pasa, Buz Tosunu, Ana Maya), Kabarik Atalar, dort yeni Azman
/// ve uc yeni Oyuk koruyucusu (Camur Devi, Kuvars Akrebi, Buz Aynasi). Motorsuz: yalniz kural ve veri.
/// </summary>
public class Ata4Tests
{
    static readonly GameData G = TestData.Load();
    static AtalarFile D => G.Atalar;
    static CombatFile C => G.Combat;

    static AtaFight Start(string id, AtaProgress? p = null, int team = 1)
    {
        var f = AtaRules.Summon(p ?? new AtaProgress(), D, D.Get(id), team, force: true)!;
        f.ShieldT = 0;
        return f;
    }

    static float Hit(AtaFight f, string id, float amt, AtaHitKind kind = AtaHitKind.Weapon, List<AtaCommand>? cmds = null) =>
        AtaRules.Damage(f, D, C, D.Get(id), amt, kind, cmds);

    /// <summary>Hp'yi verilen orana ceker (esikleri asarak asama gecisini tetikler) ve kalkani indirir.</summary>
    static void ToFraction(AtaFight f, string id, float frac)
    {
        f.Body.Hp = f.MaxHp * frac;
        Hit(f, id, 0.01f);
        f.ShieldT = 0;
    }

    /// <summary>Zaman ilerletir; istenen Attack komutu gelene dek (en cok 60 sn). Gelen komutlari doner.</summary>
    static List<AtaCommand> RunUntilAttack(AtaFight f, AtaDef def, string attack)
    {
        var got = new List<AtaCommand>();
        for (int i = 0; i < 6000; i++)
        {
            var cmds = AtaRules.Tick(f, def, 0.01f);
            got.AddRange(cmds);
            if (cmds.Any(c => c.Cue == AtaCue.Attack && c.Attack == attack)) return got;
        }
        throw new Xunit.Sdk.XunitException($"{def.Id}: {attack} saldirisi gelmedi");
    }

    static readonly string[] New = { "kurbaga_aga", "ciyan_pasa", "buz_tosunu", "ana_maya" };

    [Fact]
    public void Data_six_atalar_with_valid_phases_arenas_seals_and_placeholders()
    {
        Assert.Equal(6, D.Atalar.Count);
        foreach (var id in New)
        {
            var def = D.Get(id);
            Assert.Equal(3, def.Phases.Count);
            Assert.Equal(0f, def.Phases[^1].HpUntil);
            for (int i = 1; i < 3; i++) Assert.True(def.Phases[i].HpUntil < def.Phases[i - 1].HpUntil);
            Assert.True(def.Hp > 0 && def.Arena.Radius > 0 && def.Arena.Center.Count == 2 && def.Arena.Stone.Count == 2);
            Assert.False(string.IsNullOrEmpty(def.Placeholder));
            Assert.True(string.IsNullOrEmpty(def.Arena.Temporary), "M4 kabul: arena Ata Tasi'na oturdu, GECICI isareti kalkti (AcceptanceTests dogrular)");
            Assert.NotNull(D.SealOf(def.Reward.Seal));
            Assert.NotNull(D.AddOf(def.Add));
            foreach (var ph in def.Phases) if (ph.AddId != "") Assert.NotNull(D.AddOf(ph.AddId));
            Assert.True(def.Offering.Count >= 3);
            Assert.True(AtaArena.OnMap(G.Biomes, def.Arena.Center[0], def.Arena.Center[1], def.Arena.Radius), $"{id} arenasi arazi disinda");
        }
        Assert.Equal(new[] { "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" }, New.Select(i => D.Get(i).Biome));
    }

    [Fact]
    public void Kurbaga_swallow_inside_hits_crit_dive_is_immune_and_belly_sac_opens_after_swell()
    {
        var def = D.Get("kurbaga_aga");
        var f = Start("kurbaga_aga");
        // 1. asama: yutma; icerden vurus kritik (carpan), disardan degil.
        float outside = Hit(f, def.Id, 20);
        Assert.True(AtaRules.BeginSwallow(f, def));
        Assert.Equal(3f, f.SwallowT, 3);
        float inside = Hit(f, def.Id, 20, AtaHitKind.Inside);
        Assert.Equal(outside * 2.5f, inside, 3);
        // 2. asama: dalis: suyun altinda hasar yok; sure bitince alan hasari var.
        ToFraction(f, def.Id, 0.6f);
        Assert.Equal(1, f.Phase);
        f.AttackT = 0.01f; f.AttackIndex = 0; // sira: dive
        RunUntilAttack(f, def, "dive");
        Assert.True(f.SubmergedT > 3f);
        Assert.Equal(0f, Hit(f, def.Id, 50));
        for (int i = 0; i < 400; i++) AtaRules.Tick(f, def, 0.01f);
        Assert.Equal(0f, f.SubmergedT);
        Assert.True(Hit(f, def.Id, 50) > 0);
        // 3. asama: karin kesesi yalniz "swell"den sonra isir; kirilinca sersemler.
        ToFraction(f, def.Id, 0.3f);
        Assert.Equal(2, f.Phase);
        Assert.False(AtaRules.WeakOpen(f, def));
        RunUntilAttack(f, def, "swell");
        Assert.True(AtaRules.WeakOpen(f, def));
        var cmds = new List<AtaCommand>();
        Assert.True(AtaRules.HitWeakPoint(f, D, C, def, 0, 500, cmds));
        Assert.Contains(cmds, c => c.Cue == AtaCue.WeakPointBroken);
        Assert.True(f.StunT > 0);
    }

    [Fact]
    public void Ciyan_pasa_drum_needs_sixty_seconds_of_power_fourteen_segments_then_barrels()
    {
        var def = D.Get("ciyan_pasa");
        var p = new AtaProgress();
        var bag = new Inventory(60, 100);
        bag.Add("celik", 20); bag.Add("kaucuk", 10); bag.Add("devre", 4);
        Assert.False(AtaRules.OfferingComplete(p, def));
        Assert.Equal(34, AtaRules.OfferFromBag(p, def, bag));
        Assert.True(AtaRules.ItemsComplete(p, def));
        Assert.False(AtaRules.OfferingComplete(p, def));           // davul henuz beslenmedi
        for (int i = 0; i < 590; i++) AtaRules.ChargeDrum(p, def, true, 0.1f);
        Assert.False(AtaRules.DrumReady(p, def));
        AtaRules.ChargeDrum(p, def, false, 0.1f);                  // besleme kesildi: sifirlanir
        Assert.Equal(0f, p.Record(def.Id).PowerT);
        for (int i = 0; i < 601; i++) AtaRules.ChargeDrum(p, def, true, 0.1f);
        Assert.True(AtaRules.OfferingComplete(p, def));
        var f = AtaRules.Summon(p, D, def, 1)!;
        f.ShieldT = 0;
        Assert.Equal(14, f.WeakPoints.Count);
        // 1. asama: boğumlar tek tek kirilir
        int broke = 0;
        for (int i = 0; i < 14; i++) if (AtaRules.HitWeakPoint(f, D, C, def, i, 100)) broke++;
        Assert.Equal(14, broke);
        // 2. asama: kopan bogum yardimci dogurur, kum firtinasi saldirisi var
        ToFraction(f, def.Id, 0.5f);
        Assert.Equal(1, f.Phase);
        Assert.True(AtaRules.PhaseOf(f, def).BreakSpawnsAdd);
        Assert.Contains("sandstorm", AtaRules.PhaseOf(f, def).Attacks);
        Assert.Equal(14, f.WeakPoints.Count);
        // 3. asama: kafa zirhli (silah ceyrek), varil patlamalari asil hasar
        ToFraction(f, def.Id, 0.3f);
        Assert.Equal(2, f.Phase);
        Assert.Equal(4, f.ShardsDropped);
        float gun = Hit(f, def.Id, 100);
        float barrel = Hit(f, def.Id, 300, AtaHitKind.Thrown);
        Assert.True(barrel > gun * 4);
        // Odul: ulti tanimi gelene dek "borc" olarak yazilir.
        var bag2 = new Inventory(60, 100);
        AtaRules.Reward(p, def, bag2);
        Assert.Equal(1, bag2.Count("kanyon_cekirdegi"));
        Assert.Contains("yorunge_sulamasi", p.UltisOwed);
        Assert.Contains("sarsilmaz", p.Seals);
    }

    [Fact]
    public void Buz_tosunu_cold_outside_warm_points_glacier_melts_by_burn_then_belly_opens()
    {
        var def = D.Get("buz_tosunu");
        var f = Start(def.Id);
        ToFraction(f, def.Id, 0.5f);
        Assert.Equal(1, f.Phase);
        Assert.Equal(0f, AtaRules.ColdDamage(f, def, nearWarm: true, 1f));
        Assert.Equal(4f, AtaRules.ColdDamage(f, def, nearWarm: false, 1f), 3);
        ToFraction(f, def.Id, 0.2f);
        Assert.Equal(2, f.Phase);
        Assert.Equal(0.8f, f.Armor, 3);
        Assert.False(AtaRules.WeakOpen(f, def));                                   // buzul eriyene dek kapali
        float armored = Hit(f, def.Id, 100);
        for (int i = 0; i < 100; i++) AtaRules.Burn(f, def, 0.1f);                // 10 sn yanik: 0.6 zirh yer
        Assert.True(f.Armor < 0.25f);
        Assert.True(AtaRules.WeakOpen(f, def));
        Assert.True(Hit(f, def.Id, 100) > armored);                               // zirh dustu, hasar arttı
        Assert.True(AtaRules.HitWeakPoint(f, D, C, def, 0, 400));
        Assert.True(f.StunT > 0);
    }

    [Fact]
    public void Ana_maya_copies_flood_then_swell_and_core_and_additive_seal()
    {
        var def = D.Get("ana_maya");
        var f = Start(def.Id);
        Assert.Equal(1f, AtaRules.PlayerSwell(f, def));
        Assert.Equal("maya_kopya", AtaRules.PhaseOf(f, def).AddId);
        ToFraction(f, def.Id, 0.5f);
        Assert.Equal("hamurcuk", AtaRules.PhaseOf(f, def).AddId);
        Assert.Contains("surge", AtaRules.PhaseOf(f, def).Attacks);
        ToFraction(f, def.Id, 0.2f);
        Assert.Equal(2, f.Phase);
        Assert.Equal(1.8f, AtaRules.PlayerSwell(f, def), 3);
        Assert.Equal(1.6f, AtaRules.PlayerDamageMult(f, def), 3);
        Assert.True(AtaRules.HitWeakPoint(f, D, C, def, 0, 600));                  // cekirdek her an acik
        var p = new AtaProgress();
        AtaRules.Reward(p, def, new Inventory(10, 10));
        Assert.Equal(2f, p.SealAdd(D, "boy_puani"));
        Assert.Contains("recipe:kabartma_odasi", p.Unlocks);
        Assert.Contains("mode:yeni_ay", p.Unlocks);
    }

    [Fact]
    public void Kalin_post_has_two_effects_and_islak_zemin_scales_water()
    {
        var p = new AtaProgress();
        p.Seals.AddRange(new[] { "kalin_post", "islak_zemin" });
        Assert.Equal(1.25f, p.SealMult(D, "tehlike_direnci"), 3);
        Assert.Equal(1.25f, p.SealMult(D, "hayvan_cani"), 3);
        Assert.Equal(1.3f, p.SealMult(D, "su_yapilari"), 3);
        Assert.Equal(1f, p.SealMult(D, "bilinmeyen"), 3);
    }

    // ---------- Kabarik Atalar ----------

    [Fact]
    public void Kabarik_resummon_needs_defeat_offering_and_maya_scales_by_ks_and_rewards_only_chip_material()
    {
        foreach (var id in D.Atalar.Select(a => a.Id))
        {
            var def = D.Get(id);
            var p = new AtaProgress();
            var bag = new Inventory(80, 200);
            // yenilmemis Ata icin Kabarik yok
            Assert.Null(AtaRules.SummonKabarik(p, D, def, 5, 1, bag, force: true));
            p.Record(id).Defeats = 1;
            // adak (kabarik kaydina) + Maya
            foreach (var o in def.Offering.Where(o => !o.Swollen)) bag.Add(o.Item, o.Count);
            foreach (var o in def.Offering.Where(o => o.Swollen)) for (int i = 0; i < o.Count; i++) Assert.True(AtaRules.OfferSwollen(p, def, o.Item, kab: true));
            AtaRules.OfferFromBag(p, def, bag, kab: true);
            if (def.PowerWatts > 0) for (int i = 0; i < (int)(def.PowerSeconds * 10) + 5; i++) AtaRules.ChargeDrum(p, def, true, 0.1f, kab: true);
            Assert.True(AtaRules.OfferingComplete(p, def, kab: true), id);
            Assert.Null(AtaRules.SummonKabarik(p, D, def, 10, 1, bag));              // Maya yok: kabul yok, adak yerinde
            Assert.True(AtaRules.OfferingComplete(p, def, kab: true));
            Assert.Null(AtaRules.SummonKabarik(p, D, def, 7, 1, bag, force: false));  // gecersiz KS
            bag.Add("maya", 10 * 50 + 3);
            var f = AtaRules.SummonKabarik(p, D, def, 10, 1, bag)!;
            Assert.NotNull(f);
            Assert.Equal(3, bag.Count("maya"));                                       // tam 10 × 50 dustu
            Assert.True(f.Kabarik);
            Assert.Equal(10, f.Ks);
            Assert.Equal(def.Hp * 2f, f.MaxHp, 1);                                    // 1 + 0.1 × 10
            Assert.Equal(1.2f, f.DamageMult, 3);
            Assert.Equal("kabarik_" + id, AtaRules.KabarikId(id));
            // yenilgi: yalniz cip malzemesi (KS/5 = 2); Cekirdek, muhur vb. tekrar yok
            var seals = p.Seals.Count; var ultis = p.Ultis.Count;
            var reward = new Inventory(80, 200);
            Assert.Equal(0, AtaRules.RewardKabarik(p, D, def, 10, reward));
            Assert.Equal(2, reward.Count("kabarik_oz"));
            Assert.Equal(0, reward.Count(def.Reward.Items.FirstOrDefault()?.Item ?? "yok"));
            Assert.Equal(seals, p.Seals.Count); Assert.Equal(ultis, p.Ultis.Count);
            Assert.Equal(10, p.Record("kabarik_" + id).BestKs);
            Assert.Null(p.Active);
            // ayni dovus tekrar acilir (yeni adak + Maya ile)
            Assert.Null(AtaRules.SummonKabarik(p, D, def, 5, 1, bag));
        }
    }

    [Fact]
    public void Kabarik_cost_and_scale_follow_ks_list()
    {
        Assert.Equal(new[] { 5, 10, 15, 20, 30 }, D.Kabarik.Ks);
        Assert.Equal(250, AtaRules.MayaCost(D, 5));
        Assert.Equal(1500, AtaRules.MayaCost(D, 30));
        Assert.Equal(4f, AtaRules.KabarikHpMult(D, 30), 3);
        // normal adak yenilmemis Ata'ya, kabarik adak yenilmise: karismaz
        var p = new AtaProgress(); var bag = new Inventory(80, 100); bag.Add("yun", 10); bag.Add("copper_ingot", 20);
        var pof = D.Get("koca_pofuduk");
        Assert.Equal(0, AtaRules.OfferFromBag(p, pof, bag, kab: true));
        Assert.True(AtaRules.OfferFromBag(p, pof, bag) > 0);
        p.Record("koca_pofuduk").Defeats = 1;
        Assert.Equal(0, AtaRules.OfferFromBag(p, pof, bag));
    }

    // ---------- Azmanlar ----------

    static AzmanFile AF => G.Azman;

    static AzmanFight AzSpawn(string id, AzmanProgress p, int ks = 5, float x = 0, float z = 0)
    {
        var f = AzmanRules.Spawn(p, AF, AF.Get(id), ks, 1, (ulong)(p.Active.Count + 1), x, z);
        f.Body.ShieldT = 0;
        return f;
    }

    [Fact]
    public void Azman_data_six_each_biome_has_one_and_defeat_reward_exists()
    {
        Assert.Equal(6, AF.Azmanlar.Count);
        foreach (var b in new[] { "altin_cayir", "sis_korusu", "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" })
            Assert.NotNull(AF.ForBiome(b));
        foreach (var a in AF.Azmanlar) { Assert.False(string.IsNullOrEmpty(a.Placeholder)); Assert.True(a.Reward.Items.Count > 0); Assert.NotNull(G.Creatures.Creatures.FirstOrDefault(c => c.Id == a.Base)); }
    }

    [Fact]
    public void Azman_kiskac_armor_pierced_only_from_behind()
    {
        var p = new AzmanProgress(); var def = AF.Get("azman_kiskac");
        var f = AzSpawn(def.Id, p, x: 0, z: 0); f.Yaw = 0; // +Z'ye bakar
        var hit = new CombatHit(100, DamageType.Darbe, CombatTeam.Player);
        float hp0 = f.Body.Hp;
        AzmanRules.Damage(f, C, def, hit, null, null, 0, 10);       // onden
        float front = hp0 - f.Body.Hp; hp0 = f.Body.Hp;
        AzmanRules.Damage(f, C, def, hit, null, null, 0, -10);      // arkadan
        float back = hp0 - f.Body.Hp;
        Assert.True(back > front * 5, $"on {front} arka {back}");
        Assert.True(AzmanRules.FromRear(f, def, 0, -10));
        Assert.False(AzmanRules.FromRear(f, def, 10, 0));
        Assert.Contains("rip", def.Attacks);
    }

    [Fact]
    public void Azman_kizgin_ignites_patches_burn_players_and_water_puts_them_out()
    {
        var p = new AzmanProgress(); var def = AF.Get("azman_kizgin");
        var f = AzSpawn(def.Id, p);
        int n = AzmanRules.Ignite(f, def, Enumerable.Range(0, 9).Select(i => (i * 10f, 0f)));
        Assert.Equal(def.Ignite!.Patches, n);                                  // en cok Patches
        Assert.Equal(0f, AzmanRules.FireDamage(f, def, 200, 200, 1f));
        Assert.Equal(def.Ignite.Dps, AzmanRules.FireDamage(f, def, 1, 1, 1f), 3);
        Assert.Equal(0, AzmanRules.Douse(f, 0, 0, 3, 3f));                      // sulama: sure kisalir, henuz sonmez
        Assert.Equal(1, AzmanRules.Douse(f, 0, 0, 3, def.Ignite.Seconds));      // tam sonmesi
        Assert.Equal(n - 1, f.Fires.Count);
        for (int i = 0; i < (int)(def.Ignite.Seconds * 10) + 5; i++) AzmanRules.Tick(f, C, def, 0.1f);
        Assert.Empty(f.Fires);                                                  // yangin kendiliginden de biter
    }

    [Fact]
    public void Surubasi_howls_up_to_max_wolves_until_killed()
    {
        var p = new AzmanProgress(); var def = AF.Get("surubasi");
        var f = AzSpawn(def.Id, p);
        Assert.Equal(2, AzmanRules.Howl(f, def));
        Assert.Equal(2, AzmanRules.Howl(f, def));
        Assert.Equal(2, AzmanRules.Howl(f, def));
        Assert.Equal(0, AzmanRules.Howl(f, def));                               // Max 6
        AzmanRules.SummonDied(f);
        Assert.Equal(1, AzmanRules.Howl(f, def));
        // saldiri sirasi howl'u icerir ve Acting kalmaz (anlik saldiri)
        f.AttackT = 0.01f; f.AttackIndex = 0;
        var cmds = new List<AzmanCommand>();
        for (int i = 0; i < 400 && !cmds.Any(c => c.Cue == AzmanCue.Attack); i++) cmds.AddRange(AzmanRules.Tick(f, C, def, 0.01f));
        Assert.Contains(cmds, c => c.Cue == AzmanCue.Attack && c.Attack == "howl");
        Assert.Equal("", f.Acting);
    }

    [Fact]
    public void Azman_hamur_splits_at_thresholds_and_only_last_piece_pays()
    {
        var p = new AzmanProgress(); var def = AF.Get("azman_hamur");
        var f = AzSpawn(def.Id, p);
        var cmds = new List<AzmanCommand>();
        f.Body.Hp = f.Body.MaxHp * 0.6f;
        AzmanRules.Damage(f, C, def, new CombatHit(1, DamageType.Darbe, CombatTeam.Player), cmds);
        Assert.Single(cmds, c => c.Cue == AzmanCue.Split);
        f.Body.Hp = f.Body.MaxHp * 0.3f;
        AzmanRules.Damage(f, C, def, new CombatHit(1, DamageType.Darbe, CombatTeam.Player), cmds);
        Assert.Equal(2, cmds.Count(c => c.Cue == AzmanCue.Split));
        var a = AzmanRules.SpawnPiece(p, AF, def, f, 100, 3, 3);
        var b = AzmanRules.SpawnPiece(p, AF, def, f, 101, -3, 3);
        Assert.Equal(1, a.Gen);
        Assert.Equal(f.Body.MaxHp * def.Split!.HpFrac, a.Body.MaxHp, 2);
        var bag = new Inventory(20, 100);
        // parcalar sirayla olur; odul yalniz sonuncuda
        a.Body.Hp = 0; b.Body.Hp = 0;
        Assert.False(AzmanRules.IsLastPiece(p, a)); AzmanRules.DropPiece(p, a);
        Assert.False(AzmanRules.IsLastPiece(p, b)); AzmanRules.DropPiece(p, b);
        Assert.True(AzmanRules.IsLastPiece(p, f));                              // ana parca = cekirdek
        Assert.Equal(0, AzmanRules.Reward(p, f, def, bag));
        Assert.Equal(1, bag.Count("azman_hamuru"));
        Assert.Equal(1, p.Defeats["azman_hamur"]);
    }

    // ---------- Oyuk koruyucular ----------

    static OyukGuardianDef OG(string id) => G.Oyuk.Get(id);

    [Fact]
    public void Oyuk_data_five_guardians_each_kind_tiers_and_rewards()
    {
        Assert.Equal(5, G.Oyuk.Guardians.Count);
        foreach (var (b, kind) in new[] { ("isil_sazlik", "camur"), ("kizil_kanyon", "akrep"), ("ayaz_yaylasi", "ayna") })
        {
            var g = G.Oyuk.ForBiome(b)!;
            Assert.Equal(kind, g.Kind);
            Assert.Equal(3, g.Hp.Count);
            Assert.True(g.Hp[0] < g.Hp[1] && g.Hp[1] < g.Hp[2]);
            Assert.False(string.IsNullOrEmpty(g.Placeholder));
            Assert.Equal(2, g.Chests);
            Assert.True(OyukRules.ChestLoot(g, 2, 0).Count > 0);
        }
    }

    [Fact]
    public void Mud_hulk_wet_is_immune_and_heals_fire_dries_only_darbe_hurts_pool_rewets()
    {
        var m = new MudFight(OG("camur_devi"), 0);
        Assert.Equal(0f, m.Hit(100, DamageType.Darbe));                         // islak: hasar yok
        m.Heat(30f); Assert.False(m.Dry);                                       // yetmez
        Assert.True(m.Heat(80f)); Assert.True(m.Dry);                           // yeterince ates: kurur
        float cut = m.Hit(100, DamageType.Kesme), smash = m.Hit(100, DamageType.Darbe);
        Assert.True(smash > cut * 5);                                           // kuru parca Darbe ile kirilir
        // kuruyan dev goluyne yurur, girince yeniden islanir
        float hp = m.Hp;
        for (int i = 0; i < 600 && m.Dry; i++) m.Tick(0.05f, 0, 6);
        Assert.False(m.Dry);
        Assert.Equal(1, m.Rewets);
        // islakken canlanir
        for (int i = 0; i < 200; i++) m.Tick(0.05f, 0, 6);
        Assert.True(m.Hp > hp);
        // yenilir: kuru ve Darbe ile
        var m2 = new MudFight(OG("camur_devi"), 0);
        m2.Heat(200f);
        for (int i = 0; i < 40 && !m2.Dead; i++) m2.Hit(120, DamageType.Darbe);
        Assert.True(m2.Dead);
    }

    [Fact]
    public void Scorpion_bounces_cracks_by_heavy_hit_or_stalactite_then_takes_damage()
    {
        var g = OG("kuvars_akrebi");
        var s = new ScorpionFight(g, 0);
        Assert.Equal(0f, s.Hit(30, DamageType.Kesme));
        Assert.True(s.Bounces > 0 && !s.Cracked);
        s.Hit(80, DamageType.Darbe);                                            // Havan: agir vurus catlatir
        Assert.True(s.Cracked);
        Assert.True(s.Hit(50, DamageType.Kesme) > 50);                          // catlakta WeakMult
        for (int i = 0; i < 200; i++) s.Tick(0.05f, 0, 20);                     // catlak kapanir
        Assert.False(s.Cracked);
        // sarkit: Akrep altindayken dussun
        var s2 = new ScorpionFight(g, 0);
        Assert.Equal(g.Stalactites, s2.Stalactites.Count);
        int under = 0;
        // oyuncu sarkitin yanina cagirir: Akrep ona yurur
        var st = s2.Stalactites[under];
        for (int i = 0; i < 400; i++) s2.Tick(0.05f, st.X + 1f, st.Z);          // Akrep sarkide dogru yurur
        Assert.True(s2.DropStalactite(under));
        for (int i = 0; i < 40; i++) s2.Tick(0.05f, st.X + 6f, st.Z);
        Assert.True(s2.Stalactites[under].Fallen);
        Assert.True(s2.Cracked, "Akrep sarkitin altindayken zirh catlamali");
        // uzaktayken dusen sarkit catlatmaz
        var s3 = new ScorpionFight(g, 0);
        Assert.True(s3.DropStalactite(1));
        for (int i = 0; i < 40; i++) s3.Tick(0.05f, 0, 0);
        Assert.False(s3.Cracked);
        // kuyruk menzildeyken zehir atar
        var s4 = new ScorpionFight(g, 0);
        float dmg = 0; for (int i = 0; i < 200; i++) dmg += s4.Tick(0.05f, 0, 8);
        Assert.True(dmg > 0);
    }

    [Fact]
    public void Ice_mirror_invulnerable_while_copies_exist_fire_and_shock_melt_them()
    {
        var g = OG("buz_aynasi");
        var mir = new MirrorFight(g, 0, G.Oyuk.Interior.Radius);
        Assert.True(mir.Vulnerable);
        for (int i = 0; i < 400 && mir.Copies.Count == 0; i++) mir.Tick(0.05f, 0, 10);
        Assert.NotEmpty(mir.Copies);
        Assert.False(mir.Vulnerable);
        Assert.Equal(0f, mir.Hit(100, DamageType.Kesme));
        Assert.False(mir.HitCopy(0, 10, DamageType.Kesme));                     // sade silah erimez (%25)
        Assert.True(mir.HitCopy(0, 1, DamageType.Ates));                        // Ates eritir
        Assert.Empty(mir.Copies);
        Assert.True(mir.Vulnerable);
        Assert.True(mir.Hit(100, DamageType.Kesme) > 0);
        // kopyalar oyuncuya vurur
        var m2 = new MirrorFight(g, 0, G.Oyuk.Interior.Radius);
        float taken = 0; for (int i = 0; i < 800; i++) taken += m2.Tick(0.05f, 0, m2.MirrorZ + 3);
        Assert.True(taken > 0);
        // tum kopyalar erirken ayna oluncaya dek vurulur
        var m3 = new MirrorFight(g, 2, G.Oyuk.Interior.Radius);
        for (int round = 0; round < 400 && !m3.Dead; round++)
        {
            m3.Tick(0.2f, 0, 0);
            for (int i = m3.Copies.Count - 1; i >= 0; i--) m3.HitCopy(i, 1, DamageType.Sok);
            m3.Hit(500, DamageType.Kesme);
        }
        Assert.True(m3.Dead);
    }
}
