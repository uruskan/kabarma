using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>M4 arac akisi (ICERIK §14): 6 arac, 10 eklenti, yakit carpani, zemin, ucus, dusme, kasa, uretim.</summary>
public class VehicleM4Tests
{
    static readonly GameData D = TestData.Load();
    static VehicleDef V(string id) => D.Vehicle(id);
    static VehicleState Ready(string id, int fuelUnits = 99)
    {
        var d = V(id);
        var s = Vehicles.New(d, 1, 0, 0);
        Vehicles.Board(d, s, 1);
        Vehicles.Refuel(d, s, fuelUnits);
        return s;
    }
    static void Drive(VehicleDef d, VehicleState s, VehicleInput i, float seconds, VehicleMods? m = null)
    {
        for (float t = 0; t < seconds; t += 0.1f) Vehicles.Step(d, s, i, 0.1f, m ?? VehicleMods.None);
    }

    [Fact]
    public void Six_vehicles_ten_attachments_match_the_icerik_table()
    {
        var ids = D.Vehicles.Vehicles.Select(v => v.Id).ToArray();
        Assert.Equal(new[] { "cekirge", "traktor", "kamyon", "bicerdover", "hava_yastikli", "zeplin" }, ids);
        Assert.Equal((2, 2), (V("cekirge").Tier, V("cekirge").Seats));
        Assert.Equal((3, 2, 2), (V("traktor").Tier, V("traktor").Seats, V("traktor").Slots));
        Assert.Equal((3, 3, 2, 6), (V("kamyon").Tier, V("kamyon").Seats, V("kamyon").Slots, V("kamyon").CargoCrates));
        Assert.Equal((4, 1, 1), (V("bicerdover").Tier, V("bicerdover").Seats, V("bicerdover").Slots));
        Assert.Equal((4, 4, 2, "hover"), (V("hava_yastikli").Tier, V("hava_yastikli").Seats, V("hava_yastikli").Slots, V("hava_yastikli").Kind));
        Assert.Equal((5, 6, 3, "air"), (V("zeplin").Tier, V("zeplin").Seats, V("zeplin").Slots, V("zeplin").Kind));
        Assert.Equal(new[] { "romork", "pulluk", "ekim_makinesi", "su_tankeri", "vinc", "kepce", "zirh_tampon", "arac_kulesi", "arac_projektoru", "balon" }, D.Vehicles.Attachments.Select(a => a.Id).ToArray());
        Assert.DoesNotContain("projektor", D.Vehicles.Attachments.Select(a => a.Id)); // yerlestirilebilirle cakismaz
        Assert.True(V("cekirge").Recipe.ContainsKey("resin") && !V("cekirge").Recipe.ContainsKey("recine"));
    }

    [Fact]
    public void Every_vehicle_and_attachment_has_a_recipe_at_its_station_and_valid_inputs()
    {
        var made = D.Tool.Recipes.Select(r => r.Id).ToHashSet();
        foreach (var v in D.Vehicles.Vehicles)
        {
            var r = D.Tool.Recipes.Single(x => x.Id == v.Id);
            Assert.Equal(v.Station, r.Station);
            Assert.True(r.Load, $"{v.Id} yuk olarak cikar");
            Assert.Equal(v.Recipe, r.In);
            Assert.Equal(v.Station == "zeplin_hangari" ? "zeplin" : "arac_atolyesi", v.Id == "zeplin" ? "zeplin" : v.Station);
        }
        foreach (var a in D.Vehicles.Attachments)
        {
            var r = D.Tool.Recipes.Single(x => x.Id == a.Id);
            Assert.Equal("arac_atolyesi", r.Station);
            Assert.Equal(a.Recipe, r.In);
        }
        var raw = D.Research.RawSources.Select(s => s.Id).ToHashSet();
        foreach (var r in D.Tool.Recipes.Where(r => Vehicles.IsProduct(D.Vehicles, r.Id) || D.Vehicles.Attachments.Any(a => a.Id == r.Id)))
            foreach (var k in r.In.Keys) Assert.True(made.Contains(k) || raw.Contains(k) || D.Tool.StartItems.ContainsKey(k), $"{r.Id}: girdi {k}");
    }

