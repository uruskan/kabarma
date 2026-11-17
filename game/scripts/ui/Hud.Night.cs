using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Pano, Kabarma (SANAT-SES 8.1: gunduz panosuna ek olarak dalga sayaci, Oz cubugu, Gosteri carpani ve kart seridi; M1
/// karari 20: ayni Rust panel dilinde). Yerlesim: pusula ve adim satirinin altinda, ust ortada (GDD 14.5: Gosteri ekranin
/// ust ortasinda); kart secimi ekranin altinda, kemerin ustunde (GDD 15).
/// </summary>
public partial class Hud
{
    // Hud._Ready'nin kurdugu eski dugumler (gizli tutucuda).
    VBoxContainer _nightBox;
    Label _wave, _level, _gosteri;
    ProgressBar _oz;
    HBoxContainer _cards;

    CardOffer _offer;
    KabarmaNight _offerNight;
    double _offerAt, _gosteriAt = -10;
    float _gosteriShown = 1f;

    static readonly string[] OfferKeys = { "Z", "X", "C" };

    static Color RarityColor(string r) => r switch
    {
        "N" => T.Stamina,
        "D" => T.Oz,
        "E" => T.Short,
        _ => T.Muted,
    };

    static string RarityName(string r) => r switch { "N" => "Nadir", "D" => "Destansı", "E" => "Efsane", _ => "Sıradan" };

    /// <summary>Hud._Process'ten (eski adiyla): eski ust orta kutusu kapali kalir.</summary>
    void UpdateNightBox() => _nightBox.Visible = false;

    bool KabarmaOn => _d.Night != null && _d.Night.Phase != NightPhase.Dawn && !_report.Visible;

    /// <summary>Rutbe noktalari: dolu ve bos daireler (yazi tipine guvenmez).</summary>
    float Pips(float x, float y, int rank, int max, Color on, float r = 3.5f)
    {
        for (int i = 0; i < max; i++)
            Disc(x + r + i * (r * 2 + 3), y + r, r, i < rank ? on : T.A(T.Ink, 0.25f), i < rank, 1.2f);
        return max * (r * 2 + 3);
    }

    void PaintKabarma()
    {
        if (!KabarmaOn) return;
        var night = _d.Night;
        _o = TopFrame;
        var c = T.Data.Compass;
        float w = c.KabarmaWidth, x = (T.DesignW - w) / 2f, y = c.KabarmaY + (string.IsNullOrEmpty(_defterText) ? 0 : 20);

        // Kart seridi satiri (alinan kartlar) yuksekligi: sigdigi kadar satir
        var taken = _d.Data.Cards.Cards.Where(cd => night.Rank(cd.Id) > 0).ToList();
        int max = _d.Data.Cards.MaxRank;
        var chipW = taken.Select(cd => Measure(T.Upper(cd.Name), 14, true, true) + 22 + max * 10f).ToList();
        int rows = 0; float rowW = 0;
        for (int i = 0; i < chipW.Count; i++) { if (i == 0 || rowW + chipW[i] + 6 > w - 16) { rows++; rowW = 0; } rowW += chipW[i] + 6; }
        float h = 84 + rows * 30;
        Rect(x, y, w, h, T.HudSlot);

        // 1. satir: dalga sayaci (sol) ve Gosteri (sag)
        int waves = night.Def.Waves.Count;
        int cur = night.Phase == NightPhase.Alarm ? 0 : Mathf.Clamp(night.WaveIndex + 1, 0, waves);
        Text(night.Phase == NightPhase.Alarm ? "KABARMA GELİYOR" : $"DALGA {cur}/{waves}", x + 12, y + 6, T.Data.Type.Title, T.Ink, true, true);
        for (int i = 0; i < waves; i++)
        {
            bool done = i < cur - 1 || (i == cur - 1 && night.SpawnedThisWave >= (night.CurrentWave?.Count ?? 0) && Zipzip.All.Count(z => !z.Dead) == 0);
            bool active = i == cur - 1 && !done;
            var col = done ? T.Teal : active ? T.A(T.Teal, 0.55f + 0.35f * Mathf.Sin((float)_now * 8f)) : T.A(T.Ink, 0.22f);
            Rect(x + 12 + i * 30, y + 38, 26, 5, col);
        }
        float g = night.Gosteri;
        if (g > _gosteriShown + 0.01f) _gosteriAt = _now;
        _gosteriShown = g;
        float bump = (float)Math.Max(0, 1 - (_now - _gosteriAt) / 0.25);
        float gs = 32 * (1 + 0.25f * bump);
        var gc = T.Short.Lerp(T.Brick, Mathf.Clamp((g - 1f) / 4f, 0, 1));
        TextIn($"×{g:0.0}", x, y + 2 - (gs - 32) / 2f, w - 12, gs, gc, HorizontalAlignment.Right, true, true);
        TextIn("GÖSTERİ", x, y + 36, w - 12 - Measure($"×{g:0.0}", 32, true, true) - 8, T.Data.Type.Caption, T.Muted, HorizontalAlignment.Right, true, true);

        // 2. satir: Oz cubugu (seviye esigine dek), solda Oz ve seviye, sagda sondurulen ve yerdeki Oz
        int lv = night.Level, maxLv = night.Def.MaxCardLevels;
        float lo = lv == 0 ? 0 : night.Threshold(lv), hi = night.Threshold(lv + 1);
        bool capped = lv >= maxLv;
        float frac = capped ? 1 : Mathf.Clamp((night.Oz - lo) / Math.Max(1e-3f, hi - lo), 0, 1);
        Rect(x + 12, y + 52, w - 24, 8, T.Panel);
        if (frac > 0) Rect(x + 12, y + 52, (w - 24) * frac, 8, T.Oz);
        Text(capped ? $"ÖZ · SEVİYE {lv}/{maxLv} · DOLU" : $"ÖZ {(int)night.Oz}/{(int)hi} · SEVİYE {lv}/{maxLv}", x + 12, y + 63, T.Data.Type.Caption, T.Ink, true, true);
        // Yerdeki Oz (M1 karari 19): toplaninca ya da dalga bitince sayilir.
        string right = $"{night.Kills} SÖNDÜ" + (night.Motes.Count > 0 ? $"  ·  YERDE {(int)night.OzOnGround} ÖZ" : "");
        TextIn(right, x, y + 63, w - 12, T.Data.Type.Caption, T.A(T.Ink, 0.8f), HorizontalAlignment.Right, true, true);

        // 3. satir: kart seridi (bu gece alinan kartlar ve rutbeleri)
        float cx = x + 8, cy = y + 84;
        for (int i = 0; i < taken.Count; i++)
        {
            if (i > 0 && cx + chipW[i] > x + w - 8) { cx = x + 8; cy += 30; }
            var cd = taken[i];
            Rect(cx, cy, chipW[i], 24, T.Panel);
            Rect(cx, cy, 3, 24, RarityColor(cd.Rarity));
            float tw = Text(T.Upper(cd.Name), cx + 9, cy + 4, 14, T.Ink, true, true);
            Pips(cx + 9 + tw + 6, cy + 8.5f, night.Rank(cd.Id), max, T.Teal, 3.5f);
            cx += chipW[i] + 6;
        }
    }

    /// <summary>Kart secimi: oyun durmaz, kartlar altta belirir, tek tusla secilir; 20 sn'de ilki alinir (GDD 15).</summary>
    public void ShowOffer(CardOffer offer, KabarmaNight night)
    {
        foreach (var c in _cards.GetChildren()) c.QueueFree();
        _offer = offer;
        _offerNight = night;
        _offerAt = Time.GetTicksMsec() / 1000.0;
        _cards.Visible = offer != null;
    }

    void PaintOffer()
    {
        var offer = _offer;
        var night = _offerNight;
        if (offer == null || night == null || _d.Night != night || night.Offer != offer) return;
        _o = CanvasFrame;
        int n = offer.Cards.Count;
        float cw = 250, ch = 164, gap = 18;
        float x0 = (T.DesignW - (n * cw + (n - 1) * gap)) / 2f, y = T.Data.Belt.Y - 30 - ch;
        float appear = Mathf.Clamp((float)(_now - _offerAt) / 0.2f, 0, 1);
        int max = _d.Data.Cards.MaxRank;
        for (int i = 0; i < n; i++)
        {
            var card = offer.Cards[i];
            int rank = night.Rank(card.Id);
            var rc = RarityColor(card.Rarity);
            float x = x0 + i * (cw + gap), yy = y + (1 - appear) * 24;
            Rect(x, yy, cw, ch, T.A(T.Dialog, appear));
            Rect(x, yy, cw, 4, T.A(rc, appear));
            Keycap(i < OfferKeys.Length ? OfferKeys[i] : "", x + 12, yy + 14, 24, T.A(T.Ink, appear));
            TextIn(T.Upper(RarityName(card.Rarity)), x, yy + 18, cw - 12, T.Data.Type.Caption, T.A(rc, appear), HorizontalAlignment.Right, true, true);
            Text(Fit(T.Upper(card.Name), cw - 24, T.Data.Type.Title, true, true), x + 12, yy + 44, T.Data.Type.Title, T.A(T.Ink, appear), true, true);
            var lines = WrapLines(card.Text, cw - 24, 16);
            for (int l = 0; l < Math.Min(3, lines.Count); l++)
                Text(l == 2 && lines.Count > 3 ? Fit(lines[l] + " …", cw - 24, 16) : lines[l], x + 12, yy + 78 + l * 19, 16, T.A(T.Ink, 0.82f * appear));
            float pw = Pips(x + 12, yy + ch - 22, rank, max, T.Teal);
            Text(rank > 0 ? $"SIRADAKİ: {rank + 1}. RÜTBE" : "YENİ KART", x + 18 + pw, yy + ch - 25, T.Data.Type.Caption, T.A(T.Teal, appear), true, true);
            if (i == 0)
            {
                // 20 sn icinde secilmezse ilki alinir: ilk kartin altinda geri sayim
                float left = Mathf.Clamp(1f - offer.Age / _d.Data.Cards.AutoPickSeconds, 0, 1);
                Rect(x, yy + ch - 4, cw, 4, T.A(T.Panel, appear));
                Rect(x, yy + ch - 4, cw * left, 4, T.A(T.Short, appear));
            }
        }
    }
}
