using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Istasyonlar ve yemek: civi, corba, kasa; yemek guclendirmesi (GDD 11, 3.3).</summary>
public class CraftingTests
{
    static GameData Data() => TestData.Load();

    [Fact]
    public void Stations_craft_nails_cook_soup_and_pack_crates()
    {
        var d = Data();
        var s = new GameState(d);
        s.Backpack.Add("stone", 4);
        s.Backpack.Add("turnip", 7);
        var tez = s.Stations["tezgah"];
        Assert.True(tez.Enqueue(tez.Recipes.First(), s.Backpack));
        Assert.True(tez.Enqueue(tez.Recipes.First(), s.Backpack));
        Assert.False(tez.Enqueue(tez.Recipes.First(), s.Backpack)); // tas bitti
        for (int i = 0; i < 50; i++) tez.Tick(0.1f, s.Backpack);
        Assert.Equal(40 + 20, s.Backpack.Count("civi"));
        var firin = s.Stations["tas_firin"];
        firin.Enqueue(firin.Recipes.First(), s.Backpack);
        var pak = s.Stations["paketleme"];
        pak.Enqueue(pak.Recipes.First(), s.Backpack);
        for (int i = 0; i < 50; i++) { firin.Tick(0.1f, s.Backpack); pak.Tick(0.1f, s.Backpack); }
        Assert.Equal(1, s.Backpack.Count("salgam_corbasi"));
        Assert.Equal(1, s.Backpack.Count("salgam_kasasi"));
        Assert.Equal(0, s.Backpack.Count("turnip"));
        Assert.Equal(90, s.SellItem("salgam_kasasi", 1));
        Assert.True(s.Food.Eat(d.Tool.Foods.First()));
        Assert.Equal(25f, s.Food.BonusMaxHp);
        s.Food.Tick(301f);
        Assert.Equal(0f, s.Food.BonusMaxHp);
    }
}
