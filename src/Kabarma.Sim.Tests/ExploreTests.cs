using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M2 dunyasi (dunya2): biyom haritasi, dogma tablolari, kesif noktasi yerlesimi, kesif durumu, mancinik
/// (GDD §21, ICERIK §17 "M2 dunyasindaki T1-T2 noktalari", TEKNIK §7 "M2 dunyasi").
/// </summary>
public class ExploreTests
{
    static GameData Data640()
    {
        var d = TestData.Load();
        d.Biomes.World.Terrain.Size = 640; // M2 dunyasi (veri birlesince 640 olur; test boyuttan bagimsiz kurali sinar)
        return d;
    }

    [Fact]
    public void Biome_map_starts_in_meadow_and_turns_into_forest_toward_the_crater()
    {
        var d = Data640();
        var m = new BiomeMap(d.Biomes);
        Assert.Equal("altin_cayir", m.BiomeAt(0, 0));
        Assert.Equal(1f, m.Weight("altin_cayir", 0, 0), 3);
        Assert.Equal(0f, m.Weight("sis_korusu", 0, 0), 3);
        // Krater yonunde 300 m: Koru.
        var p = d.Biomes.World.Pillar;
        float len = MathF.Sqrt(p[0] * p[0] + p[1] * p[1]);
        float kx = p[0] / len * 300, kz = p[1] / len * 300;
        Assert.Equal("sis_korusu", m.BiomeAt(kx, kz));
        Assert.True(m.Weight("sis_korusu", kx, kz) > 0.99f);
        // Agirliklarin toplami 1, gecis yumusak (bant icinde ara degerler var).
        bool sawMix = false;
        for (float t = 0; t <= 320; t += 2)
        {
            float x = p[0] / len * t, z = p[1] / len * t;
            float a = m.Weight("altin_cayir", x, z), b = m.Weight("sis_korusu", x, z);
            Assert.Equal(1f, a + b, 3);
            if (a > 0.2f && b > 0.2f) sawMix = true;
        }
        Assert.True(sawMix, "gecis bandi yok");
        Assert.Equal(2, m.Tier("sis_korusu"));
        // Uste uste ayni sonuc (tohumlu, durumsuz).
        Assert.Equal(m.Depth(123.4f, 56.7f), new BiomeMap(Data640().Biomes).Depth(123.4f, 56.7f));
    }

    [Fact]
    public void M1_meadow_core_stays_meadow()
    {
        var m = new BiomeMap(Data640().Biomes);
        // Us, gol, tepe ve kapsul Cayir'in icinde kalir (M1 yerlesimi bozulmaz).
        foreach (var (x, z) in new[] { (0f, 0f), (-67f, 34f), (50f, -87f), (9f, -9f), (0f, 100f), (-100f, 60f) })
            Assert.True(m.Weight("altin_cayir", x, z) > 0.99f, $"({x},{z}) cayir degil");
    }

