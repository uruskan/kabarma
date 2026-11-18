using System.Collections.Generic;
using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Dokulu bir modeli kabarabilir yapar: her yuzeyin StandardMaterial3D'si, ayni dokuyu kullanan
/// swell_tex shader'ina cevrilir. Sonra tek cagriyla sisme, isima, parlama ve renk verilir.
/// Butun turler (Gidak, Zipzip, Pofuduk) ve kabarik ekin ayni shader'i kullanir (SANAT-SES §4).
/// Turler icin: SwellDef ile kurulursa Swell(bool) + Tick(dt) sisme sekansini (elastik gecis, isima, adim ve darbe
/// esnemesi) kendisi yurutur; Tick'in dondurdugu oran govde olcegine uygulanir.
/// </summary>
public sealed class Swellable
{
    static Shader _shader;
    readonly List<ShaderMaterial> _mats = new();
    public Node3D Root { get; }

    public Swellable(Node3D root, float veinScale = 3f)
    {
        Root = root;
        _shader ??= GD.Load<Shader>("res://shaders/swell_tex.gdshader");
        var cache = new Dictionary<Material, ShaderMaterial>();
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Append(root as MeshInstance3D).Where(m => m != null))
        {
            if (mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var src = mi.GetActiveMaterial(i);
                if (src == null) continue;
                if (!cache.TryGetValue(src, out var sm))
                {
                    sm = new ShaderMaterial { Shader = _shader };
                    if (src is BaseMaterial3D b)
                    {
                        sm.SetShaderParameter("albedo_color", b.AlbedoColor);
                        if (b.AlbedoTexture != null) { sm.SetShaderParameter("albedo_tex", b.AlbedoTexture); sm.SetShaderParameter("has_albedo_tex", true); }
                        if (b.NormalEnabled && b.NormalTexture != null) { sm.SetShaderParameter("normal_tex", b.NormalTexture); sm.SetShaderParameter("has_normal_tex", true); }
                        sm.SetShaderParameter("roughness_value", Mathf.Clamp(b.Roughness, 0.4f, 1f));
                    }
                    sm.SetShaderParameter("vein_scale", veinScale);
                    cache[src] = sm;
                    _mats.Add(sm);
                }
                mi.SetSurfaceOverrideMaterial(i, sm);
            }
        }
    }

    /// <summary>Tur ayarlariyla (creatures.json "swell"): sekans Swell/Tick ile yurur.</summary>
    public Swellable(Node3D root, SwellDef def) : this(root, def.VeinScale)
    {
        _def = def;
        Set(swell: 0f, glow: 0f, push: def.Push, wobble: def.Wobble);
    }

    /// <summary>
    /// Ornekli mesh (suru uyeleri, MultiMesh): yuzey malzemeleri mesh'in kendisinde sisme shader'ina cevrilir;
    /// bu mesh'i kullanan butun ornekler birlikte sisip isir.
    /// </summary>
    public Swellable(ArrayMesh mesh, SwellDef def)
    {
        _shader ??= GD.Load<Shader>("res://shaders/swell_tex.gdshader");
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            var sm = new ShaderMaterial { Shader = _shader };
            if (mesh.SurfaceGetMaterial(i) is BaseMaterial3D b)
            {
                sm.SetShaderParameter("albedo_color", b.AlbedoColor);
                sm.SetShaderParameter("roughness_value", Mathf.Clamp(b.Roughness, 0.4f, 1f));
            }
            sm.SetShaderParameter("vein_scale", def.VeinScale);
            mesh.SurfaceSetMaterial(i, sm);
            _mats.Add(sm);
        }
        _def = def;
        Set(swell: 0f, glow: 0f, push: def.Push, wobble: def.Wobble);
    }

    public bool Valid => _mats.Count > 0;
    public int MaterialCount => _mats.Count;
    public static bool UsesShader(Material m) => m is ShaderMaterial sm && sm.Shader != null && sm.Shader.ResourcePath == "res://shaders/swell_tex.gdshader";

    public void Set(float swell = -1, float glow = -1, float flash = -1, float push = -1, float wobble = -1)
    {
        foreach (var m in _mats)
        {
            if (swell >= 0) m.SetShaderParameter("swell", swell);
            if (glow >= 0) m.SetShaderParameter("glow", glow);
            if (flash >= 0) m.SetShaderParameter("flash", flash);
            if (push >= 0) m.SetShaderParameter("swell_push", push);
            if (wobble >= 0) m.SetShaderParameter("wobble_amp", wobble);
        }
    }

    public void Tint(Color c) { foreach (var m in _mats) m.SetShaderParameter("tint", c); }

    // ---------- Sisme sekansi (SANAT-SES §4: normal yonunde sisme, yay titremesi, damar isimasi) ----------

    readonly SwellDef _def;
    float _from, _target, _k = 1f, _amount, _jiggle;

    /// <summary>Sisme orani: 0 normal, 1 tam kabarmis (elastik gecis 1'i biraz asabilir).</summary>
    public float Amount => _amount;
    public bool SwollenTarget => _target > 0.5f;

    /// <summary>Kabar ya da son: elastik gecis baslar (Tick ilerletir).</summary>
    public void Swell(bool on)
    {
        float t = on ? 1f : 0f;
        if (Mathf.IsEqualApprox(t, _target) && _k > 0f) return;
        _from = _amount; _target = t; _k = 0f;
    }

    /// <summary>Gecissiz: dogan kabarmis dusman zaten kabarmistir.</summary>
    public void Snap(bool on) { _from = _target = _amount = on ? 1f : 0f; _k = 1f; Apply(); }

    /// <summary>Yuzey esnemesi (model birimi); en buyugu kalir, kendiliginden soner.</summary>
    public void Impulse(float amount) => _jiggle = Mathf.Max(_jiggle, amount);
    public void StepJiggle() => Impulse(_def?.StepJiggle ?? 0f);
    public void HitJiggle() => Impulse(_def?.HitJiggle ?? 0f);

    /// <summary>Sekansi ilerletir, shader'i gunceller; govde olcegi icin orani dondurur.</summary>
    public float Tick(float dt)
    {
        float secs = Mathf.Max(0.05f, _def?.Seconds ?? 1f);
        _k = Mathf.Min(1f, _k + dt / secs);
        // Kabarirken hedefe asar ve geri yaylanir (ekinle ayni egri); sonerken yumusak iner.
        float e = _target > _from ? (_k >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * _k) * Mathf.Cos(_k * 10.5f)) : 1f - (1f - _k) * (1f - _k);
        _amount = Mathf.Lerp(_from, _target, e);
        _jiggle *= Mathf.Exp(-dt * 7f);
        Apply();
        return _amount;
    }

    void Apply()
    {
        float s = Mathf.Clamp(_amount, 0f, 1f);
        float glow = _def?.Glow ?? 1f;
        foreach (var m in _mats)
        {
            m.SetShaderParameter("swell", s);
            m.SetShaderParameter("glow", s * glow);
            m.SetShaderParameter("jiggle", _jiggle * (0.35f + 0.65f * s));
        }
    }
}
