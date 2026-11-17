"""
Kabarma ahsap yapi kiti (ICERIK §10.1, GDD §7, SANAT-SES §1/§4/§5).

26 sekil (Makine Platformu 3 boy) = 28 GLB, kodla uretilir: parcalar milimetrik oturur (URETIM §2.2 C).
Izgara 3 m, kat 3 m. Kalin, pahli kalas ve kirisler; tohumlu kucuk duzensizlik; el boyamasi gorunumlu
yordamsal ahsap dokusu (numpy ile Blender icinde uretilir, 1024 px atlas).

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/build_kit.py -- [--only duvar,kare_temel] [--no-sheet] [--tex 1024]

Cikti:
  game/assets/models/build/wood/<id>.glb          her parca kendi GLB'si (dokular gomulu)
  game/assets/models/build/wood/wood_atlas_*.png  ortak doku (Godot'ta tek malzeme: wood_kit.tres)
  game/assets/models/build/wood/sockets.json      tur, boyut, yuvalar, carpisma kutulari, ucgen sayilari
  progress/m1/build_kit_sheet.png                 temas foyu (1,6 m'lik olcek mankeni ile)

Koordinatlar: butun geometri Godot eksenlerinde kurulur (Y yukari, -Z ileri, +Z = parcanin "on/dis" yuzu)
ve Blender'a (x, -z, y) olarak verilir; glTF disa aktarimi bunu tekrar Godot eksenine cevirir.
"""
import bpy
import math
import json
import os
import sys
import random
import zlib
import tempfile

import numpy as np

# glTF disa aktarici gecici klasor acar; kum havuzunda varsayilan TEMP yazilamayabilir.
if os.environ.get("KIT_TMP"):
    os.makedirs(os.environ["KIT_TMP"], exist_ok=True)
    tempfile.tempdir = os.environ["KIT_TMP"]
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT_DIR = os.path.join(ROOT, "game", "assets", "models", "build", "wood")
SHEET = os.path.join(ROOT, "progress", "m1", "build_kit_sheet.png")
TMP_DIR = os.path.join(ROOT, "progress", "m1", "_kit_tiles")

G = 3.0          # izgara
S = 3.0          # kat yuksekligi
TOP = 2.99       # duvar ust kirisinin gercek ust yuzu (kat cizgisinde 1 cm golge)
BEV = 0.03       # kalas pahi
BEAM_BEV = 0.04  # kiris pahi
R3 = math.sqrt(3.0)
TRI_IN = R3 / 2.0  # 3 m eskenar ucgenin ic yaricapi
TRI_OUT = R3       # dis yaricap

X = Vector((1, 0, 0))
Y = Vector((0, 1, 0))
Z = Vector((0, 0, 1))


def V(*a):
    if len(a) == 1:
        return Vector(a[0])
    return Vector(a)


# ---------------------------------------------------------------------------
# Doku: yordamsal ahsap atlasi (1024 x 1024)
#   V ekseni 8 serit (alttan): 0-2 acik kalas, 3-4 koyu kiris, 5-6 cati tahtasi, 7 karolar
#   Serit = 4 m x 0,5 m (256 px/m). Karo satiri: 0 kalas kesiti, 1 kiris kesiti, 2 cati kesiti, 3 demir, 4 pasli demir
# ---------------------------------------------------------------------------
STRIPS = {"plank": [0, 1, 2], "beam": [3, 4], "roof": [5, 6]}
END_TILE = {"plank": 0, "beam": 1, "roof": 2}
ATLAS_U_M = 4.0
STRIP_M = 0.5

WOOD = {
    #           acik (sRGB)        koyu damar          sicak/soguk kayma
    "plank": ((0.80, 0.60, 0.39), (0.55, 0.36, 0.21), (0.05, 0.02, -0.03)),
    "beam": ((0.60, 0.42, 0.28), (0.36, 0.23, 0.14), (0.04, 0.01, -0.02)),
    "roof": ((0.66, 0.39, 0.29), (0.40, 0.21, 0.15), (0.05, -0.01, -0.02)),
}


def _smooth(t):
    return t * t * (3 - 2 * t)


def vnoise(h, w, cy, cx, rng):
    """Sarmalanan deger gurultusu, (h, w), 0..1."""
    g = rng.random((cy, cx)).astype(np.float32)
    ys = np.arange(h, dtype=np.float32) * cy / h
    xs = np.arange(w, dtype=np.float32) * cx / w
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    ty = _smooth(ys - y0)[:, None]
    tx = _smooth(xs - x0)[None, :]
    y1 = (y0 + 1) % cy
    x1 = (x0 + 1) % cx
    y0 %= cy
    x0 %= cx
    a = g[y0][:, x0]
    b = g[y0][:, x1]
    c = g[y1][:, x0]
    d = g[y1][:, x1]
    top = a + (b - a) * tx
    bot = c + (d - c) * tx
    return top + (bot - top) * ty


def fbm(h, w, cy, cx, rng, octaves=3):
    tot = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        tot += vnoise(h, w, max(1, cy << o), max(1, cx << o), rng) * amp
        norm += amp
        amp *= 0.5
    return tot / norm


def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def wood_strip(w, h, cls, rng):
    """Bir kalas seridi: renk (h,w,3), yukseklik (h,w), puruz (h,w). Damar U boyunca."""
    light, dark, shift = (np.array(c, np.float32) for c in WOOD[cls])
    u = np.arange(w, dtype=np.float32) / w * ATLAS_U_M
    v = np.arange(h, dtype=np.float32) / h * STRIP_M
    U, Vv = np.meshgrid(u, v)
    warp = fbm(h, w, 2, 9, rng, 3) - 0.5
    warp2 = fbm(h, w, 3, 40, rng, 2) - 0.5
    ringf = rng.uniform(9.0, 13.0)
    g = Vv * ringf + warp * 2.2 + warp2 * 0.35
    knot = np.zeros_like(U)
    for _ in range(int(rng.integers(1, 4))):
        ku, kv = rng.uniform(0.25, 3.75), rng.uniform(0.08, 0.42)
        du = (U - ku) / 0.075
        dv = (Vv - kv) / 0.03
        r2 = du * du + dv * dv
        g += 1.4 * np.exp(-r2 * 0.18)
        knot = np.maximum(knot, np.exp(-r2 * 1.2))
    t = g - np.floor(g)
    band = sstep(0.55, 0.92, t) * (1 - sstep(0.92, 1.0, t))
    fibre = fbm(h, w, 24, 260, rng, 2)
    patch = fbm(h, w, 2, 5, rng, 2)
    # el boyamasi: yumusak lekeler + damar bantlari + hafif lif
    mix = np.clip(band * 0.55 + knot * 0.75 + (fibre - 0.5) * 0.18, 0, 1)
    col = light[None, None, :] * (1 - mix[..., None]) + dark[None, None, :] * mix[..., None]
    col = col * (0.90 + 0.2 * patch[..., None]) + shift[None, None, :] * (patch[..., None] - 0.5) * 2
    # catlaklar (kiris ve cati tahtasinda belirgin)
    if cls != "plank":
        for _ in range(int(rng.integers(2, 5))):
            kv = rng.uniform(0.05, 0.45)
            u0, u1 = sorted(rng.uniform(0, 4, 2))
            wob = (fbm(1, w, 1, 12, rng, 2)[0] - 0.5) * 0.02
            d = np.abs(Vv - (kv + wob[None, :]))
            crack = np.exp(-(d / 0.004) ** 2) * sstep(u0, u0 + 0.3, U) * (1 - sstep(u1 - 0.3, u1, U))
            col *= (1 - 0.35 * crack[..., None])
            band = np.maximum(band, crack)
    height = 0.5 - band * 0.35 - knot * 0.25 + (fibre - 0.5) * 0.25
    rough = 0.80 + band * 0.08 - (fibre - 0.5) * 0.06
    return np.clip(col, 0, 1), height, rough


def end_tile(s, cls, rng):
    light, dark, _ = (np.array(c, np.float32) for c in WOOD[cls])
    light = light * 0.92
    y, x = np.mgrid[0:s, 0:s].astype(np.float32)
    cx, cy = s * rng.uniform(0.42, 0.58), s * rng.uniform(0.42, 0.58)
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / s * STRIP_M
    ang = np.arctan2(y - cy, x - cx)
    warp = fbm(s, s, 3, 3, rng, 3) - 0.5
    g = r * 26.0 + warp * 1.5
    t = g - np.floor(g)
    band = sstep(0.6, 0.95, t) * (1 - sstep(0.95, 1.0, t))
    mix = np.clip(band * 0.6 + np.exp(-(r / 0.012) ** 2) * 0.8, 0, 1)
    for _ in range(2):
        a0 = rng.uniform(-math.pi, math.pi)
        da = np.abs(np.angle(np.exp(1j * (ang - a0))))
        crack = np.exp(-(da / 0.03) ** 2) * (r > 0.03)
        mix = np.maximum(mix, crack * 0.9)
    col = light[None, None, :] * (1 - mix[..., None]) + dark[None, None, :] * mix[..., None]
    height = 0.5 - mix * 0.4
    return np.clip(col, 0, 1), height, 0.85 + mix * 0.05


def metal_tile(s, rng, rust):
    base = np.array((0.30, 0.31, 0.34), np.float32)
    n = fbm(s, s, 4, 4, rng, 3)
    col = base[None, None, :] * (0.8 + 0.4 * n[..., None])
    rough = 0.45 + 0.2 * n
    metal = np.full((s, s), 0.85, np.float32)
    if rust:
        r = sstep(0.5, 0.7, fbm(s, s, 3, 3, rng, 3))
        rc = np.array((0.50, 0.29, 0.17), np.float32)
        col = col * (1 - r[..., None]) + rc[None, None, :] * r[..., None]
        rough = rough * (1 - r) + 0.9 * r
        metal = metal * (1 - r) + 0.2 * r
    return np.clip(col, 0, 1), 0.5 + (n - 0.5) * 0.2, rough, metal


def make_textures(N, seed=4417):
    rng = np.random.default_rng(seed)
    SH = N // 8
    albedo = np.zeros((N, N, 3), np.float32)
    height = np.full((N, N), 0.5, np.float32)
    rough = np.full((N, N), 0.8, np.float32)
    metal = np.zeros((N, N), np.float32)
    for cls, rows in STRIPS.items():
        for row in rows:
            c, h, r = wood_strip(N, SH, cls, rng)
            albedo[row * SH:(row + 1) * SH] = c
            height[row * SH:(row + 1) * SH] = h
            rough[row * SH:(row + 1) * SH] = r
    y0 = 7 * SH
    for cls, tile in END_TILE.items():
        c, h, r = end_tile(SH, cls, rng)
        albedo[y0:y0 + SH, tile * SH:(tile + 1) * SH] = c
        height[y0:y0 + SH, tile * SH:(tile + 1) * SH] = h
        rough[y0:y0 + SH, tile * SH:(tile + 1) * SH] = r
    for tile, rust in ((3, False), (4, True)):
        c, h, r, m = metal_tile(SH, rng, rust)
        sl = (slice(y0, y0 + SH), slice(tile * SH, (tile + 1) * SH))
        albedo[sl] = c
        height[sl] = h
        rough[sl] = r
        metal[sl] = m
    # kalan karolar: duz kiris rengi (bos kalmasin)
    for tile in range(5, 8):
        albedo[y0:y0 + SH, tile * SH:(tile + 1) * SH] = np.array(WOOD["beam"][0], np.float32)
    # normal: yukseklikten (OpenGL, +Y yukari), hafif
    k = 2.2 * N / 1024.0
    gy, gx = np.gradient(height)
    nx, ny = -gx * k, -gy * k
    nz = np.ones_like(nx)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / ln, ny / ln, nz / ln], -1) * 0.5 + 0.5
    rm = np.stack([np.ones_like(rough), np.clip(rough, 0, 1), np.clip(metal, 0, 1)], -1)
    return albedo, normal, rm


def np_to_image(name, arr, path, non_color):
    h, w, _ = arr.shape
    img = bpy.data.images.get(name) or bpy.data.images.new(name, w, h, alpha=False)
    if img.size[0] != w:
        img.scale(w, h)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    rgba = np.concatenate([arr, np.ones((h, w, 1), np.float32)], -1).astype(np.float32)
    img.pixels.foreach_set(rgba.ravel())
    img.update()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.pack()
    return img


# ---------------------------------------------------------------------------
# Geometri: dis bukey cokgenlerden olusan parcalar
# ---------------------------------------------------------------------------
def newell(pts):
    n = Vector((0, 0, 0))
    m = len(pts)
    for i in range(m):
        p, q = pts[i], pts[(i + 1) % m]
        n.x += (p.y - q.y) * (p.z + q.z)
        n.y += (p.z - q.z) * (p.x + q.x)
        n.z += (p.x - q.x) * (p.y + q.y)
    return n


def dedupe(pts, eps=1e-5):
    out = []
    for p in pts:
        if not out or (p - out[-1]).length > eps:
            out.append(p)
    while len(out) > 1 and (out[0] - out[-1]).length <= eps:
        out.pop()
    return out


