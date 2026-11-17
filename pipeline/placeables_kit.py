"""
Kabarma: ilk gunun ahsap yerlestirilebilirleri, yapi kitinin kalas diliyle (URETIM 6 M1 karari: koddan, kredisiz).
  Yemlik      6 Odun          agilda; icine yem konur (ICERIK 10.2)
  Folluk      4 Odun + 4 Lif  agildaki Gidak buraya yumurtlar, 6 yuva
  Ahsap Kapi  4 Odun          Kapi Kasasi'na oturur (aciklik 1,5 x 2,3 m), E ile acilir; orijin kanadin alt ortasi

build_kit.py'nin parca tezgahini (Kit, kalas/kiris kesitleri, pah, civata, demir serit) ve ortak ahsap atlasini kullanir;
dokuyu yeniden uretmez, game/assets/models/build/wood/wood_atlas_*.png'den okur. Godot'ta ayni wood_kit.tres malzemesi.

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/placeables_kit.py

Cikti: game/assets/models/yerlestirilebilir/<id>.glb ve .glb.import, kit_info.json (olcu ve ucgen sayisi; bilgi).
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import build_kit as bk  # noqa: E402
from mathutils import Vector  # noqa: E402

OUT = os.path.join(bk.ROOT, "game", "assets", "models", "yerlestirilebilir")
RES_WOOD = "res://assets/models/build/wood"


def legs(k, xs, zs, y1, hw=0.045):
    for x in xs:
        for z in zs:
            k.box((x - hw, 0.0, z - hw), (x + hw, y1, z + hw), "beam", axis=1, bevel=0.015)


def p_yemlik():
    """Yemlik: dort ayakli uzun tekne, ici tahta; kenar kirisleri ve civatalar."""
    k = bk.Kit("yemlik")
    L, W, y0, y1 = 0.66, 0.22, 0.28, 0.56
    legs(k, (-L + 0.06, L - 0.06), (-W + 0.02, W - 0.02), y0 + 0.02)
    k.box((-L, y0, -W), (L, y0 + 0.04, W), "plank", axis=0)                       # taban
    for s in (-1, 1):
        k.box((-L, y0, s * W - (0.04 if s > 0 else 0)), (L, y1, s * W + (0.0 if s > 0 else 0.04)), "plank", axis=0)  # yan
        k.box((s * L - (0.04 if s > 0 else 0), y0, -W - 0.03), (s * L + (0.0 if s > 0 else 0.04), y1 + 0.04, W + 0.03), "plank", axis=2)  # uc
    for s in (-1, 1):
        k.box((-L - 0.02, y1, s * W - 0.05), (L + 0.02, y1 + 0.05, s * W + 0.05), "beam", axis=0, bevel=0.015)  # kenar
    k.box((-L + 0.06, 0.12, -0.04), (L - 0.06, 0.18, 0.04), "beam", axis=0, bevel=0.015)  # ayak baglantisi
    for s in (-1, 1):
        for x in (-L + 0.2, L - 0.2):
            k.bolt((x, (y0 + y1) / 2, s * (W + 0.005)), (0, 0, s), r=0.022, h=0.015)
    k.col_box((-L, 0, -W - 0.03), (L, y1 + 0.05, W + 0.03))
    return k, dict(kind="placeable", name="Yemlik", origin="bottom_center")


def p_folluk():
    """Folluk: ayakli, iki kat uc gozlu (6 yuva) yumurtlama kutusu; egik cati tahtasi, on esikler."""
    k = bk.Kit("folluk")
    X, D, y0, ym, y1 = 0.62, 0.26, 0.32, 0.74, 1.16
    legs(k, (-X + 0.06, X - 0.06), (-D + 0.06, D - 0.06), y0 + 0.02)
    k.box((-X, y0, -D), (X, y0 + 0.04, D), "plank", axis=0)            # taban
    k.box((-X, ym, -D), (X, ym + 0.04, D - 0.02), "plank", axis=0)     # raf
    k.box((-X, y0, -D), (X, y1, -D + 0.04), "plank", axis=0)            # arka
    for s in (-1, 1):
        k.box((s * X - (0.04 if s > 0 else 0), y0, -D), (s * X + (0.0 if s > 0 else 0.04), y1, D), "plank", axis=1)  # yan
        k.box((s * 0.21 - 0.02, y0 + 0.04, -D + 0.04), (s * 0.21 + 0.02, y1, D - 0.03), "plank", axis=1)            # bolme
    for y in (y0 + 0.04, ym + 0.04):
        k.box((-X, y, D - 0.04), (X, y + 0.1, D), "beam", axis=0, bevel=0.015)  # on esik
    # egik cati: arkada yuksek, onde alcak
    k.obox((0, y1 + 0.1, 0.0), (1, 0, 0), (0, math.cos(0.32), math.sin(0.32)), 2 * X + 0.16, 2 * D + 0.18, 0.05, cls="roof", bevel=0.02)
    for x in (-X + 0.12, 0.0, X - 0.12):
        k.bolt((x, y1 - 0.06, D + 0.005), (0, 0, 1), r=0.02, h=0.015)
    k.col_box((-X, 0, -D), (X, y1 + 0.18, D))
    return k, dict(kind="placeable", name="Folluk", origin="bottom_center")


def p_ahsap_kapi():
    """Ahsap Kapi: dikey kalaslar, iki yatay kusak ve capraz destek (arkada), demir mentese seritleri, kol."""
    k = bk.Kit("ahsap_kapi")
    hw, y0, y1, t = 0.73, 0.02, 2.27, 0.045
    n = 5
    for i in range(n):
        x0 = -hw + 2 * hw * i / n
        x1 = -hw + 2 * hw * (i + 1) / n - 0.008
        k.box((x0, y0, -t), (x1, y1 - k.rng.uniform(0, 0.02), t), "plank", axis=1)
    for y in (0.35, 1.85):
        k.box((-hw + 0.06, y, -t - 0.05), (hw - 0.06, y + 0.16, -t), "beam", axis=0, bevel=0.02)
    k.beam_between((-hw + 0.12, 0.5, -t - 0.03), (hw - 0.12, 1.86, -t - 0.03), 0.12, 0.05, up=bk.Z)
    for y in (0.4, 1.9):
        k.strap((-hw - 0.02, y, t), (-hw + 0.55, y + 0.07, t + 0.02), rust=True)
    k.strap((hw - 0.16, 1.0, t), (hw - 0.1, 1.16, t + 0.06))
    k.bolt((hw - 0.13, 1.08, t + 0.06), (0, 0, 1), r=0.03, h=0.03)
    k.col_box((-hw, y0, -t - 0.05), (hw, y1, t))
    return k, dict(kind="placeable", name="Ahşap Kapı", origin="bottom_center", hinge=[-0.75, 0, 0],
                   note="Kapi Kasasi acikligi 1,5 x 2,3 m; mentese kanadin sol kenari (x=-0,75).")


PIECES = [("yemlik", p_yemlik), ("folluk", p_folluk), ("ahsap_kapi", p_ahsap_kapi)]


def load_atlas(name, non_color):
    path = os.path.join(bk.OUT_DIR, name + ".png")
    img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pack()
    return img


def write_import(pid):
    p = os.path.join(OUT, pid + ".glb.import")
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write('[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[params]\n\n'
                'meshes/ensure_tangents=true\nmeshes/generate_lods=true\nmeshes/create_shadow_meshes=true\n'
                'gltf/embedded_image_handling=0\n'
                '_subresources={\n"materials": {\n"kabarma_wood": {\n"use_external/enabled": true,\n'
                f'"use_external/path": "{RES_WOOD}/wood_kit.tres",\n"use_external/fallback_path": "{RES_WOOD}/wood_kit.tres"\n'
                '}\n}\n}\n')


def main():
    os.makedirs(OUT, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    mat = bk.make_material(load_atlas("wood_atlas_albedo", False), load_atlas("wood_atlas_normal", True), load_atlas("wood_atlas_rm", True))
    info = {}
    for pid, fn in PIECES:
        objs, meta = bk.build_piece(pid, fn, mat)
        bk.export_glb(objs, os.path.join(OUT, f"{pid}.glb"))
        write_import(pid)
        info[pid] = {key: meta[key] for key in ("name", "tris", "size", "bounds") if key in meta}
        flag = "" if meta["tris"] <= 1500 else "  <-- BUTCE DISI"
        print(f"PLACEABLE {pid:12s} tris={meta['tris']:5d} size={meta['size']}{flag}")
        for o in objs:
            bpy.data.objects.remove(o)
    with open(os.path.join(OUT, "kit_info.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=1)
    print("PLACEABLES DONE", len(info))


if __name__ == "__main__":
    main()