    [Fact]
    public void Fuel_multipliers_biofuel_gives_double_and_unknown_fuel_is_refused()
    {
        var d = V("traktor");
        var s = Vehicles.New(d, 1, 0, 0);
        Assert.Equal(1f, Vehicles.FuelMult(d, "bitkisel_yag"));
        Assert.Equal(2f, Vehicles.FuelMult(d, "biyoyakit"));
        Assert.Equal(0f, Vehicles.FuelMult(d, "charcoal"));
        Assert.Equal(3, Vehicles.Refuel(d, s, "biyoyakit", 3));
        Assert.Equal(3 * d.UnitSeconds * 2, s.Fuel, 1);
        Assert.Equal(0, Vehicles.Refuel(d, s, "charcoal", 5));
        Assert.True(Vehicles.Refuel(d, s, "bitkisel_yag", 999) <= d.TankUnits);
        Assert.True(s.Fuel <= Vehicles.TankSeconds(d) + 0.01f);
    }

    [Fact]
    public void Ground_vehicles_crawl_in_water_hover_does_not()
    {
        var cek = V("cekirge"); var hov = V("hava_yastikli");
        var a = Ready("cekirge"); var b = Ready("hava_yastikli");
        Drive(cek, a, new VehicleInput { Throttle = 1, Surface = "water" }, 8f);
        Drive(hov, b, new VehicleInput { Throttle = 1, Surface = "water" }, 8f);
        Assert.True(a.Speed <= cek.MaxSpeed * 0.21f + 0.01f, $"cekirge suda {a.Speed:0.0}");
        Assert.True(b.Speed > hov.MaxSpeed * 0.95f, $"hava yastikli suda {b.Speed:0.0}");
        var c = Ready("traktor");
        Drive(V("traktor"), c, new VehicleInput { Throttle = 1, Surface = "swamp" }, 8f);
        Assert.True(c.Speed < V("traktor").MaxSpeed * 0.62f);
        Assert.Equal(1f, Vehicles.SurfaceMult(hov, "snow"));
        Assert.True(Vehicles.SurfaceMult(cek, "snow") < 1f && Vehicles.SurfaceMult(cek, "sand") < 1f);
        Assert.False(Vehicles.IsAir(hov)); Assert.True(Vehicles.IsHover(hov));
    }

    [Fact]
    public void Zeppelin_takes_off_climbs_holds_altitude_and_lands()
    {
        var d = V("zeplin");
        var s = Ready("zeplin");
        Drive(d, s, new VehicleInput { Throttle = 1 }, 6f);
        Assert.True(s.Speed <= d.TaxiSpeed + 0.01f, "yerde yalniz taksi");
        Drive(d, s, new VehicleInput { Lift = 1 }, 5f);
        Assert.True(s.Altitude > 20f && s.Altitude <= d.MaxAltitude, $"yukseklik {s.Altitude:0.0}");
        float alt = s.Altitude;
        Drive(d, s, new VehicleInput { Throttle = 1 }, 20f);
        Assert.True(s.Speed > d.TaxiSpeed + 1f, "havada tam hiz");
        Assert.Equal(alt, s.Altitude, 3); // kumanda yok: asili
        Assert.False(s.Flipped);
        Drive(d, s, new VehicleInput { Throttle = 1, SlopeDeg = 80 }, 2f);
        Assert.False(s.Flipped); // ucan devrilmez
        Drive(d, s, new VehicleInput { Lift = -1 }, 40f);
        Assert.Equal(0f, s.Altitude);
        Vehicles.Settle(d, s, 12f, 0.1f, VehicleMods.None, D.Vehicles.Rules);
        Assert.Equal(12f, s.Y, 3);
        Assert.True(V("zeplin").Station == "zeplin_hangari");
        Assert.True(Vehicles.InHangar(s, s.X + 5, s.Z, D.Vehicles.Rules) && !Vehicles.InHangar(s, s.X + 50, s.Z, D.Vehicles.Rules));
    }

