"""
Gorsel yenileme uretim istegi (gorsel-model akisi, 18 Kasim 2026). Sahibin yonu: "bebek oyunu" degil; olgun, cilali,
AAA'ya yakin yari stilize dil (Astroneer cilasi, Valheim/Rust malzeme ciddiyeti), Kabarma'nin sicak cayiri korunur.

  python pipeline/gen_gorsel.py                      # hepsi --dry-run: plan ve kredi tahmini, KREDI HARCAMAZ
  python pipeline/gen_gorsel.py --run kapsul_govde   # URETIR, KREDI HARCAR: yalniz sahibin o tutara onayiyla

Referans gorsel: progress/gorsel/model/ref/<ad>.png varsa istem metni yerine gorselden uretilir (image-to-model; metin
yine --name gecmisinde durur). Referanslari agy uretir (istemler progress/gorsel/model/uretim_istegi.md).
Cikti sabit yolda: <sourceRoot>/.tools/tripo/<ad>_v3/ (git disi). Sonra pipeline/golden_manifest.json "in" yolu,
python pipeline/run_golden.py <ad>, ardindan python ... pbr_bake.py --only <ad> (bkz. uretim_istegi.md, "Oturtma").
Alt oturumlar --run kullanmaz.
"""
import json, os, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MAN = json.load(open(os.path.join(ROOT, "pipeline", "golden_manifest.json"), encoding="utf-8"))
REF = os.path.join(ROOT, "progress", "gorsel", "model", "ref")

# Yeni stil cumlesi (SANAT-SES §1, 18 Kasim guncellemesi). Eski "toy-like / chunky soft" dili birakildi.
STYLE = ("Semi-stylised high-end game asset, mature and believable, grounded PBR materials with real wear: chipped paint on edges, grime in crevices, scorch and rust "
         "streaks, crisp bevelled hard-surface detail with panel seams, bolts and rivets, strong readable silhouette. "
         "Not cute, not toy-like, not cartoon, no plastic look. Muted off-white, cobalt blue and safety-yellow accents "
         "for a frontier farming co-op on a warm golden-meadow moon.")
Q = ["--model", "tripo-v3.1", "-p", "texture_quality=detailed", "-p", "texture_version=v3.5", "-p", "pbr=true"]
SEGMENT_CREDITS = 40  # tripo --dry-run segment adimini tahmine katmiyor; M0 olcumu

