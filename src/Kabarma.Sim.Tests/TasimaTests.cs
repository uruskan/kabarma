using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>
/// M4 tasima (ICERIK §12; GDD 11.1-11.3): kalan guc, sinyal, kapi ve tasima parcalari. Hepsi Sim; Godot'a dayanmaz.
/// Depolar sahte dunyadan (TestWorld) gelir.
/// </summary>
public class TasimaTests
{
    sealed class TestWorld : ITransportWorld
    {
        public readonly List<(float x, float z, Inventory inv)> Stores = new();
        public Inventory Add(float x, float z, int slots = 20)
        {
            var inv = new Inventory(slots, id => 50);
            Stores.Add((x, z, inv));
            return inv;
        }
        public IItemStore? StoreAt(float x, float z, float radius)
        {
            foreach (var s in Stores) if ((s.x - x) * (s.x - x) + (s.z - z) * (s.z - z) <= radius * radius) return s.inv;
            return null;
        }
    }

    static PowerFile Fixed(float watts = 5000)
    {
        var f = TestData.Load().Power;
        f.Devices.Add(new PowerDeviceDef { Id = "sabit", Kind = "generator", Source = "fixed", Watts = watts, Sockets = 8 });
        return f;
    }

    static void Run(PowerNet net, PowerFile f, float seconds, float dt = 0.05f)
    {
        for (float t = 0; t < seconds; t += dt) net.Tick(f, dt, dt / 3600f);
    }

    static PowerNode Add(PowerNet net, PowerFile f, string id, float x, float z, float yaw = 0) { var n = net.Add(f, id, x, 0, z); n.Yaw = yaw; return n; }
    static void Wire(PowerNet net, PowerFile f, PowerNode a, PowerNode b, bool signal = false) => Assert.NotNull(net.Connect(f, a.Id, b.Id, signal));

    /// <summary>ICERIK §12: 43 parca (Kablo esya, Elektrik Lambasi M2'den: sayi ayni kalir).</summary>
    static readonly string[] Parts43 =
    {
        "hayvan_carki", "ruzgar_gulu", "su_carki", "biyoyakit_jeneratoru", "gunes_paneli", "cizbiz_tanki", "kabarma_toplayicisi", "maya_reaktoru",
        "aku_mk1", "aku_mk2", "aku_mk3", "aku_mk4", "kablo", "direk", "salter", "sigorta_kutusu",
        "dugme", "salter_kolu", "basinc_plakasi", "hareket_sensoru", "gun_gece_sensoru", "kabarma_sensoru", "depo_sensoru", "guc_sensoru", "nem_sensoru", "sayac", "zamanlayici",
        "kapi_ve", "kapi_veya", "kapi_degil", "karsilastirici", "bellek", "siren", "sinyal_lambasi",
        "bant", "ayirici", "birlestirici", "tasima_kolu", "boru", "pompa", "drone_istasyonu", "teleferik_istasyonu", "teleferik_diregi",
    };

    [Fact]
    public void Part_count_matches_plan_7_1_and_every_part_is_craftable()
    {
        var d = TestData.Load();
        Assert.Equal(43, Parts43.Length);
        Assert.Equal(43, Parts43.Distinct().Count());
        foreach (var id in Parts43)
        {
            Assert.True(id == "kablo" || d.Power.IsDevice(id), "power.json'da yok: " + id);
            Assert.NotNull(d.Tool.RecipeFor(id));
            if (id != "kablo") Assert.Contains(d.Tool.Placeables, p => p.Id == id); // Kablo esyadir, yerlestirilmez
        }
        // power.json: 43 parca - Kablo (esya) + Elektrik Lambasi (M2) = 43 cihaz
        Assert.Equal(43, d.Power.Devices.Count);
        // Hepsi tarif grafinda ulasilir (Ocak T6'da)
        var g = RecipeGraph.Walk(d, 6);
        foreach (var id in Parts43) Assert.True(g.Has(id), "tarif grafinda ulasilamaz: " + id);
        Assert.Equal(1000f, d.Power.Def("kabarma_toplayicisi")!.Watts);
        Assert.Equal(30000f, d.Power.Def("aku_mk4")!.CapacityWh);
        Assert.Equal(6, d.Power.Def("drone_istasyonu")!.Drones);
        Assert.Equal(80f, d.Power.Def("drone_istasyonu")!.Range);
        Assert.Equal(0.5f, d.Power.Def("bant")!.Gap); // saniyede 2 nesne
    }

