using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Sinama botu: ilk 10 dakikayi oyuncunun girisleriyle (hareket, nisan, tetik, tus) oynar.
/// Hile yok: Capa'yi ve Director'u oyuncunun kullandigi yoldan surer.
/// </summary>
public partial class Bot : Node
{
    readonly Director _d;
    public bool Fire, FirePressed;
    float _t, _stepT;
    int _cell;
    string _lastStep = "";
    readonly List<Vector3> _cells = new();
    bool _fed;
    bool _done;
    float _dbg;

    public Bot(Director d) { _d = d; }

    Player P => _d.Player;

    bool MoveTo(Vector3 target, float within, bool sprint = true)
    {
        var d = target - P.GlobalPosition; d.Y = 0;
        if (d.Length() <= within) { P.MoveOverride = Vector3.Zero; _stuckT = 0; return true; }
        var dir = d.Normalized();
        // Engel: hiz yoksa bir sure yana kay (duvar, istasyon, agac)
        float dt = (float)GetProcessDeltaTime();
        if (_sideT > 0) { _sideT -= dt; dir = (dir.Cross(Vector3.Up) * _sideSign + dir * 0.3f).Normalized(); }
        else if (new Vector2(P.Velocity.X, P.Velocity.Z).Length() < 0.3f) { _stuckT += dt; if (_stuckT > 0.6f) { _stuckT = 0; _sideT = 1.2f; _sideSign = -_sideSign; } }
        else _stuckT = 0;
        P.MoveOverride = dir * (sprint ? 1.5f : 1f);
        return false;
    }

    void Aim(Vector3 at) => P.AimOverride = at;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        _stepT += dt;
        if (_t < 1.0f) return;
        var sc = _d.State.Scenario;
        string step = sc.Current?.Id ?? "end";
        if (step != _lastStep) { _lastStep = step; _stepT = 0; _cell = 0; Fire = false; GD.Print($"BOT step {step} t={_t:0.0}"); }
        Fire = false;
        _dbg -= dt;
        if (_dbg <= 0 && Main.Args.ContainsKey("botdebug")) { _dbg = 2f; GD.Print($"BOTDBG {step} pos={P.GlobalPosition} aim={P.AimOverride} cell={_cell} prog={sc.Progress} ocak={_d.OcakPos} head={_d.Capa.Current} vel={P.Velocity} floor={P.IsOnFloor()} down={P.Down} move={P.MoveOverride} held={_d.Capa.Held != null}"); }

