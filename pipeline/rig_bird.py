"""Kus govde plani icin iskelet kurar ve agirliklari verir (URETIM §2.2 E.1, M0 denemesi 4; kredi yok).

Blender 5.3 bassiz:
  blender -b --factory-startup --python-exit-code 1 -P pipeline/rig_bird.py -- \
      --in game/assets/models/gidak/gidak.glb --out game/assets/models/gidak/gidak_rig.glb \
      --name gidak_rig --height 0.75 --src_forward +x --renders progress/m1/yaratik/gidak_rig

Ne yapar:
  1. Modeli alir, ileri yonu glTF +Z'ye (Godot'ta ileri) cevirir, boyu --height'a, tabani 0'a oturtur.
  2. Bicimi olcer (bacak sutunlari, govde alti, kanat uclari, kuyruk, tepelik) ve kemikleri bu olculere gore
     yerlestirir. Kemik adlari poz kutuphanesinin rol adlaridir (game/data/poses.json "bird"):
     root, body, head, comb, tail, wing_l, wing_r, thigh_l, shin_l, foot_l, thigh_r, shin_r, foot_r.
  3. Agirlik: once Blender'in isi yontemi (ARMATURE_AUTO). Agirliksiz kose kalirsa ya da bacak/kanat bolgesi
     yanlis kemige baglanirsa geometrik yonteme duser: koseden kemik parcasina uzaklik, bolge kurallariyla.
  4. Denetim: agirliksiz kose sayisi, bolge dogrulugu; poz verilmis dort goruntu (kanat, bacak, bas, kuyruk).
  5. Disa aktarim: GLB, deri (skin) ile, kliplersiz (klipler oyunda PoseLibrary'den kurulur). Rapor: <out>.report.json.
"""
import bpy, bmesh, sys, os, json, math, argparse
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="inp", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--name", required=True)
ap.add_argument("--height", type=float, default=0.75)
ap.add_argument("--src_forward", default="+x", help="kaynak modelin baktigi glTF ekseni: +x, -x, +z, -z")
ap.add_argument("--weights", default="auto", choices=["auto", "heat", "geo"], help="auto: isi, tutmazsa geometrik")
ap.add_argument("--renders", default="")
args = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(args.inp))
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
for o in list(bpy.context.scene.objects):
    if o != obj:
        bpy.data.objects.remove(o, do_unlink=True)

# --- 1. yon, boy, taban ---
# glTF ekseni -> Blender ekseni: +x -> +X, +z -> -Y. Hedef ileri: glTF +Z = Blender -Y.
fwd_bl = {"+x": Vector((1, 0, 0)), "-x": Vector((-1, 0, 0)), "+z": Vector((0, -1, 0)), "-z": Vector((0, 1, 0))}[args.src_forward]
target = Vector((0, -1, 0))
ang = math.atan2(fwd_bl.cross(target).z, fwd_bl.dot(target))
obj.matrix_world = Matrix.Rotation(ang, 4, "Z") @ obj.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
co = [v.co.copy() for v in me.vertices]
mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
s = args.height / (mx.z - mn.z)
off = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
for v in me.vertices:
    v.co = (v.co - off) * s
me.update()
co = [v.co.copy() for v in me.vertices]
H = args.height
W = max(c.x for c in co) - min(c.x for c in co)
D = max(c.y for c in co) - min(c.y for c in co)

# --- 2. bicim olcumu (hepsi H, W, D oranlariyla; metre sabiti yok) ---
def slice_width(z0, z1):
    xs = [c.x for c in co if z0 <= c.z < z1]
    return (max(xs) - min(xs)) if xs else 0.0

# Govde alti: alttan yukari dilimlerde genislik toplam genisligin %55'ini ilk gectigi yukseklik.
step = H / 60
body_bottom = H * 0.2
z = 0.0
while z < H * 0.5:
    if slice_width(z, z + step) > 0.55 * W:
        body_bottom = z
        break
    z += step
leg = {}
for side, sgn in (("l", 1), ("r", -1)):
    pts = [c for c in co if c.z < body_bottom and c.x * sgn > 0]
    if not pts:
        pts = [Vector((sgn * W * 0.15, 0, 0))]
    upper = [c for c in pts if c.z > body_bottom * 0.45] or pts
    lx = sum(c.x for c in upper) / len(upper)
    ly = sum(c.y for c in upper) / len(upper)
    toe = min(pts, key=lambda c: c.y)
    leg[side] = dict(x=lx, y=ly, toe_y=toe.y, toe_z=max(toe.z, H * 0.01))
# Kanat: govdenin orta bandinda en dis koseler.
mid = [c for c in co if body_bottom + 0.1 * H < c.z < 0.7 * H]
wing = {}
for side, sgn in (("l", 1), ("r", -1)):
    side_pts = [c for c in mid if c.x * sgn > 0]
    ext = max(abs(c.x) for c in side_pts)
    tip = [c for c in side_pts if abs(c.x) > ext * 0.9]
    wz = sum(c.z for c in tip) / len(tip)
    wy = sum(c.y for c in tip) / len(tip)
    wing[side] = dict(x_in=sgn * ext * 0.62, x_out=sgn * ext * 0.97, y=wy, z_top=wz + 0.07 * H, z_tip=wz - 0.04 * H)
