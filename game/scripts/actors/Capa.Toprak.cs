using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Capa, Toprak basligi: tapu icinde tarla surme ve ekme.</summary>
public partial class Capa
{
    // ---------- Toprak ----------

    public bool TillOrPlant(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.TillRange) return false;
        if (_d.Ocak == null) { _d.Hud.Hint("Önce Ocak'ı kur: tapu dışında tarla açılmaz"); return false; }
        if (aim.DistanceTo(_d.OcakPos) > Ocak.ClaimRadius) { _d.Hud.Hint("Tapunun dışı"); return false; }
        if (aim.DistanceTo(_d.OcakPos) < 1.6f || aim.DistanceTo(_d.Pod.GlobalPosition) < 2.2f) return false;
        var (x, y) = FarmView.Cell(aim);
        var farm = _d.State.Farm;
        var plot = farm.Get(x, y);
        if (plot == null)
        {
            if (!farm.Till(x, y)) return false;
            Sfx.Play("till", _d.FarmView.CellCenter(x, y), -2f);
            _d.State.Scenario.SetProgress("till", farm.Plots.Count());
            return true;
        }
        if (plot.Crop == null)
        {
            if (!farm.Plant(x, y, "turnip", _d.State.Backpack)) { _d.Hud.Hint("Tohum kalmadı"); return false; }
            Sfx.Play("place", _d.FarmView.CellCenter(x, y), -10f);
            _d.State.Scenario.SetProgress("plant", farm.Plots.Count(q => q.Crop != null));
            return true;
        }
        return false;
    }
}
