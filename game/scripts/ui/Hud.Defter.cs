using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Defter'in ilk sayfasi (URETIM 6 M1; SANAT-SES 8.2 "Defter (is emirleri, ...)"; GDD 13, 24; PLAN 1.4):
/// gun ve saat, ogretici adimi, Ciraklik zinciri (ilk zincir) ve siradaki is emri. Envanter panelinin ikinci sayfasi.
/// </summary>
public partial class Hud
{
    void RebuildDefter()
    {
        foreach (var c in _defterPage.GetChildren()) c.QueueFree();
        var s = _d.State;
        var o = s.Orders;

        var head = new HBoxContainer();
        var title = L("İŞ EMİRLERİ", 24, Palette.OffWhite, true);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(title);
        var cal = s.Calendar;
        head.AddChild(L(cal.Running ? $"Gün {cal.Day} · {cal.Part.Name}" : "Gün 1 · İlk gün", 18, Palette.Fog, true));
        _defterPage.AddChild(head);
        _defterPage.AddChild(L(o.DefterOpen
            ? $"{o.ChainsTotal} zincir · {o.OrdersTotal} iş emri. Suna okur, Defter tutar."
            : "Defter ilk Kabarma'dan sonra açılır. Şimdilik Çıraklık.", 16, Palette.Fog));

        // Ogretici adimi (GDD 24): senaryo surerken
        var step = s.Scenario.Current;
        if (step != null)
        {
            _defterPage.AddChild(Section("ÖĞRETİCİ"));
            _defterPage.AddChild(OrderRow(false, true, step.Goal, step.Target > 1 ? $"{s.Scenario.Progress}/{step.Target}" : ""));
        }

        foreach (var chain in o.Chains)
        {
            if (!chain.Opened) continue;
            _defterPage.AddChild(Section($"{chain.Def.Name.ToUpperInvariant()} · {chain.Def.Group} · {chain.DoneCount}/{chain.Def.Steps.Count}"));
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
            _defterPage.AddChild(L($"Zincir ödülü: {chain.Def.Reward}", 14, Palette.MayaTeal, true));
        }

        if (!o.DefterOpen)
        {
            var next = o.Chains.Count > 1 ? o.Chains[1] : null;
            if (next?.Current != null) _defterPage.AddChild(L($"Sıradaki (Defter açılınca): {next.Def.Name} · {next.Current.Goal}", 15, Palette.Fog, true));
        }
        IgnoreMouse(_defterPage);
    }

    Control Section(string text)
    {
        var l = L(text, 15, Palette.Warning, true);
        return l;
    }

    Control OrderRow(bool done, bool current, string goal, string progress)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        var mark = L(done ? "✓" : current ? "▸" : "○", 20, done ? Palette.Plant : current ? Palette.Warning : new Color(Palette.Fog, 0.55f), true);
        mark.CustomMinimumSize = new Vector2(22, 0);
        h.AddChild(mark);
        var g = L(goal, 19, done ? new Color(Palette.Fog, 0.7f) : current ? Palette.OffWhite : new Color(Palette.Fog, 0.75f), current);
        g.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        h.AddChild(g);
        if (progress != "") h.AddChild(L(progress, 18, current ? Palette.Warning : Palette.Fog, true));
        return h;
    }
}
