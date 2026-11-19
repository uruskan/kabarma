using Godot;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Tek panel dili (SANAT-SES §8.1 Rust tarzi; M6 arayuz cilasi): koyu sicak gri, yuvarlak olmayan koseler, sol kenarda uyari rengi serit,
/// Barlow yazi. Eski paneller (Suna kutusu, ses, tus atama, F1 his, fotograf modu) bunu kullanir; yeni paneller de.
/// </summary>
public static class UiPanels
{
    /// <summary>Rust tarzi panel: tint verilirse zemin o renk (yari saydam), kenar serit rengi accent.</summary>
    public static StyleBoxFlat Box(Color? bg = null, Color? accent = null, float alpha = 0.92f, int margin = 14)
    {
        var c = bg ?? T.Dialog;
        return new StyleBoxFlat
        {
            BgColor = new Color(c.R, c.G, c.B, alpha),
            BorderColor = accent ?? T.Warning,
            BorderWidthLeft = T.Px(4),
            ContentMarginLeft = T.Px(margin + 4), ContentMarginRight = T.Px(margin), ContentMarginTop = T.Px(margin - 4), ContentMarginBottom = T.Px(margin - 4),
        };
    }

    /// <summary>Paneli (PanelContainer) tek dile cevirir ve Barlow temasini verir.</summary>
    public static T0 Skin<T0>(T0 panel, Color? bg = null, Color? accent = null) where T0 : PanelContainer
    {
        panel.Theme = T.Theme;
        panel.AddThemeStyleboxOverride("panel", Box(bg, accent));
        return panel;
    }

    public static Label Title(string text, float size = 30, Color? color = null)
    {
        var l = new Label { Text = T.Upper(text), MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", T.Font(size, bold: true, condensed: true));
        l.AddThemeFontSizeOverride("font_size", T.Px(size));
        l.AddThemeColorOverride("font_color", color ?? T.Ivory);
        return l;
    }

    public static Label Text(string text, float size = 18, Color? color = null, bool bold = false)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontOverride("font", T.Font(size, bold));
        l.AddThemeFontSizeOverride("font_size", T.Px(size));
        l.AddThemeColorOverride("font_color", color ?? T.Ink);
        return l;
    }
}
