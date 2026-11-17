"""
Stil foyu (URETIM.md §4): altin varliklari ayni isikta, gercek boylariyla, 1,6 m'lik insan olcegi yaninda tek goruntude toplar.
blender -b --factory-startup -P pipeline/style_sheet.py -- --out docs/stil/altin_varliklar.png
"""
import bpy, sys, os, math, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser(); ap.add_argument("--out", required=True); args = ap.parse_args(argv)
root = os.path.join(os.path.dirname(__file__), "..", "game", "assets", "models")
ITEMS = [  # (dosya, etiket) - URETIM §4 sirasi: karakter, iki yaratik, iki bitki, kaya, iki makine, arac, yapi, esya, kabarik urun
    ("player/player.glb", "Oyuncu"), ("zipzip/zipzip.glb", "Zıpzıp"), ("pofuduk/pofuduk.glb", "Pofuduk"),
    ("agac/agac.glb", "Çayır ağacı"), ("salgam/salgam.glb", "Şalgam"), ("kaya/kaya.glb", "Kaya"),
    ("tezgah/tezgah.glb", "Tezgâh"), ("tas_firin/tas_firin.glb", "Taş Fırın"), ("el_arabasi/el_arabasi.glb", "El Arabası"),
    ("build/wood/duvar.glb", "Ahşap duvar"), ("kova/kova.glb", "Sulama kovası"), ("ocak/ocak.glb", "Ocak"),
    ("kapsul/kapsul.glb", "İniş kapsülü"), ("paketleme/paketleme.glb", "Paketleme"), ("capa/capa.glb", "Çapa"), ("gidak/gidak.glb", "Gıdak"),
]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
x = 0.0
placed = []
for rel, label in ITEMS:
    path = os.path.join(root, rel)
    if not os.path.exists(path):
        print("SKIP", rel); continue
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    bpy.context.view_layer.update()
    meshes = [o for o in new if o.type == "MESH"]
    if not meshes: continue
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    if rel.startswith("gidak/"):  # oyunda 0,75 m'ye olceklenir
        k = 0.75 / max(0.01, mx.z - mn.z)
        for r in roots: r.scale *= k
        bpy.context.view_layer.update()
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    w = mx.x - mn.x
    for r in roots:
        r.location += Vector((x - mn.x, -(mn.y + mx.y) / 2, -mn.z))
    # etiket
    bpy.ops.object.text_add(location=(x + w / 2, -1.6, 0.05), rotation=(math.radians(90), 0, 0))
    t = bpy.context.object; t.data.body = label; t.data.size = 0.32; t.data.align_x = "CENTER"
    placed.append((label, x, w, mx.z - mn.z))
    x += w + 0.6
# insan olcegi: 1,6 m'lik cubuk
bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=1.6, location=(-0.6, 0, 0.8))
bpy.ops.object.text_add(location=(-0.6, -1.6, 0.05), rotation=(math.radians(90), 0, 0)); t = bpy.context.object; t.data.body = "1,6 m"; t.data.size = 0.28; t.data.align_x = "CENTER"
bpy.ops.mesh.primitive_plane_add(size=1, location=(x / 2, 0, 0)); g = bpy.context.object; g.scale = (x + 4, 6, 1)

scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 3600; scene.render.resolution_y = 760
cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = x + 1.2
cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
cam.location = (x / 2 - 0.6, -30, 4.6)
cam.rotation_euler = (math.radians(84), 0, 0)
scene.render.filepath = os.path.abspath(args.out)
os.makedirs(os.path.dirname(scene.render.filepath), exist_ok=True)
bpy.ops.render.render(write_still=True)
for p in placed: print("PLACED", p)
