"""
Kredisiz PBR yukseltmesi (gorsel-model akisi, 18 Kasim 2026).

Tripo API modelleri yalniz renk dokusuyla geliyor (roughness 0.9, metal 0): her yuzey ayni tebeşir/plastik gibi isik aliyor.
Bu betik her modelin KENDI UV'sine Cycles ile pisirir ve renk dokusundan malzeme haritalari cikarir:
  - AO (yere temas dahil), kenar (Pointiness), yukseklik (nesne Z'si), kapsama maskesi
  - normal haritasi: renk dokusunun ince/orta bantlari + malzeme sinifina gore tane (OpenGL, +Y)
  - ORM: R = AO, G = roughness, B = metalness (renk kurallariyla sinif: boya, metal, tas, ahsap, bitki, lastik ...)
  - albedo: doygunluk/kontrast ayari, AO ve oyuk kirliligi, kenar asinmasi (boya kenarinda metal cikar), alttan toprak, leke
GLB'ye dokunulmaz. Ciktilar game/assets/models/<ad>/pbr/ altina PNG; oyun game/data/materials.json uzerinden
malzeme adiyla eslestirip yuklenen malzemeye takar (game/scripts/fx/MaterialUplift.cs).

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/pbr_bake.py -- [--only kapsul,ocak] [--samples 24] [--preview progress/gorsel/model/pbr]
Ayarlar: pipeline/pbr_manifest.json. Sonunda game/data/materials.json yeniden yazilir (manifestteki butun varliklar).
"""
import bpy
import json
import math
import os
import sys
import tempfile
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MANIFEST = os.path.join(HERE, "pbr_manifest.json")
MATERIALS_JSON = os.path.join(ROOT, "game", "data", "materials.json")

if os.environ.get("KIT_TMP"):
    os.makedirs(os.environ["KIT_TMP"], exist_ok=True)
    tempfile.tempdir = os.environ["KIT_TMP"]


def argv():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = {"only": None, "samples": 24, "preview": None}
    i = 0
    while i < len(a):
        if a[i] == "--only":
            out["only"] = set(a[i + 1].split(",")); i += 2
        elif a[i] == "--samples":
            out["samples"] = int(a[i + 1]); i += 2
        elif a[i] == "--preview":
            out["preview"] = a[i + 1]; i += 2
        else:
            raise SystemExit(f"bilinmeyen secenek {a[i]}")
    return out


# ---------------------------------------------------------------- numpy yardimcilari

def box_blur(x, r, axis):
    if r < 1:
        return x
    pad = [(0, 0)] * x.ndim
    pad[axis] = (r + 1, r)
    p = np.pad(x, pad, mode="edge")
    c = np.cumsum(p, axis=axis, dtype=np.float64)
    n = x.shape[axis]
    hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
    lo = np.take(c, np.arange(0, n), axis=axis)
    return ((hi - lo) / (2 * r + 1)).astype(np.float32)


