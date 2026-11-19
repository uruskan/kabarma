using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Defter'in Ustalik sekmesi ve Defter girdisi (GDD 10; ICERIK 19). Sekmeler sol/sag, satirlar yukari/asagi.
/// Ustalik: secili dalin 10/20/30/40/50. seviye kademeleri; secim bekleyen kademede 1 = A, 2 = B (kolda X / Y);
/// R (kolda Geri) dalin secimlerini Kredi karsiligi sifirlar (iki basis: ilki bedeli soyler).
/// </summary>
public partial class Hud
{
    bool _eKey1, _eKey2, _eKeyR, _eJoyX, _eJoyY, _eJoyBack;
    string _respecArmed = "";
    float _respecArmT;

    static bool JPd(params string[] actions) => actions.Any(a => InputMap.HasAction(a) && Input.IsActionJustPressed(a));

    static bool Edge(ref bool prev, bool now) { bool e = now && !prev; prev = now; return e; }

    /// <summary>Defter sayfasi girdisi (Hud.Inventory.UpdateInventoryFrame'den): sekme, satir, secim.</summary>
    void DefterInput()
    {
        var s = _d.State;
        bool k1 = Edge(ref _eKey1, Input.IsKeyPressed(Key.Key1)), k2 = Edge(ref _eKey2, Input.IsKeyPressed(Key.Key2)), kr = Edge(ref _eKeyR, Input.IsKeyPressed(Key.R));
        bool jx = Edge(ref _eJoyX, Input.IsJoyButtonPressed(0, JoyButton.X)), jy = Edge(ref _eJoyY, Input.IsJoyButtonPressed(0, JoyButton.Y)), jb = Edge(ref _eJoyBack, Input.IsJoyButtonPressed(0, JoyButton.Back));
        if (_respecArmT > 0) { _respecArmT -= (float)GetProcessDeltaTime(); if (_respecArmT <= 0) _respecArmed = ""; }

        int tab = _defterTab;
        if (JPd("ui_left", "move_left")) tab = (tab + DefterTabNames.Length - 1) % DefterTabNames.Length;
        else if (JPd("ui_right", "move_right")) tab = (tab + 1) % DefterTabNames.Length;
        if (tab != _defterTab) { _defterTab = tab; _defterSel = 0; Sfx.Play("place", null, -18f); RebuildDefter(); return; }

        int rows = _defterTab switch
        {
            0 => s.Orders.Chains.Count(c => c.Opened),
            1 => s.Mastery.Branches.Count,
            3 => ChipEntries().Count, // Hud.Chips.cs
            _ => s.Achievements.All.Count,
        };
        int move = JPd("ui_down", "move_back") ? 1 : JPd("ui_up", "move_forward") ? -1 : 0;
        if (move != 0 && rows > 0) { _defterSel = (_defterSel + move + rows) % rows; RebuildDefter(); }

        if (_defterTab == 0 && JPd("ui_accept"))
        {
            var chain = s.Orders.Chains.Where(c => c.Opened).ElementAtOrDefault(_defterSel);
            if (chain != null && !chain.Done && s.Orders.Pin(chain.Def.Id)) { Hint(Loc.T("mastery.track", chain.Def.Name), 1.5f); Sfx.Play("place", null, -12f); RebuildDefter(); }
        }
        else if (_defterTab == 3 && JPd("ui_accept")) ChipsAccept(); // Hud.Chips.cs
        else if (_defterTab == 1)
        {
            var b = s.Mastery.Branches.ElementAtOrDefault(_defterSel);
            if (b == null) return;
            if (k1 || jx) ChooseMastery(b, 'A');
            else if (k2 || jy) ChooseMastery(b, 'B');
            else if (kr || jb) RespecMastery(b);
        }
    }

    void ChooseMastery(MasteryBranchDef b, char slot)
    {
        var m = _d.State.Mastery;
        int tier = m.PendingTiers(b.Id).FirstOrDefault();
        if (tier == 0 || !m.Choose(b.Id, tier, slot)) { Sfx.Play("hit", null, -14f); return; }
        var perk = m.ChosenPerk(b.Id, tier)!;
        Hint($"{b.Name} {tier}: {perk.Name}", 2f);
        Sfx.Play("levelup", null, -10f);
        RebuildDefter();
    }

