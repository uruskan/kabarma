"""agy ham gorselini ikona cevirir: macenta (#FF00FF) arka plani anahtarla, renk tasmasini temizle,
kirp, kareye ortala (%84 doluluk), hafif temas golgesi ekle (model ikonlarindaki golge yakalayici gibi), 256 px.

  python pipeline/icon_post.py <ham.png> <cikti.png> [--size 256] [--fill 0.84] [--no-shadow]

Anahtarlama: kenarlardan baslayan taskin dolgusu (yalniz arka plana bagli macenta silinir; nesnenin icindeki mor/pembe korunur),
yumusak kenar icin renk uzakligina gore alfa rampasi, sonra macenta tasmasi (R ve B'nin G'yi astigi kisim) bastirilir.
"""
import sys, argparse
from collections import deque
from PIL import Image, ImageFilter

ap = argparse.ArgumentParser()
ap.add_argument("src"); ap.add_argument("out")
ap.add_argument("--size", type=int, default=256)
ap.add_argument("--fill", type=float, default=0.84)
ap.add_argument("--no-shadow", action="store_true")
a = ap.parse_args()

im = Image.open(a.src).convert("RGB")
W, H = im.size
px = im.load()

# arka plan rengi: kenar piksellerinin ortancasi (tam macenta olmayabilir)
edge = [px[x, 0] for x in range(W)] + [px[x, H - 1] for x in range(W)] + [px[0, y] for y in range(H)] + [px[W - 1, y] for y in range(H)]
bg = tuple(sorted(c[i] for c in edge)[len(edge) // 2] for i in range(3))

def dist(c):
    return ((c[0] - bg[0]) ** 2 + (c[1] - bg[1]) ** 2 + (c[2] - bg[2]) ** 2) ** 0.5

def magenta_ness(c):
    # macentaya benzerlik: R ve B yuksek, G dusuk
    return min(c[0], c[2]) - c[1]

T_IN, T_OUT = 70, 150  # bu uzakligin altinda tam arka plan, ustunde tam nesne
alpha = Image.new("L", (W, H), 255)
al = alpha.load()
seen = bytearray(W * H)
q = deque()
for x in range(W):
    q.append((x, 0)); q.append((x, H - 1))
for y in range(H):
    q.append((0, y)); q.append((W - 1, y))
while q:
    x, y = q.popleft()
    i = y * W + x
    if seen[i]:
        continue
    seen[i] = 1
    c = px[x, y]
    d = dist(c)
    if d >= T_OUT and magenta_ness(c) < 60:
        continue  # nesne siniri
    t = max(0.0, min(1.0, (d - T_IN) / (T_OUT - T_IN)))
    if magenta_ness(c) > 90:
        t = min(t, 0.15)
    al[x, y] = int(255 * t)
    if t < 0.98:
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < W and 0 <= ny < H and not seen[ny * W + nx]:
                q.append((nx, ny))

# kapali ceplerdeki saf macenta (kulp araligi vb.)
for y in range(H):
    for x in range(W):
        c = px[x, y]
        if al[x, y] > 0 and dist(c) < T_IN:
            al[x, y] = 0

# tasma bastirma: macentaya kayan kenar renklerini notrle
out = im.convert("RGBA")
op = out.load()
for y in range(H):
    for x in range(W):
        r, g, b = px[x, y]
        m = min(r, b) - g
        if m > 0 and al[x, y] < 255 or m > 70:
            k = m * (0.85 if al[x, y] < 255 else 0.5)
            r = int(max(0, r - k)); b = int(max(0, b - k))
        op[x, y] = (r, g, b, al[x, y])
# alfa kenarini 1 px icine cek (ince macenta hale)
a_er = alpha.filter(ImageFilter.MinFilter(3))
alpha = Image.blend(alpha, a_er, 0.6)
out.putalpha(alpha)

bbox = alpha.point(lambda v: 255 if v > 24 else 0).getbbox()
if not bbox:
    print("POST_EMPTY", a.src); sys.exit(1)
obj = out.crop(bbox)
S = a.size * 2
inner = int(S * a.fill)
sc = inner / max(obj.size)
obj = obj.resize((max(1, int(obj.size[0] * sc)), max(1, int(obj.size[1] * sc))), Image.LANCZOS)
canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ox = (S - obj.size[0]) // 2
oy = (S - obj.size[1]) // 2 - int(S * 0.02)
if not a.no_shadow:
    # temas golgesi: nesnenin alt bolgesinin yassilmis, bulaniklastirilmis alfasi
    sh_w = int(obj.size[0] * 0.8); sh_h = max(4, int(obj.size[1] * 0.12))
    sh = Image.new("L", (S, S), 0)
    from PIL import ImageDraw
    ImageDraw.Draw(sh).ellipse([S // 2 - sh_w // 2, oy + obj.size[1] - sh_h // 2, S // 2 + sh_w // 2, oy + obj.size[1] + sh_h // 2], fill=120)
    sh = sh.filter(ImageFilter.GaussianBlur(S * 0.025))
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0)); shadow.putalpha(sh)
    canvas.alpha_composite(shadow)
canvas.alpha_composite(obj, (ox, oy))
canvas = canvas.resize((a.size, a.size), Image.LANCZOS)
canvas.save(a.out)
print("POST_OK", a.out, "bg", bg)
