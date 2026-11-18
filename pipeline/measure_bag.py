"""
Sirt cantasi her klipte bicim degistiriyor mu? Olcer, tahmin etmez (Blender 5.3, kredisiz).

  blender -b --factory-startup --python-exit-code 1 -P pipeline/measure_bag.py -- \
      --glb game/assets/models/player/player.glb [--bone Spine02] [--step 2] [--json progress/m1/oyuncu/log/canta_olcum.json]

1. Kemik izleri: her klipte konum izi oynayan kemikler (Hips disinda) ve olcegi 1'den sapan kemikler.
2. Sapma: sirt bolgesindeki her kosenin derilenmis konumu, --bone kemiginin KATI cercevesinde (olcek ayiklanmis
   donus + konum) her --step karede ornekleyip dinlenme konumundan en buyuk sapmasi. Koseler doku renginden "canta"
   (acik gri, doygunlugu dusuk) ya da "govde" diye ayrilir; en cok oynayan canta koseleri agirliklariyla yazilir.
   Ayri bir katı canta nesnesi varsa (iskelette bir kemige bagli, derisiz) onun koseleri de ayni yolla olculur.
Cikis kodu: --max verilirse ve cantanin sapmasi bunu asarsa 1.
"""
import bpy, sys, json, argparse
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--glb", required=True)
ap.add_argument("--bone", default="Spine02", help="cantanin vidalanacagi kemik (olcum cercevesi)")
ap.add_argument("--step", type=int, default=2)
ap.add_argument("--json", default="")
ap.add_argument("--max", type=float, default=-1.0, help="canta sapma siniri (m); asilirsa cikis 1")
args = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.glb)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
skinned = [o for o in bpy.context.scene.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
rigid_objs = [o for o in bpy.context.scene.objects if o.type == "MESH" and o not in skinned and o.parent_type == "BONE"]
body = max(skinned, key=lambda o: len(o.data.vertices))
print("OBJECTS skinned:", [o.name for o in skinned], "rigid:", [(o.name, o.parent_type, o.parent_bone) for o in rigid_objs])


def fcurves(a):
    if hasattr(a, "fcurves"):
        return list(a.fcurves)
    out = []
    for layer in a.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                out.extend(cb.fcurves)
    return out


def bind(a):
    arm.animation_data_create()
    arm.animation_data.action = a
    if hasattr(arm.animation_data, "action_slot") and len(getattr(a, "slots", [])) > 0:
        arm.animation_data.action_slot = a.slots[0]


actions = sorted(bpy.data.actions, key=lambda a: a.name)

# ---- 1. Kemik izleri ----
tracks = {}
for a in actions:
    loc, scl = {}, {}
    for fc in fcurves(a):
        dp = fc.data_path
        if not dp.startswith('pose.bones["'):
            continue
        bone = dp.split('"')[1]
        vals = [kp.co[1] for kp in fc.keyframe_points]
        if not vals:
            continue
        if dp.endswith(".scale"):
            dev = max(abs(v - 1.0) for v in vals)
            if dev > 1e-3:
                scl[bone] = max(scl.get(bone, 0.0), dev)
        elif dp.endswith(".location") and bone != "Hips":
            rng = max(vals) - min(vals)
            if rng > 1e-3:
                loc[bone] = max(loc.get(bone, 0.0), rng)
    tracks[a.name] = {"location_range_m": loc, "scale_dev": scl}
    print(f"TRACKS {a.name:14s} konum oynayan: {sorted(loc.items(), key=lambda kv: -kv[1])[:6]}  olcek: {sorted(scl.items(), key=lambda kv: -kv[1])[:6]}")

# ---- 2. Doku rengi: canta mi govde mi ----
def base_image(o):
    for slot in o.material_slots:
        m = slot.material
        if not m or not m.use_nodes:
            continue
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED":
                link = n.inputs["Base Color"].links
                if link and link[0].from_node.type == "TEX_IMAGE":
                    return link[0].from_node.image
    return None


def vertex_colors(o):
    img = base_image(o)
    n = len(o.data.vertices)
    if img is None or not o.data.uv_layers:
        return np.full((n, 3), 0.5)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, img.channels)
    uv = o.data.uv_layers.active.data
    col = np.zeros((n, 3), dtype=np.float32)
    cnt = np.zeros(n)
    for loop in o.data.loops:
        u, v = uv[loop.index].uv
        x = min(w - 1, max(0, int((u % 1.0) * w)))
        y = min(h - 1, max(0, int((v % 1.0) * h)))
        col[loop.vertex_index] += px[y, x, :3]
        cnt[loop.vertex_index] += 1
    return col / np.maximum(cnt, 1)[:, None]


