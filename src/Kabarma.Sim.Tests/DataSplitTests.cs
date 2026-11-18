using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// tool.json'un TEKNIK §13 duzeninde bolunmesi (heads, weapons, recipes, placeables, foods, prices, tool) bellekteki
/// Data.Tool'u degistirmedi: her dosyadan birkac anahtar deger. Bir sayiyi bilerek degistirirsen buradaki karsiligini da guncelle.
/// </summary>
public class DataSplitTests
{
    static readonly HashSet<string> M1Recipes = new() { "tezgah", "mesale", "uyku_tulumu", "sandik", "yemlik", "folluk", "ahsap_kapi", "civi", "civi_tabancasi", "el_arabasi", "copper_ingot", "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi", "kargo_kasasi", "tas_firin", "ocakbasi", "paketleme" };
    static bool IsM1Recipe(string id) => M1Recipes.Contains(id);

    [Fact]
    public void Tool_values_survive_the_file_split()
    {
        var t = TestData.Load().Tool;
        // heads.json
        Assert.Equal(new[] { "kazma", "bicme", "toprak", "sulama", "cekim" }, t.Heads);
        Assert.Equal(1.6f, t.MowRadius);
        Assert.Equal(5f, t.MowRange);
        Assert.Equal(2.5f, t.MineRate);
        Assert.Equal(9f, t.MineRange);
        Assert.Equal(7f, t.TillRange);
        Assert.Equal(9f, t.WaterRange);
        Assert.Equal(10f, t.CarryRange);
        Assert.Equal(8f, t.BuildRange);
        Assert.Equal(2, t.WoodPerChop);
        // weapons.json
        Assert.Equal("tirpan_isini", t.Weapon.Id);
        Assert.Equal(14f, t.Weapon.Damage);
        Assert.Equal(0.42f, t.Weapon.FireInterval);
        Assert.Equal("Çivi Tabancası", t.Nailgun.Name);
        Assert.Equal("civi", t.Nailgun.Ammo);
        Assert.Equal(1, t.Nailgun.AmmoPerShot);
        // recipes.json
        // (ekonomi M1: tarifler, bedeller, yemekler ve fiyatlar ICERIK §15, §18, §20'ye uyduruldu; ayrintisi CraftingTests, EconomyTests)
        // karar 23: el tarifleri (yerlestirilebilirler) basta, istasyon tarifleri sonra; Tezgah'ta yapilan istasyonlar en sonda
        Assert.Equal(new[] { "tezgah", "mesale", "uyku_tulumu", "sandik", "yemlik", "folluk", "ahsap_kapi", "civi", "civi_tabancasi", "el_arabasi", "copper_ingot", "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi", "kargo_kasasi", "tas_firin", "ocakbasi", "paketleme" }, t.Recipes.Select(r => r.Id).Where(IsM1Recipe)); // M2 akislari satir ekler (hayvan: suluk, kasinma_fircasi, ari_kovani, kulucka); M1 sirasi korunur
        Assert.Equal(20, t.RecipeFor("civi")!.Out);
        Assert.Equal(2, t.RecipeFor("sebze_corbasi")!.In["turnip"]);
        // placeables.json
        // ekonomi M1 yerlestirilebilirleri, ardindan ilkgun'unkiler (Ahsap Kapi, Yemlik, Folluk) arkaya eklenir
        Assert.Equal(new[] { "ocak", "tezgah", "tas_firin", "ocakbasi", "paketleme", "sandik", "uyku_tulumu", "mesale", "ahsap_kapi", "yemlik", "folluk" }, t.Placeables.Select(p => p.Id).Take(11)); // M2 satirlari arkaya eklenir
        Assert.True(t.Placeables[0].Once);
        Assert.Equal(15, t.CostOf(t.Placeables[2].Id)["stone"]);
        Assert.Equal(28, t.BuildCosts.Count);
        Assert.Equal(6, t.BuildCosts["kare_temel"]);
        // foods.json
        Assert.Equal(25f, t.Foods.Single(f => f.Id == "sebze_corbasi").MaxHp);
        Assert.Equal(1200f, t.Foods.Single(f => f.Id == "sebze_corbasi").Seconds);
        Assert.Equal(3, t.FoodSlots);
        // prices.json
        Assert.Equal(new[] { "turnip", "wheat", "egg", "copper_ore" }, t.Prices.Keys);
        Assert.Equal(15, t.Prices["turnip"]);
        Assert.Equal(1.5f, t.ProcessedMult);
        // tool.json
        Assert.Equal(100f, t.Player.MaxHp);
        Assert.Equal(6.5f, t.Player.Jump);
        Assert.Equal(20, t.BackpackSlots);
        Assert.Equal(50, t.StackResource);
        Assert.Equal(new[] { "turnip_seed", "wheat", "civi", "ocak" }, t.StartItems.Keys); // karar 23: Ocak kapsulden gelir, kemerden kurulur
        Assert.Equal(40, t.StartItems["civi"]);
    }
}
