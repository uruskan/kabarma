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
        if (d.Length() <= within) { P.MoveOverride = Vector3.Zero; return true; }
        P.MoveOverride = d.Normalized() * (sprint ? 1.5f : 1f);
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
        if (_dbg <= 0 && Main.Args.ContainsKey("botdebug")) { _dbg = 2f; GD.Print($"BOTDBG {step} pos={P.GlobalPosition} aim={P.AimOverride} cell={_cell} prog={sc.Progress} ocak={_d.OcakPos} head={_d.Capa.Current}"); }

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
                    if (MoveTo(_d.Gidak.GlobalPosition, 2.0f)) { _d.Interact(); _fed = true; }
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
                    if (MoveTo(pad + away.Normalized() * 3.6f, 0.8f, false) && _d.Capa.OverPad(held.GlobalPosition) && _stepT > 0.8f) { FirePressed = true; _stepT = 0; }
                }
                break;
            }
            case "end":
            {
                P.MoveOverride = Vector3.Zero;
                Aim(_d.Pod.PadCenter + Vector3.Up * 12f);
                if (!_done && _d.Hud.ReportOpen)
                {
                    _done = true;
                    var s = _d.State;
                    GD.Print($"RESULT ok credits={s.Credits} eggs={s.Eggs} kills={_d.Night?.Kills} cards={_d.Night?.CardsCount} maya={_d.Night?.Maya:0} t={_t:0.0}");
                }
                break;
            }
        }
    }
}
