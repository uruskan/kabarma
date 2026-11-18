# Üretim isteği: görsel yenileme (gorsel-model akışı, 18 Kasım 2026)

> **Durum (18 Kasım, gece):** üstteki 6 model ana oturumda üretildi (300 kredi, bakiye 10) ve `pipeline/oturt_gorsel.py` ile oturtuldu. Vitrin görüntüleri `progress/gorsel/model/yeni/`.
> - Kapsül gövdesi kendi kısa ayaklarıyla geldi. Bu yüzden `data/kapsul.json` `leg` boş: Blender ayakları takılmıyor.
> - Kapı boşluğu ölçüldü: yükseklik 1,05–1,75 m, en 0,8 m, `+Z`'ye bakıyor. Blender kapağı ona göre ayarlandı: menteşe [0, 0.9, 1.42], boy 1,0 m.
> - El Arabası üç parça geldi: tekne `body`, çerçeve `frame`, teker `wheel`. Soketler `wheel_pivot`, `handle`, `load_slot`.
> - `AssetsTests` yeni yapıya uyarlandı: kapsülde `hatch` parçası yok; El Arabası'nda tek `handle` soketi var.
> - Taş Fırın sonraya kaldı (Tripo web havuzu).

Bu dal **hiç kredi harcamadı**. Aşağıdaki üretimleri ana oturum, sahibin onayıyla çalıştırır. Sahip Tripo API bakiyesinin tamamını (310) bu iş için onayladı; liste kaliteye göre yeniden yazıldı.

- Betik: `pipeline/gen_gorsel.py`. Varsayılan `--dry-run`'dır ve kredi harcamaz. Üretim: `python pipeline/gen_gorsel.py --run <ad>`.
- Tahmin kaydı: `progress/gorsel/model/gen_gorsel_dry_run.log` (tripo CLI `--dry-run --json`, ham JSON dahil). CLI parçalama adımını tahmine katmıyor; M0'da ölçülen 40 kredi elle eklendi.
- Ortak ayar: `--model tripo-v3.1 -p texture_quality=detailed -p texture_version=v3.5 -p pbr=true`. Yeni modeller normal ve metal/pürüzlülük haritalarıyla gelir.
- **Kalite yükseltmesi:** öncelikli dört modelde `-p geometry_quality=detailed` (Ultra geometri). Dry-run tahmini model başına 50 kredi (standart 30).
- Çıktı (git dışı): `C:/Users/uruskan/Documents/GitHub/kabarma/.tools/tripo/<ad>_v3/`.
- Yeni stil cümlesi her istemin sonunda yer alıyor; eski "toy-like, chunky soft, no thin spikes" dili bırakıldı: *Semi-stylised high-end game asset, mature and believable, grounded PBR materials with real wear: chipped paint on edges, grime in crevices, scorch and rust streaks, crisp bevelled hard-surface detail with panel seams, bolts and rivets, strong readable silhouette. Not cute, not toy-like, not cartoon, no plastic look. Muted off-white, cobalt blue and safety-yellow accents for a frontier farming co-op on a warm golden-meadow moon.*

## Tripo'ya gitmeden çözülenler (kredisiz)

| Ne | Nasıl | Dosya |
|---|---|---|
| Kapsül kapağı = rampa | Blender modeli (`pipeline/build_props.py` `kapsul_kapak`). Boyalı dış yüzde kobalt bant ve uyarı şeritleri; iç yüzde baklava sac ve kaymaz çıtalar; menteşe pimi ve kilit dilleri | `props/kapsul_kapak.glb` |
| Kapsül iniş ayağı (4 örnek) | Blender modeli (`kapsul_ayak`): taban tablası ve mafsal, krom piston, toz körüğü, hidrolik hortum, üst çatal | `props/kapsul_ayak.glb` |
| Kazılan taş kayası, dik kaya, düz kaya | Sahibin kütüphanesinden (F:\Assets, Poly Haven CC0: `boulder_01`, `rock_moss_set_02`), `pipeline/lib_import.py` | `lib/kaya`, `lib/kaya_dik`, `lib/kaya_duz` |

Bu üçü önceki listede 60 kredi tutuyordu (kapak ve ayak). Kazanılan kredi dört öncelikli modelin Ultra geometrisine gitti.

Kapak ve ayaklar yalnız yeni gövdeyle takılır: eski `kapsul.glb`'de kendi `hatch` düğümü ve kısa ayakları var. `--test=lineup --lineup_deneme`, kapağı eski gövdenin kendi kapağı çıkarılmış hâliyle gösterir (`kapsul_parca/kapak_deneme_*.png`). Kapalıyken boyalı yüz dışarıda, açıkken rampa yüzü yukarıda.

## Liste ve kredi

