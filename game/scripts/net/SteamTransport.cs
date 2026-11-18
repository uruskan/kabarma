using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Kabarma.Sim;
using Steamworks;

namespace Kabarma.Game;

/// <summary>
/// Steam taban katmani: Steamworks.NET'i tembel ve hataya dayanikli baslatir (Steam kapaliysa ya da steam_api64.dll
/// yoksa oyun tek oyuncu ve ENet ile surer). Surec duzeyinde tek: SteamAPI'nin kendisi de surec basina tektir; oyun
/// durumu tutmaz. Uygulama kimligi net.json steam.appId (480: Valve'in deneme uygulamasi).
/// </summary>
public static class SteamBoot
{
    static bool _tried, _ok;
    public static string Error { get; private set; } = "";

    public static bool Ensure(uint appId)
    {
        if (_tried) return _ok;
        _tried = true;
        try
        {
            // Godot oyun derlemesini kendi yukleme baglaminda acar; yerel steam_api64 derlemenin yanindan (ya da lib/'den) yuklenir.
            NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (name, asm, path) =>
            {
                if (!name.StartsWith("steam_api")) return IntPtr.Zero;
                string file = OperatingSystem.IsWindows() ? "steam_api64.dll" : "libsteam_api.so";
                foreach (var dir in new[] { System.IO.Path.GetDirectoryName(typeof(SteamBoot).Assembly.Location), System.IO.Path.GetDirectoryName(asm.Location),
                    ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin/Debug"), ProjectSettings.GlobalizePath("res://../lib/steamworks-2025.165.0/" + (OperatingSystem.IsWindows() ? "win-x64" : "linux-x64")), System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) })
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    var p = System.IO.Path.Combine(dir, file);
                    if (System.IO.File.Exists(p) && NativeLibrary.TryLoad(p, out var h)) return h;
                }
                return IntPtr.Zero;
            });
            // steam_appid.txt yerine: Steam istemcisi uygulamayi ortam degiskeninden tanir (gelistirme ve sinama).
            System.Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
            System.Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
            if (!Packsize.Test() || !DllCheck.Test()) { Error = "Steamworks.NET paket boyu ya da DLL surumu uyusmuyor"; return false; }
            _ok = SteamAPI.Init();
            if (!_ok) { Error = "Steam başlatılamadı (Steam açık ve oturum açık mı?)"; return false; }
            SteamNetworkingUtils.InitRelayNetworkAccess();
            GD.Print($"STEAM init ok app={SteamUtils.GetAppID()}");
            return true;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
        {
            Error = "steam_api64 yüklenemedi: " + e.GetType().Name;
            _ok = false;
            return false;
        }
    }

    public static bool Ready => _ok;
    public static void Pump() { if (_ok) SteamAPI.RunCallbacks(); }
    public static string PersonaName => _ok ? SteamFriends.GetPersonaName() : "";
}

/// <summary>
/// Steam Networking Sockets tasimasi (TEKNIK 5.1: arkadas daveti, lobi, relay; port acmak gerekmez).
/// Host: arkadaslara acik lobi kurar (lobi verisi "kabarma" = protokol), P2P dinler; Steam arayuzunden davet eder.
/// Istemci: lobiye girer (davet kabulu, --connect_lobby=ID ya da Steam'in +connect_lobby baslatma argumani), lobi
/// sahibine P2P baglanir. M0 adim 3'te ayni kutuphaneyle iki surec 20/20 mesaj (TEKNIK 1.3).
/// </summary>
public sealed class SteamTransport : ITransport
{
    readonly NetFile _cfg;
    HSteamListenSocket _listen = HSteamListenSocket.Invalid;
    readonly Dictionary<int, HSteamNetConnection> _peers = new();
    readonly Dictionary<uint, int> _ids = new();
    HSteamNetConnection _server = HSteamNetConnection.Invalid;
    readonly List<NetEvent> _pending = new();
    Callback<SteamNetConnectionStatusChangedCallback_t> _status;
    Callback<LobbyEnter_t> _enter;
    Callback<GameLobbyJoinRequested_t> _joinReq;
    CallResult<LobbyCreated_t> _created;
    int _next = 1;
    bool _connected;
    readonly IntPtr[] _msgs = new IntPtr[64];

    public CSteamID Lobby { get; private set; } = CSteamID.Nil;
    public string Name => "steam";
    public bool IsHost { get; }
    public bool Up => IsHost ? _listen != HSteamListenSocket.Invalid : _connected;
    public string Address => Lobby.IsValid() ? $"Steam lobi {Lobby.m_SteamID}" : "Steam";
    public string LastError { get; private set; } = "";

    SteamTransport(NetFile cfg, bool host) { _cfg = cfg; IsHost = host; }

    public static ITransport Create(NetSync net, bool host, out string error)
    {
        error = null;
        if (!SteamBoot.Ensure(net.Cfg.Steam.AppId)) { error = SteamBoot.Error; return null; }
        var t = new SteamTransport(net.Cfg, host);
        t._status = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(t.OnStatus);
        t._enter = Callback<LobbyEnter_t>.Create(t.OnLobbyEnter);
        t._joinReq = Callback<GameLobbyJoinRequested_t>.Create(r => SteamMatchmaking.JoinLobby(r.m_steamIDLobby));
        if (host) t.HostLobby(net.Cfg.MaxPlayers);
        else
        {
            ulong lobby = Main.Args.TryGetValue("connect_lobby", out var s) && ulong.TryParse(s, out var l) ? l : 0;
            if (lobby != 0) SteamMatchmaking.JoinLobby(new CSteamID(lobby));
            else GD.Print("STEAM istemci: davet bekleniyor (Steam arkadas listesinden 'Oyuna katil')");
        }
        return t;
    }

