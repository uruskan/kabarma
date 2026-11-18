using System.Text.Json;
using Kabarma.Sim;
using Xunit;

namespace Kabarma.Sim.Tests;

/// <summary>Tasima dengesi (GDD §7): kopru, kule, sokulen ayak; artimli = tam cozum; bedeller, plan modu, Plan Defteri.</summary>
public class StructureTests
{
    static StructureGraph Graph(out BuildFile b)
    {
        b = TestData.Load().Build;
        var g = new StructureGraph();
        g.Bind(b);
        return g;
    }

    static StructurePiece P(string shape, int units, float cy, bool horizontal = false, bool grounded = false, string mat = "wood", int[]? by = null, int[]? sup = null, float x = 0, float z = 0) => new()
    {
        Shape = shape, Units = units, CenterY = cy, Horizontal = horizontal, Grounded = grounded, Material = mat,
        Pos = new[] { x, cy, z }, SupportedBy = (by ?? Array.Empty<int>()).ToList(), Supports = (sup ?? Array.Empty<int>()).ToList(),
    };

    static int Foundation(StructureGraph g, float x = 0) => g.Add(P("kare_temel", 6, -0.3f, grounded: true, x: x));
    static int Wall(StructureGraph g, int on, float cy = 1.5f, string mat = "wood") => g.Add(P("duvar", 4, cy, mat: mat, by: new[] { on }));
    /// <summary>Zemin: komsu zeminlerle karsilikli bag (yuva kurali iki yonlu).</summary>
    static int Floor(StructureGraph g, int[] by, string mat = "wood", float x = 0)
    {
        var floors = by.Where(id => g.Get(id)!.Horizontal).ToArray();
        return g.Add(P("kare_zemin", 4, 3f, horizontal: true, mat: mat, by: by, sup: floors, x: x));
    }

    [Fact]
    public void Bridge_spans_three_from_each_side_and_the_middle_needs_stone()
    {
        var g = Graph(out _);
        int wa = Wall(g, Foundation(g, 0));
        int wb = Wall(g, Foundation(g, 24));
        int a1 = Floor(g, new[] { wa }), a2 = Floor(g, new[] { a1 }), a3 = Floor(g, new[] { a2 });
        int b1 = Floor(g, new[] { wb }), b2 = Floor(g, new[] { b1 }), b3 = Floor(g, new[] { b2 });
        Assert.Equal(new[] { 1, 2, 3 }, new[] { a1, a2, a3 }.Select(i => g.Get(i)!.Span));
        Assert.Equal(new[] { 1, 2, 3 }, new[] { b1, b2, b3 }.Select(i => g.Get(i)!.Span));
        // 7 parcalik boslugun ortasi: iki yandan da 4. Ahsap (3) olmaz, Kerpic-Tas (4) olur.
        var wood = g.Evaluate(P("kare_zemin", 4, 3f, horizontal: true, by: new[] { a3, b3 }, sup: new[] { a3, b3 }));
        Assert.False(wood.Valid);
        Assert.Equal("açıklık 4 > 3", wood.Reason);
        var stone = g.Evaluate(P("kare_zemin", 4, 3f, horizontal: true, mat: "kerpic_tas", by: new[] { a3, b3 }, sup: new[] { a3, b3 }));
        Assert.True(stone.Valid, stone.Reason);
        Assert.Equal(1f, stone.Strain);
        // Onizleme grafi degistirmez.
        Assert.Equal(10, g.Pieces.Count);
        int mid = Floor(g, new[] { a3, b3 }, mat: "kerpic_tas");
        Assert.Equal(4, g.Get(mid)!.Span);
        // Ortadaki agir parcanin yuku iki yana esit bolunur: a3 = 4 + 8/2.
        Assert.True(MathF.Abs(g.Get(a3)!.Load - 8f) < 1e-3f, string.Join(" ", g.Pieces.Values.Select(p => $"{p.Id}:{p.Shape}/{p.Material} s{p.Span} L{p.Load} by[{string.Join(",", p.SupportedBy)}] sup[{string.Join(",", p.Supports)}]")));
        Assert.Equal(g.Get(a1)!.Load, g.Get(b1)!.Load, 3);
        Assert.All(g.Pieces.Values, p => Assert.True(p.Stable));
    }

