"""
El Arabasi saplari (oyuncu akisi, 18 Kasim 2026; sahibin karari: "Blender'da duzelt", kredi yok). KREDI HARCAMAZ.

Tripo arabasinda (f2eb82e) iki sap capraz koselerde: biri arkada, oteki onde tekerlegin ustunde. Iki el iki sapa
oturamaz. Bu betik saplarin bulundugu mesh'i (kova; saplar kovaya kaynasik tek parca) arka sapin oldugu yarisindan
simetrik yapar: mesh kovanin orta boyuna duzleminden kesilir, arka sapli yari aynalanir. Sonuc: iki sap arkada, birbirinin
aynasi; ondeki sap kalkar. Kovanin obur yarisi bu yarinin aynasi olur (doku da aynalanir). Kesik ucgenler duzlemde
bolunur, dikis koselerinin normalleri iki yanda ayni olsun diye duzleme paralel yapilir (golge dikisi kalmaz).

Delikler: kovanin on duvarinda Tripo'dan gelen bir delik var (catalin ucu icinden gecer); aynalama onu obur yana da
tasir, orada delikten yer gorunur. Ustten bakinca gorunen her delik, kovanin atilan yarisinin ayni yerdeki ozgun
(deliksiz) duvar parcasiyla yamanir: yama gercek duvarin bicimini ve dokusunu tasir, her kosesi kendi normali boyunca
mesh yuzeyinin PATCH_LIFT disina oturtulur (iki yari birkac mm farkli; ust uste binme titremesi olmaz).

Yalniz o mesh'in koseleri ve ucgenleri degisir: yeni veri GLB'nin ikili bolumunun sonuna eklenir ve primitive yeni
accessor'lara yonlendirilir. Dokular, malzemeler, obur mesh'ler (cerceve, tekerlek) ve dugumler (handle, load_slot,
wheel_pivot) aynen kalir. Tekrar kosmak zararsizdir: arkada zaten simetrik iki sap varsa dosyaya dokunmaz.

Zincir: oturt_gorsel.py -> run_golden.py (Tripo kaynagindan temiz GLB) -> grip_markers.py el_arabasi. grip_markers
arabayi olcmeden once bu betigin fix()'ini cagirir; oturt_gorsel.py'ye satir gerekmez. Tripo'nun ham kaynagina dokunulmaz.

  blender -b --factory-startup -P pipeline/fix_cart_handles.py                     # el_arabasi.glb'yi duzelt
  blender -b --factory-startup -P pipeline/fix_cart_handles.py -- <a.glb> --check  # yalniz olc

On yon: tekerlek ('wheel' adli dugum/mesh) tarafi. Ust bolge: yuksekligin ust yarisi. Sap ucu: ileri eksende en arka
REAR payindaki ust koseler, yana dogru SPLIT bosluguyla kumelenir.
"""
import json, os, struct, sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DEFAULT = os.path.join(ROOT, "game", "assets", "models", "el_arabasi", "el_arabasi.glb")
UP = np.array([0.0, 1.0, 0.0])

REAR = 0.15      # arka bolge: ileri eksende arabanin en arka bu payi
UPPER = 0.5      # ust bolge: yuksekligin bu oranindan yukarisi
SPLIT = 0.05     # sap uclarini yana ayiran bosluk (m)
SYM = 0.1        # iki sap ortaya gore bu kadar simetrik (m)
SPAN = (0.25, 0.9)  # iki sap arasi (m)
EPS = 1e-6       # duzlem toleransi (m)
CELL = 0.01      # ustten delik aramasinin izgara adimi (m)
HOLE_MAX = 0.03  # bundan buyuk kapali bosluk delik sayilmaz, betik durur (m2)
PATCH_PAD = 0.03     # yama deligin kenarini bu kadar asar (ustten bakista, m)
PATCH_LIFT = 0.0015  # yama yuzeyin bu kadar disinda (kendi normali boyunca, m)
PATCH_SNAP = 0.012   # yama kosesi en fazla bu kadar uzaktaki yuzeye oturtulur (daha uzaktaki obur katmandir, m)

