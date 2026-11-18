using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=stress (URETIM 6 M3 kabulu: KS 30 stres sahnesi, 8 bot; TEKNIK 5.3, 10): ayni makinede basliksiz host 7 ENet
/// istemcisini baslatir; hepsi ortak oyun botudur (Bot.Coop.cs: kazar, bicer, kurar). Host'ta sahnede surekli 220
/// kabarmis Zipzip tutulur; akis alaniyla Ocak'a ve ekinlere yururler (Zipzip.cs, TEKNIK 8.1). Tapu icindeki duvar
/// halkasinin bir parcasi her 2 sn'de kalkar ve geri konur (artimli onarim olculur).
/// Olcum penceresinde (net.json stress.measureSeconds): host'un CPU karesi (sinirsiz FPS, kare arasi sure), akis
/// alani tiki (is yapilan kareler), kablo bayti; istemcilerin indirmesi, kare sureleri ve gordukleri kukla sayisi.
/// Sert sinirlar bu akisin: ag butcesi, kuklalar, akis alani tiki. Kare butcesi ayri alan ("partial" = yalniz kare asti).
/// GECICI (kabarma3 Wave* main'e girene kadar): dusmanlari bu sahne dogurur; KS'nin can/hasar olcegi uygulanmaz
/// (botlar savasmaz; olculen sayi, yol ve ag yukudur). Wave* gelince dogurucu onunkiyle degisir.
/// Cikti: "STRESS RESULT ok|partial|fail ..." (host).
/// </summary>
public partial class StressTest : Node
{
    const float Timeout = 600f;
    const ulong WallOwner = 0x5157_0000_0000UL; // stres duvarlarinin iz kimligi (yapi parcasi degil)
    Director _d;
    NetSync Net => _d?.Net;
    NetStressDef S => _d.Data.Net.Stress;
    /// <summary>--solo: istemcisiz (yalniz host); ayni makinedeki 8 surecin yarismasini gercek maliyetten ayirmak icin. Kabul olcutu degildir.</summary>
    int Players => Main.Args.ContainsKey("solo") ? 1 : S.Players;
    /// <summary>--enemies=N (yalniz tani): dusman sayisini degistirir; 0 = bos sahnenin taban maliyeti. Kabul olcutu degildir.</summary>
    int Enemies => Main.Args.TryGetValue("enemies", out var e) && int.TryParse(e, out var n) ? n : S.Enemies;
    bool _host;
    float _t, _quitT = -1, _measureT = -1, _wallT, _collectT = -1;
    int _exit, _wallIdx;
    bool _started, _measuring, _done, _wallsUp, _lastWallDown;
    readonly List<int> _pids = new();
    readonly List<Zipzip> _enemies = new();
    // _frame: ardisik iki _Process arasi gercek sure (ms; host sinirsiz FPS'te = CPU karesi). Performance.TimeProcess
    // saniyenin en kotu karesini tuttugu icin kullanilmaz. _flowWork: yalniz alan guncellenen karelerin tik suresi.
    readonly List<float> _frame = new(), _flowWork = new(), _scan = new();
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    double _lastMs = -1;
    float _startT;
    int _cellsMax;
    readonly List<int> _active = new();
    long _sent0, _recv0;
    float _window;
    int _repairs0, _recomputes0, _spawned;
    readonly Dictionary<byte, Report> _reports = new();
    string _logPath;
    int _port;
    bool _sentReport;

    sealed class Report
    {
        public float DownKBps, ProcMed, ProcP95;
        public int Seen, Avatars;
    }

