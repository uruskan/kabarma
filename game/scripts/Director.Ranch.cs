namespace Kabarma.Game;

/// <summary>Director: hayvancilik (hayvan akisi, M2; GDD 9.1-9.4). RanchSystem kurulumu.</summary>
public partial class Director
{
    public RanchSystem Ranch { get; private set; }

    void SetupRanch()
    {
        Ranch = new RanchSystem(this);
        AddChild(Ranch);
    }
}
