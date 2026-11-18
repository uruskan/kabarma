using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Sinama botu: ilk 10 dakikayi oyuncunun girisleriyle (hareket, nisan, tetik, tus) oynar.
/// Hile yok: Capa'yi ve Director'u oyuncunun kullandigi yoldan surer.
/// Adimlar kayitlidir (_steps: senaryo adim kimligi -> isleyici). Parcalar: Bot.Day.cs, Bot.Night.cs, Bot.Machines.cs, Bot.Build.cs.
/// Yeni adim: yeni bir Bot.Xxx.cs (partial) icinde RegisterXxx() { _steps["adim"] = StepXxx; } ve asagidaki kurucuya bir satir.
/// Isleyicisi olmayan adim: "BOT unknown step" yazilir ve bot basarisiz cikar.
/// </summary>
public partial class Bot : Node
{
    readonly Director _d;
    public bool Fire, FirePressed;
    float _t, _stepT;
    int _cell;
    string _lastStep = "";
    readonly List<Vector3> _cells = new();
    bool _done;
    float _dbg;
    readonly Dictionary<string, Action> _steps = new();

    /// <summary>Zincirin sonuna ("end") varildi, RESULT ok yazildi.</summary>
    public bool Done => _done;
    /// <summary>Bot kurtarilamaz bir hataya dustu (ornegin isleyicisi olmayan adim).</summary>
    public bool Failed { get; private set; }
    /// <summary>Su anki senaryo adimi (sonuc satiri icin).</summary>
    public string CurrentStep => _lastStep;

    public Bot(Director d)
    {
        _d = d;
        RegisterDay();       // Bot.Day.cs: mine, ocak, till, plant, water, grow
        RegisterNight();     // Bot.Night.cs: night
        RegisterMachines();  // Bot.Machines.cs: sell, tezgah_kur, civi, hasat, firin_kur, corba, paketle, kasa_sat
        RegisterBuild();     // Bot.Build.cs: odun, yapi
        RegisterIlkgun();    // Bot.Ilkgun.cs: ilk gun (GDD 24): mine (kapak), tezgah, tarla, agil, gidak, night (agil kapisi)
        RegisterZincir();    // Bot.Zincir.cs (entegrasyon): yemek, firin, civi, kasa, barinak
        RegisterMarket();    // Bot.Market.cs (ekonomi2): katalog (rampadan tohum al, kapsulu ac)
        RegisterYapi();      // Bot.Yapi.cs (M2 yapi): yukselt (duvari Kerpic-Tas'a)
        RegisterUretim();    // Bot.Uretim.cs (zincir M2): arastir_t2 (--test=zincir; senaryoda yok)
        _steps["end"] = StepEnd;
        RegisterCoop();      // Bot.Coop.cs (ortak): --test=coop'ta butun adimlar ortak bot
        RegisterTarim();     // Bot.Tarim.cs (tarim M2): yalniz --test=tarim; ikinci ekin, Korkuluk, yabani tohum
        RegisterPower();     // Bot.Power.cs (guc): yalniz --test=power'da "end" guc adimina uzar
    }

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

        if (_steps.TryGetValue(step, out var run)) RunStep(step, run); // Bot.Hands.cs: girdi kipinde sentetik olay
        else UnknownStep(step);
    }

    /// <summary>Senaryoda botun bilmedigi bir adim var: yaz ve basarisiz cik.</summary>
    void UnknownStep(string step)
    {
        if (Failed) return;
        Failed = true;
        P.MoveOverride = Vector3.Zero;
        GD.Print($"BOT unknown step {step}");
        GD.Print($"RESULT fail bot unknown_step={step} t={_t:0.0}");
        GetTree().Quit(1);
    }

    void StepEnd()
    {
        P.MoveOverride = Vector3.Zero;
        Aim(_d.Pod.PadCenter + Vector3.Up * 12f);
        if (!_done)
        {
            _done = true;
            var s = _d.State;
            if (OrdersCheck() is string orderWhy) { GD.Print($"RESULT fail orders {orderWhy} t={_t:0.0}"); GetTree().Quit(1); return; } // Bot.Ilerleme.cs: ilk is emri zinciri
            GD.Print($"RESULT ok ciraklik={s.Orders.Chain("ciraklik")!.DoneCount}/5 veri={s.Research.Veri} credits={s.Credits} eggs={s.Eggs} kills={_d.Night?.Kills} cards={_d.Night?.CardsCount} maya={_d.Night?.Maya:0} stations={_d.Stations.Count} harvests={_d.Harvests} skipped={_d.SkippedIds} t={_t:0.0}");
        }
    }

    float _stuckT, _sideT, _sideSign = 1f;
}
