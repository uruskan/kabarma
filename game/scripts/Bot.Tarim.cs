using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, tarim (M2) sinamasi (--test=tarim): ilk gun senaryosu yerine ikinci bir ekini bastan sona yetistirir.
/// Hile yok, butun dokunuslar oyuncunun yolundan (Bot.Hands.cs, Bot.Kemer.cs):
///   Ocak'i kur → Korkuluk uret ve tarlanin yanina kur (Odun ve Lif Bicme ile) → yabani Bugday'i Bicme ile biç (urun ve
///   yabani tohum, Tohum Kaydi'na soy) → Toprak ile 2 kare sur, tohumu sec (T), ek → Sulama → buyurken sula →
///   olgunu Bicme ile hasat et. Yalniz --test=tarim'da kayitlidir; ilk gun botu (--test=bot) degismez.
/// </summary>
public partial class Bot
{
    int _tStage;
    string _tCrop;
    readonly List<Vector3> _tCells = new();
    float _tLog;

    void RegisterTarim()
    {
        if (!Main.Args.ContainsKey("tarim")) return;
        // Senaryonun hangi adimi gorunurse gorunsun bot tarim planini oynar (ilk gun zinciri bu sinamada yok).
        foreach (var id in _steps.Keys.ToList()) _steps[id] = StepTarim;
        foreach (var s in _d.Data.Scenario.Steps) _steps[s.Id] = StepTarim;
    }

    Vector3 TarimCell(int i)
    {
        var o = _d.OcakPos;
        // Ilk gun tarlasindan (Ocak +3..+5, -1..+1) ayri, Ocak'in guneyinde iki kare.
        return _d.World.Ground(new Vector3(Mathf.Floor(o.X) + 3 + i + 0.5f, 0, Mathf.Floor(o.Z) + 4 + 0.5f));
    }

