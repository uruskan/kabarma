using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Pano, dolaysiz kip cizim (Rust tarzi arayuz, M1 karari 20; basvuru Hollow-Universe GroundUi.Paint.cs): tasarim
/// tuvalinde dikdortgen, yazi, glif ve cizgi. Her uzunluk <see cref="UiTheme.Px"/>'ten gecer, konumlar tam piksele oturur.
/// Cerceveler pencere kenarlarina tutturulur: alt orta (kemer, envanter), sag alt (can), sol ust (Suna), ust orta (pusula),
/// orta (nisangah, cark).
/// </summary>
public partial class Hud
{
    UiPainter _p;
    Vector2 _o;

    // ---------- Koordinat ----------

    static int S(float v) => v < 0 ? -T.Px(-v) : T.Px(v);
    Vector2 At(float x, float y) => _o + new Vector2(S(x), S(y));
    Rect2 Area(float x, float y, float w, float h) => new(At(x, y), new Vector2(T.Px(w), T.Px(h)));
    /// <summary>Yuvarlanmamis tasarim noktasi (cizgi ve yay).</summary>
    Vector2 Pt(float x, float y) => _o + new Vector2(x, y) * T.Scale;
    Vector2 Pt(Vector2 p) => Pt(p.X, p.Y);
    Vector2 ScreenSize => GetViewport().GetVisibleRect().Size;

    /// <summary>Alt orta: tuvalin alt kenari pencerenin alt kenarinda, yatayda ortali (kemer, envanter).</summary>
    Vector2 CanvasFrame => new(Mathf.Floor((ScreenSize.X - T.Px(T.DesignW)) / 2f), ScreenSize.Y - T.Px(T.DesignH));
    /// <summary>Orta: tuvalin ortasi pencerenin ortasinda (nisangah, istem, cark).</summary>
    Vector2 CentreFrame => new(Mathf.Floor(ScreenSize.X / 2f) - T.Px(T.DesignW / 2f), Mathf.Floor(ScreenSize.Y / 2f) - T.Px(T.DesignH / 2f));
    /// <summary>Sag alt (can, cephane, toplanan esya).</summary>
    Vector2 RightFrame => new(ScreenSize.X - T.Px(T.DesignW), ScreenSize.Y - T.Px(T.DesignH));
    /// <summary>Ust orta (pusula, Defter adimi, Kabarma panosu, envanter sekmeleri).</summary>
    Vector2 TopFrame => new(CanvasFrame.X, 0);
    /// <summary>Sol ust (Suna).</summary>
    static Vector2 TopLeftFrame => Vector2.Zero;

    /// <summary>Isaretci bu cercevenin tasarim koordinatinda.</summary>
    Vector2 DesignPointer => (_pointer - _o) / T.Scale;
    bool Under(Rect2 r) => _pointerSeen && r.HasPoint(_pointer);

    // ---------- Ilkeller ----------

    void Rect(float x, float y, float w, float h, Color c) => _p.DrawRect(Area(x, y, w, h), c);

