using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Vizor bir ekrandir (SANAT-SES §3): gozler ve agiz iki boyutlu, kodla cizilir (URETIM §2.2 G). Kaskin on yuzeyine
/// oturan egri bir yama (ArrayMesh) bas kemigine bagli durur; yuz golgelendiricide cizilir (goz bicimi, kapak, agiz
/// egrisi ve acikligi, titreme, uyku harfleri). Ifadeler ve olay siralari game/data/visor.json'da.
/// Oncelik: sabit (sinama) > bayilma > uyku > olay sirasi > yorgun (dayaniklilik) > notr. Arada goz kirpar,
/// gozbebekleri bakis yonune kayar.
/// </summary>
public partial class Visor : Node3D
{
    VisorFile _def = new();
    ShaderMaterial _mat;
    readonly Queue<VisorStepDef> _queue = new();
    VisorStepDef _step;
    float _stepT, _blinkT = 3f, _blinkLeft;
    string _hold, _target = "notr", _shown = "";
    bool _down, _wasSleeping;
    // yumusatilan degerler
    float _scale = 1f, _open = 1f, _curve, _mouthOpen, _width = 0.45f, _shake;
    Vector2 _look;

    public string Expression => _target;

    static readonly Dictionary<string, int> Shapes = new() { ["round"] = 0, ["happy"] = 1, ["wide"] = 2, ["closed"] = 3, ["x"] = 4 };

