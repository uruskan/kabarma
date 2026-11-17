using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Bot, ilk gun (GDD 24 sirasi): kapak acilisini bekler, Tezgah, tek adimda tarla, agil (5 Cit + 1 Bahce Kapisi),
/// Gidak'i bugdayla kazanip acik kapidan agila sokar, geceden once yumurtayi alip kapiyi kapatir.
/// Oyuncunun yolu: hareket, nisan, tetik, E, tekerlek. Yapi parcasini koymadan once hayalete bakar (oyuncu da bakar).
/// </summary>
public partial class Bot
{
    // Agil plani (Ocak'a gore): 1x2 hucre, 3 x 6 m. Ilk cit arazide serbest; gerisi ilk citin gercek yerine gore.
    static readonly Vector3 PenFirst = new(-4.5f, 0, -9f);
    static readonly Vector3 TezgahAt = new(-2.5f, 0, 4.5f);
    Vector3 _aimFix;
    int _penStage;
    int _gidakStage;

    void RegisterIlkgun()
    {
        _steps["mine"] = AfterIntro(StepMine);
        _steps["tezgah"] = Machine(StepTezgah);
        _steps["tarla"] = StepTarla;
        _steps["agil"] = Machine(StepAgil);
        _steps["gidak"] = Machine(StepGidak);
        _steps["night"] = StepNightPen;
        _steps["bakir_kil"] = Machine(StepBakirKil);
        RegisterIlkgunSonra(); // Bot.IlkgunSonra.cs: 11 yemek, 13 firin, 14 civi, 15 kasa, 16 barinak
    }

    // ---------- 12 Bakir ve kil: Kazma ile damardan ve kil yatagindan ----------

    void StepBakirKil(Vector3 o)
    {
        if (HaveItem("copper_ore", 1)) HaveItem("clay", 1);
    }

    /// <summary>Kapak acilisi bitmeden oyuncu hareket etmez (PLAN 1.4, 0:00).</summary>
    System.Action AfterIntro(System.Action step) => () =>
    {
        if (!_d.Pod.IntroDone) { P.MoveOverride = Vector3.Zero; return; }
        step();
    };

    // ---------- kaynak ----------

    // Adlar ekonomi'nin Bot.Machines EnsureItem/EnsureCost'undan ayri (birlesince ayni imza derleme hatasi olurdu).
    /// <summary>Yerlestirilebilirin bedeli cantada yoksa eksik kaynagi oyun yolundan toplar. Tamamsa true.</summary>
    bool HaveCost(PlaceableDef def)
    {
        foreach (var (item, n) in def.Cost)
            if (_d.State.Backpack.Count(item) < n) { Gather(item); return false; }
        return true;
    }

    bool HaveItem(string item, int n)
    {
        if (_d.State.Backpack.Count(item) >= n) return true;
        Gather(item);
        return false;
    }

