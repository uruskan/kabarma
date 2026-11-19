using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (URETIM §3): M4 kale'nin 5 kulesi ve 4 tuzagi (ICERIK §13) koddan. Kredi harcanmadi; gercek modeller
/// gelince placeables.json "model" bunlarin yerini alir (pipeline/licenses.md "kale"). Kuleler DefenseNode sozlesmesine
/// uyar: "head" (donen bas), "muzzle" (namlu ucu, ileri -Z); Cukur'un "leaf_*" ve Firlatma Pedi'nin "plate" adlari yok.
/// </summary>
public static class DefenseVisuals4
{
    public static Node3D Make(PlaceableDef def)
    {
        switch (def.Id)
        {
            case "alev_kulesi": return Tower(Palette.Accent.Lerp(Palette.DarkGrey, 0.35f), 0.55f, 1.4f, k =>
            {
                k.Cylinder(0.22f, 0.26f, 0.9f, MeshKit.At(new Vector3(0, 0.0f, -0.35f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.DarkGrey, 10);
                k.Cylinder(0.13f, 0.18f, 0.25f, MeshKit.At(new Vector3(0, 0.0f, -0.88f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.Warning, 10);
                k.Sphere(0.22f, MeshKit.At(0, 0.2f, 0.25f), Palette.Accent); // yakit kabi
            }, 0.9f);
            case "havan_kulesi": return Tower(Palette.Rock.Lerp(Palette.DarkGrey, 0.4f), 0.8f, 1.1f, k =>
            {
                k.Cylinder(0.2f, 0.32f, 1.1f, MeshKit.At(new Vector3(0, 0.25f, -0.15f), new Vector3(Mathf.Pi / 2f - 0.55f, 0, 0)), Palette.DarkGrey, 12);
                k.Cylinder(0.36f, 0.36f, 0.12f, MeshKit.At(0, 0.0f, 0), Palette.Wood, 12);
            }, 1.1f, muzzleY: 0.8f, muzzleZ: -0.5f);
            case "ark_bobini": return Tower(Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.4f), 0.5f, 1.8f, k =>
            {
                for (int i = 0; i < 4; i++) k.Cylinder(0.3f - i * 0.04f, 0.3f - i * 0.04f, 0.07f, MeshKit.At(0, i * 0.16f, 0), Palette.Warning.Lerp(Palette.OffWhite, 0.3f), 14);
                k.Cylinder(0.05f, 0.05f, 0.9f, MeshKit.At(0, 0.55f, 0), Palette.OffWhite, 8);
                k.Sphere(0.14f, MeshKit.At(0, 1.05f, 0), Palette.MayaTeal);
            }, 1.6f, muzzleY: 1.05f, muzzleZ: 0f);
            case "lazer_kulesi": return Tower(Palette.CoopBlue.Lightened(0.3f).Lerp(Palette.OffWhite, 0.4f), 0.45f, 2.0f, k =>
            {
                k.Pillow(new Vector3(0.3f, 0.3f, 0.8f), MeshKit.At(0, 0.1f, -0.1f), Palette.DarkGrey, 0.3f);
                k.Cylinder(0.07f, 0.07f, 0.9f, MeshKit.At(new Vector3(0, 0.1f, -0.7f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.OffWhite, 8);
                k.Sphere(0.1f, MeshKit.At(0, 0.1f, -1.15f), Palette.HoldingRed);
            }, 1.9f);
            case "maya_topu_kulesi": return Tower(Palette.MayaTeal.Lerp(Palette.DarkGrey, 0.55f), 0.7f, 1.9f, k =>
            {
                k.Sphere(0.45f, MeshKit.At(0, 0.1f, 0), Palette.MayaViolet.Lerp(Palette.DarkGrey, 0.3f), 14, 8);
                k.Cylinder(0.2f, 0.26f, 0.9f, MeshKit.At(new Vector3(0, 0.1f, -0.6f), new Vector3(Mathf.Pi / 2f, 0, 0)), Palette.MayaTeal, 12);
            }, 1.9f);

            case "elektrikli_cit":
            {
                var k = new MeshKit();
                foreach (var x in new[] { -0.85f, 0.85f }) k.Cylinder(0.06f, 0.07f, 1.1f, MeshKit.At(x, 0.55f, 0), Palette.Wood, 7);
                foreach (var y in new[] { 0.35f, 0.65f, 0.95f }) k.Cylinder(0.012f, 0.012f, 1.7f, MeshKit.At(new Vector3(0, y, 0), new Vector3(0, 0, Mathf.Pi / 2f)), Palette.Warning, 5);
                foreach (var x in new[] { -0.85f, 0.85f }) k.Sphere(0.06f, MeshKit.At(x, 1.12f, 0), Palette.MayaTeal);
                return k.Instance();
            }
            case "mayin":
            {
                var k = new MeshKit();
                k.Cylinder(0.42f, 0.46f, 0.12f, MeshKit.At(0, 0.06f, 0), Palette.DarkGrey.Lerp(Palette.Soil, 0.4f), 14);
                k.Cylinder(0.12f, 0.12f, 0.06f, MeshKit.At(0, 0.14f, 0), Palette.HoldingRed, 10);
                return k.Instance();
            }
            case "yag_hendegi":
            {
                var k = new MeshKit();
                k.Pillow(new Vector3(1.9f, 0.05f, 1.9f), MeshKit.At(0, 0.03f, 0), Palette.NightDeep.Lerp(Palette.Soil, 0.5f), 0.1f);
                k.Pillow(new Vector3(1.6f, 0.02f, 1.6f), MeshKit.At(0, 0.06f, 0), Palette.Shadow.Lerp(Palette.Warning, 0.15f), 0.1f);
                return k.Instance();
            }
            case "dondurucu_puskurtucu":
            {
                var k = new MeshKit();
                k.Cylinder(0.5f, 0.6f, 0.2f, MeshKit.At(0, 0.1f, 0), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.5f), 12);
                k.Cylinder(0.14f, 0.2f, 0.7f, MeshKit.At(0, 0.55f, 0), Palette.OffWhite.Lerp(Palette.CoopBlue, 0.3f), 10);
                k.Sphere(0.12f, MeshKit.At(0, 0.95f, 0), Color.FromHtml("#BFE8FF"));
                foreach (var a in new[] { 0f, 2.09f, 4.19f }) k.Cylinder(0.04f, 0.04f, 0.5f, MeshKit.At(new Vector3(Mathf.Cos(a) * 0.9f, 0.2f, Mathf.Sin(a) * 0.9f), new Vector3(0, 0, 0)), Palette.OffWhite, 6);
                return k.Instance();
            }
        }
        return null;
    }

    /// <summary>Taban + donen bas (head) + namlu ucu (muzzle). parts: namlu, bas parcalari (head yerelinde, ileri -Z).</summary>
    static Node3D Tower(Color body, float baseRadius, float headY, System.Action<MeshKit> parts, float muzzleHeight, float muzzleY = 0.1f, float muzzleZ = -1.1f)
    {
        var root = new Node3D();
        var k = new MeshKit();
        k.Cylinder(baseRadius * 0.8f, baseRadius, headY, MeshKit.At(0, headY / 2f, 0), body, 12);
        k.Cylinder(baseRadius * 1.15f, baseRadius * 1.15f, 0.12f, MeshKit.At(0, 0.06f, 0), Palette.Rock, 12);
        root.AddChild(k.Instance());
        var head = new Node3D { Name = "head", Position = new Vector3(0, headY, 0) };
        var hk = new MeshKit();
        parts(hk);
        head.AddChild(hk.Instance());
        head.AddChild(new Node3D { Name = "muzzle", Position = new Vector3(0, muzzleY, muzzleZ) });
        root.AddChild(head);
        return root;
    }
}
