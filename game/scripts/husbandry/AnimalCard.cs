using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Hayvan bilgi karti ve agil paneli (GDD 9.2-9.4), Rust tarzi koyu paneller (UiTheme).
/// - Kart: oyuncunun 4 m icindeki en yakin hayvan. Tur, yabani/evcil, guven ya da mutluluk cubugu, ihtiyaclar, genler,
///   rol ve komut, ustunde bekleyen urun.
/// - Agil paneli: hayvanin panelini (E, RanchSystem.OpenPanel) acinca sol sutunda. Agilin ihtiyaclari, alani ve icindeki
///   her hayvanin satiri.
/// </summary>
public partial class AnimalCard : Control
{
    readonly Director _d;
    readonly RanchSystem _r;
    HusbandryAgent _target;

    public AnimalCard(Director d, RanchSystem r)
    {
        _d = d; _r = r;
        Name = "AnimalCard";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Kartta gosterilen hayvan (sinama).</summary>
    public HusbandryAgent Target => _target;
    /// <summary>Sinama goruntusu: karti bu hayvana sabitler (null: en yakin).</summary>
    public HusbandryAgent Pin { get; set; }

    public override void _Process(double delta)
    {
        var p = _d.Player.GlobalPosition;
        if (Pin != null && IsInstanceValid(Pin)) { _target = Pin; QueueRedraw(); return; }
        _target = _r.Agents.Where(a => IsInstanceValid(a) && IsInstanceValid(a.Host) && a.Host.Visible && a.Host.GlobalPosition.DistanceTo(p) < 4f)
            .OrderBy(a => a.Host.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        QueueRedraw();
    }

    static string Mark(bool ok) => ok ? "✓" : "✗";

    /// <summary>Ihtiyaclar, mutluluk, genler: kart ve panel altligi icin tek satirlik ozet.</summary>
    public static string Summary(RanchSystem r, Director d, HusbandryAgent ag)
    {
        var a = ag.Animal;
        var pen = r.Ranch.PenOf(a, d.Pens.Pens);
        var n = r.Ranch.NeedsOf(a, pen, pen != null && r.RoofOver(pen));
        string needs = a.HiveId != 0 ? "Kovanda" : pen == null ? "Ağılda değil" : $"Yem {Mark(n.Food)}  Su {Mark(n.Water)}  Çatı {Mark(n.Roof)}  Alan {Mark(n.Space)}  Fırça {Mark(n.Brush)}  Gübre {n.Manure}";
        return $"♥ {a.Happiness:0}  ★{r.Ranch.Rules.Tier(a.Happiness).Stars}  ·  B{a.Genes.B} U{a.Genes.U} V{a.Genes.V} K{a.Genes.K}  ·  {needs}";
    }

    public override void _Draw()
    {
        if (_d.Hud.MenuOpen)
        {
            if (_r.PanelAgent != null && IsInstanceValid(_r.PanelAgent)) DrawPenPanel(_r.PanelAgent);
            return;
        }
        if (_target != null) DrawCard(_target);
    }

    // ---------- cizim yardimcilari ----------

    float S(float v) => T.Pxf(v);

    void Txt(string s, float x, float y, float size, Color c, bool bold = false, bool cond = false, float w = -1)
    {
        var f = T.Font(size, bold, cond);
        DrawString(f, new Vector2(x, y + S(size)), s, HorizontalAlignment.Left, w < 0 ? -1 : w, T.Px(size), c);
    }

    void Bar(float x, float y, float w, float h, float frac, Color fill)
    {
        DrawRect(new Rect2(x, y, w, h), T.A(T.Ink, 0.12f));
        DrawRect(new Rect2(x, y, w * Mathf.Clamp(frac, 0, 1), h), fill);
    }

    void DrawCard(HusbandryAgent ag)
    {
        var a = ag.Animal; var s = ag.Species; var rules = _r.Ranch.Rules;
        float w = S(340), x = Size.X - w - S(24), y = S(150), pad = S(14), line = S(24);
        bool tame = a.Tame;
        int rows = tame ? 6 : 3;
        float h = pad * 2 + S(34) + rows * line;
        DrawRect(new Rect2(x, y, w, h), new Color(Palette.NightDeep, 0.85f)); // okunurluk: parlak cimenin ustunde koyu taban
        DrawRect(new Rect2(x, y, w, h), T.Panel);
        DrawRect(new Rect2(x, y, S(4), h), tame ? Palette.CoopBlue : Palette.Warning);
        float cx = x + pad + S(4), cy = y + pad;
        Txt(T.Upper(ag.Title), cx, cy, 24, T.Ink, true, true);
        Txt(tame ? (a.Registered ? "EVCİL · KAYITLI" : "EVCİL · İZLİYOR") : "YABANİ", x + w - S(150), cy + S(4), 15, T.Muted, true, true, S(136));
        cy += S(34);
        if (!tame)
        {
            Txt($"Güven  {a.Trust * 100:0}%", cx, cy, 17, T.Ink); Bar(cx + S(130), cy + S(6), w - S(170), S(10), a.Trust, Palette.Accent); cy += line;
            string how = s.Tame switch { "egg" => "Yuvadan yumurta, Kuluçka'da çıkar", "subdue" => a.Bound ? $"Tutuldu: {ItemNames.Of(s.Food)} ver" : "Canı %25 altında Çekim ile tut", _ => $"Sevdiği yem: {ItemNames.Of(s.Food)}" };
            Txt(how, cx, cy, 16, T.Muted); cy += line;
            Txt($"B{a.Genes.B}  U{a.Genes.U}  V{a.Genes.V}  K{a.Genes.K}", cx, cy, 16, T.Muted);
            return;
        }
        var tier = rules.Tier(a.Happiness);
        Txt($"Mutluluk  {a.Happiness:0}", cx, cy, 17, T.Ink); Bar(cx + S(130), cy + S(6), w - S(200), S(10), a.Happiness / 100f, a.Happiness >= rules.Def.Breeding.MinHappiness ? T.Green : a.Happiness < rules.Def.Escape.Below ? T.Red : Palette.Warning);
        Txt(new string('★', tier.Stars), x + w - S(62), cy, 17, Palette.Warning, true); cy += line;
        var pen = _r.Ranch.PenOf(a, _d.Pens.Pens);
        var n = _r.Ranch.NeedsOf(a, pen, pen != null && _r.RoofOver(pen));
        Txt(a.HiveId != 0 ? "Kovanda" : pen == null ? "Ağılda değil" : $"Yem {Mark(n.Food)}  Su {Mark(n.Water)}  Çatı {Mark(n.Roof)}  Alan {Mark(n.Space)}  Fırça {Mark(n.Brush)}  Okşama {Mark(n.Petted)}", cx, cy, 16, T.Ink); cy += line;
        Txt($"Gen  B{a.Genes.B}  U{a.Genes.U}  V{a.Genes.V}  K{a.Genes.K}{(a.Alaca == "" ? "" : "  · Alaca: " + a.Alaca)}", cx, cy, 16, a.Alaca == "" ? T.Ink : Palette.Warning); cy += line;
        var roleName = rules.Def.Roles.FirstOrDefault(r => r.Id == a.Role)?.Name ?? a.Role;
        var cmdName = rules.Def.Commands.FirstOrDefault(c => c.Id == a.Command)?.Name ?? a.Command;
        Txt($"Rol {roleName}  ·  Komut {cmdName}", cx, cy, 16, T.Muted); cy += line;
        string pend = a.Pending.Count == 0 ? "Ürün yok" : string.Join("  ", a.Pending.Select(p => $"{p.Count} {ItemNames.Of(p.Item)} {new string('★', p.Stars)}"));
        Txt(pend, cx, cy, 16, a.Pending.Count > 0 ? T.Green : T.Muted); cy += line;
        if (a.WantsOut) Txt("Mutsuz: açık kapıdan kaçar", cx, cy, 16, T.Red);
        else if (a.BreedTimer >= 0) Txt($"Yavru yolda: {a.BreedTimer / rules.DaySeconds:0.0} gün", cx, cy, 16, T.Green);
    }

    void DrawPenPanel(HusbandryAgent ag)
    {
        var rules = _r.Ranch.Rules;
        var pen = _r.Ranch.PenOf(ag.Animal, _d.Pens.Pens);
        float w = S(420), x = S(24), y = S(110), pad = S(14), line = S(26);
        var members = pen == null ? new[] { ag.Animal } : _r.Ranch.Animals.Where(a => a.Registered && a.HiveId == 0 && pen.Contains(new P2(a.X, a.Z))).ToArray();
        float h = pad * 2 + S(36) + line * (3 + members.Length);
        DrawRect(new Rect2(x, y, w, h), new Color(Palette.NightDeep, 0.85f)); // okunurluk: parlak cimenin ustunde koyu taban
        DrawRect(new Rect2(x, y, w, h), T.Panel);
        float cx = x + pad, cy = y + pad;
        Txt(T.Upper(pen == null ? "Ağılda değil" : $"Ağıl · {pen.Cells(_d.Data.Pen.CellArea)} hücre"), cx, cy, 24, T.Ink, true, true);
        cy += S(36);
        if (pen != null)
        {
            float used = _r.Ranch.UsedArea(pen);
            Txt($"Alan {used:0}/{pen.Area:0} m²", cx, cy, 17, T.Ink); Bar(cx + S(150), cy + S(7), w - S(180), S(10), used / pen.Area, used <= pen.Area ? T.Green : T.Red); cy += line;
            var feeders = _r.Ranch.Feeders.Where(f => pen.Contains(new P2(f.X, f.Z))).ToList();
            string feed = feeders.Count == 0 ? "Yemlik yok" : string.Join("  ", feeders.Select(f => f.Amount == 0 ? "Yemlik boş" : $"{ItemNames.Of(f.Food)} {f.Amount}"));
            Txt(feed, cx, cy, 16, T.Muted); cy += line;
            int manure = _r.Ranch.ManureIn(pen);
            bool water = _r.Ranch.Troughs.Any(t => t.Water > 0 && pen.Contains(new P2(t.X, t.Z)));
            Txt($"Su {Mark(water)}   Gübre {manure}   Fırça {Mark(_r.Ranch.Brushes.Any(b => pen.Contains(new P2(b.X, b.Z))))}", cx, cy, 16, T.Muted); cy += line;
        }
        foreach (var a in members)
        {
            bool sel = a == ag.Animal;
            if (sel) DrawRect(new Rect2(x + S(4), cy - S(2), w - S(8), line), T.Selected);
            var s = rules.Species(a.Species);
            var role = rules.Def.Roles.FirstOrDefault(r => r.Id == a.Role)?.Name ?? a.Role;
            Txt($"{s.Name}{(a.Adult(rules.Def.Breeding.AdultDays) ? "" : " (yavru)")}", cx, cy, 17, T.Ink, true);
            Txt($"♥{a.Happiness:0}  B{a.Genes.B} U{a.Genes.U} V{a.Genes.V} K{a.Genes.K}  {role}", cx + S(130), cy, 16, T.Muted);
            cy += line;
        }
    }
}
