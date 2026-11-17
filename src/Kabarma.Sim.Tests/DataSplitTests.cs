using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// tool.json'un TEKNIK §13 duzeninde bolunmesi (heads, weapons, recipes, placeables, foods, prices, tool) bellekteki
/// Data.Tool'u degistirmedi: her dosyadan birkac anahtar deger. Bir sayiyi bilerek degistirirsen buradaki karsiligini da guncelle.
/// </summary>
public class DataSplitTests
{
    [Fact]
    public void Tool_values_survive_the_file_split()
    {
        var t = TestData.Load().Tool;
        // heads.json
        Assert.Equal(new[] { "kazma", "bicme", "toprak", "sulama", "cekim", "insa" }, t.Heads);
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
        Assert.Equal(new[] { "civi", "civi_tabancasi", "el_arabasi", "copper_ingot", "kozde_salgam", "haslanmis_yumurta", "sebze_corbasi", "kargo_kasasi" }, t.Recipes.Select(r => r.Id));
        Assert.Equal(20, t.Recipes[0].Out);
        Assert.Equal(2, t.Recipes[6].In["turnip"]);
        // placeables.json
        Assert.Equal(new[] { "ocak", "tezgah", "tas_firin", "ocakbasi", "paketleme", "sandik", "uyku_tulumu", "mesale" }, t.Placeables.Select(p => p.Id));
        Assert.True(t.Placeables[0].Once);
        Assert.Equal(15, t.Placeables[2].Cost["stone"]);
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
        Assert.Equal(new[] { "turnip_seed", "wheat", "civi" }, t.StartItems.Keys);
        Assert.Equal(40, t.StartItems["civi"]);
    }
}
