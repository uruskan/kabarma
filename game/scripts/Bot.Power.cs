using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, guc adimi (--test=power; GDD 11.1, 11.2; ICERIK 12 "M2 bedelleri"). Ilk gunun zinciri ("end") bitince oyuncunun
/// yolundan: Bakir Cevheri kazar, Tas Firin'da Bakir Kulce, Tezgah'ta Bakir Tel (copper_wire), Kablo, Ruzgar Gulu,
/// Elektrik Lambasi ve Dugme uretir; kemerden kurar; Kablo eldeyken soketlere sol tikla guc kablosu ceker (lamba yanar),
/// R ile sinyal teline gecip Dugme'yi lambaya baglar (lamba soner), Dugme'ye E basar (lamba yanar). Esya verilmez, adim
/// atlanmaz; senaryo adimi eklenmez (yalniz --test=power'da "end" bu adima uzar).
/// </summary>
public partial class Bot
{
    // Agilin (x -6..-3, z -9..-3) guneyinde, kapsulun (Ocak'a gore ~ +8.5, -8.5; 3,5 m kurulmaz) uzaginda.
    static readonly Vector3 PowerGenAt = new(-2f, 0, -14f);
    static readonly Vector3 PowerLampAt = new(2.5f, 0, -14f);
    static readonly Vector3 PowerButtonAt = new(2.5f, 0, -11.5f);

    bool _pwLit, _pwSignal, _pwPressed, _pwLitBySignal, _pwDone;
    float _pwT, _pwPressT, _pwDbg;

    void RegisterPower()
    {
        if (!Main.Args.ContainsKey("power")) return;
        _steps["end"] = Machine(StepPower);
    }

    PowerDevice PowerOf(string id) => _d.Power?.Devices.FirstOrDefault(x => x.Def.Id == id);

    /// <summary>Bakir Tel: kulce varsa Tezgah'ta cek, yoksa Tas Firin'da erit (cevher Kazma ile). Yeterliyse true.</summary>
    bool EnsureWire(int need)
    {
        var bag = _d.State.Backpack;
        if (bag.Count("copper_wire") >= need) return true;
        var tz = StationOf("tezgah"); var fr = StationOf("tas_firin");
        if (tz == null || fr == null) { if (_stepT > 3f) { GD.Print("BOT power: Tezgah ya da Tas Firin yok"); _stepT = 0; } return false; }
        if (tz.Queue.Active != null) { WaitAt(tz); return false; }
        if (bag.Count("copper_ingot") >= 1) { UseStation(tz, RecipeIndex(tz, "copper_wire")); return false; }
        if (fr.Queue.Active != null) { WaitAt(fr); return false; }
        if (EnsureItem("copper_ore", 2) && EnsureItem("wood", 1)) UseStation(fr, RecipeIndex(fr, "copper_ingot"));
        return false;
    }

    /// <summary>Kablo eldeyken: soket A, sonra soket B (sol tik). Kablo cekildiyse true.</summary>
    bool PullCable(PowerDevice a, PowerDevice b, bool signal)
    {
        var ps = _d.Power;
        if (ps.Net.Links.Any(l => l.Signal == signal && ((l.A == a.NodeId && l.B == b.NodeId) || (l.A == b.NodeId && l.B == a.NodeId)))) return true;
        if (_d.State.Backpack.Count("kablo") < 1) { HavePlaceable("kablo"); return false; }
        if (!HoldItem("kablo")) return false;
        if (ps.SignalMode != signal) { if (_stepT > 0.4f) { if (!UsesInput) ps.ToggleMode(); else _driver.Tap("build_rotate"); _stepT = 0; } return false; }
        var target = ps.Picked == a.NodeId ? b : a;
        var sock = target.SocketWorld;
        Aim(sock);
        var stand = sock + (P.GlobalPosition - sock).Normalized() * 3f; stand.Y = P.GlobalPosition.Y;
        if (!MoveTo(stand, 1.2f, false)) return false;
        if (_stepT > 0.8f)
        {
            if (ps.Picked >= 0 && ps.Picked != a.NodeId) { FirePressed = true; _stepT = 0; return false; } // yanlis secim: birak
            FirePressed = true;
            _stepT = 0;
            GD.Print($"BOT power click {target.Def.Id} picked={ps.Picked} signal={signal} last={ps.LastReason}");
        }
        return false;
    }

