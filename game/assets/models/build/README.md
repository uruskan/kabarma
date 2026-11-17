# Yapı kiti: ahşap (ICERIK §10.1, GDD §7)

26 şekil, Makine Platformu üç boy olduğu için **28 GLB**. Parçalar yapay zekâyla değil kodla üretilir (`pipeline/build_kit.py`), çünkü yuvalara milimetrik oturmaları gerekir (URETIM §2.2 C). Görünüm: dokulu, orta poligon, el boyaması stilize (SANAT-SES §1, 17 Kasım güncellemesi). Kalın, pahlı kalaslar ve kirişler, görünür kalas derzleri, tohumlu küçük düzensizlik (kalas genişliği, kalınlığı ve uç payları), demir cıvata başları ve kuşaklar (SANAT-SES §5).

Temas föyü: `progress/m1/build_kit_sheet.png`. Her karede 1,6 m'lik bir manken ve üçgen sayısı var.

## Dosyalar (`wood/`)

| Dosya | Ne |
|---|---|
| `<id>.glb` | Her parça ayrı GLB; dokular gömülü (WebP). Tek yüzey, tek malzeme `kabarma_wood`, `COLOR_0` köşe rengi (kalas tonu, pah parlaması, alt gölge). |
| `wood_atlas_albedo.png` / `_normal.png` / `_rm.png` | Ortak 1024 px atlas. Blender içinde numpy ile yordamsal üretilir. 8 şerit: 0–2 açık kalas, 3–4 koyu kiriş, 5–6 çatı tahtası, 7 karolar (kalas/kiriş/çatı kesiti halkaları, demir, paslı demir). `rm`: G pürüz, B metal. |
| `wood_kit.tres` | Godot'ta 28 parçanın paylaştığı tek StandardMaterial3D (albedo × köşe rengi, normal 0,6, pürüz/metal kanalları). `.glb.import` dosyaları malzemeyi buna bağlar ve gömülü dokuları ayıklamaz, yani bellekte tek doku seti olur. |
| `sockets.json` | Tür, boyut, sınırlar, merkez, yuvalar, çarpışma şekilleri, arazi oturma ayarı, kapı kanadı düğümleri, üçgen sayısı ve yuva kuralları. `BuildKit.Load()` bunu okur. |

## Parçalar

Bütçe 200–1.500 üçgen (SANAT-SES §4). Godot'ta yüklenip sayıldı: 28/28 içeride.

| id | Ad | Tür | Orijin | Üçgen | Boyut x×y×z (m) | Yuvalar |
|---|---|---|---|---|---|---|
| `kare_temel` | Kare Temel | foundation | top_center | 910 | 3.07 x 1.5 x 3.07 | cell, corner, edge |
| `ucgen_temel` | Üçgen Temel | foundation | top_center | 662 | 2.97 x 1.5 x 2.63 | corner, edge |
| `kazik_temel` | Kazık Temel | foundation | top_center | 1172 | 3.12 x 3.5 x 3.12 | cell, corner, edge |
| `duvar` | Duvar | wall | bottom_center | 700 | 3.0 x 3.01 x 0.35 | wall_bottom, wall_end, wall_top |
| `yarim_duvar` | Yarım Duvar | wall | bottom_center | 416 | 3.0 x 1.51 x 0.32 | wall_bottom, wall_end, wall_top |
| `alcak_duvar` | Alçak Duvar | wall | bottom_center | 328 | 3.0 x 1.01 x 0.39 | wall_bottom, wall_end, wall_top |
| `kapi_kasasi` | Kapı Kasası | wall | bottom_center | 1008 | 3.0 x 3.01 x 0.37 | wall_bottom, wall_end, wall_top |
| `pencereli_duvar` | Pencereli Duvar | wall | bottom_center | 1164 | 3.0 x 3.01 x 0.42 | wall_bottom, wall_end, wall_top |
| `ucgen_duvar` | Üçgen Duvar | wall | bottom_center | 428 | 3.0 x 3.11 x 0.35 | wall_bottom, wall_end |
| `kare_zemin` | Kare Zemin | floor | top_center | 668 | 3.0 x 0.38 x 3.07 | cell, corner, edge |
| `ucgen_zemin` | Üçgen Zemin | floor | top_center | 532 | 2.97 x 0.38 x 2.63 | corner, edge |
| `merdiven` | Merdiven | stairs | bottom_center | 716 | 2.67 x 3.02 x 3.0 | cell_bottom, stair_bottom, stair_top |
| `rampa` | Rampa | stairs | bottom_center | 816 | 3.07 x 2.0 x 3.08 | cell_bottom, stair_bottom, stair_top |
| `egimli_cati` | Eğimli Çatı | roof | eave_center | 1044 | 3.0 x 4.0 x 3.6 | roof_eave, roof_side, roof_top |
| `cati_kosesi` | Çatı Köşesi | roof | eave_center | 1068 | 3.71 x 4.24 x 3.7 | roof_eave |
| `cati_tepesi` | Çatı Tepesi | roof | ridge_center | 260 | 3.0 x 0.62 x 0.76 | ridge, ridge_end |
| `duz_cati` | Düz Çatı | roof | top_center | 708 | 3.0 x 0.47 x 3.0 | cell, corner, edge |
| `sutun` | Sütun | pillar | bottom_center | 284 | 0.46 x 3.01 x 0.46 | pillar_bottom, pillar_top |
| `kiris` | Kiriş | beam | bottom_center | 228 | 3.0 x 1.08 x 0.41 | beam_end, wall_bottom, wall_top |
| `parmaklik` | Parmaklık | fence | bottom_center | 472 | 3.0 x 1.14 x 0.26 | wall_bottom, wall_end |
| `cit` | Çit | fence | bottom_center | 864 | 3.0 x 1.78 x 0.26 | wall_bottom, wall_end |
| `bahce_kapisi` | Bahçe Kapısı | fence | bottom_center | 1188 | 3.0 x 2.0 x 0.28 | wall_bottom, wall_end |
| `sur_duvari` | Sur Duvarı | fence | bottom_center | 488 | 2.98 x 3.98 x 0.61 | wall_bottom, wall_end |
| `sur_kapisi` | Sur Kapısı | fence | bottom_center | 1276 | 3.0 x 4.65 x 0.52 | wall_bottom, wall_end |
| `kule_tabani` | Kule Tabanı | platform | bottom_center | 1244 | 3.0 x 3.52 x 3.0 | cell, cell_bottom, corner, edge |
| `makine_platformu_kucuk` | Makine Platformu (küçük) | platform | top_center | 936 | 3.0 x 1.24 x 3.07 | cell, corner, edge |
| `makine_platformu_orta` | Makine Platformu (orta) | platform | top_center | 1076 | 6.0 x 1.24 x 6.07 | cell, corner, edge |
| `makine_platformu_buyuk` | Makine Platformu (büyük) | platform | top_center | 1498 | 9.0 x 1.24 x 9.07 | cell, corner, edge |

