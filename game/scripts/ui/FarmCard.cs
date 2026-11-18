using System.Linq;
using Godot;
using Kabarma.Sim;
using T = Kabarma.Game.UiTheme;

namespace Kabarma.Game;

/// <summary>
/// Tohum ve ekin bilgi karti (tarim M2, GDD 8.3), Rust tarzi panel dilinde (M1 karari 20):
/// Toprak basligi eldeyken secili tohum (adet, soy, gen cubuklari V/H/D/M, Alaca), nisan bir ekindeyken o ekin
/// (evre, buyume, Nem, Bereket, genler, Alaca, koruma ve Zarar). Icerik degisince yeniden kurulur.
/// </summary>
public partial class FarmCard : CanvasLayer
{
    readonly FarmView _fv;
    PanelContainer _panel;
    VBoxContainer _box;
    string _sig = "";
    float _pulse;

    static readonly string[] GeneNames = { "Verim", "Hız", "Dayanıklılık", "Maya" };

    public FarmCard(FarmView fv) { _fv = fv; Layer = 4; }

    public override void _Ready()
    {
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AnchorLeft = _panel.AnchorRight = 1f;
        _panel.AnchorTop = _panel.AnchorBottom = 0.5f;
        // Defter ile ayni koyu diyalog zemini, sabit opaklik (T.Panel yari saydam: oyun icinde metin okunmuyordu)
        var sb = new StyleBoxFlat { BgColor = new Color(T.Dialog.R, T.Dialog.G, T.Dialog.B, 0.9f), ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 };
        sb.BorderColor = T.Ivory; sb.BorderWidthLeft = 3;
        _panel.AddThemeStyleboxOverride("panel", sb);
        _box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _box.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(_box);
        AddChild(_panel);
        _panel.Visible = false;
    }

    public void Pulse() { _pulse = 0.35f; _sig = ""; }

    public override void _Process(double delta)
    {
        var d = _fv.GetParent() as Director;
        if (d == null || _panel == null) return;
        _pulse = Mathf.Max(0, _pulse - (float)delta);
        bool hidden = d.MenuOpen || d.Hud.ReportOpen;
        Plot aimed = null;
        if (!hidden && d.Capa.InHand)
        {
            var aim = d.Player.AimTarget;
            if (aim.DistanceTo(d.Player.GlobalPosition) < 9f)
            {
                var (x, y) = FarmView.Cell(aim);
                var p = d.State.Farm.Get(x, y);
                if (p != null && p.Planted) aimed = p;
            }
        }
        bool seedCard = !hidden && aimed == null && _fv.ToprakInHand;
        _panel.Visible = aimed != null || seedCard;
        if (!_panel.Visible) { _sig = ""; return; }

        string sig = aimed != null
            ? $"p{aimed.X},{aimed.Y}|{aimed.Crop}|{(int)(aimed.Growth * 20)}|{(int)aimed.Moisture / 5}|{(int)aimed.Fertility}|{aimed.Genes}|{aimed.Alaca}|{aimed.Protected}|{(int)(aimed.PestDamage * 20)}|{aimed.Swollen?.Id}"
            : $"s{d.State.Farm.Selected}|{d.State.Backpack.Count(d.Data.FindCrop(d.State.Farm.Selected)?.SeedId ?? "")}|{d.State.Farm.Seeds.Lots.Count}|{SeedChoicesSig(d)}";
        _panel.Modulate = new Color(1, 1, 1, 1) * (1f + _pulse * 0.6f);
        if (sig == _sig) return;
        _sig = sig;
        foreach (var c in _box.GetChildren()) { _box.RemoveChild(c); c.QueueFree(); }
        if (aimed != null) BuildCrop(d, aimed); else BuildSeed(d);
        _panel.ResetSize();
        var sz = _panel.GetCombinedMinimumSize();
        _panel.OffsetLeft = -sz.X - T.Px(24); _panel.OffsetRight = -T.Px(24);
        _panel.OffsetTop = -sz.Y * 0.5f + T.Px(60); _panel.OffsetBottom = sz.Y * 0.5f + T.Px(60);
    }

    string SeedChoicesSig(Director d) => string.Join(",", _fv.SeedChoices().Select(c => c.Id));

