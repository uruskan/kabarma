namespace Kabarma.Game;

/// <summary>Director: akis alani (TEKNIK 8.1, yolbulma akisi). Host'ta ve tek oyuncuda isler; istemcide bos kalir.</summary>
public partial class Director
{
    /// <summary>Akis alanli yol bulma: Flow.Steer(pos, "crop"), Flow.Sample(...).Blocker, Flow.FindPath (Holding).</summary>
    public FlowFieldSystem Flow { get; private set; }

    void InitFlow()
    {
        Flow = new FlowFieldSystem(this);
        AddChild(Flow);
    }
}
