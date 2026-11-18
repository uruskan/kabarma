using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// --test=savashayvan (M3 savas-hayvan, GDD 9.5): hayvan botu ilk gunu ve ikinci turu (Pofuduk, sut) durustce bitirir;
/// sonra bu bilesen sinamasi gercek girislerle Kabarma'da hayvanlari ve bineki sinar:
///   1. Savas Agili Bayragi agila konur (PenSystem.PlaceProp), Pofuduk'a Savasci rolu (panelin SetRole'u).
///   2. Homur: Eyerlik'in E eylemi eyer takar (RanchSystem.PropAction), panelin "Bin"i (MountUp), oyuncu yurur, binek
///      altinda tasinir ve yol sayilir; Baglama Diregi yaninda inilince baglanir.
///   3. Ahir Zili Hu'yu cagirir; kapali kapida duvar onu durdurur (evcil WildCreature duvar kirpmasi), kapi acilinca
///      girer ve kayit olur.
///   4. Kucuk Kabarma (F1 yolu, RestartKucukKabarma): Pofuduk kabarir ve savas govdesi alir, bagli Homur saklanir;
///      gelistirici vurusu (Ranch.Hit) Pofuduk'u bayiltir; safakta agilinda yorgun uyanir.
/// GELISTIRICI KISAYOLLARI (bot degil, bilesen sinamasi; raporda yazili): Homur ve Dalli'nin guveni 1 yapilir (yemleri
/// kabak ve bogurtlen henuz dunyada alinamiyor), cantaya 1 eyer konur (Deri uzun aralikli urun), bayiltan vurus.
/// Sonuc: "SAVASHAYVAN ok ..." ya da "RESULT fail savashayvan ...", cikis 0/1. --savashots ile progress/m3/savas-hayvan/.
/// </summary>
public partial class SavasHayvanTest : Node
{
    readonly Director _d;
    enum S { Setup, Ride, Bell, Night, Dawn, Done }
    S _s = S.Setup;
    float _st, _total;
    readonly List<string> _fails = new(), _notes = new();
    HusbandryAgent _pof, _homur, _dalli;
    Vector3 _rideStart;
    float _baseSpeed;
    int _rideStage, _bellStage, _nightStage;
    PenShape _pen;
    bool _stoppedAtWall;
    float _diagT;

    public SavasHayvanTest(Director d) { _d = d; Name = "SavasHayvanTest"; ProcessPriority = 1000; /* botun (Done sonrasi MoveOverride = 0) ardindan */ }

    RanchSystem R => _d.Ranch;
    Ranch Sim => _d.State.Ranch;
    Player P => _d.Player;

