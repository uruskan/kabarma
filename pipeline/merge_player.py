"""
Oyuncu modelini teslim edilen Genex (Meshy) dosyalarindan bastan kurar: kemikli govde + butun klipler -> player.glb.

  blender -b --factory-startup --python-exit-code 1 -P pipeline/merge_player.py -- \
      --base  <ana kopya>/game/assets/genex/af89e8bf-.../chunky-...-rigged-a1-glb.glb \
      --clips <ana kopya>/game/assets/genex/af89e8bf-... <ana kopya>/game/assets/genex/f05fdd46-... \
      --anims game/data/player_anims.json \
      --out   game/assets/models/player/player.glb

Klip kimlikleri ve teslim adlari game/data/player_anims.json'da (oyun da ayni dosyayi okur). Yeni klip gelince:
klasoru --clips'e ekle, klibin "action" adini veride yaz, "placeholder" isaretini kaldir, betigi yeniden kos.

Asamalar:
 A. glTF birlestirme (saf Python). Klip GLB'lerinde deri (skin) yok; Blender onlari iskelet degil bos nesne
    (EMPTY) hiyerarsisi olarak alir ve eylemler iskelete baglanmaz: 17 Kasim'daki player.glb'de 8 klibin tek kare
    (0,04 sn) cikmasinin nedeni buydu. Bu yuzden animasyon kanallari dugum adina gore dogrudan derili tabana
    kopyalanir; Blender tek bir derili GLB acar ve her klip "OBArmature" yuvasina bagli eylem olur.
 B. Blender: olcegi uygula (Meshy iskeleti 0,01 olcekli, santimetre), yerinde kliplerde kalca kaymasini sil,
    gogse (Spine02) "Backpack" kemigi ekle, cantanin yuzlerini govde aginden ayri, derisiz bir mesh olarak ayir ve
    o kemige bagla (Meshy cantayi kollara ve bacaklara agirliklamisti; canta kliplerde uzuyordu), her eylemi NLA izi
    yapip disa aktar. Godot bunu iskelette BoneAttachment3D olarak acar: canta hicbir klipte bicim degistirmez.
 C. Denetim: ciktidaki her beklenen klip var, 0,2 sn'den uzun ve kanalli; Backpack eklemi var; Backpack_Mesh derisiz
    ve Backpack ekleminin cocugu; govdede Backpack'e agirlikli kose yok. Yoksa cikis 1.
    Olcum: pipeline/measure_bag.py her klipte cantanin gogus kemigine gore sapmasini olcer.
"""
import bpy, sys, os, json, struct, glob, argparse, tempfile

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--base", required=True, help="derili govde GLB (Meshy rigged)")
ap.add_argument("--clips", nargs="+", required=True, help="klip GLB klasorleri (*-glb.glb, *-source-glb haric)")
ap.add_argument("--anims", required=True, help="game/data/player_anims.json")
ap.add_argument("--out", required=True)
args = ap.parse_args(argv)

anims = json.load(open(args.anims, encoding="utf-8"))
CLIPS = [c for c in anims["clips"] if c.get("source") in ("library", "locomotion") and c.get("action")]
IN_PLACE = {c["id"] for c in CLIPS if c.get("inPlace")}


def fail(msg):
    print("MERGE FAIL:", msg)
    sys.exit(1)


# ---------------- A. glTF birlestirme ----------------
def read_glb(path):
    data = open(path, "rb").read()
    magic, _ver, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        fail(f"{path}: GLB degil")
    off, js, binc = 12, None, b""
    while off < length:
        clen, ctype = struct.unpack_from("<I4s", data, off)
        chunk = data[off + 8: off + 8 + clen]
        if ctype == b"JSON":
            js = json.loads(chunk)
        elif ctype == b"BIN\0":
            binc = chunk
        off += 8 + clen
    return js, bytearray(binc)


def write_glb(path, js, binc):
    jb = json.dumps(js, separators=(",", ":")).encode("utf-8")
    jb += b" " * ((4 - len(jb) % 4) % 4)
    binc = bytes(binc) + b"\0" * ((4 - len(binc) % 4) % 4)
    total = 12 + 8 + len(jb) + 8 + len(binc)
    with open(path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(jb), b"JSON")); f.write(jb)
        f.write(struct.pack("<I4s", len(binc), b"BIN\0")); f.write(binc)