    [Fact]
    public void Wooden_tower_stops_at_its_load_limit_and_a_stone_base_lets_it_grow()
    {
        var g = Graph(out var b);
        int f = Foundation(g);
        var walls = new List<int>();
        int below = f;
        for (int i = 0; i < 20; i++)
        {
            var cand = P("duvar", 4, 1.5f + 3f * i, by: new[] { below });
            var rep = g.Evaluate(cand);
            if (!rep.Valid) { Assert.StartsWith("aşırı yük", rep.Reason); break; }
            below = Wall(g, below, 1.5f + 3f * i);
            walls.Add(below);
        }
        var wood = b.Material("wood")!;
        int expected = (int)(wood.Capacity / (4 * wood.WeightPerUnit));
        Assert.Equal(expected, walls.Count);
        Assert.All(walls, w => Assert.Equal(0, g.Get(w)!.Span)); // dusey yigin aciklik eklemez
        // Tepeye beton koymak alttakini ezer: yukseltme reddedilir, malzeme degismez.
        var heavy = g.TrySetMaterial(walls[^1], "mayali_beton");
        Assert.False(heavy.Valid);
        Assert.Equal("wood", g.Get(walls[^1])!.Material);
        // Tabani Kerpic-Tas'a yukseltince bir kat daha cikar.
        Assert.True(g.TrySetMaterial(walls[0], "kerpic_tas").Valid);
        Assert.True(g.Evaluate(P("duvar", 4, 1.5f + 3f * walls.Count, by: new[] { walls[^1] })).Valid);
    }

    [Fact]
    public void Removing_the_middle_leg_drops_only_the_overlong_floor()
    {
        var g = Graph(out _);
        int wa = Wall(g, Foundation(g, 0));
        int wb = Wall(g, Foundation(g, 24));
        int a1 = Floor(g, new[] { wa }), a2 = Floor(g, new[] { a1 }), a3 = Floor(g, new[] { a2 });
        int leg = g.Add(P("sutun", 2, 1.5f, by: new[] { Foundation(g, 12) }));
        int mid = Floor(g, new[] { leg, a3 });
        int b1 = Floor(g, new[] { wb }), b2 = Floor(g, new[] { b1 }), b3 = Floor(g, new[] { b2, mid });
        Assert.Equal(1, g.Get(mid)!.Span);
        Assert.Equal(2, g.Get(b3)!.Span);
        var fell = g.Remove(leg);
        Assert.Equal(new[] { mid }, fell.Select(p => p.Id));
        Assert.False(g.Pieces.ContainsKey(mid));
        Assert.Equal(3, g.Get(b3)!.Span);
        Assert.Equal(3, g.Get(a3)!.Span);
    }

    [Fact]
    public void Removing_one_of_two_legs_overloads_the_other_and_the_slab_falls()
    {
        var g = Graph(out _);
        int p1 = g.Add(P("sutun", 2, 1.5f, by: new[] { Foundation(g, 0) }));
        int p2 = g.Add(P("sutun", 2, 1.5f, by: new[] { Foundation(g, 3) }));
        int s1 = g.Add(P("kare_zemin", 4, 3f, horizontal: true, mat: "mayali_beton", by: new[] { p1, p2 }));
        int s2 = g.Add(P("kare_zemin", 4, 3f, horizontal: true, mat: "mayali_beton", by: new[] { s1 }, sup: new[] { s1 }));
        int s3 = g.Add(P("kare_zemin", 4, 3f, horizontal: true, mat: "mayali_beton", by: new[] { s2 }, sup: new[] { s2 }));
        Assert.All(new[] { p1, p2 }, id => Assert.False(g.Overloaded(g.Get(id)!)));
        Assert.Equal(2 + 18f, g.Get(p1)!.Load, 3);
        var fell = g.Remove(p2).Select(p => p.Id).ToHashSet();
        Assert.Equal(new HashSet<int> { p1, s1, s2, s3 }, fell);
        Assert.Equal(2, g.Pieces.Count); // iki temel kalir
    }

