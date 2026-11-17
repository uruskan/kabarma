using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Pano, gece: dalga, Oz, Gosteri, seviye ve kart seridi (SANAT-SES §8, GDD 15).</summary>
public partial class Hud
{
    VBoxContainer _nightBox;
    Label _wave, _level, _gosteri;
    ProgressBar _oz;
    HBoxContainer _cards;

    /// <summary>Gece paneli (ust orta): yalnizca Kabarma surerken gorunur.</summary>
    void UpdateNightBox()
    {
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
    }

    /// <summary>Kart secimi: oyun durmaz, kartlar altta belirir, tek tusla secilir (GDD 15).</summary>
    public void ShowOffer(CardOffer offer, KabarmaNight night)
    {
        foreach (var c in _cards.GetChildren()) c.QueueFree();
        _cards.Visible = offer != null;
        if (offer == null) return;
        string[] keys = { G("{act:card_1}"), G("{act:card_2}"), G("{act:card_3}") };
        for (int i = 0; i < offer.Cards.Count; i++)
        {
            var card = offer.Cards[i];
            int rank = night.Rank(card.Id);
            var rc = card.Rarity switch { "N" => Palette.CoopBlue.Lerp(Palette.MayaTeal, 0.3f), "D" => Palette.MayaViolet, "E" => Palette.Warning, _ => Palette.OffWhite.Lerp(Palette.Shadow, 0.3f) };
            var p = new PanelContainer { CustomMinimumSize = new Vector2(250, 150) };
            p.AddThemeStyleboxOverride("panel", Box(new Color(Palette.NightDeep, 0.93f), 16, rc, 4));
            var v = new VBoxContainer();
            v.AddChild(L($"{keys[i]}  " +(card.Rarity switch { "N" => "Nadir", "D" => "Destansı", "E" => "Efsane", _ => "Sıradan" }), 16, rc, true));
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
}
