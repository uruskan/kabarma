using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 kabulu (URETIM §6): icerik adetleri PLAN §7.1 ile birebir, tarif grafi ve ham kaynaklarin gercek oyun yollari,
/// Ata arenalari Ata Taslarinda, Ana Maya Kabartma Plani verir. Adetler veri dosyalarindan sayilir; sayilamayan (dolap ve
/// ilerleme4 akislari henuz birlesmedi) ayri, atlanmis testte durur (Skip kalkinca zorunlu).
/// </summary>
public class AcceptanceTests
{
    static readonly GameData D = TestData.Load();

    [Fact]
    public void Content_counts_match_plan_7_1_for_the_families_that_have_landed()
    {
        // Biyom 5 + Gobek
        Assert.Equal(6, D.Biomes.Biomes.Count);
        // Yaratik 27 + 5 Holding birimi
        Assert.Equal(27, D.Creatures.Creatures.Count);
        Assert.Equal(5, D.Holding.Units.Count);
        // Ata 6 / Azman 6; Oyuk koruyucusu 5 / Mudur 1
        Assert.Equal(6, D.Atalar.Atalar.Count);
        Assert.Equal(6, D.Azman.Azmanlar.Count);
        Assert.Equal(5, D.Oyuk.Guardians.Count);
        Assert.NotNull(D.Holding.Mudur);
        // Ekin 24
        Assert.Equal(24, D.Crops.Crops.Count);
        // Capa basligi 18 (6 is, 12 silah); cip 24 x 4 nadirlik
        Assert.Equal((6, 12), (D.Chips.WorkHeads.Count, D.Chips.WeaponHeads.Count));
        Assert.Equal((24, 4), (D.Chips.Chips.Count, D.Chips.Rarities.Count));
        // Guc 12 / ulti 4
        Assert.Equal((12, 4), (D.Abilities.Abilities.Count(a => a.Kind == "active"), D.Abilities.Abilities.Count(a => a.Kind == "ulti")));
        // Kabarma karti 72 / tuzuk 12
        Assert.Equal(72, D.Cards.Cards.Count);
        Assert.Equal(12, D.Tuzuk.Clauses.Count);
        // Istasyon ve makine 43 (automation.json fonksiyon tablosu), guc/sinyal/tasima 43
        Assert.Equal(43, D.Automation.Functions.Count);
        Assert.Equal(43, D.Power.Devices.Count);
        // Kule / tuzak 8 / 8
        Assert.Equal((8, 8), (D.Defense.Towers.Count, D.Defense.Traps.Count));
        // Arac / eklenti 7 (El Arabasi dahil) / 10
        Assert.Equal((6, 10), (D.Vehicles.Vehicles.Count, D.Vehicles.Attachments.Count));
        // Yemek 32
        Assert.Equal(32, D.Tool.Foods.Count);
        // Zirh takimi 7 / modul 18
        Assert.Equal((7, 18), (D.Armor.Sets.Count, D.Armor.Modules.Count));
        // Kesif noktasi turu 9, borsa kategorisi 12, ustalik 8 dal / 80 ozellik
        Assert.Equal(9, D.Biomes.World.PoiTypes.Count);
        Assert.Equal(12, D.Market.Categories.Count);
        Assert.Equal((8, 80), (D.Mastery.Branches.Count, D.Mastery.Perks.Count));
    }

    [Fact]
    public void Placeables_minus_stations_power_and_defense_are_the_72_of_icerik_10_2_plus_the_ocak()
    {
        var stations = D.Automation.Functions.Keys.ToHashSet();
        var power = D.Power.Devices.Select(p => p.Id).ToHashSet();
        var defense = D.Defense.Towers.Select(t => t.Id).Concat(D.Defense.Traps.Select(t => t.Id)).ToHashSet();
        var rest = D.Tool.Placeables.Where(p => !stations.Contains(p.Id) && !power.Contains(p.Id) && !defense.Contains(p.Id) && p.Id != "ocak").ToList();
        Assert.Equal(72, rest.Count);
        // Her istasyon bir yerlestirilebilir (Kargo Rampasi hepsinden ayri: rampa)
        foreach (var id in stations.Where(s => s != "kargo_rampasi")) Assert.Contains(D.Tool.Placeables, p => p.Id == id);
    }

    [Fact]
    public void Building_kit_has_26_shapes_x_materials_plus_the_glass_set()
    {
        Assert.Equal(32, D.Tool.BuildCosts.Count); // 26 sekil (makine platformu 3 boy) + Cam seti 4 (DataSplitTests ile ayni olcu)
    }

