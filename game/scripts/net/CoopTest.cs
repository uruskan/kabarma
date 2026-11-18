using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=coop (URETIM 6 M2 kabulu: 4 bot istemciyle senkron farki &lt; 1 cm; TEKNIK 11 ortak oyun): ayni makinede
/// basliksiz host 3 istemciyi kendisi baslatir (ENet, bos bir port); 2'si hemen, 1'i sonradan katilir. Botlar kazar,
/// odun bicer, temel kurar ve durur (Bot.Coop.cs). Hepsi bitince host "topla" der; her istemci kendi konumunu,
/// gordugu avatarlarin konumunu ve bolum ozetlerini yollar. Olcutler:
///  · her oyuncunun avatari, sahibinin kendi konumundan &lt; 1 cm (her surecte);
///  · canta (host'taki kopyayla), yapi, yerlestirilebilir, kaya, tutam ozetleri host'unkiyle ayni;
///  · her bot en az bir temel kurdu ve tas kazdi (istekler gercekten host'tan gecti);
///  · sonradan katilan, katilmadan once kurulmus her seyi goruyor.
/// Cikti: "COOP RESULT ok|fail ..." (host). Istemci gunlukleri .tools/run/test/coop_client_N.log.
/// </summary>
public partial class CoopTest : Node
{
    const int Clients = 3;
    const float LateJoinAt = 35f;   // gercek sn: ilk ikisi kurmaya baslamisken
    const float Timeout = 420f;
    const float SettleSeconds = 2.5f;

    Director _d;
    NetSync Net => _d?.Net;
    bool _host;
    float _t;
    readonly List<int> _pids = new();
    bool _lateSpawned, _collecting, _finished;
    float _collectT, _quitT = -1;
    int _exit;
    readonly Dictionary<byte, Report> _reports = new();
    readonly HashSet<byte> _done = new();
    string _logPath;
    int _port;

    sealed class Report
    {
        public Vector3 Own;
        public Dictionary<byte, Vector3> Seen = new();
        public Dictionary<string, ulong> Hashes = new();
        public int Stone, Built, Mismatches, Applied;
        public int PiecesSeen;
    }

    /// <summary>Main.Tests["coop"]: rol ve bos port; Director kurulmadan once.</summary>
    public static void Setup()
    {
        var a = Main.Args;
        string role = a.TryGetValue("role", out var r) ? r : "host";
        a["coop"] = role;
        a["bot"] = "1";
        a["coopbot"] = "1";
        a["mute"] = "1";
        Sfx.Muted = true;
        if (role == "host") { a.TryAdd("bot_id", "0"); a.TryAdd("name", "Host"); if (!a.ContainsKey("port")) a["port"] = FreePort().ToString(); }
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
        return 27015;
    }

    public override void _Ready()
    {
        Engine.MaxFps = 60; // ag gercek zamanli; dort surec ayni makinede
        _host = Main.Args["coop"] == "host";
        _port = int.Parse(Main.Args["port"]);
        string runDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../.tools/run/test";
        DirAccess.MakeDirRecursiveAbsolute(runDir);
        if (!_host) _logPath = $"{runDir}/coop_client_{Main.Args.GetValueOrDefault("bot_id", "x")}.log";
        CallDeferred(nameof(Hook));
        Log($"COOP {(_host ? "host" : "client")} port={_port} bot={Main.Args.GetValueOrDefault("bot_id")}");
    }

    void Hook()
    {
        _d = GetTree().Root.FindChild("Director", true, false) as Director;
        if (Net == null) { Log("COOP RESULT fail no NetSync"); Quit(1); return; }
        if (_host)
        {
            Net.TestMessageFrom += OnClientMessage;
            for (int k = 1; k <= Clients - 1; k++) Spawn(k);
        }
        else Net.TestMessage += OnHostMessage;
    }

    void Spawn(int k)
    {
        string path = ProjectSettings.GlobalizePath("res://");
        var args = new[] { "--headless", "--path", path, "--", "--test=coop", "--role=client", $"--port={_port}", $"--bot_id={k}", $"--name=Bot {k}", "--mute" };
        int pid = OS.CreateProcess(OS.GetExecutablePath(), args);
        _pids.Add(pid);
        Log($"COOP spawn client {k} pid={pid}");
    }

