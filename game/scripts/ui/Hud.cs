using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Pano: gunduz sade (kemer, can, pusula yerine Defter), gece yogun (dalga, Oz, Gosteri, kart seridi). SANAT-SES §8.
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
    Label _goal, _goalProgress;
    PanelContainer _goalPanel;
    HBoxContainer _belt;
    readonly List<PanelContainer> _beltSlots = new();
    Label _bag;
    ProgressBar _hp;
    Label _hint;
    float _hintT;
    Control _crosshair;
    VBoxContainer _nightBox;
    Label _wave, _level, _gosteri;
    ProgressBar _oz;
    HBoxContainer _cards;
    PanelContainer _report;
    Label _bigText;
    float _bigT;
    ColorRect _vignette;
    VBoxContainer _bottomLeft;

    public bool ReportOpen => _report != null && _report.Visible;

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

        // Kemer: alt orta
        _belt = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _belt.AddThemeConstantOverride("separation", 8);
        _belt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        for (int i = 0; i < Capa.HeadNames.Length; i++)
        {
            var p = new PanelContainer { CustomMinimumSize = new Vector2(118, 64) };
            var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            var k = L($"{i + 1}", 15, Palette.Warning, true); k.HorizontalAlignment = HorizontalAlignment.Center;
            var n = L(Capa.HeadNames[i], 17, Palette.OffWhite, true); n.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(k); v.AddChild(n);
            p.AddChild(v);
            _belt.AddChild(p);
            _beltSlots.Add(p);
        }
        root.AddChild(_belt);

        // Can ve canta: sol alt
        var bl = new VBoxContainer();
        _bottomLeft = bl;
        _hp = new ProgressBar { CustomMinimumSize = new Vector2(280, 22), MaxValue = 100, Value = 100, ShowPercentage = false };
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

        _report = new PanelContainer { Visible = false };
        _report.AddThemeStyleboxOverride("panel", Box(new Color(Palette.OffWhite, 0.96f), 22, Palette.CoopBlue, 5));
        _report.SetAnchorsPreset(Control.LayoutPreset.Center);
        root.AddChild(_report);
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
        // Can ve canta kemerin ustunde, sol altta; dar ekranda kemerle cakismaz.
        _bottomLeft.Position = new Vector2(24, vp.Y - _bottomLeft.Size.Y - (_belt.Size.Y + 30));
        _cards.Position = new Vector2((vp.X - _cards.Size.X) / 2f, vp.Y - _belt.Size.Y - _cards.Size.Y - 40);
        _nightBox.Position = new Vector2((vp.X - _nightBox.Size.X) / 2f, 18);
        _goalPanel.Position = new Vector2(vp.X - _goalPanel.Size.X - 24, 24);
        if (_report.Visible) _report.Position = (vp - _report.Size) / 2f;

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

        var s = _d.State;
        var step = s.Scenario.Current;
        if (step != null)
        {
            _goal.Text = step.Goal;
            _goalProgress.Text = step.Target > 1 ? $"{s.Scenario.Progress} / {step.Target}" : "";
        }
        else
        {
            _goal.Text = "Defter: 240 iş emri";
            _goalProgress.Text = "Sıradaki: 5 kabarık ürün yetiştir";
        }

        _hp.Value = _d.Player.Hp / _d.Player.MaxHp * 100f;
        _bag.Text = $"Taş {s.Backpack.Count("stone")}   Tohum {s.Backpack.Count("turnip_seed")}   Buğday {s.Backpack.Count("wheat")}   Yumurta {s.Eggs}   ·   {s.Credits} Kredi";

        _hintT -= dt;
        if (_hintT <= 0) _hint.Text = "";
        _bigT -= dt;
        if (_bigT <= 0) _bigText.Text = "";
        else _bigText.Modulate = new Color(1, 1, 1, Mathf.Clamp(_bigT, 0, 1));
        float hurt = 1f - _d.Player.Hp / _d.Player.MaxHp;
        _vignette.Color = new Color(Palette.HoldingRed, _d.Player.Down ? 0.35f : hurt * 0.12f);

        var night = _d.Night;
        _nightBox.Visible = night != null && night.Phase != NightPhase.Dawn;
        if (_nightBox.Visible)
        {
            int waves = _d.Data.Kabarma.FirstNight.Waves.Count;
            _wave.Text = night.Phase == NightPhase.Alarm ? "KABARMA GELİYOR" : $"KÜÇÜK KABARMA · Dalga {night.WaveIndex + 1}/{waves}";
            _gosteri.Text = $"Gösteri ×{night.Gosteri:0.0}";
            int lv = night.Level;
            float lo = lv == 0 ? 0 : night.Threshold(lv), hi = night.Threshold(lv + 1);
            bool capped = lv >= _d.Data.Kabarma.FirstNight.MaxCardLevels;
            _oz.Value = capped ? 100 : Mathf.Clamp((night.Oz - lo) / (hi - lo) * 100f, 0, 100);
            _level.Text = capped ? $"Seviye {lv} · Öz dolu · {night.Kills} kabarık söndü" : $"Seviye {lv} · Öz {(int)night.Oz}/{(int)hi} · {night.Kills} kabarık söndü";
        }
        _crosshair.Visible = !_report.Visible;
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

    public void OnHead(int h)
    {
        for (int i = 0; i < _beltSlots.Count; i++)
        {
            bool on = i == h;
            _beltSlots[i].AddThemeStyleboxOverride("panel", Box(on ? new Color(Capa.HeadColors[i].Darkened(0.35f), 0.95f) : new Color(Palette.NightDeep, 0.7f), 12, on ? Palette.Warning : new Color(Palette.OffWhite, 0.25f), on ? 3 : 2));
        }
    }

    /// <summary>Kart secimi: oyun durmaz, kartlar altta belirir, tek tusla secilir (GDD 15).</summary>
    public void ShowOffer(CardOffer offer, KabarmaNight night)
    {
        foreach (var c in _cards.GetChildren()) c.QueueFree();
        _cards.Visible = offer != null;
        if (offer == null) return;
        string[] keys = { "Z", "X", "C" };
        for (int i = 0; i < offer.Cards.Count; i++)
        {
            var card = offer.Cards[i];
            int rank = night.Rank(card.Id);
            var rc = card.Rarity switch { "N" => Palette.CoopBlue.Lerp(Palette.MayaTeal, 0.3f), "D" => Palette.MayaViolet, "E" => Palette.Warning, _ => Palette.OffWhite.Lerp(Palette.Shadow, 0.3f) };
            var p = new PanelContainer { CustomMinimumSize = new Vector2(250, 150) };
            p.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.93f), 16, rc, 4));
            var v = new VBoxContainer();
            v.AddChild(L($"[{keys[i]}]  " + (card.Rarity switch { "N" => "Nadir", "D" => "Destansı", "E" => "Efsane", _ => "Sıradan" }), 16, rc, true));
            v.AddChild(L(card.Name, 30, Palette.OffWhite, true));
            var t = L(card.Text, 19, Palette.Fog);
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            v.AddChild(t);
            v.AddChild(L(rank > 0 ? $"Rütbe {rank} → {rank + 1}" : "Yeni", 16, Palette.MayaTeal, true));
            p.AddChild(v);
            _cards.AddChild(p);
            p.Scale = new Vector2(0.6f, 0.6f);
            p.PivotOffset = new Vector2(125, 150);
            p.CreateTween().TweenProperty(p, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        IgnoreMouse(_cards);
    }

    /// <summary>Hasat Raporu (PLAN 1.4, 10:00).</summary>
    public void ShowReport(GameState s, KabarmaNight n, int swollenLeft)
    {
        foreach (var c in _report.GetChildren()) c.QueueFree();
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        v.AddThemeConstantOverride("separation", 6);
        v.AddChild(L("HASAT RAPORU · Gece 1", 34, Palette.CoopBlue, true));
        v.AddChild(L("Küçük Kabarma atlatıldı.", 20, Palette.DarkGrey));
        v.AddChild(new HSeparator());
        void Row(string a, string b)
        {
            var h = new HBoxContainer();
            var l = L(a, 22, Palette.DarkGrey); l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            h.AddChild(l);
            h.AddChild(L(b, 22, Palette.Accent, true));
            v.AddChild(h);
        }
        Row("Toplanan Maya", $"{(int)(n?.Maya ?? 0)}");
        Row("Söndürülen kabarık", $"{n?.Kills ?? 0}");
        Row("Seçilen kart", $"{n?.CardsCount ?? 0}");
        Row("En yüksek Gösteri", $"×{n?.PeakGosteri ?? 1:0.0}");
        Row("Kabarık ürün (tarlada)", $"{swollenLeft}");
        int sold = 0; foreach (var x in s.Sales) sold += x.value;
        Row("Kargo satışı", $"+{sold} Kredi");
        Row("Yumurta", $"{s.Eggs}");
        v.AddChild(new HSeparator());
        v.AddChild(L($"Defter açıldı: {_d.Data.Scenario.OrdersTotal} iş emri.", 22, Palette.CoopBlue, true));
        v.AddChild(L(_d.Data.Scenario.NextOrder, 22, Palette.DarkGrey, true));
        v.AddChild(L("Enter: rapor kapanır, çiftlik seninle.", 17, Palette.Shadow));
        _report.AddChild(v);
        IgnoreMouse(_report);
        _report.Visible = true;
        _report.Scale = new Vector2(0.8f, 0.8f);
        _report.CreateTween().TweenProperty(_report, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void HideReport() => _report.Visible = false;
}
