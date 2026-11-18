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
        Head(Capa.Head.Kazma);
        var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Item == "stone").OrderBy(r => r.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (rock == null) return;
        Aim(rock.Center);
        if (MoveTo(rock.GlobalPosition, 4.5f)) Fire = true;
    }

    /// <summary>Ocak kapsulden cantada gelir (karar 23): kemerden ele al, hayaletle kur.</summary>
    void StepOcak()
    {
        if (!HoldItem("ocak")) { _stepT = 0; return; }
        var spot = _d.World.Ground(new Vector3(0, 0, 0));
        Aim(spot);
        if (MoveTo(spot + new Vector3(3, 0, -3), 0.8f) && _stepT > 1.5f) { FirePressed = true; _stepT = 0; }
    }

    void StepTillPlant()
    {
        Head(Capa.Head.Toprak);
        if (_cells.Count == 0)
        {
            var o = _d.OcakPos;
            for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++) _cells.Add(_d.World.Ground(new Vector3(Mathf.Floor(o.X) + 3 + x + 0.5f, 0, Mathf.Floor(o.Z) - 1 + z + 0.5f)));
        }
        var c = _cells[_cell % 9];
        Aim(c);
        if (_cellIdx != _cell) { _cellIdx = _cell; _cellT = 0; }
        if (MoveTo(c + new Vector3(0, 0, -3.5f), 1.0f, false))
        {
            Fire = true;
            // Kare islenince (surulunce / ekilince) sonrakine gecilir; islenmezse karede 3 sn ates sonra (egim, tohum yok).
            // Eskiden sabit 0,2 sn'lik pencere vardi: nisanin aninda oturdugu dogrudan kipte yetiyordu, kolla nisan alan
            // oyuncuya (--test=input --device=pad) yetmiyordu.
            _cellT += (float)GetProcessDeltaTime();
            var plot = _d.State.Farm.Get(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Z));
            // Entegrasyon: ilkgun'un tek "tarla" adimi (sur + ek) ekilmeyi bekler; eski "till" adimi yalniz surmeyi.
            bool worked = _lastStep is "plant" or "tarla" ? plot?.Planted == true : plot != null;
            if (worked || _cellT > 3f) { _stepT = 0; _cell++; }
        }
    }

    int _cellIdx = -1;
    float _cellT;

    void StepWater()
    {
        Head(Capa.Head.Sulama);
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
            if (_d.Gidak.GlobalPosition.DistanceTo(P.GlobalPosition) < 2.8f) { Use(); _fed = true; }
        }
        else { Aim(_d.FarmCenter() + Vector3.Up * 0.5f); MoveTo(_d.FarmCenter() + new Vector3(-6, 0, -2), 1f, false); }
    }
}