    /// <summary>Kaskin onune egri ekran yamasi kurar ve bas kemigine baglar. Bas kemigi ya da mesh yoksa null.</summary>
    public static Visor Attach(Skeleton3D skel, VisorFile def)
    {
        int head = skel.FindBone("Head");
        var mesh = skel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()
                   ?? skel.GetParent()?.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
        if (head < 0 || mesh == null) return null;
        var patch = BuildPatch(skel, mesh, head);
        if (patch == null) return null;
        var att = new BoneAttachment3D { Name = "VisorMount", BoneName = "Head" };
        skel.AddChild(att);
        var v = new Visor { Name = "Visor", _def = def };
        att.AddChild(v);
        v._mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        var col = Color.FromHtml(def.Color);
        v._mat.SetShaderParameter("face_color", col);
        v._mat.SetShaderParameter("emission", def.Emission);
        v.AddChild(new MeshInstance3D { Mesh = patch, MaterialOverride = v._mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        v.Apply(def.Expressions.GetValueOrDefault("notr") ?? new VisorExprDef(), 1f);
        return v;
    }

    /// <summary>
    /// Kaskin on yuzeyini kafa bolgesindeki koselerden olcer: merkezden her yama yonunde en distaki koseye 6 mm ekler.
    /// Yama bas kemiginin yerel uzayinda kurulur (BoneAttachment3D onu son pozla tasir).
    /// </summary>
    static ArrayMesh BuildPatch(Skeleton3D skel, MeshInstance3D mi, int head)
    {
        var headRest = skel.GetBoneGlobalRest(head);
        var toSkel = skel.GlobalTransform.AffineInverse() * mi.GlobalTransform;
        var verts = new List<Vector3>();
        for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
        {
            var arr = mi.Mesh.SurfaceGetArrays(s);
            foreach (var v in (Vector3[])arr[(int)Mesh.ArrayType.Vertex])
            {
                var p = toSkel * v;
                if (p.Y > headRest.Origin.Y + 0.04f) verts.Add(p);
            }
        }
        if (verts.Count < 50) return null;
        var lo = verts[0]; var hi = verts[0];
        foreach (var p in verts) { lo = lo.Min(p); hi = hi.Max(p); }
        var c = (lo + hi) * 0.5f;
        const int W = 18, H = 12;
        const float yaw0 = 42f, pitchLo = -26f, pitchHi = 22f;
        var dirs = new Vector3[W * H];
        var dist = new float[W * H];
        var cosCone = Mathf.Cos(Mathf.DegToRad(7f));
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float yaw = Mathf.DegToRad(Mathf.Lerp(yaw0, -yaw0, i / (W - 1f)));       // u: sol (+X) -> sag
                float pit = Mathf.DegToRad(Mathf.Lerp(pitchHi, pitchLo, j / (H - 1f)));  // v: ust -> alt
                var d = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pit), Mathf.Sin(pit), Mathf.Cos(yaw) * Mathf.Cos(pit));
                float best = 0f;
                foreach (var p in verts)
                {
                    var o = p - c; float l = o.Length();
                    if (l > 1e-4f && o.Dot(d) / l > cosCone) best = Mathf.Max(best, o.Dot(d));
                }
                dirs[j * W + i] = d; dist[j * W + i] = best;
            }
        // bosluklari ve kenar gurultusunu komsu ortalamasiyla duzelt
        var sm = (float[])dist.Clone();
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float sum = 0; int n = 0;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int x = i + di, y = j + dj;
                        if (x < 0 || y < 0 || x >= W || y >= H || dist[y * W + x] <= 0) continue;
                        sum += dist[y * W + x]; n++;
                    }
                sm[j * W + i] = n > 0 ? sum / n : 0.25f;
            }
        var inv = headRest.AffineInverse();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 P(int i, int j) => inv * (c + dirs[j * W + i] * (Mathf.Max(sm[j * W + i], dist[j * W + i]) + 0.006f));
        for (int j = 0; j < H - 1; j++)
            for (int i = 0; i < W - 1; i++)
            {
                void V(int x, int y) { st.SetUV(new Vector2(x / (W - 1f), y / (H - 1f))); st.AddVertex(P(x, y)); }
                V(i, j); V(i + 1, j); V(i + 1, j + 1);
                V(i, j); V(i + 1, j + 1); V(i, j + 1);
            }
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>Olayin ifade sirasini baslatir (visor.json "events").</summary>
    public void Event(string ev)
    {
        if (!_def.Events.TryGetValue(ev, out var steps) || steps.Count == 0) return;
        _queue.Clear();
        foreach (var s in steps) _queue.Enqueue(s);
        _step = _queue.Dequeue(); _stepT = 0f;
    }

    /// <summary>Ifadeyi sabitler (sinama ve goruntu); null birakir.</summary>
    public void Hold(string expr) => _hold = expr;

    public void SetDown(bool down) { _down = down; if (down) { _queue.Clear(); _step = null; } }

    public void Tick(float dt, Player p)
    {
        bool sleeping = p.Posed && p.CurrentClip == "sleep";
        if (_wasSleeping && !sleeping) Event("uyanma");
        _wasSleeping = sleeping;
        if (_step != null)
        {
            _stepT += dt;
            if (_stepT >= _step.Seconds) { _step = _queue.Count > 0 ? _queue.Dequeue() : null; _stepT = 0f; }
        }
        var st = p.Stamina;
        bool tired = st != null && (st.Exhausted || st.Fraction < _def.TiredBelow);
        _target = _hold ?? (_down ? "bayilma" : sleeping ? "uyku" : _step?.Expr ?? (tired ? "yorgun" : "notr"));
        if (!_def.Expressions.TryGetValue(_target, out var e)) e = _def.Expressions.GetValueOrDefault("notr") ?? new VisorExprDef();

        // goz kirpma (yalniz acik gozlerde)
        _blinkT -= dt;
        if (_blinkT <= 0f) { _blinkLeft = _def.BlinkSeconds; _blinkT = (float)GD.RandRange(_def.BlinkMin, _def.BlinkMax); }
        _blinkLeft -= dt;
        float blink = _blinkLeft > 0f && e.Eye is "round" or "wide" ? 0.05f : 1f;

        var body = p.Rig?.Body;
        var look = body == null ? Vector2.Zero : new Vector2(Mathf.Clamp(body.LookYaw / 1.2f, -1f, 1f), Mathf.Clamp(body.LookPitch / 0.8f, -1f, 1f));
        _look = _look.Lerp(look, Mathf.Min(1f, dt * 8f));
        Apply(e, Mathf.Min(1f, dt * _def.Blend), blink);
    }

    void Apply(VisorExprDef e, float k, float blink = 1f)
    {
        _scale = Mathf.Lerp(_scale, e.EyeScale, k);
        _open = Mathf.Lerp(_open, e.Open * blink, blink < 1f ? 1f : k);
        _curve = Mathf.Lerp(_curve, e.MouthCurve, k);
        _mouthOpen = Mathf.Lerp(_mouthOpen, e.MouthOpen, k);
        _width = Mathf.Lerp(_width, e.MouthWidth, k);
        _shake = Mathf.Lerp(_shake, e.Shake, k);
        if (_mat == null) return;
        if (_shown != e.Eye) { _mat.SetShaderParameter("eye_shape", Shapes.GetValueOrDefault(e.Eye, 0)); _shown = e.Eye; }
        _mat.SetShaderParameter("eye_scale", _scale);
        _mat.SetShaderParameter("eye_open", Mathf.Max(0.04f, _open));
        _mat.SetShaderParameter("mouth_curve", _curve);
        _mat.SetShaderParameter("mouth_open", _mouthOpen);
        _mat.SetShaderParameter("mouth_width", _width);
        _mat.SetShaderParameter("shake", _shake);
        _mat.SetShaderParameter("zzz", e.Zzz ? 1f : 0f);
        _mat.SetShaderParameter("look", _look);
    }

    const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 face_color : source_color = vec4(0.93, 0.9, 0.84, 1.0);
