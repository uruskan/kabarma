# Üretim hattı

Statik varlıklar ve makineler için URETIM.md §2.2 A ve B adımları. Hepsi Blender 5.3 başsız; kredi harcamaz.

## Toplu: `run_golden.py`

```
python pipeline/run_golden.py                 # pipeline/golden_manifest.json'daki 11 altın varlık
python pipeline/run_golden.py kaya tas_firin  # yalnız bunlar
python pipeline/run_golden.py --sync-only     # Blender yok: raporlardan game/data/assets.json
```

Ham kaynaklar git'te değil (ana kopyada `.tools/tripo`, `.tools/tripo_web`); yol `golden_manifest.json` içinde. Her varlık için Blender'da `clean_asset.py` çalışır (günlük `progress/m1/varlik/log/`), eski rapor bir kez `progress/m1/varlik/eski/` altına saklanır, eski ve yeni boy/parça konumları `progress/m1/varlik/fark.md`'ye yazılır, sonra `game/data/assets.json` güncellenir. Ardından Godot içe aktarma: `& $G --headless --path game --import`.

## Tek varlık: `clean_asset.py`

```
blender -b --factory-startup --python-exit-code 1 -P pipeline/clean_asset.py -- \
  --in ham.glb --out game/assets/models/<ad>/<ad>.glb --name <ad> --label "<Türkçe ad>" \
  --height <m> --tris <hedef> --class <bütçe sınıfı> --keep_texture \
  [--parts --rename tripo_part_0=body,... --drop tripo_part_7 --pivots chimney=bottom --sockets ad=parça:center[:çocuk]] \
  --renders progress/m1/varlik/render/<ad> --card progress/m1/varlik/kart/<ad>.png
```

Adımlar: parçalar birleşir (kaynak adıyla kimlik) → dikiş köşeleri kaynar (parça içinde) → bütçeye sadeleşir → **onarım** (şerit yüzler, ikiden çok yüzlü kenar, delik kapama, dışa bakan normal) → doku 1K → boy, taban, orta → denetim (SANAT-SES §9 ölçüt 1–5) → parça modu: adlı düğüm, pivot, soket → görüntü (4 açı, kalınlık haritası, 64 px siluet, ikon) → ana GLB, iki LOD, çarpışma kabuğu, kart, rapor.

Çıktılar ana GLB'nin yanında: `<ad>.glb`, `<ad>_lod1.glb` (yarı üçgen, 512 px doku), `<ad>_lod2.glb` (beşte bir, 256 px), `<ad>_hull.tres` (Godot `ConvexPolygonShape3D`, en fazla 64 nokta, Y yukarı), `<ad>_icon.png` (256 px, saydam, aynı ışık ve açı), `<ad>.report.json`. Ana GLB'ye LOD ya da çarpışma düğümü konmaz: oyun kodu her `MeshInstance3D`'yi görünüş sayıyor. Godot içe aktarma ayrıca kendi LOD'larını da üretir (`meshes/generate_lods`).

**Ölçütler (betik):** 1 doku var ve sınıf sınırında (`assets.json` `budgets`) · 2 üçgen sınıf bütçesinde · 3 kalınlık örneklemesi: her yüzde içe ışınla yerel kalınlık; kalınlığı sınırın (en uzun kenarın %6'sı) altında kalan yüzün çevresi iki yönde de inceyse çubuk/diken, tek yönde inceyse levha (yaprak, kapak); çubuk/diken alanı yüzeyin %2'sini geçmez · 4 manifold dışı kenar, delik kenarı, ters yüz, negatif hacimli parça ve havada kalan parça sıfır · 5 boy hedefte, taban y=0, orta x=z=0; ön yön betikle ölçülemez, bildirilen eksen yazılır (Tripo +X). Ölçüt 6–8 görüntüden elle puanlanır ve `assets.json`'a yazılır.

## Stil föyü: `style_sheet.py`

```
blender -b --factory-startup -P pipeline/style_sheet.py -- --out docs/stil/altin_varliklar.png --lights docs/stil/altin_varliklar_4isik.png
```

Altın varlıklar gerçek boylarıyla, 1,6 m ölçek yanında; altında `game/data/palette.json` paleti. `--lights`: SANAT-SES §2'nin dört gün saati (EEVEE), ölçüt 7 için.

Stüdyo görüntüsü Workbench (dokular, gölge, oyuk), adlar 2B yazı katmanında. Dört ışık görüntüsünde dünya yalnız ortam ışığını (SANAT-SES §2 sis rengi) verir; gökyüzü geçişi (tepe → ufuk) saydam görüntünün arkasına çizilir, çünkü ortografik kamerada dünya her yerde tek renk görünür. glTF iskeletlerinin kemik gösterim küresi (`Icosphere`) boya katılmaz.

## Üretim istekleri: `gen_golden.sh`, `gen_retry.py`

Kredi harcayan Tripo istemleri. `gen_golden.sh` M0/M1'in ilk üretimi (bash; bu makinede PowerShell'den `tripo make ... --dry-run` ile tek tek). `gen_retry.py` ölçütten geçemeyenlerin yeniden üretimi: varsayılan `--dry-run` (tahmin, kredi harcamaz), `--run <ad>` üretir. Alt oturumlar çalıştırmaz; sahibin onayıyla ana oturum çalıştırır. Güncel liste: `progress/m1/varlik/uretim_istegi.md`.

## Seçenekler

`--dark_fill <pay>` (varsayılan 0, kapalı): yüzeyin bu payından büyük delik yamaları dokunun en koyu bölgesine bağlanır. Kapsülde denendi: penceredeki açık renkli levhalar kaynağın kendi geometrisi çıktı, görünür fark olmadı; kapalı bırakıldı.

## Ses (ses akışı)

- `audio_qc.py`: ses denetimi (URETIM §2.2 H.3, I.3): süre, gerçek tepe, kırpılma, LUFS, baş/son sessizlik, döngü dikişi, grup içi spektrum, müzikte tempo ve katman hizası. Yalnız Python + ffmpeg. `python pipeline/audio_qc.py [dosyalar] [--fix-dir klasör]` → `progress/m1/ses/audio_qc.md`.
- `synth_ui.py`: 5 arayüz sesi × 3 sürüm, sentez, kredi yok.
- `eleven.py`: ElevenLabs üretimi (kredi harcar, sahibin onayıyla). Sahibin onayladığı sınırlar kodda sert sınır; harcama defteri `progress/m1/ses/eleven_ledger.json`. Önce `python pipeline/eleven.py pipeline/requests/m1_audio.json --dry-run`. Katman eşlemesi değişirse, indirilmiş köklerden kredisiz yeniden toplama: `--only stems --reuse-raw --force`.
- `licenses.md`: ses kaynakları, planlar, üretim tarihleri.
