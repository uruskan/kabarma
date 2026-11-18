using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: Sis Korusu'nun havasi (ICERIK §1 "mavi-yesil sis katmanlari, isik huzmeleri"; dunya2). Hacimsel sis "yuksek"
/// on ayarda kapali (TEKNIK §10): katmanlar araziyi izleyen seyrek aglar (fog_layer.gdshader), huzmeler capraz kart
/// MultiMesh'i (light_shaft.gdshader). Sayilar biomes.json "atmosphere". Gun saatine gore ApplyTime gucunu ayarlar.
/// Cizim: katman basina 128 m'lik parcalar (~10), huzmeler tek MultiMesh.
/// </summary>
public partial class World
{
    readonly List<ShaderMaterial> _fogMats = new();
    ShaderMaterial _shaftMat;
    public int FogLayerChunks { get; private set; }
    public int ShaftCount { get; private set; }

    const int FogChunk = 128;

    void BuildAtmosphere()
    {
        foreach (var (id, b) in OtherBiomes)
        {
            var a = b.Atmosphere;
            if (a.FogLayers.Count > 0 && a.FogAlpha > 0) BuildFogLayers(id, a);
            if (a.Shafts > 0 && a.ShaftAlpha > 0) BuildShafts(id, a);
        }
    }

    void BuildFogLayers(string biome, AtmosphereDef a)
    {
        var noise = new NoiseTexture2D
        {
            Width = 256, Height = 256, Seamless = true, GenerateMipmaps = true,
            Noise = new FastNoiseLite { Seed = T.Seed + 91, Frequency = 0.02f, FractalOctaves = 3, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin },
        };
        float cell = Mathf.Max(2f, a.FogCell);
        int n = Mathf.CeilToInt(Size / cell);
        // Agirlik izgarasi bir kez (katmanlar paylasir).
        var w = new float[n + 1, n + 1];
        for (int i = 0; i <= n; i++)
        for (int j = 0; j <= n; j++)
            w[i, j] = Map.Weight(biome, i * cell - Size / 2f, j * cell - Size / 2f);
        var root = new Node3D { Name = "KoruFog" };
        AddChild(root);
        for (int layer = 0; layer < a.FogLayers.Count; layer++)
        {
            float lift = a.FogLayers[layer];
            var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/fog_layer.gdshader") };
            mat.SetShaderParameter("fog_color", new Color(a.FogColor));
            // Ust katmanlar daha ince ve buyuk desenli.
            mat.SetShaderParameter("alpha", a.FogAlpha * (1f - 0.22f * layer));
            mat.SetShaderParameter("scale", 45f + 30f * layer);
            mat.SetShaderParameter("drift", new Vector2(0.010f + 0.004f * layer, 0.006f - 0.003f * layer));
            mat.SetShaderParameter("noise_tex", noise);
            mat.RenderPriority = layer;
            _fogMats.Add(mat);
            int step = FogChunk / (int)cell;
            for (int ci = 0; ci < n; ci += step)
            for (int cj = 0; cj < n; cj += step)
            {
                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);
                int quads = 0;
                for (int i = ci; i < Mathf.Min(ci + step, n); i++)
                for (int j = cj; j < Mathf.Min(cj + step, n); j++)
                {
                    if (w[i, j] + w[i + 1, j] + w[i, j + 1] + w[i + 1, j + 1] < 0.02f) continue;
                    Vector3 V(int x, int z)
                    {
                        float wx = x * cell - Size / 2f, wz = z * cell - Size / 2f;
                        return new Vector3(wx, HeightAt(wx, wz) + lift, wz);
                    }
                    void P(int x, int z) { st.SetColor(new Color(1, 1, 1, w[x, z])); st.AddVertex(V(x, z)); }
                    P(i, j); P(i + 1, j); P(i, j + 1);
                    P(i + 1, j); P(i + 1, j + 1); P(i, j + 1);
                    quads++;
                }
                if (quads == 0) continue;
                var mi = new MeshInstance3D
                {
                    Name = $"Fog{layer}_{ci / step}_{cj / step}", Mesh = st.Commit(), MaterialOverride = mat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                root.AddChild(mi);
                FogLayerChunks++;
            }
        }
    }

