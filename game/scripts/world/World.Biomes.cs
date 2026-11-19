using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: biyomlar (M2, dunya2 akisi; TEKNIK §7 "M2 dunyasi", M4 biyom akisi "M4 ara adimi"). Biyom haritasi Sim'de
/// (BiomeMap: Ocak'tan krater sutununa dogru derinlik zinciri + kraterin cevresinde acisal dilimler); burada:
///  - biyomun yukseklik terimi (sirtli gurultu + taban yukseltmesi, biyom agirligiyla carpilir; Cayir'da 0),
///  - biyom agirlik dokulari (terrain ve ot shader'lari zemin rengini, ot sikligini ve rengini karistirir):
///    biome_tex RGBA = Koru, Sazlik, Kanyon, Ayaz; biome_tex2 R = Gobek (OtherBiomes sirasiyla),
///  - kamera hangi biyomdaysa gun saati isiginin o biyomun saatlerine karismasi (World.Sky.cs ApplyTime).
/// "Biome" (Current) hep baslangic biyomudur (Cayir): diger akislar Biome.Decor / Nodes okur.
/// </summary>
public partial class World
{
    /// <summary>Biyom haritasi ve kesif noktalari (Sim'deki ile ayni tohumdan; GameState.BiomeMap / Pois ile ozdes).</summary>
    public BiomeMap Map { get; private set; }
    public List<Poi> Pois { get; private set; }

    /// <summary>Baslangic biyomu disindaki biyomlar (Koru, Sazlik, Kanyon, Ayaz, Gobek), kademe sirasiyla.</summary>
    public IEnumerable<(string id, BiomeDef def)> OtherBiomes =>
        Map.Order.Where(id => id != Biomes.World.Biome && Biomes.Biomes.ContainsKey(id)).Select(id => (id, Biomes.Biomes[id]));

    readonly Dictionary<string, FastNoiseLite> _biomeNoise = new();

    readonly Dictionary<string, int> _orderIndex = new();

    void BuildBiomeNoise()
    {
        for (int i = 0; i < Map.Order.Count; i++) _orderIndex[Map.Order[i]] = i;
        foreach (var (id, b) in OtherBiomes)
            if (b.Terrain.Amp > 0 || b.Terrain.Offset != 0)
                _biomeNoise[id] = new FastNoiseLite { Seed = T.Seed + 300 + b.Seed, Frequency = b.Terrain.Frequency, FractalOctaves = 4, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin };
    }