    [Fact]
    public void Pois_follow_the_icerik_17_table_and_rules()
    {
        var d = Data640();
        var map = new BiomeMap(d.Biomes);
        var pois = PoiPlacer.Place(d.Biomes, map);
        foreach (var biome in new[] { "altin_cayir", "sis_korusu" })
        foreach (var rule in d.Biomes.Biomes[biome].Pois)
        {
            var mine = pois.Where(p => p.Biome == biome && p.Type == rule.Type).ToList();
            Assert.True(mine.Count == rule.Count, $"{biome} {rule.Type} {mine.Count}/{rule.Count}");
            foreach (var p in mine)
            {
                Assert.True(map.Weight(biome, p.X, p.Z) >= rule.MinWeight - 1e-3f || rule.Fixed.Any(f => f[0] == p.X && f[1] == p.Z), $"{p.Id} biyom disinda");
                Assert.False(PoiPlacer.InLake(d.Biomes.World, p.X, p.Z), $"{p.Id} golde");
                Assert.True(MathF.Abs(p.X) < 320 && MathF.Abs(p.Z) < 320);
                foreach (var q in mine.Where(q => q != p))
                    Assert.True(MathF.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Z - q.Z) * (p.Z - q.Z)) >= rule.MinSpacing, $"{p.Id}-{q.Id} yakin");
            }
        }
        // ICERIK 17 M2 tablosu: iki biyomda Seyir Kulesi 1, Ata Tasi 1, Yabani Bahce 1, Yuva 2, Enkaz 3.
        foreach (var biome in new[] { "altin_cayir", "sis_korusu" })
        {
            int N(string t) => pois.Count(p => p.Biome == biome && p.Type == t);
            Assert.Equal((1, 1, 1, 2, 3), (N("seyir_kulesi"), N("ata_tasi"), N("yabani_bahce"), N("yuva"), N("enkaz")));
        }
        // M2'de calismayan turler yerlesmez; her kural turu tanimli.
        Assert.DoesNotContain(pois, p => d.Biomes.World.PoiTypes[p.Type].Milestone != "M2");
        // Kimlikler tekil ve tohumdan kararli.
        Assert.Equal(pois.Count, pois.Select(p => p.Id).Distinct().Count());
        var again = PoiPlacer.Place(Data640().Biomes, new BiomeMap(Data640().Biomes));
        Assert.Equal(pois.Select(p => (p.Id, p.X, p.Z)), again.Select(p => (p.Id, p.X, p.Z)));
    }

    [Fact]
    public void Spawn_tables_pick_by_time_weight_and_cap()
    {
        var d = Data640();
        var map = new BiomeMap(d.Biomes);
        var t = new SpawnTable(d.Biomes, map);
        // Her biyomun gunduz ve gece satiri var; her satirin sayilari gecerli.
        foreach (var (id, b) in d.Biomes.Biomes)
            foreach (var c in b.Spawns.Creatures)
            {
                Assert.False(string.IsNullOrEmpty(c.Creature));
                Assert.Contains(c.Time, new[] { "day", "night", "any" });
                Assert.True(c.Group.Count == 2 && c.Group[0] >= 1 && c.Group[1] >= c.Group[0], $"{id}/{c.Creature} group");
                Assert.True(c.BaseDistance.Count == 2 && c.BaseDistance[1] > c.BaseDistance[0]);
                Assert.True(c.MaxGroups >= 1 && c.Weight > 0);
            }
        Assert.Contains(t.Candidates("sis_korusu", night: true), c => c.Creature == "hu");
        Assert.DoesNotContain(t.Candidates("sis_korusu", night: true), c => c.Creature == "dalli");
        Assert.Contains(t.Candidates("sis_korusu", night: false), c => c.Creature == "dalli");
        var rng = new Rng(7);
        // Dolu satir secilmez; bilinmeyen tur atlanir.
        var only = t.Pick("sis_korusu", false, rng, c => c.Creature == "dalli" ? 0 : 99);
        Assert.Equal("dalli", only?.Creature);
        Assert.Null(t.Pick("sis_korusu", false, rng, _ => 0, known: _ => false));
        // Dogma yeri kurallara uyar.
        var dalli = d.Biomes.Biomes["sis_korusu"].Spawns.Creatures.First(c => c.Creature == "dalli");
        for (int i = 0; i < 30; i++)
        {
            var at = t.Place("sis_korusu", dalli, rng);
            Assert.NotNull(at);
            var (x, z) = at!.Value;
            Assert.True(map.Weight("sis_korusu", x, z) >= dalli.MinWeight);
            Assert.InRange(MathF.Sqrt(x * x + z * z), dalli.BaseDistance[0], dalli.BaseDistance[1]);
            int g = SpawnTable.GroupSize(dalli, rng);
            Assert.InRange(g, dalli.Group[0], dalli.Group[1]);
        }
        // Ata satirlari kendi biyomunun Ata Tasi'na baglanir (boss akisi).
        var pois = PoiPlacer.Place(d.Biomes, map);
        var dede = t.AtaSite("boynuzlu_dede", pois);
        Assert.NotNull(dede);
        Assert.Equal("sis_korusu", dede!.Value.site.Biome);
        Assert.Equal("altin_cayir", t.AtaSite("koca_pofuduk", pois)!.Value.site.Biome);
    }

    [Fact]
    public void Exploration_reveals_climbs_takes_and_round_trips_as_json()
    {
        var d = Data640();
        var e = new Exploration(d);
        Assert.Equal(80, e.N); // 640 / 8
        Assert.Equal(0, e.Percent);
        int got = e.Reveal(0, 0, 45);
        Assert.True(got > 80 && e.IsRevealedAt(0, 0) && e.IsRevealedAt(40, 0) && !e.IsRevealedAt(90, 0));
        Assert.Equal(0, e.Reveal(0, 0, 45)); // ikinci kez yeni hucre yok
        // Seyir Kulesi: 600 m bu haritanin tamamini acar.
        var map = new BiomeMap(d.Biomes);
        var pois = PoiPlacer.Place(d.Biomes, map);
        var tower = pois.First(p => p.Type == "seyir_kulesi");
        var tt = d.Biomes.World.PoiTypes["seyir_kulesi"];
        Assert.True(e.Climb(tower, tt));
        Assert.False(e.Climb(tower, tt));
        Assert.Contains(tower.Id, e.Found);
        Assert.True(e.Percent > 90f, $"kule acti %{e.Percent:0}");
        // Enkaz: Kazma ile vurus basina perHit; bitince 5 gun sonra yenilenir.
        var enkaz = pois.First(p => p.Type == "enkaz");
        var et = d.Biomes.World.PoiTypes["enkaz"];
        int total = 0;
        for (int i = 0; i < 20; i++) total += e.Take(enkaz, et, day: 3, want: et.PerHit).Sum(x => x.count);
        Assert.Equal(12, total);
        Assert.Equal(0, e.Left(enkaz, et, day: 7));
        Assert.Equal(12, e.Left(enkaz, et, day: 8));
        // Yabani Bahce biyoma gore farkli tohum verir.
        var bahce = pois.First(p => p.Type == "yabani_bahce" && p.Biome == "altin_cayir");
        var items = e.Take(bahce, d.Biomes.World.PoiTypes["yabani_bahce"], 1, 100);
        Assert.Equal(new[] { ("turnip_seed", 6), ("wheat", 4) }, items.ToArray());
        // Isaretler.
        var mk = e.AddMarker(10, 20, "Bakır");
        Assert.True(e.RemoveMarker(mk.Id));
        e.AddMarker(-5, 5, "Ev");
        // Kayit: duz JSON gidip gelir (TEKNIK §9).
        var json = JsonSerializer.Serialize(e);
        var back = JsonSerializer.Deserialize<Exploration>(json)!;
        Assert.Equal(e.Revealed, back.Revealed);
        Assert.Equal(e.Bits, back.Bits);
        Assert.True(back.IsRevealedAt(0, 0));
        Assert.Equal(e.Climbed, back.Climbed);
        Assert.Equal("Ev", back.Markers.Single().Label);
        Assert.Equal(12, back.Left(enkaz, et, 8)); // gun 8'de yenilenmis (Left yenilemeyi uygular)
    }

    [Fact]
    public void Catapult_refuses_cargo_closed_and_near_stops_and_plans_an_arc()
    {
        var def = Data640().Biomes.World.Catapult;
        var a = new CatapultStop { Id = "ocak", X = 0, Z = 0 };
        var b = new CatapultStop { Id = "kule", X = 200, Z = 100, Y = 10 };
        var c = new CatapultStop { Id = "yakin", X = 10, Z = 0 };
        Assert.Equal(Catapult.Refusal.None, Catapult.Check(def, a, b, carrying: false));
        Assert.Equal(Catapult.Refusal.Carrying, Catapult.Check(def, a, b, carrying: true));
        Assert.Equal(Catapult.Refusal.TooClose, Catapult.Check(def, a, c, false));
        Assert.Equal(Catapult.Refusal.Same, Catapult.Check(def, a, a, false));
        Assert.Equal(Catapult.Refusal.NoStop, Catapult.Check(def, null, b, false));
        b.Open = false;
        Assert.Equal(Catapult.Refusal.Closed, Catapult.Check(def, a, b, false));
        b.Open = true;
        var f = Catapult.Plan(def, a, b, (x, z) => 10f);
        Assert.InRange(f.Seconds, def.FlightBase, def.FlightMax);
        var (x0, y0, z0) = f.Sample(0);
        var (x1, y1, z1) = f.Sample(f.Seconds);
        var (_, ym, _) = f.Sample(f.Seconds / 2);
        Assert.Equal((0f, 1.5f), (x0, y0));
        Assert.Equal(10f, y1, 3);
        // Hedef duragin landOffset berisine iner; yayin tepesi iki uctan yuksek.
        float land = MathF.Sqrt((x1 - b.X) * (x1 - b.X) + (z1 - b.Z) * (z1 - b.Z));
        Assert.Equal(def.LandOffset, land, 2);
        Assert.True(ym > Math.Max(y0, y1) + def.ApexMin * 0.9f);
    }
}
