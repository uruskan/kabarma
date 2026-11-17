using System;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Rust tarzi arayuz (SANAT-SES 8.1 "Gorsel dil", URETIM 6 M1 karari 20). Pano tek bir dolaysiz kip katmaninda cizilir
/// (<see cref="UiPainter"/>; basvuru Hollow-Universe GroundUi): alt ortada kemer, sag altta can ve dayaniklilik, ust ortada
/// pusula ve Defter adimi, nisangahin yaninda etkilesim istemi, sagda toplanan esya, sol ustte Suna. Envanter ve cark ayni
/// katmanda, arkalarinda bulanik ekran.
/// Eski Control dugumleri (Hud._Ready kurar; baska akislarin parcalari alanlarina yazar) gizli bir tutucuya alinir: alanlar
/// ve davranislari yerinde kalir, yalniz gorunmezler. Hasat Raporu ve Defter sayfasi Control olarak kalir, yeni dilde.
/// </summary>
public partial class Hud
{
    UiPainter _canvas;
    ColorRect _blur;
    ShaderMaterial _blurMat;
    Control _legacy;
    double _now;

    /// <summary>Dayaniklilik cubugunun degeri (deger, en cok, tukendi): oyuncu akisinin Sim'i (GameState.Stamina).</summary>
    public Func<(float value, float max, bool exhausted)> StaminaSource { get; set; }

    (float, float, bool) ReadStamina()
    {
        var s = _d?.State?.Stamina;
        return s == null ? (1f, 1f, false) : (s.Value, s.Max, s.Exhausted);
    }

