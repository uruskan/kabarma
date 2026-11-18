using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Atalarin savas sozlesmesine gecisi (GDD §16.1, §18.1): tur profili, durumlar, surekli hasar, kayit.</summary>
public class AtaCombatTests
{
    static AtalarFile D => TestData.Load().Atalar;
    static CombatFile C => TestData.Load().Combat;
    static AtaDef Pofuduk => D.Get("koca_pofuduk");

    static AtaFight Start(AtaDef def)
    {
        var f = AtaRules.Summon(new AtaProgress(), D, def, 1, force: true)!;
        f.ShieldT = 0;
        return f;
    }

    [Fact]
    public void Summon_builds_body_with_profile_and_team()
    {
        var f = Start(Pofuduk);
        Assert.Equal("koca_pofuduk", f.Body.Profile);
        Assert.Equal(CombatTeam.Wild, f.Body.Team);
        Assert.Equal(f.MaxHp, f.Body.MaxHp);
        Assert.True(f.Body.Mass > 1);
    }

    [Fact]
    public void Fire_weakness_and_burn_status_apply_and_dot_counts()
    {
        var f = Start(Pofuduk);
        float hp0 = f.Hp;
        float done = AtaRules.Damage(f, D, C, Pofuduk, 40, AtaHitKind.Thrown, type: DamageType.Ates);
        Assert.Equal(40f * C.Mult("koca_pofuduk", DamageType.Ates), done, 3);
        Assert.True(Status.Has(f.Body, StatusKind.Burn));
        float dot = 0;
        for (int i = 0; i < 40; i++) dot += AtaRules.TickStatus(f, D, C, Pofuduk, 0.1f);
        Assert.True(dot > 0);
        Assert.Equal(hp0 - done - dot, f.Hp, 2);
        Assert.True(f.DamageBySource["Status"] > 0);
    }

    [Fact]
    public void Burn_status_eats_wool_in_phase_two()
    {
        var f = Start(Pofuduk);
        AtaRules.Damage(f, D, C, Pofuduk, f.MaxHp * 0.4f, AtaHitKind.Thrown);
        Assert.Equal(1, f.Phase);
        f.ShieldT = 0;
        float a0 = f.Armor;
        AtaRules.Damage(f, D, C, Pofuduk, 1, AtaHitKind.Thrown, procs: StatusProc.Burn);
        AtaRules.TickStatus(f, D, C, Pofuduk, 1f);
        Assert.True(f.Armor < a0);
    }

    [Fact]
    public void Weapon_armor_matches_old_fraction_rule()
    {
        var f = Start(Pofuduk);
        AtaRules.Damage(f, D, C, Pofuduk, f.MaxHp * 0.4f, AtaHitKind.Thrown);
        f.ShieldT = 0;
        Assert.True(f.Armor > 0);
        Assert.Equal(10f * (1f - f.Armor), AtaRules.Damage(f, D, C, Pofuduk, 10, AtaHitKind.Weapon, type: DamageType.Kesme), 3);
    }

    [Fact]
    public void Freeze_lock_stops_attack_cycle()
    {
        var f = Start(Pofuduk);
        for (int i = 0; i < C.Freeze.LockStacks; i++) AtaRules.Damage(f, D, C, Pofuduk, 1, AtaHitKind.Weapon, type: DamageType.Buz);
        Assert.False(Status.CanAct(f.Body));
        f.AttackT = 0.01f;
        Assert.Empty(AtaRules.Tick(f, Pofuduk, 0.1f));
    }

    [Fact]
    public void Phase_change_clears_statuses()
    {
        var f = Start(Pofuduk);
        AtaRules.Damage(f, D, C, Pofuduk, 1, AtaHitKind.Thrown, procs: StatusProc.Burn);
        Assert.NotEmpty(f.Body.Statuses);
        AtaRules.Damage(f, D, C, Pofuduk, f.MaxHp * 0.4f, AtaHitKind.Thrown);
        Assert.Equal(1, f.Phase);
        Assert.Empty(f.Body.Statuses);
    }

    [Fact]
    public void Old_save_without_body_keeps_hp()
    {
        var json = """{"ataId":"koca_pofuduk","hp":321,"maxHp":900,"phase":1}""";
        var f = SaveJson.From<AtaFight>(System.Text.Encoding.UTF8.GetBytes(json));
        Assert.Equal(321f, f.Hp);
        Assert.Equal(900f, f.MaxHp);
        var back = SaveJson.From<AtaFight>(SaveJson.Bytes(f));
        Assert.Equal(321f, back.Body.Hp);
    }
}
