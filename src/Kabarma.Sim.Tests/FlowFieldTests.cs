using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Akis alani (TEKNIK 8, 8.1): maliyet, kirma maliyetli engeller, capraz sizinti, artimli onarim = tam hesap, A*.</summary>
public class FlowFieldTests
{
    static FlowFieldFile Def => TestData.Load().FlowField;

    static FlowGrid Grid(int size = 32) => new(Def, 0, 0, size);

    /// <summary>Hucre (x, z) merkezinin dunya konumu.</summary>
    static (float x, float z) W(FlowGrid g, int x, int z) => g.Center(g.Index(x, z));

    [Fact]
    public void Data_matches_docs()
    {
        var d = Def;
        Assert.Equal(160, d.GridSize);   // TEKNIK 8.1: 160 m pencere
        Assert.Equal("walk", d.StampOf("foundation"));
        Assert.Equal("walk", d.StampOf("floor"));
        Assert.Equal("wall", d.StampOf("wall"));
        Assert.Equal("fence", d.StampOf("fence"));
        Assert.Equal("ignore", d.StampOf("roof"));
        Assert.Equal("wall", d.StampOf("bilinmeyen_tur"));
        Assert.True(d.BreakOf("wall") > d.OpenCost * 10, "duvar dolanmadan pahali olmali (kapali degil, kirma maliyetli)");
        Assert.Equal(0, d.BreakOf("doorOpen"));
    }

    [Fact]
    public void Open_field_points_to_goal_and_cost_is_octile()
    {
        var g = Grid();
        var set = new FlowFieldSet(g);
        set.SetGoals("kazan", new[] { W(g, 16, 16) });
        var (x, z) = W(g, 6, 16);
        var s = set.Sample("kazan", x, z);
        Assert.True(s.Valid);
        Assert.True(s.DirX > 0.99f, $"dogu yonu bekleniyor: {s.DirX},{s.DirZ}");
        Assert.Equal(10 * g.OpenCost, s.Cost);
        var (dx, dz) = W(g, 6, 6); // capraz 10 adim
        Assert.Equal(10 * g.OpenCost * 14 / 10, set.Sample("kazan", dx, dz).Cost);
        Assert.True(set.Sample("kazan", W(g, 16, 16).x, W(g, 16, 16).z).AtGoal);
        Assert.False(set.Sample("kazan", -100, -100).Valid); // pencere disi
    }

    [Fact]
    public void Wall_is_breakable_cost_not_closed_and_reports_blocker()
    {
        var g = Grid();
        var set = new FlowFieldSet(g);
        // Hedefi tamamen saran kapali halka (enclosed base): yol yine bulunur, duvardan gecer.
        var ring = new List<int>();
        for (int i = 10; i <= 22; i++) { ring.Add(g.Index(i, 10)); ring.Add(g.Index(i, 22)); ring.Add(g.Index(10, i)); ring.Add(g.Index(22, i)); }
        g.Stamp(7, "wall", ring);
        set.SetGoals("kazan", new[] { W(g, 16, 16) });
        var (x, z) = W(g, 3, 16);
        var s = set.Sample("kazan", x, z);
        Assert.True(s.Valid, "kapali us da yol vermeli (duvar kirma maliyetli)");
        Assert.True(s.Cost >= Def.BreakOf("wall"));
        // Duvarin hemen disinda: sonraki hucre duvar, sahibi 7.
        var (ox, oz) = W(g, 9, 16);
        var o = set.Sample("kazan", ox, oz);
        Assert.True(o.Blocked);
        Assert.Equal(7UL, o.Blocker);
        Assert.Equal("wall", o.BlockerKind);
    }

    [Fact]
    public void Gap_in_wall_is_preferred_over_breaking()
    {
        var g = Grid();
        var set = new FlowFieldSet(g);
        var wall = Enumerable.Range(0, 32).Where(z => z != 3).Select(z => g.Index(16, z)).ToList(); // z=3'te kapi araligi
        g.Stamp(1, "wall", wall);
        set.SetGoals("crop", new[] { W(g, 25, 16) });
        var (x, z) = W(g, 8, 16);
        var s = set.Sample("crop", x, z);
        Assert.True(s.Valid);
        Assert.False(s.Blocked);
        // Yol aralikta duvari gecer: maliyet duvar kirma maliyetinden az.
        Assert.True(s.Cost < Def.BreakOf("wall"), $"cost {s.Cost}");
        // Kapi kapali (door) olunca aralik da kirma ister; acik kapi (doorOpen) yurunur.
        g.Stamp(2, "door", new[] { g.Index(16, 3) });
        set.Tick(10, int.MaxValue);
        Assert.True(set.Sample("crop", x, z).Cost >= Def.BreakOf("door"));
        g.Stamp(2, "doorOpen", new[] { g.Index(16, 3) });
        set.Tick(10, int.MaxValue);
        Assert.True(set.Sample("crop", x, z).Cost < Def.BreakOf("wall"));
    }

