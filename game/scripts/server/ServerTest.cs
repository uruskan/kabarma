using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=server (M5 sunucu akisi): basliksiz bir adanmis sunucu surecini (--server) ve iki bot istemciyi kendisi baslatir.
/// Sirayla: sunucu hazir ("server_ready.txt") → 2 istemci baglanir (durum dosyasi oyuncu=2) → istemci 1 oyun ici yonetici olur
/// ("giris", "oyuncular": srv.cmd/srv.reply) → komut dosyasiyla oyuncular, duyuru, kaydet, at 2 → kayit yuvasi diskte, istemci 2
/// reddedildi → yasakla "Bot 1" → istemci 1 atildi ve ayni adla gelen 3. istemci yasak yuzunden girmez → kapat: sunucu kaydedip
/// kapanir. Surecler arasi iletisim bir klasordeki dosyalarla (--server_dir). Cikti: "SERVER RESULT ok|fail ..." ve cikis kodu.
/// </summary>
public partial class ServerTest : Node
{
    const float Timeout = 360f;
    string _role = "orch";
    string _dir, _saveRoot;
    int _port, _stage;
    float _t, _stageT;
    int _serverPid = -1;
    readonly List<int> _pids = new();
    readonly List<string> _fail = new();
    int _checks;

    /// <summary>Main.Tests["server"]: rol (orch ya da client) ve istemci bayraklari. Orkestratorde true (Director kurulmaz).</summary>
    public static bool Setup(Main m)
    {
        var a = Main.Args;
        string role = a.TryGetValue("role", out var r) ? r : "orch";
        a["mute"] = "1"; Sfx.Muted = true;
        if (role == "client")
        {
            a["coop"] = "client"; a["transport"] = "enet"; a["bot"] = "1"; a["coopbot"] = "1";
            a.TryAdd("connect", "127.0.0.1");
        }
        m.AddChild(new ServerTest { _role = role });
        return role == "orch";
    }

    void Check(bool ok, string what) { _checks++; if (!ok) _fail.Add(what); GD.Print($"SERVER {(ok ? "ok" : "FAIL")} {what}"); }

    public override void _Ready()
    {
        Engine.MaxFps = 60;
        var a = Main.Args;
        _dir = a.TryGetValue("server_dir", out var d) ? d.Replace('\\', '/').TrimEnd('/') + "/" : null;
        if (_role != "orch") return;
        string id = (OS.GetProcessId() % 100000).ToString();
        _saveRoot = $"user://test/server_{id}";
        _dir = ProjectSettings.GlobalizePath($"user://test/server_{id}_ctl").Replace('\\', '/') + "/";
        DirAccess.MakeDirRecursiveAbsolute(_dir);
        foreach (var f in new[] { "server_ready.txt", "server_commands.txt", "server_results.log", "server_status.txt", "client_1_joined.txt", "client_2_joined.txt", "client_1_admin.txt", "client_2_failed.txt", "client_1_failed.txt", "client_3_failed.txt", "client_4_failed.txt", "client_4_admin.txt", "client_1_quit.txt", "server_runtime.json" })
            if (FileAccess.FileExists(_dir + f)) DirAccess.RemoveAbsolute(_dir + f);
        _port = FreePort();
        SpawnServer();
    }

