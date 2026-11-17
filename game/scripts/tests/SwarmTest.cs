using System;
using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// M0 adim 8 (TEKNIK.md §1.2): suru cizimi. 2.000 Zipzip tek MultiMesh'te, ziplama animasyonu kose animasyon dokusundan (VAT).
/// Animasyon C#'ta bir kez pisirilir (16 kare x kose sayisi, RGBA float doku); shader VERTEX_ID ve ornek basina faz ile okur.
/// Olcum: GPU kare suresi suru acikken ve kapaliyken (ayni sahne, ayni kamera), farki suru maliyetidir.
/// Gecme olcutu: RTX 2070S'te 2 ms'nin altinda.
/// </summary>
public partial class SwarmTest : Node3D
{
    const int Count = 2000;
    const int Frames = 16;
    MultiMeshInstance3D _mmi;
    readonly List<double> _on = new(), _off = new();
    float _t;
    int _phaseFrames;
    bool _visible = true;
    Rid _vp;

    public override void _Ready()
    {
        Engine.MaxFps = 0;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var world = new World { Name = "World" };
        AddChild(world);
        world.SetNight(1f); // Kabarma gecesi: suruler gece gelir

        var (mesh, vat) = BakeZipzip();
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/swarm_vat.gdshader") };
        mat.SetShaderParameter("vat", vat);
        mat.SetShaderParameter("frames", Frames);
        mesh.SurfaceSetMaterial(0, mat);

        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = mesh, InstanceCount = Count };
        var rng = new RandomNumberGenerator { Seed = 7 };
        for (int i = 0; i < Count; i++)
        {
            float a = rng.Randf() * Mathf.Tau, r = Mathf.Sqrt(rng.Randf()) * 34f;
            var p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            p.Y = world.HeightAt(p.X, p.Z);
            float s = rng.RandfRange(0.8f, 1.4f) * (rng.Randf() < 0.15f ? 2.4f : 1f); // bazilari kabarmis
            mm.SetInstanceTransform(i, new Transform3D(Basis.FromEuler(new Vector3(0, rng.Randf() * Mathf.Tau, 0)).Scaled(Vector3.One * s), p));
            mm.SetInstanceCustomData(i, new Color(rng.Randf(), rng.RandfRange(0.8f, 1.3f), s > 2f ? 1f : 0f, 0));
        }
        _mmi = new MultiMeshInstance3D { Multimesh = mm };
        AddChild(_mmi);

        var cam = new Camera3D { Fov = 68f };
        AddChild(cam);
        cam.GlobalPosition = new Vector3(0, 9, 30);
        cam.LookAt(new Vector3(0, 0, 0));
        cam.MakeCurrent();
        _vp = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(_vp, true);
        GD.Print($"SWARM instances={Count} verts={mesh.GetFaces().Length} frames={Frames}");
    }

    /// <summary>Kodla Zipzip govdesi ve 16 karelik ziplama dongusu (ezilme, esneme, kulak sallanmasi).</summary>
    static (ArrayMesh, ImageTexture) BakeZipzip()
    {
        var kit = new MeshKit();
        var fur = Palette.Ground.Lerp(Palette.Accent, 0.35f);
        kit.Sphere(0.36f, MeshKit.At(new Vector3(0, 0.36f, 0), Vector3.Zero, new Vector3(1f, 0.92f, 1.05f)), fur, 10, 6);
        kit.Pillow(new Vector3(0.13f, 0.42f, 0.08f), MeshKit.At(new Vector3(-0.13f, 0.8f, -0.02f), new Vector3(-0.2f, 0, -0.25f)), fur, 0.5f, 8, 4);
        kit.Pillow(new Vector3(0.13f, 0.42f, 0.08f), MeshKit.At(new Vector3(0.13f, 0.8f, -0.02f), new Vector3(-0.2f, 0, 0.25f)), fur, 0.5f, 8, 4);
        kit.Sphere(0.09f, MeshKit.At(-0.13f, 0.47f, 0.28f), Palette.OffWhite, 6, 4);
        kit.Sphere(0.09f, MeshKit.At(0.13f, 0.47f, 0.28f), Palette.OffWhite, 6, 4);
        var mesh = kit.Build();
        var arr = mesh.SurfaceGetArrays(0);
        var verts = (Vector3[])arr[(int)Mesh.ArrayType.Vertex];
        int n = verts.Length;
        var img = Image.CreateEmpty(n, Frames, false, Image.Format.Rgbaf);
        for (int f = 0; f < Frames; f++)
        {
            float k = f / (float)Frames;
            float hop = Mathf.Max(0f, Mathf.Sin(k * Mathf.Tau));            // havada
            float land = Mathf.Max(0f, -Mathf.Sin(k * Mathf.Tau));          // yerde ezilme
            for (int v = 0; v < n; v++)
            {
                var p = verts[v];
                float sq = 1f - land * 0.22f + hop * 0.12f;                   // dikey olcek
                float wide = 1f + land * 0.18f - hop * 0.05f;                 // yatay
                var q = new Vector3(p.X * wide, p.Y * sq + hop * 0.55f, p.Z * wide);
                if (p.Y > 0.6f) q.Z += Mathf.Sin(k * Mathf.Tau + 1.2f) * 0.12f * (p.Y - 0.6f) * 4f; // kulaklar geriden gelir
                var off = q - p;
                img.SetPixel(v, f, new Color(off.X, off.Y, off.Z, 1f));
            }
        }
        return (mesh, ImageTexture.CreateFromImage(img));
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t < 2f) return; // isinma
        double gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_vp);
        (_visible ? _on : _off).Add(gpu);
        _phaseFrames++;
        if (_phaseFrames >= 240)
        {
            _phaseFrames = 0;
            _visible = !_visible;
            _mmi.Visible = _visible;
        }
        if (_on.Count >= 720 && _off.Count >= 720)
        {
            double on = Median(_on), off = Median(_off);
            GD.Print($"SWARM gpu_ms_on={on:0.000} gpu_ms_off={off:0.000} swarm_cost_ms={on - off:0.000} fps_on~{1000.0 / Math.Max(0.01, on):0}");
            GD.Print($"RESULT swarm {(on - off < 2.0 ? "PASS" : "FAIL")} cost_ms={on - off:0.000} (limit 2.0)");
            GetTree().Quit(on - off < 2.0 ? 0 : 1);
        }
    }

    static double Median(List<double> l) { var c = new List<double>(l); c.Sort(); return c[c.Count / 2]; }
}
