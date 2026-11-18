"""
Kabarma uretim hatti, statik varlik adimlari A.4-A.7 ve B (URETIM.md §2.2): AI modelini oyuna hazirlar.

Blender basliksiz calisir (tek tek ya da pipeline/run_golden.py ile toplu):
  blender -b --factory-startup --python-exit-code 1 -P pipeline/clean_asset.py -- \
      --in ham.glb --out game/assets/models/x/x.glb --name x --height 1.0 --tris 4000 --class esya_kaya \
      [--keep_texture] [--parts --rename tripo_part_0=body,...] [--renders progress/m1/varlik/render/x] [--card ...]

Adimlar:
  1. GLB'yi alir; parcalari (Tripo kaynak adlariyla) birlestirir. --merge kirintilari komsu parcaya katar.
  2. Kaynak: dikis koselerini kaynastirir (parca modunda parca icinde kalir; parcalar birbirine yapismaz).
  3. Sinif butcesine sadelestirir (Decimate, collapse).
  4. Onarim (olcut 4): kirik ucgen, ikiden cok yuzlu kenar (seritler silinir), delik kapama (yeni yuzlere komsu UV),
     disari bakan tutarli normaller. Yeniden denetlenir; gecene kadar en fazla --repair_passes tur.
  5. Doku kipinde dokulari --tex_size'a indirir (kipsiz eski yol: palete oturtma).
  6. Boy, taban, eksen; yumusak golgeleme, aciyla sert kenar.
  7. Denetimler (SANAT-SES §9, 1-5): doku, poligon (sinif butcesi data/assets.json), kalinlik ornekleme (isin), butunluk
     (delik, ters yuz, havada kalan parca), olcek ve eksen. Kart icin palet yonu (dokudan ornek, en yakin palet rengi).
  8. Parca modu: adli dugumler (--rename kaynak ya da otomatik ad), pivot (--pivots), soket bos dugumleri (--sockets).
  9. Goruntuler (olcut 6-8): 4 aci, 64 px siluet, ikon (seffaf, ayni isik ve aci), varlik karti.
 10. Disa aktarim: ana GLB, iki LOD GLB (dokular kucultulur), dis bukey carpisma kabugu (Godot .tres), JSON rapor.

Ciktilarin hepsi ana GLB'nin yaninda: <ad>.glb, <ad>_lod1.glb, <ad>_lod2.glb, <ad>_hull.tres, <ad>_icon.png, <ad>.report.json.
Ana GLB'ye LOD ya da carpisma dugumu konmaz (oyun kodu her MeshInstance3D'yi gorunus sayiyor).
"""
import bpy, bmesh, sys, json, math, os, argparse, random
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__))
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="inp", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--name", required=True)
ap.add_argument("--label", default="", help="kart ve stil foyu icin Turkce ad")
ap.add_argument("--height", type=float, required=True)
ap.add_argument("--tris", type=int, required=True, help="sadelestirme hedefi")
ap.add_argument("--class", dest="klass", default="", help="butce sinifi (game/data/assets.json budgets)")
ap.add_argument("--assets", default=os.path.join(HERE, "..", "game", "data", "assets.json"))
ap.add_argument("--rig", action="store_true")
ap.add_argument("--sets", default="altin_cayir,neutral,tones")
ap.add_argument("--palette", default=os.path.join(HERE, "..", "game", "data", "palette.json"))
ap.add_argument("--renders", default="", help="4 aci + 64 px siluet klasoru")
ap.add_argument("--card", default="", help="varlik karti PNG yolu")
ap.add_argument("--icon", default="", help="ikon PNG yolu (bos: <ad>_icon.png)")
ap.add_argument("--forward", default="+X", help="modelin on yonu (Blender ekseni; Tripo +X)")
ap.add_argument("--smooth_passes", type=int, default=2)
ap.add_argument("--min_region", type=int, default=0)
ap.add_argument("--lweight", type=float, default=0.45)
ap.add_argument("--keep_texture", action="store_true", help="dokulari ve malzemeleri koru (palet oturtma yok)")
ap.add_argument("--tex_size", type=int, default=1024)
ap.add_argument("--parts", action="store_true", help="makine: Tripo'nun parcalarini ayri, adli dugumler olarak koru")
ap.add_argument("--rename", default="", help="ad duzeltme: kaynak_ya_da_otomatik_ad=yeni,...")
ap.add_argument("--merge", default="", help="kirinti parcayi baskasina kat: kaynak=hedef_kaynak,...")
ap.add_argument("--drop", default="", help="havada kalan kirinti kaynak parcalari at: kaynak,...")
ap.add_argument("--pivots", default="", help="parca orijini: parca=bottom|center|top,...")
ap.add_argument("--sockets", default="", help="bos dugum: ad=parca[+parca]:center|top|bottom[:parent],...")
ap.add_argument("--exclude", default="")
ap.add_argument("--relax", type=int, default=0)
ap.add_argument("--weld", type=float, default=1e-6, help="kaynak mesafesi (en buyuk boyutun orani)")
ap.add_argument("--no_repair", action="store_true")
ap.add_argument("--repair_passes", type=int, default=8)
ap.add_argument("--drop_floating", type=float, default=0.01, help="havada kalan parca yuzeyin bu payindan kucukse silinir")
ap.add_argument("--dark_fill", type=float, default=0.0,
                help="yuzeyin bu payindan buyuk delik yamasi dokunun en koyu noktasina baglanir (acik yerine ic golge gibi okunur); "
                     "0 = kapali (varsayilan: kapsulde denendi, gorunur fark yok; gizli kesik agizlari koyulasir)")
ap.add_argument("--lods", default="0.5,0.2", help="LOD ucgen oranlari")
ap.add_argument("--lod_tex", default="512,256", help="LOD doku kenarlari")
ap.add_argument("--hull_points", type=int, default=64, help="carpisma kabugu en fazla nokta")
ap.add_argument("--thin", type=float, default=0.06, help="olcut 3: en uzun kenarin orani (SANAT-SES §1.1)")
ap.add_argument("--thin_area", type=float, default=0.02, help="olcut 3: ince bolgenin izin verilen yuzey payi")
ap.add_argument("--thick_samples", type=int, default=2000)
ap.add_argument("--icon_size", type=int, default=256)
ap.add_argument("--seed", type=int, default=7)
args = ap.parse_args(argv)
random.seed(args.seed)

report = {"name": args.name, "label": args.label or args.name, "input": args.inp, "class": args.klass,
          "forward": args.forward, "checks": {}}

def kv_list(s):
    return [tuple(x.split("=", 1)) for x in s.split(",") if "=" in x]

# ---------------- renk: sRGB -> Lab ----------------
def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def to_lab(rgb):
    r, g, b = (srgb_to_lin(x) for x in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 1.0
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    fx, fy, fz = f(x), f(y), f(z)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))

def de(a, b):
    return math.sqrt(sum((p - q) ** 2 for p, q in zip(a, b)))

pal = json.load(open(args.palette, encoding="utf-8"))
allowed = []
for s in args.sets.split(","):
    for k, hx in pal["sets"][s].items():
        if f"{s}.{k}" in args.exclude.split(","):
            continue
        rgb = tuple(int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4))
        allowed.append((f"{s}.{k}", rgb, to_lab(rgb), hx))

def de_match(a, b):
    return math.sqrt(((a[0] - b[0]) * args.lweight) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)

def nearest(rgb):
    lab = to_lab(rgb)
    best = min(range(len(allowed)), key=lambda i: de_match(lab, allowed[i][2]))
    return best, de(lab, allowed[best][2])

