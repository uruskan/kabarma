using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Guc gostergesi (Rust tarzi: koyu yari saydam kutular, duz renk dolgu): Sarj cubugu, iki etkin yuva (ikincisi
/// kilitli), ulti ve Maya olceri. Sag alt kosede, kemerin ustunde. Hic guc ve ulti yokken gizlidir.
/// </summary>
public partial class AbilityHud : CanvasLayer
{
    readonly AbilitySystem _sys;
    Control _root;
    ColorRect _chargeFill, _meterFill;
    Label _chargeText, _meterText;
    readonly Label[] _slotName = new Label[3];
    readonly ColorRect[] _slotCd = new ColorRect[3];
    readonly Label[] _slotKey = new Label[3];

    const float W = 300f, SlotH = 46f;

    public AbilityHud(AbilitySystem sys) { _sys = sys; Layer = 5; Name = "AbilityHud"; }

    public override void _Ready()
    {
        _root = new Control { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -W - 24, OffsetRight = -24, OffsetTop = -250, OffsetBottom = -120, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        float y = 0;
        for (int i = 0; i < 3; i++)
        {
            var box = Box(new Rect2(0, y, W, SlotH));
            _slotKey[i] = Text(box, new Rect2(8, 0, 70, SlotH), 13, Palette.Warning);
            _slotName[i] = Text(box, new Rect2(80, 0, W - 88, SlotH), 17, Palette.OffWhite);
            _slotCd[i] = new ColorRect { Color = new Color(0, 0, 0, 0.55f), Position = Vector2.Zero, Size = new Vector2(0, SlotH), MouseFilter = Control.MouseFilterEnum.Ignore };
            box.AddChild(_slotCd[i]);
            y += SlotH + 4;
        }
        (_chargeFill, _chargeText) = Bar(y, Palette.MayaTeal); y += 18;
        (_meterFill, _meterText) = Bar(y, Palette.MayaViolet);
    }

    Control Box(Rect2 r)
    {
        var bg = new ColorRect { Color = new Color(0.08f, 0.08f, 0.08f, 0.62f), Position = r.Position, Size = r.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.AddChild(bg);
        return bg;
    }

    static Label Text(Control parent, Rect2 r, int size, Color c)
    {
        var l = new Label { Position = r.Position, Size = r.Size, VerticalAlignment = VerticalAlignment.Center, ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        parent.AddChild(l);
        return l;
    }

    (ColorRect, Label) Bar(float y, Color c)
    {
        var bg = Box(new Rect2(0, y, W, 14));
        var fill = new ColorRect { Color = c, Position = Vector2.Zero, Size = new Vector2(W, 14), MouseFilter = Control.MouseFilterEnum.Ignore };
        bg.AddChild(fill);
        var t = Text(bg, new Rect2(6, -2, W - 12, 18), 11, Palette.OffWhite);
        return (fill, t);
    }

    public override void _Process(double delta)
    {
        var f = _sys.F;
        var l = _sys.L;
        bool any = _sys.D.State.Abilities.Unlocked.Count > 0 || _sys.D.State.Atalar.Ultis.Count > 0;
        _root.Visible = any;
        if (!any) return;
        AbilityRules.Normalize(f, l);
        var c = _sys.D.Controls;
        string Key(string act) => c != null ? $"[{c.Glyph(act)}]" : act;
        for (int i = 0; i < 3; i++)
        {
            bool ulti = i == 2;
            string id = ulti ? l.Ulti : l.Slots[i];
            bool locked = !ulti && i >= f.ActiveSlots;
            var def = string.IsNullOrEmpty(id) ? null : f.Def(id);
            _slotKey[i].Text = Key(ulti ? "ulti" : i == 0 ? "ability_1" : "ability_2");
            _slotName[i].Text = locked ? "Kilitli · Kurbağa Ağa" : def == null ? (ulti ? "Ulti yok" : "Boş yuva") : (ulti ? "Ulti · " : "") + def.Name;
            _slotName[i].AddThemeColorOverride("font_color", locked || def == null ? Palette.OffWhite.Darkened(0.45f) : Palette.OffWhite);
            float frac = 0;
            if (def != null && !ulti && def.Cooldown > 0) frac = l.CooldownOf(id) / def.Cooldown;
            if (def != null && !ulti && l.Charge < def.Charge) frac = Mathf.Max(frac, 0.25f);
            if (ulti && def != null) frac = l.GiantLeft > 0 ? 0 : 1f - l.Meter / f.UltiMeter.Max;
            _slotCd[i].Size = new Vector2(W * Mathf.Clamp(frac, 0, 1), SlotH);
        }
        _chargeFill.Size = new Vector2(W * l.Charge / f.Charge.Max, 14);
        _chargeText.Text = $"Şarj {l.Charge:0}/{f.Charge.Max:0}";
        _meterFill.Size = new Vector2(W * l.Meter / f.UltiMeter.Max, 14);
        _meterText.Text = l.GiantLeft > 0 ? $"Dev Mod {l.GiantLeft:0} sn" : $"Maya ölçeri {l.Meter:0}/{f.UltiMeter.Max:0}";
    }
}
