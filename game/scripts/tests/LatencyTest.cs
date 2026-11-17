using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// His olcutleri sinamasi (--test=latency; GDD 4, URETIM 6 M1 kabul, TEKNIK 11 "His olcutleri").
///
/// Girdi -> tepki: alti is basligi ve iki silah icin gercek girdi yolundan (Input.ParseInputEvent -> InputMap "fire" ->
/// Director.ReadInput -> Capa.Process) basis ile ilk gorsel tepki (isin, puskurtme, mermi, tutma, yerlestirme) arasini
/// olcer. Bot yok: tetik oyuncu girdisinden gelir. Kapi kare sayisindadir (belirlenimci): tepki girdinin oyuna
/// girdigi ilk karede olmali ve (kare farki + cizim karesi) x kare suresi <= 50 ms (feel.json criteria, 60 FPS).
/// Duvar saati (giristen tepkili karenin sonuna) bilgi icin yazilir: basliksiz kosuda cizim olmadigindan
/// "motor tarafi" suredir, ekrandaki sure degildir; ekran onayi sahibin donaniminda (RTX 2070S).
///
/// Vurus duraklamasi: Tirpan ve Civi gercek isabetle (Zipzip'e mermi) ve kritik degeriyle; olculen gercek sure
/// istenenin bir iki kare icinde ve istenen 30-80 ms araliginda olmali. Ayrica: Civi yari otomatik (basili tutmak tek
/// civi), Tirpan tam otomatik, Cekim 80 kg siniri ve firlatma.
/// Sonuc: "RESULT ok ..." ya da "RESULT fail ...", cikis kodu 0/1. --test_out=KLASOR verilirse latency.log yazilir.
/// </summary>
public partial class LatencyTest : Node
{
    const int Presses = 8;
    const double TimeoutSeconds = 240;
    Director _d;
    FeelCriteria _c;
    float _frameMs;
    readonly List<ReactionSample> _samples = new();
    readonly List<string> _log = new();
    readonly List<string> _fails = new();
    readonly Dictionary<string, (double budget, double wallMed, double wallMax, ulong framesMax)> _heads = new();
    ulong _startUsec;
    bool _done;

    public override void _Ready()
    {
        ProcessPriority = -1000;
        _startUsec = Time.GetTicksUsec();
        CallDeferred(nameof(Begin));
    }

    async void Begin()
    {
        try { await Run(); }
        catch (Exception e) { _fails.Add("istisna: " + e.Message); Say(e.ToString()); }
        Finish();
    }

    public override void _Process(double delta)
    {
        if (!_done && (Time.GetTicksUsec() - _startUsec) / 1e6 > TimeoutSeconds) { _fails.Add($"zaman asimi {TimeoutSeconds} sn"); Finish(); }
    }

    void Say(string s) { GD.Print(s); _log.Add(s); }

    async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task Run()
    {
        while ((_d = GetParent().GetNodeOrNull<Director>("Director")) == null || _d.Capa == null) await Frames(1);
        _c = _d.Feel.Data.Criteria;
        Engine.MaxFps = (int)_c.TargetFps;
        _frameMs = 1000f / _c.TargetFps;
        _d.FeelPanel.Reacted += s => _samples.Add(s);
        Say($"LATENCY display={DisplayServer.GetName()} fps_cap={Engine.MaxFps} frame_ms={_frameMs:0.0} present_frames={_c.PresentFrames} budget<={_c.MaxLatencyMs} ms");
        await Frames(90);

        var P = _d.Player;
        var away = _d.World.Ground(new Vector3(-14, 0, 6));
        foreach (Capa.Head h in Enum.GetValues(typeof(Capa.Head)))
        {
            Place(h, out var stand, out var aim);
            P.GlobalPosition = _d.World.Ground(stand) + Vector3.Up * 0.2f;
            P.AimOverride = aim;
            _d.Capa.Select(h);
            await Frames(25);
            if (_d.Capa.Current != h) { _fails.Add($"{h}: secilemedi"); continue; }
            await MeasureHead(h, aim);
        }

        await CarryChecks();
        await TriggerModes();
        await HitStops();
        SaveRoundTrip();
        await PanelShot();
    }

