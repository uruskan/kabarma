# Esya ikonlari: kaynaklar ve istemler

SANAT-SES 8.1. Her ikon 256 px, seffaf PNG, `res://assets/icons/items/<kimlik>.png`; koddan `ItemIcons.Get(id)` / `ItemIcons.Find(id)`.
Eksik kimlikte `_placeholder.png` (koddan uretilmis yer tutucu).

## Yollar

- **model**: oyunun kendi 3B modeli, `pipeline/render_icons.py` (Blender 5.3 Cycles, 96 ornek + denoise; sicak anahtar, soguk dolgu, kenar isigi;
  3/4 onden, 28 derece yukaridan, 40 mm; golge yakalayici zeminde hafif temas golgesi; 512 px cizilip 256'ya indirilir). Liste: `pipeline/icons_models.json`.
- **agy**: Google Antigravity CLI (`agy -p`, `image-generator` alt ajani, `generate_image`), `pipeline/gen_icons_agy.ps1`; macenta arka plan
  `pipeline/icon_post.py` ile anahtarlanir, kirpilir, %84 dolulukla ortalanir, temas golgesi eklenir, 256 px. Istemler: `pipeline/icons_agy.json`.
- Pafta: `python pipeline/icon_sheet.py game/assets/icons/items progress/gorsel/ikon/pafta.png`.

Ortak agy stil eki:

> Game inventory icon in the style of the item icons of the survival game Rust: a near-photoreal 3D product render with physically based materials, slightly worn hand-crafted look, soft diffused studio lighting from the upper left, a gentle cool fill light from the right and a subtle rim light, three-quarter view from slightly above (about 30 degrees), a single object or tight group centered and filling about 80 percent of the square frame, crisp readable silhouette. Not cartoon, not illustration, no outlines. Background: perfectly flat solid pure magenta (#FF00FF) from edge to edge, no gradient, no floor, no cast shadow on the background, no reflections. No text, no letters, no border, no UI frame. Square 1:1 image.

## Ikonlar

| Kimlik | Yol | Kaynak / istem |
|---|---|---|
| `ahsap_kapi` | model | `game/assets/models/yerlestirilebilir/ahsap_kapi.glb` |
| `alcak_duvar` | model | `game/assets/models/build/wood/alcak_duvar.glb` |
| `bahce_kapisi` | model | `game/assets/models/build/wood/bahce_kapisi.glb` |
| `capa` | model | `game/assets/models/capa/capa.glb` |
| `cati_kosesi` | model | `game/assets/models/build/wood/cati_kosesi.glb` |
| `cati_tepesi` | model | `game/assets/models/build/wood/cati_tepesi.glb` |
| `cit` | model | `game/assets/models/build/wood/cit.glb` |
| `civi` | agy | a small heap of shiny copper nails with flat heads |
| `civi_tabancasi` | agy | a chunky handheld nail gun, retro-futuristic farm tool with white and light blue painted metal body, rubber grip and a copper nail magazine |
| `clay` | agy | a lump of moist reddish-brown raw clay with finger marks and a smooth wet sheen |
| `copper_ingot` | agy | a single cast copper ingot bar with a trapezoid profile, shiny metallic copper with slight hammer marks and darker oxidized edges |
| `copper_ore` | agy | a fist-sized chunk of raw copper ore: dark grey rock with bright metallic orange copper nuggets and a few green malachite specks |
| `duvar` | model | `game/assets/models/build/wood/duvar.glb` |
| `duz_cati` | model | `game/assets/models/build/wood/duz_cati.glb` |
| `egg` | agy | a single large cream-colored egg with fine brown speckles |
| `egimli_cati` | model | `game/assets/models/build/wood/egimli_cati.glb` |
| `el_arabasi` | model | `game/assets/models/el_arabasi/el_arabasi.glb` |
| `fiber` | agy | a tied bundle of dry golden-green plant fiber strands, loosely coiled like raw twine |
| `folluk` | model | `game/assets/models/yerlestirilebilir/folluk.glb` |
| `kapi_kasasi` | model | `game/assets/models/build/wood/kapi_kasasi.glb` |
| `kare_temel` | model | `game/assets/models/build/wood/kare_temel.glb` |
| `kare_zemin` | model | `game/assets/models/build/wood/kare_zemin.glb` |
| `kargo_kasasi` | model | `game/assets/models/placeables/kargo_kasasi.glb` |
| `kazik_temel` | model | `game/assets/models/build/wood/kazik_temel.glb` |
| `kiris` | model | `game/assets/models/build/wood/kiris.glb` |
| `kozde_salgam` | agy | a fire-roasted turnip with charred blistered skin split open showing soft steaming golden flesh |
| `kule_tabani` | model | `game/assets/models/build/wood/kule_tabani.glb` |
| `makine_platformu_buyuk` | model | `game/assets/models/build/wood/makine_platformu_buyuk.glb` |
| `makine_platformu_kucuk` | model | `game/assets/models/build/wood/makine_platformu_kucuk.glb` |
| `makine_platformu_orta` | model | `game/assets/models/build/wood/makine_platformu_orta.glb` |
| `merdiven` | model | `game/assets/models/build/wood/merdiven.glb` |
| `mesale` | model | `game/assets/models/placeables/mesale.glb` |
| `ocak` | model | `game/assets/models/ocak/ocak.glb` |
| `paketleme` | model | `game/assets/models/paketleme/paketleme.glb` |
| `parmaklik` | model | `game/assets/models/build/wood/parmaklik.glb` |
| `pencereli_duvar` | model | `game/assets/models/build/wood/pencereli_duvar.glb` |
| `rampa` | model | `game/assets/models/build/wood/rampa.glb` |
| `sandik` | model | `game/assets/models/placeables/sandik.glb` |
| `stone` | model (Megascans) | `F:/Assets/small_stones_pack_vbuxda0iw_high.glb` (Quixel Megascans, sahibin Fab kitapligi; Fab Standart Lisansi, oyunda kullanim serbest; model repoya kopyalanmadi, yalniz render) |
| `sur_duvari` | model | `game/assets/models/build/wood/sur_duvari.glb` |
| `sur_kapisi` | model | `game/assets/models/build/wood/sur_kapisi.glb` |
| `sutun` | model | `game/assets/models/build/wood/sutun.glb` |
| `tas_firin` | model | `game/assets/models/tas_firin/tas_firin.glb` |
| `tezgah` | model | `game/assets/models/tezgah/tezgah.glb` (yaw 180) |
| `turnip` | agy | a fresh turnip root with white and purple skin and a few short green leaves |
| `turnip_seed` | agy | a small burlap seed pouch tied with string, open at the top, with a few small dark round seeds spilled beside it |
| `ucgen_duvar` | model | `game/assets/models/build/wood/ucgen_duvar.glb` |
| `ucgen_temel` | model | `game/assets/models/build/wood/ucgen_temel.glb` |
| `ucgen_zemin` | model | `game/assets/models/build/wood/ucgen_zemin.glb` |
| `uyku_tulumu` | model | `game/assets/models/placeables/uyku_tulumu.glb` |
| `wheat` | agy | a small bound sheaf of golden ripe wheat stalks with full heavy ears, tied with twine |
| `wood` | agy | a small stack of three split firewood logs with rough brown bark and pale cut ends showing growth rings |
| `yarim_duvar` | model | `game/assets/models/build/wood/yarim_duvar.glb` |
| `yemlik` | model | `game/assets/models/yerlestirilebilir/yemlik.glb` |
