"""Arazi dokulari: bes dosenebilir PBR katmani, tamamen koddan (kredi yok, lisans: projenin kendi isi).

Calistirma (Blender'in Python'u numpy icerir):
    "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" --background --factory-startup \
        --python pipeline/gen_terrain_textures.py -- [cikti_klasoru] [boyut]

Her katman iki PNG yazar (game/assets/textures/terrain/):
    <ad>_albedo.png : RGB = renk (sRGB), A = yukseklik (0..1; katmanlar arasi yukseklik karisimi icin)
    <ad>_nrm.png    : R,G = normalin dunya X ve Z bileseni (0,5 merkez; G dunya +Z, goruntu satiri asagi = +Z),
                      B = puruzluluk, A = ortam kapamasi (AO)
Katmanlar: cimen (cayir tabani), toprak (patika, cigne toprak), kaya (egimli yuzey), kum (gol kiyisi), tarla (surulmus).

Butun gurultuler FFT ile periyodik uretilir: doku kenarlari dikissiz dosenir.
"""

import os
import struct
import sys
import zlib

import numpy as np

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = ARGS[0] if len(ARGS) > 0 else os.path.join(ROOT, "game", "assets", "textures", "terrain")
N = int(ARGS[1]) if len(ARGS) > 1 else 1024
os.makedirs(OUT, exist_ok=True)


# ---------------------------------------------------------------- yardimcilar

def write_png(path, rgba):
    """rgba: (h, w, 4) uint8. zlib + struct; bpy renk yonetimine girmez."""
    h, w, _ = rgba.shape
    raw = np.zeros((h, w * 4 + 1), dtype=np.uint8)
    raw[:, 1:] = rgba.reshape(h, w * 4)
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw.tobytes(), 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def norm01(a, lo=0.5, hi=99.5):
    p0, p1 = np.percentile(a, [lo, hi])
    return np.clip((a - p0) / max(p1 - p0, 1e-9), 0.0, 1.0).astype(np.float32)


def freqs(n):
    f = np.fft.fftfreq(n) * n
    return np.sqrt(f[None, :] ** 2 + f[:, None] ** 2)


def fbm(n, seed, beta=1.8, fmin=1.0, fmax=None):
    """Periyodik kesirli gurultu: beyaz gurultu, 1/f^beta genlik. fmin/fmax dalga sayisi (doku basina devir)."""
    r = np.random.default_rng(seed)
    F = np.fft.fft2(r.standard_normal((n, n)))
    f = freqs(n)
    f[0, 0] = 1.0
    amp = f ** (-beta)
    amp[f < fmin] = 0.0
    if fmax:
        amp *= np.exp(-(f / fmax) ** 2)
    return norm01(np.real(np.fft.ifft2(F * amp)))


def blur(a, sigma_px):
    """Periyodik Gauss bulaniklik (FFT)."""
    n = a.shape[0]
    f = np.fft.fftfreq(n)
    g = np.exp(-2 * (np.pi * sigma_px) ** 2 * (f[None, :] ** 2 + f[:, None] ** 2))
    if a.ndim == 3:
        return np.stack([np.real(np.fft.ifft2(np.fft.fft2(a[..., c]) * g)) for c in range(a.shape[2])], -1).astype(np.float32)
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g)).astype(np.float32)


