using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 ara adimi (TEKNIK §7, 1800 m dunya): Cayir ve Koru'nun 640 m'lik cekirdegi bit bit korunur, dort yeni biyom
/// kraterin cevresinde dilim dilim yerlesir, keşif noktalari ve kayit tasimasi (Exploration.RemapTo).
/// </summary>
public class BiomeCoreTests
{
    static GameData M2()
    {
        var d = TestData.Load();
        var w = d.Biomes.World;
        w.Terrain.Size = 640; w.Terrain.Center.Clear(); w.Terrain.CoreSize = 0;
        w.BiomeMap.Order = new List<string> { "altin_cayir", "sis_korusu" };
        w.BiomeMap.Home.Clear(); w.BiomeMap.Slices.Clear(); w.BiomeMap.Crater = new CraterDef();
        return d;
    }

    static readonly GameData D = TestData.Load();
    static readonly BiomeMap Map = new(D.Biomes);

    [Fact]
    public void Core_weights_equal_the_m2_world_exactly()
    {
        var old = new BiomeMap(M2().Biomes);
        for (float x = -316; x <= 316; x += 12)
        for (float z = -316; z <= 316; z += 12)
        {
            Assert.Equal(old.Weight("altin_cayir", x, z), Map.Weight("altin_cayir", x, z));
            Assert.Equal(old.Weight("sis_korusu", x, z), Map.Weight("sis_korusu", x, z));
            foreach (var id in new[] { "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" })
                Assert.Equal(0f, Map.Weight(id, x, z));
        }
    }

    [Fact]
    public void Weights_sum_to_one_everywhere_and_each_biome_has_its_region()
    {
        var w = D.Biomes.World.Terrain;
        float half = w.Size / 2f;
        var seen = new HashSet<string>();
        for (float x = w.CenterX - half; x <= w.CenterX + half; x += 40)
        for (float z = w.CenterZ - half; z <= w.CenterZ + half; z += 40)
        {
            float sum = 0;
            foreach (var id in Map.Order) sum += Map.Weight(id, x, z);
            Assert.Equal(1f, sum, 3);
            seen.Add(Map.BiomeAt(x, z));
        }
        Assert.Equal(6, seen.Count);
    }