def rest_world(o):
    me = o.data
    co = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(o.matrix_world)
    return co @ mw[:3, :3].T + mw[:3, 3]


body_rest = rest_world(body)
cols = vertex_colors(body)
mx, mn = cols.max(1), cols.min(1)
sat = (mx - mn) / np.maximum(mx, 1e-4)
is_bag_color = sat < 0.3  # gri (canta govdesi acik ve orta gri); tulum lacivert, seritler sari
# Sirt bolgesi (Blender ekseni: govde -Y'ye bakar, sirt +Y): canta ve cevresi.
p = body_rest
region = (p[:, 1] > 0.03) & (p[:, 2] > 0.55) & (p[:, 2] < 1.45) & (np.abs(p[:, 0]) < 0.42)
hn = {g.index for g in body.vertex_groups if g.name in ("Head", "neck")}
head_w = np.array([sum(g.weight for g in v.groups if g.group in hn) for v in body.data.vertices])
# Cantanin kutusu (merge_player.py: |x| < 0,265, z 0,71..1,315, y > 0,03); basa bagli koseler kasktir, canta degil.
bag_box = (np.abs(p[:, 0]) < 0.265) & (p[:, 2] > 0.71) & (p[:, 2] < 1.315) & (p[:, 1] > 0.03)
bag_v = region & bag_box & is_bag_color & (head_w < 0.5)
# Cantanin altindaki sirt (tulum): kati canta hangi kemige vidalanirsa ona gore en az oynamali (bosluk ve icine gecme).
contact = (p[:, 1] > 0.0) & (p[:, 1] < 0.065) & (np.abs(p[:, 0]) < 0.25) & (p[:, 2] > 0.78) & (p[:, 2] < 1.28)
print(f"REGION sirt {int(region.sum())} kose; canta rengi {int(bag_v.sum())}; govde rengi {int((region & ~bag_v).sum())}; canta alti sirt {int(contact.sum())}")
bb = p[bag_v]
if len(bb):
    print(f"BAG BOX x {bb[:, 0].min():.3f}..{bb[:, 0].max():.3f}  y {bb[:, 1].min():.3f}..{bb[:, 1].max():.3f}  z {bb[:, 2].min():.3f}..{bb[:, 2].max():.3f}")

groups = {g.index: g.name for g in body.vertex_groups}


def weights(vi):
    v = body.data.vertices[vi]
    return sorted(((groups[g.group], round(g.weight, 3)) for g in v.groups if g.weight > 0.01), key=lambda t: -t[1])


def frame_of(bone):
    m = arm.matrix_world @ arm.pose.bones[bone].matrix
    loc, rot, _scl = m.decompose()
    return Matrix.Translation(loc) @ rot.to_matrix().to_4x4(), _scl


def deformed(o):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    co = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    ev.to_mesh_clear()
    co = co.reshape(-1, 3)
    mw = np.array(o.matrix_world)  # rigid_objs: kemige bagli nesnenin dunya matrisi (kemikle hareket eder)
    if o in rigid_objs:
        mw = np.array(o.evaluated_get(dg).matrix_world)
    return co @ mw[:3, :3].T + mw[:3, 3]


def in_frame(pts, F):
    Fi = np.array(F.inverted())
    return pts @ Fi[:3, :3].T + Fi[:3, 3]


