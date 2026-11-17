using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: gokyuzu, sis, gunes/ay isigi, gun saati, ufuk tepeleri ve krater isik sutunu.
/// Gun saatleri data/biomes.json'da (SANAT-SES §2): sabah, ogle, gunbatimi, kabarma_gecesi, normal_gece.
/// Director her karede SetDay/SetNight/SetDawn cagirir (aksam, gece, safak); SetNormalNight gun dongusu icindir.
/// ForceTime(anahtar) hepsinin ustune gecer (yer imi goruntuleri ve olcum).
/// </summary>
public partial class World
{
    public WorldEnvironment Env { get; private set; }
    /// <summary>Yonlu isik: gunduz gunes, gece ay (SANAT-SES §2 "Gunes yok; ay 35 derece").</summary>
    public DirectionalLight3D Sun { get; private set; }
    ShaderMaterial _sky;
    MeshInstance3D _pillar;

    float _dayMix;         // 0 ogle, 1 gunbatimi
    float _nightMix;       // 0..1 Kabarma gecesi
    float _dawnMix;        // 0..1 sabah (safak)
    float _normalNightMix; // 0..1 normal gece (Kabarma'siz)
    string _forced;

    /// <summary>Su anki harmanlanmis isik (sinamalar ve olcum okur).</summary>
    public TimeLight Light { get; private set; }

    /// <summary>Bir gun saatinin isigi; iki saat arasinda dogrusal harmanlanir.</summary>
    public readonly struct TimeLight
    {
        public readonly float Elev, Az, Energy, SunDisk, MoonDisk, MoonElev, MoonAz, FogDensity, Ambient, Stars, Aurora, Clouds, Veins;
        public readonly Color LightColor, Top, Horizon, Fog, CloudColor;

        public TimeLight(TimeOfDayDef d)
        {
            Elev = d.LightElevation; Az = d.LightAzimuth; Energy = d.LightEnergy; SunDisk = d.SunDisk; MoonDisk = d.MoonDisk;
            MoonElev = d.MoonElevation; MoonAz = d.MoonAzimuth; FogDensity = d.FogDensity; Ambient = d.Ambient;
            Stars = d.Stars; Aurora = d.Aurora; Clouds = d.Clouds; Veins = d.Veins;
            LightColor = new Color(d.LightColor); Top = new Color(d.SkyTop); Horizon = new Color(d.SkyHorizon);
            Fog = new Color(d.Fog); CloudColor = new Color(d.CloudColor);
        }

        TimeLight(float elev, float az, float energy, float sunDisk, float moonDisk, float moonElev, float moonAz, float fogDensity,
            float ambient, float stars, float aurora, float clouds, float veins, Color light, Color top, Color hor, Color fog, Color cloud)
        {
            Elev = elev; Az = az; Energy = energy; SunDisk = sunDisk; MoonDisk = moonDisk; MoonElev = moonElev; MoonAz = moonAz;
            FogDensity = fogDensity; Ambient = ambient; Stars = stars; Aurora = aurora; Clouds = clouds; Veins = veins;
            LightColor = light; Top = top; Horizon = hor; Fog = fog; CloudColor = cloud;
        }

        static float LerpAngle(float a, float b, float t) => a + Mathf.Wrap(b - a + 180f, 0f, 360f) * t - 180f * t;

        public static TimeLight Lerp(in TimeLight a, in TimeLight b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return new TimeLight(Mathf.Lerp(a.Elev, b.Elev, t), LerpAngle(a.Az, b.Az, t), Mathf.Lerp(a.Energy, b.Energy, t),
                Mathf.Lerp(a.SunDisk, b.SunDisk, t), Mathf.Lerp(a.MoonDisk, b.MoonDisk, t), Mathf.Lerp(a.MoonElev, b.MoonElev, t),
                LerpAngle(a.MoonAz, b.MoonAz, t), Mathf.Lerp(a.FogDensity, b.FogDensity, t), Mathf.Lerp(a.Ambient, b.Ambient, t),
                Mathf.Lerp(a.Stars, b.Stars, t), Mathf.Lerp(a.Aurora, b.Aurora, t), Mathf.Lerp(a.Clouds, b.Clouds, t),
                Mathf.Lerp(a.Veins, b.Veins, t), a.LightColor.Lerp(b.LightColor, t), a.Top.Lerp(b.Top, t),
                a.Horizon.Lerp(b.Horizon, t), a.Fog.Lerp(b.Fog, t), a.CloudColor.Lerp(b.CloudColor, t));
        }
    }

    readonly Dictionary<string, TimeLight> _keys = new();
    TimeLight Key(string id) => _keys.TryGetValue(id, out var k) ? k : _keys["ogle"];

    /// <summary>Gun saati anahtarlari (SANAT-SES §2): 16 goruntunun dort saati + normal gece.</summary>
    public static readonly string[] ShotTimes = { "sabah", "ogle", "gunbatimi", "kabarma_gecesi" };