    /// <summary>
    /// F1 paneli kaydi: Set -> Save -> Reset -> LoadUser ayri bir dosyada (oyuncunun user://feel_tuning.json'una
    /// dokunmaz). Dosyada yalnizca degisen sayilar olmali; okununca ayni degerler donmeli. Dosya sonunda silinir.
    /// </summary>
    void SaveRoundTrip()
    {
        string dir = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') : "user://test";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));
        string path = dir + "/feel_tuning_test.json";
        var t = FeelTuning.ForFile(path);
        float civiDefault = t.Data.Source("civi").HitStopMs;
        t.Set((JsonObject)t.Doc["master"]!, "shake", 0.5);
        t.Set((JsonObject)((JsonObject)t.Doc["sources"]!)["tirpan"]!, "hitStopMs", 55);
        string msg = t.Save();
        t.Reset();
        bool reset = Math.Abs(t.Data.Master.Shake - 1f) < 1e-6;
        bool loaded = t.LoadUser();
        bool values = Math.Abs(t.Data.Master.Shake - 0.5f) < 1e-6 && t.Data.Source("tirpan").HitStopMs == 55f && t.Data.Source("civi").HitStopMs == civiDefault;
        var saved = FileAccess.FileExists(path) ? JsonNode.Parse(FileAccess.GetFileAsString(path)) as JsonObject : null;
        bool onlyDiff = saved != null && saved.Count == 2 && ((JsonObject)saved["master"]!).Count == 1 && ((JsonObject)saved["sources"]!).Count == 1;
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        bool ok = msg.StartsWith("Kaydedildi") && reset && loaded && values && onlyDiff;
        if (!ok) _fails.Add($"F1 kaydi: kayit='{msg}' sifirlama={reset} okuma={loaded} degerler={values} yalniz_fark={onlyDiff}");
        Say($"F1KAYIT kayit={msg.StartsWith("Kaydedildi")} sifirlama={reset} okuma={loaded} degerler={values} yalniz_fark={onlyDiff} {(ok ? "ok" : "FAIL")}");
    }

