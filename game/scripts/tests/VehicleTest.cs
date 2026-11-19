using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arac sinamasi (--test=vehicles, M4 arac akisi): gercek oyunu (Director) kurar; Arac Atolyesi'nden Traktor uretilir,
/// yakit (Biyoyakit iki kat) ve eklenti menu eylemleriyle girer, binilir ve gercek girdi eylemleriyle surulur; Pulluk
/// tapuda tarla surer, Ekim Makinesi eker, Su Tankeri sular; Hava Yastikli gol uzerinde hizini korur (Cekirge suda yavaslar);
/// Zeplin Hangari'ndan Zeplin cikar, kalkar, yukseklikte ucar, alcalip hangara park eder; Kamyon kasa yukler; Balon yavas dusurur.
/// Sinama oldugu icin malzeme dogrudan verilir, istasyon gucu gevsetilir (guc kurali guc akisinin testinde). Bot degildir.
/// Sonuc: "VEHICLE RESULT ok=N fail=M"; basarisizlikta cikis kodu 1.
/// </summary>
public partial class VehicleTest : Node
{
    readonly Director _d;
    int _ok, _fail;
    public VehicleTest(Director d) { _d = d; Name = "VehicleTest"; }

    void Check(bool cond, string what)
    {
        if (cond) _ok++; else _fail++;
        GD.Print($"VEHICLE {(cond ? "OK  " : "FAIL")} {what}");
    }

