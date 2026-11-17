using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Altin Cayir'in 300 x 300 m'lik kosesi (URETIM 6 M1): arazi, gol, gokyuzu, sis, isik, bitki ortusu, kaynaklar, ufuk.
/// Sayilar data/biomes.json'da. Ussun 45 m'si icinde yukseklik MVP ile aynidir; yeni terimler (tepeler, kenar sirti,
/// en yuksek tepe, gol) daha uzakta acilir. Eksen: Ocak (0,0,0), kuzey +Z (krater), dogu -X.
/// Parcalar: World.Sky.cs (gokyuzu, gun saati, ufuk), World.Water.cs (gol, su yuzeyi), World.Decor.cs (bitki ve sus kayasi),
/// World.Resources.cs (tutamlar, kil, bakir, tas), World.Grass.cs (ot), World.Bookmarks.cs (yer imleri). Burada: arazi.
/// </summary>
public partial class World : Node3D
{
    public static readonly Vector3 BaseCenter = new(0, 0, 0);

    /// <summary>Veri: Director varsa onunki; yalniz World kuran sinamalarda (BuildTest, SwarmTest) kendisi yukler.</summary>
    public GameData Data { get; private set; }
    public BiomesFile Biomes => Data.Biomes;
    public BiomeDef Biome => Data.Biomes.Current;
    TerrainDef T => Data.Biomes.World.Terrain;

    /// <summary>Kenar uzunlugu (m) ve 1 m'lik hucre sayisi.</summary>
    public float Size { get; private set; }
    public int Cells { get; private set; }

    FastNoiseLite _noise, _hillNoise, _shoreNoise;
    float[,] _h;
    ShaderMaterial _groundMat;

    public override void _Ready()
    {
        Data = (GetParent() as Director)?.Data ?? GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        Cells = T.Size;
        Size = T.Size;
        _noise = new FastNoiseLite { Seed = T.Seed, Frequency = T.NoiseFrequency, FractalOctaves = 3, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        _hillNoise = new FastNoiseLite { Seed = T.Seed + 1, Frequency = T.HillFrequency, FractalOctaves = 2, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        _shoreNoise = new FastNoiseLite { Seed = T.Seed + 2, Frequency = 0.08f, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
        BuildHeights();
        BuildTerrain();
        BuildEnvironment();
        BuildHorizon();
        BuildWater();
        BuildDecor();
        BuildResources();
        BuildGrass();
        ApplyGraphics();
        ApplyTime();
    }

    // ---------- Arazi ----------

    /// <summary>Golsuz yukseklik. Ussun icindeki terim MVP'deki formulle birebir aynidir (n * flat).</summary>
    float BaseHeight(float wx, float wz)
    {
        float n = _noise.GetNoise2D(wx, wz) * T.NoiseAmp + _noise.GetNoise2D(wx * 3f, wz * 3f) * T.DetailAmp;
        float d = new Vector2(wx, wz).Length();
        float flat = Mathf.SmoothStep(T.BaseFlatInner, T.BaseFlatOuter, d);
        float far = Mathf.SmoothStep(T.HillStart, T.HillEnd, d);
        float hills = (_hillNoise.GetNoise2D(wx, wz) * 0.5f + 0.5f) * T.HillAmp * far;
        float edge = Mathf.SmoothStep(T.EdgeStart, Size / 2f, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz))) * T.EdgeHeight;
        float peak = 0f;
        if (T.Peak.Count == 2)
        {
            float dp = new Vector2(wx - T.Peak[0], wz - T.Peak[1]).Length() / T.PeakRadius;
            peak = T.PeakHeight * Mathf.Exp(-dp * dp) * Mathf.SmoothStep(T.HillStart, T.HillStart + 25f, d);
        }
        return n * flat + hills + edge + peak;
    }

    void BuildHeights()
    {
        _h = new float[Cells + 1, Cells + 1];
        for (int x = 0; x <= Cells; x++)
        for (int z = 0; z <= Cells; z++)
            _h[x, z] = BaseHeight(x - Cells / 2f, z - Cells / 2f);
        CarveLake();
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

    /// <summary>Egim (derece): 1 m'lik merkezi farklardan.</summary>
    public float SlopeAt(float wx, float wz)
    {
        float dx = (HeightAt(wx + 0.5f, wz) - HeightAt(wx - 0.5f, wz));
        float dz = (HeightAt(wx, wz + 0.5f) - HeightAt(wx, wz - 0.5f));
        return Mathf.RadToDeg(Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)));
    }