    /// <summary>Biyomlarin yukseklik terimi: agirlik 0 olan yerde (ussun cevresi, M2 cekirdegi) tam 0.</summary>
    float BiomeHeight(float wx, float wz)
    {
        float h = 0;
        System.Span<float> ws = stackalloc float[Map.Order.Count];
        Map.Weights(wx, wz, ws); // tek gecis: derinlik ve kutup hesabi paylasilir (1800 m dunyada ~3,2 milyon hucre)
        foreach (var (id, noise) in _biomeNoise)
        {
            int oi = _orderIndex.TryGetValue(id, out var k) ? k : -1;
            float w = oi >= 0 ? ws[oi] : 0f;
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

    // ---------- Biyom orneklem alanlari (M2 cekirdegi + 1800 m dunya) ----------

    /// <summary>Cayir ve Koru (derinlik zinciri) M2'de ±(cekirdek/2) karesinde orneklenirdi: yerlesim ve tohum akisi bit bit ayni kalsin.</summary>
    public bool IsHome(string biome) => Map.Home.Contains(biome);

    /// <summary>Orneklem karesi [x0, z0, x1, z1]: ev biyomlari icin eski cekirdek (merkez Ocak), digerleri icin dunya karesi.</summary>
    public Rect2 SampleRect(string biome, float margin)
    {
        if (IsHome(biome) && T.CoreSize > 0)
        {
            float h = T.CoreSize / 2f - margin;
            return new Rect2(-h, -h, 2 * h, 2 * h);
        }
        float hh = Size / 2f - margin;
        return new Rect2(Center.X - hh, Center.Y - hh, 2 * hh, 2 * hh);
    }

    readonly Dictionary<string, Rect2> _biomeRects = new();

    /// <summary>
    /// Orneklem karesi: ev biyomlari icin SampleRect (eski tohum akisi); digerleri icin biyomun agirliginin 0,02'yi astigi
    /// bolgenin sinir kutusu (32 m'lik tarama): kucuk biyomlarda (Gobek) reddetme ornekleme yeterli denemeye sahip olsun.
    /// </summary>
    public Rect2 BiomeRect(string biome, float margin)
    {
        if (IsHome(biome)) return SampleRect(biome, margin);
        if (!_biomeRects.TryGetValue(biome, out var r0))
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            var w = WorldRect(0f);
            for (float x = w.Position.X; x <= w.End.X; x += 32f)
            for (float z = w.Position.Y; z <= w.End.Y; z += 32f)
                if (Map.Weight(biome, x, z) > 0.02f) { x0 = Mathf.Min(x0, x); z0 = Mathf.Min(z0, z); x1 = Mathf.Max(x1, x); z1 = Mathf.Max(z1, z); }
            r0 = x0 > x1 ? new Rect2(w.Position, Vector2.Zero) : new Rect2(x0 - 32f, z0 - 32f, x1 - x0 + 64f, z1 - z0 + 64f);
            _biomeRects[biome] = r0;
        }
        var lim = WorldRect(margin);
        var inter = r0.Intersection(lim);
        return inter.Size.X > 0 && inter.Size.Y > 0 ? inter : lim;
    }

    /// <summary>Cekirdegin disinda kalan dunya (ev biyomlarinin "uzak" bolgesi icin).</summary>
    public bool InCore(float x, float z)
    {
        if (T.CoreSize <= 0) return true;
        float h = T.CoreSize / 2f;
        return Mathf.Abs(x) <= h && Mathf.Abs(z) <= h;
    }

    // ---------- Biyom agirlik dokulari (shader'lar) ----------

    /// <summary>Doku cozunurlugu: 2 m (640 m dunyada 320^2), 1800 m dunyada 900^2.</summary>
    const float BiomeTexCell = 2f;
    ImageTexture _biomeTex, _biomeTex2;

    void BuildBiomeTextures()
    {
        int res = Mathf.CeilToInt(Size / BiomeTexCell);
        var others = OtherBiomes.Select(b => b.id).ToList();
        var a = new byte[res * res * 4];
        var b = new byte[res * res];
        var wbuf = new float[Map.Order.Count];
        var ordIdx = others.Select(o => _orderIndex[o]).ToArray();
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float wx = (x + 0.5f) * BiomeTexCell + Origin.X, wz = (z + 0.5f) * BiomeTexCell + Origin.Y;
            Map.Weights(wx, wz, wbuf);
            for (int k = 0; k < others.Count && k < 5; k++)
            {
                byte v = (byte)(Mathf.Clamp(wbuf[ordIdx[k]], 0f, 1f) * 255f);
                if (k < 4) a[(z * res + x) * 4 + k] = v; else b[z * res + x] = v;
            }
        }
        _biomeTex = ImageTexture.CreateFromImage(Image.CreateFromData(res, res, false, Image.Format.Rgba8, a));
        _biomeTex2 = ImageTexture.CreateFromImage(Image.CreateFromData(res, res, false, Image.Format.R8, b));
    }

    /// <summary>RGBA: Koru, Sazlik, Kanyon, Ayaz agirliklari (OtherBiomes sirasiyla).</summary>
    public ImageTexture BiomeTex { get { if (_biomeTex == null) BuildBiomeTextures(); return _biomeTex; } }
    /// <summary>R: besinci diger biyom (Gobek).</summary>
    public ImageTexture BiomeTex2 { get { if (_biomeTex2 == null) BuildBiomeTextures(); return _biomeTex2; } }

    Color OtherPal(BiomeDef b, string key, Color fallback) =>
        b.Palette.TryGetValue(key, out var hex) && !string.IsNullOrEmpty(hex) ? new Color(hex) : fallback;

