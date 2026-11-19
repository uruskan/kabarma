using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ortak efekt kitapligi (SANAT-SES §7, data/vfx.json). Kimlikle cagrilir: Vfx.Play("hit_spark", konum). Parcacik dugumleri
/// efekt (ve renk) basina havuzlanir ve kisa omurludur; halkalar ve telgraf diskleri tek paylasimli golgelendiriciyle cizilir.
/// Bilinmeyen kimlik sessizce yok sayilir (yeni sistemler once cagri koyabilir, tarif sonra gelir).
/// Kancalar: Head (Capa basliklari), Hit (savas isabeti), Power (guclar ve ultiler; M4 yeni guclerini kusam bagler),
/// Loop/Stop (durum aurasi), Telegraph, Ring.
/// </summary>
public static class Vfx
{
    static Director _d;
    static VfxFile _f = new();
    static readonly Dictionary<string, Queue<GpuParticles3D>> Pool = new();
    static readonly Queue<MeshInstance3D> RingPool = new();
    static readonly Queue<OmniLight3D> LightPool = new();
    static readonly Dictionary<string, Color> Names = BuildNames();
    static ShaderMaterial _ringProto;
    static int _active;
    /// <summary>Sinama: bu kosuda calan efekt sayisi ve kimlikleri.</summary>
    public static int Played;
    public static readonly HashSet<string> PlayedIds = new();

