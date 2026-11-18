using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=boss (boss akisi, GDD §18.1): bot iki Ata'yi bulur ve yener. Oyuncunun yolu: yururek (MoveOverride) Ata
/// Tasi'na gider, sunakta etkilesir (adak eksik: bilesenler M2'de baska akislarda), Ata'yi F1 gelistirici cagirmasiyla
/// indirir; dovus gercek girdiyle: nisan (AimOverride), tetik (sentetik fare olayi, InputMap "fire"), zipla (InputMap
/// "jump"), baslik secimi (Capa.Select, kemer). Esya, can ya da hasar verilmez; olurse basarisiz.
/// Koca Pofuduk: Tirpan; yunu Bicme ile kirkar; ciplak asamada sutunun arkasina gecip hucumu sutuna toslatir.
/// Boynuzlu Dede: Tirpan; kok dalgasinda ziplar, kovanlari vurur; kirik boynuz parcalarini Cekim'le firlatir.
/// Sonuc: "RESULT ok ..." ya da "RESULT fail ...", cikis 0/1. --test_out=KLASOR ile (pencereli) goruntuler.
/// </summary>
public partial class AtaTest : Node
{
    Director _d;
    AtaSystem _sys;
    readonly string[] _order = { "koca_pofuduk", "boynuzlu_dede" };
    int _idx;
    enum S { Wait, Walk, Altar, Fight, After, Done }
    S _s = S.Wait;
    float _t, _st, _total, _jumpT, _cekimT, _shotCd;
    bool _fireDown, _jumpDown;
    readonly List<string> _fails = new();
    readonly List<string> _log = new();
    readonly Dictionary<string, float> _fightTime = new();
    float _hpAtStart, _lastHp = 100f;
    int _jumps;
    int _cekimStage; // 0 git/tut, 1 tutuyor (firlat), 2 bekle
    AntlerShard _shard;
    readonly HashSet<string> _shots = new();

    Player P => _d.Player;
    AtaDef Cur => _sys.Def(_order[_idx]);

    public override void _Ready() { ProcessPriority = -500; }

    void Say(string s) { GD.Print(s); _log.Add(s); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt; _st += dt; _total += dt; _shotCd -= dt;
        if (_s == S.Done) return;
        if (_total > 1500f) { Fail($"zaman asimi ({_s}, {Cur.Id})"); return; }
        switch (_s)
        {
            case S.Wait:
                _d ??= GetParent().GetNodeOrNull<Director>("Director");
                if (_d?.Atalar == null || _d.Capa == null || _t < 2.5f) return;
                _sys = _d.Atalar;
                _hpAtStart = P.Hp;
                Say($"BOSS start hp={P.Hp:0} arenas={_sys.Progress.Records.Count}");
                Go(S.Walk);
                return;
            case S.Walk: Walk(dt); return;
            case S.Altar: Altar(); return;
            case S.Fight: FightTick(dt); return;
            case S.After: After(); return;
        }
    }

    void Go(S s) { _s = s; _st = 0; Say($"BOSS state {s} {(_idx < _order.Length ? _order[_idx] : "-")} t={_total:0.0}"); }

    void Fail(string why)
    {
        if (_s == S.Done) return;
        _fails.Add(why);
        Finish();
    }

    // ---------- Bulma ----------

    void Walk(float dt)
    {
        var stand = _sys.AltarPos(Cur);
        var toArena = (_sys.ArenaCenter(Cur) - stand) with { Y = 0 };
        var spot = stand + toArena.Normalized() * 1.2f; // sunagin arena tarafi
        P.AimOverride = _sys.StonePos(Cur) + Vector3.Up * 2f;
        if (MoveTo(spot, 1.6f)) { P.MoveOverride = Vector3.Zero; Go(S.Altar); }
        if (_st > 240f) Fail($"{Cur.Id}: Ata Tasi'na varilamadi (uzaklik {P.GlobalPosition.DistanceTo(spot):0.0} m)");
    }

    void Altar()
    {
        if (_st < 0.5f) return;
        if (_st < 0.6f)
        {
            // Gercek etkilesim tusu: adak eksik (Yun, Demir, Recine, kabarik urun M2'de baska akislarda).
            Input.ActionPress("interact");
            return;
        }
        if (_st < 0.8f) { Input.ActionRelease("interact"); return; }
        Shot($"{Cur.Id}_tas");
        if (_st < 1.2f) return;
        Say($"BOSS altar {Cur.Id} offering='{_sys.OfferingText(Cur)}' complete={AtaRules.OfferingComplete(_sys.Progress, Cur)}");
        if (!_sys.DevSummon(Cur.Id)) { Fail($"{Cur.Id}: cagrilamadi"); return; }
        _fightTime[Cur.Id] = 0;
        _cekimStage = 0; _shard = null;
        Go(S.Fight);
    }