    void Say(string s) => GD.Print("SAVASHAYVAN " + s);
    void Check(bool ok, string what) { if (!ok) _fails.Add(what); Say($"{(ok ? "ok" : "FAIL")} {what}"); }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _st += dt; _total += dt;
        if (_s == S.Done) return;
        if (_total > 900f) { _fails.Add($"zaman asimi ({_s})"); Finish(); return; }
        if (_d.Hud.MenuOpen && _s != S.Night && _s != S.Dawn) _d.Hud.HideMenu();
        switch (_s)
        {
            case S.Setup: Setup(); break;
            case S.Ride: Ride(dt); break;
            case S.Bell: Bell(); break;
            case S.Night: Night(); break;
            case S.Dawn: Dawn(); break;
        }
    }

    void Go(S s) { _s = s; _st = 0; Say($"state {s} t={_total:0.0}"); }

    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.First(p => p.Id == id);

    PenProp Place(string id, Vector3 at) => _d.Pens.PlaceProp(Def(id), _d.World.Ground(at), 0f);

    HusbandryAgent Find(string species) => R.Agents.Where(a => IsInstanceValid(a) && !a.Passive && a.Species.Id == species && a.Animal.AgeDays >= 1)
        .OrderBy(a => a.Host.GlobalPosition.DistanceTo(P.GlobalPosition)).FirstOrDefault();

    void Setup()
    {
        if (_st < 1f || _d.Hud.ReportOpen || _d.NightActive) return;
        _pen = _d.Pens.Pens.FirstOrDefault(p => p.HasGate);
        _pof = Find("pofuduk");
        _homur = Find("homur");
        _dalli = Find("hu"); // zil: agilda yer olan tur (18 m2: Gidak 4 + Pofuduk 9 + Hu 4)
        if (_pen == null || _pof == null || _homur == null || _dalli == null) { _fails.Add($"kurulum: pen={_pen != null} pof={_pof != null} homur={_homur != null} dalli={_dalli != null}"); Finish(); return; }
        Say($"actors homur={_homur.Host.GetType().Name} dalli={_dalli.Host.GetType().Name} pof={_pof.Host.GetType().Name}");
        // 1. Savas Agili
        var c = new Vector3(_pen.Centroid.X, 0, _pen.Centroid.Z);
        var flag = Place("savas_agili_bayragi", c + new Vector3(0.6f, 0, -0.6f));
        Check(flag?.Flag != null && Sim.IsWarPen(_pen), "bayrak agili Savas Agili yapar");
        Check(Sim.Rules.SetRole(_pof.Animal, "savasci"), "Pofuduk Savasci");
        Check(_pof.Animal.Registered && Sim.Fights(_pof.Animal, _d.Pens.Pens), "Pofuduk savasa hazir");
        // 2. Homur: gelistirici evcillestirmesi (kabak dunyada yok), Eyerlik'ten eyer
        _homur.Animal.Trust = 1f;
        Check(Sim.Rules.SetRole(_homur.Animal, "binek"), "Homur Binek");
        _d.State.Backpack.Add("eyer", 1); // GELISTIRICI: Deri uzun aralikli urun
        var rack = Place("eyerlik", _homur.Host.GlobalPosition + new Vector3(1.2f, 0, 0));
        var (act, label) = R.PropAction(rack);
        Check(act != null, $"Eyerlik eylemi ({label})");
        act?.Invoke();
        Check(_homur.Animal.Saddled && _d.State.Backpack.Count("eyer") == 0, "eyer takildi");
        Go(S.Ride);
    }

    void Ride(float dt)
    {
        switch (_rideStage)
        {
            case 0:
                // Homur'un yanina yuru, sonra panelin "Bin"i
                var to = _homur.Host.GlobalPosition - P.GlobalPosition; to.Y = 0;
                if (to.Length() > 2f && _st < 20f) { P.MoveOverride = to.Normalized(); return; }
                P.MoveOverride = Vector3.Zero;
                _baseSpeed = P.SpeedMult;
                R.MountUp(_homur);
                Check(R.Riding && _homur.Animal.Rider != 0, "Homur'a binildi");
                Check(P.SpeedMult > _baseSpeed * 1.05f, $"binek hizi (carpan {P.SpeedMult:0.00})");
                _rideStart = P.GlobalPosition;
                _rideStage = 1; _st = 0;
                return;
            case 1:
                // 5 sn duz yuru (agildan uzaga)
                // agildan acik kapidan disari, 25 m uzaga (duvarin etrafindan yol: PenSystem.Route)
                var dest = _rideStart + (_rideStart - new Vector3(_pen.Centroid.X, _rideStart.Y, _pen.Centroid.Z)).Normalized() * 25f;
                var way = _d.Pens.Route(P.GlobalPosition, dest, 0.45f) - P.GlobalPosition; way.Y = 0;
                P.MoveOverride = way.LengthSquared() > 0.01f ? way.Normalized() : Vector3.Zero;
                _diagT -= dt;
                if (_diagT <= 0) { _diagT = 1f; Say($"diag ride p={P.GlobalPosition} v={P.Velocity.Length():0.00} mo={P.MoveOverride} lock={P.MovementLocked} ui={P.UiCapture} down={P.Down} phys={P.IsPhysicsProcessing()} mode={P.ProcessMode} paused={GetTree().Paused} speed={P.SpeedMult:0.00} floor={P.IsOnFloor()}"); }
                if (_st < 8f) return;
                P.MoveOverride = Vector3.Zero;
                float moved = new Vector2(P.GlobalPosition.X - _rideStart.X, P.GlobalPosition.Z - _rideStart.Z).Length();
                float gap = new Vector2(P.GlobalPosition.X - _homur.Host.GlobalPosition.X, P.GlobalPosition.Z - _homur.Host.GlobalPosition.Z).Length();
                Check(moved > 8f, $"binekle yol ({moved:0.0} m)");
                Check(gap < 0.6f, $"binek altinda ({gap:0.00} m)");
                Check(_d.State.Orders.Stats.GetValueOrDefault("mount_m") >= (int)(moved * 0.8f), $"mount_m olcusu ({_d.State.Orders.Stats.GetValueOrDefault("mount_m")})");
                Place("baglama_diregi", P.GlobalPosition + P.GlobalBasis.X * 1.5f);
                _rideStage = 2; _st = 0;
                Shot("binek");
                return;
            default:
                if (_st < 0.5f) return;
                R.Dismount();
                Check(!R.Riding && Mathf.IsEqualApprox(P.SpeedMult, _baseSpeed), "inildi, hiz geri");
                Check(_homur.Animal.PostId != 0 && _homur.Animal.Command == "bekle", "direge baglandi");
                Go(S.Bell);
                return;
        }
    }

    void Bell()
    {
        var gate = _d.Pens.Gates.FirstOrDefault(g => g.Leaves.Count > 0);
        switch (_bellStage)
        {
            case 0:
                if (gate != null) _d.Pens.SetGate(gate, false);
                _dalli.Animal.Trust = 1f; // GELISTIRICI: Hu yumurtadan (Kulucka) evcillesir, kanca zincir akisinda
                _dalli.Animal.Registered = false;
                var bellAt = new Vector3(_pen.Centroid.X, 0, _pen.Centroid.Z) + new Vector3(-0.8f, 0, 0.8f);
                var bell = Place("ahir_zili", bellAt);
                // Dalli zilin yaricapinda (30 m) ama agilin disinda
                var outside = _d.World.Ground(bellAt + (new Vector3(14, 0, 6)));
                _dalli.Host.GlobalPosition = outside; _dalli.Animal.X = outside.X; _dalli.Animal.Z = outside.Z;
                var (act, label) = R.PropAction(bell);
                Check(act != null && label == "Zili çal", "zil eylemi");
                act?.Invoke();
                Check(_dalli.Animal.Called, "Hu cagrildi");
                _bellStage = 1; _st = 0;
                return;
            case 1:
                // kapali kapi: duvarin onunde durur (evcil WildCreature duvar kirpmasi)
                if (_d.Pens.PenAt(_dalli.Host.GlobalPosition) != null) { Check(false, "kapali kapidan gecti"); _bellStage = 2; _st = 0; return; }
                if (_st < 12f) return;
                _stoppedAtWall = _d.Pens.PenAt(_dalli.Host.GlobalPosition) == null;
                Check(_stoppedAtWall, "kapali agil duvari durdurdu");
                if (gate != null) _d.Pens.SetGate(gate, true);
                _bellStage = 2; _st = 0;
                return;
            default:
                _diagT -= (float)GetProcessDeltaTime();
                if (_diagT <= 0) { _diagT = 5f; Say($"diag bell hu={_dalli.Host.GlobalPosition} called={_dalli.Animal.Called} tame={_dalli.Animal.Tame} reg={_dalli.Animal.Registered} driven={_dalli.Driven} gate={gate?.Open} inpen={_d.Pens.PenAt(_dalli.Host.GlobalPosition) != null} call=({_dalli.Animal.CallX:0.0},{_dalli.Animal.CallZ:0.0}) cmd={_dalli.Animal.Command} hidden={_dalli.Animal.Hidden}"); }
                if (!_dalli.Animal.Registered && _st < 90f) return;
                Check(_dalli.Animal.Registered && !_dalli.Animal.Called, $"zille agila dondu ve kayit oldu ({_st:0} sn)");
                if (gate != null) _d.Pens.SetGate(gate, false);
                Go(S.Night);
                return;
        }
    }

    void Night()
    {
        switch (_nightStage)
        {
            case 0:
                if (_d.Hud.MenuOpen) _d.Hud.HideMenu();
                if (!_d.RestartKucukKabarma()) { if (_st > 10f) { Check(false, "Kucuk Kabarma baslamadi"); Finish(); } return; }
                _nightStage = 1; _st = 0;
                return;
            case 1:
                if (!_d.NightActive || _st < 2f) return;
                Check(Sim.AtWar, "Kabarma: savas basladi");
                Check(_pof.Animal.War != null && _pof.Animal.War.Team == CombatTeam.Player, $"Pofuduk kabardi (can {_pof.Animal.War?.MaxHp:0})");
                Check(_homur.Animal.Hidden && !_homur.Host.Visible, "direkteki Homur saklandi");
                Check(_dalli.Animal.Hidden, "ahirdaki (Savasci olmayan) Hu saklandi");
                _nightStage = 2; _st = 0;
                return;
            case 2:
                if (_st < 6f) return;
                Shot("kabarma");
                // GELISTIRICI: bayiltan vurus (dalga sozlesmesi gelene kadar hasar temas kuralindan da gelir)
                var ev = new List<WarEvent>();
                Sim.Hit(_pof.Animal, _d.Data.Combat, new CombatHit(99999, DamageType.Darbe, CombatTeam.Wild), ev);
                Check(_pof.Animal.Fainted && ev.Any(e => e.Kind == WarEventKind.Fainted), "Pofuduk bayildi");
                Check(Sim.Animals.Contains(_pof.Animal), "bayilan olmedi");
                Say($"kills={_d.State.Orders.Stats.GetValueOrDefault("animal_kills")} (bilgi: dalga agilin yanina gelirse)");
                Go(S.Dawn);
                return;
        }
    }

    void Dawn()
    {
        if (_d.NightActive || _st < 1f) { if (_st > 400f) { Check(false, "safak gelmedi"); Finish(); } return; }
        if (_st < 2f) return;
        var a = _pof.Animal;
        Check(!a.Fainted && a.TiredDawns == 1 && a.War == null, "safakta yorgun uyandi");
        Check(new Vector2(a.X - _pen.Centroid.X, a.Z - _pen.Centroid.Z).Length() < 0.5f, "agilinda uyandi");
        Check(!Sim.Fights(a, _d.Pens.Pens), "yorgun savasmaz");
        Check(_homur.Host.Visible && !_homur.Animal.Hidden, "saklanan geri geldi");
        Finish();
    }

    void Shot(string name)
    {
        if (!Main.Args.ContainsKey("savashots")) return;
        var dir = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../progress/m3/savas-hayvan";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
        GD.Print($"SCREENSHOT {dir}/{name}.png");
    }

    void Finish()
    {
        _s = S.Done;
        P.MoveOverride = null;
        if (_fails.Count == 0) { GD.Print($"SAVASHAYVAN ok checks t={_total:0.0}"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(0); }
        else { GD.Print($"RESULT fail savashayvan {string.Join(" | ", _fails)}"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(1); }
    }
}
