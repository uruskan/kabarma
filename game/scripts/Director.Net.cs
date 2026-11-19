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
        // Steam daveti oyunu kapaliyken kabul edilirse Steam oyunu "+connect_lobby <no>" ile baslatir.
        var cmd = OS.GetCmdlineArgs();
        int i = System.Array.IndexOf(cmd, "+connect_lobby");
        if (i >= 0 && i + 1 < cmd.Length && !Main.Args.ContainsKey("coop")) { Main.Args["coop"] = "client"; Main.Args["transport"] = "steam"; Main.Args["connect_lobby"] = cmd[i + 1]; }
        Net = NetSync.TryCreate(this);
        if (Net != null) Riv.BindNet(); // M5 cekisme: "rivalry" bolumu ve istekleri
        if (Net != null) AddChild(new PlayerList(this) { Name = "PlayerList" });
        AddChild(new NetHotkeys(this) { Name = "NetHotkeys" });
    }

    /// <summary>Ortak oyunda zaman yavaslamaz: vurus duraklamasi ve kart secimi yalniz tek oyuncuda (GDD 15; NetCards).</summary>
    void NetTimeScale()
    {
        if (Net != null) Engine.TimeScale = SpeedScale;
    }
}
