"""ElevenLabs uretim betigi: Kabarma M1 ses paketi (URETIM §2.1, §2.2 H-J; SANAT-SES §10; M1 kararlari 8, 9).

KREDI HARCAR. Yalniz sahibin onayiyla ve onayli sinirlar icinde calistirilir. --dry-run ag kullanmaz, anahtar istemez.

Anahtar yalnizca ELEVENLABS_API_KEY ortam degiskeninden okunur. Bu betik .env dosyasini acmaz; anahtari hicbir
ciktiya, gunluge ya da hata iletisine yazmaz.

Sahibin onayladigi ust sinirlar (URETIM §6 "M1 kararlari", 17 Kasim 2026) asagida sabit ve kodda SERT SINIR:
asilacaksa betik durur. Harcamalar progress/m1/ses/eleven_ledger.json defterine yazilir; sinirlar onceki kosularla
birlikte (yeniden denemeler dahil) sayilir.

Kullanim (depo kokunden):
  python pipeline/eleven.py pipeline/requests/m1_audio.json --dry-run
  python pipeline/eleven.py pipeline/requests/m1_audio.json --only sfx [--ids siren,sisme] [--variants 3]
  python pipeline/eleven.py pipeline/requests/m1_audio.json --only music [--ids kabarma]
  python pipeline/eleven.py pipeline/requests/m1_audio.json --only stems          # Kabarma -> 3 katman
  python pipeline/eleven.py pipeline/requests/m1_audio.json --only design         # Suna: 3 aday ses
  python pipeline/eleven.py pipeline/requests/m1_audio.json --only save-voice --generated-id <aday kimligi>
  python pipeline/eleven.py pipeline/requests/m1_audio.json --only voice --lang tr [--voice-id <id>]
Var olan cikti dosyasi yeniden uretilmez (--force ile uretilir; o da sinira sayilir).
"""
import argparse
import array
import base64
import datetime
import difflib
import io
import json
import mimetypes
import os
import re
import shutil
import subprocess
import sys
import unicodedata
import urllib.error
import urllib.request
import uuid
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))

API = "https://api.elevenlabs.io"
KEY_ENV = "ELEVENLABS_API_KEY"
OUTPUT_FORMAT = "mp3_44100_128"

# ---------- Sabit model kimlikleri ----------
SFX_MODEL = "eleven_text_to_sound_v3"     # belge: URETIM §2.1 "sabitlenecek"
MUSIC_MODEL = "music_v2_5"                # belge: URETIM §2.1 "sabitlenecek"
TTS_MODEL = "eleven_v3"                   # belge: URETIM §2.1 "eleven_v4 ve eleven_v3"; v3 secildi (ses etiketli, Turkce)
VOICE_DESIGN_MODEL = "eleven_ttv_v3"      # belge susuyor: ElevenLabs API'nin v3 ses tasarim modeli
STT_MODEL = "scribe_v3"                   # belge susuyor: geri cevirme icin guncel yaziya cevirme modeli

# ---------- Sahibin onayladigi SERT sinirlar (URETIM §6 M1 kararlari) ----------
CAP_VOICE_CANDIDATES = 3            # Suna icin 3 aday ses (tek tasarim cagrisi)
CAP_DESIGN_CALLS = 1
CAP_VOICE_LINES_PER_LANG = 30       # 30 TR + 30 EN replik
CAP_LANGS = ("tr", "en")
CAP_TTS_CHARS = 7200                # replikler + yeniden denemeler + tasarim onizleme metni, en fazla 7.200 karakter
CAP_SFX_SOUNDS = 60                 # 60 ses
CAP_SFX_VARIANTS = (3, 4)           # ses basina 3-4 surum
CAP_SFX_SECONDS = 19 * 60           # en fazla ~19 dk (denemeler dahil)
CAP_MUSIC_TRACKS = 2                # cayir gunduzu ~3 dk, 3 katmanli Kabarma ~2 dk
CAP_MUSIC_SECONDS = 12.5 * 60       # denemelerle en fazla ~12,5 dk; katman ayristirma da buraya sayilir (fiyati yazmiyor)
CAP_STT_SECONDS = 15 * 60           # geri cevirme: replik suresinin rahat ustu (0,20 $/saat)

# Fiyatlar (USD, elevenlabs.io/pricing/api, 17 Kasim 2026 okundu): yalniz tahmin icin.
PRICE = {"tts_chars": 0.08 / 1000, "sfx_seconds": 0.12 / 60, "music_seconds": 0.15 / 60, "stt_seconds": 0.20 / 3600}

LEDGER = ROOT / "progress" / "m1" / "ses" / "eleven_ledger.json"
RAW = ROOT / ".tools" / "eleven_raw"   # git disi: islenmemis indirmeler

