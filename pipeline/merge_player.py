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
    sirt cantasina "Backpack" kemigi ekle (Meshy cantayi kollara ve bacaklara agirliklamis; canta yuruyuste
    bukuluyordu) ve agirliklari ona aktar, her eylemi NLA izi yapip disa aktar.
 C. Denetim: ciktidaki her beklenen klip var, 0,2 sn'den uzun ve kanalli; Backpack eklemi var. Yoksa cikis 1.
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

# --- Backpack kemigi (cantanin salinimi, SANAT-SES §3.1 "canta ve suslerin salinimi") ---
# Blender ekseninde govde -Y'ye bakar; canta arkada (+Y), y 0,07..0,26, z 0,78..1,30, |x| < 0,27 (olculdu).
bpy.ops.object.mode_set(mode="EDIT")
eb = arm.data.edit_bones
bag = eb.new("Backpack")
bag.head = (0.0, 0.11, 1.22)   # askilarin sirta bindigi ust on kenar
bag.tail = (0.0, 0.19, 0.86)   # cantanin alt merkezi
bag.parent = eb["Spine"]
bag.use_connect = False
bpy.ops.object.mode_set(mode="OBJECT")


def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


vg = mesh.vertex_groups.get("Backpack") or mesh.vertex_groups.new(name="Backpack")
mw = mesh.matrix_world
moved = 0
for v in mesh.data.vertices:
    p = mw @ v.co
    w = smooth(0.075, 0.13, p.y) * smooth(0.72, 0.80, p.z) * (1 - smooth(1.27, 1.33, p.z)) * (1 - smooth(0.25, 0.32, abs(p.x)))
    if p.z > 1.20:
        w *= smooth(0.11, 0.15, p.y)  # kask arkasi (y <= 0,11) cantaya girmez
    if w <= 0.01:
        continue
    for g in v.groups:
        g.weight *= (1 - w)
    vg.add([v.index], w, "REPLACE")
    moved += 1
print("BACKPACK vertices", moved)
if moved < 100:
    fail(f"canta bolgesinde yalnizca {moved} kose bulundu")

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
for n, (d, c) in sorted(durs.items()):
    print(f"OUT {n:12s} {d:6.2f} sn  {c} kanal")
if problems:
    fail("; ".join(problems))
print(f"MERGE OK {len(durs)} klip, {len(joints)} eklem")
