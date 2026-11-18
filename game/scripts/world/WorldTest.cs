using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Basliksiz dunya sinamasi (--test=world): URETIM 6 M1 dunya kurallarini olcer.
///  - 300 x 300 m; ussun 42 m'si icinde yukseklik MVP formulu ile ayni (Director'un sabit konumlari kaymaz).
///  - Tarla alani (Ocak'in 12 m'si) egimi < 15 derece (GDD 8.1).
///  - Gol: her su hucresi Ocak'a 60-90 m; IsWater gol tabaninda dogru, ussta yanlis.
///  - Kaynaklar: tas, kil (kiyida), bakir dugumleri; Lif ve Bugday tutamlari, ussa yakin kumeler; TakeTuft kurali.
///  - 8 bitki + 4 kaya turu yerlesti; 5 gun saati; 4 yer imi (tepe en yuksek ic nokta).
/// Cikti: "WORLD RESULT ok|fail ..." ve cikis kodu.
/// </summary>
public partial class WorldTest : Node3D
{
    readonly List<string> _fail = new();
    readonly List<string> _info = new();

    void Check(bool ok, string what) { if (!ok) _fail.Add(what); }

    public override void _Ready()
    {
        try { Run(); }
        catch (System.Exception e)
        {
            GD.Print($"WORLD RESULT fail exception {e.GetType().Name}: {e.Message}");
            GD.PrintErr(e.ToString());
            GetTree().Quit(1);
        }
    }

    void Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var w = new World { Name = "World" };
        AddChild(w);
        _info.Add($"build_ms={sw.ElapsedMilliseconds}");
        var t = w.Data.Biomes.World.Terrain;

        // 1. Boyut
        Check(Mathf.IsEqualApprox(w.Size, 300f), $"size={w.Size}");