    /// <summary>Azimut (0 kuzey +Z, 90 dogu -X) ve yukseklikten birim yon.</summary>
    public static Vector3 Dir(float elevDeg, float azDeg)
    {
        float e = Mathf.DegToRad(elevDeg), a = Mathf.DegToRad(azDeg);
        return new Vector3(-Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)).Normalized();
    }

    // ---------- Gokyuzu, sis, isik ----------

    void BuildEnvironment()
    {
        foreach (var (id, def) in Biome.Times) _keys[id] = new TimeLight(def);
        _sky = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sky_kabarma.gdshader") };
        var cloudNoise = new NoiseTexture2D
        {
            Width = 512, Height = 512, Seamless = true, GenerateMipmaps = true,
            Noise = new FastNoiseLite { Seed = T.Seed + 5, Frequency = 0.012f, FractalOctaves = 4, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin },
        };
        _sky.SetShaderParameter("cloud_noise", cloudNoise);
        _sky.SetShaderParameter("ground_color", Pal("ground", Palette.Ground).Lerp(Pal("shadow", Palette.Shadow), 0.4f));
        _sky.SetShaderParameter("aurora_a", Palette.MayaTeal);
        _sky.SetShaderParameter("aurora_b", Palette.MayaViolet);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            // Gok hareketli (bulut, yildiz): isik kupu her karede hizli yolla guncellenir (TEKNIK §10 olcumu bunu da sayar).
            Sky = new Sky { SkyMaterial = _sky, ProcessMode = Sky.ProcessModeEnum.Realtime, RadianceSize = Sky.RadianceSizeEnum.Size256 },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = 0.75f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            // Ton eslemesi ve derecelendirme (gorsel-dunya): AgX, hafif kontrast ve doygunluk; isik enerjileri biomes.json'da.
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.0f,
            TonemapAgxContrast = 1.35f,
            AdjustmentEnabled = true,
            AdjustmentContrast = 1.05f,
            AdjustmentSaturation = 1.12f,
            FogEnabled = true,
            FogLightColor = Palette.Fog,
            FogDensity = 0.0015f,
            // Uzak tepeler gokyuzu rengine erir (hava perspektifi); gunes yonunde sis isir.
            FogAerialPerspective = 0.7f,
            FogSkyAffect = 0.3f,
            FogSunScatter = 0.15f,
            GlowEnabled = true,
            GlowIntensity = 0.45f,
            GlowStrength = 0.9f,
            GlowBloom = 0.02f,
            GlowHdrThreshold = 1.0f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
            SsaoEnabled = true,
            SsaoRadius = 1.1f,
            SsaoIntensity = 1.8f,
            SsaoPower = 1.6f,
            SsaoDetail = 0.6f,
            SsaoLightAffect = 0.1f,
            SsilRadius = 4f,
            SsilIntensity = 0.8f,
            SdfgiUseOcclusion = true,
            SdfgiCascades = 4,
            SdfgiMinCellSize = 0.4f,
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
        var rings = Biomes.World.HorizonRings;
        var plant = Pal("plant", Palette.Plant); var fog = Pal("fog", Palette.Fog); var shadow = Pal("shadow", Palette.Shadow);
        for (int layer = 0; layer < rings.Count; layer++)
        {
            float dist = rings[layer];
            int count = 18 + layer * 6;
            var col = plant.Lerp(fog, 0.3f + layer * 0.22f).Lerp(shadow, 0.12f);
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Tau * (i + r.Randf() * 0.6f) / count;
                var p = new Vector3(Mathf.Cos(a) * dist, -8f, Mathf.Sin(a) * dist);
                float w = r.RandfRange(50f, 85f) + layer * 25f, h = r.RandfRange(22f, 40f) + layer * 14f;
                kit.Sphere(1f, MeshKit.At(p, new Vector3(0, a, 0), new Vector3(w, h, w * 0.7f)), col, 10, 5);
            }
        }
        var hills = kit.Instance();
        hills.Name = "Horizon";
        hills.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        // Uzak tepeler: duz renk yerine orman lekeli, kayali yuzey (far_hills.gdshader); sis gokyuzune eritir.
        var hillMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/far_hills.gdshader") };
        hillMat.SetShaderParameter("macro_noise", MacroNoise);
        hillMat.SetShaderParameter("forest", Pal("plant", Palette.Plant).Lerp(Pal("shadow", Palette.Shadow), 0.55f).Darkened(0.3f));
        hillMat.SetShaderParameter("meadow", Pal("ground", Palette.Ground).Lerp(Pal("plant", Palette.Plant), 0.55f).Darkened(0.35f));
        hillMat.SetShaderParameter("rock", new Color(0.52f, 0.48f, 0.43f));
        hills.MaterialOverride = hillMat;
        AddChild(hills);

        // Krater isik sutunu: Maya'ya ait tek isiyan sey (SANAT-SES §1.5); her yerden gorunur. Yumusak kenarli,
        // tepeye dogru incelen, icinde akan isik demeti (maya_beam.gdshader); sise kendisi karisir.
        var beam = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/maya_beam.gdshader") };
        beam.SetShaderParameter("color_a", Palette.MayaTeal);
        beam.SetShaderParameter("color_b", Palette.MayaViolet);
        var pp = Biomes.World.Pillar;
        _pillar = new MeshInstance3D
        {
            Name = "Pillar",
            Mesh = new CylinderMesh { TopRadius = 9f, BottomRadius = 22f, Height = 900f, RadialSegments = 32, CapTop = false, CapBottom = false },
            MaterialOverride = beam,
            Position = pp.Count == 2 ? new Vector3(pp[0], 420f, pp[1]) : new Vector3(-180f, 280f, -330f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_pillar);
    }

    // ---------- Gun saati ----------

    public void SetDay(float sunsetMix) { _dayMix = Mathf.Clamp(sunsetMix, 0, 1); ApplyTime(); }
    public void SetNight(float mix) { _nightMix = Mathf.Clamp(mix, 0, 1); ApplyTime(); }
    public void SetDawn(float mix) { _dawnMix = Mathf.Clamp(mix, 0, 1); ApplyTime(); }
    /// <summary>Normal (Kabarma'siz) gece: 0 gunduz, 1 tam gece. Gun dongusu (gecehud akisi) icin.</summary>
    public void SetNormalNight(float mix) { _normalNightMix = Mathf.Clamp(mix, 0, 1); ApplyTime(); }
    /// <summary>Gun saatini sabitler ("sabah", "ogle", "gunbatimi", "kabarma_gecesi", "normal_gece"); null birakir.</summary>
    public void ForceTime(string key) { _forced = string.IsNullOrEmpty(key) ? null : key; ApplyTime(); }
    public string ForcedTime => _forced;

    void ApplyTime()
    {
        if (_sky == null || _keys.Count == 0) return;
        TimeLight L;
        if (_forced != null) L = Key(_forced);
        else
        {
            L = TimeLight.Lerp(Key("ogle"), Key("gunbatimi"), _dayMix);
            L = TimeLight.Lerp(L, Key("kabarma_gecesi"), _nightMix);
            L = TimeLight.Lerp(L, Key("sabah"), _dawnMix);
            L = TimeLight.Lerp(L, Key("normal_gece"), _normalNightMix);
        }
        Light = L;
        var lightDir = Dir(L.Elev, L.Az);
        var moonDir = Dir(L.MoonElev, L.MoonAz);
        _sky.SetShaderParameter("top_color", L.Top);
        _sky.SetShaderParameter("horizon_color", L.Horizon);
        _sky.SetShaderParameter("sun_dir", L.SunDisk > 0 ? lightDir : -lightDir);
        _sky.SetShaderParameter("sun_color", L.LightColor);
        _sky.SetShaderParameter("sun_disk", L.SunDisk);
        _sky.SetShaderParameter("moon_dir", moonDir);
        _sky.SetShaderParameter("moon2_dir", Dir(L.MoonElev + 11f, L.MoonAz + 34f));
        _sky.SetShaderParameter("moon_disk", L.MoonDisk);
        _sky.SetShaderParameter("stars", L.Stars);
        _sky.SetShaderParameter("aurora", L.Aurora);
        _sky.SetShaderParameter("clouds", L.Clouds);
        _sky.SetShaderParameter("cloud_color", L.CloudColor);
        _sky.SetShaderParameter("cloud_shade", L.CloudColor.Lerp(Pal("shadow", Palette.Shadow), 0.3f).Lerp(L.Top, 0.25f));

        var env = Env.Environment;
        env.FogLightColor = L.Fog;
        env.FogDensity = L.FogDensity;
        env.AmbientLightEnergy = L.Ambient;
        Sun.LightColor = L.LightColor;
        Sun.LightEnergy = L.Energy;
        Sun.Basis = Basis.LookingAt(-lightDir, Mathf.Abs(lightDir.Y) > 0.98f ? Vector3.Forward : Vector3.Up);
        _groundMat?.SetShaderParameter("glow", L.Veins);
        _groundMat?.SetShaderParameter("vein_scale", 0.35f);
        float veins = Biome.Times.TryGetValue("kabarma_gecesi", out var kn) && kn.Veins > 0 ? L.Veins / kn.Veins : 0f;
        if (_pillar != null) _pillar.Scale = new Vector3(1f + veins * 1.5f, 1f, 1f + veins * 1.5f);
        // Su gun saatine gore koyulasir (gece mavi, karanlik degil).
        _waterMat?.SetShaderParameter("tint", Colors.White.Lerp(L.Horizon.Lerp(L.Top, 0.5f), 0.25f) * Mathf.Clamp(0.45f + L.Energy * 0.45f, 0.5f, 1.05f));
    }
}
