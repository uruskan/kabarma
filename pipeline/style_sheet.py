"""
Stil foyu (URETIM.md §4, docs/stil/README.md): altin varliklari gercek boylariyla, 1,6 m'lik insan olcegi yaninda,
ayni isikta yan yana; altinda palet (game/data/palette.json, renkler buradan okunur).

  blender -b --factory-startup -P pipeline/style_sheet.py -- --out docs/stil/altin_varliklar.png \
      --lights docs/stil/altin_varliklar_4isik.png

--out    : studyo isigi (Workbench), varliklar + palet seridi.
--lights : SANAT-SES §2'nin dort gun saati (sabah, ogle, gun batimi, Kabarma gecesi), EEVEE, dort satir.
           SANAT-SES §9 olcut 7 (dil uyumu: dort isik kosulunda yan yana) icin.
"""
import bpy, sys, os, math, argparse, json
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HERE = os.path.dirname(os.path.abspath(__file__))
ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
ap.add_argument("--lights", default="")
ap.add_argument("--palette", default=os.path.join(HERE, "..", "game", "data", "palette.json"))
ap.add_argument("--width", type=int, default=3600)
args = ap.parse_args(argv)
root = os.path.join(HERE, "..", "game", "assets", "models")
PAL = json.load(open(args.palette, encoding="utf-8"))

def hex_lin(hx):
    def f(c):
        c = int(c, 16) / 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (f(hx[0:2]), f(hx[2:4]), f(hx[4:6]))

ITEMS = [  # URETIM §4 sirasi: karakter, iki yaratik, iki bitki, kaya, iki makine, arac, yapi, esya, kabarik urun; sonra ekler
    ("player/player.glb", "Oyuncu"), ("zipzip/zipzip.glb", "Zıpzıp"), ("pofuduk/pofuduk.glb", "Pofuduk"),
    ("agac/agac.glb", "Çayır ağacı"), ("salgam/salgam.glb", "Şalgam"), ("kaya/kaya.glb", "Kaya"),
    ("tezgah/tezgah.glb", "Tezgâh"), ("tas_firin/tas_firin.glb", "Taş Fırın"), ("el_arabasi/el_arabasi.glb", "El Arabası"),
    ("build/wood/duvar.glb", "Ahşap duvar"), ("kova/kova.glb", "Sulama kovası"), ("ocak/ocak.glb", "Ocak"),
    ("kapsul/kapsul.glb", "İniş kapsülü"), ("paketleme/paketleme.glb", "Paketleme"), ("capa/capa.glb", "Çapa"), ("gidak/gidak.glb", "Gıdak"),
]

# SANAT-SES §2 tablosu, Altin Cayir'in dort gun saati:
# (ad, gunes/ay yuksekligi, yonu (0 = dogu), isik rengi, gokyuzu tepe, ufuk, sis (ortam isigi), enerji, ortam gucu)
HOURS = [
    ("Sabah", 12, 0, "FFE2B8", "A3CFE6", "F6D7B0", "F3D9A4", 3.0, 0.55),
    ("Öğle", 62, 90, "FFF4DE", "8CC8E8", "F3D9A4", "F3D9A4", 4.0, 0.6),
    ("Gün batımı", 4, 180, "FF9A5C", "6E79B0", "F58A5B", "F2A878", 3.0, 0.5),
    ("Kabarma gecesi", 35, 140, "A9B8E8", "1A2238", "5FF0C8", "2A3358", 1.2, 0.6),
]

bpy.ops.wm.read_factory_settings(use_empty=True)  # once bosalt: sonra yuklenen yazi tipi silinmesin
font = None
for fp in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
    if os.path.exists(fp):
        font = bpy.data.fonts.load(fp); break