# Sinif butcesi (SANAT-SES §4), veriden
budget = None
if args.klass and os.path.exists(args.assets):
    B = json.load(open(args.assets, encoding="utf-8")).get("budgets", {})
    budget = B.get(args.klass)
    if budget is None:
        raise SystemExit(f"bilinmeyen butce sinifi: {args.klass}")

# ---------------- sahne ----------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.inp)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
arm = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
if not meshes:
    raise SystemExit("mesh yok")
src_names = [o.name for o in meshes]
report["source_parts"] = src_names
# --drop: havada kalan kirinti parcalar (olcut 4) tamamen atilir
drop = [x for x in args.drop.split(",") if x]
for d in drop:
    if d not in src_names:
        raise SystemExit(f"--drop: bilinmeyen parca {d} (kaynak: {src_names})")
for o in [o for o in meshes if o.name in drop]:
    meshes.remove(o); bpy.data.objects.remove(o, do_unlink=True)
report["dropped"] = drop

def base_image(mat):
    if not mat or not mat.use_nodes:
        return None, None
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            inp = n.inputs.get("Base Color")
            if inp and inp.is_linked:
                stack = [inp.links[0].from_node]
                while stack:
                    m = stack.pop()
                    if m.type == "TEX_IMAGE" and m.image:
                        return m.image, None
                    for i in m.inputs:
                        if i.is_linked:
                            stack.append(i.links[0].from_node)
            if inp:
                c = inp.default_value
                return None, (c[0], c[1], c[2])
    return None, None

# Parca kimligi: birlestirmeden once her yuze kaynagin sirasi (--merge: kirinti hedef parcanin kimligini alir).
merge = dict(kv_list(args.merge))
for k, v in merge.items():
    if k not in src_names or v not in src_names:
        raise SystemExit(f"--merge: bilinmeyen parca {k}={v} (kaynak: {src_names})")
for i, o in enumerate(meshes):
    pid = src_names.index(merge.get(o.name, o.name))
    att = o.data.attributes.new("part", "INT", "FACE")
    for k in range(len(o.data.polygons)):
        att.data[k].value = pid
report["merged"] = merge
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
if not args.rig:
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != obj and o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)

me = obj.data
tris_in = sum(len(p.vertices) - 2 for p in me.polygons)
report["tris_in"] = tris_in

def extent_of(bm):
    if not bm.verts:
        return 1.0
    return max((max(v.co[i] for v in bm.verts) - min(v.co[i] for v in bm.verts)) for i in range(3))

# ---------------- kaynak (parca icinde) ----------------
bm = bmesh.new(); bm.from_mesh(me)
ext = extent_of(bm)
before = len(bm.verts)
lay = bm.faces.layers.int.get("part")
if args.parts and lay is not None:
    groups = {}
    for f in bm.faces:
        groups.setdefault(f[lay], set()).update(f.verts)
    for vs in groups.values():
        bmesh.ops.remove_doubles(bm, verts=list(vs), dist=ext * args.weld)
else:
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=ext * args.weld)
bm.to_mesh(me); bm.free(); me.update()
report["welded_verts"] = before - len(me.vertices)

# ---------------- sadelestirme ----------------
def decimate(o, target):
    t = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if t <= target:
        return
    mod = o.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = max(0.0002, target / t * 0.97)
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=mod.name)

decimate(obj, args.tris)
me = obj.data
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------- onarim (olcut 4) ----------------
def edge_census(bm):
    fan = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    bnd = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    wire = sum(1 for e in bm.edges if len(e.link_faces) == 0)
    return {"fan": fan, "boundary": bnd, "wire": wire}

def mark_fill(bm, faces):
    """Onarimin yaptigi yuzler 'dolgu' katmaninda 1 (sonra buyuk yamalar koyulastirilir)."""
    lay = bm.faces.layers.int.get("dolgu")  # repair() basinda kurulur: katman eklemek eldeki yuz baglarini gecersiz kilar
    if lay is None:
        return
    for f in faces:
        if f.is_valid:
            f[lay] = 1

def copy_uvs_to_new_faces(bm, faces):
    """Delik yamasinin koselerine ayni kosenin eski bir yuzdeki UV'si (renk cevreyle ayni kalir)."""
    mark_fill(bm, faces)
    uv = bm.loops.layers.uv.active
    if uv is None:
        return
    new = set(faces)
    for f in faces:
        for l in f.loops:
            for l2 in l.vert.link_loops:
                if l2.face not in new:
                    l[uv].uv = l2[uv].uv.copy()
                    break

def fan_fill(bm):
    """holes_fill'in kapatamadigi delik (kendine degen dongu): sinir kenarlari zincirlenir, ortaya bir kose konur, ucgen yelpaze."""
    uv = bm.loops.layers.uv.active
    bnd = {e for e in bm.edges if e.is_boundary}
    made = []
    while bnd:
        e0 = bnd.pop(); chain = [e0.verts[0], e0.verts[1]]
        while True:
            last = chain[-1]
            nxt = next((e for e in last.link_edges if e in bnd), None)
            if nxt is None:
                break
            bnd.discard(nxt)
            v = nxt.other_vert(last)
            if v is chain[0]:
                break
            chain.append(v)
        if len(chain) < 3:
            continue
        old_uv = {}
        if uv is not None:
            for v in chain:
                ls = [l for l in v.link_loops]
                if ls:
                    old_uv[v] = ls[0][uv].uv.copy()
        c = bm.verts.new(sum((v.co for v in chain), Vector()) / len(chain))
        for i in range(len(chain)):
            a, b = chain[i], chain[(i + 1) % len(chain)]
            try:
                f = bm.faces.new((b, a, c))
            except ValueError:
                continue
            made.append(f)
            if uv is not None and a in old_uv and b in old_uv:
                for l in f.loops:
                    l[uv].uv = old_uv.get(l.vert, (old_uv[a] + old_uv[b]) / 2)
    mark_fill(bm, made)
    return made

def darken_big_fills(bm, mats, share):
    """Buyuk delik yamalari (kapak penceresi, parca kesik agzi) kenar UV'siyle duz, acik renkli bir levha gibi gorunur.
    Yuzeyin `share` payindan buyuk her bitisik yama, kendi malzemesinin taban rengi dokusunda en koyu 1/64'luk
    bolgenin UV'sine baglanir: acik yerine ic golge gibi okunur (olcut 7). Kucuk yamalar cevre rengini korur."""
    import numpy as np
    lay = bm.faces.layers.int.get("dolgu"); uv = bm.loops.layers.uv.active
    out = {"patches": 0, "faces": 0, "area_share": 0.0}
    if lay is None or uv is None or share <= 0:
        return out
    tot = sum(f.calc_area() for f in bm.faces) or 1.0
    fill = {f for f in bm.faces if f[lay] == 1}
    dark_uv = {}
    def dark_of(mi):
        if mi in dark_uv:
            return dark_uv[mi]
        img = base_image(mats[mi])[0] if mi < len(mats) else None
        res = None
        if img is not None and img.size[0] > 0:
            w, h = img.size
            a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a)
            a = a.reshape(h, w, 4)[:, :, :3]
            g = 64; bh, bw = h // g, w // g
            lum = (a[:bh * g, :bw * g] @ np.array([0.2126, 0.7152, 0.0722], np.float32)).reshape(g, bh, g, bw).mean(axis=(1, 3))
            y, x = np.unravel_index(int(np.argmin(lum)), lum.shape)
            res = Vector(((x + 0.5) / g, (y + 0.5) / g))
        dark_uv[mi] = res
        return res
    while fill:
        f0 = fill.pop(); patch = [f0]; stack = [f0]
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g2 in e.link_faces:
                    if g2 in fill:
                        fill.discard(g2); patch.append(g2); stack.append(g2)
        area = sum(f.calc_area() for f in patch)
        if area / tot < share:
            continue
        d = dark_of(patch[0].material_index)
        if d is None:
            continue
        for f in patch:
            for l in f.loops:
                l[uv].uv = d.copy()
        out["patches"] += 1; out["faces"] += len(patch); out["area_share"] += area / tot
    out["area_share"] = round(out["area_share"], 4)
    bm.faces.layers.int.remove(lay)
    return out