COMP_SIZE = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
TYPE_N = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def copy_accessor(src_js, src_bin, ai, dst_js, dst_bin):
    a = dict(src_js["accessors"][ai])
    bv = src_js["bufferViews"][a["bufferView"]]
    elem = COMP_SIZE[a["componentType"]] * TYPE_N[a["type"]]
    stride = bv.get("byteStride", elem)
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    raw = bytearray()
    for k in range(a["count"]):
        raw += src_bin[start + k * stride: start + k * stride + elem]
    while len(dst_bin) % 4:
        dst_bin.append(0)
    dst_js["bufferViews"].append({"buffer": 0, "byteOffset": len(dst_bin), "byteLength": len(raw)})
    dst_bin += raw
    a["bufferView"] = len(dst_js["bufferViews"]) - 1
    a.pop("byteOffset", None)
    dst_js["accessors"].append(a)
    return len(dst_js["accessors"]) - 1


def clip_id_for(name):
    """Teslim adi -> klip kimligi. 'Armature|running|baselayer' gibi adlarda | ile ayrilan parcalardan biri esler."""
    parts = set(name.split("|"))
    for c in CLIPS:
        if name == c["action"] or c["action"] in parts:
            return c["id"]
    return None


base_js, base_bin = read_glb(args.base)
if not base_js.get("skins"):
    fail(f"{args.base}: derili govde degil (skin yok)")
node_by_name = {n.get("name"): i for i, n in enumerate(base_js["nodes"])}
dropped = [a.get("name") for a in base_js.get("animations", [])]
base_js["animations"] = []  # Meshy'nin tek kare 'clip0' pozu atilir
print("BASE", os.path.basename(args.base), "dropped", dropped)

files = []
for folder in args.clips:
    files += [p for p in sorted(glob.glob(os.path.join(folder, "*-glb.glb"))) if "source-glb" not in p and "rigged" not in p]
taken, unmapped = {}, []
for path in files:
    js, binc = read_glb(path)
    for an in js.get("animations", []):
        cid = clip_id_for(an.get("name", ""))
        if cid is None:
            unmapped.append(f"{os.path.basename(path)}: {an.get('name')}")
            continue
        if cid in taken:
            print(f"DUP {cid}: {os.path.basename(path)} atlandi ({taken[cid]} kullanildi)")
            continue
        new = {"name": cid, "samplers": [], "channels": []}
        for s in an["samplers"]:
            new["samplers"].append({
                "input": copy_accessor(js, binc, s["input"], base_js, base_bin),
                "output": copy_accessor(js, binc, s["output"], base_js, base_bin),
                "interpolation": s.get("interpolation", "LINEAR"),
            })
        for ch in an["channels"]:
            nm = js["nodes"][ch["target"]["node"]].get("name")
            if nm not in node_by_name:
                continue
            new["channels"].append({"sampler": ch["sampler"], "target": {"node": node_by_name[nm], "path": ch["target"]["path"]}})
        base_js["animations"].append(new)
        taken[cid] = os.path.basename(path)
        print(f"CLIP {cid:12s} <- {an.get('name')}  ({len(new['channels'])} kanal)")
for u in unmapped:
    print("UNMAPPED", u, "(player_anims.json'da action yok)")
base_js["buffers"] = [{"byteLength": len(base_bin)}]
merged = os.path.join(tempfile.gettempdir(), "kabarma_player_merged.glb")
write_glb(merged, base_js, base_bin)

expected = [c["id"] for c in CLIPS if not c.get("placeholder")]
missing = [e for e in expected if e not in taken]
if missing:
    fail(f"teslim dosyalarinda bulunamayan klipler: {missing}")

# ---------------- B. Blender ----------------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=merged)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
print("BLENDER actions:", sorted(a.name for a in bpy.data.actions), "armature scale", tuple(arm.scale))


