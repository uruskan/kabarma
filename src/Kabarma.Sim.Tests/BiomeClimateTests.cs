using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Biyom sozlesmesi (M4 biyom): kimlikler, kademeler, iklim tehlikesi ve direnc (GDD §3.5, ICERIK §1).</summary>
public class BiomeClimateTests
{
    static readonly GameData D = TestData.Load();
    static readonly BiomeClimate C = new(D);

    static ClimateContext Ctx(bool night = false, string[]? gear = null, string[]? foods = null, string[]? zones = null, string[]? cond = null) => new()
    {
        Night = night,
        Gear = new HashSet<string>(gear ?? Array.Empty<string>()),
        Foods = new HashSet<string>(foods ?? Array.Empty<string>()),
        Zones = new HashSet<string>(zones ?? Array.Empty<string>()),
        Conditions = new HashSet<string>(cond ?? Array.Empty<string>()),
    };

    [Fact]
    public void Six_biomes_in_tier_order_with_ids_from_kimlikler()
    {
        Assert.Equal(new[] { "altin_cayir", "sis_korusu", "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" }, C.BiomeIds.ToArray());
        for (int i = 0; i < 6; i++) Assert.Equal(i + 1, C.Tier(C.BiomeIds[i]));
        Assert.Equal(0, C.Tier("yok"));
    }

    [Fact]
    public void Map_tiers_agree_with_the_contract_for_mapped_biomes()
    {
        var map = new BiomeMap(D.Biomes);
        foreach (var id in map.Order) Assert.Equal(C.Tier(id), map.Tier(id));
    }

    [Fact]
    public void Every_hazard_reference_exists_and_drain_is_1_to_3()
    {
        foreach (var (id, b) in D.Climate.Biomes)
            foreach (var h in b.Hazards) Assert.True(D.Climate.Hazards.ContainsKey(h.Hazard), $"{id}: {h.Hazard}");
        Assert.All(D.Climate.Hazards.Values, h => Assert.InRange(h.DrainPerSecond, 1f, 3f));
        Assert.All(C.BiomeIds, id => Assert.True(D.Climate.Biomes.ContainsKey(id), id));
    }

    [Fact]
    public void Meadow_and_grove_are_safe()
    {
        Assert.Empty(C.Exposure("altin_cayir", Ctx()));
        Assert.Empty(C.Exposure("sis_korusu", Ctx(night: true)));
    }

    [Fact]
    public void Gas_only_in_pits_and_mask_resists()
    {
        Assert.Empty(C.Exposure("isil_sazlik", Ctx()));
        var bare = C.Exposure("isil_sazlik", Ctx(zones: new[] { "gas_pit" }));
        Assert.Single(bare);
        Assert.False(bare[0].Resisted);
        Assert.True(bare[0].DrainPerSecond > 0 && bare[0].BlocksStaminaRegen);
        var masked = C.Exposure("isil_sazlik", Ctx(gear: new[] { "filtre_maskesi" }, zones: new[] { "gas_pit" }));
        Assert.True(masked[0].Resisted);
        Assert.Equal(0f, masked[0].DrainPerSecond);
    }

    [Fact]
    public void Canyon_heat_is_a_day_hazard_resisted_by_vest_food_shade_or_wet()
    {
        Assert.Empty(C.Exposure("kizil_kanyon", Ctx(night: true)));
        Assert.False(C.Exposure("kizil_kanyon", Ctx())[0].Resisted);
        Assert.True(C.Exposure("kizil_kanyon", Ctx(gear: new[] { "serin_yelek" }))[0].Resisted);
        Assert.True(C.Exposure("kizil_kanyon", Ctx(foods: new[] { "ayran" }))[0].Resisted);
        Assert.True(C.Exposure("kizil_kanyon", Ctx(cond: new[] { "shade" }))[0].Resisted);
        Assert.True(C.Exposure("kizil_kanyon", Ctx(cond: new[] { "wet" }))[0].Resisted);
        Assert.False(C.Exposure("kizil_kanyon", Ctx(gear: new[] { "termal_astar" }))[0].Resisted); // yanlis donanim
    }

    [Fact]
    public void Plateau_cold_and_core_maya_burn_have_their_resistances()
    {
        Assert.False(C.Exposure("ayaz_yaylasi", Ctx(night: true))[0].Resisted);
        Assert.True(C.Exposure("ayaz_yaylasi", Ctx(gear: new[] { "termal_astar" }))[0].Resisted);
        Assert.True(C.Exposure("ayaz_yaylasi", Ctx(foods: new[] { "kar_cayi" }))[0].Resisted);
        Assert.True(C.Exposure("ayaz_yaylasi", Ctx(cond: new[] { "shelter" }))[0].Resisted);
        Assert.False(C.Exposure("gobek", Ctx())[0].Resisted);
        Assert.True(C.Exposure("gobek", Ctx(gear: new[] { "maya_yalitimi" }))[0].Resisted);
        Assert.True(C.Exposure("gobek", Ctx(foods: new[] { "eksi_maya_ekmegi" }))[0].Resisted);
    }

    [Fact]
    public void Total_sums_unresisted_and_blocks_regen()
    {
        var (drain, block) = C.Total("gobek", Ctx());
        Assert.True(drain > 0 && block);
        var (d2, b2) = C.Total("gobek", Ctx(gear: new[] { "maya_yalitimi" }));
        Assert.Equal(0f, d2);
        Assert.False(b2);
    }

    [Fact]
    public void Entry_gear_is_defined_for_the_hazardous_biomes()
    {
        foreach (var id in new[] { "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" })
            Assert.NotEmpty(C.EntryGear(id));
        Assert.Empty(C.EntryGear("altin_cayir"));
    }
}
