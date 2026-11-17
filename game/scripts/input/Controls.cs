using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

public enum InputDevice { Kbm, Pad }

/// <summary>
/// Girdi: input.json'dan InputMap (klavye-fare ve oyun kolu), yeniden atama (degisenler user://input.json'da),
/// son kullanilan aygit ve glif (SANAT-SES §8.1: "ipucu ikonlari son kullanilan aygita gore degisir").
/// Main'in cocugu; durum bu dugumde (Godot katmaninda statik durum yok, TEKNIK §9).
/// Metin: Expand("{act:interact} ile ac") → "[E] ile ac" ya da "Ⓧ ile ac".
/// </summary>
public partial class Controls : Node
{
    public InputFile Data { get; private set; }
    public InputDevice Last { get; private set; } = InputDevice.Kbm;
    /// <summary>Son kullanilan aygit degisti: glif gosteren yazilar yeniden genisletilir.</summary>
    public event Action<InputDevice> DeviceChanged;
    /// <summary>Cozulemeyen belirtecler ("eylem: belirtec"). --test=input bos olmasini ister.</summary>
    public readonly List<string> Problems = new();
    /// <summary>Kalici yeniden atama dosyasi; null ise kaydedilmez (sinamalar sahibin dosyasina dokunmaz).</summary>
    public string UserPath { get; private set; }

    readonly Dictionary<string, List<string>> _kb = new(), _pad = new();

    public void Init(InputFile data, string userPath)
    {
        Data = data;
        UserPath = userPath;
        Name = "Controls";
        ResetToDefaults();
        LoadUser();
        foreach (var id in Data.Actions.Keys) ApplyAction(id);
    }

    // ---------- Baglamalar ----------

    public IReadOnlyList<string> Bindings(string action, InputDevice d) =>
        (d == InputDevice.Kbm ? _kb : _pad).TryGetValue(action, out var l) ? l : Array.Empty<string>();

    void ResetToDefaults()
    {
        _kb.Clear(); _pad.Clear();
        foreach (var (id, a) in Data.Actions) { _kb[id] = a.Kb.ToList(); _pad[id] = a.Pad.ToList(); }
    }

    void ApplyAction(string id)
    {
        _glyphs.Clear();
        if (!InputMap.HasAction(id)) InputMap.AddAction(id, Data.Deadzone);
        else { InputMap.ActionEraseEvents(id); InputMap.ActionSetDeadzone(id, Data.Deadzone); }
        Problems.RemoveAll(p => p.StartsWith(id + ":", StringComparison.Ordinal));
        foreach (var t in _kb[id])
        {
            var e = MapEvent(t, InputDevice.Kbm);
            if (e == null) Problems.Add($"{id}: klavye '{t}'"); else InputMap.ActionAddEvent(id, e);
        }
        foreach (var t in _pad[id])
        {
            var e = MapEvent(t, InputDevice.Pad);
            if (e == null) Problems.Add($"{id}: kol '{t}'"); else InputMap.ActionAddEvent(id, e);
        }
    }

    /// <summary>Eylemin o aygittaki ilk baglamasini degistirir (digerleri kalir), kaydeder. Belirtec cozulmezse false.</summary>
    public bool Rebind(string action, InputDevice d, string token)
    {
        if (!Data.Actions.ContainsKey(action) || MapEvent(token, d) == null) return false;
        var list = d == InputDevice.Kbm ? _kb[action] : _pad[action];
        if (list.Count == 0) list.Add(token); else list[0] = token;
        ApplyAction(action);
        SaveUser();
        GD.Print($"INPUT rebind {action} {d} -> {token}");
        DeviceChanged?.Invoke(Last); // glifler yenilensin
        return true;
    }

    /// <summary>
    /// Butun eylemleri input.json'daki varsayilana dondurur ve kalici dosyayi temizler. Yalnizca degisen eylemler yeniden
    /// kurulur: InputMap'te olaylari silmek eylemi birakir, basili tutulan cubuk ya da tus sifirlamada dusmesin.
    /// </summary>
    public void ResetAll()
    {
        var oldKb = _kb.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        var oldPad = _pad.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        ResetToDefaults();
        _glyphs.Clear();
        foreach (var id in Data.Actions.Keys)
            if (!oldKb.TryGetValue(id, out var k) || !k.SequenceEqual(_kb[id]) || !oldPad.TryGetValue(id, out var p) || !p.SequenceEqual(_pad[id]))
                ApplyAction(id);
        SaveUser();
        DeviceChanged?.Invoke(Last);
    }

    sealed class UserFile
    {
        public Dictionary<string, List<string>> Kb { get; set; } = new();
        public Dictionary<string, List<string>> Pad { get; set; } = new();
    }