    /// <summary>Kaynagi oyuncunun yolundan toplar: tas/kil/bakir Kazma, odun agactan Bicme, lif/bugday tutamdan Bicme.</summary>
    void Gather(string item)
    {
        if (item is "wood")
        {
            _d.Capa.Select(Capa.Head.Bicme);
            var t = _d.World.TreePositions.OrderBy(x => x.DistanceTo(P.GlobalPosition)).First();
            Aim(t + Vector3.Up * 1.4f);
            if (MoveTo(t, 3.0f)) Fire = true;
            return;
        }
        if (item is "fiber" or "wheat")
        {
            _d.Capa.Select(Capa.Head.Bicme);
            var tuft = _d.World.Tufts.Where(x => x.Left > 0 && x.Item == item).OrderBy(x => x.Position.DistanceTo(P.GlobalPosition)).FirstOrDefault();
            if (tuft == null) { GD.Print($"BOT no {item} tufts left"); return; }
            Aim(tuft.Position + Vector3.Up * 0.3f);
            if (MoveTo(tuft.Position, 3.0f)) Fire = true;
            return;
        }
        _d.Capa.Select(Capa.Head.Kazma);
        var rock = _d.World.Rocks.Where(r => IsInstanceValid(r) && r.Stones > 0 && r.Item == item).OrderBy(r => r.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (rock == null) { GD.Print($"BOT no {item} rocks left"); return; }
        Aim(rock.Center);
        if (MoveTo(rock.GlobalPosition, 4.5f)) Fire = true;
    }

    PlaceableDef PlaceableOf(string id) => _d.Data.Tool.Placeables.First(p => p.Id == id);

    // ---------- 4 Tezgah ----------

    void StepTezgah(Vector3 o)
    {
        if (HaveCost(PlaceableOf("tezgah"))) PlaceAt("tezgah", o + TezgahAt);
    }

    // ---------- 5 Tarla: sur, ek, sula ----------

    void StepTarla()
    {
        var f = _d.State.Farm;
        if (_cells.Count < 9 || f.Plots.Count(p => p.Planted) < 9) { StepTillPlant(); return; }
        StepWater();
    }

    // ---------- 6 Agil ----------

    /// <summary>Agilin alti parcasi: (merkez, parca, durulacak yon). Ilk citin gercek yerine gore.</summary>
    List<(Vector3 at, string id, Vector3 outward)> PenPlan(Vector3 first)
    {
        var c0 = first + new Vector3(-1.5f, 0, 0);
        var c1 = first + new Vector3(1.5f, 0, 0);
        return new()
        {
            (first, "cit", Vector3.Forward),                              // guney (-Z)
            (c1 + new Vector3(0, 0, 1.5f), "cit", Vector3.Right),        // dogu alt
            (c1 + new Vector3(0, 0, 4.5f), "bahce_kapisi", Vector3.Right), // dogu ust: kapi uslere bakar
            (first + new Vector3(0, 0, 6f), "cit", Vector3.Back),        // kuzey (+Z)
            (c0 + new Vector3(0, 0, 4.5f), "cit", Vector3.Left),         // bati ust
            (c0 + new Vector3(0, 0, 1.5f), "cit", Vector3.Left),         // bati alt: halka kapanir
        };
    }

    BuildPiece PieceNear(Vector3 at, string id) =>
        _d.Build.System.Pieces.FirstOrDefault(p => p.Def.Id == id && new Vector2(p.GlobalPosition.X - at.X, p.GlobalPosition.Z - at.Z).Length() < 0.6f);

    void StepAgil(Vector3 o)
    {
        var b = _d.Build;
        if (b == null) { GD.Print("BOT no build kit"); return; }
        var first = PieceNear(o + PenFirst, "cit") ?? b.System.Pieces.FirstOrDefault(p => p.Def.Id == "cit" && p.GlobalPosition.DistanceTo(o + PenFirst) < 2.5f);
        var plan = PenPlan(first != null ? first.GlobalPosition with { Y = 0 } : o + PenFirst);
        int i = plan.FindIndex(x => PieceNear(x.at, x.id) == null);
        if (i < 0) { P.MoveOverride = Vector3.Zero; return; } // hepsi kurulu; sayac PenChanged ile ilerler
        if (i != _penStage) { _penStage = i; _aimFix = Vector3.Zero; }
        var (at, id, outward) = plan[i];
        if (!HaveItem("wood", b.CostOf(id))) return;
        PlacePiece(id, _d.World.Ground(at), _d.World.Ground(at + outward * 6f), i == 0);
    }

    /// <summary>
    /// Yapi parcasini koyar: Insa basligi, tekerlekle parca secimi, durulacak yere yuru, nisan al, hayalete bak, sol tik.
    /// Hayalet hedefin disindaysa nisani duzeltir (oyuncu da hayaleti hizalar).
    /// </summary>
    void PlacePiece(string want, Vector3 target, Vector3 stand, bool free)
    {
        var b = _d.Build;
        _d.Capa.Select(Capa.Head.Insa);
        if (_d.Capa.CurrentPlaceable != want) { if (_stepT > 0.12f) { _d.Capa.CyclePlaceable(1); _stepT = 0; } return; }
        Aim(target + _aimFix + Vector3.Up * 0.3f);
        if (!MoveTo(stand, 0.6f, false)) { _stepT = 0; return; }
        var cur = b.System.Current;
        if (cur == null || cur.Def.Id != want || _stepT < 0.9f) return;
        var err = new Vector3(target.X - cur.Xf.Origin.X, 0, target.Z - cur.Xf.Origin.Z);
        if (err.Length() > 0.3f || !cur.Valid)
        {
            if (_stepT > 1.4f)
            {
                _aimFix += err * (free ? 0.8f : 0.5f);
                if (_aimFix.Length() > 3f) _aimFix = _aimFix.Normalized() * 3f;
                GD.Print($"BOT aim {want} err={err.Length():0.00} valid={cur.Valid} reason={cur.Reason} mode={cur.Mode}");
                _stepT = 0.5f;
            }
            return;
        }
        FirePressed = true;
        _stepT = 0;
        GD.Print($"BOT place {want} at {cur.Xf.Origin}");
    }

    // ---------- 7 Gidak ----------

    PenSystem.Gate PenGate => _d.Pens?.Gates.FirstOrDefault(g => g.Leaves.Count > 0);

    /// <summary>Agil duvarlarinin etrafindan (acik kapidan) yurur; oyuncu da duvara yaslanmadan dolasir.</summary>
    bool MoveRouted(Vector3 target, float within)
    {
        var way = _d.Pens?.Route(P.GlobalPosition, target, 0.45f) ?? target;
        if (new Vector2(way.X - target.X, way.Z - target.Z).Length() > 0.05f) { MoveTo(way, 0.3f, false); return false; }
        return MoveTo(target, within, false);
    }

    /// <summary>Kapinin disi ve ici (kapi ortasindan 1,8 m).</summary>
    (Vector3 outside, Vector3 inside) GatePoints(PenSystem.Gate g)
    {
        var pen = _d.Pens.Pens.FirstOrDefault();
        var mid = g.Piece.GlobalPosition with { Y = 0 };
        var c = pen != null ? new Vector3(pen.Centroid.X, 0, pen.Centroid.Z) : _d.OcakPos with { Y = 0 };
        var n = g.Piece.GlobalBasis.Z with { Y = 0 };
        n = n.Normalized();
        if ((mid - c).Dot(n) < 0) n = -n; // disa dogru
        return (_d.World.Ground(mid + n * 1.8f), _d.World.Ground(mid - n * 1.8f));
    }

    PenProp PropIn(string id) => _d.Pens.Pens.Count == 0 ? null : _d.Pens.PropsIn(_d.Pens.Pens[0], id).FirstOrDefault();

    /// <summary>
    /// Gidak gelmeden agili donatir (ICERIK 10.2): Folluk (4 Odun + 4 Lif, Lif tutamdan Bicme ile) bati yakasina, Yemlik
    /// (6 Odun) guney ucuna; ikisi de citin disindan konur (hayalet nisan noktasina oturur). Sonra Yemlik'e cantadaki
    /// bugday (E, citin uzerinden). Bitince true.
    /// </summary>
    bool FurnishPen()
    {
        var pen = _d.Pens.Pens.FirstOrDefault();
        if (pen == null) return true;
        float minX = pen.Polygon.Min(v => v.X), minZ = pen.Polygon.Min(v => v.Z);
        var c = pen.Centroid;
        var follukAt = _d.World.Ground(new Vector3(minX + 0.8f, 0, c.Z - 0.6f));
        var follukStand = _d.World.Ground(new Vector3(minX - 1.6f, 0, c.Z - 0.6f));
        var yemlikAt = _d.World.Ground(new Vector3(c.X, 0, minZ + 0.9f));
        var yemlikStand = _d.World.Ground(new Vector3(c.X, 0, minZ - 1.5f));
        if (PropIn("folluk") == null) { if (HaveCost(PlaceableOf("folluk"))) PlacePropAt("folluk", follukAt, follukStand); return false; }
        var y = PropIn("yemlik");
        if (y == null) { if (HaveCost(PlaceableOf("yemlik"))) PlacePropAt("yemlik", yemlikAt, yemlikStand); return false; }
        if (y.Feeder.Amount > 0) return true;
        if (!HaveItem("wheat", 1)) return false;
        Aim(y.GlobalPosition + Vector3.Up * 0.4f);
        if (MoveRouted(yemlikStand, 0.5f) && _menuT <= 0) { _d.Interact(); _menuT = 0.8f; }
        return false;
    }

    /// <summary>Istasyon olmayan yerlestirilebiliri (Yemlik, Folluk) koyar: Insa, tekerlek, durulacak yer, nisan, sol tik.</summary>
    void PlacePropAt(string id, Vector3 at, Vector3 stand)
    {
        _d.Capa.Select(Capa.Head.Insa);
        if (_d.Capa.CurrentPlaceable != id) { if (_stepT > 0.15f) { _d.Capa.CyclePlaceable(1); _stepT = 0; } return; }
        Aim(at);
        if (!MoveRouted(stand, 0.5f)) { _stepT = 0; return; }
        if (_stepT > 1.0f) { FirePressed = true; _stepT = 0; GD.Print($"BOT place {id} at {at}"); }
    }

    void StepGidak(Vector3 o)
    {
        var g = _d.Gidak;
        var gate = PenGate;
        if (!g.Visible || gate == null) { P.MoveOverride = Vector3.Zero; return; }
        if (!g.Tame)
        {
            if (!FurnishPen()) return;
            // Bugday: oyuncunun yolu (E), Gidak'in yanina git.
            if (!HaveItem("wheat", 1)) return;
            Aim(g.GlobalPosition + Vector3.Up * 0.4f);
            // E once en yakin istasyonun menusunu acar (Director.Interact): istasyon erimindeyse Gidak'in obur yanina gec.
            var st = _d.Stations.FirstOrDefault(s => s.InReach(P.GlobalPosition));
            if (st != null) { var away = g.GlobalPosition - st.GlobalPosition; away.Y = 0; MoveTo(g.GlobalPosition + away.Normalized() * 1.5f, 0.4f, false); return; }
            if (MoveTo(g.GlobalPosition, 1.8f, false) && _menuT <= 0) { _d.Interact(); _menuT = 0.8f; }
            return;
        }
        var (outside, inside) = GatePoints(gate);
        if (!gate.Open)
        {
            Aim(gate.Piece.GlobalPosition + Vector3.Up);
            if (MoveRouted(outside, 0.5f) && _menuT <= 0) { _d.Interact(); _menuT = 0.8f; }
            return;
        }
        // Kapidan iceri: once kapinin disina, sonra icine; Gidak izler.
        if (_gidakStage == 0) { Aim(inside + Vector3.Up); if (MoveRouted(outside, 0.5f)) _gidakStage = 1; return; }
        Aim(g.GlobalPosition + Vector3.Up * 0.4f);
        var pen = _d.Pens.Pens.FirstOrDefault();
        var deep = pen != null ? _d.World.Ground(new Vector3(pen.Centroid.X, 0, pen.Centroid.Z)) : inside;
        if (_gidakStage == 1) { if (MoveTo(inside, 0.5f, false)) _gidakStage = 2; return; }
        MoveTo(deep + (inside - deep).Normalized() * 0.8f, 0.5f, false);
    }

    // ---------- 8 Gece: yumurtayi al, agildan cik, kapiyi kapat, sonra savun ----------

    int _exitStage;

    void StepNightPen()
    {
        _menuT -= (float)GetProcessDeltaTime();
        // Acik kalmis menu tetigi yutar (Director.ReadInput): gece boyu ates edilemez, safakta Cekim calismaz. Kapat.
        if (_d.Hud.MenuOpen && !_d.Hud.ReportOpen && _menuT <= 0) { GD.Print("BOT closed stray menu"); _d.Hud.HideMenu(); _menuT = 0.3f; return; }
        var gate = PenGate;
        bool dusk = _d.Phase == Director.Act.Dusk;
        if (gate != null && (dusk || _d.Night?.Phase == NightPhase.Alarm))
        {
            // Folluk'taki yumurta (agilin icinden, E)
            var nest = PropIn("folluk");
            if (nest != null && nest.Nest.Eggs > 0 && _d.Pens.PenAt(P.GlobalPosition) != null)
            {
                var pc = _d.Pens.Pens[0].Centroid;
                var toC = new Vector3(pc.X - nest.GlobalPosition.X, 0, pc.Z - nest.GlobalPosition.Z).Normalized();
                Aim(nest.GlobalPosition + Vector3.Up * 0.6f);
                if (MoveTo(nest.GlobalPosition + toC * 1.2f, 0.4f, false) && _menuT <= 0) { _d.Interact(); _menuT = 0.6f; }
                return;
            }
            var egg = _d.Pens.GroundEggs.FirstOrDefault(e => IsInstanceValid(e));
            if (egg != null)
            {
                Aim(egg.GlobalPosition);
                if (MoveTo(egg.GlobalPosition, 0.8f, false) && _menuT <= 0) { _d.Interact(); _menuT = 0.6f; }
                return;
            }
            var (outside, inside) = GatePoints(gate);
            bool inPen = _d.Pens.PenAt(P.GlobalPosition) != null;
            if (inPen || _exitStage == 1)
            {
                if (_exitStage == 0) { if (MoveTo(inside, 0.5f, false)) _exitStage = 1; return; }
                if (_exitStage == 1) { Aim(outside + Vector3.Up); if (MoveTo(outside, 0.5f, false)) _exitStage = 2; return; }
            }
            if (gate.Open)
            {
                Aim(gate.Piece.GlobalPosition + Vector3.Up);
                if (MoveRouted(outside, 0.6f) && _menuT <= 0) { _d.Interact(); _menuT = 0.8f; }
                return;
            }
        }
        if (gate == null) { StepNight(); return; }
        if (HoldBehindFence()) return;
        NightFight();
    }

    /// <summary>
    /// Agilli ilk gece savunmasi: nobet yerinde durur, menzildeki Zipzip'lerden tarlaya en yakinina ates eder (cit
    /// otekileri tutar; Tirpan Isini citin ustunden gecer). Kovalamaz, kacmaz.
    /// </summary>
    void NightFight()
    {
        PickCard();
        // Nobet yerinden (tarlanin agil yani) ayrilmaz: agilin etrafindan Zipzip kovalamak oyuncuyu yakin dovuse sokup
        // yere seriyor, tarla korumasiz kaliyordu. Menzildekilerden tarlaya en yakini vurulur; citte kalan safakta soner.
        var farm = _d.FarmCenter();
        MoveTo(NightPost(), 1f, false);
        float Dist(Zipzip e) => new Vector2(e.GlobalPosition.X - P.GlobalPosition.X, e.GlobalPosition.Z - P.GlobalPosition.Z).Length();
        var z = Zipzip.All.Where(e => !e.Dead && Dist(e) < 11f).OrderBy(e => e.GlobalPosition.DistanceTo(farm)).FirstOrDefault();
        if (z == null) { Aim(farm + Vector3.Up); return; }
        Aim(z.Center);
        Fire = true;
    }

    /// <summary>Ilk gecenin nobet yeri: tarla merkezinden agila dogru 2,5 m (cit nobetiyle ayni yer).</summary>
    Vector3 NightPost()
    {
        var f = _d.FarmCenter() with { Y = 0 };
        if (_d.Pens.Pens.Count == 0) return _d.World.Ground(f);
        var c = _d.Pens.Pens[0].Centroid;
        return _d.World.Ground(f + new Vector3(c.X - f.X, 0, c.Z - f.Z).Normalized() * 2.5f);
    }

    // Ilk dalgada citin arkasinda (tarla yaninda) bekler: Zipzip yoldaki cite yuklenir, kabarmis Gidak cit ustunden
    // tekmeler (GDD 9.1, 14.3, 24). Yakina geleni vurur. Citte yeterince hucum gorulunce ya da sure dolunca savunmaya gecer.
    const int FenceWatchHits = 6;
    const float FenceWatchSeconds = 40f;
    float _fenceWatchT;
    bool _fenceWatchDone;

    /// <summary>Sunulan karttan secim: ilk kart; --botcard=KIMLIK verilmisse o kart (sinama: or. Catal'la gece).</summary>
    void PickCard()
    {
        var o = _d.Night?.Offer;
        if (o == null || o.Age <= 0.6f) return;
        int i = 0;
        if (Main.Args.TryGetValue("botcard", out var want))
            for (int k = 0; k < o.Cards.Count; k++) if (o.Cards[k].Id == want) { i = k; break; }
        _d.ChooseCard(i);
    }

    bool HoldBehindFence()
    {
        var n = _d.Night;
        if (_fenceWatchDone || n == null || n.Phase == NightPhase.Dawn || _d.Pens.Pens.Count == 0) return false;
        _fenceWatchT += (float)GetProcessDeltaTime();
        if (_d.Pens.FenceHits >= FenceWatchHits || _fenceWatchT > FenceWatchSeconds)
        {
            _fenceWatchDone = true;
            GD.Print($"BOT fence watch done hits={_d.Pens.FenceHits} t={_fenceWatchT:0.0}");
            return false;
        }
        PickCard();
        var c = _d.Pens.Pens[0].Centroid;
        var near = Zipzip.All.Where(e => !e.Dead && e.GlobalPosition.DistanceTo(P.GlobalPosition) < 5f).OrderBy(e => e.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();
        if (near != null) { Aim(near.Center); Fire = true; P.MoveOverride = Vector3.Zero; return true; }
        Aim(new Vector3(c.X, 1f, c.Z));
        MoveTo(NightPost(), 1f, false);
        return true;
    }
}
