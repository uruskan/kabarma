using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Capa, Kazma basligi: isinla kaya kemirme (tas, kil, bakir: Rock.Item), parca cantaya ucar.</summary>
public partial class Capa
{
    // ---------- Kazma ----------

    float _mineDebrisT;

    Rock NearestRockTo(Vector3 point, float within) =>
        _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Center.DistanceTo(point) < r.Radius + within)
            .OrderBy(r => r.Center.DistanceTo(point)).FirstOrDefault();

    /// <summary>Kazma isini kayayi kemirir; kopan her parca hedefi sarsar ve cantaya ucar (bot da bunu cagirir).</summary>
    public void MineRock(Rock rock, float dt, Vector3 hitPoint)
    {
        int got = rock.Mine(_t.MineRate * dt);
        var src = F.Source("kazma");
        _mineDebrisT -= dt;
        if (_mineDebrisT <= 0f) { _mineDebrisT = src.DebrisInterval; FeelDebris("kazma", hitPoint, Palette.Rock); }
        string item = rock.Item;
        for (int i = 0; i < got; i++)
        {
            // Kopan parca: kayada ezilip esneme, kucuk sarsinti ve geri tepme (duraklama yok: surekli is).
            FeelHit("kazma", hitPoint, Palette.Rock, squash: rock);
            Fx.FlyTo(hitPoint, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Rock, 0.14f, () =>
            {
                _d.State.Backpack.Add(item, 1);
                FeelHit("toplama", _d.Player.BackpackPoint.GlobalPosition, Palette.Rock);
                Events.RaiseGathered(item, 1);
            });
        }
        if (rock.Stones <= 0) _d.World.RemoveRock(rock);
    }
}