    void BuildShafts(string biome, AtmosphereDef a)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(T.Seed + 93) };
        var xfs = new List<(Transform3D xf, Color custom)>();
        float half = Size / 2f - 10f;
        float wdt = a.ShaftSize.Count > 0 ? a.ShaftSize[0] : 3f, len = a.ShaftSize.Count > 1 ? a.ShaftSize[1] : 22f;
        // Huzme yonu: ogle gunesinden asagi (biraz egik), butun huzmeler paralel.
        var noon = Biome.Times.TryGetValue("ogle", out var o) ? o : null;
        var down = noon != null ? -Dir(Mathf.Min(noon.LightElevation, 70f), noon.LightAzimuth) : Vector3.Down;
        for (int tries = 0; tries < a.Shafts * 30 && xfs.Count < a.Shafts; tries++)
        {
            var p = new Vector3(rng.RandfRange(-half, half), 0, rng.RandfRange(-half, half));
            if (Map.Weight(biome, p.X, p.Z) < 0.8f) continue;
            float s = rng.RandfRange(0.7f, 1.3f);
            // Kartin boyu huzme yonunde; dip noktasi zeminde, tepe tac hizasinda.
            var bottom = Ground(p) + Vector3.Up * 0.5f;
            var top = bottom - down * len * s;
            var mid = (top + bottom) * 0.5f;
            var axis = (top - bottom).Normalized();
            var side = axis.Cross(Vector3.Up).Normalized();
            if (side.LengthSquared() < 0.01f) side = Vector3.Right;
            var face = side.Cross(axis).Normalized();
            float spin = rng.Randf() * Mathf.Pi;
            var basis = new Basis(side.Rotated(axis, spin) * wdt * s, face.Rotated(axis, spin), axis * len * s);
            xfs.Add((new Transform3D(basis, mid), new Color(rng.Randf(), rng.Randf(), 0, 0)));
        }
        if (xfs.Count == 0) return;
        // Capraz iki kart: yerel X genislik, Z boy (UV.y tepe 0 dip 1).
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var rot in new[] { 0f, Mathf.Pi / 2f })
        {
            var sx = new Vector3(Mathf.Cos(rot), Mathf.Sin(rot), 0) * 0.5f;
            Vector3 top = new(0, 0, 0.5f), bot = new(0, 0, -0.5f);
            void Q(Vector3 p, Vector2 uv) { st.SetUV(uv); st.AddVertex(p); }
            Q(top - sx, new Vector2(0, 0)); Q(top + sx, new Vector2(1, 0)); Q(bot - sx, new Vector2(0, 1));
            Q(top + sx, new Vector2(1, 0)); Q(bot + sx, new Vector2(1, 1)); Q(bot - sx, new Vector2(0, 1));
        }
        _shaftMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/light_shaft.gdshader") };
        _shaftMat.SetShaderParameter("shaft_color", new Color(a.ShaftColor));
        _shaftMat.SetShaderParameter("alpha", a.ShaftAlpha);
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, _shaftMat);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = xfs.Count };
        for (int i = 0; i < xfs.Count; i++) { mm.SetInstanceTransform(i, xfs[i].xf); mm.SetInstanceCustomData(i, xfs[i].custom); }
        AddChild(new MultiMeshInstance3D { Name = "KoruShafts", Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        ShaftCount = xfs.Count;
    }

    /// <summary>ApplyTime'dan: huzmeler gunesle guclenir (gece yok), sis gece koyulasir.</summary>
    void UpdateAtmosphere(in TimeLight L)
    {
        float sun = Mathf.Clamp(L.SunDisk * Mathf.SmoothStep(2f, 30f, L.Elev), 0f, 1f);
        _shaftMat?.SetShaderParameter("strength", sun);
        foreach (var m in _fogMats)
        {
            m.SetShaderParameter("strength", Mathf.Lerp(0.75f, 1f, Mathf.Clamp(L.Energy / 2.4f, 0f, 1f)));
            var c = SecondBiome.Atmosphere.FogColor;
            if (!string.IsNullOrEmpty(c)) m.SetShaderParameter("fog_color", new Color(c).Lerp(L.Fog, 0.45f));
        }
    }
}
