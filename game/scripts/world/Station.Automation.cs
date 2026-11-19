using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Istasyon islevleri (M4 tasima; GDD 11.4, ICERIK §11, §12): Hasat Robotu, Ekim Robotu, Otomatik Yemlik, Yumurta Toplayici,
/// Sagim-Kirkim Kolu, Paketleyici, Sondaj Kulesi, Sera Kontrol Unitesi ve Montaj Makinesi. Kurallar Sim'de
/// (AutomationRules, automation.json); burada her makinenin zamanlayicisi, gucu ve dunyadan girdisi. Makine hedefi varken guc
/// ister (PowerSystem.SyncStations: WantsPower), beslenmisse tur basina calisir. Girdi ve cikti makinenin yanindaki
/// Sandiklardan (canta degil). Yalniz host'ta ve tek oyuncuda islenir.
/// </summary>
public partial class Station
{
    float _autoT, _idleT;
    bool _autoWork;
    readonly HashSet<(int, int)> _sera = new();
    string _autoNote = "";

    /// <summary>Makinenin hedefi var: guc ister (is yokken talep yok).</summary>
    public bool WantsPower => _autoWork;

    MachineDef Machine => _d.Data.Automation.Machines.TryGetValue(Def.Station ?? "", out var m) ? m : null;

    List<Inventory> Chests(float r) => _d.Furniture
        .Where(f => IsInstanceValid(f) && f.Storage != null && new Vector2(f.GlobalPosition.X - GlobalPosition.X, f.GlobalPosition.Z - GlobalPosition.Z).Length() <= r)
        .OrderBy(f => f.GlobalPosition.DistanceSquaredTo(GlobalPosition)).Select(f => f.Storage).ToList();

    string AutoTail() => Machine == null || Def.Station == "montaj_makinesi" ? "" : _autoWork ? "\n" + _autoNote : "\n" + Loc.T("world.auto_no_target");