    [Fact]
    public void Hanging_wall_costs_a_step_and_vertical_stack_does_not()
    {
        var g = Graph(out _);
        int w = Wall(g, Foundation(g));
        int f = Floor(g, new[] { w });
        int up = Wall(g, f, 4.5f);
        int hang = g.Add(P("duvar", 4, 1.5f, by: new[] { f }, x: 3));
        Assert.Equal(1, g.Get(up)!.Span);
        Assert.Equal(2, g.Get(hang)!.Span);
    }

    [Fact]
    public void Incremental_updates_match_a_full_rebuild_and_survive_a_save_round_trip()
    {
        var g = Graph(out var b);
        var rng = new Random(4417);
        var mats = b.Materials.Select(m => m.Id).ToArray();
        for (int step = 0; step < 400; step++)
        {
            var ids = g.Pieces.Keys.ToList();
            if (ids.Count > 4 && rng.NextDouble() < 0.3)
                g.Remove(ids[rng.Next(ids.Count)]);
            else if (ids.Count == 0 || rng.NextDouble() < 0.15)
                g.Add(P("kare_temel", 6, -0.3f, grounded: true, mat: mats[rng.Next(mats.Length)], x: rng.Next(40)));
            else
            {
                var s = g.Get(ids[rng.Next(ids.Count)])!;
                bool hor = rng.NextDouble() < 0.5;
                var by = new List<int> { s.Id };
                if (rng.NextDouble() < 0.3) by.Add(ids[rng.Next(ids.Count)]);
                var sup = hor ? by.Where(i => g.Get(i)!.Horizontal).ToArray() : Array.Empty<int>();
                float cy = s.CenterY + (hor ? 1.5f : (rng.NextDouble() < 0.8 ? 3f : -1.5f));
                var cand = P(hor ? "kare_zemin" : "duvar", rng.Next(1, 7), cy, horizontal: hor, mat: mats[rng.Next(mats.Length)], by: by.Distinct().ToArray(), sup: sup);
                cand.Planned = rng.NextDouble() < 0.1;
                g.Add(cand);
            }
            if (step % 20 == 0 || step == 399) AssertMatchesRebuild(g, b);
        }
    }

    static void AssertMatchesRebuild(StructureGraph g, BuildFile b)
    {
        var json = JsonSerializer.Serialize(g);
        var copy = JsonSerializer.Deserialize<StructureGraph>(json)!;
        copy.Bind(b);
        Assert.Equal(g.Pieces.Keys.OrderBy(x => x), copy.Pieces.Keys.OrderBy(x => x));
        foreach (var (id, p) in g.Pieces)
        {
            var q = copy.Pieces[id];
            Assert.True(p.Span == q.Span, $"piece {id}: span {p.Span} vs {q.Span}");
            Assert.True(MathF.Abs(p.Load - q.Load) < 1e-2f, $"piece {id}: load {p.Load} vs {q.Load}");
        }
    }