    /// <summary>Terrain ve ot malzemesine biyom karisimi uniform'larini verir (digerleri: ilk = Koru "biome_*", sonrakiler "b2_*".."b5_*").</summary>
    void SetBiomeUniforms(ShaderMaterial m, bool grass)
    {
        m.SetShaderParameter("biome_tex", BiomeTex);
        m.SetShaderParameter("biome_tex2", BiomeTex2);
        m.SetShaderParameter("world_center", Center);
        int i = 0;
        foreach (var (_, b) in OtherBiomes)
        {
            if (i >= 5) break;
            var ground = OtherPal(b, "ground", Palette.Ground); var plant = OtherPal(b, "plant", Palette.Plant); var shadow = OtherPal(b, "shadow", Palette.Shadow);
            string pre = i == 0 ? "biome_" : $"b{i + 1}_";
            if (grass)
            {
                m.SetShaderParameter(pre + "tip", plant.Lerp(ground, 0.25f).Darkened(0.08f));
                m.SetShaderParameter(pre + "root", plant.Lerp(shadow, 0.5f).Darkened(0.6f));
            }
            else
            {
                m.SetShaderParameter(i == 0 ? "biome_floor_mix" : $"b{i + 1}_mix", b.Terrain.FloorMix);
                m.SetShaderParameter(pre + "floor", ground.Lerp(shadow, 0.25f));
                m.SetShaderParameter(pre + "moss", plant.Lerp(ground, 0.4f));
            }
            i++;
        }
    }

    public void SetGrassBiome(ShaderMaterial m) => SetBiomeUniforms(m, grass: true);

    // ---------- Kameranin biyomu: isik karisimi ----------

    /// <summary>Kameranin bulundugu yerdeki ilk diger biyomun (Koru) agirligi (0..1), yumusatilmis; sinamalar okur.</summary>
    public float CameraBiomeMix => _camMix.Length > 0 ? _camMix[0] : 0f;
    float[] _camMix = System.Array.Empty<float>();
    float _biomeCheckT;
    /// <summary>Goruntu ve olcum araci: kameradan bagimsiz sabit Koru karisimi (null birakir; digerleri 0).</summary>
    public float? ForcedBiomeMix;

    float[] CameraWeights(Vector3 at) => OtherBiomes.Select(b => Map.Weight(b.id, at.X, at.Z)).ToArray();

    public override void _Process(double delta)
    {
        _biomeCheckT -= (float)delta;
        if (_biomeCheckT > 0) return;
        _biomeCheckT = 0.1f;
        int n = OtherBiomes.Count();
        if (n == 0) return;
        float[] target = new float[n];
        if (ForcedBiomeMix != null) target[0] = ForcedBiomeMix.Value;
        else
        {
            var cam = GetViewport()?.GetCamera3D();
            if (cam == null) return;
            target = CameraWeights(cam.GlobalPosition);
        }
        if (_camMix.Length != n) _camMix = new float[n];
        bool changed = false;
        for (int i = 0; i < n; i++)
        {
            float next = ForcedBiomeMix != null ? target[i] : Mathf.MoveToward(_camMix[i], target[i], 0.25f);
            if (Mathf.Abs(next - _camMix[i]) < 0.002f) continue;
            _camMix[i] = next; changed = true;
        }
        if (changed) ApplyTime();
    }

    /// <summary>Goruntu araci icin: karisimi hemen kameranin yerine gore ayarlar.</summary>
    public void SnapBiomeMix(Vector3 at)
    {
        _camMix = OtherBiomes.Any() ? CameraWeights(at) : System.Array.Empty<float>();
        ApplyTime();
    }

    readonly Dictionary<string, Dictionary<string, TimeLight>> _keysOf = new();

    /// <summary>Diger biyomun gun saati anahtari (o biyomda yoksa Cayir'inki).</summary>
    TimeLight KeyOf(string biome, string id)
    {
        if (!_keysOf.TryGetValue(biome, out var d))
        {
            d = new Dictionary<string, TimeLight>();
            foreach (var (k, def) in Biomes.Biomes[biome].Times) d[k] = new TimeLight(def);
            _keysOf[biome] = d;
        }
        return d.TryGetValue(id, out var v) ? v : Key(id);
    }

    /// <summary>Ilk diger biyomun (Koru) sis rengi; sis katmani yoksa Cayir'inki. World.Fog.cs okur.</summary>
    BiomeDef SecondBiome => OtherBiomes.Select(b => b.def).FirstOrDefault() ?? Biome;
}
