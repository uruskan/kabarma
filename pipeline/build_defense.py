"""
Kabarma: savunma yapilari (M3, YER TUTUCU): uc kule ve dort tuzak, yapi kitinin kalas diliyle; kredi yok.
  civi_kulesi    Civi Kulesi: hizli tek hedef, bakir namlulu civi tabancasi basi, civi kutusu
  sapan_kulesi   Sapan Kulesi: Y catalli tahta sapan, deri cep, tas sepeti (tas firlatir)
  sacma_kulesi   Sacma Kulesi (T2): demir kaplama, kisa genis agizli sacma namlusu, barut fici
  diken_tarlasi  Diken Tarlasi: ahsap cerceve, demir ve ahsap diken siralari
  yapiskan_zemin Yapiskan Zemin: ahsap cerceve, parlak yesil-amber yapiskan tabaka ("goo" dugumu)
  firlatma_pedi  Firlatma Pedi: ahsap kasa, yaylar, firlatma plakasi ("plate" dugumu, pivot plakanin alt ortasi)
  cukur_kapagi   Cukur Kapagi: cerceve, iki kapak kanadi ("leaf_l", "leaf_r"; pivot dis mentese kenari, Godot Z
                 ekseni etrafinda donunce asagi acilir), zeminin altinda 1 m karanlik cukur ("pit")

Kuleler: donen bas ayri "head" dugumu (pivot tabanin ustunde, yaw ekseninde), icinde namlu ucunda bos "muzzle"
dugumu (sapanda cepte). On yuz Godot -Z (namlu -Z'ye bakar).

build_kit.py'nin parca tezgahini (Kit, kalas/kiris, pah, civata, demir serit) ve ortak ahsap atlasini kullanir;
dokuyu yeniden uretmez (game/assets/models/build/wood/wood_atlas_*.png). Godot'ta ortak wood_kit.tres malzemesi;
yalniz "goo" kendi gomulu parlak malzemesini (kabarma_goo) tasir.

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/build_defense.py -- [--only civi_kulesi,cukur_kapagi] [--no-sheet]

Cikti:
  game/assets/models/defense/<id>.glb ve .glb.import
  game/assets/models/defense/defense.json   ucgen sayisi, sinirlar, carpisma kutulari, dugum pivotlari
  progress/m3/savunma/defense_models_sheet.png  temas foyu (kule baslari 150 derece dondurulmus, cukur kapaklari 40 derece acik, plaka 12 cm kalkik)
"""
import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
os.environ.setdefault("KIT_TMP", os.path.join(os.path.dirname(HERE), "progress", "m3", "savunma", "_kit_tmp"))

import bpy  # noqa: E402
import build_kit as bk  # noqa: E402
from mathutils import Vector  # noqa: E402

ROOT = bk.ROOT
OUT = os.path.join(ROOT, "game", "assets", "models", "defense")
SHEET = os.path.join(ROOT, "progress", "m3", "savunma", "defense_models_sheet.png")
RES_WOOD = bk.RES_DIR

COPPER = (1.0, 0.62, 0.40)
IRON = (0.62, 0.64, 0.70)
DARK_IRON = (0.40, 0.41, 0.46)
LEATHER = (0.62, 0.40, 0.26)
RUBBER = (0.42, 0.20, 0.16)
STONE = (0.80, 0.76, 0.80)      # palette rock 9C909B'ye yakin (doku carpani)
WARN = (1.0, 0.76, 0.24)        # palette warning F2B632
PIT = (0.30, 0.22, 0.20)        # palette soil koyusu
SOOT = (0.08, 0.07, 0.07)
GOO = (0.50, 0.56, 0.08, 1.0)   # yesil-amber

HB = 1.5   # kule tabaninin ust yuzu (head pivotu)


def metal_log(k, p0, p1, r, sides=8, tint=IRON, rust=False, **kw):
    p = k.log(p0, p1, r, sides=sides, cls="metal", tint=tint, **kw)
    p.rust = rust
    return p


def metal_box(k, mn, mx, tint=IRON, rust=False, bevel=0.01):
    p = k.box(mn, mx, cls="metal", bevel=bevel, tint=tint)
    p.rust = rust
    return p


