namespace Kabarma.Sim;

/// <summary>
/// Maya Kazani'nin deposu (GDD 6, 14.4; ICERIK §2.1 #26): Kabarma safaginda gecenin Maya'si (borc dusulmus, Night.Maya) buraya
/// tam sayi olarak girer; oyuncu Ocak'in yaninda "maya" esyasi olarak alir (Maya Dokumhanesi, Maya Sirıngasi, Maya Reaktoru
/// bedelleri). Tatbikat Maya vermez (Night.Maya 0). Duz durum (TEKNIK 9): kayit bolumu "maya_vault".
/// </summary>
public sealed class MayaVault
{
    public const string Item = "maya";

    /// <summary>Depoda bekleyen Maya.</summary>
    public int Stock { get; set; }
    /// <summary>Gecmiste depoya giren toplam (basarim ve test).</summary>
    public int TotalDeposited { get; set; }

    /// <summary>Safakta: gecenin Maya'si (kesirli) tam sayiya yuvarlanip depoya girer. Donen: eklenen.</summary>
    public int Deposit(float maya)
    {
        int n = (int)MathF.Floor(Math.Max(0f, maya));
        Stock += n;
        TotalDeposited += n;
        return n;
    }

    /// <summary>Depodaki Maya'yi <paramref name="to"/>'ya (canta + Sandiklar) alir; sigdigi kadar. Donen: alinan.</summary>
    public int Withdraw(IItemStore to, int max = int.MaxValue)
    {
        int want = Math.Min(Stock, max);
        if (want <= 0) return 0;
        int put = to.Add(Item, want);
        Stock -= put;
        return put;
    }
}

public sealed partial class GameState
{
    /// <summary>Maya Kazani'nin deposu: safakta dolar, Ocak'in yaninda bosaltilir (Director.TickZincir).</summary>
    public MayaVault MayaStock { get; private set; } = new();

    [SaveSection("maya_vault", 1, Order = SaveOrder.State, Covers = new[] { nameof(MayaStock) })]
    MayaVault MayaVaultSection
    {
        get => MayaStock;
        set { MayaStock = value ?? new MayaVault(); }
    }
}
