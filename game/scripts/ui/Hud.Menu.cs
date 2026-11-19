using System;
using System.Collections.Generic;
using Godot;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Pano: ortak panel sablonu (istasyon, satis; SANAT-SES 8.2 "istasyon panelleri (ortak sablon)"). M1 karari 20 ile envanter
/// ekraninin sag sutununa tasindi (Rust'in "loot" paneli gibi): ortada canta ve kemer, sagda istasyonun satirlari. Satirlar
/// tiklanir, 1-9 ile ya da d-pad + A ile secilir. Eski Control dugumleri (_menu, _menuList) gizli tutucuda yasar: acik olup
/// olmadigi ve satirlari onlarda (bot ve girdi akisi bunlara bakar).
/// </summary>
public partial class Hud
{
    // ---------- Menu: istasyon, canta, satis (ortak sablon, SANAT-SES §8.2) ----------
    public sealed record MenuItem(string Label, System.Func<bool> Enabled, System.Action Act, string Icon = null);
    PanelContainer _menu;
    VBoxContainer _menuList;
    Label _menuTitle, _menuFoot;
    readonly List<MenuItem> _menuItems = new();
    int _menuRow;
    string _menuShownTitle;
    /// <summary>Herhangi bir panel acik (istasyon menusu ya da envanter): tetik ve E dunyaya gitmez.</summary>
    public bool MenuOpen => (_menu != null && _menu.Visible) || InventoryOpen;
    /// <summary>Istasyon ya da rampa paneli acik (envanter ekraninin sag sutununda).</summary>
    public bool StationOpen => _menu != null && _menu.Visible;
    /// <summary>Acik panelin basligi ve satirlari (sinama icin).</summary>
    public string MenuTitleText => _menuTitle?.Text ?? "";
    public int MenuRows => _menuItems.Count;

    public void ShowMenu(string title, List<MenuItem> items, string footer)
    {
        _menuItems.Clear();
        _menuItems.AddRange(items);
        _menuTitle.Text = title;
        _menuFootRaw = footer; _menuFoot.Text = G(footer); // Hud.Controls.cs: aygit degisince yenilenir
        foreach (var c in _menuList.GetChildren()) c.QueueFree();
        for (int i = 0; i < items.Count; i++)
        {
            var l = L($"{i + 1}   {items[i].Label}", 21, Palette.OffWhite, true);
            l.SetMeta("i", i);
            _menuList.AddChild(l);
        }
        if (items.Count == 0) _menuList.AddChild(L(Loc.T("hud.menu_empty"), 20, Palette.Fog));
        _menu.Visible = true;
        RefreshMenu();
        // Ayni panel yenilendiyse (satistan sonra) imlec yerinde kalir; yeni panelde ilk satirdan baslar.
        if (_menuShownTitle != title) _menuRow = 0;
        _menuShownTitle = title;
        _menuRow = Mathf.Clamp(_menuRow, 0, Math.Max(0, items.Count - 1));
        OpenInventory(0); // Hud.Inventory.cs: sablon envanter ekrani, panel sag sutunda
    }

    public void HideMenu() { if (_menu != null) _menu.Visible = false; _menuShownTitle = null; CloseInventory(); }

    public void ActivateMenu(int i)
    {
        if (CraftOpen) { CraftActivate(i); return; } // Uretim sayfasi: satiri uret (Hud.Craft.cs)
        if (!StationOpen) { if (InventoryOpen) InventoryNumber(i); return; } // envanterde sayi tusu: kemer yuvasi
        if (i < 0 || i >= _menuItems.Count) return;
        var it = _menuItems[i];
        if (!it.Enabled()) { Sfx.Play("hit", null, -12f); return; }
        it.Act();
        RefreshMenu();
    }

    void RefreshMenu()
    {
        foreach (var n in _menuList.GetChildren())
            if (n is Label l && l.HasMeta("i"))
            {
                int i = (int)l.GetMeta("i");
                if (i < _menuItems.Count) l.AddThemeColorOverride("font_color", _menuItems[i].Enabled() ? Palette.OffWhite : new Color(Palette.OffWhite, 0.35f));
            }
    }

    // ---------- Cizim: envanter ekraninin sag sutunu ----------

    /// <summary>Satir etiketi "Ad ×n   ←   bedel   (sure)" ise iki satir: ad ve bedel.</summary>
    static (string head, string sub) SplitMenuLabel(string label)
    {
        const string sep = "   ←   ";
        int k = label.IndexOf(sep, StringComparison.Ordinal);
        return k < 0 ? (label, "") : (label[..k], label[(k + sep.Length)..]);
    }

    float MenuRowHeight(int i) => SplitMenuLabel(_menuItems[i].Label).sub.Length > 0 ? 56 : 42;

    float MenuTop
    {
        get
        {
            float h = 40;
            for (int i = 0; i < _menuItems.Count; i++) h += MenuRowHeight(i) + 5;
            float bottom = BagY0 + (((_d.State.Backpack.Slots + InventoryColumns - 1) / InventoryColumns) - 1) * T.Data.Inventory.Pitch + T.Data.Inventory.Slot;
            return Math.Min(BagY0 - 34, bottom - h - 50);
        }
    }

    Rect2 MenuRowRect(int i)
    {
        _o = CanvasFrame;
        float y = MenuTop + 40;
        for (int k = 0; k < i; k++) y += MenuRowHeight(k) + 5;
        return Area(RightX, y, ColW, MenuRowHeight(i));
    }

    void PaintStation()
    {
        _o = CanvasFrame;
        float x = RightX, top = MenuTop;
        Section(Fit(T.Upper(_menuTitle.Text), ColW, T.Data.Type.Section, true, true), x, top);
        float y = top + 40;
        if (_menuItems.Count == 0)
        {
            Rect(x, y, ColW, 42, T.Panel);
            Text(Loc.T("hud.menu_empty"), x + 14, y + 11, 17, T.Muted);
            y += 47;
        }
        for (int i = 0; i < _menuItems.Count; i++)
        {
            var it = _menuItems[i];
            bool on = it.Enabled();
            float h = MenuRowHeight(i);
            var area = Area(x, y, ColW, h);
            Color fill = i == _menuRow && (_padLast || !_pointerSeen) ? T.Selected : Under(area) && on ? T.PanelHover : T.Panel;
            _p.DrawRect(area, fill);
            if (i < 9) Keycap((i + 1).ToString(), x + 10, y + (h - 24) / 2f, 24, on ? T.Ink : T.Muted);
            var (head, sub) = SplitMenuLabel(it.Label);
            float tx = x + 46, tw = ColW - 56;
            if (it.Icon != null && ItemIcons.Has(it.Icon)) { ItemIcon(it.Icon, x + ColW - h - 2, y, h, h, on ? 1f : 0.45f, false); tw -= h; } // satirin esyasi (ItemIcons)
            if (sub.Length > 0)
            {
                Text(Fit(T.Upper(head), tw, 20, true, true), tx, y + 6, 20, on ? T.Ink : T.Muted, true, true);
                Text(Fit(sub, tw, 15), tx, y + 31, 15, on ? T.A(T.Ink, 0.8f) : T.A(T.Muted, 0.7f));
            }
            else Text(Fit(head, tw, 18, true), tx, y + (h - 18) / 2f - 2, 18, on ? T.Ink : T.Muted, true);
            y += h + 5;
        }
        if (!string.IsNullOrEmpty(_menuFoot.Text)) Paragraph(_menuFoot.Text, x, y + 4, ColW, 14, T.A(T.Ink, 0.7f), 3);
    }
}
