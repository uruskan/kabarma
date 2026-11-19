using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// tarim (M2, GDD 8.2-8.3) Godot katmani: tohum secimi (H / DPad sol), yabani bitkiler (yer tutucu: Ocak cevresinde),
/// Korkuluk ve Ari Kovani taramasi (Sim'e konum), gunduz zararlisi isirigi, hasat geri bildirimi, bilgi karti (FarmCard).
/// Durum Sim'de (Farm.Selected, Farm.Seeds); burada statik durum yok.
/// </summary>
public partial class FarmView
{
    Director D => GetParent() as Director;
    FarmCard _card;
    float _scanT;

    void InitTarim()
    {
        Farm.Bitten += p => { View(p)?.OnBitten(); GD.Print($"TARIM bite plot={p.X},{p.Y} crop={p.Crop} damage={p.PestDamage:0.00}"); };
        Farm.Eaten += p => { if (!p.Wild && p.Stage == CropStage.Mature && p.Swollen == null) GD.Print($"TARIM eaten plot={p.X},{p.Y} crop={p.Crop}"); };
        Farm.WildGrown += p =>
        {
            var v = View(p);
            if (v == null) { v = new PlotView(p, this); _views[(p.X, p.Y)] = v; AddChild(v); v.GlobalPosition = CellCenter(p.X, p.Y); }
            v.OnPlanted();
        };
        _card = new FarmCard(this);
        AddChild(_card);
        Callable.From(SpawnWildPlants).CallDeferred();
    }

    // ---------- tohum secimi ----------

    /// <summary>Cantada tohumu olan ekinler, crops.json sirasiyla.</summary>
    public List<CropDef> SeedChoices() => _data.Crops.Crops.Where(c => D?.State.Backpack.Count(c.SeedId) > 0).ToList();

    /// <summary>Ekilecek tohum: secili ekinin tohumu varsa o, yoksa elde olan ilk tohum (secim ona kayar). Yoksa null.</summary>
    public string PlantableSeed()
    {
        var bag = D?.State.Backpack;
        if (bag == null) return null;
        var sel = _data.FindCrop(Farm.Selected);
        if (sel != null && bag.Count(sel.SeedId) > 0) return sel.Id;
        var first = SeedChoices().FirstOrDefault();
        if (first != null) Farm.Selected = first.Id;
        return first?.Id;
    }

    /// <summary>Siradaki tohuma gec (H / DPad sol; bot da bunu cagirir).</summary>
    public void CycleSeed()
    {
        var list = SeedChoices();
        if (list.Count == 0) { D?.Hud.Hint(Loc.T("world.no_seeds"), 1.4f); return; }
        int i = list.FindIndex(c => c.Id == Farm.Selected);
        Farm.Selected = list[(i + 1) % list.Count].Id;
        Sfx.Play("hit", null, -14f);
        _card?.Pulse();
    }

    /// <summary>Belirli ekinin tohumunu sec (bot; oyuncunun H'ye basmasi gibi, tohum yoksa secmez).</summary>
    public bool SelectSeed(string crop)
    {
        var def = _data.FindCrop(crop);
        if (def == null || D?.State.Backpack.Count(def.SeedId) <= 0) return false;
        Farm.Selected = crop;
        _card?.Pulse();
        return true;
    }

    /// <summary>Toprak basligi elde mi (tohum karti ve secim tusu icin).</summary>
    public bool ToprakInHand => D != null && D.Capa.InHand && D.Capa.Current == Capa.Head.Toprak;

    public override void _Process(double delta)
    {
        var d = D;
        if (d == null) return;
        if (Input.IsActionJustPressed("seed_cycle") && ToprakInHand && !d.MenuOpen) CycleSeed();
        _scanT -= (float)delta;
        if (_scanT <= 0) { _scanT = 1f; ScanPlaceables(d); }
    }

    /// <summary>Korkuluk (GDD 8.2) ve Ari Kovani / "pollinator" grubu (GDD 8.3 caprazlama) konumlarini Sim'e yazar.</summary>
    void ScanPlaceables(Director d)
    {
        var scare = new List<(float, float)>();
        var hives = new List<(float, float)>();
        // Korkuluk ve Ari Kovani Capa.Insa'da "prop" olarak kurulur (PenSystem.Props); Furniture'a da bakilir.
        var placed = d.Furniture.Where(f => IsInstanceValid(f) && f.IsInsideTree()).Select(f => (f.Def.Id, (Node3D)f))
            .Concat((d.Pens?.Props ?? (IReadOnlyList<PenProp>)System.Array.Empty<PenProp>()).Where(p => IsInstanceValid(p) && p.IsInsideTree()).Select(p => (p.Def.Id, (Node3D)p)));
        foreach (var (id, node) in placed)
        {
            var p = node.GlobalPosition;
            if (id == "korkuluk") scare.Add((p.X, p.Z));
            else if (id == "ari_kovani") hives.Add((p.X, p.Z));
        }
        foreach (var n in GetTree().GetNodesInGroup("pollinator"))
            if (n is Node3D n3 && IsInstanceValid(n3)) hives.Add((n3.GlobalPosition.X, n3.GlobalPosition.Z));
        Farm.SetScarecrows(scare);
        Farm.SetPollinators(hives);
        ApplyGreenhouses(d);
    }

    HashSet<(int, int)> _seraSet = new();

