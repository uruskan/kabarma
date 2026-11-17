using System;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Capa, Bicme basligi: olgun ekin hasadi ve agactan odun (GDD 8, 8.2).</summary>
public partial class Capa
{
    // ---------- Bicme ----------

    /// <summary>Bicme: nisan noktasi cevresindeki olgun ekinleri biçer; urun ve tohum cantaya ucar (GDD 8.2).</summary>
    public int Mow(Vector3 aim)
    {
        if (_d.Player.Muzzle.GlobalPosition.DistanceTo(aim) > _t.MowRange) return 0;
        int n = 0;
        foreach (var plot in _d.State.Farm.Plots.ToList())
        {
            var c = _d.FarmView.CellCenter(plot.X, plot.Y);
            if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > _t.MowRadius) continue;
            var h = _d.State.Farm.Harvest(plot.X, plot.Y, _d.State.Rng);
            if (h == null) continue;
            n++;
            var (yield, stars, seeds) = h.Value;
            Fx.Burst(c + Vector3.Up * 0.4f, Palette.Plant, 18, 4f, 0.08f, 0.5f);
            Fx.Text3D(c + Vector3.Up * 1.0f, $"+{yield} Şalgam {new string('★', stars)}", Palette.Warning, 48, 1.2f, 1.3f);
            for (int i = 0; i < Math.Min(yield, 4); i++)
                Fx.FlyTo(c + Vector3.Up * 0.3f, () => _d.Player.BackpackPoint.GlobalPosition, Palette.TurnipBody, 0.12f);
            _d.State.Backpack.Add("turnip", yield);
            _d.State.Backpack.Add("turnip_seed", seeds);
            Sfx.Play("till", c, 0f);
        }
        if (n > 0) _d.OnHarvest(n);
        // Agac biçilirse odun verir (GDD 8: Biçme agac, cali ve ekin biçer). Agac yerinde kalir.
        if (n == 0)
        {
            var tree = _d.World.TreePositions.Where(t => new Vector2(t.X - aim.X, t.Z - aim.Z).Length() < 1.8f).OrderBy(t => t.DistanceTo(aim)).Cast<Vector3?>().FirstOrDefault();
            if (tree is Vector3 tp)
            {
                int w = _d.Data.Tool.WoodPerChop;
                _d.State.Backpack.Add("wood", w);
                Fx.Burst(aim, Palette.Plant, 14, 4f, 0.07f, 0.6f);
                Fx.Burst(aim, Palette.Wood, 6, 3f, 0.06f, 0.4f);
                Fx.FlyTo(aim, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Wood, 0.12f);
                Sfx.Play("hit", aim, -4f);
                Events.RaiseGathered("wood", w);
            }
            // Ot, cali ve yabani bugday (YER TUTUCU tutamlar, World.Placeholders.cs): Lif ya da Bugday, geri gelmez.
            else if (_d.World.NearestTuft(aim, _t.MowRadius) is World.Tuft tuft)
            {
                int got = _d.World.TakeTuft(tuft, _d.Data.Tool.WoodPerChop);
                _d.State.Backpack.Add(tuft.Item, got);
                Fx.Burst(aim, tuft.Item == "wheat" ? Palette.Warning : Palette.Plant, 14, 4f, 0.07f, 0.6f);
                Fx.FlyTo(aim, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Plant, 0.12f);
                Sfx.Play("till", aim, -4f);
                Events.RaiseGathered(tuft.Item, got);
            }
        }
        return n;
    }
}
