using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Giris noktasi. Komut satiri (Godot'un "--" sonrasi):
///   --bot            ilk 10 dakikayi kendi oynar (sinama)
///   --fast=N         zamani N kat hizlandirir
///   --shots=a,b,c    bu oyun saniyelerinde ekran goruntusu alir (progress/shots/)
///   --shot_prefix=x  goruntu adlarinin oneki
///   --quit_after=N   N oyun saniyesi sonra kapanir (bot bu sure icinde zincirin sonuna varmadiysa cikis kodu 1)
///   --mute           ses yok
///   --test=AD        basliksiz sinama (TEKNIK §11): bot, build, netsync (--role=host|client), swarm,
///                    input (--device=kbm|pad: botun kararlari sentetik klavye-fare / oyun kolu olayiyla).
///                    Sifir olmayan cikis kodu = hata. Eski bayraklar (--bot, --netsync=..., --swarm) de calisir.
///   --input_user=YOL tus atamalarinin kalici dosyasi (varsayilan user://input.json; --test kosularinda yok)
///   --botdebug       bot ve girdi surucusu durum satirlari (BOTDBG, INPUTDBG); --aimtrace kare kare nisan izi (AIMTRACE)
/// Yeni sinama: Tests sozlugune bir satir; sinama dugumu bitince GetTree().Quit(kod) cagirir.
/// </summary>
public partial class Main : Node3D
{
    public static Dictionary<string, string> Args = new();
    Director _dir;
    float _t;
    readonly List<float> _shots = new();
    string _prefix = "shot";
    float _quitAfter = -1;
    bool _quitting;
    Controls _controls;
    InputDevice? _inputDevice;

    /// <summary>--test=AD: sinama adi -> calistir. true donerse oyun (Director) kurulmaz.</summary>
    static readonly Dictionary<string, System.Func<Main, bool>> Tests = new()
    {
        ["bot"] = m => { Args["bot"] = "1"; if (m._quitAfter <= 0) m._quitAfter = 400; return false; },
        ["build"] = m => { m.AddChild(new BuildTest()); return true; },
        ["netsync"] = m => { if (!Args.ContainsKey("netsync")) Args["netsync"] = Args.TryGetValue("role", out var r) ? r : "host"; m.AddChild(new NetSyncTest()); return true; },
        ["swarm"] = m => { m.AddChild(new SwarmTest()); return true; },
        ["audio"] = m => { m.AddChild(new AudioTest()); return true; }, // ses akisi: kanallar, 60 ses, muzik durumlari
        ["world"] = m => { m.AddChild(new WorldTest()); return true; },
        ["explore"] = m => { Args["explore"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; return false; }, // dunya2: Director + ExploreTest (kule, mancinik, yuva, enkaz, harita)
        ["shots"] = m => { Args.TryAdd("preset", "yuksek"); if (!Args.ContainsKey("shots_quick") && !Args.ContainsKey("shots_clip")) Args["bot"] = "1"; m.AddChild(new WorldShots()); return false; },
        ["perf"] = m => { Args.TryAdd("preset", "yuksek"); if (Args.GetValueOrDefault("perf") == "night") Args["bot"] = "1"; m.AddChild(new PerfProbe()); return false; },
        ["gfxab"] = m => { Args.TryAdd("preset", "yuksek"); m.AddChild(new GfxAb()); return false; },
        ["creatures"] = m => { m.AddChild(new CreatureTest()); return true; },
        ["latency"] = m => { m.AddChild(new LatencyTest()); return false; }, // his olcutleri: girdi->tepki, vurus duraklamasi
        ["ekonomi"] = m => { Args["ekonomi"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; return false; }, // Director + EconomyTest (Director.Economy.cs)
        ["gece"] = m => { Args["skip"] = "night"; Args["gecetest"] = "1"; if (!Args.ContainsKey("fast")) Args["fast"] = "6"; return false; }, // GeceHudTest.cs (gecehud)
        ["ilerleme"] = m => { Args["ilerlemetest"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; return false; }, // IlerlemeTest.cs (ilerleme): Defter sekmeleri, Ustalik, basarim, odeme
        ["lineup"] =m => { m.AddChild(new ModelLineup()); return true; }, // gorsel-model: her model ayni isikta (progress/gorsel/model/)
        ["camera"] = m => { m.AddChild(new CameraTest()); return false; }, // oyuncu akisi: kamera kipleri, tekerlek, carpisma, nisan, ayar dosyasi
        ["grip"] = m => { Args["mute"] = "1"; Sfx.Muted = true; m.AddChild(new GripTest()); return false; }, // oyuncu akisi: Capa elde, namlu nisanda, araba iki elle
        ["kemer"] = m => { Args["bot"] = "1"; if (m._quitAfter <= 0) m._quitAfter = 600; m.AddChild(new KemerShots()); return false; }, // karar 23 kaniti: progress/kemer/*.png
        ["save"] = m => { SaveTest.Prepare(); m.AddChild(new SaveTest(m)); return false; }, // kayit: bot bir gun oynar, kaydeder, sahne bastan yuklenir; fark 0 (save/SaveTest.cs)
        ["menu"] = m => { MenuFlowTest.Prepare(); m.AddChild(new MenuFlowTest(m)); return false; }, // kayit: ana menu -> yeni oyun -> Esc ana menu -> Devam -> kapatma kaydi (save/MenuFlowTest.cs)
        ["hayvan"] = m => { Args["bot"] = "1"; Args["hayvanbot"] = "1"; if (m._quitAfter <= 0) m._quitAfter = 1300; return false; }, // hayvan akisi: zincir + ikinci tur (Bot.Hayvan.cs)
        ["borsa"] = m => { Args["borsatest"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; return false; }, // ekonomi2: Borsa, Katalog, Cark (MarketTest.cs)
        ["holding"] = m => { Args["holdingtest"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; return false; }, // holding M3: kamp, Perde, kervan, baskinlar (holding/HoldingTest.cs)
        ["boss"] = m => { Args.TryAdd("fast", "3"); Args.TryAdd("mute", "1"); Sfx.Muted = Args["mute"] == "1"; m.AddChild(new AtaTest()); return false; }, // boss akisi: bot iki Ata'yi bulur ve yener (boss/AtaTest.cs)
        ["zincir"] = m => { Args["bot"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; m.AddChild(new ZincirTest()); return false; }, // zincir M2: bot T2 urununu arastirir ve uretir (ZincirTest.cs)
        ["tarim"] = m => { Args["bot"] = "1"; Args["tarim"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; if (m._quitAfter <= 0) m._quitAfter = 900; return false; }, // tarim M2: Bot.Tarim.cs ikinci ekin
        ["coop"] = m => { CoopTest.Setup(); m.AddChild(new CoopTest()); return false; }, // ortak: host + 3 istemci (ENet), senkron < 1 cm (net/CoopTest.cs)
        ["steam"] = m => { m.AddChild(new SteamTest()); return true; }, // ortak: tek surec Steam baslatma ve lobi (yalniz -Only steam)
        ["power"] = m => { Args["bot"] = "1"; Args["power"] = "1"; if (m._quitAfter <= 0) m._quitAfter = 1500; return false; }, // guc akisi: ilk gun zinciri, sonra uretec + lamba + dugme (Bot.Power.cs)
        ["powershots"] = m => { Args["powershots"] = "1"; Args["mute"] = "1"; Sfx.Muted = true; return false; }, // guc akisi: parcalar ve kablolar, gunduz/gece goruntusu (progress/m2/guc/)
        ["input"] = m => { m._inputDevice = InputDriver.ParseDevice(Args.TryGetValue("device", out var d) ? d : "kbm"); if (m._quitAfter <= 0) m._quitAfter = 900; return false; }, // zincir bitince Bot.Hands cikar; bu ust sinir
    };

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            var s = a.TrimStart('-');
            int eq = s.IndexOf('=');
            if (eq > 0) Args[s[..eq]] = s[(eq + 1)..]; else Args[s] = "1";
        }
        SetupInput();
        // Sahibin karari (18 Kasim 2026): otomatik kosular (test, bot, goruntu, sure sinirli kosu) hic ses cikarmaz;
        // sahip calisirken muzik dinliyor. Sesi duymak gereken bir kosu --ses ile acar.
        bool automated = Args.ContainsKey("test") || Args.ContainsKey("bot") || Args.ContainsKey("shots") || Args.ContainsKey("quit_after")
            || Args.ContainsKey("checkcolors") || Args.ContainsKey("gfxab") || Args.ContainsKey("perf");
        if (Args.ContainsKey("mute") || (automated && !Args.ContainsKey("ses"))) { Args["mute"] = "1"; Sfx.Muted = true; AudioServer.SetBusMute(0, true); }
        if (Args.TryGetValue("shots", out var shots)) _shots.AddRange(shots.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)));
        if (Args.TryGetValue("shot_prefix", out var pre)) _prefix = pre;
        if (Args.TryGetValue("quit_after", out var q)) _quitAfter = float.Parse(q, System.Globalization.CultureInfo.InvariantCulture);

        if (Args.TryGetValue("checkcolors", out var cc)) { CheckColors(cc); GetTree().Quit(); return; }
        if (Args.TryGetValue("test", out var test))
        {
            if (!Tests.TryGetValue(test, out var run))
            {
                GD.Print($"RESULT fail unknown test '{test}' (bilinen: {string.Join(", ", Tests.Keys)})");
                GetTree().Quit(2);
                return;
            }
            GD.Print($"TEST {test}");
            if (run(this)) return;
        }
        if (Args.ContainsKey("netsync")) { AddChild(new NetSyncTest()); return; }
        if (Args.ContainsKey("swarm")) { AddChild(new SwarmTest()); return; }
        if (ShowMainMenu()) return; // save/Main.Flow.cs: argumansiz acilista ana menu (otomasyon kosulari dogrudan oyuna)
        _dir = new Director { Name = "Director", Controls = _controls };
        if (Args.TryGetValue("fast", out var f)) _dir.SpeedScale = float.Parse(f, System.Globalization.CultureInfo.InvariantCulture);
        AddChild(_dir);
        if (_inputDevice is InputDevice dev) _dir.StartInputDriver(dev);
        GD.Print("BOOT Kabarma MVP");
    }

    /// <summary>Uretim hatti denetimi: GLB'deki kose renkleri Godot'a hangi uzayda geliyor.</summary>
    static void CheckColors(string path)
    {
        var inst = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        foreach (var mi in inst.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var arr = mi.Mesh.SurfaceGetArrays(0);
            var cols = (Color[])arr[(int)Mesh.ArrayType.Color];
            var top = cols.GroupBy(c => c.ToHtml(false)).OrderByDescending(g => g.Count()).Take(4);
            GD.Print($"COLORS {mi.Name}: " + string.Join(" ", top.Select(g => $"{g.Key}x{g.Count()}")));
        }
    }

    /// <summary>Kodla klavye baglamasi ekler (eski yol). Yeni eylem input.json'a yazilir; boylece kol baglamasi, glif ve yeniden atama da gelir.</summary>
    static void Key(string action, params Key[] keys)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var k in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
    }

    /// <summary>
    /// Girdi eslemesi veriden (game/data/input.json): her eylemin klavye-fare ve oyun kolu baglamasi, Controls dugumu
    /// (yeniden atama, son aygit, glif). Oyuncunun degisiklikleri user://input.json'da; sinamalar (--test=...) o dosyaya
    /// dokunmaz, yalnizca --input_user=YOL verilirse onu kullanir. Veride olmayan eylem eklemek: input.json'a bir satir.
    /// </summary>
    void SetupInput()
    {
        var data = GameData.LoadInput(f => FileAccess.GetFileAsString("res://data/" + f));
        string userPath = Args.TryGetValue("input_user", out var u) ? u : Args.ContainsKey("test") ? null : "user://input.json";
        _controls = new Controls();
        _controls.Init(data, userPath);
        AddChild(_controls);
        foreach (var p in _controls.Problems) GD.PushWarning($"INPUT cozulemeyen baglama {p}");
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_shots.Count > 0 && _t >= _shots[0])
        {
            float at = _shots[0];
            _shots.RemoveAt(0);
            var img = GetViewport().GetTexture().GetImage();
            var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/shots";
            DirAccess.MakeDirRecursiveAbsolute(dir);
            var path = $"{dir}/{_prefix}_{at:000.00}.png".Replace(",", ".");
            img.SavePng(path);
            GD.Print($"SCREENSHOT {path}");
        }
        if (_quitAfter > 0 && _t >= _quitAfter && !_quitting)
        {
            _quitting = true;
            int code = 0;
            // Bot sure dolmadan zincirin sonuna ("end") varmadiysa sinama basarisiz.
            var bot = _dir?.Bot;
            if (bot != null && !bot.Done) { code = 1; GD.Print($"RESULT fail bot did not reach end step={bot.CurrentStep} t={_t:0.0}"); }
            GD.Print($"QUIT t={_t:0.0}");
            GetTree().Quit(code);
        }
    }
}