    void Log(string s)
    {
        GD.Print(s);
        if (_logPath == null) return;
        using var f = FileAccess.FileExists(_logPath) ? FileAccess.Open(_logPath, FileAccess.ModeFlags.ReadWrite) : FileAccess.Open(_logPath, FileAccess.ModeFlags.Write);
        if (f == null) return;
        f.SeekEnd();
        f.StoreLine($"{Time.GetTicksMsec() / 1000.0:0.00} {s}");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta / Mathf.Max(0.01f, (float)Engine.TimeScale);
        _t += dt;
        if (_quitT > 0) { _quitT -= dt; if (_quitT <= 0) Quit(_exit); return; }
        if (_d == null || Net == null) return;
        if (Net.Failed) { Log($"COOP RESULT fail net: {Net.Status}"); Finish(1); return; }
        if (_t > Timeout) { Log($"COOP RESULT fail timeout t={_t:0} done={string.Join(",", _done)} reports={_reports.Count} roster={Net.Roster.Count}"); Finish(1); return; }
        var bot = _d.Bot;
        if (!_host)
        {
            if (bot != null && bot.CoopDone && !_sentDone) { _sentDone = true; SendHost(0); Log($"COOP client done t={_t:0.0}"); }
            return;
        }
        if (!_lateSpawned && _t >= LateJoinAt) { _lateSpawned = true; Spawn(Clients); }
        if (bot != null && bot.CoopDone) _done.Add(0);
        if (!_collecting && _lateSpawned && _done.Count >= Clients + 1 && Net.Actors.Count == Clients)
        {
            _collecting = true;
            _collectT = SettleSeconds;
            Log($"COOP all done t={_t:0.0}; durulma {SettleSeconds} sn");
        }
        if (_collecting && _collectT > 0)
        {
            _collectT -= dt;
            if (_collectT <= 0)
            {
                Net.Flush();
                var w = NetPackets.Begin(NetMsg.Final); w.U8(1);
                Net.Broadcast(w.ToArray());
                Log("COOP collect");
            }
        }
        if (_collecting && _collectT <= 0 && _reports.Count == Clients && !_finished) Judge();
    }

    bool _sentDone;

    void SendHost(byte kind)
    {
        var w = NetPackets.Begin(NetMsg.Report);
        w.U8(kind);
        if (kind == 1) WriteReport(w);
        Net.SendHost(w.ToArray());
    }

    // ---------- istemci ----------

    void OnHostMessage(NetMsg msg, NetReader r)
    {
        if (msg != NetMsg.Final) return;
        byte k = r.U8();
        if (k == 1) { SendHost(1); Log("COOP report sent"); }
        else if (k == 2) { int code = r.U8(); Log($"COOP final from host code={code}"); _exit = code; _quitT = 0.5f; }
    }

    void WriteReport(NetWriter w)
    {
        var q = Net.Cfg.PosPerMetre;
        var own = _d.Player.GlobalPosition;
        w.F32(own.X); w.F32(own.Y); w.F32(own.Z);
        w.U8((byte)Net.Avatars.Count);
        foreach (var (peer, av) in Net.Avatars) { w.U8(peer); var p = av.GlobalPosition; w.F32(p.X); w.F32(p.Y); w.F32(p.Z); }
        w.U8((byte)Net.Sections.All.Count);
        foreach (var s in Net.Sections.All) { w.Str(s.Id); w.U64(Net.Sections.Hash(s, Net.LocalPeer)); }
        w.VarU((uint)_d.State.Backpack.Count("stone"));
        w.VarU((uint)(_d.Bot?.CoopBuilt ?? 0));
        w.VarU((uint)Net.SectionMismatches);
        w.VarU((uint)Net.SectionsApplied);
        w.VarU((uint)(_d.Build?.System.Pieces.Count ?? 0));
    }

    // ---------- host ----------

