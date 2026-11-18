using System.Linq;

namespace Kabarma.Game;

/// <summary>Istasyon (zincir M2): cok isli kuyrugun etiketi (siradaki isler, bekleme nedeni).</summary>
public partial class Station
{
    /// <summary>Etiketin ikinci satirina eklenen: siradaki isler ve beklemenin nedeni.</summary>
    string QueueTail()
    {
        var jobs = Queue.Jobs;
        string next = jobs.Count > 1 ? "\nsonra: " + string.Join(", ", jobs.Skip(1).Take(3).Select(j => $"{j.Recipe.Name} ×{j.Left}")) + (jobs.Count > 4 ? " …" : "") : "";
        string why = Queue.Blocked ? "\nbekliyor: çanta ve Sandıklar dolu" : Queue.PowerOk != null && !Queue.PowerOk() ? $"\nbekliyor: güç yok ({Queue.Watts} W)" : "";
        return next + why;
    }
}