def components(bm):
    """Kose baglantisina gore ayrik parcalar (kose listeleri)."""
    bm.verts.ensure_lookup_table()
    seen = set(); parts = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]; comp = []
        seen.add(v.index)
        while stack:
            a = stack.pop(); comp.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index); stack.append(b)
        parts.append(comp)
    return parts

def find_floating(bm, parts, longest):
    """Havada kalan parca: diger parcalara uzakligi > %1 en uzun kenar, icine gomulu degil ve tabani zeminden yukarida."""
    out = []
    if len(parts) < 2:
        return out
    bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    zfloor = min(v.co.z for v in bm.verts)
    comp_of = {}
    for ci, comp in enumerate(parts):
        for v in comp:
            comp_of[v.index] = ci
    for ci, comp in enumerate(parts):
        others = [f for f in bm.faces if comp_of[f.verts[0].index] != ci]
        if not others:
            continue
        verts = [v.co.copy() for f in others for v in f.verts]
        polys = []; k = 0
        for f in others:
            polys.append(list(range(k, k + len(f.verts)))); k += len(f.verts)
        tree = BVHTree.FromPolygons(verts, polys)
        dmin = 1e9; move = None
        for v in comp:
            loc, nrm, _, dist = tree.find_nearest(v.co)
            if loc is None:
                continue
            if (v.co - loc).dot(nrm) < 0:  # baska bir parcanin icine gomulu: bagli sayilir
                dmin = 0.0; break
            if dist < dmin:
                dmin = dist; move = loc - v.co
        zmin = min(v.co.z for v in comp)
        if dmin > 0.01 * longest and zmin - zfloor > 0.01 * longest:
            fs = {f for v in comp for f in v.link_faces}
            c = sum((v.co for v in comp), Vector()) / len(comp)
            out.append({"index": ci, "verts": len(comp), "gap_m": round(dmin, 3), "z_m": round(zmin - zfloor, 3),
                        "center": [round(x, 3) for x in c], "area": sum(f.calc_area() for f in fs), "move": move})
    return out

def repair(bm, ext):
    log = []
    if bm.faces.layers.int.get("dolgu") is None:
        bm.faces.layers.int.new("dolgu")
    for it in range(args.repair_passes):
        loose_e = [e for e in bm.edges if not e.link_faces]
        if loose_e:
            bmesh.ops.delete(bm, geom=loose_e, context="EDGES")
        loose_v = [v for v in bm.verts if not v.link_edges]
        if loose_v:
            bmesh.ops.delete(bm, geom=loose_v, context="VERTS")
        bmesh.ops.dissolve_degenerate(bm, edges=bm.edges[:], dist=ext * 1e-7)
        fans = [e for e in bm.edges if len(e.link_faces) > 2]
        doomed = set()
        if fans and it < 2:
            # ikiden cok yuzlu kenar: en kucuk yuzler (seritler) silinir, kalan iki yuz kenari paylasir
            for e in fans:
                fs = sorted(e.link_faces, key=lambda f: f.calc_area())
                for f in fs[:len(fs) - 2]:
                    doomed.add(f)
        elif fans:
            # inatci kenar (iki yuzey bir kenarda kesisiyor): kenarin iki kosesindeki butun yuzler silinir, delik yeniden kapanir
            for e in fans:
                for v in e.verts:
                    doomed.update(v.link_faces)
        if doomed:
            bmesh.ops.delete(bm, geom=list(doomed), context="FACES_ONLY")
            loose_e = [e for e in bm.edges if not e.link_faces]
            if loose_e:
                bmesh.ops.delete(bm, geom=loose_e, context="EDGES")
            loose_v = [v for v in bm.verts if not v.link_edges]
            if loose_v:
                bmesh.ops.delete(bm, geom=loose_v, context="VERTS")
        # delikler: kenar dongusu yuzle kapanir, sonra ucgenlenir
        bnd = [e for e in bm.edges if e.is_boundary]
        filled = []
        if bnd:
            res = bmesh.ops.holes_fill(bm, edges=bnd, sides=0)
            filled = res.get("faces", [])
            if filled:
                tri = bmesh.ops.triangulate(bm, faces=filled, quad_method="BEAUTY", ngon_method="BEAUTY")
                filled = tri.get("faces", filled)
                copy_uvs_to_new_faces(bm, filled)
            elif it > 0:
                filled = fan_fill(bm)
        c = edge_census(bm)
        nmv = sum(1 for v in bm.verts if not v.is_manifold)
        log.append({"pass": it + 1, "deleted_slivers": len(doomed), "filled_faces": len(filled), **c, "nonmanifold_verts": nmv})
        if c["fan"] == 0 and c["boundary"] == 0 and c["wire"] == 0:
            break
        if not doomed and not filled and it > 0:
            break
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # Yonlendirilemeyen bolge (kenarin iki yuzu ayni yonde dolasiyor): o yuzler silinir, delik kapanir, yeniden yonlenir.
    for k in range(3):
        bad = [e for e in bm.edges if len(e.link_loops) == 2 and e.link_loops[0].vert == e.link_loops[1].vert]
        if not bad:
            break
        fs = {f for e in bad for f in e.link_faces}
        bmesh.ops.delete(bm, geom=list(fs), context="FACES_ONLY")
        for e in [e for e in bm.edges if not e.link_faces]:
            bm.edges.remove(e)
        for v in [v for v in bm.verts if not v.link_edges]:
            bm.verts.remove(v)
        res = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
        nf = res.get("faces", [])
        if nf:
            nf = bmesh.ops.triangulate(bm, faces=nf, quad_method="BEAUTY", ngon_method="BEAUTY").get("faces", nf)
            copy_uvs_to_new_faces(bm, nf)
        if any(e.is_boundary for e in bm.edges):
            fan_fill(bm)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        log.append({"pass": f"sarim{k + 1}", "deleted_faces": len(fs), **edge_census(bm)})
    return log

if not args.no_repair:
    bm = bmesh.new(); bm.from_mesh(me)
    report["repair_before"] = edge_census(bm)
    report["repair_log"] = repair(bm, extent_of(bm))
    # Havada kalan kucuk kirintilar (olcut 4): yuzeyin --drop_floating payindan kucukse silinir; buyukse denetimde kalir.
    ext_r = extent_of(bm)
    tot = sum(f.calc_area() for f in bm.faces) or 1.0
    parts_r = components(bm)
    gone, snapped = [], []
    for fl in find_floating(bm, parts_r, ext_r):
        info = {k: v for k, v in fl.items() if k not in ("index", "area", "move")} | {"area_share": round(fl["area"] / tot, 4)}
        vs = parts_r[fl["index"]]
        if fl["area"] / tot < args.drop_floating:
            bmesh.ops.delete(bm, geom=vs, context="VERTS")
            gone.append(info)
        elif fl["move"] is not None:
            # buyuk parca silinmez: en yakin yuzeye yaslanir (bosluk kadar, %0,2 icine gomulerek)
            mv = fl["move"] + fl["move"].normalized() * 0.002 * ext_r
            bmesh.ops.translate(bm, verts=vs, vec=mv)
            snapped.append(info | {"moved_m": round(mv.length, 4)})
    report["dropped_floating"] = gone
    report["snapped_floating"] = snapped
    report["dark_fill"] = darken_big_fills(bm, list(me.materials), args.dark_fill) if args.keep_texture else None
    if bm.faces.layers.int.get("dolgu"):
        bm.faces.layers.int.remove(bm.faces.layers.int.get("dolgu"))
    bm.to_mesh(me); bm.free(); me.update()