GLB_MAGIC, JSON_T, BIN_T = 0x46546C67, 0x4E4F534A, 0x004E4942
CT = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}
ATTR_OK = {"POSITION", "NORMAL", "TEXCOORD_0", "TEXCOORD_1", "COLOR_0"}


class Fail(Exception):
    pass


def req(ok, why):
    if not ok:
        raise Fail(why)


# ---------------------------------------------------------------- GLB

def load(path):
    b = open(path, "rb").read()
    magic, _ver, length = struct.unpack_from("<III", b, 0)
    req(magic == GLB_MAGIC, "GLB degil")
    off, chunks = 12, []
    while off < length:
        n, t = struct.unpack_from("<II", b, off)
        chunks.append([t, b[off + 8: off + 8 + n]])
        off += 8 + n
    req(len(chunks) >= 2 and chunks[0][0] == JSON_T and chunks[1][0] == BIN_T, "GLB JSON/BIN bolumu yok")
    gl = json.loads(chunks[0][1].decode("utf-8").rstrip(" \0"))
    req(len(gl.get("buffers", [])) >= 1 and "uri" not in gl["buffers"][0], "ilk buffer GLB'nin ikili bolumu degil")
    return gl, bytearray(chunks[1][1][: gl["buffers"][0]["byteLength"]]), chunks[2:]


def save(path, gl, bin_, rest):
    gl["buffers"][0]["byteLength"] = len(bin_)
    js = json.dumps(gl, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    body = bytearray()
    for t, data in [[JSON_T, js], [BIN_T, bytes(bin_)]] + rest:
        data = bytes(data) + (b" " if t == JSON_T else b"\0") * ((4 - len(data) % 4) % 4)
        body += struct.pack("<II", len(data), t) + data
    with open(path, "wb") as f:
        f.write(struct.pack("<III", GLB_MAGIC, 2, 12 + len(body)) + body)


def read_acc(gl, bin_, i):
    a = gl["accessors"][i]
    req("sparse" not in a and "bufferView" in a, f"accessor {i} desteklenmiyor (sparse/bos)")
    bv = gl["bufferViews"][a["bufferView"]]
    req(bv.get("buffer", 0) == 0, f"accessor {i} baska bir buffer'da")
    dt, k = np.dtype(CT[a["componentType"]]), NC[a["type"]]
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", 0) or k * dt.itemsize
    arr = np.ndarray((a["count"], k), dt, bin_, off, (stride, dt.itemsize))
    return arr.copy()


def append_acc(gl, bin_, data, like, target):
    """data'yi ikili bolumun sonuna yeni bir bufferView olarak ekler; 'like' accessor'un turunde yeni accessor doner."""
    while len(bin_) % 4:
        bin_.append(0)
    off = len(bin_)
    bin_ += data.tobytes()
    gl["bufferViews"].append({"buffer": 0, "byteOffset": off, "byteLength": data.nbytes, "target": target})
    a = {k: v for k, v in like.items() if k not in ("bufferView", "byteOffset", "count", "min", "max", "sparse")}
    a["bufferView"] = len(gl["bufferViews"]) - 1
    a["count"] = int(data.shape[0])
    gl["accessors"].append(a)
    return a, len(gl["accessors"]) - 1


# ---------------------------------------------------------------- sahne

def local_matrix(n):
    if "matrix" in n:
        return np.array(n["matrix"], float).reshape(4, 4).T
    m = np.eye(4)
    x, y, z, w = n.get("rotation", [0, 0, 0, 1])
    m[:3, :3] = np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    m[:3, :3] = m[:3, :3] * np.array(n.get("scale", [1, 1, 1]))
    m[:3, 3] = n.get("translation", [0, 0, 0])
    return m


def mesh_nodes(gl):
    """(dugum adi, mesh indeksi, dunya matrisi) listesi."""
    nodes = gl.get("nodes", [])
    parent = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}

    def world(i):
        m = local_matrix(nodes[i])
        while i in parent:
            i = parent[i]
            m = local_matrix(nodes[i]) @ m
        return m
    return [(n.get("name", f"node{i}"), n["mesh"], world(i)) for i, n in enumerate(nodes) if "mesh" in n]


