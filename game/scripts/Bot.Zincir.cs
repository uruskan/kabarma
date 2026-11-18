using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, ilk gunun ilk 10 dakikadan sonraki adimlari (GDD 24; scenario.json 11-16), entegrasyon dali: ilkgun'un adim
/// kimlikleri ekonomi akisinin makine isleyicilerine (Bot.Machines.cs) baglanir. Her adim oyuncunun yolundan (Bot.Hands.cs):
/// kaynak toplama, Insa ile kurma, istasyon menusu, canta izgarasi, Cekim ile tasima. Esya verilmez, adim atlanmaz.
///  - yemek: Ocakbasi kur, Sebze Corbasi pisir, cantadan ye (StepCorba).
///  - firin: Tas Firin kur (15 Tas + 5 Kil), Bakir Kulce erit.
///  - civi: Tezgah'ta once Civi Tabancasi (2 Kulce + 4 Odun), sonra Civi (1 Kulce); kulce Tas Firin'dan.
///  - kasa: Paketleme Tezgahi, kasa doldur ve kapat, kasayi rampaya tasi, gonder. Not: kasa El Arabasi yerine Cekim ile
///    tasinir (adim sayaci yalniz satisa bakar; El Arabasi'na yukleme botta yok).
///  - barinak: Uyku Tulumu, kare temel, uc duvar, kapi kasasi, kasaya Ahsap Kapi, duz cati.
/// Yerler Ocak'a gore; ilkgun'un agili (x -6..-3, z -9..-3), Tezgah'i (-2,5; 4,5) ve tarla (x 3..6, z -1..2) disinda.
/// </summary>
public partial class Bot
{
    static readonly Vector3 FirinAt = new(-5f, 0, 9f);
    static readonly Vector3 PaketlemeAt = new(1.5f, 0, 8f);
    static readonly Vector3 ShelterAt = new(7f, 0, 4f);
    static readonly Vector3 ShelterBagAt = new(10.5f, 0, 1f);

    void RegisterZincir()
    {
        _steps["yemek"] = Machine(StepCorba);       // Bot.Machines.cs (ekonomi): Ocakbasi, corba, ye
        _steps["firin"] = Machine(StepFirin);
        _steps["civi"] = Machine(StepCiviIkili);    // ilkgun'un civi adimi (hedef 2) ekonomi'nin tek civi isleyicisinin yerine
        _steps["kasa"] = Machine(StepKasa);
        _steps["barinak"] = Machine(StepBarinak);
    }

    // ---------- 13 Tas Firin ve Bakir Kulce ----------

    void StepFirin(Vector3 o)
    {
        var fr = StationOf("tas_firin");
        if (fr == null) { if (EnsureCost("tas_firin")) PlaceAt("tas_firin", o + FirinAt); return; }
        if (fr.Queue.Active != null) { WaitAt(fr); return; }
        if (EnsureItem("copper_ore", 2) && EnsureItem("wood", 1)) UseStation(fr, RecipeIndex(fr, "copper_ingot"));
    }

    // ---------- 14 Civi Tabancasi ve Civi ----------

    void StepCiviIkili(Vector3 o)
    {
        var tz = StationOf("tezgah");
        var fr = StationOf("tas_firin");
        if (tz == null || fr == null) { P.MoveOverride = Vector3.Zero; return; }
        if (tz.Queue.Active != null) { WaitAt(tz); return; }
        var bag = _d.State.Backpack;
        string next = bag.Count("civi_tabancasi") == 0 ? "civi_tabancasi" : "civi";
        var recipe = tz.Queue.Recipes.First(r => r.Id == next);
        int ingots = recipe.In.TryGetValue("copper_ingot", out int n) ? n : 0;
        if (bag.Count("copper_ingot") >= ingots)
        {
            foreach (var (item, need) in recipe.In) if (item != "copper_ingot" && !EnsureItem(item, need)) return;
            UseStation(tz, RecipeIndex(tz, next));
            return;
        }
        if (fr.Queue.Active != null) { WaitAt(fr); return; }
        if (EnsureItem("copper_ore", 2) && EnsureItem("wood", 1)) UseStation(fr, RecipeIndex(fr, "copper_ingot"));
    }

    // ---------- 15 Kasa ----------

    void StepKasa(Vector3 o)
    {
        if (StationOf("paketleme") == null) { if (EnsureCost("paketleme")) PlaceAt("paketleme", o + PaketlemeAt); return; }
        bool crateOut = _d.GetTree().GetNodesInGroup("cargo_crates").OfType<CargoCrateNode>().Any(c => !c.Sold);
        if (crateOut) StepKasaSat(o); // Bot.Machines.cs: Cekim ile rampaya, E: Gonder
        else StepPaketle(o);          // Bot.Machines.cs: salgami kasaya koy, kasayi kapat
    }

