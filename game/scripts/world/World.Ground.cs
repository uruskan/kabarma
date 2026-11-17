using System;
using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: zemin katmanlari (gorsel-dunya). Arazi malzemesi bes dosenebilir PBR katmani karistirir (terrain.gdshader):
/// cimen (taban), toprak (patika, aciklik), kum (gol kiyisi), tarla (surulmus), kaya (egim). Dokular
/// pipeline/gen_terrain_textures.py ile koddan uretilir (assets/textures/terrain/).
/// Denetim dokusu (dunya XZ, ControlRes^2, RGBA8): R toprak/patika, G kiyi kumu, B surulmus tarla, A ot yok (aciklik, yapi).
/// Yukseklik dokusu (Rf, 301^2) ot alaninin (GrassField) koklerini araziye oturtur.
/// </summary>
public partial class World
{
    public const int ControlRes = 512;
    public ImageTexture ControlTex { get; private set; }
    public ImageTexture HeightTex { get; private set; }
    public NoiseTexture2D MacroNoise { get; private set; }
    byte[] _ctl;
    Image _ctlImg;

    static readonly string[] LayerNames = { "cimen", "toprak", "kaya", "kum", "tarla" };
    static Texture2DArray _albedoArr, _nrmArr;

    /// <summary>Katman dokularini bir kez yukler (sinamalarda birden cok World kurulur).</summary>
    static void LoadLayers()
    {
        if (_albedoArr != null) return;
        _albedoArr = LoadArray("albedo");
        _nrmArr = LoadArray("nrm");
    }

    static Texture2DArray LoadArray(string kind)
    {
        var imgs = new Godot.Collections.Array<Image>();
        foreach (var n in LayerNames)
        {
            var tex = GD.Load<Texture2D>($"res://assets/textures/terrain/{n}_{kind}.png");
            var img = tex.GetImage();
            if (img.IsCompressed()) img.Decompress();
            if (img.GetFormat() != Image.Format.Rgba8) img.Convert(Image.Format.Rgba8);
            if (!img.HasMipmaps()) img.GenerateMipmaps();
            imgs.Add(img);
        }
        var arr = new Texture2DArray();
        arr.CreateFromImages(imgs);
        return arr;
    }

    /// <summary>Dunya noktasindan denetim dokusu hucresi.</summary>
    (int x, int z) CtlCell(float wx, float wz) =>
        (Mathf.Clamp((int)((wx + Size / 2f) / Size * ControlRes), 0, ControlRes - 1),
         Mathf.Clamp((int)((wz + Size / 2f) / Size * ControlRes), 0, ControlRes - 1));

    Vector2 CtlWorld(int x, int z) => new((x + 0.5f) / ControlRes * Size - Size / 2f, (z + 0.5f) / ControlRes * Size - Size / 2f);

    void BuildGroundLayers()
    {
        LoadLayers();
        MacroNoise = new NoiseTexture2D
        {
            Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true,
            Noise = new FastNoiseLite { Seed = T.Seed + 77, Frequency = 0.008f, FractalOctaves = 5, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex },
        };

        // Yukseklik: 301 x 301 Rf, ot koklerini araziye oturtur.
        int n = Cells + 1;
        var hf = new float[n * n];
        for (int z = 0; z < n; z++)
        for (int x = 0; x < n; x++)
            hf[z * n + x] = _h[x, z];
        var hb = new byte[hf.Length * 4];
        Buffer.BlockCopy(hf, 0, hb, 0, hb.Length);
        HeightTex = ImageTexture.CreateFromImage(Image.CreateFromData(n, n, false, Image.Format.Rf, hb));

        // Denetim dokusu.
        _ctl = new byte[ControlRes * ControlRes * 4];
        var edge = new FastNoiseLite { Seed = T.Seed + 43, Frequency = 0.18f, FractalOctaves = 2 };
        var wear = new FastNoiseLite { Seed = T.Seed + 44, Frequency = 0.05f, FractalOctaves = 3 };
        var paths = Biomes.World.Paths;
        float pw = Biomes.World.PathWidth;
        for (int z = 0; z < ControlRes; z++)
        for (int x = 0; x < ControlRes; x++)
        {
            var w = CtlWorld(x, z);
            float r = 0, g = 0, a = 0;
            // Patikalar: kenari gurultuyle titreyen toprak seridi, ortasi daha cignenmis.
            float dp = PathDistance(paths, w);
            if (dp < pw * 2f)
            {
                float half = pw * 0.5f * (0.75f + 0.5f * (edge.GetNoise2D(w.X, w.Y) * 0.5f + 0.5f));
                r = Mathf.Max(r, (1f - Mathf.SmoothStep(half * 0.6f, half + 0.6f, dp)) * (0.75f + 0.25f * wear.GetNoise2D(w.X, w.Y)));
            }
            // Aciklik: ussun ve kapsulun cevresi; ortasi cignenmis toprak, kenara dogru seyrek ot.
            foreach (var c in Biomes.World.Clearings)
            {
                if (c.Count < 3) continue;
                float d = new Vector2(w.X - c[0], w.Y - c[1]).Length();
                if (d > c[2] * 1.5f) continue;
                float n0 = edge.GetNoise2D(w.X * 0.6f, w.Y * 0.6f) * 0.5f + 0.5f;
                a = Mathf.Max(a, 1f - Mathf.SmoothStep(c[2] * 0.75f, c[2] * (1.05f + 0.35f * n0), d));
                r = Mathf.Max(r, (1f - Mathf.SmoothStep(c[2] * 0.2f, c[2] * (0.8f + 0.4f * n0), d)) * (0.45f + 0.35f * (wear.GetNoise2D(w.X, w.Y) * 0.5f + 0.5f)));
            }
            if (Biome.Grass.BaseClear > 0)
                a = Mathf.Max(a, 1f - Mathf.SmoothStep(Biome.Grass.BaseClear * 0.8f, Biome.Grass.BaseClear * 1.15f, w.Length()));
            // Gol kiyisi: suyun icinde ve kiyidan ~2-3 m'ye kadar kum.
            if (HasLake)
            {
                float sd = ShoreDistance(w.X, w.Y);
                float reach = 2.2f + 1.4f * (edge.GetNoise2D(w.X * 0.7f, w.Y * 0.7f) * 0.5f + 0.5f);
                g = 1f - Mathf.SmoothStep(0.2f, reach, sd);
            }
            int i = (z * ControlRes + x) * 4;
            _ctl[i] = (byte)(Mathf.Clamp(r, 0, 1) * 255);
            _ctl[i + 1] = (byte)(Mathf.Clamp(g, 0, 1) * 255);
            _ctl[i + 2] = 0;
            _ctl[i + 3] = (byte)(Mathf.Clamp(a, 0, 1) * 255);
        }
        _ctlImg = Image.CreateFromData(ControlRes, ControlRes, false, Image.Format.Rgba8, _ctl);
        ControlTex = ImageTexture.CreateFromImage(_ctlImg);
    }

