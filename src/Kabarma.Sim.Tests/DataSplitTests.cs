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
        Assert.Equal(new[] { "civi", "salgam_corbasi", "salgam_kasasi" }, t.Recipes.Select(r => r.Id));
        Assert.Equal(10, t.Recipes[0].Out);
        Assert.Equal(5, t.Recipes[2].In["turnip"]);
        // placeables.json
        Assert.Equal(new[] { "ocak", "tezgah", "tas_firin", "paketleme" }, t.Placeables.Select(p => p.Id));
        Assert.True(t.Placeables[0].Once);
        Assert.Equal(10, t.Placeables[2].Cost["stone"]);
        Assert.Equal(9, t.BuildCosts.Count);
        Assert.Equal(6, t.BuildCosts["foundation"]);
        // foods.json
        Assert.Equal(25f, t.Foods.Single().MaxHp);
        Assert.Equal(300f, t.Foods.Single().Seconds);
        // prices.json: sira onemli (satis menusu ve bot bu siradan secer)
        Assert.Equal(new[] { "turnip", "salgam_kasasi", "egg" }, t.Prices.Keys);
        Assert.Equal(90, t.Prices["salgam_kasasi"]);
        // tool.json
        Assert.Equal(100f, t.Player.MaxHp);
        Assert.Equal(6.5f, t.Player.Jump);
        Assert.Equal(20, t.BackpackSlots);
        Assert.Equal(50, t.StackResource);
        Assert.Equal(new[] { "turnip_seed", "wheat", "civi" }, t.StartItems.Keys);
        Assert.Equal(40, t.StartItems["civi"]);
    }
}
