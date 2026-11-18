using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

public class VehicleTests
{
    static VehicleDef Cekirge() => TestData.Load().Vehicle("cekirge");

    [Fact]
    public void Cekirge_matches_the_doc_two_seats_one_slot_hour_tank()
    {
        var d = Cekirge();
        Assert.Equal(2, d.Tier);
        Assert.Equal(2, d.Seats);
        Assert.Equal(1, d.Slots);
        Assert.Equal(3600f, Vehicles.TankSeconds(d), 1);
        Assert.Equal(15, d.Recipe["iron_ingot"]);
        Assert.Equal(4, d.Recipe["copper_wire"]);
    }

    [Fact]
    public void Drives_to_top_speed_turns_and_burns_fuel_only_on_throttle()
    {
        var d = Cekirge();
        var s = Vehicles.New(d, 1, 0, 0);
        Vehicles.Step(d, s, new VehicleInput { Throttle = 1 }, 1f);
        Assert.Equal(0f, s.Speed); // surucu yok
        Assert.True(Vehicles.Board(d, s, 7));
        Vehicles.Step(d, s, new VehicleInput { Throttle = 1 }, 1f);
        Assert.Equal(0f, s.Speed); // depo bos
        Assert.Equal(12, Vehicles.Refuel(d, s, 20));
        for (int i = 0; i < 100; i++) Vehicles.Step(d, s, new VehicleInput { Throttle = 1 }, 0.1f);
        Assert.Equal(d.MaxSpeed, s.Speed, 2);
        Assert.True(s.Z > 50);
        Assert.Equal(3600f - 10f, s.Fuel, 1);
        float yaw = s.Yaw;
        Vehicles.Step(d, s, new VehicleInput { Throttle = 1, Steer = 1 }, 0.5f);
        Assert.True(s.Yaw > yaw);
        float fuel = s.Fuel;
        for (int i = 0; i < 200; i++) Vehicles.Step(d, s, new VehicleInput(), 0.1f);
        Assert.Equal(0f, s.Speed);
        Assert.Equal(fuel, s.Fuel);
    }

    [Fact]
    public void Seats_are_limited_and_fast_on_steep_slope_flips()
    {
        var d = Cekirge();
        var s = Vehicles.New(d, 1, 0, 0);
        Assert.True(Vehicles.Board(d, s, 1));
        Assert.True(Vehicles.Board(d, s, 2));
        Assert.False(Vehicles.Board(d, s, 3));
        Vehicles.Refuel(d, s, 1);
        Vehicles.Step(d, s, new VehicleInput { Throttle = 1, SlopeDeg = 40 }, 0.2f);
        Assert.False(s.Flipped); // yavas
        for (int i = 0; i < 10; i++) Vehicles.Step(d, s, new VehicleInput { Throttle = 1, SlopeDeg = 40 }, 0.1f);
        Assert.True(s.Flipped);
        Vehicles.Right(s);
        Assert.False(s.Flipped);
    }
}