    static Dictionary<string, Color> BuildNames()
    {
        var d = new Dictionary<string, Color>();
        foreach (var fi in typeof(Palette).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (fi.FieldType == typeof(Color)) d[fi.Name] = (Color)fi.GetValue(null);
        return d;
    }

    public static void Init(Director d)
    {
        _d = d;
        _f = d.Data?.Vfx ?? new VfxFile();
        Pool.Clear(); RingPool.Clear(); LightPool.Clear(); _active = 0; Played = 0; PlayedIds.Clear();
    }

    public static VfxFile Data => _f;
    public static bool Has(string id) => _f.Effects.ContainsKey(id);
    static Node3D Root => Fx.Root;

    static Color Col(string s) => s.StartsWith('#') ? new Color(s) : Names.TryGetValue(s, out var c) ? c : Palette.OffWhite;

    // ---------------- parcacik ----------------

    static GpuParticles3D Make(VfxDef def, Color? tint, int amount, bool loop)
    {
        var a = Col(def.Colors.FirstOrDefault() ?? "OffWhite");
        var b = def.Colors.Count > 1 ? Col(def.Colors[^1]) : a;
        if (tint is Color t) { a = t; b = t.Lerp(b, 0.4f); }
        var pm = new ParticleProcessMaterial
        {
            Direction = def.Dir.Count >= 3 ? new Vector3(def.Dir[0], def.Dir[1], def.Dir[2]) : Vector3.Up,
            Spread = def.Spread,
            InitialVelocityMin = def.Speed * 0.4f,
            InitialVelocityMax = def.Speed,
            Gravity = new Vector3(0, -def.Gravity, 0),
            ScaleMin = 0.7f,
            ScaleMax = 1.2f,
            DampingMin = def.Damping * 0.5f,
            DampingMax = def.Damping,
        };
        if (def.Emit > 0) { pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere; pm.EmissionSphereRadius = def.Emit; }
        var ramp = new Gradient();
        ramp.SetColor(0, a);
        ramp.SetColor(1, new Color(b, 0f));
        pm.ColorRamp = new GradientTexture1D { Gradient = ramp };
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 1));
        curve.AddPoint(new Vector2(1, def.SizeEnd));
        pm.ScaleCurve = new CurveTexture { Curve = curve };
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = Colors.White,
        };
        if (def.Glow) { mat.EmissionEnabled = true; mat.Emission = Colors.White; mat.EmissionEnergyMultiplier = 1.6f; }
        Mesh mesh;
        float s = def.Size;
        switch (def.Shape)
        {
            case "streak":
                pm.ParticleFlagAlignY = true;
                mesh = new BoxMesh { Size = new Vector3(s * 0.45f, s * 3.2f, s * 0.45f), Material = mat };
                break;
            case "quad":
                pm.AngularVelocityMin = -300f; pm.AngularVelocityMax = 300f;
                mesh = new BoxMesh { Size = new Vector3(s, s * 0.6f, s), Material = mat };
                break;
            default:
                mesh = new SphereMesh { Radius = s, Height = s * 2, RadialSegments = 6, Rings = 3, Material = mat };
                break;
        }
        return new GpuParticles3D
        {
            Amount = Math.Max(1, amount),
            Lifetime = def.Life,
            OneShot = !loop,
            Explosiveness = loop ? 0f : 0.95f,
            Emitting = false,
            LocalCoords = false,
            ProcessMaterial = pm,
            DrawPass1 = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    static string Key(string id, Color? tint) => tint is Color t ? id + "#" + t.ToHtml(false) : id;

    /// <summary>Tek seferlik efekt: parcacik (varsa), halka (varsa), isik ve sarsinti.</summary>
    public static void Play(string id, Vector3 pos, Vector3? dir = null, Color? tint = null, float scale = 1f)
    {
        if (Root == null || !_f.Effects.TryGetValue(id, out var def)) return;
        Played++; PlayedIds.Add(id);
        if (def.Count > 0 && !string.IsNullOrEmpty(def.Text))
        {
            Fx.Text3D(pos, def.Text, tint ?? Col(def.Colors.FirstOrDefault() ?? "OffWhite"), 56, 0.8f + def.Speed * 0.3f, def.Life);
        }
        else if (def.Count > 0 && _active < _f.PoolMax) Emit(id, def, pos, dir, tint);
        if (def.Ring > 0) Ring(pos, def.Ring * scale, def.RingSeconds, tint ?? Col(def.Colors.FirstOrDefault() ?? "OffWhite"), def.RingWidth);
        if (def.Light > 0) Flash(pos + Vector3.Up * 0.5f, def.Light * scale, tint ?? Col(def.Colors.FirstOrDefault() ?? "OffWhite"));
        if (def.Shake > 0 && _d?.Player != null)
        {
            float m = _d.Feel?.Data?.Master.Shake ?? 1f;
            float fall = Mathf.Clamp(1f - pos.DistanceTo(_d.Player.GlobalPosition) / 30f, 0f, 1f);
            if (fall > 0) _d.Player.Shake(def.Shake * m * fall);
        }
    }

    static void Emit(string id, VfxDef def, Vector3 pos, Vector3? dir, Color? tint)
    {
        string key = Key(id, tint);
        if (!Pool.TryGetValue(key, out var q)) Pool[key] = q = new Queue<GpuParticles3D>();
        GpuParticles3D p = null;
        while (q.Count > 0 && p == null) { p = q.Dequeue(); if (!GodotObject.IsInstanceValid(p)) p = null; }
        if (p == null) { p = Make(def, tint, def.Count, false); Root.AddChild(p); }
        if (dir is Vector3 dv && dv.LengthSquared() > 1e-4f && p.ProcessMaterial is ParticleProcessMaterial pm) pm.Direction = dv.Normalized();
        p.GlobalPosition = pos;
        p.Restart();
        p.Emitting = true;
        _active++;
        var tree = Root.GetTree();
        tree.CreateTimer(def.Life + 0.15f).Timeout += () =>
        {
            _active--;
            if (!GodotObject.IsInstanceValid(p)) return;
            p.Emitting = false;
            q.Enqueue(p);
        };
    }

    /// <summary>Capa basligi katmani: "head.&lt;baslik&gt;.muzzle|impact|trail"; baslik tarifi yoksa head.default.</summary>
    public static void Head(string head, string kind, Vector3 pos, Color? tint = null, Vector3? dir = null)
    {
        string id = $"head.{head}.{kind}";
        if (!Has(id)) id = $"head.default.{kind}";
        Play(id, pos, dir, Has($"head.{head}.{kind}") ? null : tint);
    }

    /// <summary>Savas isabeti: kritik ve zayif nokta ozel, hasar turu rengi, durum sonuclari (patlama, kilit).</summary>
    public static void Hit(Vector3 at, CombatHit hit, CombatResult r)
    {
        if (r.Blocked) { Play("hit_dust", at); return; }
        Color? tint = hit.Type switch
        {
            DamageType.Ates => Palette.Accent,
            DamageType.Buz => new Color("BFE8FF"),
            DamageType.Sok => Palette.MayaTeal,
            DamageType.Zehir => new Color("8FE070"),
            DamageType.Maya => Palette.MayaViolet,
            _ => null,
        };
        Play(hit.Crit ? "hit_crit" : hit.Weak ? "hit_weak" : "hit_spark", at, null, tint);
        if (r.Burst) Play("swell_burst", at);
        if (r.Locked) Play("freeze_shatter", at);
    }

    /// <summary>Guc ya da ulti: "power.&lt;kimlik&gt;", yoksa power.default. Kusam akisi yeni gucleri buraya baglar (vfx.json'a satir).</summary>
    public static void Power(string id, Vector3 pos, Vector3? dir = null)
    {
        string k = "power." + id;
        Play(Has(k) ? k : "power.default", pos, dir);
    }

    // ---------------- surekli (durum aurasi) ----------------

    /// <summary>Surekli efekt hedefe baglanir (durum aurasi). Donen dugumu Stop ile birakirsin.</summary>
    public static GpuParticles3D Loop(string id, Node3D parent, Vector3 offset, float radius = 0f)
    {
        if (Root == null || parent == null || !_f.Effects.TryGetValue(id, out var def)) return null;
        var p = Make(def, null, def.Count + (int)(radius * 10f), true);
        if (radius > 0 && p.ProcessMaterial is ParticleProcessMaterial lm)
        {
            lm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            lm.EmissionSphereRadius = Mathf.Max(def.Emit, radius * 1.1f); // govdenin icinde degil, yuzeyinde dogsun
        }
        parent.AddChild(p);
        p.Position = offset;
        p.Emitting = true;
        Played++; PlayedIds.Add(id);
        return p;
    }

    public static void Stop(GpuParticles3D p)
    {
        if (p == null || !GodotObject.IsInstanceValid(p)) return;
        p.Emitting = false;
        p.GetTree().CreateTimer(p.Lifetime + 0.2f).Timeout += () => { if (GodotObject.IsInstanceValid(p)) p.QueueFree(); };
    }

    // ---------------- halka ve telgraf ----------------

    static ShaderMaterial RingMaterial()
    {
        _ringProto ??= new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, fog_disabled;
uniform vec4 col : source_color = vec4(1.0);
uniform float p = 0.0;
uniform float w = 0.1;
uniform float mode = 0.0;
void fragment() {
    vec2 c = UV * 2.0 - 1.0;
    float r = length(c);
    float a;
    if (mode < 0.5) {
        a = smoothstep(p - w, p, r) * (1.0 - smoothstep(p, p + 0.02, r)) * (1.0 - p * p);
    } else {
        float rim = smoothstep(1.0 - w, 1.0 - w * 0.4, r) * (1.0 - smoothstep(0.99, 1.0, r));
        float fill = (1.0 - step(p, r)) * 0.28;
        a = (rim * 0.9 + fill) * (r <= 1.0 ? 1.0 : 0.0);
    }
    ALBEDO = col.rgb * 1.8;
    ALPHA = clamp(a * col.a, 0.0, 1.0);
}"
            }
        };
        return (ShaderMaterial)_ringProto.Duplicate();
    }

    static MeshInstance3D RentRing()
    {
        while (RingPool.Count > 0)
        {
            var m = RingPool.Dequeue();
            if (GodotObject.IsInstanceValid(m)) return m;
        }
        var mi = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2, 2), Orientation = PlaneMesh.OrientationEnum.Y },
            MaterialOverride = RingMaterial(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        Root.AddChild(mi);
        return mi;
    }

    static void RunRing(Vector3 pos, float radius, float seconds, Color color, float width, float mode)
    {
        if (Root == null) return;
        var mi = RentRing();
        var mat = (ShaderMaterial)mi.MaterialOverride;
        mat.SetShaderParameter("col", color);
        mat.SetShaderParameter("w", Mathf.Clamp(width / Mathf.Max(radius, 0.2f), 0.02f, 0.9f) * (mode > 0.5f ? 1.6f : 1f));
        mat.SetShaderParameter("mode", mode);
        mat.SetShaderParameter("p", 0f);
        mi.Scale = new Vector3(radius, 1f, radius);
        mi.GlobalPosition = pos + Vector3.Up * 0.06f;
        mi.Visible = true;
        var tw = mi.CreateTween();
        tw.TweenMethod(Callable.From<float>(v => mat.SetShaderParameter("p", v)), 0f, 1f, seconds);
        tw.TweenCallback(Callable.From(() => { mi.Visible = false; RingPool.Enqueue(mi); }));
    }

    /// <summary>Yerde buyuyen halka (guc dalgasi, yukseltme, patlama).</summary>
    public static void Ring(Vector3 pos, float radius, float seconds, Color color, float width = 0.15f) => RunRing(pos, radius, seconds, color, width, 0f);

    /// <summary>Telgraf: saldirinin vuracagi daire. Kenar sabit, ic dolgu sure boyunca kenara dolar (kirmizi: tehlike).</summary>
    public static void Telegraph(Vector3 pos, float radius, float seconds, bool danger = false)
    {
        Played++; PlayedIds.Add(danger ? "telegraph_danger" : "telegraph_warn");
        RunRing(pos, radius, seconds, danger ? Palette.HoldingRed : Palette.Warning, 0.18f, 1f);
    }

    static void Flash(Vector3 pos, float energy, Color color)
    {
        OmniLight3D l = null;
        while (LightPool.Count > 0 && l == null) { l = LightPool.Dequeue(); if (!GodotObject.IsInstanceValid(l)) l = null; }
        if (l == null) { l = new OmniLight3D { OmniRange = 7f, ShadowEnabled = false }; Root.AddChild(l); }
        l.GlobalPosition = pos;
        l.LightColor = color;
        l.LightEnergy = energy;
        l.Visible = true;
        var tw = l.CreateTween();
        tw.TweenProperty(l, "light_energy", 0f, 0.18f);
        tw.TweenCallback(Callable.From(() => { l.Visible = false; LightPool.Enqueue(l); }));
    }
}
