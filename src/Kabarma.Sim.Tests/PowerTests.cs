using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Guc agi ve temel sinyal (GDD 11.1, 11.2; ICERIK 12 "M2 bedelleri"): graf, denge, aku, oncelik, salter, sinyal, kayit.</summary>
public class PowerTests
{
    static PowerFile F() => TestData.Load().Power;

    /// <summary>Ruzgarsiz sabit uretec: testler ruzgar evresine bagli kalmasin.</summary>
    static PowerFile Fixed(float watts)
    {
        var f = F();
        f.Devices.Add(new PowerDeviceDef { Id = "sabit", Kind = "generator", Source = "fixed", Watts = watts, Sockets = 4 });
        return f;
    }

    static (PowerNet net, PowerNode gen) Grid(PowerFile f, float x = 0)
    {
        var net = new PowerNet();
        var g = net.Add(f, "sabit", x, 0, 0);
        return (net, g);
    }

    [Fact]
    public void Data_matches_icerik_12()
    {
        var d = TestData.Load();
        var f = d.Power;
        Assert.Equal(12f, f.CableMaxLength);
        Assert.Equal(4, f.Def("direk")!.Sockets);
        Assert.Equal(80f, f.Def("ruzgar_gulu")!.Watts);
        Assert.Equal(40f, f.Def("hayvan_carki")!.Watts);
        Assert.Equal(500f, f.Def("aku_mk1")!.CapacityWh);
        Assert.Equal(new[] { "wood", "bakir_tel" }, d.Tool.RecipeFor("ruzgar_gulu")!.In.Keys);
        Assert.Equal(2, d.Tool.RecipeFor("bakir_tel")!.Out);
        // her yerlestirilebilir guc parcasinin tarifi ve tanimi var; tarifi olmayan Aku Mk1 yerlestirilebilir degil (Demir yok)
        foreach (var dev in f.Devices.Where(x => x.Id != "aku_mk1"))
        {
            Assert.NotNull(d.Tool.RecipeFor(dev.Id));
            Assert.Contains(d.Tool.Placeables, p => p.Id == dev.Id);
        }
        Assert.Null(f.Def("tezgah")); // guc istemeyen istasyon ag parcasi degil
        Assert.Equal(0f, f.ConsumerWatts("tezgah"));
        Assert.Equal("consumer", f.Def("bicki")!.Kind);
    }

    [Fact]
    public void Wind_turbine_lights_a_lamp_through_a_cable_and_pole()
    {
        var f = F();
        var net = new PowerNet();
        var w = net.Add(f, "ruzgar_gulu", 0, 0, 0);
        var pole = net.Add(f, "direk", 10, 0, 0);
        var lamp = net.Add(f, "elektrik_lambasi", 20, 0, 0);
        Assert.NotNull(net.Connect(f, w.Id, pole.Id, false));
        Assert.NotNull(net.Connect(f, pole.Id, lamp.Id, false));
        net.Tick(f, 0.1f, 0.001f);
        Assert.True(w.OutputW >= 8f && w.OutputW <= 80f, $"ruzgar {w.OutputW}");
        Assert.True(lamp.Powered); // en dusuk ruzgar (min 0.1 -> 8 W) 5 W'lik lambayi yakar
        Assert.Single(net.Nets);
        Assert.Equal(5f, net.StatsOf(lamp.Id)!.ServedW);
    }

    [Fact]
    public void Wind_varies_but_stays_within_0_to_80()
    {
        var f = F();
        var net = new PowerNet();
        float lo = 1, hi = 0;
        for (int i = 0; i < 5000; i++) { net.Clock = i * 3.0; float k = net.WindFactor(f); lo = Math.Min(lo, k); hi = Math.Max(hi, k); }
        Assert.True(lo >= f.Wind.Min - 1e-4f && hi <= 1f);
        Assert.True(hi - lo > 0.5f, "ruzgar degismiyor");
    }

    [Fact]
    public void Cable_is_at_most_12_m_and_sockets_run_out()
    {
        var f = Fixed(100);
        var (net, g) = Grid(f);
        var far = net.Add(f, "direk", 12.5f, 0, 0);
        Assert.Contains("12", net.CanConnect(f, g.Id, far.Id, false));
        var lamp = net.Add(f, "elektrik_lambasi", 3, 0, 0);
        var lamp2 = net.Add(f, "elektrik_lambasi", 3, 0, 3);
        Assert.NotNull(net.Connect(f, g.Id, lamp.Id, false));
        Assert.Null(net.Connect(f, g.Id, lamp.Id, false)); // zaten bagli
        // lambanin tek soketi dolu
        Assert.Contains("Soket", net.CanConnect(f, lamp.Id, lamp2.Id, false));
        // direk 4 soket
        var pole = net.Add(f, "direk", 0, 0, 5);
        for (int i = 0; i < 4; i++) Assert.NotNull(net.Connect(f, pole.Id, net.Add(f, "elektrik_lambasi", i, 0, 8).Id, false));
        Assert.Contains("Soket", net.CanConnect(f, pole.Id, lamp2.Id, false));
        // sokunce kablo sayisi doner
        Assert.Equal(4, net.Remove(pole.Id));
    }

