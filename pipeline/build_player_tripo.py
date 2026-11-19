"""
Oyuncu modelini Tripo'dan gelen insan bicimli (Mixamo) iskeletli modelden kurar: game/assets/models/player/player.glb.
Kredi harcamaz. Eski hat (Genex/Meshy) icin pipeline/merge_player.py; bu betik onun yerini alir.

  blender -b --factory-startup --python-exit-code 1 -P pipeline/build_player_tripo.py -- \
      --new  .tools/tripo_web/oyuncu/kabarma_oyuncu_rig.glb \
      --old  <eski Meshy player.glb: git'te game/assets/models/player/player.glb'nin onceki surumu> \
      --bag  .tools/tripo_web/oyuncu/kabarma_canta_low.glb   (pipeline/prep_bag.py cikisi) \
      --anims game/data/player_anims.json --out game/assets/models/player/player.glb

Asamalar:
 1. Tripo iskeleti 1,6 m'ye olceklenir (yalniz govde+kemikler; Armature dugumu 1,0 kalir), kemikler ESKI adlara cevrilir
    (Mixamo Spine/Spine1/Spine2 -> Spine02/Spine01/Spine: eski iskelette zincir Hips>Spine02>Spine01>Spine>neck>Head idi;
    mixamorig: oneki atilir). Boylece C# kodu (PlayerBody, PlayerRig, PlayerPoses, Grip) degismez.
 2. Tripo'nun kendi klipleri (idle, walk, cheer, greet_01 -> wave) kemik adlari degisip aynen kullanilir.
 3. Eski Meshy kliplerinin geri kalani (run, sprint, jump, ...) YENIDEN HEDEFLENIR: her karede eski kemigin dunya donusu
    G_eski okunur; yeni kemik G_yeni = G_eski * L (L: dinlenme durusunun yalniz Y-ekseni burulmasi; yonu eskisiyle ayni
    tutar, A-durus/T-durus farkini siler). Kalca konumu kalca yuksekligi oraniyla olceklenir. Yerel donus
    pozlama zincirinden hesaplanir (ust kemik once).
 4. Canta: ayri, derisiz, KATI mesh; ust kemigi Spine (gogus) olan "Backpack" kemigine bagli (BoneAttachment3D).
 5. Denetim: beklenen klipler var, Backpack eklemi var, Backpack_Mesh derisiz ve Backpack'in cocugu.
"""
import bpy, sys, os, json, math, argparse
from mathutils import Matrix, Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--new", required=True)
ap.add_argument("--old", required=True)
ap.add_argument("--bag", required=True)
ap.add_argument("--anims", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--height", type=float, default=1.6)
ap.add_argument("--bag-height", type=float, default=0.42)
ap.add_argument("--bag-rot", type=float, nargs=3, default=[0, 0, 180], help="canta mesh'i icin XYZ derece (Blender, kemik cercevesinde onceden)")
ap.add_argument("--bag-sink", type=float, default=0.02, help="canta govdeye bu kadar gomulur (m)")
ap.add_argument("--keep-native", nargs="*", default=[], help="Tripo klibi:kimlik. Bos: hepsi eski Meshy kliplerinden yeniden hedeflenir (Tripo idle/cheer/greet_01 T-poz sabit cikti, walk 2,33 sn ile oyunun yurume hizina uymadi)")
ap.add_argument("--step", type=int, default=4, help="yeniden hedefleme ornekleme adimi (kare; sahne 120 fps)")
args = ap.parse_args(argv)

anims = json.load(open(args.anims, encoding="utf-8"))
real_clips = [c["id"] for c in anims["clips"] if c.get("source") in ("library", "locomotion") and c.get("action") and not c.get("placeholder")]
native = {}
for kv in args.keep_native:
    a, _, b = kv.partition(":")
    native[a] = b or a
RETARGET = [c for c in real_clips if c not in native.values()]


def fail(msg):
    print("BUILD FAIL:", msg)
    sys.exit(1)


def fcurves(a):
    if hasattr(a, "fcurves"):
        return list(a.fcurves)
    out = []
    for layer in a.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                out.extend(cb.fcurves)
    return out


scene = bpy.context.scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 120   # kare = 1/120 sn: glTF zaman <-> kare cevirisi kaybetmez

# ---------- eski iskelet (yalniz klip kaynagi) ----------
bpy.ops.import_scene.gltf(filepath=args.old)
old = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
old.name = "OLD"
for o in list(bpy.context.scene.objects):
    if o.type != "ARMATURE":
        bpy.data.objects.remove(o, do_unlink=True)
old_actions = {a.name: a for a in bpy.data.actions}
for a in old_actions.values():
    a.name = "old_" + a.name
    a.use_fake_user = True
old_actions = {k: bpy.data.actions["old_" + k] for k in old_actions}

# ---------- yeni model ----------
bpy.ops.import_scene.gltf(filepath=args.new)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE" and o is not old)
body = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.parent is arm]
if len(body) != 1:
    fail(f"tek govde mesh'i bekleniyordu, {len(body)} bulundu")
