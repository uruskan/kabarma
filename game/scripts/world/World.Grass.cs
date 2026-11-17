using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: ot tutamlari (SANAT-SES §6 "oyuncu yakininda orneklenmis tutamlar"). 16 m'lik parcalar, her biri bir MultiMesh;
/// parca on ayarin ot yaricapindan (TEKNIK §10 "yuksek": 40 m) sonra cizilmez, shader kenarda boyu kisaltir.
/// Ot Bicme kaynagi degildir (Lif veren ot kumeleri World.Decor.cs'deki "ot" modelidir).
/// </summary>
public partial class World
{
    ShaderMaterial _grassMat;
    public int GrassInstances { get; private set; }

    void BuildGrass()
    {
        var g = Biome.Grass;
        if (g.PerChunk <= 0 || Preset.GrassRadius <= 0) return;
        var mesh = GrassTuftMesh(g);
        _grassMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/grass.gdshader") };
        _grassMat.SetShaderParameter("fade_radius", Preset.GrassRadius);
        _grassMat.SetShaderParameter("wind_strength", g.Wind);
        mesh.SurfaceSetMaterial(0, _grassMat);
        var rng = new RandomNumberGenerator { Seed = (ulong)(T.Seed + 31) };
        int per = Mathf.RoundToInt(g.PerChunk * Preset.PlantDensity);
        int n = Mathf.CeilToInt(Size / g.Chunk);
        float half = Size / 2f;
        var xfs = new List<Transform3D>(per);
        for (int cx = 0; cx < n; cx++)
        for (int cz = 0; cz < n; cz++)
        {
            var origin = new Vector3(-half + (cx + 0.5f) * g.Chunk, 0, -half + (cz + 0.5f) * g.Chunk);
            xfs.Clear();
            for (int i = 0; i < per; i++)
            {
                var p = origin + new Vector3(rng.RandfRange(-0.5f, 0.5f) * g.Chunk, 0, rng.RandfRange(-0.5f, 0.5f) * g.Chunk);
                if (Mathf.Abs(p.X) > half - 1 || Mathf.Abs(p.Z) > half - 1) continue;
                if (new Vector2(p.X, p.Z).Length() < g.BaseClear || InClearing(p) || ShoreDistance(p.X, p.Z) < 0.4f) continue;
                float s = rng.RandfRange(g.Height[0], g.Height.Count > 1 ? g.Height[1] : g.Height[0]);
                var basis = Basis.FromEuler(new Vector3(0, rng.Randf() * Mathf.Tau, 0)).Scaled(new Vector3(s * 1.4f, s, s * 1.4f));
                xfs.Add(new Transform3D(basis, Ground(p) - origin));
            }
            if (xfs.Count == 0) continue;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = xfs.Count };
            for (int i = 0; i < xfs.Count; i++) mm.SetInstanceTransform(i, xfs[i]);
            GrassInstances += xfs.Count;
            AddChild(new MultiMeshInstance3D
            {
                Name = $"Grass_{cx}_{cz}",
                Multimesh = mm,
                Position = origin,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = Preset.GrassRadius + g.Chunk * 0.75f,
            });
        }
    }

    /// <summary>Bes bicakli tutam (1 m boyunda; ornek olcegi boyu verir). Kose rengi: dipte yesil golge, ucta altin (ICERIK §2.1).</summary>
    ArrayMesh GrassTuftMesh(GrassDef g)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var root = Pal("plant", Palette.Plant).Lerp(Pal("shadow", Palette.Shadow), 0.3f);
        var tipCol = Pal("ground", Palette.Ground).Lerp(Pal("fog", Palette.Fog), 0.25f);
        var mid = Pal("plant", Palette.Plant).Lerp(tipCol, 0.45f);
        var r = new RandomNumberGenerator { Seed = 5 };
        const int blades = 5, segs = 3;
        for (int b = 0; b < blades; b++)
        {
            float a = b * Mathf.Tau / blades + r.Randf() * 0.6f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = new Vector3(-dir.Z, 0, dir.X);
            float lean = r.RandfRange(0.15f, 0.35f), w = r.RandfRange(0.035f, 0.05f), h = r.RandfRange(0.75f, 1f);
            var basePos = dir * r.RandfRange(0.02f, 0.08f);
            Vector3 At(float t) => basePos + dir * lean * t * t + Vector3.Up * h * t;
            for (int s = 0; s < segs; s++)
            {
                float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                float w0 = w * (1f - t0), w1 = w * (1f - t1);
                var p0 = At(t0); var p1 = At(t1);
                var n = side.Cross(At(t1) - At(t0)).Normalized();
                Color C(float t) { var c = t < 0.5f ? root.Lerp(mid, t * 2f) : mid.Lerp(tipCol, (t - 0.5f) * 2f); c = c.SrgbToLinear(); c.A = t; return c; }
                void V(Vector3 p, float t) { st.SetColor(C(t)); st.SetNormal(n); st.AddVertex(p); }
                V(p0 - side * w0, t0); V(p1 - side * w1, t1); V(p0 + side * w0, t0);
                V(p0 + side * w0, t0); V(p1 - side * w1, t1); V(p1 + side * w1, t1);
            }
        }
        return st.Commit();
    }
}