    void OnClientMessage(byte peer, NetMsg msg, NetReader r)
    {
        if (msg != NetMsg.Report) return;
        byte kind = r.U8();
        if (kind == 0) { _done.Add(peer); Log($"COOP peer {peer} done t={_t:0.0}"); return; }
        var rep = new Report { Own = new Vector3(r.F32(), r.F32(), r.F32()) };
        int n = r.U8();
        for (int i = 0; i < n; i++) { byte p = r.U8(); rep.Seen[p] = new Vector3(r.F32(), r.F32(), r.F32()); }
        int m = r.U8();
        for (int i = 0; i < m; i++) rep.Hashes[r.Str()] = r.U64();
        rep.Stone = (int)r.VarU(); rep.Built = (int)r.VarU(); rep.Mismatches = (int)r.VarU(); rep.Applied = (int)r.VarU(); rep.PiecesSeen = (int)r.VarU();
        _reports[peer] = rep;
        Log($"COOP report peer={peer} stone={rep.Stone} built={rep.Built} pieces={rep.PiecesSeen} applied={rep.Applied} mismatches={rep.Mismatches}");
    }

    void Judge()
    {
        _finished = true;
        var own = new Dictionary<byte, Vector3> { [0] = _d.Player.GlobalPosition };
        foreach (var (p, rep) in _reports) own[p] = rep.Own;
        double maxDiff = 0;
        var fails = new List<string>();
        // Host'un gordugu avatarlar
        foreach (var (p, av) in Net.Avatars)
        {
            double d = av.GlobalPosition.DistanceTo(own.GetValueOrDefault(p, Vector3.Inf));
            maxDiff = System.Math.Max(maxDiff, d);
        }
        foreach (var (c, rep) in _reports)
        {
            foreach (var (p, pos) in rep.Seen)
            {
                double d = pos.DistanceTo(own.GetValueOrDefault(p, Vector3.Inf));
                maxDiff = System.Math.Max(maxDiff, d);
                if (d >= 0.01) fails.Add($"peer{c} sees peer{p} off {d * 1000:0.0}mm");
            }
            if (rep.Seen.Count != Clients) fails.Add($"peer{c} sees {rep.Seen.Count} players");
            foreach (var s in Net.Sections.All)
            {
                ulong want = Net.Sections.Hash(s, c);
                if (!rep.Hashes.TryGetValue(s.Id, out var got) || got != want) fails.Add($"peer{c} section {s.Id} differs");
            }
            if (rep.Built < 1) fails.Add($"peer{c} built nothing");
            var actor = Net.Actors.GetValueOrDefault(c);
            if (actor == null || actor.Bag.Count("stone") < 4) fails.Add($"peer{c} mined {actor?.Bag.Count("stone")}");
        }
        int pieces = _d.Build?.System.Pieces.Count ?? 0;
        int bases = _d.Build?.System.Pieces.Count(p => p.Def.Id == "kare_temel") ?? 0;
        if (bases < Clients + 1) fails.Add($"host has {bases} foundations, want {Clients + 1}");
        bool ok = fails.Count == 0 && maxDiff < 0.01;
        int rejected = Net.RequestsRejected, corr = Net.Corrections;
        Log($"COOP RESULT {(ok ? "ok" : "fail")} players={Clients + 1} max_diff_mm={maxDiff * 1000:0.000} pieces={pieces} foundations={bases} late_join=1 rejected={rejected} corrections={corr} t={_t:0.0}" + (fails.Count > 0 ? " :: " + string.Join("; ", fails.Take(8)) : ""));
        Finish(ok ? 0 : 1);
    }

    void Finish(int code)
    {
        if (_finished && _quitT > 0) return;
        _finished = true;
        _exit = code;
        if (_host && Net != null)
        {
            var w = NetPackets.Begin(NetMsg.Final); w.U8(2); w.U8((byte)code);
            Net.Broadcast(w.ToArray());
        }
        _quitT = 2.0f;
    }

    void Quit(int code)
    {
        foreach (var pid in _pids) if (OS.IsProcessRunning(pid)) OS.Kill(pid);
        GetTree().Quit(code);
    }
}
