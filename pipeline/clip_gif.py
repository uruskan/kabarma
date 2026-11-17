"""
dunya akisi: --test=shots --shots_clip=YER_IMI ile alinan kareleri (progress/m1/dunya/shots/clip/) kucuk bir GIF'e cevirir
(PLAN §7.2 kosul 6 "kisa video"; ruzgar salinimi ve su hareketi).

  python -I pipeline/clip_gif.py progress/m1/dunya/shots/clip progress/m1/dunya/clip_gol.gif [genislik]
"""
import sys, os
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
width = int(sys.argv[3]) if len(sys.argv) > 3 else 640
files = sorted(f for f in os.listdir(src) if f.endswith(".png"))
frames = []
for f in files:
    im = Image.open(os.path.join(src, f)).convert("RGB")
    h = im.height * width // im.width
    frames.append(im.resize((width, h), Image.LANCZOS).quantize(colors=128, method=Image.Quantize.MEDIANCUT))
frames[0].save(out, save_all=True, append_images=frames[1:], duration=100, loop=0, optimize=True)
print(out, len(frames), os.path.getsize(out))
