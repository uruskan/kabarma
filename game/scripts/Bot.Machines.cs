using System;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, kabarik urun satisi ve M1 makine zinciri: oyuncunun yaptigini yapar (hareket, nisan, tetik, E, menu tuslari).
/// Makine adimlari Machine(...) ile sarilir: rapor aciksa once kapatilir, menu zamanlayicisi isler, isleyici Ocak konumunu alir.
/// </summary>
public partial class Bot
{
    float _menuT;

    void RegisterMachines()
    {
        _steps["sell"] = StepSell;
        _steps["tezgah_kur"] = Machine(StepTezgahKur);
        _steps["civi"] = Machine(StepCivi);
        _steps["hasat"] = Machine(StepHasat);
        _steps["firin_kur"] = Machine(StepFirinKur);
        _steps["corba"] = Machine(StepCorba);
        _steps["paketle"] = Machine(StepPaketle);
        _steps["kasa_sat"] = Machine(StepKasaSat);
    }

    /// <summary>Makine zinciri adimi: Hasat Raporu aciksa once kapatir; degilse menu zamanlayicisini isletip adimi Ocak konumuyla cagirir.</summary>
    Action Machine(Action<Vector3> step) => () =>
    {
        if (_d.Hud.ReportOpen) { if (_stepT > 1.5f) { _d.CloseReport(); _stepT = 0; } P.MoveOverride = Vector3.Zero; return; }
        _menuT -= (float)GetProcessDeltaTime();
        var o = _d.OcakPos;
        step(o);
    };

    void StepSell()
    {
        _d.Capa.Select(Capa.Head.Cekim);
        var held = _d.Capa.Held;
        var pad = _d.Pod.PadCenter;
        if (held == null)
        {
            var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted).OrderBy(v => v.GlobalPosition.DistanceTo(pad)).FirstOrDefault();
            var loose = _d.GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>().FirstOrDefault(s => !s.Sold);
            Vector3? target = loose?.GlobalPosition ?? view?.CropTop;
            if (target == null) return;
            Aim(target.Value);
            if (MoveTo(target.Value, 4.5f, false) && _stepT > 0.6f) { FirePressed = true; _stepT = 0; }
        }
        else
        {
            var away = (P.GlobalPosition - pad); away.Y = 0;
            Aim(pad + Vector3.Up * 0.5f);
            if (MoveTo(pad + away.Normalized() * 3.2f, 0.8f, false) && _stepT > 0.8f) { FirePressed = true; _stepT = 0; }
        }
    }

    // ---------- M1 makine zinciri: oyuncunun yaptigini yapar (hareket, nisan, tetik, E, menu tuslari) ----------

    /// <summary>Yeterli tas yoksa en yakin kayayi kazar. Yeterliyse true.</summary>
    bool EnsureStone(int need)
    {
        if (_d.State.Backpack.Count("stone") >= need) return true;
        _d.Capa.Select(Capa.Head.Kazma);
        var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0).OrderBy(r => r.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (rock == null) { GD.Print("BOT no rocks left"); return true; }
        Aim(rock.Center);
        if (MoveTo(rock.GlobalPosition, 4.5f)) Fire = true;
        return false;
    }

    /// <summary>Insa basliginda istenen yerlestirilebiliri secip verilen noktaya koyar.</summary>
    void PlaceAt(string id, Vector3 at)
    {
        _d.Capa.Select(Capa.Head.Insa);
        if (_d.Capa.CurrentPlaceable != id) { if (_stepT > 0.15f) { _d.Capa.CyclePlaceable(1); _stepT = 0; } return; }
        var standAt = at + new Vector3(0, 0, -4.5f);
        Aim(_d.World.Ground(at));
        if (MoveTo(standAt, 0.6f, false) && _stepT > 1.0f) { FirePressed = true; _stepT = 0; }
    }

    Station StationOf(string id) => _d.Stations.FirstOrDefault(s => s.Def.Id == id);

    /// <summary>Istasyona yurur, E ile menuyu acar, ilk tarifi siraya ekler.</summary>
    void UseStation(Station st, int item)
    {
        Aim(st.GlobalPosition + Vector3.Up);
        if (!MoveTo(st.GlobalPosition + (P.GlobalPosition - st.GlobalPosition).Normalized() * 2.2f, 0.5f, false)) return;
        if (!_d.Hud.MenuOpen && st.Queue.Active == null && _menuT <= 0) { _d.Interact(); _menuT = 0.6f; }
        else if (_d.Hud.MenuOpen && _menuT <= 0) { _d.Hud.ActivateMenu(item); _d.Hud.HideMenu(); _menuT = 1.0f; }
    }

    void StepTezgahKur(Vector3 o)
    {
        if (EnsureStone(6)) PlaceAt("tezgah", o + new Vector3(-5f, 0, 1.5f));
    }

    void StepCivi(Vector3 o)
    {
        var st = StationOf("tezgah");
        if (st != null && EnsureStone(2)) UseStation(st, 0);
    }

    void StepHasat(Vector3 o)
    {
        _d.Capa.Select(Capa.Head.Bicme);
        var v = _d.FarmView.Views.Where(x => x.Plot.Planted && x.Plot.Stage == Kabarma.Sim.CropStage.Mature)
            .OrderBy(x => x.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (v == null) { GD.Print("BOT nothing to harvest"); return; }
        Aim(v.GlobalPosition + Vector3.Up * 0.3f);
        if (MoveTo(v.GlobalPosition, 3.2f, false)) Fire = true;
    }

    void StepFirinKur(Vector3 o)
    {
        if (EnsureStone(10)) PlaceAt("tas_firin", o + new Vector3(-5f, 0, -3.5f));
    }

    void StepCorba(Vector3 o)
    {
        var st = StationOf("tas_firin");
        if (st == null) return;
        if (_d.State.Backpack.Count("salgam_corbasi") > 0)
        {
            P.MoveOverride = Vector3.Zero;
            if (!_d.Hud.MenuOpen && _menuT <= 0) { _d.OpenBagMenu(); _menuT = 0.5f; }
            else if (_d.Hud.MenuOpen && _menuT <= 0) { _d.Hud.ActivateMenu(0); _d.Hud.HideMenu(); _menuT = 1f; }
        }
        else if (st.Queue.Active == null) UseStation(st, 0);
        else { P.MoveOverride = Vector3.Zero; Aim(st.GlobalPosition + Vector3.Up); }
    }

    void StepPaketle(Vector3 o)
    {
        var st = StationOf("paketleme");
        if (st == null) { if (EnsureStone(8)) PlaceAt("paketleme", o + new Vector3(-1f, 0, 5.5f)); return; }
        if (st.Queue.Active == null && _d.State.Backpack.Count("salgam_kasasi") == 0) UseStation(st, 0);
        else { P.MoveOverride = Vector3.Zero; Aim(st.GlobalPosition + Vector3.Up); }
    }

    void StepKasaSat(Vector3 o)
    {
        var pad = _d.Pod.PadCenter;
        Aim(pad + Vector3.Up);
        if (!MoveTo(pad + (P.GlobalPosition - pad).Normalized() * 3.2f, 0.6f, false)) return;
        if (!_d.Hud.MenuOpen && _menuT <= 0) { _d.Interact(); _menuT = 0.6f; }
        else if (_d.Hud.MenuOpen && _menuT <= 0)
        {
            int idx = _d.Data.Tool.Prices.Keys.ToList().IndexOf("salgam_kasasi");
            _d.Hud.ActivateMenu(idx); _d.Hud.HideMenu(); _menuT = 1f;
        }
    }
}