# Dinlenme: eylem yok, iskelet dinlenme pozunda.
arm.data.pose_position = "REST"
bpy.context.view_layer.update()
F0, _ = frame_of(args.bone)
rest_body = in_frame(deformed(body), F0)
rest_rigid = {o.name: in_frame(deformed(o), F0) for o in rigid_objs}
arm.data.pose_position = "POSE"

report = {"bone": args.bone, "tracks": tracks, "clips": {}}
worst_bag = 0.0
for a in actions:
    bind(a)
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    dev_body = np.zeros(len(body.data.vertices))
    dev_rigid = {o.name: 0.0 for o in rigid_objs}
    bone_scale = 0.0
    for f in range(f0, f1 + 1, max(1, args.step)):
        bpy.context.scene.frame_set(f)
        F, s = frame_of(args.bone)
        bone_scale = max(bone_scale, max(abs(s.x - 1), abs(s.y - 1), abs(s.z - 1)))
        cur = in_frame(deformed(body), F)
        dev_body = np.maximum(dev_body, np.linalg.norm(cur - rest_body, axis=1))
        for o in rigid_objs:
            d = np.linalg.norm(in_frame(deformed(o), F) - rest_rigid[o.name], axis=1)
            dev_rigid[o.name] = max(dev_rigid[o.name], float(d.max()) if len(d) else 0.0)
    bag_dev = float(dev_body[bag_v].max()) if bag_v.any() else 0.0
    bag_p95 = float(np.percentile(dev_body[bag_v], 95)) if bag_v.any() else 0.0
    rigid_dev = max(dev_rigid.values()) if dev_rigid else 0.0
    worst_bag = max(worst_bag, bag_dev if not rigid_objs else rigid_dev)
    con_max = float(dev_body[contact].max()) if contact.any() else 0.0
    con_p95 = float(np.percentile(dev_body[contact], 95)) if contact.any() else 0.0
    top = np.argsort(-np.where(bag_v, dev_body, -1))[:8]
    print(f"CLIP {a.name:14s} kareler {f0}-{f1}  canta(deri) max={bag_dev*100:5.1f} cm p95={bag_p95*100:5.1f} cm  "
          f"kati nesne max={rigid_dev*100:5.2f} cm  canta alti sirt max={con_max*100:5.1f} p95={con_p95*100:5.1f} cm  "
          f"{args.bone} olcek sapmasi={bone_scale:.4f}")
    report.setdefault("contact", {})[a.name] = {"max_m": con_max, "p95_m": con_p95}
    for vi in top[:4]:
        if dev_body[vi] > 0.005:
            q = body_rest[vi]
            print(f"     kose {vi:5d} ({q[0]:+.2f},{q[1]:+.2f},{q[2]:+.2f}) {dev_body[vi]*100:5.1f} cm  {weights(vi)[:4]}")
    report["clips"][a.name] = {"bag_skinned_max_m": bag_dev, "bag_skinned_p95_m": bag_p95, "rigid_max_m": rigid_dev,
                               "bone_scale_dev": bone_scale,
                               "worst": [{"v": int(vi), "rest": [float(x) for x in body_rest[vi]], "dev_m": float(dev_body[vi]),
                                          "w": weights(vi)[:4]} for vi in top[:8]]}

# Hangi kemikler canta koselerini en cok tasiyor (agirlik toplamı, canta rengi koseler)
load = {}
for vi in np.nonzero(bag_v)[0]:
    for name, w in weights(int(vi)):
        load[name] = load.get(name, 0.0) + w
print("BAG WEIGHTS (canta rengi koselerin agirlik toplami):", sorted(((k, round(v, 1)) for k, v in load.items()), key=lambda kv: -kv[1])[:10])
report["bag_weight_load"] = load
report["worst_bag_m"] = worst_bag
if args.json:
    with open(args.json, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=1, ensure_ascii=False)
    print("JSON", args.json)
print(f"WORST bag {worst_bag*100:.2f} cm")
if args.max >= 0 and worst_bag > args.max:
    print("MEASURE FAIL canta bicim degistiriyor")
    sys.exit(1)