if args.relax > 0:
    mod = obj.modifiers.new("relax", "LAPLACIANSMOOTH")
    mod.iterations = args.relax
    mod.lambda_factor = 0.8
    mod.use_volume_preserve = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)

if not args.keep_texture:
    # ---------------- dokudan kose rengi (eski, dokusuz yon) ----------------
    img_cache = {}
    def pixels_of(img):
        if img.name in img_cache:
            return img_cache[img.name]
        w, h = img.size
        img_cache[img.name] = (w, h, list(img.pixels[:]))
        return img_cache[img.name]

    def sample(img, u, v):
        w, h, px = pixels_of(img)
        x = int((u % 1.0) * (w - 1)); y = int((v % 1.0) * (h - 1))
        i = (y * w + x) * 4
        return (px[i], px[i + 1], px[i + 2])

    uv = me.uv_layers.active.data if me.uv_layers.active else None
    mats = me.materials
    face_rgb = []
    for p in me.polygons:
        mat = mats[p.material_index] if p.material_index < len(mats) else None
        img, flat = base_image(mat)
        if img is not None and uv is not None:
            uvs = [uv[li].uv for li in p.loop_indices]
            c = Vector((0, 0))
            for t in uvs:
                c += t
            c /= len(uvs)
            pts = [c] + [c.lerp(t, 0.6) for t in uvs[:6]]
            acc = [0.0, 0.0, 0.0]
            for q in pts:
                s = sample(img, q.x, q.y)
                for k in range(3):
                    acc[k] += s[k]
            rgb = tuple(a / len(pts) for a in acc)
        elif flat is not None:
            rgb = tuple((min(1, max(0, x))) ** (1 / 2.2) for x in flat)
        else:
            rgb = (0.6, 0.6, 0.6)
        face_rgb.append(rgb)
    labels, dists = [], []
    for rgb in face_rgb:
        i, d = nearest(rgb)
        labels.append(i); dists.append(d)
    report["source_mean_dE"] = round(sum(dists) / max(1, len(dists)), 2)
    report["source_within_dE8"] = round(sum(1 for d in dists if d < 8) / max(1, len(dists)), 3)
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    for _ in range(args.smooth_passes):
        new = labels[:]
        for f in bm.faces:
            counts = {}
            for e in f.edges:
                for g in e.link_faces:
                    if g.index != f.index:
                        counts[labels[g.index]] = counts.get(labels[g.index], 0) + 1
            if counts:
                top, n = max(counts.items(), key=lambda kv: kv[1])
                if n >= 2 and labels[f.index] not in counts:
                    new[f.index] = top
        labels = new
    bm.free()
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    col = me.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")
    for p in me.polygons:
        rgb = allowed[labels[p.index]][1]
        for li in p.loop_indices:
            col.data[li].color_srgb = (rgb[0], rgb[1], rgb[2], 1.0)
    me.color_attributes.active_color = col
    used = {}
    for p in me.polygons:
        used[allowed[labels[p.index]][0]] = used.get(allowed[labels[p.index]][0], 0) + 1
    report["palette_use"] = dict(sorted(used.items(), key=lambda kv: -kv[1]))
    me.materials.clear()
    mat = bpy.data.materials.new(args.name + "_vc")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85
    me.materials.append(mat)
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)
else:
    for img in bpy.data.images:
        w, h = img.size
        if max(w, h) > args.tex_size:
            k = args.tex_size / max(w, h)
            img.scale(max(1, int(w * k)), max(1, int(h * k)))
            img.pack()
    report["tex_size"] = args.tex_size

# ---------------- olcek ve eksen ----------------
def world_bbox(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx

root = arm if (args.rig and arm) else obj
bpy.context.view_layer.update()
mn, mx = world_bbox([obj])
h = mx.z - mn.z
s = args.height / h if h > 1e-6 else 1.0
root.scale = root.scale * s
bpy.context.view_layer.update()
mn, mx = world_bbox([obj])
root.location -= Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
bpy.context.view_layer.update()
if not args.rig:
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
bpy.context.view_layer.objects.active = obj
try:
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
except Exception:
    bpy.ops.object.shade_smooth()

# ---------------- denetimler ----------------
me = obj.data
bpy.context.view_layer.update()
mn, mx = world_bbox([obj])
size = mx - mn
longest = max(size.x, size.y, size.z)
tris = sum(len(p.vertices) - 2 for p in me.polygons)

bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
census = edge_census(bm)
nonman = sum(1 for e in bm.edges if not e.is_manifold)
parts = components(bm)

# Eski kalinlik olcusu (rapor icin yan yana): parca basina kutunun en dar boyutu / en uzun kenar.
bbox_ratio = []
for comp in parts:
    xs = [p.co.x for p in comp]; ys = [p.co.y for p in comp]; zs = [p.co.z for p in comp]
    dims = sorted([max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)])
    bbox_ratio.append(dims[0] / longest if longest > 0 else 0)

# Ters yuz: kenari paylasan iki yuz kenari ters yonde dolasmali (tutarli sarim); aksi ters yuz. Kapali parcanin
# isaretli hacmi pozitif olmali (normaller disari). Kesin olcu: normal yeniden hesaplama sezgiseline dayanmaz.
inverted = 0
for e in bm.edges:
    ls = list(e.link_loops)
    if len(ls) == 2 and ls[0].vert == ls[1].vert:
        inverted += 1
neg_volume = 0
neg_detail = []
flat_shells = []
_bb = [v.co for v in bm.verts]
flat_eps = 2e-4 * ((max(p.x for p in _bb) - min(p.x for p in _bb)) * (max(p.y for p in _bb) - min(p.y for p in _bb)) *
                   (max(p.z for p in _bb) - min(p.z for p in _bb)))  # kutu hacminin 1/5000'i
for comp in parts:
    cs = set(comp)
    fs = {f for v in comp for f in v.link_faces}
    if any(e.is_boundary for f in fs for e in f.edges):
        continue
    vol = 0.0
    for f in fs:
        vs = [v.co for v in f.verts]
        for i in range(1, len(vs) - 1):
            vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
    if vol < 0 and abs(vol) < flat_eps:
        # neredeyse sifir hacim: iki kati ust uste yassi kabuk (ornegin El Arabasi'nin delik yamasi + kapanan kenari).
        # Sarim yonu sayisal olarak anlamsiz; ters yuz sayilmaz, raporda ayri yazilir.
        flat_shells.append({"verts": len(comp), "volume": round(vol, 7)})
    elif vol < 0:
        neg_volume += 1
        c0 = sum((v.co for v in comp), Vector()) / len(comp)
        neg_detail.append({"verts": len(comp), "volume": round(vol, 6), "center": [round(x, 3) for x in c0]})

# Havada kalan parca: diger parcalara en yakin uzakligi > %1 en uzun kenar, gomulu degil, tabani yerden yukarida.
floating = [{k: v for k, v in fl.items() if k not in ("index", "area", "move")} for fl in find_floating(bm, parts, longest)]