# Seslendirilen replikte tus adi olmaz (SANAT-SES §10.3, GDD §24).
KEY_NAME = re.compile(r"\(\s*\d\s*\)|\b[A-Z]'(?:ye|ya|e|a|yi|i)\b|\bTab\b|\bShift\b|\bEsc\b|\btu[sş]\b|\bpress\b|\bkey\b", re.IGNORECASE)


class CapError(Exception):
    pass


# ---------- Defter ----------

def load_ledger():
    if LEDGER.exists():
        return json.loads(LEDGER.read_text(encoding="utf-8"))
    return {"_not": "eleven.py harcama defteri. Sinirlar bu kayitlarla birlikte sayilir. Anahtar burada yok.", "entries": [], "state": {}}


def save_ledger(led):
    LEDGER.parent.mkdir(parents=True, exist_ok=True)
    LEDGER.write_text(json.dumps(led, ensure_ascii=False, indent=1), encoding="utf-8")


def used(led, kind):
    return sum(e["amount"] for e in led["entries"] if e["kind"] == kind and e.get("billed", True))


CAPS = {"tts_chars": CAP_TTS_CHARS, "sfx_seconds": CAP_SFX_SECONDS, "music_seconds": CAP_MUSIC_SECONDS, "stt_seconds": CAP_STT_SECONDS}


def reserve(led, kind, amount, label):
    cap = CAPS[kind]
    have = used(led, kind)
    if have + amount > cap + 1e-6:
        raise CapError(f"SINIR: {kind} {have:.1f} + {amount:.1f} > {cap} ({label}). Sahibe yeniden sorulmali.")
    e = {"kind": kind, "amount": round(amount, 3), "label": label, "at": datetime.datetime.now().isoformat(timespec="seconds"), "billed": True}
    led["entries"].append(e)
    save_ledger(led)
    return e


# ---------- HTTP (anahtar hicbir yere yazilmaz) ----------

def api_key():
    k = os.environ.get(KEY_ENV, "").strip()
    if not k:
        sys.exit(f"{KEY_ENV} ortam degiskeni yok. Bu betik .env okumaz; anahtari ortama sahip/ana oturum verir.")
    return k


def http(method, path, key, body=None, files=None, query=None, expect="bytes"):
    url = API + path
    if query:
        url += "?" + "&".join(f"{k}={v}" for k, v in query.items())
    headers = {"xi-api-key": key, "Accept": "*/*"}
    data = None
    if files is not None:
        boundary = uuid.uuid4().hex
        buf = io.BytesIO()
        for name, value in (body or {}).items():
            buf.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode())
        for name, (fname, content) in files.items():
            ctype = mimetypes.guess_type(fname)[0] or "application/octet-stream"
            buf.write(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"; filename=\"{fname}\"\r\nContent-Type: {ctype}\r\n\r\n".encode())
            buf.write(content)
            buf.write(b"\r\n")
        buf.write(f"--{boundary}--\r\n".encode())
        data = buf.getvalue()
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    elif body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            raw = r.read()
    except urllib.error.HTTPError as e:
        detail = e.read()[:600].decode("utf-8", "replace")
        raise RuntimeError(f"HTTP {e.code} {path}: {detail}") from None
    except urllib.error.URLError as e:
        raise RuntimeError(f"Ag hatasi {path}: {e.reason}") from None
    return json.loads(raw) if expect == "json" else raw


# ---------- Istek dosyasi ve replikler ----------

def load_request(path):
    req = json.loads(Path(path).read_text(encoding="utf-8"))
    req["_dir"] = str(Path(path).resolve().parent)
    return req


def rel(p):
    return (ROOT / p) if not Path(p).is_absolute() else Path(p)


def load_audio_data(req):
    return json.loads(rel(req["audioData"]).read_text(encoding="utf-8"))


def shown(p):
    p = Path(p)
    try:
        return p.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(p)


