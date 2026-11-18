using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Savas sozlesmesi (GDD §16.1): azaltma sirasi, zirh, takim, durumlar, alan etkileri, Kabarik, itme, kayit.</summary>
public class CombatTests
{
    static GameData G => TestData.Load();
    static CombatFile D => G.Combat;

    static CombatBody Body(float hp = 100, string profile = "", float armor = 0, CombatTeam team = CombatTeam.Wild) =>
        CombatBody.Of(hp, team, profile, armor);

    static CombatHit Hit(float amount, DamageType t = DamageType.Darbe) => new(amount, t, CombatTeam.Player);

    [Fact]
    public void Data_types_match_enum_and_profiles_and_weapons_exist()
    {
        Assert.Equal(Enum.GetValues<DamageType>().Select(Combat.Id), D.Types.Select(t => t.Id));
        var known = G.Creatures.Creatures.Select(c => c.Id).Concat(G.Atalar.Atalar.Select(a => a.Id)).Concat(G.Azman.Azmanlar.Select(a => a.Id)).ToHashSet();
        foreach (var (id, mults) in D.Profiles)
        {
            Assert.True(known.Contains(id), $"combat.json profil '{id}' bilinmeyen kimlik");
            foreach (var t in mults.Keys) Assert.True(Combat.TryParse(t, out _), $"{id}: bilinmeyen tur {t}");
        }
        foreach (var w in new[] { G.Tool.Weapon.Id, G.Tool.Nailgun.Id }) Assert.True(D.WeaponTypes.ContainsKey(w), w);
        foreach (var t in D.WeaponTypes.Values) Assert.True(Combat.TryParse(t, out _), t);
    }

    [Fact]
    public void Armor_follows_gdd_formula_and_points_round_trip()
    {
        Assert.Equal(0.5f, Combat.ArmorReduction(D, 100), 3);
        Assert.Equal(0f, Combat.ArmorReduction(D, 0));
        Assert.Equal(0.6f, Combat.ArmorReduction(D, Combat.ArmorPointsFor(D, 0.6f)), 3);
        var b = Body(armor: 100);
        var r = Combat.Apply(D, b, Hit(40));
        Assert.Equal(20f, r.Damage, 3);
        Assert.Equal(80f, b.Hp, 3);
    }

    [Fact]
    public void Mitigation_order_is_profile_then_armor_and_ignore_armor_skips_armor()
    {
        var b = Body(profile: "pofuduk", armor: 100); // ates ×1.5
        Assert.Equal(30f, Combat.Mitigate(D, b, Hit(40, DamageType.Ates)), 3); // 40 × 1.5 × 0.5
        Assert.Equal(60f, Combat.Mitigate(D, b, Hit(40, DamageType.Ates) with { IgnoreArmor = true }), 3);
        Assert.Equal(15f, Combat.Mitigate(D, b, Hit(40, DamageType.Buz)), 3); // ×0.75 × 0.5
    }

    [Fact]
    public void Friendly_fire_off_blocks_damage_but_not_knock_and_shield_blocks()
    {
        var ally = Body(team: CombatTeam.Player);
        var r = Combat.Apply(D, ally, Hit(30) with { Knock = 4 });
        Assert.True(r.Blocked);
        Assert.Equal(100f, ally.Hp);
        Assert.True(r.Knock > 0);
        Assert.Equal(0f, Combat.Mitigate(D, ally, Hit(30)));
        var neutral = Body(team: CombatTeam.Player);
        Assert.Equal(30f, Combat.Apply(D, neutral, new CombatHit(30, DamageType.Darbe, CombatTeam.Neutral)).Damage);

        var shielded = Body(); shielded.ShieldT = 1;
        Assert.True(Combat.Apply(D, shielded, Hit(30)).Blocked);
        Combat.Tick(D, shielded, 1.1f);
        Assert.Equal(30f, Combat.Apply(D, shielded, Hit(30)).Damage);
    }

