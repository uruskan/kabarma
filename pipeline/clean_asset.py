"""
Kabarma uretim hatti, adim A.4-A.5 (URETIM.md §2.2): AI modelini oyuna hazirlar.

Blender basliksiz calisir:
  blender -b --factory-startup --python-exit-code 1 -P pipeline/clean_asset.py -- \
      --in raw.glb --out game/assets/models/x/x.glb --name x --height 1.0 --tris 2000 [--rig] [--sets altin_cayir,neutral,tones]

Yaptiklari:
  1. GLB'yi alir, mesh'leri birlestirir (--rig ise iskeleti ve agirliklari korur).
  2. Sinif butcesine sadelestirir (Decimate, collapse). --rig'de de calisir; agirliklar korunur.
  3. Dokuyu kose rengine cevirir: her yuzde dokudan 7 nokta orneklenir, ortalamasi palet rengine (CIELAB, en yakin) oturtulur,
     ardindan komsu yuzlerle cogunluk suzgeci (benekleri temizler). Doku ve malzemeler atilir.
  4. Olcek: boyu --height metreye getirir; taban y=0, ortasi orijinde.
  5. Yumusak golgeleme, aci ile sert kenar.
  6. Denetimler (SANAT-SES §9, 1-5) ve 4 acidan + 64 px siluet goruntusu; JSON rapor.
  7. GLB disa aktarir (kose rengi COLOR_0, doku yok).
"""
import bpy, bmesh, sys, json, math, os, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="inp", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--name", required=True)
ap.add_argument("--height", type=float, required=True)
ap.add_argument("--tris", type=int, required=True)
ap.add_argument("--rig", action="store_true")
ap.add_argument("--sets", default="altin_cayir,neutral,tones")
ap.add_argument("--palette", default=os.path.join(os.path.dirname(__file__), "..", "game", "data", "palette.json"))
ap.add_argument("--renders", default="")
ap.add_argument("--smooth_passes", type=int, default=2)
args = ap.parse_args(argv)

report = {"name": args.name, "input": args.inp, "checks": {}}

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
        rgb = tuple(int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4))
        allowed.append((f"{s}.{k}", rgb, to_lab(rgb)))

def nearest(rgb):
    lab = to_lab(rgb)
    best = min(range(len(allowed)), key=lambda i: de(lab, allowed[i][2]))
    return best, de(lab, allowed[best][2])

# ---------------- sahne ----------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.inp)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
arm = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)
if not meshes:
    raise SystemExit("mesh yok")

# Dokuyu bul (birlestirmeden once; malzemeler korunur)
def base_image(mat):
    if not mat or not mat.use_nodes:
        return None, None
    for n in mat.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            inp = n.inputs.get("Base Color")
            if inp and inp.is_linked:
                src = inp.links[0].from_node
                # araya Mix/Multiply girebilir; ilk Image Texture'u ara
                stack = [src]
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

bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
if not args.rig:
    # Iskelet yoksa donusumleri uygula; ebeveynden kopar.
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != obj and o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)

me = obj.data
tris_in = sum(len(p.vertices) - 2 for p in me.polygons)
report["tris_in"] = tris_in

# Kaynak modeller doku dikislerinde ayrik gelir: konumu ayni koseleri kaynak (UV kose basina korunur).
bm = bmesh.new(); bm.from_mesh(me)
ext = max((max(v.co[i] for v in bm.verts) - min(v.co[i] for v in bm.verts)) for i in range(3))
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=ext * 1e-4)
bm.to_mesh(me); bm.free(); me.update()
report["welded_verts"] = before - len(me.vertices)

# Sadelestirme
if tris_in > args.tris:
    mod = obj.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = max(0.01, args.tris / tris_in * 0.97)
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
me = obj.data
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------- dokudan kose rengi ----------------
img_cache = {}
def pixels_of(img):
    if img.name in img_cache:
        return img_cache[img.name]
    w, h = img.size
    px = list(img.pixels[:])  # 8 bit sRGB goruntulerde degerler sRGB kodlu
    img_cache[img.name] = (w, h, px)
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
        rgb = tuple(min(1, max(0, x)) for x in flat)
        rgb = tuple((x ** (1 / 2.2)) for x in rgb)  # dogrusal -> yaklasik sRGB
    else:
        rgb = (0.6, 0.6, 0.6)
    face_rgb.append(rgb)

labels, dists = [], []
for rgb in face_rgb:
    i, d = nearest(rgb)
    labels.append(i); dists.append(d)
report["source_mean_dE"] = round(sum(dists) / max(1, len(dists)), 2)
report["source_within_dE8"] = round(sum(1 for d in dists if d < 8) / max(1, len(dists)), 3)

# Cogunluk suzgeci: tek basina kalan benekleri komsulara katar.
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

# Kose rengi katmani (yuz basina tek renk: koseler yuz rengini alir)
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

# Doku ve malzemeleri at; kose rengini gosteren tek malzeme
me.materials.clear()
mat = bpy.data.materials.new(args.name + "_vc")
mat.use_nodes = True
nt = mat.node_tree
bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.85
if "Metallic" in bsdf.inputs:
    bsdf.inputs["Metallic"].default_value = 0.0
me.materials.append(mat)
for img in list(bpy.data.images):
    bpy.data.images.remove(img)

