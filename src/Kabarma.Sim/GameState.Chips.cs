namespace Kabarma.Sim;

/// <summary>GameState parcasi: Cip Deposu ve baslik yuvalari (ICERIK 6.3; GDD 4). Kayit: "chips" bolumu.</summary>
public sealed partial class GameState
{
    ChipBook? _chips;
    /// <summary>Ilk eriside kurulur; yuva sayisi Ocak basamagindan (Capa Mk).</summary>
    public ChipBook Chips => _chips ??= new ChipBook(Data.Chips, () => Research.OcakTier);
    /// <summary>Cip dusme akisi (kendi tohumundan: oyunun Rng'ini kaydirmaz).</summary>
    public Rng ChipRng => _chipRng ??= new Rng(Seed * 29 + 11);
    Rng? _chipRng;

    /// <summary>Kaynaktan cip dusurur ("ata:koca_pofuduk", "kabarma_reward", "wheel:common"...).</summary>
    public ChipInstance? DropChip(string source, int ks = 0)
    {
        return Chips.Drop(source, ChipRng, ks);
    }

    [SaveSection("chips", 1, Order = SaveOrder.State, Covers = new[] { nameof(Chips), nameof(ChipRng) })]
    ChipSectionSave ChipSection
    {
        get => new() { Book = Chips.Save(), Rng = ChipRng.State };
        set { Chips.Load(value.Book); ChipRng.State = value.Rng; }
    }
}

public sealed class ChipSectionSave
{
    public ChipsSave Book { get; set; } = new();
    public ulong Rng { get; set; }
}