    void StepPower(Vector3 o)
    {
        if (_pwDone) { StepEnd(); return; }
        _pwT += (float)GetProcessDeltaTime();
        var bag = _d.State.Backpack;
        var gen = PowerOf("ruzgar_gulu"); var lamp = PowerOf("elektrik_lambasi"); var btn = PowerOf("dugme");
        _pwDbg -= (float)GetProcessDeltaTime();
        if (_pwDbg <= 0)
        {
            _pwDbg = 10f;
            GD.Print($"BOT power state t={_pwT:0} ore={bag.Count("copper_ore")} ingot={bag.Count("copper_ingot")} wire={bag.Count("copper_wire")} kablo={bag.Count("kablo")} fiber={bag.Count("fiber")} wood={bag.Count("wood")} gen={gen != null} lamp={lamp != null} btn={btn != null} links={_d.State.Power.Links.Count} lit={_pwLit} sig={_pwSignal} held={_d.State.Belt.Held} menu={_d.Hud.MenuOpen} belt=[{string.Join(",", _d.State.Belt.Ids)}]");
        }
        // 1) malzeme: tel (Ruzgar Gulu 4, lamba 1, dugme 1, Kablo 1 -> 2 Kablo)
        int wire = (gen == null && bag.Count("ruzgar_gulu") == 0 ? 4 : 0) + (lamp == null && bag.Count("elektrik_lambasi") == 0 ? 1 : 0)
                 + (btn == null && bag.Count("dugme") == 0 ? 1 : 0) + (bag.Count("kablo") < 2 && _d.Power.Net.Links.Count < 2 ? 1 : 0);
        if (wire > 0 && bag.Count("copper_wire") < wire) { EnsureWire(wire); return; }
        // 2) kur: uretec, lamba, dugme (kemerden, hayalet, sol tik)
        if (gen == null) { PlaceAt("ruzgar_gulu", o + PowerGenAt); return; }
        if (lamp == null) { PlaceAt("elektrik_lambasi", o + PowerLampAt); return; }
        if (btn == null) { PlaceAt("dugme", o + PowerButtonAt); return; }
        var net = _d.State.Power;
        var ln = net.Get(lamp.NodeId);
        // 3) guc kablosu: Ruzgar Gulu -> lamba; lamba yanar
        if (!_pwLit)
        {
            if (!PullCable(gen, lamp, false)) return;
            P.MoveOverride = Vector3.Zero;
            Aim(lamp.SocketWorld);
            if (ln.Powered) { _pwLit = true; GD.Print($"POWER lit by wind gen={net.Get(gen.NodeId).OutputW:0}W stats={net.StatsOf(lamp.NodeId)?.ServedW:0}W"); }
            else if (_stepT > 5f) { GD.Print($"BOT power lamp dark: links={net.Links.Count} gen={net.Get(gen.NodeId).OutputW:0}W"); _stepT = 0; }
            return;
        }
        // 4) sinyal teli: Dugme -> lamba; sinyal 0 iken lamba soner
        if (!_pwSignal)
        {
            if (!PullCable(btn, lamp, true)) return;
            if (!ln.Powered && ln.SignalWired) { _pwSignal = true; GD.Print("POWER signal wired: lamp waits for signal"); }
            return;
        }
        // 5) Dugme'ye E: lamba yanar
        if (ln.Powered && _pwPressed) { _pwLitBySignal = true; GD.Print($"POWER lit by button signal={ln.Signal}"); }
        if (_pwLitBySignal)
        {
            _pwDone = true;
            if (_d.Hud.MenuOpen) MenuClose();
            GD.Print($"POWER RESULT ok lit={_pwLit} signal={_pwSignal} pressed={_pwPressed} nodes={net.Nodes.Count} links={net.Links.Count} t={_pwT:0.0}");
            StepEnd();
            GetTree().CreateTimer(1.0).Timeout += () => GetTree().Quit(0);
            return;
        }
        if (_d.Hud.MenuOpen) { if (_menuT <= 0) { MenuClose(); _menuT = 0.3f; } return; }
        if (!HoldItem(PlanTools.Capa)) return; // eller Capa'da: E dugmeye basar (Kablo eldeyken E ayar paneli acar)
        var away = (btn.GlobalPosition - lamp.GlobalPosition); away.Y = 0;
        var stand = btn.GlobalPosition + away.Normalized() * 1.4f;
        Aim(btn.GlobalPosition + Vector3.Up);
        if (!MoveTo(stand, 0.4f, false)) return;
        _pwPressT -= (float)GetProcessDeltaTime();
        if (_pwPressT <= 0) { Use(); _pwPressed = true; _pwPressT = 1.5f; GD.Print("BOT power press"); }
    }
}