# ---------------- olcek ve eksen ----------------
def world_bbox(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx

root = arm if (args.rig and arm) else obj
bpy.context.view_layer.update()
mn, mx = world_bbox(obj)
h = mx.z - mn.z
s = args.height / h if h > 1e-6 else 1.0
root.scale = root.scale * s
bpy.context.view_layer.update()
mn, mx = world_bbox(obj)
root.location -= Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
bpy.context.view_layer.update()
if not args.rig:
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Yumusak golgeleme, sert kenar yalnizca kirilan yerde
bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
bpy.context.view_layer.objects.active = obj
try:
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
except Exception:
    bpy.ops.object.shade_smooth()

# ---------------- denetimler ----------------
me = obj.data
bpy.context.view_layer.update()
mn, mx = world_bbox(obj)
size = mx - mn
tris = sum(len(p.vertices) - 2 for p in me.polygons)
bm = bmesh.new(); bm.from_mesh(me)
nonman = sum(1 for e in bm.edges if not e.is_manifold)
boundary = sum(1 for e in bm.edges if e.is_boundary)
# ayrik parcalar
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
longest = max(size.x, size.y, size.z)
thin = []
for comp in parts:
    xs = [p.co.x for p in comp]; ys = [p.co.y for p in comp]; zs = [p.co.z for p in comp]
    dims = sorted([max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)])
    thin.append(dims[0] / longest if longest > 0 else 0)
# ters yuz tahmini: yuz normali ile (yuz merkezi - nesne merkezi) cogunlukla ayni yonde olmali
center = sum((v.co for v in bm.verts), Vector()) / max(1, len(bm.verts))
inward = sum(1 for f in bm.faces if f.normal.dot(f.calc_center_median() - center) < -0.2 * f.calc_area() ** 0.5)
bm.free()
budget_ok = tris <= args.tris
report["tris"] = tris
report["size_m"] = [round(size.x, 3), round(size.y, 3), round(size.z, 3)]
report["parts"] = len(parts)
report["checks"] = {
    "1_palette": {"pass": True, "note": f"butun yuzler palete oturtuldu; kaynakta ortalama dE {report['source_mean_dE']}, dE<8 orani {report['source_within_dE8']}"},
    "2_polygon": {"pass": budget_ok, "value": tris, "budget": args.tris},
    "3_thickness": {"pass": min(thin) >= 0.06 if thin else True, "min_ratio": round(min(thin), 3) if thin else None, "note": "parca basina en dar boyut / en uzun kenar"},
    "4_integrity": {"pass": nonman == 0 or (boundary == 0 and inward < len(me.polygons) * 0.05), "non_manifold_edges": nonman, "boundary_edges": boundary, "inward_faces": inward, "loose_parts": len(parts)},
    "5_scale_axis": {"pass": abs(size.z - args.height) < 0.02 and abs(mn.z) < 0.01 and abs((mn.x + mx.x) / 2) < 0.02, "height": round(size.z, 3), "target": args.height},
}

# ---------------- goruntuler (olcut 6-8 icin) ----------------
if args.renders:
    args.renders = os.path.abspath(args.renders)
    os.makedirs(args.renders, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "VERTEX"
    scene.render.resolution_x = 512; scene.render.resolution_y = 512
    scene.render.film_transparent = True
    world = bpy.data.worlds.new("w"); scene.world = world
    cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    ctr = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2))
    cam_data.ortho_scale = longest * 1.35
    for i, ang in enumerate([20, 110, 200, 290]):
        a = math.radians(ang)
        d = longest * 3
        cam.location = ctr + Vector((math.sin(a) * d, -math.cos(a) * d, d * 0.45))
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(args.renders, f"{args.name}_{i}.png")
        bpy.ops.render.render(write_still=True)
    # 64 px siluet
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.02, 0.02, 0.02)
    scene.display.shading.light = "FLAT"
    scene.render.resolution_x = 64; scene.render.resolution_y = 64
    a = math.radians(20); d = longest * 3
    cam.location = ctr + Vector((math.sin(a) * d, -math.cos(a) * d, d * 0.45))
    cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(args.renders, f"{args.name}_silhouette64.png")
    bpy.ops.render.render(write_still=True)

# ---------------- disa aktar ----------------
os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
kw = dict(filepath=args.out, export_format="GLB", use_selection=False, export_yup=True, export_texcoords=False, export_normals=True)
try:
    bpy.ops.export_scene.gltf(**kw, export_vertex_color="ACTIVE", export_animations=args.rig, export_skins=args.rig)
except TypeError:
    bpy.ops.export_scene.gltf(**kw, export_colors=True, export_animations=args.rig, export_skins=args.rig)
report["out"] = args.out
report["pass_scripted"] = all(c["pass"] for c in report["checks"].values())
rp = os.path.splitext(args.out)[0] + ".report.json"
json.dump(report, open(rp, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("PIPELINE_REPORT " + json.dumps({k: report[k] for k in ("name", "tris_in", "tris", "size_m", "parts", "pass_scripted")}, ensure_ascii=False))
for k, c in report["checks"].items():
    print(f"CHECK {k} {'PASS' if c['pass'] else 'FAIL'} {json.dumps({kk: vv for kk, vv in c.items() if kk != 'pass'}, ensure_ascii=False)}")
