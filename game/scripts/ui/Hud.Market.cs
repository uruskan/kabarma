using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Kargo Rampasi terminali (GDD 13; ekonomi2): Borsa, Katalog ve Tohum Carki panelleri. Rampa paneli (ortak sablon,
/// Hud.Menu.cs) sag sutunda satirlari tasir; bu dosya sol sutunu (Kusam'in yerini) Rust dilinde doldurur: Borsa'nin 12
/// kategorisi, sicak kategori, doygunluk ve 30 gunluk gecmis; Katalog'un cuzdani ve yoldaki teslimatlari; Carkin oranlari,
/// son odulu ve Kooperatif deposu. Satirlar menu yolundan (1-9, d-pad + A) secilir; panel yalniz cizer.
/// </summary>
public partial class Hud
{
    string _marketMode, _marketTitle;

    /// <summary>Borsa'da gecmisi buyuk cizilen kategori.</summary>
    public string MarketCategory { get; set; } = "kok";

    /// <summary>Acik terminal paneli: "borsa", "katalog", "cark" (rampa panelinde "borsa"); kapaliysa null.</summary>
    public string MarketMode => StationOpen && _marketMode != null && _menuShownTitle == _marketTitle ? _marketMode : null;

    /// <summary>Director bir terminal panelini acmadan hemen once cagirir: sol sutunda hangi panel, hangi baslikla.</summary>
    public void SetMarketPanel(string mode, string title) { _marketMode = mode; _marketTitle = title; }

    float MarketBottom => BagY0 + (((_d.State.Backpack.Slots + InventoryColumns - 1) / InventoryColumns) - 1) * T.Data.Inventory.Pitch + T.Data.Inventory.Slot;

    /// <summary>PaintInventory'den: terminal acikken sol sutunu cizer (true), degilse false (Kusam cizilir).</summary>
    bool PaintMarket()
    {
        var mode = MarketMode;
        if (mode == null) return false;
        _o = CanvasFrame;
        float top = 96, bottom = MarketBottom;
        Rect(LeftX - 10, top - 10, ColW + 20, bottom - top + 20, T.A(T.Shade, 0.55f));
        switch (mode)
        {
            case "katalog": PaintCatalogPanel(top, bottom); break;
            case "cark": PaintWheelPanel(top, bottom); break;
            default: PaintBorsaPanel(top, bottom); break;
        }
        return true;
    }

    static string F2(float v) => v.ToString("0.00", CultureInfo.GetCultureInfo("tr-TR"));

    // ---------- Borsa ----------

    void PaintBorsaPanel(float top, float bottom)
    {
        var m = _d.State.Market;
        float x = LeftX, w = ColW;
        Section("Borsa", x, top);
        string day = m.State.Open ? $"GÜN {m.State.Day}" : "AÇILIŞ: İLK ŞAFAKTAN SONRA";
        TextIn(day, x, top + 6, w, 14, T.Muted, HorizontalAlignment.Right, true, true);
        float y = top + 38;
        const float rowH = 30;
        Text("KATEGORİ", x + 8, y, 12, T.Muted, true, true);
        TextIn("ÇARPAN", x, y, w - 150, 12, T.Muted, HorizontalAlignment.Right, true, true);
        TextIn("DOYGUNLUK", x, y, w - 64, 12, T.Muted, HorizontalAlignment.Right, true, true);
        TextIn("30 GÜN", x, y, w - 6, 12, T.Muted, HorizontalAlignment.Right, true, true);
        y += 18;
        foreach (var c in m.Categories)
        {
            bool hot = m.IsHot(c.Id), sel = c.Id == MarketCategory;
            float b = m.Borsa(c.Id), s = m.Saturation(c.Id), f = b * s;
            Rect(x, y, w, rowH - 3, sel ? T.Selected : T.Panel);
            if (hot) Rect(x, y, 4, rowH - 3, T.Warning);
            Text(Fit(T.Upper(c.Name), w - 250, 15, true, true), x + 10, y + 5, 15, T.Ink, true, true);
            if (hot) Text("SICAK ×2", x + 12 + Measure(Fit(T.Upper(c.Name), w - 250, 15, true, true), 15, true, true) + 8, y + 7, 12, T.Warning, true, true);
            var tone = f >= 1.05f ? T.Green : f <= 0.95f ? T.Red : T.Ink;
            TextIn("×" + F2(f), x, y + 5, w - 150, 16, tone, HorizontalAlignment.Right, true, true);
            // doygunluk cubugu
            float bx = x + w - 140, bw = 70;
            Rect(bx, y + 10, bw, 7, T.A(T.Ink, 0.12f));
            Rect(bx, y + 10, bw * s, 7, s < 0.7f ? T.Red : T.A(T.Ink, 0.6f));
            Spark(m.State.History.TryGetValue(c.Id, out var h) ? h : null, x + w - 62, y + 3, 56, rowH - 9, T.A(T.Teal, 0.9f));
            y += rowH;
        }
        // Secili kategorinin buyuk gecmisi
        y += 10;
        var cat = m.Categories.FirstOrDefault(c => c.Id == MarketCategory) ?? m.Categories.First();
        float gh = Math.Min(150, bottom - y - 40);
        if (gh > 60)
        {
            Text(T.Upper(cat.Name) + "  ·  BORSA ÇARPANI, SON 30 GÜN", x, y, 14, T.Ink, true, true);
            y += 22;
            Rect(x, y, w, gh, T.Panel);
            var mf = _d.Data.Market;
            float lo = mf.Min, hi = mf.Max * mf.HotMult;
            float ly = y + gh - 6 - (1f - lo) / (hi - lo) * (gh - 12);
            Line(T.A(T.Ink, 0.25f), 1, new Vector2(x + 4, ly), new Vector2(x + w - 4, ly));
            TextIn("×1", x, ly - 16, w - 6, 11, T.Muted, HorizontalAlignment.Right, true, true);
            Spark(m.State.History.TryGetValue(cat.Id, out var hh) ? hh : null, x + 6, y + 6, w - 12, gh - 12, T.Teal, lo, hi, 2.5f);
            y += gh + 6;
        }
        Paragraph("Fiyat = taban × kalite × Borsa × doygunluk. Fiyat fırlatma anında kilitlenir. Aynı kategoriden her kasa %3 düşürür; her sabah geri gelir.",
            x, Math.Min(y, bottom - 36), w, 13, T.A(T.Ink, 0.7f), 2);
    }

    /// <summary>Kucuk cizgi grafik (tasarim pikseli). lo/hi verilmezse gecmisin kendi araligi.</summary>
    void Spark(List<float> h, float x, float y, float w, float hgt, Color c, float lo = float.NaN, float hi = float.NaN, float width = 1.5f)
    {
        if (h == null || h.Count == 0) { Text("—", x + w / 2f - 4, y, 14, T.Muted); return; }
        if (float.IsNaN(lo)) { lo = h.Min() - 0.05f; hi = h.Max() + 0.05f; }
        if (hi - lo < 1e-3f) hi = lo + 1e-3f;
        var pts = new Vector2[Math.Max(2, h.Count)];
        for (int i = 0; i < pts.Length; i++)
        {
            float v = h[Math.Min(i, h.Count - 1)];
            float px = x + (h.Count <= 1 ? (i == 0 ? 0 : w) : w * i / (h.Count - 1f));
            pts[i] = new Vector2(px, y + hgt - (Mathf.Clamp(v, lo, hi) - lo) / (hi - lo) * hgt);
        }
        Line(c, width, pts);
    }

    // ---------- Katalog ----------

    void PaintCatalogPanel(float top, float bottom)
    {
        var s = _d.State;
        float x = LeftX, w = ColW;
        Section("Katalog", x, top);
        float y = top + 40;
        Rect(x, y, w, 60, T.Panel);
        Text("CÜZDAN (EKİPÇE ORTAK)", x + 12, y + 8, 13, T.Muted, true, true);
        Text($"{s.Credits.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"))} KREDİ", x + 12, y + 26, 24, T.Warning, true, true);
        y += 72;
        Text("TESLİMATLAR", x, y, 15, T.Ink, true, true);
        y += 24;
        var dls = s.Catalog.Deliveries;
        if (dls.Count == 0) { Rect(x, y, w, 40, T.Panel); Text("Yolda teslimat yok.", x + 12, y + 10, 16, T.Muted); y += 46; }
        foreach (var dl in dls.Take(6))
        {
            Rect(x, y, w, 52, T.Panel);
            string icon = dl.Items.Keys.FirstOrDefault() ?? (dl.Load.Length > 0 ? dl.Load : null);
            if (icon != null) ItemIcon(icon, x + 4, y + 4, 44, 44, 1f, false);
            string name = dl.Source.StartsWith("cark:") ? "Çark ödülü" : s.Data.Catalog.Entries.FirstOrDefault(e => e.Id == dl.Source)?.Name ?? dl.Source;
            Text(Fit(T.Upper(name), w - 200, 16, true, true), x + 56, y + 6, 16, T.Ink, true, true);
            string what = string.Join("  ·  ", dl.Items.Select(kv => $"{ItemNames.Of(kv.Key)} ×{kv.Value}").Concat(dl.Load.Length > 0 ? new[] { ItemNames.Of(dl.Load) } : Array.Empty<string>()));
            Text(Fit(what, w - 70, 14), x + 56, y + 29, 14, T.A(T.Ink, 0.8f));
            float total = s.Catalog.Seconds(dl.Via);
            if (!dl.Arrived)
            {
                TextIn($"{(dl.Via == "rampa" ? "RAMPAYA" : "KAPSÜL")}  {Mathf.Ceil(dl.Left):0} SN", x, y + 7, w - 10, 14, T.Muted, HorizontalAlignment.Right, true, true);
                Rect(x, y + 48, w * (1f - dl.Left / Math.Max(1f, total)), 4, T.Green);
            }
            else TextIn(dl.Via == "rampa" ? "RAMPADA" : "İNDİ: E İLE AÇ", x, y + 7, w - 10, 14, T.Green, HorizontalAlignment.Right, true, true);
            y += 57;
        }
        y += 6;
        if (y < bottom - 60)
            Paragraph("Kapsül gökten Ocak'ın yanına iner; yanına git, E ile aç, içi çantaya geçer. Araç gövdesi Kargo Rampası'na iner. Katalog fiyatı Borsa'dan etkilenmez.",
                x, y, w, 13, T.A(T.Ink, 0.7f), 4);
    }

    // ---------- Tohum Carki ----------

    void PaintWheelPanel(float top, float bottom)
    {
        var s = _d.State;
        var wh = s.Wheel;
        float x = LeftX, w = ColW;
        Section("Tohum Çarkı", x, top);
        TextIn(s.WheelFreeToday ? "BUGÜN BEDAVA" : $"{wh.Cost} KREDİ", x, top + 6, w, 14, s.WheelFreeToday ? T.Green : T.Muted, HorizontalAlignment.Right, true, true);
        float y = top + 40;
        // Agirlikli serit: her odul payi kadar
        float bx = x, total = wh.TotalWeight;
        var tones = new[] { T.Warning, T.Green, T.Teal, T.Ivory, T.Brick, T.Health, T.Star, T.Red };
        int i = 0;
        foreach (var r in wh.Rewards)
        {
            float bw = Math.Max(2f, w * r.Weight / Math.Max(1e-3f, total));
            Rect(bx, y, bw - 1, 26, r.Id == wh.State.Last ? T.Ink : T.A(tones[i % tones.Length], 0.85f));
            bx += bw; i++;
        }
        y += 36;
        i = 0;
        foreach (var r in wh.Rewards)
        {
            bool last = r.Id == wh.State.Last;
            Rect(x, y, w, 30, last ? T.Selected : T.Panel);
            Rect(x, y, 6, 30, T.A(tones[i % tones.Length], 0.85f));
            Text(Fit(T.Upper(r.Name), w - 200, 14, true, true), x + 14, y + 7, 14, T.Ink, true, true);
            string val = r.Kind == "credits" ? string.Join(" / ", r.Amounts) + " KR" : $"≈{r.Value} KR";
            TextIn(val, x, y + 7, w - 70, 13, T.Muted, HorizontalAlignment.Right, true, true);
            TextIn($"%{(wh.Chance(r) * 100f).ToString("0.#", CultureInfo.GetCultureInfo("tr-TR"))}", x, y + 7, w - 10, 14, T.Ink, HorizontalAlignment.Right, true, true);
            y += 33; i++;
        }
        y += 6;
        string ev = $"BEKLENEN DEĞER  ≈{wh.ExpectedValue:0} KREDİ  (%{wh.ExpectedValue / Math.Max(1, wh.Cost) * 100f:0})";
        Text(ev, x, y, 13, T.A(T.Ink, 0.75f), true, true);
        y += 24;
        if (wh.State.Last.Length > 0)
        {
            var last = wh.Rewards.FirstOrDefault(r => r.Id == wh.State.Last);
            string got = last == null ? wh.State.Last : last.Kind == "credits" ? $"{wh.State.LastCredits} Kredi" : last.Name;
            Rect(x, y, w, 40, T.Panel);
            Text("SON ÖDÜL", x + 12, y + 12, 13, T.Muted, true, true);
            TextIn(T.Upper(got), x, y + 9, w - 12, 18, T.Warning, HorizontalAlignment.Right, true, true);
            y += 48;
        }
        var tokens = wh.State.Tokens.Where(kv => kv.Value > 0).ToList();
        if (tokens.Count > 0 && y < bottom - 40)
        {
            Text("KOOPERATİF DEPOSU (SİSTEMİ GELİNCE ALINIR)", x, y, 12, T.Muted, true, true);
            y += 18;
            Paragraph(string.Join("  ·  ", tokens.Select(kv => $"{wh.Rewards.FirstOrDefault(r => r.Id == kv.Key)?.Name ?? kv.Key} ×{kv.Value}")), x, y, w, 14, T.Ink, 3);
        }
    }
}
