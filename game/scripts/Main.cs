using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Giris noktasi. Komut satiri (Godot'un "--" sonrasi):
///   --bot            ilk 10 dakikayi kendi oynar (sinama)
///   --fast=N         zamani N kat hizlandirir
///   --shots=a,b,c    bu oyun saniyelerinde ekran goruntusu alir (progress/shots/)
///   --shot_prefix=x  goruntu adlarinin oneki
///   --quit_after=N   N oyun saniyesi sonra kapanir (bot bu sure icinde zincirin sonuna varmadiysa cikis kodu 1)
///   --mute           ses yok
///   --test=AD        basliksiz sinama (TEKNIK §11): bot, build, netsync (--role=host|client), swarm.
///                    Sifir olmayan cikis kodu = hata. Eski bayraklar (--bot, --netsync=..., --swarm) de calisir.
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

    /// <summary>--test=AD: sinama adi -> calistir. true donerse oyun (Director) kurulmaz.</summary>
    static readonly Dictionary<string, System.Func<Main, bool>> Tests = new()
    {
        ["bot"] = m => { Args["bot"] = "1"; if (m._quitAfter <= 0) m._quitAfter = 400; return false; },
        ["build"] = m => { m.AddChild(new BuildTest()); return true; },
        ["netsync"] = m => { if (!Args.ContainsKey("netsync")) Args["netsync"] = Args.TryGetValue("role", out var r) ? r : "host"; m.AddChild(new NetSyncTest()); return true; },
        ["swarm"] = m => { m.AddChild(new SwarmTest()); return true; },
        ["audio"] = m => { m.AddChild(new AudioTest()); return true; }, // ses akisi: kanallar, 60 ses, muzik durumlari
        ["world"] = m => { m.AddChild(new WorldTest()); return true; },
        ["shots"] = m => { Args.TryAdd("preset", "yuksek"); if (!Args.ContainsKey("shots_quick") && !Args.ContainsKey("shots_clip")) Args["bot"] = "1"; m.AddChild(new WorldShots()); return false; },
        ["perf"] = m => { Args.TryAdd("preset", "yuksek"); if (Args.GetValueOrDefault("perf") == "night") Args["bot"] = "1"; m.AddChild(new PerfProbe()); return false; },
        ["creatures"] = m => { m.AddChild(new CreatureTest()); return true; },
        ["latency"] = m => { m.AddChild(new LatencyTest()); return false; }, // his olcutleri: girdi->tepki, vurus duraklamasi
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
        if (Args.ContainsKey("mute")) Sfx.Muted = true;
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
        _dir = new Director { Name = "Director" };
        if (Args.TryGetValue("fast", out var f)) _dir.SpeedScale = float.Parse(f, System.Globalization.CultureInfo.InvariantCulture);
        AddChild(_dir);
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

    static void Key(string action, params Key[] keys)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var k in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
    }

    static void SetupInput()
    {
        Key("move_forward", Godot.Key.W, Godot.Key.Up);
        Key("move_back", Godot.Key.S, Godot.Key.Down);
        Key("move_left", Godot.Key.A, Godot.Key.Left);
        Key("move_right", Godot.Key.D, Godot.Key.Right);
        Key("jump", Godot.Key.Space);
        Key("sprint", Godot.Key.Shift);
        Key("interact", Godot.Key.E);
        Key("bag", Godot.Key.Tab, Godot.Key.I);
        Key("head_wheel", Godot.Key.Q);
        Key("build_rotate", Godot.Key.R);
        if (!InputMap.HasAction("build_next")) InputMap.AddAction("build_next");
        InputMap.ActionAddEvent("build_next", new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown });
        InputMap.ActionAddEvent("build_next", new InputEventKey { PhysicalKeycode = Godot.Key.Period });
        if (!InputMap.HasAction("build_prev")) InputMap.AddAction("build_prev");
        InputMap.ActionAddEvent("build_prev", new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp });
        InputMap.ActionAddEvent("build_prev", new InputEventKey { PhysicalKeycode = Godot.Key.Comma });
        Key("card_1", Godot.Key.Z);
        Key("card_2", Godot.Key.X);
        Key("card_3", Godot.Key.C);
        for (int i = 1; i <= 9; i++) Key($"head_{i}", Godot.Key.Key0 + i);
        if (!InputMap.HasAction("fire")) InputMap.AddAction("fire");
        InputMap.ActionAddEvent("fire", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        // Oyun kolu: sol cubuk hareket, sag tetik kullan, A zipla
        InputMap.ActionAddEvent("fire", new InputEventJoypadMotion { Axis = JoyAxis.TriggerRight, AxisValue = 1 });
        InputMap.ActionAddEvent("jump", new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        InputMap.ActionAddEvent("interact", new InputEventJoypadButton { ButtonIndex = JoyButton.X });
        InputMap.ActionAddEvent("move_left", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1 });
        InputMap.ActionAddEvent("move_right", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1 });
        InputMap.ActionAddEvent("move_forward", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1 });
        InputMap.ActionAddEvent("move_back", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1 });
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
