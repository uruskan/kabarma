"""
Yeni Tripo modellerini oyuna oturtur (gorsel-model, 18 Kasim 2026; uretim: pipeline/gen_gorsel.py --run <ad>).
KREDI HARCAMAZ: yalniz inmis dosyalari isler.

  python pipeline/oturt_gorsel.py                     # inmis olan hepsi: bul, manifest, run_golden, malzeme, ice aktar, vitrin
  python pipeline/oturt_gorsel.py capa ocak           # yalniz bunlar
  python pipeline/oturt_gorsel.py --plan              # yalniz bul ve ne yapilacagini yaz (hicbir seye dokunmaz)
  python pipeline/oturt_gorsel.py --parts el_arabasi  # parcalanmis modelin parcalarini (kutu, ucgen) listele

Her model icin:
 1. .tools/tripo/<uretim_adi>_v3/ altindaki en yeni model.glb (parcalama zincirinde 'segment' gecen) bulunur.
 2. pipeline/golden_manifest.json: "in" yolu, ucgen hedefi (sinif butcesinin icinde, Ultra ayrintisi korunur) ve
    parca ayarlari. Kapsul govdesi artik tek parca: eski 'hatch' parcasi yok, Blender kapagi ve ayaklari
    Pod.Model.cs takar (data/kapsul.json).
 3. python pipeline/run_golden.py <anahtarlar>: temizlik, boy, taban, LOD, kabuk, ikon, assets.json.
 4. Malzeme: yeni modeller pbr=true ile kendi normal ve metal/puruzluluk haritasini getirir. Eski pisirilmis haritalar
    data/materials.json ve pipeline/pbr_manifest.json'dan silinir (MaterialUplift zaten yalniz tam adla eslesir).
 5. Godot ice aktarma, sonra --test=lineup --lineup_tag=yeni (kapsulde kapak denemesi de).

El Arabasi parcali gelir; parca numaralari her uretimde degisir. RENAME bos ise betik once --parts listesini basar
ve durur: listeye bakip asagidaki EL_ARABASI_RENAME'i doldurun (ad: body, wheel, frame_l/r, grip_l/r, fork).
"""
import glob, json, os, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MANP = os.path.join(ROOT, "pipeline", "golden_manifest.json")
MAN = json.load(open(MANP, encoding="utf-8"))
SRC = MAN["sourceRoot"]

# El Arabasi: yeni parcalamanin numaralari gelince doldurulur (--parts el_arabasi ciktisina gore).
# 18 Kasim uretimi (segment 50b6d0eb) uc parca verdi: 0 tekne (1,00 x 0,77 x 0,46), 1 cerceve + saplar + ayaklar
# (0,70 x 0,38 x 0,45), 2 teker (cap 0,4, +X onde). Ayri sap parcasi yok: handle soketi cerceveye baglanir.
EL_ARABASI_RENAME = "tripo_part_0=body,tripo_part_1=frame,tripo_part_2=wheel"
EL_ARABASI_DROP = ""
EL_ARABASI_SOCKETS = "wheel_pivot=wheel:center:wheel,handle=frame:center,load_slot=body:center"

# uretim adi (gen_gorsel.py) -> manifest anahtari ve ayarlar. Ucgen hedefleri SANAT-SES §4 butcesinin icinde:
# makine 15000, arac 20000, esya_kaya 6000.
JOBS = {
    "kapsul_govde": {"key": "kapsul", "tris": 14000, "drop_keys": ["parts", "rename"]},
    "capa":         {"key": "capa", "tris": 6000},
    "ocak":         {"key": "ocak", "tris": 13000},
    "tezgah":       {"key": "tezgah", "tris": 13000},
    "paketleme":    {"key": "paketleme", "tris": 12000},
    "el_arabasi":   {"key": "el_arabasi", "tris": 14000, "segment": True},
    "tas_firin":    {"key": "tas_firin", "tris": 12000, "segment": True},
}


def find_model(gen_name, segment=False):
    base = os.path.join(SRC, ".tools", "tripo", f"{gen_name}_v3")
    files = [f for f in glob.glob(os.path.join(base, "**", "*.glb"), recursive=True)]
    if segment:
        seg = [f for f in files if "segment" in f.replace("\\", "/").lower()]
        files = seg or files
    if not files:
        return None
    return max(files, key=os.path.getmtime)


def rel_src(p):
    return os.path.relpath(p, SRC).replace("\\", "/")


