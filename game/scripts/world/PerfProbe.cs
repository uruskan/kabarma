using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// M1 performans olcumu (TEKNIK §10): "yuksek" on ayar, 1920 x 1080, dikey esitleme kapali, kare siniri yok.
/// Pencereli calisir. Kare suresi gercek saatle (Time.GetTicksUsec), cizim cagrisi RenderingServer'dan.
///  --test=perf --perf=noon               bot yok; her yer iminde ogle isiginda isinma + 60 sn.
///  --test=perf --perf=night --perf_bookmark=ID
///                                        bot ilk gunu hizli oynar; ilk gecenin son dalgasi baslayinca zaman 1x'e doner,
///                                        kamera yer imine gecer ve dalga boyunca (en fazla nightSeconds) olculur.
/// Cikti: progress/m1/dunya/perf/perf_&lt;mod&gt;[_&lt;yer imi&gt;].json ve "PERF ..." satirlari; "PERF RESULT ok|over|fail".
/// Butce (11,1 ms) asilsa da cikis kodu 0'dir: karar sahibin (RTX 2070S hedefi sahibin onayini bekler).
/// </summary>
public partial class PerfProbe : Node
{
    Director _d;
    PerfDef _p;
    string _mode;
    string _only;
    readonly List<BookmarkDef> _todo = new();
    BookmarkDef _cur;
    Camera3D _cam;
    double _phaseT;
    ulong _lastUsec;
    bool _measuring, _finished, _nightArmed;
    float _fastBefore = 4f;
    readonly List<double> _ms = new();
    readonly List<long> _draws = new(), _prims = new();
    readonly List<Dictionary<string, object>> _results = new();

    public override void _Ready()
    {
        _mode = Main.Args.TryGetValue("perf", out var m) ? m : "noon";
        _only = Main.Args.TryGetValue("perf_bookmark", out var b) ? b : null;
    }

    public override void _Process(double delta)
    {
        if (_finished) return;
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d == null) return;
        _p ??= _d.Data.Graphics.Perf;
        ulong now = Time.GetTicksUsec();
        double realDt = _lastUsec == 0 ? 0 : (now - _lastUsec) / 1e6;
        _lastUsec = now;
        _phaseT += realDt;

        if (_measuring)
        {
            _ms.Add(realDt * 1000.0);
            _draws.Add((long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame));
            _prims.Add((long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame));
        }

