using Godot;

namespace Kabarma.Game;

/// <summary>
/// Guc ve sinyal parcalarinin gorunusu, koddan (MeshKit; kredi yok, yer tutucu: pipeline/licenses.md "guc parcalari").
/// SANAT-SES 8: makineler oyuncak gibi okunur, iri dugme, donen parca; soketler fiziksel fis (beyaz seramik kulak).
/// Donen parca "rotor", yanan parca "bulb", basilan parca "knob" adli alt dugumdur (PowerDevice canlandirir).
/// </summary>
public static class PowerVisuals
{
    static readonly Color Metal = Palette.DarkGrey.Lerp(Palette.OffWhite, 0.18f);
    static readonly Color Ceramic = Palette.OffWhite;
    static readonly Color WoodDark = Palette.Wood.Lerp(Palette.Shadow, 0.3f);

    public static bool Has(string id) => id is "direk" or "salter" or "ruzgar_gulu" or "hayvan_carki" or "aku_mk1" or "elektrik_lambasi" or "dugme" or "basinc_plakasi";

    /// <summary>Kablonun taktigi soket noktasi (yerel).</summary>
    public static Vector3 Socket(string id) => id switch
    {
        "direk" => new Vector3(0, 2.45f, 0),
        "salter" => new Vector3(0, 1.25f, 0),
        "ruzgar_gulu" => new Vector3(0.28f, 0.75f, 0),
        "hayvan_carki" => new Vector3(0.9f, 0.85f, 0),
        "aku_mk1" => new Vector3(0, 0.95f, 0),
        "elektrik_lambasi" => new Vector3(0, 1.2f, 0.08f),
        "dugme" => new Vector3(0, 1.0f, 0),
        "basinc_plakasi" => new Vector3(0.45f, 0.12f, 0.45f),
        _ => new Vector3(0, 1.3f, 0), // guc isteyen istasyon
    };

