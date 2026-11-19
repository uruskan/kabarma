using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// YER TUTUCU (URETIM §3): M4 zincir4 istasyonlarinin (ICERIK §11 T1 eksikleri ve T3-T6) koddan gorunusu. Kredi harcanmadi;
/// gercek modeller gelince placeables.json "model" alani bunlarin yerini alir. Her istasyon: renkli govde (basamagin
/// rengi: T3 bakir, T4 mavi, T5 cam turkuazi, T6 maya yesili) + turune gore bir sembol (baca, tank, kol, kule, levha).
/// Dil SANAT-SES §1: yastik kutu ve silindir.
/// </summary>
public static class ZincirVisuals4
{
    enum Sym { Chimney, Tank, Arm, Tower, Panel, Barrel, Hopper, Dome }

    // id -> (en, yukseklik, derinlik, basamak 1-6, sembol)
    static readonly System.Collections.Generic.Dictionary<string, (float w, float h, float d, int tier, Sym sym)> Table = new()
    {
        ["sirt_yazicisi"] = (0.5f, 0.45f, 0.3f, 1, Sym.Panel),
        ["tabaklama_askisi"] = (1.6f, 1.5f, 0.2f, 1, Sym.Arm),
        ["harman_yeri"] = (2.2f, 0.12f, 2.2f, 1, Sym.Hopper),
        ["yuksek_firin"] = (1.8f, 1.2f, 1.6f, 3, Sym.Chimney),
        ["kimya_tezgahi"] = (1.4f, 0.9f, 0.8f, 3, Sym.Tank),
        ["pres"] = (1.2f, 1.1f, 1.0f, 3, Sym.Panel),
        ["mutfak"] = (1.8f, 0.9f, 0.9f, 3, Sym.Chimney),
        ["damitici"] = (1.1f, 1.2f, 1.1f, 3, Sym.Dome),
        ["tursu_ficisi"] = (0.8f, 0.9f, 0.8f, 3, Sym.Barrel),
        ["hasat_robotu"] = (0.9f, 0.8f, 0.9f, 3, Sym.Arm),
        ["otomatik_yemlik"] = (1.1f, 0.7f, 0.9f, 3, Sym.Hopper),
        ["yumurta_toplayici"] = (0.8f, 0.6f, 1.0f, 3, Sym.Hopper),
        ["kompostor"] = (1.1f, 0.9f, 1.1f, 3, Sym.Barrel),
        ["cam_ocagi"] = (1.6f, 1.2f, 1.4f, 4, Sym.Chimney),
        ["elektronik_tezgahi"] = (1.4f, 0.9f, 0.8f, 4, Sym.Panel),
        ["montaj_makinesi"] = (2.4f, 1.5f, 1.6f, 4, Sym.Arm),
        ["rafineri"] = (2.0f, 1.8f, 1.8f, 4, Sym.Tower),
        ["sera_kontrol_unitesi"] = (0.8f, 1.1f, 0.6f, 4, Sym.Panel),
        ["asi_tezgahi"] = (1.2f, 1.0f, 0.8f, 5, Sym.Arm), // tarim4 (M4): gen aktarma, ICERIK 11
        ["ekim_robotu"] = (0.9f, 0.8f, 0.9f, 4, Sym.Arm),
        ["sagim_kirkim_kolu"] = (1.2f, 1.0f, 0.8f, 4, Sym.Arm),
        ["paketleyici"] = (1.6f, 0.9f, 1.0f, 4, Sym.Hopper),
        ["sondaj_kulesi"] = (1.8f, 3.0f, 1.8f, 4, Sym.Tower),
        ["kristal_kesici"] = (1.4f, 0.9f, 1.0f, 5, Sym.Dome),
        ["batarya_tezgahi"] = (1.6f, 0.9f, 1.0f, 5, Sym.Tank),
        ["zeplin_hangari"] = (6.0f, 3.2f, 3.5f, 5, Sym.Dome),
        ["maya_dokumhanesi"] = (3.0f, 1.8f, 2.4f, 6, Sym.Chimney),
        ["kabartma_odasi"] = (4.0f, 2.6f, 4.0f, 6, Sym.Dome),
    };

