"""
M1 altin varliklarindan ucretsiz onarimla (pipeline/clean_asset.py) SANAT-SES §9'dan gecemeyenlerin yeniden uretim istegi.
SANAT-SES §9: "Olcutten gecemeyen varlik en fazla dort kez yeniden uretilir; yine gecemezse koddan kurulur."

  python pipeline/gen_retry.py                 # hepsi --dry-run: plan ve kredi tahmini, KREDI HARCAMAZ
  python pipeline/gen_retry.py --run capa      # URETIR, KREDI HARCAR: yalniz sahibin o tutara onayiyla (CLAUDE.md)

Cikti sabit yolda: <sourceRoot>/.tools/tripo/<ad>_v2/ (git disi). Sonra golden_manifest.json'da o varligin "in" yolu
yeni model.glb'ye cevrilir ve `python pipeline/run_golden.py <ad>` calistirilir; assets.json durumu kendiliginden guncellenir.
Alt oturumlar --run kullanmaz.
"""
import json, os, subprocess, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MAN = json.load(open(os.path.join(ROOT, "pipeline", "golden_manifest.json"), encoding="utf-8"))
# gen_golden.sh ile ayni stil cumlesi (docs/stil/README.md "Istem dili")
STYLE = ("Stylised hand-painted game asset, grounded believable materials with subtle wear, chunky readable silhouette, "
         "soft bevelled forms, no thin spikes, warm sunny sci-fi farm on a golden meadow moon.")
Q = ["--model", "tripo-v3.1", "-p", "texture_quality=detailed", "-p", "texture_version=v3.5"]
SEGMENT_CREDITS = 40

REQUESTS = {
    # olcut 3: ince boru cerceve ve tutamak kollari %6,6 cubuk alani (sinir %2; 6% x 1,8 m = 11 cm)
    "el_arabasi": {
        "why": "olcut 3 (kalinlik): ince boru cerceve, tutamak kollari ve bacaklar",
        "prompt": ("A chunky farm wheelbarrow with a stocky toy-like build: deep blue metal tub with a thick rounded rim, "
                   "one big fat rubber front wheel with a yellow hub on a thick fork, two thick rounded wooden handle beams "
                   "as thick as a wrist ending in chunky grips, two short stubby legs at the back. Every frame part is thick "
                   "and rounded; no thin tubes, rods or bars. " + STYLE),
        "face_limit": 12000, "then": "segment:segmentation_granularity=simple",
    },
    # olcut 3: %5,0 cubuk/diken alani: sivri kok uclari ve tac kumelerinden cikan ince pullar (4,2 m agacta sinir 25 cm)
    "agac": {
        "why": "olcut 3 (kalinlik): sivri kok uclari, tacta ince yaprak pullari",
        "prompt": ("A round meadow tree: a thick short trunk that flares at the base into four rounded root bumps (no pointed "
                   "roots), a few stout branches, and a big puffy crown made of five or six soft rounded leaf clusters with "
                   "smooth outlines; no thin twigs, no loose leaves sticking out, no spikes. Warm green leaves, brown bark. "
                   + STYLE),
        "face_limit": 7000, "then": "",
    },
    # olcut 3: %2,1 (sinir %2): nozul halkasinin ince kenari ve ustteki ince gosterge cubugu
    "capa": {
        "why": "olcut 3 (kalinlik, sinirda): nozul halkasinin ince kenari, ustte ince gosterge cubugu",
        "prompt": ("A handheld multi-tool gadget for a space farmer: chunky pistol-grip body in off-white plastic and blue "
                   "metal, a big round yellow emitter nozzle at the front with a thick rounded rim, a big top carry handle, a "
                   "stubby rounded teal gauge dome on top (no antenna, no thin rods), a slot on top for swappable heads. "
                   + STYLE),
        "face_limit": 4000, "then": "",
    },
}


def cmd_for(name, r, dry):
    c = ["tripo.cmd" if os.name == "nt" else "tripo", "make", r["prompt"], *Q, "-p", f"face_limit={r['face_limit']}", "--name", f"{name}_v2"]
    if r["then"]:
        c += ["--then", r["then"]]
    if dry:
        c += ["--dry-run", "--json"]
    else:
        out = os.path.join(MAN["sourceRoot"], ".tools", "tripo", f"{name}_v2")
        c += ["--out", out, "--json", "--yes", "--no-open"]
    return c


def main():
    argv = sys.argv[1:]
    run = "--run" in argv
    names = [a for a in argv if not a.startswith("--")] or list(REQUESTS)
    if run and not names:
        raise SystemExit("--run icin varlik adi gerekir")
    total = 0
    for n in names:
        r = REQUESTS[n]
        c = cmd_for(n, r, dry=not run)
        p = subprocess.run(c, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=os.path.join(MAN["sourceRoot"], ".tools", "tripo"))
        if not run:
            try:
                d = json.loads(p.stdout)
            except Exception:
                print(f"{n}: dry-run cikti okunamadi (kod {p.returncode}): {p.stdout[-400:]} {p.stderr[-400:]}")
                continue
            est = d.get("generation_credits_estimate")
            # tripo --dry-run "--then segment" adimini tahmine katmiyor; M0'da olculen: parcalama 40 kredi (El Arabasi 70 = 30 + 40)
            then = SEGMENT_CREDITS if r["then"].startswith("segment") else 0
            total += (est or 0) + then
            print(f"{n}: gecerli={d.get('valid')} tahmin={est} kredi + sonraki adim {then} = {(est or 0) + then}; "
                  f"uyari={d.get('warnings')}; hata={d.get('errors')}; neden: {r['why']}")
        else:
            print(f"{n}: kod {p.returncode}\n{p.stdout[-2000:]}\n{p.stderr[-2000:]}")
    if not run:
        print(f"TOPLAM tahmin (birer deneme): {total} kredi; SANAT-SES §9 en fazla 4 deneme -> en kotu {total * 4}")


if __name__ == "__main__":
    main()
