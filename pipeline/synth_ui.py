"""Arayuz sesleri: Python standart kutuphanesiyle sentez, kredi yok (SANAT-SES §10.1: "Arayuz grubu Oduncu'daki
sentez betikleriyle de yapilabilir"). Yontem Oduncu'nun tools/sfx/gen_sfx.py betigiyle ayni (sinus, gurultu,
tek kutuplu suzgec, zarf); sesler Kabarma icin yeniden yazildi.

Cikti: game/assets/audio/sfx/<id>_<n>.mp3 (n = 1..3), 44,1 kHz tek kanal, tepe -3 dBFS, ffmpeg libmp3lame q2.
  python pipeline/synth_ui.py            # bes ses x 3 varyasyon
  python pipeline/synth_ui.py --only ui_onay
Ardindan Godot ice aktarma (--import) ve python pipeline/audio_qc.py.
"""
import argparse
import math
import os
import random
import shutil
import struct
import subprocess
import sys
import tempfile
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "assets" / "audio" / "sfx"
SR = 44100
TAU = 2 * math.pi


def n_of(sec):
    return int(SR * sec)


class LP:
    """Tek kutuplu alcak geciren."""

    def __init__(self, hz):
        self.a = 1 - math.exp(-TAU * hz / SR)
        self.y = 0.0

    def __call__(self, x):
        self.y += (x - self.y) * self.a
        return self.y


def band(hz_lo, hz_hi):
    lo, hi = LP(hz_lo), LP(hz_hi)
    return lambda x: hi(x) - lo(x)


def env(i, n, attack=0.002, decay=None):
    t = i / SR
    a = min(1.0, t / attack) if attack > 0 else 1.0
    d = math.exp(-t / decay) if decay else 1.0
    tail = min(1.0, (n - i) / max(1, n_of(0.004)))  # son 4 ms tiklamasin
    return a * d * tail


def bell(freq, sec, decay, partials=((1, 1.0), (2.0, 0.25), (3.01, 0.08)), detune=0.0):
    n = n_of(sec)
    out = [0.0] * n
    for k, (mult, amp) in enumerate(partials):
        f = freq * mult * (1 + detune * (k - 1))
        for i in range(n):
            out[i] += amp * math.sin(TAU * f * i / SR) * env(i, n, 0.003, decay / (1 + 0.6 * k))
    return out


def mix(*tracks, offsets=None):
    offsets = offsets or [0.0] * len(tracks)
    n = max(n_of(o) + len(t) for t, o in zip(tracks, offsets))
    out = [0.0] * n
    for t, o in zip(tracks, offsets):
        s = n_of(o)
        for i, x in enumerate(t):
            out[s + i] += x
    return out


def warm(samples, hz=5200):
    lp = LP(hz)
    return [lp(x) for x in samples]


# ---------- Sesler ----------

def ui_odak(v, rnd):
    """Odak: yumusak, kisa tahta-cam tik."""
    f = (1180, 1260, 1340)[v]
    tone = bell(f, 0.07, 0.018, ((1, 1.0), (2.76, 0.18)))
    bp = band(1500, 6000)
    click = [bp(rnd.uniform(-1, 1)) * env(i, n_of(0.012), 0.0005, 0.003) * 0.5 for i in range(n_of(0.012))]
    return warm(mix(tone, click))


def ui_onay(v, rnd):
    """Onay: iki notali yukselen, sicak zil (dorduncu, besinci, buyuk ucuncu)."""
    base = (587.3, 554.4, 622.3)[v]
    ratio = (4 / 3, 3 / 2, 5 / 4)[v]
    a = bell(base, 0.22, 0.09, detune=0.002)
    b = bell(base * ratio, 0.34, 0.14, detune=0.002)
    return warm(mix([x * 0.8 for x in a], b, offsets=[0.0, 0.085]))


def ui_hata(v, rnd):
    """Hata: alcak, kisa iki 'bonk' (inen), yumusatilmis kare dalga."""
    f = (196.0, 185.0, 207.7)[v]

    def bonk(freq, sec):
        n = n_of(sec)
        lp = LP(900)
        out = []
        for i in range(n):
            ph = (freq * i / SR) % 1.0
            sq = 1.0 if ph < 0.5 else -1.0
            out.append(lp(sq) * env(i, n, 0.002, 0.045))
        return out

    return warm(mix(bonk(f, 0.11), bonk(f * 0.84, 0.13), offsets=[0.0, 0.095]), 2400)