def load_lines(req, lang):
    """
    Suna replikleri (kimlik, metin); oyundaki VoiceLines.FromJson ile ayni kural:
    - ilkgun yapisi: "lines": {kimlik: {"tr": ..., "en": ...}}. Kimlik acik, iki dil ayni dosyada. Bir replikte o
      dilin metni bossa uyari doner (voice_plan bunu engel sayar: 30 + 30 replik eksiksiz olmali).
    - eski yapi ("lines" yok): kimlik turetilir (intro_N, {adim}_start, {adim}_done, night_{anahtar}, next_order);
      yalniz Turkce metin vardir.
    Donus: ([(kimlik, metin)], uyari ya da None).
    """
    path = rel(req["voice"]["lines"])
    if not path.exists():
        return [], f"{shown(path)} yok"
    sc = json.loads(path.read_text(encoding="utf-8"))
    lines = sc.get("lines")
    if isinstance(lines, dict):
        out, empty = [], []
        for i, v in lines.items():
            if i.startswith("_"):
                continue  # aciklama anahtari
            t = v.get(lang) if isinstance(v, dict) else (v if lang == "tr" else None)
            if isinstance(t, str) and t.strip():
                out.append((i, t.strip()))
            else:
                empty.append(i)
        return out, (f"{lang}: metni bos replik: {', '.join(empty)}" if empty else None)
    if lang != "tr":
        return [], f"{shown(path)}: eski yapi ('lines' yok), {lang} metni yok"
    out = []
    for i, t in enumerate(sc.get("intro", [])):
        out.append((f"intro_{i + 1}", t))
    for s in sc.get("steps", []):
        for k in ("start", "done"):
            if s.get(k):
                out.append((f"{s['id']}_{k}", s[k]))
    for k, v in sc.get("nightLines", {}).items():
        out.append((f"night_{k}", v))
    if sc.get("nextOrder"):
        out.append(("next_order", sc["nextOrder"]))
    return [(i, t.strip()) for i, t in out if t and t.strip()], None


def out_path(req, kind, **kw):
    pat = req["outputs"][kind]
    for k, v in kw.items():
        pat = pat.replace("{" + k + "}", str(v))
    return rel(pat)


# ---------- Plan (kuru kosu ve gercek kosu ayni plani kullanir) ----------

def sfx_plan(req, audio, ids, variants):
    known = {s["id"]: s for s in audio["sounds"]}
    plan, problems = [], []
    sounds = req["sfx"]["sounds"]
    if len(sounds) > CAP_SFX_SOUNDS:
        problems.append(f"istekte {len(sounds)} ses > {CAP_SFX_SOUNDS}")
    for s in sounds:
        if s["id"] not in known:
            problems.append(f"{s['id']}: audio.json'da yok")
            continue
        if not s.get("enabled", True) or (ids and s["id"] not in ids):
            continue
        n = variants or req["sfx"].get("variants", 3)
        if not CAP_SFX_VARIANTS[0] <= n <= CAP_SFX_VARIANTS[1]:
            problems.append(f"{s['id']}: {n} surum, sinir {CAP_SFX_VARIANTS}")
            continue
        sec = float(s["seconds"])
        if not 0.5 <= sec <= 30:
            problems.append(f"{s['id']}: sure {sec} (API 0,5-30 sn)")
        if bool(s.get("loop")) != bool(known[s["id"]]["loop"]):
            problems.append(f"{s['id']}: dongu bilgisi audio.json ile ayni degil")
        prompts = s.get("variantPrompts") or [s["prompt"]]
        for v in range(1, n + 1):
            text = prompts[(v - 1) % len(prompts)] + " " + req["sfx"].get("style", "")
            plan.append({"id": s["id"], "n": v, "seconds": sec, "loop": bool(s.get("loop")), "text": text.strip(),
                         "out": out_path(req, "sfx", id=s["id"], n=v)})
    missing = [i for i in known if i not in {s["id"] for s in sounds}]
    if missing:
        problems.append(f"istekte olmayan sesler: {missing}")
    return plan, problems


def music_plan(req, ids):
    plan, problems = [], []
    tracks = req["music"]["tracks"]
    if len(tracks) > CAP_MUSIC_TRACKS:
        problems.append(f"{len(tracks)} parca > {CAP_MUSIC_TRACKS}")
    for t in tracks:
        if ids and t["id"] not in ids:
            continue
        plan.append({**t, "out": out_path(req, "music", file=t["file"])})
    return plan, problems


def voice_plan(req, lang):
    """(plan, sorunlar, engel): engel varsa uretim yapilmaz (metin yok ya da replik sayisi sinirin ustunde)."""
    lines, warn = load_lines(req, lang)
    problems = [warn] if warn else []
    blocked = bool(warn) or not lines
    if len(lines) > CAP_VOICE_LINES_PER_LANG:
        problems.append(f"{lang}: {len(lines)} replik > {CAP_VOICE_LINES_PER_LANG} (M1: 30 replik; ilkgun akisinin 30 replikli metni bekleniyor)")
        blocked = True
    keyed = [i for i, t in lines if KEY_NAME.search(t)]
    if keyed:
        problems.append(f"{lang}: tus adi iceren {len(keyed)} replik seslendirilmez (SANAT-SES §10.3): {', '.join(keyed)}")
    plan = [{"id": i, "text": t, "chars": len(t), "out": out_path(req, "voice", lang=lang, line=i), "keyed": i in keyed} for i, t in lines]
    return plan, problems, blocked


