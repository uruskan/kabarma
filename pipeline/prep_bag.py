"""
Tripo'dan gelen 1,9 milyon ucgenli canta GLB'sini oyunluk hale getirir (kredi yok): ucgen sayisini indirir, dokulari kucultur.

  blender -b --factory-startup --python-exit-code 1 -P pipeline/prep_bag.py -- \
      --in  .tools/tripo_web/oyuncu/kabarma_canta.glb --out .tools/tripo_web/oyuncu/kabarma_canta_low.glb \
      [--tris 3000] [--tex 1024]

Yontem: Decimate (collapse, UV sinirlari korunur), sonra butun doku gorselleri --tex boyuta kucultulur.
Cikti git'te degil (.tools); merge hatti (build_player_tripo.py) bunu okur.
"""
import bpy, sys, argparse

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="inp", required=True)
ap.add_argument("--out", required=True)
ap.add_argument("--tris", type=int, default=3000)
ap.add_argument("--tex", type=int, default=1024)
args = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=args.inp)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
print("BAG meshes", [(o.name, len(o.data.polygons)) for o in meshes])
for o in meshes:
    n = sum(len(p.vertices) - 2 for p in o.data.polygons)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    # Tek gecis UV dikislerinde takilir (ilk denemede 1,9 milyon -> 130 bin); hedefe inene kadar gecis tekrarlanir.
    cur = n
    import bmesh
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(o.data); bm.free()
    cur = sum(len(p.vertices) - 2 for p in o.data.polygons)
    print("BAG merged ->", cur, "verts", len(o.data.vertices))
    for _ in range(8):
        if cur <= args.tris * 1.1:
            break
        m = o.modifiers.new("dec", "DECIMATE")
        m.decimate_type = "COLLAPSE"
        m.ratio = max(0.02, min(1.0, args.tris / cur))
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=m.name)
        cur = sum(len(p.vertices) - 2 for p in o.data.polygons)
        print("BAG pass ->", cur)
    print("BAG", o.name, n, "->", cur)
for im in bpy.data.images:
    if im.size[0] > args.tex:
        im.scale(args.tex, args.tex)
        # scale() bellek gorseli birakir; disa aktarimda JPEG mime + bozuk veri cikiyordu (Godot: ERR_PARSE_ERROR).
        # Diske JPEG yazip dosya olarak yeniden yukle.
        import tempfile, os
        tmp = os.path.join(tempfile.gettempdir(), "prepbag_" + im.name + ".jpg")
        im.filepath_raw = tmp
        im.file_format = "JPEG"
        im.save()
        im.source = "FILE"
        im.filepath = tmp
        im.reload()
        print("TEX", im.name, "->", args.tex)
bpy.ops.export_scene.gltf(filepath=args.out, export_format="GLB", export_yup=True)
print("EXPORTED", args.out)



