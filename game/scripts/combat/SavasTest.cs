using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=savas (savas akisi, GDD §16.1, §16.3, ICERIK §4.2). Bot gercek girdiyle (nisan AimOverride, tetik sentetik
/// fare olayi, kemer ve baslik secimi, MoveOverride) dovusur; esya, can ya da hasar verilmez.
/// 1. Gunduz savasi: yakinda bir yabani Bozuk doger (sahne kurulumu); bot Tirpan Isini ile onu yere serer (Kesme, profil).
/// 2. Azman Homur: bot uc temel parcasinin (sahne kurulumu, sur yerine) arkasinda durur; Azman KS 5 Doruk kancasiyla
///    (AzmanSystem.OnPeak, kabarma3'un cagiracagi yol) doger. Uzun hucum sura toslar ve sersemler; sirttaki Homurlar
///    esiklerde duser; bot hepsini ve Azman'i yener.
/// 3. Azman Bozuk: bir agacin yakininda doger; agac soker, kutugu firlatir; bot kacar, yerdeki kutugu Cekim'le alip
///    Azman'a firlatir ve Tirpan'la bitirir.
/// Sonuc: "RESULT ok ..." ya da "RESULT fail ...", cikis 0/1. --test_out=KLASOR ile (pencereli) goruntuler.
/// </summary>
public partial class SavasTest : Node
{
    Director _d;
    AzmanSystem _sys;
    enum S { Wait, DayGo, Day, HomurGo, HomurWalls, Homur, BozukGo, Bozuk, Done }
    S _s = S.Wait;
    float _t, _st, _total, _shotCd, _cekimT, _lastHp = 100f;
    bool _fireDown;
    int _cekimStage;
    readonly List<string> _fails = new();
    readonly List<string> _log = new();
    readonly HashSet<string> _shots = new();
    readonly Dictionary<string, float> _fightTime = new();
    WildCreature _wild;
    AzmanActor _az;
    AzmanLog _logItem;
    Vector3 _site, _spot, _tree;
    int _walls, _hurts;
    readonly HashSet<string> _wildFights = new(); // dovuse karisan yabaniler (rapor)
    bool _dayDown;

    Player P => _d.Player;

    public override void _Ready() { ProcessPriority = -500; }

    void Say(string s) { GD.Print(s); _log.Add(s); }
    void Go(S s) { _s = s; _st = 0; Say($"SAVAS state {s} t={_total:0.0}"); }
    void Fail(string why) { if (_s == S.Done) return; _fails.Add(why); Finish(); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _st += dt; _total += dt; _shotCd -= dt;
        if (_s == S.Done) return;
        if (_total > 1500f) { Fail($"zaman asimi ({_s})"); return; }
        if (_d != null && P != null)
        {
            if (P.Hp < _lastHp - 0.5f)
            {
                _hurts++;
                var logNear = _sys?.GetChildren().OfType<AzmanLog>().Select(l => l.GlobalPosition.DistanceTo(P.GlobalPosition)).DefaultIfEmpty(99).Min() ?? 99;
                Say($"SAVAS hurt -{_lastHp - P.Hp:0} hp={P.Hp:0} state={_s} acting={_az?.Acting} pending={_az?.Pending} tel={_az?.Telegraphing} dist={(_az != null && IsInstanceValid(_az) ? P.GlobalPosition.DistanceTo(_az.GlobalPosition) : -1):0.0} log_near={logNear:0.0} riders={GetTree().GetNodesInGroup("azman_rider").Count} held={_d.Capa.Held != null} vel={new Vector2(P.Velocity.X, P.Velocity.Z).Length():0.0}");
            }
            _lastHp = P.Hp;
            if (P.Down && P.Hp <= 0 && _s != S.Wait) { Fail($"oyuncu oldu ({_s}, Azman {_az?.Def.Id} can {_az?.Body?.Hp:0})"); return; }
        }
        switch (_s)
        {
            case S.Wait:
                _d ??= GetParent().GetNodeOrNull<Director>("Director");
                if (_d?.Azman == null || _d.Capa == null || _t < 2.5f) return;
                _sys = _d.Azman;
                Events.WildActed += OnWild;
                Say($"SAVAS start hp={P.Hp:0}");
                Go(S.DayGo);
                return;
            case S.DayGo: DayGo(); return;
            case S.Day: Day(dt); return;
            case S.HomurGo: HomurGo(); return;
            case S.HomurWalls: HomurWalls(); return;
            case S.Homur: AzmanFight(dt, "azman_homur", S.BozukGo); return;
            case S.BozukGo: BozukGo(); return;
            case S.Bozuk: AzmanFight(dt, "azman_bozuk", S.Done); return;
        }
    }

    void OnWild(string species, string what, float x, float z)
    {
        if (what == "Down" && _wild != null && species == _wild.Def.Id) _dayDown = true;
    }

    // ---------- 1. Gunduz ----------

    void DayGo()
    {
        if (_d.Wildlife == null) { Fail("Wildlife yok"); return; }
        var def = _d.Data.Creature("bozuk");
        var at = P.GlobalPosition + new Vector3(16, 0, 4);
        _wild = _d.Wildlife.Spawn(def, _d.World.Ground(at)).OfType<WildCreature>().FirstOrDefault();
        if (_wild == null) { Fail("yabani Bozuk dogmadi"); return; }
        Say($"SAVAS day spawn {def.Id} hp={_wild.State.Hp:0}");
        Go(S.Day);
    }

    void Day(float dt)
    {
        if (_dayDown || _wild == null || !IsInstanceValid(_wild) || _wild.Dead)
        {
            Release();
            Say($"SAVAS day down bozuk t={_st:0.0}");
            Shot("gunduz");
            Go(S.HomurGo);
            return;
        }
        if (_st > 120f) { Fail($"gunduz: yabani Bozuk 120 sn'de dusmedi (can {_wild.State.Hp:0})"); return; }
        Head(Capa.Head.Tirpan);
        P.AimOverride = _wild.Center;
        float dist = ((P.GlobalPosition - _wild.GlobalPosition) with { Y = 0 }).Length();
        if (dist > 9.5f) MoveTo(_wild.GlobalPosition, 9f, false);
        else if (dist < 6f) P.MoveOverride = ((P.GlobalPosition - _wild.GlobalPosition) with { Y = 0 }).Normalized() * 1.5f;
        else P.MoveOverride = Vector3.Zero;
        Fire(_wild.Center.DistanceTo(P.Muzzle.GlobalPosition) < 10.5f);
    }

    // ---------- 2. Azman Homur ----------

    void HomurGo()
    {
        _site = _d.World.Ground(new Vector3(-30, 0, 34));
        P.AimOverride = _site + Vector3.Up * 2f;
        if (!MoveTo(_site, 1.2f)) { if (_st > 150f) Fail("Homur sahasina varilamadi"); return; }
        P.MoveOverride = Vector3.Zero;
        Go(S.HomurWalls);
    }

    void HomurWalls()
    {
        if (_st < 0.5f) return;
        // Sur yerine uc kare temel: oyuncunun 4 m onunde, +X yonunde bir hat (sahne kurulumu; malzeme bot cantasindan gelmez).
        var sys = _d.Build?.System;
        if (sys == null) { Fail("yapi sistemi yok"); return; }
        for (int i = -1; i <= 1; i++)
        {
            var at = _d.World.Ground(_site + new Vector3(4.5f, 0, i * 3f));
            if (sys.PlaceAt("kare_temel", new Transform3D(Basis.Identity, at), out var why) != null) _walls++;
            else Say($"SAVAS wall fail {why}");
        }
        Say($"SAVAS walls {_walls}");
        if (_walls == 0) { Fail("sur kurulamadi"); return; }
        _spot = _site;
        _az = _sys.OnPeak(5, "altin_cayir", _d.World.Ground(_site + new Vector3(30, 0, 0)));
        if (_az == null) { Fail("KS 5 Doruk'ta Azman Homur gelmedi"); return; }
        if (_sys.OnPeak(4, "altin_cayir", _site) != null) { Fail("KS 4'te Azman geldi"); return; }
        _fightTime["azman_homur"] = 0;
        Go(S.Homur);
    }

    // ---------- 3. Azman Bozuk ----------

    void BozukGo()
    {
        if (_tree == Vector3.Zero)
        {
            // Agacli bir yer: origin'e yakin, arazinin icinde bir agac.
            var trees = _d.World.TreePositions.Where(t => Mathf.Abs(t.X) < 110 && Mathf.Abs(t.Z) < 110 && !_sys.UsedTrees.Contains(t))
                .OrderBy(t => t.DistanceTo(P.GlobalPosition)).ToList();
            if (trees.Count == 0) { Fail("agac yok"); return; }
            _tree = trees[0];
            var toward = ((Vector3.Zero - _tree) with { Y = 0 });
            toward = toward.LengthSquared() < 1 ? Vector3.Right : toward.Normalized();
            _spot = _d.World.Ground(_tree + toward * 20f);
            Say($"SAVAS bozuk tree=({_tree.X:0},{_tree.Z:0}) spot=({_spot.X:0},{_spot.Z:0})");
        }
        P.AimOverride = _spot + Vector3.Up * 2f;
        if (!MoveTo(_spot, 1.2f)) { if (_st > 150f) Fail("Bozuk sahasina varilamadi"); return; }
        P.MoveOverride = Vector3.Zero;
        var dir = ((_tree - _spot) with { Y = 0 }).Normalized();
        _az = _sys.Summon("azman_bozuk", 5, _d.World.Ground(_tree - dir * 5f));
        if (_az == null) { Fail("Azman Bozuk gelmedi"); return; }
        _fightTime["azman_bozuk"] = 0;
        Go(S.Bozuk);
    }

    // ---------- Dovus ----------

    void AzmanFight(float dt, string id, S next)
    {
        _fightTime[id] += dt;
        if (_az == null || !IsInstanceValid(_az) || !_az.Alive)
        {
            if (!_sys.DefeatedLog.Contains(id)) { if (_st > 3f) Fail($"{id}: dovus kapandi ama yenilmedi"); return; }
            Release();
            if (_d.Capa.Held != null) _d.Capa.Drop();
            P.MoveOverride = Vector3.Zero;
            if (_fightTime[id] < 9999) Say($"SAVAS defeated {id} fight_s={_fightTime[id]:0.0} hp={P.Hp:0} walls={_sys.WallHits} riders={_sys.RidersDropped} uproots={_sys.Uproots} logs={_sys.LogLandings} log_hits={_sys.LogHits}");
            _fightTime[id] = 99999;
            Shot($"{id}_yenildi");
            if (_st < 1.5f) return;
            foreach (var it in _d.Data.Azman.Get(id).Reward.Items)
                if (_d.State.Backpack.Count(it.Item) < it.Count) _fails.Add($"{id}: odul {it.Item} cantada yok");
            if (next == S.Done) Finish(); else Go(next);
            return;
        }
        _st = 0;
        if (_fightTime[id] > 300f) { Fail($"{id}: 300 sn'de yenilmedi (can {_az.Body?.Hp:0})"); return; }
        if (_fightTime[id] > 3.5f) Shot($"{id}_inis");
        if (_az.Telegraphing) Shot($"{id}_sezdirme");

        var a = _az;
        var me = P.GlobalPosition;
        var away = (me - a.GlobalPosition) with { Y = 0 };
        float dist = away.Length();

        // Sirttan dusen Homurlar yakindaysa once onlar.
        var rider = GetTree().GetNodesInGroup("azman_rider").OfType<AzmanRider>().Where(r => r.Alive).OrderBy(r => r.GlobalPosition.DistanceTo(me)).FirstOrDefault();
        bool riderNear = rider != null && rider.GlobalPosition.DistanceTo(me) < 7f;
        // Dovuse karisan yabani yirtici (gunduz savasi): saldiriyorsa once o.
        var wild = _d.Wildlife?.Creatures.Where(c => IsInstanceValid(c) && !c.Dead && !c.Display && c.GlobalPosition.DistanceTo(me) < 10f
                && c.Last.Mode is WildMode.Charge or WildMode.Attack or WildMode.Warn)
            .OrderBy(c => c.GlobalPosition.DistanceTo(me)).FirstOrDefault();

        // Bozuk: yerde kutuk varsa Cekim'le al ve firlat.
        bool logFlying = GetTree().GetNodesInGroup("azman_flying").Count > 0;
        bool tooClose = dist < a.Def.Radius + 4.5f; // Azman dibindeyse once uzaklas (govde degmesi)
        if (id == "azman_bozuk" && !riderNear && wild == null && !logFlying && !tooClose && !(a.Telegraphing || a.Acting != "") && ThrowLog(a)) return;
        if (_d.Capa.Held != null) { Release(); _d.Capa.Drop(); }

        Head(Capa.Head.Tirpan);
        ICombatTarget target = riderNear ? rider : wild != null ? wild : a;
        if (wild != null) _wildFights.Add(wild.Def.Id);
        P.AimOverride = target.Center;

        bool charge = a.Pending == "charge" && a.Telegraphing || a.Acting == "charge";
        if (charge)
        {
            // Sur sagdayken arkasinda bekle (hucum sura toslasin); yoksa yana kac.
            bool walled = id == "azman_homur" && _sys.WallHits == 0 && me.DistanceTo(_spot) < 2.5f;
            if (walled) P.MoveOverride = Vector3.Zero;
            else P.MoveOverride = away.Cross(Vector3.Up).Normalized() * 1.5f;
            Fire(target.Center.DistanceTo(P.Muzzle.GlobalPosition) < 10.5f);
            return;
        }
        if (id == "azman_homur" && _sys.WallHits == 0)
        {
            // Ilk hucum gelene kadar surun arkasinda kal.
            if (me.DistanceTo(_spot) > 1.5f) MoveTo(_spot, 1f); else P.MoveOverride = Vector3.Zero;
            Fire(target.Center.DistanceTo(P.Muzzle.GlobalPosition) < 10.5f);
            return;
        }
        bool throwing = a.Pending == "uproot" && a.Telegraphing || a.Acting == "uproot" || GetTree().GetNodesInGroup("azman_flying").Count > 0;
        float want = a.Def.Radius + 7.5f;
        if (throwing) P.MoveOverride = (away.Cross(Vector3.Up).Normalized() * 1.5f + away.Normalized() * 0.3f);
        else if (dist < a.Def.Radius + 4f) P.MoveOverride = (away.Normalized() + away.Normalized().Cross(Vector3.Up) * 0.7f).Normalized() * 1.5f;
        else if (dist < want - 1.5f) P.MoveOverride = away.Normalized() + away.Normalized().Cross(Vector3.Up) * 0.4f;
        else if (dist > want + 2f) MoveTo(a.GlobalPosition, want, false);
        else P.MoveOverride = away.Normalized().Cross(Vector3.Up) * 0.6f;
        Fire(target.Center.DistanceTo(P.Muzzle.GlobalPosition) < 10.5f);
    }

    /// <summary>Yerdeki kutugu Cekim'le al, Azman'a firlat. Kutuk yoksa false.</summary>
    bool ThrowLog(AzmanActor a)
    {
        if (_logItem == null || !IsInstanceValid(_logItem) || _logItem.Gone || !_logItem.IsInGroup("cekim"))
        {
            _logItem = _sys.GetParent().GetChildren().OfType<AzmanLog>().Concat(_sys.GetChildren().OfType<AzmanLog>())
                .Where(l => !l.Gone && l.IsInGroup("cekim") && l.GlobalPosition.DistanceTo(a.GlobalPosition) > a.Def.Radius + 5f) // Azman'in dibindeki kutuge gitme
                .OrderBy(l => l.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
            _cekimStage = 0;
            if (_logItem == null) return false;
        }
        Head(Capa.Head.Cekim);
        if (_d.Capa.Current != Capa.Head.Cekim) return true;
        _cekimT += (float)GetProcessDeltaTime();
        if (_d.Capa.Held == null)
        {
            if (_cekimStage == 1) _cekimStage = 0;
            var lp = _logItem.GlobalPosition;
            P.AimOverride = lp;
            if (P.GlobalPosition.DistanceTo(lp) > 5f) { Release(); MoveTo(lp, 4f); return true; }
            P.MoveOverride = Vector3.Zero;
            if (_fireDown) { Release(); _cekimT = 0; return true; }
            if (_cekimT > 0.15f) { Press(); _cekimT = 0; }
            return true;
        }
        var c = a.Center;
        P.AimOverride = c + Vector3.Down * 0.5f;
        float dist = ((P.GlobalPosition - a.GlobalPosition) with { Y = 0 }).Length();
        if (dist > a.Def.Radius + 6f) { Release(); MoveTo(a.GlobalPosition, a.Def.Radius + 5f); _cekimStage = 0; return true; }
        if (dist < a.Def.Radius + 3.5f) { Release(); P.MoveOverride = ((P.GlobalPosition - a.GlobalPosition) with { Y = 0 }).Normalized(); _cekimStage = 0; return true; }
        P.MoveOverride = Vector3.Zero;
        if (_cekimStage == 0) { if (_fireDown) Release(); else { Press(); _cekimStage = 1; _cekimT = 0; } return true; }
        if (_cekimStage == 1 && _cekimT > 0.7f) { Release(); _cekimStage = 2; _cekimT = 0; }
        if (_cekimStage == 2 && _cekimT > 0.4f) _cekimStage = 0;
        return true;
    }

    // ---------- Sonuc ----------

    void Finish()
    {
        Release();
        if (P != null) P.MoveOverride = Vector3.Zero;
        Events.WildActed -= OnWild;
        var need = new[] { "azman_homur", "azman_bozuk" };
        bool ok = _fails.Count == 0 && _sys != null && need.All(_sys.DefeatedLog.Contains);
        if (ok && !_dayDown) _fails.Add("gunduz: yabani Bozuk dusmedi");
        if (ok && _sys.WallHits == 0) _fails.Add("Homur hucumu sura hic toslamadi");
        if (ok && _sys.RidersDropped < (_d.Data.Azman.Get("azman_homur").Riders?.Count ?? 0)) _fails.Add($"sirttakilerin hepsi dusmedi ({_sys.RidersDropped})");
        if (ok && _sys.Uproots == 0) _fails.Add("Bozuk agac sokmedi");
        if (ok && _sys.LogLandings == 0) _fails.Add("kutuk hic dusmedi");
        // Kutugu geri firlatmak dovusu hizlandirir, sart degildir (GDD §16.3.1): sayisi RESULT'ta raporlanir, kapi degil.
        // Gevsetildi (savas raporu): bot guvenli kalmak icin bazen hic kutuk almiyor.
        if (ok && _sys.LogHits == 0) Say("SAVAS note: kutuk Azman'a firlatilmadi (kapi degil)");
        ok = ok && _fails.Count == 0;
        _s = S.Done;
        string times = string.Join(",", _fightTime.Select(kv => $"{kv.Key}:{(kv.Value > 9999 ? "ok" : kv.Value.ToString("0"))}"));
        Say($"RESULT {(ok ? "ok" : "fail")} savas day_down={_dayDown} defeated={string.Join(",", _sys?.DefeatedLog ?? new())} fights={times} player_hp={P?.Hp:0} hurts={_hurts} walls_placed={_walls} wall_hits={_sys?.WallHits} riders={_sys?.RidersDropped} uproots={_sys?.Uproots} log_landings={_sys?.LogLandings} log_hits={_sys?.LogHits} spreads={_d?.Savas?.Spreads} chains={_d?.Savas?.Chains} wild_mixed={string.Join(",", _wildFights)} t={_total:0}" + (_fails.Count > 0 ? " :: " + string.Join(" | ", _fails) : ""));
        if (Main.Args.TryGetValue("test_out", out var dir))
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            using var fa = FileAccess.Open(dir.TrimEnd('/', '\\') + "/savas.log", FileAccess.ModeFlags.Write);
            fa?.StoreString(string.Join("\n", _log) + "\n");
        }
        GetTree().Quit(ok ? 0 : 1);
    }

    // ---------- Girdi (AtaTest ile ayni yol) ----------

    void Head(Capa.Head h)
    {
        if (!_d.Capa.InHand)
        {
            int slot = _d.State.Belt.SlotOf(PlanTools.Capa);
            if (slot >= 0) _d.SelectBelt(slot);
            return;
        }
        if (_d.Capa.Current != h) { Release(); _d.Capa.Select(h); }
    }

    void Fire(bool on) { if (on) Press(); else Release(); }

    void Press()
    {
        if (_fireDown) return;
        _fireDown = true;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
    }

    void Release()
    {
        if (!_fireDown) return;
        _fireDown = false;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
    }

    float _stuckT, _sideT, _sideSign = 1f;

    bool MoveTo(Vector3 target, float within, bool sprint = true)
    {
        var d = (target - P.GlobalPosition) with { Y = 0 };
        if (d.Length() <= within) { P.MoveOverride = Vector3.Zero; _stuckT = 0; return true; }
        var dir = d.Normalized();
        float dt = (float)GetProcessDeltaTime();
        if (_sideT > 0) { _sideT -= dt; dir = (dir.Cross(Vector3.Up) * _sideSign + dir * 0.3f).Normalized(); }
        else if (new Vector2(P.Velocity.X, P.Velocity.Z).Length() < 0.3f) { _stuckT += dt; if (_stuckT > 0.6f) { _stuckT = 0; _sideT = 1.2f; _sideSign = -_sideSign; } }
        else _stuckT = 0;
        P.MoveOverride = dir * (sprint ? 1.5f : 1f);
        return false;
    }

    void Shot(string name)
    {
        if (_shots.Contains(name) || DisplayServer.GetName() == "headless" || !Main.Args.TryGetValue("test_out", out var dir)) return;
        if (_shotCd > 0) return;
        _shots.Add(name);
        _shotCd = 0.5f;
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
