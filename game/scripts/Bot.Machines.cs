using System;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, kabarik urun satisi ve M1 makine zinciri: oyuncunun yaptigini yapar (hareket, nisan, tetik, E, menu tuslari).
/// Makine adimlari Machine(...) ile sarilir: rapor aciksa once kapatilir, menu zamanlayicisi isler, isleyici Ocak konumunu alir.
/// </summary>
public partial class Bot
{
    float _menuT;

    void RegisterMachines()
    {
        _steps["sell"] = StepSell;
        _steps["tezgah_kur"] = Machine(StepTezgahKur);
        _steps["civi"] = Machine(StepCivi);
        _steps["hasat"] = Machine(StepHasat);
        _steps["firin_kur"] = Machine(StepFirinKur);
        _steps["corba"] = Machine(StepCorba);
        _steps["paketle"] = Machine(StepPaketle);
        _steps["kasa_sat"] = Machine(StepKasaSat);
    }

    /// <summary>Makine zinciri adimi: Hasat Raporu aciksa once kapatir; degilse menu zamanlayicisini isletip adimi Ocak konumuyla cagirir.</summary>
    Action Machine(Action<Vector3> step) => () =>
    {
        if (_d.Hud.ReportOpen) { if (_stepT > 1.5f) { CloseReport(); _stepT = 0; } P.MoveOverride = Vector3.Zero; return; }
        _menuT -= (float)GetProcessDeltaTime();
        var o = _d.OcakPos;
        step(o);
    };

    void StepSell()
    {
        Head(Capa.Head.Cekim);
        var held = _d.Capa.Held;
        var pad = _d.Pod.PadCenter;
        if (held == null)
        {
            var view = _d.FarmView.Views.Where(v => v.Plot.Swollen != null && v.Plot.Planted).OrderBy(v => v.GlobalPosition.DistanceTo(pad)).FirstOrDefault();
            var loose = _d.GetTree().GetNodesInGroup("swollen").OfType<SwollenCrop>().FirstOrDefault(s => !s.Sold);
            Vector3? target = loose?.GlobalPosition ?? view?.CropTop;
            if (target == null) return;
            // Kabarik urunun carpisma sekli yok: nisangah dibine (yere) tutulur, insan da oyle yapar; ust noktaya
            // tutulan kamera isini urunun icinden gecip arkadaki yere carpar ve TryGrab menzil disinda kalir.
            Aim(_d.World.Ground(target.Value));
            if (MoveTo(target.Value, 4.5f, false) && _stepT > 0.6f) { FirePressed = true; _stepT = 0; }
        }
        else
        {
            var away = (P.GlobalPosition - pad); away.Y = 0;
            Aim(pad + Vector3.Up * 0.5f);
            if (MoveTo(pad + away.Normalized() * 3.2f, 0.8f, false) && _stepT > 0.8f) { FirePressed = true; _stepT = 0; }
        }
    }

    // ---------- M1 makine zinciri: oyuncunun yaptigini yapar (hareket, nisan, tetik, E, menu tuslari) ----------

    bool EnsureStone(int need) => EnsureItem("stone", need);

