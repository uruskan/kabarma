using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: gol ve su yuzeyi (URETIM 6 M1: Ocak'a 60-90 m'de bir gol; SANAT-SES §6: stilize, duz renkli, kiyida kopuk
/// cizgisi, derinlige gore renk). Gol sigdir, oyuncu icinde yurur. Su yuzeyinin carpismasi yoktur: nisan isini gol
/// tabanina duser; Sulama deposu IsWater ile sorar (Capa.Sulama.cs).
/// </summary>
public partial class World
{
    LakeDef Lake => Data.Biomes.World.Lake;
    public bool HasLake => Lake.Center.Count == 2 && Lake.Radius > 0;
    public Vector3 LakeCenter => HasLake ? new Vector3(Lake.Center[0], WaterLevel, Lake.Center[1]) : Vector3.Zero;
    /// <summary>Su yuzeyinin yuksekligi (m). Golsuz dunyada cok asagida.</summary>
    public float WaterLevel { get; private set; } = -1000f;
    ShaderMaterial _waterMat;

    /// <summary>Kiyinin yaricapi bu acida (gurultuyle bukulmus daire).</summary>
    public float ShoreRadius(float angle) =>
        Lake.Radius + Lake.ShoreNoise * _shoreNoise.GetNoise2D(Mathf.Cos(angle) * 12f, Mathf.Sin(angle) * 12f);

    float ShoreRadiusAt(float wx, float wz) => ShoreRadius(Mathf.Atan2(wz - Lake.Center[1], wx - Lake.Center[0]));

    /// <summary>Gol: su seviyesi cevrenin ortalamasindan; kiyi bandi duzlenir, icerisi sig bir canak olur.</summary>
    void CarveLake()
    {
        if (!HasLake) return;
        var c = new Vector2(Lake.Center[0], Lake.Center[1]);
        float sum = 0; const int ring = 32;
        for (int i = 0; i < ring; i++)
        {
            float a = Mathf.Tau * i / ring;
            var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Lake.Radius + 10f);
            sum += BaseHeight(p.X, p.Y);
        }
        float w = sum / ring - 0.5f;
        WaterLevel = w;
        // Duzleme bandi kiyidan 18 m: ussun 42 m'lik duzlugune girmez (dunya sinamasi denetler).
        const float band = 18f;
        float reach = Lake.Radius + Lake.ShoreNoise + band + 2f;
        for (int x = 0; x <= Cells; x++)
        for (int z = 0; z <= Cells; z++)
        {
            float wx = x - Cells / 2f, wz = z - Cells / 2f;
            float d = new Vector2(wx - c.X, wz - c.Y).Length();
            if (d > reach) continue;
            float r = ShoreRadiusAt(wx, wz);
            float h0 = _h[x, z];
            float mask = 1f - Mathf.SmoothStep(r + 3f, r + band, d);
            float shore = w + Mathf.Max(0f, d - r) * 0.08f;
            float h = Mathf.Lerp(h0, shore, mask);
            if (d < r)
            {
                float t = d / r;
                h = w - Lake.Depth * (1f - t * t);
            }
            else h = Mathf.Lerp(h, Mathf.Max(h, w + 0.02f), mask);
            _h[x, z] = h;
        }
    }

    /// <summary>Kiyi ve gol tabani rengi icin 0..1 (islak kum, koyulasan taban).</summary>
    float LakeWetness(float wx, float wz, float h)
    {
        if (!HasLake) return 0f;
        float d = new Vector2(wx - Lake.Center[0], wz - Lake.Center[1]).Length();
        if (d > Lake.Radius + Lake.ShoreNoise + 6f) return 0f;
        return Mathf.Clamp((WaterLevel + 0.35f - h) / 0.35f, 0f, 1f) * 0.75f;
    }

    /// <summary>Nokta suyun icinde mi (yuzeyde ya da gol tabaninda): Sulama deposu buradan dolar.</summary>
    public bool IsWater(Vector3 p)
    {
        if (!HasLake) return false;
        float d = new Vector2(p.X - Lake.Center[0], p.Z - Lake.Center[1]).Length();
        if (d > ShoreRadiusAt(p.X, p.Z) + 0.3f) return false;
        return HeightAt(p.X, p.Z) < WaterLevel - 0.01f && p.Y < WaterLevel + 0.6f;
    }

    /// <summary>Kiyidan uzaklik (m): suyun icinde negatif. Bitki ortusu ve kil yataklari icin.</summary>
    public float ShoreDistance(float wx, float wz)
    {
        if (!HasLake) return 1e6f;
        float d = new Vector2(wx - Lake.Center[0], wz - Lake.Center[1]).Length();
        return d - ShoreRadiusAt(wx, wz);
    }

    void BuildWater()
    {
        if (!HasLake) return;
        // Duz disk: kiyinin en disindan 2 m tasar; disarida kalan kisim kiyinin altinda kalir (arazi suyun ustunde).
        float r = Lake.Radius + Lake.ShoreNoise + 2f;
        const int seg = 96, rings = 6;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        Vector3 P(int ring, int s) { float a = Mathf.Tau * s / seg; float rr = r * ring / rings; return new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr); }
        for (int ring = 0; ring < rings; ring++)
        for (int s = 0; s < seg; s++)
        {
            var a = P(ring, s); var b = P(ring, s + 1); var c = P(ring + 1, s); var d = P(ring + 1, s + 1);
            // Sarim arazininkiyle ayni yonde (yukaridan bakinca on yuz).
            if (ring > 0) { st.AddVertex(a); st.AddVertex(c); st.AddVertex(b); }
            st.AddVertex(b); st.AddVertex(c); st.AddVertex(d);
        }
        _waterMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };
        _waterMat.SetShaderParameter("shallow", Hex(Lake.Shallow, Palette.SkyNoon));
        _waterMat.SetShaderParameter("deep", Hex(Lake.Deep, Palette.CoopBlue));
        _waterMat.SetShaderParameter("foam", Hex(Lake.Foam, Palette.OffWhite));
        _waterMat.SetShaderParameter("depth_scale", Mathf.Max(0.1f, Lake.Depth * 0.6f));
        _waterMat.SetShaderParameter("foam_width", Lake.FoamWidth);
        _waterMat.SetShaderParameter("clarity", Lake.Clarity);
        var mi = new MeshInstance3D
        {
            Name = "Lake",
            Mesh = st.Commit(),
            MaterialOverride = _waterMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(Lake.Center[0], WaterLevel, Lake.Center[1]),
        };
        AddChild(mi);
    }
}
