using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Pano: gunduz sade (kemer, can, pusula yerine Defter), gece yogun (dalga, Oz, Gosteri, kart seridi). SANAT-SES §8.
/// Parcalar: Hud.Day.cs, Hud.Night.cs, Hud.Menu.cs, Hud.Report.cs, Hud.Wheel.cs. Kurulum (_Ready) burada, cizim sirasi icin tek yerde.
/// </summary>
public partial class Hud : CanvasLayer
{
    Director _d;
    Font _font, _bold;
    PanelContainer _suna;
    Label _sunaText;
    readonly Queue<string> _lines = new();
    float _lineT, _typeT;
    string _full = "";
    Label _hint;
    float _hintT;
    Control _crosshair;
    Label _bigText;
    float _bigT;
    ColorRect _vignette;

    public void Init(Director d) { _d = d; }

    StyleBoxFlat Box(Color bg, int radius = 14, Color? border = null, int bw = 0)
    {
        var s = new StyleBoxFlat { BgColor = bg, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10 };
        if (border is Color b) { s.BorderColor = b; s.BorderWidthBottom = s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = bw; }
        return s;
    }

    Label L(string text, int size, Color c, bool bold = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", bold ? _bold : _font);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        l.AddThemeColorOverride("font_outline_color", Palette.NightDeep);
        l.AddThemeConstantOverride("outline_size", 0);
        return l;
    }

