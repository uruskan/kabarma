"""
Kabarma: M1 yerlestirilebilirlerinden ekonomi akisinin uc modeli (ICERIK §10.2; URETIM §6 M1 "Yerlestirilebilir (6)").
Sandik, Uyku Tulumu, Mesale; bir de yuk nesnesi kargo kasasi (CargoCrateNode).
Yapi kitinin kalas dili ve ortak ahsap atlasi (pipeline/build_kit.py): kredi yok.
(Ahsap Kapi, Yemlik, Folluk ilkgun akisinda.)

Calistir (depo kokunden; once build_kit.py atlasi uretmis olmali):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/build_placeables.py -- [--only sandik,mesale]

Cikti:
  game/assets/models/placeables/<id>.glb        (Godot'ta ortak wood_kit.tres malzemesi)
  game/assets/models/placeables/<id>.glb.import
  game/assets/models/placeables/placeables.json ucgen sayisi, sinirlar, carpisma kutulari, dugum pivotlari
Koordinatlar build_kit.py ile ayni: Godot eksenleri, +Z on yuz, birim metre.
"""
import os
import sys
import json

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
os.environ.setdefault("KIT_TMP", os.path.join(os.path.dirname(HERE), "progress", "m1", "_kit_tmp"))
import bpy  # noqa: E402
import build_kit as bk  # noqa: E402
from build_kit import Kit  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT_DIR = os.path.join(ROOT, "game", "assets", "models", "placeables")
WOOD_DIR = bk.OUT_DIR
RES_WOOD = bk.RES_DIR

BLUE = (0.42, 0.62, 0.86)      # CoopBlue'ya yakin boyali kumas (doku carpani)
CREAM = (1.0, 0.97, 0.9)
ORANGE = (1.0, 0.78, 0.4)
CHAR = (0.32, 0.27, 0.24)


def p_sandik():
    """Sandik (20 yuva): kalas govde, kose kirisleri, demir seritler; kapak 'lid' dugumu (arka mentese)."""
    k = Kit("sandik")
    W, D, H = 1.1, 0.66, 0.56
    hw, hd = W / 2, D / 2
    k.box((-hw + 0.02, 0.0, -hd + 0.02), (hw - 0.02, 0.07, hd - 0.02), "plank", axis=0)  # taban
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.box((min(sx * hw, sx * (hw - 0.09)), 0.0, min(sz * hd, sz * (hd - 0.09))),
                  (max(sx * hw, sx * (hw - 0.09)), H, max(sz * hd, sz * (hd - 0.09))), "beam", axis=1, bevel=0.025)
    rows = 3
    for i in range(rows):
        y0 = 0.05 + i * (H - 0.07) / rows
        y1 = y0 + (H - 0.07) / rows - 0.012
        for sz in (-1, 1):  # on/arka
            k.box((-hw + 0.08, y0, min(sz * hd, sz * (hd - 0.06))), (hw - 0.08, y1, max(sz * hd, sz * (hd - 0.06))), "plank", axis=0, bevel=0.018)
        for sx in (-1, 1):  # yanlar
            k.box((min(sx * hw, sx * (hw - 0.06)), y0, -hd + 0.08), (max(sx * hw, sx * (hw - 0.06)), y1, hd - 0.08), "plank", axis=2, bevel=0.018)
    for x in (-0.33, 0.33):  # on seritler
        k.strap((x - 0.035, 0.04, hd), (x + 0.035, H - 0.02, hd + 0.014))
        k.bolt((x, 0.14, hd + 0.014), (0, 0, 1), r=0.022, h=0.012)
        k.bolt((x, H - 0.12, hd + 0.014), (0, 0, 1), r=0.022, h=0.012)
    # kapak: arka ust kenardan menteseli
    k.cur_node = "lid"
    k.nodes["lid"] = [0.0, H, -hd]
    n = 4
    for i in range(n):
        z0 = -hd - 0.02 + i * (D + 0.04) / n
        z1 = z0 + (D + 0.04) / n - 0.01
        k.box((-hw - 0.02, H, z0), (hw + 0.02, H + 0.08, z1), "plank", axis=0, bevel=0.02)
    for x in (-0.33, 0.33):
        k.strap((x - 0.035, H + 0.08, -hd - 0.02), (x + 0.035, H + 0.094, hd + 0.02))
    k.box((-0.07, H - 0.06, hd + 0.02), (0.07, H + 0.07, hd + 0.05), "metal", bevel=0.008, tint=(1, 1, 1))  # kilit
    k.cur_node = None
    k.col_box((-hw, 0, -hd), (hw, H + 0.09, hd))
    return k, dict(kind="placeable", name="Sandık", origin="bottom_center")