    [Fact]
    public void Shortage_sheds_lowest_priority_first()
    {
        var f = Fixed(30);
        var (net, g) = Grid(f);
        var pole = net.Add(f, "direk", 1, 0, 0);
        net.Connect(f, g.Id, pole.Id, false);
        var a = net.Add(f, "bicki", 2, 0, 0); a.Priority = 1; a.Wants = true;
        var b = net.Add(f, "degirmen", 2, 0, 1); b.Priority = 5; b.Wants = true;
        var c = net.Add(f, "elektrik_lambasi", 2, 0, 2); c.Priority = 3;
        foreach (var n in new[] { a, b, c }) net.Connect(f, pole.Id, n.Id, false);
        net.Tick(f, 0.1f, 0.001f);
        var st = net.StatsOf(pole.Id)!;
        Assert.True(a.Powered && c.Powered);
        Assert.False(b.Powered);
        Assert.True(b.Shed && st.Overloaded);
        Assert.Equal(45f, st.DemandW);
        Assert.Equal(25f, st.ServedW);
        // talep dusunce yeniden beslenir
        a.Wants = false;
        net.Tick(f, 0.1f, 0.001f);
        Assert.True(b.Powered && !net.StatsOf(pole.Id)!.Overloaded);
    }

    [Fact]
    public void Station_consumer_draws_only_while_working()
    {
        var f = Fixed(10);
        var (net, g) = Grid(f);
        var saw = net.Add(f, "bicki", 2, 0, 0);
        Assert.False(saw.Wants);
        net.Connect(f, g.Id, saw.Id, false);
        net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(0f, net.StatsOf(saw.Id)!.DemandW);
        saw.Wants = true;
        net.Tick(f, 0.1f, 0.001f);
        Assert.False(saw.Powered); // 20 W > 10 W
        Assert.True(saw.Shed);
    }

    [Fact]
    public void Battery_charges_from_surplus_and_covers_the_gap()
    {
        var f = Fixed(100);
        var (net, g) = Grid(f);
        var bat = net.Add(f, "aku_mk1", 1, 0, 0);
        var pole = net.Add(f, "direk", 2, 0, 0);
        net.Connect(f, g.Id, bat.Id, false);
        net.Connect(f, bat.Id, pole.Id, false);
        net.Tick(f, 1f, 1f); // 1 saat: 100 W fazla, aku en fazla 200 W -> +100 Wh
        Assert.Equal(100f, bat.StoredWh, 2);
        for (int i = 0; i < 10; i++) net.Tick(f, 1f, 1f);
        Assert.Equal(500f, bat.StoredWh, 2); // dolu kalir
        // uretec gidince aku besler: 3 x 20 W (direk: aku + 3 tuketici)
        net.Remove(g.Id);
        var loads = Enumerable.Range(0, 3).Select(i => { var n = net.Add(f, "bicki", 3, 0, i); n.Wants = true; Assert.NotNull(net.Connect(f, pole.Id, n.Id, false)); return n; }).ToList();
        net.Tick(f, 1f, 1f);
        Assert.All(loads, n => Assert.True(n.Powered));
        Assert.Equal(440f, bat.StoredWh, 2);
        Assert.Equal(60f, net.StatsOf(pole.Id)!.BatteryW, 2);
        // bosalinca kapanir
        for (int i = 0; i < 10; i++) net.Tick(f, 1f, 1f);
        Assert.Equal(0f, bat.StoredWh, 2);
        Assert.Contains(loads, n => !n.Powered);
    }

    [Fact]
    public void Open_switch_splits_the_net()
    {
        var f = Fixed(50);
        var (net, g) = Grid(f);
        var sw = net.Add(f, "salter", 2, 0, 0);
        var lamp = net.Add(f, "elektrik_lambasi", 4, 0, 0);
        net.Connect(f, g.Id, sw.Id, false);
        net.Connect(f, sw.Id, lamp.Id, false);
        net.Tick(f, 0.1f, 0.001f);
        Assert.True(lamp.Powered);
        sw.On = false;
        net.Tick(f, 0.1f, 0.001f);
        Assert.False(lamp.Powered);
        Assert.Equal(0f, net.StatsOf(lamp.Id)!.SupplyW); // lamba uretecsiz bir ada
        Assert.NotEqual(g.Net, lamp.Net);
    }

