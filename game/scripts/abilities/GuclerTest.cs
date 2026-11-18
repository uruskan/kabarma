using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=gucler (gucler akisi, GDD 17.1, 14.7). Oyuncu gercek girdiyle Atilma'nin Kadim Tas'ina yurur (MoveOverride),
/// E'ye basar (InputMap "interact"), parkurun halkalarindan sirayla gecer; guc acilir ve 1. yuvaya girer. Sonra
/// "ability_1" ile Atilir: yer degistirme, Sarj (100 -> 75) ve bekleme denenir. Esya ya da guc verilmez.
/// Sinama kosumu (bot degil, dogrudan cagri): Tatbikat (StartDrill: hayalet dalga dogar, oyuncuya yurur; bitince
/// soner, gece sayaci, Kredi ve acilan gucler degismez) ve Maya Feneri (Drill.Place ile kurulur, cantadan dusmez; yakma
/// istegi ve gunluk sinir). Sonuc: "GUCLER RESULT ok|fail ...", cikis 0/1. --test_out=KLASOR (pencereli) goruntuler.
/// </summary>
public partial class GuclerTest : Node
{
    readonly Director _d;
    enum S { Wait, Walk, Press, Rings, Dash, Drill, Fener, Done }
    S _s = S.Wait;
    float _t, _st, _total, _shotCd;
    int _ring;
    Vector3 _dashFrom;
    float _chargeBefore;
    bool _dashArmed;
    int _usesBefore, _nightsBefore, _creditsBefore, _spawnedSeen;
    float _ghostDist0 = -1;
    readonly List<string> _fails = new();
    readonly List<string> _log = new();
    readonly HashSet<string> _shots = new();
    float _sideT, _stuckT; int _sideSign = 1;

    Player P => _d.Player;
    AbilitySystem A => _d.Abilities;
    KadimTasDef Stone => A.F.Stone("kadim_atilma");

    public GuclerTest(Director d) { _d = d; Name = "GuclerTest"; ProcessPriority = -500; }

    void Say(string s) { GD.Print(s); _log.Add(s); }
    void Go(S s) { _s = s; _st = 0; Say($"GUCLER state {s} t={_total:0.0}"); }
    void Fail(string why) { if (_s == S.Done) return; _fails.Add(why); Finish(); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _st += dt; _total += dt; _shotCd -= dt;
        if (_s == S.Done) return;
        if (_total > 600f) { Fail($"zaman asimi ({_s})"); return; }
        switch (_s)
        {
            case S.Wait:
                if (A?.Stones == null || P == null || _t < 3f) return;
                Say($"GUCLER start pos={P.GlobalPosition} stone={A.Stones.StonePos(Stone)} unlocked={A.D.State.Abilities.Unlocked.Count}");
                if (A.D.State.Abilities.Unlocked.Count > 0) { Fail("baslangicta acik guc var"); return; }
                foreach (var s in A.F.Stones)
                {
                    var sp = A.Stones.StonePos(s);
                    if (A.D.World.HasLake && sp.Y < A.D.World.WaterLevel + 0.3f) { Fail($"{s.Id} suyun icinde (y {sp.Y:0.0} < su {A.D.World.WaterLevel:0.0})"); return; }
                    foreach (var g in s.Rings)
                    {
                        var rp = A.Stones.Local(s, g[0], g[1]);
                        if (A.D.World.HasLake && rp.Y < A.D.World.WaterLevel + 0.3f) { Fail($"{s.Id} halkasi suda ({g[0]},{g[1]})"); return; }
                    }
                }
                Go(S.Walk);
                return;
            case S.Walk:
            {
                var stone = A.Stones.StonePos(Stone);
                P.AimOverride = stone + Vector3.Up * 2f;
                if (MoveTo(stone + (P.GlobalPosition - stone).Normalized() * 1.6f, 0.6f)) { P.MoveOverride = Vector3.Zero; Go(S.Press); }
                else if (_st > 200f) Fail($"Kadim Tas'a varilamadi ({P.GlobalPosition.DistanceTo(stone):0.0} m)");
                return;
            }
            case S.Press:
                if (_st < 0.3f) return;
                if (_st < 0.4f) { Shot("kadim_tas"); Input.ActionPress("interact"); return; }
                if (_st < 0.6f) { Input.ActionRelease("interact"); return; }
                if (A.Stones.Run == null) { Fail("E deneme baslatmadi"); return; }
                Say($"GUCLER trial started {A.Stones.Run.StoneId}");
                _ring = 0;
                Go(S.Rings);
                return;
            case S.Rings: Rings(); return;
            case S.Dash: Dash(); return;
            case S.Drill: Drill(); return;
            case S.Fener: Fener(); return;
        }
    }