# ---------- Kuru kosu ----------

def dry_run(req, args):
    led = load_ledger()
    audio = load_audio_data(req)
    print("ElevenLabs M1 ses paketi: KURU KOSU (ag yok, anahtar okunmadi)\n")
    print(f"Modeller: ses efekti {SFX_MODEL} · muzik {MUSIC_MODEL} · konusma {TTS_MODEL} · ses tasarimi {VOICE_DESIGN_MODEL} · geri cevirme {STT_MODEL}")
    print(f"Defter: {LEDGER.relative_to(ROOT)} · onceki harcama: " + ", ".join(f"{k} {used(led, k):.0f}/{CAPS[k]:.0f}" for k in CAPS) + "\n")
    totals = {k: 0.0 for k in CAPS}

    plan, problems = sfx_plan(req, audio, set(args.ids or []), args.variants)
    sec = sum(p["seconds"] for p in plan)
    totals["sfx_seconds"] += sec
    sounds = len({p["id"] for p in plan})
    skipped = [s["id"] for s in req["sfx"]["sounds"] if not s.get("enabled", True)]
    print(f"[ses efekti] {sounds} ses, {len(plan)} uretim, {sec:.1f} sn ({sec / 60:.2f} dk) / sinir {CAP_SFX_SECONDS / 60:.0f} dk")
    if skipped:
        print(f"  kapali (sentezle uretildi, pipeline/synth_ui.py): {', '.join(skipped)}")
    print(f"  cikti: {req['outputs']['sfx']}")
    for p in problems:
        print("  ! " + p)

    mplan, mprob = music_plan(req, set(args.ids or []))
    msec = sum(t["seconds"] for t in mplan)
    stems = sum(t["seconds"] for t in mplan if t.get("stems"))
    totals["music_seconds"] += msec + stems
    print(f"\n[muzik] {len(mplan)} parca, {msec:.0f} sn + katman ayristirma {stems:.0f} sn = {(msec + stems) / 60:.2f} dk / sinir {CAP_MUSIC_SECONDS / 60:.1f} dk")
    for t in mplan:
        lay = f" -> katmanlar {', '.join(t['layers'])}" if t.get("stems") else ""
        print(f"  {t['id']}: {t['seconds']} sn -> {t['out'].relative_to(ROOT)}{lay}")
    print("  ! katman ayristirmanin fiyati fiyat sayfasinda yok: tutumlu olsun diye muzik dakikasina sayildi")
    for p in mprob:
        print("  ! " + p)

    design = req["voice"]["design"]
    dchars = len(design["previewText"])
    totals["tts_chars"] += dchars
    print(f"\n[Suna ses tasarimi] {CAP_DESIGN_CALLS} cagri, {CAP_VOICE_CANDIDATES} aday, onizleme metni {dchars} karakter (karaktere sayildi)")
    print(f"  cikti: {req['outputs']['voiceCandidates']}")
    if not 100 <= dchars <= 1000:
        print("  ! onizleme metni 100-1000 karakter olmali (API)")

    for lang in CAP_LANGS:
        vplan, vprob, blocked = voice_plan(req, lang)
        ok = [v for v in vplan if not v["keyed"]]
        chars = sum(v["chars"] for v in ok)
        totals["tts_chars"] += chars
        stt = sum(estimate_speech_seconds(v["text"]) for v in ok)
        totals["stt_seconds"] += stt
        print(f"\n[Suna {lang}] {len(vplan)} replik, seslendirilebilir {len(ok)}, {chars} karakter; geri cevirme ~{stt:.0f} sn")
        print(f"  cikti: {req['outputs']['voice'].replace('{lang}', lang)}")
        for p in vprob:
            print("  ! " + p)
        if blocked:
            print(f"  ! ENGEL: {lang} seslendirmesi bu metinle baslatilamaz")
    retry = req["voice"].get("maxRetriesPerLine", 1)
    print(f"\n  yeniden deneme payi: replik basina en fazla {retry} (geri cevirme tutmazsa); karakter sinirina sayilir")

    print("\nTOPLAM (bu plan):")
    usd = 0.0
    for k in CAPS:
        unit = "karakter" if k == "tts_chars" else "sn"
        cost = totals[k] * PRICE[k]
        usd += cost
        flag = "  ASIYOR" if totals[k] + used(led, k) > CAPS[k] else ""
        print(f"  {k:14} {totals[k]:9.1f} {unit:8} (defterde {used(led, k):.1f}) / sinir {CAPS[k]:.0f}{flag}  ~{cost:.2f} $")
    print(f"  tahmini tutar ~{usd:.2f} $ (liste fiyati; ses tasarimi ve katman ayristirma fiyati yazmiyor)")
    return 0