def ui_baslik_carki(v, rnd):
    """Baslik carki: mekanik cirt (cark disi) ve kisa ton."""
    clicks = (3, 2, 4)[v]
    parts, offs = [], []
    for k in range(clicks):
        n = n_of(0.016)
        bp = band(1200, 5200)
        parts.append([bp(rnd.uniform(-1, 1)) * env(i, n, 0.0004, 0.004) for i in range(n)])
        offs.append(k * 0.022)
    parts.append([x * 0.35 for x in bell((880, 940, 830)[v], 0.09, 0.03)])
    offs.append(0.0)
    return warm(mix(*parts, offsets=offs), 6500)


def telsiz_mandali(v, rnd):
    """Telsiz mandali (Suna'nin basi ve sonu): bas-konus tiki ve kisa hisirti kuyrugu."""
    n_click = n_of(0.010)
    bp_c = band(400, 3500)
    click = [bp_c(rnd.uniform(-1, 1)) * env(i, n_click, 0.0003, 0.0025) * 1.2 for i in range(n_click)]
    sec = (0.09, 0.07, 0.11)[v]
    n = n_of(sec)
    bp = band(900, 3200)
    hiss = [bp(rnd.uniform(-1, 1)) * env(i, n, 0.004, sec / 2.5) * 0.35 for i in range(n)]
    thump = [math.sin(TAU * 140 * i / SR) * env(i, n_of(0.03), 0.001, 0.008) * 0.4 for i in range(n_of(0.03))]
    return mix(click, thump, hiss, offsets=[0.0, 0.0, 0.006])


def parca_emilis(v, rnd):
    """Parca emilisi (Capa.Kazma: tas cantaya girer): tonsuz cakil tikirtisi, kucuk sekme ve bez canta tok sesi.
    Sahibin sikayeti (18 Kasim 2026): uretilen surumde surekli bir ton vardi, saniyede 2-3 kez calinca 'gaklama'
    gibi duyuluyordu. Burada surekli ton yok; yalniz gurultu tiklari ve cok kisa, alcak bir tok ses."""
    def tick(lo, hi, decay, amp):
        n = n_of(decay * 6)
        bp = band(lo, hi)
        return [bp(rnd.uniform(-1, 1)) * env(i, n, 0.0003, decay) * amp for i in range(n)]

    hit = tick((1800, 2200, 1600)[v], (6500, 7500, 6000)[v], 0.0025, 1.0)
    bounce = tick(2400, 7000, 0.0018, 0.45)
    n_th = n_of(0.05)
    lp = LP(220)
    thump = [lp(rnd.uniform(-1, 1)) * env(i, n_th, 0.002, 0.012) * 3.0 for i in range(n_th)]
    n_r = n_of(0.06)
    bp_r = band(700, 2600)
    rustle = [bp_r(rnd.uniform(-1, 1)) * env(i, n_r, 0.004, 0.018) * 0.3 for i in range(n_r)]
    return warm(mix(hit, bounce, thump, rustle, offsets=[0.0, (0.035, 0.028, 0.042)[v], 0.004, 0.006]), 7000)


SOUNDS = {"ui_odak": ui_odak, "ui_onay": ui_onay, "ui_hata": ui_hata, "ui_baslik_carki": ui_baslik_carki, "telsiz_mandali": telsiz_mandali,
          "parca_emilis": parca_emilis}


def write_wav(path, samples, peak_dbfs=-3.0):
    peak = max(1e-9, max(abs(s) for s in samples))
    gain = 10 ** (peak_dbfs / 20) / peak
    frames = bytearray()
    for s in samples:
        frames += struct.pack("<h", int(max(-1.0, min(1.0, s * gain)) * 32767))
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(bytes(frames))


def main():
    ap = argparse.ArgumentParser(description="Kabarma arayuz sesleri (sentez, kredi yok)")
    ap.add_argument("--only", nargs="*", help="yalniz bu kimlikler")
    ap.add_argument("--variants", type=int, default=3)
    args = ap.parse_args()
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg bulunamadi")
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for sid, fn in SOUNDS.items():
            if args.only and sid not in args.only:
                continue
            for v in range(args.variants):
                rnd = random.Random(f"{sid}-{v}")
                wav = Path(tmp) / f"{sid}_{v + 1}.wav"
                write_wav(wav, fn(v % 3, rnd))
                mp3 = OUT / f"{sid}_{v + 1}.mp3"
                subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(wav), "-c:a", "libmp3lame", "-q:a", "2", str(mp3)], check=True)
                print(mp3.relative_to(ROOT))


if __name__ == "__main__":
    main()
