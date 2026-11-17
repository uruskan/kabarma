using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Pano, gunduz (SANAT-SES 8.1; URETIM 6 M1 karar 11): ust ortada pusula (Ocak'i ve etkin hedefi isaretler), altinda
/// etkin adimin tek satiri ve onun altinda Defter takibi. Sol altta can ve dayaniklilik (dayaniklilik cubugu
/// oyuncu akisinin Hud.Stamina.cs'i; yeri <see cref="StaminaSlot"/>).
/// </summary>
public partial class Hud
{
    CompassBar _compass;
    VBoxContainer _topCenter;
    Label _stepLine, _defterLine;

    /// <summary>Dayaniklilik cubugunun yeri: sol altta, can cubugunun hemen altinda (oyuncu akisi doldurur).</summary>
    public VBoxContainer StaminaSlot { get; private set; }

    /// <summary>Hud._Ready'den bir kez: gecehud panolari (pusula, adim satiri, gece, envanter, Defter, rapor).</summary>
    void BuildGeceHud(Control root)
    {
        // Eski sag ust Defter kutusu ve can altindaki canta satiri emekli: Defter pusulanin altina, Kredi envantere tasindi.
        _goalPanel.Visible = false;
        _bag.Visible = false;

        StaminaSlot = new VBoxContainer { Name = "StaminaSlot" };
        _bottomLeft.AddChild(StaminaSlot);
        _bottomLeft.MoveChild(StaminaSlot, _hp.GetIndex() + 1);

        _topCenter = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        _topCenter.AddThemeConstantOverride("separation", 2);
        _compass = new CompassBar { CustomMinimumSize = new Vector2(480, 48), Font = _bold, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _topCenter.AddChild(_compass);
        _stepLine = L("", 21, Palette.OffWhite, true);
        _stepLine.HorizontalAlignment = HorizontalAlignment.Center;
        _stepLine.AddThemeConstantOverride("outline_size", 9);
        _stepLine.CustomMinimumSize = new Vector2(520, 0);
        _topCenter.AddChild(_stepLine);
        _defterLine = L("", 16, Palette.Fog, true);
        _defterLine.HorizontalAlignment = HorizontalAlignment.Center;
        _defterLine.AddThemeConstantOverride("outline_size", 7);
        _topCenter.AddChild(_defterLine);
        root.AddChild(_topCenter);

        BuildNightHud(root);   // Hud.Night.cs
        BuildInventory(root);  // Hud.Inventory.cs (Canta ve Defter sayfalari)
    }

    /// <summary>Hud._Process'ten her kare.</summary>
    void UpdateGeceHud(float dt)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        _topCenter.Position = new Vector2((vp.X - _topCenter.Size.X) / 2f, 12);
        bool modal = _report.Visible || InventoryOpen;
        _topCenter.Visible = !modal;
        // Ipucu satiri ortali, takili baslik kutusunun altinda (eski yeri kutunun ustune biniyordu).
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.Size = new Vector2(vp.X, _hint.Size.Y);
        _hint.Position = new Vector2(0, vp.Y / 2f + 112);
        UpdateCompass();
        UpdateStepLines();
        UpdateKabarmaHud(vp);  // Hud.Night.cs
        if (InventoryOpen) UpdateInventory(vp, dt); // Hud.Inventory.cs
    }

    void UpdateCompass()
    {
        var p = _d.Player;
        var fwd = -p.Cam.GlobalBasis.Z;
        _compass.Heading = Mathf.Atan2(fwd.X, -fwd.Z);
        _compass.FovRadians = Mathf.DegToRad(_d.Data.Hud.CompassFovDegrees);
        _compass.Origin = p.GlobalPosition;
        _compass.Home = _d.Ocak != null ? _d.Ocak.GlobalPosition : null;
        _compass.Target = _d.CompassTarget(out var name);
        _compass.TargetName = name;
        _compass.QueueRedraw();
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
            _stepLine.Text = GoalText(step.Goal) + (step.Target > 1 ? $"   {s.Scenario.Progress}/{step.Target}" : "");
            _defterLine.Text = s.Orders.DefterOpen && orderLine != "" ? "DEFTER · " + orderLine : "";
        }
        else
        {
            _stepLine.Text = orderLine != "" ? orderLine : "Defter: " + s.Orders.OrdersTotal + " iş emri";
            _defterLine.Text = "";
        }
        _defterLine.Visible = _defterLine.Text != "";
    }

    static readonly System.Text.RegularExpressions.Regex ActToken = new(@"\{act:([a-z0-9_]+)\}");

    /// <summary>
    /// Adim metni ekranda. ilkgun/girdi dallari adim metnine {act:eylem} yer tutucusu koyar; birlesince bu tek satir
    /// onlarin genisleticisine (ActText.Plain / Controls.Expand) baglanir. O zamana dek yer tutucu cig gorunmesin: [eylem].
    /// </summary>
    string GoalText(string goal) => G(goal); // entegrasyon: girdi akisinin genisleticisi (Hud.Controls.cs)
}

/// <summary>Ust ortadaki yatay pusula serit: K/D/G/B, Ocak ve etkin hedef isaretleri (SANAT-SES 8.1).</summary>
public partial class CompassBar : Control
{
    public float Heading, FovRadians = Mathf.DegToRad(150);
    public Vector3 Origin;
    public Vector3? Home, Target;
    public string TargetName = "";
    public Font Font;

    static readonly (string label, float angle)[] Cardinals =
    {
        ("K", 0f), ("KD", Mathf.Pi * 0.25f), ("D", Mathf.Pi * 0.5f), ("GD", Mathf.Pi * 0.75f),
        ("G", Mathf.Pi), ("GB", -Mathf.Pi * 0.75f), ("B", -Mathf.Pi * 0.5f), ("KB", -Mathf.Pi * 0.25f),
    };

