using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Ot alani (gorsel-dunya): etkin kameraya bagli tek MultiMesh halkasi (grass_field.gdshader). Ornekler duzgun bir
/// kafeste; dugum her karede kameranin altina ornek araligina oturtularak tasinir, boylece kokler dunyada sabit kalir.
/// Kok konumu, boy, renk ve "ot olur mu" shader'da dunya dokularindan hesaplanir (World.Ground.cs).
/// Yakin halka sik, uzak halka seyrek ve genis tutamli; ikisi birbirine erir.
/// </summary>
public partial class GrassField : MultiMeshInstance3D
{
    float _spacing;
    ShaderMaterial _mat;
    Node3D _trampler;

    public int Instances => Multimesh?.InstanceCount ?? 0;

    public static GrassField Create(World w, Mesh blade, float spacing, float rIn, float rOut, float keepFar, float widen, string name)
    {
        var g = w.Biome.Grass;
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/grass_field.gdshader") };
        mat.SetShaderParameter("height_tex", w.HeightTex);
        mat.SetShaderParameter("height_res", (float)(w.Cells + 1));
        mat.SetShaderParameter("control", w.ControlTex);
        mat.SetShaderParameter("macro_noise", w.MacroNoise);
        mat.SetShaderParameter("world_size", w.Size);
        mat.SetShaderParameter("spacing", spacing);
        mat.SetShaderParameter("radius_in", rIn);
        mat.SetShaderParameter("radius_out", rOut);
        mat.SetShaderParameter("fade_band", Mathf.Max(3f, rOut * 0.15f));
        mat.SetShaderParameter("keep_far", keepFar);
        mat.SetShaderParameter("widen", widen);
        mat.SetShaderParameter("blade_height", new Vector2(g.Height[0], g.Height.Count > 1 ? g.Height[1] : g.Height[0]));
        mat.SetShaderParameter("wind_strength", g.Wind);
        // Renkler biyom paletinden: taze uc "plant", kuru uc "ground" (Altin Cayir), dipler golgeli.
        var plant = w.Pal("plant", Palette.Plant); var ground = w.Pal("ground", Palette.Ground); var shadow = w.Pal("shadow", Palette.Shadow);
        mat.SetShaderParameter("lush_tip", plant.Lerp(ground, 0.12f).Darkened(0.12f));
        mat.SetShaderParameter("lush_root", plant.Lerp(shadow, 0.35f).Darkened(0.6f));
        mat.SetShaderParameter("dry_tip", ground.Lerp(new Color(0.93f, 0.86f, 0.62f), 0.2f).Darkened(0.1f));
        mat.SetShaderParameter("dry_root", ground.Lerp(shadow, 0.45f).Darkened(0.6f));

        var offs = new List<Vector3>();
        int n = Mathf.CeilToInt(rOut / spacing) + 1;
        float inner = Mathf.Max(0f, rIn - Mathf.Max(3f, rOut * 0.15f));
        for (int i = -n; i <= n; i++)
        for (int j = -n; j <= n; j++)
        {
            float d = Mathf.Sqrt(i * i + j * j) * spacing;
            if (d > rOut + spacing || (rIn > 0 && d < inner)) continue;
            offs.Add(new Vector3(i * spacing, 0, j * spacing));
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = blade, InstanceCount = offs.Count };
        for (int i = 0; i < offs.Count; i++) mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, offs[i]));
        var f = new GrassField
        {
            Name = name,
            Multimesh = mm,
            MaterialOverride = mat,
            CastShadow = ShadowCastingSetting.Off,
            // Kokler shader'da araziye iner: kutu butun yukseklik araligini kapsar.
            CustomAabb = new Aabb(new Vector3(-rOut - 2, -60, -rOut - 2), new Vector3(rOut * 2 + 4, 160, rOut * 2 + 4)),
            _spacing = spacing,
            _mat = mat,
        };
        return f;
    }

    public override void _Process(double delta)
    {
        _trampler ??= (GetParent()?.GetParent() as Director)?.Player;
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null) return;
        var p = cam.GlobalPosition;
        GlobalPosition = new Vector3(Mathf.Round(p.X / _spacing) * _spacing, 0, Mathf.Round(p.Z / _spacing) * _spacing);
        if (_trampler != null && IsInstanceValid(_trampler))
        {
            var t = _trampler.GlobalPosition;
            _mat.SetShaderParameter("trample", new Vector4(t.X, t.Y, t.Z, 0.9f));
        }
    }
}