class Part:
    def __init__(self, cls, grain, smooth=False, tint=None, mapping="box", axis_pt=None):
        self.cls = cls
        self.grain = Vector(grain).normalized()
        self.smooth = smooth
        self.tint = tint
        self.mapping = mapping
        self.axis_pt = axis_pt
        self.polys = []  # (list[Vector], tag)
        self.node = None  # ayri dugum adi (kapi kanadi)

    def clip(self, n, d, cut_tag="side"):
        """dot(n,p) <= d tarafini tutar; dis bukey govdeyi duzlemle keser ve kapatir."""
        n = Vector(n).normalized()
        out, cut = [], []
        eps = 1e-6
        for pts, tag in self.polys:
            res = []
            m = len(pts)
            for i in range(m):
                p, q = pts[i], pts[(i + 1) % m]
                dp, dq = n.dot(p) - d, n.dot(q) - d
                if dp <= eps:
                    res.append(p.copy())
                    if dp >= -eps:
                        cut.append(p.copy())
                if (dp < -eps and dq > eps) or (dp > eps and dq < -eps):
                    t = dp / (dp - dq)
                    x = p.lerp(q, t)
                    res.append(x)
                    cut.append(x.copy())
            res = dedupe(res)
            if len(res) >= 3 and newell(res).length > 1e-9:
                out.append((res, tag))
        if len(cut) >= 3:
            c = sum(cut, Vector()) / len(cut)
            a = n.orthogonal().normalized()
            b = n.cross(a)
            uniq = []
            for p in cut:
                if all((p - q).length > 1e-5 for q in uniq):
                    uniq.append(p)
            uniq.sort(key=lambda p: math.atan2((p - c).dot(b), (p - c).dot(a)))
            if len(uniq) >= 3 and newell(uniq).length > 1e-9:
                out.append((uniq, cut_tag))
        self.polys = out
        return self

    def transform(self, fn):
        self.polys = [([fn(p) for p in pts], tag) for pts, tag in self.polys]
        return self

    def fix_orientation(self):
        allp = [p for pts, _ in self.polys for p in pts]
        if not allp:
            return
        c = sum(allp, Vector()) / len(allp)
        fixed = []
        for pts, tag in self.polys:
            fc = sum(pts, Vector()) / len(pts)
            n = newell(pts)
            if n.dot(fc - c) < 0:
                pts = list(reversed(pts))
            fixed.append((pts, tag))
        self.polys = fixed

    def tri_count(self):
        return sum(len(p) - 2 for p, _ in self.polys)


def offset_poly(P, c):
    """CCW dis bukey cokgeni c kadar iceri ofsetler."""
    n = len(P)
    out = []
    for i in range(n):
        a, b, d = P[i - 1], P[i], P[(i + 1) % n]
        e1 = (b - a).normalized()
        e2 = (d - b).normalized()
        n1 = Vector((-e1.y, e1.x))
        n2 = Vector((-e2.y, e2.x))
        den = 1 + n1.dot(n2)
        out.append(b + (n1 + n2) * (c / max(den, 0.2)))
    return out


def area2(P):
    return 0.5 * sum(P[i].x * P[(i + 1) % len(P)].y - P[(i + 1) % len(P)].x * P[i].y for i in range(len(P)))


def prism(profile, O, ex, ey, L, bevel=BEV, cap0=True, cap1=True):
    """profile (ex,ey) duzleminde dis bukey cokgen; ez = ex x ey boyunca L uzunlugunda pahli prizma."""
    ex, ey = Vector(ex).normalized(), Vector(ey).normalized()
    ez = ex.cross(ey).normalized()
    O = Vector(O)
    P = [Vector((float(p[0]), float(p[1]))) for p in profile]
    P2 = []
    for p in P:
        if not P2 or (p - P2[-1]).length > 1e-4:
            P2.append(p)
    if len(P2) > 1 and (P2[0] - P2[-1]).length <= 1e-4:
        P2.pop()
    P = P2
    if len(P) < 3:
        return []
    if area2(P) < 0:
        P.reverse()
    n = len(P)

    def W(p, d):
        return O + ex * p.x + ey * p.y + ez * d

    polys = []
    if bevel <= 0:
        for i in range(n):
            j = (i + 1) % n
            polys.append(([W(P[i], 0), W(P[j], 0), W(P[j], L), W(P[i], L)], "side"))
        if cap0:
            polys.append(([W(p, 0) for p in P], "cap"))
        if cap1:
            polys.append(([W(p, L) for p in P], "cap"))
        return polys
    min_edge = min((P[(i + 1) % n] - P[i]).length for i in range(n))
    c = min(bevel, 0.3 * min_edge, 0.3 * L)
    # sivri koselerde ofset tasmasin
    for i in range(n):
        e1 = (P[i] - P[i - 1]).normalized()
        e2 = (P[(i + 1) % n] - P[i]).normalized()
        if 1 + Vector((-e1.y, e1.x)).dot(Vector((-e2.y, e2.x))) < 0.6:
            c = min(c, 0.12 * min_edge)
    inset = offset_poly(P, c)
    A, B = [], []
    for i in range(n):
        A.append(P[i] + (P[i - 1] - P[i]).normalized() * c)
        B.append(P[i] + (P[(i + 1) % n] - P[i]).normalized() * c)
    d0, d1, d2, d3 = 0.0, c, L - c, L
    if cap0:
        polys.append(([W(q, d0) for q in inset], "cap"))
    if cap1:
        polys.append(([W(q, d3) for q in inset], "cap"))
    for dA, dB in ((d0, d1), (d3, d2)):
        for i in range(n):
            j = (i + 1) % n
            polys.append(([W(inset[i], dA), W(A[i], dB), W(B[i], dB)], "bevel"))
            polys.append(([W(inset[i], dA), W(B[i], dB), W(A[j], dB), W(inset[j], dA)], "bevel"))
    for i in range(n):
        j = (i + 1) % n
        polys.append(([W(A[i], d1), W(B[i], d1), W(B[i], d2), W(A[i], d2)], "bevel"))
        polys.append(([W(B[i], d1), W(A[j], d1), W(A[j], d2), W(B[i], d2)], "side"))
    return polys


class Kit:
    """Bir parcanin yapim tezgahi."""

    def __init__(self, pid, seed=None):
        self.pid = pid
        self.rng = random.Random(seed if seed is not None else zlib.crc32(pid.encode()))
        self.parts = []
        self.sockets = []
        self.colliders = []
        self.nodes = {}  # ad -> pivot (Godot)
        self.cur_node = None

    # --- parca ekleyiciler ---
    def add(self, part):
        part.node = self.cur_node
        self.parts.append(part)
        return part

    def wood_tint(self, cls):
        r = self.rng
        k = r.uniform(0.88, 0.99)
        w = r.uniform(-0.03, 0.03)
        return (k + w, k, k - w)

    def box(self, mn, mx, cls="plank", axis=None, bevel=None, tint=None, cap0=True, cap1=True):
        mn, mx = Vector(mn), Vector(mx)
        size = mx - mn
        if axis is None:
            axis = max(range(3), key=lambda i: size[i])
        if axis == 0:
            ex, ey, ez = Y, Z, X
        elif axis == 1:
            ex, ey, ez = Z, X, Y
        else:
            ex, ey, ez = X, Y, Z
        prof = [(mn.dot(ex), mn.dot(ey)), (mx.dot(ex), mn.dot(ey)), (mx.dot(ex), mx.dot(ey)), (mn.dot(ex), mx.dot(ey))]
        O = ez * mn.dot(ez)
        if bevel is None:
            bevel = BEAM_BEV if cls == "beam" else BEV
        p = Part(cls, ez, tint=tint or self.wood_tint(cls))
        p.polys = prism(prof, O, ex, ey, size[axis], bevel, cap0, cap1)
        return self.add(p)

    def prism_part(self, profile, O, ex, ey, L, cls="plank", grain=None, bevel=None, tint=None, cap0=True, cap1=True):
        ex, ey = Vector(ex), Vector(ey)
        ez = ex.cross(ey).normalized()
        if bevel is None:
            bevel = BEAM_BEV if cls == "beam" else BEV
        p = Part(cls, grain if grain is not None else ez, tint=tint or self.wood_tint(cls))
        p.polys = prism(profile, O, ex, ey, L, bevel, cap0, cap1)
        return self.add(p)

    def prism_z(self, prof_xy, z0, z1, **kw):
        """profil (x, y), Z boyunca z0..z1."""
        return self.prism_part(prof_xy, Z * z0, X, Y, z1 - z0, **kw)

    def prism_x(self, prof_zy, x0, x1, **kw):
        """profil (z, y), X boyunca x0..x1."""
        prof = [(p[1], p[0]) for p in prof_zy]  # (y, z): ex=Y, ey=Z -> ez=X
        return self.prism_part(prof, X * x0, Y, Z, x1 - x0, **kw)

    def prism_y(self, prof_xz, y0, y1, **kw):
        """profil (x, z), Y boyunca y0..y1."""
        prof = [(p[1], p[0]) for p in prof_xz]  # (z, x): ex=Z, ey=X -> ez=Y
        return self.prism_part(prof, Y * y0, Z, X, y1 - y0, **kw)

    def obox(self, center, axis, up, length, width, height, cls="beam", bevel=None, tint=None, cap0=True, cap1=True):
        ez = Vector(axis).normalized()
        ey = (Vector(up) - ez * Vector(up).dot(ez)).normalized()
        ex = ey.cross(ez).normalized()
        O = Vector(center) - ez * (length / 2)
        prof = [(-width / 2, -height / 2), (width / 2, -height / 2), (width / 2, height / 2), (-width / 2, height / 2)]
        return self.prism_part(prof, O, ex, ey, length, cls=cls, grain=ez, bevel=bevel, tint=tint, cap0=cap0, cap1=cap1)

    def beam_between(self, p0, p1, width, height, up=Y, cls="beam", **kw):
        p0, p1 = Vector(p0), Vector(p1)
        ax = p1 - p0
        return self.obox((p0 + p1) / 2, ax, up if abs(ax.normalized().dot(Vector(up))) < 0.95 else X, ax.length, width, height, cls=cls, **kw)

    def log(self, p0, p1, r, sides=8, tip=0.0, tip_r=0.5, cls="beam", tint=None, cap0=False):
        """Yuvarlak kutuk: p0'dan p1'e; tip > 0 ise ucu kesik koni (sivri degil, SANAT-SES §1)."""
        p0, p1 = Vector(p0), Vector(p1)
        ez = (p1 - p0).normalized()
        L = (p1 - p0).length
        ex = ez.orthogonal().normalized()
        ey = ez.cross(ex)
        off = self.rng.uniform(0, math.tau)

        def ring(d, rr):
            return [p0 + ez * d + (ex * math.cos(off + math.tau * i / sides) + ey * math.sin(off + math.tau * i / sides)) * rr for i in range(sides)]

        polys = []
        r0 = ring(0, r)
        r1 = ring(L - tip, r)
        for i in range(sides):
            j = (i + 1) % sides
            polys.append(([r0[i], r0[j], r1[j], r1[i]], "side"))
        top = r1
        if tip > 0:
            r2 = ring(L, r * tip_r)
            for i in range(sides):
                j = (i + 1) % sides
                polys.append(([r1[i], r1[j], r2[j], r2[i]], "bevel"))
            top = r2
        polys.append((top, "cap"))
        if cap0:
            polys.append((r0, "cap"))
        p = Part(cls, ez, smooth=True, tint=tint or self.wood_tint(cls), mapping="cyl", axis_pt=p0)
        p.polys = polys
        return self.add(p)

    def bolt(self, pos, normal, r=0.05, h=0.035, rust=None):
        n = Vector(normal).normalized()
        ex = n.orthogonal().normalized()
        ey = n.cross(ex)
        a0 = self.rng.uniform(0, math.pi)
        prof = [(math.cos(a0 + i * math.pi / 3) * r, math.sin(a0 + i * math.pi / 3) * r) for i in range(6)]
        O = Vector(pos) - n * 0.01
        p = Part("metal", n, tint=(1, 1, 1))
        p.rust = self.rng.random() < 0.35 if rust is None else rust
        p.polys = prism(prof, O, ex, ey, h + 0.01, 0, cap0=False, cap1=True)
        return self.add(p)

    def strap(self, mn, mx, rust=None):
        p = self.box(mn, mx, cls="metal", bevel=0.008, tint=(1, 1, 1))
        p.rust = self.rng.random() < 0.4 if rust is None else rust
        return p

    # --- yuvalar ve carpisma ---
    def socket(self, typ, pos, d, name=None):
        d = Vector(d)
        if d.length > 0:
            d.normalize()
        self.sockets.append({
            "name": name or f"{typ}_{len(self.sockets)}",
            "type": typ,
            "pos": [round(c, 4) for c in pos],
            "dir": [round(c, 4) for c in d],
        })

    def col_box(self, mn, mx, node=None):
        mn, mx = Vector(mn), Vector(mx)
        c = (mn + mx) / 2
        s = mx - mn
        d = {"type": "box", "center": [round(v, 4) for v in c], "size": [round(v, 4) for v in s],
             "basis": [[1, 0, 0], [0, 1, 0], [0, 0, 1]]}
        if node:
            d["node"] = node
        self.colliders.append(d)

    def col_obox(self, center, axis, up, length, width, height, node=None):
        ez = Vector(axis).normalized()
        ey = (Vector(up) - ez * Vector(up).dot(ez)).normalized()
        ex = ey.cross(ez).normalized()
        d = {"type": "box", "center": [round(v, 4) for v in center], "size": [round(width, 4), round(height, 4), round(length, 4)],
             "basis": [[round(v, 5) for v in ex], [round(v, 5) for v in ey], [round(v, 5) for v in ez]]}
        if node:
            d["node"] = node
        self.colliders.append(d)

    def col_convex(self, pts, node=None):
        d = {"type": "convex", "points": [[round(c, 4) for c in p] for p in pts]}
        if node:
            d["node"] = node
        self.colliders.append(d)

    def wall_sockets(self, top_h, stack=True, ends=True):
        self.socket("wall_bottom", (0, 0, 0), (0, 0, 1), "bottom")
        if stack:
            self.socket("wall_top", (0, top_h, 0), (0, 0, 1), "top")
        if ends:
            self.socket("wall_end", (-G / 2, 0, 0), (-1, 0, 0), "end_l")
            self.socket("wall_end", (G / 2, 0, 0), (1, 0, 0), "end_r")

    def square_sockets(self, half=G / 2, y=0.0, cells=True, edge_attach=True):
        """Kare yuzey (temel/zemin/platform): kenar orta noktalari (3 m'de bir), koseler, hucre merkezleri."""
        n = int(round(2 * half / G))
        for k in range(n):
            t = -half + G / 2 + k * G
            self.socket("edge", (t, y, half), (0, 0, 1))
            self.socket("edge", (t, y, -half), (0, 0, -1))
            self.socket("edge", (half, y, t), (1, 0, 0))
            self.socket("edge", (-half, y, t), (-1, 0, 0))
        for i in range(n + 1):
            for j in range(n + 1):
                self.socket("corner", (-half + i * G, y, -half + j * G), (0, 0, 1))
        if cells:
            for i in range(n):
                for j in range(n):
                    self.socket("cell", (-half + G / 2 + i * G, y, -half + G / 2 + j * G), (0, 0, 1))


