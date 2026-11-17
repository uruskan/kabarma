using Godot;

namespace Kabarma.Game;

/// <summary>Dunya: gokyuzu, sis, gunes, gun saati (ApplyTime), ufuk tepeleri ve krater isik sutunu.</summary>
public partial class World
{
    public WorldEnvironment Env { get; private set; }
    public DirectionalLight3D Sun { get; private set; }
    ProceduralSkyMaterial _sky;
    MeshInstance3D _pillar;

    float _dayMix;     // 0 ogle, 1 gunbatimi
    float _nightMix;   // 0..1 Kabarma gecesi
    float _dawnMix;    // 0..1 safak

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
