using System.Numerics;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Gidak'in tekmesi ve Zipzip'in gunduz surusu (PLAN §1.4, URETIM §6 M1, GDD §8.1).</summary>
public class CreatureTests
{
    static readonly GameData D = TestData.Load();

    [Fact]
    public void Gidak_kick_carries_a_zipzip_over_the_fence()
    {
        var g = D.Creature("gidak");
        var arc = KickArc.Toward(g, Vector3.Zero, new Vector3(1, 0, 0.5f), 0f);
        // Cit 1,3 m (build_kit.py p_cit); firlatma noktasindan KickClearAt metrede en az KickClearHeight.
        Assert.True(g.KickClearHeight >= 1.3f);
        Assert.True(arc.HeightAtDistance(g.KickClearAt) >= g.KickClearHeight, $"yukseklik {arc.HeightAtDistance(g.KickClearAt):0.00} m");
        // Tepe ortada, inis menzilde ve zeminde.
        Assert.Equal(g.KickApex, arc.At(0.5f).Y, 3);
        Assert.Equal(g.KickDistance, new Vector2(arc.To.X, arc.To.Z).Length(), 3);
        Assert.Equal(0f, arc.At(1f).Y, 3);
        Assert.Equal(1f, arc.Progress(g.KickFlightSeconds * 2));
    }

    [Fact]
    public void Kick_lands_on_lower_or_higher_ground()
    {
        var g = D.Creature("gidak");
        var arc = KickArc.Toward(g, new Vector3(0, 2, 0), Vector3.UnitX, -1f);
        Assert.Equal(-1f, arc.At(1f).Y, 3);
        Assert.True(arc.At(0.5f).Y > 2f);
    }

    [Fact]
    public void Day_groups_are_two_or_three()
    {
        var d = D.Creature("zipzip").Day!;
        var rng = new Rng(7);
        for (int i = 0; i < 50; i++)
        {
            var sizes = Herds.GroupSizes(d, rng);
            Assert.Equal(d.Groups, sizes.Count);
            Assert.All(sizes, s => Assert.InRange(s, 2, 3));
        }
        Assert.Equal(2, d.GroupMin);
        Assert.Equal(3, d.GroupMax);
    }

    [Fact]
    public void Herd_is_skittish_and_comes_only_to_mature_crops()
    {
        var d = D.Creature("zipzip").Day!;
        Assert.True(Herds.ShouldFlee(d, d.FleeRadius - 0.1f));
        Assert.False(Herds.ShouldFlee(d, d.FleeRadius + 0.1f));
        Assert.False(Herds.Out(nightActive: true));

        var data = TestData.Load();
        var farm = new Farm(data);
        var bag = new Inventory(10, 99);
        bag.Add("turnip_seed", 3);
        farm.Till(0, 0); farm.Plant(0, 0, "turnip", bag); farm.Water(0, 0);
        var p = farm.Get(0, 0)!;
        Assert.False(Herds.Attracts(p));            // tohum
        while (p.Stage != CropStage.Mature) farm.Tick(1f, true);
        Assert.True(Herds.Attracts(p));             // olgun
        farm.SwellMature(1, 0);
        Assert.False(Herds.Attracts(p));            // kabarik urun gunduz zararlisinin isi degil
    }
}
