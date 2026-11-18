"""Arazi katmanlari: sahibin varlik kitapligindaki taramalardan (F:/Assets, yalniz okunur) paketli 2K dokular.

Calistirma (Blender'in Python'u):
    "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" --background --factory-startup \
        --python pipeline/import_terrain_scans.py -- [kaynak_kok] [cikti] [boyut] [katman,listesi]
Yapilan: cimen, toprak, kaya 2048; kum, tarla 1024 (World.Ground.LoadArray dizideki en buyuge buyutur).

Cikti, gen_terrain_textures.py ile ayni duzende (terrain.gdshader bunu okur):
    <ad>_albedo.png : RGB renk (sRGB), A yukseklik (0..1)
    <ad>_nrm.png    : R,G normalin dunya X ve Z bileseni (G: goruntu satiri asagi = dunya +Z), B puruzluluk, A AO
Normal haritasinin kurali (OpenGL / DirectX) yukseklik haritasiyla karsilastirilarak kendiliginden bulunur.
Tarla katmani: orman tabani taramasinin rengi ve ayrintisi ustune koddan egatlar.
Lisanslar pipeline/licenses.md'de.
"""

import os
import struct
import sys
import zlib

import numpy as np
import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SRC = ARGS[0] if len(ARGS) > 0 else "F:/Assets"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = ARGS[1] if len(ARGS) > 1 else os.path.join(ROOT, "game", "assets", "textures", "terrain")
N = int(ARGS[2]) if len(ARGS) > 2 else 2048

LAYERS = {
    # ad: (klasor, renk, normal, puruzluluk, yukseklik, ao)  -- None: tureterek
    "cimen": ("leafy_grass_4k", "leafy_grass_diff_4k.png", "leafy_grass_nor_gl_4k.png", "leafy_grass_rough_4k.png",
              "leafy_grass_disp_4k.png", None),
    "toprak": ("forest_floor_wgyhdgy_4k", "Forest_Floor_wgyhdgy_4K_BaseColor.jpg", "Forest_Floor_wgyhdgy_4K_Normal.jpg",
               "Forest_Floor_wgyhdgy_4K_Roughness.jpg", "Forest_Floor_wgyhdgy_4K_Displacement.jpg", "Forest_Floor_wgyhdgy_4K_AO.jpg"),
    "kaya": ("cracked_nordic_coastal_rock_ukohaj0mw_4k", "*BaseColor*", "*Normal*", "*Roughness*", "*Bump*", "*AO*"),
    "kum": ("beach_sand_uinladeo_4k", "Beach_Sand_uinladeo_4K_BaseColor.jpg", "Beach_Sand_uinladeo_4K_Normal.jpg",
            "Beach_Sand_uinladeo_4K_Roughness.jpg", "Beach_Sand_uinladeo_4K_Bump.jpg", "Beach_Sand_uinladeo_4K_AO.jpg"),
}


# Renk ortalamasi Altin Cayir paletine cekilir (kanal basina carpan; ayrinti ve oranlar korunur).
# Hedefler eski koddan dokularin ortalamalari; World.Ground.CimenMean = cimen hedefi.
GRADE = {
    "cimen": (0.42, 0.40, 0.20),
    "toprak": (0.42, 0.32, 0.21),
    "kum": (0.67, 0.60, 0.47),
}


def find(folder, pattern):
    if pattern is None:
        return None
    d = os.path.join(SRC, folder)
    if "*" not in pattern:
        return os.path.join(d, pattern)
    key = pattern.strip("*").lower()
    for f in sorted(os.listdir(d)):
        if key in f.lower() and "billboard" not in f.lower():
            return os.path.join(d, f)
    raise FileNotFoundError(f"{folder}/{pattern}")


def load(path, n=N):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"  # degerler oldugu gibi (sRGB renk sRGB kalir)
    if img.size[0] != n or img.size[1] != n:
        img.scale(n, n)
    a = np.array(img.pixels[:], np.float32).reshape(n, n, 4)[::-1]
    bpy.data.images.remove(img)
    return a


def write_png(path, rgba):
    h, w, _ = rgba.shape
    raw = np.zeros((h, w * 4 + 1), dtype=np.uint8)
    raw[:, 1:] = rgba.reshape(h, w * 4)
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw.tobytes(), 9)) + chunk(b"IEND", b""))


def norm01(a, lo=0.5, hi=99.5):
    p0, p1 = np.percentile(a, [lo, hi])
    return np.clip((a - p0) / max(p1 - p0, 1e-6), 0, 1).astype(np.float32)