# ---------------------------------------------------------------------------
# Parcalar
# ---------------------------------------------------------------------------
def deck_planks(k, half_x, half_z, y_top, t, n, cls="plank", along="x", gap=0.0, bevel=None, splits=1):
    """Duz kalas doseme: n sira; her sira 'splits' parcaya bolunur (derzler kaydirilir)."""
    r = k.rng
    w = 2 * half_z / n if along == "x" else 2 * half_x / n
    for i in range(n):
        a0 = (-half_z if along == "x" else -half_x) + i * w + gap / 2
        a1 = a0 + w - gap
        top = y_top - r.uniform(0.0, 0.008)
        L = 2 * (half_x if along == "x" else half_z)
        cuts = [-L / 2]
        if splits > 1:
            seg = L / splits
            jitter = r.uniform(-0.25, 0.25) * seg if i % 2 else 0
            for s in range(1, splits):
                cuts.append(-L / 2 + s * seg + (jitter if s == 1 else 0))
        cuts.append(L / 2)
        for s in range(len(cuts) - 1):
            l0 = cuts[s] + (r.uniform(0, 0.015) if s == 0 else 0.006)
            l1 = cuts[s + 1] - (r.uniform(0, 0.015) if s == len(cuts) - 2 else 0.006)
            if along == "x":
                k.box((l0, top - t, a0), (l1, top, a1), cls, axis=0, bevel=bevel)
            else:
                k.box((a0, top - t, l0), (a1, top, l1), cls, axis=2, bevel=bevel)


def square_foundation(k, depth, piles=False):
    """Kare temel govdesi (orijin ust yuz merkezi)."""
    h = G / 2
    deck_planks(k, h, h, 0.0, 0.18, 8)
    rim = 0.26
    k.box((-h, -0.52, h - rim), (h, -0.18, h), "beam")
    k.box((-h, -0.52, -h), (h, -0.18, -h + rim), "beam")
    k.box((h - rim, -0.52, -h + rim), (h, -0.18, h - rim), "beam")
    k.box((-h, -0.52, -h + rim), (-h + rim, -0.18, h - rim), "beam")
    k.box((-h + rim, -0.44, -0.13), (h - rim, -0.18, 0.13), "beam", bevel=0)  # orta kiris (gorunmez)
    ps = 0.34 if not piles else 0.38
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = sx * (h - 0.04 - ps / 2), sz * (h - 0.04 - ps / 2)
            if piles:
                k.log((cx, -depth, cz), (cx, -0.52, cz), ps / 2 + 0.02, sides=8, cap0=False)
            else:
                k.box((cx - ps / 2, -depth, cz - ps / 2), (cx + ps / 2, -0.52, cz + ps / 2), "beam", axis=1, cap0=False)
            # kose civatalari (iki dis yuz)
            k.bolt((sx * h, -0.35, sz * (h - 0.13)), (sx, 0, 0))
            k.bolt((sx * (h - 0.13), -0.35, sz * h), (0, 0, sz))
    if not piles:
        k.box((-0.17, -depth * 0.75, -0.17), (0.17, -0.44, 0.17), "beam", axis=1, bevel=0, cap0=False)
    for s in (-1, 1):
        k.bolt((0.35 * s, -0.35, h), (0, 0, 1))
        k.bolt((0.35 * s, -0.35, -h), (0, 0, -1))
    k.square_sockets()


def p_kare_temel():
    k = Kit("kare_temel")
    square_foundation(k, 1.5)
    k.col_box((-1.5, -1.5, -1.5), (1.5, 0, 1.5))
    return k, dict(kind="foundation", name="Kare Temel", origin="top_center", ground=dict(mode="top", clearance=0.35, depth=1.45))


def p_kazik_temel():
    k = Kit("kazik_temel")
    depth = 3.5
    square_foundation(k, depth, piles=True)
    h = G / 2
    # capraz destekler ve bag kirisleri (kaziklar arasi)
    c = h - 0.04 - 0.19
    for (a, b) in (((-c, c), (c, c)), ((-c, -c), (c, -c)), ((c, -c), (c, c)), ((-c, -c), (-c, c))):
        p0 = Vector((a[0], -2.9, a[1]))
        p1 = Vector((b[0], -0.62, b[1]))
        side = Vector((a[0] + b[0], 0, a[1] + b[1])).normalized()
        off = side * 0.21
        k.beam_between(p0 + off, p1 + off, 0.2, 0.12, up=side)
        k.beam_between(Vector((a[0], -2.2, a[1])) + off * 1.0, Vector((b[0], -2.2, b[1])) + off * 1.0, 0.16, 0.2, up=Y, bevel=0.03)
    k.col_box((-1.5, -depth, -1.5), (1.5, 0, 1.5))
    return k, dict(kind="foundation", name="Kazık Temel", origin="top_center", ground=dict(mode="top", clearance=0.6, depth=3.4))


def tri_pts(scale_in=0.0):
    """Eskenar ucgen (x,z): taban +Z'de. scale_in: kenarlardan iceri ofset."""
    A = Vector((-1.5, TRI_IN))
    B = Vector((1.5, TRI_IN))
    C = Vector((0.0, -TRI_OUT))
    P = [A, C, B]  # (x,z) duzleminde... yon prizma tarafindan duzeltilir
    if scale_in > 0:
        # merkez (0,0) -> ic yaricap kucult
        f = (TRI_IN - scale_in) / TRI_IN
        P = [p * f for p in P]
    return P


def clip_poly2(P, a, b, c):
    """a*x + b*y + c >= 0 tarafini tutar."""
    out = []
    n = len(P)
    for i in range(n):
        p, q = P[i], P[(i + 1) % n]
        dp, dq = a * p.x + b * p.y + c, a * q.x + b * q.y + c
        if dp >= 0:
            out.append(p)
        if (dp >= 0) != (dq >= 0):
            t = dp / (dp - dq)
            out.append(p.lerp(q, t))
    return [Vector((p.x, p.y)) for p in dedupe([Vector((p.x, p.y, 0)) for p in out])]


def tri_deck(k, y_top, t, rows, rim_y0, rim_y1, cls="plank"):
    T = tri_pts()
    T_in = tri_pts(0.0)
    zs = np.linspace(TRI_IN, -TRI_OUT, rows + 1)
    for i in range(rows):
        z0, z1 = zs[i], zs[i + 1]
        P = [Vector((p.x, p.y)) for p in T_in]
        P = clip_poly2(P, 0, -1, z0 - 0.003)   # z <= z0
        P = clip_poly2(P, 0, 1, -z1 - 0.003)   # z >= z1
        if len(P) < 3 or abs(area2(P)) < 0.01:
            continue
        top = y_top - k.rng.uniform(0, 0.008)
        k.prism_y([(p.x, p.y) for p in P], top - t, top, cls=cls, grain=X)
    # mitre kesimli kenar kirisleri
    inner = tri_pts(0.26)
    for i in range(3):
        a, b = T[i], T[(i + 1) % 3]
        ai, bi = inner[i], inner[(i + 1) % 3]
        prof = [(a.x, a.y), (b.x, b.y), (bi.x, bi.y), (ai.x, ai.y)]
        k.prism_y(prof, rim_y0, rim_y1, cls="beam", grain=Vector((b.x - a.x, 0, b.y - a.y)))
    return T


def tri_sockets(k, y=0.0):
    T = tri_pts()
    for i in range(3):
        a, b = T[i], T[(i + 1) % 3]
        m = (a + b) / 2
        k.socket("edge", (m.x, y, m.y), (m.x, 0, m.y))
        k.socket("corner", (a.x, y, a.y), (0, 0, 1))


def p_ucgen_temel():
    k = Kit("ucgen_temel")
    T = tri_deck(k, 0.0, 0.18, 7, -0.52, -0.18)
    for p in tri_pts(0.3):
        k.box((p.x - 0.17, -1.5, p.y - 0.17), (p.x + 0.17, -0.52, p.y + 0.17), "beam", axis=1, cap0=False)
    for i in range(3):
        a, b = T[i], T[(i + 1) % 3]
        m = (a + b) / 2
        nrm = Vector((m.x, 0, m.y)).normalized()
        for t in (0.3, 0.7):
            p = a.lerp(b, t)
            k.bolt((p.x, -0.35, p.y), nrm)
    tri_sockets(k)
    pts = [(p.x, y, p.y) for p in T for y in (0.0, -1.5)]
    k.col_convex(pts)
    return k, dict(kind="foundation", name="Üçgen Temel", origin="top_center", ground=dict(mode="top", clearance=0.35, depth=1.45))


# --- duvarlar ---
def wall_frame(k, top_h, posts=True, sill=True, plate=True, stud=True):
    """Duvar iskeleti: alt kiris, uc dikmeler, ust kiris. Doner: dolgu araligi (y0, y1)."""
    h = G / 2
    y_sill = 0.2
    y_plate = top_h - 0.01 - 0.21
    if sill:
        k.box((-h, -0.02, -0.16), (h, y_sill, 0.16), "beam", axis=0)
    if plate:
        k.box((-h, y_plate, -0.15), (h, top_h - 0.01, 0.15), "beam", axis=0)
    if posts:
        for s in (-1, 1):
            k.box((min(s * h, s * (h - 0.26)), y_sill, -0.14), (max(s * h, s * (h - 0.26)), y_plate, 0.14), "beam", axis=1)
    return y_sill, y_plate


def plank_rows(k, x0, x1, y0, y1, n=None, depth=0.08, cls="plank", min_h=0.06):
    if y1 - y0 < min_h:
        return
    if n is None:
        n = max(1, round((y1 - y0) / 0.43))
    r = k.rng
    ws = [r.uniform(0.85, 1.15) for _ in range(n)]
    tot = sum(ws)
    y = y0
    for i in range(n):
        hgt = (y1 - y0) * ws[i] / tot
        d = depth + r.uniform(-0.008, 0.008)
        k.box((x0, y, -d), (x1, y + hgt, d), cls, axis=0)
        y += hgt


def bolts_both(k, x, y, z=0.11):
    k.bolt((x, y, z), (0, 0, 1))
    k.bolt((x, y, -z), (0, 0, -1))


def p_duvar():
    k = Kit("duvar")
    ys, yp = wall_frame(k, S)
    plank_rows(k, -1.26, 1.26, ys, yp, 6)
    k.box((-0.12, ys, -0.11), (0.12, yp, 0.11), "beam", axis=1)
    # kosebentler (payanda)
    k.beam_between((-1.24, ys + 0.12, 0), (-0.12, ys + 1.25, 0), 0.17, 0.21, up=Z, cls="beam")
    k.beam_between((1.24, ys + 0.12, 0), (0.12, ys + 1.25, 0), 0.17, 0.21, up=Z, cls="beam")
    for y in (ys + 0.1, yp - 0.1):
        bolts_both(k, 0, y)
    for x in (-1.37, 1.37):
        bolts_both(k, x, yp - 0.12, 0.14)
    k.wall_sockets(S)
    k.col_box((-1.5, 0, -0.16), (1.5, S, 0.16))
    return k, dict(kind="wall", name="Duvar", origin="bottom_center")


def p_yarim_duvar():
    k = Kit("yarim_duvar")
    hh = S / 2
    ys, yp = wall_frame(k, hh)
    plank_rows(k, -1.26, 1.26, ys, yp, 3)
    k.box((-0.12, ys, -0.11), (0.12, yp, 0.11), "beam", axis=1)
    for y in (ys + 0.1, yp - 0.1):
        bolts_both(k, 0, y)
    k.wall_sockets(hh)
    k.col_box((-1.5, 0, -0.16), (1.5, hh, 0.16))
    return k, dict(kind="wall", name="Yarım Duvar", origin="bottom_center")


