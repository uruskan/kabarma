using System;
using System.Collections.Generic;
using Godot;
using Kabarma.Sim;
using Steamworks;

namespace Kabarma.Game;

/// <summary>Sunucu listesi satiri (Steam tarayicisindan ya da kayitli).</summary>
public sealed class ServerEntry
{
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 27015;
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Ping { get; set; }
    public bool Password { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>
/// Adanmis sunucuyu Steam sunucu tarayicisinda gosterir (Steamworks.NET GameServer; M5 sunucu akisi). Steam ya da
/// steam_api64 yoksa atlanir (oyun ENet dogrudan baglantisiyla surer). Hata dayanikli: her cagri try/catch'tedir.
/// </summary>
public static class SteamListing
{
    static bool _on;
    static float _t;

    public static bool Active => _on;

    public static void Start(ServerFile cfg, NetSync net, ServerHost host)
    {
        if (!cfg.Steam.Listed) return;
        try
        {
            SteamBoot.Ensure((uint)net.D.Data.Net.Steam.AppId); // yalniz yerel kutuphane cozucusu icin; istemci girisi sunucuda sart degil
            if (!GameServer.Init(0, (ushort)cfg.Port, (ushort)(cfg.Port + 1), EServerMode.eServerModeNoAuthentication, "1.0.0.0"))
            {
                ServerRunner.Log("steam sunucu listesi: GameServer.Init basarisiz (atlandi)");
                return;
            }
            SteamGameServer.SetModDir(cfg.Steam.GameDir);
            SteamGameServer.SetProduct(cfg.Steam.Product);
            SteamGameServer.SetGameDescription(cfg.Steam.Description);
            SteamGameServer.SetDedicatedServer(true);
            SteamGameServer.SetServerName(cfg.Name);
            SteamGameServer.SetMaxPlayerCount(cfg.MaxPlayers);
            SteamGameServer.SetPasswordProtected(host.Password.Length > 0);
            SteamGameServer.SetMapName("Bereket Adası");
            SteamGameServer.LogOnAnonymous();
            SteamGameServer.SetAdvertiseServerActive(true);
            _on = true;
            ServerRunner.Log("steam sunucu listesi: acik");
        }
        catch (Exception e)
        {
            ServerRunner.Log($"steam sunucu listesi atlandi ({e.GetType().Name}: {SteamBoot.Error})");
        }
    }

    /// <summary>Her kare: Steam geri cagirimlari; saniyede bir sunucu bilgisi (oyuncu sayisi, sifre) tazelenir.</summary>
    public static void Tick(float dt, ServerHost host, ServerFile cfg)
    {
        if (!_on) return;
        try
        {
            GameServer.RunCallbacks();
            _t -= dt;
            if (_t > 0) return;
            _t = 5f;
            SteamGameServer.SetBotPlayerCount(0);
            SteamGameServer.SetPasswordProtected(host.Password.Length > 0);
            SteamGameServer.SetMaxPlayerCount(cfg.MaxPlayers);
        }
        catch (Exception) { _on = false; }
    }

    public static void Stop()
    {
        if (!_on) return;
        try { SteamGameServer.LogOff(); GameServer.Shutdown(); } catch (Exception) { /* kapanirken */ }
        _on = false;
    }
}

/// <summary>Steam sunucu tarayicisi (istemci): internet listesinden gamedir'i eslesen sunuculari toplar. Steam yoksa bos ve neden.</summary>
public sealed class SteamServerBrowser
{
    public readonly List<ServerEntry> Entries = new();
    public string Status { get; private set; } = "";
    public bool Busy { get; private set; }
    HServerListRequest _req;
    ISteamMatchmakingServerListResponse _resp;
    float _timeout;
    int _max;

    public void Refresh(GameData data)
    {
        Entries.Clear();
        Cancel();
        try
        {
            if (!SteamBoot.Ensure((uint)data.Net.Steam.AppId)) { Status = Loc.T("server.steam_off", SteamBoot.Error); return; }
            _max = data.Server.Ping.SteamMaxResults;
            _timeout = data.Server.Ping.SteamTimeoutSeconds;
            _resp = new ISteamMatchmakingServerListResponse(OnResponded, OnFailed, OnComplete);
            var filters = new[] { new MatchMakingKeyValuePair_t { m_szKey = "gamedir", m_szValue = data.Server.Steam.GameDir } };
            _req = SteamMatchmakingServers.RequestInternetServerList(SteamUtils.GetAppID(), filters, (uint)filters.Length, _resp);
            Busy = true;
            Status = Loc.T("server.steam_searching");
        }
        catch (Exception e) { Status = Loc.T("server.steam_failed", e.GetType().Name); Busy = false; }
    }

    void OnResponded(HServerListRequest req, int i)
    {
        if (Entries.Count >= _max) return;
        try
        {
            var d = SteamMatchmakingServers.GetServerDetails(req, i);
            Entries.Add(new ServerEntry
            {
                Name = d.GetServerName(), Host = d.m_NetAdr.GetConnectionAddressString().Split(':')[0], Port = d.m_NetAdr.GetConnectionPort(),
                Players = d.m_nPlayers, MaxPlayers = d.m_nMaxPlayers, Ping = d.m_nPing, Password = d.m_bPassword, Source = "steam",
            });
        }
        catch (Exception) { /* bozuk satir */ }
    }

    void OnFailed(HServerListRequest req, int i) { }

    void OnComplete(HServerListRequest req, EMatchMakingServerResponse r)
    {
        Busy = false;
        Status = Entries.Count > 0 ? Loc.T("server.count", Entries.Count) : Loc.T("server.none");
    }

    /// <summary>Her kare (Steam geri cagirimlari + zaman asimi).</summary>
    public void Tick(float dt)
    {
        if (!Busy) return;
        SteamBoot.Pump();
        _timeout -= dt;
        if (_timeout <= 0) { Cancel(); Busy = false; Status = Entries.Count > 0 ? Loc.T("server.count", Entries.Count) : Loc.T("server.none"); }
    }

    public void Cancel()
    {
        try { if (_req != HServerListRequest.Invalid) SteamMatchmakingServers.ReleaseRequest(_req); } catch (Exception) { /* yok */ }
        _req = HServerListRequest.Invalid;
    }
}