def fcurves(a):
    """Blender 5: eylem katmanli (layers > strips > channelbags > fcurves)."""
    if hasattr(a, "fcurves"):
        return list(a.fcurves)
    out = []
    for layer in a.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                out.extend(cb.fcurves)
    return out


k = arm.scale.x
for a in bpy.data.actions:
    for fc in fcurves(a):
        if fc.data_path.endswith(".location") or fc.data_path == "location":
            for kp in fc.keyframe_points:
                kp.co[1] *= k; kp.handle_left[1] *= k; kp.handle_right[1] *= k
    if a.name in IN_PLACE:  # kalcanin yatay kaymasini sil (glTF X ve Z = Blender kemik yerelinde 0 ve 2)
        for fc in fcurves(a):
            if fc.data_path == 'pose.bones["Hips"].location' and fc.array_index in (0, 2):
                v0 = fc.keyframe_points[0].co[1] if fc.keyframe_points else 0
                for kp in fc.keyframe_points:
                    kp.co[1] = v0; kp.handle_left[1] = v0; kp.handle_right[1] = v0
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
for c in arm.children:
    c.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
print("APPLIED scale", k, "-> armature scale", tuple(arm.scale))

# --- Canta: ayri, derisiz, KATI bir mesh; gogse vidali "Backpack" kemigine bagli (sahip, 18 Kasim: "canta hicbir
# klipte bicim degistirmez; gogse vidali kati bir kutu gibi govdeyle birlikte doner ve kayar"). ---
# Olcum (pipeline/measure_bag.py, 18 Kasim): kliplerde kemik olcek ya da konum izi yok (Hips disinda); cantayi uzatan
# sey Meshy'nin agirliklariydi. Eski agirlik kutusu (y 0,075..0,13 rampasi) cantanin sirta bakan yuzunu (y ~0,08),
# yan panellerini ve ust koselerini disarida birakiyordu: bu koseler %80 kola, omza ya da uyluga bagliydi ve
# kliplerde 8..37 cm oynuyordu. Simdi canta yuzleri govde aginden AYRILIR, derisi yoktur, kemige baglidir: Godot
# glTF iceaktarimi bunu iskelette BoneAttachment3D + MeshInstance3D yapar. Kemik canlandirilmaz (kod da yazmaz).
# Blender ekseninde govde -Y'ye bakar; canta arkada (+Y). Kesitlerle olculen kutu: |x| < 0,25, z 0,76..1,31,
# sirta bakan yuzu y ~0,065..0,08; tulumun sirti y <= 0,06.
BAG_PARENT = "Spine02"   # gogus: omuzlar ve boyun buna bagli
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.data.edit_bones
bag = eb.new("Backpack")
bag.head = (0.0, 0.11, 1.22)   # askilarin sirta bindigi ust on kenar
bag.tail = (0.0, 0.19, 0.86)   # cantanin alt merkezi
bag.parent = eb[BAG_PARENT]
bag.use_connect = False
bag.use_deform = True          # derinin eklem listesinde kalsin (oyun kemigi bulur); hicbir kose ona agirlikli degil
bpy.ops.object.mode_set(mode="OBJECT")

BAG_X, BAG_Z0, BAG_Z1 = 0.265, 0.74, 1.315   # yuz merkezi bu kutuda
BAG_Y_ANY, BAG_Y_GREY = 0.095, 0.03           # y > 0,095 her yuz (kayislar dahil); 0,03..0,095 arasi yalniz gri yuz
GREY_SAT = 0.3                                # gri: doygunluk bundan az (tulum lacivert, seritler sari: doygun)
HEAD_MAX, HEAD_Y = 0.3, 0.12                  # z > 1,20 ve y < 0,12'de basa bu kadar agirlikli kose kaskindir


def base_pixels(o):
    for slot in o.material_slots:
        m = slot.material
        if not m or not m.use_nodes:
            continue
        for n in m.node_tree.nodes:
            if n.type == "BSDF_PRINCIPLED" and n.inputs["Base Color"].links:
                fn = n.inputs["Base Color"].links[0].from_node
                if fn.type == "TEX_IMAGE" and fn.image:
                    w, h = fn.image.size
                    return list(fn.image.pixels[:]), w, h, fn.image.channels
    return None


