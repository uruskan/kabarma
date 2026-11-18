using System;
using System.Collections.Generic;
using Godot;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Cark (GDD 4, 7; SANAT-SES 8.1; M1 kararlari 20 ve 23). Uc tur: Capa'nin bes is basligi, Tirpan Isini ve Civi Tabancasi
/// (yalniz Capa eldeyken, Q); Insa Plani'nin parca carki (sag tik basili); Insa Plani ile parcaya bakip E basili (yukselt,
/// onar, sok). Dilimler listeden gelir (<see cref="WheelSlice"/>). Basliklar **yalniz** buradan secilir. Q basili tutulur (kolda sol tampon), fareyle ya da sag cubukla dilim secilir, birakinca takilir;
/// Esc ya da sag tik secmeden kapatir. Gorunus Rust'in insa carki gibi: krem halka, kizil-turuncu secim dilimi, ortada
/// secilenin buyuk glifi, adi ve bir satir aciklamasi. Basvuru: Hollow-Universe GroundUi.Radial.cs.
/// </summary>
public partial class Hud
{
    public enum WheelKind { Heads, Pieces, Plan, Book }
    /// <summary>Carkin bir dilimi: glif (ya da esya ikonu), baslik, aciklama, secilebilir mi.</summary>
    public sealed record WheelSlice(string Glyph, string Icon, string Title, string Text, bool Enabled = true);

    Control _wheel;
    readonly List<PanelContainer> _wheelSlots = new();
    readonly List<WheelSlice> _slices = new();
    Vector2 _wheelDir;
    int _wheelSel = -1, _wheelCurrent;
    public bool WheelOpen => _wheel != null && _wheel.Visible;
    /// <summary>Acik carkin turu.</summary>
    public WheelKind OpenWheelKind { get; private set; }
    /// <summary>Carkta su an secili dilim (sinama icin).</summary>
    public int WheelSelection => _wheelSel;

    int WheelCount => Math.Max(1, _slices.Count);

    /// <summary>Baslik carki (Q, Capa eldeyken): dilimler Capa.Head sirasiyla.</summary>
    public void OpenWheel(int current)
    {
        var l = new List<WheelSlice>();
        for (int i = 0; i < Capa.HeadNames.Length; i++)
        {
            var head = i < T.Data.Heads.Count ? T.Data.Heads[i] : new Kabarma.Sim.UiHeadDef { Title = Capa.HeadNames[i] };
            l.Add(new WheelSlice(HeadGlyphName(i), null, head.Title, head.Text, _d.Capa.IsAvailable((Capa.Head)i)));
        }
        OpenWheel(WheelKind.Heads, l, current);
    }

    /// <summary>Listeden cark: Insa Plani'nin parcalari ya da E basili eylemleri.</summary>
    public void OpenWheel(WheelKind kind, List<WheelSlice> slices, int current)
    {
        _slices.Clear();
        _slices.AddRange(slices);
        OpenWheelKind = kind;
        _wheel.Visible = true;
        _wheelDir = Vector2.Zero;
        _wheelSel = current;
        _wheelCurrent = current;
        Sfx.Play("place", null, -20f);
    }

    /// <summary>Fare hareketi carkta yon secer (fare kilitli oldugu icin goreli hareket birikir).</summary>
    public void WheelMotion(Vector2 rel)
    {
        _wheelDir = (_wheelDir + rel * 0.02f).LimitLength(1f);
        PickWheel();
    }

    void PickWheel()
    {
        if (_wheelDir.Length() <= T.Data.Wheel.DeadZone) return;
        int n = WheelCount;
        float a = Mathf.PosMod(Mathf.Atan2(_wheelDir.X, -_wheelDir.Y), Mathf.Tau);
        int sel = Mathf.PosMod((int)Mathf.Round(a / (Mathf.Tau / n)), n);
        if (sel != _wheelSel) Sfx.Play("place", null, -26f);
        _wheelSel = sel;
    }

    /// <summary>Kol: sag cubuk carkta yon secer (girdi akisi da ayni yere baglar).</summary>
    void UpdateWheelPad()
    {
        if (!WheelOpen) return;
        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        if (stick.Length() > 0.5f) { _wheelDir = stick.LimitLength(1f); PickWheel(); }
    }

    /// <summary>Q birakildi: secili dilimi dondurur (Director takar).</summary>
    public int CloseWheel() { _wheel.Visible = false; return _wheelSel; }

    /// <summary>Esc ya da sag tik: cark secmeden kapanir; Q'nun birakilmasi artik bir sey yapmaz.</summary>
    public void CancelWheel()
    {
        if (!WheelOpen) return;
        _wheel.Visible = false;
        _wheelSel = -1;
        _d.Player.LookLocked = false;
    }

    /// <summary>Eski dugum yerlesimi (Hud._Process cagirir): cark artik ciziliyor, yerlesecek dugum yok.</summary>
    void LayoutWheel(Vector2 vp) { }

    // ---------- Cizim ----------

    static double WheelAngle(int i, int n) => -Math.PI / 2 + Math.Tau / n * i;

