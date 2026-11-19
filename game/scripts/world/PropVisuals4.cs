using System.Collections.Generic;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (URETIM §3): M4 kale'nin kalan 56 yerlestirilebilirinin (ICERIK §10.2) koddan gorunusu. Kredi harcanmadi;
/// gercek modeller gelince placeables.json "model" bunlarin yerini alir (pipeline/licenses.md "kale"). Ortak dil: yastik
/// kutu, silindir (SANAT-SES §1). Isik veren parcalar (lightRange) gece isigi tasir.
/// </summary>
public static class PropVisuals4
{
    enum S { Box, Cyl, Flat, Post, Door, Frame, Bed, Table, Fire }

    static readonly Dictionary<string, (S s, float w, float h, float d, Color c)> Table = new()
    {
        // kapilar
        ["celik_kapi"] = (S.Door, 1.4f, 2.3f, 0.12f, Palette.DarkGrey.Lightened(0.15f)),
        ["cift_kanat_kapi"] = (S.Door, 2.6f, 2.3f, 0.12f, Palette.Wood),
        ["garaj_kapisi"] = (S.Door, 4.0f, 2.8f, 0.14f, Palette.OffWhite.Lerp(Palette.DarkGrey, 0.4f)),
        ["tavan_kapagi"] = (S.Flat, 1.1f, 0.08f, 1.1f, Palette.Wood.Lerp(Palette.Shadow, 0.3f)),
        ["el_merdiveni"] = (S.Frame, 0.6f, 2.4f, 0.1f, Palette.Wood),
        // depo
        ["ambar"] = (S.Box, 2.4f, 1.6f, 1.8f, Palette.Wood.Lerp(Palette.Shadow, 0.2f)),
        ["silo"] = (S.Cyl, 1.2f, 3.0f, 1.2f, Palette.OffWhite.Lerp(Palette.DarkGrey, 0.35f)),
        ["sivi_tanki"] = (S.Cyl, 1.0f, 1.4f, 1.0f, Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.35f)),
        ["akilli_depo"] = (S.Box, 1.3f, 1.4f, 0.9f, Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.45f)),
        ["tohum_dolabi"] = (S.Box, 1.0f, 1.8f, 0.6f, Palette.Plant.Lerp(Palette.Wood, 0.5f)),
        ["alet_askisi"] = (S.Frame, 1.2f, 1.0f, 0.1f, Palette.Wood),
        ["kupa_rafi"] = (S.Frame, 1.2f, 1.2f, 0.25f, Palette.Wood.Lightened(0.15f)),
        // yasam
        ["yatak"] = (S.Bed, 1.0f, 0.5f, 2.0f, Palette.CoopBlue.Lightened(0.2f)),
        ["ranza"] = (S.Bed, 1.0f, 1.7f, 2.0f, Palette.CoopBlue.Lightened(0.2f)),
        ["sedir"] = (S.Bed, 1.9f, 0.5f, 0.8f, Palette.Accent.Lerp(Palette.OffWhite, 0.4f)),
        ["masa"] = (S.Table, 1.4f, 0.8f, 0.9f, Palette.Wood),
        ["sandalye"] = (S.Table, 0.45f, 0.5f, 0.45f, Palette.Wood.Lightened(0.1f)),
        ["hali"] = (S.Flat, 1.8f, 0.03f, 1.2f, Palette.Accent.Lerp(Palette.Shadow, 0.3f)),
        ["soba"] = (S.Fire, 0.6f, 0.9f, 0.6f, Palette.DarkGrey),
        // isik
        ["fener"] = (S.Post, 0.2f, 1.1f, 0.2f, Palette.Wood),
        ["projektor"] = (S.Post, 0.5f, 2.2f, 0.5f, Palette.DarkGrey),
        ["nilufer_saksisi"] = (S.Cyl, 0.5f, 0.5f, 0.5f, Palette.MayaTeal.Lerp(Palette.Soil, 0.5f)),
        ["sokak_lambasi"] = (S.Post, 0.3f, 3.0f, 0.3f, Palette.DarkGrey),
        // tarim
        ["yagmurlama"] = (S.Post, 0.2f, 1.0f, 0.2f, Palette.CoopBlue),
        ["damla_hatti"] = (S.Flat, 1.4f, 0.06f, 0.1f, Palette.DarkGrey),
        ["buyutme_lambasi"] = (S.Post, 0.5f, 1.8f, 0.5f, Palette.MayaViolet.Lerp(Palette.DarkGrey, 0.5f)),
        ["kompost_kasasi"] = (S.Box, 1.3f, 0.8f, 1.3f, Palette.Soil),
        ["saksi"] = (S.Cyl, 0.5f, 0.45f, 0.5f, Palette.Accent.Lerp(Palette.Soil, 0.5f)),
        ["yukseltilmis_yatak"] = (S.Box, 2.0f, 0.5f, 1.0f, Palette.Soil),
        ["sirik"] = (S.Post, 0.12f, 2.2f, 0.12f, Palette.Wood),
        ["tarla_tabelasi"] = (S.Frame, 0.6f, 0.9f, 0.08f, Palette.Wood),
        // hizmet
        ["harita_masasi"] = (S.Table, 1.6f, 0.9f, 1.1f, Palette.Wood.Lerp(Palette.OffWhite, 0.3f)),
        ["is_emri_panosu"] = (S.Frame, 1.4f, 1.6f, 0.1f, Palette.Wood),
        ["borsa_tahtasi"] = (S.Frame, 1.6f, 1.8f, 0.1f, Palette.NightDeep.Lerp(Palette.CoopBlue, 0.3f)),
        ["katalog_terminali"] = (S.Box, 0.7f, 1.3f, 0.6f, Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.4f)),
        ["tohum_carki"] = (S.Cyl, 1.0f, 1.2f, 1.0f, Palette.Plant.Lerp(Palette.Warning, 0.4f)),
        ["gardirop"] = (S.Box, 1.2f, 2.0f, 0.6f, Palette.Wood.Lerp(Palette.Shadow, 0.15f)),
        ["tabela"] = (S.Post, 0.7f, 1.6f, 0.08f, Palette.Wood),
        // sus
        ["saksi_cicegi"] = (S.Cyl, 0.35f, 0.5f, 0.35f, Palette.Accent),
        ["duvar_halisi"] = (S.Frame, 1.2f, 1.6f, 0.04f, Palette.Accent.Lerp(Palette.Shadow, 0.3f)),
        ["tablo"] = (S.Frame, 0.9f, 0.7f, 0.05f, Palette.Wood.Lightened(0.2f)),
        ["ekip_bayragi"] = (S.Post, 0.8f, 3.0f, 0.04f, Palette.CoopBlue),
        ["ata_heykeli"] = (S.Cyl, 1.0f, 2.6f, 1.0f, Palette.Rock),
        ["ruzgar_cani"] = (S.Post, 0.3f, 1.4f, 0.3f, Palette.Warning.Lerp(Palette.OffWhite, 0.4f)),
        ["kus_evi"] = (S.Post, 0.45f, 2.0f, 0.45f, Palette.Wood.Lightened(0.15f)),
        ["gidak_cucesi"] = (S.Cyl, 0.4f, 0.6f, 0.4f, Palette.Warning),
        ["cesme"] = (S.Cyl, 1.6f, 1.0f, 1.6f, Palette.Rock.Lerp(Palette.CoopBlue, 0.25f)),
        ["hamak"] = (S.Bed, 1.2f, 0.9f, 2.4f, Palette.Plant.Lerp(Palette.OffWhite, 0.4f)),
        ["salincak"] = (S.Frame, 1.8f, 2.4f, 0.3f, Palette.Wood),
        ["kamp_atesi"] = (S.Fire, 0.9f, 0.3f, 0.9f, Palette.Rock),
        ["raf"] = (S.Frame, 1.2f, 0.9f, 0.3f, Palette.Wood),
        ["kitaplik"] = (S.Box, 1.2f, 1.9f, 0.4f, Palette.Wood.Lerp(Palette.Shadow, 0.2f)),
        ["saat"] = (S.Post, 0.4f, 1.8f, 0.3f, Palette.Wood.Lightened(0.1f)),
        ["radyo"] = (S.Box, 0.5f, 0.3f, 0.2f, Palette.Accent.Lerp(Palette.DarkGrey, 0.4f)),
        ["semaver"] = (S.Cyl, 0.4f, 0.6f, 0.4f, Palette.Warning.Lerp(Palette.DarkGrey, 0.3f)),
        ["kilim"] = (S.Flat, 2.0f, 0.03f, 1.3f, Palette.CoopBlue.Lerp(Palette.Accent, 0.5f)),
    };

    public static bool Has(string id) => Table.ContainsKey(id);

    public static Node3D Make(PlaceableDef def)
    {
        if (!Table.TryGetValue(def.Id, out var t)) return null;
        var root = new Node3D();
        var k = new MeshKit();
        var dark = t.c.Lerp(Palette.Shadow, 0.4f);
        switch (t.s)
        {
            case S.Box: k.Pillow(new Vector3(t.w, t.h, t.d), MeshKit.At(0, t.h / 2f, 0), t.c, 0.25f); k.Pillow(new Vector3(t.w * 0.8f, 0.05f, 0.04f), MeshKit.At(0, t.h * 0.6f, t.d / 2f), dark, 0.2f); break;
            case S.Cyl: k.Cylinder(t.w * 0.45f, t.w * 0.5f, t.h, MeshKit.At(0, t.h / 2f, 0), t.c, 14); k.Cylinder(t.w * 0.5f, t.w * 0.5f, 0.06f, MeshKit.At(0, t.h, 0), dark, 14); break;
            case S.Flat: k.Pillow(new Vector3(t.w, t.h, t.d), MeshKit.At(0, t.h / 2f, 0), t.c, 0.2f); break;
            case S.Post:
                k.Cylinder(0.05f, 0.07f, t.h, MeshKit.At(0, t.h / 2f, 0), Palette.Wood, 7);
                k.Pillow(new Vector3(t.w, Mathf.Min(0.45f, t.h * 0.3f), t.d), MeshKit.At(0, t.h - 0.1f, 0), t.c, 0.3f);
                break;
            case S.Door:
                foreach (var x in new[] { -t.w / 2f, t.w / 2f }) k.Pillow(new Vector3(0.12f, t.h, 0.16f), MeshKit.At(x, t.h / 2f, 0), dark, 0.2f);
                k.Pillow(new Vector3(t.w, 0.12f, 0.16f), MeshKit.At(0, t.h, 0), dark, 0.2f);
                k.Pillow(new Vector3(t.w - 0.1f, t.h - 0.1f, t.d), MeshKit.At(0, t.h / 2f - 0.05f, 0), t.c, 0.2f);
                k.Sphere(0.06f, MeshKit.At(t.w * 0.35f, t.h * 0.45f, t.d), Palette.Warning);
                break;
            case S.Frame:
                k.Pillow(new Vector3(t.w, t.h, t.d), MeshKit.At(0, t.h / 2f + 0.2f, 0), t.c, 0.15f);
                foreach (var x in new[] { -t.w / 2f + 0.05f, t.w / 2f - 0.05f }) k.Cylinder(0.04f, 0.05f, t.h + 0.2f, MeshKit.At(x, (t.h + 0.2f) / 2f, -0.03f), dark, 6);
                break;
            case S.Bed:
                k.Pillow(new Vector3(t.w, 0.25f, t.d), MeshKit.At(0, 0.2f, 0), Palette.Wood, 0.2f);
                k.Pillow(new Vector3(t.w - 0.1f, 0.2f, t.d - 0.1f), MeshKit.At(0, 0.42f, 0), t.c, 0.5f);
                if (t.h > 1f) k.Pillow(new Vector3(t.w - 0.1f, 0.2f, t.d - 0.1f), MeshKit.At(0, 1.2f, 0), t.c, 0.5f);
                break;
            case S.Table:
                k.Pillow(new Vector3(t.w, 0.08f, t.d), MeshKit.At(0, t.h, 0), t.c, 0.2f);
                foreach (var x in new[] { -t.w / 2f + 0.06f, t.w / 2f - 0.06f }) foreach (var z in new[] { -t.d / 2f + 0.06f, t.d / 2f - 0.06f }) k.Cylinder(0.04f, 0.04f, t.h, MeshKit.At(x, t.h / 2f, z), dark, 6);
                break;
            case S.Fire:
                k.Cylinder(t.w * 0.5f, t.w * 0.55f, t.h, MeshKit.At(0, t.h / 2f, 0), t.c, 10);
                break;
        }
        root.AddChild(k.Instance());
        if (def.LightRange > 0)
        {
            float y = t.s == S.Post ? t.h - 0.1f : t.s == S.Fire ? t.h + 0.2f : t.h + 0.3f;
            if (t.s == S.Fire) PlaceableVisuals.Fire(root, new Vector3(0, t.h, 0), def.LightRange, 1.1f);
            else root.AddChild(new OmniLight3D { LightColor = def.Id == "buyutme_lambasi" ? new Color("D898FF") : new Color("FFE2A8"), LightEnergy = 1.4f, OmniRange = def.LightRange, Position = new Vector3(0, y, 0), ShadowEnabled = false });
        }
        return root;
    }
}