def p_uyku_tulumu():
    """Uyku Tulumu: alcak kalas palet uzerinde boyali kumas tulum ve rulo yastik."""
    k = Kit("uyku_tulumu")
    L, Wd = 2.0, 0.92
    hl, hw = L / 2, Wd / 2
    for x in (-hw + 0.08, hw - 0.08):  # kizaklar
        k.box((x - 0.06, 0.0, -hl), (x + 0.06, 0.09, hl), "beam", axis=2, bevel=0.025)
    n = 7
    for i in range(n):
        z = -hl + 0.12 + i * (L - 0.24) / (n - 1)
        k.box((-hw, 0.09, z - 0.09), (hw, 0.13, z + 0.09), "plank", axis=0, bevel=0.015)
    k.box((-hw + 0.05, 0.13, -hl + 0.06), (hw - 0.05, 0.27, hl - 0.38), "plank", axis=2, bevel=0.06, tint=BLUE)  # tulum
    k.box((-hw + 0.08, 0.27, -hl + 0.1), (hw - 0.08, 0.3, -0.05), "plank", axis=2, bevel=0.03, tint=(0.5, 0.7, 0.92))  # kivrik ust kat
    k.log((-hw + 0.06, 0.29, hl - 0.2), (hw - 0.06, 0.29, hl - 0.2), 0.15, sides=10, cls="plank", tint=CREAM)  # rulo yastik
    for x in (-0.25, 0.25):
        k.box((x - 0.03, 0.13, hl - 0.37), (x + 0.03, 0.45, hl - 0.03), "plank", axis=1, bevel=0.01, tint=ORANGE)  # baglar
    k.col_box((-hw, 0, -hl), (hw, 0.45, hl))
    return k, dict(kind="placeable", name="Uyku Tulumu", origin="bottom_center")


def p_mesale():
    """Mesale: yere cakili kutuk sap, demir halka, sarili bas (alev Godot'ta: isik + parcacik)."""
    k = Kit("mesale")
    k.log((0, -0.25, 0), (0, 1.5, 0), 0.055, sides=7, cls="beam", tip=0.0)
    k.log((0, 1.42, 0), (0, 1.68, 0), 0.105, sides=8, cls="plank", tint=CHAR)
    k.strap((-0.075, 1.38, -0.075), (0.075, 1.44, 0.075))
    k.strap((-0.115, 1.6, -0.115), (0.115, 1.64, 0.115))
    for a in range(3):  # yerde destek takozlari
        import math
        c, s = math.cos(a * 2.1), math.sin(a * 2.1)
        k.beam_between((c * 0.05, 0.28, s * 0.05), (c * 0.28, -0.02, s * 0.28), 0.06, 0.06)
    k.col_box((-0.12, 0, -0.12), (0.12, 1.7, 0.12))
    return k, dict(kind="placeable", name="Meşale", origin="bottom_center", flame=[0.0, 1.78, 0.0])


