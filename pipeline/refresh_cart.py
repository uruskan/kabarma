"""
El Arabasi'nin yan dosyalarini (LOD, kabuk, ikon, kart, rapor) DUZELTILMIS modelden yeniden uretir (varlik akisi). KREDI HARCAMAZ.

fix_cart_handles.py (oyuncu akisi) el_arabasi.glb'yi duzeltir; clean_asset.py'yi ham Tripo kaynagindan kosturmak o duzeltmeyi
silerdi. Bu betik duzeltilmis GLB'yi girdi alir:
  1. fix_cart_handles (zaten simetrikse dokunmaz)
  2. clean_asset: sadelestirme (--tris), onarim, LOD, kabuk, ikon, kart, rapor; govde/cerceve/teker dugumleri, wheel_pivot, handle, load_slot
  3. grip_markers: handle_l / handle_r (oyuncu akisinin elle tutma dugumleri)
Sonra: python pipeline/run_golden.py --sync-only ; Godot --import.

  python pipeline/refresh_cart.py [--tris 13800]
"""
import json, os, shutil, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MAN = json.load(open(os.path.join(ROOT, "pipeline", "golden_manifest.json"), encoding="utf-8"))
A = MAN["assets"]["el_arabasi"]; D = MAN["defaults"]
GLB = os.path.join(ROOT, "game", "assets", "models", "el_arabasi", "el_arabasi.glb")
tris = sys.argv[sys.argv.index("--tris") + 1] if "--tris" in sys.argv else "13800"


def blender(*a):
    return subprocess.run([MAN["blender"], "-b", "--factory-startup", "--python-exit-code", "1", *a], cwd=ROOT).returncode


if blender("-P", "pipeline/fix_cart_handles.py") != 0:
    raise SystemExit("fix_cart_handles basarisiz")
src = os.path.join(tempfile.gettempdir(), "el_arabasi_fixed.glb")
shutil.copy(GLB, src)


def glb_height(path):
    """Duzeltilmis modelin kendi boyu (m, Y yukari): POSITION accessor kutularindan, dugum cevirileriyle. Olcek korunur:
    fix_cart_handles one kalkan sapi kaldirdigi icin boy 0,85'ten dusuk; 0,85'e zorlamak arabayi ve el isaretlerini buyutur."""
    import struct
    d = open(path, "rb").read(); n = struct.unpack("<I", d[12:16])[0]; j = json.loads(d[20:20 + n])
    lo, hi = 1e9, -1e9
    def walk(i, ty):
        nonlocal lo, hi
        nd = j["nodes"][i]; ty = ty + (nd.get("translation") or [0, 0, 0])[1]
        if "mesh" in nd:
            for p in j["meshes"][nd["mesh"]]["primitives"]:
                a = j["accessors"][p["attributes"]["POSITION"]]
                lo, hi = min(lo, a["min"][1] + ty), max(hi, a["max"][1] + ty)
        for c in nd.get("children", []):
            walk(c, ty)
    for r in j["scenes"][0]["nodes"]:
        walk(r, 0.0)
    return hi - lo


height = glb_height(src)
print("duzeltilmis model boyu", round(height, 4), "m (hedef olcek korunur)")
r = blender("-P", "pipeline/clean_asset.py", "--", "--in", src, "--out", GLB, "--name", "el_arabasi", "--label", A["label"],
            "--height", f"{height:.4f}", "--tris", tris, "--class", A["class"],
            "--renders", os.path.join(ROOT, MAN["renders"], "el_arabasi"), "--card", os.path.join(ROOT, MAN["cards"], "el_arabasi.png"),
            "--tex_size", str(D["tex_size"]), "--weld", str(D["weld"]), "--lods", D["lods"], "--lod_tex", D["lod_tex"],
            "--hull_points", str(D["hull_points"]), "--thin", str(D["thin"]), "--thin_area", str(D["thin_area"]),
            "--thick_samples", str(D["thick_samples"]), "--icon_size", str(D["icon_size"]), "--forward", D["forward"],
            "--drop_floating", str(D["drop_floating"]), "--dark_fill", str(D.get("dark_fill", 0)), "--keep_texture", "--parts",
            "--rename", "body=body,frame=frame,wheel=wheel", "--pivots", A["pivots"], "--sockets", A["sockets"])
if r != 0:
    raise SystemExit("clean_asset basarisiz")
raise SystemExit(blender("-P", "pipeline/grip_markers.py", "--", "el_arabasi"))