    const string BlurShader = @"shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float blur = 2.5;
uniform float dim = 0.86;
void fragment() {
    vec3 c = textureLod(screen_texture, SCREEN_UV, blur).rgb * dim;
    float g = fract(sin(dot(SCREEN_UV * 731.0, vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
    COLOR = vec4(c + g * 0.012, 1.0);
}";

    /// <summary>BuildGeceHud'dan bir kez (Hud._Ready sonu).</summary>
    void BuildRustUi(Control root)
    {
        T.Load(_d.Data.Ui);
        AddChild(new UiScaler { Name = "UiScaler" });
        StaminaSource ??= ReadStamina; // eski ince cubuk (Hud.Stamina.cs) gizli tutucuda; sag alttaki cubuk bunu cizer

        // Eski dugumler gizli tutucuya: alanlari ve kendi Visible bayraklari yerinde (MenuOpen, WheelOpen onlara bakar).
        _legacy = new Control { Name = "Legacy", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(_legacy);
        foreach (var c in new Control[] { _suna, _goalPanel, _belt, _headTag, _wheel, _bottomLeft, _crosshair, _hint, _nightBox, _cards, _bigText, _menu })
            if (c != null && c.GetParent() == root) c.Reparent(_legacy, false);

        _blur = new ColorRect { Name = "UiBlur", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _blur.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _blurMat = new ShaderMaterial { Shader = new Shader { Code = BlurShader } };
        _blur.Material = _blurMat;
        root.AddChild(_blur);
        _canvas = new UiPainter { Name = "UiCanvas", Paint = PaintRust };
        root.AddChild(_canvas);
        // Rapor her seyin ustunde kalsin.
        root.MoveChild(_report, root.GetChildCount() - 1);
        _report.Theme = T.Theme; // UiTheme.Theme: Barlow, olcekli boy, duz panel
        _report.AddThemeStyleboxOverride("panel", FlatBox(T.Dialog, 28, 22));

        _d.State.Backpack.Changed += OnBackpackChanged; // Hud.Feed.cs
        SnapshotBackpack();
        T.ScaleChanged += OnUiScale;
    }

    void OnUiScale()
    {
        if (!IsInstanceValid(this)) return;
        if (_report.Visible && _lastReport != null) ShowReport(_lastReport); // rapor yeni olcekte yeniden kurulur
        if (InventoryOpen && _invPage == 1) RebuildDefter();
    }

    public override void _ExitTree() => T.ScaleChanged -= OnUiScale;

    /// <summary>Kose yuvarlatmasiz duz panel (Rust): Control olarak kalan rapor ve Defter icin.</summary>
    static StyleBoxFlat FlatBox(Color bg, float marginX = 16, float marginY = 12)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            ContentMarginLeft = T.Px(marginX), ContentMarginRight = T.Px(marginX),
            ContentMarginTop = T.Px(marginY), ContentMarginBottom = T.Px(marginY),
        };
    }

    /// <summary>Yeni dilde etiket (Barlow, olcekli boy): rapor ve Defter.</summary>
    Label UL(string text, float size, Color c, bool bold = false, bool condensed = false)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", T.Font(size, bold, condensed));
        l.AddThemeFontSizeOverride("font_size", T.Px(size));
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    /// <summary>UpdateGeceHud'dan her kare.</summary>
    void UpdateRustUi(float dt)
    {
        _now = Time.GetTicksMsec() / 1000.0;
        bool blur = InventoryOpen || WheelOpen;
        _blur.Visible = blur;
        if (blur)
        {
            var w = T.Data.Wheel;
            var inv = T.Data.Inventory;
            _blurMat.SetShaderParameter("blur", InventoryOpen ? inv.Blur : w.Blur);
            _blurMat.SetShaderParameter("dim", InventoryOpen ? inv.Dim : w.Dim);
        }
        UpdatePrompt();
        UpdateWheelPad();
        UpdateInventoryFrame(dt);
        _canvas.QueueRedraw();
    }

    /// <summary>Cizim sirasi: pano, ortadakiler, Suna, envanter, cark, buyuk yazi, surukleme ve ipucu.</summary>
    void PaintRust(UiPainter p)
    {
        _p = p;
        bool inv = InventoryOpen, wheel = WheelOpen, report = _report.Visible;
        bool down = _d.Player.Down;
        if (!inv && !report)
        {
            PaintCompass();   // Hud.Compass.cs
            PaintKabarma();   // Hud.Night.cs
        }
        if (!inv && !wheel && !report && !down) PaintCentre();
        if (!report)
        {
            PaintBelt(inv);   // Hud.Belt.cs
            PaintVitals(inv);
        }
        if (!inv && !report)
        {
            PaintFeed();      // Hud.Feed.cs
            PaintOffer();     // Hud.Night.cs
        }
        PaintSuna();
        if (inv && _hintT > 0 && !string.IsNullOrEmpty(_hint.Text))
        {
            _o = TopFrame; // envanterde kisa not sekmelerin altinda (ortada izgara var)
            CentreText(_hint.Text, 64, T.Data.Type.Body, T.Ink, true, false, true);
        }
        if (inv) PaintInventory();   // Hud.Inventory.cs
        if (wheel) PaintWheel();     // Hud.Wheel.cs
        PaintBig();
        if (inv) PaintDragAndTooltip();
    }

    // ---------- Orta: nisangah, istem, kisa not ----------

    void PaintCentre()
    {
        _o = CentreFrame;
        var pr = T.Data.Prompt;
        float cx = T.DesignW / 2f, cy = T.DesignH / 2f;
        Disc(cx, cy, pr.CrosshairDot * 0.5f + 1.2f, T.A(T.Shade, 0.6f));
        Disc(cx, cy, pr.CrosshairDot * 0.5f, T.Ink);
        if (_promptVerb != null)
        {
            float h = 26, x = cx + pr.Dx, y = cy + pr.Dy;
            float w = Keycap(_promptKey, x, y, h, T.Ink, PadDevice);
            float tx = x + w + 9;
            float vw = Text(T.Upper(_promptVerb), tx, y - 1, T.Data.Type.Prompt, T.Ink, true, true, true);
            if (!string.IsNullOrEmpty(_promptObject))
                Text(_promptObject, tx + vw + 8, y - 1, T.Data.Type.Prompt, T.A(T.Ink, 0.78f), false, true, true);
        }
        if (_hintT > 0 && !string.IsNullOrEmpty(_hint.Text) && !(_promptVerb != null && (_hint.Text.StartsWith("E: ") || _hint.Text.StartsWith(G("{act:interact}")))))
            CentreText(_hint.Text, pr.HintY, T.Data.Type.Body + 2, T.Ink, true, false, true);
    }

    // ---------- Etkilesim istemi (glif + fiil + nesne) ----------

    string _promptKey = "E", _promptVerb, _promptObject;

    /// <summary>
    /// E'nin su an ne yapacagi (Director.Interact ile ayni sira): istasyon, Kargo Rampasi, Gidak'a bugday. Yoksa istem yok.
    /// </summary>
    void UpdatePrompt()
    {
        _promptVerb = null;
        _promptObject = null;
        if (MenuOpen || _report.Visible || WheelOpen || _d.Player.Down) return;
        var p = _d.Player.GlobalPosition;
        _promptKey = KeyText("interact", PadDevice ? "X" : "E");
        var st = _d.Stations.Where(s => s.InReach(p)).OrderBy(s => s.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (st != null) { _promptVerb = "Kullan"; _promptObject = st.Def.Name; return; }
        if (_d.Pod != null && _d.Pod.IsRamp && new Vector2(p.X - _d.Pod.PadCenter.X, p.Z - _d.Pod.PadCenter.Z).Length() < 4.5f)
        { _promptVerb = "Sat"; _promptObject = "Kargo Rampası"; return; }
        var g = _d.Gidak;
        if (g != null && g.Visible && !g.Swollen && g.GlobalPosition.DistanceTo(p) < 3f && _d.State.Backpack.Count("wheat") > 0)
        { _promptVerb = "Buğday ver"; _promptObject = "Gıdak"; }
    }

    // ---------- Suna (sol ust) ----------

    void PaintSuna()
    {
        if (_suna == null || !_suna.Visible || string.IsNullOrEmpty(_sunaText.Text)) return;
        _o = TopLeftFrame;
        float x = 16, y = 16, w = 470, pad = 14;
        float size = T.Data.Type.Body + 1;
        var lines = WrapLines(_full, w - 2 * pad, size); // tam metinle sar: daktilo satir atlatmasin
        float h = pad + 18 + 6 + lines.Count * (size + 5) + pad - 4;
        Rect(x, y, w, h, T.A(T.Dialog, 0.86f));
        Rect(x, y, 3, h, T.Teal);
        Text("SUNA · KOOPERATİF SANTRALİ", x + pad, y + pad - 2, T.Data.Type.Caption + 2, T.Teal, true, true);
        // Daktilo: gorunen harf sayisi kadar
        int shown = _sunaText.Text.Length, used = 0;
        for (int i = 0; i < lines.Count && used < shown; i++)
        {
            string l = lines[i];
            int take = Math.Min(l.Length, shown - used);
            Text(l[..take], x + pad, y + pad + 22 + i * (size + 5), size, T.Ink);
            used += l.Length + 1;
        }
    }

    // ---------- Buyuk yazi (GUN 2, DALGA 1, SAFAK) ----------

    void PaintBig()
    {
        if (_bigT <= 0 || string.IsNullOrEmpty(_bigText.Text)) return;
        _o = CentreFrame;
        var c = _bigText.GetThemeColor("font_color");
        float a = Mathf.Clamp(_bigT, 0, 1);
        float size = T.Data.Type.Big;
        string text = T.Upper(_bigText.Text);
        while (size > 24 && Measure(text, size, true, true) > T.DesignW - 120) size -= 4;
        CentreText(text, 250, size, new Color(c, a), true, true, true);
    }
}
