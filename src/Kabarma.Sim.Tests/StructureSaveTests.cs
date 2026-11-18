using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Yapi kayit bolumu "build" (TEKNIK 9.1): gidis-donus ayni bayt, aciklik ve yuk yuklemede yeniden kurulur.</summary>
public class StructureSaveTests
{
    static SaveRegistry Reg(GameState s) => new SaveRegistry().AddRange(s.SaveSections());

    static StructurePiece P(string shape, int units, float cy, bool hor = false, bool grounded = false, int[]? by = null, int[]? sup = null) => new()
    {
        Shape = shape, Units = units, CenterY = cy, Horizontal = hor, Grounded = grounded, Pos = new[] { 1f, cy, 2f },
        SupportedBy = (by ?? Array.Empty<int>()).ToList(), Supports = (sup ?? Array.Empty<int>()).ToList(),
    };

    [Fact]
    public void Build_section_round_trips_byte_identical_with_derived_balance_rebuilt()
    {
        var a = new GameState(TestData.Load());
        var g = a.Build.Graph;
        int f = g.Add(P("kare_temel", 6, -0.3f, grounded: true));
        int w = g.Add(P("duvar", 4, 1.5f, by: new[] { f }));
        int fl = g.Add(P("kare_zemin", 4, 3f, hor: true, by: new[] { w }));
        Assert.True(g.TrySetMaterial(w, "kerpic_tas").Valid);
        var plan = P("duvar", 4, 4.5f, by: new[] { fl });
        plan.Planned = true;
        int pl = g.Add(plan);
        g.Get(pl)!.Delivered["wood"] = 3;
        a.Build.PlanBook.Save(2, PlanBook.Capture(g, f, 200, "kulübe")!);

        var save = SaveCodec.Decode(SaveCodec.Encode(Reg(a).Capture(SaveScope.World, a.Manifest("yapi"))));
        Assert.Contains("build", save.Sections.Keys);
        var b = new GameState(TestData.Load());
        var graphBefore = b.Build.Graph; // Godot katmani bu nesneye bakar: kayit yerinde doldurmali
        Assert.Empty(Reg(b).Restore(save).Missing);
        Assert.Same(graphBefore, b.Build.Graph);
        var again = Reg(b).Capture(SaveScope.World);
        Assert.True(save.Sections["build"].Data.AsSpan().SequenceEqual(again.Sections["build"].Data), "'build' gidis-donuste degisti");

        var bg = b.Build.Graph;
        Assert.Equal(4, bg.Pieces.Count);
        Assert.Equal("kerpic_tas", bg.Get(w)!.Material);
        Assert.Equal(1, bg.Get(fl)!.Span);
        Assert.Equal(g.Get(w)!.Load, bg.Get(w)!.Load, 3);
        Assert.True(bg.Get(pl)!.Planned);
        Assert.Equal(3, bg.Get(pl)!.Delivered["wood"]);
        Assert.Equal(12, b.Build.PlanBook.Slots.Count);
        Assert.Equal(4, b.Build.PlanBook.Get(2)!.Pieces.Count);
        // Yuklenen graf ayni sekilde calismaya devam eder.
        Assert.Equal(g.Remove(w).Select(p => p.Id), bg.Remove(w).Select(p => p.Id));
    }
}
