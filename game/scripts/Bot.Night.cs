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
        if (!_d.Capa.InHand && !_d.Hud.MenuOpen) { HoldItem(PlanTools.Capa); return; } // karar 23: Capa elde degilse ates yok
        var n = _d.Night;
        // --bot_card_delay=S: goruntu almak icin kart seridi S oyun saniyesi acik kalir (varsayilan 0,6; otomatik secim 20)
        float delay = Main.Args.TryGetValue("bot_card_delay", out var cd) ? float.Parse(cd, System.Globalization.CultureInfo.InvariantCulture) : 0.6f;
        if (n?.Offer != null && n.Offer.Age > delay) Card(0);
        var z = Zipzip.All.Where(e => !e.Dead).OrderBy(e => e.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        var home = _d.FarmCenter() + new Vector3(-2.5f, 0, -2.5f);
        var oz = NearestOz(); // yerdeki Oz (M1 karari 19): bot da yuruyerek toplar
        if (z != null)
        {
            Aim(z.Center);
            float dist = z.GlobalPosition.DistanceTo(P.GlobalPosition);
            if (dist > 9f) MoveTo(z.GlobalPosition, 7f);
            else if (dist < 3f) P.MoveOverride = (P.GlobalPosition - z.GlobalPosition).Normalized() with { Y = 0 };
            else if (oz is Vector3 near && near.DistanceTo(P.GlobalPosition) < 8f) MoveTo(near, 0.5f, false); // ates ederken yakindaki Oz'e kay
            else P.MoveOverride = Vector3.Zero;
            Fire = dist < 11f;
        }
        else if (oz is Vector3 o) { Aim(o + Vector3.Up); MoveTo(o, 0.5f, false); }
        else { Aim(_d.FarmCenter() + Vector3.Up); MoveTo(home, 1.5f, false); }
    }

    Vector3? NearestOz()
    {
        var n = _d.Night;
        if (n == null || n.Motes.Count == 0) return null;
        var me = P.GlobalPosition;
        var m = n.Motes.OrderBy(o => (o.X - me.X) * (o.X - me.X) + (o.Z - me.Z) * (o.Z - me.Z)).First();
        return _d.World.Ground(new Vector3(m.X, 0, m.Z));
    }
}