    public CompassBar() { MouseFilter = MouseFilterEnum.Ignore; }

    float X(float bearing, out bool inside)
    {
        float rel = Mathf.Wrap(bearing - Heading, -Mathf.Pi, Mathf.Pi);
        float half = FovRadians / 2f;
        inside = Mathf.Abs(rel) <= half;
        return Size.X / 2f + Mathf.Clamp(rel / half, -1f, 1f) * (Size.X / 2f - 14f);
    }

    float Bearing(Vector3 to) { var d = to - Origin; return Mathf.Atan2(d.X, -d.Z); }

    const float Band = 30f; // ust serit: kademe ve yonler; altinda isaretler

    public override void _Draw()
    {
        var font = Font ?? ThemeDB.FallbackFont;
        float w = Size.X;
        DrawRect(new Rect2(0, 0, w, Band), new Color(Palette.NightDeep, 0.55f));
        DrawColoredPolygon(new[] { new Vector2(w / 2f - 6, 0), new Vector2(w / 2f + 6, 0), new Vector2(w / 2f, 7) }, Palette.Warning);
        // Kademe cizgileri: 15 derecede bir
        for (int deg = 0; deg < 360; deg += 15)
        {
            float x = X(Mathf.DegToRad(deg), out bool inside);
            if (!inside) continue;
            bool major = deg % 45 == 0;
            DrawLine(new Vector2(x, major ? 19 : 23), new Vector2(x, Band - 2), new Color(Palette.OffWhite, major ? 0.7f : 0.35f), 1.5f);
        }
        foreach (var (label, a) in Cardinals)
        {
            float x = X(a, out bool inside);
            if (!inside) continue;
            int size = label.Length == 1 ? 17 : 12;
            var col = label == "K" ? Palette.Accent : Palette.OffWhite;
            var sz = font.GetStringSize(label, HorizontalAlignment.Left, -1, size);
            DrawString(font, new Vector2(x - sz.X / 2f, 17), label, HorizontalAlignment.Left, -1, size, col);
        }
        // Hedef once (adi saginda); Ocak'in adi solunda, hedefin adina binerse saga, orada da binerse yazilmaz.
        // Iki isaret ayni yondeyse adlar ikisinin de disina yazilir (isaret ada binmesin).
        float? xh = Home is Vector3 hp ? X(Bearing(hp), out _) : null;
        float? xt = Target is Vector3 tp ? X(Bearing(tp), out _) : null;
        Rect2? taken = null;
        if (Target is Vector3 t) taken = Marker(font, t, Palette.MayaTeal, TargetName, false, leftLabel: false, null, xh);
        if (Home is Vector3 home) Marker(font, home, Palette.Warning, "Ocak", true, leftLabel: true, taken, xt);
    }

    /// <summary>Isareti ve adini cizer; adin kapladigi alani dondurur (avoid ile cakisirsa ad yazilmaz).</summary>
    Rect2? Marker(Font font, Vector3 at, Color c, string name, bool diamond, bool leftLabel, Rect2? avoid, float? otherX)
    {
        float x = X(Bearing(at), out bool inside);
        float ox = otherX is float o && Mathf.Abs(o - x) < 18f ? o : x; // yakindaki obur isaret
        var flat = at - Origin; flat.Y = 0;
        int dist = Mathf.RoundToInt(flat.Length());
        float top = Band + 1;
        if (diamond)
            DrawColoredPolygon(new[] { new Vector2(x, top), new Vector2(x + 6, top + 7), new Vector2(x, top + 14), new Vector2(x - 6, top + 7) }, c);
        else
            DrawColoredPolygon(new[] { new Vector2(x - 7, top + 13), new Vector2(x + 7, top + 13), new Vector2(x, top) }, c);
        if (!inside)
        {
            // Gorus acisinin disinda: kenarda yon oku
            float dir = x < Size.X / 2f ? -1f : 1f;
            DrawColoredPolygon(new[] { new Vector2(x + dir * 14, top + 7), new Vector2(x + dir * 8, top + 2), new Vector2(x + dir * 8, top + 12) }, c);
        }
        string text = string.IsNullOrEmpty(name) ? $"{dist} m" : $"{name} · {dist} m";
        var sz = font.GetStringSize(text, HorizontalAlignment.Left, -1, 12);
        float lo = Mathf.Min(x, ox), hi = Mathf.Max(x, ox);
        bool left = leftLabel ? lo - 12 - sz.X >= 0 : hi + 12 + sz.X > Size.X;
        Rect2 At(bool l) => new(l ? lo - 12 - sz.X : hi + 12, top, sz.X, 14);
        // Ad ne obur adin ne de obur isaretin ustune binsin: once tercih edilen yan, sonra obur yan.
        Rect2? icon = otherX is float oi ? new Rect2(oi - 8, top, 16, 14) : null;
        bool Bad(Rect2 r) => r.Position.X < 0 || r.End.X > Size.X
            || (avoid is Rect2 a && r.Grow(4).Intersects(a)) || (icon is Rect2 ic && r.Grow(2).Intersects(ic));
        Rect2 rect = At(left);
        if (Bad(rect))
        {
            var alt = At(!left);
            if (!Bad(alt)) rect = alt;
            else if (avoid != null) return null; // ikinci ad (Ocak) yer bulamazsa yazilmaz; hedefin adi her zaman yazilir
        }
        DrawString(font, new Vector2(rect.Position.X + 1, top + 13), text, HorizontalAlignment.Left, -1, 12, new Color(Palette.NightDeep, 0.85f));
        DrawString(font, new Vector2(rect.Position.X, top + 12), text, HorizontalAlignment.Left, -1, 12, c);
        return rect;
    }
}
