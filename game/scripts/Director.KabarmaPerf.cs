using System.Diagnostics;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kabarma gecesi olcumu (TEKNIK 5.3 butcesi, KS 10 / KS 30 stres): 5 gercek saniyede bir "KABARMA perf" satiri.
/// frameMs gercek duvar saati (oyun hizindan bagimsiz); ai = Zipzip yapay zekasi, look = gorunus (sisme shader parametreleri),
/// process/physics = Godot'un kare isleme suresi, rcpu/rgpu = cizim (viewport olcumu), draws/objs = cizim cagrisi ve nesne.
/// </summary>
public partial class Director
{
    float _perfT; int _perfFrames;
    ulong _perfStartUs;
    double _perfProcSum, _perfPhysSum, _perfCpuSum, _perfGpuSum, _perfDrawSum, _perfObjSum, _perfWorst;
    ulong _perfLastUs;
    bool _perfMeasureOn;

    void PerfTick(KabarmaNight n, float dt)
    {
        ulong now = Time.GetTicksUsec();
        var vp = GetViewport().GetViewportRid();
        if (!_perfMeasureOn) { RenderingServer.ViewportSetMeasureRenderTime(vp, true); _perfMeasureOn = true; _perfStartUs = _perfLastUs = now; return; }
        _perfWorst = Mathf.Max(_perfWorst, (now - _perfLastUs) / 1000.0);
        _perfLastUs = now;
        _perfFrames++;
        _perfProcSum += Performance.GetMonitor(Performance.Monitor.TimeProcess);
        _perfPhysSum += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess);
        _perfCpuSum += RenderingServer.ViewportGetMeasuredRenderTimeCpu(vp);
        _perfGpuSum += RenderingServer.ViewportGetMeasuredRenderTimeGpu(vp);
        _perfDrawSum += Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        _perfObjSum += Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        double wall = (now - _perfStartUs) / 1000.0;
        if (wall < 5000) return;
        double f = _perfFrames, tick = Stopwatch.Frequency / 1000.0;
        double ai = (Zipzip.ProcessTicks - Zipzip.LookTicks) / tick / f, look = Zipzip.LookTicks / tick / f;
        GD.Print($"KABARMA perf wave={n.WaveIndex + 1} active={Kabarans.Live.Count()} swarms={_nightSwarms.Count} pending={n.PendingSpawns} " +
                 $"frameMs={wall / f:0.0} worstMs={_perfWorst:0.0} ai={ai:0.00} look={look:0.00} process={_perfProcSum * 1000 / f:0.0} physics={_perfPhysSum * 1000 / f:0.0} " +
                 $"rcpu={_perfCpuSum / f:0.0} rgpu={_perfGpuSum / f:0.0} draws={_perfDrawSum / f:0} objs={_perfObjSum / f:0}");
        Zipzip.ProcessTicks = 0; Zipzip.LookTicks = 0;
        _perfFrames = 0; _perfStartUs = now; _perfWorst = 0;
        _perfProcSum = _perfPhysSum = _perfCpuSum = _perfGpuSum = _perfDrawSum = _perfObjSum = 0;
    }
}
