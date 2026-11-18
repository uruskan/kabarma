using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Azmanlar (ICERIK §4.2): gelis KS'si, can olcegi, sirttakiler, saldiri sirasi, durumlar, odul, kayit.</summary>
public class AzmanTests
{
    static GameData G => TestData.Load();
    static AzmanFile D => G.Azman;
    static CombatFile C => G.Combat;
    static AzmanDef Homur => D.Get("azman_homur");
    static AzmanDef Bozuk => D.Get("azman_bozuk");

    static AzmanFight Start(AzmanDef def, int ks = 5, AzmanProgress? p = null)
    {
        var f = AzmanRules.Spawn(p ?? new AzmanProgress(), D, def, ks, 1, 1);
        f.Body.ShieldT = 0;
        return f;
    }

    static CombatHit Hit(float n, DamageType t = DamageType.Darbe) => new(n, t, CombatTeam.Player) { IgnoreArmor = true };

    [Fact]
    public void Data_has_homur_and_bozuk_for_m3_biomes_with_valid_bases()
    {
        Assert.Equal("altin_cayir", Homur.Biome);
        Assert.Equal("sis_korusu", Bozuk.Biome);
        Assert.Same(Homur, D.ForBiome("altin_cayir"));
        foreach (var def in new[] { Homur, Bozuk })
        {
            Assert.NotNull(G.Creatures.Creatures.FirstOrDefault(c => c.Id == def.Base));
            Assert.False(string.IsNullOrEmpty(def.Placeholder)); // URETIM §3
            Assert.True(def.Hp > 0 && def.Attacks.Count > 0 && def.Reward.Items.Count > 0);
            Assert.True(C.Profiles.ContainsKey(def.Id));
        }
        Assert.NotNull(Homur.Charge);
        Assert.NotNull(Homur.Riders);
        Assert.NotNull(Bozuk.Uproot);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(7, false)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    public void Appears_every_fifth_ks(int ks, bool yes) => Assert.Equal(yes, AzmanRules.ShouldAppear(D, ks));

    [Fact]
    public void Hp_scales_with_ks_and_team()
    {
        var p = new AzmanProgress();
        var a = AzmanRules.Spawn(p, D, Homur, 5, 1, 1);
        var b = AzmanRules.Spawn(p, D, Homur, 10, 1, 2);
        var c = AzmanRules.Spawn(p, D, Homur, 5, 3, 3);
        Assert.Equal(Homur.Hp, a.Body.MaxHp, 2);
        Assert.Equal(Homur.Hp * (1 + D.HpPerKs * 5), b.Body.MaxHp, 2);
        Assert.Equal(Homur.Hp * 2f, c.Body.MaxHp, 2);
        Assert.Equal(3, p.Active.Count);
        Assert.Equal(Homur.Armor, a.Body.Armor);
        Assert.Equal("azman_homur", a.Body.Profile);
    }

    [Fact]
    public void Arrival_shield_blocks_damage()
    {
        var f = AzmanRules.Spawn(new AzmanProgress(), D, Homur, 5, 1, 1);
        Assert.True(AzmanRules.Damage(f, C, Homur, Hit(100)).Blocked);
        AzmanRules.Tick(f, C, Homur, D.ArriveSeconds + 0.1f);
        Assert.True(AzmanRules.Damage(f, C, Homur, Hit(100)).Damage > 0);
    }

    [Fact]
    public void Riders_drop_at_thresholds_and_all_by_last()
    {
        var f = Start(Homur);
        var rd = Homur.Riders!;
        int dropped = 0;
        var cmds = new List<AzmanCommand>();
        foreach (var at in rd.At)
        {
            float target = f.Body.MaxHp * at - 1;
            AzmanRules.Damage(f, C, Homur, Hit(f.Body.Hp - target), cmds);
        }
        dropped = cmds.Where(c => c.Cue == AzmanCue.DropRiders).Sum(c => c.Count);
        Assert.Equal(rd.Count, dropped);
        Assert.Equal(0, f.Riders);
        Assert.Equal(rd.At.Count, cmds.Count(c => c.Cue == AzmanCue.DropRiders));
    }

    [Fact]
    public void One_big_hit_drops_riders_through_all_crossed_thresholds()
    {
        var f = Start(Homur);
        var cmds = new List<AzmanCommand>();
        AzmanRules.Damage(f, C, Homur, Hit(f.Body.MaxHp * 0.8f), cmds);
        Assert.Equal(Homur.Riders!.Count, cmds.Where(c => c.Cue == AzmanCue.DropRiders).Sum(c => c.Count));
    }

    [Fact]
    public void Attack_cycle_telegraphs_then_acts_then_walks()
    {
        var f = Start(Bozuk);
        var all = new List<AzmanCommand>();
        for (int i = 0; i < 200; i++) all.AddRange(AzmanRules.Tick(f, C, Bozuk, 0.1f));
        Assert.Contains(all, c => c.Cue == AzmanCue.Telegraph && c.Attack == "uproot");
        Assert.Contains(all, c => c.Cue == AzmanCue.Attack && c.Attack == "uproot");
        int tel = all.FindIndex(c => c.Cue == AzmanCue.Telegraph), att = all.FindIndex(c => c.Cue == AzmanCue.Attack);
        Assert.True(tel < att);
    }

    [Fact]
    public void Freeze_lock_and_wall_stun_halt_attacks()
    {
        var f = Start(Homur);
        for (int i = 0; i < C.Freeze.LockStacks; i++) AzmanRules.Damage(f, C, Homur, Hit(1, DamageType.Buz));
        f.AttackT = 0.01f;
        Assert.Empty(AzmanRules.Tick(f, C, Homur, 0.1f));
        var g = Start(Homur);
        g.Acting = "charge"; g.ActT = 2;
        AzmanRules.WallStun(g, Homur);
        Assert.Equal("", g.Acting);
        Assert.False(Status.CanAct(g.Body));
    }

    [Fact]
    public void Fire_weakness_and_dot_can_defeat()
    {
        var f = Start(Bozuk);
        float done = AzmanRules.Damage(f, C, Bozuk, Hit(100, DamageType.Ates)).Damage;
        Assert.Equal(100f * C.Mult("azman_bozuk", DamageType.Ates), done, 2);
        f.Body.Hp = 1;
        var cmds = new List<AzmanCommand>();
        for (int i = 0; i < 20 && !f.Defeated; i++) cmds.AddRange(AzmanRules.Tick(f, C, Bozuk, 0.2f));
        Assert.True(f.Defeated);
        Assert.Contains(cmds, c => c.Cue == AzmanCue.Defeated);
    }

    [Fact]
    public void Weapons_alone_finish_both_at_ks5_in_reasonable_time()
    {
        var w = G.Tool.Weapon; // Tirpan Isini: gunduz zarsiz, zirhla
        foreach (var def in new[] { Homur, Bozuk })
        {
            var f = Start(def);
            float t = 0, cd = 0;
            while (!f.Defeated && t < 300)
            {
                AzmanRules.Tick(f, C, def, 0.05f);
                cd -= 0.05f; t += 0.05f;
                if (cd <= 0) { cd = w.FireInterval; AzmanRules.Damage(f, C, def, new CombatHit(w.Damage, C.WeaponType(w.Id), CombatTeam.Player)); }
            }
            Assert.True(f.Defeated, def.Id);
            Assert.InRange(t, 20f, 180f);
        }
    }

    [Fact]
    public void Reward_goes_to_bag_and_closes_fight()
    {
        var p = new AzmanProgress();
        var f = Start(Homur, p: p);
        var bag = new Inventory(40, 99);
        AzmanRules.Reward(p, f, Homur, bag);
        Assert.Empty(p.Active);
        Assert.Equal(1, p.Defeats["azman_homur"]);
        foreach (var it in Homur.Reward.Items) Assert.Equal(it.Count, bag.Count(it.Item));
    }

    [Fact]
    public void Progress_round_trips_through_save_json()
    {
        var p = new AzmanProgress();
        var f = Start(Homur, p: p);
        f.X = 12; f.Z = -3; f.Acting = "charge"; f.ActT = 1.5f;
        AzmanRules.Damage(f, C, Homur, Hit(30, DamageType.Ates));
        p.Defeats["azman_bozuk"] = 2;
        var back = SaveJson.From<AzmanProgress>(SaveJson.Bytes(p));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(p), System.Text.Json.JsonSerializer.Serialize(back));
        Assert.True(Status.Has(back.Active[0].Body, StatusKind.Burn));
    }
}