Tripo API bakiyesi 310. Sıra öncelik sırasıdır; kesme çizgisi bakiyenin içinde kalan son satırın altındadır.

| # | Ad | Neden | Ayar | Tahmin | Birikimli | Hedef dosya |
|---|---|---|---|---|---|---|
| 1 | `kapsul_govde` | Sahibin 1 numaralı şikâyeti: oyuncak balon kapsül. İlk kare, Kargo Rampası | Ultra, 14k yüz | 50 | 50 | `kapsul/kapsul.glb` |
| 2 | `capa` | Ekranda her an oyuncunun elinde; oyuncak tabanca gibi | Ultra, 5k yüz | 50 | 100 | `capa/capa.glb` |
| 3 | `ocak` | Üssün kalbi; mavi kubbe ve topak biçim | Ultra, 14k yüz | 50 | 150 | `ocak/ocak.glb` |
| 4 | `tezgah` | Mavi oyuncak çerçeve, silueti koltuğa benziyor | Ultra, 14k yüz | 50 | 200 | `tezgah/tezgah.glb` |
| 5 | `paketleme` | Dağınık oyuncak; işlevi okunmuyor | standart, 14k yüz | 30 | 230 | `paketleme/paketleme.glb` |
| 6 | `el_arabasi` | İnce boru çerçeve, oyuncak tekne; tekerlek ayrı parça | standart + parçalama | 30 + 40 = 70 | **300** | `el_arabasi/el_arabasi.glb` |
| | **kesme çizgisi (310)** | | | | | |
| 7 | `tas_firin` | Kabarcık biçim; kapak ve baca ayrı parça | standart + parçalama | 30 + 40 = 70 | 370 | `tas_firin/tas_firin.glb` |

