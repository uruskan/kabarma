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
    /// <summary>Toplam oyuncu (host dahil): varsayilan 3; sekiz (M5) icin --clients=8 (8 surec: makine kuralina gore dikkat, test.ps1 varsayilani 3).</summary>
    static readonly int Clients = Main.Args.TryGetValue("clients", out var cs) && int.TryParse(cs, out var cn) ? System.Math.Clamp(cn, 2, 8) : 3;
    const float LateJoinAt = 35f;   // gercek sn: ilk ikisi kurmaya baslamisken
    const float Timeout = 420f;
    const float SettleSeconds = 3.5f;
    string _glassProbe = ""; // kale: istemci Cam parca yerlestirir, Cam oder (NetSync.CoopGlassProbe)
    string _rivProbe = ""; // M5 cekisme: iki takim, gunduz dokunulmazlik, gece baskini (RivalrySystem.CoopProbe)
    int _ataProbe = -1; // -1: denenmedi, 0: baslamadi, 1: host dovusu basladi (ata4: Ata adak ve dovus ortak oyunda)

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
        public HashSet<string> SaveApplied = new();
        public int VoiceFrom2, VoiceSent, Dynamic, Plots, Ata, Ruins;
    }

    // Tatbikat (hepsi bitince): host bayilir, Bot 1 sok tokadiyla kaldirir; Bot 2 konusur (sinus, mikrofonsuz).
    const float DrillTimeout = 25f;
    const int VoiceFrames = 25;
    bool _drill;
    int _plots0;
    float _drillT;

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
            _plots0 = Tilled(); // botlarin surdugu kareler (tarim: yabani bitkiler sayilmaz)
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
            if (_handClientT >= 0f)
            {
                _handClientT += dt;
                if (_handClientT > HandoverWait - 3f && !_handLogged)
                {
                    _handLogged = true;
                    Log($"COOP HANDOVER state={(Net.IsHost ? "host" : "client")} joined={Net.Joined} roster={Net.Roster.Count} avatars={Net.Avatars.Count} {Net.Status}");
                }
            }
            return;
        }
        if (_handover) { TickHandover(dt); return; }
        if (!_lateSpawned && _t >= LateJoinAt) { _lateSpawned = true; Spawn(Clients); }
        if (bot != null && bot.CoopDone) _done.Add(0);
        if (!_drill && _lateSpawned && _done.Count >= Clients + 1 && Net.Actors.Count == Clients)
        {
            _drill = true;
            _d.Player.Hurt(_d.Player.Hp + 1f, _d.Player.GlobalPosition); // ortak oyunda bayilir (30 sn), olmez
            var w = NetPackets.Begin(NetMsg.Final); w.U8(3);
            Net.Broadcast(w.ToArray());
            Log($"COOP all done t={_t:0.0}; tatbikat: host bayildi={_d.Player.Down}, tokat ve ses");
        }
        if (_drill && !_collecting)
        {
            _drillT += dt;
            int voice = Net.Voice?.Received.Values.Sum() ?? 0; // konusan Bot 2; es kimligi baglanma sirasina gore
            if (_glassProbe == "" && (_drillT > 3f)) { var g = Net.CoopGlassProbe(); if (g != null) _glassProbe = g; } // kale: Cam yerlestirme (kare kare, bekleme suresiyle)
            if (_glassProbe != "" && ((Net.Revives >= 1 && !_d.Player.Down && voice >= VoiceFrames * 4 / 5) || _drillT > DrillTimeout))
            {
                _collecting = true;
                _collectT = SettleSeconds;
                var probeActor = Net.Actors.Values.FirstOrDefault();
                _ataProbe = probeActor != null && _d.Atalar.CoopProbe(probeActor.Bag) ? 1 : 0;
                Log($"COOP ata probe={_ataProbe} bag_yun_after={probeActor?.Bag.Count("yun")}");
                Log($"COOP glass probe: {_glassProbe}");
                _rivProbe = _d.CoopRivalryProbe();
                Log($"COOP rivalry probe: {_rivProbe}");
                Log($"COOP tatbikat bitti revives={Net.Revives} down={_d.Player.Down} voice_from2={voice} t={_drillT:0.0}; durulma {SettleSeconds} sn");
            }
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
        else if (k == 4) { _handClientT = 0f; Log("COOP handover basliyor"); } // sekiz (M5)
        else if (k == 3)
        {
            string id = Main.Args.GetValueOrDefault("bot_id");
            if (id == "1") { Net.SlapHeld = true; Log("COOP tatbikat: tokat basili"); }       // dinlenme yeri host'un 1,5 m yaninda
            if (id == "2") { Net.Voice?.InjectTone(VoiceFrames); Log($"COOP tatbikat: konustu {VoiceFrames} cerceve"); }
        }
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
        w.U8((byte)Net.Applied.Count);
        foreach (var id in Net.Applied.Keys) w.Str(id);
        w.VarU((uint)(Net.Voice?.Received.Values.Sum() ?? 0)); w.VarU((uint)(Net.Voice?.Sent ?? 0));
        w.VarU((uint)Net.DynamicCount);
        w.VarU((uint)_d.State.Farm.Plots.Count());
        w.U8((byte)(_d.Atalar.Fight != null && _d.Atalar.Actor != null ? 1 : 0)); // ata4: dovus ve kukla goruluyor mu
        w.VarU((uint)_d.State.Rivalry.Ruins.Count); // M5 cekisme: istemci enkaz planlarini goruyor mu
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
        int k = r.U8();
        for (int i = 0; i < k; i++) rep.SaveApplied.Add(r.Str());
        rep.VoiceFrom2 = (int)r.VarU(); rep.VoiceSent = (int)r.VarU(); rep.Dynamic = (int)r.VarU(); rep.Plots = (int)r.VarU(); rep.Ata = r.U8(); rep.Ruins = (int)r.VarU();
        _reports[peer] = rep;
        Log($"COOP report peer={peer} stone={rep.Stone} built={rep.Built} pieces={rep.PiecesSeen} applied={rep.Applied} mismatches={rep.Mismatches} save_sections={rep.SaveApplied.Count}/{Net.BridgedSections} voice_heard={rep.VoiceFrom2} voice_sent={rep.VoiceSent} creatures_spawned={rep.Dynamic} plots={rep.Plots}");
    }

    int Tilled() => _d.State.Farm.Plots.Count(p => !p.Wild);

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
                if (!rep.Hashes.TryGetValue(s.Id, out var got)) { fails.Add($"peer{c} section {s.Id} missing"); continue; }
                if (Net.IsVolatile(s.Id)) { if (!rep.SaveApplied.Contains(s.Id)) fails.Add($"peer{c} never applied {s.Id}"); continue; } // istemcide de isler: yalniz uygulandi mi
                if (s.Id == "ata.state") continue; // dovus suruyor: oynak, asagida rep.Ata ile
                if (s.Id == "spawns") continue; // dogan/olen yaratik toplama aninda degisebilir: asagida sayiyla
                if (got != Net.Sections.Hash(s, c)) fails.Add($"peer{c} section {s.Id} differs");
            }
            if (rep.Built < 1) fails.Add($"peer{c} built nothing");
            if (_ataProbe == 1 && rep.Ata != 1) fails.Add($"peer{c} Ata dovusunu/kuklayi gormedi");
            if (rep.Ruins != _d.State.Rivalry.Ruins.Count) fails.Add($"peer{c} sees {rep.Ruins} ruins, host {_d.State.Rivalry.Ruins.Count}");
            var actor = Net.Actors.GetValueOrDefault(c);
            if (actor == null || actor.Bag.Count("stone") < 4) fails.Add($"peer{c} mined {actor?.Bag.Count("stone")}");
            if (rep.VoiceSent == 0 && rep.VoiceFrom2 < VoiceFrames * 4 / 5) fails.Add($"peer{c} heard {rep.VoiceFrom2}/{VoiceFrames} voice frames from peer2");
            if (System.Math.Abs(rep.Dynamic - Net.DynamicCount) > 3) fails.Add($"peer{c} has {rep.Dynamic} spawned creatures, host {Net.DynamicCount}");
            if (System.Math.Abs(rep.Plots - _d.State.Farm.Plots.Count()) > 1) fails.Add($"peer{c} sees {rep.Plots} plots, host {_d.State.Farm.Plots.Count()}");
            if (actor == null || actor.Bag.Count(Bot.CoopCraft) < 1) fails.Add($"peer{c} crafted no {Bot.CoopCraft} (host'taki canta)");
        }
        int hostVoice = Net.Voice?.Received.Values.Sum() ?? 0;
        if (hostVoice < VoiceFrames * 4 / 5) fails.Add($"host heard {hostVoice}/{VoiceFrames} voice frames");
        if (Net.Revives < 1 || _d.Player.Down) fails.Add($"slap revive failed (revives={Net.Revives} down={_d.Player.Down})");
        int plots = _d.State.Farm.Plots.Count(), tilled = Tilled();
        if (tilled < _plots0 + Clients + 1) fails.Add($"host has {tilled} tilled plots, want {_plots0} + {Clients + 1}");
        if (_d.State.Backpack.Count(Bot.CoopCraft) < 1) fails.Add($"host crafted no {Bot.CoopCraft}");
        if (_ataProbe != 1) fails.Add("ata probe: dovus baslamadi");
        else if (Net.Actors.Values.FirstOrDefault() is { } pa && pa.Bag.Count("copper_ingot") > 0 && pa.Bag.Count("yun") > 0) fails.Add("ata probe: adak istemci cantasindan dusmedi");
        int pieces = _d.Build?.System.Pieces.Count ?? 0;
        int bases = _d.Build?.System.Pieces.Count(p => p.Def.Id == "kare_temel") ?? 0;
        int ruinedBases = _d.State.Rivalry.Ruins.Count(r => r.Shape == "kare_temel"); // cekisme probu A takiminin temellerini yikti (enkaz plani kaldi)
        if (!_glassProbe.StartsWith("ok")) fails.Add("glass probe: " + _glassProbe);
        if (!_rivProbe.StartsWith("ok")) fails.Add("rivalry probe: " + _rivProbe);
        if (bases + ruinedBases < Clients + 1) fails.Add($"host has {bases} foundations (+{ruinedBases} ruins), want {Clients + 1}");
        bool ok = fails.Count == 0 && maxDiff < 0.01;
        int rejected = Net.RequestsRejected, corr = Net.Corrections;
        _resultLine = ($"COOP RESULT {(ok ? "ok" : "fail")} players={Clients + 1} max_diff_mm={maxDiff * 1000:0.000} pieces={pieces} foundations={bases} late_join=1 plots={plots} tilled={tilled} crafted={Net.Actors.Values.Count(x => x.Bag.Count(Bot.CoopCraft) > 0) + (_d.State.Backpack.Count(Bot.CoopCraft) > 0 ? 1 : 0)} revives={Net.Revives} voice_frames={hostVoice} creatures_spawned={Net.DynamicCount} rejected={rejected} corrections={corr} glass={_glassProbe} rivalry={_rivProbe} ruins={_d.State.Rivalry.Ruins.Count} t={_t:0.0}" + (fails.Count > 0 ? " :: " + string.Join("; ", fails.Take(8)) : ""));
        if (ok) StartHandover(); // sekiz (M5): sonuc satiri devirden sonra yazilir
        else { Log(_resultLine); Finish(1); }
    }

    // ---------- dunya devri (sekiz, M5) ----------

    const float HandoverWait = 14f;
    bool _handover, _handLogged;
    float _handT, _handClientT = -1f;
    string _resultLine = "";

    /// <summary>Host: sinama bitince dunyayi bir istemciye devreder, kendisi yeni host'a istemci olarak baglanir.</summary>
    void StartHandover()
    {
        _handover = true;
        var w = NetPackets.Begin(NetMsg.Final); w.U8(4);
        Net.Broadcast(w.ToArray());
        if (!Net.Handover()) { Log($"{_resultLine.Replace("RESULT ok", "RESULT fail")} :: devir baslamadi: {Net.Status}"); Finish(1); return; }
        Log("COOP handover basladi");
    }

    /// <summary>Eski host: devirden sonra yeni host'a istemci olarak katilmis olmali; tam bir istemci yeni host, oteki ikisi istemci.</summary>
    void TickHandover(float dt)
    {
        _handT += dt;
        if (_handT < HandoverWait) return;
        _handover = false;
        var fails = new List<string>();
        int want = Clients + 1;
        if (!Net.IsClient || !Net.Joined) fails.Add($"eski host istemci degil (host={Net.IsHost} joined={Net.Joined} {Net.Status})");
        if (Net.Roster.Count != want) fails.Add($"eski host listesi {Net.Roster.Count}/{want}");
        if (Net.Avatars.Count != Clients) fails.Add($"eski host {Net.Avatars.Count}/{Clients} oyuncu goruyor");
        int hosts = 0, clients = 0;
        string runDir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../.tools/run/test";
        for (int k = 1; k <= Clients; k++)
        {
            string path = $"{runDir}/coop_client_{k}.log";
            string line = FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path).Split('\n').LastOrDefault(l => l.Contains("COOP HANDOVER state=")) : null;
            if (line == null) { fails.Add($"bot {k} devir durumu yazmadi"); continue; }
            if (line.Contains("state=host") && line.Contains($"roster={want}")) hosts++;
            else if (line.Contains("state=client") && line.Contains("joined=True") && line.Contains($"roster={want}")) clients++;
            else fails.Add($"bot {k}: {line.Trim()}");
        }
        if (hosts != 1 || clients != Clients - 1) fails.Add($"yeni host {hosts} (1 olmali), istemci {clients} ({Clients - 1} olmali)");
        bool ok = fails.Count == 0;
        Log($"COOP HANDOVER {(ok ? "ok" : "fail")} t={_handT:0.0}" + (ok ? "" : " :: " + string.Join("; ", fails.Take(6))));
        Log(ok ? _resultLine : _resultLine.Replace("RESULT ok", "RESULT fail") + " :: devir: " + string.Join("; ", fails.Take(4)));
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
