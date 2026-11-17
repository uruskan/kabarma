# Kabarma: bir GLB'deki animasyon kliplerini, ayni iskeletli (ayni kemik adlari) baska bir GLB'ye ekler. Kredisiz.
#
# Ornek (uretim istegi: Zipzip yurume klibi, progress/m1/yaratik/uretim_istegi.md):
#   blender -b -P pipeline/merge_clips.py -- --base game/assets/models/zipzip/zipzip_5k.glb \
#       --clips <tripo-out>/model.glb --only walk --out game/assets/models/zipzip/zipzip_5k.glb
#
#   --base   klipler eklenecek model (dokusu, ucgenleri, var olan klipleri korunur)
#   --clips  kaynak GLB (ayni Tripo iskeleti; kemik adlari base ile ayni olmali)
#   --only   yalniz adinda bu sozcuklerden biri gecen klipler (virgulle; bos: hepsi)
#   --rename tek klip eklenirken yeni adi
#   --out    cikti GLB; yaninda <out>.report.json (klipler, eslesmeyen kemikler, sonuc)
#
# Base'deki ayni adli klip degistirilir. Kaynak klipte base'de olmayan kemik varsa rapor eder ve durur (cikis 1).
import argparse
import json
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--base", required=True)
ap.add_argument("--clips", required=True)
ap.add_argument("--only", default="")
ap.add_argument("--rename", default="")
ap.add_argument("--out", required=True)
args = ap.parse_args(argv)
only = [w.strip().lower() for w in args.only.split(",") if w.strip()]


def clip_name(action):
    # glTF ice aktaricisi adlari "Armature|walk" ya da "walk" bicimde verir; ad cakisinca Blender ".001" ekler.
    n = action.name.split("|")[-1]
    head, _, tail = n.rpartition(".")
    return head if head and tail.isdigit() and len(tail) == 3 else n


def action_bones(action):
    names = set()
    fcurves = []
    try:
        fcurves = list(action.fcurves)
    except AttributeError:
        pass
    if not fcurves:
        # Blender 4.4+ katmanli eylemler: kanal torbalari
        for layer in getattr(action, "layers", []):
            for strip in layer.strips:
                for bag in getattr(strip, "channelbags", []):
                    fcurves.extend(bag.fcurves)
    for fc in fcurves:
        p = fc.data_path
        if p.startswith('pose.bones["'):
            names.add(p.split('"')[1])
    return names


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.base)
base_objs = set(bpy.data.objects)
base_arm = next((o for o in base_objs if o.type == "ARMATURE"), None)
if base_arm is None:
    print("MERGE hata: base'de iskelet yok")
    sys.exit(1)
base_bones = {b.name for b in base_arm.data.bones}
base_actions = {a: clip_name(a) for a in bpy.data.actions}

bpy.ops.import_scene.gltf(filepath=args.clips)
new_objs = [o for o in bpy.data.objects if o not in base_objs]
new_actions = [a for a in bpy.data.actions if a not in base_actions]
picked = [a for a in new_actions if not only or any(w in clip_name(a).lower() for w in only)]

report = {"base": args.base, "clips": args.clips, "out": args.out, "base_clips": sorted(base_actions.values()),
          "source_clips": sorted(clip_name(a) for a in new_actions), "added": [], "replaced": [], "missing_bones": {}}
if not picked:
    report["pass"] = False
    report["error"] = "secilen klip yok"
    print("MERGE hata: secilen klip yok", report["source_clips"])
    json.dump(report, open(args.out + ".report.json", "w"), indent=2)
    sys.exit(1)

for a in picked:
    miss = sorted(action_bones(a) - base_bones)
    if miss:
        report["missing_bones"][clip_name(a)] = miss

if report["missing_bones"]:
    report["pass"] = False
    print("MERGE hata: eslesmeyen kemikler", report["missing_bones"])
    json.dump(report, open(args.out + ".report.json", "w"), indent=2)
    sys.exit(1)

if base_arm.animation_data is None:
    base_arm.animation_data_create()
ad = base_arm.animation_data
for a in picked:
    name = args.rename if (args.rename and len(picked) == 1) else clip_name(a)
    for old, oname in list(base_actions.items()):
        if oname == name:
            for tr in list(ad.nla_tracks):
                if any(s.action == old for s in tr.strips):
                    ad.nla_tracks.remove(tr)
            del base_actions[old]  # silinen eylemin anahtari gecersizlesir: once sozlukten cikar
            bpy.data.actions.remove(old)
            report["replaced"].append(name)
    a.name = name
    # Eylemi base iskeletine bagla (4.4+: yuva), NLA izine it, etkin eylemi bosalt.
    ad.action = a
    if hasattr(ad, "action_slot") and getattr(a, "slots", None):
        ad.action_slot = a.slots[0]
    tr = ad.nla_tracks.new()
    tr.name = name
    st = tr.strips.new(name, int(a.frame_range[0]), a)
    ad.action = None
    a.use_fake_user = True
    report["added"].append(name)

# Kaynak sahnenin nesnelerini sil (mesh, iskelet); yalniz eylemler kalir.
for o in new_objs:
    bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.export_scene.gltf(filepath=args.out, export_format="GLB", use_selection=False, export_yup=True,
                          export_animations=True, export_skins=True, export_animation_mode="NLA_TRACKS")

# Dogrulama: ciktiyi yeniden ac, klip adlarini say.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.out)
report["out_clips"] = sorted(clip_name(a) for a in bpy.data.actions)
want = set(base_actions.values()) | set(report["added"])
report["pass"] = want.issubset(set(report["out_clips"]))
json.dump(report, open(args.out + ".report.json", "w"), indent=2)
print("MERGE", "ok" if report["pass"] else "FAIL", "clips=" + ",".join(report["out_clips"]))
sys.exit(0 if report["pass"] else 1)