    // ---------------- ureticiler ve aku ----------------

    [Fact]
    public void Solar_follows_the_sun_and_the_kabarma_collector_only_works_in_kabarma()
    {
        var f = TestData.Load().Power;
        var net = new PowerNet();
        var sun = net.Add(f, "gunes_paneli", 0, 0, 0);
        var col = net.Add(f, "kabarma_toplayicisi", 4, 0, 0);
        var pole = net.Add(f, "direk", 2, 0, 0);
        Wire(net, f, sun, pole); Wire(net, f, col, pole);
        net.Sun = 1f; net.KabarmaLevel = 0; net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(60f, sun.OutputW); Assert.Equal(0f, col.OutputW);
        net.Sun = 0f; net.KabarmaLevel = 40; net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(0f, sun.OutputW); Assert.Equal(1000f, col.OutputW);
    }

    [Fact]
    public void Biofuel_generator_burns_fuel_and_stops_when_empty()
    {
        var f = TestData.Load().Power;
        var net = new PowerNet();
        var gen = net.Add(f, "biyoyakit_jeneratoru", 0, 0, 0);
        Wire(net, f, gen, net.Add(f, "direk", 3, 0, 0));
        net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(0f, gen.OutputW);
        Assert.Equal(120f, net.Refuel(f, gen.Id, 1));
        net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(250f, gen.OutputW);
        for (int i = 0; i < 1300; i++) net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(0f, gen.OutputW);
        Assert.Equal(0f, gen.Fuel);
    }

    [Fact]
    public void Fuse_box_sets_the_priority_of_what_hangs_on_it()
    {
        var f = Fixed(10);
        var net = new PowerNet();
        var g = net.Add(f, "sabit", 0, 0, 0);
        var pole = net.Add(f, "direk", 2, 0, 0);
        var fuse = net.Add(f, "sigorta_kutusu", 4, 0, 0); fuse.Priority = 1;
        var a = net.Add(f, "sinyal_lambasi", 4, 0, 3);    // 2 W, kutuda: oncelik 1
        var b = net.Add(f, "siren", 5, 0, 0);             // 10 W, oncelik 3
        var c = net.Add(f, "elektrik_lambasi", 6, 0, 0);  // 5 W, oncelik 3
        Wire(net, f, g, pole); Wire(net, f, pole, fuse); Wire(net, f, fuse, a); Wire(net, f, pole, b); Wire(net, f, pole, c);
        net.Tick(f, 0.1f, 0.001f);
        Assert.True(a.Powered);              // 2 W: sigorta onceligi
        Assert.True(c.Powered && !b.Powered); // kalan 8 W: lamba (5) sigar, siren (10) kapanir
    }

    // ---------------- sinyal: kaynaklar ve kapilar ----------------

    static (PowerNet net, PowerFile f) SignalNet() => (new PowerNet(), Fixed());

