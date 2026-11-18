# Arazi dokulari: kaynak ve lisans

Bu klasordeki on PNG, sahibin varlik kitapligindaki taramalardan uretilir (`F:\Assets`, yalniz okunur):
`pipeline/import_terrain_scans.py` (Blender'in Python'u + numpy). Lisans satirlari: `pipeline/licenses.md`
("Doku kaynaklari (arazi)").

| Katman | Kaynak | Boy | Karo |
|---|---|---|---|
| `cimen` | Poly Haven `leafy_grass` (CC0) | 2048 | 3 m |
| `toprak` | Megascans `forest_floor_wgyhdgy` | 2048 | 3 m |
| `kaya` | Megascans `cracked_nordic_coastal_rock_ukohaj0mw` | 2048 | 6 m, uc duzlemli |
| `kum` | Megascans `beach_sand_uinladeo` | 1024 | 2,5 m |
| `tarla` | `toprak` taramasi + koddan egatlar | 1024 | 2 m |

Renkler Altin Cayir paletine cekilir (betikteki `GRADE`: kanal basina carpan, ortalama hedefe). `cimen` hedefi
`World.Ground.cs` `CimenMean` ile ayni olmali. Farkli boylar `World.Ground.LoadArray`'de en buyuge buyutulur.

Yeniden uretmek (once 2K, sonra 1K katmanlar):

```
"C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" --background --factory-startup --python pipeline/import_terrain_scans.py -- F:/Assets game/assets/textures/terrain 2048 cimen,toprak,kaya
"C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" --background --factory-startup --python pipeline/import_terrain_scans.py -- F:/Assets game/assets/textures/terrain 1024 kum,tarla
```

| Dosya | Icerik |
|---|---|
| `<katman>_albedo.png` | RGB renk (sRGB), A yukseklik (katman karisimi) |
| `<katman>_nrm.png` | R,G normalin dunya X ve Z bileseni (0,5 merkez), B puruzluluk, A AO |

Normal kurali (OpenGL/DirectX) yukseklikle karsilastirilarak kendiliginden bulunur. Iceri aktarmada
`process/fix_alpha_border=false` (alfa veri tasir). Kitaplik yoksa yedek: `pipeline/gen_terrain_textures.py` ayni
duzende tamamen koddan uretir (dis kaynak yok).