# Kalinlik ornekleme (olcut 3, SANAT-SES §1.1 "ince cubuk ve sivri diken yok; en ince parca en uzun kenarin %6'sindan kalin"):
# 1) her yuzde (alan agirlikli ornekler) ice dogru isinla yerel kalinlik t. 2) t < sinir olan yuzler "ince".
# 3) her ince yuzun cevresi (ince yuzlerden, 2 x sinir yaricapinda) temel bilesenlere ayrilir: ikinci buyuk uzanim da
#    sinirdan kucukse orasi cubuk/diken (iki yonde ince); degilse levha (yaprak, kapak: tek yonde ince, genis).
# Olcut: cubuk/diken alaninin yuzeye orani <= --thin_area. Levha payi ve eski kutu olcusu bilgi olarak yazilir.
import numpy as np, bisect
tree = BVHTree.FromBMesh(bm)
thin_lim = args.thin * longest
eps = longest * 1e-4
areas = [f.calc_area() for f in bm.faces]
total_area = sum(areas) or 1.0
face_t = {}
per_face = max(1, args.thick_samples // max(1, len(bm.faces)))
for f in bm.faces:
    n = f.normal
    if n.length < 0.5:
        continue
    vs = [v.co for v in f.verts]
    ts = []
    for k in range(per_face):
        if k == 0:
            p = f.calc_center_median()
        else:
            a, b = random.random(), random.random()
            if a + b > 1:
                a, b = 1 - a, 1 - b
            p = vs[0] + (vs[1] - vs[0]) * a + (vs[2] - vs[0]) * b
        hit = tree.ray_cast(p - n * eps, -n, longest * 2)
        if hit[0] is not None:
            ts.append(hit[3] + eps)
    if ts:
        face_t[f.index] = sorted(ts)[len(ts) // 2]
thin_faces = {i for i, t in face_t.items() if t < thin_lim}
# Komsuluk "inceye yakin" yuzlerden (t < 2 x sinir) kurulur: tasli kubbe gibi kalin yerleri ince yariklarla bolunen
# bir levha, yariklar tek basina dar serit gibi gorunse de levha sayilir. Govdeye baglanan gercek cubugun komsulugu
# govdeye (kalin) gecemez, cubuk kalir.
near_thin = {i for i, t in face_t.items() if t < 2 * thin_lim}
centers = {f.index: f.calc_center_median() for f in bm.faces}
stick = set()
R = 2 * thin_lim
for i in thin_faces:
    c0 = centers[i]
    seen = {i}; stack = [i]; pts = []
    while stack:
        j = stack.pop(); pts.append(centers[j])
        for e in bm.faces[j].edges:
            for g in e.link_faces:
                if g.index in near_thin and g.index not in seen and (centers[g.index] - c0).length <= R:
                    seen.add(g.index); stack.append(g.index)
    if len(pts) < 4:
        continue  # kalin yuzeyin icinde tek basina ince olculen yuz: ic ice kabuktan gelen olcum gurultusu
    P = np.array([[p.x, p.y, p.z] for p in pts]); P -= P.mean(axis=0)
    w, V = np.linalg.eigh(P.T @ P)
    proj = P @ V
    ext3 = sorted((proj.max(axis=0) - proj.min(axis=0)).tolist(), reverse=True)
    if ext3[1] < thin_lim:
        stick.add(i)
# Kalinlik haritasi (kart ve inceleme icin): kirmizi cubuk/diken, turuncu levha, gri kalin.
tmap = me.color_attributes.new(name="thinmap", type="BYTE_COLOR", domain="CORNER")
for p in me.polygons:
    col = (0.85, 0.08, 0.05, 1) if p.index in stick else ((0.98, 0.62, 0.2, 1) if p.index in thin_faces else (0.78, 0.78, 0.76, 1))
    for li in p.loop_indices:
        tmap.data[li].color = col
stick_area = sum(areas[i] for i in stick)
plate_area = sum(areas[i] for i in thin_faces) - stick_area
stick_share = stick_area / total_area
thick = {
    "pass": stick_share <= args.thin_area,
    "thin_share": round(stick_share, 4), "allowed_share": args.thin_area, "limit_m": round(thin_lim, 4),
    "stick_min_t_m": round(min((face_t[i] for i in stick), default=0.0), 4),
    "plate_share": round(plate_area / total_area, 4),
    "faces_measured": len(face_t),
    "old_bbox_min_ratio": round(min(bbox_ratio), 3) if bbox_ratio else None,
    "note": "cubuk/diken alani (iki yonde ince) / yuzey; levha payi (tek yonde ince: yaprak, kapak) bilgi; eski olcu: parca kutusunun en dar boyutu / en uzun kenar",
}
bm.free()

# Olcut 1: doku var, butce cozunurlugunde (dokulu yon). Eski yon: palet orani.
imgs = {}
for m in me.materials:
    if m and m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                imgs[n.image.name] = tuple(n.image.size)
base_imgs = {base_image(m)[0].name for m in me.materials if m and base_image(m)[0]}
max_tex = budget.get("maxTex", args.tex_size) if budget else args.tex_size
if args.keep_texture:
    c1 = {"pass": bool(base_imgs) and all(max(sz) <= max_tex for sz in imgs.values()),
          "textures": {k: list(v) for k, v in imgs.items()}, "base_color_maps": len(base_imgs), "max_tex": max_tex}
else:
    c1 = {"pass": (report.get("source_within_dE8") or 0) >= 0.95, "note": "eski yon: kaynakta dE<8 orani"}

budget_max = budget["maxTris"] if budget else args.tris
report["tris"] = tris
report["size_m"] = [round(size.x, 3), round(size.y, 3), round(size.z, 3)]
report["parts"] = len(parts)
report["checks"] = {
    "1_texture": c1,
    "2_polygon": {"pass": tris <= budget_max, "value": tris, "budget": budget_max, "class": args.klass,
                  "below_min": bool(budget and tris < budget.get("minTris", 0))},
    "3_thickness": thick,
    "4_integrity": {"pass": nonman == 0 and census["boundary"] == 0 and inverted == 0 and neg_volume == 0 and not floating,
                    "non_manifold_edges": nonman, "boundary_edges": census["boundary"], "inverted_faces": inverted,
                    "inverted_note": "ters sarimli kenar sayisi",
                    "negative_volume_parts": neg_volume, "negative_volume_detail": neg_detail, "flat_shells": flat_shells, "floating_parts": floating, "loose_parts": len(parts)},
    "5_scale_axis": {"pass": abs(size.z - args.height) < 0.02 and abs(mn.z) < 0.01 and abs((mn.x + mx.x) / 2) < 0.02 and abs((mn.y + mx.y) / 2) < 0.02,
                     "height": round(size.z, 3), "target": args.height, "base_z": round(mn.z, 4),
                     "center_xy": [round((mn.x + mx.x) / 2, 4), round((mn.y + mx.y) / 2, 4)], "forward": args.forward,
                     "note": "on yon betikle olculemez; bildirilen eksen yazilir"},
}

# Palet yonu (kart; dokulu yonde zorunlu degil): dokudan alana gore ornek, en yakin palet rengi.
def palette_direction(o, n=1500):
    try:
        import numpy as np
    except Exception:
        return None
    m = o.data
    uvl = m.uv_layers.active
    if uvl is None:
        return None
    cache = {}
    def px(img):
        if img.name not in cache:
            w, h = img.size
            a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a)
            cache[img.name] = (w, h, a.reshape(h, w, 4))
        return cache[img.name]
    polys = list(m.polygons)
    ar = [p.area for p in polys]; tot = sum(ar)
    cs = []; acc = 0.0
    for a in ar:
        acc += a; cs.append(acc)
    use = {}; dsum = 0.0; cnt = 0
    for _ in range(n):
        r = random.random() * tot
        p = polys[min(len(cs) - 1, bisect.bisect_left(cs, r))]
        mat = m.materials[p.material_index] if p.material_index < len(m.materials) else None
        img, flat = base_image(mat)
        if img is None:
            continue
        uvs = [uvl.data[li].uv for li in p.loop_indices]
        a, b = random.random(), random.random()
        if a + b > 1:
            a, b = 1 - a, 1 - b
        q = uvs[0] + (uvs[1] - uvs[0]) * a + (uvs[2] - uvs[0]) * b
        w, h, arr = px(img)
        x = int((q.x % 1.0) * (w - 1)); y = int((q.y % 1.0) * (h - 1))
        rgb = tuple(float(c) for c in arr[y, x, :3])
        if img.colorspace_settings.name != "sRGB":
            continue
        i, d = nearest(rgb)
        use[allowed[i][0]] = use.get(allowed[i][0], 0) + 1
        dsum += d; cnt += 1
    if cnt == 0:
        return None
    top = sorted(use.items(), key=lambda kv: -kv[1])
    hexes = {a[0]: a[3] for a in allowed}
    return {"samples": cnt, "mean_dE": round(dsum / cnt, 1),
            "share": [{"color": k, "hex": hexes[k], "share": round(v / cnt, 3)} for k, v in top[:6]]}

if args.keep_texture:
    report["palette_direction"] = palette_direction(obj)

# ---------------- parca modu: adli dugumler, pivot, soket ----------------
objects = [obj]
sockets_out = []
if args.parts and "part" in obj.data.attributes:
    part_attr = obj.data.attributes["part"]
    ids = sorted({part_attr.data[p.index].value for p in obj.data.polygons})
    pieces = []
    for i in ids:
        o = obj.copy(); o.data = obj.data.copy(); bpy.context.scene.collection.objects.link(o)
        b2 = bmesh.new(); b2.from_mesh(o.data)
        lay = b2.faces.layers.int.get("part")
        bmesh.ops.delete(b2, geom=[f for f in b2.faces if f[lay] != i], context="FACES")
        b2.to_mesh(o.data); b2.free()
        o["src"] = src_names[i]
        pieces.append(o)
    bpy.data.objects.remove(obj, do_unlink=True)
    pieces = [o for o in pieces if len(o.data.polygons) > 0]
    info = []
    for o in pieces:
        pmn, pmx = world_bbox([o])
        info.append({"obj": o, "src": o["src"], "center": (pmn + pmx) / 2, "size": pmx - pmn, "mn": pmn, "mx": pmx,
                     "vol": (pmx - pmn).x * (pmx - pmn).y * (pmx - pmn).z})
    # Otomatik adlar (eski kural), sonra --rename (kaynak adi ya da otomatik ad ile)
    names = {}
    body = max(info, key=lambda d: d["vol"]); names[body["src"]] = "body"
    rest = [d for d in info if d is not body]
    if rest:
        top = max(rest, key=lambda d: d["mx"].z)
        if top["mx"].z >= mx.z - 0.02 * (mx.z - mn.z) and top["size"].x < 0.5 * size.x:
            names[top["src"]] = "chimney"; rest.remove(top)
    for k, d in enumerate(sorted(rest, key=lambda d: -d["vol"])):
        names[d["src"]] = f"part_{k + 1}"
    fix = dict(kv_list(args.rename))
    names = {src: fix.get(src, fix.get(auto, auto)) for src, auto in names.items()}
    if len(set(names.values())) != len(names):
        raise SystemExit(f"parca adlari cakisiyor: {names}")
    by_name = {}
    for d in info:
        o = d["obj"]; o.name = names[d["src"]]; o.data.name = o.name
        by_name[o.name] = d
    # Pivot: parcanin orijini kendi kutusunun alt-ortasina / ortasina (baca "puf"u, teker donusu kendi ekseninde)
    for pname, where in kv_list(args.pivots):
        d = by_name.get(pname)
        if d is None:
            raise SystemExit(f"--pivots: parca yok {pname}")
        c = d["center"].copy()
        if where == "bottom":
            c.z = d["mn"].z
        elif where == "top":
            c.z = d["mx"].z
        o = d["obj"]
        o.data.transform(Matrix.Translation(-c)); o.matrix_world = Matrix.Translation(c)
        d["pivot"] = c
    # Soket: bos dugum (Godot'ta Node3D). ad=parca[+parca]:center|top|bottom[:ebeveyn]
    for sname, spec in kv_list(args.sockets):
        bits = spec.split(":")
        refs = bits[0].split("+"); where = bits[1] if len(bits) > 1 else "center"; parent = bits[2] if len(bits) > 2 else ""
        cs = []
        for r in refs:
            d = by_name.get(r)
            if d is None:
                raise SystemExit(f"--sockets: parca yok {r}")
            c = d["center"].copy()
            if where == "bottom":
                c.z = d["mn"].z
            elif where == "top":
                c.z = d["mx"].z
            cs.append(c)
        c = sum(cs, Vector()) / len(cs)
        e = bpy.data.objects.new(sname, None); e.empty_display_size = 0.1
        bpy.context.scene.collection.objects.link(e)
        e.location = c
        sockets_out.append({"name": sname, "of": refs, "at": where, "pos_blender": [round(v, 3) for v in c],
                            "pos_godot": [round(c.x, 3), round(c.z, 3), round(-c.y, 3)]})
        if parent:
            # ebeveyn: bu soket bir parcanin ustune, parca dunyada yerinde kalir (orn. wheel_pivot -> wheel)
            child = bpy.data.objects.get(parent)
            if child is None:
                raise SystemExit(f"--sockets: cocuk yok {parent}")
            # once guncelle: yeni bos dugumun matrix_world'u yoksa birim kalir ve cocuk iki kez kayar
            # (17 Kasim ilk calismada teker GLB'de pivotun iki kati uzakta duruyordu)
            bpy.context.view_layer.update()
            mw = child.matrix_world.copy()
            child.parent = e; child.matrix_parent_inverse = Matrix.Identity(4)
            child.matrix_basis = e.matrix_world.inverted() @ mw
            sockets_out[-1]["child"] = parent
    report["parts_named"] = []
    for d in info:
        o = d["obj"]
        report["parts_named"].append({"name": o.name, "source": d["src"], "center": [round(v, 3) for v in d["center"]],
                                      "size": [round(v, 3) for v in d["size"]], "faces": len(o.data.polygons),
                                      "pivot": [round(v, 3) for v in d["pivot"]] if "pivot" in d else None})
    objects = [d["obj"] for d in info]
report["sockets"] = sockets_out

# Parca modunda butunluk ve parca bazinda bilgi: her parca ayri kapali mi?
if args.parts:
    per = {}
    for o in objects:
        b3 = bmesh.new(); b3.from_mesh(o.data)
        per[o.name] = {**edge_census(b3), "non_manifold": sum(1 for e in b3.edges if not e.is_manifold)}
        b3.free()
    report["checks"]["4_integrity"]["per_part"] = per
    report["checks"]["4_integrity"]["pass"] = report["checks"]["4_integrity"]["pass"] and all(v["non_manifold"] == 0 for v in per.values())

# ---------------- goruntuler ----------------
scene = bpy.context.scene
ctr = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2))
fwd_ang = {"+X": 90, "-X": -90, "+Y": 180, "-Y": 0}.get(args.forward, 90)  # kamera aci 0 = -Y'den bakar

