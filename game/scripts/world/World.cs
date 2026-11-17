using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Altin Cayir'in 160 x 160 m'lik kosesi: arazi, gokyuzu, sis, isik, kayalar, bitki, ufuk katmanlari.
/// Gun saati 0 (ogle) .. 1 (gunbatimi) .. 2 (Kabarma gecesi) .. 3 (safak) araliginda harmanlanir.
/// Parcalar: World.Sky.cs (gokyuzu, gunes, sis, gun saati, ufuk), World.Decor.cs (agac, cali, ot, kaya). Burada: arazi.
/// </summary>
public partial class World : Node3D
{
    public const float Size = 160f;
    public const int Cells = 160;
    public static readonly Vector3 BaseCenter = new(0, 0, 0);

    FastNoiseLite _noise;
    float[,] _h;
    ShaderMaterial _groundMat;

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
}