    [Fact]
    public void Gates_follow_their_truth_tables()
    {
        var (net, f) = SignalNet();
        var a = Add(net, f, "salter_kolu", 0, 0); var b = Add(net, f, "salter_kolu", 1, 0);
        var and = Add(net, f, "kapi_ve", 2, 0); var or = Add(net, f, "kapi_veya", 2, 1); var not = Add(net, f, "kapi_degil", 2, 2);
        Wire(net, f, a, and, true); Wire(net, f, b, and, true);
        Wire(net, f, a, or, true); Wire(net, f, b, or, true);
        Wire(net, f, a, not, true);
        foreach (var (av, bv) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            a.On = av; b.On = bv; net.Tick(f, 0.1f, 0.001f);
            Assert.Equal(av && bv, and.Signal > 0);
            Assert.Equal(av || bv, or.Signal > 0);
            Assert.Equal(!av, not.Signal > 0);
        }
    }

    [Fact]
    public void Gates_chain_and_compare_orders_its_inputs()
    {
        var (net, f) = SignalNet();
        var lever = Add(net, f, "salter_kolu", 0, 0);
        var not1 = Add(net, f, "kapi_degil", 1, 0); var not2 = Add(net, f, "kapi_degil", 2, 0);
        Wire(net, f, lever, not1, true); Wire(net, f, not1, not2, true);
        lever.On = true; net.Tick(f, 0.1f, 0.001f);
        Assert.True(not2.Signal > 0 && not1.Signal == 0); // iki DEGIL = ayni deger
        // Karsilastirici: birinci tel > ikinci tel
        var depoA = Add(net, f, "depo_sensoru", 0, 3); var depoB = Add(net, f, "nem_sensoru", 1, 3);
        var cmp = Add(net, f, "karsilastirici", 2, 3);
        Wire(net, f, depoA, cmp, true); Wire(net, f, depoB, cmp, true);
        depoA.Reading = 70; depoB.Reading = 40; net.Tick(f, 0.1f, 0.001f); Assert.True(cmp.Signal > 0);
        depoA.Reading = 40; depoB.Reading = 70; net.Tick(f, 0.1f, 0.001f); Assert.Equal(0, cmp.Signal);
    }

    [Fact]
    public void Memory_latches_set_and_reset_and_counter_and_timer_work()
    {
        var (net, f) = SignalNet();
        var set = Add(net, f, "salter_kolu", 0, 0); var reset = Add(net, f, "salter_kolu", 1, 0);
        var mem = Add(net, f, "bellek", 2, 0);
        Wire(net, f, set, mem, true); Wire(net, f, reset, mem, true);
        reset.On = false; set.On = true; net.Tick(f, 0.1f, 0.001f); Assert.True(mem.Signal > 0);
        set.On = false; net.Tick(f, 0.1f, 0.001f); Assert.True(mem.Signal > 0); // tutar
        reset.On = true; net.Tick(f, 0.1f, 0.001f); Assert.Equal(0, mem.Signal);
        reset.On = false; net.Tick(f, 0.1f, 0.001f); Assert.Equal(0, mem.Signal);

        // Sayac: 5 kenar sonra 100
        var btn = Add(net, f, "dugme", 0, 5); var cnt = Add(net, f, "sayac", 1, 5);
        Wire(net, f, btn, cnt, true);
        for (int i = 0; i < 4; i++) { net.Press(f, btn.Id); Run(net, f, 1.2f); }
        Assert.Equal(4, cnt.Count); Assert.Equal(0, cnt.Signal);
        net.Press(f, btn.Id); Run(net, f, 1.2f);
        Assert.Equal(5, cnt.Count); Assert.True(cnt.Signal > 0);

        // Zamanlayici: 10 sn dongude 2 sn acik
        var tm = Add(net, f, "zamanlayici", 0, 8);
        int on = 0, total = 0;
        for (int i = 0; i < 400; i++) { net.Tick(f, 0.05f, 0.0001f); total++; if (tm.Signal > 0) on++; }
        Assert.InRange(on / (float)total, 0.17f, 0.23f);
    }

