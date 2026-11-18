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
        Assert.Equal(10, d.Cards.Cards.Count);
        var night = new GameState(d).StartNight();
        // Her kart uygulanabilmeli: bilinmeyen etki istisna atar.
        foreach (var _ in d.Cards.Cards)
        {
            night.PendingLevels.Enqueue(1);
            night.TryOpenOffer();
            Assert.NotNull(night.Choose(0));
        }
    }
}
