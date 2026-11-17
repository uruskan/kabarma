"""
Oyuncu modeline yeni klipleri ekler ve iskeleti gercek olcege (metre) getirir.
blender -b --factory-startup --python-exit-code 1 -P pipeline/merge_player.py -- \
    --base game/assets/models/player/player.glb --clips <klasor> --out game/assets/models/player/player.glb

Neden: Meshy iskeleti 0,01 olcekli "Armature" altinda santimetre birimli gelir. Fizik kemikleri (ragdoll)
olcegi atar; iskelet olcekliyken deri devasa ucgenlere cekilir. Olcek uygulanir, konum anahtarlari 0,01 ile carpilir.
Yerinde oynamasi gereken kliplerde kalcanin yatay kaymasi silinir (govdeyi oyun kodu tasir).
"""
import bpy, sys, os, re, argparse, glob

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--base", required=True)
ap.add_argument("--clips", required=True)
ap.add_argument("--out", required=True)
args = ap.parse_args(argv)

RENAME = {  # Meshy katalog anahtari -> oyundaki ad
    "Hit_Reaction": "hurt", "Knock_Down": "knockdown", "Carry_Heavy_Object_Walk": "carry",
    "Push_and_Walk_Forward": "push", "Female_Crouch_Pick_Up_Place_Side": "kneel",
    "Victory_Cheer": "cheer", "Big_Wave_Hello": "wave", "Electrocuted_Fall": "shock_fall",
}
IN_PLACE = {"hurt", "knockdown", "push", "kneel", "cheer", "wave", "shock_fall"}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.base)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
base_objs = set(bpy.context.scene.objects)
print("BASE actions:", [a.name for a in bpy.data.actions], "armature scale", tuple(arm.scale))

# --- yeni klipler ---
for path in sorted(glob.glob(os.path.join(args.clips, "*-animation-*-r1-glb.glb"))):
    before_actions = set(bpy.data.actions)
    before_objs = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new_actions = [a for a in bpy.data.actions if a not in before_actions]
    for o in [o for o in bpy.data.objects if o not in before_objs]:
        bpy.data.objects.remove(o, do_unlink=True)
    for a in new_actions:
        key = next((k for k in RENAME if k.lower() in a.name.lower()), None)
        if key is None:
            print("SKIP action", a.name); continue
        name = RENAME[key]
        old = bpy.data.actions.get(name)
        if old and old != a: bpy.data.actions.remove(old)
        a.name = name
        a.use_fake_user = True
        print("ADDED", path.split("animation-")[-1][:8], "->", name)

def fcurves(a):
    """Blender 5: aksiyon katmanli (layers > strips > channelbags > fcurves)."""
    if hasattr(a, "fcurves"):
        return list(a.fcurves)
    out = []
    for layer in a.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                out.extend(cb.fcurves)
    return out

# --- olcegi uygula: iskelet ve deri metre birimine ---
k = arm.scale.x
for a in bpy.data.actions:
    for fc in fcurves(a):
        if fc.data_path.endswith(".location") or fc.data_path == "location":
            for kp in fc.keyframe_points:
                kp.co[1] *= k; kp.handle_left[1] *= k; kp.handle_right[1] *= k
    # yerinde klipler: kalcanin yatay kaymasini sil (X ve Z; Y yukari kalir)
    if a.name in IN_PLACE:
        for fc in fcurves(a):
            if fc.data_path == 'pose.bones["Hips"].location' and fc.array_index in (0, 2):
                v0 = fc.keyframe_points[0].co[1] if fc.keyframe_points else 0
                for kp in fc.keyframe_points:
                    kp.co[1] = v0; kp.handle_left[1] = v0; kp.handle_right[1] = v0
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
for c in arm.children: c.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
print("APPLIED scale", k, "-> armature scale", tuple(arm.scale))

# Tum aksiyonlar NLA'ya: disa aktarim her birini ayri klip yapar
arm.animation_data_create()
for a in bpy.data.actions:
    tr = arm.animation_data.nla_tracks.new(); tr.name = a.name
    tr.strips.new(a.name, int(a.frame_range[0]), a)
arm.animation_data.action = None

bpy.ops.export_scene.gltf(filepath=args.out, export_format="GLB", export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_skins=True, export_yup=True)
print("EXPORTED", args.out, "clips:", sorted(a.name for a in bpy.data.actions))
