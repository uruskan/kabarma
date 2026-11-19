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
        GetTree().CreateTimer(600, true, false, true).Timeout += () => { GD.Print("HOLDING ZAMAN ASIMI"); _fail++; Finish(); };
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
            await Four(); // M4 (holding4): 2. ve 3. kamp, Genel Merkez, Mudur
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

    // ---------------------------------------------------------------- M4 (holding4)

    CombatHit Hit(float amount) => new(amount, DamageType.Darbe, CombatTeam.Player);
    MudurPart Part(string id) => _d.Holding.Encounter?.Parts.FirstOrDefault(p => p.Part == id);
    float Do(string id, float amount) => Part(id) is { } p && p.Alive ? p.TakeHit(Hit(amount), _d.Player.GlobalPosition).Damage : 0f;

    /// <summary>
    /// 2. kamp (Ocak T3, Isil Sazlik), 3. kamp (T4, Kizil Kanyon), Genel Merkez (T5, krater eteginde); GM yalniz gece girilir; Cekirdek
    /// dusunce Mudur: avlu (kamyon, motor kapagi, Mekanik onarimi), yonetim kati (jeneratorler, hoparlorler, Ustabasi), cati (balonlar),
    /// zafer (odul, acilanlar). Vurus silah yolundan (IAtaTarget.TakeHit); GECICI: kamyon govdesi koddan (arac akisi gelene dek).
    /// </summary>
    async Task Four()
    {
        int day = S.Calendar.Day + 10;
        H.OnMorning(day, 3); await Frames(6);
        var c2 = H.State.Camps.FirstOrDefault(x => x.Slot == 2);
        Check(c2 != null && S.BiomeMap.BiomeAt(c2.X, c2.Z) == "isil_sazlik" && _d.Holding.CampNode(c2.Id) != null, "2. kamp (Ocak T3) Isil Sazlik'ta kuruldu ve dugumu var");
        Check(c2 != null && !_d.World.IsWater(new Vector3(c2.X, 0, c2.Z)), "2. kamp suda degil");
        H.OnMorning(day + 1, 4); await Frames(6);
        var c3 = H.State.Camps.FirstOrDefault(x => x.Slot == 3);
        Check(c3 != null && S.BiomeMap.BiomeAt(c3.X, c3.Z) == "kizil_kanyon" && _d.Holding.CampNode(c3.Id) != null, "3. kamp (Ocak T4) Kizil Kanyon'da kuruldu ve dugumu var");
        H.OnMorning(day + 2, 5); await Frames(6);
        var hq = H.State.Camps.FirstOrDefault(x => x.Slot == 4);
        Check(hq != null && hq.Kind == "hq" && hq.Level == 10 && _d.Holding.CampNode(hq.Id) != null, "Genel Merkez (Ocak T5) kuruldu, seviye 10");
        if (hq == null) return;
        Check(H.PerdeUp(hq), "Genel Merkez gunduz Perde'li (girilmez)");
        H.OnNightStart(true);
        Check(!H.PerdeUp(hq), "Kabarma gecesi Genel Merkez girilir");
        foreach (var id in new[] { "silo", "atolye", "ahir", "cekirdek" }) H.HitStructure(hq.Id, id, 99999, out _);
        await Frames(8);
        var enc = _d.Holding.Encounter;
        Check(H.State.Mudur != null && enc != null && enc.Stage == 0, "Cekirdek dustu: Mudur avlu asamasi basladi");
        Check(Part("kasa") != null && Part("motor") != null && enc.Truck != null, "kamyon: kasa ve motor kapagi hedefleri");
        var p0 = enc.Truck.GlobalPosition; await Wait(0.8f);
        Check(enc.Truck.GlobalPosition.DistanceTo(p0) > 1f, "kamyon avluda doner");
        Check(H.MechanicsAlive(hq.Id) >= 2, "Mekanikler avluda");
        await Wait(2.2f); // kalkan
        var f = H.State.Mudur;
        float body = Do("kasa", 100), motor = Do("motor", 100);
        Check(body > 0 && motor > body * 5, $"motor kapagi zayif nokta (kasa {body:0}, motor {motor:0})");
        f.TruckHp = f.TruckMax * 0.5f; float hp0 = f.TruckHp; await Wait(2f);
        Check(f.TruckHp > hp0, $"Mekanikler kamyonu onariyor ({hp0:0} -> {f.TruckHp:0})");
        Do("motor", 100000); await Frames(6);
        Check(f.Stage == 1 && Part("jenerator:0") != null && Part("hoparlor:1") != null, "yonetim kati: jeneratorler ve hoparlorler");
        await Wait(2.4f);
        Check(Part("mudur") is { Alive: false }, "jeneratorler kapanmadan Mudur vurulamaz");
        Check(MudurRules.UnitSpeedMult(S.Data.Holding, f) > 1f, "hoparlor birimleri hizlandirir");
        Do("hoparlor:0", 1000); Do("hoparlor:1", 1000);
        Check(MudurRules.UnitSpeedMult(S.Data.Holding, f) == 1f, "hoparlorler susturuldu");
        int ust = hq.Units.Count(u => u.Kind == "ustabasi"); f.CallT = 0.05f; await Wait(0.4f);
        Check(hq.Units.Count(u => u.Kind == "ustabasi") > ust, "Mudur Ustabasi cagirdi");
        for (int i = 0; i < 4; i++) Do($"jenerator:{i}", 1000);
        Check(Part("mudur") is { Alive: true }, "jeneratorler kapaninca Mudur vurulur");
        Do("mudur", f.BossHp - f.BossMax * 0.4f + 1f); await Frames(8);
        Check(f.Stage == 2 && Part("balon:5") != null, "cati: zeplin ve 6 balon");
        await Wait(2.4f);
        Check(Part("mudur") is { Alive: false }, "balonlar inmeden Mudur vurulamaz");
        for (int i = 0; i < 6; i++) Do($"balon:{i}", 1000);
        Check(MudurRules.BalloonsLeft(f) == 0 && Part("mudur") is { Alive: true }, "balonlar patladi, Mudur vurulur");
        int plan = S.Backpack.Count("holding_plani");
        Do("mudur", 100000); await Frames(8);
        Check(H.State.MudurDefeats == 1 && H.State.Mudur == null && hq.Cleared, "Mudur yenildi, Genel Merkez temizlendi");
        Check(S.Backpack.Count("holding_plani") >= plan + 3 && H.State.Unlocks.Contains("head:yercekimi") && H.State.Unlocks.Contains("cosmetic:kask_holding_muduru"), "odul: Efsane planlari, Yercekimi basligi, kask ve kaplama acildi");
        await Frames(4);
        Check(_d.Holding.Encounter == null, "Mudur sahnesi kalkti");
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
