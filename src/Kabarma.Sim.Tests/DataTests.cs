using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Veri yuklenir ve her kartin etkisi bilinir.</summary>
public class DataTests
{
    static GameData Data() => TestData.Load();

    [Fact]
    public void Data_loads_and_cards_all_have_known_effects()
    {
        var d = Data();
        Assert.Contains(d.Crops.Crops, c => c.Id == "turnip"); // tarim M2: T1-T2'nin 11 ekini (FarmM2Tests)
        Assert.Equal(72, d.Cards.Cards.Count); // ICERIK 8: 60 kart + 12 asi
        // Her kart uygulanabilmeli (bilinmeyen etki istisna atmaz); havuz dolunca teklif bitebilir (GDD 15.1).
        foreach (var c in d.Cards.Cards)
        {
            var night = new GameState(d).StartNight();
            Assert.NotNull(night.Grant(c.Id));
        }
    }
}
