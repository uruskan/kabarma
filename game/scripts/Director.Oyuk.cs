namespace Kabarma.Game;

/// <summary>
/// Director: Oyuklar (M3, oyuk akisi; ICERIK §4.3). Director._Ready bir satirla InitOyuk cagirir; Interact ve HUD istemi
/// birer satirla OyukSystem'e sorar; Capa.Sulama iceride suyu deliklere verir.
/// </summary>
public partial class Director
{
    public OyukSystem Oyuk { get; private set; }

    void InitOyuk()
    {
        Oyuk = new OyukSystem(this);
        AddChild(Oyuk);
        if (Main.Args.ContainsKey("oyuk")) AddChild(new OyukTest(this)); // --test=oyuk
    }

    /// <summary>E: Oyuk agzi, kapi, sandik, kandil.</summary>
    bool OyukInteract() => Oyuk != null && Oyuk.Interact();
}
