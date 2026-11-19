using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

public class OyukTests
{
    static readonly GameData D = TestData.Load();
    static OyukGuardianDef Mole => D.Oyuk.Get("kostebek_bey");
    static OyukGuardianDef Bat => D.Oyuk.Get("yarasa_yumagi");

    [Fact]
    public void Three_oyuks_per_biome_outside_m1_box_and_rooms_off_map()
    {
        var s = new GameState(D);
        foreach (var biome in new[] { "altin_cayir", "sis_korusu" })
        {
            var list = s.Pois.Where(p => p.Type == "oyuk" && p.Biome == biome).ToList();
            Assert.Equal(3, list.Count);
            Assert.Equal(new[] { 0, 1, 2 }, list.Select(OyukRules.TierOf).OrderBy(t => t));
            Assert.NotNull(D.Oyuk.ForBiome(biome));
            foreach (var p in list) Assert.True(Math.Max(Math.Abs(p.X), Math.Abs(p.Z)) > 146f, $"{p.Id} M1 kutusunda");
        }
        var all = OyukRules.Oyuks(s.Pois);
        float half = D.Biomes.World.Terrain.Size / 2f;
        for (int i = 0; i < all.Count; i++)
        {
            var (x, z) = OyukRules.RoomCenter(D.Oyuk.Interior, i);
            Assert.True(Math.Abs(x - D.Biomes.World.Terrain.CenterX) - D.Oyuk.Interior.Radius > half + 100, "oda oynanabilir karenin icinde");
            if (i > 0) Assert.True(x - OyukRules.RoomCenter(D.Oyuk.Interior, i - 1).x > D.Oyuk.Interior.Radius * 2 + 20);
        }
    }

    [Fact]
    public void Mole_hides_surfaces_and_only_takes_damage_up_top()
    {
        var m = new MoleFight(Mole, 0, new Rng(7));
        Assert.Equal(MoleFight.Phase.Hidden, m.State);
        Assert.Equal(0, m.Hit(50));
        RunUntil(m, () => m.State == MoleFight.Phase.Surfaced);
        Assert.True(m.At >= 0);
        Assert.Equal(50, m.Hit(50));
        RunUntil(m, () => m.State == MoleFight.Phase.Hidden);
    }

    [Fact]
    public void Surfacing_next_to_player_hits_and_watering_its_hole_stuns_for_double_damage()
    {
        var g = Mole;
        // Oyuncu her delikte durursa ilk cikis carpar.
        var m = new MoleFight(g, 1, new Rng(3));
        float got = 0;
        for (int i = 0; i < 400 && m.State != MoleFight.Phase.Surfaced; i++)
        {
            var (hx, hz) = (m.Holes[0].X, m.Holes[0].Z);
            got += m.Tick(0.05f, hx, hz);
            if (m.State == MoleFight.Phase.Surfaced && m.At != 0) { got = -1; break; }
        }
        Assert.True(got == -1 || got == OyukGuardianDef.Tier(g.Damage, 1));

        var w = new MoleFight(g, 0, new Rng(11));
        RunUntil(w, () => w.State == MoleFight.Phase.Surfaced);
        Assert.False(w.Water(w.At, g.FloodLiters - 1));
        Assert.True(w.Water(w.At, 1));
        Assert.Equal(MoleFight.Phase.Stunned, w.State);
        Assert.Equal(20 * g.WeakMult, w.Hit(20));
    }

    [Fact]
    public void Wet_hole_stuns_on_exit_and_plugged_hole_is_never_used()
    {
        var g = Mole;
        var m = new MoleFight(g, 0, new Rng(5));
        // Bir delik hariç hepsini tika, kalanı islat: oradan cikar ve sersemler.
        for (int i = 1; i < m.Holes.Count; i++) Assert.True(m.Plug(i));
        m.Water(0, g.FloodLiters);
        Assert.True(m.Holes[0].Wet > 0);
        RunUntil(m, () => m.State != MoleFight.Phase.Hidden);
        Assert.Equal(0, m.At);
        Assert.Equal(MoleFight.Phase.Stunned, m.State);
        for (int k = 0; k < 2000; k++) { m.Tick(0.05f, 99, 99); Assert.True(m.At <= 0); }
    }

