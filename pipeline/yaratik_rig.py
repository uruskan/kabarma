"""
Kabarma yaratik hatti, KEMIKLI model (Tripo web/API dortayakli ya da baska plan) -> oyuna hazir GLB. Kredisiz, Blender basliksiz.

  blender -b --factory-startup --python-exit-code 1 -P pipeline/yaratik_rig.py -- \
      --in <rigli.glb> --out game/assets/models/homur/homur.glb --tris 8000 --tex 2048 \
      [--walk "preset:quadruped:walk"] [--dump progress/m4/yaratik4/homur/kemikler.json]

Adimlar:
  1. GLB'yi alir; kemikli ag (Armature altindaki tek mesh) ve iskelet bulunur.
  2. Ag --tris ucgene sadelestirilir (Decimate collapse, UV ve kemik agirliklari korunur: agirlik toplam 1'e yeniden normallenir,
     en fazla 4 kemik).
  3. Dokular --tex piksele kucultulur (en uzun kenar), JPEG olarak gomulur.
  4. Yurume klibi --walk adindan "walk"a adlandirilir (creatures.json clips: "glb:walk"). Koşu ve bekleme klipleri yoktur;
     PoseLibrary poz kliplerini kodla uretir (poses.json plani), kosu yurumenin hiz olcegidir.
  5. Cikti GLB (iskelet, klip, doku gomulu) ve <out>.report.json (ucgen once/sonra, doku boyutu, kemik sayisi, agirlik denetimi).
  6. --dump: her kemigin rest konumu (model uzayi, m), ebeveyni ve ag sinirlari: rig.bones esleme ve on yonu icin.
Tekrarlanabilir: ayni betik her tur icin; yalniz --in/--out/--tris degisir (ICERIK §3 sinif butcesi: kucuk 5000, orta 8000, buyuk 12000).
"""
import bpy
import sys
import os
import json
import argparse

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="inp", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--tris", type=int, default=8000)
ap.add_argument("--tex", type=int, default=2048)
ap.add_argument("--walk", default="preset:quadruped:walk")
ap.add_argument("--dump", default="")
ap.add_argument("--jpeg", type=int, default=88)
a = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=a.inp)

arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
# Blender'in glTF ice aktarmasi kemik gosterim sekli olarak "Icosphere" adli bir mesh de ekler: kemikli ag, kose grubu olan mesh'tir.
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
mesh = max((o for o in meshes if len(o.vertex_groups) > 0), key=lambda o: len(o.data.polygons), default=None)
if arm is None or mesh is None:
    print("HATA: iskelet ya da kemik agirlikli ag yok")
    sys.exit(1)
for o in meshes:
    if o is not mesh:
        bpy.data.objects.remove(o, do_unlink=True)

report = {"in": a.inp, "out": a.out, "bones": len(arm.data.bones)}

# ---- sadelestirme ----
def tri_count(o):
    dg = bpy.context.evaluated_depsgraph_get()
    e = o.evaluated_get(dg)
    m = e.to_mesh()
    n = sum(len(p.vertices) - 2 for p in m.polygons)
    e.to_mesh_clear()
    return n

# Armature modifier'i listenin basinda kalir; Decimate onun ONUNE alinip uygulanir (iskelet deformasyonu olmadan, rest ag).
bpy.context.view_layer.objects.active = mesh
for o in bpy.data.objects:
    o.select_set(o is mesh)
before = len(mesh.data.polygons)
# glTF ucgendir: ucgen sayisi = yuz sayisi
tris_before = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
report["tris_before"] = tris_before
if tris_before > a.tris:
    mod = mesh.modifiers.new("kabarma_decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = a.tris / tris_before
    mod.use_collapse_triangulate = True
    mod.delimit = {"UV"}
    while mesh.modifiers[0] != mod:
        bpy.ops.object.modifier_move_up(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)
tris_after = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
report["tris_after"] = tris_after

# ---- kemik agirliklari: en fazla 4, toplam 1 ----
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
unweighted = 0
for v in mesh.data.vertices:
    if sum(g.weight for g in v.groups) < 0.99:
        unweighted += 1
report["unweighted_verts"] = unweighted
report["verts"] = len(mesh.data.vertices)

# ---- dokular ----
texs = {}
for img in bpy.data.images:
    if img.size[0] == 0:
        continue
    w, h = img.size
    longest = max(w, h)
    if longest > a.tex:
        k = a.tex / longest
        img.scale(max(1, int(w * k)), max(1, int(h * k)))
    texs[img.name] = list(img.size)
report["textures"] = texs

# ---- klip adi ----
clips = []
for act in bpy.data.actions:
    if a.walk and act.name == a.walk:
        act.name = "walk"
    clips.append(act.name)
report["clips"] = clips
if arm.animation_data is None:
    arm.animation_data_create()
if bpy.data.actions.get("walk") is not None and arm.animation_data.action is None:
    arm.animation_data.action = bpy.data.actions["walk"]

# ---- kemik dokumu ----
if a.dump:
    inv = arm.matrix_world
    bones = []
    for b in arm.data.bones:
        h = inv @ b.head_local
        t = inv @ b.tail_local
        bones.append({"name": b.name, "parent": b.parent.name if b.parent else "", "head": [round(h.x, 4), round(h.y, 4), round(h.z, 4)],
                      "tail": [round(t.x, 4), round(t.y, 4), round(t.z, 4)]})
    corners = [inv @ v for v in [mesh.matrix_world @ __import__("mathutils").Vector(c) for c in mesh.bound_box]]
    lo = [min(c[i] for c in corners) for i in range(3)]
    hi = [max(c[i] for c in corners) for i in range(3)]
    os.makedirs(os.path.dirname(os.path.abspath(a.dump)), exist_ok=True)
    json.dump({"bones": bones, "bbox_min": [round(x, 4) for x in lo], "bbox_max": [round(x, 4) for x in hi],
               "_": "Blender uzayi (+Z yukari); glTF'e cikista Y-yukari olur: gX=bX, gY=bZ, gZ=-bY"}, open(a.dump, "w"), indent=1)

# ---- disa aktarim ----
os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=a.out, export_format="GLB", export_image_format="JPEG", export_jpeg_quality=a.jpeg,
                          export_animations=True, export_skins=True, export_yup=True, use_selection=False)
report["size_kb"] = os.path.getsize(a.out) // 1024
json.dump(report, open(a.out + ".report.json", "w"), indent=1)
print("YARATIK_RIG", json.dumps(report))