    /// <summary>Ince cerceve (tasarim pikseli kalinliginda, iceriden).</summary>
    void Frame(float x, float y, float w, float h, Color c, float width = 1)
    {
        var r = Area(x, y, w, h);
        int t = T.Px(width);
        _p.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, t)), c);
        _p.DrawRect(new Rect2(r.Position + new Vector2(0, r.Size.Y - t), new Vector2(r.Size.X, t)), c);
        _p.DrawRect(new Rect2(r.Position + new Vector2(0, t), new Vector2(t, r.Size.Y - 2 * t)), c);
        _p.DrawRect(new Rect2(r.Position + new Vector2(r.Size.X - t, t), new Vector2(t, r.Size.Y - 2 * t)), c);
    }

    static float Measure(string text, float size, bool bold = false, bool condensed = false) =>
        string.IsNullOrEmpty(text) ? 0 : T.Font(size, bold, condensed).GetStringSize(text, HorizontalAlignment.Left, -1, T.Px(size)).X / T.Scale;

    /// <summary>Ustu (x, y)'de duran yazi; tasarim pikseli genisligini dondurur. outline: dunyanin ustunde okunsun diye golge.</summary>
    float Text(string text, float x, float y, float size, Color c, bool bold = false, bool condensed = false, bool outline = false)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var font = T.Font(size, bold, condensed);
        int px = T.Px(size);
        var top = At(x, y);
        var baseLine = new Vector2(top.X, MathF.Round(top.Y + font.GetAscent(px)));
        if (outline) _p.DrawStringOutline(font, baseLine, text, HorizontalAlignment.Left, -1, px, T.Px(4), T.A(T.Shade, c.A));
        _p.DrawString(font, baseLine, text, HorizontalAlignment.Left, -1, px, c);
        return font.GetStringSize(text, HorizontalAlignment.Left, -1, px).X / T.Scale;
    }

    /// <summary>w genisliginde kutuya hizali yazi (sol, orta, sag).</summary>
    void TextIn(string text, float x, float y, float w, float size, Color c, HorizontalAlignment align, bool bold = false, bool condensed = false, bool outline = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = T.Font(size, bold, condensed);
        int px = T.Px(size);
        float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, px).X;
        var top = At(x, y);
        float left = align switch
        {
            HorizontalAlignment.Center => top.X + MathF.Floor((T.Px(w) - width) / 2f),
            HorizontalAlignment.Right => top.X + T.Px(w) - MathF.Ceiling(width),
            _ => top.X,
        };
        var baseLine = new Vector2(left, MathF.Round(top.Y + font.GetAscent(px)));
        if (outline) _p.DrawStringOutline(font, baseLine, text, HorizontalAlignment.Left, -1, px, T.Px(4), T.A(T.Shade, c.A));
        _p.DrawString(font, baseLine, text, HorizontalAlignment.Left, -1, px, c);
    }

    /// <summary>Tuvalin orta sutununa ortali yazi.</summary>
    void CentreText(string text, float y, float size, Color c, bool bold = true, bool condensed = true, bool outline = false) =>
        TextIn(text, 0, y, T.DesignW, size, c, HorizontalAlignment.Center, bold, condensed, outline);

    /// <summary>Sigmazsa sonu "…" ile kisaltir.</summary>
    static string Fit(string text, float width, float size, bool bold = false, bool condensed = false)
    {
        if (string.IsNullOrEmpty(text) || Measure(text, size, bold, condensed) <= width) return text ?? "";
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (int i = starts.Length - 1; i > 0; i--)
        {
            string s = text[..starts[i]].TrimEnd() + "…";
            if (Measure(s, size, bold, condensed) <= width) return s;
        }
        return "…";
    }

    /// <summary>Kelimeden saran satirlar (uzun kelime harften bolunur).</summary>
    static List<string> WrapLines(string text, float width, float size, bool bold = false, bool condensed = false)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in (text ?? "").Replace('\n', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string joined = line.Length == 0 ? word : line + " " + word;
            if (Measure(joined, size, bold, condensed) <= width) { line = joined; continue; }
            if (line.Length > 0) lines.Add(line);
            line = "";
            foreach (char ch in word)
            {
                if (line.Length > 0 && Measure(line + ch, size, bold, condensed) > width) { lines.Add(line); line = ""; }
                line += ch;
            }
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    /// <summary>Sarilmis paragraf; yuksekligini (tasarim pikseli) dondurur.</summary>
    float Paragraph(string text, float x, float y, float w, float size, Color c, int maxLines = 99, bool bold = false, bool outline = false, float lineGap = 4)
    {
        var lines = WrapLines(text, w, size, bold);
        int n = Math.Min(lines.Count, maxLines);
        for (int i = 0; i < n; i++)
            Text(i == maxLines - 1 && lines.Count > maxLines ? Fit(lines[i] + " …", w, size, bold) : lines[i], x, y + i * (size + lineGap), size, c, bold, false, outline);
        return n * (size + lineGap);
    }

    /// <summary>Tasarim noktalarindan cizgi; genislik olcek 1'de ekran pikseli.</summary>
    void Line(Color c, float width, params Vector2[] points)
    {
        var screen = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) screen[i] = Pt(points[i]);
        _p.DrawPolyline(screen, c, Math.Max(1f, T.Pxf(width)), true);
    }

    void Poly(Color c, params Vector2[] points)
    {
        var screen = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) screen[i] = Pt(points[i]);
        _p.DrawColoredPolygon(screen, c);
    }

    void Disc(float x, float y, float r, Color c, bool filled = true, float width = 1.5f)
    {
        if (filled) _p.DrawCircle(Pt(x, y), T.Pxf(r), c, true, -1, true);
        else _p.DrawArc(Pt(x, y), T.Pxf(r), 0, Mathf.Tau, 24, c, Math.Max(1f, T.Pxf(width)), true);
    }

    /// <summary>Bes koseli yildiz (kalite). Yazi tipine guvenmez: sekil olarak cizilir.</summary>
    void Star(float x, float y, float r, Color c)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -MathF.PI / 2 + i * MathF.PI / 5;
            float rr = i % 2 == 1 ? r * 0.45f : r;
            pts[i] = new Vector2(x + MathF.Cos(a) * rr, y + MathF.Sin(a) * rr);
        }
        // Ici: merkezden yelpaze (yildiz icbukey)
        for (int i = 0; i < 10; i++) Poly(c, new Vector2(x, y), pts[i], pts[(i + 1) % 10]);
    }

    /// <summary>Kilit (bos ve kilitli kusam yuvasi).</summary>
    void Lock(float cx, float cy, float s, Color c)
    {
        Rect(cx - s * 0.5f, cy - s * 0.05f, s, s * 0.7f, c);
        Line(c, 2f, new Vector2(cx - s * 0.32f, cy - s * 0.05f), new Vector2(cx - s * 0.32f, cy - s * 0.35f),
            new Vector2(cx - s * 0.18f, cy - s * 0.55f), new Vector2(cx + s * 0.18f, cy - s * 0.55f),
            new Vector2(cx + s * 0.32f, cy - s * 0.35f), new Vector2(cx + s * 0.32f, cy - s * 0.05f));
    }

    /// <summary>Tus glifi: klavyede kare tus, kolda yuvarlak dugme. Genisligini dondurur.</summary>
    float Keycap(string key, float x, float y, float h, Color c, bool pad = false)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        float size = h * 0.66f;
        float tw = Measure(key, size, true, true);
        float w = pad ? h : Math.Max(h, tw + h * 0.5f);
        if (pad) { Disc(x + h / 2f, y + h / 2f, h / 2f - 1, T.A(T.HudSlot, 1.6f)); Disc(x + h / 2f, y + h / 2f, h / 2f - 1, c, false, 1.5f); }
        else { Rect(x, y, w, h, T.A(T.HudSlot, 1.6f)); Frame(x, y, w, h, c, 1.5f); }
        TextIn(key, x, y + (h - size) / 2f - size * 0.12f, w, size, c, HorizontalAlignment.Center, true, true);
        return w;
    }

    /// <summary>Rust'in dugmesi: duz dolgu, isaretcinin altinda acilir; dar kalin etiket. Alanini dondurur (tiklama icin).</summary>
    Rect2 Button(string label, float x, float y, float w, float h, float size = 22, Color? fill = null, bool enabled = true, bool left = false, bool active = false)
    {
        var area = Area(x, y, w, h);
        Color f = active ? T.Selected : fill ?? T.Panel;
        bool hover = enabled && Under(area);
        _p.DrawRect(area, hover ? new Color(Math.Min(1, f.R + 0.06f), Math.Min(1, f.G + 0.06f), Math.Min(1, f.B + 0.06f), Math.Max(0.26f, f.A)) : f);
        if (!string.IsNullOrEmpty(label))
            TextIn(label, left ? x + 14 : x, y + (h - size) / 2f - size * 0.1f, left ? w - 28 : w, size, enabled ? T.Ink : T.Muted,
                left ? HorizontalAlignment.Left : HorizontalAlignment.Center, true, true);
        return area;
    }

    /// <summary>Bolum basligi (28 dar kalin): ÇANTA, KUŞAM, KEMER...</summary>
    void Section(string text, float x, float y) => Text(T.Upper(text), x, y, T.Data.Type.Section, T.Ink, true, true);

    /// <summary>Adet yazisi: x40, binler virgulle.</summary>
    static string Count(int n) => "x" + (n >= 1000 ? $"{n / 1000},{n % 1000:000}" : n.ToString(CultureInfo.InvariantCulture));

    // ---------- Yuva ve ikon ----------

    /// <summary>
    /// Esya ikonu (Hud.Icons.cs: assets/icons/items/&lt;kimlik&gt;.png), kutuya oranini bozmadan ortali. Ikon yoksa yer tutucu:
    /// esyanin tonunda kare ve icinde adi (dar yazi, en cok iki satir).
    /// </summary>
    void ItemIcon(string id, float x, float y, float w, float h, float alpha = 1f, bool label = true)
    {
        if (string.IsNullOrEmpty(id)) return;
        var tex = ItemIconTexture(id);
        if (tex != null)
        {
            float side = Math.Min(w, h) * 0.86f;
            _p.DrawTextureRect(tex, Area(x + (w - side) / 2f, y + (h - side) / 2f, side, side), false, new Color(1, 1, 1, alpha));
            return;
        }
        var tone = T.ItemTone(id);
        Rect(x, y, w, h, new Color(tone.Darkened(0.35f), 0.55f * alpha));
        if (!label) return;
        float size = Math.Clamp(h * 0.2f, 11, 16);
        var lines = WrapLines(T.Upper(ItemNames.Of(id)), w - 6, size, true, true);
        int n = Math.Min(2, lines.Count);
        float top = y + (h - n * (size + 1)) / 2f - size * 0.2f;
        for (int i = 0; i < n; i++)
            TextIn(i == 1 && lines.Count > 2 ? Fit(lines[1] + "…", w - 6, size, true, true) : lines[i], x + 3, top + i * (size + 1), w - 6, size, new Color(T.Ink, alpha), HorizontalAlignment.Center, true, true);
    }

    /// <summary>
    /// Rust'in yuvasi: duz kare (envanterde isaretcinin altinda acilir; secili ya da etkinse mavi, mavi-beyaz kenar), ikon,
    /// sag altta adet, solda dayaniklilik cubugu (varsa), sol ustte yuva numarasi (kemerde), sag ustte kalite yildizlari.
    /// </summary>
    void SlotBox(float x, float y, float size, string id, int count, bool inventory, bool selected, int number = 0,
        float? condition = null, int stars = 0, float alpha = 1f, bool dim = false)
    {
        var area = Area(x, y, size, size);
        Color fill = inventory ? T.Panel : T.HudSlot;
        if (inventory && Under(area) && _drag is not { Moving: true }) fill = T.PanelHover;
        if (selected) fill = T.Selected;
        _p.DrawRect(area, new Color(fill, fill.A * alpha));
        if (selected) Frame(x, y, size, size, new Color(T.SelectedEdge, alpha), 1.5f);
        if (!string.IsNullOrEmpty(id))
        {
            float inset = inventory ? 7 : 4;
            ItemIcon(id, x + inset, y + inset, size - 2 * inset, size - 2 * inset - (count > 1 ? 9 : 0), (dim ? 0.45f : 1f) * alpha);
            if (count > 1 || inventory && count == 1)
                TextIn(Count(count), x + 6, y + size - T.Data.Type.Count - 6, size - 12, T.Data.Type.Count, new Color(dim ? T.Muted : T.Ink, alpha), HorizontalAlignment.Right, true, false, !inventory);
            else if (count == 0)
                TextIn("0", x + 6, y + size - T.Data.Type.Count - 6, size - 12, T.Data.Type.Count, new Color(T.Red, alpha), HorizontalAlignment.Right, true);
            if (condition is float cnd)
            {
                float full = size - 6, filled = full * Math.Clamp(cnd, 0, 1);
                if (filled > 0) Rect(x + 2, y + 3 + full - filled, 3, filled, new Color(T.Green, alpha));
            }
            for (int s = 0; s < stars; s++) Star(x + size - 8 - s * 11, y + 8, 5, new Color(T.Star, alpha));
        }
        if (number > 0) Text(number.ToString(CultureInfo.InvariantCulture), x + 5, y + 2, T.Data.Type.Caption, new Color(selected ? T.Ivory : T.Muted, alpha), true, true);
    }
}