pix = base_pixels(mesh)
uvl = mesh.data.uv_layers.active.data if mesh.data.uv_layers else None


def texel(u, v):
    px, w, h, ch = pix
    x = min(w - 1, max(0, int((u % 1.0) * w)))
    y = min(h - 1, max(0, int((v % 1.0) * h)))
    i = (y * w + x) * ch
    return px[i], px[i + 1], px[i + 2]


def face_grey(poly):
    """Yuzun dokusu gri mi (canta govdesi): UV merkezi ve koseleri ornegi; cogunluk."""
    if pix is None or uvl is None:
        return False
    uvs = [tuple(uvl[li].uv) for li in poly.loop_indices]
    cu = sum(u for u, _ in uvs) / len(uvs)
    cv = sum(v for _, v in uvs) / len(uvs)
    # koseler merkeze dogru biraz cekilir (komsu UV adasinin rengine tasmasin)
    samples = [texel(cu, cv)] + [texel(cu + (u - cu) * 0.6, cv + (v - cv) * 0.6) for u, v in uvs]
    grey = 0
    for r, g, b in samples:
        mx, mn = max(r, g, b), min(r, g, b)
        grey += (mx - mn) / max(mx, 1e-4) < GREY_SAT
    return grey * 2 > len(samples)


head_g = mesh.vertex_groups.get("Head")
neck_g = mesh.vertex_groups.get("neck")


def head_weight(vi):
    s = 0.0
    for g in mesh.data.vertices[vi].groups:
        if (head_g and g.group == head_g.index) or (neck_g and g.group == neck_g.index):
            s += g.weight
    return s


mw = mesh.matrix_world
n_bag = 0
# Yuz secim kipi, koseler ve kenarlar secimsiz: duzenleme kipine girerken secim yuzlerden tasinir (kose kipinde
# Blender secimi koselerden kurar ve yuz secimi kaybolurdu).
bpy.context.tool_settings.mesh_select_mode = (False, False, True)
for v in mesh.data.vertices:
    v.select = False
for e in mesh.data.edges:
    e.select = False
for poly in mesh.data.polygons:
    c = mw @ poly.center
    sel = abs(c.x) < BAG_X and BAG_Z0 < c.z < BAG_Z1 and c.y > BAG_Y_GREY
    if sel and c.y <= BAG_Y_ANY:
        sel = face_grey(poly)                     # lacivert tulum ve sari serit govdede kalir
    if sel and c.z > 1.20 and c.y < HEAD_Y:
        sel = all(head_weight(vi) < HEAD_MAX for vi in poly.vertices)  # kaskin arka alt kenari cantaya girmez
    poly.select = sel
    n_bag += sel
print("BAG faces", n_bag, "/", len(mesh.data.polygons))
if n_bag < 300:
    fail(f"canta bolgesinde yalnizca {n_bag} yuz bulundu")

# Ayir: secili yuzler yeni nesne olur. Derisi (Armature degistiricisi, kose gruplari) silinir, kemige baglanir.
if arm.animation_data:
    arm.animation_data.action = None
arm.data.pose_position = "REST"
bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
mesh.select_set(True)
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.separate(type="SELECTED")
bpy.ops.object.mode_set(mode="OBJECT")
bag_obj = next(o for o in bpy.context.selected_objects if o is not mesh)
bag_obj.name = "Backpack_Mesh"
bag_obj.data.name = "Backpack_Mesh"
for m in list(bag_obj.modifiers):
    bag_obj.modifiers.remove(m)
bag_obj.vertex_groups.clear()
keep = bag_obj.matrix_world.copy()
bag_obj.parent = arm
bag_obj.parent_type = "BONE"
bag_obj.parent_bone = "Backpack"
bpy.context.view_layer.update()
bag_obj.matrix_world = keep
bpy.context.view_layer.update()
drift = max((bag_obj.matrix_world @ v.co - keep @ v.co).length for v in bag_obj.data.vertices)
arm.data.pose_position = "POSE"
rigid = len(bag_obj.data.vertices)

