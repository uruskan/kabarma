using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>tarim (M2) kaydi (PLAN 7.2 kosul 5): Tohum Kaydi, Islah notlari, yabani bitki, Alaca, Zarar, nobet gidis-donusu.</summary>
public class GeneticsSaveTests
{
    static SaveRegistry Reg(GameState s) => new SaveRegistry().AddRange(s.SaveSections());

    static GameState Played()
    {
        var s = new GameState(TestData.Load());
        var d = s.Data;
        var rng = new Rng(77);
        s.Backpack.Add("tomato_seed", 3); s.Backpack.Add("turnip_seed", 2);
        s.Farm.Till(0, 0); s.Farm.Till(1, 0); s.Farm.Till(4, 0);
        s.Farm.Plant(0, 0, "tomato", s.Backpack, rng); s.Farm.Plant(1, 0, "tomato", s.Backpack, rng); s.Farm.Plant(4, 0, "turnip", s.Backpack, rng);
        s.Farm.SpawnWild(30, 30, "hazelnut", rng);
        s.Farm.SetPollinators(new[] { (1f, 1f) });
        GeneticsTests.GrowAll(s.Farm, d, 200f);
        s.Farm.HarvestFull(0, 0, rng);   // caprazlama, Tohum Kaydi, not
        s.Farm.HarvestFull(30, 30, rng); // yabani, yeniden bitme sayaci
        s.Farm.Plant(0, 0, "tomato", s.Backpack, rng);
        s.Farm.PestBite(4, 0);
        s.Farm.Tick(3f, true);
        s.Farm.Selected = "tomato";
        return s;
    }

    [Fact]
    public void Farm_genetics_round_trip_byte_identical_and_runs_on_identically()
    {
        var a = Played();
        Assert.True(a.Farm.Log.Crosses > 0);
        var g = SaveCodec.Decode(SaveCodec.Encode(Reg(a).Capture(SaveScope.World, a.Manifest("tarim"))));
        Assert.Contains("farm.genetics", g.Sections.Keys);
        var b = new GameState(TestData.Load());
        var rep = Reg(b).Restore(g);
        Assert.Empty(rep.Missing);
        var g2 = Reg(b).Capture(SaveScope.World);
        foreach (var (id, blob) in g.Sections)
            Assert.True(blob.Data.AsSpan().SequenceEqual(g2.Sections[id].Data), $"'{id}' bolumu gidis-donuste degisti");

        Assert.True(b.Farm.Get(30, 30)!.Wild);
        Assert.Equal(a.Farm.Get(4, 0)!.PestDamage, b.Farm.Get(4, 0)!.PestDamage);
        Assert.Equal(a.Farm.Seeds.CountFor("tomato"), b.Farm.Seeds.CountFor("tomato"));
        Assert.Equal("tomato", b.Farm.Selected);

        // Ayni komutlarla ayni yere varir (koruma ve yabani sayac dahil)
        var ra = new Rng(5); var rb = new Rng(5);
        for (int i = 0; i < 400; i++) { a.Farm.Tick(0.5f, true); b.Farm.Tick(0.5f, true); }
        var ha = a.Farm.HarvestFull(30, 30, ra); var hb = b.Farm.HarvestFull(30, 30, rb);
        Assert.Equal(ha?.Yield, hb?.Yield);
        var c1 = Reg(a).Capture(SaveScope.World); var c2 = Reg(b).Capture(SaveScope.World);
        Assert.True(c1.Sections["farm.genetics"].Data.AsSpan().SequenceEqual(c2.Sections["farm.genetics"].Data));
        Assert.True(c1.Sections["farm"].Data.AsSpan().SequenceEqual(c2.Sections["farm"].Data));
    }
}