- **Kesme çizgisinin üstü: 300 kredi**, bakiyede 10 kalır. **Tam liste: 370 kredi.**
- Parçalama adımında CLI şu uyarıyı veriyor: "v2-only parameters set — using segment model v2.0-20260430". Bu bilgi notu; dry-run geçerli, hata yok.
- En kötü durum (SANAT-SES §9: en fazla 4 deneme): yalnız kapsül gövdesi için 4 deneme 200 kredi tutar. Öneri: kapsülde agy referansıyla tek deneme; beğenilmezse ikinci deneme `paketleme`nin yerine geçer.
- **Risk:** metinden 3B'ye istemlerde "door NOT included / legs NOT included" çoğu zaman yok sayılır. Gövde kapak ya da ayakla birleşik gelirse:
  - Kapı birleşik gelirse gövde yine kullanılır ve Blender kapağı takılmaz (`kapsul.glb`'ye `hatch` düğümü ayrılırsa eski yol çalışır).
  - Ayaklar birleşik gelirse `data/kapsul.json` `leg` satırı boşaltılır.
  - Gövdede agy referansı (kapısız, ayaksız çizilmiş) bu riski azaltır.
- Sırada olmayan, düşünülmesi gereken: çayır ağacı `agac` (Tripo web, havuz 3.200). Oyun içinde "bebek" hissini en çok veren varlık bu (lolipop taç). Kütüphanedeki huş paketi (CC-BY 4.0, LOLIPOP) alfa yapraklı; `plant_wind.gdshader`'da alfa yok, o yüzden alınmadı. Yaratıklar (Zıpzıp, Gıdak, Pofuduk) kimliğin parçası; yaratik akışına bırakıldı.

## agy referans istemleri (çalıştırılmadı)

Bu oturum `agy`'yi çalıştıramadı: çağrı Claude Code izin sınıflandırıcısında reddedildi. Ana oturum, sahibin izniyle, referansı üretip `progress/gorsel/model/ref/<ad>.png` olarak koyarsa `gen_gorsel.py` o varlığı metin yerine görselden üretir (image-to-model). Bunun **kredisi tahmin edilmedi**: referans dosyası olmadığı için dry-run metinle koştu; görsel girişin bedeli ilk dry-run'da görülmeli. Yalnız kapsül gövdesi ve Ocak için öneriliyor:

```
agy -p "Generate ONE image with your image generation tool and save it as kapsul_govde.png in the current directory. Do not run shell commands. Concept art reference for an image-to-3D generator: a single rugged industrial planetary landing capsule hull, three-quarter front view, plain light grey studio background, soft even light, whole object in frame, no people, no text. Squat truncated-cone body about 3 m tall, heavy scorched ablative heat-shield ring at the bottom, off-white painted steel hull plates with panel seams, rivets and chipped worn paint, muted cobalt-blue and safety-yellow bands, a large rectangular door opening on the front (empty recessed frame, door not included), small round porthole, recessed grab handles, RCS thruster quads, short antenna mast, stencilled hazard chevrons, four leg mount brackets near the base (legs not included). Mature semi-stylised survival-game look (Astroneer polish, Rust/Valheim material seriousness). Not cute, not toy-like."
```

```
agy -p "Generate ONE image with your image generation tool and save it as ocak.png in the current directory. Do not run shell commands. Concept art reference for an image-to-3D generator: a compact frontier hearth furnace about 2 m tall, three-quarter front view, plain light grey studio background, whole object in frame, no text. Heavy stone and mortar base, cast-iron firebox with a big glowing fire door on the front, riveted dark steel hood, tall flue pipe with spark arrestor cap, brass pressure gauge and valve wheel on the side, small cobalt-blue co-op badge plate, soot around the door. Mature semi-stylised survival-game look. Not cute, not toy-like."
```

## Oturtma (yeni model gelince)

Her model için ortak adımlar:

1. `pipeline/golden_manifest.json` → o varlığın `"in"` yolunu `.tools/tripo/<ad>_v3/.../model.glb` yap.
2. `python pipeline/run_golden.py <ad>`: temizlik, boy, taban, LOD, kabuk, ikon, `assets.json`.
3. Malzeme: yeni model `pbr=true` ile kendi normal ve metal/pürüzlülük haritasını getirir. İki yol var:
   - `pipeline/pbr_manifest.json`'da o varlığa `"keepMaps": true` yaz ve `pbr_bake.py --only <ad>` çalıştır (yalnız albedo derecelendirmesi).
   - Ya da satırı sil ve `materials.json`'daki kaydını kaldır.

   Unutulursa zarar yok: `MaterialUplift` yalnız tam malzeme adıyla eşleşir. Tripo adı görev kimliğini taşıdığından eski haritalar yeni modele takılmaz; yalnız `UPLIFT ... yuzeyi yok` uyarısı çıkar.
4. `& $G --headless --path game --import`, sonra `--test=lineup --lineup_only=<ad> --lineup_tag=yeni`.

Varlığa özel:

| Ad | `run_golden` ayarı | Oyun bağlaması |
|---|---|---|
| `kapsul_govde` | `height 3.0, tris 14000, class makine`; `--parts` yok (tek gövde) ya da Tripo `body/base` ayrımı | `Pod.cs` aynı yolu yükler (`kapsul.glb`, −90° Y). **Yeni gövdede `hatch` düğümü olmamalı**: o zaman `Pod.Model.cs` Blender kapağını menteşeye (`hinge` [0, 0.5, 1.28]) ve dört Blender ayağını takar. Ayak bağlantıları (`data/kapsul.json` `legs`: ±0,95 / 0,97 / ±0,95, 28° dışa eğim, `legLength` 1,1 m) yeni gövdenin braketlerine göre yalnız veriyle ayarlanır; menteşe noktası da öyle. Çarpışma silindiri (`Pod.cs` r 1,35 / h 2,6) 3 m gövdeye uyar. |
| `capa` | eskisi gibi (`golden_manifest` satırı) | Değişiklik yok (`Player.cs` aynı yolu yükler). Elde yön ve tutma noktası oyuncu akışında. |
| `ocak` | eskisi gibi | Değişiklik yok (`Ocak.cs`). Ateş ağzı önde olmalı: ışık `(0, 1.2, 0.9)`. |
| `tezgah`, `paketleme` | eskisi gibi | Değişiklik yok (`placeables.json` `model`). |
| `el_arabasi` | `--parts --rename` yeni parçalamaya göre yeniden eşlenir. Parçalar: `body`, `wheel`, `frame_l/r`, `grip_l/r`, `fork`. Boş düğümler: `wheel_pivot`, `handle_l/r`, `handle`, `load_slot` (bkz. varlik notu) | `ElArabasi.cs` düğüm adlarıyla çalışır; adlar korunursa kod değişmez. |
| `tas_firin` | `--parts --rename` → `body, base, door, door_handle, chimney, panel`; `--pivots chimney=bottom` | `TasFirin` (Pofuduk.cs) değişmeden çalışır. |

## Bu dalda kredisiz yapılanlar

- 21 modele pişirilmiş PBR (`pipeline/pbr_bake.py`, `game/data/materials.json`). 18'inde normal, ORM ve albedo var; Tripo web'den gelen 3'ünde (kaya, ağaç, kova) yalnız albedo.
- Blender modelleri (`pipeline/build_props.py`): Ocakbaşı, Kargo Rampası pisti, kargo roketi, kapsül kapağı, kapsül iniş ayağı.
- Kütüphane kayaları (`pipeline/lib_import.py`, lisanslar `pipeline/licenses.md`): kazılan taş kayası ve süs kayaları (dik, düz).
