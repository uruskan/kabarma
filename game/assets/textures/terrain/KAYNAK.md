# Arazi dokulari: kaynak ve lisans

Bu klasordeki on PNG tamamen koddan uretilir: `pipeline/gen_terrain_textures.py` (Blender'in Python'u + numpy, FFT
gurultusu, Voronoi, firca izleri). Dis kaynak, tarama, yapay zeka uretimi ya da indirilmis doku yoktur; projenin kendi
isidir, kredi harcanmadi.

Yeniden uretmek:

```
"C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" --background --factory-startup --python pipeline/gen_terrain_textures.py
```

| Dosya | Icerik |
|---|---|
| `<katman>_albedo.png` | RGB renk (sRGB), A yukseklik (katman karisimi) |
| `<katman>_nrm.png` | R,G normalin dunya X ve Z bileseni (0,5 merkez), B puruzluluk, A AO |

Katmanlar: `cimen` (cayir tabani, 3 m karo), `toprak` (patika, 3 m), `kaya` (egim, 6 m, uc duzlemli), `kum` (gol
kiyisi, 2,5 m), `tarla` (surulmus, 2 m). Iceri aktarmada `process/fix_alpha_border=false` (alfa veri tasir).

Gercek fotograf tabanli dokular (agy ya da Genex `texture --terrain`) ayni dosya adlari ve kanal duzeniyle buraya
konabilir; shader degismez. `World.Ground.cs` icindeki `CimenMean` yeni cimen dokusunun ortalama rengine guncellenir.