    static int FreePort()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = 0; i < 40; i++)
        {
            int p = rng.RandiRange(27100, 28900);
            var c = new ENetConnection();
            if (c.CreateHostBound("*", p, 1, 1) == Error.Ok) { c.Destroy(); return p; }
        }
        return 27016;
    }

    string Res => ProjectSettings.GlobalizePath("res://");

    void SpawnServer()
    {
        var args = new[] { "--headless", "--path", Res, "--", "--server", $"--port={_port}", $"--server_dir={_dir}", $"--save_root={_saveRoot}", "--slot=sunucutest", "--admin_password=ypw", "--name=Deneme", "--max_players=4" };
        _serverPid = OS.CreateProcess(OS.GetExecutablePath(), args);
        GD.Print($"SERVER spawn server pid={_serverPid} port={_port} dir={_dir}");
    }

    void SpawnClient(int k, string name = null)
    {
        var args = new[] { "--headless", "--path", Res, "--", "--test=server", "--role=client", $"--connect=127.0.0.1", $"--port={_port}", $"--bot_id={k}", $"--name={name ?? "Bot " + k}", $"--server_dir={_dir}", "--admin_password=ypw", "--mute" };
        _pids.Add(OS.CreateProcess(OS.GetExecutablePath(), args));
    }

    string Read(string file) => FileAccess.FileExists(_dir + file) ? FileAccess.GetFileAsString(_dir + file) : "";
    bool Has(string file, string needle = "") => needle.Length == 0 ? FileAccess.FileExists(_dir + file) : Read(file).Contains(needle);
    void Cmd(params string[] lines) { using var f = FileAccess.Open(_dir + "server_commands.txt", FileAccess.ModeFlags.Write); f.StoreString(string.Join("\n", lines) + "\n"); }
    void Next() { _stage++; _stageT = 0; }

    public override void _Process(double delta)
    {
        float dt = (float)delta / Mathf.Max(0.01f, (float)Engine.TimeScale);
        _t += dt; _stageT += dt;
        if (_role == "client") { ClientTick(); return; }
        if (_t > Timeout) { _fail.Add($"zaman asimi stage={_stage}"); Finish(); return; }
        switch (_stage)
        {
            case 0: // sunucu hazir
                if (Has("server_ready.txt")) { Check(true, "sunucu basliksiz kalkti (server_ready)"); SpawnClient(1); SpawnClient(2); Next(); }
                else if (_stageT > 120) { _fail.Add("sunucu kalkmadi"); Finish(); }
                break;
            case 1: // iki istemci baglandi
                if (Has("client_1_joined.txt") && Has("client_2_joined.txt") && Has("server_status.txt", "oyuncu=2/")) { Check(true, "2 bot istemci baglandi (durum dosyasi oyuncu=2)"); Next(); }
                else if (_stageT > 150) { _fail.Add($"istemciler baglanmadi (j1={Has("client_1_joined.txt")} j2={Has("client_2_joined.txt")} durum='{Read("server_status.txt").Trim()}')"); Finish(); }
                break;
            case 2: // oyun ici yonetici
                if (Has("client_1_admin.txt", "ok")) { Check(true, "oyun ici yonetici: giris + oyuncular (srv.cmd/srv.reply)"); Put("client_1_quit.txt", "1"); Next(); }
                else if (_stageT > 60) { _fail.Add($"oyun ici yonetim calismadi: '{Read("client_1_admin.txt").Trim()}'"); Finish(); }
                break;
            case 3: // yonetici normal ayrilir: sunucuda yalniz istemci 2 kalir
                if (Has("server_status.txt", "oyuncu=1/")) { Check(true, "yonetici ayrildi (oyuncu=1)"); SpawnClient(4); Next(); }
                else if (_stageT > 40) { _fail.Add("yonetici ayrilmadi: " + Read("server_status.txt").Trim()); Finish(); }
                break;
            case 4: // ayni es kimligini alan yeni oyuncu yonetici DEGILDIR
                if (Has("client_4_admin.txt"))
                {
                    Check(Read("client_4_admin.txt").Contains("ok"), "yeni oyuncu (yeniden kullanilan es kimligi) yonetici degil (" + Read("client_4_admin.txt").Trim() + ")");
                    Cmd("oyuncular", "duyuru Deneme duyurusu", "kaydet", "durum", "at 2 deneme"); Next();
                }
                else if (_stageT > 90) { _fail.Add("istemci 4 sinamasi calismadi"); Finish(); }
                break;
            case 5: // komut dosyasi
                if (Has("server_results.log", "at 2 deneme =>"))
                {
                    string r = Read("server_results.log");
                    Check(r.Contains("oyuncular => 2 oyuncu"), "komut oyuncular");
                    Check(r.Contains("duyuru Deneme duyurusu => duyuruldu"), "komut duyuru");
                    Check(r.Contains("kaydet => kaydedildi"), "komut kaydet (" + r.Split('\n').FirstOrDefault(l => l.StartsWith("kaydet")) + ")");
                    Check(r.Contains("at 2 deneme => atıldı"), "komut at");
                    var store = new SaveStore(ProjectSettings.GlobalizePath(_saveRoot), GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f)).Save);
                    Check(store.Exists("sunucutest"), "kayit yuvasi diskte");
                    Next();
                }
                else if (_stageT > 60) { _fail.Add($"komutlar calismadi: '{Read("server_results.log").Replace('\n', '|')}'"); Finish(); }
                break;
            case 6: // atilan istemci reddedildi
                if (Has("client_2_failed.txt")) { Check(Read("client_2_failed.txt").Contains("deneme"), "atilan istemci nedeni gordu (" + Read("client_2_failed.txt").Trim() + ")"); Cmd("yasakla Bot 4 5 hile"); Next(); }
                else if (_stageT > 30) { _fail.Add("atilan istemci kopmadi"); Finish(); }
                break;
            case 7: // yasaklanan atildi, ayni adla yeniden gelen girmez
                if (Has("client_4_failed.txt")) { Check(Read("client_4_failed.txt").Contains("yasak"), "yasaklanan istemci atildi (" + Read("client_4_failed.txt").Trim() + ")"); SpawnClient(3, "Bot 4"); Next(); }
                else if (_stageT > 30) { _fail.Add("yasaklanan atilmadi"); Finish(); }
                break;
            case 8:
                if (Has("client_3_failed.txt")) { Check(Read("client_3_failed.txt").Contains("yasak"), "yasakli ad girmedi (" + Read("client_3_failed.txt").Trim() + ")"); Check(Has("server_runtime.json", "bot 4"), "yasak diske yazildi"); Cmd("kapat"); Next(); }
                else if (_stageT > 90) { _fail.Add("yasakli istemci reddedilmedi"); Finish(); }
                break;
            case 9: // kapanis
                if (_serverPid > 0 && !OS.IsProcessRunning(_serverPid)) { Check(Read("server_status.txt").StartsWith("kapali"), "sunucu kaydedip kapandi"); Finish(); }
                else if (_stageT > 40) { _fail.Add("sunucu kapanmadi"); Finish(); }
                break;
        }
    }

    // ---------------------------------------------------------------- istemci

    bool _wrote, _failedWritten, _adminStarted, _adminSent2;
    float _joinedT;
    string _id => Main.Args.GetValueOrDefault("bot_id", "x");

    void ClientTick()
    {
        var d = GetTree().Root.FindChild("Director", true, false) as Director;
        var net = d?.Net;
        if (net == null || _dir == null) return;
        if (net.Failed && !_failedWritten) { _failedWritten = true; Put($"client_{_id}_failed.txt", net.Status); GD.Print($"SERVER client {_id} failed: {net.Status}"); return; }
        if (!net.Joined) return;
        _joinedT += (float)GetProcessDeltaTime();
        if (!_wrote) { _wrote = true; Put($"client_{_id}_joined.txt", "ok"); }
        if (_id == "1" && _joinedT > 4f && !_adminStarted)
        {
            _adminStarted = true;
            net.Forward("srv.cmd", w => w.Str("giris ypw"));
        }
        if (_id == "1" && _adminStarted && !_adminSent2 && ServerConsolePanel.LastReply.Contains("yönetici oldun"))
        {
            _adminSent2 = true;
            net.Forward("srv.cmd", w => w.Str("oyuncular"));
        }
        if (_id == "1" && Has("client_1_quit.txt")) { GetTree().Quit(); return; } // normal ayrilis (Bye)
        if (_id == "4" && _joinedT > 3f && !_adminStarted) { _adminStarted = true; net.Forward("srv.cmd", w => w.Str("oyuncular")); }
        if (_id == "4" && _adminStarted && ServerConsolePanel.LastReply.Contains("yönetici değilsin") && !Has("client_4_admin.txt")) Put("client_4_admin.txt", "ok " + ServerConsolePanel.LastReply);
        if (_id == "1" && _adminSent2 && ServerConsolePanel.LastReply.Contains("oyuncu:") && !Has($"client_1_admin.txt")) Put("client_1_admin.txt", "ok " + ServerConsolePanel.LastReply.Replace('\n', ' '));
    }

    void Put(string file, string text) { using var f = FileAccess.Open(_dir + file, FileAccess.ModeFlags.Write); f?.StoreString(text); }

    void Finish()
    {
        bool ok = _fail.Count == 0;
        GD.Print($"SERVER RESULT {(ok ? "ok" : "fail")} checks={_checks} t={_t:0}" + (ok ? "" : " :: " + string.Join(" | ", _fail)));
        foreach (var pid in _pids) if (OS.IsProcessRunning(pid)) OS.Kill(pid);
        if (_serverPid > 0 && OS.IsProcessRunning(_serverPid)) OS.Kill(_serverPid);
        GetTree().Quit(ok ? 0 : 1);
    }
}
