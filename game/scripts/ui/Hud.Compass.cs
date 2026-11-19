using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Pano, gunduz (SANAT-SES 8.1; URETIM 6 M1 karar 11 ve 20): ust ortada pusula seridi (Ocak'i ve etkin hedefi isaretler),
/// altinda etkin adimin tek satiri ve onun altinda Defter takibi. Rust tarzi: ince koyu serit, dar kalin yazi.
/// </summary>
public partial class Hud
{
    string _stepText = "", _defterText = "";
    float _heading;
    Vector3 _compassOrigin;
    Vector3? _compassHome, _compassTarget;
    string _compassTargetName = "";

    /// <summary>
    /// Dayaniklilik cubugunun eski yeri (oyuncu akisinin Hud.Stamina.cs'i sol alt kutuya ekler). Yeni panoda dayaniklilik
    /// sag altta <see cref="StaminaSource"/>'tan cizilir; bu kutu gizli tutucuda kalir.
    /// </summary>
    public VBoxContainer StaminaSlot { get; private set; }

    /// <summary>Hud._Ready'den bir kez: gecehud panolari (Rust tarzi cizim katmani, envanterin Defter sayfasi, rapor).</summary>
    void BuildGeceHud(Control root)
    {
        // Eski sag ust Defter kutusu ve can altindaki canta satiri emekli.
        _goalPanel.Visible = false;
        _bag.Visible = false;
        StaminaSlot = new VBoxContainer { Name = "StaminaSlot" };
        _bottomLeft.AddChild(StaminaSlot);
        _bottomLeft.MoveChild(StaminaSlot, _hp.GetIndex() + 1);

        BuildRustUi(root);     // Hud.Rust.cs: cizim katmani, olcek, eski dugumler gizli
        BuildInventory(root);  // Hud.Inventory.cs: Defter sayfasi (Control)
    }

    /// <summary>Hud._Process'ten her kare.</summary>
    void UpdateGeceHud(float dt)
    {
        UpdateCompass();
        UpdateStepLines();
        UpdateRustUi(dt);      // Hud.Rust.cs
    }

    void UpdateCompass()
    {
        var p = _d.Player;
        var fwd = -p.Cam.GlobalBasis.Z;
        _heading = Mathf.Atan2(fwd.X, -fwd.Z);
        _compassOrigin = p.GlobalPosition;
        _compassHome = _d.Ocak != null ? _d.Ocak.GlobalPosition : null;
        _compassTarget = _d.CompassTarget(out var name);
        _compassTargetName = name;
    }

    /// <summary>Etkin adimin tek satiri; altinda Defter takibi (takip edilen is emri zinciri).</summary>
    void UpdateStepLines()
    {
        var s = _d.State;
        var step = s.Scenario.Current;
        var tracked = s.Orders.Tracked;
        string orderLine = "";
        if (tracked?.Current is OrderStepDef cur)
        {
            int i = tracked.CurrentStep;
            orderLine = $"{tracked.Def.Name} {i + 1}/{tracked.Def.Steps.Count} · {cur.Goal}" + (cur.Target > 1 ? $"  {tracked.Progress(i)}/{cur.Target}" : "");
        }
        if (step != null)
        {
            _stepText = GoalText(step.Goal) + (step.Target > 1 ? $"   {s.Scenario.Progress}/{step.Target}" : "");
            _defterText = s.Orders.DefterOpen && orderLine != "" ? Loc.T("hud.defter_prefix", orderLine) : "";
        }
        else
        {
            _stepText = orderLine != "" ? orderLine : Loc.T("hud.defter_total", s.Orders.OrdersTotal);
            _defterText = "";
        }
    }

    /// <summary>Adim metni ekranda: {act:eylem} yer tutucusu girdi akisinin genisleticisiyle aygitin glifine doner.</summary>
    string GoalText(string goal) => G(goal); // entegrasyon: girdi akisinin genisleticisi (Hud.Controls.cs)

    // ---------- Cizim ----------

    static readonly (string key, float angle)[] Cardinals =
    {
        ("hud.compass_n", 0f), ("hud.compass_ne", Mathf.Pi * 0.25f), ("hud.compass_e", Mathf.Pi * 0.5f), ("hud.compass_se", Mathf.Pi * 0.75f),
        ("hud.compass_s", Mathf.Pi), ("hud.compass_sw", -Mathf.Pi * 0.75f), ("hud.compass_w", -Mathf.Pi * 0.5f), ("hud.compass_nw", -Mathf.Pi * 0.25f),
    };

    float CompassX(float bearing, out bool inside, float left, float width)
    {
        float half = Mathf.DegToRad(_d.Data.Hud.CompassFovDegrees) / 2f;
        float rel = Mathf.Wrap(bearing - _heading, -Mathf.Pi, Mathf.Pi);
        inside = Mathf.Abs(rel) <= half;
        return left + width / 2f + Mathf.Clamp(rel / half, -1f, 1f) * (width / 2f - 10f);
    }

    float Bearing(Vector3 to) { var d = to - _compassOrigin; return Mathf.Atan2(d.X, -d.Z); }

    /// <summary>Ust orta: pusula seridi (yonler, 15 derecede bir kademe, Ocak ve hedef isareti), altinda adim satirlari.</summary>
    void PaintCompass()
    {
        _o = TopFrame;
        var c = T.Data.Compass;
        float w = c.Width, h = c.Height, x = (T.DesignW - w) / 2f, y = c.Y;
        Rect(x, y, w, h, T.HudSlot);
        Poly(T.Ivory, new Vector2(T.DesignW / 2f - 5, y), new Vector2(T.DesignW / 2f + 5, y), new Vector2(T.DesignW / 2f, y + 6));
        for (int deg = 0; deg < 360; deg += 15)
        {
            float cx = CompassX(Mathf.DegToRad(deg), out bool inside, x, w);
            if (!inside) continue;
            bool major = deg % 45 == 0;
            Rect(Mathf.Round(cx), y + (major ? h - 8 : h - 5), 1, major ? 8 : 5, T.A(T.Ink, major ? 0.7f : 0.35f));
        }
        foreach (var (key, a) in Cardinals)
        {
            float cx = CompassX(a, out bool inside, x, w);
            if (!inside) continue;
            string label = Loc.T(key);
            float size = label.Length == 1 ? 17 : 13;
            TextIn(label, cx - 20, y + 3 + (17 - size) / 2f, 40, size, a == 0f ? T.Warning : T.Ink, HorizontalAlignment.Center, true, true);
        }
        // Isaretler: hedef (ucgen) ve Ocak (baklava) seridin altinda, adlari yanlarinda.
        float? xh = _compassHome is Vector3 hp ? CompassX(Bearing(hp), out _, x, w) : null;
        float? xt = _compassTarget is Vector3 tp ? CompassX(Bearing(tp), out _, x, w) : null;
        Rect2? taken = null;
        if (_compassTarget is Vector3 t) taken = CompassMarker(t, T.Teal, _compassTargetName, false, false, null, xh, x, w, y + h + 2);
        if (_compassHome is Vector3 home) CompassMarker(home, T.Short, "Ocak", true, true, taken, xt, x, w, y + h + 2);

        if (!string.IsNullOrEmpty(_stepText))
        {
            float size = T.Data.Type.Body + 3;
            string line = Fit(_stepText, 900, size, true, true);
            CentreText(line, c.StepY, size, T.Ink, true, true, true);
        }
        if (!string.IsNullOrEmpty(_defterText))
            CentreText(Fit(_defterText, 900, T.Data.Type.Small, true, true), c.StepY + 25, T.Data.Type.Small, T.A(T.Ink, 0.75f), true, true, true);
    }

    /// <summary>Isareti ve adini cizer; adin kapladigi alani (tasarim) dondurur (avoid ile cakisirsa ikinci ad yazilmaz).</summary>
    Rect2? CompassMarker(Vector3 at, Color c, string name, bool diamond, bool leftLabel, Rect2? avoid, float? otherX, float left, float width, float top)
    {
        float x = CompassX(Bearing(at), out bool inside, left, width);
        float ox = otherX is float o && Mathf.Abs(o - x) < 18f ? o : x;
        var flat = at - _compassOrigin; flat.Y = 0;
        int dist = Mathf.RoundToInt(flat.Length());
        if (diamond) Poly(c, new Vector2(x, top), new Vector2(x + 6, top + 7), new Vector2(x, top + 14), new Vector2(x - 6, top + 7));
        else Poly(c, new Vector2(x - 7, top + 13), new Vector2(x + 7, top + 13), new Vector2(x, top));
        if (!inside)
        {
            float dir = x < left + width / 2f ? -1f : 1f;
            Poly(c, new Vector2(x + dir * 14, top + 7), new Vector2(x + dir * 8, top + 2), new Vector2(x + dir * 8, top + 12));
        }
        string text = T.Upper(string.IsNullOrEmpty(name) ? $"{dist} m" : $"{name} · {dist} m");
        float size = T.Data.Type.Caption;
        float tw = Measure(text, size, true, true);
        float lo = Mathf.Min(x, ox), hi = Mathf.Max(x, ox);
        bool toLeft = leftLabel ? lo - 12 - tw >= left : hi + 12 + tw > left + width;
        Rect2 Spot(bool l) => new(l ? lo - 12 - tw : hi + 12, top, tw, 14);
        Rect2? icon = otherX is float oi ? new Rect2(oi - 8, top, 16, 14) : null;
        bool Bad(Rect2 r) => r.Position.X < left || r.End.X > left + width
            || (avoid is Rect2 a && r.Grow(4).Intersects(a)) || (icon is Rect2 ic && r.Grow(2).Intersects(ic));
        Rect2 rect = Spot(toLeft);
        if (Bad(rect))
        {
            var alt = Spot(!toLeft);
            if (!Bad(alt)) rect = alt;
            else if (avoid != null) return null; // ikinci ad (Ocak) yer bulamazsa yazilmaz; hedefin adi her zaman yazilir
        }
        Text(text, rect.Position.X, top, size, c, true, true, true);
        return rect;
    }
}