# Kuyruk: arkadaki (+Y) en uc nokta; tepelik: en ustteki nokta.
back = max(co, key=lambda c: c.y)
top = max(co, key=lambda c: c.z)
beak = min((c for c in co if c.z > 0.4 * H), key=lambda c: c.y)
head_base = 0.58 * H
body_y = sum(c.y for c in mid) / len(mid)
comb_base = top.z - 0.14 * H

J = {
    "root": (Vector((0, body_y, 0)), Vector((0, body_y, 0.12 * H))),
    "body": (Vector((0, body_y, body_bottom + 0.06 * H)), Vector((0, body_y, head_base))),
    "head": (Vector((0, body_y, head_base)), Vector((0, body_y, comb_base))),
    "comb": (Vector((0, top.y, comb_base)), Vector((0, top.y, top.z))),
    "tail": (Vector((0, back.y - 0.25 * D, back.z - 0.02 * H)), Vector((0, back.y, back.z + 0.04 * H))),
}
for side in ("l", "r"):
    w = wing[side]
    J[f"wing_{side}"] = (Vector((w["x_in"], w["y"], w["z_top"])), Vector((w["x_out"], w["y"], w["z_tip"])))
    L = leg[side]
    hip = Vector((L["x"], L["y"], body_bottom + 0.08 * H))
    knee = Vector((L["x"], L["y"], body_bottom * 0.62))
    ankle = Vector((L["x"], L["y"], max(L["toe_z"] + 0.02 * H, body_bottom * 0.2)))
    toe = Vector((L["x"], L["toe_y"], L["toe_z"]))
    J[f"thigh_{side}"] = (hip, knee)
    J[f"shin_{side}"] = (knee, ankle)
    J[f"foot_{side}"] = (ankle, toe)
PARENT = {"body": "root", "head": "body", "comb": "head", "tail": "body",
          "wing_l": "body", "wing_r": "body", "thigh_l": "body", "thigh_r": "body",
          "shin_l": "thigh_l", "shin_r": "thigh_r", "foot_l": "shin_l", "foot_r": "shin_r"}

arm_data = bpy.data.armatures.new(args.name + "_skel")
arm = bpy.data.objects.new("Armature", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
for name, (h, t) in J.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = h, t
    if (t - h).length < 1e-4:
        b.tail = h + Vector((0, 0, 0.02 * H))
for name, p in PARENT.items():
    arm_data.edit_bones[name].parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode="OBJECT")

# --- 3. agirliklar ---
def region(c):
    """Bolge kurali: hangi kemik ailesine ait olabilir (geometrik yontem ve denetim icin)."""
    if c.z < body_bottom:
        return "leg_l" if c.x > 0 else "leg_r"
    for side, sgn in (("l", 1), ("r", -1)):
        w = wing[side]
        if c.x * sgn > abs(w["x_in"]) * 0.92 and w["z_tip"] - 0.12 * H < c.z < w["z_top"] + 0.08 * H:
            return "wing_" + side
    return "core"

FAMILY = {"leg_l": {"thigh_l", "shin_l", "foot_l"}, "leg_r": {"thigh_r", "shin_r", "foot_r"},
          "wing_l": {"wing_l"}, "wing_r": {"wing_r"}, "core": {"root", "body", "head", "comb", "tail"}}

def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

def geo_weights():
    obj.vertex_groups.clear()
    groups = {n: obj.vertex_groups.new(name=n) for n in J}
    for v in me.vertices:
        c = v.co
        reg = region(c)
        allowed = set(FAMILY[reg])
        # Bolge sinirinda yumusak gecis: bacak ustu ve kanat koku govdeyle paylasilir.
        if reg.startswith("leg") and c.z > body_bottom * 0.8:
            allowed |= {"body"}
        if reg.startswith("wing"):
            allowed |= {"body"}
        if reg == "core" and c.z < body_bottom + 0.06 * H:
            allowed |= {"thigh_l" if c.x > 0 else "thigh_r"}
        ws = []
        for n in allowed:
            d = seg_dist(c, *J[n])
            ws.append((n, 1.0 / (d + 0.02 * H) ** 4))
        if reg == "core":  # tepelik yalniz tepede, kuyruk yalniz arkada
            ws = [(n, w * (0.0 if n == "comb" and c.z < J["comb"][0].z - 0.02 * H else 1.0)) for n, w in ws]
            ws = [(n, w * (0.0 if n == "tail" and c.y < J["tail"][0].y - 0.05 * D else 1.0)) for n, w in ws]
        ws.sort(key=lambda x: -x[1])
        ws = ws[:3]
        tot = sum(w for _, w in ws) or 1.0
        for n, w in ws:
            if w / tot > 0.01:
                groups[n].add([v.index], w / tot, "REPLACE")