        switch (step)
        {
            case "mine":
            {
                _d.Capa.Select(Capa.Head.Kazma);
                var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0).OrderBy(r => r.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
                if (rock == null) break;
                Aim(rock.Center);
                if (MoveTo(rock.GlobalPosition, 4.5f)) Fire = true;
                break;
            }
            case "ocak":
            {
                _d.Capa.Select(Capa.Head.Insa);
                var spot = _d.World.Ground(new Vector3(0, 0, 0));
                Aim(spot);
                if (MoveTo(spot + new Vector3(3, 0, -3), 0.8f) && _stepT > 1.5f) { FirePressed = true; _stepT = 0; }
                break;
            }
            case "till":
            case "plant":
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
                break;
            }
            case "water":
            {
                _d.Capa.Select(Capa.Head.Sulama);
                var c = _cells[_cell % 9];
                Aim(c);
                if (MoveTo(c + new Vector3(0, 0, -4f), 1.0f, false))
                {
                    Fire = true;
                    if (_stepT > 0.35f) { _stepT = 0; _cell++; }
                }
                break;
            }
            case "grow":
            {
                if (!_fed && _d.Gidak.Visible)
                {
                    Aim(_d.Gidak.GlobalPosition + Vector3.Up * 0.5f);
                    MoveTo(_d.Gidak.GlobalPosition, 2.0f);
                    if (_d.Gidak.GlobalPosition.DistanceTo(P.GlobalPosition) < 2.8f) { _d.Interact(); _fed = true; }
                }
                else { Aim(_d.FarmCenter() + Vector3.Up * 0.5f); MoveTo(_d.FarmCenter() + new Vector3(-6, 0, -2), 1f, false); }
                break;
            }
            case "night":
            {
                var n = _d.Night;
                if (n?.Offer != null && n.Offer.Age > 0.6f) _d.ChooseCard(0);
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
                break;
            }
            case "sell":
            {
                _d.Capa.Select(Capa.Head.Cekim);
                var held = _d.Capa.Held;
                var pad = _d.Pod.PadCenter;
                if (held == null)
                {
                    var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted).OrderBy(v => v.GlobalPosition.DistanceTo(pad)).FirstOrDefault();
                    var loose = _d.GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>().FirstOrDefault(s => !s.Sold);
                    Vector3? target = loose?.GlobalPosition ?? view?.CropTop;
                    if (target == null) break;
                    Aim(target.Value);
                    if (MoveTo(target.Value, 4.5f, false) && _stepT > 0.6f) { FirePressed = true; _stepT = 0; }
                }
                else
                {
                    var away = (P.GlobalPosition - pad); away.Y = 0;
                    Aim(pad + Vector3.Up * 0.5f);
                    if (MoveTo(pad + away.Normalized() * 3.2f, 0.8f, false) && _stepT > 0.8f) { FirePressed = true; _stepT = 0; }
                }
                break;
            }
            case "tezgah_kur":
            case "civi":
            case "hasat":
            case "firin_kur":
            case "corba":
            case "paketle":
            case "kasa_sat":
            case "odun":
            case "yapi":
                if (_d.Hud.ReportOpen) { if (_stepT > 1.5f) { _d.CloseReport(); _stepT = 0; } P.MoveOverride = Vector3.Zero; break; }
                MachineChain(step);
                break;
            case "end":
            {
                P.MoveOverride = Vector3.Zero;
                Aim(_d.Pod.PadCenter + Vector3.Up * 12f);
                if (!_done)
                {
                    _done = true;
                    var s = _d.State;
                    GD.Print($"RESULT ok credits={s.Credits} eggs={s.Eggs} kills={_d.Night?.Kills} cards={_d.Night?.CardsCount} maya={_d.Night?.Maya:0} stations={_d.Stations.Count} harvests={_d.Harvests} t={_t:0.0}");
                }
                break;
            }
        }
        }

    // ---------- M1 makine zinciri: oyuncunun yaptigini yapar (hareket, nisan, tetik, E, menu tuslari) ----------

    float _menuT;
    float _stuckT, _sideT, _sideSign = 1f;
    int _built, _pendingCheck = -1;

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

    void MachineChain(string step)
    {
        _menuT -= (float)GetProcessDeltaTime();
        var o = _d.OcakPos;
        switch (step)
        {
            case "tezgah_kur":
                if (EnsureStone(6)) PlaceAt("tezgah", o + new Vector3(-5f, 0, 1.5f));
                break;
            case "civi":
            {
                var st = StationOf("tezgah");
                if (st != null && EnsureStone(2)) UseStation(st, 0);
                break;
            }
            case "hasat":
            {
                _d.Capa.Select(Capa.Head.Bicme);
                var v = _d.FarmView.Views.Where(x => x.Plot.Planted && x.Plot.Stage == Kabarma.Sim.CropStage.Mature)
                    .OrderBy(x => x.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
                if (v == null) { GD.Print("BOT nothing to harvest"); break; }
                Aim(v.GlobalPosition + Vector3.Up * 0.3f);
                if (MoveTo(v.GlobalPosition, 3.2f, false)) Fire = true;
                break;
            }
            case "firin_kur":
                if (EnsureStone(10)) PlaceAt("tas_firin", o + new Vector3(-5f, 0, -3.5f));
                break;
            case "corba":
            {
                var st = StationOf("tas_firin");
                if (st == null) break;
                if (_d.State.Backpack.Count("salgam_corbasi") > 0)
                {
                    P.MoveOverride = Vector3.Zero;
                    if (!_d.Hud.MenuOpen && _menuT <= 0) { _d.OpenBagMenu(); _menuT = 0.5f; }
                    else if (_d.Hud.MenuOpen && _menuT <= 0) { _d.Hud.ActivateMenu(0); _d.Hud.HideMenu(); _menuT = 1f; }
                }
                else if (st.Queue.Active == null) UseStation(st, 0);
                else { P.MoveOverride = Vector3.Zero; Aim(st.GlobalPosition + Vector3.Up); }
                break;
            }
            case "paketle":
            {
                var st = StationOf("paketleme");
                if (st == null) { if (EnsureStone(8)) PlaceAt("paketleme", o + new Vector3(-1f, 0, 5.5f)); break; }
                if (st.Queue.Active == null && _d.State.Backpack.Count("salgam_kasasi") == 0) UseStation(st, 0);
                else { P.MoveOverride = Vector3.Zero; Aim(st.GlobalPosition + Vector3.Up); }
                break;
            }
            case "odun":
            {
                _d.Capa.Select(Capa.Head.Bicme);
                var t = _d.World.TreePositions.OrderBy(x => x.DistanceTo(P.GlobalPosition)).First();
                Aim(t + Vector3.Up * 1.4f);
                if (MoveTo(t, 3.0f)) Fire = true;
                break;
            }
            case "yapi":
            {
                var b = _d.Build;
                if (b == null) { GD.Print("BOT no build kit"); break; }
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
                    break;
                }
                _d.Capa.Select(Capa.Head.Insa);
                if (_d.Capa.CurrentPlaceable != want) { if (_stepT > 0.12f) { _d.Capa.CyclePlaceable(1); _stepT = 0; } break; }
                Aim(target);
                if (MoveTo(stand, 0.6f, false) && _stepT > 1.2f)
                {
                    int before = b.System.Pieces.Count;
                    FirePressed = true; _stepT = 0;
                    _pendingCheck = before;
                }
                if (_pendingCheck >= 0 && b.System.Pieces.Count > _pendingCheck) { _built++; _pendingCheck = -1; GD.Print($"BOT built {want} -> {_built}"); }
                else if (_pendingCheck >= 0 && _stepT > 1.0f) { GD.Print($"BOT place failed {want}: {b.System.Current?.Reason} valid={b.System.Current?.Valid}"); _pendingCheck = -1; }
                break;
            }
            case "kasa_sat":
            {
                var pad = _d.Pod.PadCenter;
                Aim(pad + Vector3.Up);
                if (!MoveTo(pad + (P.GlobalPosition - pad).Normalized() * 3.2f, 0.6f, false)) break;
                if (!_d.Hud.MenuOpen && _menuT <= 0) { _d.Interact(); _menuT = 0.6f; }
                else if (_d.Hud.MenuOpen && _menuT <= 0)
                {
                    int idx = _d.Data.Tool.Prices.Keys.ToList().IndexOf("salgam_kasasi");
                    _d.Hud.ActivateMenu(idx); _d.Hud.HideMenu(); _menuT = 1f;
                }
                break;
            }
        }
    }
}
