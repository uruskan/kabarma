"""Kutuphane varligini (F:\\Assets) oyuna alir: secili nesneler -> tek mesh, sadelestirme, olcek, taban orijini, 1K doku, GLB.

Kaynak dosyaya dokunmaz (yalniz okur). Cikti ve kucultulmus dokular --out yanina yazilir.

blender -b --factory-startup -P pipeline/lib_import.py -- --in kaynak.(glb|gltf|fbx|blend) --out game/assets/models/lib/x.glb \
    --height 1.2 [--objects a,b] [--up y|-y|x|-x|z] [--tris 5000] [--tex 1024] [--yaw 0] [--preview out.png]
"""
import bpy, bmesh, sys, os, argparse, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="inp", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--height", type=float, required=True, help="hedef yukseklik (m)")
ap.add_argument("--objects", default="", help="alinacak mesh nesneleri (bos: hepsi)")
ap.add_argument("--up", default="z", help="kaynakta yukari bakacak eksen (Blender ekseni)")
ap.add_argument("--tris", type=int, default=5000)
ap.add_argument("--tex", type=int, default=1024)
ap.add_argument("--yaw", type=float, default=0.0)
ap.add_argument("--preview", default="")
a = ap.parse_args(argv)

src = os.path.abspath(a.inp)
if src.endswith(".blend"):
    bpy.ops.wm.open_mainfile(filepath=src, load_ui=False, use_scripts=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if src.endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=src)
    else:
        bpy.ops.import_scene.gltf(filepath=src)

want = [s for s in a.objects.split(",") if s]
meshes = [o for o in bpy.data.objects if o.type == "MESH" and (not want or o.name in want)]
if not meshes:
    sys.exit("nesne bulunamadi: " + ",".join(o.name for o in bpy.data.objects))
for o in list(bpy.data.objects):
    if o not in meshes:
        bpy.data.objects.remove(o, do_unlink=True)

# tek mesh, donusumler uygulanmis
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    if o.name not in bpy.context.scene.collection.all_objects:
        bpy.context.scene.collection.objects.link(o)
    o.hide_set(False); o.hide_viewport = False
    o.select_set(True)
for o in meshes:
    o.parent = None if o.parent is None else o.parent
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
o = bpy.context.view_layer.objects.active
o.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

up = {"z": Matrix.Identity(4), "-z": Matrix.Rotation(math.pi, 4, "X"),
      "y": Matrix.Rotation(math.pi / 2, 4, "X"), "-y": Matrix.Rotation(-math.pi / 2, 4, "X"),
      "x": Matrix.Rotation(-math.pi / 2, 4, "Y"), "-x": Matrix.Rotation(math.pi / 2, 4, "Y")}[a.up]
o.data.transform(Matrix.Rotation(math.radians(a.yaw), 4, "Z") @ up)

tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
if tris > a.tris:
    m = o.modifiers.new("dec", "DECIMATE")
    m.ratio = a.tris / tris
    m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=m.name)
# eski ozel normaller sadelestirmeden sonra bozulur: duzgun golgelendirme
for p in o.data.polygons:
    p.use_smooth = True
try:
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))
except Exception:
    pass

vs = [v.co for v in o.data.vertices]
mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
k = a.height / max(1e-6, mx.z - mn.z)
c = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
o.data.transform(Matrix.Scale(k, 4) @ Matrix.Translation(-c))
o.location = (0, 0, 0)
name = os.path.splitext(os.path.basename(a.out))[0]
o.name = o.data.name = name

# dokular: --tex kenarina kucult, cikti klasorune PNG yaz (kaynak dosyaya yazilmaz)
outdir = os.path.dirname(os.path.abspath(a.out))
texdir = os.path.join(bpy.app.tempdir or outdir, "lib_tex_" + name)
os.makedirs(texdir, exist_ok=True)
for mat in o.data.materials:
    if not mat or not mat.use_nodes:
        continue
    mat.name = name + "_" + mat.name.split(".")[0]
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            img = n.image
            if img.get("_lib_done"):
                continue
            w, h = img.size
            if w == 0:
                continue
            if max(w, h) > a.tex:
                s = a.tex / max(w, h)
                img.scale(max(1, int(w * s)), max(1, int(h * s)))
            p = os.path.join(texdir, name + "_" + bpy.path.clean_name(img.name) + ".png")
            img.filepath_raw = p
            img.file_format = "PNG"
            img.save()
            img["_lib_done"] = 1
            print("TEX", img.name, img.size[:], img.colorspace_settings.name, flush=True)

os.makedirs(outdir, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
o.select_set(True)
bpy.ops.export_scene.gltf(filepath=os.path.abspath(a.out), export_format="GLB", use_selection=True,
                          export_image_format="AUTO", export_apply=True)
tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
d = o.dimensions
print(f"LIB {a.out} tris={tris} size=({d.x:.2f},{d.y:.2f},{d.z:.2f}) mats={[m.name for m in o.data.materials]}", flush=True)