body = body[0]
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and o is not body:
        bpy.data.objects.remove(o, do_unlink=True)
arm.name = "Armature"
body.name = "char1"
new_actions = [a for a in bpy.data.actions if not a.name.startswith("old_")]
for a in new_actions:
    a.use_fake_user = True
arm.animation_data_clear()

# olcek: govde yuksekligi -> --height; ayaklar y=0
bpy.context.view_layer.update()
zs = [(body.matrix_world @ v.co).z for v in body.data.vertices]
h0, zmin = max(zs) - min(zs), min(zs)
s = args.height / h0
print(f"NEW height {h0:.4f} -> scale {s:.4f}, zmin {zmin:.4f}")
mw = body.matrix_world.copy()
body.parent = None
body.matrix_world = mw
arm.scale = (s, s, s)
body.scale = (s, s, s)
bpy.ops.object.select_all(action="DESELECT")
for o in (arm, body):
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
body.parent = arm
body.matrix_parent_inverse = Matrix.Identity(4)
for a in new_actions:
    for fc in fcurves(a):
        if fc.data_path.endswith(".location"):
            for kp in fc.keyframe_points:
                kp.co[1] *= s; kp.handle_left[1] *= s; kp.handle_right[1] *= s

# ayaklari yere (z=0) otur: govde min z'si ~0 degilse kaydir (kemikler ve mesh birlikte)
bpy.context.view_layer.update()
zmin2 = min((body.matrix_world @ v.co).z for v in body.data.vertices)
print("zmin after scale", zmin2)

# ---------- kemik adlari ----------
RENAME = {"Spine": "Spine02", "Spine1": "Spine01", "Spine2": "Spine", "Neck": "neck", "HeadTop_End": "head_end"}


def newname(n):
    n = n.replace("mixamorig:", "")
    return RENAME.get(n, n)


path_map = {}
for b in arm.data.bones:
    path_map[f'pose.bones["{b.name}"]'] = f'pose.bones["{newname(b.name)}"]'
# gecici ad (cakisma onlemi: Spine -> Spine02 iken Spine2 -> Spine)
for b in arm.data.bones:
    b.name = "TMP_" + b.name
for b in arm.data.bones:
    b.name = newname(b.name[4:])
# Blender kemik adi degisince govdenin kose gruplarini kendisi yeniden adlandirir; dogrula\nbad = [vg.name for vg in body.vertex_groups if vg.name not in arm.data.bones]\nif bad:\n    fail(f"kose grubu kemik adiyla eslesmiyor: {bad[:5]}")\nfor a in new_actions:
    for fc in fcurves(a):
        for k, v in path_map.items():
            if fc.data_path.startswith(k + "."):
                fc.data_path = v + fc.data_path[len(k):]
                break

# ---------- yeniden hedefleme ----------
OLD_MAP = [b.name for b in old.data.bones if b.name in arm.data.bones and b.name not in ("head_end", "Backpack", "headfront")]
print("MAPPED", len(OLD_MAP), OLD_MAP)


def rest_q(arm_obj, name):
    return arm_obj.data.bones[name].matrix_local.to_3x3().to_quaternion()


def rest_dir(arm_obj, name):
    return (arm_obj.data.bones[name].matrix_local.to_3x3() @ Vector((0, 1, 0))).normalized()


L = {}
for n in OLD_MAP:
    Ro, Rn = rest_q(old, n), rest_q(arm, n)
    S = rest_dir(old, n).rotation_difference(rest_dir(arm, n))
    L[n] = ((S @ Ro).inverted() @ Rn)
    # kontrol: L yalniz Y ekseni burulmasi mi
    ax = (L[n].to_matrix() @ Vector((0, 1, 0)))
    if (ax - Vector((0, 1, 0))).length > 1e-3:
        fail(f"{n}: L Y-burulmasi degil {ax}")

