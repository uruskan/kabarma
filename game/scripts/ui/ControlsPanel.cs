using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Tus atama paneli (SANAT-SES §8.3 "Tus ve kol eslemesi degistirilebilir"). F2 / Gorunum ile acilir.
/// Klavye ya da kolla gezilir: ↑↓ / ✚ satir, Enter / Ⓐ sec, sonra yeni tusa ya da kol dugmesine bas; olayin aygiti
/// hangi baglamanin degisecegini belirler. Esc / Ⓑ vazgecer ya da kapatir. Son satir hepsini varsayilana dondurur.
/// Degisiklikler Controls uzerinden user://input.json'a yazilir (sinamalarda --input_user yolu).
/// </summary>
public partial class ControlsPanel : CanvasLayer
{
    Controls _c;
    PanelContainer _box;
    ScrollContainer _scroll;
    VBoxContainer _list;
    Label _foot;
    readonly List<string> _ids = new();
    readonly List<Label> _rows = new();
    int _sel;
    bool _capturing;
    StyleBoxFlat _on;

    public new bool Visible => _box != null && _box.Visible;

    public void Init(Controls c) { _c = c; Name = "ControlsPanel"; }

    public override void _Ready()
    {
        Layer = 60;
        _box = new PanelContainer { Visible = false };
        UiPanels.Skin(_box); // M6: tek panel dili (Rust tarzi)
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(760, 0) };
        v.AddChild(Text(Loc.T("menu.controls_title"), 28, Palette.Warning));
        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(760, 560), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer();
        _scroll.AddChild(_list);
        v.AddChild(_scroll);
        _foot = Text("", 16, Palette.Fog);
        _foot.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _foot.CustomMinimumSize = new Vector2(740, 0);
        v.AddChild(_foot);
        _box.AddChild(v);
        AddChild(_box);
        _on = new StyleBoxFlat { BgColor = new Color(Palette.Warning, 0.2f), BorderColor = Palette.Warning, BorderWidthLeft = 3, ContentMarginLeft = 8 };
        _c.DeviceChanged += _ => { if (IsInstanceValid(this) && Visible) Rebuild(); };
    }

    static Label Text(string s, int size, Color c)
    {
        var l = new Label { Text = s };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public void Toggle()
    {
        _box.Visible = !_box.Visible;
        _capturing = false;
        if (_box.Visible) Rebuild();
    }

    void Rebuild()
    {
        _ids.Clear(); _rows.Clear();
        foreach (var n in _list.GetChildren()) n.QueueFree();
        foreach (var (id, a) in _c.Data.Actions)
        {
            _ids.Add(id);
            var kb = _c.Bindings(id, InputDevice.Kbm).Count > 0 ? _c.Glyph(id, InputDevice.Kbm) : a.KbGlyph ?? "—";
            var pad = _c.Bindings(id, InputDevice.Pad).Count > 0 ? _c.Glyph(id, InputDevice.Pad) : a.PadVia != null ? $"({_c.Glyph(a.PadVia, InputDevice.Pad)})" : "—";
            var l = Text($"{a.Name,-28}  {kb,-16}  {pad}", 19, Palette.OffWhite);
            _list.AddChild(l);
            _rows.Add(l);
        }
        _ids.Add("");
        var reset = Text(Loc.T("menu.controls_reset"), 19, Palette.Accent);
        _list.AddChild(reset);
        _rows.Add(reset);
        _sel = Mathf.Clamp(_sel, 0, _rows.Count - 1);
        Paint();
    }

    void Paint()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (i == _sel) _rows[i].AddThemeStyleboxOverride("normal", _on); else _rows[i].RemoveThemeStyleboxOverride("normal");
        }
        if (_sel < _rows.Count) _scroll.CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, _rows[_sel]);
        _foot.Text = _capturing
            ? Loc.T("menu.controls_capture", _c.Data.Actions[_ids[_sel]].Name, _c.Glyph("ui_cancel"))
            : _c.Expand(Loc.T("menu.controls_help")) +
              (_c.UserPath != null ? "\n" + Loc.T("menu.controls_saved", _c.UserPath) : "\n" + Loc.T("menu.controls_test"));
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible || !_capturing) return;
        if (e.IsActionPressed("ui_cancel")) { _capturing = false; Paint(); GetViewport().SetInputAsHandled(); return; }
        var token = _c.TokenOf(e);
        if (token == null) return;
        var id = _ids[_sel];
        _capturing = false;
        _c.Rebind(id, Controls.DeviceOf(e), token);
        GetViewport().SetInputAsHandled();
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _capturing) return;
        if (Input.IsActionJustPressed("ui_cancel")) { Toggle(); return; }
        if (Input.IsActionJustPressed("ui_down")) { _sel = (_sel + 1) % _rows.Count; Paint(); }
        if (Input.IsActionJustPressed("ui_up")) { _sel = (_sel + _rows.Count - 1) % _rows.Count; Paint(); }
        if (Input.IsActionJustPressed("ui_accept"))
        {
            if (_ids[_sel] == "") { _c.ResetAll(); Rebuild(); }
            else { _capturing = true; Paint(); }
        }
    }
}
