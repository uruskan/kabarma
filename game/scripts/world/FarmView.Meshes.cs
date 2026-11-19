using Godot;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (tarim, M2): Cayir ve Koru ekinlerinin koddan modelleri, salgamin dilinde (tiknaz yastik ve kure,
/// kose rengi, toon). Kredi yok; uretim istegi raporda (Tripo, ekin basina 1 model). pipeline/licenses.md "kod".
/// Model olgun boyda kurulur; PlotView evreye gore olcekler. Salgam altin modeli (assets/models/salgam) aynen kalir.
/// </summary>
public static partial class CropVisuals
{
    static readonly Color Leaf = new("5E9E3A"), LeafDark = new("3F7A2A"), Stem = new("7FA64A"), Straw = new("D9B451");

    public static Node3D Make(string crop, ShaderMaterial mat)
    {
        var k = new MeshKit();
        switch (crop)
        {
            case "wheat": // bes basak demeti
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.Tau / 5f, r = i == 0 ? 0f : 0.09f;
                    var b = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    k.Cylinder(0.018f, 0.025f, 0.55f, MeshKit.At(b + new Vector3(0, 0.27f, 0), new Vector3(0.08f * Mathf.Cos(a), 0, 0.08f * Mathf.Sin(a))), Straw.Darkened(0.1f), 5);
                    k.Capsule(0.045f, 0.2f, MeshKit.At(b * 1.3f + new Vector3(0, 0.62f, 0)), Straw, 6, 2);
                }
                break;
            case "pumpkin": // dilimli turuncu kure + sap + yaprak
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.Tau / 6f;
                    k.Sphere(0.2f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.1f, 0.2f, Mathf.Sin(a) * 0.1f), Vector3.Zero, new Vector3(0.75f, 0.9f, 0.75f)), new Color("E8862A"), 10, 6);
                }
                k.Cylinder(0.03f, 0.04f, 0.12f, MeshKit.At(0, 0.42f, 0), new Color("6B5A2A"), 6);
                k.Pillow(new Vector3(0.35f, 0.04f, 0.22f), MeshKit.At(new Vector3(0.25f, 0.06f, 0.05f), new Vector3(0, 0.4f, 0)), Leaf, 0.5f, 8, 3);
                break;
            case "sunflower": // sap, iki yaprak, sari tac + kahve gobek
                k.Cylinder(0.03f, 0.045f, 0.9f, MeshKit.At(0, 0.45f, 0), Stem, 6);
                k.Pillow(new Vector3(0.28f, 0.03f, 0.14f), MeshKit.At(new Vector3(0.14f, 0.4f, 0), new Vector3(0, 0, 0.4f)), Leaf, 0.5f, 8, 3);
                k.Pillow(new Vector3(0.28f, 0.03f, 0.14f), MeshKit.At(new Vector3(-0.14f, 0.55f, 0), new Vector3(0, 0, -0.4f)), Leaf, 0.5f, 8, 3);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.Tau / 10f;
                    k.Pillow(new Vector3(0.1f, 0.03f, 0.05f), MeshKit.At(new Vector3(Mathf.Cos(a) * 0.16f, 0.95f + Mathf.Sin(a) * 0.16f, 0.03f), new Vector3(Mathf.Pi / 2f, 0, a)), new Color("F2C531"), 0.5f, 6, 2);
                }
                k.Cylinder(0.12f, 0.12f, 0.06f, MeshKit.At(new Vector3(0, 0.95f, 0.05f), new Vector3(Mathf.Pi / 2f, 0, 0)), new Color("6B4423"), 10);
                break;
            case "flax": // ince mavimsi yesil yapraklar, kucuk mavi cicekler
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.4f, r = 0.04f + 0.03f * (i % 3);
                    var b = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    k.Cylinder(0.01f, 0.016f, 0.6f, MeshKit.At(b + new Vector3(0, 0.3f, 0), new Vector3(0.12f * Mathf.Cos(a), 0, 0.12f * Mathf.Sin(a))), new Color("6FA58A"), 4);
                    if (i % 2 == 0) k.Sphere(0.035f, MeshKit.At(b * 2.2f + new Vector3(0, 0.62f, 0)), new Color("7FA7E8"), 6, 3);
                }
                break;
            case "tomato": // yaprakli calı + kirmizi toplar
                k.Sphere(0.24f, MeshKit.At(new Vector3(0, 0.3f, 0), Vector3.Zero, new Vector3(1f, 1.1f, 1f)), LeafDark, 10, 6);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.Tau / 5f + 0.3f;
                    k.Sphere(0.075f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.2f, 0.22f + 0.08f * (i % 2), Mathf.Sin(a) * 0.2f)), new Color("D93A2B"), 8, 5);
                }
                break;
            case "mushroom": // tiknaz sap + kahve sapka, beyaz benek
                k.Cylinder(0.07f, 0.09f, 0.22f, MeshKit.At(0, 0.11f, 0), new Color("EDE3CC"), 8);
                k.Sphere(0.22f, MeshKit.At(new Vector3(0, 0.26f, 0), Vector3.Zero, new Vector3(1f, 0.55f, 1f)), new Color("A4572E"), 12, 6);
                for (int i = 0; i < 4; i++) { float a = i * 1.6f; k.Sphere(0.035f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.12f, 0.36f, Mathf.Sin(a) * 0.12f)), new Color("F4EEDD"), 6, 3); }
                break;
            case "blackberry": // dikenli cali + koyu mor kumeler
                k.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.3f, 0), Vector3.Zero, new Vector3(1.15f, 0.9f, 1.15f)), LeafDark, 10, 6);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.Tau / 6f;
                    k.Sphere(0.05f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.3f, 0.3f + 0.1f * (i % 3 - 1), Mathf.Sin(a) * 0.3f)), new Color("3A1F4A"), 6, 4);
                }
                break;
            case "sourberry": // dik cali + sari-yesil eksi toplar
                k.Capsule(0.18f, 0.5f, MeshKit.At(0, 0.3f, 0), Leaf, 10, 4);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.Tau / 5f;
                    k.Sphere(0.06f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.17f, 0.25f + 0.07f * i, Mathf.Sin(a) * 0.17f)), new Color("C9D43A"), 7, 4);
                }
                break;
            case "hazelnut": // gövdeli kucuk cali + kahve findiklar
                k.Cylinder(0.05f, 0.07f, 0.35f, MeshKit.At(0, 0.17f, 0), new Color("7A5634"), 6);
                k.Sphere(0.34f, MeshKit.At(new Vector3(0, 0.55f, 0), Vector3.Zero, new Vector3(1.1f, 0.85f, 1.1f)), Leaf, 10, 6);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.Tau / 5f + 0.5f;
                    k.Sphere(0.055f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.32f, 0.45f, Mathf.Sin(a) * 0.32f), Vector3.Zero, new Vector3(1f, 1.2f, 1f)), new Color("A8743E"), 7, 4);
                }
                break;
            case "garlic": // beyaz bas yarisi toprakta + uzun yapraklar
                k.Sphere(0.14f, MeshKit.At(new Vector3(0, 0.08f, 0), Vector3.Zero, new Vector3(1f, 0.85f, 1f)), new Color("F1ECE0"), 10, 6);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.Tau / 5f;
                    k.Cylinder(0.012f, 0.025f, 0.5f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.03f, 0.38f, Mathf.Sin(a) * 0.03f), new Vector3(0.18f * Mathf.Cos(a), 0, 0.18f * Mathf.Sin(a))), Stem, 4);
                }
                break;
            default: // bilinmeyen ekin: salgamin yedek sekli (M4 ekinleri: FarmView.Crops4.cs)
                if (MakeM4(crop, k)) break;
                k.Sphere(0.3f, MeshKit.At(new Vector3(0, 0.18f, 0), Vector3.Zero, new Vector3(1f, 0.95f, 1f)), Palette.TurnipBody, 12, 8);
                k.Sphere(0.27f, MeshKit.At(new Vector3(0, 0.3f, 0), Vector3.Zero, new Vector3(1f, 0.55f, 1f)), Palette.TurnipTop, 12, 6);
                break;
        }
        return k.Instance(mat);
    }

    /// <summary>Alaca'nin rengi (tint): Benekli acik benek, Altin altin, Aurora turkuaz-pembe (GDD 8.3 "kendine ozgu gorunus").</summary>
    public static Color AlacaTint(string alaca) => alaca switch
    {
        "benekli" => new Color(1.25f, 1.2f, 1.05f),
        "altin" => new Color(1.6f, 1.3f, 0.45f),
        "aurora" => new Color(0.7f, 1.35f, 1.5f),
        _ => Colors.White,
    };

    /// <summary>Hasat yazisinin ve ucan parcanin rengi.</summary>
    public static Color ProduceColor(string crop) => crop switch
    {
        "wheat" => new Color("E2C35A"), "pumpkin" => new Color("E8862A"), "sunflower" => new Color("F2C531"), "flax" => new Color("7FA7E8"),
        "tomato" => new Color("D93A2B"), "mushroom" => new Color("A4572E"), "blackberry" => new Color("5A2F6E"), "sourberry" => new Color("C9D43A"),
        "hazelnut" => new Color("A8743E"), "garlic" => new Color("F1ECE0"), _ => M4Color(crop),
    };
}

