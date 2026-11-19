using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Tasima sinamasi (--test=tasima; tasima, M4): gercek oyunda Tezgah'ta gercek tariflerle uretilen parcalarla
/// (1) Sandik -> Kol -> 3 Bant -> Sandik hatti kurulur, kablolu guc ve sinyal telli Salter Kolu ile durdurulup
/// baslatilir; bandin getirdigi Bakir Kulce Sandik'tan istasyon kuyrugunu besler (Tezgah: Civi);
/// (2) Drone Istasyonu iki Sandik arasinda yuk tasir (6 drone, iz, 80 m). Sonuc: "TASIMA RESULT ok=N fail=M".
/// DURUST: sahibe raporlanan GEVSETILEN adimlar (Relax): Ocak basamagi dogrudan T5 (Ata yolu), T1-T4 ara urunleri ve
/// Biyoyakit esya olarak verilir (Damitici ve Sazlik/Kanyon yolu daha yok), Tezgah ve parcalar PlaceStation/Power.Place ile
/// kurulur (kemer hayaleti degil), kablo Sim'den cekilir (1 Kablo harcanir), Kol filtresi dugume yazilir (E paneli degil).
/// Pencereli kosuda goruntuler progress/m4/tasima/.
/// </summary>
public partial class TasimaTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    bool _finished;
    string _outDir;
    readonly List<string> _relaxed = new();

    public TasimaTest(Director d) { _d = d; Name = "TasimaTest"; }

    GameState S => _d.State;
    PowerNet Net => S.Power;
    PowerFile F => _d.Data.Power;
    void Check(bool cond, string what) { if (cond) _ok++; else _fail++; GD.Print($"TASIMA {(cond ? "OK  " : "FAIL")} {what}"); }
    void Relax(string what) { if (!_relaxed.Contains(what)) { _relaxed.Add(what); GD.Print("TASIMA GEVSETILDI " + what); } }
    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    async Task Wait(float s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
    async Task<bool> Until(Func<bool> cond, float timeout) { for (float t = 0; t < timeout; t += 0.1f) { if (cond()) return true; await Wait(0.1f); } return cond(); }

    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.Single(p => p.Id == id);
    Vector3 G(float x, float z) => _d.World.Ground(new Vector3(x, 0, z));

    public override async void _Ready()
    {
        _outDir = Main.Args.TryGetValue("test_out", out var o) ? o.TrimEnd('/', '\\') + "/" : ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\') + "/../progress/m4/tasima/";
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        GetTree().CreateTimer(420, true, false, true).Timeout += () => { GD.Print("TASIMA ZAMAN ASIMI"); _fail++; Finish(); };
        await Frames(10);
        try
        {
            _d.PlaceOcak(G(0, 0));
            S.Calendar.Start(S.Data.Calendar.FirstDay);
            S.Calendar.SetTimeOfDay(0.1f);
            await Frames(3);
            await Build();
            await BeltLine();
            await FeedStation();
            await Signal();
            await Robot();
            await Drones();
            await Persist();
        }
        catch (Exception e) { _fail++; GD.Print("TASIMA HATA " + e); }
        Finish();
    }

    // Duzen: Ocak yakininda tam sayi koordinatlar (bant 1 m karoya oturur).
    float _bx, _bz;
    Station _tezgah;
    PowerDevice _gen, _hub, _arm, _lever, _drone;
    Furniture _chestA, _chestB, _chestC, _chestD;
    readonly List<PowerDevice> _belts = new();

    async Task<bool> HandCraft(string id, int count = 1)
    {
        var r = _d.Data.Tool.RecipeFor(id)!;
        int before = S.Backpack.Count(id);
        if (!_d.Craft(r, count)) { GD.Print($"TASIMA el tarifi kuyruga girmedi: {id}"); return false; }
        return await Until(() => S.Backpack.Count(id) >= before + count * r.Out, 20f + r.Seconds * count);
    }

    async Task<bool> StationCraft(string id, int count = 1)
    {
        var r = _d.Data.Tool.RecipeFor(id)!;
        int before = _d.StationStore().Count(id);
        for (int i = 0; i < count; i++) if (!await Until(() => _tezgah.Queue.Enqueue(r, _d.StationStore()), 10f)) { GD.Print($"TASIMA istasyon tarifi kuyruga girmedi: {id}"); return false; }
        return await Until(() => _d.StationStore().Count(id) >= before + count * r.Out, 20f + r.Seconds * count * 1.5f);
    }

    async Task Build()
    {
        var o = _d.OcakPos;
        _bx = Mathf.Round(o.X) + 10; _bz = Mathf.Round(o.Z) + 10;
        _tezgah = _d.PlaceStation(Def("tezgah"), G(_bx - 6, _bz - 6), 0);
        Relax("Tezgah PlaceStation ile kuruldu (M1 botu gercek yolunu sinar)");
        Relax("Ocak basamagi dogrudan T5 (Ata yolu: boss akisi)");
        S.Research.SetOcakTier(5);
        // Girdiler: T1-T4 ara urunleri (zincir testleri uretimi sinar) ve Biyoyakit.
        foreach (var (item, n) in new[] { ("celik", 16), ("copper_wire", 22), ("kaucuk", 5), ("fiber", 4), ("wood", 48), ("plank", 2),
                                           ("titan_ingot", 10), ("devre", 8), ("plastik", 6), ("aku_hucresi", 2), ("biyoyakit", 3) })
        {
            S.Backpack.Add(item, n);
        }
        Relax("celik, copper_wire, kaucuk, fiber, wood, plank, titan_ingot, devre, plastik, aku_hucresi ve biyoyakit verildi (ara urun zincirleri ayri sinanir)");
        // Parcalar gercek tarifle uretilir.
        Check(await StationCraft("kablo", 3), "Kablo uretildi (Tezgah, 3 is x 2)");
        Check(await StationCraft("biyoyakit_jeneratoru"), "Biyoyakit Jeneratoru uretildi (Ocak T3 tarifi)");
        Check(await StationCraft("tasima_kolu"), "Kol uretildi");
        Check(await StationCraft("bant"), "4 Bant uretildi (tek is)");
        Check(await StationCraft("salter_kolu"), "Salter Kolu uretildi");
        Check(await StationCraft("drone_istasyonu"), "Drone Istasyonu uretildi (Ocak T5 tarifi)");
        Check(await HandCraft("direk", 2), "2 Direk uretildi (el tarifi)");
        Check(await HandCraft("sandik", 4), "4 Sandik uretildi (el tarifi)");
        Check(S.Backpack.Count("bant") == 4 && S.Backpack.Count("kablo") == 6, "uretilen parcalar cantada: 4 Bant, 6 Kablo");
        // Kurulum (kemer hayaleti degil: Power.Place / PlaceFurniture).
        Relax("parcalar Power.Place / PlaceFurniture ile kuruldu (kemer hayaleti ve R donusu Capa.Insa testinde)");
        _chestA = _d.PlaceFurniture(Def("sandik"), G(_bx, _bz - 1), 0); S.Backpack.Remove("sandik", 1);
        _arm = _d.Power.Place(Def("tasima_kolu"), G(_bx, _bz), 0); S.Backpack.Remove("tasima_kolu", 1);
        for (int i = 1; i <= 3; i++) { _belts.Add(_d.Power.Place(Def("bant"), G(_bx, _bz + i), 0)); S.Backpack.Remove("bant", 1); }
        _chestB = _d.PlaceFurniture(Def("sandik"), G(_bx, _bz + 4), 0); S.Backpack.Remove("sandik", 1);
        _gen = _d.Power.Place(Def("biyoyakit_jeneratoru"), G(_bx + 4, _bz - 2), 0); S.Backpack.Remove("biyoyakit_jeneratoru", 1);
        _hub = _d.Power.Place(Def("direk"), G(_bx + 3, _bz), 0); S.Backpack.Remove("direk", 1);
        _lever = _d.Power.Place(Def("salter_kolu"), G(_bx - 2, _bz + 2), 0); S.Backpack.Remove("salter_kolu", 1);
        await Frames(3);
        bool cables = Cable(_gen, _hub) && Cable(_hub, _arm);
        Check(cables, "guc kablosu: Jenerator -> Direk -> Kol (2 Kablo harcandi)");
        // Yakit: biyoyakit esyasi jeneratore (E paneli yolu: Op.Refuel, burada Sim'e dogrudan).
        S.Backpack.Remove("biyoyakit", 1);
        Check(Net.Refuel(F, _gen.NodeId, 1) > 0, "Jenerator Biyoyakit ile beslendi (120 sn)");
        Net.Get(_arm.NodeId)!.Filter = "copper_ingot";
        Relax("Kol filtresi dugume yazildi (E paneli Op.Filter yolu Godot'ta ayri denetlenmedi)");
        await Frames(10);
        Check(Net.Get(_arm.NodeId)!.Powered, "Kol beslendi (Jenerator 250 W, Kol 10 W)");
    }

    bool Cable(PowerDevice a, PowerDevice b, bool signal = false)
    {
        if (S.Backpack.Count("kablo") < 1) return false;
        var l = Net.Connect(F, a.NodeId, b.NodeId, signal);
        if (l == null) { GD.Print($"TASIMA kablo cekilemedi {a.Def.Id} -> {b.Def.Id}: {Net.CanConnect(F, a.NodeId, b.NodeId, signal)}"); return false; }
        S.Backpack.Remove("kablo", 1);
        return true;
    }

    async Task BeltLine()
    {
        _chestA.Storage.Add("copper_ingot", 12);
        int seen = 0;
        bool arrived = await Until(() => { seen = Math.Max(seen, _d.Transport.ShownItems); return _chestB.Storage.Count("copper_ingot") >= 3; }, 40f);
        Check(arrived, $"Kol + Bant: Sandik A'dan Sandik B'ye {_chestB.Storage.Count("copper_ingot")} Bakir Kulce ulasti");
        Check(seen > 0, $"bantta giden nesneler gorunur (en cok {seen})");
        await Shot("bant_hatti.png", new Vector3(_bx + 5, 4.5f, _bz + 1.5f), new Vector3(_bx, 0.5f, _bz + 1.5f));
        await Until(() => _chestB.Storage.Count("copper_ingot") >= 12, 40f);
        Check(_chestB.Storage.Count("copper_ingot") == 12 && _chestA.Storage.Count("copper_ingot") == 0, "12 Bakir Kulce kaybolmadan tasindi");
    }

    async Task FeedStation()
    {
        // Bantin getirdigi Sandik'taki Bakir Kulce istasyonu besler (ambardan uretim, cantada Bakir Kulce yok).
        Check(S.Backpack.Count("copper_ingot") == 0, "cantada Bakir Kulce yok");
        int civiBefore = _d.StationStore().Count("civi");
        var r = _d.Data.Tool.RecipeFor("civi")!;
        bool queued = _tezgah.Queue.Enqueue(r, _d.StationStore());
        Check(queued && _chestB.Storage.Count("copper_ingot") == 11, "Tezgah Civi isini Sandik B'deki (bantin getirdigi) Kulce'den kuyruga aldi");
        bool made = await Until(() => _d.StationStore().Count("civi") >= civiBefore + 20, 20f);
        Check(made, "istasyon bantin getirdigiyle 20 Civi uretti");
    }

    async Task Signal()
    {
        // Salter Kolu'nun sinyal teli ortadaki banda: kapaliyken akis durur.
        Check(Cable(_lever, _belts[1], true), "sinyal teli: Salter Kolu -> orta Bant (1 Kablo)");
        _chestA.Storage.Add("copper_ingot", 6);
        var ln = Net.Get(_lever.NodeId)!;
        ln.On = false;
        await Wait(1f);
        int b0 = _chestB.Storage.Count("copper_ingot");
        await Wait(6f);
        Check(_chestB.Storage.Count("copper_ingot") == b0, $"kol kapaliyken Bant durur ({b0} -> {_chestB.Storage.Count("copper_ingot")})");
        ln.On = true;
        Check(await Until(() => _chestB.Storage.Count("copper_ingot") >= b0 + 6, 30f), "kol acilinca akis surer, 6 Kulce daha ulasti");
    }

    /// <summary>Hasat Robotu (ICERIK §11): arastirilir, Tezgah'ta uretilir, kablolanir; olgun ekini Sandiga toplar.</summary>
    async Task Robot()
    {
        Relax("Veri verildi (Hasat Robotu arastirmasi); celik, copper_wire, kaucuk ek olarak verildi; ekin buyumesi Sim'de hizlandirildi (Farm.Tick)");
        S.Research.AddVeri(200, "test");
        foreach (var (item, n) in new[] { ("celik", 10), ("copper_wire", 10), ("kaucuk", 4) }) S.Backpack.Add(item, n);
        Check(!_tezgah.Queue.IsUnlocked(_d.Data.Tool.RecipeFor("hasat_robotu")!), "Hasat Robotu tarifi arastirilmadan kilitli");
        Check(S.Research.Research("hasat_robotu", true), "Hasat Robotu arastirildi (Arastirma Masasi kurali)");
        Check(await StationCraft("hasat_robotu"), "Hasat Robotu uretildi (Tezgah)");
        float sx = _bx + 8, sz = _bz - 8;
        var st = _d.PlaceStation(Def("hasat_robotu"), G(sx, sz), 0); S.Backpack.Remove("hasat_robotu", 1);
        await Frames(4);
        var port = _d.Power.PortOf(st);
        Check(port != null && port.Watts == 60f, "Hasat Robotu 60 W'lik guc ucu acti");
        var link = port == null ? null : Net.Connect(F, _hub.NodeId, port.NodeId, false);
        Check(link != null && S.Backpack.Remove("kablo", 1), "guc kablosu: Direk -> Hasat Robotu");
        var chest = _d.PlaceFurniture(Def("sandik"), G(sx + 3, sz), 0);
        int cx = Mathf.FloorToInt(sx), cz = Mathf.FloorToInt(sz);
        chest.Storage.Add("turnip_seed", 3);
        var farm = S.Farm; var rng = S.Rng;
        for (int i = 0; i < 3; i++) { farm.Till(cx - 2 + i, cz + 3); Check(farm.Plant(cx - 2 + i, cz + 3, "turnip", chest.Storage, rng), $"Sandiktaki tohum ekildi ({i + 1}/3)"); }
        for (float t = 0; t < 400f; t += 0.5f) { foreach (var p in farm.Plots) if (p.Planted && p.Moisture < 60) farm.Water(p.X, p.Y); farm.Tick(0.5f, true); }
        Check(farm.Plots.Count(p => p.Planted && p.Stage == CropStage.Mature) == 3, "3 ekin olgun");
        bool got = await Until(() => chest.Storage.Count("turnip") >= 3, 40f);
        Check(got && farm.Plots.All(p => !p.Planted || p.Stage != CropStage.Mature), $"robot 3 ekini hasat edip Sandiga koydu ({chest.Storage.Count("turnip")} Salgam, {chest.Storage.Count("turnip_seed")} tohum)");
        Check(chest.Storage.Count("turnip_seed") >= 1, "hasatta tohum da geldi");
    }

    async Task Drones()
    {
        float sx = _bx + 6, sz = _bz + 14;
        _drone = _d.Power.Place(Def("drone_istasyonu"), G(sx, sz), 0); S.Backpack.Remove("drone_istasyonu", 1);
        var hub2 = _d.Power.Place(Def("direk"), G(_bx + 5, _bz + 8), 0); S.Backpack.Remove("direk", 1);
        // Jeneratorden drone istasyonuna: Direk -> Direk -> Istasyon.
        bool wired = Cable(_hub, hub2) && Cable(hub2, _drone);
        Check(wired, "guc: Direk -> Direk -> Drone Istasyonu");
        _chestC = _d.PlaceFurniture(Def("sandik"), G(sx + 28, sz), 0);
        _chestD = _d.PlaceFurniture(Def("sandik"), G(sx - 30, sz + 12), 0);
        await Frames(3);
        var drones = Net.Transport.Drones;
        string far = drones.AddOrder(Net, F, _drone.NodeId, new DroneOrder { Item = "stone", Fx = sx + 28, Fz = sz, Tx = sx - 130, Tz = sz });
        Check(far != null, "80 m disindaki hedef reddedildi: " + far);
        string err = drones.AddOrder(Net, F, _drone.NodeId, new DroneOrder { Item = "stone", Fx = sx + 28, Fz = sz, Tx = sx - 30, Tz = sz + 12 });
        Check(err == null, "drone gorevi eklendi (28 m ve 32 m) " + err);
        _chestC.Storage.Add("stone", 40);
        bool flew = false; int shownMax = 0;
        bool done = await Until(() =>
        {
            flew |= drones.Station(_drone.NodeId).Fleet.Any(x => x.Phase is 1 or 2);
            shownMax = Math.Max(shownMax, _d.Transport.ShownDrones);
            return _chestD.Storage.Count("stone") >= 40;
        }, 120f);
        Check(flew, "drone'lar ucustu");
        Check(done && _chestC.Storage.Count("stone") == 0, $"40 Tas kaybolmadan Sandik C'den D'ye tasindi ({_chestD.Storage.Count("stone")}/40)");
        Check(shownMax == 6, $"6 drone gorunur ({shownMax})");
        // Ucus sirasinda goruntu.
        _chestC.Storage.Add("stone", 24);
        await Until(() => drones.Station(_drone.NodeId).Fleet.Count(x => x.Phase == 2) >= 2, 20f);
        await Shot("drone_ucus.png", new Vector3(sx - 2, 12, sz + 18), new Vector3(sx - 2, 2, sz + 4));
    }

    async Task Persist()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(Net);
        var back = System.Text.Json.JsonSerializer.Deserialize<PowerNet>(json)!;
        Check(back.Nodes.Count == Net.Nodes.Count && back.Transport.Drones.Stations.Count == 1 && back.Get(_arm.NodeId)!.Filter == "copper_ingot", "tasima durumu kayitta gidip geliyor (Sim JSON; tam kayit/yukleme save testinde)");
        await Frames(1);
        Relax("Ag (ortak oyun) denetlenmedi: tasima host'ta isler, 'power' bolumu yeni alanlari tasir ama 4 kisilik sinama yok");
    }

    async Task Shot(string file, Vector3 from, Vector3 look)
    {
        if (DisplayServer.GetName() == "headless") return;
        var cam = new Camera3D { Current = true, Fov = 55f };
        AddChild(cam);
        cam.GlobalPosition = from; cam.LookAt(look);
        await Frames(6);
        GetViewport().GetTexture().GetImage().SavePng(_outDir + file);
        GD.Print("TASIMA SHOT " + file);
        cam.QueueFree();
        await Frames(2);
    }

    void Finish()
    {
        if (_finished) return;
        _finished = true;
        GD.Print($"TASIMA GEVSETILEN ADIMLAR ({_relaxed.Count}): " + string.Join("; ", _relaxed));
        string line = $"TASIMA RESULT ok={_ok} fail={_fail}";
        GD.Print(line);
        GetTree().Quit(_fail == 0 && _ok > 0 ? 0 : 1);
    }
}
