"""
M1 altin varliklarini toplu isler ve game/data/assets.json'u gunceller (URETIM §2.2, §3).

  python pipeline/run_golden.py                 # manifestteki hepsi
  python pipeline/run_golden.py kaya tas_firin  # yalniz bunlar
  python pipeline/run_golden.py --sync-only     # Blender yok: raporlardan assets.json'u yeniden yaz

Her varlik icin: eski raporu bir kez progress/m1/varlik/eski/ altina saklar, Blender'da clean_asset.py'yi calistirir
(gunluk progress/m1/varlik/log/<ad>.log), eski ve yeni boy/parca konumlarini karsilastirir (progress/m1/varlik/fark.md).
assets.json'da yalniz betigin bildigi alanlar yazilir (olcut 1-5, ucgen, boy, LOD, kabuk, ikon, kart, parca, soket, durum);
elle yazilanlar (olcut 6-8 puanlari, kaynak, lisans, notlar, sahibin onayi) korunur.
Durum kurali: sekiz olcut gecti + sahibin onayi = "altin"; sekiz olcut gecti = "onay_bekliyor"; aksi "yer_tutucu".
"""
import json, os, subprocess, sys, shutil, time

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MAN = json.load(open(os.path.join(ROOT, "pipeline", "golden_manifest.json"), encoding="utf-8"))
ASSETS = os.path.join(ROOT, "game", "data", "assets.json")
CRIT_NAMES = {1: "doku", 2: "poligon", 3: "kalinlik", 4: "butunluk", 5: "olcek_eksen", 6: "siluet", 7: "dil_uyumu", 8: "okunaklilik"}


def rel(p):
    return os.path.relpath(p, ROOT).replace("\\", "/")


def res_path(p):
    r = rel(p)
    return "res://" + r[len("game/"):] if r.startswith("game/") else r


def run_one(name, a):
    d = {**MAN["defaults"], **a}
    out = os.path.join(ROOT, MAN["out"], name, f"{name}.glb")
    old = os.path.splitext(out)[0] + ".report.json"
    bdir = os.path.join(ROOT, MAN["backup"]); os.makedirs(bdir, exist_ok=True)
    bak = os.path.join(bdir, f"{name}.report.json")
    if os.path.exists(old) and not os.path.exists(bak):
        shutil.copy(old, bak)
    cmd = [MAN["blender"], "-b", "--factory-startup", "--python-exit-code", "1",
           "-P", os.path.join(ROOT, "pipeline", "clean_asset.py"), "--",
           "--in", os.path.join(MAN["sourceRoot"], d["in"]), "--out", out, "--name", name, "--label", d.get("label", name),
           "--height", str(d["height"]), "--tris", str(d["tris"]), "--class", d["class"],
           "--renders", os.path.join(ROOT, MAN["renders"], name), "--card", os.path.join(ROOT, MAN["cards"], f"{name}.png"),
           "--tex_size", str(d["tex_size"]), "--weld", str(d["weld"]), "--lods", d["lods"], "--lod_tex", d["lod_tex"],
           "--hull_points", str(d["hull_points"]), "--thin", str(d["thin"]), "--thin_area", str(d["thin_area"]),
           "--thick_samples", str(d["thick_samples"]), "--icon_size", str(d["icon_size"]), "--forward", d["forward"],
           "--drop_floating", str(d["drop_floating"]), "--dark_fill", str(d.get("dark_fill", 0.0))]
    if d.get("keep_texture"):
        cmd.append("--keep_texture")
    if d.get("parts"):
        cmd.append("--parts")
    for k in ("rename", "merge", "drop", "pivots", "sockets"):
        if d.get(k):
            cmd += [f"--{k}", d[k]]
    ldir = os.path.join(ROOT, MAN["logs"]); os.makedirs(ldir, exist_ok=True)
    t0 = time.time()
    with open(os.path.join(ldir, f"{name}.log"), "w", encoding="utf-8", errors="replace") as fh:
        p = subprocess.run(cmd, stdout=fh, stderr=subprocess.STDOUT, cwd=ROOT)
    print(f"{name}: exit={p.returncode} {time.time() - t0:.0f} sn", flush=True)
    return p.returncode == 0


