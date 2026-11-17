using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Bot, GDD 24'un ilk 10 dakikadan sonraki adimlari: 11 yemek, 13 firin, 14 civi, 15 kasa, 16 barinak (10 hasat
/// Bot.Machines'te, 12 bakir_kil Bot.Ilkgun'da). Bu adimlar veride on kosullari olunca acilir (scenario.json requires:
/// ocakbasi, copper_ingot, civi_tabancasi, el_arabasi, uyku_tulumu; ekonomi akisi); o zamana kadar yuklemede atlanir
/// ve bu isleyiciler calismaz.
/// Istasyonlari ilk gunun yerlesimine gore (agil, tarla, Tezgah ile cakismadan) bu dosya kurar. Ocakbasi'nda yemek ve
/// Paketleme'de kasa eski zincirin isleyicileriyle olur (Bot.Machines: StepCorba, StepPaketle, StepKasaSat); birlesince
/// govdeleri ekonomi'ninkidir. Kaynak hep oyun yolundan gelir (Kazma, Bicme, istasyon menusu).
/// </summary>
public partial class Bot
{
    // Ilk gunun yerlesimi (Ocak'a gore): Tezgah (-2,5; 4,5), tarla x 3..6 z -1..2, agil x -6..-3 z -9..-3 (kapisi doguda),
    // kapsul (9; -9). Istasyonlar birbirinden 2,2 m, tarladan 1,6 m, kapsulden 3,5 m uzakta (Capa.Insa hayalet kurali).
    static readonly Vector3 OcakbasiAt = new(1.0f, 0, 6.5f), OcakbasiStand = new(1.0f, 0, 3.5f);
    static readonly Vector3 FirinAt = new(2.5f, 0, -5.5f), FirinStand = new(0.5f, 0, -3.0f);
    // Paketleme Tas Firin'dan uzak: E en yakin istasyonun menusunu acar, yaklasirken firin daha yakin kalmasin.
    static readonly Vector3 PaketlemeAt = new(1.0f, 0, -11.0f), PaketlemeStand = new(1.0f, 0, -8.0f);
    static readonly Vector3 BarinakAt = new(8.5f, 0, 6.5f); // tarlanin kuzeydogusu, tarladan ~4 m (temel 3 x 3 m)
    int _tryIdx;
    string _tryFor;
    bool _tulumPlaced;

    void RegisterIlkgunSonra()
    {
        _steps["yemek"] = Machine(StepYemek);
        _steps["firin"] = Machine(StepFirin);
        _steps["civi"] = Machine(StepCiviVeTabanca); // eski zincirin "civi" isleyicisini (yalniz Civi) gecer
        _steps["kasa"] = Machine(StepKasa);
        _steps["barinak"] = Machine(StepBarinak);
        Events.StationPlaced += id => { if (id == "uyku_tulumu") _tulumPlaced = true; };
    }

    // ---------- ortak ----------

    /// <summary>Istasyon yoksa bedelini oyun yolundan toplayip yerlesimdeki yerine kurar. Istasyon varsa true.</summary>
    bool HaveStation(string id, Vector3 o, Vector3 at, Vector3 stand)
    {
        if (StationOf(id) != null) return true;
        if (HaveCost(PlaceableOf(id))) PlacePropAt(id, _d.World.Ground(o + at), _d.World.Ground(o + stand));
        return false;
    }

    /// <summary>Istasyon menusundeki tarif sirasi (menu istasyonun tariflerini veri sirasiyla listeler).</summary>
    static int RecipeSlot(Station st, string recipe) => st.Queue.Recipes.Select(r => r.Id).ToList().IndexOf(recipe);

    void StandBy(Station st) { P.MoveOverride = Vector3.Zero; Aim(st.GlobalPosition + Vector3.Up); }

