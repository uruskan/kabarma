namespace Kabarma.Game;

/// <summary>Director: Azmanlar (ICERIK §4.2). kabarma3 Doruk asamasinda Azman.OnPeak(KS, biyom, gelis noktasi) cagirir.</summary>
public partial class Director
{
    public AzmanSystem Azman { get; private set; }

    void InitAzman()
    {
        Azman = new AzmanSystem(this) { Name = "Azmanlar" };
        AddChild(Azman);
    }
}