    void LoadUser()
    {
        if (string.IsNullOrEmpty(UserPath) || !FileAccess.FileExists(UserPath)) return;
        try
        {
            var u = JsonSerializer.Deserialize<UserFile>(FileAccess.GetFileAsString(UserPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (u == null) return;
            foreach (var (id, l) in u.Kb) if (_kb.ContainsKey(id)) _kb[id] = l.ToList();
            foreach (var (id, l) in u.Pad) if (_pad.ContainsKey(id)) _pad[id] = l.ToList();
            GD.Print($"INPUT user bindings {UserPath} kb={u.Kb.Count} pad={u.Pad.Count}");
        }
        catch (Exception ex) { GD.PushWarning($"INPUT {UserPath} okunamadi, varsayilan kullaniliyor: {ex.Message}"); }
    }

    void SaveUser()
    {
        if (string.IsNullOrEmpty(UserPath)) return;
        var u = new UserFile();
        foreach (var (id, a) in Data.Actions)
        {
            if (!_kb[id].SequenceEqual(a.Kb)) u.Kb[id] = _kb[id];
            if (!_pad[id].SequenceEqual(a.Pad)) u.Pad[id] = _pad[id];
        }
        using var f = FileAccess.Open(UserPath, FileAccess.ModeFlags.Write);
        if (f == null) { GD.PushWarning($"INPUT {UserPath} yazilamadi: {FileAccess.GetOpenError()}"); return; }
        f.StoreString(JsonSerializer.Serialize(u, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    // ---------- Son kullanilan aygit ----------

    public override void _Input(InputEvent e)
    {
        InputDevice? d = e switch
        {
            InputEventKey k when k.Pressed && !k.Echo => InputDevice.Kbm,
            InputEventMouseButton mb when mb.Pressed => InputDevice.Kbm,
            InputEventMouseMotion mm when mm.Relative.LengthSquared() > 4f => InputDevice.Kbm,
            InputEventJoypadButton jb when jb.Pressed => InputDevice.Pad,
            InputEventJoypadMotion jm when Mathf.Abs(jm.AxisValue) > Data.Deadzone => InputDevice.Pad,
            _ => null,
        };
        if (d is InputDevice dev && dev != Last)
        {
            Last = dev;
            DeviceChanged?.Invoke(dev);
        }
    }

    // ---------- Glif ----------

    /// <summary>Metindeki {act:x} yer tutucularini son kullanilan aygitin glifiyle doldurur.</summary>
    public string Expand(string text) => InputText.Expand(text, a => Glyph(a, Last));

    public string Glyph(string action) => Glyph(action, Last);

    public string Glyph(string action, InputDevice d)
    {
        if (_glyphs.TryGetValue((action, d), out var cached)) return cached;
        var g = MakeGlyph(action, d);
        _glyphs[(action, d)] = g;
        return g;
    }

    /// <summary>Glif onbellegi (pano her karede genisletir); baglama degisince temizlenir.</summary>
    readonly Dictionary<(string, InputDevice), string> _glyphs = new();

    string MakeGlyph(string action, InputDevice d)
    {
        if (Data.Hints.TryGetValue(action, out var h)) return d == InputDevice.Kbm ? h.Kb : h.Pad;
        if (!Data.Actions.TryGetValue(action, out var a)) return $"[{action}?]";
        if (d == InputDevice.Kbm)
        {
            var l = _kb[action];
            if (l.Count == 0) return a.KbGlyph ?? "?";
            return string.Format(Data.KbFormat, Data.KbGlyphs.TryGetValue(l[0], out var g) ? g : KeyLabel(l[0]));
        }
        var p = _pad[action];
        if (p.Count == 0) return a.PadVia != null ? Glyph(a.PadVia, d) : "?";
        return string.Format(Data.PadFormat, Data.PadGlyphs.TryGetValue(p[0], out var pg) ? pg : p[0]);
    }

    /// <summary>Fiziksel tusun oyuncunun klavye duzenindeki adi (Turkce Q'da da dogru harf).</summary>
    static string KeyLabel(string token)
    {
        var k = OS.FindKeycodeFromString(token);
        if (k == Key.None) return token;
        if (DisplayServer.GetName() == "headless") return OS.GetKeycodeString(k); // basliksiz kipte klavye duzeni yok
        var local = DisplayServer.KeyboardGetKeycodeFromPhysical(k);
        var s = OS.GetKeycodeString(local != Key.None ? local : k);
        return string.IsNullOrEmpty(s) ? token : s;
    }

    // ---------- Belirtec <-> olay ----------

    static readonly Dictionary<string, JoyButton> PadButtons = new()
    {
        ["A"] = JoyButton.A, ["B"] = JoyButton.B, ["X"] = JoyButton.X, ["Y"] = JoyButton.Y,
        ["LB"] = JoyButton.LeftShoulder, ["RB"] = JoyButton.RightShoulder, ["L3"] = JoyButton.LeftStick, ["R3"] = JoyButton.RightStick,
        ["Back"] = JoyButton.Back, ["Start"] = JoyButton.Start, ["Guide"] = JoyButton.Guide,
        ["DPad:Up"] = JoyButton.DpadUp, ["DPad:Down"] = JoyButton.DpadDown, ["DPad:Left"] = JoyButton.DpadLeft, ["DPad:Right"] = JoyButton.DpadRight,
    };

    static readonly Dictionary<string, (JoyAxis axis, float sign)> PadAxes = new()
    {
        ["LS:Left"] = (JoyAxis.LeftX, -1), ["LS:Right"] = (JoyAxis.LeftX, 1), ["LS:Up"] = (JoyAxis.LeftY, -1), ["LS:Down"] = (JoyAxis.LeftY, 1),
        ["RS:Left"] = (JoyAxis.RightX, -1), ["RS:Right"] = (JoyAxis.RightX, 1), ["RS:Up"] = (JoyAxis.RightY, -1), ["RS:Down"] = (JoyAxis.RightY, 1),
        ["LT"] = (JoyAxis.TriggerLeft, 1), ["RT"] = (JoyAxis.TriggerRight, 1),
    };

    static readonly Dictionary<string, MouseButton> MouseButtons = new()
    {
        ["Mouse:Left"] = MouseButton.Left, ["Mouse:Right"] = MouseButton.Right, ["Mouse:Middle"] = MouseButton.Middle,
        ["Wheel:Up"] = MouseButton.WheelUp, ["Wheel:Down"] = MouseButton.WheelDown,
    };

    /// <summary>InputMap'e konacak olay (her aygit, Device = -1). Cozulemezse null.</summary>
    public static InputEvent MapEvent(string token, InputDevice d)
    {
        if (d == InputDevice.Pad)
        {
            if (PadButtons.TryGetValue(token, out var b)) return new InputEventJoypadButton { ButtonIndex = b, Device = -1 };
            if (PadAxes.TryGetValue(token, out var ax)) return new InputEventJoypadMotion { Axis = ax.axis, AxisValue = ax.sign, Device = -1 };
            return null;
        }
        if (MouseButtons.TryGetValue(token, out var mb)) return new InputEventMouseButton { ButtonIndex = mb, Device = -1 };
        var k = OS.FindKeycodeFromString(token);
        return k == Key.None ? null : new InputEventKey { PhysicalKeycode = k, Device = -1 };
    }

    /// <summary>
    /// Gercek bir aygitin gonderecegi olay (sentetik surucu): tus/dugme basildi-birakildi ya da eksen degeri (0..1).
    /// Eksen belirtecinde amount eksenin o yondeki degeridir; 0 cubugu ortaya birakir.
    /// </summary>
    public static InputEvent DeviceEvent(string token, InputDevice d, bool pressed, float amount = 1f)
    {
        if (d == InputDevice.Pad)
        {
            if (PadButtons.TryGetValue(token, out var b)) return new InputEventJoypadButton { ButtonIndex = b, Pressed = pressed, Device = 0 };
            if (PadAxes.TryGetValue(token, out var ax)) return new InputEventJoypadMotion { Axis = ax.axis, AxisValue = pressed ? ax.sign * Mathf.Clamp(amount, 0f, 1f) : 0f, Device = 0 };
            return null;
        }
        if (MouseButtons.TryGetValue(token, out var mb)) return new InputEventMouseButton { ButtonIndex = mb, Pressed = pressed, Device = 0 };
        var k = OS.FindKeycodeFromString(token);
        return k == Key.None ? null : new InputEventKey { PhysicalKeycode = k, Keycode = k, Pressed = pressed, Device = 0 };
    }

    /// <summary>Yeniden atama sirasinda yakalanan olayin belirteci (bilinmeyen ya da yetersiz olaysa null).</summary>
    public string TokenOf(InputEvent e) => e switch
    {
        InputEventKey k when k.Pressed && !k.Echo => OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode),
        InputEventMouseButton mb when mb.Pressed => MouseButtons.FirstOrDefault(kv => kv.Value == mb.ButtonIndex).Key,
        InputEventJoypadButton jb when jb.Pressed => PadButtons.FirstOrDefault(kv => kv.Value == jb.ButtonIndex).Key,
        InputEventJoypadMotion jm when Mathf.Abs(jm.AxisValue) > 0.6f => PadAxes.FirstOrDefault(kv => kv.Value.axis == jm.Axis && Mathf.Sign(kv.Value.sign) == Mathf.Sign(jm.AxisValue)).Key,
        _ => null,
    };

    /// <summary>Kol eksen belirteci mi (cubuk yonu ya da tetik); hangi eksen ve hangi yon.</summary>
    public static bool IsAxis(string token, out JoyAxis axis, out float sign)
    {
        if (token != null && PadAxes.TryGetValue(token, out var a)) { axis = a.axis; sign = a.sign; return true; }
        axis = JoyAxis.Invalid; sign = 0; return false;
    }

    public static InputDevice DeviceOf(InputEvent e) => e is InputEventJoypadButton or InputEventJoypadMotion ? InputDevice.Pad : InputDevice.Kbm;
}