uniform float emission = 1.4;
uniform int eye_shape = 0;
uniform float eye_scale = 1.0;
uniform float eye_open = 1.0;
uniform float mouth_curve = 0.3;
uniform float mouth_open = 0.0;
uniform float mouth_width = 0.45;
uniform float shake = 0.0;
uniform float zzz = 0.0;
uniform vec2 look = vec2(0.0);
const float ASPECT = 1.55;

float sd_seg(vec2 p, vec2 a, vec2 b) { vec2 pa = p - a, ba = b - a; float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0); return length(pa - ba * h); }
float sd_ell(vec2 p, vec2 r) { float k = length(p / max(r, vec2(1e-4))); return (k - 1.0) * min(r.x, r.y); }
float fillv(float d) { float aa = fwidth(d) * 1.2 + 1e-4; return 1.0 - smoothstep(-aa, aa, d); }

float eye(vec2 q, float r) {
    float d;
    if (eye_shape == 1) {            // mutlu: ters U yay
        vec2 o = q - vec2(0.0, r * 0.35);
        d = abs(length(o) - r * 0.75) - 0.016;
        d = max(d, o.y);
    } else if (eye_shape == 2) {     // genis: halka + gozbebegi
        float ring = abs(sd_ell(q, vec2(r, r * eye_open))) - 0.014;
        float pupil = sd_ell(q - look * vec2(-1.0, -1.0) * r * 0.35, vec2(r * 0.38, r * 0.38 * eye_open));
        d = min(ring, pupil);
    } else if (eye_shape == 3) {     // kapali: asagi kivrik cizgi
        vec2 o = q + vec2(0.0, 0.18 * r * (1.0 - pow(q.x / r, 2.0)));
        d = sd_seg(o, vec2(-r * 0.85, 0.0), vec2(r * 0.85, 0.0)) - 0.014;
    } else if (eye_shape == 4) {     // carpi
        float a = 0.7 * r;
        d = min(sd_seg(q, vec2(-a, -a), vec2(a, a)), sd_seg(q, vec2(-a, a), vec2(a, -a))) - 0.015;
    } else {                         // yuvarlak
        d = sd_ell(q - look * vec2(-1.0, -1.0) * r * 0.25, vec2(r * 0.78, r * max(eye_open, 0.04)));
    }
    return d;
}

float zglyph(vec2 p, vec2 c, float s) {
    vec2 q = (p - c) / s;
    float d = min(sd_seg(q, vec2(-0.5, -0.5), vec2(0.5, -0.5)), sd_seg(q, vec2(0.5, -0.5), vec2(-0.5, 0.5)));
    d = min(d, sd_seg(q, vec2(-0.5, 0.5), vec2(0.5, 0.5)));
    return (d - 0.12) * s;
}

void fragment() {
    vec2 uv = UV + vec2(sin(TIME * 63.0), cos(TIME * 51.0)) * 0.006 * shake;
    vec2 p = (uv - vec2(0.5, 0.5)) * vec2(ASPECT, 1.0);
    float r = 0.125 * eye_scale;
    float d = min(eye(p - vec2(-0.29, -0.01), r), eye(p - vec2(0.29, -0.01), r));
    // agiz: koseler gulumsemede yukari (v asagi buyur)
    float w = mouth_width * 0.5;
    vec2 m = p - vec2(0.0, 0.25);
    float x = clamp(m.x / max(w, 1e-3), -1.0, 1.0);
    float top = mouth_curve * 0.07 * (1.0 - 2.0 * x * x) * 0.5;
    float line = sd_seg(vec2(m.x, m.y - top), vec2(-w, 0.0), vec2(w, 0.0)) - 0.016;
    float depth = mouth_open * 0.13 * (1.0 - x * x);
    float inside = max(abs(m.x) - w, max(top - m.y, m.y - (top + depth)));
    float mouth = min(line, mouth_open > 0.02 ? inside : 1.0);
    d = min(d, mouth);
    if (zzz > 0.5) {
        float t = fract(TIME * 0.4);
        for (int i = 0; i < 3; i++) {
            float ti = fract(t + float(i) / 3.0);
            vec2 zc = vec2(0.45 + ti * 0.12, -0.12 - ti * 0.3);
            d = min(d, zglyph(p, zc, 0.05 + ti * 0.04) + (ti > 0.85 ? 1.0 : 0.0));
        }
    }
    float a = fillv(d);
    ALBEDO = face_color.rgb * emission;
    ALPHA = a * face_color.a;
}
";
}