    [Theory]
    [InlineData(0.37f)]
    [InlineData(0.785f)]
    [InlineData(1.1f)]
    [InlineData(2.6f)]
    public void Rotated_wall_ring_does_not_leak_diagonally(float yaw)
    {
        // Ekseni egik 8 duvar parcasindan sekizgen halka; hedef icinde. Duvar kirmadan iceri girilemez.
        var g = Grid(48);
        var set = new FlowFieldSet(g);
        var (cx, cz) = W(g, 24, 24);
        for (int k = 0; k < 8; k++)
        {
            float a = yaw + k * MathF.PI / 4f, r = 9f;
            float px = cx + MathF.Cos(a) * r, pz = cz + MathF.Sin(a) * r;
            // Parca yerel x'i teget yonunde: Godot Y donusu yerel x -> (cos y, -sin y) = teget (-sin a, cos a).
            float rot = MathF.Atan2(-MathF.Cos(a), -MathF.Sin(a));
            g.Stamp((ulong)(10 + k), "wall", g.CellsUnderBox(px, pz, 3.9f, 0.15f, rot));
        }
        set.SetGoals("kazan", new[] { (cx, cz) });
        var (x, z) = W(g, 2, 2);
        var s = set.Sample("kazan", x, z);
        Assert.True(s.Valid);
        Assert.True(s.Cost >= Def.BreakOf("wall"), $"egik halka sizdirdi: yaw={yaw} cost={s.Cost}");
    }

    [Fact]
    public void Box_raster_is_conservative()
    {
        var g = Grid();
        var (cx, cz) = W(g, 10, 10);
        // Hucre merkezinde 0,2 m kare: tek hucre.
        Assert.Single(g.CellsUnderBox(cx, cz, 0.1f, 0.1f, 0f));
        // Iki hucrenin sinirinda ince duvar: iki hucre de.
        Assert.Equal(2, g.CellsUnderBox(cx + 0.5f, cz, 0.05f, 0.1f, 0f).Count());
        // 45 derece 4 m duvar: kosegen hucreler ve komsulari (sizinti olmasin diye en az 6).
        Assert.True(g.CellsUnderBox(cx, cz, 2f, 0.1f, MathF.PI / 4f).Count() >= 6);
    }

    [Fact]
    public void Cliff_is_impassable_and_slope_costs_more()
    {
        var g = Grid();
        Assert.Equal(FlowGrid.Impassable, g.TerrainCost(Def.MaxSlopeDegrees + 1, false));
        Assert.True(g.TerrainCost(20, false) > g.TerrainCost(0, false));
        Assert.True(g.TerrainCost(0, true) > g.TerrainCost(0, false));
        // Ucurum seridi bir araliktan dolasilir.
        var set = new FlowFieldSet(g);
        for (int z = 0; z < 32; z++) if (z != 30) g.SetTerrain(g.Index(16, z), FlowGrid.Impassable);
        set.SetGoals("k", new[] { W(g, 24, 16) });
        var (x, z0) = W(g, 8, 16);
        var s = set.Sample("k", x, z0);
        Assert.True(s.Valid);
        Assert.True(s.Cost > 16 * g.OpenCost, "aralikten dolasmali");
    }

    [Fact]
    public void Removing_eaten_goal_redirects_to_next_crop()
    {
        var g = Grid();
        var set = new FlowFieldSet(g);
        set.SetGoals("crop", new[] { W(g, 20, 16), W(g, 4, 16) });
        var (x, z) = W(g, 17, 16);
        Assert.True(set.Sample("crop", x, z).DirX > 0.9f); // yakin ekin dogu
        set.SetGoals("crop", new[] { W(g, 4, 16) }); // dogudaki yendi
        set.Tick(10, int.MaxValue);
        Assert.True(set.Sample("crop", x, z).DirX < -0.9f);
        Assert.Equal(13 * g.OpenCost, set.Sample("crop", x, z).Cost);
    }

