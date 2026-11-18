using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: biyomlar (M2, dunya2 akisi; TEKNIK §7 "M2 dunyasi"). Biyom haritasi Sim'de (BiomeMap: Ocak'tan krater
/// sutununa dogru, gurultuyle bukulmus sinir); burada:
///  - biyomun yukseklik terimi (sirtli gurultu + taban yukseltmesi, biyom agirligiyla carpilir; Cayir'da 0),
///  - biyom agirlik dokusu (terrain ve ot shader'lari zemin rengini, ot sikligini ve rengini karistirir),
///  - kamera hangi biyomdaysa gun saati isiginin o biyomun saatlerine karismasi (World.Sky.cs ApplyTime).
/// "Biome" (Current) hep baslangic biyomudur (Cayir): diger akislar Biome.Decor / Nodes okur.
/// </summary>
public partial class World
{
    /// <summary>Biyom haritasi ve kesif noktalari (Sim'deki ile ayni tohumdan; GameState.BiomeMap / Pois ile ozdes).</summary>
    public BiomeMap Map { get; private set; }
    public List<Poi> Pois { get; private set; }

    /// <summary>Baslangic biyomu disindaki biyomlar (Koru).</summary>
    public IEnumerable<(string id, BiomeDef def)> OtherBiomes =>
        Map.Order.Where(id => id != Biomes.World.Biome && Biomes.Biomes.ContainsKey(id)).Select(id => (id, Biomes.Biomes[id]));

    readonly Dictionary<string, FastNoiseLite> _biomeNoise = new();

    void BuildBiomeNoise()
    {
        foreach (var (id, b) in OtherBiomes)
            if (b.Terrain.Amp > 0 || b.Terrain.Offset != 0)
                _biomeNoise[id] = new FastNoiseLite { Seed = T.Seed + 300 + b.Seed, Frequency = b.Terrain.Frequency, FractalOctaves = 4, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
    }

    /// <summary>Biyomlarin yukseklik terimi: agirlik 0 olan yerde (ussun cevresi) tam 0.</summary>
    float BiomeHeight(float wx, float wz)
    {
        float h = 0;
        foreach (var (id, noise) in _biomeNoise)
        {
            float w = Map.Weight(id, wx, wz);
            if (w <= 0f) continue;
            var t = Biomes.Biomes[id].Terrain;
            float n = noise.GetNoise2D(wx, wz);
            float shape = Mathf.Lerp(n * 0.5f + 0.5f, 1f - Mathf.Abs(n), t.Ridge);
            h += w * (t.Offset + t.Amp * shape);
        }
        return h;
    }

    public float BiomeWeight(string id, Vector3 p) => Map.Weight(id, p.X, p.Z);
    public string BiomeAt(Vector3 p) => Map.BiomeAt(p.X, p.Z);
    public BiomeDef BiomeDefAt(Vector3 p) => Biomes.Biomes[BiomeAt(p)];

    // ---------- Biyom agirlik dokusu (shader'lar) ----------

    /// <summary>Doku cozunurlugu: 2 m (640 m dunyada 320^2).</summary>
    const float BiomeTexCell = 2f;
    ImageTexture _biomeTex;

    /// <summary>R: ikinci biyomun (Koru) agirligi. Shader'lar zemin, ot rengi ve sikligi icin okur.</summary>
    public ImageTexture BiomeTex
    {
        get
        {
            if (_biomeTex != null) return _biomeTex;
            int res = Mathf.CeilToInt(Size / BiomeTexCell);
            var bytes = new byte[res * res];
            var second = OtherBiomes.Select(b => b.id).FirstOrDefault();
            if (second != null)
                for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float wx = (x + 0.5f) * BiomeTexCell - Size / 2f, wz = (z + 0.5f) * BiomeTexCell - Size / 2f;
                    bytes[z * res + x] = (byte)(Mathf.Clamp(Map.Weight(second, wx, wz), 0f, 1f) * 255f);
                }
            _biomeTex = ImageTexture.CreateFromImage(Image.CreateFromData(res, res, false, Image.Format.R8, bytes));
            return _biomeTex;
        }
    }