def clusters_1d(vals, gap):
    order = np.argsort(vals)
    out, start = [], 0
    for i in range(1, len(order) + 1):
        if i == len(order) or vals[order[i]] - vals[order[i - 1]] > gap:
            idx = order[start:i]
            out.append((float(vals[idx].min()), float(vals[idx].max()), idx))
            start = i
    return out


class Frame:
    """Arabanin olcu cercevesi: ileri (tekerlege), sag (ileri x yukari), yukseklik; arka ust bolgede sap uclari."""

    def __init__(self, gl, bin_):
        self.parts = []
        for name, mi, M in mesh_nodes(gl):
            pts = [read_acc(gl, bin_, p["attributes"]["POSITION"]).astype(float) for p in gl["meshes"][mi]["primitives"]]
            W = np.vstack(pts) @ M[:3, :3].T + M[:3, 3]
            self.parts.append((name, mi, M, W))
        P = np.vstack([w for *_, w in self.parts])
        lo, hi = P.min(0), P.max(0)
        ext = hi - lo
        ax = 0 if ext[0] >= ext[2] else 2
        fwd = np.zeros(3)
        fwd[ax] = 1.0
        wheel = [w for n, mi, _, w in self.parts if "wheel" in n.lower() or "wheel" in str(gl["meshes"][mi].get("name", "")).lower()]
        req(wheel, "tekerlek ('wheel') yok: on yon bilinemez")
        if np.vstack(wheel).mean(0) @ fwd < ((lo + hi) / 2) @ fwd:
            fwd = -fwd
        self.fwd, self.right = fwd, np.cross(fwd, UP)
        self.lo, self.hi, self.L, self.H = lo, hi, ext[ax], ext[1]
        self.smin, self.smax = float((P @ fwd).min()), float((P @ fwd).max())

    def handles(self, W):
        """W noktalarinda arkadaki sap uclari: [(yan orta, yan min, yan max)], ve ondeki ust uclar (bilgi)."""
        s, lat, y = W @ self.fwd, W @ self.right, W[:, 1]
        up = y > self.lo[1] + UPPER * self.H
        rear = (s < self.smin + REAR * self.L) & up
        front = (s > self.smax - REAR * self.L) & up
        cl = [((a + b) / 2, a, b) for a, b, _ in clusters_1d(lat[rear], SPLIT)] if rear.any() else []
        fr = [((a + b) / 2, a, b) for a, b, _ in clusters_1d(lat[front], SPLIT)] if front.any() else []
        return cl, fr

    def tub_mid(self, W):
        """Kovanin yana orta duzlemi: ileri eksende ortadaki bantta (saplarin disinda) yan uclarin ortasi."""
        s, lat = W @ self.fwd, W @ self.right
        c = (s.min() + s.max()) / 2
        band = np.abs(s - c) < 0.25 * (s.max() - s.min())
        return float((lat[band].min() + lat[band].max()) / 2)


def symmetric(cl, mid):
    if len(cl) != 2:
        return False
    (m1, *_), (m2, *_) = sorted(cl)
    return abs((m1 - mid) + (m2 - mid)) < SYM and SPAN[0] < m2 - m1 < SPAN[1]


# ---------------------------------------------------------------- kes ve aynala

