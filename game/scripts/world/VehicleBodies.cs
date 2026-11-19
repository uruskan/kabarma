using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Arac govdeleri (ICERIK §14; M4 arac akisi): koddan yer tutucu (MeshKit), Tripo modelleri gelince Assets.Load ile degisir
/// (vehicles.json placeholder). Her govde "Visual" altinda; tekerlekler "Wheels" kimlikli dugumler (donerler).
/// </summary>
public static class VehicleBodies
{
    /// <summary>Aks araligi (m): yokus ve devrilme hesabi.</summary>
    public static float Wheelbase(VehicleDef d) => d.Id switch
    {
        "traktor" => 1.9f, "kamyon" => 3.0f, "bicerdover" => 2.6f, "hava_yastikli" => 2.4f, "zeplin" => 4.0f, _ => 1.3f,
    };

    /// <summary>Oyuncunun oturdugu yer (yerel): surucu koltugu.</summary>
    public static Vector3 SeatOffset(VehicleDef d) => d.Id switch
    {
        "traktor" => new Vector3(0, 1.55f, -0.1f), "kamyon" => new Vector3(-0.35f, 1.7f, 0.9f), "bicerdover" => new Vector3(0, 2.3f, 0.5f),
        "hava_yastikli" => new Vector3(-0.35f, 1.35f, 0.3f), "zeplin" => new Vector3(0, -2.2f, 0.8f), _ => new Vector3(0, 0.95f, -0.1f),
    };

    /// <summary>Yolcu koltuklari (yerel); 0. surucudur.</summary>
    public static Vector3 SeatFor(VehicleDef d, int index)
    {
        var s = SeatOffset(d);
        if (index == 0) return s;
        int row = (index + 1) / 2;
        return s + new Vector3(index % 2 == 1 ? 0.7f : -0.7f, 0, -0.9f * row);
    }

