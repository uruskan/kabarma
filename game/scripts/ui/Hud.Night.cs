using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Pano, Kabarma (SANAT-SES 8.1: gunduz panosuna ek olarak dalga sayaci, Oz cubugu, Gosteri carpani ve kart seridi).
/// Yerlesim: pusula ve adim satirinin altinda, ust ortada (GDD 14.5: Gosteri ekranin ust ortasinda); kart secimi
/// ekranin altinda, kemerin ustunde (GDD 15). Eski ust orta kutusu (_nightBox) kullanilmiyor.
/// </summary>
public partial class Hud
{
    VBoxContainer _nightBox;
    Label _wave, _level, _gosteri;
    ProgressBar _oz;
    HBoxContainer _cards;

    VBoxContainer _kabarma;
    Label _kWave, _kGosteri, _kGosteriTag, _kLevel;
    HBoxContainer _kWavePips, _kCardStrip;
    ProgressBar _kOz;
    ProgressBar _offerTimer;
    float _gosteriShown = 1f;
    int _stripCount = -1;

    static readonly string[] OfferKeys = { "Z", "X", "C" };

    static Color RarityColor(string r) => r switch
    {
        "N" => Palette.CoopBlue.Lerp(Palette.MayaTeal, 0.3f),
        "D" => Palette.MayaViolet,
        "E" => Palette.Warning,
        _ => Palette.OffWhite.Lerp(Palette.Shadow, 0.3f),
    };

    static string RarityName(string r) => r switch { "N" => "Nadir", "D" => "Destansı", "E" => "Efsane", _ => "Sıradan" };

    static string Pips(int rank, int max) => new string('●', rank) + new string('○', System.Math.Max(0, max - rank));

    void BuildNightHud(Control root)
    {
        _kabarma = new VBoxContainer { Visible = false, CustomMinimumSize = new Vector2(520, 0) };
        _kabarma.AddThemeConstantOverride("separation", 4);

        // 1. satir: dalga sayaci (sol) ve Gosteri carpani (sag)
        var row = new HBoxContainer();
        var wv = new VBoxContainer();
        wv.AddThemeConstantOverride("separation", 0);
        _kWave = L("", 24, Palette.MayaTeal, true);
        _kWave.AddThemeConstantOverride("outline_size", 10);
        _kWavePips = new HBoxContainer();
        _kWavePips.AddThemeConstantOverride("separation", 5);
        wv.AddChild(_kWave);
        wv.AddChild(_kWavePips);
        wv.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(wv);
        var gv = new VBoxContainer();
        gv.AddThemeConstantOverride("separation", -6);
        _kGosteriTag = L("GÖSTERİ", 13, Palette.Fog, true);
        _kGosteriTag.HorizontalAlignment = HorizontalAlignment.Right;
        _kGosteriTag.AddThemeConstantOverride("outline_size", 6);
        _kGosteri = L("×1.0", 34, Palette.Warning, true);
        _kGosteri.HorizontalAlignment = HorizontalAlignment.Right;
        _kGosteri.AddThemeConstantOverride("outline_size", 12);
        gv.AddChild(_kGosteriTag);
        gv.AddChild(_kGosteri);
        row.AddChild(gv);
        _kabarma.AddChild(row);

        // 2. satir: Oz cubugu ve seviye
        _kOz = new ProgressBar { CustomMinimumSize = new Vector2(520, 14), ShowPercentage = false, MaxValue = 100 };
        var ozFill = Box(Palette.MayaViolet, 7); ozFill.ContentMarginTop = ozFill.ContentMarginBottom = 0;
        _kOz.AddThemeStyleboxOverride("fill", ozFill);
        _kOz.AddThemeStyleboxOverride("background", Box(new Color(Palette.NightDeep, 0.75f), 7, new Color(Palette.MayaViolet, 0.5f), 1));
        _kabarma.AddChild(_kOz);
        _kLevel = L("", 15, Palette.OffWhite, true);
        _kLevel.AddThemeConstantOverride("outline_size", 7);
        _kabarma.AddChild(_kLevel);

        // 3. satir: kart seridi (bu gece alinan kartlar ve rutbeleri)
        _kCardStrip = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _kCardStrip.AddThemeConstantOverride("separation", 6);
        _kabarma.AddChild(_kCardStrip);
        root.AddChild(_kabarma);
    }