    /// <summary>
    /// Cantada yeterli `item` yoksa oyun yolundan toplar: tas, kil, bakir cevheri Kazma ile kayadan (kil ve bakir
    /// World.Placeholders yer tutucu dugumleri); odun Bicme ile agactan; lif ve bugday Bicme ile tutamdan. Yeterliyse true.
    /// </summary>
    bool EnsureItem(string item, int need)
    {
        if (_d.State.Backpack.Count(item) >= need) return true;
        var p = P.GlobalPosition;
        if (item == "wood")
        {
            Head(Capa.Head.Bicme);
            var tr = _d.World.TreePositions.OrderBy(x => x.DistanceTo(p)).First();
            Aim(tr + Vector3.Up * 1.4f);
            if (MoveTo(tr, 3.0f)) Fire = true;
            return false;
        }
        if (item == "fiber" || item == "wheat")
        {
            // Agac Bicme'de tutamdan once gelir: yaninda agac olmayan tutami sec.
            var t = _d.World.Tufts.Where(x => x.Left > 0 && x.Item == item && !_d.World.TreePositions.Any(tp => new Vector2(tp.X - x.Position.X, tp.Z - x.Position.Z).Length() < 2.2f))
                .OrderBy(x => x.Position.DistanceTo(p)).FirstOrDefault();
            if (t == null) { GD.Print($"BOT no {item} tufts left"); return true; }
            Head(Capa.Head.Bicme);
            Aim(t.Position + Vector3.Up * 0.3f);
            if (MoveTo(t.Position, 3.0f)) Fire = true;
            return false;
        }
        Head(Capa.Head.Kazma);
        var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Item == item).OrderBy(r => r.GlobalPosition.DistanceTo(p)).FirstOrDefault();
        if (rock == null) { GD.Print($"BOT no {item} rocks left"); return true; }
        Aim(rock.Center);
        if (MoveTo(rock.GlobalPosition, 4.5f)) Fire = true;
        return false;
    }

    /// <summary>Istasyon menusundeki tarif sirasi (Director.OpenStationMenu veri sirasiyla listeler).</summary>
    static int RecipeIndex(Station st, string recipe) => st.Queue.Recipes.Select(r => r.Id).ToList().IndexOf(recipe);

    /// <summary>Istasyonun yaninda bekler (uretim surerken).</summary>
    void WaitAt(Station st) { P.MoveOverride = Vector3.Zero; Aim(st.GlobalPosition + Vector3.Up); }

    /// <summary>Yerlestirilebiliri edinip (uret) kemerden ele alir ve verilen noktaya koyar (Bot.Kemer.cs, karar 23).</summary>
    void PlaceAt(string id, Vector3 at) => PlaceHeld(id, _d.World.Ground(at), at + new Vector3(0, 0, -4.5f));

    Station StationOf(string id) => _d.Stations.FirstOrDefault(s => s.Def.Id == id);

    /// <summary>Istasyona yurur, E ile menuyu acar, ilk tarifi siraya ekler.</summary>
    void UseStation(Station st, int item)
    {
        Aim(st.GlobalPosition + Vector3.Up);
        if (!MoveTo(st.GlobalPosition + (P.GlobalPosition - st.GlobalPosition).Normalized() * 2.2f, 0.5f, false)) return;
        if (!_d.Hud.MenuOpen && st.Queue.Active == null && _menuT <= 0) { Use(); _menuT = 0.6f; }
        else if (_d.Hud.MenuOpen && _menuT <= 0) { MenuPick(item); MenuClose(); _menuT = 1.0f; }
    }

    /// <summary>
    /// Karar 23: yerlestirilebilir uretilir. Cantada yoksa malzemesini toplayip uretir (Bot.Kemer.cs HavePlaceable); varsa
    /// ve istenirse yakit icin fazladan Odun toplar. Hazirsa true.
    /// </summary>
    bool EnsureCost(string placeable, int extraWood = 0)
    {
        if (!HavePlaceable(placeable)) return false;
        return extraWood <= 0 || EnsureItem("wood", extraWood);
    }

    void StepTezgahKur(Vector3 o)
    {
        if (EnsureCost("tezgah")) PlaceAt("tezgah", o + new Vector3(-5f, 0, 1.5f));
    }

    /// <summary>Bakir cevheri kaz, Tas Firin'da Bakir Kulce erit (1 Odun yakit), Tezgah'ta kulceden civi yap.</summary>
    void StepCivi(Vector3 o)
    {
        var tz = StationOf("tezgah");
        var fr = StationOf("tas_firin");
        if (tz == null || fr == null) return;
        if (tz.Queue.Active != null) { WaitAt(tz); return; }
        if (_d.State.Backpack.Count("copper_ingot") > 0) { UseStation(tz, RecipeIndex(tz, "civi")); return; }
        if (fr.Queue.Active != null) { WaitAt(fr); return; }
        if (EnsureItem("copper_ore", 2) && EnsureItem("wood", 1)) UseStation(fr, RecipeIndex(fr, "copper_ingot"));
    }

    void StepHasat(Vector3 o)
    {
        Head(Capa.Head.Bicme);
        var v = _d.FarmView.Views.Where(x => x.Plot.Planted && !x.Plot.Wild && x.Plot.Stage == Kabarma.Sim.CropStage.Mature)
            .OrderBy(x => x.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (v == null) { GD.Print("BOT nothing to harvest"); return; }
        Aim(v.GlobalPosition + Vector3.Up * 0.3f);
        if (MoveTo(v.GlobalPosition, 3.2f, false)) Fire = true;
    }

    void StepFirinKur(Vector3 o)
    {
        if (EnsureCost("tas_firin")) PlaceAt("tas_firin", o + new Vector3(-5f, 0, -3.5f));
    }

    /// <summary>Ocakbasi kur (yakit icin bir odun fazla topla), Sebze Corbasi pisir (2 Salgam + 1 Odun), cantadan ye.</summary>
    void StepCorba(Vector3 o)
    {
        var st = StationOf("ocakbasi");
        if (st == null) { if (EnsureCost("ocakbasi", 1)) PlaceAt("ocakbasi", o + new Vector3(-9f, 0, -1f)); return; }
        if (_d.State.Backpack.Count("sebze_corbasi") > 0)
        {
            P.MoveOverride = Vector3.Zero;
            if (!_d.Hud.MenuOpen && _menuT <= 0) { Bag(); _menuT = 0.5f; }
            else if (_d.Hud.InventoryOpen && _menuT <= 0 && BagUse("sebze_corbasi")) { MenuClose(); _menuT = 1f; } // envanter izgarasi: corbaya git, Enter (Bot.Hands.cs)
        }
        else if (st.Queue.Active != null) WaitAt(st);
        else if (EnsureItem("wood", 1)) UseStation(st, RecipeIndex(st, "sebze_corbasi"));
    }

    /// <summary>Paketleme Tezgahi kur; cantadaki satilir mali (once Salgam) acik kasaya koy, kasayi kapat. Kasa tezgahin onune cikar.</summary>
    void StepPaketle(Vector3 o)
    {
        var st = StationOf("paketleme");
        if (st == null) { if (EnsureCost("paketleme")) PlaceAt("paketleme", o + new Vector3(-1f, 0, 5.5f)); return; }
        if (st.Queue.Active != null) { WaitAt(st); return; }
        var cargo = _d.State.Cargo;
        int item = 0; // 0 = "Kasayi kapat"
        if (cargo.Open == null || cargo.Open.Items.IsEmpty)
        {
            // Director.OpenPackingMenu ile ayni sira: kapat, sonra cantadaki satilir mallar.
            var goods = _d.State.Backpack.Contents.Select(c => c.id).Distinct().Where(cargo.Pricing.Sellable).Take(8).ToList();
            var pick = goods.Contains("turnip") ? "turnip" : goods.FirstOrDefault();
            if (pick == null) { GD.Print("BOT nothing to pack"); return; }
            item = goods.IndexOf(pick) + 1;
        }
        UseStation(st, item);
    }

    /// <summary>Kasayi Cekim ile Kargo Rampasi'na tasi, birak (rampaya yuklenir), rampanin dibinde E: Gonder.</summary>
    void StepKasaSat(Vector3 o)
    {
        var pad = _d.Pod.PadCenter;
        var crate = _d.GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().FirstOrDefault(c => !c.Sold);
        if (crate == null) { P.MoveOverride = Vector3.Zero; return; }
        if (crate.OnRamp)
        {
            Aim(pad + Vector3.Up);
            if (!MoveTo(pad + (P.GlobalPosition - pad).Normalized() * 3.2f, 0.6f, false)) return;
            if (!_d.Hud.MenuOpen && _menuT <= 0) { Use(); _menuT = 0.6f; }
            else if (_d.Hud.MenuOpen && _menuT <= 0) { MenuPick(0); MenuClose(); _menuT = 1f; }
            return;
        }
        Head(Capa.Head.Cekim);
        if (_d.Capa.Held == null)
        {
            Aim(crate.GlobalPosition);
            if (MoveTo(crate.GlobalPosition, 4.0f, false) && _stepT > 0.6f) { FirePressed = true; _stepT = 0; }
        }
        else
        {
            var away = P.GlobalPosition - pad; away.Y = 0;
            Aim(pad + Vector3.Up * 0.5f);
            if (MoveTo(pad + away.Normalized() * 3.2f, 0.8f, false) && _stepT > 0.8f) { FirePressed = true; _stepT = 0; }
        }
    }
}
