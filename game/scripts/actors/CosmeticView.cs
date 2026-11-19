using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Kozmetik parcalarin gorunumu (cosmetics.json look tarifleri; kodla cizilir, kredi yok). Bir iskelete (oyuncu ya da uzak oyuncu
/// avatari) uygulanir: kask (Head kemigine), canta susu (Backpack kemigine), tulum deseni (govde malzemesi uzerine golgelendirici,
/// mavi tulum bolgesi), Capa kaplamasi (malzeme ustu) ve vizor yuzu (Visor.SetStyle). Her Apply oncekini temizleyip yeniden kurar.
/// Kask ve canta susu modelleri yer tutucudur (basit mesh).
/// </summary>
public static class CosmeticView
{
    const string HelmetName = "CosHelmet", CharmName = "CosCharm";

    static StandardMaterial3D Mat(string hex, bool glow = false, float alpha = 1f, float metal = 0f)
    {
        var c = new Color(hex); c.A = alpha;
        var m = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.55f, Metallic = metal };
        if (alpha < 1f) m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (glow) { m.EmissionEnabled = true; m.Emission = c; m.EmissionEnergyMultiplier = 1.6f; }
        return m;
    }

    static MeshInstance3D Part(Node3D parent, Mesh mesh, Vector3 pos, string hex, Vector3? rotDeg = null, bool glow = false, float alpha = 1f, float metal = 0f)
    {
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = Mat(hex, glow, alpha, metal), Position = pos, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        if (rotDeg is Vector3 r) mi.RotationDegrees = r;
        parent.AddChild(mi);
        return mi;
    }

    static CylinderMesh Cyl(float r, float h, float top = -1f) => new() { BottomRadius = r, TopRadius = top < 0 ? r : top, Height = h, RadialSegments = 14, Rings = 1 };
    static SphereMesh Sph(float r, float sy = 1f) => new() { Radius = r, Height = r * 2f * sy, RadialSegments = 14, Rings = 8 };
    static TorusMesh Torus(float inner, float outer) => new() { InnerRadius = inner, OuterRadius = outer, Rings = 16, RingSegments = 8 };
    static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };

    static BoneAttachment3D Attach(Skeleton3D skel, string bone, string name)
    {
        skel.FindChild(name, false, false)?.Free();
        var att = new BoneAttachment3D { Name = name, BoneName = bone };
        skel.AddChild(att);
        return att;
    }

    // ---------------- ana giris ----------------

    /// <summary>equipped: slot -> kozmetik kimligi. visor ve capa bos olabilir (uzak avatarda Capa yok). body: derili govde mesh'i.</summary>
    public static void Apply(Skeleton3D skel, Dictionary<string, string> equipped, GameData data, Visor visor = null, Node3D capa = null)
    {
        if (skel == null) return;
        CosmeticDef Get(string slot) => equipped.TryGetValue(slot, out var id) && !string.IsNullOrEmpty(id) ? data.Cosmetics.Get(id) : null;
        Helmet(skel, Get("kask"));
        Charm(skel, Get("canta"));
        Suit(skel, Get("tulum"));
        visor?.SetStyle(Get("vizor")?.Look.GetValueOrDefault("style") ?? "dot");
        if (capa != null) CapaSkin(capa, Get("capa"));
    }

    // ---------------- kask ----------------

    static void Helmet(Skeleton3D skel, CosmeticDef def)
    {
        var old = skel.FindChild(HelmetName, false, false);
        old?.Free();
        string kind = def?.Look.GetValueOrDefault("kind") ?? "none";
        if (kind == "none") return;
        string c1 = def.Look.GetValueOrDefault("c1", "#EDE6D6"), c2 = def.Look.GetValueOrDefault("c2", "#3B3F4A");
        var att = Attach(skel, "Head", HelmetName);
        var n = new Node3D();
        att.AddChild(n);
        // Bas kemigi: +Y yukari, +X on. Kaskin tepesi bas kemiginin yaklasik 0,32 m yukarisinda.
        switch (kind)
        {
            case "straw": Part(n, Cyl(0.30f, 0.02f), new Vector3(0, 0.30f, 0), c1); Part(n, Cyl(0.17f, 0.12f), new Vector3(0, 0.36f, 0), c1); Part(n, Cyl(0.175f, 0.03f), new Vector3(0, 0.32f, 0), c2); break;
            case "cap": Part(n, Sph(0.22f, 0.55f), new Vector3(0, 0.30f, 0), c1); Part(n, Box(0.16f, 0.02f, 0.28f), new Vector3(0.20f, 0.29f, 0), c2); break;
            case "takke": Part(n, Sph(0.17f, 0.6f), new Vector3(0, 0.33f, 0), c1); Part(n, Sph(0.03f), new Vector3(0, 0.44f, 0), c2); break;
            case "glass": Part(n, Sph(0.27f), new Vector3(0, 0.15f, 0), c1, null, false, 0.28f); Part(n, Cyl(0.26f, 0.02f), new Vector3(0, -0.02f, 0), c2); break;
            case "diver": Part(n, Sph(0.28f), new Vector3(0, 0.15f, 0), c1, null, false, 0.35f, 0.6f); Part(n, Torus(0.17f, 0.24f), new Vector3(0, -0.02f, 0), c2, new Vector3(0, 0, 0), false, 1f, 0.6f); Part(n, Cyl(0.18f, 0.04f), new Vector3(0, -0.04f, 0), c2); break;
            case "veil": Part(n, Cyl(0.32f, 0.02f), new Vector3(0, 0.30f, 0), c1); Part(n, Cyl(0.24f, 0.46f, 0.24f), new Vector3(0, 0.09f, 0), c1, null, false, 0.3f); break;
            case "welder": Part(n, Box(0.26f, 0.15f, 0.30f), new Vector3(0.10f, 0.28f, 0), c1, new Vector3(0, 0, -20)); Part(n, Box(0.02f, 0.10f, 0.20f), new Vector3(0.24f, 0.30f, 0), c2, new Vector3(0, 0, -20), true); break;
            case "horns":
                foreach (float s in new[] { -1f, 1f })
                    Part(n, Cyl(0.05f, 0.26f, 0.005f), new Vector3(0.02f, 0.38f, 0.15f * s), c2, new Vector3(22 * s, 0, 0));
                Part(n, Cyl(0.19f, 0.03f), new Vector3(0, 0.30f, 0), c1);
                break;
            case "egg": Part(n, Sph(0.21f, 0.75f), new Vector3(0, 0.30f, 0), c1); Part(n, Cyl(0.205f, 0.025f), new Vector3(0, 0.25f, 0), c2); break;
            case "aurora":
                var ring = Part(n, new TorusMesh { InnerRadius = 0.17f, OuterRadius = 0.21f, Rings = 18, RingSegments = 8 }, new Vector3(0, 0.46f, 0), c1, null, true);
                Part(n, Sph(0.03f), new Vector3(0.19f, 0.46f, 0), c2, null, true);
                var tw = ring.CreateTween().SetLoops();
                tw.TweenProperty(ring, "rotation_degrees:y", 360f, 5f).From(0f);
                break;
            case "director": Part(n, Cyl(0.30f, 0.02f), new Vector3(0, 0.30f, 0), c1); Part(n, Cyl(0.15f, 0.24f), new Vector3(0, 0.42f, 0), c1); Part(n, Cyl(0.155f, 0.04f), new Vector3(0, 0.34f, 0), c2); break;
        }
    }

    // ---------------- canta kutusu ----------------

    /// <summary>Canta mesh'inin (Backpack_Mesh, Spine'a agirlikli) iskelet uzayindaki kutusu; yoksa null.</summary>
    public static Aabb? BagBox(Skeleton3D skel)
    {
        var mi = skel.FindChildren("Backpack_Mesh*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()
                 ?? skel.GetParent()?.FindChildren("Backpack_Mesh*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
        if (mi == null || mi.Mesh == null) return null;
        var toSkel = skel.GlobalTransform.AffineInverse() * mi.GlobalTransform;
        return toSkel * mi.GetAabb();
    }

    /// <summary>Susu konumu Backpack kemigi yerelinde: cantanin arka yuzu (-Z), alt kenar, hafif yanda.</summary>
    static Vector3? BagCharmLocal(Skeleton3D skel)
    {
        int bi = skel.FindBone("Backpack");
        if (bi < 0 || BagBox(skel) is not Aabb box) return null;
        var want = new Vector3(box.GetCenter().X + 0.1f, box.Position.Y + 0.12f, box.Position.Z - 0.03f);
        return skel.GetBoneGlobalRest(bi).AffineInverse() * want;
    }

    // ---------------- canta susu ----------------

    static void Charm(Skeleton3D skel, CosmeticDef def)
    {
        skel.FindChild(CharmName, false, false)?.Free();
        string kind = def?.Look.GetValueOrDefault("kind");
        if (string.IsNullOrEmpty(kind) || skel.FindBone("Backpack") < 0) return;
        string c = def.Look.GetValueOrDefault("c1", "#EDE6D6");
        var att = Attach(skel, "Backpack", CharmName);
        // Susu cantanin alt, sirta bakmayan (arka) yuzune asilir: konum cantanin kendi kutusundan (iskelet uzayi, +Z on) hesaplanir,
        // Backpack kemiginin dinlenme durusuna gore yerele cevrilir (kemik cerceveleri modelden modele farkli olabilir).
        var n = new Node3D { Position = BagCharmLocal(skel) ?? new Vector3(0.14f, 0.32f, 0.36f) };
        att.AddChild(n);
        switch (kind)
        {
            case "pot": Part(n, Cyl(0.07f, 0.09f), Vector3.Zero, c, null, false, 1f, 0.5f); Part(n, Cyl(0.075f, 0.02f), new Vector3(0, -0.055f, 0), "#3B3F4A"); break;
            case "pan": Part(n, Cyl(0.09f, 0.025f), Vector3.Zero, c); Part(n, Box(0.12f, 0.02f, 0.025f), new Vector3(0, 0.1f, 0), "#B07A4A", new Vector3(0, 0, 90)); break;
            case "jug": Part(n, Cyl(0.05f, 0.14f, 0.035f), Vector3.Zero, c, null, false, 0.8f); break;
            case "lantern": Part(n, Box(0.08f, 0.1f, 0.08f), Vector3.Zero, c, null, true); Part(n, Box(0.1f, 0.015f, 0.1f), new Vector3(0, -0.06f, 0), "#3B3F4A"); break;
            case "shovel": Part(n, Box(0.02f, 0.30f, 0.02f), new Vector3(0, 0.05f, 0), c); Part(n, Box(0.08f, 0.09f, 0.015f), new Vector3(0, 0.22f, 0), "#8A8F9A"); break;
            case "plush": Part(n, Sph(0.07f), Vector3.Zero, c); Part(n, Sph(0.04f), new Vector3(0, -0.09f, 0.03f), c); Part(n, Cyl(0.012f, 0.03f, 0.0f), new Vector3(0, -0.13f, 0.06f), "#F2B632", new Vector3(90, 0, 0)); break;
            case "flag": Part(n, Cyl(0.008f, 0.4f), new Vector3(0, -0.12f, 0), "#B07A4A"); Part(n, Box(0.16f, 0.1f, 0.01f), new Vector3(0.09f, -0.26f, 0), c); break;
            case "pinwheel":
                var hub = new Node3D(); n.AddChild(hub);
                for (int i = 0; i < 4; i++) Part(hub, Cyl(0.05f, 0.1f, 0f), Vector3.Zero, i % 2 == 0 ? c : "#F2B632", new Vector3(0, 0, i * 90), true).Scale = new Vector3(1f, 1f, 0.25f);
                var tw = hub.CreateTween().SetLoops(); tw.TweenProperty(hub, "rotation_degrees:z", 360f, 1.6f).From(0f);
                break;
            case "planter": Part(n, Cyl(0.06f, 0.08f, 0.045f), Vector3.Zero, c); Part(n, Sph(0.055f), new Vector3(0, -0.07f, 0), "#7FA650"); break;
            case "teapot": Part(n, Sph(0.07f, 0.85f), Vector3.Zero, c, null, false, 1f, 0.5f); Part(n, Cyl(0.01f, 0.07f, 0.02f), new Vector3(0.09f, 0.0f, 0), c, new Vector3(0, 0, 60)); Part(n, Sph(0.025f), new Vector3(0, -0.08f, 0), "#3B3F4A"); break;
            case "rosette": Part(n, Cyl(0.06f, 0.01f), Vector3.Zero, c, new Vector3(90, 0, 0), true); Part(n, Box(0.04f, 0.12f, 0.005f), new Vector3(-0.02f, 0.09f, 0), "#C8372D"); break;
            case "jar": Part(n, Cyl(0.05f, 0.1f), Vector3.Zero, c, null, true, 0.7f); Part(n, Cyl(0.052f, 0.02f), new Vector3(0, -0.06f, 0), "#3B3F4A"); break;
        }
    }

    // ---------------- tulum deseni ----------------

    const string SuitShader = @"
shader_type spatial;
uniform sampler2D albedo_tex : source_color, filter_linear_mipmap;
uniform sampler2D rm_tex : hint_default_white, filter_linear_mipmap;
uniform sampler2D normal_tex : hint_roughness_normal, filter_linear_mipmap;
uniform vec4 c1 : source_color = vec4(0.24, 0.47, 0.70, 1.0);
uniform vec4 c2 : source_color = vec4(0.95, 0.71, 0.2, 1.0);
uniform int pattern = 0;
varying vec3 lp;
void vertex() { lp = VERTEX; }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
vec3 hue(float h) { return clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0); }
void fragment() {
    vec4 a = texture(albedo_tex, UV);
    float blue = step(0.06, a.b - max(a.r, a.g) * 1.1) * step(0.22, a.b);   // mavi tulum bolgesi
    float lum = dot(a.rgb, vec3(0.30, 0.59, 0.11));
    vec3 col = c1.rgb;
    float m = 0.0;
    vec2 q = lp.xy * 14.0;
    if (pattern == 1) { m = smoothstep(0.1, 0.9, sin(lp.x * 22.0 + sin(lp.y * 17.0) * 2.0) * sin(lp.y * 19.0 + lp.z * 11.0) * 0.5 + 0.5); }   // camurlu
    else if (pattern == 2) { m = max(step(0.5, fract(lp.y * 9.0)), step(0.5, fract(lp.x * 9.0 + lp.z * 9.0))); }   // ekose
    else if (pattern == 3) { m = step(0.5, fract(lp.y * 10.0)); }                                                 // cizgili
    else if (pattern == 4) { vec2 g = floor(q); m = step(length(fract(q) - 0.5), 0.28 + hash(g) * 0.12); }       // benekli
    else if (pattern == 5) { m = 0.0; }                                                                           // altin
    else if (pattern == 6) { vec2 g = floor(lp.xy * 7.0 + lp.z * 5.0); m = step(0.5, hash(g)); }                  // kamuflaj
    else if (pattern == 7) { col = hue(fract(TIME * 0.12 + lp.y * 0.7)); m = 0.0; }                                // aurora
    col = mix(col, c2.rgb, m);
    float shade = 0.45 + lum * 1.35;
    ALBEDO = mix(a.rgb, col * shade, blue);
    vec4 rm = texture(rm_tex, UV);
    ROUGHNESS = rm.g;
    METALLIC = pattern == 5 ? mix(rm.b, 0.85, blue) : rm.b;
    NORMAL_MAP = texture(normal_tex, UV).xyz;
    if (pattern == 7) EMISSION = col * blue * 0.35;
}";

    static readonly Dictionary<string, int> Patterns = new()
    { ["solid"] = 0, ["mud"] = 1, ["plaid"] = 2, ["stripes"] = 3, ["dots"] = 4, ["gold"] = 5, ["camo"] = 6, ["aurora"] = 7 };

    static readonly Dictionary<ulong, Material> OriginalSuit = new();

    static MeshInstance3D Body(Skeleton3D skel) =>
        skel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Where(m => m.Skin != null && m.Mesh != null)
            .OrderByDescending(m => m.Mesh.GetFaces().Length).FirstOrDefault();

    static void Suit(Skeleton3D skel, CosmeticDef def)
    {
        var body = Body(skel);
        if (body == null) return;
        ulong id = body.GetInstanceId();
        if (!OriginalSuit.ContainsKey(id)) { OriginalSuit[id] = body.GetActiveMaterial(0); body.TreeExiting += () => OriginalSuit.Remove(id); }
        string pat = def?.Look.GetValueOrDefault("pattern", "solid") ?? "solid";
        bool isDefault = def == null || (def.Id == "tulum_kooperatif_mavisi");
        if (isDefault) { body.SetSurfaceOverrideMaterial(0, null); return; }
        var src = OriginalSuit[id] as BaseMaterial3D;
        var mat = new ShaderMaterial { Shader = new Shader { Code = SuitShader } };
        if (src != null)
        {
            mat.SetShaderParameter("albedo_tex", src.AlbedoTexture);
            if (src.MetallicTexture != null) mat.SetShaderParameter("rm_tex", src.MetallicTexture);
            else if (src.RoughnessTexture != null) mat.SetShaderParameter("rm_tex", src.RoughnessTexture);
            if (src.NormalTexture != null) mat.SetShaderParameter("normal_tex", src.NormalTexture);
        }
        mat.SetShaderParameter("c1", new Color(def.Look.GetValueOrDefault("c1", "#3E78B2")));
        mat.SetShaderParameter("c2", new Color(def.Look.GetValueOrDefault("c2", "#F2B632")));
        mat.SetShaderParameter("pattern", Patterns.GetValueOrDefault(pat, 0));
        body.SetSurfaceOverrideMaterial(0, mat);
    }

    // ---------------- Capa kaplamasi ----------------

    static void CapaSkin(Node3D capa, CosmeticDef def)
    {
        bool factory = def == null || def.Look.GetValueOrDefault("finish") == "factory";
        foreach (var mi in capa.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (factory) { mi.MaterialOverlay = null; continue; }
            string fin = def.Look.GetValueOrDefault("finish", "factory");
            var c = new Color(def.Look.GetValueOrDefault("c1", "#EDE6D6")); c.A = fin is "crystal" ? 0.55f : 0.62f;
            var m = new StandardMaterial3D
            {
                AlbedoColor = c, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = fin is "chrome" or "crystal" or "gold" ? 0.12f : 0.7f,
                Metallic = fin is "chrome" or "gold" or "rusty" ? 0.9f : 0f,
            };
            if (fin is "crystal" or "aurora" or "sour" or "hq") { m.EmissionEnabled = true; m.Emission = new Color(def.Look.GetValueOrDefault("c2", "#5FF0C8")); m.EmissionEnergyMultiplier = 0.6f; }
            mi.MaterialOverlay = m;
        }
    }
}
