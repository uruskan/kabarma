using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Hasat Raporu (GDD 14.1: safakta acilir; alanlar Sim HarvestReport). Ilk gecede PLAN 1.4 sirasi: kabarik urun
/// satildiktan sonra acilir ve Defter'in acildigini soyler; F1'den yeniden oynanan gecede safakta acilir.
/// Gorunus Rust tarzi panel dili (M1 karari 20): koyu duz panel, dar kalin basliklar, olcekli boy.
/// </summary>
public partial class Hud
{
    PanelContainer _report;
    HarvestReport _lastReport;
    bool _lastDefterOpened;

    public bool ReportOpen => _report != null && _report.Visible;

    public void ShowReport(HarvestReport r, bool defterJustOpened)
    {
        _lastReport = r;
        _lastDefterOpened = defterJustOpened;
        foreach (var c in _report.GetChildren()) { _report.RemoveChild(c); c.QueueFree(); }
        // Onceki raporun boyunda kalmasin (ilk gecenin Defter bolumu uzundur): icerige gore kucul.
        Callable.From(() => _report.Size = Vector2.Zero).CallDeferred();
        _report.AddThemeStyleboxOverride("panel", FlatBox(T.Dialog, 28, 22));
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(T.Px(640), 0) };
        v.AddThemeConstantOverride("separation", T.Px(5));
        v.AddChild(UL(T.Upper($"Hasat Raporu · Gece {r.NightNumber}"), 34, T.Ink, true, true));
        int min = (int)(r.Seconds / 60f), sec = (int)(r.Seconds % 60f);
        v.AddChild(UL($"Küçük Kabarma · KS {r.Ks} · {min}:{sec:00} · {r.WavesCleared}/{r.WavesTotal} dalga", 17, T.Muted));
        v.AddChild(Rule());
        void Row(string a, string b, Color? col = null)
        {
            var h = new PanelContainer();
            h.AddThemeStyleboxOverride("panel", FlatBox(T.Panel, 10, 3));
            var hb = new HBoxContainer();
            var l = UL(a, 18, T.Ink, true); l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hb.AddChild(l);
            hb.AddChild(UL(b, 20, col ?? T.Ink, true, true));
            h.AddChild(hb);
            v.AddChild(h);
        }
        Row("Söndürülen kabarık", $"{r.Kills}");
        Row("Toplanan Maya", $"{(int)r.Maya}", T.Oz);
        Row("Öz · kart seviyesi", $"{(int)r.Oz} · {r.Level}/{r.MaxLevel}");
        Row("En yüksek Gösteri", $"×{r.PeakGosteri:0.0}", T.Short);
        Row("Verilen hasar", $"{(int)r.DamageDealt}");
        string sizes = string.Join(" ", r.SwollenBySize.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
        Row("Kabaran ekin", r.SwollenTotal > 0 ? $"{r.SwollenTotal}  ({sizes})" : "0", T.Green);
        Row("Yenen ekin", $"{r.CropsEaten}", r.CropsEaten > 0 ? T.Red : T.Green);
        Row("Kabaran hayvan", $"{r.AnimalsSwollen}");
        Row("Yere düşme", $"{r.PlayerDowns}", r.PlayerDowns > 0 ? T.Red : T.Green);
        if (r.SoldCredits > 0) Row("Kargo satışı", $"+{r.SoldCredits} Kredi", T.Short);

        v.AddChild(Rule());
        v.AddChild(UL("KARTLAR · ŞAFAKTA SÖNDÜ", 16, T.Muted, true, true));
        var cards = new HFlowContainer();
        cards.AddThemeConstantOverride("h_separation", T.Px(6));
        cards.AddThemeConstantOverride("v_separation", T.Px(4));
        int max = _d.Data.Cards.MaxRank;
        foreach (var c in r.Cards)
        {
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", FlatBox(T.Selected, 8, 3));
            chip.AddChild(UL($"{T.Upper(c.Name)}  {c.Rank}/{max}", 15, T.Ink, true, true));
            cards.AddChild(chip);
        }
        if (r.Cards.Count == 0) cards.AddChild(UL("Kart seçilmedi.", 15, T.Muted));
        v.AddChild(cards);

        if (defterJustOpened)
        {
            v.AddChild(Rule());
            var o = _d.State.Orders;
            v.AddChild(UL($"Defter açıldı: {o.OrdersTotal} iş emri.", 21, T.Teal, true));
            var next = o.Tracked?.Current;
            if (next != null) v.AddChild(UL($"Sıradaki iş emri: {next.Goal}.", 18, T.Ink));
        }
        v.AddChild(UL(G("{act:ui_accept}: rapor kapanır. Defter: {act:bag}, sonra Q."), 15, T.Muted));
        _report.AddChild(v);
        IgnoreMouse(_report);
        _report.Visible = true;
        _report.Scale = new Vector2(0.92f, 0.92f);
        _report.PivotOffset = Vector2.Zero;
        _report.CreateTween().TweenProperty(_report, "scale", Vector2.One, 0.18f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Olcek degisince acik rapor yeni boyda yeniden kurulur.</summary>
    void ShowReport(HarvestReport r) => ShowReport(r, _lastDefterOpened);

    /// <summary>Ince ayirici cizgi (panel dili).</summary>
    static Control Rule()
    {
        var c = new ColorRect { Color = T.A(T.Ink, 0.18f), CustomMinimumSize = new Vector2(0, Mathf.Max(1, T.Px(1))), MouseFilter = Control.MouseFilterEnum.Ignore };
        return c;
    }

    public void HideReport() => _report.Visible = false;
}
