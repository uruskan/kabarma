"""Kabarma ses denetimi (URETIM §2.2 H.3, I.3, J; SANAT-SES §10). Yalniz Python standart kutuphanesi + ffmpeg.

Her dosya icin: sure, tepe (ornek ve gercek tepe, dBTP), kirpilma, yukseklik (LUFS: butunlesik ve en yuksek anlik),
bastaki ve sondaki sessizlik, spektrum agirlik merkezi. Dongulerde dikis (bas ile son arasindaki atlama ve duzey
farki). Muzikte tempo kestirimi ve katmanlarin vurus hizasi. Ayni grubun sesleri spektrumda benzer aralikta mi.
Suna: efektlerin ortalamasinin 3 dB ustunde mi (SANAT-SES §10.1).

Kullanim (depo kokunden):
  python pipeline/audio_qc.py                         # game/assets/audio altindaki her sey
  python pipeline/audio_qc.py game/assets/audio/sfx/siren.mp3 ...
  python pipeline/audio_qc.py --fix-dir build/fixed   # bas/son sessizligi kirpilmis, tepesi -1,5 dBTP'ye inmis kopyalar

Rapor: progress/m1/ses/audio_qc.json ve audio_qc.md (--out ile degisir). Herhangi bir KALDI varsa cikis kodu 1
(--report-only ile 0). "UYARI" satirlari belgenin sayi vermedigi yerlerdir: sahibin dinleme turuna kalir.
"""
import argparse
import array
import json
import math
import os
import re
import shutil
import statistics
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
AUDIO = ROOT / "game" / "assets" / "audio"
DATA = ROOT / "game" / "data" / "audio.json"

# Esikler. "belge" yazanlar belgeden; digerleri belgenin sustugu yerde secilen deger (rapora UYARI olarak gecer).
LIMITS = {
    "true_peak_dbtp": -1.0,        # belge: SANAT-SES §10.1 "ana cikista tepe -1 dBTP" (dosya basina da uygulanir)
    "clip_run": 3,                  # ard arda 3 tam olcek ornek = kirpilma
    "silence_dbfs": -50.0,          # sessizlik esigi
    "lead_silence_s": {"sfx": 0.03, "loop": 0.25, "voice": 0.30, "music": 0.50},   # belge H.3: bas sessizlik kirpilir
    "tail_silence_s": {"sfx": 0.25, "loop": 0.25, "voice": 0.60, "music": 2.00},   # belge H.3: son sessizlik kirpilir
    "duration_s": {"sfx": (0.1, 30.0), "ui": (0.03, 1.2), "loop": (1.0, 30.0), "voice": (0.4, 15.0),
                   "music_day": (120.0, 240.0), "music_kabarma": (60.0, 180.0), "music": (30.0, 600.0)},
    "seam_level_db": 3.0,           # dongu: son 50 ms ile ilk 50 ms arasindaki duzey farki
    "seam_jump": 6.0,               # dongu: dikisteki ornek atlamasi / tipik ornek farki
    "sfx_lufs_window": (-26.0, -10.0),   # UYARI: en yuksek anlik yukseklik (kisa seste butunlesik LUFS olcmez)
    "music_lufs_window": (-20.0, -12.0), # UYARI
    "voice_over_sfx_db": 3.0,       # belge: SANAT-SES §10.1 "Suna, efektlerin ortalamasinin 3 dB ustunde"
    "voice_over_sfx_tol": 2.0,      # UYARI
    "centroid_ratio": 2.5,          # UYARI: grup ortancasindan bu kat sapan spektrum
    "tempo_tol": 0.03,              # muzik: veri temposundan sapma (yarim/iki kat da kabul)
    "layer_offset_ms": 15.0,        # katmanlar arasi vurus kaymasi
}

PCM_RATE = 22050


def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")


def need(tool):
    if shutil.which(tool) is None:
        sys.exit(f"{tool} bulunamadi (PATH). ffmpeg kurulu olmali.")


def probe_duration(path):
    r = run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(path)])
    try:
        return float(r.stdout.strip())
    except ValueError:
        return None


