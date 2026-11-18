using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Holding sinamasi (--test=holding; holding, M3): gercek oyunu kurar; ilk gercek Kabarma'nin ertesi sabahi kamp kurulur
/// (Sim olaylari dogrudan cagrilir: bu bir sinamadir, bot degildir), sonra Godot katmani denetlenir: kamp ve birim
/// dugumleri, gunduz Perde'si (iter, hasar gecmez), kervan cikisi ve pususu (ganimet cantaya), gece Perde iner,
/// baskin ekibi tapuya girer (Husumet −40) ve kabarik urunu kapar, tasiyici duserse urun yere duser, bekci ates eder,
/// oyuncu baskini (Silo, Cekirdek kilidi, Cekirdek dusunce kamp yikilir), safakta baskin dagilir.
/// Mermiler gercek giristen gecer (Director.HoldingProjectileHit, Capa.Weapons'in cagirdigi yol).
/// Sonuc: "HOLDING RESULT ok=N fail=M"; basarisizlikta cikis kodu 1. Pencereli kosuda goruntuler progress/m3/holding/.
/// </summary>
public partial class HoldingTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    bool _finished;
    string _outDir;
    Camera3D _cam;

    public HoldingTest(Director d) { _d = d; Name = "HoldingTest"; }

    GameState S => _d.State;
    HoldingSystem H => S.Holding;
    void Check(bool cond, string what) { if (cond) _ok++; else _fail++; GD.Print($"HOLDING {(cond ? "OK  " : "FAIL")} {what}"); }
    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    async Task Wait(float s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
    async Task<bool> Until(Func<bool> cond, float timeout) { for (float t = 0; t < timeout; t += 0.1f) { if (cond()) return true; await Wait(0.1f); } return cond(); }

    public override async void _Ready()
    {
        _outDir = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m3/holding/";
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        GetTree().CreateTimer(400, true, false, true).Timeout += () => { GD.Print("HOLDING ZAMAN ASIMI"); _fail++; Finish(); };
        await Frames(10);
        try
        {
            _d.PlaceOcak(_d.World.Ground(Vector3.Zero));
            S.Calendar.Start(S.Data.Calendar.FirstDay);
            S.Calendar.SetTimeOfDay(0.05f);
            await Frames(3);
            var camp = await Found();
            await Perde(camp);
            await Caravan(camp);
            await TheirRaid(camp);
            await PlayerRaid(camp);
            await Dawn(camp);
        }
        catch (Exception e) { _fail++; GD.Print("HOLDING HATA " + e); }
        Finish();
    }

    async Task<HoldingCamp> Found()
    {
        Check(H.FirstCamp == null, "ilk gercek Kabarma'dan once kamp yok");
        H.OnKabarmaEnded(true, S.Calendar.Day);
        H.OnMorning(S.Calendar.Day + 1, S.Research.OcakTier);
        var c = H.FirstCamp;
        Check(c != null, "gercek Kabarma'nin ertesi sabahi kamp kuruldu");
        float dist = new Vector2(c.X - _d.OcakPos.X, c.Z - _d.OcakPos.Z).Length();
        Check(dist >= S.Data.Holding.Camp.MinDistance - 1f, $"kamp Ocak'tan {dist:0} m (>= {S.Data.Holding.Camp.MinDistance})");
        await Frames(5);
        var alive = c.Units.Count(u => u.Alive);
        Check(_d.Holding.CampNode(c.Id) != null && _d.Holding.Units.Count == alive && alive > 0, $"kamp dugumu ve {alive} birim dugumu");
        await View(c, new Vector3(28, 14, 30), "kamp_gunduz.png");
        return c;
    }

    async Task Perde(HoldingCamp c)
    {
        float r = S.Data.Holding.Camp.PerdeRadius;
        _d.Player.GlobalPosition = _d.World.Ground(new Vector3(c.X + 6, 0, c.Z)) + Vector3.Up * 0.3f;
        await Wait(1.5f);
        float d = new Vector2(_d.Player.GlobalPosition.X - c.X, _d.Player.GlobalPosition.Z - c.Z).Length();
        Check(d >= r - 0.6f, $"gunduz Perde oyuncuyu disari itti ({d:0.0} m / {r})");
        float silo = c.Structures["silo"];
        var center = _d.World.Ground(new Vector3(c.X, 0, c.Z)) + Vector3.Up * 2f; // Perde kamp merkezlidir
        bool hit = _d.HoldingProjectileHit(center + new Vector3(0, 0, r + 6f), center + new Vector3(0, 0, r - 2f), 0.3f, 500f);
        Check(hit && c.Structures["silo"] == silo, "gunduz Perde mermiyi durdurur, Silo hasar almaz");
    }

    async Task Caravan(HoldingCamp c)
    {
        int bagOre = S.Backpack.Count("copper_ore");
        S.Calendar.SetTimeOfDay(S.Data.Holding.Caravan.DepartTime + 0.01f);
        Check(await Until(() => c.Caravan != null && c.Caravan.Phase == "out", 3f), "kervan sabah yola cikti");
        var k = c.Caravan;
        await Wait(1f);
        var truck = _d.Holding.Truck(c.Id);
        var start = new Vector2(c.X, c.Z);
        Check(truck != null && truck.Visible && new Vector2(k.X, k.Z).DistanceTo(start) > 1f, "kervan araci depoya ilerliyor");
        // Pusu: araci Perde'nin disina tasi (sinama kisayolu), korumalari yanina al, hepsini vur.
        var depot = new Vector2(S.Data.Holding.Caravan.Depot[0], S.Data.Holding.Caravan.Depot[^1]);
        var mid = start.Lerp(depot, 0.6f);
        k.X = mid.X; k.Z = mid.Y;
        foreach (var a in _d.Holding.Units.Values.Where(a => a.Job == "caravan")) a.GlobalPosition = _d.World.Ground(new Vector3(mid.X + 3, 0, mid.Y + 3));
        _d.Player.GlobalPosition = _d.World.Ground(new Vector3(mid.X + 12, 0, mid.Y)) + Vector3.Up * 0.3f;
        await Frames(4);
        await View(c, new Vector3(10, 7, 12), "kervan.png", new Vector3(mid.X, 0, mid.Y));
        for (int i = 0; i < 40 && !k.Ambushed; i++)
        {
            var target = _d.Holding.Units.Values.FirstOrDefault(a => a.Job == "caravan" && IsInstanceValid(a));
            var p = target != null ? target.Center : _d.Holding.Truck(c.Id).GlobalPosition + Vector3.Up * 1.2f;
            _d.HoldingProjectileHit(p + Vector3.Up * 3f, p, 0.3f, 120f);
            await Frames(2);
        }
        Check(k.Ambushed && H.State.Ambushes == 1, "kervan pususu basarili (korumalar ve arac dustu)");
        Check(S.Backpack.Count("copper_ore") > bagOre, "pusu ganimeti cantada (bakir cevheri)");
        Check(c.Husumet >= S.Data.Holding.Husumet.Ambush, $"pusu Husumet'i artirdi ({c.Husumet:0})");
    }

    async Task TheirRaid(HoldingCamp c)
    {
        c.Husumet = 60;
        var size = S.Data.Crops.SizeClasses.First();
        var crop = new SwollenCrop("turnip", size, new MeshKit().Sphere(0.4f, MeshKit.At(0, 0.4f, 0), Palette.TurnipBody).Instance(), null, 1f);
        _d.AddChild(crop);
        crop.GlobalPosition = _d.World.Ground(_d.OcakPos + new Vector3(6, 0, 4));
        _d.Player.GlobalPosition = _d.World.Ground(_d.OcakPos + new Vector3(-40, 0, -40)) + Vector3.Up * 0.3f; // uzakta: ekip ona takilmasin
        _d.HoldingNight(true);
        await Frames(3);
        var raid = _d.Holding.Units.Values.Where(a => a.Job == "raid").ToList();
        Check(!H.PerdeUp(c), "gece Perde indi");
        Check(raid.Count == S.Data.Holding.Raid.BaseCount + c.Level / 2, $"baskin ekibi geldi ({raid.Count})");
        Check(await Until(() => c.RaidEntered, 40f), "baskin ekibi tapuya girdi");
        Check(c.Husumet <= 60 + S.Data.Holding.Husumet.TheyRaided + 0.01f, $"onlarin baskini Husumet'i dusurdu ({c.Husumet:0})");
        Check(await Until(() => crop.Held, 30f), "tasiyici kabarik urunu kapti");
        await View(c, new Vector3(10, 8, 12), "baskin.png", _d.OcakPos);
        var carrier = _d.Holding.Units.Values.FirstOrDefault(a => a.Carried == crop);
        for (int i = 0; i < 20 && carrier != null && IsInstanceValid(carrier) && carrier.IsInsideTree() && _d.Holding.Units.ContainsKey(carrier.UnitId); i++)
        {
            _d.HoldingProjectileHit(carrier.Center + Vector3.Up * 3f, carrier.Center, 0.3f, 150f);
            await Frames(2);
        }
        await Frames(3);
        Check(IsInstanceValid(crop) && !crop.Held, "tasiyici dusunce urun yere dustu");
        // Bekci ates eder: oyuncu bir baskinci yaninda.
        var shooter = _d.Holding.Units.Values.FirstOrDefault(a => a.Job == "raid" && a.Def.Damage > 0 && IsInstanceValid(a));
        if (shooter != null)
        {
            float hp = _d.Player.Hp;
            _d.Player.GlobalPosition = shooter.GlobalPosition + new Vector3(5, 0.3f, 0);
            await Wait(3f);
            Check(_d.Player.Hp < hp || _d.Player.Down, $"baskinci oyuncuya ates etti ({hp:0} -> {_d.Player.Hp:0})");
        }
        else Check(false, "ates edecek baskinci yok");
    }

    async Task PlayerRaid(HoldingCamp c)
    {
        _d.Player.GlobalPosition = _d.World.Ground(new Vector3(c.X + 34, 0, c.Z)) + Vector3.Up * 0.3f;
        int credits = S.Credits;
        float hus = c.Husumet;
        var core = CoreCenter(c);
        float coreHp = c.Structures["cekirdek"];
        _d.HoldingProjectileHit(core + Vector3.Up * 4f, core, 0.3f, 100f);
        Check(c.Structures["cekirdek"] == coreHp, "Cekirdek oteki binalar ayaktayken kilitli");
        Check(c.Husumet >= hus + S.Data.Holding.Husumet.NightRaid - 0.01f && H.State.PlayerRaids == 1, "gece baskini: Husumet +25, sayac sifir");
        await Smash(c, "silo");
        Check(c.Structures["silo"] <= 0 && S.Credits >= credits + S.Data.Holding.Loot["silo"].Credits * c.Level, $"Silo yikildi, Kredi ganimeti ({S.Credits - credits})");
        int cores = 0;
        Events.HoldingCoreDestroyed += _ => cores++;
        await Smash(c, "atolye");
        Check(S.Backpack.Count("holding_plani") >= 1, "Atolye ganimeti: Holding Plani");
        await Smash(c, "ahir");
        Check(H.State.Depot.GetValueOrDefault("nadir_hayvan") >= 1, "Ahir ganimeti: nadir hayvan depoda");
        await Smash(c, "cekirdek");
        Check(c.Destroyed && cores == 1 && S.Backpack.Count("holding_parcasi") >= 2, "Cekirdek dustu: kamp yikildi, Holding Parcasi");
        await Frames(3);
        Check(_d.Holding.Units.Count == 0, "yikik kampta birim kalmadi");
        await View(c, new Vector3(26, 14, 28), "kamp_yikik.png");
    }

    async Task Smash(HoldingCamp c, string id)
    {
        for (int i = 0; i < 60 && c.Structures[id] > 0; i++)
        {
            var t = _d.Holding.CampNode(c.Id).Targets(c).FirstOrDefault(x => x.id == id);
            if (t.id == null) break;
            _d.HoldingProjectileHit(t.center + Vector3.Up * 5f, t.center, 0.3f, 150f);
            await Frames(1);
        }
    }

    async Task Dawn(HoldingCamp c)
    {
        _d.OnHoldingDawn();
        await Frames(3);
        Check(H.State.NightOpen == false && !_d.Holding.Units.Values.Any(a => a.Job == "raid"), "safakta Perde kalkti, baskin dagildi");
        Check(c.RebuildDay > S.Calendar.Day, $"yikik kamp gun {c.RebuildDay} yeniden kurulacak");
    }

    Vector3 SiloCenter(HoldingCamp c) => _d.Holding.CampNode(c.Id).Targets(c).First(t => t.id == "silo").center;
    Vector3 CoreCenter(HoldingCamp c) => _d.Holding.CampNode(c.Id).Targets(c).First(t => t.id == "cekirdek").center;

    async Task View(HoldingCamp c, Vector3 off, string file, Vector3? at = null)
    {
        if (DisplayServer.GetName() == "headless") return;
        var look = _d.World.Ground(at ?? new Vector3(c.X, 0, c.Z));
        _cam ??= new Camera3D { Fov = 55f };
        if (_cam.GetParent() == null) _d.AddChild(_cam);
        _cam.GlobalPosition = look + off;
        _cam.LookAt(look + Vector3.Up * 2f);
        _cam.MakeCurrent();
        await Frames(6);
        GetViewport().GetTexture().GetImage().SavePng(_outDir + file);
        GD.Print($"HOLDING SHOT {file}");
        _cam.ClearCurrent();
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        GD.Print($"HOLDING RESULT ok={_ok} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }
}