def clip_mirror(attrs, tri, n_axis, mid, keep_sign):
    """attrs: ad -> (N, k) float; tri: (T, 3). n_axis yonunde 'mid' duzleminin keep_sign tarafini tutar, aynalar."""
    names = list(attrs)
    data = {k: v.astype(np.float64) for k, v in attrs.items()}
    pos = data["POSITION"]
    d = (pos @ n_axis - mid) * keep_sign
    inside = d >= -EPS
    cnt = inside[tri].sum(1)
    flat = (np.abs(d[tri]) < EPS).all(1)              # tam duzlemde yatan ucgen: aynasi ustune biner, atilir
    out = [tri[(cnt == 3) & ~flat]]
    rows = {k: [] for k in names}
    nv = len(pos)
    cache = {}

    def cut(i, j):
        key = (min(i, j), max(i, j))
        if key not in cache:
            t = d[i] / (d[i] - d[j])
            for k in names:
                rows[k].append(data[k][i] + (data[k][j] - data[k][i]) * t)
            cache[key] = nv + len(rows[names[0]]) - 1
        return cache[key]

    new = []
    for t in tri[(cnt == 1) | (cnt == 2)]:
        ins = inside[t]
        if ins.sum() == 1:
            r = int(np.argmax(ins))
            a, b, c = t[r], t[(r + 1) % 3], t[(r + 2) % 3]
            new.append((a, cut(a, b), cut(a, c)))
        else:
            r = int(np.argmin(ins))
            c, a, b = t[r], t[(r + 1) % 3], t[(r + 2) % 3]
            bc, ca = cut(b, c), cut(c, a)
            new += [(a, b, bc), (a, bc, ca)]
    if new:
        out.append(np.array(new, np.int64))
        for k in names:
            data[k] = np.vstack([data[k], np.array(rows[k])])
    T = np.vstack(out)
    used, inv = np.unique(T.ravel(), return_inverse=True)
    T = inv.reshape(-1, 3)
    data = {k: v[used] for k, v in data.items()}
    pos = data["POSITION"]
    off = pos @ n_axis - mid
    seam = np.abs(off) < 1e-5
    pos[seam] -= np.outer(off[seam], n_axis)         # dikis koseleri tam duzlemde
    if "NORMAL" in data:
        nrm = data["NORMAL"]
        flat = nrm[seam] - np.outer(nrm[seam] @ n_axis, n_axis)
        ok = np.linalg.norm(flat, axis=1) > 0.1
        nrm[np.where(seam)[0][ok]] = flat[ok]
        nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    mir = {}
    for k, v in data.items():
        w = v.copy()
        if k == "POSITION":
            w -= 2 * np.outer(v @ n_axis - mid, n_axis)
        elif k == "NORMAL":
            w -= 2 * np.outer(v @ n_axis, n_axis)
        mir[k] = w
    M = len(pos)
    full = {k: np.vstack([data[k], mir[k]]) for k in names}
    T = np.vstack([T, T[:, [0, 2, 1]] + M])
    return full, T, int(seam.sum()), len(new)


# ---------------------------------------------------------------- kovadaki delikler

def see_through(P, T, fr):
    """Ustten bakinca mesh'in izdusumunde kapali bosluklar (delikler): [(merkez, yaricap)] (m)."""
    s, l = P @ fr.fwd, P @ fr.right
    s0, l0 = s.min() - 2 * CELL, l.min() - 2 * CELL
    ni, nj = int((s.max() - s0) / CELL) + 3, int((l.max() - l0) / CELL) + 3
    cover = np.zeros((ni, nj), bool)
    A = np.stack([s, l], 1)[T]
    for a, b, c in A:
        lo, hi = np.minimum(np.minimum(a, b), c), np.maximum(np.maximum(a, b), c)
        i0, i1 = int(np.ceil((lo[0] - s0) / CELL - 0.5)), int(np.floor((hi[0] - s0) / CELL - 0.5))
        j0, j1 = int(np.ceil((lo[1] - l0) / CELL - 0.5)), int(np.floor((hi[1] - l0) / CELL - 0.5))
        if i1 < i0 or j1 < j0:
            continue
        gi, gj = np.meshgrid(np.arange(i0, i1 + 1), np.arange(j0, j1 + 1), indexing="ij")
        px, py = s0 + (gi + 0.5) * CELL, l0 + (gj + 0.5) * CELL
        v0, v1 = b - a, c - a
        den = v0[0] * v1[1] - v0[1] * v1[0]
        if abs(den) < 1e-12:
            continue
        wx, wy = px - a[0], py - a[1]
        u = (wx * v1[1] - wy * v1[0]) / den
        v = (v0[0] * wy - v0[1] * wx) / den
        cover[gi, gj] |= (u >= -1e-6) & (v >= -1e-6) & (u + v <= 1 + 1e-6)
    # kenardan erisilen bosluk disaridir; geri kalan kapali bosluklar delik
    outside = np.zeros_like(cover)
    stack = [(i, j) for i in range(ni) for j in (0, nj - 1)] + [(i, j) for i in (0, ni - 1) for j in range(nj)]
    while stack:
        i, j = stack.pop()
        if 0 <= i < ni and 0 <= j < nj and not cover[i, j] and not outside[i, j]:
            outside[i, j] = True
            stack += [(i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)]
    hole = ~cover & ~outside
    seen, out = np.zeros_like(hole), []
    for i, j in zip(*np.nonzero(hole)):
        if seen[i, j]:
            continue
        comp, stack = [], [(i, j)]
        while stack:
            a, b = stack.pop()
            if 0 <= a < ni and 0 <= b < nj and hole[a, b] and not seen[a, b]:
                seen[a, b] = True
                comp.append((a, b))
                stack += [(a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1)]
        g = np.array(comp, float)
        cs, cl = s0 + (g[:, 0] + 0.5) * CELL, l0 + (g[:, 1] + 0.5) * CELL
        c2 = np.array([cs.mean(), cl.mean()])
        r = float(np.sqrt(((np.stack([cs, cl], 1) - c2) ** 2).sum(1)).max()) + 0.7 * CELL
        out.append((c2, r, len(comp)))
    return out


