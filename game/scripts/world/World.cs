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
        Map = new BiomeMap(Data.Biomes);
        Pois = PoiPlacer.Place(Data.Biomes, Map);
        BuildBiomeNoise(); // World.Biomes.cs: biyom terimleri (Sis Korusu)
        BuildHeights();
        BuildGroundLayers();
        BuildTerrain();
        BuildEnvironment();
        BuildHorizon();
        BuildWater();
        BuildDecor();
        BuildResources();
        BuildGrass();
        BuildAtmosphere(); // World.Fog.cs: Koru sis katmanlari ve isik huzmeleri
        BuildPois();       // World.Pois.cs: kesif noktalari
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
        return n * flat + hills + edge + peak + BiomeHeight(wx, wz);
    }

    void BuildHeights()
    {
        _h = new float[Cells + 1, Cells + 1];
        for (int x = 0; x <= Cells; x++)
        for (int z = 0; z <= Cells; z++)
            _h[x, z] = BaseHeight(x - Cells / 2f, z - Cells / 2f);
        CarveLake();
        FlattenPois(); // World.Pois.cs: kesif noktasi duzlestirmesi (TEKNIK 7.2)
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

    /// <summary>Gorsel arazi parcasi (m, TEKNIK §7 "64 m'lik parcalar"): gorus konisi parca parca eler.</summary>
    public const int TerrainChunk = 64;

    /// <summary>
    /// Indeksli ArrayMesh'ler: (Cells + 1)^2 kose, 64 m'lik parcalara bolunmus ("Terrain" altinda). Renk ve doku
    /// terrain.gdshader'da (World.Ground.cs katmanlari). Carpisma tek yukseklik seklidir.
    /// </summary>
    void BuildTerrain()
    {
        int n = Cells + 1;
        _groundMat = TerrainMaterial();
        var root = new Node3D { Name = "Terrain" };
        AddChild(root);
        for (int cz = 0; cz < Cells; cz += TerrainChunk)
        for (int cx = 0; cx < Cells; cx += TerrainChunk)
        {
            int w = Mathf.Min(TerrainChunk, Cells - cx), d = Mathf.Min(TerrainChunk, Cells - cz);
            int m = w + 1;
            var verts = new Vector3[m * (d + 1)];
            var norms = new Vector3[verts.Length];
            for (int z = 0; z <= d; z++)
            for (int x = 0; x <= w; x++)
            {
                int gx = cx + x, gz = cz + z, i = z * m + x;
                verts[i] = new Vector3(gx - Cells / 2f, _h[gx, gz], gz - Cells / 2f);
                float hl = _h[Mathf.Max(gx - 1, 0), gz], hr = _h[Mathf.Min(gx + 1, Cells), gz];
                float hd = _h[gx, Mathf.Max(gz - 1, 0)], hu = _h[gx, Mathf.Min(gz + 1, Cells)];
                norms[i] = new Vector3(-(hr - hl) * 0.5f, 1f, -(hu - hd) * 0.5f).Normalized();
            }
            var idx = new int[w * d * 6];
            int k = 0;
            for (int z = 0; z < d; z++)
            for (int x = 0; x < w; x++)
            {
                int a = z * m + x, b = a + 1, c = a + m, e = c + 1;
                idx[k++] = a; idx[k++] = b; idx[k++] = c;
                idx[k++] = b; idx[k++] = e; idx[k++] = c;
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            arrays[(int)Mesh.ArrayType.Normal] = norms;
            arrays[(int)Mesh.ArrayType.Index] = idx;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            root.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = _groundMat, Name = $"T_{cx / TerrainChunk}_{cz / TerrainChunk}" });
        }

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
