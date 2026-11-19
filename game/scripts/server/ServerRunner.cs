using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Adanmis sunucu (TEKNIK §5, §12; M5 sunucu akisi). <c>Godot --headless --path game -- --server</c> ile kalkar: oyuncu
/// karakteri yok (Player gizli ve park edilmis, oyuncu listesinde sayilmaz), host ENet'te dinler, dunya kayit yuvasindan
/// yuklenir (yoksa yeni dunya) ve yuvaya kaydedilir: otomatik kayit (server.json autosaveMinutes), safak ve cikis. Rapor
/// ekrani kendiliginden kapanir. Yonetim komutlari (ServerAdmin) uc yoldan gelir: konsol (stdin), komut dosyasi
/// (server.json commandFile) ve oyun icinden yonetici istegi ("srv.cmd", F8 paneli; yonetici olmak icin 'giris &lt;sifre&gt;').
/// Komut satiri server.json'u ezer: --port --name --password --admin_password --slot --max_players --server_dir.
/// </summary>
public partial class ServerRunner : Node
{
    ServerFile _cfg;
    GameData _data;
    Main _main;
    Director _d;
    ServerHost _host;
    bool _ready, _closing;
    double _t, _statusT, _cmdT, _closeT;
    readonly ConcurrentQueue<string> _stdin = new();
    string _dir;

    public ServerHost Host => _host;
    public static string Log(string s) { GD.Print("SERVER " + s); return s; }

    public override void _Ready()
    {
        _main = GetParent<Main>();
        _data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        _cfg = _data.Server;
        var a = Main.Args;
        if (a.TryGetValue("port", out var ps) && int.TryParse(ps, out var port)) _cfg.Port = port;
        if (a.TryGetValue("name", out var nm) && nm.Length > 0) _cfg.Name = nm;
        if (a.TryGetValue("password", out var pw)) _cfg.Password = pw;
        if (a.TryGetValue("admin_password", out var ap)) _cfg.AdminPassword = ap;
        if (a.TryGetValue("slot", out var sl) && sl.Length > 0) _cfg.SaveSlot = sl;
        if (a.TryGetValue("max_players", out var mp) && int.TryParse(mp, out var mpn)) _cfg.MaxPlayers = mpn;
        _dir = a.TryGetValue("server_dir", out var sd) && sd.Length > 0 ? sd.Replace('\\', '/').TrimEnd('/') : "user://";
        if (!_dir.EndsWith("/")) _dir += "/";
        if (_dir != "user://") DirAccess.MakeDirRecursiveAbsolute(_dir);
        // ortak oyun host'u, oyuncu yok
        a["coop"] = "host"; a["transport"] = "enet"; a["port"] = _cfg.Port.ToString(); a["name"] = "[Sunucu] " + _cfg.Name;
        a["mute"] = "1"; Sfx.Muted = true; AudioServer.SetBusMute(0, true);
        // kayit: yuvadan yukle (yoksa yeni dunya)
        var store = new SaveStore(ProjectSettings.GlobalizePath(_main.MenuRoot(_data)), _data.Save);
        SaveGame load = null;
        try { if (store.Exists(_cfg.SaveSlot)) load = store.Read(_cfg.SaveSlot); }
        catch (Exception e) { GD.PushError($"SERVER kayit okunamadi ({e.Message}): yeni dunya"); }
        _data.Save.AutosaveMinutes = (int)Math.Max(1, _cfg.AutosaveMinutes);
        _main.StartGame(load, _cfg.SaveSlot);
        Log($"start name='{_cfg.Name}' port={_cfg.Port} slot={_cfg.SaveSlot} load={(load != null ? "day " + load.Manifest.Day : "yeni dunya")} dir={_dir}");
        if (_cfg.Stdin) StartStdin();
    }

    void StartStdin()
    {
        var th = new Thread(() =>
        {
            try { string line; while ((line = Console.In.ReadLine()) != null) _stdin.Enqueue(line); }
            catch (Exception) { /* stdin kapali */ }
        }) { IsBackground = true, Name = "server-stdin" };
        th.Start();
    }

