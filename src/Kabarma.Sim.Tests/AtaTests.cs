using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Atalar (GDD §18, §18.1): adak, cagirma, ekip cani, asama gecisleri, zirh ve kirkma, sersemleme, kovan, odul.</summary>
public class AtaTests
{
    static AtalarFile D => TestData.Load().Atalar;
    static AtaDef Pofuduk => D.Get("koca_pofuduk");
    static AtaDef Dede => D.Get("boynuzlu_dede");

    static AtaFight Start(AtaDef def, AtaProgress? p = null, int team = 1)
    {
        p ??= new AtaProgress();
        var f = AtaRules.Summon(p, D, def, team, force: true)!;
        f.ShieldT = 0; // inis bitti
        return f;
    }

    [Fact]
    public void Data_has_both_m2_atalar_with_three_phases_and_valid_thresholds()
    {
        foreach (var def in new[] { Pofuduk, Dede })
        {
            Assert.Equal(3, def.Phases.Count);
            Assert.True(def.Hp > 0 && def.Arena.Radius > 0 && def.Arena.Center.Count == 2 && def.Arena.Stone.Count == 2);
            for (int i = 1; i < def.Phases.Count; i++) Assert.True(def.Phases[i].HpUntil < def.Phases[i - 1].HpUntil);
            Assert.Equal(0f, def.Phases[^1].HpUntil);
            Assert.NotNull(D.AddOf(def.Add));
            Assert.NotNull(D.SealOf(def.Reward.Seal));
            Assert.Equal(3, def.Offering.Count);
            Assert.False(string.IsNullOrEmpty(def.Placeholder)); // URETIM §3: yer tutucu isaretli
        }
        Assert.Contains("biome:sis_korusu", Pofuduk.Reward.Unlocks); // PLAN: ilk Ata yenilince Sis Korusu acilir
    }

    [Theory]
    [InlineData(1, 1f)]
    [InlineData(2, 1.5f)]
    [InlineData(4, 2.5f)]
    public void Team_size_scales_hp(int n, float mult)
    {
        var f = AtaRules.Summon(new AtaProgress(), D, Pofuduk, n, force: true)!;
        Assert.Equal(Pofuduk.Hp * mult, f.MaxHp, 2);
    }

    [Fact]
    public void Offering_from_bag_and_swollen_crops_then_summon()
    {
        var p = new AtaProgress();
        var bag = new Inventory(30, 100);
        bag.Add("yun", 10); bag.Add("copper_ingot", 25);
        Assert.Null(AtaRules.Summon(p, D, Pofuduk, 1));
        Assert.Equal(30, AtaRules.OfferFromBag(p, Pofuduk, bag));
        Assert.Equal(5, bag.Count("copper_ingot")); // yalniz eksik kadar
        Assert.Equal(0, bag.Count("yun"));
        Assert.False(AtaRules.OfferingComplete(p, Pofuduk));
        for (int i = 0; i < 5; i++) Assert.True(AtaRules.OfferSwollen(p, Pofuduk, "turnip"));
        Assert.False(AtaRules.OfferSwollen(p, Pofuduk, "turnip")); // fazlasi kabul edilmez
        Assert.False(AtaRules.OfferSwollen(p, Pofuduk, "mushroom"));
        Assert.True(AtaRules.OfferingComplete(p, Pofuduk));
        var f = AtaRules.Summon(p, D, Pofuduk, 1);
        Assert.NotNull(f);
        Assert.Same(f, p.Active);
        Assert.Empty(p.Record("koca_pofuduk").Offered); // adak harcandi
        Assert.Null(AtaRules.Summon(p, D, Dede, 1, force: true)); // ayni anda tek dovus
        Assert.Equal(0, AtaRules.OfferFromBag(p, Pofuduk, bag));
    }

    [Fact]
    public void Arrival_shield_blocks_damage()
    {
        var f = AtaRules.Summon(new AtaProgress(), D, Pofuduk, 1, force: true)!;
        Assert.Equal(0f, AtaRules.Damage(f, D, Pofuduk, 50, AtaHitKind.Weapon));
        AtaRules.Tick(f, Pofuduk, D.ArriveSeconds + 0.01f);
        Assert.Equal(50f, AtaRules.Damage(f, D, Pofuduk, 50, AtaHitKind.Weapon));
    }