    /// <summary>
    /// Tarifin girdilerini (veriden) ve bir Odun yakiti toplar, istasyonda siraya ekler. Istasyon calisiyorsa bekler.
    /// Girdiler toplanan kaynak olmali (ara urunu cagiran once yapar).
    /// </summary>
    void Craft(Station st, string recipe)
    {
        if (st.Queue.Active != null) { StandBy(st); return; }
        var r = st.Queue.Recipes.First(x => x.Id == recipe);
        foreach (var (item, n) in r.In)
            if (!HaveItem(item, n + (item == "wood" ? 1 : 0))) return;
        if (!HaveItem("wood", 1)) return; // istasyon yakiti (Tas Firin, Ocakbasi: is basina 1 Odun)
        UseStation(st, RecipeSlot(st, recipe));
    }

    // ---------- 11 Yemek: Ocakbasi kur, Sebze Corbasi pisir, cantadan ye (Events.Ate) ----------

    void StepYemek(Vector3 o)
    {
        if (HaveStation("ocakbasi", o, OcakbasiAt, OcakbasiStand)) StepCorba(o);
    }

    // ---------- 13 Firin: Tas Firin kur (Kil kil yatagindan), Bakir Kulce erit (Events.Produced copper_ingot) ----------

    void StepFirin(Vector3 o)
    {
        if (HaveStation("tas_firin", o, FirinAt, FirinStand)) Craft(StationOf("tas_firin"), "copper_ingot");
    }

    // ---------- 14 Civi: Tezgah'ta Civi Tabancasi, sonra Civi; kulceyi Tas Firin'da eritir ----------

    void StepCiviVeTabanca(Vector3 o)
    {
        var tz = StationOf("tezgah");
        var fr = StationOf("tas_firin");
        if (tz == null || fr == null) { if (_stepT > 5f) { GD.Print("BOT civi: tezgah or tas_firin missing"); _stepT = 0; } return; }
        if (tz.Queue.Active != null) { StandBy(tz); return; }
        string want = _d.State.Backpack.Count("civi_tabancasi") == 0 ? "civi_tabancasi" : "civi";
        int ingots = tz.Queue.Recipes.First(r => r.Id == want).In.GetValueOrDefault("copper_ingot");
        if (_d.State.Backpack.Count("copper_ingot") < ingots) { Craft(fr, "copper_ingot"); return; }
        Craft(tz, want);
    }

    // ---------- 15 Kasa: Paketleme kur, kasaya Salgam koy ve kapat, kasayi rampaya tasi, gonder (Events.Sold) ----------

    void StepKasa(Vector3 o)
    {
        if (!HaveStation("paketleme", o, PaketlemeAt, PaketlemeStand)) return;
        if (_d.GetTree().GetNodesInGroup("cargo_crates").Count > 0) StepKasaSat(o);
        else StepPaketle(o);
    }

    // ---------- 16 Barinak: temel, Uyku Tulumu, duvar, kapi kasasi, Ahsap Kapi, cati ----------

