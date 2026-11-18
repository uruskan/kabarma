using Godot;
using Steamworks;

namespace Kabarma.Game;

/// <summary>
/// --test=steam (ortak, yalniz -Only steam): tek surecte Steam baslatma, arkadaslara acik lobi kurma, lobi verisini
/// okuma, P2P dinleme soketi, lobiden cikma. Steam istemcisi acik ve oturum acik olmali; degilse "fail init".
/// Iki makineli oynama sinamasi sahibin onayini bekliyor (URETIM 6 M2 kabulu).
/// </summary>
public partial class SteamTest : Node
{
    float _t;
    bool _done;
    CallResult<LobbyCreated_t> _cr;
    HSteamListenSocket _listen = HSteamListenSocket.Invalid;

    public override void _Ready()
    {
        var data = Kabarma.Sim.GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        if (!SteamBoot.Ensure(data.Net.Steam.AppId)) { Finish(false, "init " + SteamBoot.Error); return; }
        _listen = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
        _cr = CallResult<LobbyCreated_t>.Create((r, fail) =>
        {
            if (fail || r.m_eResult != EResult.k_EResultOK) { Finish(false, $"lobby {r.m_eResult}"); return; }
            var lobby = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, "kabarma", data.Net.Protocol.ToString());
            string back = SteamMatchmaking.GetLobbyData(lobby, "kabarma");
            int members = SteamMatchmaking.GetNumLobbyMembers(lobby);
            bool ok = back == data.Net.Protocol.ToString() && members == 1 && _listen != HSteamListenSocket.Invalid;
            SteamMatchmaking.LeaveLobby(lobby);
            Finish(ok, $"lobby={lobby.m_SteamID % 100000} data={back} members={members} listen={_listen != HSteamListenSocket.Invalid} app={SteamUtils.GetAppID()}");
        });
        _cr.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, data.Net.MaxPlayers));
    }

    public override void _Process(double delta)
    {
        if (!_done) SteamBoot.Pump();
        _t += (float)delta;
        if (!_done && _t > 20f) Finish(false, "lobby timeout");
        // Kapanis geri cagri disinda: Steam kendi dagitimi icindeyken kapatilirsa coker.
        if (_done && !_closed)
        {
            _closed = true;
            if (_listen != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(_listen);
            if (SteamBoot.Ready) SteamAPI.Shutdown();
            GetTree().Quit(_ok ? 0 : 1);
        }
    }

    bool _closed, _ok;

    void Finish(bool ok, string detail)
    {
        if (_done) return;
        _done = true;
        _ok = ok;
        GD.Print($"STEAM RESULT {(ok ? "ok" : "fail")} {detail}");
        if (!SteamBoot.Ready) { _closed = true; GetTree().Quit(ok ? 0 : 1); }
    }
}