def p_kargo_kasasi():
    """Kargo kasasi (yuk nesnesi, GDD §13): 0,8 × 0,6 × 0,6 m aralikli citalar, kose kirisleri, kooperatif mavisi etiket."""
    k = Kit("kargo_kasasi")
    W, D, H = 0.8, 0.6, 0.6
    hw, hd = W / 2, D / 2
    k.box((-hw + 0.03, 0.0, -hd + 0.03), (hw - 0.03, 0.05, hd - 0.03), "plank", axis=0)  # taban
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.box((min(sx * hw, sx * (hw - 0.07)), 0.0, min(sz * hd, sz * (hd - 0.07))),
                  (max(sx * hw, sx * (hw - 0.07)), H, max(sz * hd, sz * (hd - 0.07))), "beam", axis=1, bevel=0.02)
    for y0, y1 in ((0.05, 0.19), (0.24, 0.38), (0.43, 0.57)):  # aralikli citalar
        for sz in (-1, 1):
            k.box((-hw + 0.07, y0, min(sz * hd, sz * (hd - 0.04))), (hw - 0.07, y1, max(sz * hd, sz * (hd - 0.04))), "plank", axis=0, bevel=0.014)
        for sx in (-1, 1):
            k.box((min(sx * hw, sx * (hw - 0.04)), y0, -hd + 0.07), (max(sx * hw, sx * (hw - 0.04)), y1, hd - 0.07), "plank", axis=2, bevel=0.014)
    for i in range(3):  # kapak citalari
        z0 = -hd + i * D / 3
        k.box((-hw, H - 0.04, z0 + 0.005), (hw, H, z0 + D / 3 - 0.005), "plank", axis=0, bevel=0.012)
    k.box((-0.15, 0.2, hd + 0.0), (0.15, 0.42, hd + 0.012), "plank", axis=0, bevel=0.006, tint=BLUE)  # etiket
    k.col_box((-hw, 0, -hd), (hw, H, hd))
    return k, dict(kind="load", name="Kargo Kasası", origin="bottom_center")


PIECES = [("sandik", p_sandik), ("uyku_tulumu", p_uyku_tulumu), ("mesale", p_mesale), ("kargo_kasasi", p_kargo_kasasi)]

IMPORT = ('[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[params]\n\n'
          'meshes/ensure_tangents=true\nmeshes/generate_lods=true\nmeshes/create_shadow_meshes=true\n'
          'gltf/embedded_image_handling=0\n'
          '_subresources={{\n"materials": {{\n"kabarma_wood": {{\n"use_external/enabled": true,\n'
          '"use_external/path": "{d}/wood_kit.tres",\n"use_external/fallback_path": "{d}/wood_kit.tres"\n'
          '}}\n}}\n}}\n')


def load_atlas():
    imgs = []
    for name, non_color in (("wood_atlas_albedo", False), ("wood_atlas_normal", True), ("wood_atlas_rm", True)):
        p = os.path.join(WOOD_DIR, name + ".png")
        if not os.path.exists(p):
            raise SystemExit(f"{p} yok: once pipeline/build_kit.py calistir")
        img = bpy.data.images.load(p)
        if non_color:
            img.colorspace_settings.name = "Non-Color"
        img.pack()
        imgs.append(img)
    return bk.make_material(*imgs)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in argv:
        only = set(argv[argv.index("--only") + 1].split(","))
    os.makedirs(OUT_DIR, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    mat = load_atlas()
    meta_path = os.path.join(OUT_DIR, "placeables.json")
    doc = {}
    if os.path.exists(meta_path):
        with open(meta_path, encoding="utf-8") as f:
            doc = json.load(f)
    for pid, fn in PIECES:
        if only and pid not in only:
            continue
        objs, meta = bk.build_piece(pid, fn, mat)
        bk.export_glb(objs, os.path.join(OUT_DIR, f"{pid}.glb"))
        with open(os.path.join(OUT_DIR, f"{pid}.glb.import"), "w", encoding="utf-8", newline="\n") as f:
            f.write(IMPORT.format(d=RES_WOOD))
        meta.pop("sockets", None)
        doc[pid] = meta
        print(f"PLACEABLE {pid:14s} tris={meta['tris']:5d} size={meta['size']}")
        for o in objs:
            bpy.data.objects.remove(o)
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    print("PLACEABLES DONE", len(doc))


if __name__ == "__main__":
    main()