def estimate_speech_seconds(text):
    return max(1.0, len(text) / 14.0)  # sakin konusma ~14 karakter/sn


# ---------- Gercek kosu ----------

def post_process(path, loop):
    """
    Bas/son sessizlik ve tepe: audio_qc ile olc; yalniz KALDI varsa kirp ve -1,5 dBTP'ye indir (dosyanin yerinde).
    Duzeltme basarisizsa indirilen dosya oldugu gibi kalir (para harcandi, dosya kaybolmasin).
    """
    import audio_qc
    data = json.loads((ROOT / "game" / "data" / "audio.json").read_text(encoding="utf-8"))
    r, _ = audio_qc.check_file(path, data, [])
    if not audio_qc.needs_fix(r):
        return r
    # Dongu (ve Kabarma katmani) asla kirpilmaz: yalniz tepe indirilir. Yol siniflamasina birakilmaz, cagiran bilir.
    r["loop"] = bool(r.get("loop")) or bool(loop)
    fixed = audio_qc.fix(path, r, RAW / "fixed")
    if fixed is None:
        return r
    shutil.move(str(fixed), str(path))
    r2, _ = audio_qc.check_file(path, data, [])
    return r2


def save_bytes(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def run_sfx(req, args, key, led):
    audio = load_audio_data(req)
    plan, problems = sfx_plan(req, audio, set(args.ids or []), args.variants)
    if problems:
        sys.exit("Plan sorunlu, uretim yok:\n  " + "\n  ".join(problems))
    for p in plan:
        if p["out"].exists() and not args.force:
            print(f"var, atlandi: {p['out'].relative_to(ROOT)}")
            continue
        e = reserve(led, "sfx_seconds", p["seconds"], f"sfx {p['id']}_{p['n']}")
        body = {"text": p["text"], "duration_seconds": p["seconds"], "prompt_influence": req["sfx"].get("promptInfluence", 0.4), "model_id": SFX_MODEL}
        if p["loop"]:
            body["loop"] = True
        try:
            audio_bytes = http("POST", "/v1/sound-generation", key, body=body, query={"output_format": OUTPUT_FORMAT})
        except RuntimeError as err:
            e["billed"] = False
            save_ledger(led)
            sys.exit(f"durdu: {err}")
        raw = RAW / "sfx" / p["out"].name
        save_bytes(raw, audio_bytes)
        save_bytes(p["out"], audio_bytes)
        r = post_process(p["out"], p["loop"])
        e["out"] = str(p["out"].relative_to(ROOT)).replace("\\", "/")
        e["qc"] = [f"{c['level']} {c['check']}" for c in r["checks"]]
        save_ledger(led)
        print(f"ses {p['id']}_{p['n']}: {r['duration_s']:.2f} sn  {'; '.join(e['qc']) or 'denetim gecti'}")


def run_music(req, args, key, led):
    plan, problems = music_plan(req, set(args.ids or []))
    if problems:
        sys.exit("Plan sorunlu:\n  " + "\n  ".join(problems))
    for t in plan:
        if t["out"].exists() and not args.force:
            print(f"var, atlandi: {t['out'].relative_to(ROOT)}")
            continue
        e = reserve(led, "music_seconds", t["seconds"], f"muzik {t['id']}")
        body = {"prompt": t["prompt"], "music_length_ms": int(t["seconds"] * 1000), "model_id": MUSIC_MODEL, "force_instrumental": True}
        try:
            data = http("POST", "/v1/music", key, body=body, query={"output_format": OUTPUT_FORMAT})
        except RuntimeError as err:
            e["billed"] = False
            save_ledger(led)
            sys.exit(f"durdu: {err}")
        save_bytes(t["out"], data)
        e["out"] = str(t["out"].relative_to(ROOT)).replace("\\", "/")
        save_ledger(led)
        print(f"muzik {t['id']} -> {e['out']}")


SILENT_STEM_DBTP = -60.0  # bu tepenin altindaki kok bos sayilir (enstrumantal parcada vocals gibi)


def assign_stems(stems, layers):
    """
    Kokleri katmanlara dagitir: {katman: [kok]}, bos kokler ve atlananlar. Bos kok (tepesi SILENT_STEM_DBTP altinda)
    hicbir katmana girmez. Anahtar sozcuge uymayan kok son katmana (en yogun) gider. Sesli koku olmayan katman varsa
    hata (dosya yazilmadan once).
    """
    import audio_qc
    names = list(layers)
    assign, silent = {n: [] for n in names}, []
    for s in stems:
        tp = audio_qc.ebur128(s)["true_peak_dbtp"]
        if tp is None or tp < SILENT_STEM_DBTP:
            silent.append(s)
            continue
        low = s.stem.lower()
        target = next((n for n in names if any(w in low for w in layers[n])), names[-1])
        assign[target].append(s)
    empty = [n for n, v in assign.items() if not v]
    if empty:
        raise RuntimeError(f"sesli koku olmayan katman: {', '.join(empty)} (bos kokler: {', '.join(s.name for s in silent) or '-'}); "
                           "eslemeyi istek dosyasinda duzelt")
    return assign, silent


STEM_LEAD_MAX_S = 0.1     # kokun tam parcadan bu kadarindan fazla uzun olmasi beklenmez
STEM_LEAD_SILENT = 64     # basta kirpilacak fazlalikta en buyuk ornek (~-54 dBFS); ustuyse kirpilmaz, durulur


def stem_lead(stems, full):
    """
    Ayristirma servisi kokleri tam parcadan bir mp3 karesi (1152 ornek) uzun ve o kadar gec veriyor: kodlayici
    gecikmesi koklerin basina sessizlik olarak yazilmis (18 Kasim: 5.294.592 / 5.293.440 ornek, ilk 1152 ornek
    -80 dBFS alti). Kirpilmazsa her dongude 25 ms bosluk olur. Donus: bastan kirpilacak ornek sayisi.
    """
    import audio_qc
    sr, n_full = audio_qc.native(full)
    counts = {s: audio_qc.native(s)[1] for s in stems}
    if len(set(counts.values())) > 1:
        raise RuntimeError(f"kok boylari esit degil: { {s.name: n for s, n in counts.items()} }")
    diff = next(iter(counts.values())) - n_full
    if diff <= 0:
        return 0
    if diff > STEM_LEAD_MAX_S * sr:
        raise RuntimeError(f"kokler tam parcadan {diff} ornek uzun (> {STEM_LEAD_MAX_S} sn); elle bak")
    for s in stems:
        r = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", str(s), "-ac", "1", "-f", "s16le",
                            "-t", f"{(diff + 64) / sr:.4f}", "-"], capture_output=True)
        a = array.array("h")
        a.frombytes(r.stdout[: len(r.stdout) // 2 * 2])
        peak = max((abs(v) for v in a[:diff]), default=0)
        if peak > STEM_LEAD_SILENT:
            raise RuntimeError(f"{s.name}: bastaki {diff} ornek sessiz degil (en buyuk {peak}); kirpilmadi")
    return diff


def mix_layers(assign, outs, trim=0):
    """
    Her katmanin koklerini toplayip mp3 yazar. trim: her kokun basindan atilacak ornek (stem_lead). Tepe -1 dBTP
    ustundeyse post_process indirir (katman kirpilmaz, boy degismez).
    """
    for name, files in assign.items():
        cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
        for f in files:
            cmd += ["-i", str(f)]
        cut = "".join(f"[{i}:a]atrim=start_sample={trim},asetpts=PTS-STARTPTS[s{i}];" for i in range(len(files)))
        tail = (f"{''.join(f'[s{i}]' for i in range(len(files)))}amix=inputs={len(files)}:normalize=0:duration=longest[o]"
                if len(files) > 1 else "[s0]anull[o]")
        cmd += ["-filter_complex", cut + tail, "-map", "[o]", "-c:a", "libmp3lame", "-q:a", "2", str(outs[name])]
        subprocess.run(cmd, check=True)
        post_process(outs[name], loop=True)
        print(f"katman {name}: {', '.join(f.name for f in files)}")


def run_stems(req, args, key, led):
    """
    Kabarma tam parcasini ayristirir; kokleri istek dosyasindaki anahtar sozcuklerle 3 katmana toplar.
    --reuse-raw: daha once indirilen kokler (.tools/eleven_raw/stems/<id>) varsa API'ye gidilmez, kredi harcanmaz;
    yalniz katmanlar yeniden toplanir (eslemeyi duzeltmek icin).
    """
    plan, _ = music_plan(req, set(args.ids or []))
    for t in [t for t in plan if t.get("stems")]:
        if not t["out"].exists():
            sys.exit(f"once parca uretilmeli: {t['out'].relative_to(ROOT)}")
        layer_out = {name: out_path(req, "music", file=name) for name in t["layers"]}
        if all(p.exists() for p in layer_out.values()) and not args.force:
            print("katmanlar var, atlandi (yeniden toplamak icin --force)")
            continue
        stem_dir = RAW / "stems" / t["id"]
        e = None
        if args.reuse_raw and stem_dir.exists() and any(stem_dir.iterdir()):
            print(f"kokler yeniden kullaniliyor (API yok, kredi yok): {shown(stem_dir)}")
        else:
            if args.reuse_raw:
                sys.exit(f"--reuse-raw: {shown(stem_dir)} yok ya da bos")
            e = reserve(led, "music_seconds", t["seconds"], f"katman ayristirma {t['id']}")
            try:
                z = http("POST", "/v1/music/stem-separation", key, files={"file": (t["out"].name, t["out"].read_bytes())}, body={}, query={"output_format": OUTPUT_FORMAT})
            except RuntimeError as err:
                e["billed"] = False
                save_ledger(led)
                sys.exit(f"durdu: {err}")
            if stem_dir.exists():
                shutil.rmtree(stem_dir)
            stem_dir.mkdir(parents=True)
            with zipfile.ZipFile(io.BytesIO(z)) as zf:
                for m in zf.infolist():
                    name = Path(m.filename).name
                    if name and not m.is_dir():
                        (stem_dir / name).write_bytes(zf.read(m))
        stems = sorted(stem_dir.glob("*"))
        print("kokler:", ", ".join(s.name for s in stems))
        assign, silent = assign_stems(stems, t["layers"])
        if silent:
            print("bos kokler (katmana girmedi):", ", ".join(s.name for s in silent))
        trim = stem_lead([s for v in assign.values() for s in v], t["out"])
        if trim:
            print(f"koklerin basindaki {trim} ornek sessiz kodlayici gecikmesi: kirpiliyor (tam parcayla hizali)")
        if e is not None:
            e["stems"] = {k: [s.name for s in v] for k, v in assign.items()}
            save_ledger(led)
        mix_layers(assign, layer_out, trim)


def run_design(req, args, key, led):
    state = led.setdefault("state", {})
    d = req["voice"]["design"]
    if state.get("designCalls", 0) >= CAP_DESIGN_CALLS:
        sys.exit(f"SINIR: ses tasarimi en fazla {CAP_DESIGN_CALLS} cagri ({CAP_VOICE_CANDIDATES} aday); adaylar {req['outputs']['voiceCandidates']}. Yeniden sorulmali.")
    e = reserve(led, "tts_chars", len(d["previewText"]), "Suna ses tasarimi")
    body = {"voice_description": d["description"], "model_id": VOICE_DESIGN_MODEL, "text": d["previewText"]}
    try:
        res = http("POST", "/v1/text-to-voice/design", key, body=body, query={"output_format": OUTPUT_FORMAT}, expect="json")
    except RuntimeError as err:
        e["billed"] = False
        save_ledger(led)
        sys.exit(f"durdu: {err}")
    state["designCalls"] = state.get("designCalls", 0) + 1
    cands = []
    for i, p in enumerate(res.get("previews", [])[:CAP_VOICE_CANDIDATES], 1):
        path = out_path(req, "voiceCandidates", n=i)
        save_bytes(path, base64.b64decode(p["audio_base_64"]))
        cands.append({"n": i, "generated_voice_id": p["generated_voice_id"], "file": str(path.relative_to(ROOT)).replace("\\", "/"), "seconds": p.get("duration_secs")})
    state["candidates"] = cands
    save_ledger(led)
    for c in cands:
        print(f"aday {c['n']}: {c['file']}  (generated_voice_id {c['generated_voice_id']})")
    print("Sahip dinleyip birini secer; sonra: --only save-voice --generated-id <kimlik>")


def run_save_voice(req, args, key, led):
    if not args.generated_id:
        sys.exit("--generated-id gerekli (adaylardan biri)")
    d = req["voice"]["design"]
    res = http("POST", "/v1/text-to-voice", key, body={"voice_name": d.get("name", "Suna (Kabarma)"), "voice_description": d["description"],
                                                         "generated_voice_id": args.generated_id}, expect="json")
    led.setdefault("state", {})["voiceId"] = res["voice_id"]
    save_ledger(led)
    print(f"Suna sesi kaydedildi: voice_id {res['voice_id']} (deftere yazildi; istek dosyasinda voice.voiceId olarak da verilebilir)")


def normalize(text):
    t = unicodedata.normalize("NFKC", text).replace("İ", "i").replace("I", "ı").lower()
    t = re.sub(r"[^\w\s]", " ", t)
    return " ".join(t.split())


def run_voice(req, args, key, led):
    lang = args.lang
    if lang not in CAP_LANGS:
        sys.exit(f"--lang {CAP_LANGS} olmali")
    voice_id = args.voice_id or req["voice"].get("voiceId") or led.get("state", {}).get("voiceId")
    if not voice_id:
        sys.exit("Suna'nin ses kimligi yok: once design ve save-voice (sahibin secimiyle)")
    plan, problems, blocked = voice_plan(req, lang)
    if blocked:
        sys.exit("Plan sorunlu, uretim yok:\n  " + "\n  ".join(problems))
    settings = req["voice"].get("settings", {})
    retries = int(req["voice"].get("maxRetriesPerLine", 1))
    report = []
    for v in plan:
        if v["keyed"]:
            report.append({"id": v["id"], "status": "atlandi: tus adi"})
            continue
        if v["out"].exists() and not args.force:
            print(f"var, atlandi: {v['out'].relative_to(ROOT)}")
            continue
        for attempt in range(retries + 1):
            e = reserve(led, "tts_chars", v["chars"], f"Suna {lang} {v['id']} #{attempt + 1}")
            body = {"text": v["text"], "model_id": TTS_MODEL, "language_code": lang, "voice_settings": settings}
            try:
                data = http("POST", f"/v1/text-to-speech/{voice_id}", key, body=body, query={"output_format": OUTPUT_FORMAT})
            except RuntimeError as err:
                e["billed"] = False
                save_ledger(led)
                sys.exit(f"durdu: {err}")
            save_bytes(v["out"], data)
            e["out"] = str(v["out"].relative_to(ROOT)).replace("\\", "/")
            save_ledger(led)
            if not req["voice"].get("backTranscription", True):
                break
            sec = estimate_speech_seconds(v["text"])
            reserve(led, "stt_seconds", sec, f"geri cevirme {lang} {v['id']}")
            stt = http("POST", "/v1/speech-to-text", key, body={"model_id": STT_MODEL, "language_code": lang},
                       files={"file": (v["out"].name, data)}, expect="json")
            heard = stt.get("text", "")
            ratio = difflib.SequenceMatcher(None, normalize(v["text"]), normalize(heard)).ratio()
            e["backTranscription"] = {"heard": heard, "ratio": round(ratio, 3)}
            save_ledger(led)
            if ratio >= req["voice"].get("minMatch", 0.9):
                report.append({"id": v["id"], "status": "gecti", "ratio": round(ratio, 3)})
                break
            print(f"  geri cevirme tutmadi ({ratio:.2f}): '{heard}'")
        else:
            report.append({"id": v["id"], "status": "KALDI geri cevirme", "heard": heard})
        print(f"Suna {lang} {v['id']}: {report[-1]['status'] if report else ''}")
    out = ROOT / "progress" / "m1" / "ses" / f"suna_{lang}_geri_cevirme.json"
    out.write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"rapor: {out.relative_to(ROOT)}")


def main():
    ap = argparse.ArgumentParser(description="Kabarma M1 ses paketi: ElevenLabs uretimi (kredi harcar; --dry-run harcamaz)")
    ap.add_argument("request", help="istek dosyasi (JSON), ornek pipeline/requests/m1_audio.json")
    ap.add_argument("--dry-run", action="store_true", help="karakter ve saniye toplamlarini yaz; ag yok, anahtar okunmaz")
    ap.add_argument("--only", choices=["sfx", "music", "stems", "design", "save-voice", "voice"])
    ap.add_argument("--ids", type=lambda s: [x.strip() for x in s.split(",") if x.strip()], help="yalniz bu ses/parca kimlikleri")
    ap.add_argument("--variants", type=int, help=f"ses basina surum ({CAP_SFX_VARIANTS[0]}-{CAP_SFX_VARIANTS[1]})")
    ap.add_argument("--lang", default="tr")
    ap.add_argument("--voice-id")
    ap.add_argument("--generated-id")
    ap.add_argument("--force", action="store_true", help="var olan ciktiyi yeniden uret (sinira sayilir)")
    ap.add_argument("--reuse-raw", action="store_true", help="stems: indirilmis kokleri kullan, API'ye gitme (kredisiz, anahtar istemez)")
    args = ap.parse_args()
    req = load_request(args.request)
    if args.dry_run:
        return dry_run(req, args)
    if not args.only:
        sys.exit("--only ile bir adim sec (ya da --dry-run). Toplu kosu yok: her adim sahibin onayiyla ayri calisir.")
    if args.reuse_raw and args.only != "stems":
        sys.exit("--reuse-raw yalniz --only stems ile")
    key = None if args.reuse_raw else api_key()
    led = load_ledger()
    try:
        {"sfx": run_sfx, "music": run_music, "stems": run_stems, "design": run_design, "save-voice": run_save_voice, "voice": run_voice}[args.only](req, args, key, led)
    except CapError as e:
        sys.exit(str(e))
    except RuntimeError as e:  # API ya da ag hatasi: defter kaydedildi, kosu durur (anahtar iletide yok)
        sys.exit(f"durdu: {e}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
