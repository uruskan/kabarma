namespace Kabarma.Sim;

/// <summary>GameState parcasi: Azmanlar (ICERIK §4.2). Duz ve serilestirilebilir; kayit bolumu "azman" (TEKNIK 9.1).</summary>
public sealed partial class GameState
{
    AzmanProgress? _azman;

    public AzmanProgress Azman
    {
        get => _azman ??= new AzmanProgress();
        set => _azman = value;
    }

    /// <summary>
    /// Kayit: suren Azman dovusleri (can, durumlar, konum, saldiri sirasi) ve yenilgi sayilari. Yuklenince Godot
    /// katmani (AzmanSystem) Azman'i kayitli yerde yeniden kurar. Dunya bolumu oldugu icin ortak oyunda istemciye de
    /// gider (NetSync.Save.cs): istemcinin Azman'i bu durumdan kurulan kuklalardir.
    /// </summary>
    [SaveSection("azman", 1, Order = SaveOrder.Progress, Covers = new[] { nameof(Azman) })]
    AzmanProgress AzmanSection
    {
        get => Azman;
        set => _azman = value ?? new AzmanProgress();
    }
}