def ebur128(path):
    """Butunlesik LUFS, gercek tepe (dBTP) ve en yuksek anlik yukseklik (M)."""
    r = run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-filter_complex", "ebur128=peak=true", "-f", "null", "-"])
    text = r.stderr
    out = {"lufs_i": None, "true_peak_dbtp": None, "lufs_m_max": None}
    summary = text[text.rfind("Summary:"):] if "Summary:" in text else ""
    m = re.search(r"I:\s+(-?[\d.]+|-inf)\s+LUFS", summary)
    if m and m.group(1) != "-inf":
        out["lufs_i"] = float(m.group(1))
    m = re.search(r"True peak:\s+Peak:\s+(-?[\d.]+|-inf)\s+dBFS", summary)
    if m and m.group(1) != "-inf":
        out["true_peak_dbtp"] = float(m.group(1))
    ms = [float(x) for x in re.findall(r"\sM:\s*(-?[\d.]+)", text)]
    if ms:
        out["lufs_m_max"] = max(ms)
    # 400 ms'den kisa seste anlik ve butunlesik yukseklik olculemez (ffmpeg -70/-120 verir): yok say.
    for k in ("lufs_i", "lufs_m_max"):
        if out[k] is not None and out[k] <= -69.0:
            out[k] = None
    return out


def centroid(path):
    """Ortalama spektrum agirlik merkezi (Hz), ffmpeg aspectralstats ile; sessiz kareler sayilmaz."""
    r = run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af",
             "aspectralstats=measure=centroid+flux,ametadata=mode=print:file=-", "-f", "null", "-"])
    vals = [float(x) for x in re.findall(r"aspectralstats\.1\.centroid=([\d.]+)", r.stdout)]
    vals = [v for v in vals if v > 1.0]
    return statistics.median(vals) if vals else None


