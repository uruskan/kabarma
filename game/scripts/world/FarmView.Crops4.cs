using Godot;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (tarim4, M4): Sazlik, Kanyon, Ayaz ve Gobek'in 13 ekininin koddan modelleri (ICERIK 5 #12-#24). Kredi yok;
/// ekin basina Tripo modeli uretim istegi raporda. Her model olgun boyda, tek renk gruplu, salgam dilinde.
/// </summary>
public static partial class CropVisuals
{
    static readonly Color Reed = new("7BA05B"), Sand = new("D9B26A");

    static void Ring(MeshKit k, int n, float r, float y, float rad, Color c)
    {
        for (int i = 0; i < n; i++) { float a = i * Mathf.Tau / n; k.Sphere(rad, MeshKit.At(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)), c, 7, 4); }
    }

    static void Stalks(MeshKit k, int n, float r, float h, Color c)
    {
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n, rr = i == 0 ? 0f : r;
            k.Cylinder(0.014f, 0.024f, h, MeshKit.At(new Vector3(Mathf.Cos(a) * rr, h * 0.5f, Mathf.Sin(a) * rr), new Vector3(0.1f * Mathf.Cos(a), 0, 0.1f * Mathf.Sin(a))), c, 5);
        }
    }

    static bool MakeM4(string crop, MeshKit k)
    {
        switch (crop)
        {
            case "rice": // ince saplar + sari sarkik basaklar
                Stalks(k, 7, 0.1f, 0.6f, Reed);
                Ring(k, 6, 0.12f, 0.58f, 0.04f, new Color("E6D58A"));
                return true;
            case "reed": // uzun kalin kamisler, kahverengi puskul
                Stalks(k, 5, 0.07f, 1.1f, new Color("8FAE5A"));
                k.Capsule(0.05f, 0.2f, MeshKit.At(new Vector3(0, 1.15f, 0)), new Color("8A5A2E"), 6, 2);
                return true;
            case "glow_lily": // yuvarlak yaprak + parlak mavi cicek
                k.Cylinder(0.3f, 0.3f, 0.03f, MeshKit.At(0, 0.02f, 0), new Color("3F8A5A"), 12);
                for (int i = 0; i < 6; i++) { float a = i * Mathf.Tau / 6f; k.Capsule(0.05f, 0.18f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.09f, 0.16f, Mathf.Sin(a) * 0.09f), new Vector3(0.5f * Mathf.Cos(a), 0, 0.5f * Mathf.Sin(a))), new Color("7FE8E8"), 6, 2); }
                k.Sphere(0.06f, MeshKit.At(0, 0.14f, 0), new Color("FFF3A0"), 8, 4);
                return true;
            case "chili": // dik cali + kirmizi uzun biberler
                k.Cylinder(0.025f, 0.04f, 0.4f, MeshKit.At(0, 0.2f, 0), Leaf, 5);
                for (int i = 0; i < 5; i++) { float a = i * 1.3f; k.Capsule(0.03f, 0.16f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.12f, 0.3f - 0.03f * (i % 2), Mathf.Sin(a) * 0.12f), new Vector3(0.2f, 0, 0.2f)), new Color("D8321F"), 6, 2); }
                return true;
            case "corn": // kalin govde, yapraklar, sari koceni
                k.Cylinder(0.035f, 0.05f, 0.9f, MeshKit.At(0, 0.45f, 0), Leaf, 6);
                for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4f; k.Cylinder(0.01f, 0.03f, 0.5f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.12f, 0.6f, Mathf.Sin(a) * 0.12f), new Vector3(0.5f * Mathf.Cos(a), 0, 0.5f * Mathf.Sin(a))), LeafDark, 4); }
                k.Capsule(0.06f, 0.24f, MeshKit.At(new Vector3(0.06f, 0.62f, 0), new Vector3(0, 0, -0.25f)), new Color("F2CF3A"), 7, 3);
                return true;
            case "watermelon": // cizgili yesil kure
                k.Sphere(0.34f, MeshKit.At(new Vector3(0, 0.3f, 0), Vector3.Zero, new Vector3(1.2f, 0.95f, 1f)), new Color("3E8F4A"), 12, 8);
                for (int i = 0; i < 4; i++) { float a = i * Mathf.Tau / 4f + 0.4f; k.Sphere(0.1f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.3f, 0.3f, Mathf.Sin(a) * 0.26f), Vector3.Zero, new Vector3(0.4f, 1.6f, 0.4f)), new Color("1F5A2E"), 6, 4); }
                return true;
            case "prickly_pear": // yassi pedler + kirmizi meyve
                k.Sphere(0.2f, MeshKit.At(new Vector3(0, 0.25f, 0), Vector3.Zero, new Vector3(1.3f, 1f, 0.35f)), new Color("5E9E5A"), 8, 5);
                k.Sphere(0.16f, MeshKit.At(new Vector3(0.14f, 0.5f, 0), Vector3.Zero, new Vector3(1.1f, 1f, 0.35f)), new Color("6EAE62"), 8, 5);
                k.Sphere(0.05f, MeshKit.At(new Vector3(0.2f, 0.66f, 0.03f)), new Color("C9306A"), 6, 4);
                return true;
            case "tar_bean": // koyu bodur cali + siyah baklalar
                k.Sphere(0.2f, MeshKit.At(new Vector3(0, 0.22f, 0), Vector3.Zero, new Vector3(1.2f, 0.9f, 1.2f)), new Color("3F4A2E"), 8, 5);
                for (int i = 0; i < 5; i++) { float a = i * 1.25f; k.Capsule(0.025f, 0.14f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.2f, 0.18f, Mathf.Sin(a) * 0.2f), new Vector3(1.4f, 0, 0.2f)), new Color("1A1A1A"), 5, 2); }
                return true;
            case "frost_beet": // buz mavisi pancar + beyaz-mavi yapraklar
                k.Sphere(0.26f, MeshKit.At(new Vector3(0, 0.16f, 0), Vector3.Zero, new Vector3(1f, 0.9f, 1f)), new Color("8F4AA8"), 12, 8);
                Stalks(k, 5, 0.06f, 0.5f, new Color("9ED6E8"));
                return true;
            case "snow_tea": // kucuk beyaz-yesil cali, beyaz cicekler
                k.Sphere(0.24f, MeshKit.At(new Vector3(0, 0.22f, 0), Vector3.Zero, new Vector3(1.1f, 0.9f, 1.1f)), new Color("5E9E7A"), 8, 5);
                Ring(k, 6, 0.16f, 0.36f, 0.04f, new Color("F4F8FF"));
                return true;
            case "ice_grape": // asma + acik mavi salkim
                k.Cylinder(0.025f, 0.04f, 0.5f, MeshKit.At(0, 0.25f, 0), new Color("7A6A5A"), 5);
                k.Sphere(0.2f, MeshKit.At(new Vector3(0, 0.55f, 0), Vector3.Zero, new Vector3(1.2f, 0.7f, 1.2f)), new Color("5E9E8A"), 8, 5);
                Ring(k, 7, 0.1f, 0.32f, 0.045f, new Color("A8D8F2"));
                return true;
            case "maya_flower": // mor-pembe buyuk cicek, parlayan merkez
                k.Cylinder(0.02f, 0.03f, 0.55f, MeshKit.At(0, 0.27f, 0), Stem, 5);
                for (int i = 0; i < 8; i++) { float a = i * Mathf.Tau / 8f; k.Capsule(0.045f, 0.2f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.14f, 0.58f, Mathf.Sin(a) * 0.14f), new Vector3(0, 0, 0) + new Vector3(Mathf.Sin(a) * 1.4f, 0, -Mathf.Cos(a) * 1.4f)), new Color("C45AD8"), 6, 2); }
                k.Sphere(0.08f, MeshKit.At(0, 0.58f, 0), new Color("FFE066"), 8, 4);
                return true;
            case "leaven_mushroom": // kabarik krem-sari sapka, alt benekleri
                k.Cylinder(0.09f, 0.12f, 0.24f, MeshKit.At(0, 0.12f, 0), new Color("F2E8C8"), 8);
                k.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.34f, 0), Vector3.Zero, new Vector3(1f, 0.7f, 1f)), new Color("E8C45A"), 12, 6);
                for (int i = 0; i < 5; i++) { float a = i * 1.3f; k.Sphere(0.04f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.16f, 0.5f, Mathf.Sin(a) * 0.16f)), new Color("FFF6D8"), 6, 3); }
                return true;
        }
        return false;
    }

    static Color M4Color(string crop) => crop switch
    {
        "rice" => new Color("E6D58A"), "reed" => new Color("8A5A2E"), "glow_lily" => new Color("7FE8E8"), "chili" => new Color("D8321F"),
        "corn" => new Color("F2CF3A"), "watermelon" => new Color("3E8F4A"), "prickly_pear" => new Color("C9306A"), "tar_bean" => new Color("1A1A1A"),
        "frost_beet" => new Color("8F4AA8"), "snow_tea" => new Color("F4F8FF"), "ice_grape" => new Color("A8D8F2"), "maya_flower" => new Color("C45AD8"),
        "leaven_mushroom" => new Color("E8C45A"), _ => Palette.TurnipBody,
    };
}