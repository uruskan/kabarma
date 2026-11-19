namespace Kabarma.Game;

/// <summary>Director, Cekisme dunyasi (M5 cekisme; game/scripts/rivalry/RivalrySystem.cs). Kapaliyken (varsayilan) hicbir sey yapmaz.</summary>
public partial class Director
{
    RivalrySystem _riv;
    /// <summary>Cekisme sistemi (tembel kurulur; State.Rivalry.Enabled yoksa yok hukmunde).</summary>
    public RivalrySystem Riv => _riv ??= new RivalrySystem(this);

    /// <summary>Director.Tick'ten (Cekisme kapaliyken ucuz).</summary>
    /// <summary>Cekisme + ortak oyun probu (--test=coop): iki takim, gunduz dokunulmazlik, gece baskini. Host'ta.</summary>
    public string CoopRivalryProbe() => Riv.CoopProbe();

    void TickRivalry(float dt) { if (State.Rivalry.Enabled) Riv.Tick(dt); }
}
