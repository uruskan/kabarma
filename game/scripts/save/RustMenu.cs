using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Rust tarzi menu iskeleti (SANAT-SES 8.1, karar 20; kod ve cizim ozgun): solda buyuk dar harfli dugme sutunu, sagda
/// icerik paneli (yuvalar, ayarlar, onay). Dugmeler Godot Button: klavye ve oyun kolu odagi (ui_up/ui_down/ui_accept)
/// kendiliginden calisir. Ana menu ve oyun ici Esc menusu bunu kullanir.
/// </summary>
public partial class RustMenu : CanvasLayer
{
    protected Control Root, Content;
    protected VBoxContainer Column;
    readonly List<Button> _buttons = new();

    protected RustMenu() { Layer = 50; }

    const string BlurShader = @"shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
void fragment() { COLOR = vec4(textureLod(screen_texture, SCREEN_UV, 3.0).rgb * 0.38, 1.0); }";

    /// <summary>Iskeleti kurar. dimBackground: oyunun ustunde yari saydam (Esc), degilse koyu zemin (ana menu).</summary>
    protected void Build(string title, string subtitle, bool dimBackground)
    {
        Root = new Control { Name = "Root", Theme = T.Theme };
        Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(Root);
        // Esc: oyunun ustunde bulanik ve koyu (Rust); ana menu: koyu duz zemin.
        var bg = new ColorRect { Color = dimBackground ? new Color(0, 0, 0, 1) : T.Shade, MouseFilter = Control.MouseFilterEnum.Stop };
        if (dimBackground) bg.Material = new ShaderMaterial { Shader = new Shader { Code = BlurShader } };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Root.AddChild(bg);
        if (!dimBackground)
        {
            // Zeminde sicak bir isik ve ince serit: Rust'un fotografli zemini yerine sade, ozgun bir kompozisyon.
            var glow = new ColorRect { Color = T.A(T.Brick, 0.10f), MouseFilter = Control.MouseFilterEnum.Ignore };
            glow.SetAnchorsPreset(Control.LayoutPreset.RightWide);
            glow.OffsetLeft = -T.Px(900);
            Root.AddChild(glow);
            var stripe = new ColorRect { Color = T.A(T.Warning, 0.85f), MouseFilter = Control.MouseFilterEnum.Ignore };
            stripe.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
            stripe.OffsetRight = T.Px(6);
            Root.AddChild(stripe);
        }

        var left = new VBoxContainer { Name = "Left" };
        left.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        left.OffsetLeft = T.Px(96);
        left.OffsetRight = T.Px(96 + 520);
        left.OffsetTop = T.Px(140);
        left.AddThemeConstantOverride("separation", T.Px(4));
        Root.AddChild(left);
        left.AddChild(Text(T.Upper(title), 92, T.Ivory, bold: true, condensed: true));
        if (!string.IsNullOrEmpty(subtitle)) left.AddChild(Text(subtitle, 20, T.Muted));
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, T.Px(48)) });
        Column = new VBoxContainer();
        Column.AddThemeConstantOverride("separation", T.Px(2));
        left.AddChild(Column);

        Content = new VBoxContainer { Name = "Content" };
        Content.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        Content.OffsetLeft = -T.Px(820);
        Content.OffsetRight = -T.Px(96);
        Content.OffsetTop = T.Px(250);
        Content.OffsetBottom = -T.Px(96);
        ((VBoxContainer)Content).AddThemeConstantOverride("separation", T.Px(6));
        Root.AddChild(Content);
    }

    /// <summary>Buyuk sol sutun dugmesi (dar kalin, buyuk harf). disabledNote: kapali dugmenin yanindaki not.</summary>
    protected Button Item(string label, Action press, bool enabled = true, string disabledNote = null)
    {
        var b = new Button { Text = T.Upper(label) + (enabled || disabledNote == null ? "" : $"   {disabledNote}"), Disabled = !enabled, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All };
        Style(b, 40);
        b.Pressed += () => press?.Invoke();
        Column.AddChild(b);
        _buttons.Add(b);
        return b;
    }

    /// <summary>Icerik panelindeki satir dugmesi (yuva, onay).</summary>
    protected Button Row(string label, Action press, bool enabled = true, float size = 22)
    {
        var b = new Button { Text = label, Disabled = !enabled, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All };
        Style(b, size, panel: true);
        b.Pressed += () => press?.Invoke();
        Content.AddChild(b);
        return b;
    }

    protected static void Style(Button b, float size, bool panel = false)
    {
        b.AddThemeFontOverride("font", T.Font(size, bold: true, condensed: !panel));
        b.AddThemeFontSizeOverride("font_size", T.Px(size));
        b.AddThemeColorOverride("font_color", panel ? T.Ink : T.A(T.Ivory, 0.86f));
        b.AddThemeColorOverride("font_hover_color", T.Ivory);
        b.AddThemeColorOverride("font_focus_color", T.Ivory);
        b.AddThemeColorOverride("font_pressed_color", T.Warning);
        b.AddThemeColorOverride("font_disabled_color", T.A(T.Muted, 0.6f));
        StyleBoxFlat Box(Color c, bool edge = false) => new()
        {
            BgColor = c,
            ContentMarginLeft = T.Px(panel ? 18 : 14), ContentMarginRight = T.Px(14), ContentMarginTop = T.Px(panel ? 10 : 2), ContentMarginBottom = T.Px(panel ? 10 : 2),
            BorderWidthLeft = edge ? T.Px(4) : 0, BorderColor = T.Warning,
        };
        b.AddThemeStyleboxOverride("normal", Box(panel ? T.Panel : new Color(0, 0, 0, 0)));
        b.AddThemeStyleboxOverride("hover", Box(panel ? T.PanelHover : T.A(T.Panel, 0.7f), true));
        b.AddThemeStyleboxOverride("focus", Box(panel ? T.PanelHover : T.A(T.Panel, 0.7f), true));
        b.AddThemeStyleboxOverride("pressed", Box(T.Selected, true));
        b.AddThemeStyleboxOverride("disabled", Box(panel ? T.A(T.Panel, 0.5f) : new Color(0, 0, 0, 0)));
    }

    protected static Label Text(string text, float size, Color c, bool bold = false, bool condensed = false)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontOverride("font", T.Font(size, bold, condensed));
        l.AddThemeFontSizeOverride("font_size", T.Px(size));
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    /// <summary>Sinama: yazisi bu onekle baslayan dugme (sol sutun ya da icerik paneli).</summary>
    public Button FindButton(string prefix) =>
        Root?.FindChildren("*", "Button", true, false).OfType<Button>().FirstOrDefault(b => b.Text.StartsWith(prefix, StringComparison.Ordinal) && !b.IsQueuedForDeletion());

    protected void ClearContent()
    {
        foreach (var c in Content.GetChildren()) { Content.RemoveChild(c); c.QueueFree(); }
    }

    protected void Header(string text, string note = null)
    {
        Content.AddChild(Text(T.Upper(text), 34, T.Ivory, bold: true, condensed: true));
        if (note != null) Content.AddChild(Text(note, 18, T.Muted));
        Content.AddChild(new Control { CustomMinimumSize = new Vector2(0, T.Px(10)) });
    }

    protected void FocusFirst()
    {
        foreach (var b in _buttons) if (!b.Disabled) { b.CallDeferred(Control.MethodName.GrabFocus); return; }
    }

    /// <summary>Yuvanin satir yazisi: "YUVA 2  ·  Gun 4  ·  18.11.2026 14:20  ·  1 sa 12 dk".</summary>
    public static string SlotLine(SaveSlotInfo s)
    {
        string name = T.Upper(s.Slot.Replace("yuva", "yuva "));
        if (!s.Exists) return $"{name}  ·  boş";
        if (s.Manifest == null) return $"{name}  ·  okunamadı ({s.Error})";
        var m = s.Manifest;
        string when = DateTime.TryParse(m.SavedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) : m.SavedUtc;
        var play = TimeSpan.FromSeconds(m.PlaySeconds);
        string dur = play.TotalHours >= 1 ? $"{(int)play.TotalHours} sa {play.Minutes} dk" : $"{play.Minutes} dk";
        return $"{name}  ·  Gün {m.Day}  ·  {when}  ·  {dur}";
    }
}