    [Fact]
    public void Day_night_motion_and_power_sensors_read_the_world()
    {
        var (net, f) = SignalNet();
        var dn = Add(net, f, "gun_gece_sensoru", 0, 0); var mo = Add(net, f, "hareket_sensoru", 1, 0); var kb = Add(net, f, "kabarma_sensoru", 2, 0);
        net.Night = true; mo.Occupied = true; net.KabarmaLevel = 35; net.Tick(f, 0.1f, 0.001f);
        Assert.True(dn.Signal > 0 && mo.Signal > 0); Assert.Equal(35, kb.Signal);
        net.Night = false; mo.Occupied = false; net.KabarmaLevel = 0; net.Tick(f, 0.1f, 0.001f);
        Assert.True(dn.Signal == 0 && mo.Signal == 0 && kb.Signal == 0);
        // Guc sensoru: aku yuzde 50
        var bat = net.Add(f, "aku_mk2", 5, 0, 5); bat.StoredWh = 1000;
        var ps = net.Add(f, "guc_sensoru", 6, 0, 5);
        Wire(net, f, bat, ps);
        net.Tick(f, 0.1f, 0f); net.Tick(f, 0.1f, 0f);
        Assert.InRange(ps.Signal, 49, 51);
    }

    [Fact]
    public void Siren_listens_to_a_signal_and_the_new_receivers_accept_only_valid_wires()
    {
        var (net, f) = SignalNet();
        var g = net.Add(f, "sabit", 0, 0, 0);
        var siren = Add(net, f, "siren", 3, 0); var plate = Add(net, f, "basinc_plakasi", 3, 3);
        var gate = Add(net, f, "kapi_ve", 6, 3);
        Wire(net, f, g, siren);
        Wire(net, f, plate, siren, true);
        net.Tick(f, 0.1f, 0.001f); Assert.False(siren.Powered); // sinyal yok: susar
        plate.Occupied = true; net.Tick(f, 0.1f, 0.001f); Assert.True(siren.Powered);
        // kapi kapiya baglanir, kaynak kaynaga baglanmaz
        var g2 = Add(net, f, "kapi_veya", 8, 3);
        Assert.Null(net.CanConnect(f, gate.Id, g2.Id, true));
        var lever = Add(net, f, "salter_kolu", 9, 3);
        Assert.NotNull(net.CanConnect(f, plate.Id, lever.Id, true));
        Assert.Contains("sinyal", net.CanConnect(f, plate.Id, g.Id, true)!);
    }

    // ---------------- bant, kol, boru ----------------