def pcm(path, rate=PCM_RATE):
    """Tek kanal, 16 bit ornekler."""
    r = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", str(path), "-ac", "1", "-ar", str(rate),
                        "-f", "s16le", "-"], capture_output=True)
    a = array.array("h")
    a.frombytes(r.stdout[: len(r.stdout) // 2 * 2])
    if sys.byteorder != "little":
        a.byteswap()
    return a


def db(x):
    return -120.0 if x <= 0 else 20.0 * math.log10(x)


def rms(seg):
    if not seg:
        return 0.0
    return math.sqrt(sum(s * s for s in seg) / len(seg)) / 32768.0


def samples_stats(a, rate):
    out = {}
    if not a:
        return {"sample_peak_dbfs": None, "clipped_runs": 0, "lead_silence_s": None, "tail_silence_s": None}
    peak = max(max(a), -min(a))
    out["sample_peak_dbfs"] = round(db(peak / 32768.0), 2)
    runs, run_len = 0, 0
    for s in a:
        if s >= 32700 or s <= -32700:
            run_len += 1
            if run_len == LIMITS["clip_run"]:
                runs += 1
        else:
            run_len = 0
    out["clipped_runs"] = runs
    thr = int(32768 * 10 ** (LIMITS["silence_dbfs"] / 20.0))
    first = next((i for i, s in enumerate(a) if abs(s) > thr), len(a))
    last = next((i for i in range(len(a) - 1, -1, -1) if abs(a[i]) > thr), -1)
    out["lead_silence_s"] = round(first / rate, 3)
    out["tail_silence_s"] = round((len(a) - 1 - last) / rate, 3) if last >= 0 else round(len(a) / rate, 3)
    # En yuksek 100 ms RMS (dBFS): kisa seslerde yukseklik yerine kiyas icin.
    w = max(1, int(rate * 0.1))
    best = max((rms(a[i:i + w]) for i in range(0, max(1, len(a) - w + 1), w // 2)), default=0.0)
    out["rms100_dbfs"] = round(db(best), 1)
    return out


def seam(a, rate):
    """Dongu dikisi: son ve ilk 50 ms'nin duzey farki; dikisteki atlamanin tipik ornek farkina orani."""
    n = int(rate * 0.05)
    if len(a) < 4 * n:
        return {"seam_level_db": None, "seam_jump": None}
    head, tail = a[:n], a[-n:]
    lvl = abs(db(rms(head)) - db(rms(tail)))
    step = 37
    diffs = sorted(abs(a[i + 1] - a[i]) for i in range(0, len(a) - 1, step))
    typical = max(1.0, statistics.median(diffs[len(diffs) // 2:]))  # ust yarinin ortancasi: sessiz yerler saymasin
    jump = abs(a[0] - a[-1]) / typical
    return {"seam_level_db": round(lvl, 2), "seam_jump": round(jump, 2)}


def envelope(a, rate, hop_s=0.01):
    hop = max(1, int(rate * hop_s))
    env = []
    for i in range(0, len(a) - hop, hop):
        seg = a[i:i + hop]
        env.append(sum(s * s for s in seg))
    onset = [0.0] + [max(0.0, math.sqrt(env[i]) - math.sqrt(env[i - 1])) for i in range(1, len(env))]
    return onset, hop_s


def tempo(a, rate):
    """Vurus siddeti zarfinin ozilinti tepesinden BPM (60-200)."""
    onset, hop_s = envelope(a, rate)
    if len(onset) < 400:
        return None
    mean = sum(onset) / len(onset)
    o = [x - mean for x in onset]
    best, best_lag = -1e30, None
    lo, hi = int(60 / 200 / hop_s), int(60 / 60 / hop_s)
    scores = {}
    for lag in range(lo, hi + 1):
        s = sum(o[i] * o[i + lag] for i in range(len(o) - lag))
        scores[lag] = s
        if s > best:
            best, best_lag = s, lag
    if best_lag is None:
        return None
    l0, l1, l2 = scores.get(best_lag - 1, best), best, scores.get(best_lag + 1, best)
    den = l0 - 2 * l1 + l2
    frac = 0.5 * (l0 - l2) / den if den else 0.0
    return round(60.0 / ((best_lag + frac) * hop_s), 1)


def align_offset_ms(a, b, rate, max_ms=200):
    """Iki katmanin vurus zarflari arasindaki en iyi kayma (ms). 0'a yakin olmali."""
    oa, hop_s = envelope(a, rate)
    ob, _ = envelope(b, rate)
    n = min(len(oa), len(ob))
    if n < 200:
        return None
    oa, ob = oa[:n], ob[:n]
    best, best_lag = -1e30, 0
    m = int(max_ms / 1000 / hop_s)
    for lag in range(-m, m + 1):
        if lag >= 0:
            s = sum(oa[i] * ob[i + lag] for i in range(n - lag))
        else:
            s = sum(oa[i - lag] * ob[i] for i in range(n + lag))
        if s > best:
            best, best_lag = s, lag
    return round(best_lag * hop_s * 1000.0, 1)


def classify(path, data):
    """Dosyanin turu ve verideki karsiligi: (tur, ses kimligi, grup, dongu mu)."""
    p = Path(path)
    parts = [x.lower() for x in p.parts]
    stem = p.stem
    if "voice" in parts:
        return "voice", stem, "suna", False
    if "music" in parts:
        if stem.startswith("cayir"):
            return "music_day", stem, "muzik", False
        if stem.startswith("kabarma"):
            return "music_kabarma", stem, "muzik", True
        return "music", stem, "muzik", False
    sounds = {s["id"]: s for s in data.get("sounds", [])}
    by_m0 = {s["m0"]: s for s in data.get("sounds", []) if s.get("m0")}
    m = re.match(r"^(.*)_(\d+)$", stem)
    s = sounds.get(m.group(1)) if m else None
    s = s or by_m0.get(stem)
    if s is None:
        return "sfx", stem, "?", False
    kind = "loop" if s.get("loop") else ("ui" if s.get("group") == "arayuz" else "sfx")
    return kind, s["id"], s.get("group", "?"), bool(s.get("loop"))


def check_file(path, data, verdicts):
    kind, sid, group, loop = classify(path, data)
    rate = PCM_RATE
    r = {"file": str(Path(path).relative_to(ROOT)).replace("\\", "/") if Path(path).is_relative_to(ROOT) else str(path),
         "kind": kind, "id": sid, "group": group, "loop": loop}
    r["duration_s"] = probe_duration(path)
    r.update(ebur128(path))
    a = pcm(path, rate)
    r.update(samples_stats(a, rate))
    r["centroid_hz"] = centroid(path)
    if loop:
        r.update(seam(a, rate))
    if kind.startswith("music"):
        r["tempo_bpm"] = tempo(a, rate)
    r["checks"] = judge(r, data)
    for c in r["checks"]:
        verdicts.append((r["file"], c))
    return r, a


def judge(r, data):
    out = []

    def add(level, name, msg):
        out.append({"level": level, "check": name, "msg": msg})

    kind = r["kind"]
    base = "loop" if r["loop"] and kind not in ("music_kabarma",) else ("voice" if kind == "voice" else ("music" if kind.startswith("music") else "sfx"))
    lo, hi = LIMITS["duration_s"].get(kind, LIMITS["duration_s"].get(base, (0.0, 1e9)))
    d = r["duration_s"]
    if d is None:
        add("KALDI", "sure", "okunamadi")
    elif not (lo <= d <= hi):
        add("KALDI", "sure", f"{d:.2f} sn, beklenen {lo}-{hi}")
    tp = r["true_peak_dbtp"]
    if tp is not None and tp > LIMITS["true_peak_dbtp"]:
        add("KALDI", "tepe", f"gercek tepe {tp:.1f} dBTP > {LIMITS['true_peak_dbtp']}")
    if r["clipped_runs"]:
        add("KALDI", "kirpilma", f"{r['clipped_runs']} kirpilmis bolge")
    lead, tail = r["lead_silence_s"], r["tail_silence_s"]
    if lead is not None and lead > LIMITS["lead_silence_s"][base]:
        add("KALDI", "bas_sessizlik", f"{lead:.3f} sn > {LIMITS['lead_silence_s'][base]}")
    if tail is not None and tail > LIMITS["tail_silence_s"][base]:
        add("KALDI", "son_sessizlik", f"{tail:.3f} sn > {LIMITS['tail_silence_s'][base]}")
    if r["loop"]:
        if r.get("seam_level_db") is not None and r["seam_level_db"] > LIMITS["seam_level_db"]:
            add("KALDI", "dikis_duzey", f"bas/son duzey farki {r['seam_level_db']} dB")
        if r.get("seam_jump") is not None and r["seam_jump"] > LIMITS["seam_jump"]:
            add("KALDI", "dikis_atlama", f"dikiste atlama {r['seam_jump']} x tipik")
    if kind in ("sfx", "ui", "loop"):
        a, b = LIMITS["sfx_lufs_window"]
        if r["lufs_m_max"] is None:
            add("UYARI", "yukseklik", f"400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS {r['rms100_dbfs']} dBFS")
        elif not (a <= r["lufs_m_max"] <= b):
            add("UYARI", "yukseklik", f"en yuksek anlik {r['lufs_m_max']:.1f} LUFS, secilen aralik {a}..{b}")
    if kind.startswith("music") and r["lufs_i"] is not None:
        a, b = LIMITS["music_lufs_window"]
        if not (a <= r["lufs_i"] <= b):
            add("UYARI", "yukseklik", f"{r['lufs_i']:.1f} LUFS, secilen aralik {a}..{b}")
    if kind.startswith("music") and r.get("tempo_bpm"):
        want = data.get("music", {}).get("kabarma", {}).get("bpm", 0) if kind == "music_kabarma" else 0
        if want:
            t = r["tempo_bpm"]
            ok = any(abs(t * f - want) <= want * LIMITS["tempo_tol"] for f in (0.5, 1.0, 2.0))
            if not ok:
                add("KALDI", "tempo", f"kestirilen {t} BPM, veri {want} BPM")
    return out


def group_checks(results, verdicts):
    """Ayni grubun sesleri benzer spektrum araliginda mi; Suna efektlerin 3 dB ustunde mi."""
    by_group = {}
    for r in results:
        if r["kind"] in ("sfx", "ui", "loop") and r["centroid_hz"]:
            by_group.setdefault(r["group"], []).append(r)
    for g, rs in by_group.items():
        if len(rs) < 3:
            continue
        med = statistics.median(x["centroid_hz"] for x in rs)
        for x in rs:
            ratio = max(x["centroid_hz"], med) / max(1.0, min(x["centroid_hz"], med))
            if ratio > LIMITS["centroid_ratio"]:
                c = {"level": "UYARI", "check": "spektrum", "msg": f"agirlik merkezi {x['centroid_hz']:.0f} Hz, grup '{g}' ortancasi {med:.0f} Hz"}
                x["checks"].append(c)
                verdicts.append((x["file"], c))
    sfx = [r["lufs_m_max"] for r in results if r["kind"] in ("sfx", "ui") and r["lufs_m_max"] is not None]
    voice = [r["lufs_m_max"] for r in results if r["kind"] == "voice" and r["lufs_m_max"] is not None]
    summary = {}
    if sfx and voice:
        diff = statistics.mean(voice) - statistics.mean(sfx)
        summary["voice_minus_sfx_db"] = round(diff, 1)
        if abs(diff - LIMITS["voice_over_sfx_db"]) > LIMITS["voice_over_sfx_tol"]:
            verdicts.append(("(Suna)", {"level": "UYARI", "check": "suna_duzey", "msg": f"Suna efektlerden {diff:.1f} dB yuksek; belge +3 dB"}))
    return summary


def layer_alignment(results, pcms, verdicts):
    layers = sorted([r for r in results if r["kind"] == "music_kabarma"], key=lambda r: r["file"])
    out = {}
    if len(layers) < 2:
        return out
    base = layers[0]
    durs = [r["duration_s"] for r in layers if r["duration_s"]]
    if durs and max(durs) - min(durs) > 0.05:
        verdicts.append(("(Kabarma katmanlari)", {"level": "KALDI", "check": "katman_boyu", "msg": f"katman sureleri esit degil: {durs}"}))
    for r in layers[1:]:
        off = align_offset_ms(pcms[base["file"]], pcms[r["file"]], PCM_RATE)
        out[r["file"]] = off
        if off is not None and abs(off) > LIMITS["layer_offset_ms"]:
            verdicts.append((r["file"], {"level": "KALDI", "check": "vurus_hizasi", "msg": f"{base['file']} ile {off} ms kayik"}))
    return out


def needs_fix(r):
    """Yalniz KALDI'da duzeltilir: UYARI icin kayipli yeniden kodlama yapilmaz (dongu dikisi bozulmasin)."""
    return any(c["level"] == "KALDI" and c["check"] in ("tepe", "bas_sessizlik", "son_sessizlik") for c in r["checks"])


def fix(path, r, out_dir):
    """
    Bas sessizligini 10 ms'ye, son sessizligi 100 ms'ye kirpar (donguler kirpilmaz); gercek tepe -1 dBTP ustundeyse
    -1,5'e indirir. Duzeltilmis dosyanin yolu; ffmpeg basarisizsa None (asil dosyaya dokunulmaz).
    """
    out = Path(out_dir) / Path(r["file"]).relative_to("game/assets/audio") if r["file"].startswith("game/assets/audio/") else Path(out_dir) / Path(path).name
    out.parent.mkdir(parents=True, exist_ok=True)
    filters = []
    if not r["loop"]:
        start = max(0.0, (r["lead_silence_s"] or 0) - 0.01)
        end = (r["duration_s"] or 0) - max(0.0, (r["tail_silence_s"] or 0) - 0.1)
        filters.append(f"atrim=start={start:.3f}:end={end:.3f},asetpts=PTS-STARTPTS")
        filters.append(f"afade=t=out:st={max(0.0, end - start - 0.02):.3f}:d=0.02")
    tp = r["true_peak_dbtp"]
    gain = -1.5 - tp if tp is not None and tp > LIMITS["true_peak_dbtp"] else None
    # mp3'e yeniden kodlama tepeyi ~0,5 dB geri tasiyabilir: olc, sinirin ustundeyse farki kadar daha indir (en cok 3 deneme).
    for _ in range(3):
        cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(path)]
        af = filters + ([f"volume={gain:.2f}dB"] if gain is not None else [])
        if af:
            cmd += ["-af", ",".join(af)]
        cmd += ["-c:a", "libmp3lame", "-q:a", "2", str(out)]
        res = run(cmd)
        if res.returncode != 0 or not out.exists() or out.stat().st_size == 0:
            print(f"DUZELTME BASARISIZ {path}: {res.stderr.strip()[:300]}")
            return None
        if gain is None:
            break
        got = ebur128(out)["true_peak_dbtp"]
        if got is None or got <= LIMITS["true_peak_dbtp"]:
            break
        gain -= got - (-1.5)
    return out


def main():
    ap = argparse.ArgumentParser(description="Kabarma ses denetimi")
    ap.add_argument("files", nargs="*", help="denetlenecek dosyalar (bos: game/assets/audio altindaki her mp3/ogg/wav)")
    ap.add_argument("--out", default="progress/m1/ses", help="rapor klasoru")
    ap.add_argument("--report-only", action="store_true", help="KALDI olsa da cikis kodu 0")
    ap.add_argument("--fix-dir", help="duzeltilmis kopyalari buraya yaz (asillara dokunmaz)")
    args = ap.parse_args()
    need("ffmpeg")
    need("ffprobe")
    data = json.loads(DATA.read_text(encoding="utf-8")) if DATA.exists() else {}
    files = [Path(f).resolve() for f in args.files] or sorted(p for p in AUDIO.rglob("*") if p.suffix.lower() in (".mp3", ".ogg", ".wav"))
    results, verdicts, pcms = [], [], {}
    for f in files:
        r, a = check_file(f, data, verdicts)
        results.append(r)
        if r["kind"] == "music_kabarma":
            pcms[r["file"]] = a
        mark = "KALDI" if any(c["level"] == "KALDI" for c in r["checks"]) else ("UYARI" if r["checks"] else "gecti")
        print(f"{mark:6} {r['file']}  {r['duration_s'] or 0:.2f}s  tp={r['true_peak_dbtp']}  M={r['lufs_m_max']}")
    summary = group_checks(results, verdicts)
    summary["layer_offsets_ms"] = layer_alignment(results, pcms, verdicts)
    fails = [v for v in verdicts if v[1]["level"] == "KALDI"]
    warns = [v for v in verdicts if v[1]["level"] == "UYARI"]
    summary.update({"files": len(results), "fail": len(fails), "warn": len(warns),
                    "passed": sum(1 for r in results if not any(c["level"] == "KALDI" for c in r["checks"]))})

    out = (ROOT / args.out) if not Path(args.out).is_absolute() else Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    (out / "audio_qc.json").write_text(json.dumps({"limits": LIMITS, "summary": summary, "results": results}, ensure_ascii=False, indent=1), encoding="utf-8")
    lines = ["# Ses denetimi (pipeline/audio_qc.py)", "",
             f"{summary['files']} dosya · {summary['passed']} gecti · {summary['fail']} KALDI · {summary['warn']} UYARI", "",
             "Tonlama (key) tespiti yok; tempo kestirimi yalnizca muzikte. Kulak yargisi sahibin dinleme turunda.", "",
             "| Dosya | Tur | Sure (sn) | Gercek tepe (dBTP) | LUFS-I | En yuksek anlik | Bas/son sessizlik (sn) | Dikis | Sonuc |",
             "|---|---|---|---|---|---|---|---|---|"]
    for r in results:
        res = "; ".join(f"{c['level']} {c['check']}: {c['msg']}" for c in r["checks"]) or "gecti"
        sm = f"{r.get('seam_level_db')} dB / {r.get('seam_jump')}x" if r["loop"] else ""
        lines.append(f"| {r['file']} | {r['kind']} | {r['duration_s'] or 0:.2f} | {r['true_peak_dbtp']} | {r['lufs_i']} | {r['lufs_m_max']} | "
                     f"{r['lead_silence_s']} / {r['tail_silence_s']} | {sm} | {res} |")
    if "voice_minus_sfx_db" in summary:
        lines += ["", f"Suna - efekt ortalamasi: {summary['voice_minus_sfx_db']} dB (belge: +3 dB)."]
    tempos = [f"{r['file']}: {r['tempo_bpm']} BPM" for r in results if r.get("tempo_bpm")]
    if tempos:
        lines += ["", "Tempo kestirimi: " + ", ".join(tempos) + f" (veri: Kabarma {data.get('music', {}).get('kabarma', {}).get('bpm')} BPM)."]
    if summary["layer_offsets_ms"]:
        lines += ["", "Kabarma katman kaymasi (ms): " + ", ".join(f"{k}: {v}" for k, v in summary["layer_offsets_ms"].items())]
    (out / "audio_qc.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"\n{summary['files']} dosya, {summary['passed']} gecti, {summary['fail']} KALDI, {summary['warn']} UYARI -> {out / 'audio_qc.md'}")

    if args.fix_dir:
        for f, r in zip(files, results):
            if needs_fix(r):
                print("duzeltildi:", fix(f, r, args.fix_dir))
    return 0 if (not fails or args.report_only) else 1


if __name__ == "__main__":
    sys.exit(main())
