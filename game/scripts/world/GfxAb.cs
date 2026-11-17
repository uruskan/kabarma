using System;
using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Gelistirici olcumu (gorsel-dunya): --test=gfxab [--gfxab_mark=us] [--gfxab_time=ogle]. Yer imine gecer, on ayarin
/// ustunde ozellikleri tek tek kapatip her birinde birkac saniye ortalama kare suresini (ms) yazar. Kademeleri
/// ayarlamak icin; kanit degil (ayni anda baska Godot calisiyorsa sayilar sisar). "GFXAB ..." satirlari.
/// </summary>
public partial class GfxAb : Node
{
    Director _d;
    Camera3D _cam;
    float _t, _stepT;
    int _step = -1;
    double _sum; int _n;
    ulong _last;
    readonly List<(string name, Action on, Action off)> _steps = new();

    World W => _d.World;

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        double real = _last == 0 ? 0 : (now - _last) / 1e6; // gercek sure: oyun zamani yavaslatilmis
        _last = now;
        _t += (float)real;
        _d ??= GetParent().GetNodeOrNull<Director>("Director");
        if (_d == null || _t < 3f) return;
        if (_step < 0) { Setup(); _step = 0; _steps[0].on(); _stepT = 0; _sum = 0; _n = 0; return; }
        _stepT += (float)real;
        if (_stepT > 1.2f) { _sum += real * 1000.0; _n++; }
        if (_stepT < 5f) return;
        GD.Print($"GFXAB {_steps[_step].name,-16} mean={_sum / Math.Max(_n, 1):0.00}ms frames={_n}");
        _steps[_step].off();
        _step++;
        if (_step >= _steps.Count) { GD.Print("GFXAB RESULT ok"); GetTree().Quit(0); _step = int.MaxValue / 2; return; }
        _steps[_step].on(); _stepT = 0; _sum = 0; _n = 0;
    }

    void Setup()
    {
        string mark = Main.Args.TryGetValue("gfxab_mark", out var m) ? m : "us";
        string time = Main.Args.TryGetValue("gfxab_time", out var tm) ? tm : "ogle";
        _d.SpeedScale = 0.001f;
        W.ForceTime(time);
        _cam = W.ShowBookmark(W.Bookmark(mark), null, _d.OcakPos);
        var env = W.Env.Environment;
        var vp = GetViewport();
        var sun = W.Sun;
        Node3D near = W.GetNodeOrNull<Node3D>("GrassNear"), far = W.GetNodeOrNull<Node3D>("GrassFar");
        Node3D lake = W.GetNodeOrNull<Node3D>("Lake"), terrain = W.GetNodeOrNull<Node3D>("Terrain");
        var tmat = (terrain as MeshInstance3D)?.MaterialOverride as ShaderMaterial;
        void Add(string n, Action on, Action off) => _steps.Add((n, on, off));
        Add("base", () => { }, () => { });
        Add("sun_angle_0", () => sun.LightAngularDistance = 0, () => sun.LightAngularDistance = W.Preset.SunAngle);
        Add("soft_shadow_0", () => RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.Hard),
            () => RenderingServer.DirectionalSoftShadowFilterSetQuality((RenderingServer.ShadowQuality)W.Preset.SoftShadows));
        Add("splits_2", () => sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
            () => sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits);
        Add("ssil_off", () => env.SsilEnabled = false, () => env.SsilEnabled = W.Preset.Ssil);
        Add("ssao_off", () => env.SsaoEnabled = false, () => env.SsaoEnabled = W.Preset.Ssao);
        Add("smaa_off", () => vp.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled, () => vp.ScreenSpaceAA = W.Preset.Smaa ? Viewport.ScreenSpaceAAEnum.Smaa : Viewport.ScreenSpaceAAEnum.Disabled);
        Add("glow_off", () => env.GlowEnabled = false, () => env.GlowEnabled = W.Preset.Glow);
        Add("grass_far_off", () => { if (far != null) far.Visible = false; }, () => { if (far != null) far.Visible = true; });
        Add("grass_all_off", () => { if (near != null) near.Visible = false; if (far != null) far.Visible = false; },
            () => { if (near != null) near.Visible = true; if (far != null) far.Visible = true; });
        Add("terrain_detail0", () => tmat?.SetShaderParameter("detail", 0), () => tmat?.SetShaderParameter("detail", W.Preset.TerrainDetail));
        Add("lake_off", () => { if (lake != null) lake.Visible = false; }, () => { if (lake != null) lake.Visible = true; });
        Add("sky_incremental", () => env.Sky.ProcessMode = Sky.ProcessModeEnum.Incremental, () => env.Sky.ProcessMode = Sky.ProcessModeEnum.Realtime);
        Add("shadow_off", () => sun.ShadowEnabled = false, () => sun.ShadowEnabled = true);
        Add("base_again", () => { }, () => { });
    }
}