# Sira = oncelik. "cut": 310 kredilik bakiyede kesme cizgisinin altinda kalanlar.
REQUESTS = {
    "kapsul_govde": {
        "why": "kapsul: sahibin 1 numarali sikayeti (oyuncak balon). Ilk kare, Kargo Rampasi, her sabah gorunur",
        "target": "game/assets/models/kapsul/kapsul.glb (dugum body, base)",
        "prompt": ("A rugged industrial landing capsule hull, about 3 m tall: squat "
                   "truncated-cone body with a heavy scorched ablative heat-shield ring at the bottom, off-white painted steel "
                   "hull plates with panel seams and rivets, a large rectangular door opening on the front side (doorway is "
                   "an empty recessed frame, the door itself is NOT included), small round porthole, recessed grab handles, "
                   "four RCS thruster quads, a short antenna mast and a parachute housing on top, stencilled hazard chevrons, "
                   "four short sturdy leg mount brackets near the base (legs NOT included). " + STYLE),
        "face_limit": 14000, "then": "",
    },
    "kapsul_kapak": {
        "why": "kapsul kapagi = rampa: alt kenarindan menteseli, acilinca yere iner (Pod.Hatch). Ayri model: temiz pivot",
        "target": "game/assets/models/kapsul/kapsul_kapak.glb (dugum hatch; mentese alt kenar)",
        "prompt": ("A heavy rectangular spacecraft boarding door that folds down into a ramp, shown as a single flat-lying "
                   "object: thick steel slab about 1.6 m long and 1.3 m wide, outer face off-white painted with cobalt blue "
                   "band and yellow-black hazard stripes on the edges, inner face is an anti-slip diamond tread steel ramp "
                   "with raised cleats, two big hinge knuckles along one short edge, heavy latch bolts on the other edge, "
                   "worn paint, scuffs, grime. " + STYLE),
        "face_limit": 5000, "then": "",
    },
    "kapsul_ayak": {
        "why": "kapsul inis ayagi (4 ornek): okunur, yere oturmus bir arac. Ayri model: ayaklar kodla yerlestirilir",
        "target": "game/assets/models/kapsul/kapsul_ayak.glb (dugum leg; ust ucta baglanti pimi)",
        "prompt": ("A single sturdy hydraulic landing leg for a small spacecraft, about 1.4 m long: angled steel strut with a "
                   "chrome hydraulic piston and dust boot, a pivot clevis bracket at the top, a wide round articulated foot "
                   "pad with bolted rim at the bottom, hoses clipped along the strut, chipped grey-blue paint, dust and grime. "
                   + STYLE),
        "face_limit": 3000, "then": "",
    },
    "capa": {
        "why": "Capa: ekranda her an oyuncunun elinde; tabanca oyuncak gibi",
        "target": "game/assets/models/capa/capa.glb (+X on, Player.cs tutar)",
        "prompt": ("A rugged handheld multi-tool for a frontier farmer, size of a heavy cordless drill: pistol grip with "
                   "rubber over-mould, chunky off-white painted steel housing with cobalt blue panels, a round emitter nozzle "
                   "with a thick knurled steel collar at the front, a quick-swap head socket on top, a small analog pressure "
                   "gauge, cooling fins, exposed screws, worn edges and grime. " + STYLE),
        "face_limit": 5000, "then": "",
    },
    "ocak": {
        "why": "Ocak: ussun kalbi; mavi kubbe ve topak bicim oyuncak gibi, ates agzi gorunmuyor",
        "target": "game/assets/models/ocak/ocak.glb",
        "prompt": ("A compact frontier hearth furnace, about 2 m tall: heavy stone and mortar base, cast-iron firebox with a "
                   "big glowing fire door on the front (visible fire through the grate), riveted dark steel hood, a tall "
                   "flue pipe with a spark arrestor cap, a brass pressure gauge and a valve wheel on the side, a small "
                   "cobalt-blue co-op badge plate, soot stains around the door, worn and lived-in. " + STYLE),
        "face_limit": 14000, "then": "",
    },
    "tezgah": {
        "why": "Tezgah: mavi oyuncak cerceve, 64 px silueti koltuk gibi",
        "target": "game/assets/models/tezgah/tezgah.glb",
        "prompt": ("A heavy-duty frontier workbench: thick scarred hardwood worktop on a welded steel frame painted cobalt "
                   "blue with chipped edges, a cast-iron bench vice on one end, a perforated steel tool board on the back "
                   "with a few hand tools (hammer, saw, wrench), a lower shelf with a toolbox, a hanging work lamp, sawdust "
                   "and grease stains. " + STYLE),
        "face_limit": 14000, "then": "",
    },
    "paketleme": {
        "why": "Paketleme Tezgahi: daginik oyuncak; isi okunmuyor",
        "target": "game/assets/models/paketleme/paketleme.glb",
        "prompt": ("A crate packing station for a farm co-op: sturdy wooden bench with a lever-operated lid press made of "
                   "steel, a stack of slatted wooden produce crates, a roll of brown kraft paper on a steel bar, a stencil "
                   "and a branding stamp, a yellow safety rail, cobalt-blue painted steel legs, worn wood and grime. "
                   + STYLE),
        "face_limit": 14000, "then": "",
    },
    "el_arabasi": {
        "why": "El Arabasi: ince boru cerceve ve oyuncak mavi tekne (olcut 3'te de kaldi); tekerlek kodla doner",
        "target": "game/assets/models/el_arabasi/el_arabasi.glb (dugumler body, wheel, wheel_pivot, handle_l/r, load_slot)",
        "prompt": ("A heavy-duty farm wheelbarrow: deep steel tub painted cobalt blue with dents and chipped paint, rolled "
                   "rim, sturdy square-section steel frame, two hardwood handles with worn rubber grips, two short legs, a "
                   "big chunky knobbly rubber tyre on a steel wheel with yellow hub, mud on the tyre and lower tub. "
                   + STYLE),
        "face_limit": 14000, "then": "segment:segmentation_granularity=simple",
    },
    "tas_firin": {
        "why": "Tas Firin: kabarcik bicim; parcali (kapak, baca) -> parcalama",
        "target": "game/assets/models/tas_firin/tas_firin.glb (dugumler body, base, door, door_handle, chimney, panel)",
        "prompt": ("A rustic stone bread and clay kiln oven, about 1.5 m tall: domed body of fitted fieldstones and mortar, "
                   "a cast-iron arched front door with a thick handle, a short steel chimney with a cap, a small side "
                   "control panel with a temperature dial, a stone plinth with a firewood slot, soot above the door. "
                   + STYLE),
        "face_limit": 12000, "then": "segment:segmentation_granularity=simple", "cut": True,
    },
}


def cmd_for(name, r, dry):
    ref = os.path.join(REF, f"{name}.png")
    src = ref if os.path.exists(ref) else r["prompt"]
    c = ["tripo.cmd" if os.name == "nt" else "tripo", "make", src, *Q, "-p", f"face_limit={r['face_limit']}", "--name", f"{name}_v3"]
    if r["then"]:
        c += ["--then", r["then"]]
    if dry:
        c += ["--dry-run", "--json"]
    else:
        out = os.path.join(MAN["sourceRoot"], ".tools", "tripo", f"{name}_v3")
        c += ["--out", out, "--json", "--yes", "--no-open"]
    return c, src == ref


def main():
    argv = sys.argv[1:]
    run = "--run" in argv
    names = [a for a in argv if not a.startswith("--")] or list(REQUESTS)
    if run and not [a for a in argv if not a.startswith("--")]:
        raise SystemExit("--run icin varlik adi gerekir")
    total = before_cut = 0
    rows = []
    for n in names:
        r = REQUESTS[n]
        c, from_ref = cmd_for(n, r, dry=not run)
        p = subprocess.run(c, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT)
        if run:
            print(f"{n}: kod {p.returncode}\n{p.stdout[-2000:]}\n{p.stderr[-2000:]}")
            continue
        try:
            d = json.loads(p.stdout)
        except Exception:
            print(f"{n}: dry-run cikti okunamadi (kod {p.returncode}): {p.stdout[-400:]} {p.stderr[-400:]}")
            continue
        est = d.get("generation_credits_estimate") or 0
        then = SEGMENT_CREDITS if r["then"].startswith("segment") else 0
        total += est + then
        if not r.get("cut"):
            before_cut += est + then
        rows.append((n, est, then, from_ref))
        print(f"DRY {n}: gecerli={d.get('valid')} model={d.get('model')} tahmin={est} + sonraki adim {then} = {est + then}"
              f" kaynak={'gorsel' if from_ref else 'metin'} uyari={d.get('warnings')} hata={d.get('errors')}")
        print("RAW " + json.dumps(d, ensure_ascii=False))
    if not run:
        print(f"TOPLAM tahmin (birer deneme): {total} kredi; kesme cizgisinin ustu: {before_cut} kredi")


if __name__ == "__main__":
    main()