def p_alcak_duvar():
    k = Kit("alcak_duvar")
    hh = 1.0
    h = G / 2
    k.box((-h, -0.02, -0.16), (h, 0.2, 0.16), "beam", axis=0)
    for s in (-1, 1):
        k.box((min(s * h, s * (h - 0.26)), 0.2, -0.14), (max(s * h, s * (h - 0.26)), 0.8, 0.14), "beam", axis=1)
    plank_rows(k, -1.26, 1.26, 0.2, 0.8, 2)
    k.box((-h, 0.8, -0.19), (h, 0.99, 0.19), "beam", axis=0)  # genis ust tahta (dirsek dayanir)
    bolts_both(k, -0.6, 0.1, 0.16)
    bolts_both(k, 0.6, 0.1, 0.16)
    k.wall_sockets(hh)
    k.col_box((-1.5, 0, -0.19), (1.5, hh, 0.19))
    return k, dict(kind="wall", name="Alçak Duvar", origin="bottom_center")


def p_kapi_kasasi():
    k = Kit("kapi_kasasi")
    h = G / 2
    ow, oh = 0.75, 2.3  # yarim genislik, yukseklik
    jw = 0.16
    for s in (-1, 1):
        k.box((min(s * h, s * (ow + jw)), -0.02, -0.16), (max(s * h, s * (ow + jw)), 0.2, 0.16), "beam", axis=0)
    k.box((-h, TOP - 0.21, -0.15), (h, TOP, 0.15), "beam", axis=0)
    for s in (-1, 1):
        k.box((min(s * h, s * (h - 0.26)), 0.2, -0.14), (max(s * h, s * (h - 0.26)), TOP - 0.21, 0.14), "beam", axis=1)
        # kasa dikmeleri
        k.box((min(s * ow, s * (ow + jw)), -0.02, -0.15), (max(s * ow, s * (ow + jw)), oh, 0.15), "beam", axis=1)
        plank_rows(k, min(s * (h - 0.24), s * (ow + jw - 0.02)), max(s * (h - 0.24), s * (ow + jw - 0.02)), 0.2, TOP - 0.21, 5)
        bolts_both(k, s * (ow + jw / 2), oh - 0.1, 0.15)
        bolts_both(k, s * (ow + jw / 2), 0.25, 0.15)
    k.box((-ow - jw - 0.1, oh, -0.15), (ow + jw + 0.1, oh + 0.26, 0.15), "beam", axis=0)  # lento
    plank_rows(k, -ow - jw, ow + jw, oh + 0.26, TOP - 0.21, 1)
    k.box((-ow, -0.02, -0.17), (ow, 0.04, 0.17), "beam", axis=0, bevel=0.02)  # esik
    k.wall_sockets(S)
    k.col_box((-h, 0, -0.16), (-ow, S, 0.16))
    k.col_box((ow, 0, -0.16), (h, S, 0.16))
    k.col_box((-ow, oh, -0.16), (ow, S, 0.16))
    return k, dict(kind="wall", name="Kapı Kasası", origin="bottom_center", opening=dict(width=2 * ow, height=oh))


def p_pencereli_duvar():
    k = Kit("pencereli_duvar")
    h = G / 2
    wx, y0, y1 = 0.65, 1.0, 2.1
    jw = 0.14
    ys, yp = wall_frame(k, S)
    plank_rows(k, -1.26, 1.26, ys, 0.9, 2)
    plank_rows(k, -1.26, 1.26, 2.26, yp, 1)
    for s in (-1, 1):
        plank_rows(k, min(s * 1.26, s * (wx + jw - 0.02)), max(s * 1.26, s * (wx + jw - 0.02)), 0.9, 2.26, 3)
        k.box((min(s * wx, s * (wx + jw)), y0, -0.14), (max(s * wx, s * (wx + jw)), y1, 0.14), "beam", axis=1)
    k.box((-wx - jw - 0.08, 0.9, -0.21), (wx + jw + 0.08, 1.0, 0.21), "beam", axis=0)  # pervaz (tasar)
    k.box((-wx - jw - 0.06, y1, -0.15), (wx + jw + 0.06, 2.26, 0.15), "beam", axis=0)  # lento
    # boyali kepenkler (dis yuz, kooperatif mavisi)
    blue = (0.42, 0.62, 0.95)
    for s in (-1, 1):
        xa, xb = s * (wx + jw + 0.02), s * (wx + jw + 0.02 + 0.44)
        x0_, x1_ = min(xa, xb), max(xa, xb)
        mid = (x0_ + x1_) / 2
        k.box((x0_, 0.98, 0.09), (mid - 0.004, 2.18, 0.15), "plank", axis=1, tint=blue, bevel=0.02)
        k.box((mid + 0.004, 0.98, 0.09), (x1_, 2.18, 0.15), "plank", axis=1, tint=blue, bevel=0.02)
        for yy in (1.12, 2.0):
            k.box((x0_ + 0.02, yy, 0.15), (x1_ - 0.02, yy + 0.1, 0.19), "plank", axis=0, tint=(blue[0] * 0.85, blue[1] * 0.85, blue[2] * 0.85), bevel=0.015)
        k.bolt((s * (wx + jw + 0.05), 1.17, 0.19), (0, 0, 1), r=0.035, h=0.02)
        k.bolt((s * (wx + jw + 0.05), 2.05, 0.19), (0, 0, 1), r=0.035, h=0.02)
    k.wall_sockets(S)
    k.col_box((-h, 0, -0.16), (h, y0, 0.16))
    k.col_box((-h, y1, -0.16), (h, S, 0.16))
    k.col_box((-h, y0, -0.16), (-wx, y1, 0.16))
    k.col_box((wx, y0, -0.16), (h, y1, 0.16))
    return k, dict(kind="wall", name="Pencereli Duvar", origin="bottom_center", opening=dict(width=2 * wx, height=y1 - y0, sill=y0))


GABLE_C = 1.6  # ucgen duvar ust cizgisi: y = x + 1.6 (catinin alt yuzunun 5 cm altinda)


def p_ucgen_duvar():
    k = Kit("ucgen_duvar")
    h = G / 2
    top = lambda x: x + GABLE_C
    rd = 0.24 * math.sqrt(2)  # egik ust kirisin dusey kalinligi
    # egik ust kiris (paralelkenar, dusey uclar)
    prof = [(-h, top(-h) - rd), (h, top(h) - rd), (h, top(h)), (-h, top(-h))]
    P = [Vector(p) for p in prof]
    P = clip_poly2(P, 0, 1, 0.02)  # y >= -0.02
    k.prism_z([(p.x, p.y) for p in P], -0.15, 0.15, cls="beam", grain=Vector((1, 1, 0)))
    # alt kiris (egik kirisin altinda kirpilir)
    P = [Vector(p) for p in [(-h, -0.02), (h, -0.02), (h, 0.2), (-h, 0.2)]]
    P = clip_poly2(P, 1, -1, GABLE_C - rd)  # y <= x + c - rd
    if len(P) >= 3:
        k.prism_z([(p.x, p.y) for p in P], -0.16, 0.16, cls="beam", grain=X)
    # yuksek uc dikmesi
    P = [Vector(p) for p in [(h - 0.26, 0.2), (h, 0.2), (h, top(h)), (h - 0.26, top(h))]]
    P = clip_poly2(P, 1, -1, GABLE_C - rd)
    k.prism_z([(p.x, p.y) for p in P], -0.14, 0.14, cls="beam", grain=Y)
    # yatay kalaslar
    y = 0.2
    rows = 0
    while y < top(h) - rd - 0.05 and rows < 8:
        hgt = 0.43 * k.rng.uniform(0.85, 1.15)
        P = [Vector(p) for p in [(-h, y), (h - 0.24, y), (h - 0.24, y + hgt), (-h, y + hgt)]]
        P = clip_poly2(P, 1, -1, GABLE_C - rd + 0.02)
        if len(P) >= 3 and abs(area2(P)) > 0.03:
            d = 0.08 + k.rng.uniform(-0.008, 0.008)
            k.prism_z([(p.x, p.y) for p in P], -d, d, cls="plank", grain=X)
        y += hgt
        rows += 1
    bolts_both(k, h - 0.13, 0.4, 0.14)
    bolts_both(k, h - 0.13, 2.4, 0.14)
    k.wall_sockets(S, stack=False)
    pts = [(-h, 0, z) for z in (-0.16, 0.16)] + [(h, 0, z) for z in (-0.16, 0.16)] + [(h, top(h), z) for z in (-0.16, 0.16)] + [(-h, max(top(-h), 0.05), z) for z in (-0.16, 0.16)]
    k.col_convex(pts)
    return k, dict(kind="wall", name="Üçgen Duvar", origin="bottom_center", note="Beşik çatının alın üçgeni: yüksek ucu (+X) mahyaya bakar; karşı taraf için 180° döndürülür.")


# --- zeminler ---
def p_kare_zemin():
    k = Kit("kare_zemin")
    h = G / 2
    deck_planks(k, h, h, 0.0, 0.16, 8)
    k.box((-h, -0.38, h - 0.2), (h, -0.16, h), "beam", axis=0)
    k.box((-h, -0.38, -h), (h, -0.16, -h + 0.2), "beam", axis=0)
    for x in (-1.0, 0.0, 1.0):
        k.box((x - 0.09, -0.36, -h + 0.2), (x + 0.09, -0.16, h - 0.2), "beam", axis=2)
        k.bolt((x, -0.27, h), (0, 0, 1))
        k.bolt((x, -0.27, -h), (0, 0, -1))
    k.square_sockets()
    k.col_box((-h, -0.38, -h), (h, 0, h))
    return k, dict(kind="floor", name="Kare Zemin", origin="top_center")


def p_ucgen_zemin():
    k = Kit("ucgen_zemin")
    T = tri_deck(k, 0.0, 0.16, 7, -0.38, -0.16)
    k.box((-0.09, -0.36, -TRI_OUT + 0.5), (0.09, -0.16, TRI_IN - 0.26), "beam", axis=2, bevel=0.02)
    for i in range(3):
        a, b = T[i], T[(i + 1) % 3]
        m = (a + b) / 2
        k.bolt((m.x, -0.27, m.y), Vector((m.x, 0, m.y)))
    tri_sockets(k)
    pts = [(p.x, y, p.y) for p in T for y in (0.0, -0.38)]
    k.col_convex(pts)
    return k, dict(kind="floor", name="Üçgen Zemin", origin="top_center")


# --- merdiven, rampa ---
def p_merdiven():
    k = Kit("merdiven")
    hw = 1.2
    n = 10
    rise, run = S / n, G / n
    for i in range(1, n + 1):
        ytop = rise * i
        z1 = G / 2 - run * (i - 1) + (0.05 if i > 1 else 0.0)
        z0 = G / 2 - run * i
        k.box((-hw + 0.06, ytop - 0.1, z0), (hw - 0.06, ytop - k.rng.uniform(0, 0.006), min(z1, G / 2)), "plank", axis=0)
    # yan kirisler (dusey kesilmis uclar)
    for s in (-1, 1):
        lo = lambda z: (G / 2 - z) - 0.36
        hi = lambda z: (G / 2 - z) + 0.12
        prof = [Vector((G / 2, lo(G / 2))), Vector((-G / 2, lo(-G / 2))), Vector((-G / 2, hi(-G / 2))), Vector((G / 2, hi(G / 2)))]
        prof = clip_poly2(prof, 0, 1, 0.02)          # y >= -0.02
        prof = clip_poly2(prof, 0, -1, S - 0.04)     # y <= S - 0.04
        x0, x1 = (hw - 0.06, hw + 0.1) if s > 0 else (-hw - 0.1, -hw + 0.06)
        k.prism_x([(p.x, p.y) for p in prof], x0, x1, cls="beam", grain=Vector((0, 1, -1)))
        for i in (2, 5, 8):
            z = G / 2 - run * i + run / 2
            k.bolt((s * (hw + 0.1), rise * i - 0.2, z), (s, 0, 0))
    # alt dayanak
    k.box((-hw - 0.1, -0.02, G / 2 - 0.25), (hw + 0.1, 0.1, G / 2), "beam", axis=0)
    k.socket("cell_bottom", (0, 0, 0), (0, 0, 1), "bottom")
    k.socket("stair_bottom", (0, 0, G / 2), (0, 0, 1), "low_edge")
    k.socket("stair_top", (0, S, -G / 2), (0, 0, -1), "high_edge")
    ax = Vector((0, S, -G)).normalized()
    up = Vector((0, 1, 1)).normalized()
    c = Vector((0, S / 2, 0)) - up * 0.18
    k.col_obox(c, ax, up, math.hypot(S, G), 2 * hw + 0.2, 0.36)
    return k, dict(kind="stairs", name="Merdiven", origin="bottom_center", note="45°: oyuncunun floor_max_angle'ı ≥ 46° olmalı.")


