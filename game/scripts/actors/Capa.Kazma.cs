using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Capa, Kazma basligi: isinla kaya kemirme, tas cantaya ucar.</summary>
public partial class Capa
{
    // ---------- Kazma ----------

    Rock NearestRockTo(Vector3 point, float within) =>
        _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Center.DistanceTo(point) < r.Radius + within)
            .OrderBy(r => r.Center.DistanceTo(point)).FirstOrDefault();

    /// <summary>Kazma isini kayayi kemirir; parcalar cantaya ucar (bot da bunu cagirir).</summary>
    public void MineRock(Rock rock, float dt, Vector3 hitPoint)
    {
        int got = rock.Mine(_t.MineRate * dt);
        if (Time.GetTicksMsec() % 6 == 0) Fx.Burst(hitPoint, Palette.Rock, 3, 3f, 0.06f, 0.3f);
        for (int i = 0; i < got; i++)
        {
            Fx.FlyTo(hitPoint, () => _d.Player.BackpackPoint.GlobalPosition, Palette.Rock, 0.14f, () =>
            {
                _d.State.Backpack.Add("stone", 1);
                Sfx.Play("pickup", null, -6f);
                Events.RaiseGathered("stone", 1);
            });
        }
        if (rock.Stones <= 0) _d.World.RemoveRock(rock);
    }
}