    void TickAuto(float dt)
    {
        var m = Machine;
        if (m == null || _d.NetClient) return;
        string id = Def.Station;
        if (id == "montaj_makinesi") { TickAssembler(); return; }
        _autoT += dt;
        if (_autoT < m.Interval) return;
        _autoT = 0;
        var farm = _d.State.Farm;
        float x = GlobalPosition.X, z = GlobalPosition.Z;
        var chests = Chests(m.ChestRadius);
        bool powered = _d.Power?.PortOf(this)?.Powered == true;
        bool target;
        switch (id)
        {
            case "hasat_robotu": case "ekim_robotu": case "sera_kontrol_unitesi":
                target = farm.Plots.Any(p => new Vector2(p.X + 0.5f - x, p.Y + 0.5f - z).Length() <= m.Radius);
                break;
            case "otomatik_yemlik": target = _d.Pens.Props.Any(p => p.Feeder != null && p.Feeder.Room > 0 && Near(p, m.Radius)); break;
            case "yumurta_toplayici": target = _d.Pens.Props.Any(p => p.Nest != null && p.Nest.Eggs > 0 && Near(p, m.Radius)); break;
            case "sagim_kirkim_kolu":
                target = _d.State.Ranch.Animals.Any(a => a.Tame && new Vector2(a.X - x, a.Z - z).Length() <= m.Radius && a.Pending.Any(s => m.Items.Contains(s.Item)));
                break;
            case "paketleyici":
                target = chests.Any(c => c.Contents.Any(s => _d.State.Cargo.Pricing.Sellable(s.id))) || _d.State.Cargo.Open != null || _d.State.Cargo.Pad.Count > 0;
                break;
            case "sondaj_kulesi": target = DrillPoi(m) != null; break;
            default: target = false; break;
        }
        _autoWork = target;
        if (id != "sera_kontrol_unitesi" && !target) return;
        if (!powered && id != "sera_kontrol_unitesi") { _autoNote = Loc.T("power.no_power"); return; }
        int n = 0;
        switch (id)
        {
            case "hasat_robotu":
                n = AutomationRules.Harvest(_d.Data, farm, _d.State.Rng, x, z, m.Radius, chests);
                if (n > 0) { _d.OnHarvest(n); Fx.Text3D(GlobalPosition + Vector3.Up * 2.2f, Loc.T("world.auto_harvested", n), Palette.Warning, 40, 1f, 1.1f); }
                _autoNote = Loc.T("world.auto_harvesting");
                break;
            case "ekim_robotu":
                n = AutomationRules.Plant(_d.Data, farm, _d.State.Rng, x, z, m.Radius, chests);
                if (n > 0) Fx.Text3D(GlobalPosition + Vector3.Up * 2.2f, Loc.T("world.auto_planted", n), Palette.Plant, 40, 1f, 1.1f);
                _autoNote = Loc.T("world.auto_planting");
                break;
            case "sera_kontrol_unitesi":
                n = AutomationRules.Greenhouse(farm, x, z, m.Radius, powered, _sera);
                _autoWork = n > 0 || _sera.Count > 0;
                _autoNote = powered ? Loc.T("world.auto_greenhouse", n) : Loc.T("power.no_power");
                break;
            case "otomatik_yemlik":
                n = AutomationRules.Feed(_d.Pens.Props.Where(p => p.Feeder != null && Near(p, m.Radius)).Select(p => p.Feeder).ToList(), chests, WantedFoods());
                if (n > 0) Fx.Text3D(GlobalPosition + Vector3.Up * 2.2f, Loc.T("world.auto_feed", n), Palette.Warning, 40, 1f, 1.1f);
                _autoNote = Loc.T("world.auto_feeding");
                break;
            case "yumurta_toplayici":
                n = AutomationRules.CollectEggs(_d.Pens.Props.Where(p => p.Nest != null && Near(p, m.Radius)).Select(p => p.Nest).ToList(), chests, _d.Data.Pen.Animals[0].Product);
                if (n > 0) Fx.Text3D(GlobalPosition + Vector3.Up * 2.2f, Loc.T("world.auto_eggs", n), Palette.OffWhite, 40, 1f, 1.1f);
                _autoNote = Loc.T("world.auto_collecting");
                break;
            case "sagim_kirkim_kolu":
                n = AutomationRules.Milk(_d.State.Ranch.Animals, x, z, m.Radius, m.Items, chests);
                if (n > 0) Fx.Text3D(GlobalPosition + Vector3.Up * 2.2f, Loc.T("world.auto_milk", n), Palette.OffWhite, 40, 1f, 1.1f);
                _autoNote = Loc.T("world.auto_milking");
                break;
            case "paketleyici":
                n = AutomationRules.Pack(_d.State.Cargo, chests, _idleT > m.IdleLaunchSeconds * 0.5f, out bool ready);
                _idleT = n > 0 || ready ? 0 : _idleT + m.Interval;
                var cargo = _d.State.Cargo;
                if (cargo.Pad.Count > 0 && _d.Pod.IsRamp && (cargo.Pad.Count >= _d.Data.Economy.Ramp.MaxCrates || _idleT >= m.IdleLaunchSeconds)) { _d.MachineLaunchRamp(); _idleT = 0; }
                _autoNote = Loc.T("world.auto_packing", cargo.Open?.Items.UsedSlots ?? 0, cargo.Pad.Count);
                break;
            case "sondaj_kulesi":
                var poi = DrillPoi(m);
                string ore = poi == null ? null : AutomationRules.DrillOre(_d.Data, poi.Biome);
                if (ore != null && AutomationRules.AddAll(chests, ore, 1) > 0) { n = 1; Fx.Text3D(GlobalPosition + Vector3.Up * 2.2f, $"+1 {ItemNames.Of(ore)}", Palette.Warning, 40, 1f, 1.1f); }
                _autoNote = ore == null ? Loc.T("world.auto_no_ore") : Loc.T("world.auto_drilling");
                break;
        }
        if (n > 0) GD.Print($"MACHINE {id} n={n}");
    }

    bool Near(Node3D p, float r) => new Vector2(p.GlobalPosition.X - GlobalPosition.X, p.GlobalPosition.Z - GlobalPosition.Z).Length() <= r;

