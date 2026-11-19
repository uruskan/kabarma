namespace Kabarma.Sim;

/// <summary>Asi Tezgahi sonucu (ICERIK 11: gen aktarma, 200 W).</summary>
public enum GraftResult { Ok, NoSeed, SameLot, FamilyMismatch, NoGain, BadGene }

public sealed partial class Farm
{
    /// <summary>
    /// Asi Tezgahi: vericinin bir genini aliciya aktarir. Alici ve verici Tohum Kaydi'nda birer tohumdur (sepet de ayni
    /// kayittan duser); ikisi de harcanir, yerine alicinin ekininden bir tohum cikar: geni min(verici, alici + maxStep),
    /// diger genleri alicinin. Mutasyon ve Alaca yok (alici Alaca'siysa korunur). Ayni aile sarti graft.sameFamily.
    /// Tezgahin 200 W'i ve graft.seconds suresi istasyon katmaninda uygulanir.
    /// </summary>
    public GraftResult Graft(Inventory inv, SeedLot receiver, SeedLot donor, int gene, out SeedLot? result)
    {
        result = null;
        var rules = G.Graft;
        if (gene < 0 || gene >= Genes.Count) return GraftResult.BadGene;
        if (ReferenceEquals(receiver, donor)) return GraftResult.SameLot;
        var rd = _data.FindCrop(receiver.Crop); var dd = _data.FindCrop(donor.Crop);
        if (rd == null || dd == null) return GraftResult.NoSeed;
        if (rules.SameFamily && rd.Family != dd.Family) return GraftResult.FamilyMismatch;
        if (receiver.Count < 1 || donor.Count < 1) return GraftResult.NoSeed;
        if (donor.Genes[gene] <= receiver.Genes[gene]) return GraftResult.NoGain;
        if (inv.Count(rd.SeedId) < 1 || inv.Count(dd.SeedId) < (rd.Id == dd.Id ? 2 : 1)) return GraftResult.NoSeed;

        var genes = receiver.Genes.Clone();
        genes[gene] = Genetics.Clamp(G, Math.Min(donor.Genes[gene], receiver.Genes[gene] + Math.Max(1, rules.MaxStep)));
        string? alaca = receiver.Alaca;
        inv.Remove(rd.SeedId, 1);
        inv.Remove(dd.SeedId, 1);
        ConsumeLot(receiver); ConsumeLot(donor);
        Seeds.Add(rd.Id, genes, alaca);
        inv.Add(rd.SeedId, 1);
        Log.Note(G, "graft", rd.Id, genes, alaca, Time);
        Log.Offer(G, rd.Id, genes, Time);
        result = new SeedLot { Crop = rd.Id, Genes = genes, Alaca = alaca, Count = 1 };
        return GraftResult.Ok;
    }

    void ConsumeLot(SeedLot lot)
    {
        lot.Count--;
        if (lot.Count <= 0) Seeds.Lots.Remove(lot);
    }
}