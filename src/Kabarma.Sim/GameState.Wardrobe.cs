namespace Kabarma.Sim;

/// <summary>GameState'in dolap parcasi (M4 dolap): uye tipleri ve kozmetikler karakter durumudur (dunyalar arasinda tasinir).</summary>
public sealed partial class GameState
{
    public Wardrobe Wardrobe { get; private set; } = null!;

    void InitWardrobe() => Wardrobe = new Wardrobe(Data);

    [SaveSection("wardrobe", 1, Order = SaveOrder.Progress, Scope = SaveScope.Character, Covers = new[] { nameof(Wardrobe) })]
    WardrobeSave WardrobeSection { get => Wardrobe.Export(); set => Wardrobe.Import(value); }
}
