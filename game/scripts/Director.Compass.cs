using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Director: pusulanin etkin hedefi (SANAT-SES 8.1). Senaryo adimi -> hedef turu hud.json'da (stepTargets);
/// tur burada dunyadaki bir konuma cozulur. Adim listede yoksa ya da hedef henuz yoksa yalnizca Ocak isaretlenir.
/// Karar 23: Uretim panelinde eksik malzemenin "Pusulada goster"i (<see cref="PinSource"/>) adimin hedefinin onune gecer;
/// o esya cantaya girince kalkar.
/// </summary>
public partial class Director
{
    string _pinItem, _pinKind;

    /// <summary>Pusulada isaretli kaynagin esyasi (yoksa null).</summary>
    public string PinnedSource => _pinItem;

    /// <summary>Eksik malzemenin en yakin kaynagini pusulaya isaretler (craft.json sources). Kaynak bulunamazsa false.</summary>
    public bool PinSource(string item)
    {
        var src = Kabarma.Sim.CraftRules.Source(Data.Tool, item);
        if (src == null || ResolveTarget(src.Target) == null) { Hud.Hint($"{ItemNames.Of(item)}: yakında kaynak yok", 1.4f); return false; }
        if (_pinItem == null) State.Backpack.Changed += OnPinnedGathered;
        _pinItem = item;
        _pinKind = src.Target;
        Hud.Hint($"Pusula: {ItemNames.Of(item)} ({src.Text})", 2f);
        Sfx.Play("place", null, -10f);
        GD.Print($"PIN {item} -> {src.Target}");
        return true;
    }

    void OnPinnedGathered(string id, int count)
    {
        if (id != _pinItem || count <= 0) return;
        State.Backpack.Changed -= OnPinnedGathered;
        _pinItem = null; _pinKind = null;
    }

    public Vector3? CompassTarget(out string name)
    {
        name = "";
        if (_pinKind != null && ResolveTarget(_pinKind) is Vector3 pin) { name = ItemNames.Of(_pinItem); return pin; }
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
        // Kaynak turleri (craft.json sources): rock:<esya>, tuft:<esya>, tree, station:<istasyon>
        int colon = kind.IndexOf(':');
        if (colon > 0)
        {
            string what = kind[..colon], arg = kind[(colon + 1)..];
            switch (what)
            {
                case "rock":
                    return World.Rocks.Where(r => IsInstanceValid(r) && r.IsInsideTree() && r.Item == arg && r.Stones > 0).OrderBy(r => r.GlobalPosition.DistanceSquaredTo(me)).FirstOrDefault()?.GlobalPosition;
                case "tuft":
                    var t = World.Tufts.Where(x => x.Item == arg && x.Left > 0).OrderBy(x => x.Position.DistanceSquaredTo(me)).FirstOrDefault();
                    return t?.Position;
                case "station":
                    return Stations.Where(s => IsInstanceValid(s) && s.Def.Station == arg).OrderBy(s => s.GlobalPosition.DistanceSquaredTo(me)).FirstOrDefault()?.GlobalPosition;
            }
            return null;
        }
        switch (kind)
        {
            case "tree":
                return World.TreePositions.Count == 0 ? null : World.TreePositions.OrderBy(p => p.DistanceSquaredTo(me)).First();
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
