using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Defter (Envanter ekraninin ikinci sayfasi; SANAT-SES 8.2; GDD 13, 24, 10; PLAN 1.4): gun ve saat, ogretici adimi ve
/// uc alt sekme: IS EMIRLERI (zincirler, secili zincirin bes adimi ve odulleri), USTALIK (Hud.Mastery.cs), BASARIMLAR.
/// Rust tarzi panel dili (M1 karari 20). Alt sekme: sol/sag; satir: yukari/asagi.
/// </summary>
public partial class Hud
{
    int _defterTab;   // 0 Is emirleri, 1 Ustalik, 2 Basarimlar
    int _defterSel;   // sekmedeki secili satir
    static readonly string[] DefterTabNames = { "İŞ EMİRLERİ", "USTALIK", "BAŞARIMLAR" };

    /// <summary>Acik Defter alt sekmesi: 0 Is emirleri, 1 Ustalik, 2 Basarimlar.</summary>
    public int DefterTab => _defterTab;

    /// <summary>Defter sayfasindaki yazilarin tamami (sinama ve goruntu denetimi).</summary>
    public string DefterText() =>
        string.Join("\n", _defterPage.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text));

    void RebuildDefter()
    {
        foreach (var c in _defterPage.GetChildren()) { _defterPage.RemoveChild(c); c.QueueFree(); }
        _defterPage.AddThemeConstantOverride("separation", T.Px(5));
        _defterPanel.AddThemeStyleboxOverride("panel", FlatBox(T.Dialog, 24, 16));
        _defterPanel.CustomMinimumSize = new Vector2(T.Px(800), 0);
        var s = _d.State;
        var o = s.Orders;

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", T.Px(14));
        for (int i = 0; i < DefterTabNames.Length; i++)
        {
            string label = DefterTabNames[i];
            if (i == 1 && s.Mastery.PendingCount > 0) label += $" ({s.Mastery.PendingCount})";
            head.AddChild(UL(label, i == _defterTab ? T.Data.Type.Section : 20, i == _defterTab ? T.Ink : T.Muted, true, true));
        }
        var gap = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        head.AddChild(gap);
        var cal = s.Calendar;
        head.AddChild(UL(T.Upper(cal.Running ? $"Gün {cal.Day} · {cal.Part.Name}" : "Gün 1 · İlk gün"), 18, T.Muted, true, true));
        _defterPage.AddChild(head);

        switch (_defterTab)
        {
            case 1: BuildMasteryPage(); break;
            case 2: BuildAchievementsPage(); break;
            default: BuildOrdersPage(); break;
        }
        IgnoreMouse(_defterPanel);
    }

    // ---------- Is emirleri ----------

    void BuildOrdersPage()
    {
        var s = _d.State;
        var o = s.Orders;
        var opened = o.Chains.Where(c => c.Opened).ToList();
        _defterSel = Mathf.Clamp(_defterSel, 0, Mathf.Max(0, opened.Count - 1));
        _defterPage.AddChild(UL(o.DefterOpen
            ? $"{opened.Count}/{o.ChainsTotal} zincir açık · {o.OrdersTotal} iş emri. Suna okur, Defter tutar. Kredi {s.Credits} · Veri {s.Veri}"
            : "Defter ilk Kabarma'dan sonra açılır. Şimdilik Çıraklık.", 16, T.Muted));

        // Ogretici adimi (GDD 24): senaryo surerken
        var step = s.Scenario.Current;
        if (step != null)
        {
            _defterPage.AddChild(DefterSection("ÖĞRETİCİ"));
            _defterPage.AddChild(OrderRow(false, true, GoalText(step.Goal), step.Target > 1 ? $"{s.Scenario.Progress}/{step.Target}" : ""));
        }

        // Ekrana sigsin diye secili zinciri ortalayan 9 zincirlik pencere (yukari/asagi kaydirir).
        const int Window = 9;
        int first = Mathf.Clamp(_defterSel - 3, 0, Mathf.Max(0, opened.Count - Window));
        if (first > 0) _defterPage.AddChild(UL($"↑ {first} zincir daha", 14, T.Muted, true, true));
        string group = "";
        for (int k = first; k < Mathf.Min(opened.Count, first + Window); k++)
        {
            var chain = opened[k];
            if (chain.Def.Group != group) { group = chain.Def.Group; _defterPage.AddChild(DefterSection(T.Upper(group))); }
            bool sel = k == _defterSel;
            _defterPage.AddChild(ChainRow(chain, sel, o.Tracked == chain));
            if (sel) AddChainDetail(chain);
        }
        if (first + Window < opened.Count) _defterPage.AddChild(UL($"↓ {opened.Count - first - Window} zincir daha", 14, T.Muted, true, true));
        if (!o.DefterOpen)
        {
            var next = o.Chains.Count > 1 ? o.Chains[1] : null;
            if (next?.Current != null) _defterPage.AddChild(UL($"Sıradaki (Defter açılınca): {next.Def.Name} · {next.Current.Goal}", 15, T.Muted, true));
        }
    }

    /// <summary>Zincir satiri: ad, ilerleme, siradaki adim; secili olan kutulu, takip edilen "TAKİP" etiketli.</summary>
    Control ChainRow(ChainState chain, bool selected, bool tracked)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", FlatBox(selected ? T.Selected : T.Panel, 10, 3));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", T.Px(10));
        var mark = new Panel { CustomMinimumSize = new Vector2(T.Px(10), T.Px(10)), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        mark.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = chain.Done ? T.Green : tracked ? T.Ivory : T.Muted });
        h.AddChild(mark);
        var name = UL(chain.Def.Name, 19, chain.Done ? T.Muted : T.Ink, true);
        h.AddChild(name);
        var cur = chain.Current;
        var goal = UL(cur == null ? "Tamamlandı" : $"{cur.Goal}  {chain.Progress(chain.CurrentStep)}/{cur.Target}", 16, T.Muted);
        goal.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        goal.HorizontalAlignment = HorizontalAlignment.Right;
        goal.ClipText = true;
        h.AddChild(goal);
        h.AddChild(UL((tracked ? "TAKİP · " : "") + $"{chain.DoneCount}/{chain.Def.Steps.Count}", 17, tracked ? T.Ivory : T.Muted, true, true));
        row.AddChild(h);
        return row;
    }

    void AddChainDetail(ChainState chain)
    {
        var rw = _d.Data.Orders.StepRewards;
        for (int i = 0; i < chain.Def.Steps.Count; i++)
        {
            var st = chain.Def.Steps[i];
            bool done = chain.StepDone(i);
            bool current = !done && i == chain.CurrentStep;
            string reward = $"{rw.Credits.ElementAtOrDefault(i):N0} Kr · {rw.Veri.ElementAtOrDefault(i)} Veri";
            string prog = done ? "✓" : current ? $"{chain.Progress(i)}/{st.Target}" : "";
            _defterPage.AddChild(OrderRow(done, current, $"{st.Goal}   ·   {reward}", prog));
        }
        _defterPage.AddChild(UL($"Zincir ödülü: {chain.Def.Reward}", 15, T.Teal, true));
        if (_d.State.Orders.Tracked != chain) _defterPage.AddChild(UL(_padLast ? "A: TAKİP ET" : "ENTER: TAKİP ET", 14, T.Muted, true, true));
    }

    Control DefterSection(string text)
    {
        var l = UL(text, 17, T.Short, true, true);
        return l;
    }

    // ---------- Basarimlar ----------

    void BuildAchievementsPage()
    {
        var book = _d.State.Achievements;
        _defterPage.AddChild(UL($"{book.Unlocked.Count}/{book.Total} başarım · zincirler 48'e tamamlanınca {book.Total}", 16, T.Muted));
        var list = book.All;
        _defterSel = Mathf.Clamp(_defterSel, 0, Mathf.Max(0, list.Count - 1));
        // Sayfa kaydirmasi yok: seciliyi ortalayan 12 satirlik pencere
        int first = Mathf.Clamp(_defterSel - 5, 0, Mathf.Max(0, list.Count - 12));
        for (int k = first; k < Mathf.Min(list.Count, first + 12); k++)
        {
            var a = list[k];
            bool got = book.IsUnlocked(a.Id);
            _defterPage.AddChild(OrderRow(got, k == _defterSel && !got, $"{a.Name} · {a.Text}", got ? "✓" : ""));
        }
    }

    /// <summary>Is emri satiri: solda durum karesi (bitti yesil, sirada mavi, bekliyor bos), hedef, ilerleme.</summary>
    Control OrderRow(bool done, bool current, string goal, string progress)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", FlatBox(current ? T.Selected : T.Panel, 10, 4));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", T.Px(10));
        var mark = new Panel { CustomMinimumSize = new Vector2(T.Px(12), T.Px(12)), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var mb = new StyleBoxFlat { BgColor = done ? T.Green : current ? T.Ivory : new Color(0, 0, 0, 0) };
        if (!done && !current) { mb.BorderColor = T.Muted; mb.BorderWidthBottom = mb.BorderWidthTop = mb.BorderWidthLeft = mb.BorderWidthRight = Mathf.Max(1, T.Px(1.5f)); }
        mark.AddThemeStyleboxOverride("panel", mb);
        h.AddChild(mark);
        var g = UL(goal, 18, done ? T.Muted : T.Ink, current);
        g.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        g.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        h.AddChild(g);
        if (progress != "") h.AddChild(UL(progress, 18, current ? T.Ivory : T.Muted, true, true));
        row.AddChild(h);
        return row;
    }
}
