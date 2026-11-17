"""
Koddan cizilen ilkel sekillerin yerine Blender'da kurulan ayrintili modeller (gorsel-model akisi, 18 Kasim 2026).
Kredi harcamaz. Her model: pahli geometri, yordamsal Cycles malzemeleri (boya kenarindan metal cikmasi, pas, kurum,
baklava desenli sac, tas catlagi), tek atlasa pisirilir (albedo, normal, roughness+metal) ve GLB'ye gomulur.

  ocakbasi       Ocakbasi: tas halka, kul yatagi, odunlar, dokme demir izgara, emaye tencere (eski: OcakbasiVisual MeshKit)
  rampa_pisti    Kargo Rampasi pisti: celik cerceve, baklava sac, uyari seridi, kose direkleri (eski: Pod._pad MeshKit)
  kargo_roketi   Kargo roketi: govde, burun, kanatlar, motor cani (eski: Pod._rocket MeshKit)

Calistir (depo kokunden):
  "C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
      -P pipeline/build_props.py -- [--only ocakbasi] [--tex 1024] [--samples 16]
Cikti: game/assets/models/props/<ad>.glb, progress/gorsel/model/props/<ad>.png (Cycles onizleme).
Eksen: Blender Z yukari; glTF Y yukari. Dugum adlari oyun kodunun aradigi adlardir (bkz. her kurucu).
"""
import bpy
import bmesh
import math
import os
import random
import sys
import tempfile

from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "game", "assets", "models", "props")
PREV = os.path.join(ROOT, "progress", "gorsel", "model", "props")

if os.environ.get("KIT_TMP"):
    os.makedirs(os.environ["KIT_TMP"], exist_ok=True)
    tempfile.tempdir = os.environ["KIT_TMP"]


def argv():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = {"only": None, "tex": 1024, "samples": 16}
    for i in range(0, len(a), 2):
        k = a[i].lstrip("-")
        o[k] = set(a[i + 1].split(",")) if k == "only" else int(a[i + 1])
    return o


# ------------------------------------------------------------------ sahne ve geometri

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.world = bpy.data.worlds.new("W")
    sc.world.light_settings.distance = 0.4
    return sc


def finish(o, bevel=0.0, segs=2, smooth=True, angle=40):
    """Pah (gercek geometri), yumusak golgeleme, uygulanmis degistiriciler."""
    bpy.context.view_layer.objects.active = o
    if bevel > 0:
        m = o.modifiers.new("bev", "BEVEL")
        m.width = bevel
        m.segments = segs
        m.limit_method = "ANGLE"
        m.angle_limit = math.radians(30)
        m.harden_normals = False
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)
    if smooth:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    return o


