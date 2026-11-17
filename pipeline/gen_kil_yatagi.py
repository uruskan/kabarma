"""
Kil yatagi (dunya akisi; URETIM §6 M1 kararlari "Koddan varliklar": kil yatagi Blender betiginden, kredisiz).

Gol kiyisinda yayvan, islak camur tabani ve ustunde elle sekillenmis gibi duran 5 kil topagi. Tek parca, kapali
(manifold) govde: toplar voksel yeniden orgusuyle kaynasir, alt duz kesilip kapatilir. Doku: kose renginden ve
yordamsal gurultuden 1024 px'e pisirilir (dokulu yon, SANAT-SES §1.2 ve §9 olcut 1). Renkler palette.json'dan.

Calistir (depo kokunden; ham GLB'yi clean_asset.py'ye vermek icin pipeline/make_kil_yatagi.ps1 ikisini sirayla calistirir):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/gen_kil_yatagi.py -- --out <ham.glb> [--seed 7] [--tris 2800] [--tex 1024]
"""
import bpy, bmesh, sys, os, json, math, random, argparse
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--out", required=True)
ap.add_argument("--seed", type=int, default=7)
ap.add_argument("--tris", type=int, default=2800, help="voksel boyu bu ucgen sayisinin altina inene dek buyur")
ap.add_argument("--tex", type=int, default=1024)
ap.add_argument("--palette", default=os.path.join(os.path.dirname(__file__), "..", "game", "data", "palette.json"))
args = ap.parse_args(argv)
rnd = random.Random(args.seed)

pal = json.load(open(args.palette, encoding="utf-8"))["sets"]
def hx(s):
    return tuple(int(s[i:i + 2], 16) / 255 for i in (0, 2, 4))
def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))

# Renkler (sRGB): Rock.ClayBed ile ayni aile: vurgu + toprak = kiremit kili; islak camur toprak + golge.
ACCENT, SOIL, SHADOW, OFF = hx(pal["altin_cayir"]["accent"]), hx(pal["tones"]["soil"]), hx(pal["altin_cayir"]["shadow"]), hx(pal["neutral"]["off_white"])
ACCENT_DARK = hx(pal["tones"]["accent_dark"])
CLAY = lerp(ACCENT, SOIL, 0.3)
CLAY_CUT = lerp(CLAY, ACCENT, 0.35)   # kurek kesigi: daha acik, taze
CRUST = lerp(CLAY, OFF, 0.42)        # ustte kuruyan kabuk
MUD = lerp(SOIL, SHADOW, 0.32)
MUD_WET = lerp(SOIL, SHADOW, 0.55)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------- geometri: taban + topaklar ----------------
parts = []
def blob(loc, scale, rot_z=0.0, seg=40, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    o.rotation_euler = (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), rot_z)
    parts.append(o)
    return o

# Taban: yayvan, islak camur (2,4 x 1,9 m), alti z=0'da kesilecek; ust yuzu ~0,13 m.
blob((0, 0, -0.07), (1.2, 0.95, 0.2), rnd.uniform(0, math.pi))
# Kazilmis cukurun dudagi: iki uzun, alcak kabarti.
for k in range(2):
    a = rnd.uniform(0, math.tau)
    blob((math.cos(a) * 0.8, math.sin(a) * 0.62, -0.03), (0.5, 0.26, 0.15), a + math.pi / 2)

def block(loc, size, rot):
    # Kurekle kesilmis kil kalibi: pahli kutu (kesik yuzler duz, kenarlar yumusak).
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc)
    o = bpy.context.active_object
    o.scale = size
    o.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    b = o.modifiers.new("pah", "BEVEL"); b.width = 0.07; b.segments = 3
    bpy.ops.object.modifier_apply(modifier=b.name)
    parts.append(o)

# Kil: 3 kesik kalip (biri ustte, egik) ve 3 elle yuvarlanmis topak; aralari acik, tek tek okunur.
lumps = []
a0 = rnd.uniform(0, math.tau)
for k in range(3):
    a = a0 + k * 2.1 + rnd.uniform(-0.25, 0.25)
    d = 0.42 if k < 2 else 0.12
    z = 0.26 if k < 2 else 0.5
    sz = (rnd.uniform(0.46, 0.56), rnd.uniform(0.34, 0.4), rnd.uniform(0.26, 0.3))
    loc = (math.cos(a) * d * 1.1, math.sin(a) * d * 0.9, z)
    block(loc, sz, (rnd.uniform(-0.12, 0.12), rnd.uniform(-0.25, 0.25) if k == 2 else rnd.uniform(-0.08, 0.08), a + rnd.uniform(-0.4, 0.4)))
    lumps.append((Vector(loc), max(sz) * 0.75))
for k in range(3):
    a = a0 + 1.05 + k * 2.1 + rnd.uniform(-0.2, 0.2)
    d = rnd.uniform(0.62, 0.78)
    s = rnd.uniform(0.17, 0.22)
    c = (math.cos(a) * d * 1.1, math.sin(a) * d * 0.85, 0.1 + s * 0.6)
    sc = (s * rnd.uniform(1.05, 1.25), s * rnd.uniform(0.9, 1.05), s * rnd.uniform(0.75, 0.9))
    blob(c, sc, rnd.uniform(0, math.pi), seg=32, rings=16)
    lumps.append((Vector(c), max(sc)))