    void RespecMastery(MasteryBranchDef b)
    {
        var s = _d.State;
        int cost = s.Mastery.RespecCost(b.Id);
        if (s.Mastery.ChoiceCount(b.Id) == 0) { Hint(Loc.T("mastery.nothing_reset"), 1.5f); return; }
        if (_respecArmed != b.Id)
        {
            _respecArmed = b.Id; _respecArmT = 4f;
            Hint(Loc.T("mastery.reset_confirm", b.Name, cost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), 4f);
            return;
        }
        _respecArmed = "";
        int paid = s.RespecMastery(b.Id);
        Hint(paid < 0 ? Loc.T("mastery.cant_afford", cost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)) : Loc.T("mastery.reset_done", b.Name, paid.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), 2.5f);
        RebuildDefter();
    }

    // ---------- Sayfa ----------

    void BuildMasteryPage()
    {
        var s = _d.State;
        var m = s.Mastery;
        _defterSel = Mathf.Clamp(_defterSel, 0, m.Branches.Count - 1);
        _defterPage.AddChild(UL(Loc.T("mastery.intro"), 15, T.Muted));

        for (int k = 0; k < m.Branches.Count; k++)
        {
            var b = m.Branches[k];
            bool sel = k == _defterSel;
            _defterPage.AddChild(BranchRow(b, sel));
            if (sel) AddBranchDetail(b);
        }
        _defterPage.AddChild(UL(_padLast
            ? Loc.T("mastery.keys_pad")
            : Loc.T("mastery.keys_kb"), 14, T.Muted, true, true));
    }

    Control BranchRow(MasteryBranchDef b, bool selected)
    {
        var m = _d.State.Mastery;
        int lv = m.Level(b.Id);
        bool pending = m.PendingTiers(b.Id).Any();
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", FlatBox(selected ? T.Selected : T.Panel, 10, 3));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", T.Px(12));
        var name = UL(T.Upper(b.Name), 19, T.Ink, true, true);
        name.CustomMinimumSize = new Vector2(T.Px(140), 0);
        h.AddChild(name);
        h.AddChild(UL(Loc.T("mastery.lv", lv), 19, T.Ivory, true, true));
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = m.Fraction(b.Id), ShowPercentage = false, CustomMinimumSize = new Vector2(T.Px(260), T.Px(10)), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = T.A(T.Ink, 0.15f) });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = pending ? T.Ivory : T.Teal });
        h.AddChild(bar);
        string stat = lv >= m.Def.MaxLevel ? Loc.T("mastery.max") : Loc.T("mastery.xp", (long)m.Xp(b.Id), (long)m.Threshold(lv + 1));
        h.AddChild(UL(stat, 15, T.Muted, true, true));
        if (pending) h.AddChild(UL(Loc.T("mastery.choose"), 17, T.Ivory, true, true));
        row.AddChild(h);
        return row;
    }

    void AddBranchDetail(MasteryBranchDef b)
    {
        var m = _d.State.Mastery;
        int lv = m.Level(b.Id);
        _defterPage.AddChild(UL(Loc.T("mastery.level_share", lv, m.RespecCost(b.Id).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), 14, T.Muted));
        int pendingTier = m.PendingTiers(b.Id).FirstOrDefault();
        foreach (int tier in m.Def.PerkLevels)
        {
            var chosen = m.ChosenPerk(b.Id, tier);
            bool open = tier <= lv && chosen == null;
            foreach (var p in m.PerksOf(b.Id, tier))
            {
                bool mine = chosen?.Id == p.Id;
                string head = $"{tier} · {p.Slot}: {p.Name}";
                string body = p.Text + (p.Live ? "" : " " + Loc.T("mastery.not_live"));
                string key = open ? (_padLast ? (p.Slot == "A" ? "X" : "Y") : (p.Slot == "A" ? "1" : "2")) : mine ? "✓" : "";
                _defterPage.AddChild(OrderRow(mine, open && tier == pendingTier, $"{head} — {body}", key));
            }
        }
    }
}
