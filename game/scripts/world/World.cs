using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Altin Cayir'in 160 x 160 m'lik kosesi: arazi, gokyuzu, sis, isik, kayalar, bitki, ufuk katmanlari.
/// Gun saati 0 (ogle) .. 1 (gunbatimi) .. 2 (Kabarma gecesi) .. 3 (safak) araliginda harmanlanir.
/// </summary>
public partial class World : Node3D
{
    public const float Size = 160f;
    public const int Cells = 160;
    public static readonly Vector3 BaseCenter = new(0, 0, 0);

    FastNoiseLite _noise;
    float[,] _h;
    public WorldEnvironment Env { get; private set; }
    public DirectionalLight3D Sun { get; private set; }
    ProceduralSkyMaterial _sky;
    MeshInstance3D _pillar;
    ShaderMaterial _groundMat;
    readonly List<Rock> _rocks = new();
    public IReadOnlyList<Rock> Rocks => _rocks;
    readonly RandomNumberGenerator _rng = new() { Seed = 4417 };

    float _dayMix;     // 0 ogle, 1 gunbatimi
    float _nightMix;   // 0..1 Kabarma gecesi
    float _dawnMix;    // 0..1 safak

    public override void _Ready()
    {
        _noise = new FastNoiseLite { Seed = 4417, Frequency = 0.012f, FractalOctaves = 3, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        BuildHeights();
        BuildTerrain();
        BuildEnvironment();
        BuildHorizon();
        BuildDecor();
        BuildRocks();
        ApplyTime();
    }

    // ---------- Arazi ----------

    void BuildHeights()
    {
        _h = new float[Cells + 1, Cells + 1];
        for (int x = 0; x <= Cells; x++)
        for (int z = 0; z <= Cells; z++)
        {
            float wx = x - Cells / 2f, wz = z - Cells / 2f;
            float n = _noise.GetNoise2D(wx, wz) * 9f + _noise.GetNoise2D(wx * 3f, wz * 3f) * 0.8f;
            float d = new Vector2(wx, wz).Length();
            // Ussun cevresi duz (tarla icin egim 15 dereceden az olmali, GDD 8.1); kenarlar tepelere yukselir.
            float flat = Mathf.SmoothStep(18f, 42f, d);
            float rim = Mathf.SmoothStep(55f, 80f, d) * 10f;
            _h[x, z] = n * flat + rim;
        }
    }

    public float HeightAt(float wx, float wz)
    {
        float fx = Mathf.Clamp(wx + Cells / 2f, 0, Cells - 0.001f);
        float fz = Mathf.Clamp(wz + Cells / 2f, 0, Cells - 0.001f);
        int ix = (int)fx, iz = (int)fz;
        float tx = fx - ix, tz = fz - iz;
        float a = Mathf.Lerp(_h[ix, iz], _h[ix + 1, iz], tx);
        float b = Mathf.Lerp(_h[ix, iz + 1], _h[ix + 1, iz + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    public Vector3 Ground(Vector3 p) => new(p.X, HeightAt(p.X, p.Z), p.Z);

    void BuildTerrain()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var colNoise = new FastNoiseLite { Seed = 99, Frequency = 0.05f };
        Color ColorAt(int x, int z)
        {
            float wx = x - Cells / 2f, wz = z - Cells / 2f;
            float h = _h[x, z];
            float slope = Mathf.Abs(_h[Mathf.Min(x + 1, Cells), z] - h) + Mathf.Abs(_h[x, Mathf.Min(z + 1, Cells)] - h);
            float patch = colNoise.GetNoise2D(wx, wz);
            var c = Palette.Ground;
            c = c.Lerp(Palette.Plant, Mathf.Clamp(patch * 1.6f + 0.15f, 0, 0.75f));
            c = c.Lerp(Palette.Ground.Lerp(Palette.Shadow, 0.35f), Mathf.Clamp(slope * 0.5f, 0, 0.6f));
            c = c.Lerp(Palette.Fog, Mathf.Clamp((h - 6f) / 14f, 0, 0.35f));
            return c;
        }
        for (int x = 0; x < Cells; x++)
        for (int z = 0; z < Cells; z++)
        {
            Vector3 P(int ix, int iz) => new(ix - Cells / 2f, _h[ix, iz], iz - Cells / 2f);
            var a = P(x, z); var b = P(x + 1, z); var c = P(x, z + 1); var d = P(x + 1, z + 1);
            st.SetColor(ColorAt(x, z)); st.AddVertex(a);
            st.SetColor(ColorAt(x + 1, z)); st.AddVertex(b);
            st.SetColor(ColorAt(x, z + 1)); st.AddVertex(c);
            st.SetColor(ColorAt(x + 1, z)); st.AddVertex(b);
            st.SetColor(ColorAt(x + 1, z + 1)); st.AddVertex(d);
            st.SetColor(ColorAt(x, z + 1)); st.AddVertex(c);
        }
        st.Index();
        st.GenerateNormals();
        // Godot on yuzu saat yonu tersine sayar; bizim sarimimiz yukari bakiyor mu kontrol: normal.y > 0 olmali.
        var mesh = st.Commit();
        _groundMat = MeshKit.NewLiveMaterial();
        _groundMat.SetShaderParameter("wobble_amp", 0f);
        _groundMat.SetShaderParameter("rim", 0f);
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = _groundMat, Name = "Terrain" };
        AddChild(mi);

        var body = new StaticBody3D { Name = "TerrainBody" };
        var shape = new HeightMapShape3D { MapWidth = Cells + 1, MapDepth = Cells + 1 };
        var data = new float[(Cells + 1) * (Cells + 1)];
        for (int z = 0; z <= Cells; z++)
        for (int x = 0; x <= Cells; x++)
            data[z * (Cells + 1) + x] = _h[x, z];
        shape.MapData = data;
        body.AddChild(new CollisionShape3D { Shape = shape });
        AddChild(body);
    }

    // ---------- Gokyuzu, sis, isik ----------

    void BuildEnvironment()
    {
        _sky = new ProceduralSkyMaterial
        {
            SkyTopColor = Palette.SkyNoon,
            SkyHorizonColor = Palette.Fog,
            GroundHorizonColor = Palette.Fog,
            GroundBottomColor = Palette.Ground.Lerp(Palette.Shadow, 0.4f),
            SunAngleMax = 20f,
            SkyCurve = 0.12f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = _sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.75f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            FogEnabled = true,
            FogLightColor = Palette.Fog,
            FogDensity = 0.0028f,
            FogAerialPerspective = 0.35f,
            FogSkyAffect = 0.25f,
            GlowEnabled = true,
            GlowIntensity = 0.7f,
            GlowBloom = 0.05f,
            GlowHdrThreshold = 1.1f,
            SsaoEnabled = true,
            SsaoRadius = 1.4f,
            SsaoIntensity = 1.2f,
        };
        Env = new WorldEnvironment { Environment = env };
        AddChild(Env);
        Sun = new DirectionalLight3D
        {
            ShadowEnabled = true,
            LightColor = new Color("FFF1D6"),
            LightEnergy = 1.25f,
            DirectionalShadowMaxDistance = 70f,
        };
        AddChild(Sun);
    }

    /// <summary>Ufuk: kat kat sisli tepe siluetleri ve kraterden gokyuzune yukselen Maya sutunu (PLAN 1.4).</summary>
    void BuildHorizon()
    {
        var kit = new MeshKit();
        var r = new RandomNumberGenerator { Seed = 7 };
        for (int layer = 0; layer < 3; layer++)
        {
            float dist = 130f + layer * 70f;
            int count = 16 + layer * 4;
            var col = Palette.Plant.Lerp(Palette.Fog, 0.35f + layer * 0.22f).Lerp(Palette.Shadow, 0.12f);
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Tau * (i + r.Randf() * 0.6f) / count;
                var p = new Vector3(Mathf.Cos(a) * dist, -6f, Mathf.Sin(a) * dist);
                float w = r.RandfRange(40f, 70f) + layer * 20f, h = r.RandfRange(18f, 34f) + layer * 10f;
                kit.Sphere(1f, MeshKit.At(p, new Vector3(0, a, 0), new Vector3(w, h, w * 0.7f)), col, 10, 5);
            }
        }
        var hills = kit.Instance();
        hills.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(hills);

        // Krater isik sutunu: Maya'ya ait tek isiyan sey (SANAT-SES §1.5).
        var pillarMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(Palette.MayaTeal, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = Palette.MayaTeal,
            EmissionEnergyMultiplier = 2.5f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _pillar = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 6f, BottomRadius = 14f, Height = 600f, RadialSegments = 16 },
            MaterialOverride = pillarMat,
            Position = new Vector3(-180f, 280f, -330f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_pillar);
    }

    void BuildDecor()
    {
        // Agaclar: tiknaz govde + yuvarlak tac kumeleri. Ussun 22 m icine konmaz.
        var trees = new MeshKit();
        var bushes = new MeshKit();
        // M0 denemesi #1: Tripo'dan dokulu cayir agaci (6.788 ucgen, 1K doku). Varsa butun agaclar ondan, tek MultiMesh.
        var treeNode = Assets.Load("res://assets/models/agac/agac.glb");
        var treeMesh = Assets.FirstMesh(treeNode);
        treeNode?.Free(); // yalnizca mesh'i lazim; sahneye girmeyen dugum serbest birakilir
        var treeXf = new System.Collections.Generic.List<Transform3D>();
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector3(_rng.RandfRange(-75, 75), 0, _rng.RandfRange(-75, 75));
            if (p.Length() < 24f || new Vector2(p.X - 9f, p.Z + 9f).Length() < 10f) continue;
            p = Ground(p);
            if (_rng.Randf() < 0.45f)
            {
                float s = _rng.RandfRange(0.8f, 1.5f);
                if (treeMesh != null)
                {
                    // Model 4,2 m; cesitlilik icin boy ve yon rastgele.
                    treeXf.Add(new Transform3D(Basis.FromEuler(new Vector3(0, _rng.Randf() * Mathf.Tau, 0)).Scaled(Vector3.One * s * 0.95f), p + Vector3.Down * 0.1f));
                    continue;
                }
                trees.Cylinder(0.28f * s, 0.42f * s, 2.4f * s, MeshKit.At(p + new Vector3(0, 1.1f * s, 0)), Palette.Wood.Lerp(Palette.Shadow, 0.25f), 8);
                var leaf = Palette.Plant.Lerp(Palette.Accent, _rng.Randf() < 0.25f ? 0.45f : 0f);
                trees.Sphere(1.6f * s, MeshKit.At(p + new Vector3(0, 3.2f * s, 0), Vector3.Zero, new Vector3(1, 0.85f, 1)), leaf, 10, 6);
                trees.Sphere(1.1f * s, MeshKit.At(p + new Vector3(0.9f * s, 2.7f * s, 0.3f * s)), leaf.Lerp(Palette.Shadow, 0.12f), 9, 5);
                trees.Sphere(1.0f * s, MeshKit.At(p + new Vector3(-0.8f * s, 2.8f * s, -0.4f * s)), leaf.Lerp(Palette.Fog, 0.1f), 9, 5);
            }
            else
            {
                float s = _rng.RandfRange(0.5f, 1.0f);
                var c = Palette.Plant.Lerp(Palette.Ground, _rng.Randf() * 0.4f);
                bushes.Sphere(0.9f * s, MeshKit.At(p + new Vector3(0, 0.4f * s, 0), Vector3.Zero, new Vector3(1.3f, 0.8f, 1.1f)), c, 9, 5);
                if (_rng.Randf() < 0.4f)
                    bushes.Sphere(0.22f * s, MeshKit.At(p + new Vector3(0.3f, 1.0f * s, 0.2f)), Palette.Accent, 6, 4);
            }
        }
        if (treeMesh != null && treeXf.Count > 0)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = treeMesh, InstanceCount = treeXf.Count };
            for (int i = 0; i < treeXf.Count; i++) mm.SetInstanceTransform(i, treeXf[i]);
            AddChild(new MultiMeshInstance3D { Multimesh = mm, Name = "Trees" });
            foreach (var xf in treeXf)
            {
                var body = new StaticBody3D();
                body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.45f * xf.Basis.Scale.X, Height = 3f * xf.Basis.Scale.Y }, Position = new Vector3(0, 1.5f * xf.Basis.Scale.Y, 0) });
                AddChild(body);
                body.GlobalPosition = xf.Origin;
            }
        }
        else AddChild(trees.Instance());
        var b = bushes.Instance();
        b.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(b);

        // Ot tutamlari: ussun cevresinde yogun.
        var grass = new MeshKit();
        for (int i = 0; i < 900; i++)
        {
            float a = _rng.Randf() * Mathf.Tau, d = Mathf.Sqrt(_rng.Randf()) * 60f;
            var p = new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            if (new Vector2(p.X - 9f, p.Z + 9f).Length() < 6f || p.Length() < 4f) continue;
            p = Ground(p);
            var c = Palette.Plant.Lerp(Palette.Ground, _rng.Randf() * 0.5f);
            for (int k = 0; k < 3; k++)
            {
                var off = new Vector3(_rng.RandfRange(-0.15f, 0.15f), 0.22f, _rng.RandfRange(-0.15f, 0.15f));
                grass.Cylinder(0.0f, 0.07f, 0.45f, MeshKit.At(p + off, new Vector3(_rng.RandfRange(-0.3f, 0.3f), 0, _rng.RandfRange(-0.3f, 0.3f))), c, 4);
            }
        }
        var g = grass.Instance();
        g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(g);
    }

    void BuildRocks()
    {
        // Kazilacak kayalar ussun cevresinde, ilk dakikada bulunacak kadar yakin.
        var spots = new[] { new Vector3(-7, 0, 6), new Vector3(-10, 0, 1), new Vector3(5, 0, 11), new Vector3(12, 0, 6), new Vector3(-4, 0, 14), new Vector3(16, 0, -4), new Vector3(-15, 0, -8), new Vector3(2, 0, -16) };
        int i = 0;
        foreach (var s in spots)
        {
            var rock = new Rock(_rng.RandiRange(4, 6), _rng.RandfRange(0.9f, 1.3f), (ulong)(1000 + i++));
            AddChild(rock);
            rock.GlobalPosition = Ground(s) + Vector3.Down * 0.2f;
            _rocks.Add(rock);
        }
    }

    public void RemoveRock(Rock r) => _rocks.Remove(r);

    // ---------- Gun saati ----------

    public void SetDay(float sunsetMix) { _dayMix = Mathf.Clamp(sunsetMix, 0, 1); ApplyTime(); }
    public void SetNight(float mix) { _nightMix = Mathf.Clamp(mix, 0, 1); ApplyTime(); }
    public void SetDawn(float mix) { _dawnMix = Mathf.Clamp(mix, 0, 1); ApplyTime(); }

    void ApplyTime()
    {
        if (_sky == null) return;
        var top = Palette.SkyNoon.Lerp(Palette.SkySunset.Lerp(Palette.SkyNoon, 0.35f), _dayMix);
        var hor = Palette.Fog.Lerp(Palette.SkySunset, _dayMix * 0.8f);
        var sunCol = new Color("FFF1D6").Lerp(Palette.SkySunset, _dayMix * 0.7f);
        float sunEnergy = Mathf.Lerp(1.25f, 0.9f, _dayMix);
        float sunPitch = Mathf.Lerp(-52f, -18f, _dayMix);

        // Kabarma: gece mavidir, en koyu #1A2238; gokyuzu Maya renklerine doner.
        var nTop = Palette.NightDeep.Lerp(Palette.MayaViolet, 0.18f);
        var nHor = Palette.MayaViolet.Lerp(Palette.MayaTeal, 0.35f).Lerp(Palette.NightDeep, 0.35f);
        top = top.Lerp(nTop, _nightMix);
        hor = hor.Lerp(nHor, _nightMix);
        sunCol = sunCol.Lerp(new Color("9FB4FF"), _nightMix);
        sunEnergy = Mathf.Lerp(sunEnergy, 0.55f, _nightMix);
        sunPitch = Mathf.Lerp(sunPitch, -40f, _nightMix);

        // Safak: pembe-altin sabah.
        var dTop = Palette.SkyNoon.Lerp(Palette.SkySunset, 0.25f);
        var dHor = Palette.Fog.Lerp(Palette.SkySunset, 0.45f);
        top = top.Lerp(dTop, _dawnMix);
        hor = hor.Lerp(dHor, _dawnMix);
        sunCol = sunCol.Lerp(new Color("FFD9B0"), _dawnMix);
        sunEnergy = Mathf.Lerp(sunEnergy, 1.05f, _dawnMix);
        sunPitch = Mathf.Lerp(sunPitch, -24f, _dawnMix);

        _sky.SkyTopColor = top;
        _sky.SkyHorizonColor = hor;
        _sky.GroundHorizonColor = hor;
        Env.Environment.FogLightColor = hor;
        Env.Environment.AmbientLightEnergy = Mathf.Lerp(1f, 1.35f, _nightMix * (1 - _dawnMix));
        Sun.LightColor = sunCol;
        Sun.LightEnergy = sunEnergy;
        Sun.RotationDegrees = new Vector3(sunPitch, -35f + _dayMix * 50f, 0);
        float veins = _nightMix * (1 - _dawnMix);
        _groundMat?.SetShaderParameter("glow", veins * 0.55f);
        _groundMat?.SetShaderParameter("vein_scale", 0.35f);
        if (_pillar != null) _pillar.Scale = new Vector3(1f + veins * 1.5f, 1f, 1f + veins * 1.5f);
    }
}
