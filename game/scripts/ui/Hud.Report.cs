using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>Pano: Hasat Raporu (PLAN 1.4, 10:00).</summary>
public partial class Hud
{
    PanelContainer _report;

    public bool ReportOpen => _report != null && _report.Visible;

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