    public override void _Process(double delta)
    {
        double dt = delta / Math.Max(0.01, Engine.TimeScale);
        _t += dt;
        if (_closing) { _closeT -= dt; if (_closeT <= 0) GetTree().Quit(); return; }
        if (!_ready) { TrySetup(); return; }
        // rapor ekrani oyuncu beklemeden kapanir (sabah raporu sunucuda okunmaz; kayit alinabilsin)
        if (_d.Hud.ReportOpen) _d.CloseReport();
        SteamListing.Tick((float)dt, _host, _cfg);
        _statusT -= dt; _cmdT -= dt;
        if (_statusT <= 0) { _statusT = 1; WriteStatus(); }
        if (_cmdT <= 0) { _cmdT = 0.4; PollCommands(); }
        while (_stdin.TryDequeue(out var line)) Run(line, "konsol");
    }

    void TrySetup()
    {
        _d = _main.Game;
        if (_d?.Net == null || !_d.Net.IsHost) { if (_t > 60) { Log("net kurulamadi"); GetTree().Quit(1); } return; }
        if (_d.Net.Failed) { Log($"net basarisiz: {_d.Net.Status}"); GetTree().Quit(2); return; }
        _host = new ServerHost(this, _d, _cfg, _dir);
        _d.Net.JoinGate = (name, pass, addr) =>
        {
            var why = ServerAdmin.CheckJoin(_host, name, pass, addr);
            if (why != null) Log($"join reddedildi name={name} addr={addr}: {why}");
            return why;
        };
        _host.BindNet();
        // oyuncu karakteri yok: gizle, park et, fizigi kapat
        var p = _d.Player;
        p.Visible = false; p.SetProcess(false); p.SetPhysicsProcess(false);
        p.GlobalPosition = new Vector3(0, 900, 0);
        _ready = true;
        Log($"ready port={_cfg.Port} max={_cfg.MaxPlayers} password={(_host.Password.Length > 0 ? "var" : "yok")} admin={(_cfg.AdminPassword.Length > 0 ? "var" : "yok")}");
        Write("server_ready.txt", $"port={_cfg.Port}\nslot={_cfg.SaveSlot}\n");
        SteamListing.Start(_cfg, _d.Net, _host); // Steam sunucu tarayicisinda goster (Steam yoksa atlanir)
    }

    // ---------------------------------------------------------------- komutlar

    void PollCommands()
    {
        string path = _dir + _cfg.CommandFile;
        if (!FileAccess.FileExists(path)) return;
        string text;
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Read)) text = f?.GetAsText() ?? "";
        if (text.Length == 0) return;
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write)) { f?.StoreString(""); }
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries)) Run(line.Trim('\r', ' '), "dosya");
    }

    /// <summary>Konsol ya da dosya komutu (tam yetki).</summary>
    public void Run(string line, string from)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        string res = ServerAdmin.Execute(_host, line);
        Log($"komut [{from}] {line} -> {res.Replace('\n', '|')}");
        if (from == "dosya") Append(_cfg.ResultFile, $"{line} => {res.Replace('\n', ' ')}\n");
        else if (from == "konsol") Console.WriteLine(res);
    }

    /// <summary>Kapat: oyunculari atar, kaydeder, kisa sure sonra cikar.</summary>
    public void Close()
    {
        if (_closing) return;
        _closing = true;
        _closeT = 0.8;
        _host.Save(out _);
        SteamListing.Stop();
        foreach (var p in _host.Players().ToList()) _d.Net.KickPeer((byte)p.Peer, "sunucu kapanıyor");
        Write("server_status.txt", "kapali\n");
        Log("closing");
    }

    void WriteStatus() => Write(_cfg.StateFile, _host.StatusLine() + "\n");

    public void Write(string file, string text)
    {
        using var f = FileAccess.Open(_dir + file, FileAccess.ModeFlags.Write);
        f?.StoreString(text);
    }

    void Append(string file, string text)
    {
        string path = _dir + file;
        using var f = FileAccess.Open(path, FileAccess.FileExists(path) ? FileAccess.ModeFlags.ReadWrite : FileAccess.ModeFlags.Write);
        if (f == null) return;
        f.SeekEnd();
        f.StoreString(text);
    }
}
