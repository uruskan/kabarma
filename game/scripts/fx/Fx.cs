using System;
using Godot;

namespace Kabarma.Game;

/// <summary>Kodla yazilan efektler: parcacik patlamalari, hasar sayilari, cantaya ucan parcalar (SANAT-SES §7).</summary>
public static class Fx
{
    public static Node3D Root;

    static StandardMaterial3D Unlit(Color c, bool glow = false) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = c,
        VertexColorUseAsAlbedo = true,
        EmissionEnabled = glow,
        Emission = c,
        EmissionEnergyMultiplier = glow ? 2.2f : 0f,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled,
    };

    public static GpuParticles3D MakeTrail(Color a, Color b)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Down,
            Spread = 18f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 6f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.5f,
            ScaleMax = 1.0f,
            Color = a,
        };
        var grad = new Gradient();
        grad.SetColor(0, a);
        grad.SetColor(1, new Color(b, 0f));
        pm.ColorRamp = new GradientTexture1D { Gradient = grad };
        var p = new GpuParticles3D
        {
            Amount = 48,
            Lifetime = 0.7f,
            ProcessMaterial = pm,
            DrawPass1 = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 6, Rings = 3, Material = Unlit(Colors.White, true) },
        };
        ((StandardMaterial3D)((SphereMesh)p.DrawPass1).Material).Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        return p;
    }

    /// <summary>Tek seferlik parcacik patlamasi.</summary>
    public static void Burst(Vector3 pos, Color color, int amount = 16, float speed = 5f, float size = 0.15f, float life = 0.6f, bool glow = false, float gravity = 9f)
    {
        if (Root == null) return;
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = speed * 0.4f,
            InitialVelocityMax = speed,
            Gravity = new Vector3(0, -gravity, 0),
            ScaleMin = 0.6f,
            ScaleMax = 1.2f,
            DampingMin = 1f,
            DampingMax = 3f,
        };
        var grad = new Gradient();
        grad.SetColor(0, color);
        grad.SetColor(1, new Color(color, 0f));
        pm.ColorRamp = new GradientTexture1D { Gradient = grad };
        var mat = Unlit(Colors.White, glow);
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        var p = new GpuParticles3D
        {
            Amount = Math.Max(1, amount),
            Lifetime = life,
            OneShot = true,
            Explosiveness = 0.95f,
            ProcessMaterial = pm,
            DrawPass1 = new SphereMesh { Radius = size, Height = size * 2, RadialSegments = 6, Rings = 3, Material = mat },
        };
        Root.AddChild(p);
        p.GlobalPosition = pos;
        p.Emitting = true;
        p.GetTree().CreateTimer(life + 0.5f).Timeout += p.QueueFree;
    }

    /// <summary>Hasar sayisi: kritik buyuk ve sari (SANAT-SES §7 Vurus). Sayilar feel.json damageNumbers; master.damageNumbers 0 kapatir.</summary>
    public static void DamageNumber(Vector3 pos, float dmg, bool crit)
    {
        if (Root == null) return;
        var feel = (Root as Director)?.Feel.Data;
        if (feel == null) return;
        var f = feel.DamageNumbers;
        float m = feel.Master.DamageNumbers;
        if (m <= 0f) return;
        var l = new Label3D
        {
            Text = ((int)dmg).ToString(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = Mathf.Max(1, (int)((crit ? f.CritSize : f.Size) * m)),
            OutlineSize = (int)(crit ? f.CritOutline : f.Outline),
            Modulate = crit ? Palette.Warning : Palette.OffWhite,
            OutlineModulate = Palette.NightDeep,
            PixelSize = f.PixelSize,
            RenderPriority = 10,
            OutlineRenderPriority = 9,
        };
        Root.AddChild(l);
        var start = pos + new Vector3((float)GD.RandRange(-f.Jitter, f.Jitter), f.Lift, (float)GD.RandRange(-f.Jitter, f.Jitter));
        l.GlobalPosition = start;
        var tw = l.CreateTween();
        tw.SetParallel();
        tw.TweenProperty(l, "global_position", start + Vector3.Up * f.Rise, f.RiseSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(l, "scale", Vector3.One * (crit ? f.CritScale : 1f), f.PopSeconds).From(Vector3.One * f.PopFrom);
        tw.TweenProperty(l, "modulate:a", 0f, f.FadeSeconds).SetDelay(f.FadeDelay);
        tw.Chain().TweenCallback(Callable.From(l.QueueFree));
    }

    public static void Text3D(Vector3 pos, string text, Color color, int size = 72, float rise = 2f, float life = 1.6f)
    {
        if (Root == null) return;
        var l = new Label3D
        {
            Text = text, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            FontSize = size, OutlineSize = 18, Modulate = color, OutlineModulate = Palette.NightDeep, PixelSize = 0.008f,
        };
        Root.AddChild(l);
        l.GlobalPosition = pos;
        var tw = l.CreateTween();
        tw.TweenProperty(l, "global_position", pos + Vector3.Up * rise, life).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(l, "modulate:a", 0f, 0.4f).SetDelay(life - 0.4f);
        tw.TweenCallback(Callable.From(l.QueueFree));
    }

    /// <summary>Kopan parca hedefe (sirt cantasina) ucar: "pit pit" (PLAN 1.4, 0:30).</summary>
    public static void FlyTo(Vector3 from, Func<Vector3> target, Color color, float size = 0.16f, Action onArrive = null)
    {
        if (Root == null) { onArrive?.Invoke(); return; }
        var kit = new MeshKit();
        kit.Pillow(Vector3.One * size * 2f, MeshKit.At(0, 0, 0), color, 0.5f, 8, 4);
        var mi = kit.Instance();
        Root.AddChild(mi);
        mi.GlobalPosition = from;
        var side = new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(0.6, 1.6), (float)GD.RandRange(-1, 1));
        float t = 0;
        var tw = mi.CreateTween();
        tw.TweenMethod(Callable.From<float>(k =>
        {
            t = k;
            var to = target();
            var mid = from.Lerp(to, 0.5f) + side;
            var a = from.Lerp(mid, k); var b = mid.Lerp(to, k);
            mi.GlobalPosition = a.Lerp(b, k);
            mi.Scale = Vector3.One * Mathf.Lerp(1f, 0.4f, k);
            mi.RotateY(0.3f);
        }), 0f, 1f, 0.45f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(() => { onArrive?.Invoke(); mi.QueueFree(); }));
    }
}
