"""
Kabarma: hayvan akisinin (M2) ahsap yerlestirilebilirleri, yapi kitinin kalas diliyle (koddan, kredisiz; ICERIK 10.2).
  Suluk            4 Odun + 2 Tas   agilin su ihtiyaci: ayakli tekne, ici su yuzeyi (koyu mavi kalas)
  Kasinma Fircasi  3 Odun + 3 Lif   dikme direk, iki yanda lif firca tutamlari, capraz ayak
  Ari Kovani       8 Odun + 2 Lif   ayakli iki katli kutu kovan, egik cati, on ucus deligi esigi

placeables_kit.py'nin yolu: build_kit.py'nin Kit'i ve ortak ahsap atlasi (wood_kit.tres). Diger parcalara dokunmaz.

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/hayvan_props_kit.py

Cikti: game/assets/models/yerlestirilebilir/<id>.glb ve .glb.import, hayvan_kit_info.json.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
import build_kit as bk  # noqa: E402
import placeables_kit as pk  # noqa: E402


def p_suluk():
    """Suluk: alcak, genis tekne; dort kisa ayak, ici su (koyu kalas yuzey), kenar kirisleri."""
    k = bk.Kit("suluk")
    L, W, y0, y1 = 0.6, 0.3, 0.16, 0.46
    pk.legs(k, (-L + 0.06, L - 0.06), (-W + 0.04, W - 0.04), y0 + 0.02)
    k.box((-L, y0, -W), (L, y0 + 0.04, W), "plank", axis=0)
    for s in (-1, 1):
        k.box((-L, y0, s * W - (0.04 if s > 0 else 0)), (L, y1, s * W + (0.0 if s > 0 else 0.04)), "plank", axis=0)
        k.box((s * L - (0.04 if s > 0 else 0), y0, -W), (s * L + (0.0 if s > 0 else 0.04), y1, W), "plank", axis=2)
    k.box((-L + 0.04, y1 - 0.1, -W + 0.04), (L - 0.04, y1 - 0.08, W - 0.04), "roof", axis=0)  # su yuzeyi
    for s in (-1, 1):
        k.strap((-L - 0.01, (y0 + y1) / 2 - 0.03, s * (W + 0.005)), (L + 0.01, (y0 + y1) / 2 + 0.03, s * (W + 0.02)), rust=True)
    k.col_box((-L, 0, -W), (L, y1, W))
    return k, dict(kind="placeable", name="Suluk", origin="bottom_center")


def p_kasinma_fircasi():
    """Kasinma Fircasi: capraz ayakli direk, iki yanda bagli lif tutamlari (koyu kalas olarak)."""
    k = bk.Kit("kasinma_fircasi")
    k.box((-0.07, 0.0, -0.07), (0.07, 1.25, 0.07), "beam", axis=1, bevel=0.02)
    k.box((-0.45, 0.0, -0.05), (0.45, 0.1, 0.05), "beam", axis=0, bevel=0.015)
    k.box((-0.05, 0.0, -0.45), (0.05, 0.1, 0.45), "beam", axis=2, bevel=0.015)
    for s in (-1, 1):
        k.box((s * 0.07 - (0.12 if s < 0 else 0), 0.45, -0.12), (s * 0.07 + (0.12 if s > 0 else 0), 1.0, 0.12), "roof", axis=1, bevel=0.03)
        k.strap((s * 0.2 - 0.03, 0.5, -0.13), (s * 0.2 + 0.03, 0.56, 0.13), rust=True)
        k.strap((s * 0.2 - 0.03, 0.9, -0.13), (s * 0.2 + 0.03, 0.96, 0.13), rust=True)
    k.col_box((-0.25, 0, -0.15), (0.25, 1.25, 0.15))
    return k, dict(kind="placeable", name="Kaşınma Fırçası", origin="bottom_center")


def p_ari_kovani():
    """Ari Kovani: ayakli, iki kat kutu, egik cati, on ucus esigi."""
    k = bk.Kit("ari_kovani")
    X, D, y0, ym, y1 = 0.32, 0.3, 0.36, 0.72, 1.08
    pk.legs(k, (-X + 0.05, X - 0.05), (-D + 0.05, D - 0.05), y0 + 0.02)
    for (a, b) in ((y0, ym - 0.01), (ym, y1)):
        k.box((-X, a, -D), (X, b, -D + 0.04), "plank", axis=0)
        k.box((-X, a, D - 0.04), (X, b, D), "plank", axis=0)
        for s in (-1, 1):
            k.box((s * X - (0.04 if s > 0 else 0), a, -D), (s * X + (0.0 if s > 0 else 0.04), b, D), "plank", axis=2)
    k.box((-X - 0.02, ym - 0.02, -D - 0.02), (X + 0.02, ym + 0.02, D + 0.02), "beam", axis=0, bevel=0.01)
    k.box((-0.14, y0 + 0.02, D), (0.14, y0 + 0.05, D + 0.08), "beam", axis=0, bevel=0.01)  # ucus esigi
    k.obox((0, y1 + 0.06, 0.0), (1, 0, 0), (0, math.cos(0.25), math.sin(0.25)), 2 * X + 0.16, 2 * D + 0.16, 0.05, cls="roof", bevel=0.02)
    k.col_box((-X, 0, -D), (X, y1 + 0.15, D))
    return k, dict(kind="placeable", name="Arı Kovanı", origin="bottom_center")


PIECES = [("suluk", p_suluk), ("kasinma_fircasi", p_kasinma_fircasi), ("ari_kovani", p_ari_kovani)]


def main():
    os.makedirs(pk.OUT, exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    mat = bk.make_material(pk.load_atlas("wood_atlas_albedo", False), pk.load_atlas("wood_atlas_normal", True), pk.load_atlas("wood_atlas_rm", True))
    info = {}
    for pid, fn in PIECES:
        objs, meta = bk.build_piece(pid, fn, mat)
        bk.export_glb(objs, os.path.join(pk.OUT, f"{pid}.glb"))
        pk.write_import(pid)
        info[pid] = {key: meta[key] for key in ("name", "tris", "size", "bounds") if key in meta}
        flag = "" if meta["tris"] <= 1500 else "  <-- BUTCE DISI"
        print(f"PLACEABLE {pid:16s} tris={meta['tris']:5d} size={meta['size']}{flag}")
        for o in objs:
            bpy.data.objects.remove(o)
    with open(os.path.join(pk.OUT, "hayvan_kit_info.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=1)
    print("HAYVAN PROPS DONE", len(info))


if __name__ == "__main__":
    main()