    public override void _Ready()
    {
        // Yuvarlak hatli sans (SANAT-SES §8.1); sistemde yoksa Godot'un varsayilan yazisi.
        _font = new SystemFont { FontNames = new[] { "Nunito", "Segoe UI Rounded", "Arial Rounded MT Bold", "Segoe UI", "Noto Sans" }, FontWeight = 600 };
        _bold = new SystemFont { FontNames = new[] { "Nunito", "Segoe UI Rounded", "Arial Rounded MT Bold", "Segoe UI", "Noto Sans" }, FontWeight = 800 };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _vignette = new ColorRect { Color = new Color(Palette.HoldingRed, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_vignette);

        // Suna: telsiz kutusu, sol ust
        _suna = new PanelContainer { Position = new Vector2(24, 24), CustomMinimumSize = new Vector2(520, 0), Visible = false };
        _suna.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.82f), 16, Palette.CoopBlue, 3));
        var sv = new VBoxContainer();
        sv.AddChild(L("◉ SUNA · Kooperatif Santrali", 17, Palette.MayaTeal, true));
        _sunaText = L("", 22, Palette.OffWhite);
        _sunaText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sunaText.CustomMinimumSize = new Vector2(480, 0);
        sv.AddChild(_sunaText);
        _suna.AddChild(sv);
        root.AddChild(_suna);

        // Defter: sag ust
        _goalPanel = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        _goalPanel.AddThemeStyleboxOverride("panel", Box(new Color(Palette.OffWhite, 0.9f), 14, Palette.Warning, 3));
        _goalPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _goalPanel.Position = new Vector2(-404, 24);
        var gv = new VBoxContainer();
        gv.AddChild(L("DEFTER · İş emri", 16, Palette.CoopBlue, true));
        _goal = L("", 22, Palette.DarkGrey, true);
        _goal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _goal.CustomMinimumSize = new Vector2(340, 0);
        gv.AddChild(_goal);
        _goalProgress = L("", 20, Palette.Accent, true);
        gv.AddChild(_goalProgress);
        _goalPanel.AddChild(gv);
        root.AddChild(_goalPanel);
        _goalPanel.Resized += () => _goalPanel.Position = new Vector2(GetViewport().GetVisibleRect().Size.X - _goalPanel.Size.X - 24, 24);

        // Kemer: alt orta, 8 yuva (GDD 5). Cantadaki esyalara kisayol; Alt+1..8 ile kullanilir.
        _belt = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _belt.AddThemeConstantOverride("separation", 6);
        _belt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        for (int i = 0; i < Kabarma.Sim.Belt.Slots; i++)
        {
            var p = new PanelContainer { CustomMinimumSize = new Vector2(104, 66) };
            p.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.7f), 12, new Color(Palette.OffWhite, 0.22f), 2));
            var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            var k = L($"Alt+{i + 1}", 12, Palette.Warning, true); k.HorizontalAlignment = HorizontalAlignment.Left;
            var n = L("", 15, Palette.OffWhite, true); n.HorizontalAlignment = HorizontalAlignment.Center; n.ClipText = true; n.CustomMinimumSize = new Vector2(92, 0);
            var c = L("", 18, Palette.Warning, true); c.HorizontalAlignment = HorizontalAlignment.Right;
            v.AddChild(k); v.AddChild(n); v.AddChild(c);
            p.AddChild(v);
            _belt.AddChild(p);
            _beltSlots.Add(p);
            _beltName.Add(n); _beltCount.Add(c);
        }
        root.AddChild(_belt);

        // Capa'nin takili basligi: nisangahin sag altinda (dunyaya yakin, gozun oldugu yerde)
        _headTag = new PanelContainer();
        var hv = new VBoxContainer();
        _headName = L("", 22, Palette.OffWhite, true);
        _headHint = L("Q: çark  ·  1–8  ·  tekerlek", 13, Palette.Fog, true);
        hv.AddChild(_headName); hv.AddChild(_headHint);
        _headTag.AddChild(hv);
        root.AddChild(_headTag);

        // Baslik carki (GDD 4: "sayi tuslari ya da cark"): Q basili tutulur, fareyle secilir, birakinca takilir.
        _wheel = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(_wheel);
        for (int i = 0; i < Capa.HeadNames.Length; i++)
        {
            var wp = new PanelContainer { CustomMinimumSize = new Vector2(150, 54) };
            var wl = L($"{i + 1}  {Capa.HeadNames[i]}", 19, Palette.OffWhite, true);
            wl.HorizontalAlignment = HorizontalAlignment.Center;
            wp.AddChild(wl);
            _wheel.AddChild(wp);
            _wheelSlots.Add(wp);
        }

        // Can ve canta: sol alt
        var bl = new VBoxContainer();
        _bottomLeft = bl;
        _hp = new ProgressBar { CustomMinimumSize = new Vector2(280, 22), MaxValue = 100, Value = 100, ShowPercentage = false, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        var hpFill = Box(Palette.Accent, 10); hpFill.ContentMarginTop = hpFill.ContentMarginBottom = 0;
        _hp.AddThemeStyleboxOverride("fill", hpFill);
        _hp.AddThemeStyleboxOverride("background", Box(new Color(Palette.NightDeep, 0.7f), 10));
        bl.AddChild(L("CAN", 15, Palette.OffWhite, true));
        bl.AddChild(_hp);
        _bag = L("", 19, Palette.OffWhite, true);
        _bag.AddThemeConstantOverride("outline_size", 6);
        bl.AddChild(_bag);
        root.AddChild(bl);

        // Nisangah
        _crosshair = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        var dot = new ColorRect { Color = Palette.OffWhite, Size = new Vector2(6, 6), Position = new Vector2(-3, -3) };
        var ring = new Panel { Size = new Vector2(26, 26), Position = new Vector2(-13, -13) };
        var rs = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = new Color(Palette.OffWhite, 0.7f), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, CornerRadiusBottomLeft = 13, CornerRadiusBottomRight = 13, CornerRadiusTopLeft = 13, CornerRadiusTopRight = 13 };
        ring.AddThemeStyleboxOverride("panel", rs);
        _crosshair.AddChild(ring);
        _crosshair.AddChild(dot);
        root.AddChild(_crosshair);

        _hint = L("", 20, Palette.OffWhite, true);
        _hint.AddThemeConstantOverride("outline_size", 8);
        _hint.SetAnchorsPreset(Control.LayoutPreset.Center);
        _hint.Position = new Vector2(-200, 40);
        _hint.CustomMinimumSize = new Vector2(400, 0);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(_hint);

        // Gece paneli: ust orta
        _nightBox = new VBoxContainer { Visible = false, Alignment = BoxContainer.AlignmentMode.Center };
        _nightBox.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _nightBox.Position = new Vector2(-230, 20);
        _nightBox.CustomMinimumSize = new Vector2(460, 0);
        _wave = L("", 26, Palette.MayaTeal, true); _wave.HorizontalAlignment = HorizontalAlignment.Center; _wave.AddThemeConstantOverride("outline_size", 10);
        _gosteri = L("", 22, Palette.Warning, true); _gosteri.HorizontalAlignment = HorizontalAlignment.Center; _gosteri.AddThemeConstantOverride("outline_size", 8);
        _oz = new ProgressBar { CustomMinimumSize = new Vector2(460, 16), ShowPercentage = false };
        var ozFill = Box(Palette.MayaViolet, 8); ozFill.ContentMarginTop = ozFill.ContentMarginBottom = 0;
        _oz.AddThemeStyleboxOverride("fill", ozFill);
        _oz.AddThemeStyleboxOverride("background", Box(new Color(Palette.NightDeep, 0.75f), 8));
        _level = L("", 18, Palette.OffWhite, true); _level.HorizontalAlignment = HorizontalAlignment.Center; _level.AddThemeConstantOverride("outline_size", 8);
        _nightBox.AddChild(_wave);
        _nightBox.AddChild(_gosteri);
        _nightBox.AddChild(_oz);
        _nightBox.AddChild(_level);
        root.AddChild(_nightBox);

        // Kart seridi: alt, kemerin ustunde
        _cards = new HBoxContainer { Visible = false, Alignment = BoxContainer.AlignmentMode.Center };
        _cards.AddThemeConstantOverride("separation", 18);
        _cards.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        root.AddChild(_cards);

        _bigText = L("", 64, Palette.Warning, true);
        _bigText.AddThemeConstantOverride("outline_size", 18);
        _bigText.SetAnchorsPreset(Control.LayoutPreset.Center);
        _bigText.HorizontalAlignment = HorizontalAlignment.Center;
        _bigText.CustomMinimumSize = new Vector2(900, 0);
        _bigText.Position = new Vector2(-450, -170);
        root.AddChild(_bigText);

        _menu = new PanelContainer { Visible = false };
        _menu.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.92f), 18, Palette.Warning, 3));
        var mv = new VBoxContainer { CustomMinimumSize = new Vector2(720, 0) };
        mv.AddThemeConstantOverride("separation", 8);
        _menuTitle = L("", 30, Palette.Warning, true);
        _menuList = new VBoxContainer();
        _menuList.AddThemeConstantOverride("separation", 6);
        _menuFoot = L("", 17, Palette.Fog);
        _menuFoot.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _menuFoot.CustomMinimumSize = new Vector2(700, 0);
        mv.AddChild(_menuTitle); mv.AddChild(new HSeparator()); mv.AddChild(_menuList); mv.AddChild(new HSeparator()); mv.AddChild(_menuFoot);
        _menu.AddChild(mv);
        root.AddChild(_menu);
        _report = new PanelContainer { Visible = false };
        _report.AddThemeStyleboxOverride("panel", Box(new Color(Palette.OffWhite, 0.96f), 22, Palette.CoopBlue, 5));
        _report.SetAnchorsPreset(Control.LayoutPreset.Center);
        root.AddChild(_report);
        BuildGeceHud(root); // Hud.Compass.cs: pusula, adim satiri, Kabarma panosu, envanter ve Defter
        OnHead(0);
        IgnoreMouse(root);
    }

    /// <summary>Panoda tiklanacak bir sey yok: hicbir oge fareyi oyundan almasin (nisangah dahil).</summary>
    static void IgnoreMouse(Node n)
    {
        if (n is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var ch in n.GetChildren()) IgnoreMouse(ch);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var vp = GetViewport().GetVisibleRect().Size;
        _belt.Position = new Vector2((vp.X - _belt.Size.X) / 2f, vp.Y - _belt.Size.Y - 18);
        _headTag.Position = new Vector2(vp.X / 2f + 40, vp.Y / 2f + 34);
        if (_wheel.Visible) LayoutWheel(vp);
        if (Engine.GetProcessFrames() % 6 == 0) RefreshBelt();
        // Can ve canta kemerin ustunde, sol altta; dar ekranda kemerle cakismaz.
        _bottomLeft.Position = new Vector2(24, vp.Y - _bottomLeft.Size.Y - (_belt.Size.Y + 30));
        _cards.Position = new Vector2((vp.X - _cards.Size.X) / 2f, vp.Y - _belt.Size.Y - _cards.Size.Y - 40);
        _nightBox.Position = new Vector2((vp.X - _nightBox.Size.X) / 2f, 18);
        _goalPanel.Position = new Vector2(vp.X - _goalPanel.Size.X - 24, 24);
        if (_report.Visible) _report.Position = (vp - _report.Size) / 2f;
        if (_menu.Visible) { _menu.Position = new Vector2((vp.X - _menu.Size.X) / 2f, vp.Y * 0.18f); if (Engine.GetProcessFrames() % 10 == 0) RefreshMenu(); }

        // Suna satirlari: daktilo
        if (_full.Length > 0)
        {
            _typeT += dt * 55f;
            int n = Mathf.Min(_full.Length, (int)_typeT);
            _sunaText.Text = _full.Substring(0, n);
            _lineT -= dt;
            if (_lineT <= 0) { _full = ""; _suna.Visible = false; }
        }
        if (_full.Length == 0 && _lines.Count > 0)
        {
            _full = _lines.Dequeue();
            _typeT = 0;
            _lineT = Mathf.Clamp(_full.Length * 0.055f, 2.8f, 6f) * (_lines.Count > 1 ? 0.6f : 1f);
            _suna.Visible = true;
            Sfx.Play("place", null, -22f); // telsiz mandali
        }

        UpdateDayPanels(); // Hud.Day.cs

        _hintT -= dt;
        if (_hintT <= 0) _hint.Text = "";
        _bigT -= dt;
        if (_bigT <= 0) _bigText.Text = "";
        else _bigText.Modulate = new Color(1, 1, 1, Mathf.Clamp(_bigT, 0, 1));
        float hurt = 1f - _d.Player.Hp / _d.Player.MaxHp;
        _vignette.Color = new Color(Palette.HoldingRed, _d.Player.Down ? 0.35f : hurt * 0.12f);

        UpdateNightBox(); // Hud.Night.cs
        _crosshair.Visible = !_report.Visible && !_menu.Visible && !_wheel.Visible;
        _headTag.Visible = _crosshair.Visible;
        UpdateGeceHud(dt); // Hud.Compass.cs
    }

    public void Say(string line) { if (!string.IsNullOrEmpty(line)) _lines.Enqueue(line); }
    public bool Talking => _full.Length > 0 || _lines.Count > 0;

    public void Hint(string text, float seconds = 0.25f) { _hint.Text = text; _hintT = seconds; }

    public void Big(string text, Color color, float seconds = 2.5f)
    {
        _bigText.Text = text;
        _bigText.AddThemeColorOverride("font_color", color);
        _bigT = seconds;
    }
}