order = [b.name for b in arm.data.bones]          # ust kemik once
parent = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
Rrel = {}
for b in arm.data.bones:
    Rb = rest_q(arm, b.name)
    Rrel[b.name] = (rest_q(arm, b.parent.name).inverted() @ Rb) if b.parent else Rb
hips_old_rest = old.data.bones["Hips"].head_local.copy()
hips_new_rest = arm.data.bones["Hips"].head_local.copy()
ratio = hips_new_rest.z / hips_old_rest.z
print("hips ratio", ratio)


def sample_old(act, frames):
    ad = old.animation_data or old.animation_data_create()
    ad.action = act
    if hasattr(ad, "action_slot") and len(act.slots):
        ad.action_slot = act.slots[0]
    out = []
    for f in frames:
        scene.frame_set(f)
        bpy.context.view_layer.update()
        out.append(({n: old.pose.bones[n].matrix.to_quaternion() for n in OLD_MAP}, old.pose.bones["Hips"].head.copy()))
    return out


def bake(cid, act_old):
    f0, f1 = int(round(act_old.frame_range[0])), int(round(act_old.frame_range[1]))
    frames = list(range(f0, f1, args.step)) + [f1]
    data = sample_old(act_old, frames)
    act = bpy.data.actions.new(cid)
    act.use_fake_user = True
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = act
    prev = {}
    for f, (G_old, hips_old) in zip(frames, data):
        G = {}
        for n in order:
            Gp = G[parent[n]] if parent[n] else Quaternion()
            base = Gp @ Rrel[n]
            pb = arm.pose.bones[n]
            pb.rotation_mode = "QUATERNION"
            if n in L:
                tgt = G_old[n] @ L[n]
                q = base.inverted() @ tgt
                if n in prev and prev[n].dot(q) < 0:
                    q.negate()
                prev[n] = q
                G[n] = tgt
                pb.rotation_quaternion = q
                pb.keyframe_insert("rotation_quaternion", frame=f)
            else:
                G[n] = base
        tgt_head = hips_new_rest + ratio * (hips_old - hips_old_rest)
        loc = rest_q(arm, "Hips").inverted() @ (tgt_head - hips_new_rest)
        pbh = arm.pose.bones["Hips"]
        pbh.location = loc
        pbh.keyframe_insert("location", frame=f)
    return act


retargeted = {}
for cid in RETARGET:
    if cid not in old_actions:
        fail(f"eski klip yok: {cid}")
    retargeted[cid] = bake(cid, old_actions[cid])
    print("RETARGET", cid, len(fcurves(retargeted[cid])), "kanal")
arm.animation_data_clear()
scene.frame_set(0)
for pb in arm.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)

# tutulan Tripo klipleri
final_actions = dict(retargeted)
for a in new_actions:
    if a.name in native:
        a.name = native[a.name]
        final_actions[a.name] = a
for a in new_actions:
    if a.name not in final_actions.values() and a.name not in native.values():
        pass
missing = [c for c in real_clips if c not in final_actions]
if missing:
    fail(f"klipler eksik: {missing}")

# ---------- Backpack ----------
CHEST = "Spine"
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
chest_head = arm.data.edit_bones[CHEST].head.copy()
bpy.ops.object.mode_set(mode="OBJECT")
bz = [(body.matrix_world @ v.co) for v in body.data.vertices]
TORSO = {"Spine", "Spine01", "Spine02", "Hips"}
tg = {vg.index for vg in body.vertex_groups if vg.name in TORSO}
# saca/etek degil, govdenin kendi sirti: agirligi cogunlukla omurga kemiklerinde olan koseler
back_y = max(bz[v.index].y for v in body.data.vertices
             if abs(bz[v.index].x) < 0.12 and chest_head.z - 0.15 < bz[v.index].z < chest_head.z + 0.15
             and sum(g.weight for g in v.groups if g.group in tg) > 0.3)
print("chest", tuple(chest_head), "back_y", back_y)

bpy.ops.import_scene.gltf(filepath=args.bag)
bag_objs = [o for o in bpy.context.scene.objects if o.type == "MESH" and o is not body]
if len(bag_objs) != 1:
    fail("canta mesh'i tek olmali")
bag = bag_objs[0]
for o in list(bpy.context.scene.objects):
    if o.type == "EMPTY":
        bpy.data.objects.remove(o, do_unlink=True)
bag.parent = None
bag.name = "Backpack_Mesh"; bag.data.name = "Backpack_Mesh"
bag.rotation_euler = tuple(math.radians(a) for a in args.bag_rot)
bpy.context.view_layer.objects.active = bag
bag.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
# boyut: yukseklik (Z)
bb = [bag.matrix_world @ v.co for v in bag.data.vertices]
sz = max(p.z for p in bb) - min(p.z for p in bb)
k = args.bag_height / sz
for v in bag.data.vertices:
    v.co *= k