    /// <summary>Indeksli ArrayMesh (301 x 301 kose): kose rengi egime, yukseklige ve gol kiyisina gore (SANAT-SES §6).</summary>
    void BuildTerrain()
    {
        int n = Cells + 1;
        var verts = new Vector3[n * n];
        var norms = new Vector3[n * n];
        var cols = new Color[n * n];
        var colNoise = new FastNoiseLite { Seed = 99, Frequency = 0.05f };
        var ground = Pal("ground", Palette.Ground);
        var plant = Pal("plant", Palette.Plant);
        var shadow = Pal("shadow", Palette.Shadow);
        var fog = Pal("fog", Palette.Fog);
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
        {
            int i = z * n + x;
            float wx = x - Cells / 2f, wz = z - Cells / 2f;
            float h = _h[x, z];
            verts[i] = new Vector3(wx, h, wz);
            float hl = _h[Mathf.Max(x - 1, 0), z], hr = _h[Mathf.Min(x + 1, Cells), z];
            float hd = _h[x, Mathf.Max(z - 1, 0)], hu = _h[x, Mathf.Min(z + 1, Cells)];
            norms[i] = new Vector3(-(hr - hl) * 0.5f, 1f, -(hu - hd) * 0.5f).Normalized();
            float slope = Mathf.Abs(hr - h) + Mathf.Abs(hu - h);
            float patch = colNoise.GetNoise2D(wx, wz);
            var c = ground;
            c = c.Lerp(plant, Mathf.Clamp(patch * 1.6f + 0.15f, 0, 0.75f));
            c = c.Lerp(ground.Lerp(shadow, 0.35f), Mathf.Clamp(slope * 0.5f, 0, 0.6f));
            c = c.Lerp(fog, Mathf.Clamp((h - 6f) / 14f, 0, 0.35f));
            // Gol: kiyida islak kum, suyun altinda koyulasan taban (SANAT-SES §6 "derinlige gore renk" suyla birlikte).
            float wet = LakeWetness(wx, wz, h);
            if (wet > 0) c = c.Lerp(Palette.Soil.Lerp(shadow, 0.25f), wet);
            cols[i] = c;
        }
        var idx = new int[Cells * Cells * 6];
        int k = 0;
        for (int z = 0; z < Cells; z++)
        for (int x = 0; x < Cells; x++)
        {
            int a = z * n + x, b = a + 1, c = a + n, d = c + 1;
            idx[k++] = a; idx[k++] = b; idx[k++] = c;
            idx[k++] = b; idx[k++] = d; idx[k++] = c;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        arrays[(int)Mesh.ArrayType.Index] = idx;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _groundMat = MeshKit.NewLiveMaterial();
        _groundMat.SetShaderParameter("wobble_amp", 0f);
        _groundMat.SetShaderParameter("rim", 0f);
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = _groundMat, Name = "Terrain" });

        var body = new StaticBody3D { Name = "TerrainBody" };
        var shape = new HeightMapShape3D { MapWidth = n, MapDepth = n };
        var data = new float[n * n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            data[z * n + x] = _h[x, z];
        shape.MapData = data;
        body.AddChild(new CollisionShape3D { Shape = shape });
        AddChild(body);
    }

    /// <summary>Biyom paletinden renk (SANAT-SES §2); veride yoksa Palette.cs'deki karsiligi.</summary>
    public Color Pal(string key, Color fallback) =>
        Biome.Palette.TryGetValue(key, out var hex) && !string.IsNullOrEmpty(hex) ? new Color(hex) : fallback;

    static Color Hex(string hex, Color fallback) => string.IsNullOrEmpty(hex) ? fallback : new Color(hex);

    static Vector3 V3(List<float> v, float y = 0) => v.Count >= 3 ? new Vector3(v[0], v[1], v[2]) : new Vector3(v[0], y, v[1]);
}