        if (_mode == "noon") Noon();
        else Night();
    }

    void Noon()
    {
        if (_cur == null && _todo.Count == 0 && _results.Count == 0 && _phaseT > 2.0)
        {
            _todo.AddRange(_d.World.Biomes.World.Bookmarks.Where(b => _only == null || b.Id == _only));
            _d.World.ForceTime("ogle");
            Next();
            return;
        }
        if (_cur == null) return;
        if (!_measuring && _phaseT >= _p.WarmupSeconds) { _measuring = true; _phaseT = 0; }
        else if (_measuring && _phaseT >= _p.NoonSeconds) { Record("ogle"); Next(); }
    }

    void Night()
    {
        var n = _d.Night;
        if (!_nightArmed)
        {
            if (n != null && n.Phase == NightPhase.Waves && n.WaveIndex == _d.Data.Kabarma.FirstNight.Waves.Count - 1)
            {
                _nightArmed = true;
                _fastBefore = _d.SpeedScale;
                _d.SpeedScale = 1f;
                _cur = _d.World.Bookmark(_only ?? "us");
                _cam = _d.World.ShowBookmark(_cur, _cam);
                _phaseT = 0;
                GD.Print($"PERF night last wave {n.WaveIndex + 1} bookmark={_cur.Id}");
            }
            return;
        }
        if (!_measuring && _phaseT >= 1.0) { _measuring = true; _phaseT = 0; }
        bool waveOver = n == null || n.Phase == NightPhase.Dawn;
        if (_measuring && (waveOver || _phaseT >= _p.NightSeconds))
        {
            Record("kabarma_gecesi_son_dalga" + (waveOver ? "" : "_kesik"));
            Finish();
        }
    }

    void Next()
    {
        _measuring = false;
        _phaseT = 0;
        if (_todo.Count == 0) { _cur = null; Finish(); return; }
        _cur = _todo[0];
        _todo.RemoveAt(0);
        _cam = _d.World.ShowBookmark(_cur, _cam);
    }

    void Record(string what)
    {
        _measuring = false;
        if (_ms.Count == 0) return;
        var s = _ms.OrderBy(x => x).ToList();
        double mean = _ms.Average();
        var r = new Dictionary<string, object>
        {
            ["bookmark"] = _cur.Id,
            ["light"] = what,
            ["frames"] = _ms.Count,
            ["seconds"] = System.Math.Round(_ms.Sum() / 1000.0, 1),
            ["mean_ms"] = System.Math.Round(mean, 3),
            ["fps"] = System.Math.Round(1000.0 / mean, 1),
            ["p95_ms"] = System.Math.Round(s[(int)(s.Count * 0.95)], 3),
            ["p99_ms"] = System.Math.Round(s[(int)(s.Count * 0.99)], 3),
            ["max_ms"] = System.Math.Round(s[^1], 3),
            ["draw_calls_mean"] = (long)_draws.Average(),
            ["draw_calls_max"] = _draws.Max(),
            ["primitives_mean"] = (long)_prims.Average(),
            ["budget_ms"] = _p.BudgetMs,
            ["within_budget"] = mean <= _p.BudgetMs,
        };
        _results.Add(r);
        GD.Print($"PERF {_cur.Id} {what} mean={mean:0.00}ms fps={1000.0 / mean:0.0} p99={r["p99_ms"]}ms draws={r["draw_calls_mean"]} prims={r["primitives_mean"]} frames={_ms.Count}");
        _ms.Clear(); _draws.Clear(); _prims.Clear();
    }

    void Finish()
    {
        _finished = true;
        var vp = GetViewport();
        int siblings = System.Diagnostics.Process.GetProcesses().Count(p => p.ProcessName.StartsWith("Godot", System.StringComparison.OrdinalIgnoreCase)) - 1;
        var doc = new Dictionary<string, object>
        {
            ["mode"] = _mode,
            ["preset"] = _d.World.Preset.Name,
            ["resolution"] = $"{vp.GetVisibleRect().Size.X}x{vp.GetVisibleRect().Size.Y}",
            ["window"] = DisplayServer.WindowGetSize().ToString(),
            ["vsync"] = DisplayServer.WindowGetVsyncMode().ToString(),
            ["max_fps"] = Engine.MaxFps,
            ["msaa"] = vp.Msaa3D.ToString(),
            ["gpu"] = RenderingServer.GetVideoAdapterName(),
            ["driver"] = RenderingServer.GetVideoAdapterApiVersion(),
            ["other_godot_processes"] = siblings,
            ["video_mem_mb"] = (long)(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed) / (1024 * 1024)),
            ["static_mem_mb"] = (long)(OS.GetStaticMemoryUsage() / (1024 * 1024)),
            ["date"] = Time.GetDatetimeStringFromSystem(),
            ["results"] = _results,
        };
        var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m1/dunya/perf";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var path = $"{dir}/perf_{_mode}{(_only != null ? "_" + _only : "")}.json";
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
        bool any = _results.Count > 0;
        bool within = any && _results.All(r => (bool)r["within_budget"]);
        GD.Print($"PERF file {path} gpu={doc["gpu"]} res={doc["resolution"]} vsync={doc["vsync"]} siblings={siblings}");
        GD.Print($"PERF RESULT {(!any ? "fail no samples" : within ? "ok" : "over")} budget={_p.BudgetMs}ms");
        GetTree().Quit(any ? 0 : 1);
    }
}