# Dikis ve kopru: canta tulumla tek kaynakli yuzeydi. Govdede kalan (1) cantanin koseleriyle cakisan dikis koseleri ve
# (2) cantayi sirta baglayan kopru ucgenlerinin gri (canta renkli) koseleri kola, omza, uyluga agirlikliydi; canta
# katilasinca bunlar cantanin kenarindan disari uzardi. Bunlar gogse (BAG_PARENT, Backpack ile ayni hareket) tam
# agirlikla sabitlenir: dikis acilmaz, canta kenari uzamaz. Lacivert tulum koseleri kendi agirliklarinda kalir.
from mathutils import kdtree
bag_world = [bag_obj.matrix_world @ v.co for v in bag_obj.data.vertices]
kd = kdtree.KDTree(len(bag_world))
for i, p in enumerate(bag_world):
    kd.insert(p, i)
kd.balance()
vgrey, vnavy = {}, {}
NEAR_BAG = 0.015   # cantanin bir kosesine bu kadar yakin, lacivert olmayan kose de kopru sayilir (alt on kenar)
if pix is not None and uvl is not None:
    acc = {}
    for loop in mesh.data.loops:
        r, g, b = texel(*uvl[loop.index].uv)
        s = acc.setdefault(loop.vertex_index, [0.0, 0.0, 0.0, 0])
        s[0] += r; s[1] += g; s[2] += b; s[3] += 1
    for vi, s in acc.items():  # kosenin ortalama rengi gri mi (measure_bag.py ile ayni kural)
        r, g, b = s[0] / s[3], s[1] / s[3], s[2] / s[3]
        mx, mn = max(r, g, b), min(r, g, b)
        vgrey[vi] = (mx - mn) / max(mx, 1e-4) < GREY_SAT
        vnavy[vi] = r < 0.15 and b > 0.2
chest = mesh.vertex_groups.get(BAG_PARENT) or mesh.vertex_groups.new(name=BAG_PARENT)
seam = bridge = 0
for v in mesh.data.vertices:
    p = mw @ v.co
    dist = kd.find(p)[2]
    on_seam = dist < 0.0015
    looks_bag = vgrey.get(v.index, False) or (dist < NEAR_BAG and not vnavy.get(v.index, True))
    in_bag = (abs(p.x) < BAG_X and BAG_Z0 - 0.03 < p.z < BAG_Z1 and p.y > BAG_Y_GREY and looks_bag
              and not (p.z > 1.20 and p.y < HEAD_Y and head_weight(v.index) >= HEAD_MAX))
    if not (on_seam or in_bag):
        continue
    for gi in [g.group for g in v.groups]:
        mesh.vertex_groups[gi].remove([v.index])
    chest.add([v.index], 1.0, "REPLACE")
    seam += on_seam
    bridge += (in_bag and not on_seam)
print(f"BAG seam {seam} dikis kosesi, {bridge} kopru kosesi gogse ({BAG_PARENT}) sabitlendi")
print(f"BACKPACK ayri kati mesh: {rigid} kose, {len(bag_obj.data.polygons)} yuz, kemik Backpack ({BAG_PARENT} cocugu), yer kaymasi {drift*1000:.2f} mm")
if drift > 1e-3:
    fail(f"canta kemige baglanirken yerinden kaydi ({drift*1000:.1f} mm)")

# Tum eylemler NLA'ya: disa aktarim her birini ayri klip yapar. Eylem yuvasi acikca baglanir (Blender 4.4+).
arm.animation_data_create()
for a in sorted(bpy.data.actions, key=lambda a: a.name):
    tr = arm.animation_data.nla_tracks.new(); tr.name = a.name
    st = tr.strips.new(a.name, int(a.frame_range[0]), a)
    if hasattr(st, "action_slot") and st.action_slot is None and len(getattr(a, "slots", [])) > 0:
        st.action_slot = a.slots[0]
arm.animation_data.action = None

bpy.ops.export_scene.gltf(filepath=args.out, export_format="GLB", export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_skins=True, export_yup=True)
print("EXPORTED", args.out)