    [Fact]
    public void Phases_advance_in_order_with_a_shield_between()
    {
        var f = Start(Pofuduk);
        var cmds = new List<AtaCommand>();
        // Bir vurusta esigi asan hasar bile yalniz bir asama ilerletir; gecis kalkani sonrakini tutar.
        AtaRules.Damage(f, D, Pofuduk, f.MaxHp * 0.9f, AtaHitKind.Weapon, cmds);
        Assert.Equal(1, f.Phase);
        Assert.Contains(cmds, c => c.Cue == AtaCue.PhaseChanged && c.Phase == 1);
        Assert.Equal(D.PhaseShiftSeconds, f.ShieldT, 3);
        Assert.Equal(Pofuduk.Phases[1].Armor, f.Armor, 3); // yun zirhi geldi
        Assert.Equal(0f, AtaRules.Damage(f, D, Pofuduk, 100, AtaHitKind.Weapon));
        AtaRules.Tick(f, Pofuduk, D.PhaseShiftSeconds + 0.01f);
        AtaRules.Damage(f, D, Pofuduk, 1, AtaHitKind.Thrown, cmds); // can %10: ikinci esik da gecildi
        Assert.Equal(2, f.Phase);
        Assert.Equal(0f, f.Armor);
        Assert.False(f.Defeated);
        AtaRules.Tick(f, Pofuduk, D.PhaseShiftSeconds + 0.01f);
        AtaRules.Damage(f, D, Pofuduk, 10_000, AtaHitKind.Weapon, cmds);
        Assert.True(f.Defeated);
        Assert.Equal(0f, f.Hp);
        Assert.Contains(cmds, c => c.Cue == AtaCue.Defeated);
    }

    [Fact]
    public void Wool_armor_reduces_weapon_damage_and_shearing_strips_it()
    {
        var f = Start(Pofuduk);
        AtaRules.Damage(f, D, Pofuduk, f.MaxHp * 0.4f, AtaHitKind.Weapon);
        f.ShieldT = 0;
        Assert.Equal(1, f.Phase);
        var ph = Pofuduk.Phases[1];
        Assert.Equal(10f * (1f - ph.Armor), AtaRules.Damage(f, D, Pofuduk, 10, AtaHitKind.Weapon), 3);
        Assert.Equal(10f, AtaRules.Damage(f, D, Pofuduk, 10, AtaHitKind.Thrown), 3); // zirh yalniz silaha
        int hits = 0;
        while (f.Armor > 0 && hits < 50) { AtaRules.Shear(f, Pofuduk); hits++; }
        Assert.Equal((int)Math.Ceiling(ph.Armor / ph.ShearPerHit - 1e-4), hits);
        Assert.Equal(10f, AtaRules.Damage(f, D, Pofuduk, 10, AtaHitKind.Weapon), 3);
        // Yanik da yer
        var g = Start(Pofuduk); AtaRules.Damage(g, D, Pofuduk, g.MaxHp * 0.4f, AtaHitKind.Weapon);
        float a0 = g.Armor; AtaRules.Burn(g, Pofuduk, 2f);
        Assert.Equal(a0 - ph.BurnPerSecond * 2f, g.Armor, 3);
    }

    [Fact]
    public void Phase_one_shear_does_nothing()
    {
        var f = Start(Pofuduk);
        Assert.Equal(0f, AtaRules.Shear(f, Pofuduk));
    }

    [Fact]
    public void Pillar_stun_only_in_naked_phase_and_multiplies_damage()
    {
        var f = Start(Pofuduk);
        Assert.False(AtaRules.Stun(f, Pofuduk)); // asama 1: sersemleme yok
        f.Hp = f.MaxHp * 0.2f; f.Phase = 2;
        var ph = Pofuduk.Phases[2];
        Assert.True(AtaRules.Stun(f, Pofuduk));
        Assert.Equal(10f * ph.StunMult, AtaRules.Damage(f, D, Pofuduk, 10, AtaHitKind.Weapon), 3);
        // Sersemken saldiri yok; sure bitince saldiri sirasi doner
        var c1 = AtaRules.Tick(f, Pofuduk, ph.StunSeconds * 0.5f);
        Assert.DoesNotContain(c1, c => c.Cue == AtaCue.Telegraph);
        AtaRules.Tick(f, Pofuduk, ph.StunSeconds);
        Assert.Equal(0f, f.StunT);
        Assert.Equal(10f, AtaRules.Damage(f, D, Pofuduk, 10, AtaHitKind.Weapon), 3);
    }

    [Fact]
    public void Attack_cycle_telegraphs_then_attacks()
    {
        var f = Start(Pofuduk);
        var ph = Pofuduk.Phases[0];
        var all = new List<AtaCommand>();
        for (float t = 0; t < 20f; t += 0.05f) all.AddRange(AtaRules.Tick(f, Pofuduk, 0.05f));
        var tel = all.Where(c => c.Cue == AtaCue.Telegraph).ToList();
        var att = all.Where(c => c.Cue == AtaCue.Attack).ToList();
        Assert.True(tel.Count >= 3);
        Assert.True(att.Count >= tel.Count - 1);
        Assert.Equal(ph.Attacks[0], att[0].Attack);
        Assert.Equal(ph.Attacks[1], att[1].Attack); // sira: hucum, yumak
    }

