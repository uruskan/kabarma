using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Dokulu bir modeli kabarabilir yapar: her yuzeyin StandardMaterial3D'si, ayni dokuyu kullanan
/// swell_tex shader'ina cevrilir. Sonra tek cagriyla sisme, isima, parlama ve renk verilir.
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

    public bool Valid => _mats.Count > 0;

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
}