def voronoi(n, cells, seed, jitter=0.9, warp=0.0):
    """Periyodik Voronoi: F1, F2 (hucre biriminde) ve hucre kimligi (0..1). warp: alan bukme (hucre biriminde)."""
    r = np.random.default_rng(seed)
    pts = 0.5 + (r.random((cells, cells, 2)) - 0.5) * jitter
    ids = r.random((cells, cells))
    u = (np.arange(n) + 0.5) / n * cells
    X, Y = np.meshgrid(u, u)
    if warp > 0:
        X = X + (fbm(n, seed + 500, 2.0, 1, 10) - 0.5) * warp * 2 + (fbm(n, seed + 502, 1.5, 4, 40) - 0.5) * warp * 0.6
        Y = Y + (fbm(n, seed + 501, 2.0, 1, 10) - 0.5) * warp * 2 + (fbm(n, seed + 503, 1.5, 4, 40) - 0.5) * warp * 0.6
    ix, iy = np.floor(X).astype(np.int32), np.floor(Y).astype(np.int32)
    f1 = np.full((n, n), 9.0, np.float32)
    f2 = np.full((n, n), 9.0, np.float32)
    cid = np.zeros((n, n), np.float32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx, cy = ix + dx, iy + dy
            p = pts[cy % cells, cx % cells]
            d = np.hypot(X - (cx + p[..., 0]), Y - (cy + p[..., 1])).astype(np.float32)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            cid = np.where(closer, ids[cy % cells, cx % cells], cid)
            f1 = np.where(closer, d, f1)
    return f1, f2, cid


def downsample(a, k):
    if k == 1:
        return a
    n = a.shape[0] // k
    if a.ndim == 3:
        return a.reshape(n, k, n, k, a.shape[2]).mean(axis=(1, 3))
    return a.reshape(n, k, n, k).mean(axis=(1, 3))


def strokes(M, count, length, width, seed, colour_fn, h_lo, h_hi, curl=0.25, K=24, offsets=3):
    """Ust bakista bicak/sap izleri: dip koyu, uca dogru acik ve yuksek. Donus: renk (M,M,3), yukseklik (M,M), maske."""
    r = np.random.default_rng(seed)
    x0, y0 = r.random(count) * M, r.random(count) * M
    ang = r.random(count) * 2 * np.pi
    L = r.uniform(length[0], length[1], count)
    bend = r.uniform(-curl, curl, count)
    root_c, tip_c = colour_fn(r, count)
    base_h = r.uniform(h_lo[0], h_lo[1], count)
    top_h = r.uniform(h_hi[0], h_hi[1], count)
    t = np.linspace(0, 1, K, dtype=np.float32)
    dx, dy = np.cos(ang)[:, None], np.sin(ang)[:, None]
    px, py = -dy, dx
    off = (bend[:, None] * t[None, :] ** 2) * L[:, None]
    xs = x0[:, None] + dx * L[:, None] * t[None, :] + px * off
    ys = y0[:, None] + dy * L[:, None] * t[None, :] + py * off
    taper = (1.0 - 0.85 * t)[None, :] * width
    hv = base_h[:, None] + (top_h - base_h)[:, None] * t[None, :] ** 0.8
    col = root_c[:, None, :] + (tip_c - root_c)[:, None, :] * (t[None, :, None] ** 0.7)
    allx, ally, allh, allc = [], [], [], []
    for o in np.linspace(-0.5, 0.5, offsets):
        allx.append(xs + px * taper * o)
        ally.append(ys + py * taper * o)
        allh.append(hv)
        allc.append(col)
    X = np.concatenate([a.ravel() for a in allx])
    Y = np.concatenate([a.ravel() for a in ally])
    H = np.concatenate([a.ravel() for a in allh])
    C = np.concatenate([a.reshape(-1, 3) for a in allc])
    order = np.argsort(H, kind="stable")
    ix = np.floor(X[order]).astype(np.int64) % M
    iy = np.floor(Y[order]).astype(np.int64) % M
    flat = iy * M + ix
    out_h = np.full(M * M, -1.0, np.float32)
    out_c = np.zeros((M * M, 3), np.float32)
    out_h[flat] = H[order]          # yinelenen indekste son yazilan (en yuksek) kalir
    out_c[flat] = C[order]
    mask = out_h >= 0
    return out_c.reshape(M, M, 3), out_h.reshape(M, M), mask.reshape(M, M)


def normals_from_height(h, tile_m, amp_m, sharp=1.0):
    """Yukseklik (0..1) -> dunya X/Z bilesenli normal. tile_m: dokunun dunyadaki boyu, amp_m: tepe-cukur farki."""
    n = h.shape[0]
    texel = tile_m / n
    hm = h * amp_m
    dhdx = (np.roll(hm, -1, 1) - np.roll(hm, 1, 1)) / (2 * texel)
    dhdz = (np.roll(hm, -1, 0) - np.roll(hm, 1, 0)) / (2 * texel)
    nx, nz, ny = -dhdx * sharp, -dhdz * sharp, np.ones_like(hm)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return nx / l, nz / l


def cavity_ao(h, radius_px, strength):
    return np.clip(1.0 - (blur(h, radius_px) - h) * strength, 0.0, 1.0)


def save(name, albedo, height, rough, ao, tile_m, amp_m, sharp=1.0):
    nx, nz = normals_from_height(height, tile_m, amp_m, sharp)
    a = np.concatenate([np.clip(albedo, 0, 1), height[..., None]], -1)
    b = np.stack([nx * 0.5 + 0.5, nz * 0.5 + 0.5, np.clip(rough, 0, 1), np.clip(ao, 0, 1)], -1)
    write_png(os.path.join(OUT, name + "_albedo.png"), (a * 255 + 0.5).astype(np.uint8))
    write_png(os.path.join(OUT, name + "_nrm.png"), (b * 255 + 0.5).astype(np.uint8))
    mean = np.clip(albedo, 0, 1).reshape(-1, 3).mean(0)
    print(f"TEX {name} mean_srgb=({mean[0]:.3f},{mean[1]:.3f},{mean[2]:.3f})")


def lerp(a, b, t):
    t = np.asarray(t, np.float32)
    if t.ndim >= 2:
        t = t[..., None]
    return a + (b - a) * t


def rgb(*c):
    return np.array(c, np.float32)


def pick(r, count, palette, weights):
    w = np.array(weights, np.float64)
    i = r.choice(len(palette), count, p=w / w.sum())
    base = np.array(palette, np.float32)[i]
    return base * r.uniform(0.85, 1.12, (count, 1)).astype(np.float32)


# ---------------------------------------------------------------- katmanlar

def soil_base(n, seed, dark, light):
    big = fbm(n, seed, 1.6, 2, 40)
    fine = fbm(n, seed + 1, 1.0, 40)
    t = np.clip(big * 0.75 + fine * 0.35 - 0.05, 0, 1)
    return lerp(np.broadcast_to(dark, (n, n, 3)), np.broadcast_to(light, (n, n, 3)), t), big, fine


def gen_cimen():
    """Cayir tabani: koyu toprak, kuru sap kirintisi, ustte yesil-altin bicaklar (3 m karo)."""
    tile = 3.0
    SS = 2
    M = N * SS
    soil, big, fine = soil_base(M, 11, rgb(0.20, 0.16, 0.11), rgb(0.36, 0.29, 0.20))
    h = big * 0.15 + fine * 0.08
    colour = soil.copy()

    # Kuru sap ve yaprak kirintisi (yere yatik, acik saman).
    def litter(r, c):
        root = pick(r, c, [(0.55, 0.46, 0.30), (0.66, 0.56, 0.38), (0.45, 0.38, 0.26)], [3, 2, 2])
        return root, root * 1.08
    lc, lh, lm = strokes(M, int(9000 * SS * SS / 4), (18 * SS, 50 * SS), 2.5 * SS, 12, litter, (0.22, 0.28), (0.26, 0.34), curl=0.4)
    colour[lm] = lc[lm]
    h = np.where(lm, np.maximum(h, lh), h)

    # Bicaklar: kok koyu zeytin, uc altin ya da taze yesil (Altin Cayir: yesil + altin karisik).
    def blades(r, c):
        tips = pick(r, c, [(0.40, 0.50, 0.17), (0.52, 0.58, 0.21), (0.66, 0.64, 0.28), (0.80, 0.69, 0.37), (0.72, 0.60, 0.33)],
                    [3, 3.5, 3, 2.2, 1.3])
        roots = tips * np.array([0.42, 0.48, 0.40], np.float32) + np.array([0.02, 0.03, 0.0], np.float32)
        return roots, tips
    bc, bh, bm = strokes(M, int(30000 * SS * SS / 4), (40 * SS, 120 * SS), 4.0 * SS, 13, blades, (0.3, 0.4), (0.7, 1.0), curl=0.35, K=40)
    colour[bm] = bc[bm]
    h = np.where(bm, np.maximum(h, bh), h)

    colour = downsample(colour, SS)
    h = downsample(h, SS)
    # Buyuk olcekte hafif lekelenme: karo icinde bile birornek olmasin.
    patch = fbm(N, 14, 2.0, 1, 6)
    colour *= (0.9 + 0.2 * patch)[..., None]
    rough = 0.82 - 0.12 * np.clip((h - 0.6) * 2.5, 0, 1) + 0.08 * fbm(N, 15, 1.0, 30)
    ao = cavity_ao(h, 6, 1.6) * (0.75 + 0.25 * np.clip(h * 1.4, 0, 1))
    save("cimen", colour, h, rough, ao, tile, 0.035, 1.0)


def gen_toprak():
    """Cignenmis kuru toprak ve patika: kum taneli, cakil, ince catlak, birkac kuru sap (3 m karo)."""
    tile = 3.0
    soil, big, fine = soil_base(N, 21, rgb(0.34, 0.26, 0.18), rgb(0.58, 0.47, 0.33))
    grit = fbm(N, 22, 0.4, 200)
    h = big * 0.35 + fine * 0.15 + grit * 0.06
    colour = soil * (0.92 + 0.16 * grit)[..., None]
    # Catlaklar: kucuk Voronoi hucre kenarlari, gurultuyle seyrek.
    f1, f2, _ = voronoi(N, 14, 23, 0.95)
    crack = np.clip(1.0 - (f2 - f1) / 0.05, 0, 1) * np.clip((fbm(N, 24, 1.8, 2, 12) - 0.45) * 4, 0, 1)
    h -= crack * 0.25
    colour *= (1.0 - crack * 0.35)[..., None]
    # Cakil: iki olcekte yuvarlak taslar.
    for cells, seed, size, amt in ((36, 25, 0.42, 0.22), (110, 26, 0.36, 0.12)):
        p1, _, pid = voronoi(N, cells, seed, 0.9, warp=0.25)
        keep = pid < amt
        rel = pid / max(amt, 1e-3)
        stone = np.clip(1.0 - p1 / (size * (0.45 + 0.7 * rel)), 0, 1) * keep
        dome = np.sqrt(stone)
        tint = lerp(rgb(0.36, 0.32, 0.27), rgb(0.58, 0.53, 0.45), rel)
        colour = np.where((stone > 0.02)[..., None], lerp(colour, tint * (0.8 + 0.25 * dome[..., None]), np.clip(stone * 4, 0, 1)), colour)
        h = np.where(stone > 0, np.maximum(h, 0.45 + dome * 0.5), h)
    # Birkac kuru sap.
    def straws(r, c):
        root = pick(r, c, [(0.62, 0.52, 0.33), (0.72, 0.62, 0.40)], [1, 1])
        return root * 0.85, root
    sc, sh, sm = strokes(N, 2200, (14, 40), 2.0, 27, straws, (0.42, 0.5), (0.48, 0.6), curl=0.5)
    colour[sm] = sc[sm]
    h = np.where(sm, np.maximum(h, sh), h)
    h = np.clip(h, 0, 1)
    rough = 0.93 - 0.18 * np.clip((h - 0.5) * 2, 0, 1)
    ao = cavity_ao(h, 5, 2.2)
    save("toprak", colour, h, rough, ao, tile, 0.05, 1.0)


def gen_kaya():
    """Sicak gri kum tasi: katmanlanma, buyuk catlaklar, ince doku, altin liken lekeleri (5 m karo, uc duzlemli)."""
    tile = 5.0
    warp = fbm(N, 31, 2.2, 1, 8)
    v = (np.arange(N) / N)[:, None] * np.ones((1, N))
    strata = 0.5 + 0.5 * np.sin(2 * np.pi * (v * 7 + warp * 1.6))
    strata2 = 0.5 + 0.5 * np.sin(2 * np.pi * (v * 23 + warp * 3.1 + fbm(N, 32, 2.0, 2, 20) * 0.6))
    ridged = 1.0 - np.abs(fbm(N, 33, 1.9, 2) * 2 - 1)
    ridged2 = 1.0 - np.abs(fbm(N, 42, 1.6, 6) * 2 - 1)
    fine = fbm(N, 34, 1.1, 30)
    # Catlaklar: bukulmus Voronoi, genislik degisken, gurultuyle kesik (dose tasi gibi duzgun ag olmasin).
    f1, f2, cid = voronoi(N, 5, 35, 0.95, warp=0.45)
    wvar = 0.02 + 0.03 * fbm(N, 43, 1.8, 2, 20)
    cracks = np.clip(1.0 - (f2 - f1) / wvar, 0, 1) * np.clip((fbm(N, 44, 1.8, 1, 8) - 0.35) * 3, 0, 1)
    f1b, f2b, _ = voronoi(N, 13, 36, 0.95, warp=0.5)
    cracks_b = np.clip(1.0 - (f2b - f1b) / 0.025, 0, 1) * np.clip((fbm(N, 37, 1.5, 2, 16) - 0.55) * 4, 0, 1)
    strata *= 0.6
    h = 0.22 * strata + 0.08 * strata2 + 0.32 * ridged + 0.18 * ridged2 + 0.12 * fine + 0.1 * np.clip(f1 / 0.7, 0, 1)
    h = np.clip(h - cracks * 0.4 - cracks_b * 0.18, 0, 1)
    dark, mid, light = rgb(0.33, 0.30, 0.27), rgb(0.52, 0.48, 0.43), rgb(0.71, 0.66, 0.58)
    t = np.clip(0.2 + 0.35 * strata + 0.35 * ridged + 0.2 * fine - 0.15 + 0.1 * (cid - 0.5), 0, 1)
    colour = np.where((t < 0.5)[..., None], lerp(dark, mid, t * 2), lerp(mid, light, (t - 0.5) * 2))
    colour *= (0.85 + 0.3 * fbm(N, 38, 1.4, 1, 10))[..., None]
    # Sicak demir lekesi (pas rengi) katman boyunca.
    iron = np.clip((fbm(N, 39, 2.0, 2, 10) - 0.55) * 3, 0, 1) * strata
    colour = lerp(colour, rgb(0.58, 0.40, 0.26), iron * 0.45)
    # Liken: ust yuzlerde altin ve gri-yesil lekeler.
    lich = np.clip((fbm(N, 40, 1.3, 4, 80) - 0.62) * 6, 0, 1) * (h > 0.45)
    lich_c = lerp(rgb(0.72, 0.62, 0.30), rgb(0.55, 0.58, 0.45), fbm(N, 41, 2.0, 2, 20))
    colour = lerp(colour, lich_c, lich * 0.8)
    colour *= (1.0 - cracks * 0.55 - cracks_b * 0.3)[..., None]
    rough = 0.78 + 0.12 * fine - 0.1 * lich
    ao = cavity_ao(h, 8, 1.8) * (1.0 - cracks * 0.4)
    save("kaya", colour, h, rough, ao, tile, 0.25, 1.0)


def gen_kum():
    """Gol kiyisi: ince kum, hafif dalga izi, cakil ve kavkı (2,5 m karo). Islakligi shader ekler."""
    tile = 2.5
    grain = fbm(N, 51, 0.3, 250)
    warp = fbm(N, 52, 2.0, 1, 6)
    u = (np.arange(N) / N)[None, :] * np.ones((N, 1))
    ripple = 0.5 + 0.5 * np.sin(2 * np.pi * (u * 18 + warp * 2.5 + fbm(N, 53, 2.0, 2, 14) * 0.5))
    big = fbm(N, 54, 1.8, 1, 10)
    h = ripple * 0.35 + big * 0.25 + grain * 0.12
    colour = lerp(rgb(0.56, 0.49, 0.37), rgb(0.78, 0.70, 0.54), np.clip(big * 0.6 + ripple * 0.3 + grain * 0.2, 0, 1))
    colour *= (0.92 + 0.16 * grain)[..., None]
    for cells, seed, size, amt in ((80, 55, 0.3, 0.25), (190, 56, 0.25, 0.3)):
        p1, _, pid = voronoi(N, cells, seed, 0.8)
        keep = pid < amt
        stone = np.clip(1.0 - p1 / size, 0, 1) * keep
        tint = lerp(rgb(0.40, 0.37, 0.34), rgb(0.80, 0.77, 0.70), pid / max(amt, 1e-3))
        colour = np.where((stone > 0.02)[..., None], lerp(colour, tint, np.clip(stone * 5, 0, 1)), colour)
        h = np.where(stone > 0, np.maximum(h, 0.4 + np.sqrt(stone) * 0.5), h)
    h = np.clip(h, 0, 1)
    rough = 0.88 - 0.1 * np.clip((h - 0.6) * 3, 0, 1)
    ao = cavity_ao(h, 4, 1.5)
    save("kum", colour, h, rough, ao, tile, 0.03, 1.0)


def gen_tarla():
    """Surulmus tarla: X boyunca egatlar (karo basina 4 sira), topak, koyu nemli dip (2 m karo)."""
    tile = 2.0
    warp = fbm(N, 61, 2.2, 1, 8)
    v = (np.arange(N) / N)[:, None] * np.ones((1, N))
    rows = np.abs(np.sin(np.pi * (v * 4 + warp * 0.12)))
    ridge = rows ** 0.6
    f1, _, cid = voronoi(N, 70, 62, 0.9)
    clod = np.clip(1.0 - f1 / 0.6, 0, 1) ** 0.7 * (0.6 + 0.4 * cid)
    fine = fbm(N, 63, 0.8, 80)
    h = np.clip(ridge * 0.6 + clod * 0.25 * ridge + fine * 0.15, 0, 1)
    dry = np.clip(h * 1.2 - 0.25 + (fbm(N, 64, 1.8, 2, 12) - 0.5) * 0.4, 0, 1)
    colour = lerp(rgb(0.15, 0.10, 0.07), rgb(0.40, 0.30, 0.20), dry)
    colour *= (0.9 + 0.2 * fine)[..., None]
    rough = 0.97 - 0.12 * (1 - dry)
    ao = cavity_ao(h, 10, 1.4) * (0.7 + 0.3 * rows)
    save("tarla", colour, h, rough, ao, tile, 0.12, 1.0)


if __name__ == "__main__":
    only = set(ARGS[2].split(",")) if len(ARGS) > 2 else None
    for name, fn in (("cimen", gen_cimen), ("toprak", gen_toprak), ("kaya", gen_kaya), ("kum", gen_kum), ("tarla", gen_tarla)):
        if only is None or name in only:
            fn()
    print("TEX done", OUT)