def setup_cam(ortho, res, transparent=False):
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.view_settings.view_transform = "Standard"  # dokunun rengi oldugu gibi (AgX/Filmic degil)
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE" if args.keep_texture else "VERTEX"
    scene.display.shading.show_shadows = False
    scene.display.shading.show_cavity = False
    scene.render.resolution_x = res; scene.render.resolution_y = res
    scene.render.film_transparent = transparent
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("w")
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (0.83, 0.8, 0.74)
    cam = scene.camera
    if cam is None:
        cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
        scene.collection.objects.link(cam); scene.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = ortho
    return cam

def aim(cam, az_deg, elev=0.45):
    a = math.radians(az_deg + fwd_ang)
    d = longest * 3
    cam.location = ctr + Vector((math.sin(a) * d, -math.cos(a) * d, d * elev))
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()

def render_to(path):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)

renders = {}
if args.renders:
    rd = os.path.abspath(args.renders)
    cam = setup_cam(longest * 1.6, 768)
    for i, ang in enumerate([-30, 60, 150, 240]):  # on 3/4, yan, arka 3/4, diger yan
        aim(cam, ang)
        render_to(os.path.join(rd, f"{args.name}_{i}.png"))
        renders.setdefault("views", []).append(os.path.join(rd, f"{args.name}_{i}.png"))
    # kalinlik haritasi (olcut 3'un neyi ince saydigi)
    for o in objects:
        if "thinmap" in o.data.color_attributes:
            o.data.color_attributes.active_color = o.data.color_attributes["thinmap"]
    scene.display.shading.color_type = "VERTEX"
    for i, ang in enumerate([-30, 150]):
        aim(cam, ang)
        render_to(os.path.join(rd, f"{args.name}_kalinlik_{i}.png"))
    renders["thin"] = os.path.join(rd, f"{args.name}_kalinlik_0.png")
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.02, 0.02, 0.02)
    scene.display.shading.light = "FLAT"
    scene.render.resolution_x = 64; scene.render.resolution_y = 64
    aim(cam, -30)
    render_to(os.path.join(rd, f"{args.name}_silhouette64.png"))
    renders["silhouette"] = os.path.join(rd, f"{args.name}_silhouette64.png")