    // ---------- 16 Barinak ----------

    static readonly (string id, Vector3 outward)[] ShelterWalls =
    {
        ("duvar", Vector3.Forward), ("duvar", Vector3.Right), ("duvar", Vector3.Left), ("kapi_kasasi", Vector3.Back),
    };

    int _shelterPending = -1;

    void StepBarinak(Vector3 o)
    {
        var b = _d.Build;
        if (b == null) { GD.Print("BOT no build kit"); return; }
        if (!_d.Furniture.Any(x => x.Def.Id == "uyku_tulumu")) { if (EnsureCost("uyku_tulumu")) PlaceAt("uyku_tulumu", o + ShelterBagAt); return; }

        var pieces = b.System.Pieces;
        var f = o + ShelterAt;
        var fp = pieces.Where(x => x.Def.Id == "kare_temel" && Flat(x.GlobalPosition, f) < 4f).OrderBy(x => Flat(x.GlobalPosition, f)).FirstOrDefault();
        string want;
        Vector3 target, stand;
        if (fp == null)
        {
            want = "kare_temel";
            target = _d.World.Ground(f);
            stand = target + new Vector3(0, 0, -6.5f);
        }
        else
        {
            var c = fp.GlobalPosition;
            float top = c.Y + fp.Def.BoundsMax.Y;
            var wall = ShelterWalls.FirstOrDefault(w => !pieces.Any(p => p.Def.Id == w.id && Flat(p.GlobalPosition, c + w.outward * 1.5f) < 0.8f));
            if (wall.id != null)
            {
                want = wall.id;
                target = new Vector3(c.X, top, c.Z) + wall.outward * (1.5f - 0.45f);
                stand = _d.World.Ground(c + wall.outward * 7f);
            }
            else if (!(_d.Pens?.Props.Any(p => IsInstanceValid(p) && p.Def.Id == "ahsap_kapi" && p.Kasa != null) ?? false))
            {
                // Ahsap Kapi: Insa hayaleti Kapi Kasasi'na oturur (PenSystem.SnapGhost); kasanin onunden.
                var kasa = pieces.First(p => p.Def.Id == "kapi_kasasi" && Flat(p.GlobalPosition, c + Vector3.Back * 1.5f) < 0.8f);
                var at = _d.World.Ground(kasa.GlobalPosition);
                if (HaveCost(PlaceableOf("ahsap_kapi"))) PlacePropAt("ahsap_kapi", at, _d.World.Ground(at + Vector3.Back * 4f));
                return;
            }
            else if (!pieces.Any(p => p.Def.Kind == "roof" && Flat(p.GlobalPosition, c) < 2.2f))
            {
                // Duz cati duvarlarin ustune: duvar ustu ortasina, kapisiz kenarin disindan nisan.
                want = "duz_cati";
                float wallTop = pieces.Where(p => p.Def.Kind == "wall" && Flat(p.GlobalPosition, c) < 2.2f).Select(p => p.GlobalPosition.Y + p.Def.BoundsMax.Y).DefaultIfEmpty(top + 3f).Max();
                target = new Vector3(c.X, wallTop, c.Z);
                stand = _d.World.Ground(c + Vector3.Forward * 9f);
            }
            else { P.MoveOverride = Vector3.Zero; return; } // hepsi kurulu; sayac ShelterParts ile ilerler
        }
        if (!EnsureItem("wood", b.CostOf(want))) return;
        Head(Capa.Head.Insa);
        if (_d.Capa.CurrentPlaceable != want) { if (_stepT > 0.12f) { NextPlaceable(); _stepT = 0; } return; }
        Aim(target);
        if (_shelterPending >= 0)
        {
            if (pieces.Count > _shelterPending) { GD.Print($"BOT built {want} ({pieces.Count}) at {pieces[pieces.Count - 1].GlobalPosition} target={target}"); _shelterPending = -1; }
            else if (_stepT > 1.0f) { GD.Print($"BOT place failed {want}: {b.System.Current?.Reason} valid={b.System.Current?.Valid}"); _shelterPending = -1; }
            return;
        }
        if (MoveTo(stand, 0.6f, false) && _stepT > 1.2f && b.System.Current?.Valid == true)
        {
            _shelterPending = pieces.Count;
            FirePressed = true;
            _stepT = 0;
        }
        else if (_stepT > 6f) { GD.Print($"BOT shelter wait {want}: valid={b.System.Current?.Valid} reason={b.System.Current?.Reason} mode={b.System.Current?.Mode}"); _stepT = 1.2f; }
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
