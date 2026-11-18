using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ata can cubugu (Rust tarzi pano dili, SANAT-SES §8): ekranin ustunde koyu duz panel, buyuk harfli dar yazi, kirmizi
/// can cubugu ve uc asama centigi, zirh (yun) icin ince beyaz serit, "SERSEM" ve "KALKAN" etiketi. Dovus yokken gizli.
/// </summary>
public partial class AtaBar : CanvasLayer
{
    PanelContainer _panel;
    Label _name, _phase, _tag;
    ProgressBar _hp, _armor;
    Control _ticks;
    AtaDef _def;
    AtaFight _fight;
    float _shown = 1f, _victoryT;

    public override void _Ready()
    {
        Layer = 5;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AnchorLeft = 0.5f; _panel.AnchorRight = 0.5f;
        _panel.OffsetLeft = -UiTheme.Pxf(330); _panel.OffsetRight = UiTheme.Pxf(330); _panel.OffsetTop = UiTheme.Pxf(112); // pusula ve Defter satirinin altinda
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = UiTheme.A(UiTheme.Panel, 0.92f), BorderColor = UiTheme.A(UiTheme.Red, 0.6f), BorderWidthBottom = UiTheme.Px(2),
            ContentMarginLeft = UiTheme.Pxf(16), ContentMarginRight = UiTheme.Pxf(16), ContentMarginTop = UiTheme.Pxf(8), ContentMarginBottom = UiTheme.Pxf(10),
        });
        root.AddChild(_panel);
        var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", UiTheme.Px(4));
        _panel.AddChild(v);
        var top = new HBoxContainer();
        v.AddChild(top);
        _name = Lbl(26, UiTheme.Ink);
        _name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_name);
        _tag = Lbl(20, Palette.Warning);
        top.AddChild(_tag);
        _phase = Lbl(17, UiTheme.Muted);
        top.AddChild(_phase);

        var bars = new Control { CustomMinimumSize = new Vector2(0, UiTheme.Pxf(22)) };
        v.AddChild(bars);
        _hp = Bar(UiTheme.Red, UiTheme.Pxf(22));
        bars.AddChild(_hp);
        _hp.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _ticks = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _ticks.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bars.AddChild(_ticks);
        _armor = Bar(Palette.OffWhite, UiTheme.Pxf(6));
        v.AddChild(_armor);
        Visible = false;
    }

    static Label Lbl(int size, Color c)
    {
        var l = new Label();
        l.AddThemeFontOverride("font", UiTheme.Condensed);
        l.AddThemeFontSizeOverride("font_size", UiTheme.Px(size));
        l.AddThemeColorOverride("font_color", c);
        l.AddThemeConstantOverride("outline_size", UiTheme.Px(3));
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        return l;
    }

    static ProgressBar Bar(Color fill, float h)
    {
        var b = new ProgressBar { ShowPercentage = false, MinValue = 0, MaxValue = 1, Step = 0, CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore };
        b.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.55f) });
        b.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill });
        return b;
    }

    public void Show(AtaDef def, AtaFight fight)
    {
        _def = def; _fight = fight; _victoryT = 0; _shown = fight.Hp / fight.MaxHp;
        _name.Text = UiTheme.Upper(def.Name);
        foreach (var c in _ticks.GetChildren()) c.QueueFree();
        // Asama esikleri: cubukta centik
        for (int i = 0; i < def.Phases.Count - 1; i++)
        {
            var t = new ColorRect { Color = new Color(0, 0, 0, 0.8f), MouseFilter = Control.MouseFilterEnum.Ignore };
            t.AnchorLeft = t.AnchorRight = def.Phases[i].HpUntil;
            t.AnchorTop = 0; t.AnchorBottom = 1;
            t.OffsetLeft = -UiTheme.Pxf(1.5f); t.OffsetRight = UiTheme.Pxf(1.5f);
            _ticks.AddChild(t);
        }
        Phase(fight.Phase);
        Visible = true;
    }

    public void Phase(int p)
    {
        if (_def == null) return;
        _phase.Text = $"  AŞAMA {p + 1}/{_def.Phases.Count} · {UiTheme.Upper(_def.Phases[p].Name)}";
    }

    public new void Hide() { Visible = false; _def = null; _fight = null; }

    public void Victory(AtaDef def)
    {
        _name.Text = UiTheme.Upper(def.Name) + " YENİLDİ";
        _tag.Text = "";
        _phase.Text = "";
        _hp.Value = 0;
        _armor.Visible = false;
        _fight = null;
        _victoryT = 4f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_victoryT > 0) { _victoryT -= dt; if (_victoryT <= 0) Hide(); return; }
        if (_fight == null || !Visible) return;
        float hp = _fight.Hp / Mathf.Max(1f, _fight.MaxHp);
        _shown = Mathf.Lerp(_shown, hp, Mathf.Min(1f, dt * 8f));
        _hp.Value = _shown;
        _armor.Visible = _fight.Armor > 0;
        _armor.Value = _def != null && AtaRules.PhaseOf(_fight, _def).Armor > 0 ? _fight.Armor / AtaRules.PhaseOf(_fight, _def).Armor : 0;
        _tag.Text = _fight.StunT > 0 ? "SERSEM  " : _fight.ShieldT > 0 ? "KALKAN  " : _fight.Armor > 0 ? $"YÜN %{_fight.Armor * 100:0}  " : "";
    }
}
