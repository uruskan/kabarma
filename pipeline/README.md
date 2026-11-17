# Üretim hattı

`clean_asset.py`: AI modelini oyuna hazırlar (URETIM.md §2.2 A.4–A.5). Blender 5.3 başsız:

```
"/c/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
  -P pipeline/clean_asset.py -- --in ham.glb --out game/assets/models/<ad>/<ad>.glb --name <ad> \
  --height <metre> --tris <bütçe> [--rig] --renders progress/m0/<ad>
```

Kaynak kaynaşır (doku dikişleri), bütçeye sadeleşir, doku → köşe rengi (her yüzde 7 örnek, CIELAB'da en yakın `game/data/palette.json` rengi, çoğunluk süzgeci), boy ve taban oturur, yumuşak gölge + açıyla sert kenar. Rapor: `<çıktı>.report.json` (SANAT-SES §9 ölçüt 1–5) ve 4 açı + 64 px siluet görüntüsü (ölçüt 6–8 için).

Oyunda: `Assets.Load("res://assets/models/<ad>/<ad>.glb")`. Köşe renkleri Godot'a doğrusal gelir; toon shader'da `vertex_linear = true` bunu karşılar.

## Ses (ses akışı)

- `audio_qc.py`: ses denetimi (URETIM §2.2 H.3, I.3): süre, gerçek tepe, kırpılma, LUFS, baş/son sessizlik, döngü dikişi, grup içi spektrum, müzikte tempo ve katman hizası. Yalnız Python + ffmpeg. `python pipeline/audio_qc.py [dosyalar] [--fix-dir klasör]` → `progress/m1/ses/audio_qc.md`.
- `synth_ui.py`: 5 arayüz sesi × 3 sürüm, sentez, kredi yok.
- `eleven.py`: ElevenLabs üretimi (kredi harcar, sahibin onayıyla). Sahibin onayladığı sınırlar kodda sert sınır; harcama defteri `progress/m1/ses/eleven_ledger.json`. Önce `python pipeline/eleven.py pipeline/requests/m1_audio.json --dry-run`. Katman eşlemesi değişirse, indirilmiş köklerden kredisiz yeniden toplama: `--only stems --reuse-raw --force`.
- `licenses.md`: ses kaynakları, planlar, üretim tarihleri.
