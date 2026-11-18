using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Capa, Toprak basligi: tapu icinde yalniz tarla surme (yukseltme, alcaltma, duzleme, yol M2) ve surulmus kareye ekme.
/// Egim siniri GDD 8.1 (crops.json maxSlopeDegrees, kural Farm.Till).
/// </summary>
public partial class Capa
{
    // ---------- Toprak ----------

    public bool TillOrPlant(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.TillRange) return false;
        if (_d.Ocak == null) { _d.Hud.Hint("Önce Ocak'ı kur: tapu dışında tarla açılmaz"); return false; }
        if (aim.DistanceTo(_d.OcakPos) > Ocak.ClaimRadius) { _d.Hud.Hint("Tapunun dışı"); return false; }
        if (aim.DistanceTo(_d.OcakPos) < _t.TillClearOcak || aim.DistanceTo(_d.Pod.GlobalPosition) < _t.TillClearPod) return false;
        _d.Net?.ForwardTill(aim); // ortak: istemcide host'a da gider (yerel sonuc tahmin, tarla host'tan doner)
        var (x, y) = FarmView.Cell(aim);
        var farm = _d.State.Farm;
        var plot = farm.Get(x, y);
        var center = _d.FarmView.CellCenter(x, y);
        if (plot == null)
        {
            float slope = SlopeDegrees(x + 0.5f, y + 0.5f);
            if (!farm.Till(x, y, slope))
            {
                if (!farm.CanTill(slope)) _d.Hud.Hint($"Çok eğimli: {slope:0}° (tarla {_d.Data.Crops.MaxSlopeDegrees:0}°'den az eğim ister)", 1.2f);
                return false;
            }
            FeelFire("toprak", _d.Player.Muzzle.GlobalPosition, Palette.Soil);
            FeelHit("toprak", center + Vector3.Up * 0.1f, Palette.Soil.Lerp(Palette.Ground, 0.3f), squash: _d.FarmView.View(x, y));
            _d.State.Scenario.SetProgress("till", farm.Plots.Count());
            return true;
        }
        if (plot.Crop == null)
        {
            string seed = _d.FarmView.PlantableSeed(); // tarim: secili tohum (T / DPad asagi), yoksa elde olan ilk tohum
            if (seed == null || !farm.Plant(x, y, seed, _d.State.Backpack, _d.State.Rng)) { _d.Hud.Hint("Tohum kalmadı"); return false; }
            FeelHit("toprak_ekim", center + Vector3.Up * 0.2f, Palette.Plant, squash: _d.FarmView.View(x, y));
            _d.State.Scenario.SetProgress("plant", farm.Plots.Count(q => q.Crop != null));
            return true;
        }
        return false;
    }

    /// <summary>Karenin egimi (derece): merkezin iki yanindaki zemin yuksekliginden (1 m'lik kare, GDD 8.1).</summary>
    float SlopeDegrees(float cx, float cz)
    {
        var w = _d.World;
        float dx = w.HeightAt(cx + 0.5f, cz) - w.HeightAt(cx - 0.5f, cz);
        float dz = w.HeightAt(cx, cz + 0.5f) - w.HeightAt(cx, cz - 0.5f);
        return Mathf.RadToDeg(Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)));
    }
}
