using Godot;

namespace Kabarma.Game;

/// <summary>
/// F2 (oyun kolunda Geri/Back): ses kanallari ayari (SANAT-SES §8.3 "ses kanallari ayri ayri"). Her kanal icin bir
/// surgu; degisiklik hemen duyulur ve user://audio_settings.cfg'ye yazilir. Klavye-fare ve oyun koluyla kullanilir:
/// acilinca ilk surgu odaklanir, yon tuslari/yon pedi surgu secer ve ayarlar, Esc/B kapatir.
/// </summary>
public partial class AudioPanel : CanvasLayer
{
    PanelContainer _panel;
    HSlider _first;

    public override void _Ready()
    {
        Layer = 51;
        _panel = new PanelContainer { Visible = false, Position = new Vector2(420, 160) };
        UiPanels.Skin(_panel); // M6: tek panel dili (Rust tarzi)
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        v.AddChild(UiPanels.Title(Loc.T("menu.audio_title"), 28));
        if (Sfx.Data != null)
            foreach (var b in Sfx.Data.Buses) Slider(v, b.Id, b.Name);
        _panel.AddChild(v);
        AddChild(_panel);
    }

    void Slider(VBoxContainer parent, string bus, string name)
    {
        var h = new HBoxContainer();
        var l = new Label { Text = name, CustomMinimumSize = new Vector2(150, 0) };
        float now = Sfx.GetVolume(bus);
        var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = now, CustomMinimumSize = new Vector2(160, 0), FocusMode = Control.FocusModeEnum.All };
        var val = new Label { Text = Percent(now), CustomMinimumSize = new Vector2(52, 0) };
        s.ValueChanged += x => { Sfx.SetVolume(bus, (float)x); val.Text = Percent((float)x); };
        h.AddChild(l); h.AddChild(s); h.AddChild(val);
        parent.AddChild(h);
        _first ??= s;
    }

    static string Percent(float x) => Loc.T("settings.pct", Mathf.RoundToInt(x * 100));

    public override void _Input(InputEvent e)
    {
        bool toggle = e is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.F2
                      || e is InputEventJoypadButton j && j.Pressed && j.ButtonIndex == JoyButton.Back;
        bool close = _panel.Visible && e.IsActionPressed("ui_cancel");
        if (!toggle && !close) return;
        _panel.Visible = !_panel.Visible && !close;
        Input.MouseMode = _panel.Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (_panel.Visible) _first?.GrabFocus();
        GetViewport().SetInputAsHandled();
    }

    public bool IsOpen => _panel.Visible;
}
