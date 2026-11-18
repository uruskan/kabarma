using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (URETIM §3): M2 zincir istasyonlarinin koddan gorunusu (ICERIK §11.1). Kredi harcanmadi; gercek modeller
/// varlik hattindan gelince placeables.json "model" alani bunlarin yerini alir (PlaceableVisuals.Make modeli once dener
/// degil: bu sinif yalniz modeli olmayan zincir istasyonlari icin cagrilir). Kaynak: pipeline/licenses.md "zincir".
/// Dil SANAT-SES §1: buyuk, yumusak, tiknaz; yastik kutu ve silindir, kose rengi.
/// </summary>
public static class ZincirVisuals
{
    /// <summary>Zincir istasyonunun yer tutucusu; modeli varsa ya da zincir istasyonu degilse null.</summary>
    public static Node3D Make(PlaceableDef def)
    {
        if (def.Model != null) return null;
        var k = new MeshKit();
        var wood = Palette.Wood;
        var dark = Palette.Wood.Lerp(Palette.Shadow, 0.45f);
        var iron = Palette.DarkGrey;
        var stone = Palette.Rock;
        switch (def.Id)
        {
            case "arastirma_masasi":
                Table(k, 1.5f, 0.85f, wood);
                k.Pillow(new Vector3(0.7f, 0.5f, 0.12f), MeshKit.At(new Vector3(0, 1.15f, -0.2f), new Vector3(-0.25f, 0, 0)), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.3f), 0.3f);
                k.Pillow(new Vector3(0.6f, 0.4f, 0.04f), MeshKit.At(new Vector3(0, 1.16f, -0.13f), new Vector3(-0.25f, 0, 0)), Palette.CoopBlue.Lightened(0.35f), 0.2f);
                k.Pillow(new Vector3(0.35f, 0.06f, 0.25f), MeshKit.At(0.4f, 0.93f, 0.15f), Palette.OffWhite, 0.4f); // defter
                k.Sphere(0.09f, MeshKit.At(-0.5f, 1.0f, 0.1f), Palette.Warning);                                 // numune
                break;
            case "komur_ocagi":
                k.Sphere(0.8f, new Transform3D(Basis.Identity.Scaled(new Vector3(1f, 0.75f, 1f)), new Vector3(0, 0.2f, 0)), stone.Lerp(Palette.Ground, 0.35f), 14, 8);
                k.Pillow(new Vector3(0.45f, 0.4f, 0.2f), MeshKit.At(0, 0.25f, 0.68f), Palette.Shadow, 0.4f);
                k.Cylinder(0.12f, 0.14f, 0.5f, MeshKit.At(0.25f, 0.95f, -0.1f), stone.Lerp(Palette.Shadow, 0.3f), 8);
                break;
            case "demirci_ocagi":
                k.Pillow(new Vector3(1.3f, 0.9f, 1.0f), MeshKit.At(0, 0.45f, 0), stone.Lerp(Palette.RockDark, 0.3f), 0.35f);
                k.Pillow(new Vector3(0.7f, 0.12f, 0.5f), MeshKit.At(0, 0.93f, 0.05f), Palette.Accent.Lerp(Palette.Warning, 0.4f), 0.4f); // kor
                k.Cylinder(0.18f, 0.24f, 1.0f, MeshKit.At(-0.35f, 1.4f, -0.3f), stone.Lerp(Palette.Shadow, 0.35f), 10);
                k.Pillow(new Vector3(0.5f, 0.22f, 0.24f), MeshKit.At(0.95f, 0.62f, 0.2f), iron, 0.3f); // ors
                k.Cylinder(0.1f, 0.14f, 0.5f, MeshKit.At(0.95f, 0.26f, 0.2f), dark, 8);
                break;
            case "bicki":
                Table(k, 1.8f, 0.8f, wood);
                k.Cylinder(0.42f, 0.42f, 0.04f, MeshKit.At(new Vector3(0, 1.0f, 0), new Vector3(0, 0, Mathf.Pi / 2f)), iron.Lightened(0.35f), 20);
                k.Pillow(new Vector3(0.35f, 0.3f, 0.3f), MeshKit.At(0, 0.62f, -0.25f), Palette.CoopBlue.Lerp(iron, 0.5f), 0.3f); // motor
                k.Cylinder(0.08f, 0.08f, 1.2f, MeshKit.At(new Vector3(0.0f, 0.9f, 0.25f), new Vector3(0, 0, Mathf.Pi / 2f)), wood.Lightened(0.15f), 8); // kutuk
                break;
            case "degirmen":
                k.Cylinder(0.55f, 0.6f, 0.35f, MeshKit.At(0, 0.18f, 0), stone, 18);
                k.Cylinder(0.5f, 0.5f, 0.22f, MeshKit.At(0, 0.47f, 0), stone.Lightened(0.12f), 18);
                k.Cylinder(0.06f, 0.06f, 1.2f, MeshKit.At(0, 1.1f, 0), dark, 8);
                k.Cylinder(0.05f, 0.05f, 1.3f, MeshKit.At(new Vector3(0, 1.55f, 0), new Vector3(0, 0, Mathf.Pi / 2f)), wood, 8);
                k.Cylinder(0.25f, 0.12f, 0.35f, MeshKit.At(0, 0.8f, 0), wood.Lightened(0.1f), 10); // huni
                break;
            case "dokuma_tezgahi":
                foreach (var x in new[] { -0.7f, 0.7f }) foreach (var z in new[] { -0.35f, 0.35f }) k.Cylinder(0.05f, 0.05f, 1.4f, MeshKit.At(x, 0.7f, z), wood, 7);
                k.Cylinder(0.07f, 0.07f, 1.5f, MeshKit.At(new Vector3(0, 1.35f, -0.35f), new Vector3(0, 0, Mathf.Pi / 2f)), dark, 8);
                k.Cylinder(0.07f, 0.07f, 1.5f, MeshKit.At(new Vector3(0, 0.55f, 0.35f), new Vector3(0, 0, Mathf.Pi / 2f)), dark, 8);
                k.Pillow(new Vector3(1.3f, 0.75f, 0.03f), MeshKit.At(new Vector3(0, 0.95f, 0), new Vector3(-0.6f, 0, 0)), Palette.Accent.Lerp(Palette.OffWhite, 0.45f), 0.15f); // kumas
                break;
            case "kasap_tezgahi":
                Table(k, 1.4f, 0.75f, wood.Lerp(Palette.Accent, 0.12f));
                k.Pillow(new Vector3(0.45f, 0.06f, 0.18f), MeshKit.At(0.3f, 0.9f, 0.1f), iron.Lightened(0.3f), 0.2f);
                break;
            case "kulucka":
                k.Pillow(new Vector3(0.9f, 0.6f, 0.7f), MeshKit.At(0, 0.3f, 0), wood, 0.3f);
                k.Pillow(new Vector3(0.75f, 0.12f, 0.55f), MeshKit.At(0, 0.64f, 0), Palette.OffWhite.Lerp(Palette.Warning, 0.3f), 0.3f);
                k.Cylinder(0.1f, 0.16f, 0.15f, MeshKit.At(0, 0.95f, 0), Palette.Warning, 10); // lamba
                k.Cylinder(0.02f, 0.02f, 0.3f, MeshKit.At(0, 0.8f, 0), iron, 6);
                break;
            case "arac_atolyesi":
                k.Pillow(new Vector3(3.0f, 0.12f, 2.6f), MeshKit.At(0, 0.06f, 0), stone.Lerp(Palette.Ground, 0.4f), 0.2f);
                foreach (var x in new[] { -1.3f, 1.3f }) k.Pillow(new Vector3(0.2f, 2.4f, 0.2f), MeshKit.At(x, 1.2f, -1.0f), iron, 0.3f);
                k.Pillow(new Vector3(2.8f, 0.22f, 0.25f), MeshKit.At(0, 2.4f, -1.0f), iron, 0.3f);
                k.Pillow(new Vector3(0.18f, 0.6f, 0.18f), MeshKit.At(0.3f, 1.95f, -1.0f), Palette.Warning, 0.3f); // vinc kancasi
                k.Pillow(new Vector3(1.0f, 0.8f, 0.5f), MeshKit.At(-0.9f, 0.5f, 0.8f), wood, 0.3f); // alet dolabi
                break;
            case "oyma_iskelesi":
                foreach (var x in new[] { -1.1f, 1.1f }) foreach (var z in new[] { -1.1f, 1.1f }) k.Cylinder(0.06f, 0.06f, 2.2f, MeshKit.At(x, 1.1f, z), wood, 7);
                foreach (var y in new[] { 0.8f, 1.9f })
                {
                    k.Pillow(new Vector3(2.3f, 0.08f, 0.12f), MeshKit.At(0, y, -1.1f), dark, 0.3f);
                    k.Pillow(new Vector3(2.3f, 0.08f, 0.12f), MeshKit.At(0, y, 1.1f), dark, 0.3f);
                }
                k.Pillow(new Vector3(2.3f, 0.06f, 0.6f), MeshKit.At(0, 1.92f, 0), wood.Lightened(0.15f), 0.2f);
                break;
            default:
                return null;
        }
        return k.Instance();
    }

    /// <summary>Dort ayakli masa: ust tabla 0.85 m'de.</summary>
    static void Table(MeshKit k, float w, float d, Color c)
    {
        k.Pillow(new Vector3(w, 0.12f, d), MeshKit.At(0, 0.82f, 0), c, 0.25f);
        foreach (var x in new[] { -w / 2f + 0.1f, w / 2f - 0.1f })
            foreach (var z in new[] { -d / 2f + 0.1f, d / 2f - 0.1f })
                k.Cylinder(0.05f, 0.05f, 0.78f, MeshKit.At(x, 0.39f, z), c.Lerp(Palette.Shadow, 0.35f), 7);
    }
}