    void HostLobby(int max)
    {
        _listen = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
        var type = _cfg.Steam.LobbyType switch { "public" => ELobbyType.k_ELobbyTypePublic, "private" => ELobbyType.k_ELobbyTypePrivate, _ => ELobbyType.k_ELobbyTypeFriendsOnly };
        _created = CallResult<LobbyCreated_t>.Create((r, fail) =>
        {
            if (fail || r.m_eResult != EResult.k_EResultOK) { LastError = $"lobi kurulamadı ({r.m_eResult})"; GD.PrintErr("STEAM " + LastError); return; }
            Lobby = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Lobby, "kabarma", _cfg.Protocol.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "host", SteamUser.GetSteamID().m_SteamID.ToString());
            GD.Print($"STEAM lobby {Lobby.m_SteamID}");
        });
        _created.Set(SteamMatchmaking.CreateLobby(type, max));
    }

    /// <summary>Steam arayuzunde davet penceresi (host).</summary>
    public void Invite()
    {
        if (Lobby.IsValid()) SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
    }

    void OnLobbyEnter(LobbyEnter_t e)
    {
        if (IsHost) return;
        Lobby = new CSteamID(e.m_ulSteamIDLobby);
        string proto = SteamMatchmaking.GetLobbyData(Lobby, "kabarma");
        if (proto != _cfg.Protocol.ToString()) { LastError = $"lobi sürümü farklı ({proto})"; _pending.Add(new NetEvent(NetEventKind.Disconnected, 0, null, 0)); return; }
        var owner = SteamMatchmaking.GetLobbyOwner(Lobby);
        var id = new SteamNetworkingIdentity();
        id.SetSteamID(owner);
        _server = SteamNetworkingSockets.ConnectP2P(ref id, 0, 0, null);
        GD.Print($"STEAM lobiye girildi {Lobby.m_SteamID}, host'a baglaniliyor");
    }

    void OnStatus(SteamNetConnectionStatusChangedCallback_t s)
    {
        var st = s.m_info.m_eState;
        var c = s.m_hConn;
        if (IsHost)
        {
            if (st == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting && s.m_info.m_hListenSocket == _listen)
                SteamNetworkingSockets.AcceptConnection(c);
            else if (st == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected && !_ids.ContainsKey(c.m_HSteamNetConnection))
            {
                int id = _next++;
                _peers[id] = c; _ids[c.m_HSteamNetConnection] = id;
                _pending.Add(new NetEvent(NetEventKind.Connected, id, null, 0));
            }
            else if (st is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer or ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                if (_ids.Remove(c.m_HSteamNetConnection, out int id)) { _peers.Remove(id); _pending.Add(new NetEvent(NetEventKind.Disconnected, id, null, 0)); }
                SteamNetworkingSockets.CloseConnection(c, 0, "bye", false);
            }
            return;
        }
        if (c != _server) return;
        if (st == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected) { _connected = true; _pending.Add(new NetEvent(NetEventKind.Connected, 0, null, 0)); }
        else if (st is ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer or ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
        {
            LastError = s.m_info.m_szEndDebug;
            _connected = false;
            SteamNetworkingSockets.CloseConnection(c, 0, "bye", false);
            _pending.Add(new NetEvent(NetEventKind.Disconnected, 0, null, 0));
        }
    }

    public void Send(int peer, byte[] data, NetChannel channel)
    {
        var c = IsHost ? (_peers.TryGetValue(peer, out var p) ? p : HSteamNetConnection.Invalid) : _server;
        if (c == HSteamNetConnection.Invalid) return;
        int flags = channel == NetChannel.Reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_UnreliableNoDelay;
        var h = GCHandle.Alloc(data, GCHandleType.Pinned);
        try { SteamNetworkingSockets.SendMessageToConnection(c, h.AddrOfPinnedObject(), (uint)data.Length, flags, out _); }
        finally { h.Free(); }
    }

    public void Poll(List<NetEvent> into)
    {
        SteamBoot.Pump();
        into.AddRange(_pending);
        _pending.Clear();
        if (IsHost) foreach (var (id, c) in _peers) Receive(c, id, into);
        else if (_connected) Receive(_server, 0, into);
    }

    void Receive(HSteamNetConnection c, int id, List<NetEvent> into)
    {
        int n = SteamNetworkingSockets.ReceiveMessagesOnConnection(c, _msgs, _msgs.Length);
        for (int i = 0; i < n; i++)
        {
            var m = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_msgs[i]);
            var buf = new byte[m.m_cbSize];
            Marshal.Copy(m.m_pData, buf, 0, m.m_cbSize);
            SteamNetworkingMessage_t.Release(_msgs[i]);
            into.Add(new NetEvent(NetEventKind.Data, id, buf, 0));
        }
    }

    public void Kick(int peer)
    {
        if (_peers.TryGetValue(peer, out var c)) SteamNetworkingSockets.CloseConnection(c, 0, "kick", true);
    }

    public void Dispose()
    {
        foreach (var c in _peers.Values) SteamNetworkingSockets.CloseConnection(c, 0, "kapanış", true);
        if (_server != HSteamNetConnection.Invalid) SteamNetworkingSockets.CloseConnection(_server, 0, "kapanış", true);
        if (_listen != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(_listen);
        if (Lobby.IsValid()) SteamMatchmaking.LeaveLobby(Lobby);
        _status?.Dispose(); _enter?.Dispose(); _joinReq?.Dispose(); _created?.Dispose();
    }
}