def blender_parts(path):
    """Parcali GLB'nin nesneleri ve 'part' ozniteligi: ad, ucgen, kutu boyu, merkez (Blender ekseni, m)."""
    code = r"""
import bpy, sys
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[-1])
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    me = o.data
    groups = {}
    if 'part' in me.attributes:
        a = me.attributes['part']
        for p in me.polygons: groups.setdefault(a.data[p.index].value, []).append(p)
    else:
        groups[o.name] = list(me.polygons)
    for k, ps in sorted(groups.items(), key=lambda kv: str(kv[0])):
        vs = [o.matrix_world @ me.vertices[i].co for p in ps for i in p.vertices]
        mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
        mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
        c = (mn + mx) / 2; s = mx - mn
        print(f"PART obj={o.name} part={k} tris={sum(len(p.vertices)-2 for p in ps)} size=({s.x:.2f},{s.y:.2f},{s.z:.2f}) center=({c.x:.2f},{c.y:.2f},{c.z:.2f})", flush=True)
"""
    tmp = os.path.join(ROOT, "pipeline", "_parts_probe.py")
    open(tmp, "w", encoding="utf-8").write(code)
    try:
        p = subprocess.run([MAN["blender"], "-b", "--factory-startup", "-P", tmp, "--", path],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
        return [l for l in p.stdout.splitlines() if l.startswith("PART")]
    finally:
        os.remove(tmp)


def fix_foreign_paths():
    """run_golden assets.json'u butun raporlardan yeniden yazar; baska worktree'de uretilmis raporlar (agac, kaya, kova:
    m1-varlik) mutlak yol tasir ve '../<worktree>/game/...' olarak yazilir. Bunlari res:// ve progress/ yoluna dondurur."""
    import re
    p = os.path.join(ROOT, "game", "data", "assets.json")
    t = open(p, encoding="utf-8").read()
    n = re.sub(r'"\.\./[^/"]+/game/', '"res://', t)
    n = re.sub(r'"\.\./[^/"]+/progress/', '"progress/', n)
    if n != t:
        open(p, "w", encoding="utf-8", newline="\n").write(n)
        print("assets.json: baska worktree yollari duzeltildi")


def drop_material_records(keys):
    paths = {f"res://assets/models/{k}/{k}.glb" for k in keys}
    mp = os.path.join(ROOT, "game", "data", "materials.json")
    m = json.load(open(mp, encoding="utf-8"))
    gone = [p for p in list(m.get("models", {})) if p in paths]
    for p in gone:
        del m["models"][p]
    json.dump(m, open(mp, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=2)
    pp = os.path.join(ROOT, "pipeline", "pbr_manifest.json")
    pm = json.load(open(pp, encoding="utf-8"))
    assets = pm.get("assets", {})
    if isinstance(assets, dict):
        for k in keys:
            assets.pop(k, None)
    else:
        pm["assets"] = [a for a in assets if a.get("id") not in keys]
    json.dump(pm, open(pp, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=2)
    return gone


def main():
    argv = sys.argv[1:]
    plan = "--plan" in argv
    if "--parts" in argv:
        for n in [a for a in argv if not a.startswith("--")]:
            f = find_model(n, JOBS[n].get("segment", False))
            print(f"{n}: {f}")
            if f:
                print("\n".join(blender_parts(f)))
        return
    names = [a for a in argv if not a.startswith("--")] or list(JOBS)
    ready = []
    for n in names:
        j = JOBS[n]
        f = find_model(n, j.get("segment", False))
        if not f:
            print(f"{n}: model yok (.tools/tripo/{n}_v3/), atlandi")
            continue
        if n == "el_arabasi" and not EL_ARABASI_RENAME:
            print(f"{n}: {f}\n  parca adlari bos: once 'python pipeline/oturt_gorsel.py --parts el_arabasi', sonra EL_ARABASI_RENAME")
            print("\n".join("  " + l for l in blender_parts(f)))
            continue
        print(f"{n}: {f} -> manifest '{j['key']}' tris {j['tris']}")
        ready.append((n, j, f))
    if plan or not ready:
        return
    man = json.load(open(MANP, encoding="utf-8"))
    for n, j, f in ready:
        a = man["assets"][j["key"]]
        a["in"] = rel_src(f)
        a["tris"] = j["tris"]
        for k in j.get("drop_keys", []):
            a.pop(k, None)
        if n == "el_arabasi":
            a["rename"] = EL_ARABASI_RENAME
            a["sockets"] = EL_ARABASI_SOCKETS
            a["pivots"] = "wheel=center"
            if EL_ARABASI_DROP:
                a["drop"] = EL_ARABASI_DROP
            else:
                a.pop("drop", None)
    json.dump(man, open(MANP, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=2)
    keys = [j["key"] for _, j, _ in ready]
    r = subprocess.run([sys.executable, os.path.join(ROOT, "pipeline", "run_golden.py"), *keys], cwd=ROOT)
    if r.returncode != 0:
        print("run_golden basarisiz: progress/m1/varlik/log/<ad>.log")
        return sys.exit(1)
    fix_foreign_paths()
    gone = drop_material_records(keys)
    print(f"eski pisirilmis malzeme kayitlari silindi: {gone}")
    hold = [k for k in keys if k in ("capa", "el_arabasi")]  # oyuncu akisi: tutamak ve namlu isaretleri (grips.json bunlari kullanir)
    if hold: subprocess.run([MAN["blender"], "-b", "--factory-startup", "-P", os.path.join(ROOT, "pipeline", "grip_markers.py"), "--", *hold], cwd=ROOT)
    g = MAN["godot"]
    subprocess.run([g, "--headless", "--path", "game", "--import"], cwd=ROOT, capture_output=True)
    only = ",".join(keys + (["kapsul_rampa"] if "kapsul" in keys else []))
    subprocess.run([g, "--path", "game", "--", "--test=lineup", "--lineup_tag=yeni", f"--lineup_only={only}"], cwd=ROOT)
    print("OTURTMA bitti: progress/gorsel/model/yeni/ ; kapsulde data/kapsul.json hinge ve legs yeni govdeye gore ayarlanir")


if __name__ == "__main__":
    main()
