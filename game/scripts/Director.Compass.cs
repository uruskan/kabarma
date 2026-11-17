using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: pusulanin etkin hedefi (SANAT-SES 8.1). Senaryo adimi -> hedef turu hud.json'da (stepTargets);
/// tur burada dunyadaki bir konuma cozulur. Adim listede yoksa ya da hedef henuz yoksa yalnizca Ocak isaretlenir.
/// </summary>
public partial class Director
{
    public Vector3? CompassTarget(out string name)
    {
        name = "";
        string kind = null;
        var step = State.Scenario.Current;
        if (step != null) Data.Hud.StepTargets.TryGetValue(step.Id, out kind);
        else if (State.Orders.Tracked?.Current?.Measure == "swollen_crop") kind = "farm"; // siradaki is emri: kabarik urun
        if (kind == null) return null;
        var at = ResolveTarget(kind);
        if (at != null) name = Data.Hud.TargetNames.TryGetValue(kind, out var n) ? n : kind;
        return at;
    }

    Vector3? ResolveTarget(string kind)
    {
        var me = Player.GlobalPosition;
        switch (kind)
        {
            case "rock":
                var rock = World.Rocks.Where(r => IsInstanceValid(r) && r.IsInsideTree()).OrderBy(r => r.GlobalPosition.DistanceSquaredTo(me)).FirstOrDefault();
                return rock?.GlobalPosition;
            case "farm":
                return State.Farm.Plots.Any() ? FarmCenter() : Ocak?.GlobalPosition;
            case "ramp":
                return Pod.IsRamp ? Pod.PadCenter : Pod.GlobalPosition;
            case "pod":
                return Pod.GlobalPosition;
            case "ocak":
                return Ocak?.GlobalPosition;
            case "gidak":
                return Gidak.Visible ? Gidak.GlobalPosition : null;
            default:
                // Istasyon kimligi (tezgah, tas_firin, paketleme...)
                var st = Stations.FirstOrDefault(s => IsInstanceValid(s) && s.Def.Id == kind);
                return st?.GlobalPosition ?? Ocak?.GlobalPosition;
        }
    }
}
