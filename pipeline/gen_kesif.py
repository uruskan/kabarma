"""
Sis Korusu ve kesif noktasi yer tutucu modelleri (dunya2 akisi, M2; kredisiz). Ilkel parcalardan, parca basina duz
renkli malzeme (dokusuz: SANAT-SES §9 olcut 1'den gecmez, biomes.json'da "placeholder" isaretli; Tripo istegi raporda).
Renkler SANAT-SES §2 Sis Korusu paleti ve palette.json notrleri.

Cikti (depo kokune gore):
  game/assets/models/koru/{koru_agac,dev_mantar,kutuk,demir,recine,guhercile}.glb
  game/assets/models/kesif/{seyir_kulesi,yabani_bahce,yuva,enkaz}.glb
  game/assets/models/placeables/mancinik.glb

Calistir:
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/gen_kesif.py -- [--only koru_agac,seyir_kulesi] [--root .]
"""
import bpy, bmesh, sys, os, math, random, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--only", default="")
ap.add_argument("--root", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
args = ap.parse_args(argv)
ONLY = set(x for x in args.only.split(",") if x)

def hx(s):
    return tuple(int(s[i:i + 2], 16) / 255 for i in (0, 2, 4))

# Palet (sRGB): Sis Korusu (SANAT-SES §2) + notr ahsap/tas.
P = {
    "bark": hx("4E423C"), "bark_dark": hx("3A302C"), "moss": hx("3F6B5C"), "leaf": hx("2E8C7A"), "leaf_dark": hx("23604F"),
    "leaf_light": hx("4FA88F"), "accent": hx("F2C14E"), "shadow": hx("1F3044"), "cream": hx("EDE3C8"), "cap": hx("C9583A"),
    "cap_glow": hx("F2C14E"), "stone": hx("6E7175"), "stone_dark": hx("54575B"), "rust": hx("B5562E"), "rust_dark": hx("7E3A22"),
    "amber": hx("E09A2A"), "salt": hx("EEEBE2"), "wood": hx("8A6A48"), "wood_dark": hx("5E4632"), "rope": hx("C9B07A"),
    "roof": hx("B8483A"), "straw": hx("D9B26A"), "egg": hx("F2E8D0"), "metal": hx("9AA3AD"), "metal_dark": hx("5F6872"),
    "panel": hx("D9DDE2"), "flower_a": hx("E86A8A"), "flower_b": hx("F2C14E"), "flower_c": hx("8E7BD9"), "bush": hx("4C8A45"),
}

_mats = {}
def mat(name, rough=0.8, metal=0.0, emit=0.0):
    key = (name, rough, metal, emit)
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new(f"{name}")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    c = P[name]
    # Malzeme rengi dogrusal bekler.
    lin = tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)
    b.inputs["Base Color"].default_value = (*lin, 1.0)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit > 0:
        b.inputs["Emission Color"].default_value = (*lin, 1.0)
        b.inputs["Emission Strength"].default_value = emit
    _mats[key] = m
    return m

parts = []
def put(o, m):
    o.data.materials.clear()
    o.data.materials.append(m)
    parts.append(o)
    return o

def cyl(r1, r2, h, loc, m, rot=(0, 0, 0), seg=12):
    bpy.ops.mesh.primitive_cone_add(vertices=seg, radius1=r1, radius2=r2, depth=h, location=loc, rotation=rot)
    o = bpy.context.active_object
    bpy.ops.object.shade_smooth()
    return put(o, m)