def box(name, size, loc, rot=(0, 0, 0), bevel=0.01, mat=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    finish(o, bevel)
    if mat:
        o.data.materials.append(mat)
    return o


def cyl(name, r, h, loc, rot=(0, 0, 0), verts=16, bevel=0.006, mat=None, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=h, location=loc, rotation=rot)
    o = bpy.context.object
    o.name = name
    finish(o, bevel)
    if mat:
        o.data.materials.append(mat)
    return o


def lathe(name, profile, verts=28, mat=None, loc=(0, 0, 0)):
    """(r, z) profilinden donel govde (tencere, motor cani, burun)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rings = []
    for i in range(verts):
        a = i / verts * math.tau
        rings.append([bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in profile])
    for i in range(verts):
        A, B = rings[i], rings[(i + 1) % verts]
        for j in range(len(profile) - 1):
            bm.faces.new((A[j], B[j], B[j + 1], A[j + 1]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = loc
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    finish(o, 0, smooth=True, angle=50)
    if mat:
        o.data.materials.append(mat)
    return o


def rock(name, loc, size, seed, mat):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=loc)
    o = bpy.context.object
    o.name = name
    rnd = random.Random(seed)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    ox, oy, oz = rnd.random() * 9, rnd.random() * 9, rnd.random() * 9
    for v in bm.verts:
        p = v.co
        n = (math.sin(p.x * 2.1 + ox) * math.sin(p.y * 2.3 + oy) * math.sin(p.z * 1.9 + oz))
        f = 1 + 0.26 * n + 0.09 * math.sin(p.x * 6 + oz) * math.cos(p.y * 5 + ox)
        # alt yuz duz (yere oturur), ust kenarlar pahli
        v.co = Vector((p.x * f, p.y * f, max(-0.35, p.z * f)))
    bm.to_mesh(o.data)
    bm.free()
    o.scale = size
    o.rotation_euler = (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), rnd.uniform(0, math.tau))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # tabani y=0'a oturt
    zmin = min(v.co.z for v in o.data.vertices)
    for v in o.data.vertices:
        v.co.z -= zmin + 0.02
    finish(o, 0, angle=35)
    o.data.materials.append(mat)
    return o


# ------------------------------------------------------------------ yordamsal malzemeler

def lin(c):
    """Malzeme renkleri sRGB yazilir (palet gibi); dugum agaci dogrusal ister."""
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


class Mat:
    """Principled + pisirme kaynaklari: color/rough/metal soketleri ve normal zinciri (kabartma + Bevel)."""

    def __init__(self, name):
        self.m = bpy.data.materials.new(name)
        self.m.use_nodes = True
        self.nt = self.m.node_tree
        self.n = self.nt.nodes
        self.bsdf = self.n["Principled BSDF"]
        self.src = {}

    def node(self, t, **inputs):
        nd = self.n.new(t)
        for k, v in inputs.items():
            if k.startswith("_"):
                setattr(nd, k[1:], v)
            elif isinstance(v, bpy.types.NodeSocket):
                self.nt.links.new(v, nd.inputs[k])
            else:
                nd.inputs[k].default_value = v
        return nd

    def link(self, a, b):
        self.nt.links.new(a, b)

    def value(self, v):
        nd = self.n.new("ShaderNodeValue")
        nd.outputs[0].default_value = v
        return nd.outputs[0]

    def rgb(self, c):
        nd = self.n.new("ShaderNodeRGB")
        nd.outputs[0].default_value = (*lin(c), 1)
        return nd.outputs[0]

    def noise(self, scale, detail=6, rough=0.6, coord=None, w=0.0):
        nd = self.node("ShaderNodeTexNoise", Scale=scale, Detail=detail, Roughness=rough)
        if coord is not None:
            self.link(coord, nd.inputs["Vector"])
        return nd

    def mix(self, fac, a, b, blend="MIX"):
        nd = self.n.new("ShaderNodeMix")
        nd.data_type = "RGBA"
        nd.blend_type = blend
        self.link(fac, nd.inputs["Factor"]) if isinstance(fac, bpy.types.NodeSocket) else setattr(nd.inputs["Factor"], "default_value", fac)
        for sock, v in ((nd.inputs[6], a), (nd.inputs[7], b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, sock)
            else:
                sock.default_value = (*lin(v), 1)
        return nd.outputs[2]

    def mixf(self, fac, a, b):
        nd = self.n.new("ShaderNodeMix")
        nd.data_type = "FLOAT"
        self.link(fac, nd.inputs["Factor"]) if isinstance(fac, bpy.types.NodeSocket) else setattr(nd.inputs["Factor"], "default_value", fac)
        for sock, v in ((nd.inputs[2], a), (nd.inputs[3], b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, sock)
            else:
                sock.default_value = v
        return nd.outputs[0]

    def ramp(self, x, lo, hi):
        nd = self.node("ShaderNodeMapRange", **{"From Min": lo, "From Max": hi})
        self.link(x, nd.inputs["Value"])
        return nd.outputs["Result"]

    def math(self, op, a, b=0.0):
        nd = self.n.new("ShaderNodeMath")
        nd.operation = op
        for sock, v in ((nd.inputs[0], a), (nd.inputs[1], b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.link(v, sock)
            else:
                sock.default_value = v
        return nd.outputs[0]

    def edge_mask(self, radius=0.012, lo=0.985, hi=0.9):
        """Disbukey kenar maskesi: Bevel normali geometri normalinden saptikca 1."""
        bev = self.node("ShaderNodeBevel", Radius=radius, _samples=8)
        geo = self.n.new("ShaderNodeNewGeometry")
        dot = self.n.new("ShaderNodeVectorMath")
        dot.operation = "DOT_PRODUCT"
        self.link(bev.outputs[0], dot.inputs[0])
        self.link(geo.outputs["Normal"], dot.inputs[1])
        return self.ramp(dot.outputs["Value"], lo, hi)

    def cavity(self, dist=0.05):
        ao = self.node("ShaderNodeAmbientOcclusion", Distance=dist, _samples=8, _only_local=True)
        return self.ramp(ao.outputs["AO"], 0.95, 0.4)

    def finish(self, color, rough, metal, bump_h=None, bump_strength=0.3, bevel_r=0.01):
        rough = rough if isinstance(rough, bpy.types.NodeSocket) else self.value(rough)
        metal = metal if isinstance(metal, bpy.types.NodeSocket) else self.value(metal)
        color = color if isinstance(color, bpy.types.NodeSocket) else self.rgb(color)
        self.link(color, self.bsdf.inputs["Base Color"])
        self.link(rough, self.bsdf.inputs["Roughness"])
        self.link(metal, self.bsdf.inputs["Metallic"])
        bev = self.node("ShaderNodeBevel", Radius=bevel_r, _samples=16)
        if bump_h is not None:
            bump = self.node("ShaderNodeBump", Strength=bump_strength, Distance=0.02)
            self.link(bump_h, bump.inputs["Height"])
            self.link(bump.outputs["Normal"], bev.inputs["Normal"])
        self.link(bev.outputs["Normal"], self.bsdf.inputs["Normal"])
        self.src = {"color": color, "rough": rough, "metal": metal}
        return self.m


def obj_coord(m):
    return m.n.new("ShaderNodeTexCoord").outputs["Object"]


def painted_metal(name, paint, chips=0.6, rough=0.42, dirt=0.35, stripes=None, stripe_scale=6.0, scorch=None):
    """Boyali sac: kenarda boya atar, celik gorunur; alttan kir; istenirse uyari seridi."""
    m = Mat(name)
    co = obj_coord(m)
    n1 = m.noise(7.0, coord=co)
    n2 = m.noise(40.0, coord=co)
    col = m.rgb(paint)
    if stripes:
        # capraz serit: (x + y) * olcek -> kesirli -> esik
        sep = m.n.new("ShaderNodeSeparateXYZ")
        m.link(co, sep.inputs[0])
        s = m.math("ADD", sep.outputs["X"], sep.outputs["Y"])
        s = m.math("MULTIPLY", s, stripe_scale)
        s = m.math("FRACT", s)
        s = m.math("GREATER_THAN", s, 0.5)
        col = m.mix(s, stripes[0], stripes[1])
    var = m.ramp(n1.outputs["Fac"], 0.35, 0.65)
    col = m.mix(m.math("MULTIPLY", var, 0.18), col, (0.12, 0.11, 0.1), "MULTIPLY")
    edge = m.edge_mask()
    chip = m.math("MULTIPLY", edge, m.ramp(n2.outputs["Fac"], 0.42, 0.58))
    chip = m.math("MINIMUM", m.math("MULTIPLY", chip, chips * 2.2), 1.0)
    steel = (0.52, 0.51, 0.49)
    col = m.mix(chip, col, steel)
    cav = m.cavity(0.06)
    grime = m.math("MULTIPLY", cav, dirt)
    col = m.mix(grime, col, (0.16, 0.13, 0.1))
    r = m.mixf(chip, rough, 0.3)
    r = m.math("ADD", r, m.math("MULTIPLY", grime, 0.35))
    r = m.math("ADD", r, m.math("MULTIPLY", m.math("SUBTRACT", n1.outputs["Fac"], 0.5), 0.15))
    if scorch:
        # motor yanigi: nesnenin altindan yukari is ve isi rengi (scorch = (alt z, ust z))
        sep = m.n.new("ShaderNodeSeparateXYZ")
        m.link(co, sep.inputs[0])
        zz = m.math("ADD", sep.outputs["Z"], m.math("MULTIPLY", m.math("SUBTRACT", n1.outputs["Fac"], 0.5), 0.35))
        sc = m.ramp(zz, scorch[1], scorch[0])
        col = m.mix(m.math("MULTIPLY", sc, 0.85), col, (0.08, 0.065, 0.055))
        r = m.math("ADD", r, m.math("MULTIPLY", sc, 0.3))
        # dikey akinti izleri
        streak = m.node("ShaderNodeTexNoise", Scale=2.0, Detail=3.0)
        mp = m.node("ShaderNodeMapping")
        m.link(co, mp.inputs["Vector"])
        mp.inputs["Scale"].default_value = (14.0, 14.0, 0.6)
        m.link(mp.outputs[0], streak.inputs["Vector"])
        col = m.mix(m.math("MULTIPLY", m.ramp(streak.outputs["Fac"], 0.55, 0.75), 0.35), col, (0.2, 0.17, 0.14))
    metal = m.mixf(chip, 0.0, 0.9)
    h = m.math("ADD", m.math("MULTIPLY", chip, -0.4), m.math("MULTIPLY", n2.outputs["Fac"], 0.08))
    return m.finish(col, r, metal, h, 0.25)


def bare_metal(name, base=(0.3, 0.3, 0.31), rough=0.45, rust=0.35, soot=0.0, tread=False):
    """Dokme demir / celik: pas lekesi, kurum, istege bagli baklava desen kabartmasi."""
    m = Mat(name)
    co = obj_coord(m)
    n1 = m.noise(5.0, coord=co)
    n2 = m.noise(28.0, coord=co, detail=8)
    rustm = m.ramp(m.math("ADD", n1.outputs["Fac"], m.math("MULTIPLY", n2.outputs["Fac"], 0.35)), 0.62 - rust * 0.25, 0.78 - rust * 0.25)
    rustm = m.math("MULTIPLY", rustm, rust * 1.6)
    rustm = m.math("MINIMUM", rustm, 1.0)
    col = m.mix(rustm, base, (0.36, 0.18, 0.09))
    edge = m.edge_mask()
    col = m.mix(m.math("MULTIPLY", edge, 0.5), col, (0.62, 0.61, 0.58))
    if soot > 0:
        col = m.mix(soot, col, (0.05, 0.045, 0.04))
    r = m.mixf(rustm, rough, 0.88)
    r = m.math("ADD", r, m.math("MULTIPLY", m.math("SUBTRACT", n2.outputs["Fac"], 0.5), 0.2))
    metal = m.mixf(rustm, 0.92, 0.2)
    h = m.math("MULTIPLY", n2.outputs["Fac"], 0.12)
    if tread:
        # baklava sac: iki capraz dalga dokusunun carpimi -> kisa kabartmalar
        sep = m.n.new("ShaderNodeSeparateXYZ")
        m.link(co, sep.inputs[0])
        a = m.math("SINE", m.math("MULTIPLY", m.math("ADD", sep.outputs["X"], sep.outputs["Y"]), 38))
        b = m.math("SINE", m.math("MULTIPLY", m.math("SUBTRACT", sep.outputs["X"], sep.outputs["Y"]), 38))
        t = m.ramp(m.math("MULTIPLY", a, b), 0.55, 0.9)
        h = m.math("ADD", h, m.math("MULTIPLY", t, 0.6))
        col = m.mix(m.math("MULTIPLY", t, 0.25), col, (0.7, 0.69, 0.66))
    return m.finish(col, r, metal, h, 0.35)


def stone(name, base=(0.46, 0.43, 0.39), soot=0.0):
    m = Mat(name)
    co = obj_coord(m)
    n1 = m.noise(3.0, coord=co)
    n2 = m.noise(18.0, coord=co, detail=10)
    warp = m.n.new("ShaderNodeVectorMath")
    warp.operation = "ADD"
    m.link(co, warp.inputs[0])
    m.link(m.noise(1.5, coord=co).outputs["Color"], warp.inputs[1])
    vor = m.node("ShaderNodeTexVoronoi", Scale=3.5, _feature="DISTANCE_TO_EDGE")
    m.link(warp.outputs[0], vor.inputs["Vector"])
    crack = m.math("MULTIPLY", m.ramp(vor.outputs["Distance"], 0.015, 0.0), m.ramp(n1.outputs["Fac"], 0.45, 0.6))
    col = m.mix(m.ramp(n1.outputs["Fac"], 0.3, 0.7), base, tuple(c * 0.72 for c in base))
    col = m.mix(m.math("MULTIPLY", n2.outputs["Fac"], 0.3), col, (0.62, 0.6, 0.55))
    col = m.mix(m.math("MULTIPLY", crack, 0.35), col, (0.16, 0.15, 0.13))
    col = m.mix(m.math("MULTIPLY", m.cavity(0.08), 0.5), col, (0.18, 0.15, 0.12))
    if soot > 0:
        sep = m.n.new("ShaderNodeSeparateXYZ")
        m.link(m.n.new("ShaderNodeNewGeometry").outputs["Normal"], sep.inputs[0])
        inner = m.math("MULTIPLY", m.ramp(n1.outputs["Fac"], 0.3, 0.6), soot)
        col = m.mix(inner, col, (0.06, 0.055, 0.05))
    r = m.math("ADD", 0.82, m.math("MULTIPLY", n2.outputs["Fac"], 0.12))
    h = m.math("ADD", m.math("MULTIPLY", n2.outputs["Fac"], 0.6), m.math("MULTIPLY", crack, -0.3))
    return m.finish(col, r, 0.0, h, 0.5, 0.02)


def wood(name, base=(0.42, 0.28, 0.17), char=0.0):
    m = Mat(name)
    co = obj_coord(m)
    mp = m.node("ShaderNodeMapping")
    m.link(co, mp.inputs["Vector"])
    mp.inputs["Scale"].default_value = (1.0, 1.0, 14.0)
    wave = m.node("ShaderNodeTexWave", Scale=3.0, Distortion=6.0, Detail=4.0, _wave_type="RINGS")
    m.link(mp.outputs[0], wave.inputs["Vector"])
    n2 = m.noise(30.0, coord=co)
    col = m.mix(m.ramp(wave.outputs["Fac"], 0.2, 0.9), base, tuple(c * 0.62 for c in base))
    if char > 0:
        cn = m.noise(4.0, coord=co)
        cm = m.math("MULTIPLY", m.ramp(cn.outputs["Fac"], 0.45, 0.6), char)
        col = m.mix(cm, col, (0.04, 0.035, 0.03))
        ember = m.math("MULTIPLY", m.ramp(n2.outputs["Fac"], 0.72, 0.8), cm)
        col = m.mix(ember, col, (0.55, 0.16, 0.04))
    r = m.math("ADD", 0.7, m.math("MULTIPLY", n2.outputs["Fac"], 0.15))
    h = m.math("ADD", m.math("MULTIPLY", wave.outputs["Fac"], 0.3), m.math("MULTIPLY", n2.outputs["Fac"], 0.2))
    return m.finish(col, r, 0.0, h, 0.45, 0.012)


def ash(name):
    m = Mat(name)
    co = obj_coord(m)
    n1 = m.noise(9.0, coord=co, detail=8)
    n2 = m.noise(2.5, coord=co)
    col = m.mix(m.ramp(n1.outputs["Fac"], 0.3, 0.7), (0.09, 0.085, 0.08), (0.32, 0.3, 0.28))
    ember = m.math("MULTIPLY", m.ramp(n2.outputs["Fac"], 0.62, 0.7), m.ramp(n1.outputs["Fac"], 0.55, 0.65))
    col = m.mix(ember, col, (0.6, 0.2, 0.05))
    return m.finish(col, 0.95, 0.0, n1.outputs["Fac"], 0.6, 0.02)


def enamel(name, color):
    """Emaye tencere: parlak boya, kenarda kara demir atar, dipte is."""
    m = Mat(name)
    co = obj_coord(m)
    n2 = m.noise(35.0, coord=co)
    edge = m.edge_mask(0.008)
    chip = m.math("MINIMUM", m.math("MULTIPLY", m.math("MULTIPLY", edge, m.ramp(n2.outputs["Fac"], 0.45, 0.55)), 2.5), 1.0)
    col = m.mix(chip, color, (0.07, 0.07, 0.075))
    sep = m.n.new("ShaderNodeSeparateXYZ")
    m.link(co, sep.inputs[0])
    soot = m.ramp(sep.outputs["Z"], 0.05, -0.08)
    col = m.mix(soot, col, (0.05, 0.045, 0.04))
    r = m.mixf(chip, 0.24, 0.6)
    r = m.math("ADD", r, m.math("MULTIPLY", soot, 0.5))
    return m.finish(col, r, m.math("MULTIPLY", chip, 0.6), m.math("MULTIPLY", chip, -0.3), 0.2, 0.006)


# ------------------------------------------------------------------ pisirme ve disa aktarma

def bake_and_export(name, objs, tex, samples, parts=None):
    """Butun parcalar tek UV atlasina (birlikte akilli izdusum), Cycles'ta EMIT ile renk/rough/metal, NORMAL ile normal.
    Sonra malzeme goruntu tabanli Principled'a doner; GLB'ye gomulu disa aktarilir. parts: ad -> nesne listesi
    (birlesip tek dugum olur); verilmezse hepsi tek dugum 'name'."""
    sc = bpy.context.scene
    sc.cycles.samples = samples
    # parcalari birlestir (ayni ad -> tek mesh)
    groups = parts or {name: objs}
    finals = []
    for pname, olist in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for o in olist:
            o.select_set(True)
        bpy.context.view_layer.objects.active = olist[0]
        if len(olist) > 1:
            bpy.ops.object.join()
        o = bpy.context.object
        o.name = pname
        o.data.name = pname
        finals.append(o)
    # tek atlas
    bpy.ops.object.select_all(action="DESELECT")
    for o in finals:
        o.select_set(True)
    bpy.context.view_layer.objects.active = finals[0]
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.006, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.004, rotate=True)
    bpy.ops.object.mode_set(mode="OBJECT")

    mats = []
    for o in finals:
        for s in o.material_slots:
            if s.material and s.material not in mats:
                mats.append(s.material)
    imgs = {}
    for k in ("color", "rough", "metal", "normal"):
        img = bpy.data.images.new(f"{name}_{k}", tex, tex, alpha=False, float_buffer=(k != "color"))
        img.colorspace_settings.name = "sRGB" if k == "color" else "Non-Color"
        imgs[k] = img

    for k in ("color", "rough", "metal", "normal"):
        restore = []
        for mat in mats:
            nt = mat.node_tree
            tn = nt.nodes.new("ShaderNodeTexImage")
            tn.image = imgs[k]
            nt.nodes.active = tn
            out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
            old = out.inputs["Surface"].links[0].from_socket
            em = None
            if k != "normal":
                em = nt.nodes.new("ShaderNodeEmission")
                nt.links.new(MATSRC[mat.name][k], em.inputs["Color"])
                nt.links.new(em.outputs[0], out.inputs["Surface"])
            restore.append((nt, tn, out, old, em))
        bpy.ops.object.select_all(action="DESELECT")
        for o in finals:
            o.select_set(True)
        bpy.context.view_layer.objects.active = finals[0]
        if k == "normal":
            sc.cycles.samples = samples
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", margin=8, use_clear=True)
        else:
            sc.cycles.samples = max(4, samples // 2)
            bpy.ops.object.bake(type="EMIT", margin=8, use_clear=True)
        for nt, tn, out, old, em in restore:
            if em:
                nt.nodes.remove(em)
                nt.links.new(old, out.inputs["Surface"])
            nt.nodes.remove(tn)
        print(f"PROPS {name}: {k} pisirildi", flush=True)

    # rough (G) + metal (B) tek doku: glTF metallicRoughness
    import numpy as np
    def px(img):
        a = np.empty(tex * tex * 4, np.float32)
        img.pixels.foreach_get(a)
        return a.reshape(tex, tex, 4)
    rm = np.ones((tex, tex, 4), np.float32)
    rm[..., 1] = np.clip(px(imgs["rough"])[..., 0], 0.05, 1)
    rm[..., 2] = np.clip(px(imgs["metal"])[..., 0], 0, 1)
    rm[..., 0] = 1.0
    rmimg = bpy.data.images.new(f"{name}_rm", tex, tex, alpha=False, float_buffer=False)
    rmimg.colorspace_settings.name = "Non-Color"
    rmimg.pixels.foreach_set(rm.ravel())
    nimg = bpy.data.images.new(f"{name}_normal8", tex, tex, alpha=False, float_buffer=False)
    nimg.colorspace_settings.name = "Non-Color"
    nimg.pixels.foreach_set(px(imgs["normal"]).ravel())
    cimg = bpy.data.images.new(f"{name}_albedo", tex, tex, alpha=False, float_buffer=False)
    cimg.pixels.foreach_set(px(imgs["color"]).ravel())
    for im in (rmimg, nimg, cimg):
        im.pack()

    final = bpy.data.materials.new(f"{name}_pbr")
    final.use_nodes = True
    nt = final.node_tree
    b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage"); tc.image = cimg
    nt.links.new(tc.outputs["Color"], b.inputs["Base Color"])
    tr = nt.nodes.new("ShaderNodeTexImage"); tr.image = rmimg
    sp = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(tr.outputs["Color"], sp.inputs[0])
    nt.links.new(sp.outputs["Green"], b.inputs["Roughness"])
    nt.links.new(sp.outputs["Blue"], b.inputs["Metallic"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = nimg
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    for o in finals:
        o.data.materials.clear()
        o.data.materials.append(final)

    # dugum adlari -> disa aktar
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"{name}.glb")
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type in ("MESH", "EMPTY") and not o.name.startswith("_"):
            o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_image_format="AUTO", export_cameras=False, export_lights=False, export_yup=True)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in finals)
    print(f"PROPS ok {name} tris={tris} -> {path}", flush=True)
    return final


MATSRC = {}


def make_mat(fn, *a, **k):
    # Mat ornegi finish() sonrasi src tutar; builder Mat'in malzemesini dondurur. src'yi yakalamak icin
    # Mat.finish'i saran kucuk kanca.
    global _last
    m = fn(*a, **k)
    MATSRC[m.name] = _last.src
    return m


_orig_finish = Mat.finish


def _finish_hook(self, *a, **k):
    global _last
    r = _orig_finish(self, *a, **k)
    _last = self
    return r


Mat.finish = _finish_hook
_last = None


def preview(name, dist, target_z, lens=50):
    sc = bpy.context.scene
    cam = bpy.data.cameras.new("cam")
    cam.lens = lens
    co = bpy.data.objects.new("_cam", cam)
    bpy.context.collection.objects.link(co)
    d = Vector((dist * 0.75, -dist * 0.8, dist * 0.55))
    co.location = Vector((0, 0, target_z)) + d
    co.rotation_euler = (Vector((0, 0, target_z)) - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = co
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 3.2
    so = bpy.data.objects.new("_sun", sun)
    so.rotation_euler = (math.radians(48), 0, math.radians(35))
    bpy.context.collection.objects.link(so)
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.55, 0.62, 0.7, 1)
    bg.inputs["Strength"].default_value = 0.7
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    gp = bpy.context.object
    gp.name = "_ground"
    gm = bpy.data.materials.new("_g"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.33, 0.22, 1)
    gp.data.materials.append(gm)
    sc.render.resolution_x, sc.render.resolution_y = 960, 720
    sc.cycles.samples = 48
    sc.view_settings.view_transform = "AgX"
    os.makedirs(PREV, exist_ok=True)
    sc.render.filepath = os.path.join(PREV, f"{name}.png")
    bpy.ops.render.render(write_still=True)


# ------------------------------------------------------------------ modeller

def build_ocakbasi(o):
    """Ocakbasi: tas halka (r 0,62), kul yatagi, uc odun, dort ayakli izgara (ust 0,5 m), emaye tencere."""
    reset()
    st = make_mat(stone, "ocb_tas", soot=0.6)
    ashm = make_mat(ash, "ocb_kul")
    wd = make_mat(wood, "ocb_odun", char=0.85)
    iron = make_mat(bare_metal, "ocb_demir", base=(0.16, 0.155, 0.15), rough=0.55, rust=0.45, soot=0.35)
    pot = make_mat(enamel, "ocb_tencere", (0.16, 0.3, 0.46))
    objs = []
    rnd = random.Random(4417)
    n = 9
    for i in range(n):
        a = i * math.tau / n + rnd.uniform(-0.08, 0.08)
        r = 0.66
        s = rnd.uniform(0.17, 0.22)
        objs.append(rock(f"tas{i}", (math.cos(a) * r, math.sin(a) * r, 0), (s * 1.3, s, s * 0.72), 100 + i, st))
    disc = cyl("kul", 0.56, 0.06, (0, 0, 0.03), verts=24, bevel=0.02, mat=ashm)
    objs.append(disc)
    for i in range(3):
        a = i * 2.1 + 0.3
        objs.append(cyl(f"odun{i}", 0.055, 0.66, (0, 0, 0.12), rot=(math.pi / 2 - 0.22, 0, a), verts=10, bevel=0.012, mat=wd))
    # izgara: ayaklar, cerceve, cubuklar (2,5 cm: ince cubuk sinirina yakin, okunur)
    H = 0.52
    for x, y in ((-0.44, -0.44), (0.44, -0.44), (-0.44, 0.44), (0.44, 0.44)):
        objs.append(box(f"ayak", (0.035, 0.035, H), (x, y, H / 2), bevel=0.006, mat=iron))
    for s in (-1, 1):
        objs.append(box("cer", (0.92, 0.04, 0.035), (0, s * 0.44, H), bevel=0.006, mat=iron))
        objs.append(box("cer", (0.04, 0.84, 0.03), (s * 0.44, 0, H - 0.003), bevel=0.006, mat=iron))
    for i in range(-3, 4):
        objs.append(cyl("cubuk", 0.013, 0.86, (i * 0.11, 0, H + 0.02), rot=(math.pi / 2, 0, 0), verts=8, bevel=0, mat=iron))
    # tencere: donel profil + kulp
    prof = [(0.0, 0.0), (0.15, 0.0), (0.18, 0.02), (0.19, 0.12), (0.2, 0.2), (0.215, 0.215), (0.2, 0.225), (0.18, 0.2), (0.17, 0.05), (0.0, 0.05)]
    objs.append(lathe("tencere", prof, 28, pot, loc=(0.12, 0.05, H + 0.035)))
    bpy.ops.mesh.primitive_torus_add(major_radius=0.2, minor_radius=0.009, location=(0.12, 0.05, H + 0.25), rotation=(math.pi / 2, 0, 0))
    kulp = bpy.context.object
    kulp.name = "kulp"
    bm = bmesh.new(); bm.from_mesh(kulp.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -0.02], context="VERTS")
    bm.to_mesh(kulp.data); bm.free()
    finish(kulp)
    kulp.data.materials.append(iron)
    objs.append(kulp)
    bake_and_export("ocakbasi", objs, o["tex"], o["samples"])
    preview("ocakbasi", 2.4, 0.3)


def build_rampa(o):
    """Kargo Rampasi pisti (3,2 x 3,2 m, ust yuz 0,36 m): celik cerceve, dort baklava sac, uyari seridi kenari,
    ortada hedef halkasi, dort kose diregi. Dugum: 'pad'."""
    reset()
    frame = make_mat(painted_metal, "pst_cerceve", (0.17, 0.19, 0.21), chips=0.8, rough=0.5, dirt=0.5)
    deck = make_mat(bare_metal, "pst_sac", base=(0.34, 0.345, 0.35), rough=0.42, rust=0.25, tread=True)
    hz = make_mat(painted_metal, "pst_serit", (0.85, 0.62, 0.12), chips=1.0, rough=0.5, dirt=0.4,
                  stripes=((0.86, 0.63, 0.12), (0.07, 0.07, 0.075)), stripe_scale=2.4)
    coop = make_mat(painted_metal, "pst_kooperatif", (0.18, 0.36, 0.56), chips=0.7, rough=0.45, dirt=0.3)
    objs = [box("taban", (3.2, 3.2, 0.24), (0, 0, 0.12), bevel=0.03, mat=frame)]
    # uyari seridi: dort kenar bandi
    w = 0.26
    for s in (-1, 1):
        objs.append(box("serit", (3.0, w, 0.06), (0, s * (1.5 - w / 2), 0.27), bevel=0.012, mat=hz))
        objs.append(box("serit", (w, 3.0 - 2 * w, 0.06), (s * (1.5 - w / 2), 0, 0.27), bevel=0.012, mat=hz))
    # dort sac (araliklari derz)
    q = (3.0 - 2 * w - 0.03) / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            objs.append(box("sac", (q, q, 0.08), (sx * (q / 2 + 0.015), sy * (q / 2 + 0.015), 0.28), bevel=0.01, mat=deck))
    # hedef halkasi
    bpy.ops.mesh.primitive_torus_add(major_radius=0.62, minor_radius=0.05, major_segments=40, minor_segments=8, location=(0, 0, 0.33))
    ring = bpy.context.object; ring.name = "halka"; ring.scale = (1, 1, 0.35); bpy.ops.object.transform_apply(scale=True)
    finish(ring); ring.data.materials.append(coop); objs.append(ring)
    objs.append(cyl("merkez", 0.2, 0.06, (0, 0, 0.33), verts=24, bevel=0.01, mat=coop))
    # kose direkleri: kisa, tiknaz, ustte isik yuvasi
    for sx in (-1, 1):
        for sy in (-1, 1):
            p = (sx * 1.48, sy * 1.48, 0)
            objs.append(cyl("direk", 0.07, 0.42, (p[0], p[1], 0.45), verts=12, bevel=0.01, mat=hz))
            objs.append(cyl("direk_ust", 0.09, 0.06, (p[0], p[1], 0.69), verts=12, bevel=0.01, mat=frame))
    # civata sirasi (cerceve kenari)
    for i in range(-6, 7):
        for s in (-1, 1):
            objs.append(cyl("civata", 0.022, 0.02, (i * 0.23, s * 1.58, 0.24), verts=6, bevel=0.004, mat=frame))
    bake_and_export("rampa_pisti", objs, o["tex"], o["samples"], parts={"pad": objs})
    preview("rampa_pisti", 4.6, 0.3)


def build_roket(o):
    """Kargo roketi (2,2 m): donel govde + burun, kooperatif bandi, uyari seridi, dort kanat, motor cani. Dugum: 'rocket'."""
    reset()
    sc = (0.42, 1.25)
    body = make_mat(painted_metal, "rkt_govde", (0.8, 0.78, 0.73), chips=0.9, rough=0.45, dirt=0.55, scorch=sc)
    coop = make_mat(painted_metal, "rkt_kooperatif", (0.2, 0.33, 0.48), chips=1.0, rough=0.48, dirt=0.4, scorch=sc)
    hz = make_mat(painted_metal, "rkt_serit", (0.85, 0.62, 0.12), chips=1.0, rough=0.5, dirt=0.4,
                  stripes=((0.82, 0.6, 0.13), (0.07, 0.07, 0.075)), stripe_scale=7.0, scorch=sc)
    noz = make_mat(bare_metal, "rkt_cani", base=(0.2, 0.19, 0.18), rough=0.35, rust=0.1, soot=0.55)
    seam = make_mat(bare_metal, "rkt_derz", base=(0.22, 0.22, 0.23), rough=0.5, rust=0.2, soot=0.2)
    objs = []
    R = 0.29
    prof = [(0.0, 0.42), (0.25, 0.42), (R, 0.47), (R, 1.62), (R - 0.01, 1.66), (0.0, 1.66)]
    objs.append(lathe("govde", prof, 32, body))
    nose = [(0.0, 1.64), (R - 0.005, 1.64), (R - 0.02, 1.8), (0.24, 1.96), (0.17, 2.12), (0.09, 2.24), (0.035, 2.3), (0.0, 2.31)]
    objs.append(lathe("burun", nose, 32, coop))
    objs.append(lathe("uc", [(0.0, 2.27), (0.05, 2.27), (0.04, 2.31), (0.0, 2.335)], 16, noz))
    objs.append(lathe("bant", [(0.0, 1.08), (R + 0.008, 1.08), (R + 0.008, 1.26), (0.0, 1.26)], 32, coop))
    objs.append(lathe("serit", [(0.0, 0.5), (R + 0.008, 0.5), (R + 0.008, 0.6), (0.0, 0.6)], 32, hz))
    # panel derzleri: govdeyi saran ince halkalar ve dikey sac eklemleri
    for z in (0.78, 1.42, 1.64):
        objs.append(lathe("derz", [(0.0, z - 0.012), (R + 0.012, z - 0.012), (R + 0.014, z), (R + 0.012, z + 0.012), (0.0, z + 0.012)], 32, seam))
    for i in range(6):
        a = i * math.tau / 6
        objs.append(box("dikis", (0.016, 0.016, 1.12), (math.cos(a) * (R + 0.004), math.sin(a) * (R + 0.004), 1.06), rot=(0, 0, a), bevel=0.004, mat=seam))
    # yonelim iticileri (RCS) ve kapak plakasi
    for i in range(4):
        a = i * math.pi / 2
        objs.append(box("rcs", (0.07, 0.12, 0.1), (math.cos(a) * (R + 0.03), math.sin(a) * (R + 0.03), 1.5), rot=(0, 0, a), bevel=0.012, mat=seam))
    objs.append(box("kapak", (0.03, 0.2, 0.26), (R + 0.012, 0, 0.95), bevel=0.008, mat=coop))
    objs.append(lathe("cani", [(0.11, 0.44), (0.16, 0.4), (0.24, 0.16), (0.27, 0.05), (0.235, 0.05), (0.13, 0.3), (0.085, 0.42), (0.11, 0.44)], 28, noz))
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        me = bpy.data.meshes.new("kanat")
        bm = bmesh.new()
        pts = [(R - 0.02, 0.78), (0.6, 0.22), (0.62, 0.0), (0.5, 0.0), (R - 0.02, 0.3)]
        t = 0.05
        f1 = [bm.verts.new((x, -t / 2, z)) for x, z in pts]
        f2 = [bm.verts.new((x, t / 2, z)) for x, z in pts]
        bm.faces.new(f1[::-1]); bm.faces.new(f2)
        for j in range(len(pts)):
            k = (j + 1) % len(pts)
            bm.faces.new((f1[j], f1[k], f2[k], f2[j]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me); bm.free()
        k = bpy.data.objects.new("kanat", me)
        bpy.context.collection.objects.link(k)
        k.rotation_euler = (0, 0, a)
        bpy.context.view_layer.objects.active = k
        k.select_set(True)
        bpy.ops.object.transform_apply(rotation=True)
        finish(k, 0.01)
        k.data.materials.append(coop)
        objs.append(k)
    bake_and_export("kargo_roketi", objs, o["tex"], o["samples"], parts={"rocket": objs})
    preview("kargo_roketi", 3.6, 1.0)


BUILDERS = {"ocakbasi": build_ocakbasi, "rampa_pisti": build_rampa, "kargo_roketi": build_roket}

if __name__ == "__main__":
    opts = argv()
    for name, fn in BUILDERS.items():
        if opts["only"] and name not in opts["only"]:
            continue
        fn(opts)
    print("PROPS RESULT ok", flush=True)
