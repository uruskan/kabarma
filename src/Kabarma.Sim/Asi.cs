namespace Kabarma.Sim;

/// <summary>
/// Asi kartlari (GDD 15, 15.1; ICERIK 8.2): iki ebeveyn de tavan rutbesindeyken ve asi acikken bir sonraki teklifin
/// ilk karti olur. Tek rutbelidir; alininca iki ebeveynin yuvasi tek yuvaya iner (SlotsUsed), ebeveyn etkileri surer.
/// </summary>
public sealed partial class CardHand
{
    /// <summary>Su an sunulmaya hazir asilar (veri sirasiyla).</summary>
    public IEnumerable<CardDef> AsiReady() =>
        _c.Cards.Where(a => a.IsAsi && IsOpen(a) && Rank(a.Id) == 0 && a.Parents.Count == 2 &&
                            a.Parents.All(p => _c.Find(p) is { } pc && Rank(p) >= Cap(pc)));

    /// <summary>Eldeki asilar.</summary>
    public IEnumerable<CardDef> AsiHeld => _c.Cards.Where(a => a.IsAsi && Rank(a.Id) > 0);

    /// <summary>Bir kart, eldeki bir asinin ebeveyni mi (yuvasi asiyla birlesti).</summary>
    public bool MergedIntoAsi(string id) => AsiHeld.Any(a => a.Parents.Contains(id));
}