def p_rampa():
    k = Kit("rampa")
    hw = 1.5
    rise = S / 2
    L = math.hypot(G, rise)
    d = Vector((0, rise, -G)).normalized()
    nrm = Vector((0, G, rise)).normalized()
    base = Vector((0, 0, G / 2))
    n = 8
    w = L / n
    for i in range(n):
        s0 = i * w
        c = base + d * (s0 + w / 2) - nrm * 0.07
        k.obox(c, X, nrm, 2 * hw - 0.02, w - 0.012, 0.14, cls="plank", bevel=BEV)
        if i % 2 == 1:
            k.obox(base + d * (s0 + w / 2) + nrm * 0.04, X, nrm, 2 * hw - 0.3, 0.12, 0.08, cls="beam", bevel=0.02)
    for s in (-1, 1):
        x = s * (hw - 0.12)
        p0 = base + Vector((x, 0, 0)) - nrm * 0.14
        k.beam_between(p0 - d * 0.0 + Vector((0, -0.12, 0)) * 0, p0 + d * L, 0.2, 0.22, up=nrm)
        k.box((x - 0.12, -0.5, -G / 2 + 0.05), (x + 0.12, rise - 0.2, -G / 2 + 0.29), "beam", axis=1, cap0=False)
        k.box((x - 0.12, -0.5, -0.12), (x + 0.12, rise / 2 - 0.2, 0.12), "beam", axis=1, cap0=False)
        k.bolt((s * hw - s * 0.0, rise - 0.33, -G / 2 + 0.17), (s, 0, 0))
    k.socket("cell_bottom", (0, 0, 0), (0, 0, 1), "bottom")
    k.socket("stair_bottom", (0, 0, G / 2), (0, 0, 1), "low_edge")
    k.socket("stair_top", (0, rise, -G / 2), (0, 0, -1), "high_edge")
    c = Vector((0, rise / 2, 0)) - nrm * 0.15
    k.col_obox(c, d, nrm, L, 2 * hw, 0.3)
    return k, dict(kind="stairs", name="Rampa", origin="bottom_center")


# --- catilar (45°) ---
RAFTER_V = 0.22 * math.sqrt(2)   # mertegin dusey kalinligi
UNDER_B = 0.15                   # alt yuz: y = 0.15 - z (dis duvar ust kosesinin uzerinden gecer)
BOARD_T = 0.09
BOARD_W = 0.56
BOARD_OV = 0.11
TAIL = 0.5                       # sacak tasmasi (z)


def roof_slope(k, x0, x1, z_ridge=-G, z_tail=TAIL, clip_hip=None, rafters=(-0.9, 0.9), split=True):
    """Bir 45° egimli yuzey (sacak +Z'de). Kalaslar X boyunca, bindirmeli (bindirme tahtasi gibi)."""
    D = Vector((0, 1, -1)).normalized()     # egim yukari
    N = Vector((0, 1, 1)).normalized()      # yuzey normali
    surf0 = Vector((0, UNDER_B - z_tail + RAFTER_V, z_tail))  # kalas yuzeyi, kuyruk ucu
    s_ridge = (z_tail - z_ridge) * math.sqrt(2)
    parts = []
    s = 0.0
    row = 0
    while s < s_ridge - 0.02:
        w = BOARD_W
        lift = BOARD_T if row > 0 else BOARD_T * 0.6
        # profil (z,y): alt-alt, ust-alt, ust-ust, alt-ust (bindirme: alt kenar bir oncekinin ustunde)
        pA = surf0 + D * s + N * lift
        pB = surf0 + D * (s + w)
        pC = surf0 + D * (s + w) + N * BOARD_T
        pD = surf0 + D * s + N * (lift + BOARD_T)
        prof = [(p.z, p.y) for p in (pA, pB, pC, pD)]
        cuts = [x0, x1]
        if split and (row % 2 == 1 or k.rng.random() < 0.4) and (x1 - x0) > 2.0:
            m = (x0 + x1) / 2 + k.rng.uniform(-0.8, 0.8)
            cuts = [x0, m - 0.006, m + 0.006, x1]
        for a, b in zip(cuts[0::2], cuts[1::2]):
            p = k.prism_x(prof, a, b, cls="roof", grain=X)
            p.clip(Z * -1, -z_ridge)  # z >= z_ridge
            if clip_hip is not None:
                p.clip(*clip_hip)
            parts.append(p)
        s += w - BOARD_OV
        row += 1
    # mertekler
    for rx in rafters:
        lo = lambda z: UNDER_B - z
        prof = [(z_tail, lo(z_tail)), (z_ridge, lo(z_ridge)), (z_ridge, lo(z_ridge) + RAFTER_V), (z_tail, lo(z_tail) + RAFTER_V)]
        p = k.prism_x(prof, rx - 0.09, rx + 0.09, cls="beam", grain=D)
        if clip_hip is not None:
            p.clip(*clip_hip)
    return parts


def p_egimli_cati():
    k = Kit("egimli_cati")
    h = G / 2
    roof_slope(k, -h, h)
    # sacak alin tahtasi
    zt = TAIL
    k.box((-h, UNDER_B - zt - 0.06, zt - 0.02), (h, UNDER_B - zt + RAFTER_V + 0.02, zt + 0.06), "beam", axis=0, bevel=0.02)
    for x in (-0.9, 0.9):
        k.bolt((x, UNDER_B - zt + 0.15, zt + 0.06), (0, 0, 1))
    k.socket("roof_eave", (0, 0, 0), (0, 0, 1), "eave")
    k.socket("roof_top", (0, S, -G), (0, 0, -1), "ridge")
    k.socket("roof_side", (-h, 0, 0), (-1, 0, 0), "side_l")
    k.socket("roof_side", (h, 0, 0), (1, 0, 0), "side_r")
    D = Vector((0, 1, -1)).normalized()
    N = Vector((0, 1, 1)).normalized()
    L = (TAIL + G) * math.sqrt(2)
    mid = Vector((0, UNDER_B - TAIL + RAFTER_V, TAIL)) + D * (L / 2) - N * 0.1
    k.col_obox(mid, D, N, L, G, 0.42)
    return k, dict(kind="roof", name="Eğimli Çatı", origin="eave_center", note="45°; saçak +Z, mahya -Z'de 3 m içeride ve 3 m yukarıda.")


def p_cati_kosesi():
    k = Kit("cati_kosesi")
    h = G / 2
    # On yuzey: x <= 1.5 + z (kalca cizgisinin solu). Duzlem: -x + z >= -1.5  ->  x - z <= 1.5
    hip_front = (Vector((1, 0, -1)), 1.5)
    roof_slope(k, -h, h + TAIL + 0.05, clip_hip=hip_front, rafters=(-0.9,), split=False)
    # yan yuzey: kalca cizgisine gore yansima (x,z) -> (z + 1.5, x - 1.5)
    mirror_from = len(k.parts)
    roof_slope(k, -h, h + TAIL + 0.05, clip_hip=hip_front, rafters=(-0.9,), split=False)
    for p in k.parts[mirror_from:]:
        p.transform(lambda q: Vector((q.z + 1.5, q.y, q.x - 1.5)))
        p.grain = Vector((p.grain.z, p.grain.y, p.grain.x))
    # alin tahtalari
    zt = TAIL
    k.box((-h, UNDER_B - zt - 0.06, zt - 0.02), (h + zt + 0.06, UNDER_B - zt + RAFTER_V + 0.02, zt + 0.06), "beam", axis=0, bevel=0.02)
    k.box((h + zt - 0.02, UNDER_B - zt - 0.06, -h), (h + zt + 0.06, UNDER_B - zt + RAFTER_V + 0.02, zt - 0.02), "beam", axis=2, bevel=0.02)
    # kalca kutugu
    top_off = RAFTER_V + 2 * BOARD_T + 0.08
    p0 = Vector((h + TAIL, UNDER_B - TAIL + top_off, TAIL))
    p1 = Vector((-h, UNDER_B + G + top_off, -G))
    k.log(p0, p1, 0.13, sides=8, cap0=True)
    k.socket("roof_eave", (0, 0, 0), (0, 0, 1), "eave_front")
    k.socket("roof_eave", (h, 0, -h), (1, 0, 0), "eave_side")
    D = Vector((0, 1, -1)).normalized()
    N = Vector((0, 1, 1)).normalized()
    L = (TAIL + G) * math.sqrt(2)
    mid = Vector((0, UNDER_B - TAIL + RAFTER_V, TAIL)) + D * (L / 2) - N * 0.1
    k.col_obox(mid, D, N, L, G, 0.42)
    D2 = Vector((-1, 1, 0)).normalized()
    N2 = Vector((1, 1, 0)).normalized()
    mid2 = Vector((h + TAIL, UNDER_B - TAIL + RAFTER_V, -h)) + D2 * (L / 2) - N2 * 0.1
    k.col_obox(mid2, D2, N2, L, G, 0.42)
    return k, dict(kind="roof", name="Çatı Köşesi", origin="eave_center", note="Kırma çatı dış köşesi: ön saçak +Z, yan saçak +X; kalça çizgisi (1.5,0)->(-1.5,-3).")


def p_cati_tepesi():
    k = Kit("cati_tepesi")
    h = G / 2
    # orijin = mahya noktasi (egimli_cati'nin roof_top yuvasi). Kalas ust yuzu mahyada yaklasik y=0.6
    top = UNDER_B + RAFTER_V + 2 * BOARD_T + 0.012
    N1 = Vector((0, 1, 1)).normalized()
    N2 = Vector((0, 1, -1)).normalized()
    for nrm, sgn in ((N1, 1), (N2, -1)):
        D = Vector((0, -1, sgn)).normalized()  # mahyadan asagi
        apex = Vector((0, top, 0))
        w = 0.46
        c = apex + D * (w / 2) + nrm * 0.05
        k.obox(c, X, nrm, G - 0.01, w, 0.1, cls="roof", bevel=0.025)
    k.log((-h, top + 0.12, 0), (h, top + 0.12, 0), 0.16, sides=10, cap0=True)
    for x in (-0.75, 0.75):
        k.strap((x - 0.05, top - 0.03, -0.17), (x + 0.05, top + 0.29, 0.17))
    for x in (-1.25, 0.0, 1.25):
        k.bolt((x, top + 0.12 + 0.16, 0), (0, 1, 0), r=0.045)
    k.socket("ridge", (0, 0, 0), (0, 0, 1), "ridge")
    k.socket("ridge_end", (-h, 0, 0), (-1, 0, 0), "end_l")
    k.socket("ridge_end", (h, 0, 0), (1, 0, 0), "end_r")
    k.col_box((-h, top - 0.2, -0.3), (h, top + 0.3, 0.3))
    return k, dict(kind="roof", name="Çatı Tepesi", origin="ridge_center")


def p_duz_cati():
    k = Kit("duz_cati")
    h = G / 2
    deck_planks(k, h, h, 0.0, 0.14, 7, cls="roof")
    k.box((-h, -0.36, h - 0.2), (h, -0.14, h), "beam", axis=0)
    k.box((-h, -0.36, -h), (h, -0.14, -h + 0.2), "beam", axis=0)
    for x in (-1.0, 0.0, 1.0):
        k.box((x - 0.09, -0.34, -h + 0.2), (x + 0.09, -0.14, h - 0.2), "beam", axis=2, bevel=0)
        k.box((x - 0.07, 0.0, -h + 0.004), (x + 0.07, 0.09, h - 0.004), "beam", axis=2, bevel=0.025)  # citalar
        for z in (-1.0, 0.0, 1.0):
            k.bolt((x, 0.09, z), (0, 1, 0), r=0.04, h=0.025)
    k.square_sockets()
    k.col_box((-h, -0.36, -h), (h, 0.09, h))
    return k, dict(kind="roof", name="Düz Çatı", origin="top_center")


# --- sutun, kiris ---
def p_sutun():
    k = Kit("sutun")
    k.box((-0.23, -0.02, -0.23), (0.23, 0.18, 0.23), "beam", axis=1)
    p = k.box((-0.18, 0.18, -0.18), (0.18, 2.75, 0.18), "beam", axis=1, bevel=0.06)
    k.box((-0.23, 2.75, -0.23), (0.23, TOP, 0.23), "beam", axis=1)
    for y in (0.32, 2.55):
        k.strap((-0.19, y, -0.19), (0.19, y + 0.08, 0.19))
        k.bolt((0, y + 0.04, 0.19), (0, 0, 1), r=0.035, h=0.02)
        k.bolt((0, y + 0.04, -0.19), (0, 0, -1), r=0.035, h=0.02)
    k.socket("pillar_bottom", (0, 0, 0), (0, 0, 1), "bottom")
    k.socket("pillar_top", (0, S, 0), (0, 0, 1), "top")
    k.col_box((-0.23, 0, -0.23), (0.23, S, 0.23))
    return k, dict(kind="pillar", name="Sütun", origin="bottom_center")


def p_kiris():
    k = Kit("kiris")
    h = G / 2
    k.box((-h, 2.62, -0.17), (h, TOP, 0.17), "beam", axis=0)
    for s in (-1, 1):
        k.beam_between((s * 1.32, 1.95, 0), (s * 0.75, 2.62, 0), 0.2, 0.18, up=Z)
        k.bolt((s * 1.2, 2.8, 0.17), (0, 0, 1))
        k.bolt((s * 1.2, 2.8, -0.17), (0, 0, -1))
        k.bolt((s * 0.9, 2.5, 0.09), (0, 0, 1), r=0.04)
    k.socket("wall_bottom", (0, 0, 0), (0, 0, 1), "bottom")
    k.socket("wall_top", (0, S, 0), (0, 0, 1), "top")
    k.socket("beam_end", (-h, S, 0), (-1, 0, 0), "end_l")
    k.socket("beam_end", (h, S, 0), (1, 0, 0), "end_r")
    k.col_box((-h, 2.62, -0.17), (h, S, 0.17))
    return k, dict(kind="beam", name="Kiriş", origin="bottom_center", note="Kat üstünde (2.62–2.99 m) yatay kiriş + köşebentler; sütun tepelerine ya da kenar yuvasına oturur.")