    Poi DrillPoi(MachineDef m) => _d.World?.Pois?.FirstOrDefault(p => p.Type == m.PoiType && new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() <= m.Radius);

    List<string> WantedFoods()
    {
        var ranch = _d.State.Ranch;
        var l = ranch.Animals.Where(a => a.Tame && ranch.Rules.Known(a.Species)).Select(a => ranch.Rules.Species(a.Species).Food).Distinct().ToList();
        if (!l.Contains("wheat")) l.Add("wheat");
        return l;
    }

    /// <summary>Montaj Makinesi: secili tarifi kuyrukta tutar (girdi Sandiklardan, urun Sandiga); guc kuyruk uzerinden.</summary>
    void TickAssembler()
    {
        _autoWork = false;
        var sel = _d.State.Assembler.Recipe;
        if (string.IsNullOrEmpty(sel) || _d.Stations.FirstOrDefault(s => s.Queue == Queue) != this) return;
        var r = _d.Data.Tool.RecipeFor(sel);
        if (r == null) return;
        var proxy = AssemblerState.Proxy(r);
        if (Queue.Active == null && QueueAfford(proxy)) _autoWork = true;
        if (Queue.Queued < 1 && Queue.Enqueue(proxy, _d.StationStore())) _autoNote = Loc.T("world.auto_crafting");
    }

    bool QueueAfford(RecipeDef r) => StationQueue.CanAfford(r, _d.StationStore());

    /// <summary>E: makine ve Sirt Yazicisi'nin kendi menusu. Isledi ise true (tarif paneli acilmaz).</summary>
    public bool OpenMachineMenu()
    {
        string id = Def.Station;
        if (id == "sirt_yazicisi") { _d.Hud.OpenCraft(null); return true; } // el tarifleri (Tab paneli) Sirt Yazicisi'ndan
        var m = Machine;
        if (m == null) return false;
        var items = new List<Hud.MenuItem>();
        string footer;
        if (id == "montaj_makinesi")
        {
            var a = _d.State.Assembler;
            string cur = string.IsNullOrEmpty(a.Recipe) ? Loc.T("world.auto_no_recipe") : ItemNames.Of(a.Recipe);
            items.Add(new Hud.MenuItem(Loc.T("world.auto_recipe", cur), () => true, () => { a.Next(_d.Data, _d.State.Known); Sfx.Play("place", null, -12f); OpenMachineMenu(); }));
            footer = Loc.T("world.auto_footer_assembler");
        }
        else
        {
            footer = id switch
            {
                "hasat_robotu" => Loc.T("world.auto_footer_harvest", m.Radius.ToString("0", System.Globalization.CultureInfo.InvariantCulture)),
                "ekim_robotu" => Loc.T("world.auto_footer_plant", m.Radius.ToString("0", System.Globalization.CultureInfo.InvariantCulture)),
                "otomatik_yemlik" => Loc.T("world.auto_footer_feeder"),
                "yumurta_toplayici" => Loc.T("world.auto_footer_eggs"),
                "sagim_kirkim_kolu" => Loc.T("world.auto_footer_milk"),
                "paketleyici" => Loc.T("world.auto_footer_packer"),
                "sondaj_kulesi" => Loc.T("world.auto_footer_drill"),
                "sera_kontrol_unitesi" => Loc.T("world.auto_footer_greenhouse", m.Radius.ToString("0", System.Globalization.CultureInfo.InvariantCulture)),
                _ => "",
            };
        }
        string state = _d.Power?.PortOf(this) is { } port ? (port.Powered ? Loc.T("world.auto_fed") : Loc.T("power.no_power")) : Loc.T("world.auto_not_wired");
        items.Add(new Hud.MenuItem(Loc.T("world.auto_state", state, (_autoWork ? " · " + _autoNote : "")), () => false, () => { }));
        _d.Hud.ShowMenu(Def.Name, items, footer + "\n" + Loc.T("world.close_footer"));
        return true;
    }
}