    /// <summary>Main.Tests["stress"]: rol ve bos port; Director kurulmadan once.</summary>
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
            int p = rng.RandiRange(29000, 30900);
            var c = new ENetConnection();
            if (c.CreateHostBound("*", p, 1, 1) == Error.Ok) { c.Destroy(); return p; }
        }
        return 27016;
    }

    public override void _Ready()
    {
        _host = Main.Args["coop"] == "host";
        Engine.MaxFps = _host ? 0 : 60; // host sinirsiz: kare arasi sure = CPU karesi; istemci botlari 60'ta
        _port = int.Parse(Main.Args["port"]);
        string runDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../.tools/run/test";
        DirAccess.MakeDirRecursiveAbsolute(runDir);
        if (!_host) _logPath = $"{runDir}/stress_client_{Main.Args.GetValueOrDefault("bot_id", "x")}.log";
        CallDeferred(nameof(Hook));
        Log($"STRESS {(_host ? "host" : "client")} port={_port} bot={Main.Args.GetValueOrDefault("bot_id")}");
    }

    void Hook()
    {
        _d = GetTree().Root.FindChild("Director", true, false) as Director;
        if (Net == null) { Log("STRESS RESULT fail no NetSync"); Quit(1); return; }
        if (_host) { Net.TestMessageFrom += OnClientMessage; for (int k = 1; k < Players; k++) Spawn(k); }
        else Net.TestMessage += OnHostMessage;
    }

    void Spawn(int k)
    {
        string path = ProjectSettings.GlobalizePath("res://");
        var args = new[] { "--headless", "--path", path, "--", "--test=stress", "--role=client", $"--port={_port}", $"--bot_id={k}", $"--name=Bot {k}", "--mute" };
        _pids.Add(OS.CreateProcess(OS.GetExecutablePath(), args));
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
        if (Net.Failed) { Log($"STRESS RESULT fail net: {Net.Status}"); Finish(1); return; }
        if (_t > Timeout) { Log($"STRESS RESULT fail timeout t={_t:0} players={Net.PlayerCount} active={_enemies.Count} measuring={_measuring} reports={_reports.Count}"); Finish(1); return; }
        double nowMs = _clock.Elapsed.TotalMilliseconds;
        if (_measuring && _lastMs >= 0) { _frame.Add((float)(nowMs - _lastMs)); if (!_host) _window += dt; }
        _lastMs = nowMs;
        if (!_host) return;

        if (!_started)
        {
            if (Net.Actors.Count < Players - 1 || Net.Actors.Values.Any(a => !a.Ready) || _d.Ocak == null || _d.Flow?.Fields == null) return;
            _started = true;
            _startT = _t;
            Log($"STRESS start players={Net.PlayerCount} ks={S.Ks} target={Enemies} t={_t:0.0}");
            Walls(true);
        }
        Keep();
        ToggleWall(dt);
        if (_measuring)
        {
            if (_d.Flow.LastTickWork > 0) { _flowWork.Add((float)_d.Flow.LastTickMs); _cellsMax = Mathf.Max(_cellsMax, _d.Flow.Fields.LastCells); }
            _scan.Add((float)_d.Flow.LastScanMs);
            _active.Add(_enemies.Count);
            _measureT -= dt;
            if (_measureT <= 0) EndMeasure();
        }
        // Isinma: olcum, duvarlar en az iki kez kalkip konduktan (onarim yolu JIT'lendikten) sonra baslar.
        else if (!_done && _enemies.Count >= Enemies * 0.9f && _measureT < 0 && _t - _startT >= 2 * S.WallToggleSeconds + 0.5f) BeginMeasure();
        if (_collectT > 0) { _collectT -= dt; if (_collectT <= 0 || _reports.Count >= Players - 1) Judge(); }
    }

    // ---------------------------------------------------------------- dusmanlar (GECICI dogurucu)

    void Keep()
    {
        _enemies.RemoveAll(z => !GodotObject.IsInstanceValid(z) || z.Dead || !z.IsInsideTree());
        var def = _d.Data.Creature(_d.Data.Kabarma.FirstNight.Enemy);
        var o = _d.OcakPos;
        for (int i = 0; i < S.SpawnPerFrame && _enemies.Count < Enemies; i++)
        {
            float a = GD.Randf() * Mathf.Tau, r = S.SpawnRadius + GD.Randf() * 4f;
            var p = _d.World.Ground(o + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            var z = new Zipzip(def, _d, 1f, 1f, swollen: true) { Name = $"StressZipzip{_spawned++}" };
            _d.AddChild(z);
            z.GlobalPosition = p;
            _enemies.Add(z);
            // Olcum ayrisimi (--noanim, yalniz tani): iskelet animasyonunun payini gormek icin oynatici durur.
            if (Main.Args.ContainsKey("frozen")) z.ProcessMode = ProcessModeEnum.Disabled; // --frozen (tani): betik ve animasyon durur, yalniz varlik maliyeti
            if (Main.Args.ContainsKey("noanim")) foreach (var ap in z.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>()) ap.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    /// <summary>Tapu icinde 20 m'de 6 duvar parcasi (araliklarla); biri her WallToggleSeconds'ta kalkip geri konur.</summary>
    void Walls(bool up)
    {
        for (int k = 0; k < 6; k++) Wall(k, up);
        _wallsUp = up;
    }

    void Wall(int k, bool up)
    {
        ulong id = WallOwner + (ulong)k;
        if (!up) { _d.Flow.Unstamp(id); return; }
        float a = k * Mathf.Tau / 6f;
        var c = _d.OcakPos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 20f;
        _d.Flow.StampBox(id, "wall", c, 7f, 0.15f, Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)));
    }

    void ToggleWall(float dt)
    {
        if (!_wallsUp) return;
        _wallT -= dt;
        if (_wallT > 0) return;
        _wallT = S.WallToggleSeconds;
        _lastWallDown = !_lastWallDown;
        if (_lastWallDown) { _wallIdx = (_wallIdx + 1) % 6; Wall(_wallIdx, false); } else Wall(_wallIdx, true);
    }

    // ---------------------------------------------------------------- olcum

    void BeginMeasure()
    {
        _measuring = true;
        _measureT = _window = S.MeasureSeconds;
        _sent0 = Net.BytesSent; _recv0 = Net.BytesReceived;
        var f = _d.Flow.Fields.Fields.Values;
        _repairs0 = f.Sum(x => x.Repairs); _recomputes0 = f.Sum(x => x.Recomputes);
        var w = NetPackets.Begin(NetMsg.Final); w.U8(5);
        Net.Broadcast(w.ToArray());
        Log($"STRESS measure begin active={_enemies.Count} t={_t:0.0}");    }

    void EndMeasure()
    {
        _measuring = false;
        _done = true;
        var w = NetPackets.Begin(NetMsg.Final); w.U8(1);
        Net.Broadcast(w.ToArray());
        _collectT = 8f;
        Log($"STRESS measure end t={_t:0.0}");
    }

    static float Pct(List<float> l, float p)
    {
        if (l.Count == 0) return 0;
        var s = l.OrderBy(x => x).ToList();
        return s[Mathf.Clamp((int)(p * (s.Count - 1)), 0, s.Count - 1)];
    }

    // ---------------------------------------------------------------- istemci

    void OnHostMessage(NetMsg msg, NetReader r)
    {
        if (msg != NetMsg.Final) return;
        byte k = r.U8();
        if (k == 5) { _measuring = true; _sent0 = Net.BytesSent; _recv0 = Net.BytesReceived; _window = 0; Log("STRESS client measure begin"); }
        else if (k == 1 && !_sentReport) { _sentReport = true; _measuring = false; SendReport(); }
        else if (k == 2) { int code = r.U8(); Log($"STRESS final code={code}"); _exit = code; _quitT = 0.5f; }
    }

    void SendReport()
    {
        float secs = Mathf.Max(1f, _window);
        var w = NetPackets.Begin(NetMsg.Report);
        w.U8(1);
        w.F32((Net.BytesReceived - _recv0) / 1024f / secs);
        w.F32(Pct(_frame, 0.5f)); w.F32(Pct(_frame, 0.95f));
        w.VarU((uint)Net.DynamicCount);
        w.VarU((uint)Net.Avatars.Count);
        Net.SendHost(w.ToArray());
        Log($"STRESS client report down_kBps={(Net.BytesReceived - _recv0) / 1024f / secs:0.0} frame_ms med={Pct(_frame, 0.5f):0.00} p95={Pct(_frame, 0.95f):0.00} seen={Net.DynamicCount} avatars={Net.Avatars.Count}");
    }

    // ---------------------------------------------------------------- host

    void OnClientMessage(byte peer, NetMsg msg, NetReader r)
    {
        if (msg != NetMsg.Report || r.U8() != 1) return;
        _reports[peer] = new Report { DownKBps = r.F32(), ProcMed = r.F32(), ProcP95 = r.F32(), Seen = (int)r.VarU(), Avatars = (int)r.VarU() };
    }

    void Judge()
    {
        if (_quitT > 0) return;
        _collectT = -1;
        var fails = new List<string>();
        float upKbps = (Net.BytesSent - _sent0) * 8f / 1000f / _window;
        var fs = _d.Flow.Fields.Fields.Values;
        int repairs = fs.Sum(x => x.Repairs) - _repairs0, recomputes = fs.Sum(x => x.Recomputes) - _recomputes0;
        float frameMed = Pct(_frame, 0.5f), frameP95 = Pct(_frame, 0.95f), flowP95 = Pct(_flowWork, 0.95f), flowMax = _flowWork.DefaultIfEmpty(0).Max();
        float over16 = _frame.Count == 0 ? 0 : _frame.Count(x => x > 1000f / 60f) / (float)_frame.Count;
        int minActive = _active.DefaultIfEmpty(0).Min();
        if (_reports.Count < Players - 1) fails.Add($"{_reports.Count}/{Players - 1} istemci raporu");
        if (Net.PlayerCount < Players) fails.Add($"oyuncu {Net.PlayerCount}/{Players}");
        if (minActive < Enemies * 0.9f) fails.Add($"aktif dusman en az {minActive} < {Enemies * 0.9f:0}");
        foreach (var (p, rep) in _reports)
        {
            if (rep.DownKBps > S.ClientDownKBps) fails.Add($"peer{p} indirme {rep.DownKBps:0.0} KB/sn > {S.ClientDownKBps}");
            if (rep.Seen < Enemies * 0.85f) fails.Add($"peer{p} {rep.Seen} kukla goruyor");
            if (rep.Avatars < Players - 1) fails.Add($"peer{p} {rep.Avatars} avatar");
        }
        if (upKbps > S.HostUpKbps) fails.Add($"host yukleme {upKbps:0} kbps > {S.HostUpKbps}");
        // Bu akisin sinirlari (sert): ag butcesi, kuklalar, akis alani tiki (yalniz is yapilan kareler, p95). Tik isi
        // maxCellsPerTick ile sinirli; en kotu ornek ayni makinedeki 8 surecte isletim sistemi duraklamasini da tasir
        // (ayni kosuda host karesi p95 ~180 ms goruldu), bu yuzden satira yazilir ama yargi p95'tedir.
        if (flowP95 > S.SimTickMs) fails.Add($"akis alani tiki p95 {flowP95:0.00} ms > {S.SimTickMs}");
        // Tik isi butceyle sinirli mi (en kotu ms'nin is mi duraklama mi oldugunu ayirir). Asim payi: asamalarin
        // bolunmeyen kuyrugu (degisen hucrelerin cevresi, etkilenen silme); bir butce kadar pay.
        int cellsCap = 2 * _d.Data.FlowField.MaxCellsPerTick;
        if (_cellsMax > cellsCap) fails.Add($"akis alani tik isi en cok {_cellsMax} hucre > {cellsCap}");
        if (repairs < 3 && Enemies > 0) fails.Add($"artimli onarim {repairs}"); // --enemies=0: alan sorgulanmaz, onarim yok
        bool ok = fails.Count == 0;
        // Kare butcesi (TEKNIK 10, RTX 2070S'te cizimli kare) burada yalniz basliksiz CPU karesiyle olculur: asarsa
        // sonuc "partial" (cikis 0), ayri alan olarak yazilir; sahibin makinesindeki olcum sahibin onayini bekler.
        bool frameOk = frameP95 <= S.KabarmaFrameMs;
        string verdict = !ok ? "fail" : frameOk ? "ok" : "partial";
        float downMax = _reports.Values.Select(x => x.DownKBps).DefaultIfEmpty(0).Max();
        float cliP95 = _reports.Values.Select(x => x.ProcP95).DefaultIfEmpty(0).Max();
        Log($"STRESS RESULT {verdict} players={Net.PlayerCount} ks={S.Ks} enemies_min={minActive} host_up_kbps={upKbps:0} (hedef {S.HostUpTargetKbps:0}, sinir {S.HostUpKbps:0}) client_down_kBps_max={downMax:0.0} (sinir {S.ClientDownKBps}) flow_tick_ms work_ticks={_flowWork.Count} p95={flowP95:0.00} max={flowMax:0.00} (sinir {S.SimTickMs}, p95) cells_max={_cellsMax} (butce {_d.Data.FlowField.MaxCellsPerTick}) flow_scan_p95={Pct(_scan, 0.95f):0.00} repairs={repairs} recomputes={recomputes} creature_packets={Net.CreaturePackets} frame={(frameOk ? "ok" : "over")} host_frame_ms med={frameMed:0.00} p95={frameP95:0.00} (sinir {S.KabarmaFrameMs}, basliksiz CPU) frames={_frame.Count} over16ms={over16:P0} client_frame_p95_max={cliP95:0.00} window={_window:0}s" + (fails.Count > 0 ? " :: " + string.Join("; ", fails.Take(8)) : ""));
        Finish(ok ? 0 : 1);
    }

    void Finish(int code)
    {
        if (_quitT > 0) return;
        _exit = code;
        if (_host && Net != null) { var w = NetPackets.Begin(NetMsg.Final); w.U8(2); w.U8((byte)code); Net.Broadcast(w.ToArray()); }
        _quitT = 2.0f;
    }

    void Quit(int code)
    {
        foreach (var pid in _pids) if (OS.IsProcessRunning(pid)) OS.Kill(pid);
        GetTree().Quit(code);
    }
}