# --- korkuluk, cit, kapilar ---
def p_parmaklik():
    k = Kit("parmaklik")
    h = G / 2
    for s in (-1, 1):
        k.box((min(s * h, s * (h - 0.22)), -0.02, -0.11), (max(s * h, s * (h - 0.22)), 1.12, 0.11), "beam", axis=1)
    k.box((-h + 0.2, 0.94, -0.1), (h - 0.2, 1.1, 0.1), "beam", axis=0)
    k.box((-h + 0.2, 0.1, -0.08), (h - 0.2, 0.26, 0.08), "beam", axis=0)
    n = 6
    for i in range(n):
        x = -1.0 + 2.0 * i / (n - 1)
        k.box((x - 0.08, 0.26, -0.06), (x + 0.08, 0.94, 0.06), "plank", axis=1, bevel=0.025)
    for s in (-1, 1):
        k.bolt((s * (h - 0.11), 1.02, 0.11), (0, 0, 1), r=0.04)
    k.wall_sockets(1.1, stack=False)
    k.col_box((-h, 0, -0.11), (h, 1.12, 0.11))
    return k, dict(kind="fence", name="Parmaklık", origin="bottom_center")


def picket(k, x, w, y0, h, z0, z1, tint=None, tilt=0.0):
    hw = w / 2
    prof = [(-hw, y0), (hw, y0), (hw, h - 0.09), (0.035, h), (-0.035, h), (-hw, h - 0.09)]
    ca, sa = math.cos(tilt), math.sin(tilt)
    prof = [(x + px * ca - (py - y0) * sa, y0 + px * sa + (py - y0) * ca) for px, py in prof]
    return k.prism_z(prof, z0, z1, cls="plank", grain=Y, bevel=0.02, tint=tint)


def fence_post(k, x, hw, y0, y1):
    p = k.box((x - hw, y0, -hw), (x + hw, y1, hw), "beam", axis=1, cap0=False)
    p.clip(Vector((1, 1, 0)), x + hw + y1 - 0.07)
    p.clip(Vector((-1, 1, 0)), -x + hw + y1 - 0.07)
    p.clip(Vector((0, 1, 1)), hw + y1 - 0.07)
    p.clip(Vector((0, 1, -1)), hw + y1 - 0.07)
    return p


def p_cit():
    k = Kit("cit")
    h = G / 2
    for s in (-1, 1):
        fence_post(k, s * (h - 0.11), 0.11, -0.4, 1.38)
    for y in (0.3, 0.92):
        k.box((-h + 0.2, y, -0.15), (h - 0.2, y + 0.16, -0.05), "beam", axis=0, bevel=0.025)
    n = 9
    span = 2 * (h - 0.25)
    for i in range(n):
        x = -h + 0.25 + span * (i + 0.5) / n
        picket(k, x, 0.2, -0.12, 1.18 + k.rng.uniform(-0.05, 0.08), -0.05, 0.05, tilt=math.radians(k.rng.uniform(-1.5, 1.5)))
    for s in (-1, 1):
        for y in (0.38, 1.0):
            k.bolt((s * (h - 0.6), y, 0.05), (0, 0, 1), r=0.03, h=0.02)
    k.wall_sockets(1.3, stack=False)
    k.col_box((-h, 0, -0.15), (h, 1.3, 0.11))
    return k, dict(kind="fence", name="Çit", origin="bottom_center", ground=dict(mode="bottom", spread=0.8))


def p_bahce_kapisi():
    k = Kit("bahce_kapisi")
    h = G / 2
    gw = 0.8  # kanat yari genisligi
    for s in (-1, 1):
        fence_post(k, s * (h - 0.11), 0.11, -0.4, 1.38)
        fence_post(k, s * (gw + 0.13), 0.13, -0.4, 1.6)
        for y in (0.3, 0.92):
            k.box((min(s * (h - 0.2), s * (gw + 0.24)), y, -0.15), (max(s * (h - 0.2), s * (gw + 0.24)), y + 0.16, -0.05), "beam", axis=0, bevel=0.02)
        picket(k, s * (h - 0.36), 0.2, -0.12, 1.2 + k.rng.uniform(-0.04, 0.04), -0.05, 0.05)
    # kanat (mentese x = -gw)
    k.cur_node = "Kanat"
    k.nodes["Kanat"] = [-gw, 0.0, 0.0]
    x0, x1 = -gw + 0.02, gw - 0.02
    for xx in (x0, x1 - 0.14):
        k.box((xx, 0.08, -0.13), (xx + 0.14, 1.25, -0.03), "beam", axis=1, bevel=0.025)
    for y in (0.2, 1.0):
        k.box((x0 + 0.14, y, -0.13), (x1 - 0.14, y + 0.14, -0.03), "beam", axis=0, bevel=0.025)
    k.beam_between((x0 + 0.2, 0.32, -0.08), (x1 - 0.2, 0.98, -0.08), 0.1, 0.13, up=Z)
    n = 5
    for i in range(n):
        x = x0 + (x1 - x0) * (i + 0.5) / n
        picket(k, x, 0.2, 0.08, 1.38 - 0.1 * abs(i - 2), -0.03, 0.07)
    for y in (0.24, 1.04):
        k.strap((x0 - 0.02, y, 0.07), (x0 + 0.5, y + 0.07, 0.095), rust=True)
    k.strap((x1 - 0.12, 0.62, 0.07), (x1 + 0.0, 0.72, 0.1))
    k.cur_node = None
    k.wall_sockets(1.3, stack=False)
    for s in (-1, 1):
        k.col_box((min(s * h, s * gw), 0, -0.15), (max(s * h, s * gw), 1.4, 0.13))
    k.col_box((-gw, 0, -0.13), (gw, 1.4, 0.07), node="Kanat")
    return k, dict(kind="fence", name="Bahçe Kapısı", origin="bottom_center", ground=dict(mode="bottom", spread=0.8),
                   note="'Kanat' düğümü menteşe ekseninde (x=-0.8); Y ekseninde döndürülerek açılır.")


def p_sur_duvari():
    k = Kit("sur_duvari")
    h = G / 2
    n = 8
    r = G / n / 2
    for i in range(n):
        x = -h + r + 2 * r * i
        top = 3.45 + k.rng.uniform(-0.15, 0.2)
        k.log((x + k.rng.uniform(-0.01, 0.01), -0.4, k.rng.uniform(-0.02, 0.02)), (x, top, 0), r * 0.98, sides=8, tip=0.22, tip_r=0.45)
    for y in (0.9, 2.5):
        k.box((-h + 0.02, y, -r - 0.2), (h - 0.02, y + 0.26, -r + 0.04), "beam", axis=0)
        for x in (-1.1, 0.0, 1.1):
            k.bolt((x, y + 0.13, -r - 0.2), (0, 0, -1))
    k.wall_sockets(S, stack=False)
    k.col_box((-h, 0, -r - 0.2), (h, 3.5, r))
    return k, dict(kind="fence", name="Sur Duvarı", origin="bottom_center", ground=dict(mode="bottom", spread=0.8))


def p_sur_kapisi():
    k = Kit("sur_kapisi")
    h = G / 2
    pr = 0.26
    for s in (-1, 1):
        k.log((s * (h - pr), -0.4, 0), (s * (h - pr), 4.25, 0), pr, sides=10, tip=0.25, tip_r=0.5)
    k.box((-h, 3.55, -0.2), (h, 3.92, 0.2), "beam", axis=0)
    for s in (-1, 1):
        k.bolt((s * (h - pr), 3.73, 0.2), (0, 0, 1), r=0.06)
        k.bolt((s * (h - pr), 3.73, -0.2), (0, 0, -1), r=0.06)
    lw = h - 2 * pr  # kanat genisligi (0.98)
    for side, name in ((-1, "KanatSol"), (1, "KanatSag")):
        k.cur_node = name
        hinge = side * (h - 2 * pr)
        k.nodes[name] = [hinge, 0.0, 0.0]
        a, b = sorted((hinge, hinge - side * lw))
        a += 0.01
        b -= 0.01
        n = 4
        w = (b - a) / n
        for i in range(n):
            top = 3.3 + k.rng.uniform(-0.06, 0.06)
            k.box((a + i * w + 0.004, 0.04, -0.07), (a + (i + 1) * w - 0.004, top, 0.07), "plank", axis=1)
        for y in (0.45, 1.6, 2.8):
            k.box((a + 0.05, y, -0.18), (b - 0.05, y + 0.22, -0.07), "beam", axis=0, bevel=0.03)
        k.beam_between(((a + 0.15), 0.67, -0.125), ((b - 0.15), 1.6, -0.125), 0.11, 0.18, up=Z)
        for y in (0.6, 2.7):
            k.strap((a + 0.02, y, 0.07), (b - 0.02, y + 0.12, 0.095), rust=k.rng.random() < 0.5)
            for x in (a + 0.15, (a + b) / 2, b - 0.15):
                k.bolt((x, y + 0.06, 0.095), (0, 0, 1), r=0.035, h=0.02)
        k.cur_node = None
    k.wall_sockets(S, stack=False)
    for s in (-1, 1):
        k.col_box((min(s * h, s * (h - 2 * pr)), 0, -0.26), (max(s * h, s * (h - 2 * pr)), 4.2, 0.26))
    k.col_box((-h, 3.55, -0.2), (h, 3.92, 0.2))
    k.col_box((-lw, 0, -0.18), (0, 3.36, 0.1), node="KanatSol")
    k.col_box((0, 0, -0.18), (lw, 3.36, 0.1), node="KanatSag")
    return k, dict(kind="fence", name="Sur Kapısı", origin="bottom_center", ground=dict(mode="bottom", spread=0.8),
                   note="'KanatSol' (x=-0.98) ve 'KanatSag' (x=+0.98) düğümleri menteşe ekseninde.")


def p_kule_tabani():
    k = Kit("kule_tabani")
    h = G / 2
    deck_planks(k, h, h, S, 0.18, 7)
    rim = 0.24
    k.box((-h, S - 0.42, h - rim), (h, S - 0.18, h), "beam", axis=0)
    k.box((-h, S - 0.42, -h), (h, S - 0.18, -h + rim), "beam", axis=0)
    k.box((h - rim, S - 0.42, -h + rim), (h, S - 0.18, h - rim), "beam", axis=2)
    k.box((-h, S - 0.42, -h + rim), (-h + rim, S - 0.18, h - rim), "beam", axis=2)
    bot, topc = 1.3, 1.12
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.beam_between((sx * bot, -0.5, sz * bot), (sx * topc, S - 0.42, sz * topc), 0.36, 0.36, up=Z, cap0=False)
    # yanlarda capraz destekler ve alt bag kirisi
    for side in range(4):
        ang = side * math.pi / 2
        ca, sa = math.cos(ang), math.sin(ang)
        rot = lambda x, z: (x * ca - z * sa, x * sa + z * ca)
        out = Vector(rot(0, 1)).to_3d()
        out = Vector((out.x, 0, out.y))
        def P(t, y):
            w = bot + (topc - bot) * (y + 0.5) / (S - 0.42 + 0.5)
            x, z = rot(t * (w - 0.1), w + 0.06)
            return Vector((x, y, z))
        k.beam_between(P(-1, 0.35), P(1, S - 0.75), 0.18, 0.14, up=out)
        k.beam_between(P(1, 0.35), P(-1, S - 0.75), 0.18, 0.14, up=out)
        k.beam_between(P(-1, 0.15), P(1, 0.15), 0.2, 0.16, up=Y, bevel=0.03)
        c = P(0, (0.35 + S - 0.75) / 2) + out * 0.09
        k.bolt(c, out, r=0.06)
    k.socket("cell_bottom", (0, 0, 0), (0, 0, 1), "bottom")
    k.square_sockets(y=S)
    k.col_box((-h, S - 0.42, -h), (h, S, h))
    for sx in (-1, 1):
        for sz in (-1, 1):
            k.col_box((sx * 1.21 - 0.2, 0, sz * 1.21 - 0.2), (sx * 1.21 + 0.2, S - 0.42, sz * 1.21 + 0.2))
    return k, dict(kind="platform", name="Kule Tabanı", origin="bottom_center", ground=dict(mode="bottom", spread=0.7),
                   note="Üst güverte y=3: kenar/köşe/hücre yuvaları orada (parmaklık, duvar, ikinci kat).")