    public static Node3D Build(VehicleDef d, Node3D[] wheelsOut)
    {
        var root = new Node3D { Name = "Visual" };
        var kit = new MeshKit();
        int wi = 0;
        void Wheel(float x, float z, float r, float w)
        {
            if (wi >= wheelsOut.Length) return;
            var n = new Node3D { Name = "Wheel" + wi, Position = new Vector3(x, r, z) };
            var wk = new MeshKit();
            wk.Cylinder(r, r, w, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 14);
            wk.Cylinder(r * 0.45f, r * 0.45f, w * 1.05f, MeshKit.At(Vector3.Zero, new Vector3(0, 0, Mathf.Pi / 2f)), Palette.OffWhite, 10);
            n.AddChild(wk.Instance());
            root.AddChild(n);
            wheelsOut[wi++] = n;
        }
        var olive = Palette.Plant.Lerp(Palette.Shadow, 0.35f);
        switch (d.Id)
        {
            case "traktor":
            {
                var red = new Color(0.75f, 0.22f, 0.15f);
                kit.Pillow(new Vector3(1.2f, 0.7f, 2.0f), MeshKit.At(0, 0.95f, 0.45f), red, 0.4f);                 // motor
                kit.Pillow(new Vector3(1.3f, 0.12f, 1.3f), MeshKit.At(0, 1.3f, -0.55f), Palette.DarkGrey, 0.3f);   // kabin tabani
                kit.Pillow(new Vector3(1.1f, 1.0f, 1.0f), MeshKit.At(0, 1.75f, -0.55f), red.Lightened(0.15f), 0.3f); // kabin
                kit.Cylinder(0.07f, 0.07f, 0.7f, MeshKit.At(0.35f, 1.55f, 1.0f), Palette.DarkGrey, 8);               // egzoz
                kit.Pillow(new Vector3(0.5f, 0.12f, 0.3f), MeshKit.At(0, 0.8f, -1.4f), Palette.Warning, 0.2f);       // cekme demiri
                Wheel(-0.75f, 1.1f, 0.5f, 0.35f); Wheel(0.75f, 1.1f, 0.5f, 0.35f);
                Wheel(-0.85f, -0.8f, 0.85f, 0.5f); Wheel(0.85f, -0.8f, 0.85f, 0.5f);
                break;
            }
            case "kamyon":
            {
                var blue = new Color(0.2f, 0.35f, 0.6f);
                kit.Pillow(new Vector3(1.6f, 1.2f, 1.2f), MeshKit.At(0, 1.35f, 1.1f), blue, 0.35f);                 // kabin
                kit.Pillow(new Vector3(1.7f, 0.25f, 3.0f), MeshKit.At(0, 0.85f, -0.4f), Palette.DarkGrey, 0.25f);   // sasi
                kit.Pillow(new Vector3(1.7f, 0.8f, 2.2f), MeshKit.At(0, 1.35f, -0.9f), Palette.Wood, 0.2f);         // kasa yatagi (6 kasa)
                Wheel(-0.85f, 1.2f, 0.5f, 0.35f); Wheel(0.85f, 1.2f, 0.5f, 0.35f);
                Wheel(-0.85f, -1.2f, 0.5f, 0.35f); Wheel(0.85f, -1.2f, 0.5f, 0.35f);
                break;
            }
            case "bicerdover":
            {
                var yellow = new Color(0.85f, 0.7f, 0.15f);
                kit.Pillow(new Vector3(2.0f, 1.6f, 3.0f), MeshKit.At(0, 1.5f, -0.2f), yellow, 0.35f);               // govde
                kit.Pillow(new Vector3(1.1f, 0.9f, 1.0f), MeshKit.At(0, 2.7f, 0.5f), Palette.OffWhite, 0.3f);        // kabin
                kit.Pillow(new Vector3(3.6f, 0.5f, 0.8f), MeshKit.At(0, 0.8f, 1.9f), Palette.Warning, 0.25f);       // bicme tablasi
                kit.Cylinder(0.3f, 0.3f, 3.5f, MeshKit.At(new Vector3(0, 1.2f, 2.0f), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.Accent, 10); // makara
                Wheel(-1.0f, 0.9f, 0.7f, 0.5f); Wheel(1.0f, 0.9f, 0.7f, 0.5f);
                Wheel(-0.9f, -1.4f, 0.45f, 0.35f); Wheel(0.9f, -1.4f, 0.45f, 0.35f);
                break;
            }
            case "hava_yastikli":
            {
                var white = Palette.OffWhite;
                kit.Pillow(new Vector3(2.0f, 0.5f, 3.0f), MeshKit.At(0, 0.65f, 0), white, 0.6f);                    // govde
                kit.Pillow(new Vector3(2.4f, 0.3f, 3.4f), MeshKit.At(0, 0.25f, 0), Palette.DarkGrey, 0.7f);         // yastik (etek)
                kit.Pillow(new Vector3(1.4f, 0.55f, 1.4f), MeshKit.At(0, 1.1f, 0.5f), Palette.Accent.Lerp(white, 0.3f), 0.4f); // kabin
                kit.Cylinder(0.55f, 0.55f, 0.1f, MeshKit.At(0, 1.3f, -1.2f), Palette.DarkGrey, 14);                  // pervane muhafazasi
                break;
            }
            case "zeplin":
            {
                kit.Pillow(new Vector3(3.2f, 3.0f, 10f), MeshKit.At(0, 3.6f, 0), Palette.OffWhite.Lerp(Palette.Plant, 0.25f), 1.0f, 14, 8); // zarf
                kit.Pillow(new Vector3(1.6f, 0.9f, 3.6f), MeshKit.At(0, 1.2f, 0.4f), Palette.Wood, 0.3f);           // kabin
                kit.Pillow(new Vector3(0.12f, 1.8f, 1.8f), MeshKit.At(0, 3.6f, -4.8f), Palette.Warning, 0.2f);       // dikey kuyruk
                kit.Pillow(new Vector3(3.2f, 0.12f, 1.4f), MeshKit.At(0, 3.6f, -4.8f), Palette.Warning, 0.2f);       // yatay kuyruk
                kit.Cylinder(0.5f, 0.5f, 0.08f, MeshKit.At(new Vector3(1.4f, 1.2f, -1.4f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.DarkGrey, 10); // pervane
                kit.Cylinder(0.5f, 0.5f, 0.08f, MeshKit.At(new Vector3(-1.4f, 1.2f, -1.4f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.DarkGrey, 10);
                break;
            }
            default: // cekirge
            {
                kit.Pillow(new Vector3(0.95f, 0.32f, 1.7f), MeshKit.At(0, 0.62f, 0), olive, 0.4f);
                kit.Pillow(new Vector3(1.1f, 0.12f, 0.5f), MeshKit.At(0, 0.8f, 0.72f), olive.Lerp(Palette.Warning, 0.3f), 0.3f);
                kit.Pillow(new Vector3(1.1f, 0.12f, 0.5f), MeshKit.At(0, 0.8f, -0.72f), olive.Lerp(Palette.Warning, 0.3f), 0.3f);
                kit.Pillow(new Vector3(0.5f, 0.16f, 0.95f), MeshKit.At(0, 0.88f, -0.2f), Palette.DarkGrey, 0.5f);
                kit.Cylinder(0.04f, 0.05f, 0.5f, MeshKit.At(new Vector3(0, 1.0f, 0.55f), new Vector3(-0.4f, 0, 0)), Palette.DarkGrey, 8);
                kit.Cylinder(0.03f, 0.03f, 0.8f, MeshKit.At(new Vector3(0, 1.22f, 0.48f), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 8);
                kit.Sphere(0.09f, MeshKit.At(0, 0.78f, 0.9f), Palette.Warning, 10, 6);
                kit.Pillow(new Vector3(0.6f, 0.25f, 0.4f), MeshKit.At(0, 0.98f, -0.95f), Palette.Wood, 0.3f);
                Wheel(-0.55f, 0.65f, 0.32f, 0.26f); Wheel(0.55f, 0.65f, 0.32f, 0.26f);
                Wheel(-0.55f, -0.65f, 0.32f, 0.26f); Wheel(0.55f, -0.65f, 0.32f, 0.26f);
                break;
            }
        }
        root.AddChild(kit.Instance());
        return root;
    }

    /// <summary>Eklenti gorunusu (yerel, aracin on yonu +Z): yer tutucu.</summary>
    public static Node3D Attachment(string id, VehicleDef d)
    {
        var n = new Node3D { Name = "Att_" + id };
        var kit = new MeshKit();
        float rear = -VehicleBodies.Wheelbase(d) * 0.5f - 0.8f, front = Wheelbase(d) * 0.5f + 0.6f, top = id == "zeplin" ? 0.8f : 1.5f;
        switch (id)
        {
            case "romork":
                kit.Pillow(new Vector3(1.6f, 0.6f, 2.2f), MeshKit.At(0, 0.8f, rear - 1.0f), Palette.Wood, 0.2f);
                kit.Cylinder(0.35f, 0.35f, 0.2f, MeshKit.At(new Vector3(-0.85f, 0.35f, rear - 1.0f), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 10);
                kit.Cylinder(0.35f, 0.35f, 0.2f, MeshKit.At(new Vector3(0.85f, 0.35f, rear - 1.0f), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.DarkGrey, 10);
                break;
            case "pulluk":
                for (int i = -1; i <= 1; i++) kit.Cylinder(0.01f, 0.12f, 0.5f, MeshKit.At(new Vector3(i * 0.9f, 0.3f, rear - 0.5f), new Vector3(Mathf.Pi, 0, 0)), Palette.Warning, 6);
                kit.Pillow(new Vector3(2.8f, 0.12f, 0.2f), MeshKit.At(0, 0.6f, rear - 0.5f), Palette.DarkGrey, 0.2f);
                break;
            case "ekim_makinesi":
                kit.Pillow(new Vector3(2.8f, 0.5f, 0.6f), MeshKit.At(0, 0.9f, rear - 0.6f), Palette.Plant, 0.3f);
                for (int i = -1; i <= 1; i++) kit.Cylinder(0.04f, 0.04f, 0.5f, MeshKit.At(i * 0.9f, 0.4f, rear - 0.6f), Palette.DarkGrey, 6);
                break;
            case "su_tankeri":
                kit.Cylinder(0.6f, 0.6f, 1.8f, MeshKit.At(new Vector3(0, 1.0f, rear - 1.0f), new Vector3(Mathf.Pi / 2f, 0, 0)), new Color(0.35f, 0.6f, 0.8f), 14);
                break;
            case "vinc":
                kit.Cylinder(0.08f, 0.1f, 1.6f, MeshKit.At(0, top + 0.8f, 0), Palette.DarkGrey, 8);
                kit.Pillow(new Vector3(0.1f, 0.1f, 2.4f), MeshKit.At(0, top + 1.6f, 1.0f), Palette.Warning, 0.2f);
                break;
            case "kepce":
                kit.Pillow(new Vector3(1.8f, 0.5f, 0.9f), MeshKit.At(0, 0.45f, front + 0.4f), Palette.Warning, 0.25f);
                break;
            case "zirh_tampon":
                kit.Pillow(new Vector3(2.0f, 0.4f, 0.3f), MeshKit.At(0, 0.55f, front + 0.2f), Palette.DarkGrey, 0.15f);
                break;
            case "arac_kulesi":
                kit.Cylinder(0.3f, 0.35f, 0.5f, MeshKit.At(0, top + 0.3f, 0), Palette.DarkGrey, 10);
                kit.Cylinder(0.05f, 0.05f, 1.0f, MeshKit.At(new Vector3(0, top + 0.6f, 0.5f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.Warning, 6);
                break;
            case "arac_projektoru":
                kit.Sphere(0.18f, MeshKit.At(0, top, front - 0.2f), Palette.Warning, 10, 6);
                break;
            case "balon":
                kit.Sphere(1.3f, MeshKit.At(0, top + 2.2f, -0.3f), Palette.Accent.Lerp(Palette.OffWhite, 0.4f), 14, 8);
                break;
        }
        n.AddChild(kit.Instance());
        return n;
    }
}