    /// <summary>Istasyonun yer tutucusu; tabloda yoksa ya da modeli varsa null.</summary>
    public static Node3D Make(PlaceableDef def)
    {
        if (def.Model != null || !Table.TryGetValue(def.Id, out var t)) return null;
        var k = new MeshKit();
        var body = t.tier switch
        {
            1 => Palette.Wood,
            3 => Palette.Accent.Lerp(Palette.Rock, 0.45f),
            4 => Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.35f),
            5 => Palette.CoopBlue.Lightened(0.3f).Lerp(Palette.OffWhite, 0.3f),
            _ => Palette.MayaTeal.Lerp(Palette.DarkGrey, 0.45f),
        };
        var dark = body.Lerp(Palette.Shadow, 0.5f);
        var mark = Palette.Warning;
        k.Pillow(new Vector3(t.w, t.h, t.d), MeshKit.At(0, t.h / 2f, 0), body, 0.3f);
        float top = t.h;
        switch (t.sym)
        {
            case Sym.Chimney:
                k.Cylinder(0.16f, 0.2f, 0.9f, MeshKit.At(t.w * 0.25f, top + 0.4f, -t.d * 0.15f), dark, 9);
                k.Pillow(new Vector3(0.5f, 0.3f, 0.12f), MeshKit.At(0, 0.35f, t.d / 2f), Palette.Accent.Lerp(Palette.Warning, 0.5f), 0.4f); // agiz
                break;
            case Sym.Tank:
                k.Cylinder(0.3f, 0.3f, 0.7f, MeshKit.At(-t.w * 0.2f, top + 0.35f, 0), Palette.OffWhite.Lerp(body, 0.3f), 12);
                k.Sphere(0.1f, MeshKit.At(t.w * 0.25f, top + 0.12f, 0), mark);
                break;
            case Sym.Arm:
                k.Cylinder(0.07f, 0.07f, 0.9f, MeshKit.At(0, top + 0.45f, 0), dark, 7);
                k.Cylinder(0.06f, 0.06f, 0.8f, MeshKit.At(new Vector3(0.35f, top + 0.9f, 0), new Vector3(0, 0, Mathf.Pi / 2f)), dark, 7);
                k.Sphere(0.1f, MeshKit.At(0.75f, top + 0.9f, 0), mark);
                break;
            case Sym.Tower:
                k.Pillow(new Vector3(0.35f, t.h * 0.8f, 0.35f), MeshKit.At(0, top + t.h * 0.4f, 0), dark, 0.3f);
                k.Sphere(0.14f, MeshKit.At(0, top + t.h * 0.85f, 0), mark);
                break;
            case Sym.Panel:
                k.Pillow(new Vector3(t.w * 0.7f, 0.5f, 0.1f), MeshKit.At(new Vector3(0, top + 0.3f, -t.d * 0.2f), new Vector3(-0.35f, 0, 0)), Palette.CoopBlue.Lerp(Palette.DarkGrey, 0.4f), 0.3f);
                k.Sphere(0.07f, MeshKit.At(t.w * 0.3f, top + 0.08f, t.d * 0.25f), mark);
                break;
            case Sym.Barrel:
                k.Cylinder(0.32f, 0.36f, 0.5f, MeshKit.At(0, top + 0.25f, 0), Palette.Wood.Lightened(0.1f), 12);
                break;
            case Sym.Hopper:
                k.Cylinder(0.4f, 0.18f, 0.45f, MeshKit.At(0, top + 0.22f, 0), dark, 10);
                break;
            case Sym.Dome:
                k.Sphere(Mathf.Min(t.w, t.d) * 0.32f, new Transform3D(Basis.Identity.Scaled(new Vector3(1f, 0.7f, 1f)), new Vector3(0, top + 0.1f, 0)), Palette.OffWhite.Lerp(body, 0.4f), 14, 8);
                break;
        }
        return k.Instance();
    }
}
