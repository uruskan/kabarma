using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Bot, yapi adimlari: agactan odun kes, yapi setiyle temel ve duvarlari kur.</summary>
public partial class Bot
{
    int _built, _pendingCheck = -1;

    void RegisterBuild()
    {
        _steps["odun"] = Machine(StepOdun);
        _steps["yapi"] = Machine(StepYapi);
    }

    void StepOdun(Vector3 o)
    {
        _d.Capa.Select(Capa.Head.Bicme);
        var t = _d.World.TreePositions.OrderBy(x => x.DistanceTo(P.GlobalPosition)).First();
        Aim(t + Vector3.Up * 1.4f);
        if (MoveTo(t, 3.0f)) Fire = true;
    }

    void StepYapi(Vector3 o)
    {
        var b = _d.Build;
        if (b == null) { GD.Print("BOT no build kit"); return; }
        // Temel ussun yanina; duvarlar temelin uc kenarina: temelin ust yuzune, kenarin 0,45 m icine nisan.
        var f = o + new Vector3(7f, 0, 4f);
        string want = _built == 0 ? "kare_temel" : "duvar";
        var fp = b.System.Pieces.FirstOrDefault(x => x.Def.Id == "kare_temel");
        Vector3[] outs = { Vector3.Back * -1f, Vector3.Right, Vector3.Left };  // -Z, +X, -X kenarlari
        Vector3 target, stand;
        if (_built == 0 || fp == null) { target = _d.World.Ground(f); stand = _d.World.Ground(f) + new Vector3(0, 0, -6.5f); }
        else
        {
            var outw = outs[Mathf.Min(_built - 1, 2)];
            float top = fp.GlobalPosition.Y + fp.Def.BoundsMax.Y;
            var c = fp.GlobalPosition;
            target = new Vector3(c.X, top, c.Z) + outw * (1.5f - 0.45f);
            stand = _d.World.Ground(c + outw * 7f);
        }
        if (_d.State.Backpack.Count("wood") < b.CostOf(want))
        {
            _d.Capa.Select(Capa.Head.Bicme);
            var tr = _d.World.TreePositions.OrderBy(x => x.DistanceTo(P.GlobalPosition)).First();
            Aim(tr + Vector3.Up * 1.4f);
            if (MoveTo(tr, 3.0f)) Fire = true;
            return;
        }
        _d.Capa.Select(Capa.Head.Insa);
        if (_d.Capa.CurrentPlaceable != want) { if (_stepT > 0.12f) { _d.Capa.CyclePlaceable(1); _stepT = 0; } return; }
        Aim(target);
        if (MoveTo(stand, 0.6f, false) && _stepT > 1.2f)
        {
            int before = b.System.Pieces.Count;
            FirePressed = true; _stepT = 0;
            _pendingCheck = before;
        }
        if (_pendingCheck >= 0 && b.System.Pieces.Count > _pendingCheck) { _built++; _pendingCheck = -1; GD.Print($"BOT built {want} -> {_built}"); }
        else if (_pendingCheck >= 0 && _stepT > 1.0f) { GD.Print($"BOT place failed {want}: {b.System.Current?.Reason} valid={b.System.Current?.Valid}"); _pendingCheck = -1; }
    }
}