def text(scene, s, loc, size, color=(0.1, 0.1, 0.12, 1), rot=(math.radians(90), 0, 0)):
    cu = bpy.data.curves.new("t", "FONT"); cu.body = s; cu.size = size; cu.align_x = "CENTER"
    if font:
        cu.font = font
    o = bpy.data.objects.new("t", cu); o.location = loc; o.rotation_euler = rot; o.color = color
    m = bpy.data.materials.new("tm"); m.diffuse_color = color
    m.use_nodes = True
    bs = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"); bs.inputs["Base Color"].default_value = color
    cu.materials.append(m)
    scene.collection.objects.link(o)
    return o

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
    # glTF iskeletleri kemik gosterimi icin "Icosphere" kopyalar: boyu bozmasin, gorunmesin
    shapes = {pb.custom_shape for o in new if o.type == "ARMATURE" for pb in o.pose.bones if pb.custom_shape}
    for s in shapes:
        s.hide_render = True; s.hide_viewport = True
    meshes = [o for o in new if o.type == "MESH" and o not in shapes]
    if not meshes:
        continue
    def bbox():
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
                Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))
    mn, mx = bbox()
    if rel.startswith("gidak/"):  # oyunda 0,75 m'ye olceklenir
        k = 0.75 / max(0.01, mx.z - mn.z)
        for r in roots:
            r.scale *= k
        bpy.context.view_layer.update()
        mn, mx = bbox()
    w = mx.x - mn.x
    for r in roots:
        r.location += Vector((x - mn.x, -(mn.y + mx.y) / 2, -mn.z))
    placed.append((label, x, w, mx.z - mn.z))
    x += w + 0.6
# insan olcegi: 1,6 m'lik cubuk (adlar sonra 2B yazi katmaninda, zeminin altinda)
bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=1.6, location=(-0.6, 0, 0.8))
bpy.ops.mesh.primitive_plane_add(size=1, location=(x / 2, 0, 0)); ground = bpy.context.object; ground.scale = (x + 4, 6, 1)
gm = bpy.data.materials.new("zemin"); gm.use_nodes = True
gb = next(n for n in gm.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
gb.inputs["Base Color"].default_value = hex_lin(PAL["sets"]["altin_cayir"]["fog"]) + (1,)
gb.inputs["Roughness"].default_value = 0.9
gm.diffuse_color = hex_lin(PAL["sets"]["altin_cayir"]["fog"]) + (1,)  # Workbench bu rengi kullanir
ground.data.materials.append(gm)
BG = (0.93, 0.91, 0.86)
studio_world = bpy.data.worlds.new("studyo"); studio_world.color = tuple(c ** 2.2 for c in BG)  # Workbench arka plani
scene.world = studio_world

W = args.width
H_ROW = 760
cam_data = bpy.data.cameras.new("cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = x + 1.2
cam = bpy.data.objects.new("cam", cam_data); scene.collection.objects.link(cam); scene.camera = cam
cam.location = (x / 2 - 0.6, -30, 5.6)  # bakis cizgisi y=0'da ~2,4 m: zemin alt ucte, agac sigar
cam.rotation_euler = (math.radians(84), 0, 0)
scene.view_settings.view_transform = "Standard"
bpy.context.view_layer.update()
from bpy_extras.object_utils import world_to_camera_view

def name_labels(w, h, size, col=(0.12, 0.12, 0.16, 1)):
    """Her varligin adi zemin cizgisinin hemen altinda, varligin ortasinda (piksel, sol ust)."""
    scene.render.resolution_x = w; scene.render.resolution_y = h
    out = []
    for lab, px0, pw, ph in [("1,6 m", -0.6, 0.0, 1.6)] + placed:
        p = world_to_camera_view(scene, cam, Vector((px0 + pw / 2, -3.2, 0.0)))
        out.append((lab, p.x * w, (1 - p.y) * h + size * 100 * 1.2, size, col, "CENTER"))
    return out

def render(path, w, h):
    scene.render.resolution_x = w; scene.render.resolution_y = h
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = os.path.abspath(path)
    os.makedirs(os.path.dirname(scene.render.filepath), exist_ok=True)
    bpy.ops.render.render(write_still=True)

import numpy as np
def load_px(p):
    im = bpy.data.images.load(p, check_existing=False)
    w, h = im.size
    a = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)

def save_px(a, path):
    h, w = a.shape[:2]
    im = bpy.data.images.new("o", w, h, alpha=False)
    im.pixels.foreach_set(a.ravel())
    im.filepath_raw = os.path.abspath(path); im.file_format = "PNG"; im.save()
    bpy.data.images.remove(im)

def text_layer(w, h, labels):
    """Seffaf yazi katmani: [(yazi, x, y, boy)] piksel koordinatinda (sol ust)."""
    sc = bpy.data.scenes.new("yazi"); sc.render.engine = "BLENDER_WORKBENCH"
    sc.view_settings.view_transform = "Standard"
    sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "OBJECT"
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    cd = bpy.data.cameras.new("yc"); cd.type = "ORTHO"; cd.ortho_scale = max(w, h) / 100
    cc = bpy.data.objects.new("yc", cd); sc.collection.objects.link(cc); sc.camera = cc
    cc.location = (w / 200, -h / 200, 10)
    for s, px, py, size, col, *al in labels:
        cu = bpy.data.curves.new("t", "FONT"); cu.body = s; cu.size = size
        if al:
            cu.align_x = al[0]
        if font:
            cu.font = font
        o = bpy.data.objects.new("t", cu); o.location = (px / 100, -py / 100, 0); o.color = col
        sc.collection.objects.link(o)
    tmp = os.path.abspath(args.out) + ".yazi.png"
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True, scene=sc.name)
    a = load_px(tmp); os.remove(tmp)
    return a