# ---------------- C. Denetim ----------------
out_js, _ = read_glb(args.out)
problems = []
durs = {}
for an in out_js.get("animations", []):
    dur = 0.0
    for s in an["samplers"]:
        acc = out_js["accessors"][s["input"]]
        if "max" in acc:
            dur = max(dur, acc["max"][0])
    durs[an["name"]] = (dur, len(an["channels"]))
for e in expected:
    d = durs.get(e)
    if d is None:
        problems.append(f"{e}: ciktida yok")
    elif d[0] < 0.2 or d[1] < 20:
        problems.append(f"{e}: bos klip (sure {d[0]:.2f} sn, {d[1]} kanal)")
joints = {out_js["nodes"][j].get("name") for s in out_js.get("skins", []) for j in s["joints"]}
if "Backpack" not in joints:
    problems.append("Backpack eklemi derinin eklem listesinde yok")


def read_acc(js, binc, ai):
    """Erisiciyi kose basina demet listesi olarak okur (normalize tamsayilar 0..1'e cevrilir)."""
    a = js["accessors"][ai]
    bv = js["bufferViews"][a["bufferView"]]
    ct, n = a["componentType"], TYPE_N[a["type"]]
    fmt = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}[ct]
    elem = COMP_SIZE[ct] * n
    stride = bv.get("byteStride", elem)
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    scale = {5121: 255.0, 5123: 65535.0}.get(ct) if a.get("normalized") else None
    out = []
    for k in range(a["count"]):
        t = struct.unpack_from("<" + fmt * n, binc, start + k * stride)
        out.append(tuple(x / scale for x in t) if scale else t)
    return out


out_js2, out_bin = read_glb(args.out)
partial = full = 0
for sk_i, sk in enumerate(out_js2.get("skins", [])):
    bag_j = [i for i, j in enumerate(sk["joints"]) if out_js2["nodes"][j].get("name") == "Backpack"]
    if not bag_j:
        continue
    for nd in out_js2["nodes"]:
        if nd.get("skin") != sk_i or "mesh" not in nd:
            continue
        for prim in out_js2["meshes"][nd["mesh"]]["primitives"]:
            at = prim["attributes"]
            sets = [(at[f"JOINTS_{s}"], at[f"WEIGHTS_{s}"]) for s in range(4) if f"JOINTS_{s}" in at]
            data = [(read_acc(out_js2, out_bin, j), read_acc(out_js2, out_bin, w)) for j, w in sets]
            for vi in range(len(data[0][0]) if data else 0):
                tot = bagw = 0.0
                for js_, ws_ in data:
                    for jj, ww in zip(js_[vi], ws_[vi]):
                        tot += ww
                        if jj == bag_j[0]:
                            bagw += ww
                if tot > 0 and bagw / tot >= 0.99:
                    full += 1
                elif tot > 0 and bagw / tot > 0.01:
                    partial += 1
bag_nodes = [i for i, nd in enumerate(out_js2["nodes"]) if nd.get("name") == "Backpack_Mesh" and "mesh" in nd]
bag_joint = [i for i, nd in enumerate(out_js2["nodes"]) if nd.get("name") == "Backpack"]
if not bag_nodes:
    problems.append("Backpack_Mesh dugumu ciktida yok")
elif "skin" in out_js2["nodes"][bag_nodes[0]]:
    problems.append("Backpack_Mesh derili (kati olmali)")
elif not bag_joint or bag_nodes[0] not in out_js2["nodes"][bag_joint[0]].get("children", []):
    problems.append("Backpack_Mesh Backpack ekleminin cocugu degil")
if full or partial:
    problems.append(f"govdede Backpack'e agirlikli kose kaldi ({full} tam, {partial} kismi)")
print(f"RIGID BAG cikti: ayri mesh {'var' if bag_nodes else 'YOK'} ({rigid} kose), govdede Backpack agirligi {full}+{partial}")
for n, (d, c) in sorted(durs.items()):
    print(f"OUT {n:12s} {d:6.2f} sn  {c} kanal")
if problems:
    fail("; ".join(problems))
print(f"MERGE OK {len(durs)} klip, {len(joints)} eklem")
