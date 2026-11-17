using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>Pano: ortak menu sablonu (istasyon, canta, satis). SANAT-SES §8.2.</summary>
public partial class Hud
{
    // ---------- Menu: istasyon, canta, satis (ortak sablon, SANAT-SES §8.2) ----------
    public sealed record MenuItem(string Label, System.Func<bool> Enabled, System.Action Act);
    PanelContainer _menu;
    VBoxContainer _menuList;
    Label _menuTitle, _menuFoot;
    readonly List<MenuItem> _menuItems = new();
    public bool MenuOpen => _menu != null && _menu.Visible;

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
        if (items.Count == 0) _menuList.AddChild(L("Burada yapılacak bir şey yok.", 20, Palette.Fog));
        _menu.Visible = true;
        RefreshMenu();
    }

    public void HideMenu() { if (_menu != null) _menu.Visible = false; }

    public void ActivateMenu(int i)
    {
        if (!MenuOpen || i < 0 || i >= _menuItems.Count) return;
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
}
