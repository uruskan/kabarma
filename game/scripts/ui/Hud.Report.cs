using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Hasat Raporu (GDD 14.1: safakta acilir; alanlar Sim HarvestReport). Ilk gecede PLAN 1.4 sirasi: kabarik urun
/// satildiktan sonra acilir ve Defter'in acildigini soyler; F1'den yeniden oynanan gecede safakta acilir.
/// </summary>
public partial class Hud
{
    PanelContainer _report;

    public bool ReportOpen => _report != null && _report.Visible;

    public void ShowReport(HarvestReport r, bool defterJustOpened)
    {
        foreach (var c in _report.GetChildren()) c.QueueFree();
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(660, 0) };
        v.AddThemeConstantOverride("separation", 5);
        v.AddChild(L($"HASAT RAPORU · Gece {r.NightNumber}", 34, Palette.CoopBlue, true));
        int min = (int)(r.Seconds / 60f), sec = (int)(r.Seconds % 60f);
        v.AddChild(L($"Küçük Kabarma · KS {r.Ks} · {min}:{sec:00} · {r.WavesCleared}/{r.WavesTotal} dalga", 19, Palette.DarkGrey));
        v.AddChild(new HSeparator());
        void Row(string a, string b, Color? col = null)
        {
            var h = new HBoxContainer();
            var l = L(a, 21, Palette.DarkGrey); l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            h.AddChild(l);
            h.AddChild(L(b, 21, col ?? Palette.Accent, true));
            v.AddChild(h);
        }
        Row("Söndürülen kabarık", $"{r.Kills}");
        Row("Toplanan Maya", $"{(int)r.Maya}", Palette.MayaViolet.Darkened(0.2f));
        Row("Öz · kart seviyesi", $"{(int)r.Oz} · {r.Level}/{r.MaxLevel}");
        Row("En yüksek Gösteri", $"×{r.PeakGosteri:0.0}");
        Row("Verilen hasar", $"{(int)r.DamageDealt}");
        string sizes = string.Join(" ", r.SwollenBySize.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
        Row("Kabaran ekin", r.SwollenTotal > 0 ? $"{r.SwollenTotal}  ({sizes})" : "0", Palette.Plant.Darkened(0.2f));
        Row("Yenen ekin", $"{r.CropsEaten}", r.CropsEaten > 0 ? Palette.HoldingRed : Palette.Plant.Darkened(0.2f));
        Row("Kabaran hayvan", $"{r.AnimalsSwollen}");
        Row("Yere düşme", $"{r.PlayerDowns}", r.PlayerDowns > 0 ? Palette.HoldingRed : Palette.Plant.Darkened(0.2f));
        if (r.SoldCredits > 0) Row("Kargo satışı", $"+{r.SoldCredits} Kredi", Palette.Warning.Darkened(0.25f));

        v.AddChild(new HSeparator());
        v.AddChild(L("Kartlar (şafakta söndü)", 17, Palette.DarkGrey, true));
        var cards = new HFlowContainer();
        cards.AddThemeConstantOverride("h_separation", 6);
        cards.AddThemeConstantOverride("v_separation", 4);
        int max = _d.Data.Cards.MaxRank;
        foreach (var c in r.Cards)
        {
            var chip = new PanelContainer();
            var sb = Box(new Color(Palette.NightDeep, 0.88f), 9); sb.ContentMarginLeft = sb.ContentMarginRight = 8; sb.ContentMarginTop = sb.ContentMarginBottom = 3;
            chip.AddThemeStyleboxOverride("panel", sb);
            chip.AddChild(L($"{c.Name}  {Pips(c.Rank, max)}", 15, Palette.OffWhite, true));
            cards.AddChild(chip);
        }
        if (r.Cards.Count == 0) cards.AddChild(L("Kart seçilmedi.", 16, Palette.Shadow));
        v.AddChild(cards);

        if (defterJustOpened)
        {
            v.AddChild(new HSeparator());
            var o = _d.State.Orders;
            v.AddChild(L($"Defter açıldı: {o.OrdersTotal} iş emri.", 22, Palette.CoopBlue, true));
            var next = o.Tracked?.Current;
            if (next != null) v.AddChild(L($"Sıradaki iş emri: {next.Goal}.", 21, Palette.DarkGrey, true));
        }
        v.AddChild(L("Enter / A: rapor kapanır. Defter: Tab, sonra Q.", 16, Palette.Shadow));
        _report.AddChild(v);
        IgnoreMouse(_report);
        _report.Visible = true;
        _report.Scale = new Vector2(0.8f, 0.8f);
        _report.PivotOffset = Vector2.Zero;
        _report.CreateTween().TweenProperty(_report, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void HideReport() => _report.Visible = false;
}
