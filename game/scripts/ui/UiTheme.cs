using System;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arayuz tasarim sistemi (SANAT-SES 8.1 "Gorsel dil: Rust tarzi arayuz", URETIM 6 M1 karari 20): palet, yazi rampasi,
/// olcek. Basvuru Hollow-Universe Common/Ui.cs (sahibin acik kaynak projesi), Kabarma'ya uyarlandi. Her uzunluk 1600 x 900
/// tasarim tuvalinde verilir ve <see cref="Px"/> ile ekranin gercek pikseline yuvarlanir: arayuz gerdirilmez, her olcekte
/// kendi boyunda rasterlanir (keskin kenar, keskin yazi). Sayilar ve renkler ui.json'da (<see cref="UiFile"/>).
/// </summary>
public static class UiTheme
{
    static UiFile _d = new();
    public static UiFile Data => _d;

    /// <summary>Arayuz olcegi: 1080p'ye dek 1, 1440p'de 1,33; 1280 x 720'de 0,8 (<see cref="UiScaler"/>).</summary>
    public static float Scale { get; private set; } = 1f;
    public static event Action ScaleChanged;

    public static float DesignW => _d.Design.Width;
    public static float DesignH => _d.Design.Height;

    // ---------- Palet (ui.json "palette") ----------
    public static Color Ink, Muted, Red, Green, Panel, PanelHover, HudSlot, Selected, SelectedEdge, Health, Stamina,
        Ivory, Brick, Short, Warning, Tooltip, Star, Dialog, Toast, Shade, Locked, Oz, Teal;

    public static void Load(UiFile d)
    {
        _d = d ?? new UiFile();
        Ink = C("ink"); Muted = C("muted"); Red = C("red"); Green = C("green");
        Panel = C("panel"); PanelHover = C("panelHover"); HudSlot = C("hudSlot");
        Selected = C("selected"); SelectedEdge = C("selectedEdge");
        Health = C("health"); Stamina = C("stamina");
        Ivory = C("ivory"); Brick = C("brick"); Short = C("short"); Warning = C("warning");
        Tooltip = C("tooltip"); Star = C("star"); Dialog = C("dialog"); Toast = C("toast");
        Shade = C("shade"); Locked = C("locked"); Oz = C("oz"); Teal = C("teal");
    }

    static Color C(string key)
    {
        if (_d.Palette.TryGetValue(key, out var hex)) return new Color(hex);
        GD.PushError($"ui.json palette: '{key}' yok");
        return Colors.Magenta;
    }

    public static Color A(Color c, float alpha) => new(c.R, c.G, c.B, c.A * alpha);

    // ---------- Olcek ----------

    /// <summary>Gecerli olcekte tam piksel uzunluk (0 sifir kalir; pozitif en az 1).</summary>
    public static int Px(float basePixels) => basePixels <= 0 ? 0 : Math.Max(1, (int)MathF.Round(basePixels * Scale));

    /// <summary>Gecerli olcekte yuvarlanmamis uzunluk (cizgi ve yay icin).</summary>
    public static float Pxf(float basePixels) => basePixels * Scale;

    public static void SetScale(float scale)
    {
        scale = Math.Clamp(scale, 0.5f, 6f);
        if (MathF.Abs(scale - Scale) < 1e-4f) return;
        Scale = scale;
        if (_theme != null) ApplyTheme(_theme); // yerinde yeniden kurulur: ona bagli dugumler kendiliginden guncellenir
        ScaleChanged?.Invoke();
    }

    // ---------- Tema (Control olarak kalan paneller: Hasat Raporu, Defter) ----------

    static Theme _theme;

    /// <summary>Kodla kurulan Godot temasi: Barlow, olcekli yazi boyu, sicak gri duz panel (koseler yuvarlak degil).</summary>
    public static Theme Theme
    {
        get
        {
            if (_theme == null) { _theme = new Theme(); ApplyTheme(_theme); }
            return _theme;
        }
    }

    static void ApplyTheme(Theme t)
    {
        t.DefaultFont = Medium;
        t.DefaultFontSize = Px(_d.Type.Body);
        t.SetColor("font_color", "Label", Ink);
        t.SetStylebox("panel", "PanelContainer", new StyleBoxFlat
        {
            BgColor = Dialog,
            ContentMarginLeft = Px(16), ContentMarginRight = Px(16), ContentMarginTop = Px(12), ContentMarginBottom = Px(12),
        });
        t.SetStylebox("panel", "Panel", new StyleBoxFlat { BgColor = Panel });
    }

    // ---------- Yazi (Barlow, OFL: assets/fonts/Barlow/OFL.txt) ----------

    const string Folder = "res://assets/fonts/Barlow/";
    static FontFile _medium, _semiBold, _condensed, _condensedMedium;

    /// <summary>Govde: Barlow Medium.</summary>
    public static FontFile Medium => _medium ??= LoadFont("Barlow-Medium.ttf");
    /// <summary>Kucuk kalin yazi: Barlow SemiBold.</summary>
    public static FontFile SemiBold => _semiBold ??= LoadFont("Barlow-SemiBold.ttf");
    /// <summary>Baslik, sayi, dugme: Barlow Condensed Bold.</summary>
    public static FontFile Condensed => _condensed ??= LoadFont("BarlowCondensed-Bold.ttf");
    /// <summary>Dar ince yazi (yuva icindeki ad): Barlow Condensed Medium.</summary>
    public static FontFile CondensedMedium => _condensedMedium ??= LoadFont("BarlowCondensed-Medium.ttf");

    /// <summary>Rampa kurali: dar ya da buyuk kalin yazi Condensed Bold; kucuk kalin SemiBold; digeri Medium.</summary>
    public static FontFile Font(float size, bool bold = false, bool condensed = false) =>
        condensed ? (bold ? Condensed : CondensedMedium) : bold ? (size >= _d.Type.CondensedFrom ? Condensed : SemiBold) : Medium;

    static FontFile LoadFont(string file)
    {
        var font = GD.Load<FontFile>(Folder + file);
        // Her olcekte keskin: imza mesafe alani yok, hafif ipucu, alt piksel konumu kendiliginden, gri kenar yumusatma.
        font.MultichannelSignedDistanceField = false;
        font.Hinting = TextServer.Hinting.Light;
        font.SubpixelPositioning = TextServer.SubpixelPositioning.Auto;
        font.Antialiasing = TextServer.FontAntialiasing.Gray;
        font.GenerateMipmaps = false;
        font.AllowSystemFallback = true;
        return font;
    }

    // ---------- Metin ----------

    /// <summary>Turkce buyuk harf: i -> İ, ı -> I (ToUpperInvariant "Çivi"yi "ÇIVI" yapardi).</summary>
    public static string Upper(string s) => string.IsNullOrEmpty(s) ? s ?? "" : s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

    /// <summary>Yer tutucu ikon tonu: esya kimliginden, oyunun paletinden (yeni renk yok). Modelden ikon varlik hattinda.</summary>
    public static Color ItemTone(string id)
    {
        Color[] tones = { Palette.Ground, Palette.Plant, Palette.Accent, Palette.CoopBlue, Palette.Wood, Palette.MayaViolet, Palette.Rock, Palette.TurnipTop, Palette.SkyNoon };
        int h = 0;
        foreach (char ch in id ?? "") h = h * 31 + ch;
        return tones[Mathf.PosMod(h, tones.Length)];
    }
}
