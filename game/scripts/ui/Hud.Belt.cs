using System;
using System.Globalization;
using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Pano, alt (SANAT-SES 8.1, M1 kararlari 20 ve 23): alt ortada 8 yuvali kemer (yuva numarasi, adet, dayaniklilik cubugu;
/// secili yuva = elde tutulan, mavi-beyaz). Capa'nin yuvasinin sag ustunde takili basligin kucuk simgesi (ayri baslik kutusu
/// kalkti). Sag altta can ve dayaniklilik cubuklari (ikonlu, sayilar saga yasli), Capa elde ve Civi Tabancasi takiliyken
/// cephane onlarin ustunde.
/// </summary>
public partial class Hud
{
    double[] _beltFlashAt = new double[Kabarma.Sim.Belt.Slots];
    double _beltNameAt = -10;
    double _headAt = -10;

    /// <summary>Secili kemer yuvasi = elde tutulan (Sim: State.Belt.Selected); yoksa -1.</summary>
    public int BeltSelected => _d.State.Belt.Selected;

    /// <summary>Kemer yuvasi secildi (Director.SelectBelt): yuva bir an kalkar, esyanin adi kemerin ustunde belirir.</summary>
    public void SelectBeltSlot(int i)
    {
        if (i < 0 || i >= Kabarma.Sim.Belt.Slots) return;
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
            bool tool = id != null && s.Belt.IsTool(id);
            SlotBox(x, y, b.Size, tool && !ItemIcons.Has(id) ? null : id, id == null || tool ? -1 : n, inventory, sel, i + 1, ItemCondition(id), StarsOf(id), dragged ? 0.35f : 1f, dim: id != null && !tool && n == 0);
            if (tool && !ItemIcons.Has(id)) Glyph(id == PlanTools.Plan ? "insa" : "kazma", new Vector2(x + b.Size / 2f, y + b.Size / 2f), b.Size * 0.26f, T.A(T.Ivory, dragged ? 0.35f : 1f)); // ikonu olmayan alet: glif
            if (id == PlanTools.Capa) PaintHeadBadge(x, y, b.Size); // takili baslik Capa'nin yuvasinda
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
            PaintAmmo();
        }
        else Text(Loc.T("hud.belt"), BeltX(0), b.Y - 22, T.Data.Type.Caption + 1, T.A(T.Ink, 0.8f), true, true);
    }

    /// <summary>
    /// Capa'nin yuvasinin sag ustunde takili basligin kucuk simgesi (karar 23: ayri "BASLIK" kutusu kalkti). Baslik
    /// degisince bir an parlar; Capa eldeyken yanina Q glifi (cark yalniz Capa eldeyken acilir).
    /// </summary>
    void PaintHeadBadge(float x, float y, float size)
    {
        int h = (int)_d.Capa.Current;
        float flash = (float)(_now - _headAt);
        float r = 13, cx = x + size - r - 3, cy = y + r + 3;
        Disc(cx, cy, r, T.A(T.Shade, 0.9f));
        Disc(cx, cy, r, flash < 0.3f ? T.A(T.Ivory, 1 - flash / 0.3f) : T.A(T.Ivory, 0.55f), false, 1.2f);
        Glyph(HeadGlyphName(h), new Vector2(cx, cy), 7.5f, T.Ivory);
        if (_d.Capa.InHand && !InventoryOpen) Keycap(KeyText("head_wheel", PadDevice ? "LB" : "Q"), x + size / 2f - 10, y - 24, 20, T.A(T.Ink, 0.8f), PadDevice);
    }

    /// <summary>Takili baslik degisti (Capa.Select -> OnHead): baslik yuvasi bir an parlar.</summary>
    void NoteHeadChanged() => _headAt = _now;

    /// <summary>Civi Tabancasi takiliyken cephane: sag altta, can ve dayanikliligin ustunde.</summary>
    void PaintAmmo()
    {
        if (_d.Capa.Current != Capa.Head.Civi || !_d.Capa.InHand) return;
        _o = RightFrame;
        var v = T.Data.Vitals;
        float top = v.Bottom - 2 * v.Pitch - 40;
        var ammo = _d.Data.Tool.Nailgun.Ammo;
        bool free = _d.NightActive;
        int n = string.IsNullOrEmpty(ammo) ? 0 : _d.State.Backpack.Count(ammo);
        Glyph("civi", new Vector2(v.X + 12, top + 18), 11, T.Ink);
        Text(Loc.T("hud.nail"), v.X + 28, top + 9, T.Data.Type.Small, T.A(T.Ink, 0.85f), true, true, true);
        if (free) TextIn(Loc.T("hud.ammo_unlimited"), v.X, top + 7, v.Width - 6, T.Data.Type.Body, T.Short, HorizontalAlignment.Right, true, true, true);
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
    /// Kemer yuvasinin kalite yildizi: cantadaki o esyanin en yuksek kalitesi (Inventory.Stacks, ekonomi). 0: cizilmez.
    /// </summary>
    int StarsOf(string id) => id == null ? 0 : Shown(_d.State.Backpack.Stacks.Where(s => s.id == id).Select(s => s.stars).DefaultIfEmpty(0).Max());

    /// <summary>Canta yuvasinin kalite yildizi (Inventory.Stacks[i].stars; Contents ile ayni sira).</summary>
    int StarsAt(int bagIndex) { var st = _d.State.Backpack.Stacks; return bagIndex >= 0 && bagIndex < st.Count ? Shown(st[bagIndex].stars) : 0; }

    /// <summary>Taban kalite (Inventory.BaseStars) yildizsiz gorunur; ustu yuvada yildizla.</summary>
    static int Shown(int stars) => stars > Inventory.BaseStars ? stars : 0;
}
