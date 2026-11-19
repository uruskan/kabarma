using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (URETIM §3): M4 tasima akisinin guc, sinyal ve tasima parcalarinin koddan gorunusu (ICERIK §12). Kredi
/// harcanmadi; gercek modeller gelince placeables.json "model" alani bunlarin yerini alir (pipeline/licenses.md "tasima").
/// Ortak dil: yastik kutu ve silindir. Yon: +Z onu (Sim yaw ile ayni); bant, kol ve boru parcasinda bir ok isareti.
/// </summary>
public static class PowerVisuals4
{
    static readonly Color Metal = Palette.DarkGrey.Lerp(Palette.OffWhite, 0.35f);
    static readonly Color Cer = Palette.OffWhite.Lerp(Palette.Warning, 0.15f);

    /// <summary>Kimligin yer tutucusunu kit'e yazar (ve gerekirse root'a "rotor", "knob", "bulb" ekler). Tanimsizsa false.</summary>
    public static bool Make(string id, MeshKit kit, Node3D root)
    {
        switch (id)
        {
            // ---- ureticiler ----
            case "su_carki":
                kit.Pillow(new Vector3(0.2f, 1.6f, 0.2f), MeshKit.At(-0.6f, 0.8f, 0), Palette.Wood, 0.2f);
                kit.Pillow(new Vector3(0.2f, 1.6f, 0.2f), MeshKit.At(0.6f, 0.8f, 0), Palette.Wood, 0.2f);
                kit.Pillow(new Vector3(0.4f, 0.4f, 0.4f), MeshKit.At(0.95f, 0.6f, 0), Metal, 0.3f);
                kit.Cylinder(0.05f, 0.05f, 0.08f, MeshKit.At(0.95f, 0.85f, 0), Cer, 8);
                {
                    var wheel = new MeshKit();
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.Tau / 10f;
                        wheel.Pillow(new Vector3(1.0f, 0.06f, 0.3f), MeshKit.At(new Vector3(0, Mathf.Sin(a) * 0.7f, Mathf.Cos(a) * 0.7f), new Vector3(Mathf.Pi / 2f - a, 0, 0)), Palette.CoopBlue.Lerp(Palette.Wood, 0.5f), 0.2f);
                    }
                    wheel.Cylinder(0.07f, 0.07f, 1.4f, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2f)), Metal, 8);
                    var w = wheel.Instance(); w.Name = "rotor"; w.Position = new Vector3(0, 0.85f, 0); w.RotationDegrees = new Vector3(0, 90, 0);
                    root.AddChild(w);
                }
                return true;
            case "biyoyakit_jeneratoru":
                Box(kit, 0.9f, 0.7f, 0.7f, Palette.Accent.Lerp(Palette.DarkGrey, 0.4f));
                kit.Cylinder(0.12f, 0.14f, 0.7f, MeshKit.At(0.25f, 1.05f, -0.15f), Metal, 8);
                kit.Cylinder(0.25f, 0.25f, 0.4f, MeshKit.At(-0.2f, 0.9f, 0), Palette.Warning, 10);
                Terminal(kit, 0.45f, 0.4f);
                return true;
            case "gunes_paneli":
                kit.Cylinder(0.06f, 0.06f, 0.9f, MeshKit.At(0, 0.45f, 0), Metal, 7);
                kit.Pillow(new Vector3(1.4f, 0.06f, 0.9f), MeshKit.At(new Vector3(0, 1.0f, 0), new Vector3(-0.5f, 0, 0)), Palette.CoopBlue.Lerp(Palette.NightDeep, 0.5f), 0.15f);
                Terminal(kit, 0.2f, 0.4f);
                return true;
            case "cizbiz_tanki":
                kit.Cylinder(0.55f, 0.55f, 0.9f, MeshKit.At(0, 0.45f, 0), Palette.MayaTeal.Lerp(Palette.DarkGrey, 0.6f), 14);
                kit.Pillow(new Vector3(0.9f, 0.08f, 0.9f), MeshKit.At(0, 0.95f, 0), Metal, 0.2f);
                Terminal(kit, 0.6f, 0.45f);
                return true;
            case "kabarma_toplayicisi":
                kit.Cylinder(0.7f, 0.8f, 0.3f, MeshKit.At(0, 0.15f, 0), Palette.Rock, 12);
                kit.Cylinder(0.35f, 0.5f, 1.4f, MeshKit.At(0, 1.0f, 0), Palette.MayaViolet.Lerp(Palette.DarkGrey, 0.5f), 12);
                kit.Sphere(0.3f, MeshKit.At(0, 1.9f, 0), Palette.MayaViolet);
                Terminal(kit, 0.8f, 0.3f);
                return true;
            case "maya_reaktoru":
                kit.Cylinder(1.0f, 1.1f, 0.4f, MeshKit.At(0, 0.2f, 0), Palette.Rock, 14);
                kit.Cylinder(0.6f, 0.7f, 1.8f, MeshKit.At(0, 1.3f, 0), Palette.MayaTeal.Lerp(Palette.DarkGrey, 0.5f), 14);
                kit.Sphere(0.45f, MeshKit.At(0, 2.4f, 0), Palette.MayaTeal);
                Terminal(kit, 0.9f, 0.5f);
                return true;
            case "aku_mk2": case "aku_mk3": case "aku_mk4":
                {
                    float s = id == "aku_mk2" ? 1.1f : id == "aku_mk3" ? 1.3f : 1.5f;
                    Color c = id == "aku_mk2" ? Palette.CoopBlue : id == "aku_mk3" ? Palette.Warning : Palette.MayaTeal;
                    kit.Pillow(new Vector3(0.9f * s, 0.8f * s, 0.6f * s), MeshKit.At(0, 0.4f * s, 0), Palette.DarkGrey, 0.25f);
                    kit.Pillow(new Vector3(0.5f * s, 0.16f, 0.04f), MeshKit.At(0, 0.55f * s, 0.31f * s), c, 0.3f);
                    kit.Cylinder(0.06f, 0.06f, 0.1f, MeshKit.At(-0.25f * s, 0.85f * s, 0), Palette.HoldingRed, 8);
                    kit.Cylinder(0.06f, 0.06f, 0.1f, MeshKit.At(0.25f * s, 0.85f * s, 0), Cer, 8);
                    return true;
                }
            case "sigorta_kutusu":
                kit.Cylinder(0.05f, 0.06f, 1.0f, MeshKit.At(0, 0.5f, -0.05f), Palette.Wood, 7);
                Box(kit, 0.4f, 0.5f, 0.2f, Palette.HoldingRed.Lerp(Palette.DarkGrey, 0.4f), 1.0f);
                Terminal(kit, 0.6f, 1.3f);
                return true;
            // ---- sinyal kaynaklari ----
            case "salter_kolu":
                kit.Cylinder(0.05f, 0.06f, 0.9f, MeshKit.At(0, 0.45f, 0), Palette.Wood, 7);
                kit.Pillow(new Vector3(0.3f, 0.3f, 0.12f), MeshKit.At(0, 0.98f, 0), Metal, 0.3f);
                {
                    var k = new MeshKit(); k.Cylinder(0.02f, 0.02f, 0.3f, MeshKit.At(0, 0.15f, 0), Palette.HoldingRed, 6); k.Sphere(0.05f, MeshKit.At(0, 0.32f, 0), Palette.HoldingRed);
                    var knob = k.Instance(); knob.Name = "knob"; knob.Position = new Vector3(0, 1.0f, 0.08f); root.AddChild(knob);
                }
                return true;
            case "hareket_sensoru": Sensor(kit, Palette.CoopBlue); kit.Pillow(new Vector3(0.14f, 0.14f, 0.05f), MeshKit.At(0, 1.12f, 0.11f), Palette.Warning, 0.3f); return true;
            case "gun_gece_sensoru": Sensor(kit, Palette.Warning); kit.Sphere(0.1f, MeshKit.At(0, 1.12f, 0.08f), Palette.OffWhite); return true;
            case "kabarma_sensoru": Sensor(kit, Palette.MayaViolet); kit.Sphere(0.1f, MeshKit.At(0, 1.12f, 0.08f), Palette.MayaViolet); return true;
            case "depo_sensoru": Sensor(kit, Palette.Wood); kit.Pillow(new Vector3(0.18f, 0.18f, 0.05f), MeshKit.At(0, 1.12f, 0.11f), Palette.OffWhite, 0.3f); return true;
            case "guc_sensoru": Sensor(kit, Palette.Warning); Terminal(kit, 0.3f, 0.7f); return true;
            case "nem_sensoru": Sensor(kit, Palette.MayaTeal); kit.Cylinder(0.02f, 0.02f, 0.25f, MeshKit.At(0.1f, 0.15f, 0.1f), Metal, 6); return true;
            case "sayac": Sensor(kit, Palette.OffWhite); kit.Pillow(new Vector3(0.2f, 0.12f, 0.04f), MeshKit.At(0, 1.12f, 0.11f), Palette.DarkGrey, 0.2f); return true;
            case "zamanlayici": Sensor(kit, Palette.Accent); kit.Cylinder(0.1f, 0.1f, 0.04f, MeshKit.At(new Vector3(0, 1.12f, 0.1f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.OffWhite, 12); return true;
            // ---- kapilar ----
            case "kapi_ve": Gate(kit, Palette.CoopBlue); return true;
            case "kapi_veya": Gate(kit, Palette.Accent); return true;
            case "kapi_degil": Gate(kit, Palette.HoldingRed); return true;
            case "karsilastirici": Gate(kit, Palette.Warning); return true;
            case "bellek": Gate(kit, Palette.MayaTeal); return true;
            // ---- uyari ----
            case "siren":
                kit.Cylinder(0.05f, 0.07f, 1.8f, MeshKit.At(0, 0.9f, 0), Metal, 8);
                kit.Cylinder(0.18f, 0.12f, 0.3f, MeshKit.At(0, 1.95f, 0), Palette.HoldingRed, 12);
                kit.Cylinder(0.04f, 0.04f, 0.07f, MeshKit.At(0, 1.2f, 0.08f), Cer, 8);
                {
                    var b = new MeshKit(); b.Sphere(0.14f, MeshKit.At(0, 0, 0), Palette.OffWhite);
                    var bulb = b.Instance(new StandardMaterial3D { AlbedoColor = new Color("FFC8C0"), EmissionEnabled = true, Emission = new Color("FF4030"), EmissionEnergyMultiplier = 0f });
                    bulb.Name = "bulb"; bulb.Position = new Vector3(0, 2.15f, 0); root.AddChild(bulb);
                }
                return true;
            case "sinyal_lambasi":
                kit.Cylinder(0.04f, 0.05f, 1.0f, MeshKit.At(0, 0.5f, 0), Metal, 7);
                kit.Cylinder(0.04f, 0.04f, 0.07f, MeshKit.At(0, 1.2f, 0.08f), Cer, 8);
                {
                    var b = new MeshKit(); b.Sphere(0.1f, MeshKit.At(0, 0, 0), Palette.OffWhite);
                    var bulb = b.Instance(new StandardMaterial3D { AlbedoColor = new Color("C8E8FF"), EmissionEnabled = true, Emission = new Color("5FF0C8"), EmissionEnergyMultiplier = 0f });
                    bulb.Name = "bulb"; bulb.Position = new Vector3(0, 1.1f, 0); root.AddChild(bulb);
                }
                return true;
            // ---- tasima ----
            case "bant":
                kit.Pillow(new Vector3(0.7f, 0.12f, 0.98f), MeshKit.At(0, 0.14f, 0), Palette.DarkGrey, 0.2f);
                kit.Pillow(new Vector3(0.55f, 0.03f, 0.94f), MeshKit.At(0, 0.21f, 0), Palette.Warning.Lerp(Palette.DarkGrey, 0.6f), 0.1f);
                Arrow(kit, 0.24f);
                return true;
            case "ayirici":
                kit.Pillow(new Vector3(0.9f, 0.12f, 0.9f), MeshKit.At(0, 0.14f, 0), Palette.DarkGrey, 0.2f);
                kit.Cylinder(0.3f, 0.3f, 0.06f, MeshKit.At(0, 0.23f, 0), Palette.CoopBlue, 12);
                Arrow(kit, 0.27f); Arrow(kit, 0.27f, 1); Arrow(kit, 0.27f, -1);
                return true;
            case "birlestirici":
                kit.Pillow(new Vector3(0.9f, 0.12f, 0.9f), MeshKit.At(0, 0.14f, 0), Palette.DarkGrey, 0.2f);
                kit.Cylinder(0.3f, 0.3f, 0.06f, MeshKit.At(0, 0.23f, 0), Palette.Accent, 12);
                Arrow(kit, 0.27f);
                return true;
            case "tasima_kolu":
                kit.Pillow(new Vector3(0.5f, 0.3f, 0.5f), MeshKit.At(0, 0.15f, 0), Palette.DarkGrey, 0.3f);
                kit.Pillow(new Vector3(0.14f, 0.14f, 1.1f), MeshKit.At(0, 0.55f, 0.1f), Palette.Warning, 0.3f);
                kit.Sphere(0.12f, MeshKit.At(0, 0.55f, -0.4f), Metal);
                kit.Pillow(new Vector3(0.18f, 0.1f, 0.18f), MeshKit.At(0, 0.45f, 0.65f), Metal, 0.3f); // kavrama
                Terminal(kit, 0.3f, 0.3f);
                Arrow(kit, 0.32f);
                return true;
            case "boru":
                kit.Cylinder(0.12f, 0.12f, 1.0f, MeshKit.At(new Vector3(0, 0.25f, 0), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.4f), 10);
                kit.Cylinder(0.15f, 0.15f, 0.08f, MeshKit.At(new Vector3(0, 0.25f, 0.46f), new Vector3(Mathf.Pi / 2f, 0, 0)), Metal, 10);
                kit.Cylinder(0.15f, 0.15f, 0.08f, MeshKit.At(new Vector3(0, 0.25f, -0.46f), new Vector3(Mathf.Pi / 2f, 0, 0)), Metal, 10);
                return true;
            case "pompa":
                kit.Pillow(new Vector3(0.7f, 0.5f, 0.7f), MeshKit.At(0, 0.25f, 0), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.4f), 0.3f);
                kit.Cylinder(0.25f, 0.25f, 0.5f, MeshKit.At(new Vector3(0, 0.5f, 0), new Vector3(0, 0, Mathf.Pi / 2f)), Metal, 12);
                Terminal(kit, 0.4f, 0.4f);
                Arrow(kit, 0.55f);
                return true;
            case "drone_istasyonu":
                kit.Cylinder(0.9f, 1.0f, 0.2f, MeshKit.At(0, 0.1f, 0), Palette.Rock, 14);
                kit.Cylinder(0.12f, 0.12f, 1.0f, MeshKit.At(0, 0.7f, 0), Metal, 8);
                kit.Pillow(new Vector3(0.5f, 0.3f, 0.5f), MeshKit.At(0, 1.3f, 0), Palette.CoopBlue, 0.3f);
                foreach (var a in new[] { 0f, 1.57f, 3.14f, 4.71f }) kit.Cylinder(0.12f, 0.12f, 0.03f, MeshKit.At(Mathf.Cos(a) * 0.55f, 0.24f, Mathf.Sin(a) * 0.55f), Palette.Warning, 10);
                Terminal(kit, 0.7f, 0.4f);
                return true;
            case "teleferik_istasyonu":
                kit.Pillow(new Vector3(1.8f, 0.3f, 1.6f), MeshKit.At(0, 0.15f, 0), Palette.Rock, 0.2f);
                kit.Pillow(new Vector3(0.2f, 2.6f, 0.2f), MeshKit.At(-0.7f, 1.6f, 0), Palette.Wood, 0.2f);
                kit.Pillow(new Vector3(0.2f, 2.6f, 0.2f), MeshKit.At(0.7f, 1.6f, 0), Palette.Wood, 0.2f);
                kit.Pillow(new Vector3(1.8f, 0.2f, 0.25f), MeshKit.At(0, 2.9f, 0), Palette.Wood, 0.2f);
                kit.Cylinder(0.3f, 0.3f, 0.2f, MeshKit.At(new Vector3(0, 2.9f, 0), new Vector3(0, 0, Mathf.Pi / 2f)), Metal, 12);
                Terminal(kit, 0.9f, 0.6f);
                return true;
            case "teleferik_diregi":
                kit.Cylinder(0.12f, 0.2f, 4.0f, MeshKit.At(0, 2.0f, 0), Palette.Wood, 8);
                kit.Pillow(new Vector3(1.0f, 0.12f, 0.12f), MeshKit.At(0, 3.9f, 0), Palette.Wood, 0.2f);
                kit.Cylinder(0.08f, 0.08f, 0.1f, MeshKit.At(0, 4.0f, 0), Metal, 8);
                return true;
        }
        return false;
    }

    static void Box(MeshKit k, float w, float h, float d, Color c, float y0 = 0f) => k.Pillow(new Vector3(w, h, d), MeshKit.At(0, y0 + h / 2f, 0), c, 0.3f);
    static void Terminal(MeshKit k, float x, float y) => k.Cylinder(0.05f, 0.05f, 0.08f, MeshKit.At(0, y > 1.2f ? y : 1.3f, 0), Cer, 8);
    static void Sensor(MeshKit k, Color c)
    {
        k.Cylinder(0.04f, 0.05f, 0.9f, MeshKit.At(0, 0.45f, 0), Palette.Wood, 7);
        k.Pillow(new Vector3(0.3f, 0.3f, 0.12f), MeshKit.At(0, 1.0f, 0), c.Lerp(Palette.DarkGrey, 0.35f), 0.3f);
    }
    static void Gate(MeshKit k, Color c)
    {
        k.Cylinder(0.04f, 0.05f, 0.7f, MeshKit.At(0, 0.35f, 0), Palette.Wood, 7);
        k.Pillow(new Vector3(0.4f, 0.28f, 0.14f), MeshKit.At(0, 0.85f, 0), Palette.DarkGrey, 0.3f);
        k.Pillow(new Vector3(0.28f, 0.14f, 0.04f), MeshKit.At(0, 0.85f, 0.09f), c, 0.3f);
    }
    /// <summary>Yuzeyde +Z'yi gosteren ucgen-benzeri ok (yastik iki parca); side: 0 on, 1 sol (+X), -1 sag.</summary>
    static void Arrow(MeshKit k, float y, int side = 0)
    {
        float yaw = side * Mathf.Pi / 2f;
        var c = Palette.OffWhite;
        var rot = new Vector3(0, yaw, 0);
        var dir = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        k.Pillow(new Vector3(0.1f, 0.03f, 0.3f), MeshKit.At(new Vector3(0, y, 0) + dir * 0.1f, rot), c, 0.2f);
        k.Pillow(new Vector3(0.24f, 0.03f, 0.1f), MeshKit.At(new Vector3(0, y, 0) + dir * 0.3f, rot), c, 0.2f);
    }
}