    static float PathDistance(List<List<List<float>>> paths, Vector2 p)
    {
        float best = 1e9f;
        foreach (var line in paths)
            for (int k = 0; k + 1 < line.Count; k++)
            {
                var a = new Vector2(line[k][0], line[k][1]);
                var b = new Vector2(line[k + 1][0], line[k + 1][1]);
                var ab = b - a;
                float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-4f), 0f, 1f);
                best = Mathf.Min(best, (a + ab * t - p).Length());
            }
        return best;
    }

    /// <summary>Surulmus tarla: zeminde tarla katmani, ot kalkar (FarmView her surulen kare icin cagirir).</summary>
    public void MarkSoil(Vector3 center, float half) => PaintCtl(center, half, 2, square: true);

    /// <summary>Yapi ya da yerlestirilen esya: altindaki ot kalkar.</summary>
    public void ClearGrass(Vector3 center, float radius) => PaintCtl(center, radius, 3, square: false);

    void PaintCtl(Vector3 c, float half, int channel, bool square)
    {
        if (_ctl == null) return;
        float texel = Size / ControlRes;
        var (x0, z0) = CtlCell(c.X - half - texel, c.Z - half - texel);
        var (x1, z1) = CtlCell(c.X + half + texel, c.Z + half + texel);
        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var w = CtlWorld(x, z);
            float d = square ? Mathf.Max(Mathf.Abs(w.X - c.X), Mathf.Abs(w.Y - c.Z)) : new Vector2(w.X - c.X, w.Y - c.Z).Length();
            float v = 1f - Mathf.SmoothStep(half - texel * 0.5f, half + texel * 0.5f, d);
            int i = (z * ControlRes + x) * 4 + channel;
            _ctl[i] = (byte)Mathf.Max(_ctl[i], (int)(v * 255));
        }
        _ctlImg.SetData(ControlRes, ControlRes, false, Image.Format.Rgba8, _ctl);
        ControlTex.Update(_ctlImg);
    }

    /// <summary>Palet rengini dokunun ortalama rengine boler: carpan olarak zeminin tonunu biyoma uydurur (parlaklik korunur).</summary>
    static Vector3 TintFor(Color palette, Color textureMean, float amount)
    {
        var p = palette.SrgbToLinear(); var m = textureMean.SrgbToLinear();
        var v = new Vector3(p.R / Mathf.Max(m.R, 1e-3f), p.G / Mathf.Max(m.G, 1e-3f), p.B / Mathf.Max(m.B, 1e-3f));
        float lum = v.X * 0.2126f + v.Y * 0.7152f + v.Z * 0.0722f;
        v /= Mathf.Max(lum, 1e-3f);
        return Vector3.One.Lerp(v, amount);
    }

    /// <summary>Cimen dokusunun ortalama sRGB rengi (gen_terrain_textures.py ciktisi "TEX cimen mean_srgb").</summary>
    static readonly Color CimenMean = new(0.406f, 0.391f, 0.190f);

    ShaderMaterial TerrainMaterial()
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };
        m.SetShaderParameter("layers_albedo", _albedoArr);
        m.SetShaderParameter("layers_nrm", _nrmArr);
        m.SetShaderParameter("control", ControlTex);
        m.SetShaderParameter("macro_noise", MacroNoise);
        m.SetShaderParameter("world_size", Size);
        m.SetShaderParameter("water_level", WaterLevel);
        m.SetShaderParameter("detail", Preset.TerrainDetail);
        m.SetShaderParameter("tint_lush", TintFor(Pal("plant", Palette.Plant), CimenMean, 0.45f));
        m.SetShaderParameter("tint_dry", TintFor(Pal("ground", Palette.Ground), CimenMean, 0.55f));
        m.SetShaderParameter("maya_a", Palette.MayaTeal);
        m.SetShaderParameter("maya_b", Palette.MayaViolet);
        return m;
    }
}