    /// <summary>Rastgele iz ekle/kaldir, zemin ve hedef degisimi: artimli onarim her adimda tam hesapla ayni maliyeti verir.</summary>
    [Fact]
    public void Incremental_repair_equals_full_recompute()
    {
        var rng = new Random(1234);
        var g = Grid(40);
        var set = new FlowFieldSet(g);
        var f = set.Field("crop");
        var goals = new List<(float, float)> { W(g, 20, 20), W(g, 5, 33) };
        set.SetGoals("crop", goals);
        f.Update();
        var owners = new List<ulong>();
        ulong next = 1;
        string[] kinds = { "wall", "fence", "door", "doorOpen", "trap", "station" };
        int repairs = 0;
        for (int step = 0; step < 250; step++)
        {
            int op = rng.Next(10);
            if (op < 4 || owners.Count == 0)
            {
                float yaw = (float)(rng.NextDouble() * Math.PI);
                var (cx, cz) = W(g, rng.Next(40), rng.Next(40));
                ulong o = next++;
                g.Stamp(o, kinds[rng.Next(kinds.Length)], g.CellsUnderBox(cx, cz, 0.5f + (float)rng.NextDouble() * 3f, 0.15f, yaw));
                owners.Add(o);
            }
            else if (op < 7)
            {
                int k = rng.Next(owners.Count);
                g.Unstamp(owners[k]);
                owners.RemoveAt(k);
            }
            else if (op < 8)
            {
                int i = g.Index(rng.Next(40), rng.Next(40));
                g.SetTerrain(i, rng.Next(4) == 0 ? FlowGrid.Impassable : g.OpenCost + rng.Next(30));
            }
            else
            {
                if (rng.Next(2) == 0 && goals.Count > 1) goals.RemoveAt(rng.Next(goals.Count));
                else goals.Add(W(g, rng.Next(40), rng.Next(40)));
                set.SetGoals("crop", goals);
            }
            if (f.Update()) repairs++;
            var full = new FlowField(g, "full");
            full.SetGoals(f.Goals);
            full.Recompute();
            for (int i = 0; i < g.Count; i++)
                if (f.Dist(i) != full.Dist(i)) Assert.Fail($"adim {step} op {op}: hucre {g.X(i)},{g.Z(i)} onarim {f.Dist(i)} tam {full.Dist(i)}");
        }
        Assert.True(f.Repairs > 100, $"artimli onarim calismadi (repairs={f.Repairs}, recomputes={f.Recomputes})");
    }

    /// <summary>
    /// Karelere bolunmus is (Step, kucuk butce): is surerken izgara ve hedef degisir; sorgular eski alani okur (is
    /// bitene kadar on tampon degismez), her is bittiginde ve sonda alan tam hesapla ayni maliyeti verir.
    /// </summary>
    [Fact]
    public void Sliced_repair_with_changes_mid_job_equals_full_recompute()
    {
        var rng = new Random(77);
        var g = Grid(40);
        var set = new FlowFieldSet(g);
        var f = set.Field("crop");
        var goals = new List<(float, float)> { W(g, 20, 20), W(g, 5, 33) };
        set.SetGoals("crop", goals);
        f.Update();
        var owners = new List<ulong>();
        ulong next = 1;
        string[] kinds = { "wall", "fence", "door", "doorOpen", "trap", "station" };
        int checks = 0, sliced = 0;
        void Check(string at)
        {
            var full = new FlowField(g, "full");
            full.SetGoals(f.Goals);
            full.Recompute();
            for (int i = 0; i < g.Count; i++)
                if (f.Dist(i) != full.Dist(i)) Assert.Fail($"{at}: hucre {g.X(i)},{g.Z(i)} dilimli {f.Dist(i)} tam {full.Dist(i)}");
            checks++;
        }
        for (int step = 0; step < 300; step++)
        {
            int op = rng.Next(10);
            if (op < 4 || owners.Count == 0)
            {
                var (cx, cz) = W(g, rng.Next(40), rng.Next(40));
                ulong o = next++;
                g.Stamp(o, kinds[rng.Next(kinds.Length)], g.CellsUnderBox(cx, cz, 0.5f + (float)rng.NextDouble() * 4f, 0.15f, (float)(rng.NextDouble() * Math.PI)));
                owners.Add(o);
            }
            else if (op < 7) { int k = rng.Next(owners.Count); g.Unstamp(owners[k]); owners.RemoveAt(k); }
            else if (op < 8) g.SetTerrain(g.Index(rng.Next(40), rng.Next(40)), rng.Next(4) == 0 ? FlowGrid.Impassable : g.OpenCost + rng.Next(30));
            else
            {
                if (rng.Next(2) == 0 && goals.Count > 1) goals.RemoveAt(rng.Next(goals.Count));
                else goals.Add(W(g, rng.Next(40), rng.Next(40)));
                set.SetGoals("crop", goals);
            }
            // Birkac kucuk dilim; is surerken on tampon degismez.
            int slices = rng.Next(4);
            for (int s = 0; s < slices && f.Dirty; s++)
            {
                bool wasBusy = f.Busy;
                var before = wasBusy ? Enumerable.Range(0, g.Count).Select(f.Dist).ToArray() : null;
                f.Step(37);
                if (f.Busy) sliced++;
                if (wasBusy && f.Busy)
                    for (int i = 0; i < g.Count; i++) Assert.Equal(before![i], f.Dist(i));
            }
            if (step % 10 == 9) while (f.Dirty) f.Step(37); // ara ara dilimlerle sonuna kadar
            if (!f.Dirty) Check($"adim {step} is bitti");
        }
        f.Update();
        Check("son");
        Assert.True(sliced > 50, $"is karelere bolunmedi ({sliced})");
        Assert.True(checks > 20, $"az denetim ({checks})");
    }