    [Fact]
    public void Adds_spawn_up_to_the_cap()
    {
        var f = Start(Dede);
        var ph = Dede.Phases[0];
        int spawned = 0;
        for (float t = 0; t < ph.AddEvery * 5; t += 0.1f)
            spawned += AtaRules.Tick(f, Dede, 0.1f).Where(c => c.Cue == AtaCue.SpawnAdds).Sum(c => c.Count);
        Assert.Equal(ph.AddMax, spawned); // kimse olmedi: tavan
        f.AddsAlive = 0;
        for (float t = 0; t < ph.AddEvery + 0.2f; t += 0.1f)
            spawned += AtaRules.Tick(f, Dede, 0.1f).Where(c => c.Cue == AtaCue.SpawnAdds).Sum(c => c.Count);
        Assert.Equal(ph.AddMax + ph.AddCount, spawned);
    }

    [Fact]
    public void Dede_hives_stun_when_broken_and_shards_hit_hard_in_phase_three()
    {
        var f = Start(Dede);
        var cmds = new List<AtaCommand>();
        AtaRules.Damage(f, D, Dede, f.MaxHp * 0.35f, AtaHitKind.Weapon, cmds);
        f.ShieldT = 0;
        Assert.Equal(1, f.Phase);
        Assert.Equal(2, f.WeakPoints.Count);
        var ph = Dede.Phases[1];
        // Kovanlar yalniz kok dalgasindan sonra isir (weakWindow): kapaliyken vurulmaz.
        Assert.False(AtaRules.WeakOpen(f, Dede));
        Assert.False(AtaRules.HitWeakPoint(f, D, Dede, 0, ph.WeakPointHp * 2f, cmds));
        Assert.Equal(ph.WeakPointHp, f.WeakPoints[0], 3);
        var seen = new List<AtaCommand>();
        for (int i = 0; i < 400 && !seen.Any(c => c.Cue == AtaCue.Attack); i++) seen.AddRange(AtaRules.Tick(f, Dede, 0.05f));
        Assert.Contains(seen, c => c.Cue == AtaCue.Attack && c.Attack == "rootwave");
        Assert.True(AtaRules.WeakOpen(f, Dede));
        Assert.Equal(ph.WeakWindow, f.WeakOpenT, 1);
        Assert.False(AtaRules.HitWeakPoint(f, D, Dede, 0, ph.WeakPointHp * 0.5f, cmds));
        Assert.Equal(0f, f.StunT);
        Assert.True(AtaRules.HitWeakPoint(f, D, Dede, 0, ph.WeakPointHp, cmds));
        Assert.Equal(ph.StunSeconds, f.StunT, 3);
        Assert.Contains(cmds, c => c.Cue == AtaCue.WeakPointBroken && c.Count == 0);
        Assert.False(AtaRules.HitWeakPoint(f, D, Dede, 0, 10, cmds)); // kirik kovan
        AtaRules.Tick(f, Dede, ph.WeakWindow + 0.1f); // pencere sersemken de kapanir
        Assert.False(AtaRules.WeakOpen(f, Dede));
        Assert.False(AtaRules.HitWeakPoint(f, D, Dede, 1, 10, cmds));
        // Asama 3: silah yari, parca tam
        f.StunT = 0;
        AtaRules.Damage(f, D, Dede, f.Hp - f.MaxHp * 0.3f, AtaHitKind.Thrown, cmds);
        f.ShieldT = 0;
        Assert.Equal(2, f.Phase);
        Assert.Equal(Dede.Phases[2].Shards, f.ShardsDropped);
        Assert.Equal(10f * Dede.Phases[2].WeaponMult, AtaRules.Damage(f, D, Dede, 10, AtaHitKind.Weapon), 3);
        Assert.Equal(Dede.Phases[2].ShardDamage, AtaRules.Damage(f, D, Dede, Dede.Phases[2].ShardDamage, AtaHitKind.Thrown), 3);
    }