    [Fact]
    public void All_holes_plugged_traps_the_mole_in_the_middle()
    {
        var g = Mole;
        var m = new MoleFight(g, 2, new Rng(9));
        for (int i = 0; i < m.Holes.Count; i++) m.Plug(i);
        Assert.Equal(MoleFight.Phase.Trapped, m.State);
        Assert.True(m.Weak);
        Assert.Equal(10 * g.WeakMult, m.Hit(10));
        float hurt = 0;
        for (int k = 0; k < (int)((g.TrappedStunSeconds + g.TrappedLungeEvery * 2) / 0.05f); k++) hurt += m.Tick(0.05f, 0.5f, 0.5f);
        Assert.True(hurt > 0, "sikisik kostebek hamle etmedi");
        Assert.Equal(MoleFight.Phase.Trapped, m.State);
        while (!m.Dead) m.Hit(100);
        Assert.Equal(0, m.Hp);
    }

    [Fact]
    public void Bat_knot_scatters_in_light_regroups_in_dark_and_dives_while_gathered()
    {
        var g = Bat;
        var b = new BatKnotFight(g, 0);
        Assert.False(b.Vulnerable);
        Assert.Equal(0, b.Hit(30));
        float dive = 0;
        for (int k = 0; k < (int)(OyukGuardianDef.Tier(g.DiveEvery, 0) / 0.05f) + 2; k++) dive += b.Tick(0.05f, 0, 0);
        Assert.Equal(OyukGuardianDef.Tier(g.Damage, 0), dive);
        Assert.True(b.InLight(g.LightRadius - 0.5f, 0, 0, 0));
        Assert.False(b.InLight(g.LightRadius + 0.5f, 0, 0, 0));
        b.Lit = true; b.Tick(0.05f, 0, 0);
        Assert.True(b.Scattered && b.Vulnerable);
        Assert.Equal(30 * g.WeakMult, b.Hit(30));
        b.Lit = false;
        float regroup = OyukGuardianDef.Tier(g.RegroupSeconds, 0);
        for (float t = 0; t < regroup - 0.2f; t += 0.05f) b.Tick(0.05f, 0, 0);
        Assert.True(b.Scattered);
        for (float t = 0; t < 0.5f; t += 0.05f) b.Tick(0.05f, 0, 0);
        Assert.False(b.Scattered);
        Assert.Equal(1, b.Scatters);
    }

    [Fact]
    public void Chests_open_once_after_clear_and_split_the_tier_reward()
    {
        var p = new OyukProgress();
        var g = Mole;
        Assert.Empty(p.OpenChest("oyuk_altin_cayir_1", g, 1, 0));
        p.Of("oyuk_altin_cayir_1").Cleared = true;
        var a = p.OpenChest("oyuk_altin_cayir_1", g, 1, 0);
        var b = p.OpenChest("oyuk_altin_cayir_1", g, 1, 1);
        Assert.Empty(p.OpenChest("oyuk_altin_cayir_1", g, 1, 0));
        foreach (var (item, list) in g.Rewards)
            Assert.Equal(OyukGuardianDef.Tier(list, 1), a.GetValueOrDefault(item) + b.GetValueOrDefault(item));
        Assert.Equal(1, p.ClearedCount);
    }

    [Fact]
    public void Save_section_round_trips_in_place()
    {
        var s = new GameState(D);
        var keep = s.Oyuk;
        s.Oyuk.Of("oyuk_sis_korusu_0").Cleared = true;
        s.Oyuk.Of("oyuk_sis_korusu_0").Looted.Add(1);
        var copy = new OyukProgress();
        copy.LoadFrom(s.Oyuk);
        s.Oyuk.LoadFrom(new OyukProgress());
        Assert.False(s.Oyuk.IsCleared("oyuk_sis_korusu_0"));
        s.Oyuk.LoadFrom(copy);
        Assert.Same(keep, s.Oyuk);
        Assert.True(s.Oyuk.IsCleared("oyuk_sis_korusu_0"));
        Assert.Equal(new[] { 1 }, s.Oyuk.Of("oyuk_sis_korusu_0").Looted);
    }

    static void RunUntil(MoleFight m, Func<bool> done)
    {
        for (int i = 0; i < 4000 && !done(); i++) m.Tick(0.05f, 99, 99);
        Assert.True(done(), "kostebek beklenen hale gelmedi");
    }
}
