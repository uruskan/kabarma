using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dunya: grafik on ayari (data/graphics.json; TEKNIK §10 "yuksek"). Gorsel kisim (golge, SSAO, glow, MSAA, sis,
/// bitki yogunlugu, ot yaricapi) her zaman uygulanir; pencere boyu, dikey esitleme ve kare siniri yalniz olcumde
/// ya da --preset=AD verilince (oyuncunun ekranina karismamak icin).
/// </summary>
public partial class World
{
    public GraphicsPreset Preset => Data.Graphics.Preset(Main.Args.TryGetValue("preset", out var p) ? p : null);

    void ApplyGraphics()
    {
        var g = Preset;
        var env = Env.Environment;
        env.SsaoEnabled = g.Ssao;
        RenderingServer.EnvironmentSetSsaoQuality(g.SsaoHalf ? RenderingServer.EnvironmentSsaoQuality.Medium : RenderingServer.EnvironmentSsaoQuality.High, g.SsaoHalf, 0.5f, 2, 50f, 300f);
        env.GlowEnabled = g.Glow;
        env.SdfgiEnabled = g.Sdfgi;
        env.SsilEnabled = g.Ssil;
        if (g.Ssil) RenderingServer.EnvironmentSetSsilQuality(g.SsilHalf ? RenderingServer.EnvironmentSsilQuality.Medium : RenderingServer.EnvironmentSsilQuality.High, g.SsilHalf, 0.5f, 4, 50f, 300f);
        env.SsrEnabled = g.Ssr;
        env.VolumetricFogEnabled = g.VolumetricFog && g.VolumetricDensity > 0;
        if (env.VolumetricFogEnabled)
        {
            env.VolumetricFogDensity = g.VolumetricDensity;
            env.VolumetricFogAnisotropy = 0.6f;
            env.VolumetricFogLength = 120f;
            env.VolumetricFogDetailSpread = 2f;
            env.VolumetricFogGIInject = 0.6f;
            env.VolumetricFogSkyAffect = 0.0f;
            env.VolumetricFogTemporalReprojectionEnabled = true;
        }
        // Yumusak golge: suzgec kalitesi ve gunesin acisal capi (yari golge yakinda keskin, uzakta yumusak).
        RenderingServer.DirectionalSoftShadowFilterSetQuality((RenderingServer.ShadowQuality)Mathf.Clamp(g.SoftShadows, 0, 5));
        Sun.LightAngularDistance = g.SunAngle;
        Sun.DirectionalShadowBlendSplits = g.ShadowSplits >= 4;
        Sun.ShadowBias = 0.04f;
        Sun.ShadowNormalBias = 1.2f;
        env.FogEnabled = g.DistanceFog || g.HeightFog;
        // Yukseklik sisi: gol cukuru ve vadiler daha sisli (uc derinlik katmani, SANAT-SES §1.4).
        var hf = Biomes.World.HeightFog;
        env.FogHeight = g.HeightFog ? hf[0] : -1000f;
        env.FogHeightDensity = g.HeightFog ? hf[1] : 0f;
        Sun.DirectionalShadowMode = g.ShadowSplits >= 4 ? DirectionalLight3D.ShadowMode.Parallel4Splits
            : g.ShadowSplits == 2 ? DirectionalLight3D.ShadowMode.Parallel2Splits : DirectionalLight3D.ShadowMode.Orthogonal;
        Sun.DirectionalShadowMaxDistance = g.ShadowDistance;
        if (g.ShadowAtlas > 0) RenderingServer.DirectionalShadowAtlasSetSize(g.ShadowAtlas, true);
        var vp = GetViewport();
        if (vp != null)
        {
            vp.Msaa3D = g.Msaa switch { >= 8 => Viewport.Msaa.Msaa8X, >= 4 => Viewport.Msaa.Msaa4X, >= 2 => Viewport.Msaa.Msaa2X, _ => Viewport.Msaa.Disabled };
            vp.Scaling3DScale = g.Scale;
            vp.ScreenSpaceAA = g.Smaa ? Viewport.ScreenSpaceAAEnum.Smaa : Viewport.ScreenSpaceAAEnum.Disabled;
            vp.UseTaa = g.Taa;
        }
        if (Main.Args.ContainsKey("preset")) ApplyWindow(g);
    }

    /// <summary>On ayarin pencere kismi: cozunurluk, dikey esitleme, kare siniri (olcum ve goruntu araci).</summary>
    public static void ApplyWindow(GraphicsPreset g)
    {
        if (DisplayServer.GetName() == "headless") return;
        DisplayServer.WindowSetVsyncMode(g.Vsync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = g.MaxFps;
        if (g.Width > 0 && g.Height > 0)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(g.Width, g.Height));
        }
    }
}
