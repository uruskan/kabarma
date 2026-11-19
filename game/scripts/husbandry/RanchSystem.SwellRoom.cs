using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kabartma Odasi'nin hayvan hedefi (gece4, GDD 14.10): yaricaptaki en yakin evcil hayvan, gunduz 2 dk kabarik savas govdesi
/// (Ranch.SwellForSeconds) alir ve gunduz zararlilariyla dovusur (Husbandry.Combat.cs WarProcess). Ilk gunun ogretici hayvani
/// (Passive) dahil degil.
/// </summary>
public partial class RanchSystem
{
    /// <summary>from noktasinin radius metre icindeki en yakin kabartilabilir evcil hayvan (savas govdesi yok, yorgun degil).</summary>
    public PenAnimal NearestSwellable(Vector3 from, float radius)
    {
        PenAnimal best = null; float bd = float.MaxValue;
        foreach (var ag in _agents.Where(IsInstanceValid))
        {
            var a = ag.Animal;
            if (ag.Passive || a == null || !a.Tame || a.War != null || a.Hidden || Ranch.Exhausted(a)) continue;
            float d = ag.Host.GlobalPosition.DistanceTo(from);
            if (d <= radius && d < bd) { best = a; bd = d; }
        }
        return best;
    }

    /// <summary>Hayvani kabartir (host). Donen: kabardi mi. Gorunum ShowWar ile.</summary>
    public bool SwellAnimal(PenAnimal a, float seconds)
    {
        if (!IsHost || a == null) return false;
        if (!Ranch.SwellForSeconds(a, id => _d.Data.Creature(id).Hp, seconds, _d.Pens.Pens)) return false;
        ShowWar(new WarEvent(WarEventKind.Swelled, a.Id));
        GD.Print($"RANCH swellroom {a.Species} id={a.Id} {seconds:0}s hp={a.War?.MaxHp:0}");
        return true;
    }
}