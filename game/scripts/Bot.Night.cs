using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Bot, Kabarma gecesi: kart sec, en yakin kabarigi vur, tarlanin basinda dur.</summary>
public partial class Bot
{
    void RegisterNight()
    {
        _steps["night"] = StepNight;
    }

    void StepNight()
    {
        var n = _d.Night;
        if (n?.Offer != null && n.Offer.Age > 0.6f) Card(0);
        var z = Zipzip.All.Where(e => !e.Dead).OrderBy(e => e.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        var home = _d.FarmCenter() + new Vector3(-2.5f, 0, -2.5f);
        if (z != null)
        {
            Aim(z.Center);
            float dist = z.GlobalPosition.DistanceTo(P.GlobalPosition);
            if (dist > 9f) MoveTo(z.GlobalPosition, 7f);
            else if (dist < 3f) P.MoveOverride = (P.GlobalPosition - z.GlobalPosition).Normalized() with { Y = 0 };
            else P.MoveOverride = Vector3.Zero;
            Fire = dist < 11f;
        }
        else { Aim(_d.FarmCenter() + Vector3.Up); MoveTo(home, 1.5f, false); }
    }
}