    Plot TarimPlot(int i) { var c = TarimCell(i); return _d.State.Farm.Get(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Z)); }

    void StepTarim()
    {
        float dt = (float)GetProcessDeltaTime();
        _tLog -= dt;
        _menuT -= dt; // menu bekleme sayaci (Bot.Kemer CraftHand, MenuClose) her adim kendisi isletir
        if (_tLog <= 0) { _tLog = 5f; GD.Print($"BOT tarim stage={_tStage} crop={_tCrop} seeds={(_tCrop != null ? _d.State.Backpack.Count(_tCrop + "_seed") : 0)} t={_t:0}"); }
        var bag = _d.State.Backpack;
        switch (_tStage)
        {
            case 0: // Ocak
                if (_d.Ocak != null) { _tStage = 1; _stepT = 0; return; }
                StepOcak();
                return;
            case 1: // Korkuluk: uret (Odun + Lif oyuncunun yolundan), tarlanin yanina kur
                if (_d.State.Farm.Scarecrows.Count > 0) { _tStage = 2; _stepT = 0; return; } // FarmView saniyede bir tarar
                var at = TarimCell(0) + new Vector3(-1.5f, 0, 1.5f);
                // Malzeme toplanirken Capa elde olmali (Bicme); uretilince PlaceHeld korkulugu kemerden ele alir.
                var rk = _d.Data.Tool.RecipeFor("korkuluk");
                bool missing = bag.Count("korkuluk") == 0 && rk != null && rk.In.Any(kv => bag.Count(kv.Key) < kv.Value);
                if (missing && !_d.Capa.InHand && !_d.Hud.MenuOpen) { HoldItem(PlanTools.Capa); return; }
                if (_tLog > 4.9f) GD.Print($"BOT tarim korkuluk wood={bag.Count("wood")} fiber={bag.Count("fiber")} have={bag.Count("korkuluk")} inHand={_d.Capa.InHand} head={_d.Capa.Current} menu={_d.Hud.MenuOpen} pos={P.GlobalPosition}");
                PlaceHeld("korkuluk", at, at + new Vector3(-2.5f, 0, -2.5f), 0.8f);
                return;
            case 2: // Ikinci ekinin tohumu: yabani bitkiyi Bicme ile bic
            {
                _tCrop ??= _d.FarmView.NearestWild(P.GlobalPosition, "wheat") != null ? "wheat"
                    : _d.FarmView.NearestWild(P.GlobalPosition)?.Plot.Crop is string c && c != "turnip" ? c : null;
                if (_tCrop == null) { if (_stepT > 5f) { GD.Print("BOT tarim: yabani bitki yok"); _stepT = 0; } return; }
                if (bag.Count(_tCrop + "_seed") >= 2) { _tStage = 3; _stepT = 0; return; }
                if (!_d.Capa.InHand && !_d.Hud.MenuOpen) { HoldItem(PlanTools.Capa); return; }
                var w = _d.FarmView.NearestWild(P.GlobalPosition, _tCrop);
                if (w == null) { P.MoveOverride = Vector3.Zero; return; } // yeniden bitmesini bekle
                Head(Capa.Head.Bicme);
                Aim(w.GlobalPosition + Vector3.Up * 0.3f);
                if (MoveRouted(w.GlobalPosition, 2.5f)) Fire = true;
                return;
            }
            case 3: // Sur, tohumu sec, ek
            {
                if (!_d.Capa.InHand && !_d.Hud.MenuOpen) { HoldItem(PlanTools.Capa); return; }
                int i = Enumerable.Range(0, 2).FirstOrDefault(k => TarimPlot(k)?.Planted != true, -1);
                if (i < 0 || bag.Count(_tCrop + "_seed") == 0 && TarimPlot(0)?.Planted == true) { _tStage = 4; _stepT = 0; _cell = 0; return; }
                Head(Capa.Head.Toprak);
                if (_d.State.Farm.Selected != _tCrop && _stepT > 0.3f) { _d.FarmView.SelectSeed(_tCrop); _stepT = 0; } // oyuncunun T tusu
                var c = TarimCell(i);
                Aim(c);
                if (MoveTo(c + new Vector3(0, 0, -3.5f), 1.0f, false)) Fire = true;
                return;
            }
            case 4: // Sula, buyumesini bekle (susayan kareyi yeniden sula)
            case 5:
            {
                var plots = Enumerable.Range(0, 2).Select(TarimPlot).Where(p => p != null && p.Planted && p.Crop == _tCrop).ToList();
                if (plots.Count == 0) { _tStage = 3; return; } // yenildiyse yeniden ek
                if (plots.Any(p => p.Stage == CropStage.Mature)) { _tStage = 6; _stepT = 0; return; }
                var dry = plots.FirstOrDefault(p => p.Moisture < 45f);
                if (dry != null)
                {
                    Head(Capa.Head.Sulama);
                    var c = _d.FarmView.CellCenter(dry.X, dry.Y);
                    Aim(c);
                    if (MoveTo(c + new Vector3(0, 0, -4f), 1.0f, false)) Fire = true;
                    return;
                }
                var mid = _d.FarmView.CellCenter(plots[0].X, plots[0].Y);
                Aim(mid + Vector3.Up * 0.3f);
                MoveTo(mid + new Vector3(-3f, 0, -3f), 1.0f, false);
                return;
            }
            case 6: // Hasat: Bicme
            {
                var p = Enumerable.Range(0, 2).Select(TarimPlot).FirstOrDefault(q => q != null && q.Planted && q.Crop == _tCrop && q.Stage == CropStage.Mature);
                if (p == null)
                {
                    bool harvested = Enumerable.Range(0, 2).Select(TarimPlot).Any(q => q != null && q.Crop == _tCrop && q.Harvested);
                    if (harvested) { _tStage = 7; return; }
                    _tStage = 4; return;
                }
                Head(Capa.Head.Bicme);
                var c = _d.FarmView.CellCenter(p.X, p.Y);
                Aim(c + Vector3.Up * 0.2f);
                if (MoveTo(c + new Vector3(0, 0, -3f), 1.0f, false)) Fire = true;
                return;
            }
            default:
                if (_done) return;
                _done = true;
                P.MoveOverride = Vector3.Zero;
                var f = _d.State.Farm;
                var def = _d.Data.Crop(_tCrop);
                int alaca = _d.Data.Crops.Genetics.Alaca.Sum(a => bag.Count(Genetics.AlacaItem(_tCrop, a.Id)));
                GD.Print($"RESULT ok tarim crop={_tCrop} item={bag.Count(def.ProductId)} alaca={alaca} seeds={bag.Count(def.SeedId)} " +
                         $"ledger={f.Seeds.CountFor(_tCrop)} best={(f.Log.Best.TryGetValue(_tCrop, out var g) ? g.ToString() : "-")} korkuluk={f.Scarecrows.Count} " +
                         $"protected={Enumerable.Range(0, 2).Count(k => TarimPlot(k)?.Protected == true)} t={_t:0.0}");
                GetTree().Quit(0);
                return;
        }
    }
}