def p_makine(size_cells, pid, name):
    k = Kit(pid)
    half = size_cells * G / 2
    n_rows = {1: 8, 2: 12, 3: 14}[size_cells]
    bev = BEV if size_cells == 1 else 0
    if size_cells == 1:
        deck_planks(k, half, half, 0.0, 0.16, n_rows)
    else:
        # ust kenari pahli kalaslar (alt yuz yok): ucgen butcesi icin
        w = 2 * half / n_rows
        for i in range(n_rows):
            z0 = -half + i * w + 0.008
            z1 = z0 + w - 0.016
            off = (k.rng.uniform(-0.6, 0.6) if i % 2 else 0)
            cuts = [-half] + [-half + G * c + off for c in range(1, size_cells)] + [half]
            for a, b in zip(cuts[:-1], cuts[1:]):
                top = -k.rng.uniform(0, 0.006)
                c = 0.03
                prof = [(z0, top - 0.16), (z1, top - 0.16), (z1, top - c), (z1 - c, top), (z0 + c, top), (z0, top - c)]
                k.prism_x(prof, a + 0.006, b - 0.006, cls="plank", grain=X, bevel=0, cap0=True, cap1=True)
    rim = 0.3
    k.box((-half, -0.5, half - rim), (half, -0.16, half), "beam", axis=0)
    k.box((-half, -0.5, -half), (half, -0.16, -half + rim), "beam", axis=0)
    k.box((half - rim, -0.5, -half + rim), (half, -0.16, half - rim), "beam", axis=2)
    k.box((-half, -0.5, -half + rim), (-half + rim, -0.16, half - rim), "beam", axis=2)
    for c in range(1, size_cells):
        t = -half + c * G
        k.box((t - 0.12, -0.45, -half + rim), (t + 0.12, -0.16, half - rim), "beam", axis=2, bevel=0)
    pts = [-half + 0.25, half - 0.25] if size_cells < 3 else [-half + 0.25, 0.0, half - 0.25]
    for x in pts:
        for z in pts:
            k.box((x - 0.17, -1.2, z - 0.17), (x + 0.17, -0.5, z + 0.17), "beam", axis=1, bevel=0, cap0=False)
    # kose demir plakalari + civatalar
    for sx in (-1, 1):
        for sz in (-1, 1):
            cx, cz = sx * (half - 0.25), sz * (half - 0.25)
            k.strap((cx - 0.22, -0.01, cz - 0.22), (cx + 0.22, 0.02, cz + 0.22), rust=k.rng.random() < 0.5)
            for dx in (-0.13, 0.13):
                k.bolt((cx + dx, 0.02, cz + dx * sx * sz), (0, 1, 0), r=0.035, h=0.02)
    for s in (-1, 1):
        k.bolt((0.4 * s, -0.33, half), (0, 0, 1))
        k.bolt((0.4 * s, -0.33, -half), (0, 0, -1))
    k.square_sockets(half=half)
    k.col_box((-half, -1.2, -half), (half, 0.02, half))
    return k, dict(kind="platform", name=name, origin="top_center", ground=dict(mode="top", clearance=0.3, depth=1.15),
                   cells=size_cells)


PIECES = [
    ("kare_temel", p_kare_temel),
    ("ucgen_temel", p_ucgen_temel),
    ("kazik_temel", p_kazik_temel),
    ("duvar", p_duvar),
    ("yarim_duvar", p_yarim_duvar),
    ("alcak_duvar", p_alcak_duvar),
    ("kapi_kasasi", p_kapi_kasasi),
    ("pencereli_duvar", p_pencereli_duvar),
    ("ucgen_duvar", p_ucgen_duvar),
    ("kare_zemin", p_kare_zemin),
    ("ucgen_zemin", p_ucgen_zemin),
    ("merdiven", p_merdiven),
    ("rampa", p_rampa),
    ("egimli_cati", p_egimli_cati),
    ("cati_kosesi", p_cati_kosesi),
    ("cati_tepesi", p_cati_tepesi),
    ("duz_cati", p_duz_cati),
    ("sutun", p_sutun),
    ("kiris", p_kiris),
    ("parmaklik", p_parmaklik),
    ("cit", p_cit),
    ("bahce_kapisi", p_bahce_kapisi),
    ("sur_duvari", p_sur_duvari),
    ("sur_kapisi", p_sur_kapisi),
    ("kule_tabani", p_kule_tabani),
    ("makine_platformu_kucuk", lambda: p_makine(1, "makine_platformu_kucuk", "Makine Platformu (küçük)")),
    ("makine_platformu_orta", lambda: p_makine(2, "makine_platformu_orta", "Makine Platformu (orta)")),
    ("makine_platformu_buyuk", lambda: p_makine(3, "makine_platformu_buyuk", "Makine Platformu (büyük)")),
]

RULES = [
    {"attach": "edge", "target": "edge", "align": "opposite", "kinds": ["foundation", "floor", "platform", "roof"]},
    {"attach": "edge", "target": "wall_top", "align": "either", "kinds": ["floor", "roof"]},
    {"attach": "corner", "target": "pillar_top", "align": "any4", "kinds": ["floor", "roof"]},
    {"attach": "wall_bottom", "target": "edge", "align": "either"},
    {"attach": "wall_bottom", "target": "wall_top", "align": "either"},
    {"attach": "wall_end", "target": "wall_end", "align": "opposite", "kinds": ["fence"]},
    # Cit kosesi (GDD 9.1 agil kapali halkadir): uc uca dik ekleme; ayni yone dusen aday parcanin kendi yuvasidir ("dolu").
    {"attach": "wall_end", "target": "wall_end", "align": "any4", "kinds": ["fence"]},
    {"attach": "pillar_bottom", "target": "corner", "align": "any4"},
    {"attach": "pillar_bottom", "target": "pillar_top", "align": "any4"},
    {"attach": "beam_end", "target": "pillar_top", "align": "any4"},
    {"attach": "cell_bottom", "target": "cell", "align": "any4"},
    {"attach": "stair_top", "target": "edge", "align": "opposite"},
    {"attach": "stair_bottom", "target": "stair_top", "align": "opposite"},
    {"attach": "roof_eave", "target": "wall_top", "align": "either"},
    {"attach": "roof_side", "target": "roof_side", "align": "opposite"},
    {"attach": "ridge", "target": "roof_top", "align": "either"},
    {"attach": "ridge_end", "target": "ridge_end", "align": "opposite"},
]


# ---------------------------------------------------------------------------
# UV + kose rengi + Blender nesnesi
# ---------------------------------------------------------------------------
def uv_layout(part, rng):
    lay = {}
    if part.cls == "metal":
        lay["tile"] = 4 if getattr(part, "rust", False) else 3
        return lay
    G_ = part.grain
    us = [p.dot(G_) for pts, _ in part.polys for p in pts]
    umin, umax = min(us), max(us)
    length = umax - umin
    sc = min(1.0, 3.9 / max(length, 1e-3))
    lay.update(umin=umin, us=sc, uoff=rng.uniform(0.03, max(0.03, 3.97 - length * sc)),
               strip=rng.choice(STRIPS[part.cls]), end=END_TILE[part.cls])
    return lay


def face_uvs(part, lay, pts, n, rng):
    nn = n.normalized() if n.length > 0 else Vector((0, 1, 0))
    if part.cls == "metal":
        a = nn.orthogonal().normalized()
        b = nn.cross(a)
        cs = [(p.dot(a), p.dot(b)) for p in pts]
        ca = sum(c[0] for c in cs) / len(cs)
        cb = sum(c[1] for c in cs) / len(cs)
        ext = max(max(abs(c[0] - ca), abs(c[1] - cb)) for c in cs) * 2 + 1e-4
        s = min(1.0 / STRIP_M, 0.8 / ext)
        ou, ov = rng.uniform(0.2, 0.8), rng.uniform(0.2, 0.8)
        out = []
        for c in cs:
            tu = min(max(ou + (c[0] - ca) * s, 0.03), 0.97)
            tv = min(max(ov + (c[1] - cb) * s, 0.03), 0.97)
            out.append(((lay["tile"] + tu) / 8, (7 + tv) / 8))
        return out
    G_ = part.grain
    if abs(nn.dot(G_)) > 0.8:
        a = G_.orthogonal().normalized()
        b = nn.cross(a).normalized()
        cs = [(p.dot(a), p.dot(b)) for p in pts]
        ca = sum(c[0] for c in cs) / len(cs)
        cb = sum(c[1] for c in cs) / len(cs)
        ext = max(max(abs(c[0] - ca), abs(c[1] - cb)) for c in cs) * 2 + 1e-4
        s = min(1.0 / STRIP_M, 0.84 / ext)
        ou, ov = 0.5 + rng.uniform(-0.04, 0.04), 0.5 + rng.uniform(-0.04, 0.04)
        return [((lay["end"] + ou + (c[0] - ca) * s) / 8, (7 + ov + (c[1] - cb) * s) / 8) for c in cs]
    if part.mapping == "cyl":
        ax = G_
        o = part.axis_pt
        e1 = ax.orthogonal().normalized()
        e2 = ax.cross(e1)
        fs = []
        for p in pts:
            d = p - o
            fs.append(math.atan2(d.dot(e2), d.dot(e1)) / math.tau)
        if max(fs) - min(fs) > 0.5:
            fs = [f + 1 if f < 0 else f for f in fs]
        base = math.floor(min(fs))
        fs = [f - base for f in fs]
        out = []
        for p, f in zip(pts, fs):
            U = (lay["uoff"] + (p.dot(G_) - lay["umin"]) * lay["us"]) / ATLAS_U_M
            out.append((U, (lay["strip"] + (0.03 + f * 0.40) / STRIP_M) / 8))
        return out
    B = nn.cross(G_)
    if B.length < 1e-6:
        B = G_.orthogonal()
    B.normalize()
    vs = [p.dot(B) for p in pts]
    vmin, vmax = min(vs), max(vs)
    ext = vmax - vmin
    vs_ = min(1.0, 0.44 / max(ext, 1e-4))
    voff = rng.uniform(0.02, max(0.02, 0.48 - ext * vs_))
    out = []
    for p, v in zip(pts, vs):
        U = (lay["uoff"] + (p.dot(G_) - lay["umin"]) * lay["us"]) / ATLAS_U_M
        Vv = (lay["strip"] + (voff + (v - vmin) * vs_) / STRIP_M) / 8
        out.append((U, Vv))
    return out


def corner_color(part, tag, n, p, ao):
    t = part.tint or (1, 1, 1)
    f = {"bevel": 1.07, "cap": 0.9, "cut": 0.95}.get(tag, 1.0)
    if part.cls == "metal":
        f = 1.0
    y0, y1, lo = ao
    a = lo + (1 - lo) * min(1.0, max(0.0, (p.y - y0) / max(y1 - y0, 1e-3)))
    down = 0.8 if n.normalized().y < -0.6 else 1.0
    k = f * a * down
    return (min(1.0, t[0] * k), min(1.0, t[1] * k), min(1.0, t[2] * k), 1.0)


