"""
Kabarma: hayvan akisinin M3 yerlestirilebilirleri (GDD 9.5, ICERIK 10.2), yapi kitinin kalas diliyle (koddan, kredisiz).
  Savas Agili Bayragi  6 Odun + 4 Lif      uzun direk, ucunda ucgen bez (koyu "roof" kalasi), dibinde tas yigini
  Ahir Zili            4 Odun + 2 Bakir K.  iki dikme, ust kiris, ortada asili zil (pas kayis)
  Baglama Diregi       3 Odun + 2 Lif      kalin kazik, ust halka ve sarili ip
  Eyerlik              5 Odun + 2 Deri     iki ayakli sehpa, ustunde eyer (koyu kalas egri sirt)

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/hayvan_m3_kit.py

Cikti: game/assets/models/yerlestirilebilir/<id>.glb ve .glb.import, hayvan_m3_kit_info.json.
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


def p_bayrak():
    """Savas Agili Bayragi: 2,4 m direk, ucunda ucgene yakin bez (iki egik levha), dibinde capraz ayak."""
    k = bk.Kit("savas_agili_bayragi")
    k.box((-0.05, 0.0, -0.05), (0.05, 2.4, 0.05), "beam", axis=1, bevel=0.015)
    k.box((-0.35, 0.0, -0.05), (0.35, 0.08, 0.05), "beam", axis=0, bevel=0.012)
    k.box((-0.05, 0.0, -0.35), (0.05, 0.08, 0.35), "beam", axis=2, bevel=0.012)
    k.box((0.05, 1.75, -0.015), (0.75, 2.3, 0.015), "roof", axis=0, bevel=0.01)          # bez
    k.obox((0.62, 2.02, 0.0), (1, 0, 0), (0, 1, 0), 0.3, 0.3, 0.03, cls="roof", bevel=0.01)  # ucu
    k.strap((-0.06, 2.28, -0.06), (0.06, 2.34, 0.06), rust=True)
    k.strap((-0.06, 1.72, -0.06), (0.06, 1.78, 0.06), rust=True)
    k.col_box((-0.12, 0, -0.12), (0.12, 2.4, 0.12))
    return k, dict(kind="placeable", name="Savaş Ağılı Bayrağı", origin="bottom_center")


def p_zil():
    """Ahir Zili: iki dikme ve ust kiris, kirise asili zil (kutu govde, pas kayislar)."""
    k = bk.Kit("ahir_zili")
    for s in (-1, 1):
        k.box((s * 0.4 - 0.05, 0.0, -0.05), (s * 0.4 + 0.05, 1.7, 0.05), "beam", axis=1, bevel=0.015)
    k.box((-0.5, 1.62, -0.06), (0.5, 1.74, 0.06), "beam", axis=0, bevel=0.015)
    k.box((-0.02, 1.4, -0.02), (0.02, 1.62, 0.02), "beam", axis=1)                     # askı
    k.box((-0.14, 1.1, -0.14), (0.14, 1.4, 0.14), "roof", axis=1, bevel=0.04)          # zil govdesi
    k.strap((-0.16, 1.1, -0.16), (0.16, 1.15, 0.16), rust=True)
    k.strap((-0.12, 1.36, -0.12), (0.12, 1.4, 0.12), rust=True)
    k.col_box((-0.5, 0, -0.1), (0.5, 1.74, 0.1))
    return k, dict(kind="placeable", name="Ahır Zili", origin="bottom_center")


def p_direk():
    """Baglama Diregi: kalin kazik, ust halka ve sarili ip."""
    k = bk.Kit("baglama_diregi")
    k.box((-0.09, 0.0, -0.09), (0.09, 1.15, 0.09), "beam", axis=1, bevel=0.03)
    k.box((-0.12, 1.1, -0.12), (0.12, 1.2, 0.12), "beam", axis=0, bevel=0.02)
    for y in (0.7, 0.78, 0.86):
        k.strap((-0.1, y, -0.1), (0.1, y + 0.04, 0.1), rust=False)
    k.obox((0.0, 1.0, 0.13), (1, 0, 0), (0, 1, 0), 0.16, 0.16, 0.03, cls="beam", bevel=0.01)  # halka
    k.col_box((-0.12, 0, -0.12), (0.12, 1.2, 0.12))
    return k, dict(kind="placeable", name="Bağlama Direği", origin="bottom_center")


def p_eyerlik():
    """Eyerlik: iki ayakli sehpa, ustunde egri sirtli eyer."""
    k = bk.Kit("eyerlik")
    L, y = 0.5, 0.75
    pk.legs(k, (-L + 0.06, L - 0.06), (-0.12, 0.12), y)
    k.box((-L, y, -0.08), (L, y + 0.08, 0.08), "beam", axis=0, bevel=0.015)
    for i, a in enumerate((-0.35, 0.0, 0.35)):
        k.obox((a * 0.6, y + 0.16 - abs(a) * 0.1, 0.0), (math.cos(a), math.sin(a), 0), (0, 0, 1), 0.3, 0.5, 0.05, cls="roof", bevel=0.02)
    k.strap((-0.03, y - 0.25, -0.26), (0.03, y + 0.1, -0.24), rust=True)
    k.strap((-0.03, y - 0.25, 0.24), (0.03, y + 0.1, 0.26), rust=True)
    k.col_box((-L, 0, -0.26), (L, y + 0.3, 0.26))
    return k, dict(kind="placeable", name="Eyerlik", origin="bottom_center")


PIECES = [("savas_agili_bayragi", p_bayrak), ("ahir_zili", p_zil), ("baglama_diregi", p_direk), ("eyerlik", p_eyerlik)]


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
        print(f"PLACEABLE {pid:20s} tris={meta['tris']:5d} size={meta['size']}{flag}")
        for o in objs:
            bpy.data.objects.remove(o)
    with open(os.path.join(pk.OUT, "hayvan_m3_kit_info.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=1)
    print("HAYVAN M3 PROPS DONE", len(info))


if __name__ == "__main__":
    main()
