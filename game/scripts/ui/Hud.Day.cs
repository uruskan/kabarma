using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Pano, gunduz: Defter (is emri), kemer, takili baslik, can ve canta satiri (SANAT-SES §8).</summary>
public partial class Hud
{
    Label _goal, _goalProgress;
    PanelContainer _goalPanel;
    HBoxContainer _belt;
    readonly List<PanelContainer> _beltSlots = new();
    readonly List<Label> _beltName = new(), _beltCount = new();
    PanelContainer _headTag;
    Label _headName, _headHint;
    Label _bag;
    ProgressBar _hp;
    VBoxContainer _bottomLeft;

    /// <summary>Defter, can cubugu ve canta satiri.</summary>
    void UpdateDayPanels()
    {
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
        _bag.Text = $"{s.Credits} Kredi   ·   Yumurta {s.Eggs}   ·   Çanta {s.Backpack.UsedSlots}/{s.Backpack.Slots} (Tab)";
    }

    public void OnHead(int h)
    {
        _headName.Text = "▸ " + Capa.HeadNames[h];
        _headTag.AddThemeStyleboxOverride("panel", Box(new Color(Capa.HeadColors[h].Darkened(0.45f), 0.85f), 12, Capa.HeadColors[h], 2));
    }

    void RefreshBelt()
    {
        var s = _d.State;
        for (int i = 0; i < _beltSlots.Count; i++)
        {
            var id = s.Belt.At(i);
            int n = id == null ? 0 : s.Backpack.Count(id);
            _beltName[i].Text = id == null ? "" : ItemNames.Of(id);
            _beltName[i].AddThemeFontSizeOverride("font_size", _beltName[i].Text.Length > 10 ? 12 : 15); // uzun ad kirpilmasin
            _beltCount[i].Text = id == null ? "" : n.ToString();
            _beltName[i].Modulate = n > 0 || id == null ? Colors.White : new Color(1, 1, 1, 0.4f);
        }
    }

    public void FlashBelt(int i)
    {
        if (i < 0 || i >= _beltSlots.Count) return;
        var p = _beltSlots[i];
        p.PivotOffset = p.Size / 2f;
        p.CreateTween().TweenProperty(p, "scale", Vector2.One, 0.2f).From(new Vector2(1.15f, 1.15f));
    }
}