for o in objects:
    if "thinmap" in o.data.color_attributes:
        o.data.color_attributes.remove(o.data.color_attributes["thinmap"])
icon_path = args.icon or os.path.splitext(args.out)[0] + "_icon.png"
cam = setup_cam(longest * 1.15, args.icon_size, transparent=True)
aim(cam, -35, 0.5)
render_to(icon_path)
report["icon"] = icon_path

# ---------------- disa aktar: ana GLB ----------------
def export(path, selection=None):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    if selection is not None:
        bpy.ops.object.select_all(action="DESELECT")
        for o in selection:
            o.select_set(True)
    kw = dict(filepath=path, export_format="GLB", use_selection=selection is not None, export_yup=True,
              export_texcoords=args.keep_texture, export_normals=True, export_cameras=False, export_lights=False)
    try:
        bpy.ops.export_scene.gltf(**kw, export_vertex_color=("NONE" if args.keep_texture else "ACTIVE"),
                                  export_animations=args.rig, export_skins=args.rig)
    except TypeError:
        bpy.ops.export_scene.gltf(**kw, export_colors=True, export_animations=args.rig, export_skins=args.rig)

cams = [o for o in scene.objects if o.type == "CAMERA"]
for c in cams:
    bpy.data.objects.remove(c, do_unlink=True)
export(args.out)
report["out"] = args.out

# ---------------- LOD ----------------
def merged_copy(name):
    cs = []
    for o in objects:
        c = o.copy(); c.data = o.data.copy(); c.parent = None
        bpy.context.scene.collection.objects.link(c)
        c.matrix_world = o.matrix_world.copy()
        cs.append(c)
    bpy.ops.object.select_all(action="DESELECT")
    for c in cs:
        c.select_set(True)
    bpy.context.view_layer.objects.active = cs[0]
    if len(cs) > 1:
        bpy.ops.object.join()
    m = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    m.name = name; m.data.name = name
    return m

lod_tris_all = [sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objects)]
report["lods"] = [{"level": 0, "tris": lod_tris_all[0], "file": args.out, "tex": args.tex_size}]
lod_ratios = [float(x) for x in args.lods.split(",") if x]
lod_tex = [int(x) for x in args.lod_tex.split(",") if x]
hull_src = merged_copy(args.name + "_hullsrc")
for li, ratio in enumerate(lod_ratios, start=1):
    m = merged_copy(f"{args.name}_lod{li}")
    decimate(m, int(lod_tris_all[0] * ratio))
    b4 = bmesh.new(); b4.from_mesh(m.data)
    rl = repair(b4, longest) if not args.no_repair else []
    if args.keep_texture:
        darken_big_fills(b4, list(m.data.materials), args.dark_fill)
    if b4.faces.layers.int.get("dolgu"):
        b4.faces.layers.int.remove(b4.faces.layers.int.get("dolgu"))
    cen = edge_census(b4)
    b4.to_mesh(m.data); b4.free()
    if args.keep_texture and li - 1 < len(lod_tex):
        for img in bpy.data.images:
            w, h = img.size
            if max(w, h) > lod_tex[li - 1]:
                k = lod_tex[li - 1] / max(w, h)
                img.scale(max(1, int(w * k)), max(1, int(h * k)))
                img.pack()
    path = os.path.splitext(args.out)[0] + f"_lod{li}.glb"
    export(path, [m])
    t = sum(len(p.vertices) - 2 for p in m.data.polygons)
    report["lods"].append({"level": li, "ratio": ratio, "tris": t, "file": path,
                           "tex": lod_tex[li - 1] if li - 1 < len(lod_tex) else None, "census": cen})
    bpy.data.objects.remove(m, do_unlink=True)

# ---------------- carpisma kabugu ----------------
def hull_of(points):
    """Yalniz noktalardan dis bukey kabuk (ic noktalar silinir)."""
    b = bmesh.new()
    for p in points:
        b.verts.new(p)
    bmesh.ops.convex_hull(b, input=b.verts[:], use_existing_faces=False)
    bmesh.ops.delete(b, geom=[v for v in b.verts if not v.link_faces], context="VERTS")
    return b

b5 = hull_of([v.co.copy() for v in hull_src.data.vertices])
hm = bpy.data.meshes.new("hull"); b5.to_mesh(hm); b5.free()
hobj = bpy.data.objects.new("hull", hm); bpy.context.scene.collection.objects.link(hobj)
for _ in range(6):
    if len(hm.vertices) <= args.hull_points:
        break
    mod = hobj.modifiers.new("dec", "DECIMATE"); mod.decimate_type = "COLLAPSE"
    mod.ratio = max(0.02, (args.hull_points * 2 - 4) / max(1, len(hm.polygons)))
    bpy.context.view_layer.objects.active = hobj
    bpy.ops.object.select_all(action="DESELECT"); hobj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    b6 = hull_of([v.co.copy() for v in hm.vertices])
    b6.to_mesh(hm); b6.free(); hm.update()
pts = [v.co.copy() for v in hm.vertices]
hull_path = os.path.splitext(args.out)[0] + "_hull.tres"
with open(hull_path, "w", encoding="utf-8", newline="\n") as fh:
    fh.write('[gd_resource type="ConvexPolygonShape3D" format=3]\n\n[resource]\n')
    fh.write("points = PackedVector3Array(" + ", ".join(f"{p.x:.4f}, {p.z:.4f}, {-p.y:.4f}" for p in pts) + ")\n")
def mesh_volume(m):
    v = 0.0
    for p in m.polygons:
        vs = [m.vertices[i].co for i in p.vertices]
        for i in range(1, len(vs) - 1):
            v += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
    return abs(v)
