namespace Kabarma.Sim;

/// <summary>Kart defteri (GDD 15): acik kartlar, Nisan, kart jetonlari. Karakter dosyasinda (Ustalik gibi).</summary>
public sealed partial class GameState
{
    CardBook? _cardBook;
    public CardBook CardBook => _cardBook ??= new CardBook(Data.Cards);

    [SaveSection("cards", 1, Order = SaveOrder.Progress, Scope = SaveScope.Character, Covers = new[] { nameof(CardBook) })]
    CardBookSave CardBookSection { get => CardBook.Export(); set => CardBook.Import(value); }
}
