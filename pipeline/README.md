# Üretim hattı

`clean_asset.py`: AI modelini oyuna hazırlar (URETIM.md §2.2 A.4–A.5). Blender 5.3 başsız:

```
"/c/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
  -P pipeline/clean_asset.py -- --in ham.glb --out game/assets/models/<ad>/<ad>.glb --name <ad> \
  --height <metre> --tris <bütçe> [--rig] --renders progress/m0/<ad>
```

Kaynak kaynaşır (doku dikişleri), bütçeye sadeleşir, doku → köşe rengi (her yüzde 7 örnek, CIELAB'da en yakın `game/data/palette.json` rengi, çoğunluk süzgeci), boy ve taban oturur, yumuşak gölge + açıyla sert kenar. Rapor: `<çıktı>.report.json` (SANAT-SES §9 ölçüt 1–5) ve 4 açı + 64 px siluet görüntüsü (ölçüt 6–8 için).

Oyunda: `Assets.Load("res://assets/models/<ad>/<ad>.glb")`. Köşe renkleri Godot'a doğrusal gelir; toon shader'da `vertex_linear = true` bunu karşılar.
