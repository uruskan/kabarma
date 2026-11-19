namespace Kabarma.Sim;

public sealed partial class Farm
{
    /// <summary>
    /// Tek bir olgun kareyi kabartir (Kabartma Odasi, GDD 14.10): Kabarma disinda, gunduz. Kabarik urun Safak'i beklemeden
    /// sonme saatine girer (SinceDawn = 0). Donen: kabardi mi.
    /// </summary>
    public bool SwellOne(Plot p, int ks, float bonus)
    {
        if (p.Wild || !p.Planted || p.Stage != CropStage.Mature || p.Swollen != null) return false;
        p.Swollen = SizeFor(p, ks, bonus);
        p.SinceDawn = 0f;
        Swelled?.Invoke(p);
        return true;
    }
}