    /// <summary>Ikinci biyomun paleti ve zemin ayari (shader uniform'lari icin); yoksa Cayir'inki.</summary>
    BiomeDef SecondBiome => OtherBiomes.Select(b => b.def).FirstOrDefault() ?? Biome;

    Color SecondPal(string key, Color fallback) =>
        SecondBiome.Palette.TryGetValue(key, out var hex) && !string.IsNullOrEmpty(hex) ? new Color(hex) : fallback;

    /// <summary>Terrain ve ot malzemesine biyom karisimi uniform'larini verir.</summary>
    void SetBiomeUniforms(ShaderMaterial m, bool grass)
    {
        var b = SecondBiome;
        m.SetShaderParameter("biome_tex", BiomeTex);
        m.SetShaderParameter("biome_floor_mix", b.Terrain.FloorMix);
        m.SetShaderParameter("biome_grass", b.Terrain.GrassDensity);
        var ground = SecondPal("ground", Palette.Ground); var plant = SecondPal("plant", Palette.Plant); var shadow = SecondPal("shadow", Palette.Shadow);
        if (grass)
        {
            m.SetShaderParameter("biome_tip", plant.Lerp(ground, 0.25f).Darkened(0.08f));
            m.SetShaderParameter("biome_root", plant.Lerp(shadow, 0.5f).Darkened(0.6f));
        }
        else
        {
            m.SetShaderParameter("biome_floor", ground.Lerp(shadow, 0.25f));
            m.SetShaderParameter("biome_moss", plant.Lerp(ground, 0.4f));
        }
    }

    public void SetGrassBiome(ShaderMaterial m) => SetBiomeUniforms(m, grass: true);

    // ---------- Kameranin biyomu: isik karisimi ----------

    /// <summary>Kameranin bulundugu yerdeki ikinci biyom agirligi (0 Cayir, 1 Koru), yumusatilmis.</summary>
    public float CameraBiomeMix { get; private set; }
    float _biomeCheckT;
    /// <summary>Goruntu ve olcum araci: kameradan bagimsiz sabit karisim (null birakir).</summary>
    public float? ForcedBiomeMix;

    public override void _Process(double delta)
    {
        _biomeCheckT -= (float)delta;
        if (_biomeCheckT > 0) return;
        _biomeCheckT = 0.1f;
        var second = OtherBiomes.Select(b => b.id).FirstOrDefault();
        if (second == null) return;
        float target = ForcedBiomeMix ?? 0f;
        if (ForcedBiomeMix == null)
        {
            var cam = GetViewport()?.GetCamera3D();
            if (cam == null) return;
            target = Map.Weight(second, cam.GlobalPosition.X, cam.GlobalPosition.Z);
        }
        float next = ForcedBiomeMix != null ? target : Mathf.MoveToward(CameraBiomeMix, target, 0.25f);
        if (Mathf.Abs(next - CameraBiomeMix) < 0.002f) return;
        CameraBiomeMix = next;
        ApplyTime();
    }

    /// <summary>Goruntu araci icin: karisimi hemen kameranin yerine gore ayarlar.</summary>
    public void SnapBiomeMix(Vector3 at)
    {
        var second = OtherBiomes.Select(b => b.id).FirstOrDefault();
        CameraBiomeMix = second == null ? 0f : Map.Weight(second, at.X, at.Z);
        ApplyTime();
    }

    readonly Dictionary<string, TimeLight> _keys2 = new();

    /// <summary>Ikinci biyomun gun saati anahtari (yoksa Cayir'inki).</summary>
    TimeLight Key2(string id)
    {
        if (_keys2.Count == 0)
            foreach (var (k, def) in SecondBiome.Times) _keys2[k] = new TimeLight(def);
        return _keys2.TryGetValue(id, out var v) ? v : Key(id);
    }
}
