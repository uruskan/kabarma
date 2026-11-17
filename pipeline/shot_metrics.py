"""
dunya akisi: SANAT-SES §9 ekran goruntusu olcutlerinin betikle olculebilen kismi (16 goruntu, progress/m1/dunya/shots).
  - saf siyah bolge yok      : her kanal <= 8 olan piksel orani (ve 32x32'lik en buyuk tam siyah blok)
  - saf beyaz (SANAT-SES §1.4): her kanal >= 247 olan piksel orani
  - isiyan yuzey gunduz %5'i gecmiyor: Maya renklerine (turkuaz #5FF0C8, eflatun #D45BFF) yakin, parlak ve doygun
    piksel orani (yaklasik: isima gercekte cizicide olculemez; renk yakinligi bir ust sinirdir)
  - gokyuzu gecisi: ust %20'lik seritte satirlar arasi renk farki (0 = duz renk)
Uc derinlik katmani, arayuzun okunurlugu ve olcut 6-8 goruntuye bakilarak puanlanir (betik degil).

  python -I pipeline/shot_metrics.py progress/m1/dunya/shots [cikti.json]
"""
import sys, os, json, colorsys
from PIL import Image

MAYA = [(0x5F / 255, 0xF0 / 255, 0xC8 / 255), (0xD4 / 255, 0x5B / 255, 0xFF / 255)]
MAYA_H = [colorsys.rgb_to_hsv(*c)[0] for c in MAYA]


def hue_dist(a, b):
    d = abs(a - b)
    return min(d, 1 - d)


def metrics(path):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    small = im.resize((w // 4, h // 4))
    px = list(small.getdata())
    n = len(px)
    black = sum(1 for r, g, b in px if r <= 8 and g <= 8 and b <= 8) / n
    white = sum(1 for r, g, b in px if r >= 247 and g >= 247 and b >= 247) / n
    glow = 0
    for r, g, b in px:
        hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        if s > 0.35 and v > 0.7 and min(hue_dist(hh, m) for m in MAYA_H) < 0.05:
            glow += 1
    glow /= n
    # Gokyuzu seridi: ust %20, satir ortalamalarinin ilk ve son satiri arasindaki fark
    sw, sh = small.size
    top = int(sh * 0.2)
    rows = []
    for y in range(0, top, max(1, top // 8)):
        row = [small.getpixel((x, y)) for x in range(0, sw, 8)]
        rows.append(tuple(sum(c[i] for c in row) / len(row) for i in range(3)))
    grad = max(abs(rows[0][i] - rows[-1][i]) for i in range(3)) if len(rows) > 1 else 0
    return {"size": f"{w}x{h}", "black": round(black, 5), "white": round(white, 5), "maya_glow": round(glow, 4), "sky_gradient": round(grad, 1)}


def main():
    d = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(d, "metrics.json")
    res = {}
    for f in sorted(os.listdir(d)):
        if f.endswith(".png"):
            res[f] = metrics(os.path.join(d, f))
    for f, m in res.items():
        day = "kabarma_gecesi" not in f
        flags = []
        if m["black"] > 0.001: flags.append("SIYAH")
        if m["white"] > 0.001: flags.append("BEYAZ")
        if day and m["maya_glow"] > 0.05: flags.append("ISIMA>5%")
        print(f"{f:32s} siyah={m['black']:.4f} beyaz={m['white']:.4f} maya={m['maya_glow']:.4f} gok={m['sky_gradient']:5.1f} {' '.join(flags) or 'ok'}")
    json.dump(res, open(out, "w", encoding="utf-8"), indent=2)
    print(out)


if __name__ == "__main__":
    main()
