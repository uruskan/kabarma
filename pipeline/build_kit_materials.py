"""
Kabarma yapi kitinin ahsap disi uc malzemesi (GDD §7, ICERIK §10.1, SANAT-SES §1/§5).

  kerpic_tas    Kerpic-Tas   : kalas -> kerpic tugla siralari, kiris -> yuvarlak koseli tas bloklar, cati -> kiremit
  celik_sac     Celik Sac    : kalas -> oluklu boyali sac (percinli bindirme), kiris -> percinli celik levha, cati -> galvaniz oluklu sac
  mayali_beton  Mayali Beton : kalas -> yastik gibi kabarmis dokme beton paneller, kiris -> perdahli beton + Maya damari,
                               cati -> beton plaka; derzlerde soluk Maya turkuazi

Geometri pipeline/build_kit.py'den modul olarak alinir (o dosya degismez). Her parca ahsap kitiyle AYNI sinirlara,
orijine, yuvalara ve carpisma kutularina sahiptir: betik her parcayi ahsap sockets.json'a karsi denetler, fark varsa
cikis kodu 1 olur. Malzeme farki doku atlasindan (ayni UV duzeni: kalas/kiris/cati seritleri, uc karolari, demir karolari)
ve yalnizca pah olcusunden gelir (pah topolojiyi ve sinirlari degistirmez; denetim bunu kanitlar).

Doku: yapi (tugla, blok, oluk, panel, percin, derz) numpy ile yordamsal; yuzey tanesi ve pas/kir F:\\Assets'teki CC0
Poly Haven taramalarindan (yalniz okunur, kucultulup yuksek geciren filtreyle palet renklerinin ustune karistirilir).
Lisanslar: pipeline/licenses.md.

Calistir (depo kokunden, her malzeme ayri Blender sureci):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/build_kit_materials.py -- --material kerpic_tas [--only duvar,kare_temel] [--no-sheet] \
      [--atlas-only] [--sheet yol.png] [--tex 1024]

Cikti:
  game/assets/models/build/<malzeme>/<id>.glb               28 parca (dokular gomulu)
  game/assets/models/build/<malzeme>/<malzeme>_atlas_*.png  ortak doku (Godot: <malzeme>_kit.tres)
  game/assets/models/build/<malzeme>/sockets.json           ahsabinkiyle ayni, yalniz "material" farkli
  progress/m2/yapi/build_kit_<malzeme>_sheet.png            temas foyu
"""
import bpy
import math
import json
import os
import sys
import zlib

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import build_kit as bk  # noqa: E402  (KIT_TMP ayari orada yapilir)

ROOT = bk.ROOT
WOOD_BEV = (bk.BEV, bk.BEAM_BEV)  # ahsap kitinin pahlari (degistirilmeden once)
BUILD_DIR = os.path.join(ROOT, "game", "assets", "models", "build")
WOOD_JSON = os.path.join(BUILD_DIR, "wood", "sockets.json")
SHEET_DIR = os.path.join(ROOT, "progress", "m2", "yapi")

LIB = r"F:\Assets"
PSG1 = os.path.join(LIB, "PSG_Compound_Utilities_R01", "Textures", "Source")
PSG2 = os.path.join(LIB, "PSG_Compound_Utilities_R02", "Textures", "Source")
# anahtar -> (dosya, karonun gercek boyu m). Hepsi Poly Haven CC0 (Reports/Texture_sources.json).
SCANS = {
    "rock_boulder_dry": (os.path.join(PSG2, "rock_boulder_dry", "rock_boulder_dry_diff_4k.jpg"), 2.0),
    "blue_metal_plate": (os.path.join(PSG2, "blue_metal_plate", "blue_metal_plate_diff_4k.jpg"), 1.5),
    "worn_corrugated_iron": (os.path.join(PSG2, "worn_corrugated_iron", "worn_corrugated_iron_diff_4k.jpg"), 2.0),
    "concrete_wall_001": (os.path.join(PSG1, "concrete_wall_001", "concrete_wall_001_diff_4k.jpg"), 2.5),
}

MAYA = np.array((0.37, 0.94, 0.78), np.float32)  # #5FF0C8 (SANAT-SES §2)