# ---------------------------------------------------------------------------
# Kule tabani ve donen tabla
# ---------------------------------------------------------------------------
def tower_base(k, iron=False):
    """1,2 m ayak izi: dort egik ayak, iki kat kusak, capraz destek, ustte kalas tabla."""
    foot, top = 0.54, 0.31

    def off(y):
        return foot + (top - foot) * (y / HB)

    for sx in (-1, 1):
        for sz in (-1, 1):
            k.beam_between((sx * foot, -0.02, sz * foot), (sx * top, HB - 0.06, sz * top), 0.13, 0.13)
            k.box((sx * foot - 0.11, 0.0, sz * foot - 0.11), (sx * foot + 0.11, 0.07, sz * foot + 0.11), "beam", bevel=0.02)  # ayak takozu
            if iron:
                metal_box(k, (sx * foot - 0.12, 0.07, sz * foot - 0.12), (sx * foot + 0.12, 0.1, sz * foot + 0.12), tint=DARK_IRON, rust=True)
    for y in (0.5, HB - 0.42):
        o = off(y + 0.05)
        for s in (-1, 1):
            k.box((-o - 0.04, y, s * o - 0.05), (o + 0.04, y + 0.1, s * o + 0.05), "beam", axis=0, bevel=0.02)
            k.box((s * o - 0.05, y, -o - 0.04), (s * o + 0.05, y + 0.1, o + 0.04), "beam", axis=2, bevel=0.02)
    # on ve arkada capraz destek
    for s in (-1, 1):
        a, b = off(0.6), off(HB - 0.45)
        k.beam_between((-a + 0.05, 0.6, s * (a + 0.02)), (b - 0.05, HB - 0.45, s * (b + 0.02)), 0.09, 0.05, up=bk.Z)
    # ust tabla
    n = 5
    w = 0.86 / n
    for i in range(n):
        z0 = -0.43 + i * w
        k.box((-0.45, HB - 0.08, z0 + 0.005), (0.45, HB, z0 + w - 0.005), "plank", axis=0, bevel=0.018)
    for s in (-1, 1):
        k.box((-0.47, HB - 0.16, s * 0.43 - 0.05), (0.47, HB - 0.07, s * 0.43 + 0.05), "beam", axis=0, bevel=0.02)
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.bolt((sx * 0.38, HB, sz * 0.38), (0, 1, 0), r=0.025, h=0.012)
    if iron:
        for s in (-1, 1):
            metal_box(k, (-0.48, HB - 0.15, s * 0.48 - 0.012 * (s < 0)), (0.48, HB - 0.08, s * 0.48 + 0.012 * (s > 0)), tint=DARK_IRON)
            metal_box(k, (s * 0.48 - 0.012 * (s < 0), HB - 0.15, -0.48), (s * 0.48 + 0.012 * (s > 0), HB - 0.08, 0.48), tint=DARK_IRON)
        y = 0.55
        o = off(y)
        for s in (-1, 1):
            k.bolt((-o + 0.02, y + 0.05, s * (o + 0.05)), (0, 0, s), r=0.028, h=0.015)
            k.bolt((o - 0.02, y + 0.05, s * (o + 0.05)), (0, 0, s), r=0.028, h=0.015)
    k.col_box((-0.6, 0, -0.6), (0.6, HB, 0.6))


def turntable(k, tint=IRON):
    """head dugumunun alt tablasi: demir bilezik ve kalin tahta disk."""
    metal_log(k, (0, HB, 0), (0, HB + 0.04, 0), 0.36, sides=12, tint=tint)
    k.log((0, HB + 0.04, 0), (0, HB + 0.12, 0), 0.32, sides=12, cls="beam")


def start_head(k):
    k.cur_node = "head"
    k.nodes["head"] = [0.0, HB, 0.0]