def diff_rows(name):
    """Eski ve yeni rapor: boy, kutu, parca merkezleri (diger akislar bugunku GLB'lere gore kod yaziyor)."""
    new_p = os.path.join(ROOT, MAN["out"], name, f"{name}.report.json")
    old_p = os.path.join(ROOT, MAN["backup"], f"{name}.report.json")
    if not (os.path.exists(new_p) and os.path.exists(old_p)):
        return [f"| {name} | (karsilastirma yok) | | |"]
    n = json.load(open(new_p, encoding="utf-8")); o = json.load(open(old_p, encoding="utf-8"))
    rows = [f"| {name} | boy x,y,z (m) | {o.get('size_m')} | {n.get('size_m')} |",
            f"| {name} | ucgen | {o.get('tris')} | {n.get('tris')} |"]
    op = {p["name"]: p for p in o.get("parts_named", [])}
    for p in n.get("parts_named", []):
        q = op.get(p["name"])
        was = f"{q['center']} ({q.get('source', '?')})" if q else "yok"
        rows.append(f"| {name} | parca `{p['name']}` merkez | {was} | {p['center']} ({p['source']}) |")
    for k in op:
        if k not in {p["name"] for p in n.get("parts_named", [])}:
            rows.append(f"| {name} | parca `{k}` | {op[k]['center']} | kaldirildi |")
    return rows


def compact(o, ind=0, width=150):
    """Okunur JSON: kisa nesne ve diziler tek satirda."""
    one = json.dumps(o, ensure_ascii=False)
    if len(one) + ind <= width or not isinstance(o, (dict, list)):
        return one
    pad = " " * (ind + 2)
    if isinstance(o, dict):
        items = [f'{pad}{json.dumps(k, ensure_ascii=False)}: {compact(v, ind + 2, width)}' for k, v in o.items()]
        return "{\n" + ",\n".join(items) + "\n" + " " * ind + "}"
    items = [pad + compact(v, ind + 2, width) for v in o]
    return "[\n" + ",\n".join(items) + "\n" + " " * ind + "]"


def crit_value(k, c):
    if k == 1:
        return f"{c.get('base_color_maps', 0)} renk haritasi, en buyuk {max([max(v) for v in c.get('textures', {}).values()] or [0])} px (sinir {c.get('max_tex')})"
    if k == 2:
        return f"{c['value']} / {c['budget']} ucgen" + (" (sinif alt sinirinin altinda)" if c.get("below_min") else "")
    if k == 3:
        return (f"cubuk/diken alani %{c['thin_share'] * 100:.1f} (izin %{c['allowed_share'] * 100:.0f}, sinir {c['limit_m']} m, en ince {c['stick_min_t_m']} m); "
                f"levha payi %{c['plate_share'] * 100:.1f}; eski kutu olcusu {c['old_bbox_min_ratio']}")
    if k == 4:
        return (f"manifold disi {c['non_manifold_edges']}, delik kenari {c['boundary_edges']}, ters sarimli kenar {c['inverted_faces']}, "
                f"negatif hacimli parca {c['negative_volume_parts']}, havada {len(c['floating_parts'])}, ayrik parca {c['loose_parts']}")
    if k == 5:
        return f"boy {c['height']} m (hedef {c['target']}), taban z {c['base_z']}, orta {c['center_xy']}, on {c['forward']}"
    return ""