    /// <summary>F1 panelini gercek tusla acar, goruntusunu alir (pencereli kosuda), kapatir.</summary>
    async Task PanelShot()
    {
        if (DisplayServer.GetName() == "headless") return;
        static void F1(bool down) => Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.F1, Keycode = Key.F1, Pressed = down });
        F1(true); await Frames(1); F1(false);
        await Frames(4);
        if (!_d.FeelPanel.Open) { _fails.Add("F1 paneli acilmadi"); return; }
        Shot("f1_panel");
        F1(true); await Frames(1); F1(false);
        await Frames(2);
    }

    /// <summary>Basligin hedefini ve oyuncunun durdugu yeri kurar (kaya, agac, kabarik urun ...).</summary>
    void Place(Capa.Head h, out Vector3 stand, out Vector3 aim)
    {
        var w = _d.World;
        var basePos = w.Ground(new Vector3(-14, 0, 6));
        stand = basePos;
        aim = w.Ground(basePos + new Vector3(0, 0, 4f)) + Vector3.Up * 0.05f;
        switch (h)
        {
            case Capa.Head.Kazma:
                var rock = w.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 4 && r.Item == "stone").OrderBy(r => r.GlobalPosition.DistanceTo(basePos)).First();
                stand = rock.GlobalPosition + (basePos - rock.GlobalPosition).Normalized() * 4f;
                aim = rock.Center;
                break;
            case Capa.Head.Bicme:
                var t = w.TreePositions.OrderBy(x => x.DistanceTo(basePos)).First();
                stand = t + (basePos - t).Normalized() * 3f;
                aim = t + Vector3.Up * 1.4f;
                break;
            case Capa.Head.Cekim:
                _crop = Fixture("S", basePos + new Vector3(0, 0, 3.5f));
                aim = _crop.GlobalPosition;
                break;
        }
    }

    SwollenCrop _crop;

    SwollenCrop Fixture(string size, Vector3 at)
    {
        var def = _d.Data.Crops.SizeClasses.First(s => s.Id == size);
        var mesh = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f } };
        var sc = new SwollenCrop("turnip", def, mesh, null, def.Scale);
        _d.AddChild(sc);
        sc.GlobalPosition = _d.World.Ground(at) + Vector3.Up * sc.Radius * 0.6f;
        return sc;
    }

    /// <summary>Pencereli kosuda (--test_out ile) her basligin 3. basisinda tepkiden 2 kare sonra ekran goruntusu: vurus katmanlarinin kaniti.</summary>
    const int ShotPress = 2;

    void Shot(string head)
    {
        if (DisplayServer.GetName() == "headless" || !Main.Args.TryGetValue("test_out", out var dir)) return;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var path = $"{dir.TrimEnd('/', '\\')}/his_{head}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        Say($"SHOT {path}");
    }

    static void Fire(bool down) =>
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, ButtonMask = down ? MouseButtonMask.Left : 0 });

    /// <summary>Bir basis: giris kuyruga girer (zaman damgasi), tepki ya da 20 kare beklenir, birakilir.</summary>
    async Task<ReactionSample?> PressOnce(string head, int holdFrames = 3, bool shot = false)
    {
        _samples.Clear();
        _d.FeelPanel.MarkInput();
        Fire(true);
        ReactionSample? got = null;
        for (int i = 0; i < 20 && got == null; i++)
        {
            await Frames(1);
            got = _samples.Where(s => s.Head == head).Cast<ReactionSample?>().FirstOrDefault();
        }
        if (shot && got != null) { await Frames(2); Shot(head); holdFrames = Math.Max(0, holdFrames - 2); }
        if (holdFrames > 0) await Frames(holdFrames);
        Fire(false);
        await Frames(2);
        return got;
    }

    async Task MeasureHead(Capa.Head h, Vector3 aim)
    {
        string id = Capa.HeadId(h);
        var got = new List<ReactionSample>();
        int gap = h switch
        {
            Capa.Head.Tirpan => Mathf.CeilToInt(_d.Data.Tool.Weapon.FireInterval * _c.TargetFps) + 4,
            Capa.Head.Civi => Mathf.CeilToInt(_d.Data.Tool.Nailgun.FireInterval * _c.TargetFps) + 4,
            _ => 6,
        };
        for (int i = 0; i < Presses; i++)
        {
            var s = await PressOnce(id, shot: i == ShotPress);
            if (s is ReactionSample r) got.Add(r);
            await Frames(gap);
        }
        if (h == Capa.Head.Cekim && _d.Capa.Held != null) { await PressOnce(id, 0); await Frames(6); }
        if (got.Count < Presses) { _fails.Add($"{id}: {Presses - got.Count}/{Presses} basista tepki yok"); }
        if (got.Count == 0) { Say($"LATENCY {id,-7} tepki yok"); return; }
        ulong fmax = got.Max(s => s.Frames);
        double budget = (fmax + _c.PresentFrames) * _frameMs;
        var walls = got.Select(s => s.Ms + (s.Drawn ? 0 : _c.PresentFrames * _frameMs)).OrderBy(x => x).ToList();
        double med = walls[walls.Count / 2], max = walls[^1];
        _heads[id] = (budget, med, max, fmax);
        bool ok = fmax <= 1 && budget <= _c.MaxLatencyMs;
        if (!ok) _fails.Add($"{id}: kare farki {fmax}, kare butcesi {budget:0.0} ms");
        Say($"LATENCY {id,-7} n={got.Count} frames_max={fmax} budget_ms={budget:0.0} wall_med_ms={med:0.0} wall_max_ms={max:0.0} {(ok ? "ok" : "FAIL")}");
        Say($"        {id} wall_ms=[{string.Join(", ", got.Select(s => (s.Ms + (s.Drawn ? 0 : _c.PresentFrames * _frameMs)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)))}] fps={Engine.GetFramesPerSecond():0} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} draw_calls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}");
    }

    /// <summary>Cekim: 80 kg siniri ve firlatma (URETIM 6 M1).</summary>
    async Task CarryChecks()
    {
        var P = _d.Player;
        var basePos = _d.World.Ground(new Vector3(-14, 0, 6));
        P.GlobalPosition = basePos + Vector3.Up * 0.2f;
        _d.Capa.Select(Capa.Head.Cekim);
        if (_d.Capa.Held != null) _d.Capa.Drop();
        var heavy = Fixture("M", basePos + new Vector3(3.5f, 0, 0));
        P.AimOverride = heavy.GlobalPosition;
        await Frames(25);
        await PressOnce("cekim");
        bool refused = _d.Capa.Held == null;
        Say($"CARRY agir={heavy.Size.Kg}kg sinir={_d.Data.Tool.Carry.MaxKg}kg tutulmadi={refused}");
        if (!refused) { _fails.Add("cekim: 80 kg ustu tutuldu"); _d.Capa.Drop(); }
        heavy.QueueFree();

        var light = _crop != null && IsInstanceValid(_crop) ? _crop : Fixture("S", basePos + new Vector3(0, 0, 3.5f));
        P.AimOverride = light.GlobalPosition;
        await Frames(10);
        await PressOnce("cekim");
        if (_d.Capa.Held == null) { _fails.Add("cekim: S urun tutulamadi"); return; }
        await Frames(20);
        var before = light.GlobalPosition;
        // Basili tut (yuklen), birak: firlat.
        Fire(true);
        await Frames(Mathf.CeilToInt(_d.Data.Tool.Carry.ChargeSeconds * _c.TargetFps) + 5);
        Fire(false);
        await Frames(90);
        float moved = new Vector2(light.GlobalPosition.X - before.X, light.GlobalPosition.Z - before.Z).Length();
        bool thrown = _d.Capa.Held == null && moved > 3f;
        Say($"CARRY firlatma yol={moved:0.0}m birakildi={_d.Capa.Held == null}");
        if (!thrown) _fails.Add($"cekim: firlatma {moved:0.0} m");
    }

    /// <summary>Civi yari otomatik (basili tutmak tek atis), Tirpan tam otomatik.</summary>
    async Task TriggerModes()
    {
        var P = _d.Player;
        P.AimOverride = P.GlobalPosition + new Vector3(0, 1, 10);
        foreach (var (h, semi) in new[] { (Capa.Head.Civi, true), (Capa.Head.Tirpan, false) })
        {
            _d.Capa.Select(h);
            await Frames(40);
            int before = _d.Capa.ShotsFired;
            int ammo = _d.State.Backpack.Count("civi");
            Fire(true);
            await Frames((int)_c.TargetFps); // 1 sn basili
            Fire(false);
            await Frames(3);
            int shots = _d.Capa.ShotsFired - before;
            if (h == Capa.Head.Civi && ammo - _d.State.Backpack.Count("civi") != shots) _fails.Add($"civi: cephane {ammo - _d.State.Backpack.Count("civi")}, atis {shots}");
            bool ok = semi ? shots == 1 : shots >= 2;
            Say($"TRIGGER {Capa.HeadId(h)} 1sn_basili_atis={shots} {(semi ? "yari otomatik" : "tam otomatik")} {(ok ? "ok" : "FAIL")}");
            if (!ok) _fails.Add($"{Capa.HeadId(h)}: 1 sn basili {shots} atis");
            await Frames(40);
        }
    }

    /// <summary>Vurus duraklamasi: gercek isabet (Tirpan, Civi) ve kritik degeri; olculen gercek sure.</summary>
    async Task HitStops()
    {
        var P = _d.Player;
        var basePos = _d.World.Ground(new Vector3(-14, 0, 6));
        P.GlobalPosition = basePos + Vector3.Up * 0.2f;
        var z = new Zipzip(_d.Data.Creature("zipzip"), _d, 1000f, 1f, swollen: false) { Display = true };
        _d.AddChild(z);
        z.GlobalPosition = _d.World.Ground(basePos + new Vector3(0, 0, 5f));
        await Frames(40);
        var ends = new List<(float req, float got, float frame)>();
        _d.HitStopEnded += (r, g, f) => ends.Add((r, g, f));
        foreach (var (h, src) in new[] { (Capa.Head.Tirpan, "tirpan"), (Capa.Head.Civi, "civi") })
        {
            _d.Capa.Select(h);
            P.AimOverride = z.Center;
            await Frames(40);
            // Isinma: ilk isabet yollari (JIT, ilk hasar sayisi) bir kez calissin; olculen sonraki isabet.
            for (int k = 0; k < 2; k++)
            {
                ends.Clear();
                float hp = z.Hp;
                Fire(true); await Frames(2); Fire(false);
                for (int i = 0; i < 120 && ends.Count == 0; i++) await Frames(1);
                bool hit = z.Hp < hp;
                if (k == 1) CheckStop($"{src} isabet", F(src).HitStopMs, hit ? ends : new());
                else Say($"HITSTOP {src} isinma: istenen={(ends.Count > 0 ? ends[0].req : 0):0.0}ms olculen={(ends.Count > 0 ? ends[0].got : 0):0.0}ms kare={(ends.Count > 0 ? ends[0].frame : 0):0.0}ms");
                await Frames(Mathf.CeilToInt(_d.Data.Tool.Weapon.FireInterval * _c.TargetFps) + 4);
            }
            ends.Clear();
            _d.HitStop(F(src).CritHitStopMs / 1000f * _d.Feel.Master.HitStop);
            await Frames(2);
            bool scaled = Mathf.IsEqualApprox((float)Engine.TimeScale, _d.Feel.Data.HitStop.TimeScale * _d.SpeedScale);
            for (int i = 0; i < 60 && ends.Count == 0; i++) await Frames(1);
            CheckStop($"{src} kritik", F(src).CritHitStopMs, ends);
            if (!scaled) _fails.Add($"{src}: duraklamada TimeScale {Engine.TimeScale}");
            await Frames(30);
        }
        z.QueueFree();
    }

    FeelSource F(string id) => _d.Feel.Data.Source(id);

    void CheckStop(string name, float cfgMs, List<(float req, float got, float frame)> ends)
    {
        if (ends.Count == 0) { _fails.Add($"{name}: duraklama olmadi"); Say($"HITSTOP {name} yok FAIL"); return; }
        var (req, got, frame) = ends[0];
        const float tol = 0.5f;
        bool inRange = req >= _c.HitStopMinMs - tol && req <= _c.HitStopMaxMs + tol;
        // Duraklama ancak kare basinda kalkar: olculen, istenen ile istenen + bitisi kapsayan kare arasinda olmali.
        // Eski hata (olcekli delta) istenenin ~12,5 katini verirdi.
        bool close = got >= req - tol && got <= req + frame + tol;
        bool ok = inRange && close;
        if (!ok) _fails.Add($"{name}: istenen {req:0} ms, olculen {got:0} ms (kare {frame:0.0} ms)");
        // Olculenin araliga dusmesi kare suresine bagli (yuklu makinede kareler uzar): bilgi; belirlenimci kosul Sim'de
        // (CapaTests: istenen + bir kare <= ust sinir).
        bool gotIn = got >= _c.HitStopMinMs - tol && got <= _c.HitStopMaxMs + tol;
        Say($"HITSTOP {name,-14} veri={cfgMs:0} istenen={req:0.0}ms olculen={got:0.0}ms bitis_karesi={frame:0.0}ms aralik={_c.HitStopMinMs:0}-{_c.HitStopMaxMs:0} istenen_aralikta={(inRange ? "evet" : "hayir")} olculen_aralikta={(gotIn ? "evet" : "hayir")} {(ok ? "ok" : "FAIL")}");
    }

    void Finish()
    {
        if (_done) return;
        _done = true;
        double worst = _heads.Count == 0 ? -1 : _heads.Values.Max(v => v.budget);
        double wallMed = _heads.Count == 0 ? -1 : _heads.Values.Select(v => v.wallMed).OrderBy(x => x).ElementAt(_heads.Count / 2);
        double wallMax = _heads.Count == 0 ? -1 : _heads.Values.Max(v => v.wallMax);
        int expected = Enum.GetValues(typeof(Capa.Head)).Length;
        if (_heads.Count < expected && _fails.Count == 0) _fails.Add($"yalnizca {_heads.Count}/{expected} baslik olculdu");
        string tail = $"heads={_heads.Count}/{expected} budget_max_ms={worst:0.0} wall_med_ms={wallMed:0.0} wall_max_ms={wallMax:0.0} hitstop={(_fails.Any(f => f.Contains("duraklama") || f.Contains("istenen")) ? "fail" : "ok")}";
        Say(_fails.Count == 0 ? $"RESULT ok {tail}" : $"RESULT fail {tail} :: {string.Join(" | ", _fails)}");
        if (Main.Args.TryGetValue("test_out", out var dir))
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            using var f = FileAccess.Open(dir.TrimEnd('/', '\\') + "/latency.log", FileAccess.ModeFlags.Write);
            f?.StoreString(string.Join("\n", _log) + "\n");
        }
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }
}
