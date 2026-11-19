using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya sinamasinin M2 bolumu (dunya2): Sis Korusu biyomu, Cayir'la gecis, kesif noktalari, sis ve isik.
///  - Koru'nun bitki ortusu, dev mantar ve agaclari yerlesti; agaclar Sert Odun verir; Demir, Recine, Guhercile dugumleri.
///  - Koru'nun yuksekligi Cayir'dan farkli (biyom terimi), gecis bandinda biyom agirligi yumusak.
///  - 16 kesif noktasi (ICERIK 17 M2 tablosu), Seyir Kulesi modelli ve duzlestirilmis, Enkaz Hurda kayasi, Bahce tutamlari.
///  - Sis katmanlari ve isik huzmeleri kuruldu; kamera Koru'dayken isik Koru saatlerine karisir.
/// </summary>
public partial class WorldTest
{
    void RunM2(World w)
    {
        var koru = w.Biomes.Biomes["sis_korusu"];
        // Bitki ortusu ve dugumler.
        foreach (var def in koru.Decor)
        {
            string key = w.DecorCounts.ContainsKey("sis_korusu_" + def.Id) ? "sis_korusu_" + def.Id : def.Id;
            int got = w.DecorCounts.TryGetValue(key, out var c) ? c : 0;
            Check(got >= def.Count * 0.6f, $"koru decor {def.Id} placed {got}/{def.Count}");
        }
        var koruTrees = w.TreePositions.Where(t => w.Map.Weight("sis_korusu", t.X, t.Z) > 0.5f).ToList();
        Check(koruTrees.Count >= 60, $"koru trees {koruTrees.Count}");
        Check(koruTrees.Count(t => w.TreeWood(t) == "hard_wood") >= 60, "koru trees do not give hard_wood");
        Check(w.TreeWood(w.TreePositions.OrderBy(t => t.Length()).First()) == "wood", "meadow tree is not wood");
        foreach (var item in new[] { "iron_ore", "resin", "saltpeter" })
        {
            var rocks = w.Rocks.Where(r => r.Item == item).ToList();
            Check(rocks.Count >= 4, $"koru node {item} {rocks.Count}");
            Check(rocks.All(r => w.Map.Weight("sis_korusu", r.GlobalPosition.X, r.GlobalPosition.Z) >= 0.69f), $"{item} outside Koru");
        }
        _info.Add($"koru_trees={koruTrees.Count} iron={w.Rocks.Count(r => r.Item == "iron_ore")} resin={w.Rocks.Count(r => r.Item == "resin")} saltpeter={w.Rocks.Count(r => r.Item == "saltpeter")}");

        // Arazi: Koru'nun yukseklik dagilimi Cayir'dan farkli; ussun 42 m'si ayni (yukarida sinandi).
        float koruVar = 0, n = 0, koruMax = -1e9f;
        for (float x = -300; x <= 300; x += 6)
        for (float z = -300; z <= 300; z += 6)
            if (w.Map.Weight("sis_korusu", x, z) > 0.95f) { float h = w.HeightAt(x, z); koruVar += h; n++; koruMax = Mathf.Max(koruMax, h); }
        Check(n > 600, $"koru area samples {n}");
        _info.Add($"koru_area_m2~{n * 36:0} koru_mean_h={(n > 0 ? koruVar / n : 0):0.0} koru_max_h={koruMax:0.0}");
        // Gecis: ussten kratere giden cizgide agirlik yumusak artar (en az 4 ara deger).
        var p = w.Biomes.World.Pillar;
        var dir = new Vector2(p[0], p[1]).Normalized();
        int mid = 0;
        for (float t = 0; t < 320; t += 2) { float k = w.Map.Weight("sis_korusu", dir.X * t, dir.Y * t); if (k > 0.1f && k < 0.9f) mid++; }
        Check(mid >= 4, $"no soft transition ({mid} samples)");

        // Kesif noktalari.
        // M2 noktalari 16 + M3 Oyuklar 6 (oyuk akisi; ICERIK 17 "M3: Oyuklar").
        var coreBiomes = w.Pois.Where(q => q.Biome is "altin_cayir" or "sis_korusu").ToList();
        Check(coreBiomes.Count(q => q.Type != "oyuk") == 16, $"M2 pois {coreBiomes.Count(q => q.Type != "oyuk")} != 16");
        Check(coreBiomes.Count(q => q.Type == "oyuk") == 6, $"oyuk pois {coreBiomes.Count(q => q.Type == "oyuk")} != 6");
        foreach (var poi in w.Pois)
        {
            var type = w.PoiType(poi.Type);
            if (type.Flatten > 0)
            {
                float s = w.SlopeAt(poi.X, poi.Z);
                Check(s < 8f, $"poi {poi.Id} not flattened (slope {s:0.0})");
            }
            Check(!w.IsWater(w.PoiPos(poi)), $"poi {poi.Id} in water");
        }
        foreach (var tower in w.Pois.Where(q => q.Type == "seyir_kulesi"))
        {
            Check(w.PoiNodes.ContainsKey(tower.Id), $"tower {tower.Id} has no model");
            Check(w.TowerTop(tower).Y - w.PoiPos(tower).Y > 12f, "tower too low");
        }
        Check(w.Pois.Where(q => q.Type == "enkaz").All(q => w.PoiRocks.TryGetValue(q.Id, out var r) && r.Item == "scrap"), "enkaz is not a scrap rock");
        Check(w.Pois.Where(q => q.Type == "yabani_bahce").All(q => w.PoiTufts.TryGetValue(q.Id, out var l) && l.Sum(t => t.Left) > 0), "bahce has no tufts");
        Check(w.Pois.Any(q => q.Type == "ata_tasi" && q.Biome == "altin_cayir" && Mathf.IsEqualApprox(q.X, 64f) && Mathf.IsEqualApprox(q.Z, 36f)), "cayir ata tasi not at boss arena (64,36)");
        _info.Add($"pois={w.Pois.Count} " + string.Join(" ", w.Pois.Where(q => q.Type is "seyir_kulesi" or "ata_tasi").Select(q => $"{q.Id}@{q.X:0},{q.Z:0}")));

        // Sis katmanlari, huzmeler, isik karisimi.
        Check(w.FogLayerChunks >= 6, $"fog chunks {w.FogLayerChunks}");
        Check(w.ShaftCount >= koru.Atmosphere.Shafts / 2, $"shafts {w.ShaftCount}");
        w.ForceTime("ogle");
        w.ForcedBiomeMix = 0f; w.SnapBiomeMix(Vector3.Zero);
        float fogMeadow = w.Env.Environment.FogDensity;
        var tower2 = w.Pois.First(q => q.Type == "seyir_kulesi" && q.Biome == "sis_korusu");
        w.SnapBiomeMix(w.PoiPos(tower2));
        float fogKoru = w.Env.Environment.FogDensity;
        Check(w.CameraBiomeMix > 0.9f && fogKoru > fogMeadow * 2f, $"koru light mix {w.CameraBiomeMix:0.00} fog {fogMeadow:0.0000}->{fogKoru:0.0000}");
        w.ForcedBiomeMix = null; w.SnapBiomeMix(Vector3.Zero); w.ForceTime(null);
        _info.Add($"fog_chunks={w.FogLayerChunks} shafts={w.ShaftCount} fog_meadow={fogMeadow:0.0000} fog_koru={fogKoru:0.0000}");
        // Yer imleri: iki biyomun da yer imi var.
        Check(w.Biomes.World.Bookmarks.Any(b => w.Map.BiomeAt(b.Eye[0], b.Eye[1]) == "sis_korusu"), "no Koru bookmark");
        // Yukleme suresi (TEKNIK 10: <= 15 sn; baska oturumlar ayni makinede kosarken bilgi amacli).
        _info.Add("note=build_ms ayni makinede ~15 Godot oturumu varken olculdu");
    }
}
