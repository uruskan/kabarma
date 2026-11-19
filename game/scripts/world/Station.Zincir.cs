using System.Linq;

namespace Kabarma.Game;

/// <summary>Istasyon (zincir M2): cok isli kuyrugun etiketi (siradaki isler, bekleme nedeni).</summary>
public partial class Station
{
    /// <summary>Etiketin ikinci satirina eklenen: siradaki isler ve beklemenin nedeni.</summary>
    string QueueTail()
    {
        var jobs = Queue.Jobs;
        string next = jobs.Count > 1 ? "\n" + Loc.T("world.queue_next", string.Join(", ", jobs.Skip(1).Take(3).Select(j => $"{j.Recipe.Name} ×{j.Left}"))) + (jobs.Count > 4 ? " …" : "") : "";
        string why = Queue.Blocked ? "\n" + Loc.T("world.queue_blocked") : Queue.PowerOk != null && !Queue.PowerOk() ? "\n" + Loc.T("world.queue_no_power", Queue.Watts) : "";
        return next + why;
    }
}