    // ---------- Dovus ----------

    void FightTick(float dt)
    {
        _fightTime[Cur.Id] += dt;
        if (P.Hp < _lastHp - 0.5f) Say($"BOSS hurt -{_lastHp - P.Hp:0} hp={P.Hp:0} phase={_sys.Fight?.Phase + 1} tel={_sys.Actor?.TelegraphKind} charging={_sys.Actor?.Charging} adds={_sys.Targets.OfType<AtaMinion>().Count()} waves={GetTree().GetNodesInGroup("ata_rootwave").Count} dist={(_sys.Actor != null ? P.GlobalPosition.DistanceTo(_sys.Actor.GlobalPosition) : 0):0.0}");
        _lastHp = P.Hp;
        if (P.Down && P.Hp <= 0) { Fail($"{Cur.Id}: oyuncu oldu (asama {_sys.Fight?.Phase + 1}, Ata cani {_sys.Fight?.Hp:0})"); return; }
        var f = _sys.Fight;
        if (f == null || _sys.Actor == null)
        {
            if (_sys.Progress.IsDefeated(Cur.Id)) { Release(); Go(S.After); return; }
            if (_st > 5f) { Fail($"{Cur.Id}: dovus kapandi ama yenilmedi"); return; }
            return;
        }
        if (_fightTime[Cur.Id] > 300f) { Fail($"{Cur.Id}: 300 sn'de yenilmedi (asama {f.Phase + 1}, can {f.Hp:0}/{f.MaxHp:0})"); return; }
        var a = _sys.Actor;
        if (f.Phase == 1 && f.ShieldT <= 0) Shot($"{Cur.Id}_asama2");
        if (f.Phase == 2 && f.ShieldT <= 0) Shot($"{Cur.Id}_asama3");
        if (_fightTime[Cur.Id] > 4f) Shot($"{Cur.Id}_inis");

        // Kok dalgasi: halka gelmek uzereyse yerinde zipla.
        if (DodgeRootWave(dt)) return;

        // Yakindaki yardimci once.
        var minion = _sys.Targets.OfType<AtaMinion>().Where(m => m.Alive).OrderBy(m => m.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        bool minionNear = minion != null && minion.GlobalPosition.DistanceTo(P.GlobalPosition) < 6f;

        if (a is KocaPofuduk && f.Armor > 0 && !minionNear) { Shear(a); return; }
        if (a is BoynuzluDede dede && f.Phase == 2 && !minionNear && Throw(dede)) return;
        if (_d.Capa.Held != null) { Release(); _d.Capa.Drop(); }

        Head(Capa.Head.Tirpan);
        IAtaTarget target = a;
        if (a is BoynuzluDede d2) target = d2.Hives.FirstOrDefault(h => h.Alive) as IAtaTarget ?? a;
        if (minionNear) target = minion;
        P.AimOverride = target.Center;

        var me = P.GlobalPosition;
        var away = (me - a.GlobalPosition) with { Y = 0 };
        float dist = away.Length();
        if (a.Telegraphing && a.TelegraphKind == "charge" || a.Charging)
        {
            if (a is KocaPofuduk && f.Phase == 2 && PillarCover(a)) { Fire(dist < 11f); return; }
            // Hucumdan yana kac
            var side = away.Cross(Vector3.Up).Normalized();
            P.MoveOverride = side * 1.5f;
            Fire(dist < 11f);
            return;
        }
        if (a is KocaPofuduk && f.Phase == 2 && PillarCover(a)) { Fire(dist < 11f); return; }
        float want = a.Def.Radius + 6.5f;
        if (dist < a.Def.Radius + 3.5f) Kite(a);
        else if (dist < want - 1.5f) P.MoveOverride = away.Normalized() + away.Normalized().Cross(Vector3.Up) * 0.4f;
        else if (dist > want + 2f) MoveTo(a.GlobalPosition, want, false);
        else P.MoveOverride = away.Normalized().Cross(Vector3.Up) * 0.6f; // daire ciz: yumaklardan kac
        KeepInArena();
        Fire(target.Center.DistanceTo(P.Muzzle.GlobalPosition) < 10.5f);
    }

    /// <summary>Ciplak Koca Pofuduk: en yakin sutunun arkasinda dur; hucum sutuna toslasin.</summary>
    bool PillarCover(AtaActor a)
    {
        var pillars = _sys.Pillars(a.Def);
        if (pillars.Count == 0) return false;
        var me = P.GlobalPosition;
        var pil = pillars.OrderBy(p => p.DistanceTo(me) + p.DistanceTo(a.GlobalPosition) * 0.5f).First();
        var behind = pil + ((pil - a.GlobalPosition) with { Y = 0 }).Normalized() * (a.Def.Arena.PillarRadius + 1.6f);
        Head(Capa.Head.Tirpan);
        P.AimOverride = a.Center;
        float dist = ((me - a.GlobalPosition) with { Y = 0 }).Length();
        if (a.Charging) P.MoveOverride = Vector3.Zero;
        else if (dist < a.Def.Radius + 3.5f && !a.Telegraphing) Kite(a); // yuruyerek geliyor: sutunun cevresinden uzaklas
        else MoveTo(behind, 0.6f, true);
        return true;
    }

    /// <summary>Yun kirkma: nisan Ata'nin bize bakan yuzunde (oyuncunun nisan isini oradaki govdeye carpar), Bicme menzilinde.</summary>
    void Shear(AtaActor a)
    {
        Head(Capa.Head.Bicme);
        var away = (P.GlobalPosition - a.GlobalPosition) with { Y = 0 };
        float dist = away.Length();
        P.AimOverride = a.Center + away.Normalized() * a.Def.Radius * 0.9f + Vector3.Down * (a.Def.Height * 0.2f);
        float muzzleToAim = P.Muzzle.GlobalPosition.DistanceTo(P.AimOverride.Value);
        float reach = _d.Data.Tool.MowRange - 0.4f;
        if (a.Telegraphing || a.Charging) P.MoveOverride = away.Cross(Vector3.Up).Normalized() * 1.5f + away.Normalized() * 0.5f;
        else if (dist < a.Def.Radius + 2.6f) P.MoveOverride = away.Normalized();
        else if (muzzleToAim > reach) MoveTo(a.GlobalPosition, a.Def.Radius + 2.8f);
        else P.MoveOverride = away.Normalized().Cross(Vector3.Up) * 0.4f;
        Fire(muzzleToAim < reach + 0.3f);
    }

    /// <summary>Ata yuruyerek yaklasirken uzaklas: geri ve yana (daire), arenada kal.</summary>
    void Kite(AtaActor a)
    {
        var away = (P.GlobalPosition - a.GlobalPosition) with { Y = 0 };
        P.MoveOverride = (away.Normalized() + away.Normalized().Cross(Vector3.Up) * 0.7f).Normalized() * 1.5f;
        KeepInArena();
    }

    /// <summary>Dede asama 3: yerdeki boynuz parcasini Cekim'le al, Dede'ye firlat. Parca kalmadiysa false.</summary>
    bool Throw(BoynuzluDede dede)
    {
        if (_shard == null || !IsInstanceValid(_shard) || _shard.Gone)
        {
            _shard = _sys.GetChildren().OfType<AntlerShard>().Where(s => !s.Gone).OrderBy(s => s.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
            _cekimStage = 0;
            if (_shard == null) return false;
        }
        Head(Capa.Head.Cekim);
        if (_d.Capa.Current != Capa.Head.Cekim) return true;
        if (dede.Telegraphing && dede.TelegraphKind == "charge") { Release(); P.MoveOverride = ((P.GlobalPosition - dede.GlobalPosition) with { Y = 0 }).Cross(Vector3.Up).Normalized() * 1.5f; return true; }
        _cekimT += (float)GetProcessDeltaTime();
        if (_d.Capa.Held == null)
        {
            if (_cekimStage == 1) { _cekimStage = 0; } // atis bitti
            var sp = _shard.GlobalPosition;
            P.AimOverride = sp;
            if (P.GlobalPosition.DistanceTo(sp) > 5f) { Release(); MoveTo(sp, 4f); return true; }
            P.MoveOverride = Vector3.Zero;
            // Tutmak icin yeni basis: once birak, sonra bas.
            if (_fireDown) { Release(); _cekimT = 0; return true; }
            if (_cekimT > 0.15f) { Press(); _cekimT = 0; }
            return true;
        }
        // Tutuyor: Dede'ye 6-8 m'ye gel, nisan al, basili tut ve birak (firlat).
        var c = dede.Center;
        P.AimOverride = c + Vector3.Down * 0.5f;
        float dist = ((P.GlobalPosition - dede.GlobalPosition) with { Y = 0 }).Length();
        if (dist > dede.Def.Radius + 6f) { Release(); MoveTo(dede.GlobalPosition, dede.Def.Radius + 5f); _cekimStage = 0; return true; }
        if (dist < dede.Def.Radius + 3.5f) { Release(); P.MoveOverride = ((P.GlobalPosition - dede.GlobalPosition) with { Y = 0 }).Normalized(); _cekimStage = 0; return true; }
        P.MoveOverride = Vector3.Zero;
        if (_cekimStage == 0) { if (_fireDown) Release(); else { Press(); _cekimStage = 1; _cekimT = 0; } return true; }
        if (_cekimStage == 1 && _cekimT > 0.7f) { Release(); _cekimStage = 2; _cekimT = 0; }
        if (_cekimStage == 2 && _cekimT > 0.4f) _cekimStage = 0;
        return true;
    }

    bool DodgeRootWave(float dt)
    {
        if (_jumpDown) { Input.ActionRelease("jump"); _jumpDown = false; }
        _jumpT -= dt;
        var waves = GetTree().GetNodesInGroup("ata_rootwave").OfType<RootWave>().Where(w => !w.Done).ToList();
        foreach (var w in waves)
        {
            float gap = w.GapToPlayer;
            if (gap > 0 && gap < w.Speed * 0.22f + 0.8f && _jumpT <= 0 && P.IsOnFloor())
            {
                P.MoveOverride = null; // Player: zipla yalniz kendi girdisiyle (MoveOverride bos)
                Input.ActionPress("jump");
                _jumpDown = true;
                _jumps++;
                _jumpT = 0.6f;
                return true;
            }
        }
        if (_jumpT > 0.3f) { P.MoveOverride = null; return true; } // havadayken yon verme
        return false;
    }

    void KeepInArena()
    {
        var c = _sys.ArenaCenter(Cur);
        var off = (P.GlobalPosition - c) with { Y = 0 };
        if (off.Length() > Cur.Arena.Radius - 1f && P.MoveOverride is Vector3 mv && mv.Dot(off) > 0) P.MoveOverride = (mv - off.Normalized() * mv.Dot(off.Normalized()) * 1.5f);
    }

    // ---------- Sonra ----------

    void After()
    {
        P.MoveOverride = Vector3.Zero;
        if (_st < 1.5f) return;
        Shot($"{Cur.Id}_yenildi");
        if (_st < 2.5f) return;
        var bag = _d.State.Backpack;
        foreach (var it in Cur.Reward.Items)
            if (bag.Count(it.Item) < it.Count) _fails.Add($"{Cur.Id}: odul {it.Item} cantada yok ({bag.Count(it.Item)})");
        if (!_sys.Progress.HasSeal(Cur.Reward.Seal)) _fails.Add($"{Cur.Id}: muhur {Cur.Reward.Seal} yok");
        foreach (var u in Cur.Reward.Unlocks) if (!_sys.Progress.IsUnlocked(u)) _fails.Add($"{Cur.Id}: {u} acilmadi");
        Say($"BOSS defeated {Cur.Id} fight_s={_fightTime[Cur.Id]:0.0} hp={P.Hp:0}/{P.MaxHp:0} shears={_sys.Shears} pillar_stuns={_sys.PillarStuns} shard_hits={_sys.ShardHits}");
        _idx++;
        if (_idx >= _order.Length) { Finish(); return; }
        Go(S.Walk);
    }

    void Finish()
    {
        Release();
        if (_jumpDown) Input.ActionRelease("jump");
        if (P != null) P.MoveOverride = Vector3.Zero;
        bool ok = _fails.Count == 0 && _sys != null && _order.All(_sys.Progress.IsDefeated);
        if (ok && _sys.Shears == 0) _fails.Add("Bicme kirkmasi hic olmadi");
        if (ok && _sys.ShardHits == 0) _fails.Add("boynuz parcasi hic isabet etmedi");
        ok = _fails.Count == 0 && ok;
        _s = S.Done;
        string times = string.Join(",", _fightTime.Select(kv => $"{kv.Key}:{kv.Value:0}"));
        Say($"RESULT {(ok ? "ok" : "fail")} boss defeated={string.Join(",", _sys?.DefeatedLog ?? new())} fights={times} player_hp={P?.Hp:0} shears={_sys?.Shears} pillar_stuns={_sys?.PillarStuns} shard_hits={_sys?.ShardHits} wave_jumps={_jumps} seals={string.Join(",", _sys?.Progress.Seals ?? new())} t={_total:0}" + (_fails.Count > 0 ? " :: " + string.Join(" | ", _fails) : ""));
        if (Main.Args.TryGetValue("test_out", out var dir))
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            using var fa = FileAccess.Open(dir.TrimEnd('/', '\\') + "/boss.log", FileAccess.ModeFlags.Write);
            fa?.StoreString(string.Join("\n", _log) + "\n");
        }
        GetTree().Quit(ok ? 0 : 1);
    }

    // ---------- Girdi ----------

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