def blur(a, s):
    n = a.shape[0]
    f = np.fft.fftfreq(n)
    g = np.exp(-2 * (np.pi * s) ** 2 * (f[None, :] ** 2 + f[:, None] ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g)).astype(np.float32)


def pack_normal(nrm_rgb, height):
    """Teget uzay normalini dunya X/Z bilesenlerine cevirir. Kural: G ile yuksekligin satir turevinin isareti."""
    nx = nrm_rgb[..., 0] * 2 - 1
    ny = nrm_rgb[..., 1] * 2 - 1
    dh_row = np.roll(height, -1, 0) - np.roll(height, 1, 0)
    corr = float(np.mean((ny - ny.mean()) * (dh_row - dh_row.mean())))
    # OpenGL: ny = +dh/dsatir; DirectX: ny = -dh/dsatir. Bizim G (dunya Z) = -dh/dsatir.
    gl = corr > 0
    nz = -ny if gl else ny
    return nx, nz, ("GL" if gl else "DX"), corr


def save(name, albedo, height, nx, nz, rough, ao):
    a = np.concatenate([np.clip(albedo, 0, 1), np.clip(height, 0, 1)[..., None]], -1)
    b = np.stack([np.clip(nx * 0.5 + 0.5, 0, 1), np.clip(nz * 0.5 + 0.5, 0, 1), np.clip(rough, 0, 1), np.clip(ao, 0, 1)], -1)
    write_png(os.path.join(OUT, name + "_albedo.png"), (a * 255 + 0.5).astype(np.uint8))
    write_png(os.path.join(OUT, name + "_nrm.png"), (b * 255 + 0.5).astype(np.uint8))
    m = np.clip(albedo, 0, 1).reshape(-1, 3).mean(0)
    print(f"SCAN {name} mean_srgb=({m[0]:.3f},{m[1]:.3f},{m[2]:.3f})")


def layer(name, spec, write=True):
    folder, col, nor, rou, hei, aoo = spec
    c = load(find(folder, col))[..., :3]
    if name in GRADE:
        c = np.clip(c * (np.array(GRADE[name], np.float32) / c.reshape(-1, 3).mean(0)), 0, 1)
    h = norm01(load(find(folder, hei))[..., 0])
    nx, nz, conv, corr = pack_normal(load(find(folder, nor))[..., :3], h)
    r = load(find(folder, rou))[..., 0]
    ao = load(find(folder, aoo))[..., 0] if aoo else np.clip(1.0 - (blur(h, 6) - h) * 2.0, 0, 1)
    print(f"SCAN {name} normal={conv} corr={corr:.5f}")
    if write:
        save(name, c, h, nx, nz, r, ao)
    return c, h, nx, nz, r, ao


def tarla(base):
    """Surulmus tarla: orman tabani taramasi + X boyunca 4 egat (karo basina), sirt kuru, dip koyu ve nemli."""
    c, h, nx, nz, r, ao = base
    n = c.shape[0]
    rng = np.random.default_rng(61)
    warp = blur(rng.standard_normal((n, n)).astype(np.float32), 60)
    warp = (warp - warp.mean()) / (warp.std() + 1e-6)
    v = (np.arange(n) / n)[:, None] * np.ones((1, n), np.float32)
    rows = np.abs(np.sin(np.pi * (v * 4 + warp * 0.015)))
    ridge = rows ** 0.6
    hh = np.clip(ridge * 0.65 + h * 0.35, 0, 1)
    # Egat normali: yuksekligin satir turevinden (karo 2 m, tepe-cukur 0,12 m).
    texel = 2.0 / n
    dz = (np.roll(ridge, -1, 0) - np.roll(ridge, 1, 0)) * 0.12 / (2 * texel)
    fz = -dz / np.sqrt(1 + dz * dz)
    nzz = np.clip(nz * 0.6 + fz, -1, 1)
    dry = np.clip(ridge * 1.1 - 0.15, 0, 1)[..., None]
    col = c * (0.55 + 0.45 * dry) * np.array([1.0, 0.92, 0.85], np.float32)
    rough = np.clip(r * (0.85 + 0.15 * dry[..., 0]), 0, 1)
    aoo = ao * (0.65 + 0.35 * rows)
    save("tarla", col, hh, nx * 0.6, nzz, rough, aoo)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    only = set(ARGS[3].split(",")) if len(ARGS) > 3 else None
    done = {}
    for name, spec in LAYERS.items():
        if only is None or name in only or (name == "toprak" and "tarla" in only):
            done[name] = layer(name, spec, only is None or name in only)
    if only is None or "tarla" in only:
        tarla(done["toprak"])
    print("SCAN done", OUT)
