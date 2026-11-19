using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya sinamasinin M4 bolumu (biyom akisi; TEKNIK 7 "M4 ara adimi"): 1800 m dunya, dort yeni biyom
/// (Isil Sazlik, Kizil Kanyon, Ayaz Yaylasi, Gobek): bitki ortusu, T3-T6 kaynak dugumleri, krater cukuru ve sirti,
/// kesif noktalari (Zengin Yatak dahil), her biyomun kendi isik ve sisi.
/// </summary>
public partial class WorldTest
{
    static readonly string[] NewBiomes = { "isil_sazlik", "kizil_kanyon", "ayaz_yaylasi", "gobek" };

    void RunM4(World w)
    {
        Check(Mathf.IsEqualApprox(w.Size, 1800f) && w.Biomes.World.Terrain.CoreSize == 640, $"size={w.Size} core={w.Biomes.World.Terrain.CoreSize} (M4 ara adimi 1800 m, cekirdek 640 m)");
        Check(w.Map.Order.Count == 6 && NewBiomes.All(id => w.Biomes.Biomes.ContainsKey(id)), "altı biyom tanimli degil");

        // Bitki ortusu ve kaynak dugumleri: her yeni biyomda yerlesti, kendi biyomunun icinde.
        var wanted = new System.Collections.Generic.Dictionary<string, string[]>
        {
            ["isil_sazlik"] = new[] { "turba", "kukurt", "kaucuk_sutu", "balcik", "isil_yosun", "kemik" },
            ["kizil_kanyon"] = new[] { "kum", "kuvars", "titan_ore", "katran", "tuz", "kemik" },
            ["ayaz_yaylasi"] = new[] { "buz", "ayaz_kristali", "kemik" },
            ["gobek"] = new[] { "ham_maya", "obsidyen", "isil_cevher" },
        };
        foreach (var id in NewBiomes)
        {
            var b = w.Biomes.Biomes[id];
            foreach (var def in b.Decor)
            {
                string key = w.DecorCounts.ContainsKey(id + "_" + def.Id) ? id + "_" + def.Id : def.Id;
                int got = w.DecorCounts.TryGetValue(key, out var c) ? c : 0;
                Check(got >= def.Count * 0.5f, $"{id} decor {def.Id} placed {got}/{def.Count}");
            }
            foreach (var item in wanted[id])
            {
                var rocks = w.Rocks.Where(r => r.Item == item && w.Map.Weight(id, r.GlobalPosition.X, r.GlobalPosition.Z) >= 0.5f).ToList();
                int need = b.Nodes.Where(n => n.Item == item).Sum(n => n.ScatterCount);
                Check(rocks.Count >= System.Math.Max(2, need / 2), $"{id} node {item} {rocks.Count}/{need}");
            }
        }
        _info.Add("m4_nodes=" + string.Join(",", NewBiomes.SelectMany(id => wanted[id]).Distinct().Select(i => $"{i}:{w.Rocks.Count(r => r.Item == i)}")));

        // Krater: cukur (Gobek) ve etrafinda sirt.
        var cr = w.Biomes.World.BiomeMap.Crater;
        float floor = w.HeightAt(cr.Center[0], cr.Center[1]);
        float rim = float.MinValue;
        for (float a = 0; a < Mathf.Tau; a += 0.2f)
            rim = Mathf.Max(rim, w.HeightAt(cr.Center[0] + Mathf.Cos(a) * cr.Radius * 1.17f, cr.Center[1] + Mathf.Sin(a) * cr.Radius * 1.17f));
        Check(rim - floor > 12f, $"krater cukuru yok: taban {floor:0.0} sirt {rim:0.0}");
        _info.Add($"crater_floor={floor:0.0} rim_max={rim:0.0}");

        // Kesif noktalari: M2 cekirdeginde eskisi gibi (16), yeni biyomlarda ICERIK 17 + Zengin Yatak.
        var core = w.Pois.Where(q => q.Biome is "altin_cayir" or "sis_korusu" && q.Type != "oyuk").ToList();
        Check(core.Count == 16, $"core pois {core.Count} != 16");
        Check(w.Pois.Count(q => q.Type == "zengin_yatak") == 7, $"zengin yatak {w.Pois.Count(q => q.Type == "zengin_yatak")} != 7");
        foreach (var id in NewBiomes)
        {
            Check(w.Pois.Any(q => q.Biome == id && q.Type == "seyir_kulesi"), $"{id} no tower");
            Check(w.Pois.Any(q => q.Biome == id && q.Type == "ata_tasi"), $"{id} no ata tasi");
            Check(w.Pois.Any(q => q.Biome == id && q.Type == "zengin_yatak"), $"{id} no zengin yatak");
        }
        foreach (var poi in w.Pois.Where(q => q.Type == "zengin_yatak"))
        {
            Check(w.PoiNodes.ContainsKey(poi.Id), $"zengin yatak {poi.Id} has no (placeholder) model");
            Check(w.SlopeAt(poi.X, poi.Z) < 8f, $"zengin yatak {poi.Id} not flattened");
        }
        Check(w.Pois.Where(q => q.Type == "enkaz" && NewBiomes.Contains(q.Biome)).All(q => w.PoiRocks.TryGetValue(q.Id, out var r) && r.Item == "scrap"), "new biome enkaz is not scrap");

        // Her biyomun kendi isigi ve sisi: kamera biyomun Seyir Kulesi'nde, ogle: sis yogunlugu Cayir'inkinden farkli.
        w.ForceTime("ogle");
        w.ForcedBiomeMix = 0f; w.SnapBiomeMix(Vector3.Zero);
        float fogMeadow = w.Env.Environment.FogDensity;
        var lightEnergyMeadow = w.Sun.LightEnergy;
        foreach (var id in NewBiomes)
        {
            var tower = w.Pois.First(q => q.Type == "seyir_kulesi" && q.Biome == id);
            w.ForcedBiomeMix = null;
            w.SnapBiomeMix(w.PoiPos(tower));
            float fog = w.Env.Environment.FogDensity;
            Check(Mathf.Abs(fog - fogMeadow) > fogMeadow * 0.15f || Mathf.Abs(w.Sun.LightEnergy - lightEnergyMeadow) > 0.2f, $"{id} light does not differ from meadow (fog {fog:0.0000} vs {fogMeadow:0.0000})");
            _info.Add($"light_{id}: fog={fog:0.0000} sun={w.Sun.LightEnergy:0.00}");
        }
        w.ForcedBiomeMix = null; w.SnapBiomeMix(Vector3.Zero); w.ForceTime(null);

        // Yer imleri: dort yeni biyomun da yer imi var (goruntu seti).
        foreach (var id in NewBiomes) Check(w.Biomes.World.Bookmarks.Any(b => w.Map.BiomeAt(b.Eye[0], b.Eye[1]) == id), $"no {id} bookmark");
    }
}