    [Fact]
    public void Knock_scales_with_mass_and_is_capped()
    {
        var light = Body(); light.Mass = 1;
        var heavy = Body(); heavy.Mass = 4;
        Assert.Equal(8f, Combat.Knock(D, light, 8));
        Assert.Equal(2f, Combat.Knock(D, heavy, 8));
        Assert.Equal(D.Knock.MaxSpeed, Combat.Knock(D, light, 1000));
    }

    [Fact]
    public void Fire_burns_for_three_seconds_at_twenty_percent_of_hit_and_spreads()
    {
        var b = Body(1000);
        Combat.Apply(D, b, Hit(100, DamageType.Ates));
        Assert.Equal(900f, b.Hp, 3);
        var burn = Status.Get(b, StatusKind.Burn)!;
        Assert.Equal(20f, burn.Dps, 3);
        var areas = new List<CombatArea>();
        float dealt = 0;
        for (int i = 0; i < 40; i++) dealt += Combat.Tick(D, b, 0.1f, areas);
        Assert.Equal(60f, dealt, 1); // 3 sn × 20
        Assert.False(Status.Has(b, StatusKind.Burn));
        Assert.Equal(3, areas.Count(a => a.Kind == AreaKind.BurnSpread));

        var near = Body();
        Combat.ApplyArea(D, near, areas[0]);
        Assert.True(Status.Has(near, StatusKind.Burn));
        Assert.Equal(100f, near.Hp); // sicrama hasarsiz durum verir
        var player = Body(team: CombatTeam.Player);
        Combat.ApplyArea(D, player, areas[0]);
        Assert.False(Status.Has(player, StatusKind.Burn)); // yalniz yananin takimina sicrar
    }

    [Fact]
    public void Card_burn_proc_on_slash_uses_hit_damage()
    {
        var b = Body(1000);
        Combat.Apply(D, b, Hit(50, DamageType.Kesme) with { Procs = StatusProc.Burn });
        Assert.Equal(10f, Status.Get(b, StatusKind.Burn)!.Dps, 3);
    }

    [Fact]
    public void Freeze_slows_then_locks_at_stacks()
    {
        var b = Body(1000);
        Combat.Apply(D, b, Hit(1, DamageType.Buz));
        Assert.Equal(1f - D.Freeze.Slow, Status.SpeedMult(D, b), 3);
        Assert.True(Status.CanAct(b));
        CombatResult r = default;
        for (int i = 1; i < D.Freeze.LockStacks; i++) r = Combat.Apply(D, b, Hit(1, DamageType.Buz));
        Assert.True(r.Locked);
        Assert.Equal(0f, Status.SpeedMult(D, b));
        Assert.False(Status.CanAct(b));
        Combat.Tick(D, b, D.Freeze.LockSeconds + 0.01f);
        Assert.True(Status.CanAct(b));
        Assert.Equal(1f, Status.SpeedMult(D, b));
    }

    [Fact]
    public void Shock_stuns_and_chains_once()
    {
        var b = Body(1000);
        var areas = new List<CombatArea>();
        Combat.Apply(D, b, Hit(50, DamageType.Sok), areas);
        Assert.False(Status.CanAct(b));
        var chain = Assert.Single(areas);
        Assert.Equal(AreaKind.ShockChain, chain.Kind);
        Assert.Equal(30f, chain.Amount, 3);
        Assert.Equal(D.Shock.ChainTargets, chain.MaxTargets);

        var next = Body(1000);
        var more = new List<CombatArea>();
        var r = Combat.ApplyArea(D, next, chain, more);
        Assert.Equal(30f, r.Damage, 3);
        Assert.Empty(more); // zincir yeniden sicramaz
        Assert.False(Status.CanAct(next));
    }

