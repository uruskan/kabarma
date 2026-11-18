using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Ortak oyun botu (--test=coop, ortak akisi): ayni makinede host ve istemciler ayni yolu oynar: tas kaz, agactan odun
/// bic, Insa Plani ile Ocak'in cevresine kare temel kur, bir tarla karesi sur, dinlen. Hile yok: Capa ve Insa Plani oyuncunun yolundan;
/// istemcide istekler host'a gider, canta ve dunya host'tan doner (bot yalniz kendi cantasini okur).
/// Host once Ocak'i kurar (tapu, GDD 6); istemciler Ocak bolumle gelince kurar.
/// </summary>
public partial class Bot
{
    int _coopPhase, _coopTries, _coopStone0 = -1, _coopPieces0 = -1;
    float _coopShift;
    public bool CoopDone { get; private set; }
    /// <summary>Bu botun kurdugu parca sayisi (kendi gozunden: yapi bolumunde hedef yerdeki temel).</summary>
    public int CoopBuilt { get; private set; }
    public int CoopId => Main.Args.TryGetValue("bot_id", out var s) && int.TryParse(s, out var i) ? i : 0;

    void RegisterCoop()
    {
        if (!Main.Args.ContainsKey("coopbot")) return;
        foreach (var k in _steps.Keys.ToList()) _steps[k] = CoopStep; // senaryo adimi ne olursa olsun ortak bot
    }

    /// <summary>Botun temel yeri: Ocak'in cevresinde, bot basina ayri yon.</summary>
    Vector3 CoopSpot()
    {
        float ang = CoopId * Mathf.Pi / 2f + 0.4f;
        return _d.World.Ground(_d.OcakPos + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (8f + _coopShift));
    }

    void CoopStep()
    {
        var net = _d.Net;
        if (net == null || !net.Joined || _t < 3f) { P.MoveOverride = Vector3.Zero; return; }
        var bag = _d.State.Backpack;
        if (_coopStone0 < 0) _coopStone0 = bag.Count("stone");
        switch (_coopPhase)
        {
            case 0: // Ocak: host kurar, istemci gelmesini bekler
                if (_d.Ocak != null) { Next("ocak var"); break; }
                if (net.IsClient) { P.MoveOverride = Vector3.Zero; break; }
                StepOcak();
                break;
            case 1: // kaz: en az 4 tas
                if (bag.Count("stone") >= _coopStone0 + 4) { Next($"tas {bag.Count("stone")}"); break; }
                Head(Capa.Head.Kazma);
                var rocks = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Item == "stone").OrderBy(r => r.GlobalPosition.DistanceTo(_d.OcakPos)).ToList();
                if (rocks.Count == 0) { Next("kaya yok"); break; }
                var rock = rocks[Mathf.Min(CoopId, rocks.Count - 1)];
                Aim(rock.Center);
                if (MoveTo(rock.GlobalPosition, 4.0f)) Fire = true;
                break;
            case 2: // odun: kare temelin bedeli
                int need = _d.Build?.CostOf("kare_temel") ?? 6;
                if (bag.Count("wood") >= need) { Next($"odun {bag.Count("wood")}"); break; }
                Head(Capa.Head.Bicme);
                var trees = _d.World.TreePositions.OrderBy(x => x.DistanceTo(_d.OcakPos)).ToList();
                var tr = trees[Mathf.Min(CoopId, trees.Count - 1)];
                Aim(tr + Vector3.Up * 1.4f);
                if (MoveTo(tr, 3.0f)) Fire = true;
                break;
            case 3: // kur
            {
                var b = _d.Build;
                var target = CoopSpot();
                if (_coopPieces0 < 0) _coopPieces0 = b.System.Pieces.Count;
                if (b.System.Pieces.Any(p => IsInstanceValid(p) && p.Def.Id == "kare_temel" && p.GlobalPosition.DistanceTo(target) < 2.5f))
                { CoopBuilt++; Next($"temel {target}"); break; }
                if (!PlanWith("kare_temel")) return;
                Aim(target);
                var stand = target + (target - _d.OcakPos).Normalized() * 6.5f;
                if (MoveTo(stand, 0.7f, false) && _stepT > 1.5f)
                {
                    FirePressed = true; _stepT = 0;
                    if (++_coopTries % 4 == 0) { _coopShift += 2f; GD.Print($"BOT coop yer degisti: {b.System.Current?.Reason}"); }
                }
                break;
            }
            case 4: // tarla: Ocak'in yaninda kendi yonunde bir kare sur (istemcide istek host'a gider, NetSync.Farm.cs)
            {
                Head(Capa.Head.Toprak);
                float ang = CoopId * Mathf.Pi / 2f + 0.4f;
                var cell = _d.World.Ground(_d.OcakPos + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (4.5f + _coopTries));
                cell = _d.World.Ground(new Vector3(Mathf.Floor(cell.X) + 0.5f, 0, Mathf.Floor(cell.Z) + 0.5f));
                if (_d.State.Farm.Get(Mathf.FloorToInt(cell.X), Mathf.FloorToInt(cell.Z)) != null) { Next($"tarla {cell}"); break; }
                Aim(cell);
                var from = cell + (cell - _d.OcakPos).Normalized() * 3.5f;
                if (MoveTo(from, 0.8f, false))
                {
                    Fire = true;
                    if (_stepT > 4f) { _stepT = 0; Fire = false; if (++_coopTries > 4) Next("tarla olmadi"); } // egim ya da kapsul: bir metre otede dene
                }
                else _stepT = 0;
                break;
            }
            default: // dinlen: Ocak'in yaninda dur
            {
                var rest = _d.World.Ground(_d.OcakPos + new Vector3(3f + CoopId * 1.5f, 0, -3f));
                Head(Capa.Head.Kazma);
                Aim(rest + new Vector3(0, 1, 5));
                if (MoveTo(rest, 0.5f, false) && !CoopDone) { CoopDone = true; P.MoveOverride = Vector3.Zero; GD.Print($"BOT coop done id={CoopId} stone={bag.Count("stone")} wood={bag.Count("wood")} built={CoopBuilt} t={_t:0.0}"); }
                if (CoopDone) P.MoveOverride = Vector3.Zero;
                break;
            }
        }
    }

    void Next(string why)
    {
        GD.Print($"BOT coop phase {_coopPhase} ok ({why}) t={_t:0.0}");
        _coopPhase++;
        _stepT = 0;
        _coopTries = 0;
        Fire = false;
    }
}