        // 2. Ussun yuksekligi MVP formuluyle ayni (Director, bot ve diger akislarin sabit konumlari).
        var mvp = new FastNoiseLite { Seed = 4417, Frequency = 0.012f, FractalOctaves = 3, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        float Mvp(float x, float z)
        {
            float n = mvp.GetNoise2D(x, z) * 9f + mvp.GetNoise2D(x * 3f, z * 3f) * 0.8f;
            float d = new Vector2(x, z).Length();
            return n * Mathf.SmoothStep(18f, 42f, d) + Mathf.SmoothStep(55f, 80f, d) * 10f;
        }
        float worst = 0;
        for (int x = -42; x <= 42; x++)
        for (int z = -42; z <= 42; z++)
            if (new Vector2(x, z).Length() <= 42f) worst = Mathf.Max(worst, Mathf.Abs(w.HeightAt(x, z) - Mvp(x, z)));
        Check(worst < 1e-3f, $"base heights changed (max diff {worst:0.0000} m)");
        _info.Add($"base_diff={worst:0.00000}");

        // 3. Tarla egimi
        float maxSlope = 0;
        for (float x = -12; x <= 12; x += 0.5f)
        for (float z = -12; z <= 12; z += 0.5f)
            if (new Vector2(x, z).Length() <= 12f) maxSlope = Mathf.Max(maxSlope, w.SlopeAt(x, z));
        Check(maxSlope < 15f, $"farm slope {maxSlope:0.0} >= 15");
        _info.Add($"farm_slope_max={maxSlope:0.0}");

        // 4. Gol
        var lake = w.Data.Biomes.World.Lake;
        int water = 0; float dmin = 1e9f, dmax = 0;
        for (int x = -150; x <= 150; x++)
        for (int z = -150; z <= 150; z++)
        {
            if (w.HeightAt(x, z) >= w.WaterLevel - 0.01f) continue;
            if (w.ShoreDistance(x, z) > 0.5f) continue;
            water++;
            float d = new Vector2(x, z).Length();
            dmin = Mathf.Min(dmin, d); dmax = Mathf.Max(dmax, d);
        }
        Check(water > 200, $"lake cells {water}");
        Check(dmin >= lake.OcakDistance[0] && dmax <= lake.OcakDistance[1], $"lake distance {dmin:0.0}-{dmax:0.0} not in {lake.OcakDistance[0]}-{lake.OcakDistance[1]}");
        float deepest = w.WaterLevel - w.HeightAt(lake.Center[0], lake.Center[1]);
        Check(deepest > 0.3f && deepest <= 1.2f, $"lake depth {deepest:0.00} (sig olmali, oyuncu yurur)");
        Check(w.IsWater(w.Ground(new Vector3(lake.Center[0], 0, lake.Center[1]))), "IsWater(lake center) false");
        Check(!w.IsWater(w.Ground(Vector3.Zero)), "IsWater(Ocak) true");
        _info.Add($"lake_cells={water} ocak_dist={dmin:0.0}-{dmax:0.0} depth={deepest:0.00} level={w.WaterLevel:0.00}");

        // 5. Kaynaklar
        int Count(string item) => w.Rocks.Count(r => r.Item == item);
        Check(Count("stone") >= 12, $"stone rocks {Count("stone")}");
        Check(Count("clay") >= 3, $"clay beds {Count("clay")}");
        Check(Count("copper_ore") >= 2, $"copper veins {Count("copper_ore")}");
        foreach (var r in w.Rocks.Where(r => r.Item == "clay"))
        {
            float sd = w.ShoreDistance(r.GlobalPosition.X, r.GlobalPosition.Z);
            Check(sd > -0.5f && sd < 4f, $"clay bed not on the shore ({sd:0.0} m)");
        }
        int fiber = w.Tufts.Count(x => x.Item == "fiber"), wheat = w.Tufts.Count(x => x.Item == "wheat");
        Check(fiber >= 50 && wheat >= 20, $"tufts fiber={fiber} wheat={wheat}");
        Check(w.Tufts.Count(x => x.Item == "fiber" && x.Position.Length() < 25f) >= 4, "no fiber near base");
        Check(w.Tufts.Count(x => x.Item == "wheat" && x.Position.Length() < 30f) >= 3, "no wheat near base");
        var tuft = w.Tufts.First(x => x.Item == "fiber");
        int left = tuft.Left;
        Check(w.TakeTuft(tuft, 2) == Mathf.Min(2, left) && tuft.Left == left - Mathf.Min(2, left), "TakeTuft partial");
        w.TakeTuft(tuft, 999);
        Check(tuft.Left == 0 && w.NearestTuft(tuft.Position, 0.01f) != tuft, "TakeTuft empty / NearestTuft skips empty");
        Check(w.Tufts.Where(x => x.Item != "").All(x => x.PerHit == w.Biome.Decor.First(d => d.Id == x.Kind).PerHit), "tuft perHit != biomes.json");
        // Tas yalniz altin kayadan; ilk gunun ihtiyaci ussun yakininda (ana oturum karari 18 Kasim 2026).
        var kaya = w.Biome.Nodes.First(n => n.Item == "stone");
        Check(w.Biome.Nodes.Where(n => n.Item == "stone").All(n => n.Model.EndsWith("kaya/kaya.glb")), "stone from a non-golden rock");
        float nearR = kaya.NearBase.Count > 0 ? kaya.NearBase[0] : 35f;
        int nearStone = w.Rocks.Where(r => r.Item == "stone" && new Vector2(r.GlobalPosition.X, r.GlobalPosition.Z).Length() <= nearR).Sum(r => r.Stones);
        int placeStone = w.Data.Tool.Placeables.Sum(p => p.Cost.TryGetValue("stone", out int n) ? n : 0)
            + w.Data.Tool.Recipes.Where(r => r.UnlockOn == null || !r.UnlockOn.Contains(':')).Sum(r => r.In.TryGetValue("stone", out int n) ? n : 0); // zincir M2: arastirma ve Ocak basamagi kilitli tarifler ilk gunun ihtiyaci degil
        Check(kaya.NearBase.Count < 2 || nearStone >= kaya.NearBase[1], $"stone near base {nearStone} < need {(kaya.NearBase.Count > 1 ? kaya.NearBase[1] : 0)}");
        Check(nearStone >= placeStone, $"stone near base {nearStone} < placeable+recipe stone {placeStone}");
        int farStone = w.Rocks.Count(r => r.Item == "stone" && new Vector2(r.GlobalPosition.X, r.GlobalPosition.Z).Length() > nearR + 10f);
        Check(farStone >= kaya.ScatterCount * 3 / 4, $"scattered stone rocks {farStone} < {kaya.ScatterCount}");
        Check(w.Rocks.Where(r => r.Item == "clay").All(r => r.FromModel), "clay bed not from kil_yatagi model");
        _info.Add($"stone={Count("stone")} near_stone={nearStone} need={placeStone} far_stone_rocks={farStone} clay={Count("clay")} copper={Count("copper_ore")} fiber={fiber} wheat={wheat}");

        // 6. Bitki ve kaya turleri (8 bitki: 7 dekor + Salgam tarlada; 4 kaya: altin kaya + 3).
        var plants = new[] { "agac", "turuncu_agac", "cali", "ot", "bugday", "cicek", "egrelti" };
        foreach (var id in plants.Concat(new[] { "kaya_duz", "kaya_dik" }))
        {
            var def = w.Biome.Decor.First(d => d.Id == id);
            int got = w.DecorCounts.TryGetValue(id, out var c) ? c : 0;
            Check(got >= def.Count / 2, $"decor {id} placed {got}/{def.Count}");
        }
        Check(w.TreePositions.Count >= 60, $"trees {w.TreePositions.Count}");
        _info.Add("decor=" + string.Join(",", w.DecorCounts.Select(kv => $"{kv.Key}:{kv.Value}")) + $" grass={w.GrassInstances}");

        // 7. Gun saatleri
        foreach (var key in World.ShotTimes.Append("normal_gece"))
        {
            Check(w.Biome.Times.ContainsKey(key), $"time {key} missing");
            w.ForceTime(key);
            var dir = -w.Sun.GlobalBasis.Z; // isigin gittigi yon
            float elev = Mathf.RadToDeg(Mathf.Asin(-dir.Y));
            Check(Mathf.Abs(elev - w.Biome.Times[key].LightElevation) < 0.5f, $"time {key} light elevation {elev:0.0}");
        }
        w.ForceTime(null);

        // 8. Yer imleri
        Check(w.Biomes.World.Bookmarks.Count == 4, "bookmarks != 4");
        float interiorMax = -1e9f;
        for (int x = -114; x <= 114; x++)
        for (int z = -114; z <= 114; z++) interiorMax = Mathf.Max(interiorMax, w.HeightAt(x, z));
        foreach (var b in w.Biomes.World.Bookmarks)
        {
            var xf = w.BookmarkTransform(b);
            Check(Mathf.Abs(xf.Origin.X) < 150 && Mathf.Abs(xf.Origin.Z) < 150, $"bookmark {b.Id} outside world");
            Check(xf.Origin.Y > w.HeightAt(xf.Origin.X, xf.Origin.Z) + 1f, $"bookmark {b.Id} under ground");
        }
        var tepe = w.Bookmark("tepe");
        float tepeH = w.HeightAt(tepe.Eye[0], tepe.Eye[1]);
        Check(tepeH >= interiorMax - 1f, $"tepe {tepeH:0.0} m is not the highest hill ({interiorMax:0.0})");
        var gol = w.BookmarkTransform(w.Bookmark("gol"));
        var toLake = (w.LakeCenter - gol.Origin).Normalized();
        Check((-gol.Basis.Z).Dot(toLake) > Mathf.Cos(Mathf.DegToRad(30f)), "lake not in gol bookmark frame");
        _info.Add($"tepe={tepeH:0.0} interior_max={interiorMax:0.0}");

        foreach (var i in _info) GD.Print("WORLD " + i);
        if (_fail.Count == 0) GD.Print($"WORLD RESULT ok checks passed ({sw.ElapsedMilliseconds} ms)");
        else foreach (var f in _fail) GD.Print("WORLD FAIL " + f);
        if (_fail.Count > 0) GD.Print($"WORLD RESULT fail {_fail.Count}");
        GetTree().Quit(_fail.Count == 0 ? 0 : 1);
    }
}
