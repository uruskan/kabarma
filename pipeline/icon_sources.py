"""game/assets/icons/items/SOURCES.md uretir: her ikonun yolu (model render ya da agy), kaynagi ve istemi.

  python pipeline/icon_sources.py
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
D = os.path.join(ROOT, "game", "assets", "icons", "items")
models = {e["id"]: e for e in json.load(open(os.path.join(ROOT, "pipeline", "icons_models.json"), encoding="utf-8-sig"))}
agy = json.load(open(os.path.join(ROOT, "pipeline", "icons_agy.json"), encoding="utf-8-sig"))
agy_items = {e["id"]: e for e in agy["items"]}
route_file = os.path.join(D, "routes.json")  # kimlik -> "model" | "agy" (ikisi de varsa hangisi secildi)
routes = json.load(open(route_file, encoding="utf-8")) if os.path.exists(route_file) else {}
icons = sorted(f[:-4] for f in os.listdir(D) if f.endswith(".png") and not f.startswith("_"))

L = ["# Esya ikonlari: kaynaklar ve istemler", "",
     "SANAT-SES 8.1. Her ikon 256 px, seffaf PNG, `res://assets/icons/items/<kimlik>.png`; koddan `ItemIcons.Get(id)` / `ItemIcons.Find(id)`.",
     "Eksik kimlikte `_placeholder.png` (koddan uretilmis yer tutucu).", "",
     "## Yollar", "",
     "- **model**: oyunun kendi 3B modeli, `pipeline/render_icons.py` (Blender 5.3 Cycles, 96 ornek + denoise; sicak anahtar, soguk dolgu, kenar isigi;",
     "  3/4 onden, 28 derece yukaridan, 40 mm; golge yakalayici zeminde hafif temas golgesi; 512 px cizilip 256'ya indirilir). Liste: `pipeline/icons_models.json`.",
     "- **agy**: Google Antigravity CLI (`agy -p`, `image-generator` alt ajani, `generate_image`), `pipeline/gen_icons_agy.ps1`; macenta arka plan",
     "  `pipeline/icon_post.py` ile anahtarlanir, kirpilir, %84 dolulukla ortalanir, temas golgesi eklenir, 256 px. Istemler: `pipeline/icons_agy.json`.",
     "- Pafta: `python pipeline/icon_sheet.py game/assets/icons/items progress/gorsel/ikon/pafta.png`.", "",
     "Ortak agy stil eki:", "", "> " + agy["style"], "",
     "## Ikonlar", "", "| Kimlik | Yol | Kaynak / istem |", "|---|---|---|"]
for i in icons:
    r = routes.get(i) or ("agy" if i in agy_items and i not in models else "model")
    if r == "megascans":
        L.append(f"| `{i}` | model (Megascans) | `F:/Assets/small_stones_pack_vbuxda0iw_high.glb` (Quixel Megascans, sahibin Fab kitapligi; Fab Standart Lisansi, oyunda kullanim serbest; model repoya kopyalanmadi, yalniz render) |")
    elif r == "gemini" and i in agy_items:
        L.append(f"| `{i}` | gemini | gemini.google.com (sahibin hesabi, agy kotasi bitince ayni istem; ilk istemde tam stil eki, sonrakilerde \"ayni stil ve macenta arka plan\" kisaltmasi): {agy_items[i]['subject']} |")
    elif r == "agy" and i in agy_items:
        L.append(f"| `{i}` | agy | {agy_items[i]['subject']} |")
    elif i in models:
        m = models[i]
        extra = f" (yaw {m['yaw']})" if "yaw" in m else ""
        L.append(f"| `{i}` | model | `{m['glb']}`{extra} |")
    else:
        L.append(f"| `{i}` | ? | |")
open(os.path.join(D, "SOURCES.md"), "w", encoding="utf-8").write("\n".join(L) + "\n")
print("SOURCES", len(icons))