report["hull"] = {"file": hull_path, "points": len(pts), "volume_m3": round(mesh_volume(hm), 4),
                  "mesh_volume_m3": round(mesh_volume(hull_src.data), 4), "note": "Godot ekseninde (Y yukari), model orijinine gore"}
bpy.data.objects.remove(hobj, do_unlink=True)
bpy.data.objects.remove(hull_src, do_unlink=True)

report["pass_scripted"] = all(c["pass"] for c in report["checks"].values())

# ---------------- varlik karti ----------------
def tr_num(x, nd=0):
    s = f"{x:,.{nd}f}"
    return s.replace(",", "X").replace(".", ",").replace("X", ".")

def make_card(path):
    import numpy as np
    W, H = 1600, 1000
    bg = np.array([0.93, 0.91, 0.86, 1.0], np.float32)
    canvas = np.tile(bg, (H, W, 1))
    def load_px(p):
        im = bpy.data.images.load(p, check_existing=False)
        w, h = im.size
        a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a)
        bpy.data.images.remove(im)
        return a.reshape(h, w, 4)
    def paste(img, x, y):  # x,y: sol ust (piksel); numpy satirlari alttan baslar
        h, w = img.shape[:2]
        y0 = H - y - h
        reg = canvas[y0:y0 + h, x:x + w]
        al = img[:, :, 3:4]
        reg[:, :, :3] = img[:, :, :3] * al + reg[:, :, :3] * (1 - al)
    views = renders.get("views", [])
    for i, p in enumerate(views):
        a = load_px(p)
        a = a.reshape(384, 2, 384, 2, 4).mean(axis=(1, 3))  # 768 -> 384
        paste(a, 32 + i * 388, 24)
    if renders.get("thin"):
        tm = load_px(renders["thin"]).reshape(384, 2, 384, 2, 4).mean(axis=(1, 3))
        paste(tm, 1196, 430)
    if renders.get("silhouette"):
        s64 = load_px(renders["silhouette"])
        paste(np.repeat(np.repeat(s64, 3, axis=0), 3, axis=1), 32, 776)
    if os.path.exists(icon_path):
        ic = load_px(icon_path)
        paste(ic, 248, 744)
    # Yazi katmani: ayni boyda seffaf Workbench goruntusu (1 birim = 100 piksel)
    sc = bpy.data.scenes.new("card")
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.view_settings.view_transform = "Standard"
    sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "OBJECT"
    sc.render.resolution_x = W; sc.render.resolution_y = H; sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    cd = bpy.data.cameras.new("cardcam"); cd.type = "ORTHO"; cd.ortho_scale = W / 100
    cc = bpy.data.objects.new("cardcam", cd); sc.collection.objects.link(cc); sc.camera = cc
    cc.location = (W / 200, -H / 200, 10)
    font = None
    for fp in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(fp):
            font = bpy.data.fonts.load(fp); break
    def text(s, x, y, size=0.22, color=(0.12, 0.12, 0.16, 1)):
        cu = bpy.data.curves.new("t", "FONT"); cu.body = s; cu.size = size
        if font:
            cu.font = font
        o = bpy.data.objects.new("t", cu); o.location = (x / 100, -y / 100, 0); o.color = color
        sc.collection.objects.link(o)
    def rect(x, y, w, h, color):
        m = bpy.data.meshes.new("r")
        m.from_pydata([(0, 0, 0), (w / 100, 0, 0), (w / 100, -h / 100, 0), (0, -h / 100, 0)], [], [(0, 1, 2, 3)])
        o = bpy.data.objects.new("r", m); o.location = (x / 100, -y / 100, 0); o.color = color
        sc.collection.objects.link(o)
    ok = lambda b: "geçti" if b else "KALDI"
    C = report["checks"]
    y = 440
    text(f"{report['label']}  ({args.name})", 32, y, 0.36); y += 52
    text(f"Sınıf: {args.klass or '-'}   ·   ileri yön: {args.forward}   ·   kaynak: {os.path.basename(os.path.dirname(args.inp)) or os.path.basename(args.inp)}", 32, y); y += 36
    text(f"Boyut: {tr_num(size.x, 2)} × {tr_num(size.y, 2)} × {tr_num(size.z, 2)} m (Blender x × y × yükseklik)", 32, y); y += 36
    lod_s = " · ".join(f"LOD{l['level']} {tr_num(l['tris'])}" for l in report["lods"])
    text(f"Üçgen: {lod_s}   (bütçe {tr_num(budget_max)})", 32, y); y += 36
    tex_s = ", ".join(f"{v[0]}" for v in list(imgs.values())[:6]) if imgs else "-"
    text(f"Doku: {len(imgs)} harita ({tex_s} px)   ·   çarpışma kabuğu: {report['hull']['points']} nokta", 32, y); y += 36
    if report.get("parts_named"):
        text("Parçalar: " + ", ".join(p["name"] for p in report["parts_named"]), 32, y); y += 36
    if sockets_out:
        text("Soketler (Godot): " + "  ".join(f"{s['name']} ({', '.join(tr_num(v, 2) for v in s['pos_godot'])})" for s in sockets_out), 32, y, 0.19); y += 34
    y += 8
    text(f"1 doku {ok(C['1_texture']['pass'])}   2 poligon {ok(C['2_polygon']['pass'])}   3 kalınlık {ok(C['3_thickness']['pass'])} (ince alan %{tr_num(C['3_thickness']['thin_share'] * 100, 1)})   "
         f"4 bütünlük {ok(C['4_integrity']['pass'])}   5 ölçek {ok(C['5_scale_axis']['pass'])}", 32, y); y += 40
    pdir = report.get("palette_direction")
    text("64 px siluet · ikon", 32, 976, 0.18, (0.35, 0.35, 0.4, 1))
    if renders.get("thin"):
        text("Kalınlık haritası (ölçüt 3): kırmızı çubuk/diken, turuncu levha", 1196, 838, 0.17, (0.35, 0.35, 0.4, 1))
    if pdir:
        text(f"Palet yönü (dokudan {pdir['samples']} örnek, ort. ΔE {tr_num(pdir['mean_dE'], 1)}):", 620, 760)
        for k, e in enumerate(pdir["share"]):
            hx = e["hex"]
            col = tuple(srgb_to_lin(int(hx[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1,)
            rect(620, 790 + k * 32, 26, 26, col)
            text(f"{e['color']}  %{tr_num(e['share'] * 100, 0)}", 656, 812 + k * 32, 0.2)
    out_txt = os.path.abspath(path) + ".text.png"
    sc.render.filepath = out_txt
    bpy.ops.render.render(write_still=True, scene=sc.name)
    paste(load_px(out_txt), 0, 0)
    os.remove(out_txt)
    im = bpy.data.images.new("card", W, H, alpha=False)
    im.pixels.foreach_set(canvas.ravel())
    im.filepath_raw = os.path.abspath(path); im.file_format = "PNG"
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    im.save()

if args.card:
    make_card(args.card)
    report["card"] = args.card

rp = os.path.splitext(args.out)[0] + ".report.json"
json.dump(report, open(rp, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("PIPELINE_REPORT " + json.dumps({k: report[k] for k in ("name", "tris_in", "tris", "size_m", "parts", "pass_scripted")}, ensure_ascii=False))
for k, c in report["checks"].items():
    print(f"CHECK {k} {'PASS' if c['pass'] else 'FAIL'} {json.dumps({kk: vv for kk, vv in c.items() if kk not in ('pass', 'per_part', 'textures')}, ensure_ascii=False)}")