def box(size, loc, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    return put(o, m)

def ball(r, loc, m, scale=(1, 1, 1), sub=2, rnd=None, jitter=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    if rnd and jitter > 0:
        for v in o.data.vertices:
            v.co += v.normal * rnd.uniform(-jitter, jitter) * r
    bpy.ops.object.shade_smooth()
    return put(o, m)

def finish(name, sub):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    out = os.path.normpath(os.path.join(args.root, "game", "assets", "models", sub, name + ".glb"))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=True, export_apply=True)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    dims = o.dimensions
    print(f"KESIF {name} tris={tris} size={dims.x:.2f}x{dims.y:.2f}x{dims.z:.2f} -> {out}")
    parts.clear()

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
    parts.clear()

# ---------------- Koru ----------------

def koru_agac():
    r = random.Random(3)
    h = 17.0
    # Govde: kok kanatlari + uzun, hafif egik, incelen govde (ICERIK §1 "cok yuksek govdeler").
    cyl(1.05, 0.55, 2.0, (0, 0, 1.0), mat("bark_dark"), seg=10)
    for k in range(5):
        a = k * math.tau / 5 + r.uniform(-0.2, 0.2)
        cyl(0.32, 0.05, 2.0, (math.cos(a) * 0.75, math.sin(a) * 0.75, 0.6), mat("bark_dark"), rot=(math.cos(a + math.pi / 2) * 0.9, math.sin(a + math.pi / 2) * -0.9, 0), seg=6)
    cyl(0.55, 0.32, h - 2.0, (0, 0, 2.0 + (h - 2.0) / 2), mat("bark"), seg=10)
    # Yosun kemeri.
    cyl(0.72, 0.6, 1.2, (0, 0, 1.4), mat("moss", 0.95), seg=10)
    # Tac: ustte uc dort basik kume, altta seyrek dallar.
    for k, (z, rr) in enumerate([(h - 0.5, 3.4), (h - 2.6, 4.2), (h - 4.6, 3.6), (h + 1.4, 2.4)]):
        a = r.uniform(0, math.tau)
        off = (math.cos(a) * 0.8, math.sin(a) * 0.8, 0) if k else (0, 0, 0)
        ball(rr, (off[0], off[1], z), mat("leaf_dark" if k % 2 else "leaf", 0.9), scale=(1, 1, 0.45), sub=2, rnd=r, jitter=0.18)
    for k in range(4):
        a = k * math.tau / 4 + 0.4
        cyl(0.12, 0.05, 3.0, (math.cos(a) * 1.3, math.sin(a) * 1.3, h - 6.0), mat("bark"), rot=(math.sin(a) * 1.0, -math.cos(a) * 1.0, 0), seg=6)
        ball(1.4, (math.cos(a) * 2.6, math.sin(a) * 2.6, h - 5.3), mat("leaf_light", 0.9), scale=(1, 1, 0.5), sub=1, rnd=r, jitter=0.15)
    finish("koru_agac", "koru")

def dev_mantar():
    r = random.Random(5)
    cyl(0.28, 0.22, 2.2, (0, 0, 1.1), mat("cream", 0.7), seg=12)
    cyl(0.45, 0.28, 0.35, (0, 0, 0.17), mat("cream", 0.8), seg=12)
    ball(1.35, (0, 0, 2.25), mat("cap", 0.55), scale=(1, 1, 0.42), sub=3)
    # Alt yuz (lamel) ve isiyan benekler.
    cyl(1.25, 0.3, 0.12, (0, 0, 2.05), mat("cream", 0.9), seg=16)
    for k in range(7):
        a = r.uniform(0, math.tau); d = r.uniform(0.3, 1.0)
        ball(r.uniform(0.09, 0.16), (math.cos(a) * d, math.sin(a) * d, 2.25 + 0.5 * (1 - d * d / 1.4)), mat("cap_glow", 0.4, emit=1.2), scale=(1, 1, 0.5), sub=1)
    finish("dev_mantar", "koru")

def kutuk():
    r = random.Random(7)
    cyl(0.45, 0.4, 4.2, (0, 0, 0.42), mat("bark"), rot=(0, math.pi / 2, 0), seg=10)
    cyl(0.36, 0.36, 0.05, (2.12, 0, 0.42), mat("wood"), rot=(0, math.pi / 2, 0), seg=10)
    for k in range(5):
        ball(0.32, (r.uniform(-1.6, 1.6), r.uniform(-0.15, 0.15), 0.78), mat("moss", 0.95), scale=(1.6, 1, 0.35), sub=1, rnd=r, jitter=0.2)
    finish("kutuk", "koru")

def vein_rock(name, body, dark, vein, vein_mat, crystal=False, seed=11):
    r = random.Random(seed)
    ball(0.9, (0, 0, 0.55), mat(body, 0.85), scale=(1.15, 1.0, 0.8), sub=2, rnd=r, jitter=0.18)
    ball(0.55, (0.6, 0.3, 0.35), mat(dark, 0.85), scale=(1, 1, 0.8), sub=2, rnd=r, jitter=0.2)
    for k in range(6):
        a = r.uniform(0, math.tau); z = r.uniform(0.4, 1.05)
        if crystal:
            cyl(0.1, 0.02, r.uniform(0.35, 0.6), (math.cos(a) * 0.75, math.sin(a) * 0.7, z), vein_mat, rot=(r.uniform(-0.6, 0.6), r.uniform(-0.6, 0.6), 0), seg=6)
        else:
            ball(r.uniform(0.14, 0.24), (math.cos(a) * 0.85, math.sin(a) * 0.75, z), vein_mat, scale=(1.4, 1, 0.6), sub=1, rnd=r, jitter=0.25)
    finish(name, "koru")

def recine():
    r = random.Random(13)
    cyl(0.55, 0.48, 0.75, (0, 0, 0.37), mat("bark"), seg=10)
    cyl(0.44, 0.44, 0.04, (0, 0, 0.75), mat("wood"), seg=10)
    for k in range(5):
        a = k * math.tau / 5 + r.uniform(-0.3, 0.3)
        ball(r.uniform(0.12, 0.2), (math.cos(a) * 0.52, math.sin(a) * 0.52, r.uniform(0.25, 0.65)), mat("amber", 0.25, emit=0.35), scale=(1, 1, 1.4), sub=2)
    ball(0.16, (0.1, 0.05, 0.8), mat("amber", 0.25, emit=0.35), scale=(1.3, 1.1, 0.6), sub=2)
    finish("recine", "koru")

# ---------------- Kesif noktalari ----------------

def seyir_kulesi():
    H = 14.0
    w = 1.6
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(0.2, 0.16, H, (sx * w, sy * w, H / 2), mat("wood_dark"), seg=8)
    # Capraz kusaklar: her yuzde X; ust ve alt kirisler.
    for z in (3.5, 7.0, 10.5):
        for (ax, ay, rot) in ((0, -w, 0), (0, w, 0), (-w, 0, math.pi / 2), (w, 0, math.pi / 2)):
            box((2 * w, 0.14, 0.18), (ax, ay, z), mat("wood"), rot=(0, 0, rot))
    for (ax, ay, rot) in ((0, -w, 0), (0, w, 0), (-w, 0, math.pi / 2), (w, 0, math.pi / 2)):
        for k in range(3):
            z = 1.75 + k * 3.5
            ang = math.atan2(3.5, 2 * w)
            box((math.hypot(2 * w, 3.5), 0.1, 0.12), (ax, ay, z), mat("wood"), rot=(ang if rot else 0, 0 if rot else -ang, rot))
    # Platform, korkuluk, cati.
    box((4.6, 4.6, 0.25), (0, 0, H), mat("wood"))
    for (ax, ay, rot) in ((0, -2.25, 0), (0, 2.25, 0), (-2.25, 0, math.pi / 2), (2.25, 0, math.pi / 2)):
        box((4.5, 0.1, 0.1), (ax, ay, H + 1.0), mat("wood_dark"), rot=(0, 0, rot))
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(0.09, 0.09, 2.6, (sx * 2.2, sy * 2.2, H + 1.3), mat("wood_dark"), seg=6)
    cyl(3.7, 0.15, 1.6, (0, 0, H + 3.4), mat("roof", 0.7), seg=4, rot=(0, 0, math.pi / 4))
    # Bayrak (uzaktan gorunsun).
    cyl(0.04, 0.04, 2.2, (0, 0, H + 5.1), mat("wood_dark"), seg=6)
    box((0.9, 0.04, 0.5), (0.45, 0, H + 5.9), mat("accent", 0.6))
    # Merdiven (guney yuzu).
    for sx in (-0.35, 0.35):
        cyl(0.05, 0.05, H, (sx, -w - 0.25, H / 2), mat("wood"), seg=6)
    for k in range(int(H / 0.45)):
        box((0.7, 0.06, 0.06), (0, -w - 0.25, 0.4 + k * 0.45), mat("wood"))
    finish("seyir_kulesi", "kesif")

def yabani_bahce():
    r = random.Random(17)
    for k in range(9):
        a = r.uniform(0, math.tau); d = r.uniform(0.2, 1.6)
        c = (math.cos(a) * d, math.sin(a) * d)
        ball(r.uniform(0.35, 0.6), (c[0], c[1], 0.25), mat("bush", 0.9), scale=(1, 1, 0.7), sub=2, rnd=r, jitter=0.2)
        for j in range(3):
            fa = r.uniform(0, math.tau)
            ball(0.09, (c[0] + math.cos(fa) * 0.3, c[1] + math.sin(fa) * 0.3, r.uniform(0.45, 0.7)), mat(r.choice(["flower_a", "flower_b", "flower_c"]), 0.6), sub=1)
    # Tohum kozalari: sapli kahverengi topuzlar.
    for k in range(5):
        a = r.uniform(0, math.tau); d = r.uniform(0.3, 1.4)
        cyl(0.025, 0.02, 0.9, (math.cos(a) * d, math.sin(a) * d, 0.45), mat("wood"), seg=5)
        ball(0.12, (math.cos(a) * d, math.sin(a) * d, 0.95), mat("straw", 0.8), scale=(1, 1, 1.3), sub=1)
    finish("yabani_bahce", "kesif")

def yuva():
    r = random.Random(19)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.42, minor_radius=0.16, location=(0, 0, 0.14))
    o = bpy.context.active_object
    for v in o.data.vertices:
        v.co += v.normal * r.uniform(-0.03, 0.04)
    bpy.ops.object.shade_smooth()
    put(o, mat("straw", 0.95))
    cyl(0.36, 0.36, 0.06, (0, 0, 0.05), mat("wood_dark", 0.95), seg=12)
    for k in range(10):
        a = r.uniform(0, math.tau)
        cyl(0.012, 0.012, 0.5, (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.15), mat("straw"), rot=(r.uniform(-1.2, 1.2), r.uniform(-1.2, 1.2), 0), seg=4)
    ball(0.1, (0.08, 0.04, 0.17), mat("egg", 0.5), scale=(1, 1, 1.3), sub=2)
    ball(0.1, (-0.1, -0.06, 0.16), mat("egg", 0.5), scale=(1, 1, 1.3), sub=2)
    finish("yuva", "kesif")