MATS = {
    "kerpic_tas": dict(name="Kerpiç-Taş", bev=0.045, beam_bev=0.06, nk=2.6, nscale=0.7,
                       scans=["rock_boulder_dry"]),
    "celik_sac": dict(name="Çelik Sac", bev=0.015, beam_bev=0.02, nk=2.4, nscale=0.7,
                      scans=["blue_metal_plate", "worn_corrugated_iron"]),
    "mayali_beton": dict(name="Mayalı Beton", bev=0.05, beam_bev=0.06, nk=2.4, nscale=0.7,
                         scans=["concrete_wall_001"]),
}

# denetlenen alanlar: ahsap sockets.json ile birebir ayni olmali
CHECK_KEYS = ("tris", "bounds", "center", "size", "footprint", "sockets", "colliders", "nodes", "kind", "origin")


# ---------------------------------------------------------------------------
# Yardimcilar
# ---------------------------------------------------------------------------
def C(*c):
    return np.array(c, np.float32)


def lerp(a, b, t):
    if np.ndim(t) == 2 and (np.ndim(a) == 3 or np.ndim(b) == 3 or np.ndim(a) == 1 or np.ndim(b) == 1):
        t = t[..., None]
    return a * (1 - t) + b * t


def dims(U, V):
    h, w = U.shape
    um = (U[0, -1] - U[0, 0]) * w / max(w - 1, 1)
    vm = (V[-1, 0] - V[0, 0]) * h / max(h - 1, 1)
    return h, w, um, vm


def nz(U, V, rng, per_m, octaves=3, per_m_v=None):
    """Metre basina 'per_m' hucreli fbm, 0..1."""
    h, w, um, vm = dims(U, V)
    pv = per_m if per_m_v is None else per_m_v
    return bk.fbm(h, w, max(1, int(round(vm * pv))), max(1, int(round(um * per_m))), rng, octaves)


def up_wrap(g, h, w):
    cy, cx = g.shape
    ys = (np.arange(h, dtype=np.float32) + 0.5) * cy / h - 0.5
    xs = (np.arange(w, dtype=np.float32) + 0.5) * cx / w - 0.5
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    ty = (ys - y0)[:, None]
    tx = (xs - x0)[None, :]
    y1, x1 = (y0 + 1) % cy, (x0 + 1) % cx
    y0 %= cy
    x0 %= cx
    top = g[y0][:, x0] * (1 - tx) + g[y0][:, x1] * tx
    bot = g[y1][:, x0] * (1 - tx) + g[y1][:, x1] * tx
    return top * (1 - ty) + bot * ty


