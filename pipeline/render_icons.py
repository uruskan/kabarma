"""Esya ikonlari: 3B modelden tek tip ikon (SANAT-SES 8.1 "ikonlar modelden cekilir").

Her ikon ayni isik, ayni aci, seffaf arka plan, hafif temas golgesi; Cycles + denoise, 256 px.
Isik: sicak anahtar (sol ust on), soguk dolgu (sag), arka kenar isigi, notr gri dunya.
Aci: 3/4 onden, ~28 derece yukaridan, 40 mm; cerceve modelin kose noktalarina gore sikistirilir (pay %9).

Kullanim (Blender 5.3):
  blender -b --factory-startup --python pipeline/render_icons.py -- --manifest pipeline/icons_models.json --out game/assets/icons/items [--only id1,id2] [--size 256]

Manifest: [{"id": "stone", "glb": "game/assets/models/kaya/kaya.glb", "yaw": 0, "elev": 28, "pad": 0.09}, ...]
yaw: modelin kendi etrafinda donusu (derece); elev: kamera yukseklik acisi; pad: kenar payi.
"""
import bpy, sys, os, json, math
from mathutils import Vector, Matrix
from bpy_extras.object_utils import world_to_camera_view

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default

manifest = json.load(open(arg("--manifest"), encoding="utf-8-sig"))
out_dir = os.path.abspath(arg("--out", "game/assets/icons/items"))
only = set(filter(None, (arg("--only", "") or "").split(",")))
size = int(arg("--size", "256"))
samples = int(arg("--samples", "96"))
os.makedirs(out_dir, exist_ok=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            if any(d.type == kind for d in prefs.devices):
                for d in prefs.devices:
                    d.use = d.type == kind
                sc.cycles.device = "GPU"
                break
        except Exception:
            pass
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = size * 2  # 2x ciz, sonra kucult (kenar yumusak)
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "AgX"
    try:
        sc.view_settings.look = "AgX - Medium High Contrast"
    except Exception:
        pass
    sc.view_settings.exposure = 0.35
    w = bpy.data.worlds.new("w"); sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.55, 0.56, 0.58, 1); bg.inputs[1].default_value = 0.55
    return sc


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    objs = [o for o in bpy.data.objects if o not in before]
    # iskelet/bos dugumleri birak, yalniz gorunur meshler
    meshes = [o for o in objs if o.type == "MESH"]
    for o in objs:
        if o.type not in ("MESH", "EMPTY", "ARMATURE"):
            bpy.data.objects.remove(o, do_unlink=True)
    return objs, meshes


def world_points(meshes, cap=6000):
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        step = max(1, len(me.vertices) // max(1, cap // max(1, len(meshes))))
        mw = o.matrix_world
        pts += [mw @ me.vertices[i].co for i in range(0, len(me.vertices), step)]
        ev.to_mesh_clear()
    return pts


def area_light(name, loc, target, energy, size_m, color):
    ld = bpy.data.lights.new(name, "AREA"); ld.energy = energy; ld.size = size_m; ld.color = color
    lo = bpy.data.objects.new(name, ld); bpy.context.scene.collection.objects.link(lo)
    lo.location = loc
    lo.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
    return lo


def render_one(e):
    sc = reset()
    objs, meshes = import_glb(e["glb"])
    if not meshes:
        print("ICON_SKIP", e["id"], "mesh yok"); return False
    # kok donus: yaw
    root = bpy.data.objects.new("root", None); sc.collection.objects.link(root)
    for o in objs:
        if o.parent is None:
            o.parent = root
    root.rotation_euler = (math.radians(e.get("pitch", 0)), math.radians(e.get("roll", 0)), math.radians(e.get("yaw", 0)))
    bpy.context.view_layer.update()
    pts = world_points(meshes)
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    ctr = (mn + mx) / 2; rad = max((p - ctr).length for p in pts) or 1.0

    # golge yakalayici zemin
    bpy.ops.mesh.primitive_plane_add(size=rad * 30, location=(ctr.x, ctr.y, mn.z))
    floor = bpy.context.active_object; floor.is_shadow_catcher = True

    # kamera: 3/4 onden (-Y on, sag tarafa 35 derece), elev derece yukari
    cd = bpy.data.cameras.new("cam"); cd.lens = 40; cd.sensor_width = 36
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    az = math.radians(e.get("az", -35)); el = math.radians(e.get("elev", 28))
    dirv = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    dist = rad * 3.2
    cam.location = ctr + dirv * dist
    cam.rotation_euler = (-dirv).to_track_quat("-Z", "Y").to_euler()
    cd.clip_start = rad * 0.05; cd.clip_end = rad * 50
    # cerceveyi sikistir: projeksiyon kutusunu ortala ve pay disinda doldur
    pad = e.get("pad", 0.09)
    for _ in range(6):
        bpy.context.view_layer.update()
        pr = [world_to_camera_view(sc, cam, p) for p in pts]
        x0 = min(p.x for p in pr); x1 = max(p.x for p in pr); y0 = min(p.y for p in pr); y1 = max(p.y for p in pr)
        ext = max(x1 - x0, y1 - y0)
        cd.shift_x += ((x0 + x1) / 2 - 0.5)
        cd.shift_y += ((y0 + y1) / 2 - 0.5)
        k = ext / (1 - 2 * pad)
        cd.lens /= k

    # isik duzeni (kameraya gore sabit: her ikonda ayni)
    right = (cam.matrix_world.to_3x3() @ Vector((1, 0, 0))).normalized()
    up = Vector((0, 0, 1))
    fwd = -dirv
    key = ctr + (-right * 1.1 + up * 1.4 - fwd * 0.9).normalized() * rad * 5
    fill = ctr + (right * 1.3 + up * 0.4 - fwd * 0.8).normalized() * rad * 5
    rim = ctr + (right * 0.4 + up * 1.0 + fwd * 1.4).normalized() * rad * 5
    s = rad * rad
    area_light("key", key, ctr, 900 * s, rad * 3.0, (1.0, 0.95, 0.88))
    area_light("fill", fill, ctr, 260 * s, rad * 4.0, (0.86, 0.92, 1.0))
    area_light("rim", rim, ctr, 650 * s, rad * 2.0, (1.0, 1.0, 1.0))

    raw = os.path.join(out_dir, f"_{e['id']}_raw.png")
    sc.render.filepath = raw
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(raw)
    img.scale(size, size)
    final = os.path.join(out_dir, f"{e['id']}.png")
    img.filepath_raw = final; img.file_format = "PNG"; img.save()
    os.remove(raw)
    print("ICON_OK", e["id"], final)
    return True


ok = 0
for e in manifest:
    if only and e["id"] not in only:
        continue
    try:
        ok += bool(render_one(e))
    except Exception as ex:
        print("ICON_FAIL", e["id"], ex)
print("ICON_DONE", ok)