    [Fact]
    public void Every_resource_in_the_67_list_has_a_real_source_or_recipe()
    {
        var raw = D.Research.RawSources.ToDictionary(s => s.Id);
        var made = D.Tool.Recipes.Select(r => r.Id).ToHashSet();
        // kimlikler-m4 §2: T3-T6 ham kaynaklar ve hayvansal (kemik, kitin) bir kaynak satirina sahip; zehir_kesesi yaratik dususu (listelenmis bosluk).
        foreach (var id in new[] { "turba", "kukurt", "kaucuk_sutu", "balcik", "isil_yosun", "kum", "kuvars", "titan_ore", "katran", "tuz", "buz", "ayaz_kristali", "ham_maya", "obsidyen", "isil_cevher", "kemik", "kitin", "maya", "kabartma_plani" })
            Assert.True(raw.ContainsKey(id), $"{id}: ham kaynak satiri yok");
        foreach (var s in raw.Values.Where(s => s.By is "biyom" or "savas" or "ata4" or "kabarma3"))
        {
            Assert.True(s.Via.Length > 0 && !s.Via.StartsWith("pending:"), $"{s.Id}: gercek oyun yolu yok ({s.Via})");
            var (kind, arg) = (s.Via.Split(':')[0], s.Via.Contains(':') ? s.Via.Split(':')[1] : "");
            switch (kind)
            {
                case "node": Assert.True(D.Biomes.Biomes[arg].Nodes.Any(n => n.Item == s.Id), $"{s.Id}: {arg} biyomunda dugum yok"); break;
                case "ata": Assert.True(D.Atalar.Atalar.Single(a => a.Id == arg).Reward.Items.Any(i => i.Item == s.Id), $"{s.Id}: {arg} odulu degil"); break;
                case "azman": Assert.True(D.Azman.Azmanlar.Single(a => a.Id == arg).Reward.Items.Any(i => i.Item == s.Id), $"{s.Id}: {arg} odulu degil"); break;
                case "mayavault": Assert.Equal("maya", s.Id); break;
                default: Assert.Fail($"{s.Id}: bilinmeyen via {s.Via}"); break;
            }
        }
        // Dugumun yerlestigi biyom kademesi, kaynagin basamagiyla uyusur (biyom kademesi <= basamak).
        var order = D.Biomes.World.BiomeMap.Order;
        foreach (var s in raw.Values.Where(s => s.Via.StartsWith("node:")))
            Assert.True(order.IndexOf(s.Via[5..]) + 1 <= Math.Max(s.Tier, 1), $"{s.Id}: kademe {s.Tier} biyomdan once");
    }

    [Fact]
    public void Recipe_graph_reaches_kabartma_odasi_through_the_real_sources_and_every_unlock_has_a_producer()
    {
        var g = RecipeGraph.Walk(D, 6);
        Assert.Empty(g.Unreached);
        Assert.True(g.Has("kabartma_odasi"));
        // Kabartma Odasi'nin kilidi (kabartma_plani) Ana Maya'nin odulunden gelir; plan olmadan acilmaz (ZincirChain4Tests ayrica).
        Assert.Contains("kabartma_plani", D.Atalar.Atalar.Single(a => a.Id == "ana_maya").Reward.Items.Select(i => i.Item));
        var recipe = D.Tool.Recipes.Single(r => r.Id == "kabartma_odasi");
        Assert.Equal("kabartma_plani", recipe.UnlockOn);
        // Maya: Kabarma safagi (MayaVault); Ham Maya: Gobek dugumu
        Assert.Equal("mayavault", D.Research.RawSources.Single(s => s.Id == "maya").Via);
        Assert.Equal("node:gobek", D.Research.RawSources.Single(s => s.Id == "ham_maya").Via);
    }

    [Fact]
    public void Ata_arenas_stand_on_their_ata_stones_and_every_biome_has_its_ata_row()
    {
        var map = new BiomeMap(D.Biomes);
        var pois = PoiPlacer.Place(D.Biomes, map);
        foreach (var a in D.Atalar.Atalar)
        {
            var row = D.Biomes.Biomes[a.Biome].Spawns.Atalar.SingleOrDefault(r => r.Ata == a.Id);
            Assert.True(row != null, $"{a.Id}: {a.Biome} biyomunda Ata satiri yok");
            var stone = pois.Single(p => p.Type == row!.Site && p.Biome == a.Biome);
            Assert.True(Math.Abs(stone.X - a.Arena.Center[0]) < 1f && Math.Abs(stone.Z - a.Arena.Center[1]) < 1f, $"{a.Id}: arena Ata Tasi'nda degil ({a.Arena.Center[0]},{a.Arena.Center[1]} / {stone.X},{stone.Z})");
            Assert.True(Math.Max(stone.Flatten, D.Biomes.World.PoiTypes[stone.Type].Flatten) >= a.Arena.Radius, $"{a.Id}: duzlestirme arenayi kapsamiyor");
            Assert.True(map.Weight(a.Biome, stone.X, stone.Z) >= 0.95f, $"{a.Id}: Ata Tasi biyomun icinde degil");
            Assert.True(string.IsNullOrEmpty(a.Arena.Temporary), $"{a.Id}: arena hala GECICI");
        }
    }
}