def enkaz():
    r = random.Random(23)
    # Dusmus bir kapsul kanadi, kirik saclar, boru ve kasa (Hurda kaynagi).
    box((1.6, 0.9, 0.12), (0, 0, 0.35), mat("panel", 0.55, 0.3), rot=(0.3, 0.1, 0.4))
    box((1.1, 0.7, 0.1), (0.5, 0.4, 0.2), mat("metal_dark", 0.5, 0.6), rot=(-0.2, 0.25, 1.2))
    box((0.7, 0.7, 0.6), (-0.6, -0.3, 0.3), mat("metal", 0.5, 0.5), rot=(0.05, 0.1, 0.5))
    box((0.72, 0.1, 0.12), (-0.6, -0.3, 0.62), mat("accent", 0.6), rot=(0.05, 0.1, 0.5))
    cyl(0.12, 0.12, 1.8, (0.2, -0.6, 0.15), mat("metal_dark", 0.4, 0.7), rot=(math.pi / 2, 0, 0.7), seg=10)
    for k in range(5):
        ball(0.12, (r.uniform(-0.9, 0.9), r.uniform(-0.8, 0.8), 0.08), mat("rust", 0.9), scale=(1.6, 1, 0.4), sub=1, rnd=r, jitter=0.3)
    finish("enkaz", "kesif")

