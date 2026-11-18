namespace Kabarma.Sim;

/// <summary>GameState parcasi: Atalarin ilerlemesi (GDD §18). Duz ve serilestirilebilir (kayit akisi bolumu baglar).</summary>
public sealed partial class GameState
{
    AtaProgress? _atalar;

    public AtaProgress Atalar
    {
        get => _atalar ??= new AtaProgress();
        set => _atalar = value;
    }
}