    public static Node3D Make(string id)
    {
        var root = new Node3D { Name = id };
        var kit = new MeshKit();
        switch (id)
        {
            case "direk":
                kit.Cylinder(0.09f, 0.12f, 2.5f, MeshKit.At(0, 1.25f, 0), Palette.Wood, 8);
                kit.Pillow(new Vector3(1.0f, 0.1f, 0.12f), MeshKit.At(0, 2.25f, 0), WoodDark, 0.2f);
                foreach (var x in new[] { -0.42f, -0.14f, 0.14f, 0.42f })
                {
                    kit.Cylinder(0.04f, 0.055f, 0.14f, MeshKit.At(x, 2.37f, 0), Ceramic, 8);
                    kit.Cylinder(0.065f, 0.065f, 0.025f, MeshKit.At(x, 2.33f, 0), Ceramic, 8);
                }
                kit.Cylinder(0.16f, 0.16f, 0.08f, MeshKit.At(0, 0.04f, 0), Palette.Rock, 8);
                break;
            case "salter":
                kit.Cylinder(0.06f, 0.07f, 1.2f, MeshKit.At(0, 0.6f, -0.05f), Palette.Wood, 7);
                kit.Pillow(new Vector3(0.36f, 0.42f, 0.18f), MeshKit.At(0, 0.95f, 0.05f), Metal, 0.25f);
                kit.Cylinder(0.05f, 0.05f, 0.08f, MeshKit.At(0, 1.2f, 0.05f), Ceramic, 8);
                kit.Pillow(new Vector3(0.2f, 0.06f, 0.04f), MeshKit.At(0, 1.06f, 0.15f), Palette.Warning, 0.3f);
                break;
            case "ruzgar_gulu":
                kit.Cylinder(0.08f, 0.2f, 3.4f, MeshKit.At(0, 1.7f, 0), Palette.Wood, 8);
                foreach (var a in new[] { 0f, 2.09f, 4.19f })
                    kit.Cylinder(0.035f, 0.04f, 1.3f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.28f, 0.6f, Mathf.Sin(a) * 0.28f), new Vector3(Mathf.Cos(a) * 0.4f, 0, -Mathf.Sin(a) * 0.4f)), WoodDark, 6);
                kit.Pillow(new Vector3(0.34f, 0.3f, 0.7f), MeshKit.At(0, 3.45f, 0.05f), Metal, 0.4f);
                kit.Pillow(new Vector3(0.04f, 0.42f, 0.5f), MeshKit.At(0, 3.5f, -0.55f), Palette.Accent, 0.2f); // kuyruk
                kit.Pillow(new Vector3(0.3f, 0.26f, 0.2f), MeshKit.At(0.18f, 0.75f, 0), Metal, 0.3f); // soket kutusu
                kit.Cylinder(0.05f, 0.05f, 0.08f, MeshKit.At(0.28f, 0.75f, 0), Ceramic, 8);
                {
                    var rotor = new MeshKit();
                    rotor.Sphere(0.12f, MeshKit.At(0, 0, 0), Palette.OffWhite);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.Tau / 3f;
                        var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        rotor.Pillow(new Vector3(0.16f, 0.95f, 0.035f), MeshKit.At(dir * 0.55f, new Vector3(0, 0.25f, a - Mathf.Pi / 2f)), Palette.OffWhite, 0.3f);
                        rotor.Pillow(new Vector3(0.15f, 0.2f, 0.04f), MeshKit.At(dir * 0.95f, new Vector3(0, 0.25f, a - Mathf.Pi / 2f)), Palette.Accent, 0.3f);
                    }
                    var r = rotor.Instance();
                    r.Name = "rotor";
                    r.Position = new Vector3(0, 3.45f, 0.45f);
                    root.AddChild(r);
                }
                break;
            case "hayvan_carki":
                kit.Pillow(new Vector3(0.18f, 1.5f, 0.18f), MeshKit.At(0, 0.75f, -0.55f), Palette.Wood, 0.2f);
                kit.Pillow(new Vector3(0.18f, 1.5f, 0.18f), MeshKit.At(0, 0.75f, 0.55f), Palette.Wood, 0.2f);
                kit.Pillow(new Vector3(1.4f, 0.12f, 1.4f), MeshKit.At(0, 0.06f, 0), WoodDark, 0.15f);
                kit.Pillow(new Vector3(0.36f, 0.4f, 0.36f), MeshKit.At(0.8f, 0.6f, 0), Metal, 0.3f); // dinamo
                kit.Cylinder(0.05f, 0.05f, 0.08f, MeshKit.At(0.9f, 0.84f, 0), Ceramic, 8);
                {
                    var wheel = new MeshKit();
                    const int n = 14;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * Mathf.Tau / n;
                        var p = new Vector3(0, Mathf.Sin(a) * 0.62f, Mathf.Cos(a) * 0.62f);
                        wheel.Pillow(new Vector3(0.9f, 0.08f, 0.3f), MeshKit.At(p, new Vector3(-a, 0, 0)), i % 2 == 0 ? Palette.Wood : Palette.Ground, 0.2f);
                    }
                    wheel.Cylinder(0.06f, 0.06f, 1.2f, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2f)), Metal, 8);
                    var w = wheel.Instance();
                    w.Name = "rotor";
                    w.Position = new Vector3(0, 0.8f, 0);
                    w.RotationDegrees = new Vector3(0, 90, 0);
                    root.AddChild(w);
                }
                break;
            case "aku_mk1":
                kit.Pillow(new Vector3(0.9f, 0.8f, 0.6f), MeshKit.At(0, 0.42f, 0), Palette.DarkGrey, 0.25f);
                kit.Pillow(new Vector3(0.5f, 0.16f, 0.04f), MeshKit.At(0, 0.55f, 0.31f), Palette.CoopBlue, 0.3f);
                kit.Cylinder(0.06f, 0.06f, 0.1f, MeshKit.At(-0.25f, 0.88f, 0), Palette.HoldingRed, 8);
                kit.Cylinder(0.06f, 0.06f, 0.1f, MeshKit.At(0.25f, 0.88f, 0), Ceramic, 8);
                break;
            case "elektrik_lambasi":
                kit.Cylinder(0.05f, 0.07f, 1.9f, MeshKit.At(0, 0.95f, 0), Metal, 8);
                kit.Cylinder(0.18f, 0.2f, 0.06f, MeshKit.At(0, 0.03f, 0), Palette.Rock, 10);
                kit.Cylinder(0.28f, 0.08f, 0.16f, MeshKit.At(0, 2.02f, 0), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.3f), 12); // sapka
                kit.Cylinder(0.04f, 0.04f, 0.07f, MeshKit.At(0, 1.2f, 0.08f), Ceramic, 8);
                {
                    var b = new MeshKit();
                    b.Sphere(0.14f, MeshKit.At(0, 0, 0), Palette.OffWhite);
                    var bulb = b.Instance(new StandardMaterial3D { AlbedoColor = new Color("FFF2C8"), EmissionEnabled = true, Emission = new Color("FFD27A"), EmissionEnergyMultiplier = 0f });
                    bulb.Name = "bulb";
                    bulb.Position = new Vector3(0, 1.88f, 0);
                    root.AddChild(bulb);
                }
                break;
            case "dugme":
                kit.Cylinder(0.05f, 0.06f, 0.9f, MeshKit.At(0, 0.45f, 0), Palette.Wood, 7);
                kit.Pillow(new Vector3(0.3f, 0.3f, 0.2f), MeshKit.At(0, 0.98f, 0), Metal, 0.3f);
                {
                    var k = new MeshKit();
                    k.Cylinder(0.08f, 0.09f, 0.08f, MeshKit.At(Vector3.Zero, new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.HoldingRed, 12);
                    var knob = k.Instance();
                    knob.Name = "knob";
                    knob.Position = new Vector3(0, 0.98f, 0.12f);
                    root.AddChild(knob);
                }
                break;
            case "basinc_plakasi":
                kit.Pillow(new Vector3(1.1f, 0.08f, 1.1f), MeshKit.At(0, 0.04f, 0), Palette.Rock, 0.2f);
                {
                    var k = new MeshKit();
                    k.Pillow(new Vector3(0.9f, 0.06f, 0.9f), MeshKit.At(0, 0, 0), Metal.Lerp(Palette.Warning, 0.25f), 0.25f);
                    var knob = k.Instance();
                    knob.Name = "knob";
                    knob.Position = new Vector3(0, 0.1f, 0);
                    root.AddChild(knob);
                }
                kit.Cylinder(0.04f, 0.04f, 0.06f, MeshKit.At(0.45f, 0.1f, 0.45f), Ceramic, 8);
                break;
            default:
                kit.Pillow(new Vector3(0.6f, 0.6f, 0.6f), MeshKit.At(0, 0.3f, 0), Metal, 0.3f);
                break;
        }
        var body = kit.Instance();
        body.Name = "body";
        root.AddChild(body);
        return root;
    }

    /// <summary>
    /// Iki soket arasinda gergin kablo (SANAT-SES 8: "kablo elle takilir ve gergin durur"): hafif sarkan boru. Guc kablosu
    /// kalin ve koyu, sinyal teli ince ve mavi.
    /// </summary>
    public static ArrayMesh Cable(Vector3 a, Vector3 b, bool signal, int segments = 14, int sides = 6)
    {
        float r = signal ? 0.014f : 0.028f;
        float len = a.DistanceTo(b);
        float sag = Mathf.Min(0.6f, len * 0.035f);
        var pts = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            pts[i] = a.Lerp(b, t) + Vector3.Down * (4f * sag * t * (1f - t));
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            var d = (pts[i + 1] - pts[i]).Normalized();
            var side = d.Cross(Mathf.Abs(d.Y) > 0.95f ? Vector3.Right : Vector3.Up).Normalized();
            var up = side.Cross(d).Normalized();
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.Tau / sides, a1 = (k + 1) * Mathf.Tau / sides;
                var o0 = side * Mathf.Cos(a0) * r + up * Mathf.Sin(a0) * r;
                var o1 = side * Mathf.Cos(a1) * r + up * Mathf.Sin(a1) * r;
                var p0 = pts[i] + o0; var p1 = pts[i] + o1; var p2 = pts[i + 1] + o1; var p3 = pts[i + 1] + o0;
                st.SetNormal(o0.Normalized()); st.AddVertex(p0);
                st.SetNormal(o1.Normalized()); st.AddVertex(p1);
                st.SetNormal(o1.Normalized()); st.AddVertex(p2);
                st.SetNormal(o0.Normalized()); st.AddVertex(p0);
                st.SetNormal(o1.Normalized()); st.AddVertex(p2);
                st.SetNormal(o0.Normalized()); st.AddVertex(p3);
            }
        }
        return st.Commit();
    }
}