    [Fact]
    public void Material_costs_follow_the_shape_units()
    {
        var b = TestData.Load().Build;
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 4 }, StructureCosts.Cost(b, "wood", 4));
        Assert.Equal(new Dictionary<string, int> { ["kerpic"] = 2, ["stone"] = 2 }, StructureCosts.Cost(b, "kerpic_tas", 4));
        Assert.Equal(new Dictionary<string, int> { ["kerpic"] = 2, ["stone"] = 1 }, StructureCosts.Cost(b, "kerpic_tas", 3));
        Assert.Equal(new Dictionary<string, int> { ["celik"] = 6 }, StructureCosts.Cost(b, "celik_sac", 6));
        var up = StructureCosts.Upgrade(b, "wood", 4, inClaim: false)!.Value;
        Assert.Equal("kerpic_tas", up.To.Id);
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 2 }, up.Back); // tapu disi %50
        Assert.Null(StructureCosts.Upgrade(b, "mayali_beton", 4, true));
        Assert.Equal(new Dictionary<string, int> { ["kerpic"] = 1, ["stone"] = 1 }, StructureCosts.RepairCost(StructureCosts.Cost(b, "kerpic_tas", 4), 450, 900, b));
        Assert.Equal(450f, StructureCosts.UpgradedHp(200, 400, 900));
        Assert.Equal(new[] { "wood", "kerpic_tas", "celik_sac", "mayali_beton" }, b.Materials.Select(m => m.Id));
        Assert.Equal(new[] { 3, 4, 6, 8 }, b.Materials.Select(m => m.MaxSpan));
        Assert.Equal(new[] { 400f, 900f, 2000f, 5000f }, b.Materials.Select(m => m.Hp));
    }

    [Fact]
    public void Planned_pieces_wait_for_material_and_for_what_is_below_them()
    {
        var g = Graph(out var b);
        int f = Foundation(g);
        var wallPlan = P("duvar", 4, 1.5f, by: new[] { f });
        wallPlan.Planned = true;
        int w = g.Add(wallPlan);
        // Plan zemini: plan duvari kurulu sayilir (plan modu yerlesimi); dengeye katilmaz.
        var floorPlan = P("kare_zemin", 4, 3f, horizontal: true, by: new[] { w });
        Assert.False(g.Evaluate(floorPlan).Valid);
        floorPlan = P("kare_zemin", 4, 3f, horizontal: true, by: new[] { w });
        Assert.True(g.Evaluate(floorPlan, assumePlanned: true).Valid);
        floorPlan.Planned = true;
        int fl = g.Add(floorPlan);
        Assert.False(g.Get(fl)!.Stable);
        Assert.False(g.Complete(fl).Valid); // once altini bitir

        var bag = new Inventory(8, _ => 999);
        bag.Add("wood", 3);
        var cost = StructureCosts.Cost(b, "wood", 4);
        var moved = StructureCosts.Deliver(cost, g.Get(w)!.Delivered, bag);
        Assert.Equal(3, moved["wood"]);
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 1 }, StructureCosts.Missing(cost, g.Get(w)!.Delivered));
        bag.Add("wood", 5);
        StructureCosts.Deliver(cost, g.Get(w)!.Delivered, bag);
        Assert.Equal(4, bag.Count("wood"));
        Assert.Empty(StructureCosts.Missing(cost, g.Get(w)!.Delivered));
        Assert.True(g.Complete(w).Valid);
        Assert.Equal(0, g.Get(w)!.Span);
        Assert.True(g.Complete(fl).Valid);
        Assert.Equal(1, g.Get(fl)!.Span);
        Assert.Equal(4 + 4f, g.Get(w)!.Load, 3);
    }

    [Fact]
    public void Plan_book_captures_a_structure_and_places_it_rotated()
    {
        var g = Graph(out var b);
        var f0 = P("kare_temel", 6, -0.3f, grounded: true, x: 10, z: 5);
        f0.Yaw = MathF.PI / 2;
        int f = g.Add(f0);
        var wp = P("duvar", 4, 1.5f, by: new[] { f }, x: 11.5f, z: 5);
        wp.Pos[1] = 0f;
        int w = g.Add(wp);
        int fl = Floor(g, new[] { w }, x: 10);
        var bp = PlanBook.Capture(g, fl, b.PlanBook.MaxPieces, "kulübe")!;
        Assert.Equal(new[] { "kare_temel", "duvar", "kare_zemin" }, bp.Pieces.Select(p => p.Shape));
        Assert.Equal(0f, bp.Pieces[0].Yaw, 4);
        // Ayni yere ve yone geri konunca dunya yerleri aynen cikar.
        foreach (var (piece, orig) in bp.Pieces.Zip(new[] { f, w, fl }))
        {
            var (pos, yaw) = PlanBook.ToWorld(piece, g.Get(f)!.Pos, g.Get(f)!.Yaw);
            var o = g.Get(orig)!;
            for (int i = 0; i < 3; i++) Assert.Equal(o.Pos[i], pos[i], 3);
            Assert.Equal(0f, MathF.IEEERemainder(o.Yaw - yaw, MathF.Tau), 3);
        }
        var book = new PlanBook();
        book.EnsureSlots(b.PlanBook.Slots);
        Assert.Equal(12, book.Slots.Count);
        Assert.True(book.Save(11, bp));
        Assert.False(book.Save(12, bp));
        Assert.Equal(new Dictionary<string, int> { ["wood"] = 14 }, bp.TotalCost(b, s => s == "kare_temel" ? 6 : 4));
        var round = JsonSerializer.Deserialize<PlanBook>(JsonSerializer.Serialize(book))!;
        Assert.Equal(3, round.Get(11)!.Pieces.Count);
    }
}
