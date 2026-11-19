using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Pano, girdi: tus glifleri ({act:x} → son kullanilan aygitin glifi, SANAT-SES §8.1) ve menu imleci (oyun kolu:
/// ✚ ile sec, Ⓐ ile onayla). Ham metin saklanir, gosterirken genisletilir; aygit degisince sabit yazilar yenilenir.
/// </summary>
public partial class Hud
{
    static string HeadHintText => Loc.T("hud.head_hint");
    string _menuFootRaw = "";
    StyleBoxFlat _cursorBox;

    /// <summary>Metni gosterime hazirlar: yer tutucular glife doner (Controls yoksa silinir).</summary>
    public string G(string s) => _d?.Controls != null ? _d.Controls.Expand(s) : InputText.Strip(s);

    /// <summary>_Ready sonunda bir kez: aygit degisimine abone olur, sabit yazilari ilk kez doldurur.</summary>
    void SetupControls()
    {
        if (_d?.Controls != null) _d.Controls.DeviceChanged += OnDeviceChanged;
        RefreshGlyphs();
    }

    void OnDeviceChanged(InputDevice d)
    {
        if (!IsInstanceValid(this)) return;
        RefreshGlyphs();
    }

    void RefreshGlyphs()
    {
        if (_headHint != null) _headHint.Text = G(HeadHintText);
        if (_menuFoot != null) _menuFoot.Text = G(_menuFootRaw);
        for (int i = 0; i < _beltSlots.Count; i++)
            if (_beltSlots[i].GetChild(0) is VBoxContainer v && v.GetChildCount() > 0 && v.GetChild(0) is Label k)
                k.Text = _d?.Controls?.Last == InputDevice.Pad ? "" : $"Alt+{i + 1}";
    }

    public int MenuCount => CraftOpen ? CraftRows().Count : _menuItems.Count;

    /// <summary>Menu imleci: secili satirin cercevesi (oyun kolu ✚ ile tasir, Ⓐ ile onaylar; klavyede Enter).</summary>
    public void MenuCursor(int sel)
    {
        if (CraftOpen) { CraftCursor(sel); return; } // Uretim sayfasi (Hud.Craft.cs)
        _cursorBox ??= new StyleBoxFlat
        {
            BgColor = new Color(Palette.Warning, 0.18f), BorderColor = Palette.Warning,
            BorderWidthLeft = 3, CornerRadiusTopLeft = 6, CornerRadiusBottomLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8, ContentMarginRight = 8,
        };
        foreach (var n in _menuList.GetChildren())
            if (n is Label l && l.HasMeta("i"))
            {
                bool on = (int)l.GetMeta("i") == sel;
                if (on == l.HasThemeStyleboxOverride("normal")) continue;
                if (on) l.AddThemeStyleboxOverride("normal", _cursorBox); else l.RemoveThemeStyleboxOverride("normal");
            }
    }
}