    /// <summary>
    /// Sera (ICERIK 5 bant kurali, 11): calisan bir Sera Kontrol Unitesinin greenhouse.radius yaricapindaki tarla kareleri
    /// biyom bandindan bagimsiz buyur. Yalniz bu tarama isaretledigi kareleri geri alir (elle isaretlenenlere dokunmaz).
    /// </summary>
    void ApplyGreenhouses(Director d)
    {
        float r = _data.Crops.Genetics.Greenhouse.Radius;
        var units = d.Stations.Where(s => IsInstanceValid(s) && s.IsInsideTree() && s.Def.Station == "sera_kontrol_unitesi" && (s.Queue.PowerOk == null || s.Queue.PowerOk()))
            .Select(s => s.GlobalPosition).ToList();
        var now = new HashSet<(int, int)>();
        if (units.Count > 0)
            foreach (var p in Farm.Plots)
            {
                var c = CellCenter(p.X, p.Y);
                if (units.Any(u => new Vector2(u.X - c.X, u.Z - c.Z).Length() <= r)) now.Add((p.X, p.Y));
            }
        foreach (var k in _seraSet) if (!now.Contains(k)) Farm.SetGreenhouse(k.Item1, k.Item2, false);
        foreach (var k in now) Farm.SetGreenhouse(k.Item1, k.Item2, true);
        _seraSet = now;
    }

    // ---------- yabani bitkiler ----------

    /// <summary>
    /// YER TUTUCU (dunya2'nin Yabani Bahce'si gelene kadar): her ekinden perCrop yabani bitki, Kapsul'den ringMin-ringMax m
    /// halkada, suda ve agac/kaya dibinde degil. Kayittan yuklenmis tarlada yabani varsa yeniden kurulmaz.
    /// </summary>
    void SpawnWildPlants()
    {
        var d = D;
        if (d == null || Farm.WildPlots.Any()) return;
        var w = _data.Crops.Genetics.Wild;
        var rng = new Rng(d.State.Seed ^ 0x7A12_11D5UL);
        var center = d.Pod?.GlobalPosition ?? Vector3.Zero;
        var trees = _world.TreePositions.ToList();
        int n = 0;
        foreach (var c in _data.Crops.Crops)
            for (int k = 0; k < w.PerCrop; k++)
                for (int tries = 0; tries < 24; tries++)
                {
                    float a = rng.Range(0f, Mathf.Tau), r = rng.Range(w.RingMin, w.RingMax);
                    var pos = center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    var g = _world.Ground(pos);
                    if (_world.IsWater(g) || trees.Any(t => new Vector2(t.X - g.X, t.Z - g.Z).Length() < w.Clearance)
                        || _world.Tufts.Any(t => t.Left > 0 && new Vector2(t.Position.X - g.X, t.Position.Z - g.Z).Length() < w.Clearance)) continue;
                    var (x, y) = Cell(g);
                    if (Farm.SpawnWild(x, y, c.Id, rng) != null) { n++; _world.ClearGrass(CellCenter(x, y), 0.6f); break; }
                }
        GD.Print($"TARIM wild={n}");
    }

    /// <summary>En yakin olgun yabani bitki (bot ve pusula icin).</summary>
    public PlotView NearestWild(Vector3 from, string crop = null) => _views.Values
        .Where(v => v.Plot.Wild && v.Plot.Planted && (crop == null || v.Plot.Crop == crop))
        .OrderBy(v => v.GlobalPosition.DistanceSquaredTo(from)).FirstOrDefault();

    // ---------- hasat geri bildirimi ----------

    /// <summary>Bicme hasadi (Capa.Bicme): yazi, ucan parca, Alaca ve caprazlama haberi.</summary>
    public void OnHarvestedBy(HarvestResult h, Vector3 at)
    {
        var d = D;
        var color = CropVisuals.ProduceColor(h.Crop);
        string name = ItemNames.Of(h.Item);
        Fx.Text3D(at + Vector3.Up * 1.0f, $"+{h.Yield} {name} {new string('★', h.Stars)}", Palette.Warning, 48, 1.2f, 1.3f);
        Fx.Text3D(at + Vector3.Up * 0.55f, Loc.T("world.seeds_gain", h.Seeds) + (h.Crossed ? " · " + Loc.T("world.crossed") : ""), Palette.OffWhite, 34, 1.0f, 1.2f);
        if (d != null)
            for (int i = 0; i < Mathf.Min(h.Yield, 4); i++)
                Fx.FlyTo(at + Vector3.Up * 0.3f, () => d.Player.BackpackPoint.GlobalPosition, color, 0.12f);
        if (h.Alaca != null)
        {
            var a = _data.Crops.Genetics.AlacaOf(h.Alaca);
            d?.Hud.Hint(Loc.T("world.alaca_found", name, a?.PriceMult), 2.5f);
            Fx.Burst(at + Vector3.Up * 0.6f, CropVisuals.AlacaTint(h.Alaca), 30, 6f, 0.12f, 0.9f);
        Vfx.Play("farm_alaca", at + Vector3.Up * 0.5f, null, CropVisuals.AlacaTint(h.Alaca));
            Sfx.Play("swell", at, -2f);
        }
        GD.Print($"TARIM harvest crop={h.Crop} item={h.Item} yield={h.Yield} seeds={h.Seeds} wild={h.Wild} crossed={h.Crossed}");
    }
}