    [Fact]
    public void Button_signal_drives_switch_and_lamp_for_one_second()
    {
        var f = Fixed(50);
        var (net, g) = Grid(f);
        var lamp = net.Add(f, "elektrik_lambasi", 3, 0, 0);
        var btn = net.Add(f, "dugme", 4, 0, 0);
        net.Connect(f, g.Id, lamp.Id, false);
        Assert.NotNull(net.CanConnect(f, btn.Id, lamp.Id, false)); // dugmenin guc soketi yok
        Assert.NotNull(net.Connect(f, lamp.Id, btn.Id, true)); // yon kendiliginden: dugme -> lamba
        Assert.Contains(net.Links, l => l.Signal && l.A == btn.Id && l.B == lamp.Id);
        net.Tick(f, 0.1f, 0.001f);
        Assert.False(lamp.Powered); // sinyal bagli, sinyal 0: sonuk
        net.Press(f, btn.Id);
        net.Tick(f, 0.1f, 0.001f);
        Assert.Equal(100, lamp.Signal);
        Assert.True(lamp.Powered);
        for (int i = 0; i < 10; i++) net.Tick(f, 0.1f, 0.001f);
        Assert.False(lamp.Powered);
    }

    [Fact]
    public void Pressure_plate_closes_a_switch_while_occupied()
    {
        var f = Fixed(50);
        var (net, g) = Grid(f);
        var sw = net.Add(f, "salter", 2, 0, 0);
        var lamp = net.Add(f, "elektrik_lambasi", 4, 0, 0);
        var plate = net.Add(f, "basinc_plakasi", 2, 0, 3);
        net.Connect(f, g.Id, sw.Id, false);
        net.Connect(f, sw.Id, lamp.Id, false);
        net.Connect(f, plate.Id, sw.Id, true);
        Assert.Contains("sinyal", net.CanConnect(f, plate.Id, g.Id, true)!);
        net.Tick(f, 0.1f, 0.001f);
        Assert.False(lamp.Powered); // salterin sinyali 0: acik
        plate.Occupied = true;
        net.Tick(f, 0.1f, 0.001f);
        Assert.True(lamp.Powered);
    }

    [Fact]
    public void Animal_wheel_needs_an_animal()
    {
        var f = F();
        var net = new PowerNet();
        var wh = net.Add(f, "hayvan_carki", 0, 0, 0);
        var lamp = net.Add(f, "elektrik_lambasi", 2, 0, 0);
        net.Connect(f, wh.Id, lamp.Id, false);
        net.Tick(f, 0.1f, 0.001f);
        Assert.False(lamp.Powered);
        wh.Occupied = true;
        net.Tick(f, 0.1f, 0.001f);
        Assert.True(lamp.Powered);
        Assert.Equal(40f, wh.OutputW);
    }

    [Fact]
    public void State_round_trips_through_json()
    {
        var f = Fixed(100);
        var (net, g) = Grid(f);
        var bat = net.Add(f, "aku_mk1", 1, 0, 0);
        var sw = net.Add(f, "salter", 2, 0, 0); sw.On = false;
        var lamp = net.Add(f, "elektrik_lambasi", 3, 0, 0); lamp.Priority = 1;
        var btn = net.Add(f, "dugme", 3, 0, 2);
        net.Connect(f, g.Id, bat.Id, false); net.Connect(f, bat.Id, sw.Id, false); net.Connect(f, sw.Id, lamp.Id, false);
        net.Connect(f, btn.Id, lamp.Id, true);
        net.Tick(f, 1f, 1f);
        var json = JsonSerializer.Serialize(net);
        var back = JsonSerializer.Deserialize<PowerNet>(json)!;
        Assert.Equal(net.NextId, back.NextId);
        Assert.Equal(net.Clock, back.Clock);
        Assert.Equal(net.Nodes.Count, back.Nodes.Count);
        Assert.Equal(net.Links.Count, back.Links.Count);
        Assert.Equal(bat.StoredWh, back.Get(bat.Id)!.StoredWh);
        Assert.False(back.Get(sw.Id)!.On);
        Assert.Equal(1, back.Get(lamp.Id)!.Priority);
        back.Tick(f, 1f, 1f);
        net.Tick(f, 1f, 1f);
        Assert.Equal(net.Get(bat.Id)!.StoredWh, back.Get(bat.Id)!.StoredWh);
        Assert.Equal(net.Nets.Count, back.Nets.Count);
    }

    [Fact]
    public void Game_hours_follow_the_calendar()
    {
        var c = TestData.Load().Calendar;
        // bir gun 30 gercek dakika = 24 oyun saati
        Assert.Equal(24f, PowerNet.GameHours(30 * 60, c), 3);
    }
}
