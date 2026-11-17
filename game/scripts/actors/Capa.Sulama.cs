using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Capa, Sulama basligi: nisan noktasi cevresindeki kareleri islatir.</summary>
public partial class Capa
{
    readonly HashSet<(int, int)> _wateredPlanted = new();

    // ---------- Sulama ----------

    public void WaterAt(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.WaterRange) return;
        var farm = _d.State.Farm;
        bool any = false;
        foreach (var plot in farm.Plots)
        {
            var c = _d.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > 1.1f) continue;
            if (plot.Moisture > 92f) continue;
            farm.Water(plot.X, plot.Y);
            any = true;
            if (plot.Planted && _wateredPlanted.Add((plot.X, plot.Y)))
                _d.State.Scenario.SetProgress("water", _wateredPlanted.Count);
        }
        if (any && GD.Randf() < 0.35f) Sfx.Play("water", aim, -8f);
    }
}