    [Fact]
    public void Every_phase_can_be_finished_with_weapons_alone()
    {
        // GDD §18.1: ozel mekanik kapi degil. Tirpan (14 hasar, 0,42 sn) ile yalniz silah: makul surede biter.
        var tool = TestData.Load().Tool.Weapon;
        foreach (var def in new[] { Pofuduk, Dede })
        {
            var f = Start(def);
            float t = 0, cd = 0;
            const float dt = 0.05f;
            while (!f.Defeated && t < 600f)
            {
                t += dt; cd -= dt;
                AtaRules.Tick(f, def, dt);
                if (cd <= 0) { cd = tool.FireInterval; AtaRules.Damage(f, D, def, tool.Damage, AtaHitKind.Weapon); }
            }
            Assert.True(f.Defeated, $"{def.Id} yalniz silahla {t:0} sn'de bitmedi");
            Assert.True(t < 240f, $"{def.Id} yalniz silahla cok uzun: {t:0} sn");
        }
    }

    [Fact]
    public void Reward_grants_items_seal_ulti_unlocks_and_closes_fight()
    {
        var p = new AtaProgress();
        var bag = new Inventory(20, 50);
        Start(Pofuduk, p);
        int left = AtaRules.Reward(p, Pofuduk, bag);
        Assert.Equal(0, left);
        Assert.Null(p.Active);
        Assert.Equal(1, bag.Count("cayir_cekirdegi"));
        Assert.Equal(3, bag.Count("ata_yunu"));
        Assert.True(p.IsDefeated("koca_pofuduk"));
        Assert.False(p.IsDefeated("boynuzlu_dede"));
        Assert.True(p.HasSeal("bereketli_toprak"));
        Assert.Contains("suru_cagrisi", p.Ultis);
        Assert.True(p.IsUnlocked("biome:sis_korusu"));
        Assert.Equal(0.75f, p.SealMult(D, "bereket_tuketimi"), 3);
        Assert.Equal(1f, p.SealMult(D, "yapi_cani"), 3);
        Start(Dede, p);
        AtaRules.Reward(p, Dede, bag);
        Assert.Equal(1.2f, p.SealMult(D, "yapi_cani"), 3);
        AtaRules.Reward(p, Dede, bag); // ikinci yenilgi (M4 Kabarik Ata) muhuru ikilemez
        Assert.Equal(1.2f, p.SealMult(D, "yapi_cani"), 3);
        Assert.Equal(2, p.Record("boynuzlu_dede").Defeats);
    }

    [Fact]
    public void Arena_follows_dunya2_ata_tasi_when_on_map_else_fallback()
    {
        var s = new GameState(TestData.Load());
        foreach (var def in new[] { Pofuduk, Dede })
        {
            var spot = AtaArena.Resolve(s, def);
            Assert.True(AtaArena.OnMap(s.Data.Biomes, spot.X, spot.Z, spot.Radius), $"{def.Id} arenasi arazinin disinda ({spot.X:0},{spot.Z:0})");
            var site = s.Spawns.AtaSite(def.Id, s.Pois);
            if (spot.FromSite) Assert.Equal(site!.Value.site.X, spot.X, 2);
            else Assert.Equal(def.Arena.Center[0], spot.X, 2);
        }
        // Koca Pofuduk'un Ata Tasi dunya2'de elle bu arenaya konmus (biomes.json pois fixed)
        var koca = AtaArena.Resolve(s, Pofuduk);
        Assert.True(koca.FromSite);
        Assert.Equal(Pofuduk.Arena.Center[0], koca.X, 2);
        Assert.Equal(Pofuduk.Arena.Center[1], koca.Z, 2);
    }

    [Fact]
    public void Defeated_ata_cannot_be_summoned_or_offered_again_in_m2()
    {
        var p = new AtaProgress();
        var bag = new Inventory(20, 50);
        Start(Pofuduk, p);
        AtaRules.Reward(p, Pofuduk, bag);
        Assert.Null(AtaRules.Summon(p, D, Pofuduk, 1, force: true)); // Kabarik Ata M4
        Assert.False(AtaRules.OfferSwollen(p, Pofuduk, "turnip"));
        bag.Add("yun", 10);
        Assert.Equal(0, AtaRules.OfferFromBag(p, Pofuduk, bag));
        Assert.Equal(10, bag.Count("yun"));
    }

    [Fact]
    public void Progress_round_trips_through_json()
    {
        var p = new AtaProgress();
        var bag = new Inventory(20, 50);
        bag.Add("yun", 4);
        AtaRules.OfferFromBag(p, Pofuduk, bag);
        var f = Start(Dede, p);
        AtaRules.Damage(f, D, Dede, f.MaxHp * 0.4f, AtaHitKind.Weapon);
        var json = JsonSerializer.Serialize(p);
        var back = JsonSerializer.Deserialize<AtaProgress>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back));
        Assert.Equal(4, back.Record("koca_pofuduk").Offered["yun"]);
        Assert.Equal(f.Hp, back.Active!.Hp, 3);
        Assert.Equal(1, back.Active.Phase);
        Assert.Equal(2, back.Active.WeakPoints.Count);
    }
}