    async Task Wait(float s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
    async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    Inventory Bag => _d.State.Backpack;
    /// <summary>Canta 20 yuva: sinama malzemeyi dogrudan verdigi icin her adimdan once bosaltir.</summary>
    void EmptyBag() => Bag.Load(Array.Empty<(string id, int count, int stars)>());
    Vector3 G(float x, float z) => _d.World.Ground(_d.OcakPos + new Vector3(x, 0, z));
    PlaceableDef Def(string id) => _d.Data.Tool.Placeables.Single(p => p.Id == id);
    VehicleNode Find(string id) => _d.Vehicles.LastOrDefault(v => IsInstanceValid(v) && v.Def.Id == id);

    public override async void _Ready()
    {
        GetTree().CreateTimer(420, true, false, true).Timeout += () => { GD.Print("VEHICLE ZAMAN ASIMI"); _fail++; Finish(); };
        await Frames(10);
        try
        {
            _d.PlaceOcak(_d.World.Ground(new Vector3(0, 0, 0)));
            _d.State.Research.SetOcakTier(5); // arac tarifleri ocak:2-5 ile acilir (Ata basamagi; test dogrudan verir)
            await Frames(2);
            await Production();
            await FuelAttachAndDrive();
            await FarmWork();
            await HoverWaterAndGlide();
            await Zeppelin();
            await Crates();
        }
        catch (Exception e) { _fail++; GD.Print("VEHICLE HATA " + e); }
        Finish();
    }

    /// <summary>Arac Atolyesi: Traktor ve Romork uretilir; arac yuk olarak istasyonun onune cikar, cantaya girmez.</summary>
    async Task Production()
    {
        var st = _d.PlaceStation(Def("arac_atolyesi"), G(-10, 12), 0);
        await Frames(30); // guc sistemi ucu kendisi baglar; sonra gevset
        st.Queue.PowerOk = () => true; // GEVSETILDI guc: Arac Atolyesi 60 W ister
        var r = st.Queue.Recipes.FirstOrDefault(x => x.Id == "traktor");
        Check(r != null && r.Load && st.Queue.Recipes.Any(x => x.Id == "romork") && st.Queue.Recipes.Any(x => x.Id == "cekirge"), "Arac Atolyesi tarifleri (Traktor, Romork, Cekirge)");
        if (r == null) return;
        foreach (var (k, n) in r.In) Bag.Add(k, n);
        int before = _d.Vehicles.Count;
        Check(st.Queue.Enqueue(r, Bag), "Traktor siraya");
        for (int i = 0; i < 6 && _d.Vehicles.Count == before; i++) { st.Queue.Tick(200f, _d.StationStore()); await Frames(2); }
        var t = Find("traktor");
        Check(t != null && Bag.Count("traktor") == 0, "Traktor dunyada, cantaya girmedi");
        Check(t != null && t.State.Fuel == 0 && t.State.Hp == t.Def.Hp, "yeni arac depo bos, tam can");
        EmptyBag();
        var ro = st.Queue.Recipes.First(x => x.Id == "pulluk");
        foreach (var (k, n) in ro.In) Bag.Add(k, n);
        Check(st.Queue.Enqueue(ro, Bag), "Pulluk siraya");
        for (int i = 0; i < 6 && Bag.Count("pulluk") == 0; i++) { st.Queue.Tick(200f, _d.StationStore()); await Frames(2); }
        Check(Bag.Count("pulluk") == 1, "Pulluk cantaya girdi (eklenti esya)");
    }

    /// <summary>Yakit ve eklenti menu eylemleri; binme; gercek girdi eylemleriyle surus.</summary>
    async Task FuelAttachAndDrive()
    {
        var t = Find("traktor");
        if (t == null) { Check(false, "traktor yok"); return; }
        _d.Player.GlobalPosition = t.GlobalPosition + new Vector3(1.8f, 0.2f, 0);
        Bag.Add("biyoyakit", 3);
        _d.DoVehicleAct(t, "fuel", "biyoyakit");
        Check(Bag.Count("biyoyakit") == 0 && Mathf.Abs(t.State.Fuel - 3 * t.Def.UnitSeconds * 2f) < 1f, $"Biyoyakit iki kat verim (depo {t.State.Fuel:0} sn)");
        Bag.Add("charcoal", 2);
        float f0 = t.State.Fuel; _d.DoVehicleAct(t, "fuel", "charcoal");
        Check(t.State.Fuel == f0, "yanlis yakit girmez");
        _d.DoVehicleAct(t, "attach", "romork");
        Check(!t.State.Attachments.Contains("romork"), "cantada olmayan eklenti takilmaz");
        _d.DoVehicleAct(t, "attach", "pulluk");
        Check(t.State.Attachments.Contains("pulluk") && Bag.Count("pulluk") == 0 && t.FindChild("Att_pulluk", true, false) != null, "Pulluk takildi (gorunum dahil)");
        _d.DoVehicleAct(t, "detach", "pulluk");
        Check(Bag.Count("pulluk") == 1 && t.State.Attachments.Count == 0, "Pulluk cikarilip cantaya dondu");
        _d.DoVehicleAct(t, "attach", "pulluk");

        // Gercek girdi eylemleriyle: bin, W ile ilerle, A/D ile don.
        Check(_d.BoardVehicle(t) && _d.Riding == t, "binildi");
        var start = t.GlobalPosition;
        Input.ActionPress("move_forward");
        await Wait(2.5f);
        Input.ActionRelease("move_forward");
        float dist = new Vector2(t.GlobalPosition.X - start.X, t.GlobalPosition.Z - start.Z).Length();
        Check(dist > 4f && t.State.Fuel < 3 * t.Def.UnitSeconds * 2f, $"W ile yol aldi ({dist:0.0} m), yakit yandi");
        Check(_d.Player.GlobalPosition.DistanceTo(t.Seat) < 0.5f, "oyuncu koltukta");
        float yaw = t.State.Yaw;
        Input.ActionPress("move_forward"); Input.ActionPress("move_left");
        await Wait(1.2f);
        Input.ActionRelease("move_forward"); Input.ActionRelease("move_left");
        Check(Mathf.Abs(t.State.Yaw - yaw) > 0.2f, "direksiyon dondu");
        await Wait(1.5f);
        _d.LeaveVehicle();
        Check(_d.Riding == null && t.State.Riders.Count == 0, "inildi");
    }

    /// <summary>Traktoru tapudaki seride baslat, eklentiyi tak, W ile saniye boyu sur (inince biter).</summary>
    async Task RunStrip(VehicleNode t, Vector3 at, string attachment, float seconds)
    {
        t.State.Attachments.Clear(); t.State.Attachments.Add(attachment);
        t.State.X = at.X; t.State.Z = at.Z; t.State.Yaw = 0; t.State.Speed = 0; t.State.Fuel = 500; t.State.Water = attachment == "su_tankeri" ? 200 : 0;
        t.RefreshAttachments(); t.Place();
        _d.BoardVehicle(t);
        Input.ActionPress("move_forward");
        await Wait(seconds);
        Input.ActionRelease("move_forward");
        _d.LeaveVehicle();
    }

    /// <summary>Pulluk tarla surer, Ekim Makinesi eker, Su Tankeri sular (tapuda, aracin arkasindaki seritte).</summary>
    async Task FarmWork()
    {
        var t = Find("traktor");
        if (t == null) return;
        EmptyBag();
        var farm = _d.State.Farm;
        var at = G(18, -14); // tapunun icinde, Ocak'tan ~23 m
        int plots0 = farm.Plots.Count();
        await RunStrip(t, at, "pulluk", 3.5f);
        Check(_d.VehicleTilled >= 6 && farm.Plots.Count() >= plots0 + 6, $"Pulluk tarla surdu ({_d.VehicleTilled} kare, {farm.Plots.Count() - plots0} yeni)");
        Bag.Add("turnip_seed", 60);
        await RunStrip(t, at, "ekim_makinesi", 3.5f);
        int planted = farm.Plots.Count(p => p.Crop != null);
        Check(_d.VehicleSown >= 4 && planted >= 4, $"Ekim Makinesi ekti ({_d.VehicleSown} kare, {planted} ekili)");
        await RunStrip(t, at, "su_tankeri", 3.5f);
        Check(_d.VehicleWatered >= 4 && t.State.Water < 200, $"Su Tankeri suladi ({_d.VehicleWatered} kare, kalan {t.State.Water})");
    }
    async Task HoverWaterAndGlide()
    {
        var lake = _d.World.LakeCenter;
        var hov = _d.Data.Vehicle("hava_yastikli"); var cek = _d.Data.Vehicle("cekirge");
        var a = _d.SpawnVehicle(hov, Kabarma.Sim.Vehicles.New(hov, 901, lake.X, lake.Z, 0)); a.State.Riders.Add(1); a.State.Fuel = 999;
        var b = _d.SpawnVehicle(cek, Kabarma.Sim.Vehicles.New(cek, 902, lake.X, lake.Z + 0.01f, 0)); b.State.Riders.Add(1); b.State.Fuel = 999;
        Check(a.Surface(lake.X, lake.Z) == "water", "gol merkezinde zemin 'water'");
        for (int i = 0; i < 60; i++) { a.Drive(new VehicleInput { Throttle = 1 }, 0.1f); b.Drive(new VehicleInput { Throttle = 1 }, 0.1f); a.State.X = lake.X; a.State.Z = lake.Z; b.State.X = lake.X; b.State.Z = lake.Z; }
        Check(a.State.Speed > hov.MaxSpeed * 0.9f && b.State.Speed < cek.MaxSpeed * 0.3f, $"Hava Yastikli suda {a.State.Speed:0.0} m/sn, Cekirge {b.State.Speed:0.0} m/sn");
        a.State.Riders.Clear(); b.State.Riders.Clear();
        // Balon: dususu yavaslatir.
        var kam = _d.Data.Vehicle("kamyon");
        var k = _d.SpawnVehicle(kam, Kabarma.Sim.Vehicles.New(kam, 903, 50, 50, 0)); k.State.Y = _d.World.HeightAt(50, 50) + 30f;
        k.State.Attachments.Add("balon"); k.RefreshAttachments();
        float minVy = 0;
        for (int i = 0; i < 600 && k.State.Y > _d.World.HeightAt(50, 50) + 0.4f; i++) { k.Fall(0.05f); minVy = Mathf.Min(minVy, k.State.Vy); }
        Check(minVy >= -_d.Data.Vehicles.Attachment("balon")!.Params["glide"] - 0.01f && k.State.Hp == kam.Hp, $"Balon suzulur (en cok {minVy:0.0} m/sn), hasar yok");
    }

    /// <summary>Zeplin Hangari uretir ve parklar; Zeplin kalkar, yukseklikte ucar, alcalip park eder.</summary>
    async Task Zeppelin()
    {
        EmptyBag();
        var h = _d.PlaceStation(Def("zeplin_hangari"), G(-24, -20), 0);
        await Frames(30); // guc sistemi ucu kendisi baglar; sonra gevset
        h.Queue.PowerOk = () => true; // GEVSETILDI guc: Zeplin Hangari 200 W ister
        var r = h.Queue.Recipes.FirstOrDefault(x => x.Id == "zeplin");
        Check(r != null && r.Load, "Zeplin Hangari Zeplin tarifi");
        if (r == null) return;
        foreach (var (k, n) in r.In) Bag.Add(k, n);
        Check(h.Queue.Enqueue(r, Bag), "Zeplin siraya");
        for (int i = 0; i < 8 && Find("zeplin") == null; i++) { h.Queue.Tick(300f, _d.StationStore()); await Frames(2); }
        var z = Find("zeplin");
        Check(z != null && z.State.Parked && z.State.Altitude == 0, "Zeplin hangarda park halinde cikti");
        if (z == null) return;
        z.State.Fuel = 600;
        _d.Player.GlobalPosition = z.GlobalPosition + new Vector3(2f, 0.2f, 0);
        Check(_d.BoardVehicle(z), "Zeplin'e binildi");
        Input.ActionPress("sprint");
        await Wait(3f);
        Input.ActionRelease("sprint");
        float ground = _d.World.HeightAt(z.State.X, z.State.Z);
        Check(z.State.Altitude > 10f && z.GlobalPosition.Y > ground + 10f && !z.State.Parked, $"Zeplin kalkti ({z.State.Altitude:0.0} m)");
        _d.LeaveVehicle();
        Check(_d.Riding == z, "havadayken inilmez");
        Input.ActionPress("move_forward");
        await Wait(5f);
        Input.ActionRelease("move_forward");
        Check(z.State.Speed > z.Def.TaxiSpeed + 0.8f, $"havada tam hiz ({z.State.Speed:0.0} m/sn)");
        // Hangarin ustune don, alcal.
        z.State.X = h.GlobalPosition.X + 4; z.State.Z = h.GlobalPosition.Z; z.State.Speed = 0;
        Input.ActionPress("jump");
        await Wait(6f);
        Input.ActionRelease("jump");
        Check(z.State.Altitude < 1f, $"alcaldi ({z.State.Altitude:0.0} m)");
        _d.LeaveVehicle();
        Check(_d.Riding == null && z.State.Parked, "Zeplin hangara park etti");
    }

    /// <summary>Kamyon: 6 kasa kapasitesi, yukleme ve indirme (kasa dugumleri aracin ustune oturur).</summary>
    async Task Crates()
    {
        EmptyBag();
        var kam = _d.Data.Vehicle("kamyon");
        var k = _d.SpawnVehicle(kam, Kabarma.Sim.Vehicles.New(kam, 904, G(10, 14).X, G(10, 14).Z, 0));
        var nodes = new List<CargoCrateNode>();
        for (int i = 0; i < 7; i++)
        {
            Bag.Add("egg", 1); _d.State.Cargo.Pack(Bag, "egg", 1); _d.State.Cargo.Close();
            var c = _d.State.Cargo.TakeClosed();
            if (c == null) break;
            var n = new CargoCrateNode(_d, c); _d.AddChild(n); n.GlobalPosition = k.GlobalPosition + new Vector3(1.5f, 0.5f, i * 0.1f);
            nodes.Add(n);
        }
        await Frames(2);
        _d.DoVehicleAct(k, "loadcrates", "");
        Check(nodes.Count == 7 && k.State.Crates.Count == 6, $"Kamyon 6 kasa yukledi ({k.State.Crates.Count}/{nodes.Count})");
        Check(nodes.Count(n => n.GetParent() == k) == 6, "yuklu kasa dugumleri aracin cocugu");
        _d.DoVehicleAct(k, "unloadcrates", "");
        Check(k.State.Crates.Count == 0 && nodes.All(n => n.GetParent() != k), "kasalar indirildi");
    }

    void Finish()
    {
        GD.Print($"VEHICLE RESULT ok={_ok} fail={_fail}");
        GetTree().Quit(_fail == 0 ? 0 : 1);
    }
}