# ---------------------------------------------------------------------------
# Kuleler
# ---------------------------------------------------------------------------
def p_civi_kulesi():
    k = bk.Kit("civi_kulesi")
    tower_base(k)
    start_head(k)
    turntable(k)
    y = HB + 0.12
    for s in (-1, 1):  # catal
        k.box((s * 0.25 - 0.04, y, -0.1), (s * 0.25 + 0.04, y + 0.42, 0.1), "plank", axis=1, bevel=0.02)
        k.bolt((s * 0.29, y + 0.33, 0), (s, 0, 0), r=0.035, h=0.02)
    by = y + 0.33
    k.box((-0.2, by - 0.15, -0.34), (0.2, by + 0.15, 0.34), "beam", axis=2, bevel=0.035)  # govde
    for z in (-0.26, 0.24):
        metal_box(k, (-0.215, by - 0.165, z - 0.03), (0.215, by + 0.165, z + 0.03), rust=z > 0)
    # bakir namlu, demir halkalar, agiz
    metal_log(k, (0, by, -0.34), (0, by, -0.94), 0.065, sides=8, tint=COPPER)
    for z in (-0.46, -0.72):
        metal_log(k, (0, by, z), (0, by, z - 0.05), 0.085, sides=8, tint=DARK_IRON)
    metal_log(k, (0, by, -0.9), (0, by, -0.98), 0.09, sides=8, tint=DARK_IRON)
    metal_log(k, (0, by, -0.975), (0, by, -0.985), 0.05, sides=8, tint=SOOT)
    # civi kutusu (sag) ve besleme olugu
    k.box((0.2, by - 0.12, -0.16), (0.48, by + 0.16, 0.18), "plank", axis=2, bevel=0.02)
    metal_box(k, (0.19, by + 0.13, -0.17), (0.49, by + 0.17, 0.19))
    for i in range(6):
        x = 0.27 + (i % 3) * 0.07
        z = -0.06 + (i // 3) * 0.13
        metal_log(k, (x, by + 0.17, z), (x, by + 0.27, z), 0.012, sides=4, tint=IRON)
        metal_log(k, (x, by + 0.27, z), (x, by + 0.29, z), 0.03, sides=6, tint=IRON)
    metal_box(k, (0.18, by + 0.0, -0.1), (0.22, by + 0.08, 0.1), tint=COPPER)
    # sol krank
    metal_log(k, (-0.2, by, 0.12), (-0.36, by, 0.12), 0.025, sides=6)
    k.beam_between((-0.36, by, 0.12), (-0.36, by - 0.2, 0.2), 0.05, 0.05)
    k.log((-0.4, by - 0.2, 0.2), (-0.31, by - 0.2, 0.2), 0.035, sides=6, cls="plank")
    # arka nisangah plakasi
    metal_box(k, (-0.06, by + 0.15, 0.2), (0.06, by + 0.27, 0.23), tint=DARK_IRON)
    k.cur_node = None
    k.col_box((-0.45, HB, -0.95), (0.5, by + 0.3, 0.4), node="head")
    return k, dict(kind="placeable", name="Ã‡ivi Kulesi", origin="bottom_center", placeholder=True,
                   muzzle=[0.0, by, -0.99])


def p_sapan_kulesi():
    k = bk.Kit("sapan_kulesi")
    tower_base(k)
    start_head(k)
    turntable(k)
    y = HB + 0.12
    k.box((-0.2, y, -0.22), (0.2, y + 0.18, 0.2), "beam", axis=0, bevel=0.03)  # kaide
    ty = y + 0.18
    fy = ty + 0.42
    k.log((0, ty - 0.05, -0.05), (0, fy, -0.08), 0.085, sides=7, cls="beam")      # govde
    tip_y = fy + 0.42
    tips = []
    for s in (-1, 1):
        t = (s * 0.36, tip_y, -0.12)
        tips.append(t)
        k.log((0, fy - 0.06, -0.08), t, 0.068, sides=7, cls="beam", tip=0.05, tip_r=0.75)
        metal_box(k, (t[0] - 0.08, t[1] - 0.16, t[2] - 0.08), (t[0] + 0.08, t[1] - 0.1, t[2] + 0.08), rust=True)
    metal_box(k, (-0.11, fy - 0.12, -0.19), (0.11, fy - 0.04, 0.03))  # catal bilezigi
    k.beam_between((0, ty, 0.18), (0, fy - 0.15, -0.02), 0.08, 0.06, up=bk.X)  # arka payanda
    # lastik bantlar ve deri cep (geri cekilmis: +Z)
    py, pz = fy + 0.2, 0.42
    for s, t in zip((-1, 1), tips):
        k.beam_between((t[0], t[1] - 0.04, t[2]), (s * 0.12, py + 0.02, pz - 0.04), 0.035, 0.035, cls="plank", tint=RUBBER)
    k.box((-0.15, py - 0.06, pz - 0.07), (0.15, py + 0.07, pz + 0.07), "plank", axis=0, bevel=0.035, tint=LEATHER)
    stone = metal_log(k, (0, py + 0.02, pz - 0.1), (0, py + 0.02, pz + 0.1), 0.1, sides=6, tint=STONE, tip=0.05, tip_r=0.55, cap0=True)
    # yan tas sepeti
    k.box((0.22, y, -0.12), (0.46, y + 0.06, 0.18), "plank", axis=2, bevel=0.015)
    for s in (-1, 1):
        k.box((0.22, y + 0.06, s * 0.15 - 0.02 + 0.03), (0.46, y + 0.16, s * 0.15 + 0.02 + 0.03), "plank", axis=0, bevel=0.012)
    k.box((0.44, y + 0.06, -0.12), (0.46, y + 0.16, 0.18), "plank", axis=2, bevel=0.008)
    for i, (x, z) in enumerate(((0.29, -0.03), (0.38, 0.08), (0.36, -0.06))):
        metal_log(k, (x, y + 0.08, z - 0.06 - 0.01 * i), (x + 0.01, y + 0.16, z + 0.05), 0.065, sides=6, tint=STONE, tip=0.03, tip_r=0.5, cap0=True)
    k.cur_node = None
    k.col_box((-0.45, HB, -0.25), (0.47, tip_y + 0.08, 0.52), node="head")
    del stone
    return k, dict(kind="placeable", name="Sapan Kulesi", origin="bottom_center", placeholder=True,
                   muzzle=[0.0, py + 0.02, pz])


def p_sacma_kulesi():
    k = bk.Kit("sacma_kulesi")
    tower_base(k, iron=True)
    start_head(k)
    turntable(k, tint=DARK_IRON)
    y = HB + 0.12
    for s in (-1, 1):
        k.box((s * 0.29 - 0.05, y, -0.12), (s * 0.29 + 0.05, y + 0.46, 0.12), "beam", axis=1, bevel=0.025)
        metal_box(k, (s * 0.29 - 0.06, y + 0.3, -0.13), (s * 0.29 + 0.06, y + 0.36, 0.13), tint=DARK_IRON)
        k.bolt((s * 0.35, y + 0.36, 0), (s, 0, 0), r=0.045, h=0.025)
    by = y + 0.34
    k.box((-0.23, by - 0.18, -0.3), (0.23, by + 0.18, 0.4), "beam", axis=2, bevel=0.04)
    for z in (-0.22, 0.08, 0.32):  # demir kusaklar
        metal_box(k, (-0.245, by - 0.195, z - 0.04), (0.245, by + 0.195, z + 0.04), tint=DARK_IRON, rust=z > 0.2)
    for s in (-1, 1):  # yan kaplama
        metal_box(k, (s * 0.245 - 0.012 * (s < 0), by - 0.12, -0.14), (s * 0.245 + 0.012 * (s > 0), by + 0.12, 0.0), tint=IRON)
        for z in (-0.12, -0.02):
            k.bolt((s * 0.26, by + 0.08, z), (s, 0, 0), r=0.02, h=0.012)
    # kisa kalin namlu ve genis agiz
    metal_log(k, (0, by, -0.3), (0, by, -0.62), 0.12, sides=10, tint=IRON)
    metal_log(k, (0, by, -0.4), (0, by, -0.46), 0.145, sides=10, tint=DARK_IRON)
    metal_log(k, (0, by, -0.92), (0, by, -0.58), 0.25, sides=12, tint=IRON, tip=0.3, tip_r=0.46, cap0=True)
    metal_log(k, (0, by, -0.94), (0, by, -0.88), 0.27, sides=12, tint=COPPER, cap0=True)
    metal_log(k, (0, by, -0.935), (0, by, -0.95), 0.2, sides=12, tint=SOOT)
    # barut fici (sag) ve sacma torbasi (sol)
    k.log((0.38, y + 0.02, 0.2), (0.38, y + 0.36, 0.2), 0.13, sides=10, cls="plank", cap0=True)
    for yy in (y + 0.06, y + 0.29):
        metal_log(k, (0.38, yy, 0.2), (0.38, yy + 0.035, 0.2), 0.137, sides=10, tint=DARK_IRON)
    k.box((-0.46, y + 0.0, 0.08), (-0.3, y + 0.2, 0.3), "plank", axis=1, bevel=0.06, tint=LEATHER)
    k.cur_node = None
    k.col_box((-0.5, HB, -0.95), (0.52, by + 0.28, 0.42), node="head")
    return k, dict(kind="placeable", name="SaÃ§ma Kulesi", origin="bottom_center", placeholder=True, tier=2,
                   muzzle=[0.0, by, -0.96])


# ---------------------------------------------------------------------------
# Tuzaklar
# ---------------------------------------------------------------------------
def rim_frame(k, half, h, w=0.12, bolts=True):
    """Yere oturan kare cerceve: dort kiris (bindirmeli), kose civatalari."""
    for s in (-1, 1):
        k.box((-half, 0.0, s * half - w / 2 - s * w / 2), (half, h, s * half + w / 2 - s * w / 2), "beam", axis=0, bevel=0.025)
        k.box((s * half - w / 2 - s * w / 2, 0.0, -half + w), (s * half + w / 2 - s * w / 2, h, half - w), "beam", axis=2, bevel=0.025)
    if bolts:
        for sx in (-1, 1):
            for sz in (-1, 1):
                k.bolt((sx * (half - w / 2), h, sz * (half - w / 2)), (0, 1, 0), r=0.028, h=0.014)


def bed_planks(k, half, y0, y1, n=6):
    w = 2 * half / n
    for i in range(n):
        z0 = -half + i * w
        k.box((-half, y0, z0 + 0.006), (half, y1, z0 + w - 0.006), "plank", axis=0, bevel=0.015)


def p_diken_tarlasi():
    k = bk.Kit("diken_tarlasi")
    H = 0.9
    rim_frame(k, H, 0.12)
    bed_planks(k, H - 0.12, 0.0, 0.05)
    for z in (-0.4, 0.4):
        k.box((-H + 0.12, 0.05, z - 0.05), (H - 0.12, 0.09, z + 0.05), "beam", axis=0, bevel=0.015)
    n = 5
    for i in range(n):
        for j in range(n):
            x = -0.6 + 1.2 * i / (n - 1) + k.rng.uniform(-0.03, 0.03)
            z = -0.6 + 1.2 * j / (n - 1) + (0.08 if i % 2 else -0.04)
            lean = (k.rng.uniform(-0.03, 0.03), 0, k.rng.uniform(-0.03, 0.03))
            top = (x + lean[0], 0.24, z + lean[2])
            if (i + j) % 2 == 0:
                metal_log(k, (x, 0.04, z), top, 0.035, sides=4, tint=IRON, tip=0.09, tip_r=0.18, rust=k.rng.random() < 0.4)
                metal_box(k, (x - 0.05, 0.05, z - 0.05), (x + 0.05, 0.065, z + 0.05), tint=DARK_IRON)
            else:
                k.log((x, 0.04, z), (top[0], 0.22, top[2]), 0.04, sides=5, cls="beam", tip=0.08, tip_r=0.22)
    k.col_box((-H, 0, -H), (H, 0.24, H))
    return k, dict(kind="placeable", name="Diken TarlasÄ±", origin="bottom_center", placeholder=True)


def p_yapiskan_zemin():
    k = bk.Kit("yapiskan_zemin")
    H = 0.9
    rim_frame(k, H, 0.13)
    bed_planks(k, H - 0.12, 0.0, 0.04)
    k.cur_node = "goo"
    k.nodes["goo"] = [0.0, 0.04, 0.0]
    g = H - 0.12
    k.box((-g, 0.04, -g), (g, 0.11, g), "plank", axis=0, bevel=0.03, tint=(1, 1, 1))
    for x, z, r, h in ((-0.4, -0.3, 0.16, 0.035), (0.3, 0.35, 0.2, 0.03), (0.42, -0.42, 0.11, 0.04), (-0.25, 0.42, 0.12, 0.03), (0.05, -0.05, 0.09, 0.045)):
        k.log((x, 0.1, z), (x, 0.11 + h, z), r, sides=8, cls="plank", tint=(1, 1, 1), tip=h * 0.6, tip_r=0.55)
    for x, z in ((-0.55, g), (0.2, -g), (g, 0.1)):  # cerceveden sarkan damlalar
        dx = 0.06 if abs(x) >= g - 1e-6 else 0.0
        dz = 0.06 if abs(z) >= g - 1e-6 else 0.0
        sx = 1 if x > 0 else -1
        sz = 1 if z > 0 else -1
        k.box((x - 0.07 + (sx * dx if dx else 0), 0.1, z - 0.07 + (sz * dz if dz else 0)),
              (x + 0.07 + (sx * dx if dx else 0), 0.145, z + 0.07 + (sz * dz if dz else 0)), "plank", axis=0, bevel=0.02, tint=(1, 1, 1))
    k.cur_node = None
    k.col_box((-H, 0, -H), (H, 0.13, H))
    return k, dict(kind="placeable", name="YapÄ±ÅŸkan Zemin", origin="bottom_center", placeholder=True)


def p_firlatma_pedi():
    k = bk.Kit("firlatma_pedi")
    H, BH = 0.8, 0.18
    for sx in (-1, 1):  # kose dikmeleri
        for sz in (-1, 1):
            k.box((sx * H - 0.06 - sx * 0.06, 0.0, sz * H - 0.06 - sz * 0.06), (sx * H + 0.06 - sx * 0.06, BH, sz * H + 0.06 - sz * 0.06), "beam", axis=1, bevel=0.02)
    for row in range(2):  # yan kalaslar
        y0 = row * BH / 2 + 0.005
        y1 = y0 + BH / 2 - 0.01
        for s in (-1, 1):
            k.box((-H + 0.12, y0, s * H - 0.04 * (s > 0)), (H - 0.12, y1, s * H + 0.04 * (s < 0)), "plank", axis=0, bevel=0.015)
            k.box((s * H - 0.04 * (s > 0), y0, -H + 0.12), (s * H + 0.04 * (s < 0), y1, H - 0.12), "plank", axis=2, bevel=0.015)
    bed_planks(k, H - 0.04, 0.0, 0.03, n=5)
    for sx in (-1, 1):  # yaylar
        for sz in (-1, 1):
            x, z = sx * 0.42, sz * 0.42
            metal_log(k, (x, 0.03, z), (x, BH, z), 0.05, sides=8, tint=DARK_IRON)
            for i in range(3):
                yy = 0.05 + i * 0.045
                metal_log(k, (x, yy, z), (x, yy + 0.02, z), 0.085, sides=8, tint=IRON, rust=i == 1)
    for s in (-1, 1):
        metal_box(k, (s * H - 0.012 * (s < 0) - 0.0, 0.05, -0.12), (s * H + 0.012 * (s > 0), 0.13, 0.12), tint=WARN)
    k.cur_node = "plate"
    k.nodes["plate"] = [0.0, BH, 0.0]
    P = H - 0.07
    n = 5
    w = 2 * P / n
    for i in range(n):
        x0 = -P + i * w
        k.box((x0 + 0.006, BH, -P), (x0 + w - 0.006, BH + 0.045, P), "plank", axis=2, bevel=0.015)
    for z in (-0.5, 0.5):
        metal_box(k, (-P - 0.01, BH + 0.045, z - 0.04), (P + 0.01, BH + 0.058, z + 0.04), rust=z > 0)
        for x in (-0.55, 0.55):
            k.bolt((x, BH + 0.058, z), (0, 1, 0), r=0.022, h=0.01)
    for zc in (-0.2, 0.15):  # ileri (-Z) bakan isaret oklari
        for s in (-1, 1):
            k.beam_between((0, BH + 0.05, zc - 0.22), (s * 0.3, BH + 0.05, zc + 0.08), 0.09, 0.02, cls="plank", tint=WARN, bevel=0.006)
    k.cur_node = None
    k.col_box((-H, 0, -H), (H, BH + 0.06, H))
    return k, dict(kind="placeable", name="FÄ±rlatma Pedi", origin="bottom_center", placeholder=True)


def p_cukur_kapagi():
    k = bk.Kit("cukur_kapagi")
    O, I, FH = 0.9, 0.7, 0.12
    w = O - I
    rim_frame(k, O, FH, w=w)
    for s in (-1, 1):  # kapaklarin oturdugu ic pervaz
        k.box((-I, FH - 0.07, s * I - 0.04 * (s > 0)), (I, FH - 0.04, s * I + 0.04 * (s < 0)), "plank", axis=0, bevel=0.008)
    for s in (-1, 1):
        k.bolt((s * (I + w / 2), FH, -0.45), (0, 1, 0), r=0.03, h=0.014)
        k.bolt((s * (I + w / 2), FH, 0.45), (0, 1, 0), r=0.03, h=0.014)
    for node, sx in (("leaf_l", -1), ("leaf_r", 1)):
        k.cur_node = node
        k.nodes[node] = [sx * I, FH, 0.0]
        x_out, x_in = sx * I, sx * 0.008
        lo, hi = min(x_out, x_in), max(x_out, x_in)
        n = 3
        wd = (hi - lo) / n
        for i in range(n):
            k.box((lo + i * wd + 0.004, FH - 0.04, -I + 0.01), (lo + (i + 1) * wd - 0.004, FH, I - 0.01), "plank", axis=2, bevel=0.012,
                  tint=(0.82, 0.78, 0.66) if i == 1 else None)
        for z in (-0.45, 0.45):
            k.box((lo + 0.04, FH - 0.09, z - 0.05), (hi - 0.04, FH - 0.04, z + 0.05), "beam", axis=0, bevel=0.012)  # alt kusak
            hx0 = x_out + sx * 0.04   # cerceveye biraz biner
            hx1 = x_out - sx * 0.32   # kanadin icine dogru
            metal_box(k, (min(hx0, hx1), FH, z - 0.035), (max(hx0, hx1), FH + 0.012, z + 0.035), tint=DARK_IRON, rust=True)
            k.bolt((x_out - sx * 0.22, FH + 0.012, z), (0, 1, 0), r=0.018, h=0.008)
        k.box((x_in + sx * 0.02 - 0.015 * sx, FH, -0.12), (x_in + sx * 0.09, FH + 0.012, 0.12), "plank", axis=2, bevel=0.004, tint=WARN)
    k.cur_node = "pit"
    k.nodes["pit"] = [0.0, 0.0, 0.0]
    D = 1.0
    t = 0.05
    for s in (-1, 1):  # ince duvarlar: ic yuzleri cukura bakar
        k.box((-I - t, -D, s * I - t * (s < 0)), (I + t, 0.0, s * I + t * (s > 0)), "plank", axis=0, bevel=0.0, tint=PIT)
        k.box((s * I - t * (s < 0), -D, -I), (s * I + t * (s > 0), 0.0, I), "plank", axis=2, bevel=0.0, tint=PIT)
    k.box((-I - t, -D - t, -I - t), (I + t, -D, I + t), "plank", axis=0, bevel=0.0, tint=PIT)
    for x, z in ((-0.35, -0.3), (0.3, -0.35), (0.0, 0.1), (-0.3, 0.4), (0.38, 0.35)):
        k.log((x, -D, z), (x, -D + 0.35, z), 0.045, sides=5, cls="beam", tip=0.1, tip_r=0.2, tint=(0.55, 0.45, 0.4))
    k.cur_node = None
    k.col_box((-O, 0, -O), (O, FH, O))
    return k, dict(kind="placeable", name="Ã‡ukur KapaÄŸÄ±", origin="bottom_center", placeholder=True, pit_depth=D,
                   note="leaf_l/leaf_r Godot Z ekseni etrafinda doner: leaf_l -90, leaf_r +90 derece asagi acar.")


PIECES = [
    ("civi_kulesi", p_civi_kulesi), ("sapan_kulesi", p_sapan_kulesi), ("sacma_kulesi", p_sacma_kulesi),
    ("diken_tarlasi", p_diken_tarlasi), ("yapiskan_zemin", p_yapiskan_zemin),
    ("firlatma_pedi", p_firlatma_pedi), ("cukur_kapagi", p_cukur_kapagi),
]
REQUIRED = {
    "civi_kulesi": {"head": None, "muzzle": "head"},
    "sapan_kulesi": {"head": None, "muzzle": "head"},
    "sacma_kulesi": {"head": None, "muzzle": "head"},
    "yapiskan_zemin": {"goo": None},
    "firlatma_pedi": {"plate": None},
    "cukur_kapagi": {"leaf_l": None, "leaf_r": None, "pit": None},
}
BUDGET = 3000


def load_atlas(name, non_color):
    path = os.path.join(bk.OUT_DIR, name + ".png")
    if not os.path.exists(path):
        raise SystemExit(f"{path} yok: once pipeline/build_kit.py calistir")
    img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pack()
    return img


def goo_material():
    mat = bpy.data.materials.new("kabarma_goo")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = GOO
    bsdf.inputs["Roughness"].default_value = 0.12
    bsdf.inputs["Metallic"].default_value = 0.0
    try:
        bsdf.inputs["Coat Weight"].default_value = 0.6
    except KeyError:
        pass
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Color"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs[6].default_value = GOO
    nt.links.new(vc.outputs["Color"], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return mat


def write_import(pid):
    with open(os.path.join(OUT, pid + ".glb.import"), "w", encoding="utf-8", newline="\n") as f:
        f.write('[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[params]\n\n'
                'meshes/ensure_tangents=true\nmeshes/generate_lods=true\nmeshes/create_shadow_meshes=true\n'
                'gltf/embedded_image_handling=0\n'
                '_subresources={\n"materials": {\n"kabarma_wood": {\n"use_external/enabled": true,\n'
                f'"use_external/path": "{RES_WOOD}/wood_kit.tres",\n"use_external/fallback_path": "{RES_WOOD}/wood_kit.tres"\n'
                '}\n}\n}\n')


def glb_nodes(path):
    """GLB'nin JSON parcasindan dugum adlari ve ebeveynleri."""
    with open(path, "rb") as f:
        data = f.read()
    ln, typ = struct.unpack_from("<I4s", data, 12)
    doc = json.loads(data[20:20 + ln].decode("utf-8"))
    nodes = doc.get("nodes", [])
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = i
    return {n.get("name"): (nodes[parent[i]].get("name") if i in parent else None) for i, n in enumerate(nodes)}


def godot_to_blender(v):
    return (v[0], -v[2], v[1])


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None
    sheet = "--no-sheet" not in argv
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    mat = bk.make_material(load_atlas("wood_atlas_albedo", False), load_atlas("wood_atlas_normal", True), load_atlas("wood_atlas_rm", True))
    gmat = goo_material()
    meta_path = os.path.join(OUT, "defense.json")
    doc = {}
    if os.path.exists(meta_path):
        with open(meta_path, encoding="utf-8") as f:
            doc = json.load(f)
    entries = []
    problems = []
    for pid, fn in PIECES:
        if only and pid not in only:
            continue
        objs, meta = bk.build_piece(pid, fn, mat)
        byname = {o.name: o for o in objs}
        if "goo" in byname:
            byname["goo"].data.materials.clear()
            byname["goo"].data.materials.append(gmat)
        if "muzzle" in meta:
            head = byname["head"]
            piv = Vector(meta["nodes"]["head"]["pivot"])
            d = Vector(meta["muzzle"]) - piv
            e = bpy.data.objects.new("muzzle", None)
            e.empty_display_type = "SINGLE_ARROW"
            e.empty_display_size = 0.2
            e.parent = head
            e.location = godot_to_blender(d)
            # donus yok: Godot'ta muzzle'in -Z'si basin ileri yonu
            bpy.context.scene.collection.objects.link(e)
            objs.append(e)
            meta["nodes"]["muzzle"] = {"parent": "head", "pos": [round(c, 4) for c in d]}
        path = os.path.join(OUT, f"{pid}.glb")
        bk.export_glb(objs, path)
        write_import(pid)
        names = glb_nodes(path)
        for want, par in REQUIRED.get(pid, {}).items():
            if want not in names:
                problems.append(f"{pid}: '{want}' yok")
            elif par is not None and names[want] != par:
                problems.append(f"{pid}: '{want}' ebeveyni {names[want]} (beklenen {par})")
        meta.pop("sockets", None)
        meta["glb_nodes"] = names
        doc[pid] = meta
        flag = "" if meta["tris"] <= BUDGET else "  <-- BUTCE DISI"
        print(f"DEFENSE {pid:15s} tris={meta['tris']:5d} size={meta['size']} nodes={names}{flag}")
        # temas foyu icin adlari cakismasin diye yeniden adlandir, kule basini ve kapaklari dondur
        for o in objs:
            if o.name != pid:
                o.name = f"{pid}.{o.name}"
        for o in objs:
            if o.name.endswith(".head"):
                o.rotation_euler = (0, 0, math.radians(150))
            elif o.name.endswith(".leaf_l"):
                o.rotation_euler = (0, math.radians(40), 0)
            elif o.name.endswith(".leaf_r"):
                o.rotation_euler = (0, math.radians(-40), 0)
            elif o.name.endswith(".plate"):
                o.location.z += 0.12
        entries.append((objs, meta))
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    if sheet and entries:
        bk.SHEET = SHEET
        bk.TMP_DIR = os.path.join(os.path.dirname(SHEET), "_tiles")
        bk.render_sheet(entries, cols=4, tw=520, th=440)
        print("SHEET", SHEET)
    for p in problems:
        print("PROBLEM", p)
    print("DEFENSE DONE", len(entries), "problems:", len(problems))
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