    [Fact]
    public void Poison_stacks_capped_and_blocks_heal()
    {
        var b = Body(1000);
        Combat.Apply(D, b, Hit(100, DamageType.Zehir));
        Combat.Apply(D, b, Hit(100, DamageType.Zehir));
        Assert.Equal(16f, Status.Get(b, StatusKind.Poison)!.Dps, 3);
        Assert.Equal(0f, Combat.Heal(b, 50));
        for (int i = 0; i < 100; i++) Combat.Apply(D, b, Hit(1000, DamageType.Zehir) with { Team = CombatTeam.Neutral, IgnoreArmor = true, Amount = 1 });
        Assert.True(Status.Get(b, StatusKind.Poison)!.Dps <= D.Poison.MaxDps);
        Combat.Tick(D, b, D.Poison.Seconds + 0.1f);
        Assert.False(Status.Has(b, StatusKind.Poison));
        Assert.True(Combat.Heal(b, 50) > 0);
    }

    [Fact]
    public void Leaven_swells_and_bursts_at_two_hundred_percent()
    {
        var b = Body(100);
        var areas = new List<CombatArea>();
        var r = Combat.Apply(D, b, Hit(10, DamageType.Maya), areas);
        Assert.False(r.Burst);
        Assert.Equal(10f / 100f * D.Swell.PerMaxHp, b.Swell, 3);
        // Patlama canin bitmesinden once gelir (azami canin BurstAt/PerMaxHp'si kadar Maya).
        Assert.True(D.Swell.BurstAt / D.Swell.PerMaxHp < 1f);
        var big = Body(1000);
        float half = 1000 * D.Swell.BurstAt / D.Swell.PerMaxHp * 0.5f;
        Combat.Apply(D, big, Hit(half, DamageType.Maya), areas);
        Assert.Empty(areas);
        r = Combat.Apply(D, big, Hit(half + 1, DamageType.Maya), areas);
        Assert.True(r.Burst);
        var burst = Assert.Single(areas);
        Assert.Equal(AreaKind.SwellBurst, burst.Kind);
        Assert.Equal(1000 * D.Swell.BurstOfMaxHp, burst.Amount, 3);
        Assert.Equal(0f, big.Swell);
    }

    [Fact]
    public void Burst_hits_neighbours_with_blunt()
    {
        var area = new CombatArea(AreaKind.SwellBurst, 3, int.MaxValue, 50, DamageType.Darbe, CombatTeam.Player);
        var n = Body(100, armor: 100);
        Assert.Equal(25f, Combat.ApplyArea(D, n, area).Damage, 3);
    }

    [Fact]
    public void Kill_clears_statuses_and_dead_body_ignores_hits()
    {
        var b = Body(10);
        Combat.Apply(D, b, Hit(5, DamageType.Ates));
        var r = Combat.Apply(D, b, Hit(50));
        Assert.True(r.Killed);
        Assert.Empty(b.Statuses);
        Assert.Equal(0f, Combat.Apply(D, b, Hit(50)).Damage);
        Assert.Equal(0f, Combat.Heal(b, 10));
    }

    [Fact]
    public void Dot_can_kill_and_stops()
    {
        var b = Body(5);
        Status.AddBurn(D, b, 20);
        float dealt = Combat.Tick(D, b, 1f);
        Assert.True(b.Dead);
        Assert.Equal(5f, dealt, 3);
        Assert.Empty(b.Statuses);
    }

    [Fact]
    public void Raw_applies_crit_and_weak_point()
    {
        Assert.Equal(14f * 2f * D.WeakPointMult, Combat.Raw(14, true, 2f, true, D.WeakPointMult), 3);
        Assert.Equal(14f, Combat.Raw(14, false, 2f, false, 2f));
    }

    [Fact]
    public void Body_round_trips_through_save_json()
    {
        var b = Body(200, "homur", 30);
        Combat.Apply(D, b, Hit(10, DamageType.Ates));
        Combat.Apply(D, b, Hit(10, DamageType.Buz));
        b.Swell = 0.5f;
        var back = SaveJson.From<CombatBody>(SaveJson.Bytes(b));
        Assert.Equal(JsonSerializer.Serialize(b), JsonSerializer.Serialize(back));
        Assert.Equal(2, back.Statuses.Count);
    }
}