bpy.ops.object.select_all(action="DESELECT")
for o in parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.join()
obj = bpy.context.active_object
obj.name = "kil_yatagi"

# ---------------- kaynastir: voksel yeniden orgu (kapali, tek parca) ----------------
src = obj.data.copy()
def remesh(voxel):
    obj.data = src.copy()
    m = obj.modifiers.new("vox", "REMESH")
    m.mode = "VOXEL"; m.voxel_size = voxel; m.adaptivity = 0.0; m.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=m.name)
    sm = obj.modifiers.new("soft", "SMOOTH")
    sm.factor = 0.4; sm.iterations = 1
    bpy.ops.object.modifier_apply(modifier=sm.name)
    # Alt: z=0'da duz kes, kapat (zemine oturur).
    bm = bmesh.new(); bm.from_mesh(obj.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(0, 0, 1), clear_inner=True)
    bound = [e for e in bm.edges if e.is_boundary]
    if bound:
        bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=bound)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)

voxel = 0.045
tris = remesh(voxel)
while tris > args.tris and voxel < 0.2:
    voxel *= 1.1
    tris = remesh(voxel)
print(f"KIL voxel={voxel:.3f} tris={tris}")

# ---------------- kose rengi: camur, kil, kuru kabuk ----------------
me = obj.data
def smooth01(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
col = me.color_attributes.new(name="Col", type="BYTE_COLOR", domain="POINT")
for v in me.vertices:
    p, n = v.co, v.normal
    clay = smooth01((p.z - 0.12) / 0.07)          # tabandan yukselen her sey kil
    wet = smooth01(1.0 - p.z / 0.1)                # kenarda islak, koyu camur
    base = lerp(MUD, MUD_WET, wet)
    side = smooth01((0.55 - abs(n.z)) / 0.4)      # dik yuz: kurek kesigi
    up = smooth01((n.z - 0.55) / 0.35)            # ust yuz: kuru kabuk
    c = lerp(CLAY, CLAY_CUT, side * 0.8)
    c = lerp(c, CRUST, up * smooth01((p.z - 0.2) / 0.15) * 0.6)
    c = lerp(base, c, clay)
    col.data[v.index].color_srgb = (c[0], c[1], c[2], 1.0)
me.color_attributes.active_color = col

# ---------------- UV ve doku pisirme ----------------
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
bpy.ops.object.mode_set(mode="OBJECT")

img = bpy.data.images.new("kil_yatagi_albedo", args.tex, args.tex, alpha=False)
mat = bpy.data.materials.new("kil_yatagi")
mat.use_nodes = True
nt = mat.node_tree
for n in list(nt.nodes):
    nt.nodes.remove(n)
out = nt.nodes.new("ShaderNodeOutputMaterial")
emit = nt.nodes.new("ShaderNodeEmission")
attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 9.0; noise.inputs["Detail"].default_value = 6.0
noise2 = nt.nodes.new("ShaderNodeTexNoise"); noise2.inputs["Scale"].default_value = 38.0; noise2.inputs["Detail"].default_value = 3.0
# El boyamasi titresimi: iki olcekli gurultu rengi %18 aydinlatir/koyulastirir.
ramp = nt.nodes.new("ShaderNodeMapRange")
ramp.inputs["From Min"].default_value = 0.3; ramp.inputs["From Max"].default_value = 0.7
ramp.inputs["To Min"].default_value = 0.82; ramp.inputs["To Max"].default_value = 1.12
mix_n = nt.nodes.new("ShaderNodeMath"); mix_n.operation = "MULTIPLY_ADD"
mix_n.inputs[1].default_value = 0.6
nt.links.new(noise2.outputs["Fac"], mix_n.inputs[0])
nt.links.new(noise.outputs["Fac"], mix_n.inputs[2])
half = nt.nodes.new("ShaderNodeMath"); half.operation = "MULTIPLY"; half.inputs[1].default_value = 0.625
nt.links.new(mix_n.outputs[0], half.inputs[0])
nt.links.new(half.outputs[0], ramp.inputs["Value"])
mul = nt.nodes.new("ShaderNodeVectorMath"); mul.operation = "SCALE"
nt.links.new(attr.outputs["Color"], mul.inputs[0])
nt.links.new(ramp.outputs["Result"], mul.inputs["Scale"])
nt.links.new(mul.outputs["Vector"], emit.inputs["Color"])
nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
nt.nodes.active = tex
me.materials.clear(); me.materials.append(mat)

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 4
bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.bake(type="EMIT", margin=8)
img.pack()

# Oyun malzemesi: pisirilmis doku + islak kilin hafif parlakligi.
for n in list(nt.nodes):
    nt.nodes.remove(n)
out = nt.nodes.new("ShaderNodeOutputMaterial")
bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.62
bsdf.inputs["Metallic"].default_value = 0.0
nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
me.color_attributes.remove(me.color_attributes["Col"])

os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=args.out, export_format="GLB", use_selection=False, export_yup=True,
                          export_texcoords=True, export_normals=True)
bm = bmesh.new(); bm.from_mesh(me)
nonman = sum(1 for e in bm.edges if not e.is_manifold); bm.free()
print(f"KIL_RAW out={args.out} tris={tris} voxel={voxel:.3f} non_manifold={nonman}")
