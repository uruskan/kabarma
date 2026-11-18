"""Ikon paftasi: klasordeki <id>.png ikonlarini Rust envanter yuvasina benzer koyu kutularda, adlariyla dizer.

  python pipeline/icon_sheet.py <ikon_klasoru> <cikti.png> [--cols 10] [--cell 128]
"""
import sys, os, argparse
from PIL import Image, ImageDraw, ImageFont

ap = argparse.ArgumentParser()
ap.add_argument("src"); ap.add_argument("out")
ap.add_argument("--cols", type=int, default=10)
ap.add_argument("--cell", type=int, default=128)
a = ap.parse_args()

files = sorted(f for f in os.listdir(a.src) if f.endswith(".png") and not f.startswith("_"))
cell, gap, lab = a.cell, 6, 16
rows = (len(files) + a.cols - 1) // a.cols
W = a.cols * (cell + gap) + gap
H = rows * (cell + lab + gap) + gap
sheet = Image.new("RGBA", (W, H), (24, 23, 21, 255))
d = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype("arial.ttf", 11)
except OSError:
    font = ImageFont.load_default()
for i, f in enumerate(files):
    r, c = divmod(i, a.cols)
    x = gap + c * (cell + gap); y = gap + r * (cell + lab + gap)
    d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(58, 56, 52, 255), outline=(88, 85, 80, 255))
    im = Image.open(os.path.join(a.src, f)).convert("RGBA").resize((cell - 8, cell - 8), Image.LANCZOS)
    sheet.alpha_composite(im, (x + 4, y + 4))
    d.text((x + 2, y + cell + 2), f[:-4][:20], fill=(210, 205, 195, 255), font=font)
os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
sheet.convert("RGB").save(a.out)
print("SHEET", a.out, len(files))