    void Rings()
    {
        var run = A.Stones.Run;
        if (run.Won)
        {
            P.MoveOverride = Vector3.Zero;
            var l = A.L;
            Say($"GUCLER trial won t={run.Time:0.0} unlocked={string.Join(",", A.D.State.Abilities.Unlocked)} slot0={l.Slots.FirstOrDefault()}");
            if (!A.D.State.Abilities.Unlocked.Contains("atilma")) { Fail("Atilma acilmadi"); return; }
            if (l.Slots[0] != "atilma") { Fail("Atilma yuvaya girmedi"); return; }
            if (!A.D.State.KadimTas.Done.Contains(Stone.Id)) { Fail("tas tamamlanmadi"); return; }
            Go(S.Dash);
            return;
        }
        if (run.Lost) { Fail($"parkur kaybedildi: {run.Reason}"); return; }
        _ring = run.Ring;
        if (_ring >= Stone.Rings.Count) return;
        var g = Stone.Rings[_ring];
        var at = A.Stones.Local(Stone, g[0], g[1]);
        P.AimOverride = at + Vector3.Up * 1.5f;
        MoveTo(at, 0.3f);
        if (_ring == 2) Shot("parkur_halka");
    }

    void Dash()
    {
        var l = A.L;
        if (_st < 1.5f) { P.MoveOverride = Vector3.Zero; return; }
        if (_st < 1.6f)
        {
            if (_dashArmed) return; // pencere birkac kare surer: once yakala, bir kez bas
            _dashArmed = true;
            _dashFrom = P.GlobalPosition;
            _chargeBefore = l.Charge;
            _usesBefore = A.Uses;
            P.AimOverride = P.GlobalPosition + new Vector3(0, 1, -10);
            Input.ActionPress("ability_1");
            return;
        }
        if (_st < 1.7f) { Input.ActionRelease("ability_1"); return; }
        if (_st < 1.75f) Shot("atilma");
        if (_st < 2.3f) return;
        float moved = (P.GlobalPosition - _dashFrom).Length();
        Say($"GUCLER dash moved={moved:0.00} charge {_chargeBefore:0}->{l.Charge:0.0} cd={l.CooldownOf("atilma"):0.0} uses={A.Uses}");
        if (A.Uses != _usesBefore + 1) { Fail("ability_1 gucu kullanmadi"); return; }
        if (moved < 4f) { Fail($"Atilma yer degistirmedi ({moved:0.0} m)"); return; }
        if (l.Charge > _chargeBefore - 20f) { Fail($"Sarj harcanmadi ({l.Charge:0})"); return; }
        if (l.CooldownOf("atilma") <= 0) { Fail("bekleme baslamadi"); return; }
        if (A.TryUse(0)) { Fail("bekleme surerken yeniden kullanildi"); return; }
        _nightsBefore = A.D.State.NightsStarted;
        _creditsBefore = A.D.State.Credits;
        var err = A.Drill.StartDrill(1);
        if (err != null) { Fail($"Tatbikat baslamadi: {err}"); return; }
        Go(S.Drill);
    }

    void Drill()
    {
        var ghosts = A.Ghosts.Where(g => IsInstanceValid(g) && !g.Dead).ToList();
        if (ghosts.Count > 0)
        {
            float d = ghosts.Min(g => g.GlobalPosition.DistanceTo(P.GlobalPosition));
            if (_ghostDist0 < 0) { _ghostDist0 = d; _spawnedSeen = A.Drill.Spawned; }
            if (_st > 8f && _st < 8.1f) Shot("tatbikat_hayalet");
        }
        if (_st < 10f) return;
        float dNow = ghosts.Count > 0 ? ghosts.Min(g => g.GlobalPosition.DistanceTo(P.GlobalPosition)) : -1;
        Say($"GUCLER drill spawned={A.Drill.Spawned} alive={ghosts.Count} dist {_ghostDist0:0.0}->{dNow:0.0}");
        if (_spawnedSeen <= 0) { Fail("Tatbikat hayalet dogurmadi"); return; }
        if (dNow < 0 || dNow >= _ghostDist0 - 1f) { Fail("hayaletler oyuncuya yurumedi"); return; }
        A.Drill.End("test");
        if (A.Ghosts.Any(g => IsInstanceValid(g) && !g.Dead)) { Fail("Tatbikat bitince hayalet kaldi"); return; }
        if (A.D.State.NightsStarted != _nightsBefore) { Fail("Tatbikat gece sayacini ilerletti"); return; }
        if (A.D.State.Credits != _creditsBefore) { Fail("Tatbikat Kredi verdi"); return; }
        if (A.D.State.Abilities.Unlocked.Count != 1) { Fail("Tatbikat guc acti"); return; }
        Go(S.Fener);
    }

