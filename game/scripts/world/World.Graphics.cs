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
        RenderingServer.EnvironmentSetSsaoQuality(RenderingServer.EnvironmentSsaoQuality.Medium, g.SsaoHalf, 0.5f, 2, 50f, 300f);
        env.GlowEnabled = g.Glow;
        env.SdfgiEnabled = g.Sdfgi;
        env.SsilEnabled = g.Ssil;
        env.SsrEnabled = g.Ssr;
        env.VolumetricFogEnabled = g.VolumetricFog;
        env.FogEnabled = g.DistanceFog || g.HeightFog;
        // Yukseklik sisi: gol cukuru ve vadiler daha sisli (uc derinlik katmani, SANAT-SES §1.4).
        env.FogHeight = g.HeightFog ? 3f : -1000f;
        env.FogHeightDensity = g.HeightFog ? 0.04f : 0f;
        Sun.DirectionalShadowMode = g.ShadowSplits >= 4 ? DirectionalLight3D.ShadowMode.Parallel4Splits
            : g.ShadowSplits == 2 ? DirectionalLight3D.ShadowMode.Parallel2Splits : DirectionalLight3D.ShadowMode.Orthogonal;
        Sun.DirectionalShadowMaxDistance = g.ShadowDistance;
        if (g.ShadowAtlas > 0) RenderingServer.DirectionalShadowAtlasSetSize(g.ShadowAtlas, true);
        var vp = GetViewport();
        if (vp != null)
        {
            vp.Msaa3D = g.Msaa switch { >= 8 => Viewport.Msaa.Msaa8X, >= 4 => Viewport.Msaa.Msaa4X, >= 2 => Viewport.Msaa.Msaa2X, _ => Viewport.Msaa.Disabled };
            vp.Scaling3DScale = g.Scale;
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