def build_mesh(name, parts, ao, rng):
    verts, faces, uvs, cols, smooth = [], [], [], [], []
    for part in parts:
        part.fix_orientation()
        lay = uv_layout(part, rng)
        local = {}
        for pts, tag in part.polys:
            n = newell(pts)
            if n.length < 1e-10:
                continue
            fuv = face_uvs(part, lay, pts, n, rng)
            idx, fu, fc = [], [], []
            for p, uv in zip(pts, fuv):
                key = (round(p.x, 5), round(p.y, 5), round(p.z, 5))
                if key not in local:
                    local[key] = len(verts)
                    verts.append(p)
                vi = local[key]
                if idx and idx[-1] == vi:
                    continue
                idx.append(vi)
                fu.append(uv)
                fc.append(corner_color(part, tag, n, p, ao))
            if len(idx) > 1 and idx[0] == idx[-1]:
                idx.pop(); fu.pop(); fc.pop()
            if len(idx) < 3 or len(set(idx)) != len(idx):
                continue
            faces.append(idx)
            uvs.append(fu)
            cols.append(fc)
            smooth.append(part.smooth)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(v.x, -v.z, v.y) for v in verts], [], faces)
    uvl = mesh.uv_layers.new(name="UVMap")
    ca = mesh.color_attributes.new("Color", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = ca
    try:
        mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    except Exception:
        pass
    for k, poly in enumerate(mesh.polygons):
        for j, li in enumerate(poly.loop_indices):
            uvl.data[li].uv = uvs[k][j]
            ca.data[li].color = cols[k][j]
    mesh.polygons.foreach_set("use_smooth", smooth)
    mesh.update()
    return mesh


def make_material(albedo, normal, rm):
    mat = bpy.data.materials.new("kabarma_wood")
    mat.use_nodes = True
    mat.use_backface_culling = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    t_alb = nt.nodes.new("ShaderNodeTexImage")
    t_alb.image = albedo
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Color"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    nt.links.new(t_alb.outputs["Color"], mix.inputs[6])
    nt.links.new(vc.outputs["Color"], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    t_rm = nt.nodes.new("ShaderNodeTexImage")
    t_rm.image = rm
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(t_rm.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
    nt.links.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])
    t_n = nt.nodes.new("ShaderNodeTexImage")
    t_n.image = normal
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 0.6
    nt.links.new(t_n.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def piece_ao(meta, bounds):
    o = meta.get("origin")
    if o == "top_center":
        return (-0.7, -0.1, 0.62)
    if meta["kind"] == "roof":
        return (bounds[0][1], bounds[0][1] + 0.5, 0.85)
    return (0.0, 0.7, 0.72)


def build_piece(pid, fn, mat):
    k, meta = fn()
    allp = [p for part in k.parts for pts, _ in part.polys for p in pts]
    mn = Vector((min(p.x for p in allp), min(p.y for p in allp), min(p.z for p in allp)))
    mx = Vector((max(p.x for p in allp), max(p.y for p in allp), max(p.z for p in allp)))
    ao = piece_ao(meta, (mn, mx))
    rng = random.Random(zlib.crc32(pid.encode()) ^ 0x5EED)
    groups = {}
    for part in k.parts:
        groups.setdefault(part.node, []).append(part)
    objs = []
    root_mesh = build_mesh(pid, groups.get(None, []), ao, rng)
    root = bpy.data.objects.new(pid, root_mesh)
    root.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(root)
    objs.append(root)
    for node, parts in groups.items():
        if node is None:
            continue
        piv = Vector(k.nodes[node])
        for part in parts:
            part.transform(lambda q, piv=piv: q - piv)
            if part.axis_pt is not None:
                part.axis_pt = part.axis_pt - piv
        m = build_mesh(f"{pid}_{node}", parts, (ao[0] - piv.y, ao[1] - piv.y, ao[2]), rng)
        ob = bpy.data.objects.new(node, m)
        ob.data.materials.append(mat)
        ob.parent = root
        ob.location = (piv.x, -piv.z, piv.y)
        bpy.context.scene.collection.objects.link(ob)
        objs.append(ob)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
    meta = dict(meta)
    meta.update(
        id=pid,
        glb=f"{pid}.glb",
        tris=tris,
        bounds={"min": [round(v, 4) for v in mn], "max": [round(v, 4) for v in mx]},
        center=[round(v, 4) for v in (mn + mx) / 2],
        sockets=k.sockets,
        colliders=k.colliders,
    )
    if k.nodes:
        meta["nodes"] = {n: {"pivot": p} for n, p in k.nodes.items()}
    size = meta.get("cells", 1) * G
    if meta["kind"] in ("foundation", "floor", "platform", "stairs"):
        meta["size"] = [round(mx.x - mn.x, 3), round(mx.y - mn.y, 3), round(mx.z - mn.z, 3)]
        meta["footprint"] = [size, size] if not pid.startswith("ucgen") else [G, round(TRI_IN + TRI_OUT, 4)]
    else:
        meta["size"] = [round(mx.x - mn.x, 3), round(mx.y - mn.y, 3), round(mx.z - mn.z, 3)]
    return objs, meta


def export_glb(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_materials="EXPORT",
        export_vertex_color="ACTIVE",
        export_image_format="WEBP",
        export_image_quality=90,
        export_cameras=False,
        export_lights=False,
        export_extras=False,
        export_animations=False,
    )


RES_DIR = "res://assets/models/build/wood"

WOOD_KIT_TRES = """[gd_resource type="StandardMaterial3D" load_steps=4 format=3]

[ext_resource type="Texture2D" path="{d}/wood_atlas_albedo.png" id="1_alb"]
[ext_resource type="Texture2D" path="{d}/wood_atlas_rm.png" id="2_rm"]
[ext_resource type="Texture2D" path="{d}/wood_atlas_normal.png" id="3_nrm"]

[resource]
resource_name = "kabarma_wood"
vertex_color_use_as_albedo = true
albedo_texture = ExtResource("1_alb")
metallic = 1.0
metallic_texture = ExtResource("2_rm")
metallic_texture_channel = 2
roughness_texture = ExtResource("2_rm")
roughness_texture_channel = 1
normal_enabled = true
normal_scale = 0.6
normal_texture = ExtResource("3_nrm")
texture_filter = 5
"""


def write_godot_imports(pids):
    """Godot ice aktarim ayarlari: 28 GLB tek ortak malzemeyi (wood_kit.tres) kullanir, gomulu dokular ayiklanmaz."""
    with open(os.path.join(OUT_DIR, "wood_kit.tres"), "w", encoding="utf-8", newline="\n") as f:
        f.write(WOOD_KIT_TRES.format(d=RES_DIR))
    for name, normal in (("wood_atlas_albedo", False), ("wood_atlas_rm", False), ("wood_atlas_normal", True)):
        p = os.path.join(OUT_DIR, name + ".png.import")
        if os.path.exists(p):
            continue
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write('[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n\n'
                    f'compress/mode=2\ncompress/normal_map={1 if normal else 2}\nmipmaps/generate=true\n'
                    'detect_3d/compress_to=0\n')
    for pid in pids:
        p = os.path.join(OUT_DIR, pid + ".glb.import")
        if os.path.exists(p):
            with open(p, encoding="utf-8") as f:
                if "wood_kit.tres" in f.read():
                    continue
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write('[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\n\n[params]\n\n'
                    'meshes/ensure_tangents=true\nmeshes/generate_lods=true\nmeshes/create_shadow_meshes=true\n'
                    'gltf/embedded_image_handling=0\n'
                    '_subresources={\n"materials": {\n"kabarma_wood": {\n"use_external/enabled": true,\n'
                    f'"use_external/path": "{RES_DIR}/wood_kit.tres",\n"use_external/fallback_path": "{RES_DIR}/wood_kit.tres"\n'
                    '}\n}\n}\n')


# ---------------------------------------------------------------------------
# Temas foyu
# ---------------------------------------------------------------------------
def setup_render(scene, w, h):
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
        try:
            scene.render.engine = eng
            break
        except Exception:
            continue
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in scene.view_settings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.62, 0.72, 0.80, 1)
    bg.inputs["Strength"].default_value = 0.9
    scene.world = world
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 3.2
    sun.angle = math.radians(6)
    so = bpy.data.objects.new("sun", sun)
    so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    scene.collection.objects.link(so)
    try:
        scene.eevee.taa_render_samples = 32
    except Exception:
        pass


def mannequin(scene):
    mat = bpy.data.materials.new("mank")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.42, 0.70, 1)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=1.1, location=(0, 0, 0.55), vertices=16)
    body = bpy.context.active_object
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.3, location=(0, 0, 1.3), segments=16, ring_count=8)
    head = bpy.context.active_object
    for o in (body, head):
        o.data.materials.append(mat)
    return [body, head]


def render_sheet(entries, cols=7, tw=480, th=400):
    scene = bpy.context.scene
    setup_render(scene, tw, th)
    man = mannequin(scene)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    txt_mat = bpy.data.materials.new("txt")
    txt_mat.use_nodes = True
    txt_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.03, 0.02, 1)
    ground = bpy.data.meshes.new("ground")
    ground.from_pydata([(-30, -30, 0), (30, -30, 0), (30, 30, 0), (-30, 30, 0)], [], [(0, 1, 2, 3)])
    gmat = bpy.data.materials.new("gm")
    gmat.use_nodes = True
    gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.47, 0.25, 1)
    ground.materials.append(gmat)
    gobj = bpy.data.objects.new("ground", ground)
    scene.collection.objects.link(gobj)
    os.makedirs(TMP_DIR, exist_ok=True)
    rows = (len(entries) + cols - 1) // cols
    sheet = np.zeros((rows * th, cols * tw, 4), np.float32)
    all_objs = [o for e in entries for o in e[0]]
    for o in all_objs:
        o.hide_render = True
    az, el = math.radians(35), math.radians(28)
    # kamera on-sag-ustte (Godot +Z on = Blender -Y); fwd = kameradan hedefe
    fwd = Vector((-math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), -math.sin(el)))
    for i, (objs, meta) in enumerate(entries):
        for o in objs:
            o.hide_render = False
        mn = Vector(meta["bounds"]["min"])
        mx = Vector(meta["bounds"]["max"])
        # Godot -> Blender
        bmn = Vector((mn.x, -mx.z, mn.y))
        bmx = Vector((mx.x, -mn.z, mx.y))
        gz = 0.0 if meta.get("origin") != "top_center" else 0.0
        gz = min(0.0, bmn.z) if meta["kind"] not in ("foundation", "platform") else 0.0
        if meta.get("origin") == "top_center":
            gz = -0.35
        gobj.location = (0, 0, gz)
        for o in man:
            o.location.x = bmx.x + 0.7
            o.location.y = (bmn.y + bmx.y) / 2
        man[0].location.z = gz + 0.55
        man[1].location.z = gz + 1.3
        corners = [Vector((x, y, z)) for x in (bmn.x, bmx.x + 1.1) for y in (bmn.y, bmx.y) for z in (max(bmn.z, gz), bmx.z)]
        c = sum(corners, Vector()) / 8
        rot = fwd.to_track_quat("-Z", "Y")
        cam.rotation_euler = rot.to_euler()
        right = rot @ Vector((1, 0, 0))
        up = rot @ Vector((0, 1, 0))
        xs = [(p - c).dot(right) for p in corners]
        ys = [(p - c).dot(up) for p in corners]
        wspan = max(xs) - min(xs)
        hspan = max(ys) - min(ys)
        aspect = tw / th
        cam_data.ortho_scale = max(wspan, hspan * aspect) * 1.12 + 0.3
        center = c + right * (max(xs) + min(xs)) / 2 + up * ((max(ys) + min(ys)) / 2 - 0.06 * cam_data.ortho_scale / aspect)
        cam.location = center - fwd * 40
        cam_data.clip_end = 100
        # etiket
        tc = bpy.data.curves.new("t", "FONT")
        tc.body = f"{meta['id']}  {meta['tris']} tris"
        tc.size = cam_data.ortho_scale * 0.055
        tobj = bpy.data.objects.new("label", tc)
        tobj.data.materials.append(txt_mat)
        scene.collection.objects.link(tobj)
        tobj.parent = cam
        tobj.location = (-cam_data.ortho_scale / 2 * 0.95, -cam_data.ortho_scale / aspect / 2 * 0.92, -2)
        path = os.path.join(TMP_DIR, f"{i:02d}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(tobj)
        for o in objs:
            o.hide_render = True
        img = bpy.data.images.load(path)
        px = np.array(img.pixels[:], np.float32).reshape(th, tw, 4)
        bpy.data.images.remove(img)
        r, cidx = i // cols, i % cols
        y0 = (rows - 1 - r) * th
        sheet[y0:y0 + th, cidx * tw:(cidx + 1) * tw] = px
    out = bpy.data.images.new("sheet", cols * tw, rows * th, alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = SHEET
    out.file_format = "PNG"
    out.save()
    for f in os.listdir(TMP_DIR):
        os.remove(os.path.join(TMP_DIR, f))
    os.rmdir(TMP_DIR)


# ---------------------------------------------------------------------------
def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    tex = 1024
    sheet = True
    i = 0
    while i < len(argv):
        if argv[i] == "--only":
            only = set(argv[i + 1].split(","))
            i += 1
        elif argv[i] == "--tex":
            tex = int(argv[i + 1])
            i += 1
        elif argv[i] == "--no-sheet":
            sheet = False
        i += 1
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    albedo, normal, rm = make_textures(tex)
    i_alb = np_to_image("wood_atlas_albedo", albedo, os.path.join(OUT_DIR, "wood_atlas_albedo.png"), False)
    i_n = np_to_image("wood_atlas_normal", normal, os.path.join(OUT_DIR, "wood_atlas_normal.png"), True)
    i_rm = np_to_image("wood_atlas_rm", rm, os.path.join(OUT_DIR, "wood_atlas_rm.png"), True)
    mat = make_material(i_alb, i_n, i_rm)
    entries = []
    json_path = os.path.join(OUT_DIR, "sockets.json")
    old = {}
    if only and os.path.exists(json_path):
        with open(json_path, encoding="utf-8") as f:
            old = json.load(f).get("pieces", {})
    pieces = {}
    for pid, fn in PIECES:
        if only and pid not in only:
            if pid in old:
                pieces[pid] = old[pid]
            continue
        objs, meta = build_piece(pid, fn, mat)
        export_glb(objs, os.path.join(OUT_DIR, f"{pid}.glb"))
        pieces[pid] = meta
        entries.append((objs, meta))
        flag = "" if 200 <= meta["tris"] <= 1500 else "  <-- BUTCE DISI"
        print(f"KIT {pid:26s} tris={meta['tris']:5d} size={meta['size']}{flag}")
    doc = {
        "version": 1,
        "material": "wood",
        "grid": G,
        "storey": S,
        "axes": "Godot: +Y yukari, -Z ileri; parcanin +Z yuzu 'on/dis' yuzdur. Birim metre.",
        "support": {"max_span": 3, "hp": 400, "horizontal_kinds": ["floor", "roof", "beam"]},
        "socket_types": {
            "edge": "Temel/zemin/platform ust yuzunun kenar orta noktasi (her 3 m); dir = disa dogru",
            "corner": "Ust yuz kosesi; dir = parcanin +Z'si",
            "cell": "3x3 hucre merkezi (ust yuz); merdiven, rampa, kule, makine",
            "wall_bottom": "Duvar turu parcanin alt kenar merkezi (orijin); dir = duvarin on yuzu",
            "wall_top": "Duvarin mantiksal ust kenar merkezi (kat cizgisi); dir = on yuz",
            "wall_end": "Cit/sur ucu (alt); uc uca ekleme",
            "pillar_bottom": "Sutun alti", "pillar_top": "Sutun tepesi (y=3)",
            "beam_end": "Kiris ucu, sutun tepesine oturur",
            "cell_bottom": "Merdiven/rampa/kule taban merkezi",
            "stair_bottom": "Merdiven/rampa alt kenari", "stair_top": "Merdiven/rampa ust kenari",
            "roof_eave": "Catinin sacak cizgisi (duvar ust ekseninde)", "roof_top": "Mahya noktasi (+3 m, 3 m iceride)",
            "roof_side": "Cati yan kenari", "ridge": "Mahya kapaginin orijini", "ridge_end": "Mahya kapagi ucu",
        },
        "rules": RULES,
        "pieces": pieces,
    }
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    write_godot_imports([pid for pid, _ in PIECES])
    if sheet and entries:
        render_sheet(entries)
    print("KIT DONE", len(pieces))


if __name__ == "__main__":
    main()
