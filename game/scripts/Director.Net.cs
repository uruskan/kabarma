using Godot;

namespace Kabarma.Game;

/// <summary>
/// Ortak oyun baglantisi (ortak akisi, game/scripts/net/). <see cref="Net"/> tek oyuncuda null: butun kancalar
/// <c>_d.Net?.X() == true</c> bicimindedir ve tek oyuncuda hicbir sey yapmaz.
/// </summary>
public partial class Director
{
    /// <summary>Ortak oyun katmani; tek oyuncuda null.</summary>
    public NetSync Net { get; private set; }
    /// <summary>Bu surec bir ortak oyunun istemcisi mi (dunyaya yerel dokunmaz, host'a istek yollar).</summary>
    public bool NetClient => Net != null && Net.IsClient;

    void InitNet()
    {
        Net = NetSync.TryCreate(this);
        if (Net != null) AddChild(new PlayerList(this) { Name = "PlayerList" });
    }

    /// <summary>Ortak oyunda zaman yavaslamaz: vurus duraklamasi ve kart secimi yalniz tek oyuncuda (GDD 15; NetCards).</summary>
    void NetTimeScale()
    {
        if (Net != null) Engine.TimeScale = SpeedScale;
    }
}