    void BuildSeed(Director d)
    {
        var f = d.State.Farm;
        var choices = _fv.SeedChoices();
        var def = d.Data.FindCrop(f.Selected);
        if (choices.Count == 0 || def == null || d.State.Backpack.Count(def.SeedId) <= 0)
        {
            Head("TOHUM YOK", "");
            Line("Yabani bitkiyi Biçme ile topla: ürün ve tohum verir.", T.Muted);
            return;
        }
        int n = d.State.Backpack.Count(def.SeedId);
        var best = f.Seeds.Best(def.Id, d.Data.Crops.Genetics);
        int recorded = f.Seeds.CountFor(def.Id);
        Head($"TOHUM · {T.Upper(def.Name)}", $"×{n}");
        Line($"{FamilyName(def.Family)} · {def.Days:0.#} gün · su: {def.Water}" + (def.Perennial ? " · kalıcı" : "") + (def.Dark ? " · karanlıkta" : ""), T.Muted);
        var g = best?.Genes ?? def.SeedGenes;
        Line(best != null ? $"Sıradaki: kayıtlı soy ({recorded}/{n})" : "Sıradaki: kayıtsız tohum (taban genleri)", T.Ink);
        Genes(g);
        if (best?.Alaca != null) Alaca(d, best.Alaca, "Alaca tohum: %50 Alaca verir");
        Line($"{d.Hud.G("{act:seed_cycle}")} sıradaki tohum  ·  {choices.Count} tür elde", T.Teal);
    }

    void BuildCrop(Director d, Plot p)
    {
        var def = d.Data.FindCrop(p.Crop);
        if (def == null) return;
        string stage = p.Stage switch { CropStage.Seed => "Tohum", CropStage.Sprout => "Filiz", CropStage.Young => "Genç", _ => "Olgun" };
        Head($"{T.Upper(def.Name)}" + (p.Wild ? " · YABANİ" : ""), p.Swollen != null ? $"KABARIK {p.Swollen.Id}" : stage.ToUpper());
        if (!p.Wild)
        {
            Line($"Büyüme %{p.Growth * 100:0}  ·  Nem {p.Moisture:0}  ·  Bereket {p.Fertility:0}", T.Ink);
            if (p.Moisture < d.Data.Crops.Moisture.HalfBelow) Line("Susuz: Sulama ile sula", T.Warning);
            if (p.RotationBonus) Line("Nöbet: art arda farklı aile, verim +%10", T.Green);
        }
        else Line("Biç: ürün ve yabani tohum (genler 1–3)", T.Muted);
        Genes(p.Genes);
        if (p.Alaca != null) Alaca(d, p.Alaca, "satışta ×" + d.Data.Crops.Genetics.AlacaOf(p.Alaca)?.PriceMult);
        if (!p.Wild)
        {
            if (p.Protected) Line("Korunuyor (Korkuluk ya da Sarımsak)", T.Green);
            else if (p.Stage == CropStage.Mature) Line("Korumasız: gündüz zararlısı gelebilir", T.Warning);
            if (p.PestDamage > 0) Line($"Zarar %{p.PestDamage * 100:0} (hasat ×{1 - p.PestDamage:0.00})", T.Red);
        }
    }

    void Head(string title, string right)
    {
        var h = new HBoxContainer();
        var l = Label(title, 20, T.Ink, true);
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        h.AddChild(l);
        if (right != "") h.AddChild(Label(right, 18, T.Ivory, true));
        _box.AddChild(h);
    }

    void Line(string text, Color c) => _box.AddChild(Label(text, 15, c, false));

    void Genes(Genes g)
    {
        for (int i = 0; i < Kabarma.Sim.Genes.Count; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", T.Px(3));
            var name = Label($"{Kabarma.Sim.Genes.Letters[i]} {GeneNames[i]}", 14, T.Muted, true);
            name.CustomMinimumSize = new Vector2(T.Px(118), 0);
            row.AddChild(name);
            for (int k = 1; k <= 10; k++)
            {
                var cell = new Panel { CustomMinimumSize = new Vector2(T.Px(13), T.Px(10)), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
                var sb = new StyleBoxFlat { BgColor = k <= g[i] ? (g[i] >= 7 ? T.Green : T.Ivory) : new Color(1f, 1f, 1f, 0.12f) };
                cell.AddThemeStyleboxOverride("panel", sb);
                row.AddChild(cell);
            }
            row.AddChild(Label($" {g[i]}", 14, T.Ink, true));
            _box.AddChild(row);
        }
    }

    void Alaca(Director d, string id, string tail)
    {
        var a = d.Data.Crops.Genetics.AlacaOf(id);
        var l = Label($"ALACA · {T.Upper(a?.Name ?? id)} · {tail}", 15, CropVisuals.AlacaTint(id).Lerp(T.Star, 0.4f), true);
        _box.AddChild(l);
    }

    static Label Label(string text, int size, Color c, bool bold)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", T.Font(size, bold, bold));
        l.AddThemeFontSizeOverride("font_size", T.Px(size));
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    public static string FamilyName(string f) => f switch
    {
        "root" => "Kök", "grain" => "Tahıl", "gourd" => "Kabakgil", "oil" => "Yağ", "fiber" => "Lif", "fruit" => "Meyve",
        "mushroom" => "Mantar", "spice" => "Baharat", "flower" => "Çiçek", _ => f,
    };
}