    /// <summary>Sandik A (0,-1) -> Kol (0,0) -> Bant (0,1),(0,2),(0,3) -> Sandik B (0,4). Reaktor kolu besler.</summary>
    static (PowerNet net, PowerFile f, TestWorld w, Inventory a, Inventory b, PowerNode arm, PowerNode[] belts) Line()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        var a = w.Add(0, -1); var b = w.Add(0, 4);
        var g = net.Add(f, "sabit", 3, 0, 0);
        var arm = Add(net, f, "tasima_kolu", 0, 0);
        Wire(net, f, g, arm);
        var belts = new[] { Add(net, f, "bant", 0, 1), Add(net, f, "bant", 0, 2), Add(net, f, "bant", 0, 3) };
        net.World = w;
        return (net, f, w, a, b, arm, belts);
    }

    [Fact]
    public void Arm_and_belts_carry_items_from_one_chest_to_another_at_two_per_second()
    {
        var (net, f, w, a, b, arm, belts) = Line();
        a.Add("copper_ingot", 30);
        arm.Filter = "copper_ingot";
        Run(net, f, 5f);
        int got = b.Count("copper_ingot");
        Assert.InRange(got, 2, 5);                                 // kol saniyede 1: 5 sn'de ~4 (bantta gecikme)
        Assert.Equal(30, a.Count("copper_ingot") + b.Count("copper_ingot") + net.Transport.Lanes.Values.Sum(l => l.Items.Count)); // hicbiri kaybolmaz
        Run(net, f, 40f);
        Assert.Equal(30, b.Count("copper_ingot"));
        // Bant araligi: iki nesne arasi >= 0.5 sn (saniyede en cok 2)
        var d = f.Def("bant")!;
        Assert.Equal(0.5f, d.Gap);
    }

    [Fact]
    public void Belt_stops_without_signal_and_full_target_blocks_without_losing_items()
    {
        var (net, f, w, a, b, arm, belts) = Line();
        a.Add("stone", 10); arm.Filter = "stone";
        var lever = Add(net, f, "salter_kolu", 5, 5);
        Wire(net, f, lever, belts[1], true);
        lever.On = false; Run(net, f, 10f);
        Assert.Equal(0, b.Count("stone"));                          // orta bant sinyal bekliyor
        lever.On = true; Run(net, f, 15f);
        Assert.Equal(10, b.Count("stone"));
        // dolu hedef: bant tutar
        var full = new Inventory(1, id => 5); full.Add("wood", 5);
        w.Stores.RemoveAll(s => s.inv == b); w.Stores.Add((0, 4, full));
        a.Add("stone", 10);
        Run(net, f, 30f);
        Assert.Equal(0, full.Count("stone"));
        Assert.Equal(10, a.Count("stone") + net.Transport.Lanes.Values.Sum(l => l.Items.Count));
    }

    [Fact]
    public void Splitter_alternates_between_its_outputs_and_merger_joins()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        var a = w.Add(0, -1); var left = w.Add(-1, 1); var front = w.Add(0, 2); var right = w.Add(1, 1);
        var g = net.Add(f, "sabit", 3, 0, 0);
        var arm = Add(net, f, "tasima_kolu", 0, 0); Wire(net, f, g, arm);
        Add(net, f, "ayirici", 0, 1);
        net.World = w;
        a.Add("stone", 9); arm.Filter = "stone";
        Run(net, f, 20f);
        Assert.Equal(9, left.Count("stone") + front.Count("stone") + right.Count("stone"));
        Assert.True(left.Count("stone") >= 2 && front.Count("stone") >= 2 && right.Count("stone") >= 2, $"{left.Count("stone")}/{front.Count("stone")}/{right.Count("stone")}");
    }

    [Fact]
    public void Pipe_and_pump_move_only_fluids_and_belts_refuse_them()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        var tank = w.Add(0, -1); var dest = w.Add(0, 3);
        var g = net.Add(f, "sabit", 3, 0, 0);
        var pump = Add(net, f, "pompa", 0, 0); Wire(net, f, g, pump);
        Add(net, f, "boru", 0, 1); Add(net, f, "boru", 0, 2);
        net.World = w;
        tank.Add("su", 12); tank.Add("stone", 5);
        Run(net, f, 10f);
        Assert.Equal(12, dest.Count("su"));
        Assert.Equal(0, dest.Count("stone"));                       // sivi disi tasinmaz
        // bant sivi almaz
        var (net2, f2, w2, a2, b2, arm2, belts2) = Line();
        a2.Add("su", 5); arm2.Filter = "su";
        Run(net2, f2, 10f);
        Assert.Equal(0, b2.Count("su"));
        Assert.Equal(5, a2.Count("su"));
    }

    [Fact]
    public void Transport_state_survives_a_json_round_trip()
    {
        var (net, f, w, a, b, arm, belts) = Line();
        a.Add("stone", 10); arm.Filter = "stone";
        Run(net, f, 3.3f);
        string json = JsonSerializer.Serialize(net);
        var back = JsonSerializer.Deserialize<PowerNet>(json)!;
        Assert.Equal(net.Transport.Lanes.Sum(l => l.Value.Items.Count), back.Transport.Lanes.Sum(l => l.Value.Items.Count));
        Assert.Equal("stone", back.Get(arm.Id)!.Filter);
        Assert.Equal(net.Transport.Delivered, back.Transport.Delivered);
    }

    // ---------------- ag (ortak oyun) ----------------

    [Fact]
    public void Transport_state_round_trips_through_the_net_codec_and_is_canonical()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        // bant + drone + teleferik canli durumda
        var a = w.Add(0, -1); var b = w.Add(0, 4);
        var g = net.Add(f, "sabit", 3, 0, 0);
        var arm = Add(net, f, "tasima_kolu", 0, 0); Wire(net, f, g, arm); arm.Filter = "stone";
        foreach (var z in new[] { 1, 2, 3 }) Add(net, f, "bant", 0, z);
        var g2 = net.Add(f, "sabit", 20, 0, 3);
        var drone = Add(net, f, "drone_istasyonu", 20, 0); Wire(net, f, g2, drone);
        var src = w.Add(30, 0); var dst = w.Add(40, 10);
        var ra = Add(net, f, "teleferik_istasyonu", 60, 0); var rb = Add(net, f, "teleferik_istasyonu", 100, 0);
        var g3 = net.Add(f, "sabit", 60, 0, 3); Wire(net, f, g3, ra);
        var g4 = net.Add(f, "sabit", 100, 0, 3); Wire(net, f, g4, rb);
        net.World = w;
        a.Add("stone", 10); src.Add("stone", 30);
        net.Transport.Drones.AddOrder(net, f, drone.Id, new DroneOrder { Item = "stone", Fx = 30, Fz = 0, Tx = 40, Tz = 10 });
        Assert.Null(net.Transport.Ropeways.Connect(net, f, ra.Id, rb.Id, "stone"));
        Run(net, f, 3.7f);
        Assert.True(net.Transport.Lanes.Values.Any(l => l.Items.Count > 0));
        var wr1 = new NetWriter(); TransportCodec.Write(wr1, net.Transport);
        var back = new TransportState();
        TransportCodec.Read(new NetReader(wr1.ToArray()), back);
        var wr2 = new NetWriter(); TransportCodec.Write(wr2, back);
        Assert.Equal(wr1.ToArray(), wr2.ToArray());                                   // kanonik: oku-yaz ayni bayt
        Assert.Equal(net.Transport.Lanes.Sum(l => l.Value.Items.Count), back.Lanes.Sum(l => l.Value.Items.Count));
        Assert.Equal(net.Transport.Drones.Station(drone.Id).Orders.Count, back.Drones.Stations[drone.Id].Orders.Count);
        Assert.Equal(net.Transport.Drones.Station(drone.Id).Fleet.Select(d => d.Phase), back.Drones.Stations[drone.Id].Fleet.Select(d => d.Phase));
        Assert.Single(back.Ropeways.Lines);
        Assert.Equal(net.Transport.Delivered, back.Delivered);
        // yeniden yazilan durum eskisinin ustune okunursa fazlayi siler
        back.Lanes[9999] = new LaneState();
        TransportCodec.Read(new NetReader(wr1.ToArray()), back);
        Assert.False(back.Lanes.ContainsKey(9999));
    }

    [Fact]
    public void Maya_vault_stock_can_be_written_and_read_for_the_net_section()
    {
        var v = new MayaVault(); v.Deposit(75.9f);
        Assert.Equal(75, v.Stock);
        var w = new NetWriter(); w.VarU((uint)v.Stock); w.VarU((uint)v.TotalDeposited);
        var r = new NetReader(w.ToArray()); var v2 = new MayaVault { Stock = (int)r.VarU(), TotalDeposited = (int)r.VarU() };
        Assert.Equal(v.Stock, v2.Stock); Assert.Equal(v.TotalDeposited, v2.TotalDeposited);
    }

    // ---------------- drone ----------------

    [Fact]
    public void Drones_haul_between_chests_within_80_m_and_never_lose_items()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        var src = w.Add(20, 0); var dst = w.Add(-30, 10);
        var g = net.Add(f, "sabit", 3, 0, 0);
        var st = Add(net, f, "drone_istasyonu", 0, 0); Wire(net, f, g, st);
        net.World = w;
        Assert.Null(net.Transport.Drones.AddOrder(net, f, st.Id, new DroneOrder { Item = "stone", Fx = 20, Fz = 0, Tx = -30, Tz = 10 }));
        Assert.NotNull(net.Transport.Drones.AddOrder(net, f, st.Id, new DroneOrder { Item = "stone", Fx = 20, Fz = 0, Tx = -90, Tz = 0 })); // 90 m > 80 m
        src.Add("stone", 50);
        Run(net, f, 3f);
        var fleet = net.Transport.Drones.Station(st.Id).Fleet;
        Assert.Equal(6, fleet.Count);
        Assert.True(fleet.Any(d => d.Phase != 0), "drone kalkmadi");
        Run(net, f, 60f);
        Assert.Equal(50, dst.Count("stone"));
        Assert.Equal(0, src.Count("stone"));
        Assert.All(fleet, d => Assert.Equal(0, d.Qty));
    }

    [Fact]
    public void Drones_wait_without_power_and_respect_capacity()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        var src = w.Add(10, 0); var dst = w.Add(-10, 0);
        var st = Add(net, f, "drone_istasyonu", 0, 0);
        net.World = w;
        net.Transport.Drones.AddOrder(net, f, st.Id, new DroneOrder { Item = "stone", Fx = 10, Fz = 0, Tx = -10, Tz = 0 });
        src.Add("stone", 20);
        Run(net, f, 20f);
        Assert.Equal(20, src.Count("stone"));                       // guc yok: kalkmaz
        var g = net.Add(f, "sabit", 3, 0, 0); Wire(net, f, g, st);
        Run(net, f, 0.5f);
        var carrying = net.Transport.Drones.Station(st.Id).Fleet.Where(d => d.Phase == 2).Sum(d => d.Qty);
        Run(net, f, 3f);
        Assert.True(src.Count("stone") <= 12, "kapasite 8: ilk dalga en az 8 almali");
        Run(net, f, 60f);
        Assert.Equal(20, dst.Count("stone"));
    }

    // ---------------- teleferik ----------------

    [Fact]
    public void Ropeway_needs_posts_every_60_m_and_carries_loads()
    {
        var f = Fixed(); var net = new PowerNet(); var w = new TestWorld();
        var g1 = net.Add(f, "sabit", 0, 0, 5); var g2 = net.Add(f, "sabit", 200, 0, 5);
        var a = Add(net, f, "teleferik_istasyonu", 0, 0); var b = Add(net, f, "teleferik_istasyonu", 200, 0);
        Wire(net, f, g1, a); Wire(net, f, g2, b);
        var chestA = w.Add(0, 1); var chestB = w.Add(200, 1);
        net.World = w;
        var r = net.Transport.Ropeways;
        Assert.Contains("Direk eksik (0/3)", r.Connect(net, f, a.Id, b.Id));
        foreach (var x in new[] { 50f, 100f, 150f }) Add(net, f, "teleferik_diregi", x, 2);
        Assert.Null(r.Connect(net, f, a.Id, b.Id));
        Assert.Equal("Zaten bağlı", r.Connect(net, f, a.Id, b.Id));
        // menzil
        var far = Add(net, f, "teleferik_istasyonu", 500, 0);
        Assert.Contains("en fazla", r.Connect(net, f, a.Id, far.Id));
        chestA.Add("stone", 45);
        Run(net, f, 120f);
        Assert.Equal(45, chestB.Count("stone"));
        Assert.Equal(0, chestA.Count("stone"));
    }

    [Fact]
    public void Ropeway_line_validation_and_post_math()
    {
        Assert.Equal(0, RopewayState.PostsNeeded(60f, 60f));
        Assert.Equal(1, RopewayState.PostsNeeded(61f, 60f));
        Assert.Equal(6, RopewayState.PostsNeeded(400f, 60f));
    }
}