Notlar:
- **Merdiven** 45°'dir (3 m'de 3 m yükselir, 10 basamak). Oyuncunun `FloorMaxAngle` değeri en az 46° olmalı; Godot'nun varsayılanı tam 45°.
- **Rampa** 3 m'de 1,5 m yükselir. İki rampa uç uca eklenince bir kat çıkar (`stair_bottom` → `stair_top`).
- **Çatılar** 45°'dir: bir eğimli çatı tam bir 3×3×3 hücre kaplar. Mahya, saçaktan 3 m içeride ve 3 m yukarıdadır. 6 m genişliğindeki bir evde iki karşılıklı çatı tam ortada buluşur. `ucgen_duvar` beşik çatının alın üçgenidir: yüksek ucu (+X) mahyaya bakar, karşı yarı için 180° döndürülür.
- **Kapılar:** `bahce_kapisi` içinde `Kanat` düğümü var (menteşe x = -0,8). `sur_kapisi` içinde `KanatSol` (x = -0,98) ve `KanatSag` (x = +0,98) var. Bu düğümler menteşe ekseninde durur, Y ekseninde döndürülerek açılır. Çarpışmaları `Col_<düğüm>` adıyla ayrıdır; kapı açılınca `Disabled` yapılmalı.
- Kapı kasası boşluğu 1,5 × 2,3 m. Pencere boşluğu 1,3 × 1,1 m, eşiği 1,0 m'de. Dış yüzde (+Z) mavi boyalı kepenkler var.

## Eksenler ve orijinler (Godot)

+Y yukarı, -Z ileri, birim metre. Parçanın **+Z yüzü "dış/ön" yüzüdür** (kepenkler, saçak). Izgara 3 m, kat 3 m.

| Orijin | Parçalar | Anlamı |
|---|---|---|
| `top_center` | temeller, zeminler, düz çatı, makine platformları | Üst yüz merkezi. Yürünen yüzey y=0'da, gövde aşağı iner. Temel ayakları y = -1,5'e, kazık temel -3,5'e kadar uzanır. |
| `bottom_center` | duvarlar, çit/sur, sütun, kiriş, merdiven, rampa, kule | Alt kenar (ya da alt yüz) merkezi, y=0 oturduğu yüzeydir. Duvarın gövdesi x ∈ [-1,5, 1,5], z ∈ ±0,16 aralığındadır, yani duvar ızgara kenarının **üstüne ortalanır**. |
| `eave_center` | eğimli çatı, çatı köşesi | Saçak çizgisinin merkezi, alttaki duvarın üst ekseni (kat çizgisi) üzerinde. |
| `ridge_center` | çatı tepesi | Mahya noktası. |

Kat çizgisi: duvarların gerçek üst yüzü 2,99 m'de, mantıksal üst (`wall_top`) 3,00 m'de. Duvar altı -0,02 m'ye kadar zemine gömülür. Böylece katlar arasında üst üste binen eş düzlemli yüz kalmaz (z-fighting yok) ve dışta 1 cm'lik bir gölge çizgisi oluşur. Köşelerde iki duvarın dış köşesinde 15 cm'lik bir oyuk kalır. Bunu `sutun` kapatır; sütun 0,46 m kalınlığında, köşeye ortalı ve 3 m boyundadır.