def surface_gap(pp, pn, P, T, reach=0.03):
    """Her p noktasindan kendi normali n boyunca (+-reach) ayni yone bakan en yakin ucgene uzaklik: p yuzeyin
    disindaysa +, icindeyse -. Yuzey yoksa (deligin ustu) nan."""
    A, B, C = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    fn = np.cross(B - A, C - A)
    fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)
    cen = (A + B + C) / 3
    out = np.full(len(pp), np.nan)
    for i, (p, n) in enumerate(zip(pp, pn)):
        k = np.nonzero((np.linalg.norm(cen - p, axis=1) < reach + 0.05) & (fn @ n > 0.3))[0]
        if not len(k):
            continue
        e1, e2 = B[k] - A[k], C[k] - A[k]
        h = np.cross(n, e2)
        a = (e1 * h).sum(1)
        ok = np.abs(a) > 1e-12
        f = np.where(ok, 1.0 / np.where(ok, a, 1.0), 0.0)
        sv = p - A[k]
        u = f * (sv * h).sum(1)
        q = np.cross(sv, e1)
        v = f * (q @ n)
        t = f * (e2 * q).sum(1)
        hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (np.abs(t) <= reach)
        if hit.any():
            out[i] = -t[hit][np.argmin(np.abs(t[hit]))]
    return out