bb = [v.co.copy() for v in bag.data.vertices]
minv = Vector((min(p.x for p in bb), min(p.y for p in bb), min(p.z for p in bb)))
maxv = Vector((max(p.x for p in bb), max(p.y for p in bb), max(p.z for p in bb)))
print("BAG size", tuple(maxv - minv))
# yerlestir: x ortada, on yuz (-Y yonu = govdeye bakan taraf) sirta gomulu, ust kenar omuz hizasinda
top_z = chest_head.z + 0.03
off = Vector((-(minv.x + maxv.x) / 2, back_y - args.bag_sink - minv.y, top_z - maxv.z))
for v in bag.data.vertices:
    v.co += off
bag_head = Vector((0, back_y, top_z))

bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.data.edit_bones
bp = eb.new("Backpack")
# bone frame: X = +X, Y = asagi (-Z), Z = geri (+Y)  -> sutunlar
M = Matrix.Identity(4)
M.col[0] = (1, 0, 0, 0); M.col[1] = (0, 0, -1, 0); M.col[2] = (0, 1, 0, 0); M.col[3] = (bag_head.x, bag_head.y, bag_head.z, 1)
bp.parent = eb[CHEST]
bp.use_connect = False
bp.matrix = M
bp.length = 0.1
bp.use_deform = True
bpy.ops.object.mode_set(mode="OBJECT")

keep = bag.matrix_world.copy()
bag.parent = arm
bag.parent_type = "BONE"
bag.parent_bone = "Backpack"
bpy.context.view_layer.update()
bag.matrix_world = keep
bpy.context.view_layer.update()
bag.modifiers.clear() if hasattr(bag.modifiers, "clear") else None

# ---------- NLA ve disa aktarim ----------
arm.animation_data_create()
for name, a in sorted(final_actions.items()):
    tr = arm.animation_data.nla_tracks.new(); tr.name = name
    a.name = name
    st = tr.strips.new(name, int(a.frame_range[0]), a)
    if hasattr(st, "action_slot") and st.action_slot is None and len(getattr(a, "slots", [])) > 0:
        st.action_slot = a.slots[0]
arm.animation_data.action = None
os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
bpy.ops.export_scene.gltf(filepath=args.out, export_format="GLB", export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_skins=True, export_yup=True)
print("EXPORTED", args.out)

# ---------- denetim ----------
import struct
data = open(args.out, "rb").read()
jlen = struct.unpack_from("<I", data, 12)[0]
js = json.loads(data[20:20 + jlen])
problems = []
durs = {}
for an in js.get("animations", []):
    dur = 0.0
    for sm in an["samplers"]:
        acc = js["accessors"][sm["input"]]
        if "max" in acc:
            dur = max(dur, acc["max"][0])
    durs[an["name"]] = (dur, len(an["channels"]))
for c in real_clips:
    d = durs.get(c)
    if d is None:
        problems.append(f"{c}: ciktida yok")
    elif d[0] < 0.2 or d[1] < 20:
        problems.append(f"{c}: bos klip {d}")
joints = {js["nodes"][j].get("name") for sk in js.get("skins", []) for j in sk["joints"]}
if "Backpack" not in joints:
    problems.append("Backpack eklemi yok")
bn = [i for i, nd in enumerate(js["nodes"]) if nd.get("name") == "Backpack_Mesh" and "mesh" in nd]
bj = [i for i, nd in enumerate(js["nodes"]) if nd.get("name") == "Backpack"]
if not bn:
    problems.append("Backpack_Mesh dugumu yok")
elif "skin" in js["nodes"][bn[0]]:
    problems.append("Backpack_Mesh derili")
elif not bj or bn[0] not in js["nodes"][bj[0]].get("children", []):
    problems.append("Backpack_Mesh Backpack'in cocugu degil")
tri = 0
for nd in js["nodes"]:
    if "mesh" in nd:
        for pr in js["meshes"][nd["mesh"]]["primitives"]:
            tri += js["accessors"][pr["indices"]]["count"] // 3
for n, (d, c) in sorted(durs.items()):
    print(f"OUT {n:14s} {d:6.2f} sn  {c} kanal")
print("TRIS", tri, "JOINTS", len(joints))
if problems:
    fail("; ".join(problems))
print(f"BUILD OK {len(durs)} klip")