def blur(a, f):
    T = a.shape[0]
    small = a.reshape(T // f, f, T // f, f).mean((1, 3))
    return up_wrap(small, T, T)


class Scan:
    """Bir tarama karosu: yuksek geciren ayrinti (-1..1) ve alcak geciren renk."""

    def __init__(self, key, ppm):
        path, self.m = SCANS[key]
        T = max(64, int(round(self.m * ppm / 16)) * 16)
        self.T = T
        self.key = key
        if os.path.exists(path):
            img = bpy.data.images.load(path, check_existing=False)
            img.scale(T, T)
            a = np.empty(T * T * 4, np.float32)
            img.pixels.foreach_get(a)
            bpy.data.images.remove(img)
            rgb = a.reshape(T, T, 4)[..., :3].copy()
            self.ok = True
        else:
            print(f"UYARI: tarama yok, yordamsal yedek: {path}")
            rgb = np.full((T, T, 3), 0.5, np.float32)
            self.ok = False
        lum = rgb @ C(0.30, 0.59, 0.11)
        hp = lum - blur(lum, 16)
        self.det = np.clip(hp / (hp.std() + 1e-6) / 2.5, -1, 1)
        self.low = np.stack([blur(rgb[..., i], 16) for i in range(3)], -1)
        self.lum = lum

    def at(self, arr, U, V, ou=0.0, ov=0.0):
        iy = (np.floor((V + ov) / self.m * self.T).astype(int)) % self.T
        ix = (np.floor((U + ou) / self.m * self.T).astype(int)) % self.T
        return arr[iy, ix]


def blocks(U, V, ch, lmin, lmax, rng, gap, r):
    """Siralar halinde bloklar (siralar V boyunca ch arayla, blok boyu U'da lmin..lmax, her sira kaydirmali).
    Doner: d = blok kenarina icten uzaklik (m, harc < 0; yuvarlak koseli), bv = blok basina 0..1 rastgele."""
    ci = np.floor(V / ch).astype(int)
    lo = ci * ch
    dv = np.minimum(V - lo, lo + ch - V)
    du = np.zeros_like(U)
    bid = np.zeros(U.shape, np.int64)
    umin, umax = float(U.min()), float(U.max())
    cmin = int(ci.min())
    for c in np.unique(ci):
        m = ci == c
        e = [umin - rng.uniform(0, lmax)]
        while e[-1] <= umax:
            e.append(e[-1] + rng.uniform(lmin, lmax))
        e = np.array(e)
        uu = U[m]
        k = np.clip(np.searchsorted(e, uu, side="right") - 1, 0, len(e) - 2)
        du[m] = np.minimum(uu - e[k], e[k + 1] - uu)
        bid[m] = (int(c) - cmin) * 4096 + k
    x = du - gap / 2
    y = dv - gap / 2
    d = np.where((x < r) & (y < r), r - np.hypot(r - np.minimum(x, r), r - np.minimum(y, r)), np.minimum(x, y))
    vals = rng.random(int(bid.max()) + 1).astype(np.float32)
    return d.astype(np.float32), vals[bid]


def seams_u(U, rng, lmin, lmax):
    """Lif yonune dik panel ekleri. Doner: du = en yakin eke uzaklik (m), pv = panel basina 0..1."""
    umin, umax = float(U.min()), float(U.max())
    e = [umin - rng.uniform(0, lmax)]
    while e[-1] <= umax:
        e.append(e[-1] + rng.uniform(lmin, lmax))
    e = np.array(e)
    k = np.clip(np.searchsorted(e, U, side="right") - 1, 0, len(e) - 2)
    du = np.minimum(U - e[k], e[k + 1] - U).astype(np.float32)
    vals = rng.random(len(e)).astype(np.float32)
    return du, vals[k]


def rivets(du, V, offset, spacing, rad, v0=0.0):
    """Ekin iki yaninda percin sutunlari; kubbe yuksekligi 0..1."""
    dc = np.abs(du - offset)
    vv = ((V + v0) / spacing) % 1.0 - 0.5
    r = np.hypot(dc, vv * spacing)
    return np.sqrt(np.clip(1 - (r / rad) ** 2, 0, 1)).astype(np.float32)


def veins(U, V, rng, n, width=0.004):
    """Maya damarlari: U boyunca kivrilan ince catlaklar (kiris catlaklari gibi)."""
    h, w, um, vm = dims(U, V)
    out = np.zeros_like(U)
    for _ in range(n):
        kv = rng.uniform(0.06, vm - 0.06)
        u0, u1 = sorted(rng.uniform(U.min(), U.max(), 2))
        if u1 - u0 < 0.4:
            u1 = min(float(U.max()), u0 + 0.8)
        wob = (bk.fbm(1, w, 1, max(2, int(um * 4)), rng, 3)[0] - 0.5) * 0.06
        d = np.abs(V - (kv + wob[None, :]))
        out = np.maximum(out, np.exp(-(d / width) ** 2) * bk.sstep(u0, u0 + 0.15, U) * (1 - bk.sstep(u1 - 0.15, u1, U)))
    return out


# ---------------------------------------------------------------------------
# Malzeme uretecleri: (cls, U, V, rng, scans, end) -> renk, yukseklik, puruz, metal
# ---------------------------------------------------------------------------
def g_kerpic_tas(cls, U, V, rng, sc, end):
    rock = sc["rock_boulder_dry"]
    ou, ov = rng.uniform(0, 2), rng.uniform(0, 2)
    det = rock.at(rock.det, U, V, ou, ov)
    metal = np.zeros_like(U)
    if cls == "plank":  # kerpic: gunesle kurumus camur tugla, saman lifli
        d, bv = blocks(U, V, 0.125, 0.29, 0.36, rng, gap=0.016, r=0.03)
        d = d + (nz(U, V, rng, 14, 2) - 0.5) * 0.012
        patch = nz(U, V, rng, 3, 2)
        t = np.clip(bv * 0.65 + (patch - 0.5) * 0.5 + 0.15, 0, 1)
        col = lerp(C(0.84, 0.68, 0.49), C(0.68, 0.51, 0.35), t)
        straw = bk.sstep(0.70, 0.78, nz(U, V, rng, 70, 1))
        col = col + C(0.07, 0.06, 0.01) * straw[..., None]
        col = col * (1 + det[..., None] * 0.10)
        inside = bk.sstep(-0.004, 0.012, d)
        mortar = C(0.60, 0.49, 0.37) * (0.9 + 0.15 * patch[..., None])
        col = lerp(mortar, col, inside)
        col = col * (0.80 + 0.20 * bk.sstep(-0.01, 0.035, d))[..., None]
        height = 0.2 + 0.45 * bk.sstep(-0.004, 0.035, d) + 0.07 * det + 0.06 * straw
        rough = 0.93 - 0.03 * inside
    elif cls == "beam":  # tas: yuvarlak koseli, kaba yontulmus bloklar
        d, bv = blocks(U, V, 0.25, 0.28, 0.62, rng, gap=0.022, r=0.065)
        d = d + (nz(U, V, rng, 9, 3) - 0.5) * 0.03
        bv2 = (bv * 7.31) % 1.0  # blok basina ikinci rastgele deger
        col = lerp(C(0.78, 0.71, 0.60), C(0.62, 0.56, 0.48), bv)
        warm = bk.sstep(0.6, 0.9, bv2)
        col = lerp(col, C(0.80, 0.64, 0.47), warm * 0.7)
        col = col * (0.92 + 0.16 * nz(U, V, rng, 4, 2)[..., None])
        col = col * (1 + det[..., None] * 0.22)
        inside = bk.sstep(-0.004, 0.015, d)
        col = lerp(C(0.62, 0.53, 0.42), col, inside)
        col = col * (0.78 + 0.22 * bk.sstep(-0.01, 0.06, d))[..., None]
        height = 0.12 + 0.55 * bk.sstep(-0.004, 0.07, d) + 0.14 * det
        rough = 0.88 - 0.04 * inside
    else:  # kiremit: alaturka oluklu, siralar kaydirmali
        P, ch = 0.2, 0.25
        ci = np.floor(V / ch).astype(int)
        t = V / ch - ci
        q = U / P + 0.5 * (ci % 2)
        f = q - np.floor(q)
        barrel = np.sqrt(np.clip(1 - (2 * f - 1) ** 2, 0, 1))
        tid = (ci - ci.min()) * 10000 + np.floor(q).astype(int) - int(np.floor(q).min())
        tv = rng.random(int(tid.max()) + 1).astype(np.float32)[tid]
        col = lerp(C(0.80, 0.47, 0.32), C(0.63, 0.33, 0.22), np.clip(tv * 0.8 + (nz(U, V, rng, 3, 2) - 0.5) * 0.4, 0, 1))
        col = col * (1 + det[..., None] * 0.12)
        col = col * (0.72 + 0.28 * barrel)[..., None]
        edge = bk.sstep(0.0, 0.08, t) * bk.sstep(0.0, 0.03, 1 - t)
        col = col * (0.70 + 0.30 * edge)[..., None]
        lichen = bk.sstep(0.72, 0.85, nz(U, V, rng, 6, 3)) * 0.5
        col = lerp(col, C(0.62, 0.60, 0.42), lichen)
        height = 0.1 + 0.5 * barrel * (0.4 + 0.6 * edge) + 0.15 * t + 0.06 * det
        rough = 0.80 + 0.08 * lichen
    return np.clip(col, 0, 1), height, rough, metal


def g_celik_sac(cls, U, V, rng, sc, end):
    blue, corr = sc["blue_metal_plate"], sc["worn_corrugated_iron"]
    ou, ov = rng.uniform(0, 2), rng.uniform(0, 2)
    if cls == "roof":  # galvaniz oluklu cati saci, gercek pas desenli
        P = 0.15
        ridge = 0.5 + 0.5 * np.cos(2 * np.pi * U / P) if not end else np.full_like(U, 0.5)
        du, pv = seams_u(U, rng, 1.3, 1.8)
        riv = rivets(du, V, 0.03, 0.25, 0.016, rng.uniform(0, 0.25))
        det = corr.at(corr.det, U, V, ou, ov)
        low = corr.at(corr.low, U, V, ou, ov)
        red = low[..., 0] - low[..., 2]
        lo_, hi_ = np.percentile(red, 78), np.percentile(red, 97)
        rust = bk.sstep(lo_, hi_ + 1e-4, red) * 0.7
        col = C(0.66, 0.69, 0.72) * (0.86 + 0.18 * ridge[..., None]) * (0.95 + 0.1 * pv[..., None])
        col = col * (1 + det[..., None] * 0.14)
        rc = lerp(C(0.55, 0.33, 0.20), C(0.40, 0.24, 0.15), nz(U, V, rng, 10, 2))
        col = lerp(col, rc, rust)
        seam = 1 - bk.sstep(0.0, 0.014, du)
        col = col * (1 - 0.35 * seam)[..., None] * (0.9 + 0.2 * riv)[..., None]
        height = 0.5 + 0.45 * (ridge - 0.5) * (1 - seam) + 0.35 * riv - 0.15 * seam + 0.04 * det
        metal = lerp(np.full_like(U, 0.75), np.full_like(U, 0.15), rust)
        rough = lerp(np.full_like(U, 0.42), np.full_like(U, 0.88), rust)
        return np.clip(col, 0, 1), height, rough, metal
    if cls == "plank":  # boyali oluklu sac (duvar), bindirme eklerinde percin
        P = 0.12
        ridge = 0.5 + 0.5 * np.cos(2 * np.pi * U / P) if not end else np.full_like(U, 0.5)
        du, pv = seams_u(U, rng, 0.9, 1.3)
        riv = rivets(du, V, 0.035, 0.11, 0.012, rng.uniform(0, 0.11))
        paint = lerp(C(0.47, 0.55, 0.63), C(0.42, 0.50, 0.60), pv)
        det = blue.at(blue.det, U, V, ou, ov)
        seam_w = 0.012
    else:  # kiris: percinli celik levha
        ridge = np.full_like(U, 0.5)
        du, pv = seams_u(U, rng, 0.8, 1.4)
        riv = rivets(du, V, 0.05, 0.08, 0.014, rng.uniform(0, 0.08))
        paint = lerp(C(0.38, 0.43, 0.50), C(0.34, 0.39, 0.46), pv)
        det = blue.at(blue.det, U, V, ou + 0.7, ov + 0.4)
        seam_w = 0.008
    seam = 1 - bk.sstep(0.0, seam_w, du)
    wear = bk.sstep(0.64, 0.76, nz(U, V, rng, 6, 3) * 0.65 + (det * 0.5 + 0.5) * 0.25 + ridge * 0.08 + (1 - bk.sstep(0, 0.06, du)) * 0.12)
    rust = bk.sstep(0.70, 0.82, nz(U, V, rng, 4, 3)) * 0.8
    streak = bk.sstep(0.55, 1.0, nz(U, V, rng, 30, 2, per_m_v=2)) * (1 - bk.sstep(0.0, 0.25, du)) * 0.5
    rust = np.maximum(rust, streak)
    col = paint * (0.86 + 0.18 * ridge[..., None]) * (1 + det[..., None] * 0.10)
    col = lerp(col, C(0.66, 0.67, 0.69) * (1 + det[..., None] * 0.15), wear)
    col = lerp(col, C(0.52, 0.31, 0.19), rust)
    col = col * (1 - 0.35 * seam)[..., None]
    col = lerp(col, C(0.58, 0.61, 0.65) * (0.75 + 0.35 * riv[..., None]), (riv > 0.05).astype(np.float32) * 0.8)
    height = 0.5 + 0.42 * (ridge - 0.5) * (1 - seam) + 0.4 * riv - 0.18 * seam + 0.04 * det
    metal = lerp(lerp(np.full_like(U, 0.25), np.full_like(U, 0.85), np.maximum(wear, (riv > 0.05) * 0.8)), np.full_like(U, 0.1), rust)
    rough = lerp(lerp(np.full_like(U, 0.55), np.full_like(U, 0.36), wear), np.full_like(U, 0.9), rust)
    return np.clip(col, 0, 1), height, rough, metal


def g_mayali_beton(cls, U, V, rng, sc, end):
    con = sc["concrete_wall_001"]
    ou, ov = rng.uniform(0, 2.5), rng.uniform(0, 2.5)
    det = con.at(con.det, U, V, ou, ov)
    if cls == "plank":  # yastik gibi kabarmis dokme panel, kalip izi
        du, pv = seams_u(U, rng, 0.7, 1.1)
        dist, puff_w = du, 0.18
        base = lerp(C(0.72, 0.70, 0.66), C(0.66, 0.65, 0.62), pv)
        form = bk.sstep(0.85, 1.0, 0.5 + 0.5 * np.cos(2 * np.pi * V / 0.11))
        vn = np.zeros_like(U)
    elif cls == "beam":  # perdahli kiris, uzun ekler, Maya damarlari
        du, pv = seams_u(U, rng, 1.2, 1.8)
        dist, puff_w = du, 0.1
        base = lerp(C(0.68, 0.67, 0.64), C(0.63, 0.62, 0.60), pv)
        form = np.zeros_like(U)
        vn = veins(U, V, rng, int(rng.integers(1, 3)) if not end else 0)
    else:  # cati plakasi: 0,5 x 0,25 m yastiklar
        du, pv = seams_u(U, rng, 0.5, 0.5)
        dv = np.abs(((V / 0.25) % 1.0) - 0.5) * 0.25
        dv = 0.125 - dv
        dist, puff_w = np.minimum(du, dv), 0.09
        base = lerp(C(0.67, 0.68, 0.67), C(0.62, 0.63, 0.63), pv)
        form = np.zeros_like(U)
        vn = np.zeros_like(U)
    pillow = bk.sstep(0.0, puff_w, dist)
    seam = 1 - bk.sstep(0.006, 0.014, dist)
    blot = nz(U, V, rng, 3, 3)
    pores = bk.sstep(0.80, 0.9, nz(U, V, rng, 90, 1))
    col = base * (0.90 + 0.14 * blot[..., None]) * (1 + det[..., None] * 0.14)
    col = col * (1 - 0.10 * pores)[..., None] * (1 - 0.05 * form)[..., None]
    col = col * (0.84 + 0.16 * pillow)[..., None]  # yastik kenari golgesi
    seam_col = lerp(C(0.34, 0.37, 0.36), MAYA * 0.7, 0.3)
    col = lerp(col, seam_col, seam)
    col = lerp(col, MAYA * 0.8, vn * 0.55)
    height = 0.12 + 0.62 * pillow - 0.04 * form + 0.05 * det - 0.05 * pores - 0.1 * vn
    rough = 0.90 - 0.25 * seam - 0.2 * vn
    metal = np.zeros_like(U)
    return np.clip(col, 0, 1), height, rough, metal


GENS = {"kerpic_tas": g_kerpic_tas, "celik_sac": g_celik_sac, "mayali_beton": g_mayali_beton}


def grid(w, h, um, vm, u0=0.0, v0=0.0):
    u = (np.arange(w, dtype=np.float32) + 0.5) / w * um + u0
    v = (np.arange(h, dtype=np.float32) + 0.5) / h * vm + v0
    return np.meshgrid(u, v)


def make_atlas(mid, N):
    ppm = N / bk.ATLAS_U_M
    scans = {k: Scan(k, ppm) for k in MATS[mid]["scans"]}
    rng = np.random.default_rng(zlib.crc32(mid.encode()))
    gen = GENS[mid]
    SH = N // 8
    albedo = np.zeros((N, N, 3), np.float32)
    height = np.full((N, N), 0.5, np.float32)
    rough = np.full((N, N), 0.8, np.float32)
    metal = np.zeros((N, N), np.float32)

    def put(sl, out):
        c, h, r, m = out
        albedo[sl] = c
        height[sl] = h
        rough[sl] = r
        metal[sl] = m

    for cls, rows in bk.STRIPS.items():
        for row in rows:
            U, V = grid(N, SH, bk.ATLAS_U_M, bk.STRIP_M)
            put((slice(row * SH, (row + 1) * SH), slice(None)), gen(cls, U, V, rng, scans, False))
    y0 = 7 * SH
    beam_mean = None
    for cls, tile in bk.END_TILE.items():
        U, V = grid(SH, SH, bk.STRIP_M, bk.STRIP_M, rng.uniform(0, 3), rng.uniform(0, 1))
        out = gen(cls, U, V, rng, scans, True)
        put((slice(y0, y0 + SH), slice(tile * SH, (tile + 1) * SH)), out)
        if cls == "beam":
            beam_mean = out[0].reshape(-1, 3).mean(0)
    for tile, rust in ((3, False), (4, True)):
        put((slice(y0, y0 + SH), slice(tile * SH, (tile + 1) * SH)), bk.metal_tile(SH, rng, rust))
    for tile in range(5, 8):
        albedo[y0:y0 + SH, tile * SH:(tile + 1) * SH] = beam_mean
    k = MATS[mid]["nk"] * N / 1024.0
    gy, gx = np.gradient(height)
    nx, ny = -gx * k, -gy * k
    nz_ = np.ones_like(nx)
    ln = np.sqrt(nx * nx + ny * ny + nz_ * nz_)
    normal = np.stack([nx / ln, ny / ln, nz_ / ln], -1) * 0.5 + 0.5
    rm = np.stack([np.ones_like(rough), np.clip(rough, 0, 1), np.clip(metal, 0, 1)], -1)
    used = [s.key for s in scans.values() if s.ok]
    return np.clip(albedo, 0, 1), normal, rm, used


# ---------------------------------------------------------------------------
# Godot dosyalari
# ---------------------------------------------------------------------------
KIT_TRES = """[gd_resource type="StandardMaterial3D" load_steps=4 format=3]

[ext_resource type="Texture2D" path="{d}/{m}_atlas_albedo.png" id="1_alb"]
[ext_resource type="Texture2D" path="{d}/{m}_atlas_rm.png" id="2_rm"]
[ext_resource type="Texture2D" path="{d}/{m}_atlas_normal.png" id="3_nrm"]

[resource]
resource_name = "kabarma_{m}"
vertex_color_use_as_albedo = true
albedo_texture = ExtResource("1_alb")
metallic = 1.0
metallic_texture = ExtResource("2_rm")
metallic_texture_channel = 2
roughness_texture = ExtResource("2_rm")
roughness_texture_channel = 1
normal_enabled = true
normal_scale = {ns}
normal_texture = ExtResource("3_nrm")
texture_filter = 5
"""


def write_godot_imports(mid, out_dir, pids):
    res = f"res://assets/models/build/{mid}"
    tres = f"{mid}_kit.tres"
    with open(os.path.join(out_dir, tres), "w", encoding="utf-8", newline="\n") as f:
        f.write(KIT_TRES.format(d=res, m=mid, ns=MATS[mid]["nscale"]))
    for name, normal in ((f"{mid}_atlas_albedo", False), (f"{mid}_atlas_rm", False), (f"{mid}_atlas_normal", True)):
        p = os.path.join(out_dir, name + ".png.import")
        if os.path.exists(p):
            continue
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write('[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n\n'
                    f'compress/mode=2\ncompress/normal_map={1 if normal else 2}\nmipmaps/generate=true\n'
                    'detect_3d/compress_to=0\n')
    for pid in pids:
        p = os.path.join(out_dir, pid + ".glb.import")
        if os.path.exists(p):
            with open(p, encoding="utf-8") as f:
                if tres in f.read():
                    continue
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write('[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[params]\n\n'
                    'meshes/ensure_tangents=true\nmeshes/generate_lods=true\nmeshes/create_shadow_meshes=true\n'
                    'gltf/embedded_image_handling=0\n'
                    '_subresources={\n"materials": {\n'
                    f'"kabarma_{mid}": {{\n"use_external/enabled": true,\n'
                    f'"use_external/path": "{res}/{tres}",\n"use_external/fallback_path": "{res}/{tres}"\n'
                    '}\n}\n}\n')


def norm_json(v):
    return json.loads(json.dumps(v))


# ---------------------------------------------------------------------------
def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    mid, only, tex, sheet, atlas_only, sheet_path = None, None, 1024, True, False, None
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--material":
            mid = argv[i + 1]
            i += 1
        elif a == "--only":
            only = set(argv[i + 1].split(","))
            i += 1
        elif a == "--tex":
            tex = int(argv[i + 1])
            i += 1
        elif a == "--sheet":
            sheet_path = argv[i + 1]
            i += 1
        elif a == "--no-sheet":
            sheet = False
        elif a == "--atlas-only":
            atlas_only = True
        i += 1
    if mid not in MATS:
        raise SystemExit(f"--material su degerlerden biri olmali: {', '.join(MATS)}")
    cfg = MATS[mid]
    out_dir = os.path.join(BUILD_DIR, mid)
    os.makedirs(out_dir, exist_ok=True)
    # build_kit'in modul duzeyi ayarlari: cikti yollari ve pah (pah sinirlari/topolojiyi degistirmez; asagida denetlenir)
    bk.OUT_DIR = out_dir
    bk.RES_DIR = f"res://assets/models/build/{mid}"
    bk.SHEET = sheet_path or os.path.join(SHEET_DIR, f"build_kit_{mid}_sheet.png")
    bk.TMP_DIR = os.path.join(os.environ.get("KIT_TMP") or os.path.join(ROOT, "progress", "m2", "yapi"), f"_kit_tiles_{mid}")
    bk.BEV = cfg["bev"]
    bk.BEAM_BEV = cfg["beam_bev"]
    os.makedirs(os.path.dirname(bk.SHEET), exist_ok=True)

    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    albedo, normal, rm, used = make_atlas(mid, tex)
    print(f"ATLAS {mid}: taramalar {used}")
    i_alb = bk.np_to_image(f"{mid}_atlas_albedo", albedo, os.path.join(out_dir, f"{mid}_atlas_albedo.png"), False)
    i_n = bk.np_to_image(f"{mid}_atlas_normal", normal, os.path.join(out_dir, f"{mid}_atlas_normal.png"), True)
    i_rm = bk.np_to_image(f"{mid}_atlas_rm", rm, os.path.join(out_dir, f"{mid}_atlas_rm.png"), True)
    if atlas_only:
        print("KIT ATLAS DONE", mid)
        return
    mat = bk.make_material(i_alb, i_n, i_rm)
    mat.name = f"kabarma_{mid}"
    nt = mat.node_tree
    for n in nt.nodes:
        if n.type == "NORMAL_MAP":
            n.inputs["Strength"].default_value = cfg["nscale"]

    with open(WOOD_JSON, encoding="utf-8") as f:
        wood = json.load(f)

    def compare(pid, meta):
        ref = wood["pieces"].get(pid)
        if ref is None:
            return [f"{pid}: ahsap sockets.json'da yok"]
        m = norm_json(meta)
        return [f"{pid}.{key}: {str(m.get(key))[:120]} != ahsap {str(ref.get(key))[:120]}"
                for key in CHECK_KEYS if m.get(key) != ref.get(key)]

    entries, diffs, tris, wood_bev = [], [], {}, []
    for pid, fn in bk.PIECES:
        if only and pid not in only:
            continue
        bk.BEV, bk.BEAM_BEV = cfg["bev"], cfg["beam_bev"]
        objs, meta = bk.build_piece(pid, fn, mat)
        d = compare(pid, meta)
        if d:
            # malzeme pahi bu parcada sinir/ucgen degistirdi (cati kirpmasi vb.): ahsap pahiyla yeniden kur
            for o in objs:
                me = o.data
                bpy.data.objects.remove(o)
                bpy.data.meshes.remove(me)
            bk.BEV, bk.BEAM_BEV = WOOD_BEV
            objs, meta = bk.build_piece(pid, fn, mat)
            d = compare(pid, meta)
            wood_bev.append(pid)
        diffs += d
        bk.export_glb(objs, os.path.join(out_dir, f"{pid}.glb"))
        entries.append((objs, meta))
        tris[pid] = meta["tris"]
        flag = "" if 200 <= meta["tris"] <= 1500 else "  <-- BUTCE DISI"
        note = "  (ahsap pahi)" if pid in wood_bev else ""
        print(f"KIT {mid} {pid:26s} tris={meta['tris']:5d} size={meta['size']}{flag}{note}")
    if not only:
        missing = set(wood["pieces"]) - set(tris)
        if missing:
            diffs.append(f"eksik parcalar: {sorted(missing)}")
    if diffs:
        print("KIT DENETIM HATASI (ahsap sockets.json ile fark):")
        for d in diffs:
            print("  ", d)
        raise SystemExit(1)
    doc = dict(wood)
    doc["material"] = mid
    with open(os.path.join(out_dir, "sockets.json"), "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    write_godot_imports(mid, out_dir, [pid for pid, _ in bk.PIECES])
    if sheet and entries:
        os.makedirs(bk.TMP_DIR, exist_ok=True)
        bk.render_sheet(entries)
    print(f"KIT DONE {mid} {len(tris)} parca, ucgen {min(tris.values())}-{max(tris.values())}, denetim tamam")


if __name__ == "__main__":
    main()