    [Fact]
    public void Altitude_costs_fuel_and_an_empty_zeppelin_descends()
    {
        var d = V("zeplin");
        var s = Ready("zeplin");
        float f0 = s.Fuel;
        Drive(d, s, new VehicleInput { Lift = 1 }, 3f);
        Assert.True(s.Fuel < f0);
        Vehicles.Leave(s, 1);
        float alt = s.Altitude;
        Drive(d, s, new VehicleInput(), 10f);
        Assert.True(s.Altitude < alt, "surucusuz iner");
    }

    [Fact]
    public void Attachments_fit_by_vehicle_mount_and_limits()
    {
        var f = D.Vehicles;
        var tr = Vehicles.New(V("traktor"), 1, 0, 0);
        Assert.Equal("", Vehicles.Attach(V("traktor"), tr, f, "pulluk"));
        Assert.Contains("çeki demiri dolu", Vehicles.Attach(V("traktor"), tr, f, "romork")); // tek cekme demiri
        Assert.Equal("", Vehicles.Attach(V("traktor"), tr, f, "zirh_tampon"));
        Assert.Equal("", Vehicles.Attach(V("traktor"), tr, f, "arac_projektoru"));
        Assert.Contains("gövde yuvası dolu", Vehicles.Attach(V("traktor"), tr, f, "vinc"));
        Assert.Contains("zaten", Vehicles.Attach(V("traktor"), tr, f, "zirh_tampon"));
        var cek = Vehicles.New(V("cekirge"), 2, 0, 0);
        Assert.Contains("uymaz", Vehicles.Attach(V("cekirge"), cek, f, "pulluk")); // pulluk yalniz traktorde
        var zep = Vehicles.New(V("zeplin"), 3, 0, 0);
        Assert.Contains("uymaz", Vehicles.Attach(V("zeplin"), zep, f, "balon"));
        Assert.Equal("", Vehicles.Attach(V("zeplin"), zep, f, "arac_kulesi"));
        Assert.True(Vehicles.Detach(tr, "pulluk"));
        Assert.False(Vehicles.Detach(tr, "pulluk"));
        Assert.Equal("", Vehicles.Attach(V("traktor"), tr, f, "romork")); // demir bosaldi
    }

    [Fact]
    public void Attachment_effects_speed_armor_ram_cargo_light_gunner()
    {
        var f = D.Vehicles;
        var kam = Vehicles.New(V("kamyon"), 1, 0, 0);
        Assert.Equal(6, Vehicles.CrateCapacity(V("kamyon"), kam, f));
        Vehicles.Attach(V("kamyon"), kam, f, "romork");
        Assert.Equal(10, Vehicles.CrateCapacity(V("kamyon"), kam, f));
        Vehicles.Attach(V("kamyon"), kam, f, "zirh_tampon");
        Vehicles.Attach(V("kamyon"), kam, f, "arac_projektoru");
        var m = Vehicles.ModsOf(f, kam);
        Assert.Equal(0.85f * 0.95f, m.SpeedMult, 3);
        Assert.Equal(200f, m.ArmorHp);
        Assert.True(m.HasLight && m.RamDamage > 0);
        Vehicles.Attach(V("kamyon"), new VehicleState(), f, "arac_kulesi");
        var kule = Vehicles.New(V("traktor"), 2, 0, 0); Vehicles.Attach(V("traktor"), kule, f, "arac_kulesi");
        Assert.Equal(1, Vehicles.ModsOf(f, kule).GunnerSeats);
        // hizli tampon hasar verir, yavas vermez, tamponsuz vermez
        kam.Speed = 10;
        Assert.True(Vehicles.RamDamage(kam, m, f.Rules) > 0);
        kam.Speed = 1;
        Assert.Equal(0f, Vehicles.RamDamage(kam, m, f.Rules));
        Assert.Equal(0f, Vehicles.RamDamage(new VehicleState { Speed = 12 }, VehicleMods.None, f.Rules));
        // eklenti hizi dusurur
        var s = Ready("kamyon");
        s.Attachments.AddRange(new[] { "romork", "zirh_tampon" });
        Drive(V("kamyon"), s, new VehicleInput { Throttle = 1 }, 12f, Vehicles.ModsOf(f, s));
        Assert.True(s.Speed < V("kamyon").MaxSpeed * 0.9f && s.Speed > V("kamyon").MaxSpeed * 0.7f);
    }