    void StepBarinak(Vector3 o)
    {
        var b = _d.Build;
        if (b == null) { GD.Print("BOT no build kit"); return; }
        var spot = o + BarinakAt;
        var pieces = b.System.Pieces;
        // Ilk temel arazide serbest durur, nisandan biraz kayabilir: yakindaki ilk temel barinagin temelidir.
        var fp = pieces.Where(p => p.Def.Kind == "foundation" && Flat(p.GlobalPosition - spot) < 6f).OrderBy(p => Flat(p.GlobalPosition - spot)).FirstOrDefault();
        if (fp == null)
        {
            // Kuzeyden (tarlanin ote yanindan) bakarak: isin kabarik ekinlere carpip temeli tarlaya kaydirmasin.
            var g = _d.World.Ground(spot);
            BuildWhenValid("kare_temel", new[] { g }, _d.World.Ground(spot + Vector3.Back * 6.5f)); // serbest temel ~2 m oyuncuya dogru kayar: tarladan uzaga
            return;
        }
        var c = fp.GlobalPosition;
        float top = c.Y + fp.Def.BoundsMax.Y;
        var south = Vector3.Forward; // -Z: duvar
        var east = Vector3.Right;    // +X: kapi kasasi (dogu yakasi acik ve yuruyerek ulasilir)
        if (!_tulumPlaced)
        {
            // Temelin acik (dogu) yanindan, temelin bati yarisina: dogu kenarindaki kapiya yer kalsin (yerlestirilebilirler
            // arasi bosluk kurali, Capa.Insa: yaricaplar + 0,2 m).
            if (HaveCost(PlaceableOf("uyku_tulumu"))) PlacePropAt("uyku_tulumu", (c + Vector3.Left * 0.8f) with { Y = top }, _d.World.Ground(c + Vector3.Right * 4.5f));
            return;
        }
        BuildPiece OnEdge(Vector3 outw) => pieces.FirstOrDefault(p => p.Def.Kind == "wall" && Flat(p.GlobalPosition - (c + outw * 1.5f)) < 0.6f);
        Vector3[] EdgeAims(Vector3 outw) => new[] { 1.05f, 0.9f, 1.2f }.Select(k => new Vector3(c.X, top, c.Z) + outw * k).ToArray();
        if (OnEdge(south) == null) { BuildWhenValid("duvar", EdgeAims(south), _d.World.Ground(c + south * 7f)); return; }
        var kasa = OnEdge(east);
        if (kasa == null) { BuildWhenValid("kapi_kasasi", EdgeAims(east), _d.World.Ground(c + east * 7f)); return; }
        if (kasa.Def.Id == "kapi_kasasi" && !_d.Pens.Props.Any(p => IsInstanceValid(p) && p.Def.Id == "ahsap_kapi" && p.Kasa == kasa))
        {
            if (HaveCost(PlaceableOf("ahsap_kapi"))) PlacePropAt("ahsap_kapi", kasa.GlobalPosition + Vector3.Up, _d.World.Ground(c + east * 4.5f));
            return;
        }
        if (!pieces.Any(p => p.Def.Kind == "roof" && Flat(p.GlobalPosition - c) < 2.5f))
        {
            // Duz Cati duvar ustune oturur: guneydeki duvarin ust kenarina disaridan bakarak (isin duvarin ust kenarina carpar).
            var wallTop = top + OnEdge(south).Def.BoundsMax.Y;
            var mid = c + south * 1.5f;
            var aims = new[] { mid with { Y = wallTop - 0.1f }, mid with { Y = wallTop + 0.2f }, (c - south * 0.5f) with { Y = wallTop }, c with { Y = wallTop } };
            BuildWhenValid("duz_cati", aims, _d.World.Ground(c + south * 5.5f));
            return;
        }
        P.MoveOverride = Vector3.Zero; // hepsi kurulu; sayac PiecePlaced/StationPlaced ile ilerler
    }

    static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();

    /// <summary>
    /// Yapi parcasini koyar: Insa basligi, tekerlekle parca secimi, durulacak yere yuru, nisan al; hayalet gecerliyse sol tik.
    /// Gecersizse siradaki nisan noktasini dener (oyuncu da nisani kaydirir). Odun yoksa once odun.
    /// </summary>
    void BuildWhenValid(string id, Vector3[] aims, Vector3 stand, Vector3? expect = null)
    {
        var b = _d.Build;
        if (!HaveItem("wood", b.CostOf(id))) return;
        _d.Capa.Select(Capa.Head.Insa);
        if (_d.Capa.CurrentPlaceable != id) { if (_stepT > 0.12f) { _d.Capa.CyclePlaceable(1); _stepT = 0; } return; }
        if (_tryFor != id) { _tryFor = id; _tryIdx = 0; }
        Aim(aims[_tryIdx % aims.Length]);
        if (!MoveTo(stand, 0.6f, false)) { _stepT = 0; return; }
        if (_stepT < 0.9f) return;
        var cur = b.System.Current;
        bool there = expect is not Vector3 e || Flat(cur?.Xf.Origin - e ?? Vector3.Zero) < 1f;
        if (cur != null && cur.Def.Id == id && cur.Valid && there) { FirePressed = true; _stepT = 0; GD.Print($"BOT place {id} at {cur.Xf.Origin} mode={cur.Mode}"); return; }
        if (_stepT > 1.4f)
        {
            GD.Print($"BOT aim {id} try={_tryIdx} valid={cur?.Valid} at={cur?.Xf.Origin} want={expect} reason={cur?.Reason} mode={cur?.Mode}");
            _tryIdx++;
            _stepT = 0.5f;
        }
    }
}