## Yuvalar ve kurallar

Her yuvanın `pos` ve `dir` değeri parçanın yerel koordinatındadır. `dir` yataydır ve dışa bakar. Hayaletin "attach" yuvası, yerleşik bir parçanın "target" yuvasına bir hizalama kuralıyla oturur:

- `opposite`: yönler zıt.
- `same`: yönler aynı.
- `either`: iki yön de olur; R ile seçilir.
- `any4`: 90°'nin katları.

| Attach → Target | Hizalama | Kimler | Örnek |
|---|---|---|---|
| edge → edge | opposite | foundation, floor, platform, roof | temel yanına temel, zemin yanına zemin |
| edge → wall_top | either | floor, roof | duvar üstüne zemin ya da düz çatı (içe ya da dışa) |
| corner → pillar_top | any4 | floor, roof | sütun tepesine zemin |
| wall_bottom → edge | either | hepsi | temel ya da zemin kenarına duvar, çit, kiriş |
| wall_bottom → wall_top | either | hepsi | duvar üstüne duvar ya da üçgen duvar |
| wall_end → wall_end | opposite | fence | araziye uç uca çit ya da sur |
| pillar_bottom → corner / pillar_top | any4 | hepsi | köşeye sütun, sütun üstüne sütun |
| beam_end → pillar_top | any4 | hepsi | iki sütun arasına kiriş |
| cell_bottom → cell | any4 | hepsi | hücreye merdiven, rampa ya da kule |
| stair_top → edge | opposite | hepsi | bir kenara çıkan merdiven |
| stair_bottom → stair_top | opposite | hepsi | uç uca rampa |
| roof_eave → wall_top | either | hepsi | duvar üstüne eğimli çatı ya da çatı köşesi |
| roof_side → roof_side, ridge_end → ridge_end | opposite | hepsi | yan yana çatı |
| ridge → roof_top | either | hepsi | mahyaya çatı tepesi |

Kural tablosu `sockets.json` → `rules` içindedir; sistem tabloyu oradan okur.

## Taşıma dengesi (GDD §7)

`support.max_span = 3` (ahşap). Araziye oturan parçaların açıklığı 0'dır (temel, çit, sur, kule). Dikey parçalar desteğin açıklığını devralır (duvar, sütun, merdiven). Yatay parçalar (`floor`, `roof`, `beam`) her adımda +1 ekler. 3'ü aşan yerleşime izin verilmez (kırmızı hayalet, "açıklık 4 > 3"). Bir parça sökülünce açıklıklar yeniden hesaplanır. Desteksiz kalan parçalar `RigidBody3D` olarak düşer.

## Kod

- `scripts/building/BuildKit.cs`: `sockets.json` ve GLB'leri yükler, çarpışma şekillerini kurar.
- `scripts/building/BuildSystem.cs`: hayalet, yuvaya oturma, geçerlilik, yerleştirme, söküm ve düşme. API: `Init(worldRoot, heightAt)`, `SetActive`, `UpdateAim(rayFrom, rayDir)`, `TryPlace()`, `Rotate()`, `NextPiece(±1)`, `Select(id)`, `Remove(node)`, `PieceAt(ray)`, `PlaceAt(id, xf, out reason)`, `HandleKey(Key)` (R/Q/E), olaylar `Placed`, `Removed`, `Fell`, `SelectionChanged`. Parçalar katman 1 ile katman 10'da (`BuildLayer`) durur.
- `scenes/build_test.tscn` + `scripts/building/BuildTest.cs`: bağımsız sınama. 28 parçayı yükleyip üçgen sayısını denetler, sonra nişan ve yuva API'siyle iki katlı bir ev kurar: 6 temel, 16 duvar (kapı kasası ve pencereli duvarlar dahil), 8 sütun, 4 zemin, merdiven, sundurma, beşik çatı, alın üçgenleri ve mahya. Ardından çit ve bahçe kapısını kurar, açıklık sınırını dener ve düşmeyi gösterir. Ekran görüntüleri ve günlük `progress/m1/build_*.png` ile `build_test.log` dosyalarına yazılır.

```
dotnet build game/Kabarma.Game.csproj
<godot> --path game --resolution 1600x900 res://scenes/build_test.tscn
```

## Yeniden üretme

```
"C:/Program Files/Blender Foundation/Blender 5.3/blender.exe" -b --factory-startup --python-exit-code 1 \
    -P pipeline/build_kit.py -- [--only duvar,kapi_kasasi] [--no-sheet] [--tex 1024]
<godot> --headless --path game --import
```

Çıktı tohumludur: aynı betik aynı GLB'leri byte byte üretir. `--only` yalnız seçilen parçaları yeniden yazar, `sockets.json` içindeki diğer kayıtlar korunur. Kum havuzunda TEMP yazılamıyorsa `KIT_TMP=<klasör>` verin; glTF dışa aktarıcısı geçici dosyasını oraya yazar. Yeni malzeme seti (kerpiç-taş, çelik, beton, cam) eklemek için aynı parça işlevleri kullanılır; doku şeritleri ve `WOOD` renkleri değiştirilip ayrı bir klasöre yazılır.
