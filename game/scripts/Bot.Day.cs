using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>Bot, gunduz adimlari: kaz, Ocak, sur, ek, sula, buyut (Gidak'i besle).</summary>
public partial class Bot
{
    bool _fed;

    void RegisterDay()
    {
        _steps["mine"] = StepMine;
        _steps["ocak"] = StepOcak;
        _steps["till"] = StepTillPlant;
        _steps["plant"] = StepTillPlant;
        _steps["water"] = StepWater;
        _steps["grow"] = StepGrow;
    }

    void StepMine()
    {
        _d.Capa.Select(Capa.Head.Kazma);
        var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Item == "stone").OrderBy(r => r.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (rock == null) return;
        Aim(rock.Center);
        if (MoveTo(rock.GlobalPosition, 4.5f)) Fire = true;
    }

    void StepOcak()
    {
        _d.Capa.Select(Capa.Head.Insa);
        var spot = _d.World.Ground(new Vector3(0, 0, 0));
        Aim(spot);
        if (MoveTo(spot + new Vector3(3, 0, -3), 0.8f) && _stepT > 1.5f) { FirePressed = true; _stepT = 0; }
    }

    void StepTillPlant()
    {
        _d.Capa.Select(Capa.Head.Toprak);
        if (_cells.Count == 0)
        {
            var o = _d.OcakPos;
            for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++) _cells.Add(_d.World.Ground(new Vector3(Mathf.Floor(o.X) + 3 + x + 0.5f, 0, Mathf.Floor(o.Z) - 1 + z + 0.5f)));
        }
        var c = _cells[_cell % 9];
        Aim(c);
        if (MoveTo(c + new Vector3(0, 0, -3.5f), 1.0f, false))
        {
            Fire = true;
            if (_stepT > 0.2f) { _stepT = 0; _cell++; }
        }
    }

    void StepWater()
    {
        _d.Capa.Select(Capa.Head.Sulama);
        var c = _cells[_cell % 9];
        Aim(c);
        if (MoveTo(c + new Vector3(0, 0, -4f), 1.0f, false))
        {
            Fire = true;
            if (_stepT > 0.35f) { _stepT = 0; _cell++; }
        }
    }

    void StepGrow()
    {
        if (!_fed && _d.Gidak.Visible)
        {
            Aim(_d.Gidak.GlobalPosition + Vector3.Up * 0.5f);
            MoveTo(_d.Gidak.GlobalPosition, 2.0f);
            if (_d.Gidak.GlobalPosition.DistanceTo(P.GlobalPosition) < 2.8f) { _d.Interact(); _fed = true; }
        }
        else { Aim(_d.FarmCenter() + Vector3.Up * 0.5f); MoveTo(_d.FarmCenter() + new Vector3(-6, 0, -2), 1f, false); }
    }
}
