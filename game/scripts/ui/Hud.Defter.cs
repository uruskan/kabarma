using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Defter'in ilk sayfasi (URETIM 6 M1; SANAT-SES 8.2 "Defter (is emirleri, ...)"; GDD 13, 24; PLAN 1.4):
/// gun ve saat, ogretici adimi, Ciraklik zinciri (ilk zincir) ve siradaki is emri. Envanter ekraninin ikinci sekmesi;
/// Rust tarzi panel dilinde (M1 karari 20).
/// </summary>
public partial class Hud
{
    void RebuildDefter()
    {
        foreach (var c in _defterPage.GetChildren()) { _defterPage.RemoveChild(c); c.QueueFree(); }
        _defterPage.AddThemeConstantOverride("separation", T.Px(6));
        _defterPanel.AddThemeStyleboxOverride("panel", FlatBox(T.Dialog, 24, 18));
        _defterPanel.CustomMinimumSize = new Vector2(T.Px(760), 0);
        var s = _d.State;
        var o = s.Orders;

        var head = new HBoxContainer();
        var title = UL("İŞ EMİRLERİ", T.Data.Type.Section, T.Ink, true, true);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(title);
        var cal = s.Calendar;
        head.AddChild(UL(T.Upper(cal.Running ? $"Gün {cal.Day} · {cal.Part.Name}" : "Gün 1 · İlk gün"), 18, T.Muted, true, true));
        _defterPage.AddChild(head);
        _defterPage.AddChild(UL(o.DefterOpen
            ? $"{o.ChainsTotal} zincir · {o.OrdersTotal} iş emri. Suna okur, Defter tutar."
            : "Defter ilk Kabarma'dan sonra açılır. Şimdilik Çıraklık.", 16, T.Muted));

        // Ogretici adimi (GDD 24): senaryo surerken
        var step = s.Scenario.Current;
        if (step != null)
        {
            _defterPage.AddChild(DefterSection("ÖĞRETİCİ"));
            _defterPage.AddChild(OrderRow(false, true, GoalText(step.Goal), step.Target > 1 ? $"{s.Scenario.Progress}/{step.Target}" : ""));
        }

        foreach (var chain in o.Chains)
        {
            if (!chain.Opened) continue;
            _defterPage.AddChild(DefterSection($"{T.Upper(chain.Def.Name)} · {chain.Def.Group} · {chain.DoneCount}/{chain.Def.Steps.Count}"));
            for (int i = 0; i < chain.Def.Steps.Count; i++)
            {
                var st = chain.Def.Steps[i];
                bool done = chain.StepDone(i);
                bool current = !done && i == chain.CurrentStep;
                string prog = st.Target > 1 ? $"{chain.Progress(i)}/{st.Target}" : "";
                // Sayi zincirlerinde yalnizca siradaki adim ve bitenler; ilk-X zincirinde hepsi
                if (st.Target > 1 && !done && !current) continue;
                _defterPage.AddChild(OrderRow(done, current, st.Goal, prog));
            }
            _defterPage.AddChild(UL($"Zincir ödülü: {chain.Def.Reward}", 15, T.Teal, true));
        }

        if (!o.DefterOpen)
        {
            var next = o.Chains.Count > 1 ? o.Chains[1] : null;
            if (next?.Current != null) _defterPage.AddChild(UL($"Sıradaki (Defter açılınca): {next.Def.Name} · {next.Current.Goal}", 15, T.Muted, true));
        }
        DefterIslah(); // tarim (M2): Islah notlari, Hud.DefterIslah.cs
        IgnoreMouse(_defterPanel);
    }

    Control DefterSection(string text)
    {
        var l = UL(text, 17, T.Short, true, true);
        return l;
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