def mancinik():
    # Ahsap mancinik (ICERIK §10.2 Hizmet): taban, iki dikme, kol, kepce, karsi agirlik, yay ipi.
    for sy in (-0.7, 0.7):
        box((2.6, 0.18, 0.18), (0, sy, 0.15), mat("wood_dark"))
    for sx in (-1.1, 0, 1.1):
        box((0.18, 1.6, 0.16), (sx, 0, 0.3), mat("wood"))
    for sy in (-0.55, 0.55):
        box((0.18, 0.16, 1.6), (0.1, sy, 1.0), mat("wood_dark"))
        box((0.8, 0.12, 0.12), (0.45, sy, 0.6), mat("wood"), rot=(0, 0.9, 0))
    cyl(0.07, 0.07, 1.3, (0.1, 0, 1.75), mat("metal_dark", 0.4, 0.7), rot=(math.pi / 2, 0, 0), seg=8)
    box((2.8, 0.14, 0.14), (-0.3, 0, 1.95), mat("wood"), rot=(0, -0.45, 0))
    cyl(0.42, 0.32, 0.3, (-1.55, 0, 2.55), mat("wood_dark"), seg=10)
    box((0.5, 0.5, 0.5), (1.15, 0, 1.25), mat("stone", 0.9))
    cyl(0.03, 0.03, 1.4, (-0.9, 0, 0.9), mat("rope"), rot=(0, 0.6, 0), seg=5)
    box((0.12, 1.2, 0.12), (-1.25, 0, 0.35), mat("accent", 0.6))
    finish("mancinik", "placeables")

JOBS = {
    "koru_agac": koru_agac, "dev_mantar": dev_mantar, "kutuk": kutuk,
    "demir": lambda: vein_rock("demir", "stone", "stone_dark", "rust", mat("rust", 0.6, 0.3), seed=11),
    "recine": recine,
    "guhercile": lambda: vein_rock("guhercile", "stone_dark", "stone", "salt", mat("salt", 0.35), crystal=True, seed=29),
    "seyir_kulesi": seyir_kulesi, "yabani_bahce": yabani_bahce, "yuva": yuva, "enkaz": enkaz, "mancinik": mancinik,
}

for name, job in JOBS.items():
    if ONLY and name not in ONLY:
        continue
    reset()
    job()
print("KESIF done")
