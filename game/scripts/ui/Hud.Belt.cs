using System;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Pano, alt (SANAT-SES 8.1, M1 karari 20): alt ortada 8 yuvali kemer (yuva numarasi, adet, dayaniklilik cubugu; secili
/// yuva mavi-beyaz), solunda Capa'nin takili basligi; sag altta can ve dayaniklilik cubuklari (ikonlu, sayilar saga yasli),
/// Civi Tabancasi takiliyken cephane onlarin ustunde.
/// </summary>
public partial class Hud
{
    double[] _beltFlashAt = new double[Kabarma.Sim.Belt.Slots];
    double _beltNameAt = -10;
    double _headAt = -10;

    /// <summary>Secili kemer yuvasi (1-8 ya da envanterde); yoksa -1.</summary>
    public int BeltSelected { get; private set; } = -1;

    /// <summary>Kemer yuvasini secer (Director.SelectBelt): mavi-beyaz olur, esyanin adi kemerin ustunde belirir.</summary>
    public void SelectBeltSlot(int i)
    {
        if (i < 0 || i >= Kabarma.Sim.Belt.Slots) return;
        BeltSelected = i;
        _beltNameAt = _now;
        FlashBelt(i);
    }

    float BeltX(int i)
    {
        var b = T.Data.Belt;
        float total = (Kabarma.Sim.Belt.Slots - 1) * b.Pitch + b.Size;
        return (T.DesignW - total) / 2f + i * b.Pitch;
    }

    /// <summary>Kemer yuvasinin ekrandaki alani (envanterde tiklama ve sinama icin).</summary>
    public Rect2 BeltSlotRect(int i)
    {
        _o = CanvasFrame;
        var b = T.Data.Belt;
        return Area(BeltX(i), b.Y, b.Size, b.Size);
    }

    void PaintBelt(bool inventory)
    {
        _o = CanvasFrame;
        var b = T.Data.Belt;
        var s = _d.State;
        for (int i = 0; i < Kabarma.Sim.Belt.Slots; i++)
        {
            var id = s.Belt.At(i);
            int n = id == null ? 0 : s.Backpack.Count(id);
            float x = BeltX(i), y = b.Y;
            float flash = (float)(_now - _beltFlashAt[i]);
            if (flash < b.FlashSeconds) y -= 4 * (1 - flash / b.FlashSeconds);
            bool sel = inventory ? _invFocusBelt == i : BeltSelected == i;
            bool dragged = _drag is { Moving: true, FromBelt: true } d && d.Index == i;
            SlotBox(x, y, b.Size, id, id == null ? -1 : n, inventory, sel, i + 1, ItemCondition(id), StarsOf(id), dragged ? 0.35f : 1f, dim: id != null && n == 0);
        }
        if (!inventory)
        {
            // Secilen yuvanin adi kemerin ustunde (Rust'ta elde tutulanin adi gibi), sonra soner
            float age = (float)(_now - _beltNameAt);
            if (BeltSelected >= 0 && age < b.NameSeconds && s.Belt.At(BeltSelected) is string sid)
            {
                float a = Mathf.Clamp((b.NameSeconds - age) / 0.4f, 0, 1);
                CentreText(T.Upper(ItemNames.Of(sid)), b.Y - 30, T.Data.Type.Body + 2, T.A(T.Ink, a), true, true, true);
            }
            PaintHeadSlot();
            PaintAmmo();
        }
        else Text("KEMER", BeltX(0), b.Y - 22, T.Data.Type.Caption + 1, T.A(T.Ink, 0.8f), true, true);
    }

    /// <summary>Kemerin solunda Capa'nin takili basligi: glif, ad, Q glifi (cark yalniz Q basiliyken acilir).</summary>
    void PaintHeadSlot()
    {
        var b = T.Data.Belt;
        int h = (int)_d.Capa.Current;
        float size = b.Size, x = BeltX(0) - b.Pitch - 14, y = b.Y;
        float flash = (float)(_now - _headAt);
        Rect(x, y, size, size, T.HudSlot);
        if (flash < 0.3f) Frame(x, y, size, size, T.A(T.Ivory, 1 - flash / 0.3f), 2);
        Glyph(HeadGlyphName(h), new Vector2(x + size / 2f, y + size / 2f - 7), 17, T.Ivory);
        string name = T.Upper(h < T.Data.Heads.Count ? T.Data.Heads[h].Title : Capa.HeadNames[h]);
        TextIn(Fit(name, size - 8, 13, true, true), x + 4, y + size - 19, size - 8, 13, T.Ink, HorizontalAlignment.Center, true, true);
        Keycap(_padLast ? "LB" : "Q", x + 4, y + 4, 20, T.A(T.Ink, 0.8f));
    }

    /// <summary>Takili baslik degisti (Capa.Select -> OnHead): baslik yuvasi bir an parlar.</summary>
    void NoteHeadChanged() => _headAt = _now;

    /// <summary>Civi Tabancasi takiliyken cephane: sag altta, can ve dayanikliligin ustunde.</summary>
    void PaintAmmo()
    {
        if (_d.Capa.Current != Capa.Head.Civi) return;
        _o = RightFrame;
        var v = T.Data.Vitals;
        float top = v.Bottom - 2 * v.Pitch - 40;
        var ammo = _d.Data.Tool.Nailgun.Ammo;
        bool free = _d.NightActive;
        int n = string.IsNullOrEmpty(ammo) ? 0 : _d.State.Backpack.Count(ammo);
        Glyph("civi", new Vector2(v.X + 12, top + 18), 11, T.Ink);
        Text("ÇİVİ", v.X + 28, top + 9, T.Data.Type.Small, T.A(T.Ink, 0.85f), true, true, true);
        if (free) TextIn("KABARMA'DA SINIRSIZ", v.X, top + 7, v.Width - 6, T.Data.Type.Body, T.Short, HorizontalAlignment.Right, true, true, true);
        else TextIn(n.ToString(CultureInfo.InvariantCulture), v.X, top, v.Width - 6, 30, n == 0 ? T.Red : T.Ink, HorizontalAlignment.Right, true, true, true);
    }

    /// <summary>Sag alt: can ve dayaniklilik. Sayilar saga yasli.</summary>
    void PaintVitals(bool inventory)
    {
        _o = RightFrame;
        var v = T.Data.Vitals;
        var st = StaminaSource?.Invoke() ?? (1f, 1f, false);
        (float value, float max, Color colour, string glyph)[] bars =
        {
            (_d.Player.Hp, _d.Player.MaxHp, T.Health, "health"),
            (st.value, st.max, st.exhausted ? T.Short : T.Stamina, "stamina"),
        };
        for (int i = 0; i < bars.Length; i++)
        {
            var (value, max, colour, glyph) = bars[i];
            float x = v.X, y = v.Bottom - (bars.Length - i) * v.Pitch;
            Rect(x, y, v.Width, v.Height, inventory ? T.Panel : T.HudSlot);
            float fill = v.FillWidth * Math.Clamp(max <= 0 ? 0 : value / max, 0, 1);
            if (fill > 0) Rect(x + v.FillLeft, y + (v.Height - v.FillHeight) / 2f, fill, v.FillHeight, new Color(colour, inventory ? 1 : 0.9f));
            Glyph(glyph, new Vector2(x + 15, y + v.Height / 2f), 9.5f, T.Ink);
            string num = (StaminaSource == null && glyph == "stamina") ? "—" : MathF.Round(value).ToString(CultureInfo.InvariantCulture);
            TextIn(num, x + v.FillLeft, y + (v.Height - 21) / 2f - 1, v.FillWidth - 6, 21, T.Ink, HorizontalAlignment.Right, true, false, true);
        }
    }

    /// <summary>Dayaniklilik (kondisyon) 0..1: M1'de hicbir esyada yok; cubuk destekli, ciziLmez.</summary>
    static float? ItemCondition(string id) => null;

    /// <summary>
    /// Esyanin kalite yildizi (1-3). Bu dalda canta kaliteyi tasimaz (ekonomi akisi Inventory'ye ★ ekliyor: Stacks); birlesince
    /// bu tek yontem yigindan okur. 0: yildiz cizilmez.
    /// </summary>
    int StarsOf(string id) => 0;

    /// <summary>Canta yuvasinin kalite yildizi (birlesince Inventory.Stacks[i].stars).</summary>
    int StarsAt(int bagIndex) => 0;
}