    [Fact]
    public void Crater_is_gobek_and_slices_point_west_north_east()
    {
        var c = D.Biomes.World.BiomeMap.Crater;
        Assert.Equal("gobek", Map.BiomeAt(c.Center[0], c.Center[1]));
        Assert.True(Map.Weight("gobek", c.Center[0], c.Center[1]) > 0.99f);
        // +X bati, +Z kuzey, -X dogu (biomes.json ekseni): krater cevresinde 400 m.
        Assert.Equal("isil_sazlik", Map.BiomeAt(c.Center[0] + 400 * MathF.Cos(MathF.PI * 40 / 180), c.Center[1] + 400 * MathF.Sin(MathF.PI * 40 / 180)));
        Assert.Equal("kizil_kanyon", Map.BiomeAt(c.Center[0] + 400 * MathF.Cos(MathF.PI * 120 / 180), c.Center[1] + 400 * MathF.Sin(MathF.PI * 120 / 180)));
        Assert.Equal("ayaz_yaylasi", Map.BiomeAt(c.Center[0] + 400 * MathF.Cos(MathF.PI * 200 / 180), c.Center[1] + 400 * MathF.Sin(MathF.PI * 200 / 180)));
        // Ocak yonunde Cayir, kraterin eteginde Koru.
        Assert.Equal("altin_cayir", Map.BiomeAt(0, 0));
        Assert.Equal("sis_korusu", Map.BiomeAt(c.Center[0] + 300 * MathF.Cos(MathF.PI * -60 / 180), c.Center[1] + 300 * MathF.Sin(MathF.PI * -60 / 180)));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, Map.Order.Select(Map.Tier).ToArray());
    }

    [Fact]
    public void Crater_rim_rises_only_around_the_crater()
    {
        var c = D.Biomes.World.BiomeMap.Crater;
        Assert.Equal(0f, Map.RimHeight(c.Center[0], c.Center[1]));
        Assert.Equal(0f, Map.RimHeight(0, 0));
        float rimAt = c.Radius * (c.RimStart + c.RimEnd) / 2f;
        Assert.True(Map.RimHeight(c.Center[0] + rimAt, c.Center[1]) > c.RimHeight * 0.9f);
        Assert.Equal(0f, Map.RimHeight(c.Center[0] + c.Radius * c.RimEnd + 1, c.Center[1]));
    }

    [Fact]
    public void Core_pois_are_unchanged_and_new_biomes_get_theirs_with_zengin_yatak()
    {
        var m2 = M2();
        var oldPois = PoiPlacer.Place(m2.Biomes, new BiomeMap(m2.Biomes));
        var pois = PoiPlacer.Place(D.Biomes, Map);
        Assert.Equal(oldPois.Select(p => (p.Id, p.X, p.Z)), pois.Where(p => p.Biome is "altin_cayir" or "sis_korusu").Select(p => (p.Id, p.X, p.Z)));
        foreach (var id in new[] { "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" })
        {
            var mine = pois.Where(p => p.Biome == id).ToList();
            foreach (var rule in D.Biomes.Biomes[id].Pois) Assert.Equal(rule.Count, mine.Count(p => p.Type == rule.Type));
            Assert.Contains(mine, p => p.Type == "zengin_yatak");
            Assert.Contains(mine, p => p.Type == "seyir_kulesi");
            foreach (var p in mine) Assert.True(Map.Weight(id, p.X, p.Z) > 0.5f, p.Id);
        }
        Assert.Equal(7, pois.Count(p => p.Type == "zengin_yatak"));
        Assert.Equal(pois.Count, pois.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Every_new_biome_has_defs_and_the_t3_to_t6_resources_exist_as_nodes()
    {
        var items = D.Biomes.Biomes.Values.SelectMany(b => b.Nodes.Select(n => n.Item)).ToHashSet();
        foreach (var it in new[] { "turba", "kukurt", "kaucuk_sutu", "balcik", "isil_yosun", "kum", "kuvars", "titan_ore", "katran", "tuz", "buz", "ayaz_kristali", "ham_maya", "obsidyen", "isil_cevher", "kemik" })
            Assert.Contains(it, items);
        foreach (var id in Map.Order)
        {
            Assert.True(D.Biomes.Biomes.ContainsKey(id), id);
            Assert.Equal(5, D.Biomes.Biomes[id].Times.Count);
        }
    }

    [Fact]
    public void Exploration_remaps_a_640m_save_into_the_1800m_world()
    {
        var old = new Exploration(640, 8, 0, 0);
        old.Reveal(0, 0, 100);
        old.Found.Add("x"); old.AddMarker(10, 10, "m");
        int before = old.Revealed;
        var t = D.Biomes.World.Terrain;
        var now = old.RemapTo(t.Size, D.Biomes.World.Map.Cell, t.CenterX, t.CenterZ);
        Assert.Equal(1800f, now.WorldSize);
        Assert.Equal(before, now.Revealed);
        Assert.True(now.IsRevealedAt(0, 0) && now.IsRevealedAt(80, 0));
        Assert.False(now.IsRevealedAt(400, 0)); // eskiden de kapaliydi
        Assert.False(now.IsRevealedAt(-1000, 600)); // yeni bolge
        Assert.Equal(old.Found, now.Found);
        Assert.Single(now.Markers);
        Assert.Same(old, old.RemapTo(640, 8, 0, 0)); // ayni boy ve merkezde dokunmaz
    }

    [Fact]
    public void Save_section_restores_old_exploration_into_the_new_world()
    {
        var s = new GameState(D);
        var old = new Exploration(640, 8, 0, 0);
        old.Reveal(0, 0, 60);
        var json = System.Text.Json.JsonSerializer.Serialize(old);
        var back = System.Text.Json.JsonSerializer.Deserialize<Exploration>(json)!;
        s.Explore = back.RemapTo(D.Biomes.World.Terrain.Size, D.Biomes.World.Map.Cell, D.Biomes.World.Terrain.CenterX, D.Biomes.World.Terrain.CenterZ);
        Assert.True(s.Explore.IsRevealedAt(0, 0));
        Assert.Equal(1800f, s.Explore.WorldSize);
    }
}