    [Fact]
    public void Tick_spends_at_most_cell_budget()
    {
        var g = Grid(160);
        var set = new FlowFieldSet(g);
        set.SetGoals("kazan", new[] { W(g, 80, 80) });
        set.Field("kazan").Update();
        g.Stamp(1, "wall", g.CellsUnderBox(W(g, 80, 70).x, W(g, 80, 70).z, 7f, 0.15f, 0f));
        int ticks = 0;
        var f = set.Field("kazan");
        while (f.Dirty) { set.Tick(cells: 500); ticks++; Assert.True(ticks < 1000); }
        Assert.True(ticks > 2, $"is tek tikte bitti ({ticks}); butce uygulanmiyor");
        var full = new FlowField(g, "full");
        full.SetGoals(f.Goals);
        full.Recompute();
        for (int i = 0; i < g.Count; i++) Assert.Equal(full.Dist(i), f.Dist(i));
    }

    [Fact]
    public void Repair_touches_less_than_full()
    {
        var g = Grid(160);
        var set = new FlowFieldSet(g);
        var f = set.Field("kazan");
        set.SetGoals("kazan", new[] { W(g, 80, 80) });
        f.Update();
        int full = f.LastTouched;
        g.Stamp(1, "wall", g.CellsUnderBox(W(g, 40, 40).x, W(g, 40, 40).z, 2f, 0.15f, 0.3f));
        f.Update();
        Assert.True(f.LastTouched < full / 4, $"tek duvar {f.LastTouched} hucre, tam {full}");
    }

    [Fact]
    public void Tick_respects_budget_and_old_field_stays_valid()
    {
        var g = Grid();
        var set = new FlowFieldSet(g);
        foreach (var k in new[] { "a", "b", "c" }) { set.SetGoals(k, new[] { W(g, 16, 16) }); set.Field(k).Update(); }
        g.Stamp(1, "wall", new[] { g.Index(15, 16) });
        Assert.Equal(1, set.Tick(1));
        Assert.Equal(1, set.Tick(1));
        Assert.Equal(1, set.Tick(1));
        Assert.Equal(0, set.Tick(1));
        Assert.True(set.Sample("a", W(g, 4, 16).x, W(g, 4, 16).z).Valid);
    }

    [Fact]
    public void AStar_breaks_through_only_when_cheaper()
    {
        var g = Grid();
        var set = new FlowFieldSet(g);
        var wall = Enumerable.Range(0, 32).Where(z => z != 2).Select(z => g.Index(16, z)).ToList();
        g.Stamp(5, "wall", wall);
        var (fx, fz) = W(g, 8, 16);
        var (tx, tz) = W(g, 24, 16);
        var p = set.FindPath(fx, fz, tx, tz);
        Assert.NotEmpty(p);
        Assert.Equal(g.CellAt(fx, fz), p[0]);
        Assert.Equal(g.CellAt(tx, tz), p[^1]);
        Assert.DoesNotContain(p, c => g.Stamped(c)); // 14 m dolanma < duvar kirma
        g.Stamp(6, "wall", new[] { g.Index(16, 2) }); // aralik da kapandi: duvardan gecer
        var q = set.FindPath(fx, fz, tx, tz);
        Assert.Contains(q, c => g.BlockerAt(c) is { } b && (b.owner == 5 || b.owner == 6));
    }

    [Fact]
    public void Stamp_replace_and_unstamp_restore_cost()
    {
        var g = Grid();
        int c = g.Index(3, 3);
        int before = g.Cost(c);
        g.Stamp(9, "fence", new[] { c });
        Assert.Equal(before + Def.BreakOf("fence"), g.Cost(c));
        g.Stamp(10, "wall", new[] { c }); // ust uste: en pahalisi
        Assert.Equal(before + Def.BreakOf("wall"), g.Cost(c));
        Assert.Equal(10UL, g.BlockerAt(c)!.Value.owner);
        g.Unstamp(10);
        Assert.Equal(9UL, g.BlockerAt(c)!.Value.owner);
        g.Stamp(9, "fence", Array.Empty<int>());
        Assert.Equal(before, g.Cost(c));
        Assert.Null(g.BlockerAt(c));
    }
}