def patch_holes(full, T, orig, otri, fr, n_axis, mid, keep_sign):
    """Aynalanmis mesh'te ustten bakinca gorunen her delige, kovanin atilan yarisinin ayni yerdeki (delik olmayan)
    ozgun yuzeyini yama olarak koyar: atilan yaridaki delige oldugu gibi, tutulan yaridakine aynalanarak. Yama gercek
    duvarin bicimini ve dokusunu tasir; kendi normali boyunca yuzeyin biraz disina kaldirilir (ust uste binme titremesi
    olmasin). Mesh'in kendisine dokunmaz. Doner: (full, T, yamalar)."""
    P, N = full["POSITION"], full.get("NORMAL")
    req(N is not None, "normal yok: yama yonu bilinemez")
    OP, ON = orig["POSITION"].astype(np.float64), orig["NORMAL"].astype(np.float64)
    holes = see_through(P, T, fr)
    req(all(n * CELL * CELL <= HOLE_MAX for *_, n in holes), f"cok buyuk delik: {[round(n * CELL * CELL, 4) for *_, n in holes]} m2")
    old_holes = see_through(OP, otri, fr)
    od = (OP @ n_axis - mid) * keep_sign
    os_, ol = OP @ fr.fwd, OP @ fr.right
    add = {k: [] for k in full}
    tris, done = [], []
    for c2, r, n in holes:
        on_keep = (c2[1] - mid) * keep_sign >= 0
        src = np.array([c2[0], 2 * mid - c2[1] if on_keep else c2[1]])
        req(not any(np.hypot(*(h[0] - src)) < h[1] + r for h in old_holes),
            f"delik (ileri {c2[0]:+.2f}, yan {c2[1]:+.2f}): ozgun mesh'te karsi yerde de delik var")
        inn = (np.hypot(os_ - src[0], ol - src[1]) < r + PATCH_PAD) & (od < 0)
        tri = otri[inn[otri].all(1)]
        req(len(tri) >= 4, f"delik (ileri {c2[0]:+.2f}, yan {c2[1]:+.2f}): karsi yarida yama yuzeyi yok")
        used, inv = np.unique(tri.ravel(), return_inverse=True)
        tri = inv.reshape(-1, 3)
        part = {k: orig[k][used].astype(np.float64) for k in full}
        if on_keep:
            part["POSITION"] = part["POSITION"] - 2 * np.outer(part["POSITION"] @ n_axis - mid, n_axis)
            part["NORMAL"] = part["NORMAL"] - 2 * np.outer(part["NORMAL"] @ n_axis, n_axis)
            tri = tri[:, [0, 2, 1]]
        # Yamayi yuzeye oturt: iki yari birkac mm farkli (kova tam simetrik degil). Her yama kosesi kendi normali
        # boyunca ayni yone bakan mesh yuzeyine PATCH_LIFT mesafeye cekilir; deligin ustunde kalan (yuzey bulamayan)
        # koseler en yakin oturan koselerin kaymasini alir.
        pp, pn = part["POSITION"], part["NORMAL"]
        gap = surface_gap(pp, pn, P, T)
        sn = np.isfinite(gap) & (np.abs(gap) < PATCH_SNAP)
        req(sn.mean() >= 0.5, f"delik (ileri {c2[0]:+.2f}, yan {c2[1]:+.2f}): yamanin yalniz {sn.mean():.0%} kadari yuzeye oturuyor")
        shift = np.zeros_like(pp)
        shift[sn] = pn[sn] * (PATCH_LIFT - gap[sn])[:, None]
        for i in np.nonzero(~sn)[0]:
            w = 1.0 / np.maximum(np.linalg.norm(pp[sn] - pp[i], axis=1), 1e-3) ** 2
            shift[i] = (shift[sn] * w[:, None]).sum(0) / w.sum()
        part["POSITION"] = pp + shift
        g = gap[sn]
        base = len(P) + len(add["POSITION"])
        for k in full:
            add[k] += list(part[k])
        tris.append(tri + base)
        done.append(f"{'aynalanmis' if on_keep else 'ozgun'} yama (ileri {c2[0]:+.2f}, yan {c2[1] - mid:+.2f}, delik ~{2 * r:.2f} m, "
                    f"{len(tri)} ucgen, yuzeye oturan kose %{sn.mean() * 100:.0f}, iki yari farki {g.min() * 1000:+.1f}..{g.max() * 1000:+.1f} mm)")
    if not tris:
        return full, T, done
    full = {k: np.vstack([v, np.array(add[k])]) for k, v in full.items()}
    return full, np.vstack([T] + tris), done


# ---------------------------------------------------------------- ana is