    [Fact]
    public void Balloon_glides_and_falls_without_it_hurt()
    {
        var f = D.Vehicles;
        var rules = f.Rules;
        var d = V("kamyon");
        var plain = Vehicles.New(d, 1, 0, 0); plain.Y = 40;
        for (int i = 0; i < 200 && plain.Y > 0; i++) Vehicles.Settle(d, plain, 0f, 0.05f, VehicleMods.None, rules);
        Assert.Equal(0f, plain.Y, 3);
        Assert.True(plain.Hp < d.Hp, "40 m dusme hasar verir");
        var glide = Vehicles.New(d, 2, 0, 0); glide.Y = 40; Vehicles.Attach(d, glide, f, "balon");
        var mods = Vehicles.ModsOf(f, glide);
        float minVy = 0;
        for (int i = 0; i < 2000 && glide.Y > 0; i++) { Vehicles.Settle(d, glide, 0f, 0.05f, mods, rules); minVy = MathF.Min(minVy, glide.Vy); }
        Assert.True(minVy >= -mods.Glide - 1e-3f);
        Assert.Equal(d.Hp, glide.Hp);
        // yamaci izler
        var slope = Vehicles.New(d, 3, 0, 0);
        Vehicles.Settle(d, slope, 5f, 0.05f, VehicleMods.None, rules);
        Assert.Equal(5f, slope.Y, 3);
    }

    [Fact]
    public void Crates_load_to_capacity_and_unload()
    {
        var f = D.Vehicles;
        var d = V("kamyon");
        var s = Vehicles.New(d, 1, 0, 0);
        for (int i = 1; i <= 6; i++) Assert.True(Vehicles.LoadCrate(d, s, f, i));
        Assert.False(Vehicles.LoadCrate(d, s, f, 7));
        Assert.False(Vehicles.LoadCrate(d, s, f, 3)); // ayni kasa iki kez degil
        Assert.True(Vehicles.UnloadCrate(s, 3));
        Assert.True(Vehicles.LoadCrate(d, s, f, 7));
        Assert.Equal(3, V("zeplin").CargoCrates); // altina asar
        Assert.Equal(0, V("traktor").CargoCrates);
    }

    [Fact]
    public void Plough_sower_and_tank_work_a_strip_behind_and_harvester_cuts_ahead()
    {
        var f = D.Vehicles;
        var s = Vehicles.New(V("traktor"), 1, 10, 10, yaw: 0f); // +Z ileri
        var cells = Vehicles.WorkCells(s, f.Attachments.Single(a => a.Id == "pulluk")).ToList();
        Assert.Equal(3, cells.Count);
        Assert.All(cells, c => Assert.Equal(7, c.y)); // 10 - 2.4 => 7
        Assert.Equal(3, cells.Select(c => c.x).Distinct().Count());
        Assert.Equal(200, (int)f.Attachments.Single(a => a.Id == "su_tankeri").Params["waterUnits"]);
        var t = Vehicles.New(V("traktor"), 2, 0, 0);
        Vehicles.Attach(V("traktor"), t, f, "su_tankeri");
        Assert.Equal(200, t.Water);
        Vehicles.Detach(t, "su_tankeri");
        Assert.Equal(0, t.Water);
        var h = Vehicles.New(V("bicerdover"), 3, 0, 0, yaw: 0f);
        Assert.True(Vehicles.InHarvestCone(V("bicerdover"), h, 0, 2f));
        Assert.False(Vehicles.InHarvestCone(V("bicerdover"), h, 0, -4f));
        Assert.False(Vehicles.InHarvestCone(V("traktor"), Vehicles.New(V("traktor"), 4, 0, 0), 0, 1f));
    }

    [Fact]
    public void Old_save_state_still_loads_without_new_fields()
    {
        var json = "{\"Id\":3,\"Vehicle\":\"cekirge\",\"X\":1,\"Z\":2,\"Yaw\":0,\"Speed\":0,\"Fuel\":5,\"Hp\":100,\"Flipped\":false,\"Riders\":[]}";
        var s = System.Text.Json.JsonSerializer.Deserialize<VehicleState>(json)!;
        Assert.Empty(s.Attachments); Assert.Empty(s.Crates); Assert.Equal(0f, s.Altitude);
    }
}