def over(canvas, layer):
    al = layer[:, :, 3:4]
    canvas[:, :, :3] = layer[:, :, :3] * al + canvas[:, :, :3] * (1 - al)

def palette_strip(w):
    """palette.json'daki her renk: kare + ad + onaltilik. Ayri sahnede, seffaf Workbench."""
    sc = bpy.data.scenes.new("pal"); sc.render.engine = "BLENDER_WORKBENCH"
    sc.view_settings.view_transform = "Standard"
    sc.display.shading.light = "FLAT"; sc.display.shading.color_type = "OBJECT"
    sets = PAL["sets"]
    per_row = max(1, (w - 300) // 290)  # sigmayan takim alt satira kayar
    rows = sum(-(-len(c) // per_row) for c in sets.values()); h = 70 + rows * 120
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    cd = bpy.data.cameras.new("pc"); cd.type = "ORTHO"; cd.ortho_scale = w / 100
    cc = bpy.data.objects.new("pc", cd); sc.collection.objects.link(cc); sc.camera = cc
    cc.location = (w / 200, -h / 200, 10)
    flat = (0, 0, 0)
    def rect(px, py, pw, ph, col):
        m = bpy.data.meshes.new("r")
        m.from_pydata([(0, 0, 0), (pw / 100, 0, 0), (pw / 100, -ph / 100, 0), (0, -ph / 100, 0)], [], [(0, 1, 2, 3)])
        o = bpy.data.objects.new("r", m); o.location = (px / 100, -py / 100, 0); o.color = col + (1,)
        sc.collection.objects.link(o)
    def label(s, px, py, size=0.2, col=(0.12, 0.12, 0.16, 1)):
        cu = bpy.data.curves.new("t", "FONT"); cu.body = s; cu.size = size
        if font:
            cu.font = font
        o = bpy.data.objects.new("t", cu); o.location = (px / 100, -py / 100, 0); o.color = col
        sc.collection.objects.link(o)
    label("Palet (game/data/palette.json; SANAT-SES §2): renk yönü istemde verilir, yüzeye zorla oturtulmaz", 40, 46, 0.26)
    r = 0
    for sname, cols in sets.items():
        label(sname, 40, 70 + r * 120 + 62, 0.24)
        for i, (k, hx) in enumerate(cols.items()):
            if i and i % per_row == 0:
                r += 1
            y = 70 + r * 120
            px = 300 + (i % per_row) * 290
            rect(px, y + 10, 70, 70, hex_lin(hx))
            label(k, px + 80, y + 40, 0.19)
            label("#" + hx, px + 80, y + 70, 0.17, (0.35, 0.35, 0.4, 1))
        r += 1
    tmp =os.path.abspath(args.out) + ".pal.png"
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True, scene=sc.name)
    a = load_px(tmp); os.remove(tmp)
    return a

# 1) Studyo: Workbench, dokular, golge ve oyuk
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
ground.hide_render = True  # Workbench'te yataya yakin bakilan zemin koyu bir serit olur; studyoda zemin yok
tmp = os.path.abspath(args.out) + ".models.png"
render(tmp, W, H_ROW)
models = load_px(tmp); os.remove(tmp)
over(models, text_layer(W, H_ROW, name_labels(W, H_ROW, 0.3)))
ground.hide_render = False
strip = palette_strip(W)
bg = np.array([0.93, 0.91, 0.86, 1.0], np.float32)
canvas = np.tile(bg, (models.shape[0] + strip.shape[0], W, 1))
canvas[strip.shape[0]:, :, :] = models
al = strip[:, :, 3:4]
canvas[:strip.shape[0], :, :3] = strip[:, :, :3] * al + canvas[:strip.shape[0], :, :3] * (1 - al)
save_px(canvas, args.out)
print("STUDIO", args.out)

# 2) Dort gun saati: EEVEE, gunes/ay, gokyuzu gecisi (SANAT-SES §2)
if args.lights:
    scene.render.engine = "BLENDER_EEVEE"
    try:
        scene.eevee.taa_render_samples = 32
    except Exception:
        pass
    # Ortografik kamerada dunya her yerde tek renk gorunur: dunya yalniz ortam isigi (sis rengi) verir,
    # gokyuzu gecisi (tepe -> ufuk) seffaf goruntunun arkasina numpy ile cizilir.
    world = bpy.data.worlds.new("gok"); scene.world = world; world.use_nodes = True
    nt = world.node_tree; nt.nodes.clear()
    bgn = nt.nodes.new("ShaderNodeBackground"); out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(bgn.outputs[0], out.inputs[0])
    scene.render.film_transparent = True
    scene.render.image_settings.color_mode = "RGBA"
    sun = bpy.data.lights.new("gunes", "SUN"); sun.angle = math.radians(2)
    so = bpy.data.objects.new("gunes", sun); scene.collection.objects.link(so)
    rows = []
    for name, elev, az, light, top, horizon, fog, energy, amb in HOURS:
        sun.color = hex_lin(light); sun.energy = energy
        # yon: az 0 = dogu (+X), 90 = guney (-Y, kameranin oldugu yan), 180 = bati
        a = math.radians(az)
        d = Vector((math.cos(a), -math.sin(a), math.tan(math.radians(elev)))).normalized()
        so.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        bgn.inputs[0].default_value = hex_lin(fog) + (1,); bgn.inputs[1].default_value = amb
        tmp = os.path.abspath(args.lights) + f".{len(rows)}.png"
        render(tmp, W // 2, H_ROW // 2)
        a_img = load_px(tmp); os.remove(tmp)
        h = a_img.shape[0]
        # load_px satirlari alttan baslar: t=0 alt (ufuk), t=1 ust (tepe)
        t = np.linspace(0.0, 1.0, h, dtype=np.float32)[:, None, None]
        # PNG'den okunan degerler sRGB kodlu: gokyuzu de sRGB onaltilikla karisir
        srgb = lambda hx: np.array([int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)
        sky = srgb(horizon) * (1 - t) + srgb(top) * t
        img = np.empty_like(a_img); al = a_img[:, :, 3:4]
        img[:, :, :3] = a_img[:, :, :3] * al + sky * (1 - al)
        img[:, :, 3] = 1
        rows.append((name, img))
        print("LIGHT", name)
    # satirlar alt alta (ustte sabah), her satirin sol ustunde saat adi
    hh = sum(r[1].shape[0] for r in rows)
    canvas = np.zeros((hh, W // 2, 4), np.float32)
    y = hh
    for name, a_img in rows:
        h = a_img.shape[0]; y -= h
        canvas[y:y + h] = a_img
    canvas[:, :, 3] = 1
    labels = []
    for i, (name, a_img) in enumerate(rows):
        col = (0.95, 0.95, 0.92, 1) if name == "Kabarma gecesi" else (0.1, 0.1, 0.14, 1)
        labels.append((name + " (SANAT-SES §2)", 20, i * (H_ROW // 2) + 40, 0.26, col))
        for s, px, py, size, c, al in name_labels(W // 2, H_ROW // 2, 0.15, col):
            labels.append((s, px, i * (H_ROW // 2) + py, size, c, al))
    over(canvas, text_layer(W // 2, hh, labels))
    save_px(canvas, args.lights)
    print("LIGHTS", args.lights, [r[0] for r in rows])
for p in placed:
    print("PLACED", p)