    void PaintWheel()
    {
        _p.DrawRect(new Rect2(Vector2.Zero, ScreenSize), new Color(0.025f, 0.03f, 0.03f, 0.26f));
        _o = CentreFrame;
        var w = T.Data.Wheel;
        int n = WheelCount;
        int shown = _wheelSel >= 0 ? _wheelSel : _wheelCurrent;
        if (_slices.Count == 0) return;
        double sector = Math.Tau / n;
        Annulus(0, Math.Tau, w.Inner, w.Outer, T.Ivory);
        if (shown >= 0 && shown < n)
        {
            double a = WheelAngle(shown, n);
            Annulus(a - sector / 2, a + sector / 2, w.SelectInner, w.SelectOuter, T.Brick);
        }
        float cx = T.DesignW / 2f, cy = T.DesignH / 2f;
        for (int i = 0; i < n; i++)
        {
            double a = WheelAngle(i, n);
            float r = i == shown ? w.IconRadiusSelected : w.IconRadius;
            var at = new Vector2(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
            var sl = _slices[i];
            var tone = i == shown ? T.Ivory : sl.Enabled ? T.Brick : T.A(T.Brick, 0.35f);
            if (sl.Icon != null && ItemIcons.Has(sl.Icon)) ItemIcon(sl.Icon, at.X - w.IconSize * 1.1f, at.Y - w.IconSize * 1.1f, w.IconSize * 2.2f, w.IconSize * 2.2f, sl.Enabled ? 1f : 0.4f, false);
            else Glyph(sl.Glyph, at, w.IconSize, tone);
            if (i == _wheelCurrent) Disc(at.X, at.Y + w.IconSize + 9, 3, i == shown ? T.Ivory : T.Brick); // takili baslik noktasi
        }
        if (shown >= 0 && shown < n)
        {
            var sl = _slices[shown];
            if (sl.Icon != null && ItemIcons.Has(sl.Icon)) ItemIcon(sl.Icon, cx - 46, cy - 150, 92, 92, sl.Enabled ? 1f : 0.45f, false);
            else Glyph(sl.Glyph, new Vector2(cx, cy - 105), w.CenterIconSize * 0.62f, T.Brick);
            WheelText(T.Upper(sl.Title), cy - 35, 330, 60, 30, sl.Enabled ? T.Ivory : T.Muted, true);
            WheelText(sl.Text, cy + 26, 320, 50, 18, T.Ivory, false);
            if (shown == _wheelCurrent && OpenWheelKind != WheelKind.Plan) WheelText(OpenWheelKind == WheelKind.Heads ? "TAKILI" : "SEÇİLİ", cy + 80, 300, 30, 16, T.Short, true);
        }
        string foot = OpenWheelKind switch
        {
            WheelKind.Pieces => $"{KeyText("build_wheel", PadDevice ? "LB" : "SAĞ TIK")} BIRAKILINCA SEÇİLİR   ·   ESC: VAZGEÇ",
            WheelKind.Plan => $"{KeyText("interact", PadDevice ? "X" : "E")} BIRAKILINCA YAPILIR   ·   ESC / SAĞ TIK: VAZGEÇ",
            WheelKind.Book => $"{KeyText("plan_book", PadDevice ? "→" : "B")} BIRAKILINCA: BOŞ YUVA KAYDEDER, DOLU YUVA YERLEŞTİRİR   ·   ESC: VAZGEÇ",
            _ => $"{KeyText("head_wheel", PadDevice ? "LB" : "Q")} BIRAKILINCA TAKILIR   ·   ESC / SAĞ TIK: VAZGEÇ",
        };
        CentreText(foot, T.DesignH - 108, 17, T.Ivory, false, false, true);
    }

    /// <summary>Ortadaki alanlarin her biri kendi kutusuna sarar ve sigar.</summary>
    void WheelText(string value, float y, float width, float height, float size, Color c, bool bold)
    {
        var lines = WrapLines(value, width, size, bold, bold);
        while (size > 12 && lines.Count * (size + 3) > height) { size--; lines = WrapLines(value, width, size, bold, bold); }
        for (int i = 0; i < lines.Count; i++)
            TextIn(lines[i], T.DesignW / 2f - width / 2f, y + i * (size + 3), width, size, c, HorizontalAlignment.Center, bold, bold);
    }

    /// <summary>Dolu halka dilimi, kenarlari yumusatilmis.</summary>
    void Annulus(double from, double to, float inner, float outer, Color c)
    {
        var centre = Pt(T.DesignW / 2f, T.DesignH / 2f);
        float r0 = T.Pxf(inner), r1 = T.Pxf(outer);
        int pieces = Math.Max(1, (int)Math.Ceiling((to - from) / (Math.PI / 2)));
        for (int p = 0; p < pieces; p++)
        {
            double a0 = from + (to - from) * p / pieces, a1 = from + (to - from) * (p + 1) / pieces;
            int steps = Math.Max(8, (int)Math.Ceiling((a1 - a0) / Math.Tau * 256));
            var pts = new Vector2[2 * (steps + 1)];
            for (int i = 0; i <= steps; i++)
            {
                double a = a0 + (a1 - a0) * i / steps;
                var d = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                pts[i] = centre + d * r1;
                pts[2 * steps + 1 - i] = centre + d * r0;
            }
            _p.DrawColoredPolygon(pts, c);
        }
        int arc = Math.Max(16, (int)Math.Ceiling((to - from) / Math.Tau * 256));
        _p.DrawArc(centre, r1, (float)from, (float)to, arc, c, 1.2f, true);
        _p.DrawArc(centre, r0, (float)from, (float)to, arc, c, 1.2f, true);
        if (to - from < Math.Tau - 1e-3)
            foreach (double a in new[] { from, to })
            {
                var d = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                _p.DrawLine(centre + d * r0, centre + d * r1, c, 1.2f, true);
            }
    }
}