def check_weights():
    names = {g.index: g.name for g in obj.vertex_groups}
    unweighted = 0; wrong = 0; region_count = 0
    for v in me.vertices:
        gs = [(names[g.group], g.weight) for g in v.groups if g.weight > 1e-4 and g.group in names]
        if not gs:
            unweighted += 1; continue
        reg = region(v.co)
        if reg in ("leg_l", "leg_r", "wing_l", "wing_r") or v.co.z > head_base + 0.05 * H:
            region_count += 1
            dom = max(gs, key=lambda x: x[1])[0]
            fam = FAMILY[reg] if reg != "core" else {"head", "comb"}
            if dom not in fam:
                wrong += 1
    return dict(vertices=len(me.vertices), unweighted=unweighted, region_vertices=region_count,
                wrong_region=wrong, wrong_ratio=round(wrong / max(1, region_count), 3))

method = None; heat_error = None; heat_check = None
obj.parent = arm
if args.weights in ("auto", "heat"):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        heat_check = check_weights()
        if heat_check["unweighted"] == 0 and heat_check["wrong_ratio"] < 0.05:
            method = "heat"
    except Exception as e:  # isi cozumu bulunamazsa
        heat_error = str(e)
if method is None:
    if args.weights == "heat":
        raise SystemExit("isi agirligi tutmadi: " + str(heat_error or heat_check))
    obj.parent = arm
    geo_weights()
    if not any(m.type == "ARMATURE" for m in obj.modifiers):
        mod = obj.modifiers.new("Armature", "ARMATURE"); mod.object = arm
    method = "geo"
for m in obj.modifiers:
    if m.type == "ARMATURE":
        m.object = arm
final_check = check_weights()

# Malzeme ve gorsellerin adlari: Godot cikarirken eski dokularin ustune yazmasin.
for img in bpy.data.images:
    if img.name not in ("Render Result", "Viewer Node"):
        img.name = args.name + "_" + img.name
for mat in bpy.data.materials:
    mat.name = args.name + "_" + mat.name
obj.name = args.name; obj.data.name = args.name

report = {
    "name": args.name, "input": args.inp, "height": H, "src_forward": args.src_forward,
    "weights": method, "heat_error": heat_error, "heat_check": heat_check, "check": final_check,
    "tris": sum(len(p.vertices) - 2 for p in me.polygons),
    "bones": {n: {"head": [round(x, 3) for x in h], "tail": [round(x, 3) for x in t], "parent": PARENT.get(n)} for n, (h, t) in J.items()},
    "landmarks": {"body_bottom": round(body_bottom, 3), "head_base": round(head_base, 3), "beak_y": round(beak.y, 3), "W": round(W, 3), "D": round(D, 3)},
}
report["pass"] = final_check["unweighted"] == 0 and final_check["wrong_ratio"] < 0.05

# --- 4. poz verilmis denetim goruntuleri ---
if args.renders:
    out = os.path.abspath(args.renders); os.makedirs(out, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 512; sc.render.resolution_y = 512
    sc.display.shading.background_type = "VIEWPORT"; sc.display.shading.background_color = (0.83, 0.8, 0.74)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = H * 1.6
    ctr = Vector((0, 0, H * 0.5))
    poses = {
        "rest": {},
        "kanat_bacak": {"wing_l": ("Y", -55), "wing_r": ("Y", 20), "thigh_l": ("X", -40), "shin_l": ("X", 35), "head": ("X", 20), "tail": ("X", -30), "comb": ("Y", 25)},
    }
    arm.data.pose_position = "POSE"
    for pname, pose in poses.items():
        for pb in arm.pose.bones:
            pb.rotation_mode = "XYZ"; pb.rotation_euler = (0, 0, 0)
        for bn, (axis, deg) in pose.items():
            # Model ekseninde dondur: kemigin dinlenme matrisine gore yerel eksene cevir.
            pb = arm.pose.bones[bn]
            rest = pb.bone.matrix_local.to_3x3()
            ax = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}[axis]
            q = Matrix.Rotation(math.radians(deg), 3, rest.inverted() @ ax).to_quaternion()
            pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = q
        bpy.context.view_layer.update()
        for vname, d in (("on", Vector((0, -1, 0.25))), ("yan", Vector((1, 0, 0.2)))):
            cam.location = ctr + d.normalized() * H * 4
            cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
            sc.render.filepath = os.path.join(out, f"{args.name}_{pname}_{vname}.png")
            bpy.ops.render.render(write_still=True)
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()

# --- 5. disa aktarim ---
os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=os.path.abspath(args.out), export_format="GLB", use_selection=False, export_yup=True,
                          export_texcoords=True, export_normals=True, export_skins=True, export_animations=False)
rp = os.path.splitext(args.out)[0] + ".report.json"
json.dump(report, open(rp, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
print("RIG_REPORT " + json.dumps({k: report[k] for k in ("name", "weights", "check", "tris", "pass")}, ensure_ascii=False))