def blur(x, sigma):
    """Uc kutu gecisiyle Gauss yaklasimi (scipy yok)."""
    if sigma <= 0.3:
        return x
    r = max(1, int(round(math.sqrt(12 * sigma * sigma / 3 + 1) - 1) // 2))
    for _ in range(3):
        x = box_blur(x, r, 0)
        x = box_blur(x, r, 1)
    return x


def masked_blur(x, m, sigma):
    """Ada disina tasmayan bulanıklastirma: bos piksel ortalamaya katilmaz."""
    if x.ndim == 3:
        mm = m[..., None]
        return blur(x * mm, sigma) / np.maximum(blur(mm.repeat(x.shape[2], 2), sigma), 1e-4)
    return blur(x * m, sigma) / np.maximum(blur(m, sigma), 1e-4)


def sstep(a, b, x):
    t = np.clip((x - a) / max(1e-6, b - a), 0, 1)
    return t * t * (3 - 2 * t)


def noise(h, w, sigma, rng):
    n = blur(rng.standard_normal((h, w)).astype(np.float32), sigma)
    s = n.std()
    return n / s if s > 0 else n


def rgb_to_hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(-1); mn = a.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    nz = d > 1e-5
    rc = np.where(nz & (mx == r), ((g - b) / np.where(nz, d, 1)) % 6, 0)
    gc = np.where(nz & (mx == g) & (mx != r), (b - r) / np.where(nz, d, 1) + 2, 0)
    bc = np.where(nz & (mx == b) & (mx != r) & (mx != g), (r - g) / np.where(nz, d, 1) + 4, 0)
    h = (rc + gc + bc) * 60.0
    s = np.where(mx > 1e-5, d / np.maximum(mx, 1e-5), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    c = v * s
    hp = (h % 360) / 60.0
    x = c * (1 - np.abs(hp % 2 - 1))
    z = np.zeros_like(h)
    conds = [(hp < 1), (hp < 2), (hp < 3), (hp < 4), (hp < 5), (hp >= 5)]
    rs = [c, x, z, z, x, c]; gs = [x, c, c, x, z, z]; bs = [z, z, x, c, c, x]
    r = np.select(conds, rs); g = np.select(conds, gs); b = np.select(conds, bs)
    m = v - c
    return np.stack([r + m, g + m, b + m], -1)


def soft_range(x, lo, hi, soft):
    return sstep(lo - soft, lo + soft, x) * (1 - sstep(hi - soft, hi + soft, x))


def hue_range(h, lo, hi, soft=8.0):
    if lo <= hi:
        return soft_range(h, lo, hi, soft)
    return np.maximum(soft_range(h, lo, 360 + soft, soft), soft_range(h, -soft, hi, soft))


# ---------------------------------------------------------------- Blender: pisirme

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    if not sc.world:
        sc.world = bpy.data.worlds.new("W")
    return sc


def new_image(name, w, h, data=False):
    img = bpy.data.images.new(name, w, h, alpha=False, float_buffer=True)
    img.colorspace_settings.name = "Non-Color"
    return img


def pixels(img):
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def save_png(arr, path, srgb):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "sRGB" if srgb else "Non-Color"
    rgba = np.ones((h, w, 4), np.float32)
    rgba[..., :3] = np.clip(arr[..., :3], 0, 1)
    img.pixels.foreach_set(rgba.ravel())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    write_import(path, normal=path.endswith("_normal.png"))


def write_import(path, normal):
    """Godot ice aktarma ayari: koddan takilan doku da VRAM sikistirmali ve mip'li olsun (normal haritasi RGTC).
    Dosya varsa dokunulmaz (Godot uid ve hedefleri kendisi ekler)."""
    imp = path + ".import"
    if os.path.exists(imp):
        return
    with open(imp, "w", encoding="utf-8", newline="\n") as f:
        f.write('[remap]\n\nimporter="texture"\ntype="CompressedTexture2D"\n\n[params]\n\n'
                f"compress/mode=2\ncompress/normal_map={1 if normal else 2}\nmipmaps/generate=true\ndetect_3d/compress_to=0\n")


def bake_pass(objs, mats, targets, kind, emit_builder=None, margin=8, samples=24):
    """targets: malzeme adi -> hedef imge. kind: AO ya da EMIT (emit_builder(nt) yuzey cikisini kurar)."""
    sc = bpy.context.scene
    restore = []
    for m in mats:
        nt = m.node_tree
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = targets[m.name]
        node.interpolation = "Closest"
        nt.nodes.active = node
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL" and n.is_active_output)
        old = [l.from_socket for l in out.inputs["Surface"].links]
        tmp = []
        if emit_builder is not None:
            em = nt.nodes.new("ShaderNodeEmission")
            src, extra = emit_builder(nt)
            nt.links.new(src, em.inputs["Color"])
            nt.links.new(em.outputs[0], out.inputs["Surface"])
            tmp = [em] + extra
        restore.append((nt, node, out, old, tmp))
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    sc.cycles.samples = samples
    sc.render.bake.margin = margin
    sc.render.bake.margin_type = "EXTEND"
    sc.render.bake.use_clear = True
    bpy.ops.object.bake(type=kind, margin=margin, use_clear=True)
    for nt, node, out, old, tmp in restore:
        for n in tmp:
            nt.nodes.remove(n)
        if emit_builder is not None and old:
            nt.links.new(old[0], out.inputs["Surface"])
        nt.nodes.remove(node)


def cull_enclosed(objs, size, threshold):
    """Ici bos kabuklarda (kapsul) ic yuzler dis yuzlerle ayni UV dokusunu paylasabiliyor: kapali ic yuzun AO'su,
    kenari ve kabartmasi dis yuzun ustune yazilir. Yarim kureye 16 isin; disari kacan payi esigin altindaki yuz
    pisirmeden once silinir (yalniz pisirme kopyasinda; GLB'ye dokunulmaz)."""
    if threshold <= 0:
        return
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    verts, polys, owner = [], [], []
    for o in objs:
        base = len(verts)
        mw = o.matrix_world
        verts += [mw @ v.co for v in o.data.vertices]
        for p in o.data.polygons:
            polys.append([base + i for i in p.vertices])
            owner.append((o, p.index))
    tree = BVHTree.FromPolygons(verts, polys)
    rng = np.random.default_rng(7)
    dirs = rng.standard_normal((16, 3))
    dirs /= np.linalg.norm(dirs, axis=1, keepdims=True)
    kill = {}
    openness = {}
    for (o, pi) in owner:
        p = o.data.polygons[pi]
        mw = o.matrix_world
        n = (mw.to_3x3() @ p.normal).normalized()
        c = mw @ p.center
        free = 0
        for d in dirs:
            v = Vector(d)
            if v.dot(n) < 0:
                v = -v
            v = (v + n * 0.3).normalized()
            hit = tree.ray_cast(c + n * size * 1e-3, v, size * 3)
            if hit[0] is None:
                free += 1
        openness[(o.name, pi)] = free / len(dirs)
        if free / len(dirs) < threshold:
            kill.setdefault(o, []).append(pi)
    # UV cakismasi: ayni malzemenin her UV pikselini en acikta duran yuz alir; payinin yarisindan cogunu
    # kaybeden yuz pisirmeden cikar (ic kabin tabani dis govdenin dokusunu ezmesin).
    G = 384
    for mat in {s.material for o in objs for s in o.material_slots if s.material}:
        owner_img = np.full((G, G), -1, np.int64)
        best = np.full((G, G), -1.0, np.float32)
        faces = []
        for o in objs:
            if not o.data.uv_layers:
                continue
            uv = o.data.uv_layers.active.data
            for p in o.data.polygons:
                if o.material_slots[p.material_index].material != mat:
                    continue
                pts = np.array([uv[li].uv[:] for li in p.loop_indices], np.float64) * G
                faces.append((o, p.index, pts, openness.get((o.name, p.index), 1.0)))
        cover = []
        for fi, (o, pi, pts, op) in enumerate(faces):
            x0, y0 = np.floor(pts.min(0)).astype(int); x1, y1 = np.ceil(pts.max(0)).astype(int)
            x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, G), min(y1, G)
            if x1 <= x0 or y1 <= y0:
                cover.append(None); continue
            ys, xs = np.mgrid[y0:y1, x0:x1]
            P = np.stack([xs + 0.5, ys + 0.5], -1)
            inside = np.zeros(P.shape[:2], bool)
            for k in range(1, len(pts) - 1):
                a, b, c = pts[0], pts[k], pts[k + 1]
                d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
                if abs(d) < 1e-12:
                    continue
                l1 = ((b[1] - c[1]) * (P[..., 0] - c[0]) + (c[0] - b[0]) * (P[..., 1] - c[1])) / d
                l2 = ((c[1] - a[1]) * (P[..., 0] - c[0]) + (a[0] - c[0]) * (P[..., 1] - c[1])) / d
                inside |= (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
            yy, xx = ys[inside], xs[inside]
            cover.append((yy, xx))
            win = op > best[yy, xx]
            best[yy[win], xx[win]] = op
            owner_img[yy[win], xx[win]] = fi
        for fi, cv in enumerate(cover):
            if cv is None or len(cv[0]) < 3:
                continue
            kept = np.count_nonzero(owner_img[cv[0], cv[1]] == fi)
            if kept < 0.5 * len(cv[0]):
                kill.setdefault(faces[fi][0], []).append(faces[fi][1])
    for o in kill:
        kill[o] = sorted(set(kill[o]))
    total = sum(len(o.data.polygons) for o in objs)
    removed = 0
    for o, idx in kill.items():
        bm = bmesh_from(o)
        bm.faces.ensure_lookup_table()
        faces = [bm.faces[i] for i in idx]
        removed += len(faces)
        import bmesh
        bmesh.ops.delete(bm, geom=faces, context="FACES")
        bm.to_mesh(o.data)
        bm.free()
    print(f"PBR kapali ic yuz: {removed}/{total} silindi (pisirme kopyasi)", flush=True)


def bmesh_from(o):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(o.data)
    return bm


def bake_asset(cfg, opts):
    sc = reset()
    glb = os.path.join(ROOT, cfg["glb"])
    bpy.ops.import_scene.gltf(filepath=glb)
    objs = [o for o in bpy.data.objects if o.type == "MESH"]
    # glTF dugum duzeni: bos ebeveynler olabilir; dunya koordinatlarinda kutu
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    zs = [p.z for p in pts]
    xs = [p.x for p in pts]; ys = [p.y for p in pts]
    zmin, zmax = min(zs), max(zs)
    size = max(max(xs) - min(xs), max(ys) - min(ys), zmax - zmin)
    sc.world.light_settings.distance = cfg.get("aoDistance", 0.35) * size
    cull_enclosed(objs, size, cfg.get("enclosed", 0.12))
    if cfg.get("ground", True):
        bpy.ops.mesh.primitive_plane_add(size=size * 6, location=((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, zmin))
    mats, src = [], {}
    for o in objs:
        for s in o.material_slots:
            m = s.material
            if m is None or m in mats:
                continue
            img = next((n.image for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image and
                        any(l.to_socket.name == "Base Color" for l in n.outputs[0].links)), None)
            if img is None:
                print(f"PBR {cfg['id']}: {m.name} renk dokusu yok, atlandi")
                continue
            mats.append(m)
            src[m.name] = (img, o.name)
    if not mats:
        raise RuntimeError(f"{cfg['id']}: dokulu malzeme yok")

    def targets(tag):
        return {m.name: new_image(f"{tag}_{m.name}", min(1024, src[m.name][0].size[0]), min(1024, src[m.name][0].size[1])) for m in mats}

    t0 = time.time()
    ao = targets("ao")
    bake_pass(objs, mats, ao, "AO", samples=opts["samples"])

    def pointiness(nt):
        g = nt.nodes.new("ShaderNodeNewGeometry")
        return g.outputs["Pointiness"], [g]
    edge = targets("edge")
    bake_pass(objs, mats, edge, "EMIT", pointiness, samples=1)

    def height(nt):
        g = nt.nodes.new("ShaderNodeNewGeometry")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        mr = nt.nodes.new("ShaderNodeMapRange")
        mr.inputs["From Min"].default_value = zmin
        mr.inputs["From Max"].default_value = zmax
        nt.links.new(g.outputs["Position"], sep.inputs[0])
        nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
        return mr.outputs["Result"], [g, sep, mr]
    hgt = targets("hgt")
    bake_pass(objs, mats, hgt, "EMIT", height, samples=1)

    def white(nt):
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (1, 1, 1, 1)
        return rgb.outputs[0], [rgb]
    cover = targets("cov")
    bake_pass(objs, mats, cover, "EMIT", white, margin=0, samples=1)
    print(f"PBR {cfg['id']}: pisirme {time.time() - t0:.1f} sn ({len(mats)} malzeme)", flush=True)

    out_dir = os.path.join(ROOT, os.path.dirname(cfg["glb"]), "pbr")
    entries = {}
    for i, m in enumerate(mats):
        img, obj_name = src[m.name]
        part = cfg["id"] if len(mats) == 1 else f"{cfg['id']}_{obj_name}"
        maps = process(cfg, img, ao[m.name], edge[m.name], hgt[m.name], cover[m.name], seed=hash(part) & 0xFFFF)
        rel = os.path.dirname(cfg["glb"]).replace("\\", "/")
        res = "res://" + rel[len("game/"):] + "/pbr/"
        keep = cfg.get("keepMaps", False)  # kendi normal+rm'si olan model (Tripo web): yalniz albedo derecelendirilir
        for k, (arr, srgb) in maps.items():
            if keep and k != "albedo":
                continue
            save_png(arr, os.path.join(out_dir, f"{part}_{k}.png"), srgb)
        entries[m.name] = {"albedo": res + f"{part}_albedo.png"} if keep else \
            {"albedo": res + f"{part}_albedo.png", "normal": res + f"{part}_normal.png", "orm": res + f"{part}_orm.png"}
        if opts["preview"]:
            prev = np.concatenate([maps["albedo"][0], maps["normal"][0], maps["orm"][0]], axis=1)
            save_png(prev, os.path.join(ROOT, opts["preview"], f"{part}.png"), True)
    return entries


# ---------------------------------------------------------------- haritalar

def process(cfg, albedo_img, ao_img, edge_img, hgt_img, cov_img, seed):
    classes = MAN["classes"]
    A0 = pixels(albedo_img)[..., :3].copy()
    h, w = ao_img.size[1], ao_img.size[0]
    if A0.shape[0] != h:
        # renk dokusu 1024'ten buyukse kucult (nokta ornekleme yeter: yalnız 2048 -> 1024)
        f = A0.shape[0] // h
        A0 = A0[::f, ::f]
    AO = pixels(ao_img)[..., 0]
    P = pixels(edge_img)[..., 0]
    Y = pixels(hgt_img)[..., 0]
    M = (pixels(cov_img)[..., 0] > 0.5).astype(np.float32)
    rng = np.random.default_rng(seed)
    px = w / 1024.0  # bantlar 1K'ye gore

    # --- sinif agirliklari (renk kurallari, hafif bulanik renk uzerinde)
    Ab = masked_blur(A0, M, 1.5 * px)
    H, S, V = rgb_to_hsv(Ab)
    base = cfg.get("class", "paint")
    names = list(classes.keys())
    W = {n: np.zeros((h, w), np.float32) for n in names}
    rest = np.ones((h, w), np.float32)
    for rule in cfg.get("rules", []):
        m = np.ones((h, w), np.float32)
        if "hue" in rule:
            m *= hue_range(H, rule["hue"][0], rule["hue"][1])
        if "sat" in rule:
            m *= soft_range(S, rule["sat"][0], rule["sat"][1], 0.04)
        if "val" in rule:
            m *= soft_range(V, rule["val"][0], rule["val"][1], 0.04)
        if "y" in rule:
            m *= soft_range(Y, rule["y"][0], rule["y"][1], 0.03)
        take = m * rest
        W[rule["class"]] += take
        rest -= take
    W[base] += rest
    for n in names:
        W[n] = masked_blur(W[n], M, 1.0 * px) if W[n].any() else W[n]

    def mix(key, default=0.0):
        return sum(W[n] * classes[n].get(key, default) for n in names)

    # --- gurultuler
    n_fine = noise(h, w, 0.8 * px, rng)
    n_mid = noise(h, w, 4 * px, rng)
    n_big = noise(h, w, 22 * px, rng)
    breakup = sstep(-0.3, 0.9, n_mid * 0.6 + n_big * 0.7)

    # --- kenar ve oyuk (Pointiness 0.5 duz; ust disbukey, alt icbukey)
    Pm = masked_blur(P, M, 1.0 * px)
    convex = sstep(0.5 + cfg.get("edgeLo", 0.012), 0.5 + cfg.get("edgeHi", 0.05), Pm)
    concave = sstep(0.5 - cfg.get("edgeLo", 0.012), 0.5 - cfg.get("edgeHi", 0.05) * 1.4, Pm)

    # --- yukseklik ve normal
    L = (A0 @ np.array([0.2126, 0.7152, 0.0722], np.float32))
    Lf = masked_blur(L, M, 0.7 * px)
    fine = Lf - masked_blur(L, M, 3 * px)
    mid = masked_blur(L, M, 3 * px) - masked_blur(L, M, 14 * px)
    grain = mix("grain")
    relief = cfg.get("relief", 1.0)
    Hm = relief * (fine * 2.2 + mid * 1.2) * mix("relief", 1.0) + grain * (n_fine * 0.035 + n_mid * 0.02)
    Hm += -concave * 0.04 * relief
    Hm = np.where(M > 0, Hm, masked_blur(Hm, M, 3 * px))
    strength = cfg.get("normal", 6.0) * (w / 1024.0)
    gx = (np.roll(Hm, -1, 1) - np.roll(Hm, 1, 1)) * 0.5 * strength
    gy = (np.roll(Hm, -1, 0) - np.roll(Hm, 1, 0)) * 0.5 * strength
    inner = sstep(0.55, 0.95, blur(M, 1.2 * px))
    gx *= inner; gy *= inner
    N = np.stack([-gx, -gy, np.ones_like(gx)], -1)
    N /= np.linalg.norm(N, axis=-1, keepdims=True)
    normal = N * 0.5 + 0.5

    # --- albedo derecelendirme
    Hh, Ss, Vv = rgb_to_hsv(A0)
    Ss = np.clip(Ss * cfg.get("sat", 0.85) * mix("sat", 1.0), 0, 1)
    c = cfg.get("contrast", 1.06)
    Vv = np.clip(0.5 + (Vv - 0.5) * c, 0, 1) * cfg.get("value", 1.0)
    A = hsv_to_rgb(Hh, Ss, Vv)
    aoS = cfg.get("ao", 0.55)
    AOs = masked_blur(AO, M, 0.8 * px)
    A *= (1 - aoS + aoS * AOs)[..., None]
    cav = np.clip(concave * 0.8 + np.clip(-fine * 6, 0, 1) * 0.5, 0, 1)
    A *= (1 - cfg.get("cavity", 0.35) * cav)[..., None]
    # kenar asinmasi: boyada metal cikar, digerlerinde acilir
    wear = convex * breakup * cfg.get("edge", 0.45)
    wear_metal = wear * W.get("paint", 0) * cfg.get("paintChips", 1.0)
    steel = np.array(MAN.get("steel", [0.56, 0.55, 0.53]), np.float32)
    A = A * (1 - wear_metal[..., None]) + steel * wear_metal[..., None]
    lift = wear * (1 - W.get("paint", 0))
    A = A * (1 + 0.28 * lift[..., None])
    # alttan toprak ve leke
    dirt = cfg.get("dirt", 0.0)
    dh = cfg.get("dirtHeight", 0.18)
    dcol = np.array(cfg.get("dirtColor", MAN.get("dirtColor", [0.33, 0.27, 0.2])), np.float32)
    d = (1 - sstep(dh * 0.25, dh, Y + n_mid * 0.02)) * sstep(-0.6, 0.6, n_mid * 0.7 + n_big * 0.5) * dirt
    d = np.clip(d + cav * dirt * 0.25, 0, 1)
    A = A * (1 - d[..., None]) + dcol * d[..., None]
    grime = cfg.get("grime", 0.12)
    A *= (1 - grime * sstep(-0.2, 1.4, n_big))[..., None]
    A *= (1 + n_fine * 0.018 * grain)[..., None]
    A = np.clip(A, 0.025, 0.93)

    # --- roughness ve metal
    R = mix("rough", 0.7) + mix("roughVar", 0.1) * (n_mid * 0.5 + n_big * 0.5) * 0.5
    R += cav * 0.08 + d * 0.25 - wear_metal * 0.15 + grime * 0.25 * sstep(-0.2, 1.4, n_big)
    R = np.clip(R, 0.08, 1.0)
    Mt = mix("metal", 0.0) * (1 - d) + wear_metal * 0.85
    Mt = np.clip(Mt, 0, 1)
    orm = np.stack([np.clip(AOs * 0.85 + 0.15, 0, 1), R, Mt], -1)

    # ada disini doldur (mip/filtre tasmasi)
    fillA = masked_blur(A, M, 4 * px)
    A = np.where(M[..., None] > 0, A, fillA)
    orm = np.where(M[..., None] > 0, orm, masked_blur(orm, M, 4 * px))
    return {"albedo": (A, True), "normal": (normal, False), "orm": (orm, False)}


# ---------------------------------------------------------------- ana akis

def main():
    global MAN
    opts = argv()
    with open(MANIFEST, encoding="utf-8") as f:
        MAN = json.load(f)
    old = {}
    if os.path.exists(MATERIALS_JSON):
        with open(MATERIALS_JSON, encoding="utf-8") as f:
            old = json.load(f).get("models", {})
    models = {}
    for cfg in MAN["assets"]:
        res = "res://" + cfg["glb"][len("game/"):]
        if opts["only"] and cfg["id"] not in opts["only"]:
            if res in old:
                models[res] = old[res]
            continue
        t = time.time()
        surfaces = bake_asset(cfg, opts)
        models[res] = {"id": cfg["id"], "normalScale": cfg.get("normalScale", 1.0), "surfaces": surfaces}
        print(f"PBR ok {cfg['id']} {time.time() - t:.1f} sn", flush=True)
    doc = {
        "_not": "Kredisiz PBR yukseltmesi (pipeline/pbr_bake.py yazar; elle duzenleme pbr_manifest.json'da). Model yolu -> yuzey "
                "(glTF malzeme adi) -> albedo, normal (OpenGL), orm (R ao, G roughness, B metal). Oyun MaterialUplift.cs ile "
                "yuklenen malzemeye takar; dekor ruzgar ve yaratik sisme shader'lari albedo ile normali buradan devralir.",
        "models": models,
    }
    with open(MATERIALS_JSON, "w", encoding="utf-8", newline="\n") as f:
        json.dump(doc, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"PBR RESULT ok {len(models)} model -> {MATERIALS_JSON}", flush=True)


MAN = {}
if __name__ == "__main__":
    main()