def sync(names):
    data = json.load(open(ASSETS, encoding="utf-8"))
    by_id = {e["id"]: e for e in data["assets"]}
    for name in names:
        rp = os.path.join(ROOT, MAN["out"], name, f"{name}.report.json")
        if not os.path.exists(rp):
            continue
        r = json.load(open(rp, encoding="utf-8"))
        if "1_texture" not in r.get("checks", {}):
            print(f"{name}: eski bicim rapor, atlandi (once yeniden isle)")
            continue
        e = by_id.get(name)
        if e is None:
            e = {"id": name, "name": MAN["assets"][name].get("label", name)}
            data["assets"].append(e); by_id[name] = e
        old = {c["id"]: c for c in e.get("criteria", [])}
        crit = []
        keys = ["1_texture", "2_polygon", "3_thickness", "4_integrity", "5_scale_axis"]
        for i, k in enumerate(keys, start=1):
            c = r["checks"][k]
            crit.append({"id": i, "name": CRIT_NAMES[i], "pass": bool(c["pass"]), "value": crit_value(i, c),
                         "note": old.get(i, {}).get("note", "")})
        for i in (6, 7, 8):
            c = old.get(i, {"id": i, "name": CRIT_NAMES[i], "pass": None, "score": None, "note": "puanlanmadi"})
            c["name"] = CRIT_NAMES[i]
            crit.append(c)
        e["criteria"] = crit
        e["model"] = res_path(r["out"])
        e["budgetClass"] = r.get("class", "")
        e["forward"] = r.get("forward", "")
        e["tris"] = r["tris"]
        e["sizeM"] = r["size_m"]
        e["lods"] = [{"tris": l["tris"], "tex": l.get("tex"), "path": res_path(l["file"])} for l in r.get("lods", [])]
        e["hull"] = res_path(r["hull"]["file"]) if r.get("hull") else ""
        e["icon"] = res_path(r["icon"]) if r.get("icon") else ""
        e["card"] = rel(r["card"]) if r.get("card") else ""
        e["parts"] = [p["name"] for p in r.get("parts_named", [])]
        e["sockets"] = [{"name": s["name"], "pos": s["pos_godot"]} for s in r.get("sockets", [])]
        all_pass = all(c.get("pass") is True for c in crit)
        if all_pass and e.get("ownerApproved"):
            e["status"] = "altin"
        elif all_pass:
            e["status"] = "onay_bekliyor"
        else:
            e["status"] = "yer_tutucu"
            failed = [f"{c['id']} {c['name']}" for c in crit if c.get("pass") is False]
            unscored = [str(c["id"]) for c in crit if c.get("pass") is None]
            bits = []
            if failed:
                bits.append("kalan olcut: " + ", ".join(failed))
            if unscored:
                bits.append("puanlanmamis: " + ", ".join(unscored))
            e["statusNote"] = "; ".join(bits)
        if e["status"] != "yer_tutucu":
            e["statusNote"] = "sekiz olcut gecti; sahibin onayini bekliyor" if e["status"] == "onay_bekliyor" else "sahip onayladi"
    with open(ASSETS, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(compact(data) + "\n")
    counts = {}
    for e in data["assets"]:
        counts[e["status"]] = counts.get(e["status"], 0) + 1
    print("assets.json:", counts)


def main():
    argv = sys.argv[1:]
    sync_only = "--sync-only" in argv
    names = [a for a in argv if not a.startswith("--")] or list(MAN["assets"].keys())
    for n in names:
        if n not in MAN["assets"]:
            raise SystemExit(f"manifestte yok: {n}")
    ok = True
    if not sync_only:
        for n in names:
            ok = run_one(n, MAN["assets"][n]) and ok
        rows = ["| varlik | olcu | once | sonra |", "|---|---|---|---|"]
        for n in MAN["assets"]:  # hepsi: tek varlik islense de fark.md eksiksiz kalsin
            rows += diff_rows(n)
        with open(os.path.join(ROOT, "progress", "m1", "varlik", "fark.md"), "w", encoding="utf-8", newline="\n") as fh:
            fh.write("# Yeniden isleme: once ve sonra\n\nKaynak: `pipeline/run_golden.py`. Konumlar Blender ekseninde (x, y, z yukari).\n\n" + "\n".join(rows) + "\n")
    sync(list(MAN["assets"].keys()))
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