    /// <summary>Hud._Process'ten (eski adiyla): Kabarma panosunu gunceller; eski ust orta kutusu kapali kalir.</summary>
    void UpdateNightBox()
    {
        _nightBox.Visible = false;
    }

    void UpdateKabarmaHud(Vector2 vp)
    {
        var night = _d.Night;
        bool on = night != null && night.Phase != NightPhase.Dawn && !_report.Visible;
        _kabarma.Visible = on;
        if (!on) { _stripCount = -1; return; }
        _kabarma.Position = new Vector2((vp.X - _kabarma.Size.X) / 2f, _topCenter.Position.Y + _topCenter.Size.Y + 4);

        int waves = night.Def.Waves.Count;
        int cur = night.Phase == NightPhase.Alarm ? 0 : Mathf.Clamp(night.WaveIndex + 1, 0, waves);
        _kWave.Text = night.Phase == NightPhase.Alarm ? "KABARMA GELİYOR" : $"DALGA {cur}/{waves}";
        if (_kWavePips.GetChildCount() != waves)
        {
            foreach (var c in _kWavePips.GetChildren()) c.QueueFree();
            for (int i = 0; i < waves; i++) _kWavePips.AddChild(new ColorRect { CustomMinimumSize = new Vector2(34, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        int pipIndex = 0;
        foreach (var c in _kWavePips.GetChildren())
        {
            if (c is not ColorRect r) continue;
            bool done = pipIndex < cur - 1 || (pipIndex == cur - 1 && night.SpawnedThisWave >= (night.CurrentWave?.Count ?? 0) && Zipzip.All.Count(z => !z.Dead) == 0);
            bool active = pipIndex == cur - 1 && !done;
            r.Color = done ? Palette.MayaTeal : active ? new Color(Palette.MayaTeal, 0.55f + 0.35f * Mathf.Sin(Time.GetTicksMsec() * 0.008f)) : new Color(Palette.OffWhite, 0.25f);
            pipIndex++;
        }

        // Gosteri: artinca sayi bir an buyur
        float g = night.Gosteri;
        if (g > _gosteriShown + 0.01f) { _kGosteri.PivotOffset = _kGosteri.Size / 2f; _kGosteri.CreateTween().TweenProperty(_kGosteri, "scale", Vector2.One, 0.25f).From(new Vector2(1.35f, 1.35f)); }
        _gosteriShown = g;
        _kGosteri.Text = $"×{g:0.0}";
        _kGosteri.AddThemeColorOverride("font_color", Palette.Warning.Lerp(Palette.Accent, Mathf.Clamp((g - 1f) / 4f, 0, 1)));

        int lv = night.Level;
        int maxLv = night.Def.MaxCardLevels;
        float lo = lv == 0 ? 0 : night.Threshold(lv), hi = night.Threshold(lv + 1);
        bool capped = lv >= maxLv;
        _kOz.Value = capped ? 100 : Mathf.Clamp((night.Oz - lo) / (hi - lo) * 100f, 0, 100);
        // Yerdeki Oz (M1 karari 19): toplaninca ya da dalga bitince sayilir.
        string ground = night.Motes.Count > 0 ? $"   ·   yerde {(int)night.OzOnGround} Öz" : "";
        _kLevel.Text = capped ? $"ÖZ · Seviye {lv}/{maxLv} · dolu   ·   {night.Kills} kabarık söndü{ground}"
                              : $"ÖZ {(int)night.Oz}/{(int)hi} · Seviye {lv}/{maxLv}   ·   {night.Kills} kabarık söndü{ground}";

        if (night.CardsCount != _stripCount) RebuildCardStrip(night);
        if (_offerTimer != null && IsInstanceValid(_offerTimer) && night.Offer != null)
            _offerTimer.Value = Mathf.Clamp(1f - night.Offer.Age / _d.Data.Cards.AutoPickSeconds, 0, 1) * 100f;
    }

    /// <summary>Kart seridi: bu gece alinan her kart, rutbe noktalariyla.</summary>
    void RebuildCardStrip(KabarmaNight night)
    {
        _stripCount = night.CardsCount;
        foreach (var c in _kCardStrip.GetChildren()) c.QueueFree();
        int max = _d.Data.Cards.MaxRank;
        foreach (var card in _d.Data.Cards.Cards)
        {
            int r = night.Rank(card.Id);
            if (r <= 0) continue;
            var rc = RarityColor(card.Rarity);
            var chip = new PanelContainer();
            var sb = Box(new Color(Palette.NightDeep, 0.82f), 9, rc, 2);
            sb.ContentMarginLeft = sb.ContentMarginRight = 8; sb.ContentMarginTop = sb.ContentMarginBottom = 3;
            chip.AddThemeStyleboxOverride("panel", sb);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", -4);
            v.AddChild(L(card.Name, 14, Palette.OffWhite, true));
            v.AddChild(L(Pips(r, max), 11, Palette.MayaTeal, true));
            chip.AddChild(v);
            _kCardStrip.AddChild(chip);
        }
        IgnoreMouse(_kCardStrip);
    }

    /// <summary>Kart secimi: oyun durmaz, kartlar altta belirir, tek tusla secilir; 20 sn'de ilki alinir (GDD 15).</summary>
    public void ShowOffer(CardOffer offer, KabarmaNight night)
    {
        foreach (var c in _cards.GetChildren()) c.QueueFree();
        _offerTimer = null;
        _cards.Visible = offer != null;
        if (offer == null) return;
        int max = _d.Data.Cards.MaxRank;
        for (int i = 0; i < offer.Cards.Count; i++)
        {
            var card = offer.Cards[i];
            int rank = night.Rank(card.Id);
            var rc = RarityColor(card.Rarity);
            var p = new PanelContainer { CustomMinimumSize = new Vector2(250, 156) };
            p.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.93f), 16, rc, 4));
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 2);
            var top = new HBoxContainer();
            var key = L($"[{OfferKeys[i]}]", 18, Palette.Warning, true);
            var rar = L(RarityName(card.Rarity), 14, rc, true);
            rar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rar.HorizontalAlignment = HorizontalAlignment.Right;
            top.AddChild(key); top.AddChild(rar);
            v.AddChild(top);
            v.AddChild(L(card.Name, 30, Palette.OffWhite, true));
            var t = L(card.Text, 18, Palette.Fog);
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            t.CustomMinimumSize = new Vector2(218, 0);
            v.AddChild(t);
            v.AddChild(L(rank > 0 ? $"{Pips(rank, max)}  →  {rank + 1}. rütbe" : $"{Pips(0, max)}  yeni", 14, Palette.MayaTeal, true));
            if (i == 0)
            {
                // 20 sn icinde secilmezse ilki alinir: ilk kartin altinda geri sayim
                _offerTimer = new ProgressBar { CustomMinimumSize = new Vector2(218, 5), ShowPercentage = false, MaxValue = 100, Value = 100 };
                var fill = Box(Palette.Warning, 3); fill.ContentMarginTop = fill.ContentMarginBottom = 0;
                _offerTimer.AddThemeStyleboxOverride("fill", fill);
                _offerTimer.AddThemeStyleboxOverride("background", Box(new Color(Palette.OffWhite, 0.12f), 3));
                v.AddChild(_offerTimer);
            }
            p.AddChild(v);
            _cards.AddChild(p);
            p.Scale = new Vector2(0.6f, 0.6f);
            p.PivotOffset = new Vector2(125, 156);
            p.CreateTween().TweenProperty(p, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        IgnoreMouse(_cards);
    }
}