    void Fener()
    {
        if (_st < 0.5f) return;
        var def = A.D.Data.Tool.Placeables.FirstOrDefault(p => p.Id == "maya_feneri");
        if (def == null) { Fail("placeables.json'da maya_feneri yok"); return; }
        var at = A.D.World.Ground(P.GlobalPosition + new Vector3(2, 0, 0));
        if (!A.Drill.Place(def, at, 0)) { Fail("fener kurulmadi"); return; }
        var site = A.D.State.Fener.Sites.Last();
        A.Drill.Light(site.Id);
        bool lit = A.D.State.Fener.LitDay.ContainsKey(site.Id);
        Say($"GUCLER fener {site.Id} lit={lit} night={A.D.NightActive} ks={TatbikatRules.FenerKs(A.F, A.Drill.HighestKs)}");
        if (lit)
        {
            // Gunluk sinir: ayni gun ikinci yakma reddedilir.
            int day = A.D.State.Fener.LitDay[site.Id];
            var again = TatbikatRules.LightFener(A.F, A.D.State.Fener, site.Id, day, A.Drill.HighestKs, false, 0, 0, out var why);
            if (again != null) { Fail("fener ayni gun iki kez yandi"); return; }
            Say($"GUCLER fener second refused: {why}");
        }
        Shot("maya_feneri");
        Finish();
    }

    void Finish()
    {
        if (P != null) { P.MoveOverride = Vector3.Zero; P.AimOverride = null; }
        Input.ActionRelease("interact");
        Input.ActionRelease("ability_1");
        bool ok = _fails.Count == 0;
        _s = S.Done;
        Say($"GUCLER RESULT {(ok ? "ok" : "fail")} unlocked={string.Join(",", A?.D.State.Abilities.Unlocked ?? new())} trials={string.Join(",", A?.Stones.Results ?? new())} uses={A?.Uses} drill_spawned={A?.Drill.Spawned} feners={A?.D.State.Fener.Sites.Count} t={_total:0}" + (_fails.Count > 0 ? " :: " + string.Join(" | ", _fails) : ""));
        if (Main.Args.TryGetValue("test_out", out var dir))
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            using var fa = FileAccess.Open(dir.TrimEnd('/', '\\') + "/gucler.log", FileAccess.ModeFlags.Write);
            fa?.StoreString(string.Join("\n", _log) + "\n");
        }
        GetTree().CreateTimer(DisplayServer.GetName() == "headless" ? 0.1 : 1.0).Timeout += () => GetTree().Quit(ok ? 0 : 1);
    }

    bool MoveTo(Vector3 target, float within)
    {
        var d = (target - P.GlobalPosition) with { Y = 0 };
        if (d.Length() <= within) { P.MoveOverride = Vector3.Zero; _stuckT = 0; return true; }
        var dir = d.Normalized();
        float dt = (float)GetProcessDeltaTime();
        if (_sideT > 0) { _sideT -= dt; dir = (dir.Cross(Vector3.Up) * _sideSign + dir * 0.3f).Normalized(); }
        else if (new Vector2(P.Velocity.X, P.Velocity.Z).Length() < 0.3f) { _stuckT += dt; if (_stuckT > 0.6f) { _stuckT = 0; _sideT = 1.2f; _sideSign = -_sideSign; } }
        else _stuckT = 0;
        P.MoveOverride = dir * 1.5f;
        return false;
    }

    void Shot(string name)
    {
        if (_shots.Contains(name) || DisplayServer.GetName() == "headless" || !Main.Args.TryGetValue("test_out", out var dir)) return;
        if (_shotCd > 0) return;
        _shots.Add(name);
        _shotCd = 0.3f;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var path = dir.TrimEnd('/', '\\') + $"/{name}.png";
        RenderingServer.FramePostDraw += Save;
        void Save()
        {
            RenderingServer.FramePostDraw -= Save;
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"SHOT {path}");
        }
    }
}