def fix(path, check=False):
    """Saplari arkaya simetrik getirir. Doner: (degisti mi, aciklama). Kurala uymazsa Fail."""
    gl, bin_, rest = load(path)
    fr = Frame(gl, bin_)
    # saplari tasiyan mesh: arkadaki en uc ust kose onun
    best = None
    for name, mi, M, W in fr.parts:
        cl, _ = fr.handles(W)
        if cl:
            s = (W @ fr.fwd)[(W[:, 1] > fr.lo[1] + UPPER * fr.H)].min()
            if best is None or s < best[0]:
                best = (s, name, mi, M, W)
    req(best is not None, "arkada sap ucu yok")
    _, name, mi, M, W = best
    cl, front = fr.handles(W)
    mid = fr.tub_mid(W)
    desc = lambda c: ", ".join(f"{m - mid:+.2f}" for m, *_ in c) or "-"
    if symmetric(cl, mid):
        return False, f"{name}: arkada zaten simetrik iki sap (yan {desc(cl)} m); dokunulmadi"
    req(len(cl) == 1, f"{name}: arkada {len(cl)} sap ucu (yan {desc(cl)} m); kural tek arka sap icin")
    req(np.allclose(M, np.eye(4), atol=1e-6), f"{name}: dugumunde donusum var (kural kok uzayda calisir)")
    keep = 1.0 if cl[0][0] - mid > 0 else -1.0
    req(abs(cl[0][0] - mid) > SPAN[0] / 2, f"{name}: arka sap ortada ({cl[0][0] - mid:+.2f} m)")
    before = f"once arkada yan {desc(cl)} m, onde ust uclar yan {desc(front)} m"
    if check:
        return False, f"{name}: {before}; duzeltilecek (yalniz olcum)"
    mesh = gl["meshes"][mi]
    total_new = 0
    for p in mesh["primitives"]:
        req(p.get("mode", 4) == 4 and "indices" in p and "targets" not in p, f"{name}: ucgen listesi degil")
        req(set(p["attributes"]) <= ATTR_OK, f"{name}: desteklenmeyen oznitelik {sorted(set(p['attributes']) - ATTR_OK)}")
        for k, i in p["attributes"].items():
            a = gl["accessors"][i]
            req(a["componentType"] == 5126 and not a.get("normalized"), f"{name}: {k} float degil")
        attrs = {k: read_acc(gl, bin_, i) for k, i in p["attributes"].items()}
        tri = read_acc(gl, bin_, p["indices"]).astype(np.int64).reshape(-1, 3)
        full, T, seam, cut = clip_mirror(attrs, tri, fr.right, mid, keep)
        # Kovanin on duvarinda Tripo'dan gelen bir delik var (catalin ucu icinden gecer); aynalama onu obur yana da
        # tasir ve orada delikten yer gorunur. Iki delik de karsi yarinin ozgun (deliksiz) duvariyla yamanir.
        full, T, plugs = patch_holes(full, T, attrs, tri, fr, fr.right, mid, keep)
        req(not see_through(full["POSITION"], T, fr), f"{name}: yamadan sonra tabanda hala delik var")
        total_new += len(T)
        for k, i in list(p["attributes"].items()):
            arr = full[k].astype(np.float32)
            a, ai = append_acc(gl, bin_, arr, gl["accessors"][i], 34962)
            if k == "POSITION":
                a["min"] = [float(v) for v in arr.min(0)]
                a["max"] = [float(v) for v in arr.max(0)]
            p["attributes"][k] = ai
        idx = T.ravel()
        it = (np.uint16, 5123) if idx.max() < 65535 else (np.uint32, 5125)
        a, ai = append_acc(gl, bin_, idx.astype(it[0]).reshape(-1, 1), gl["accessors"][p["indices"]], 34963)
        a["componentType"] = it[1]
        a["count"] = int(idx.size)
        p["indices"] = ai
    # dogrula: yeni geometride arkada simetrik iki sap, onde sap yok
    W2 = np.vstack([read_acc(gl, bin_, p["attributes"]["POSITION"]).astype(float) for p in mesh["primitives"]])
    cl2, front2 = fr.handles(W2)
    req(symmetric(cl2, mid), f"{name}: duzeltmeden sonra arkada simetrik sap cifti yok (yan {desc(cl2)} m)")
    save(path, gl, bin_, rest)
    return True, (f"{name}: {before}; simdi arkada yan {desc(cl2)} m, kova {name} {'+' if keep > 0 else '-'}yan yarisindan "
                  f"aynalandi ({total_new} ucgen, dikiste {seam} kose, {cut} ucgen kesildi); delik yamalari: "
                  f"{'; '.join(plugs) or 'yok'}; ileri uzunluk "
                  f"{(W @ fr.fwd).max() - (W @ fr.fwd).min():.3f} -> {(W2 @ fr.fwd).max() - (W2 @ fr.fwd).min():.3f} m")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    check = "--check" in argv
    paths = [a for a in argv if not a.startswith("--")] or [DEFAULT]
    bad = 0
    for p in paths:
        try:
            changed, msg = fix(p, check)
            print(f"CARTFIX ok {os.path.basename(p)} {'duzeltildi' if changed else 'degismedi'}: {msg}", flush=True)
        except Fail as e:
            bad += 1
            print(f"CARTFIX fail {os.path.basename(p)}: {e}", flush=True)
    sys.stdout.flush()
    os._exit(1 if bad else 0)


if __name__ == "__main__":
    main()