/// <summary>YER TUTUCU (tarim, M2): Korkuluk, koddan (kalas direk, capraz kol, cuval bas, saman). ICERIK 10.2.</summary>
public static class KorkulukVisual
{
    public static Node3D Make()
    {
        var k = new MeshKit();
        var wood = new Color("8A6238");
        k.Pillow(new Vector3(0.12f, 1.9f, 0.12f), MeshKit.At(0, 0.95f, 0), wood, 0.25f, 8, 4);
        k.Pillow(new Vector3(1.2f, 0.1f, 0.1f), MeshKit.At(0, 1.45f, 0), wood, 0.25f, 8, 4);
        k.Pillow(new Vector3(0.55f, 0.55f, 0.4f), MeshKit.At(0, 1.25f, 0), new Color("B5562F"), 0.45f, 10, 5);   // gomlek
        k.Sphere(0.2f, MeshKit.At(0, 1.82f, 0), new Color("D9C089"), 10, 6);                                     // cuval bas
        k.Cylinder(0.05f, 0.3f, 0.18f, MeshKit.At(0, 2.02f, 0), new Color("C9A24A"), 10);                          // hasir sapka
        k.Cylinder(0.32f, 0.32f, 0.03f, MeshKit.At(0, 1.95f, 0), new Color("C9A24A"), 12);
        for (int s = -1; s <= 1; s += 2)
            k.Cylinder(0.02f, 0.05f, 0.22f, MeshKit.At(new Vector3(0.66f * s, 1.42f, 0), new Vector3(0, 0, -s * 1.2f)), new Color("E2C35A"), 5); // saman eller
        return k.Instance(MeshKit.StaticMaterial);
    }
}
