# dunya akışı: M1 raporu

**(a) Dal:** `claude/m1-dunya-a5c492`. Taban `af67734` (dal `0c22fab`'dan kesilmişti; boşken `git merge --ff-only af67734` ile ileri sarıldı). Commit'ler: `3c64482` (dünya, göl, gökyüzü, 9 süs modeli, gerçek kaynaklar, dünya sınaması) + bu raporla gelen son commit (yer imleri, görüntü ve ölçüm araçları, ince ayar). Push yok, main'e birleştirme yok.

**Doğrulama:** `tools/test.ps1 -BotTimeout 900` **PASS** (build 0 uyarı, Sim 21/21, bot `RESULT ok ... harvests=4`, buildtest 60/0, world ok), günlük `progress/m1/dunya/test.log`.

**Teslim:** "Arazi, gökyüzü, sis, su; 8 bitki, 4 kaya" + "16 ekran görüntüsü (4 yer imi × 4 gün saati)" + "RTX 2070S'te 1080p yüksek ayarda ≥ 90 FPS" (URETIM §6 M1, M1 kararı 7; SANAT-SES §2, §6, §9; TEKNIK §10).

## Ne yapıldı

| Kalem | Durum | Kanıt |
|---|---|---|
| Dünya 300 × 300 m | ✅ Üssün 42 m'si MVP ile birebir aynı (fark < 0,001 m); yuvarlak tepeler, en yüksek tepe (33,3 m), kenar sırtı, ufukta 3 tepe katmanı | `--test=world`, `biomes.json` `world.terrain` |
| Tarla eğimi < 15° | ✅ Ocak'ın 12 m'si: en çok 0,0° | `--test=world` |
| Göl Ocak'a 60–90 m | ✅ 445 su hücresi, Ocak'a 63,5–87,2 m, 0,90 m sığ (oyuncu içinde yürür) | `--test=world`, `BiomesDataTests` |
| Su yüzeyi shader'ı | ✅ derinliğe göre renk, kırık köpük çizgisi, dalgacık; gün saatine göre koyulaşır; güneş parıltısı saf beyaza çıkmıyor | `shaders/water.gdshader`, görüntüler |
| Gökyüzü | ✅ palet bantları, güneş, iki ay (SANAT-SES §6), yıldız, stilize bulut, Kabarma'da Maya aurorası, krater ışık sütunu | `shaders/sky_kabarma.gdshader` |
| 4 gün saati + normal gece | ✅ veride (`biomes.json` `times`, SANAT-SES §2 tablosunun sayıları birebir); `SetDay/SetNight/SetDawn` korundu, `SetNormalNight`, `ForceTime` eklendi | `BiomesDataTests`, `--test=world` |
| Sis | ✅ mesafe sisi, gün saatine göre renk ve yoğunluk (sabah öğlenin 1,5 katı); sis katmanları ufuktaki 3 tepe halkasıyla (240/340/460 m, giderek sis rengine). Yükseklik sisi (`world.heightFog`) çok hafif: Godot'da mesafeden bağımsız olduğu için vadiye konan sis üssü de bulandırıyor | görüntüler, `BiomesDataTests` |
| 8 bitki | ✅ çayır ağacı + Şalgam (altın) + turuncu ağaç, çalı, ot, yabani buğday, çiçek, eğrelti (Tripo modelleri `pipeline/clean_asset.py` ile içe alındı, git'te) | `game/assets/models/<ad>/`, aşağıdaki ölçüt tablosu |
| 4 kaya | ✅ cevherli kaya (altın) + düz kaya, dik kaya, bakır damarı | aynı |
| Rüzgâr salınımı | ✅ bitki shader'ı (dokuyu korur; ağaçta yalnız taç), ot tutamları (40 m içinde, kenarda söner) | `shaders/plant_wind.gdshader`, `grass.gdshader`, `pipeline/clip_gif.py` → `clip_gol.gif` |
| Kil yatakları | ✅ göl kıyısında 4 yatak (8 birim), koddan yumru (kredi yok) | `Rock.ClayBed`, `--test=world` |
| Bakır damarı | ✅ `bakir` modeliyle 3 damar (8 birim), üssün ~35 m doğusunda | `biomes.json` `nodes` |
| Lif ve Buğday | ✅ ot ve çalı → Lif, yabani buğday → Buğday (tutam başına 4); geri gelmez; üssün 25 m'sinde kümeler + çayıra dağılmış (379 Lif, 74 Buğday tutamı) | `World.Resources.cs`, `--test=world` |
| `World.Placeholders.cs` | ✅ silindi; `World.Tuft` / `NearestTuft` / `TakeTuft` ve `Rock.Item` arayüzü aynı | bot `harvests=4` taban ile aynı |
| `biomes.json` + veri ailesi | ✅ `GameData.Biomes.cs`, `GameData.Graphics.cs` (`graphics.json`); bilinmeyen anahtar testi otomatik | `DataCheckTests`, `BiomesDataTests` (5 test) |
| 4 yer imi + 16 görüntü aracı | ✅ `--test=shots`: bot ilk günü oynar, Kabarma gecesi görüntüleri ilk gecenin 2. dalgasında, gündüz görüntüleri bot bitince; Deck (1280×800) öğle görüntüleri; Üs yer imi Ocak'ın gerçek konumuna bağlı | `progress/m1/dunya/shots/`, `shots/deck/`, `shots/metrics.json` |
| "Yüksek" ön ayar + ölçüm | ✅ `graphics.json` (TEKNIK §10: 1080p, MSAA 2×, gölge 2 bölme 4096, SSAO yarım, glow, SDFGI/SSIL/SSR/hacimsel sis kapalı); `--test=perf`, `tools/perf_m1.ps1` | `progress/m1/dunya/perf/` |
| Sulama deposu (100 L, su yüzeyinden dolar) | ⏸ yazılmadı: depo sayıları (tüketim, dolma hızı, başlangıç) belgede yok; ana oturuma soruldu, cevap gelmedi | — |

### Süs modelleri: URETIM §3 / SANAT-SES §9 ölçütleri

Betik (1–5) `pipeline/clean_asset.py` raporu (`game/assets/models/<ad>/<ad>.report.json`); 6–8 benim puanım (10 üzerinden, geçme 8), dört ışıkta döner tabla `progress/m1/dunya/models/<ad>/`. Son söz sahibin.

| Model | 1 doku | 2 poligon | 3 kalınlık (≥ 0,06) | 4 bütünlük | 5 ölçek | 6 siluet | 7 dil | 8 okunaklılık | Durum |
|---|---|---|---|---|---|---|---|---|---|
| cali (çalı) | ✓ 1K | 1937/2000 ✓ | 0,012 ✗ | ✗ 20 manifold dışı kenar, 6 ayrık parça | 1,1 m ✓ | 7 (yuvarlak top: kaya ile karışabilir) | 6 (buruşuk kâğıt gibi köşeli yapraklar, parlak; meyve yok) | 7 | yer tutucu, üretim isteği |
| ot | ✓ 1K | 1933/2000 ✓ | 0,023 ✗ | ✗ 43 kenar, 20 parça | 0,9 m ✓ | 8 (tüylü uçlu ot) | 8 | 8 | yer tutucu, üretim isteği |
| bugday (yabani buğday) | ✓ 1K | 1937/2000 ✓ | 0,023 ✗ | ✗ 11 kenar, 8 parça | 1,0 m ✓ | 7 (başaklar turuncu ve iri: çiçek başağına benziyor) | 8 | 7 (Buğday olduğu oyun mesafesinde tam okunmuyor) | yer tutucu, üretim isteği |
| cicek | ✓ 1K | 1930/2000 ✓ | 0,024 ✗ | ✗ 16 kenar, 2 parça | 0,6 m ✓ | 8 | 8 | 8 | yer tutucu, üretim isteği |
| turuncu_agac | ✓ 1K | 4735/7000 ✓ | 0,83 ✓ | ✗ 36 kenar | 4,8 m ✓ | 8 | 8 | 9 | yalnız ölçüt 4 |
| egrelti | ✓ 1K | 1936/2000 ✓ | 0,37 ✓ | ✗ 14 kenar | 0,7 m ✓ | 7 (geniş yapraklı bitki; eğrelti gibi değil) | 8 | 8 | yalnız ölçüt 4 |
| kaya_duz | ✓ 1K | 2866/4000 ✓ | 0,45 ✓ | ✗ 4 kenar | 0,7 m ✓ | 8 | 9 | 8 | yalnız ölçüt 4 |
| kaya_dik | ✓ 1K | 2868/4000 ✓ | 0,40 ✓ | ✗ 9 kenar | 2,4 m ✓ | 9 | 9 | 8 | yalnız ölçüt 4 |
| bakir (bakır damarı) | ✓ 1K | 2788/4000 ✓ | 0,67 ✓ | ✗ 10 kenar | 1,3 m ✓ | 8 | 8 | 7 (bakır topakları oyun mesafesinde küçük) | yalnız ölçüt 4 |

Ölçüt 4: onaylı altın `kaya` modeli de aynı denetimden geçmiyor (13 manifold dışı kenar; `docs/stil/README.md`); Tripo çıktısının dikiş kenarları bu denetimde hep kalıyor. Bu bir hat/denetim sorusu (varlik akışı), yeniden üretimle çözülmez. Hepsi `biomes.json` `placeholder` alanında nedeniyle işaretli (URETIM §3).

### Ölçüm (TEKNIK §10), bu makine: RTX 2070 SUPER, 1920 × 1080, "yüksek", dikey eşitleme kapalı, kare sınırı yok

Ölçüm sırasında başka oturumların 7–13 Godot süreci aynı GPU'da koşuyordu (`other_godot_processes`); sayılar bu yüzden kötümser ve gürültülü.

| Yer imi | Öğle (60 sn): ort. ms / FPS / p95 / çizim | İlk gecenin son dalgası: ort. ms / FPS / p95 / çizim |
|---|---|---|
| kapsul | 9,20 / 108,7 / 12,7 / 213 | 4,36 / 229,5 / 5,3 / 278 |
| us | 8,11 / 123,4 / 11,4 / 216 | 9,87 / 101,3 / 19,7 / 285 |
| gol | 9,23 / 108,4 / 15,0 / 136 | 9,41 / 106,3 / 13,6 / 149 |
| tepe | 6,50 / 153,9 / 13,5 / 248 | 4,95 / 201,8 / 6,9 / 310 |

Ortalamalar 11,1 ms bütçesinin içinde; Üs'te son dalgada p95 19,7 ms (Zıpzıp kalabalığı ve vuruş efektleri). Gece ölçümünde Üs yer imi son ayardan önceki konumdaydı. RTX 2070S hedefi **sahibin onayını bekliyor** (tek başına koşuyla yeniden ölçülmeli: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/perf_m1.ps1`).

## PLAN §7.2: teslim başına altı koşul

**1. Arazi, gökyüzü, sis, su (+ gün saati ışığı, rüzgâr)**
1. Kural ve sayı: **geçti** (URETIM §6 300 m ve 60–90 m göl, GDD 8.1 eğim, SANAT-SES §2 saat tablosu; hepsi `biomes.json` / `graphics.json`). Not: görüntü ince ayar sabitlerinin bir kısmı (glow, SSAO yarıçapı, ufuk tepelerinin boyu, ikinci ayın açısı) MVP'deki gibi kodda.
2. Otomatik test: **geçti** (`BiomesDataTests`, `--test=world` `tools/test.ps1` içinde).
3. Ses, efekt, arayüz: **kısmen** (rüzgâr, dalgacık, köpük var; göl kıyısı ortam sesi `ortam_gol_kiyisi` ses akışının dalında, bu dalda yok).
4. KM ve oyun kolu: **kısmen** (yeni girdi yok; dünya mevcut hareketle gezilir, bot yolu ile sınandı, oyun kolu bu dalda elle sınanmadı); ortak oyun **M2'de**.
5. Kayıt: **M2'de** (dünya tohumdan aynı kurulur; Sim'e yeni durum eklenmedi).
6. Görsel ölçütler: **sahibin onayını bekliyor** (betik kısmı geçti: saf siyah yok, saf beyaz yok, gündüz ışıyan yüzey ≤ %0,05, gökyüzü geçişi var).

**2. 8 bitki, 4 kaya**
1. **geçti** (`biomes.json` `decor`; yer tutucu işaretleri URETIM §3).
2. **kısmen** (betik ölçütleri 1, 2, 5 dokuzunda geçti; 3 dört bitkide, 4 dokuzunda kaldı; tablo yukarıda).
3. **geçti** (rüzgâr salınımı; Biçme ile toplananlar küçülüp kaybolur).
4. **kısmen** / ortak oyun **M2'de** (yukarıdaki gibi).
5. **M2'de**.
6. **sahibin onayını bekliyor** (6–8 puanlarım tabloda).

**3. Gerçek kaynaklar (kil, bakır, Lif, Buğday)**
1. **kısmen**: kural (Kazma/Biçme ile toplanır, geri gelmez) URETIM §6'da; verim sayıları veride ama belgede değer yok, yer tutucu sayılar korundu (soruldu).
2. **geçti** (`--test=world`: sayılar, kıyıda kil, üsse yakın Lif/Buğday, `TakeTuft`; bot `harvests=4` tabanla aynı).
3. **kısmen** (mevcut Çapa vuruş efekti ve sesi; yeni ses yok).
4. **kısmen** / ortak oyun **M2'de**.
5. **M2'de**. Not: tükenen tutam ve kaya durumu (MVP'deki yer tutucu gibi) Godot katmanındaki `World` örneğinde; kayıt için M2'de Sim'e taşınmalı.
6. **sahibin onayını bekliyor**.

**4. Sulama deposu**: 1 **yok** (sayılar belgede yok) · 2 **yok** · 3 **yok** · 4 **yok** · 5 **M2'de** · 6 **yok**. `World.IsWater(p)` hazır; depo kuralı cevap gelince Sim'de yazılır.

**5. 16 ekran görüntüsü**
1. **geçti** (yer imleri `biomes.json` `bookmarks`, SANAT-SES §9 tablosu).
2. **kısmen**: `--test=shots` 16 + 4 görüntü üretip `SHOTS RESULT ok` ile çıkar, `pipeline/shot_metrics.py` betik ölçütlerini yazar; pencereli ve ~6 dk sürdüğü için `tools/test.ps1`'de değil.
3. **geçti** (arayüz açık; Deck çözünürlüğünde okunuyor, iki kuşak etiketi kesik: gecehud).
4. —
5. —
6. **sahibin onayını bekliyor** (öz puanlarım aşağıda).

**6. RTX 2070S'te 1080p yüksek ≥ 90 FPS**
1. **geçti** (`graphics.json`, TEKNIK §10 ile birebir; test denetler).
2. **geçti** (`--test=perf` aracı; ön ayar `BiomesDataTests`'te).
6. FPS: **sahibin onayını bekliyor** (bu makinede ortalama 101–230 FPS, ayrıntı yukarıda).

### 16 görüntü: öz değerlendirme (SANAT-SES §9 ekran görüntüsü ölçütleri)

Görüntüler `progress/m1/dunya/shots/<yer imi>_<saat>.png`, hepsi bir arada `shots/sheet_16.png` (satır: kapsül, üs, göl, tepe; sütun: sabah, öğle, gün batımı, Kabarma gecesi), Deck `shots/deck/`, rüzgâr ve su klibi `clip_gol.gif` (4 sn). Betik ölçütleri (`shots/metrics.json`): 16 görüntüde saf siyah %0, saf beyaz %0, gündüz Maya ışıması ≤ %0,05 (sınır %5), gökyüzünde dikey renk geçişi var. Aşağıdaki puanlar benim (10 üzerinden, geçme 8); karar **sahibin onayını bekliyor**.

| Yer imi | Kadraj (SANAT-SES §9) | Derinlik katmanı | Puanım | Not |
|---|---|---|---|---|
| İniş kapsülü | Kapsül ve kargo rampası ön planda, çayır, ufukta krater ışık sütunu | ön (kapsül, çalı), orta (barınak, ağaçlar), uzak (tepeler) | 8 | Kapsülün rampaya dönüşmesi ekonomi akışında; bu dalda kapsül hâli |
| Üs | Ocak, 3×3 tarla (Şalgamlar), Tezgâh, Taş Fırın, Paketleme, oyuncu, ışık sütunu | ön (ot), orta (Ocak, istasyonlar), uzak (ağaçlar, tepeler) | 7 | **Ağıl ve Gıdak kadrajda yok**: ağıl ilkgün akışında. Botun barınağı solda. Yer imi Ocak'a bağlı (`anchor: "ocak"`) |
| Göl kıyısı | Su ve köpük çizgisi, kil yatakları, ağaçlar, sisli tepeler, ışık sütunu | ön (kil, eğrelti), orta (göl, ağaç), uzak (tepeler) | 8 | En güçlü kare; gece suyu koyu lacivert (siyah değil) |
| Tepe | En yüksek tepeden üsse ve kratere | ön (tepe otu), orta (çayır, ağaçlar), uzak (sis) | 6 gündüz / 8 gece | Gündüz zemin rengi sis rengine çok yakın: uzak katmanlar soluk, "üç sis katmanı" zayıf okunuyor; üs bu uzaklıkta küçük. Ayar önerisi sahibe: tepe kadrajında daha koyu ufuk katmanı ya da daha az mesafe sisi |

Saatler: sabah ile öğle üs ve kapsülde birbirine yakın (sabah biraz daha soluk ve sisli); gün batımı ve Kabarma gecesi belirgin. Kabarma gecesinde aurora, zemindeki Maya damarları ve ışık sütunu kareyi taşıyor; damarlar gece ekranın %0,4–0,7'si. Arayüz 1080p'de okunuyor; Deck'te (1280×800) okunuyor, iki kuşak etiketi kesik (gecehud).

## (c) Başka akışların dosyalarına dokunuş

- `game/scripts/Main.cs` (girdi): `Tests` sözlüğüne 3 satır eklendi (`world`, `shots`, `perf`). Var olan satır değişmedi.
- `tools/test.ps1` (girdi): `world` varsayılan listeye eklendi, Godot bulma koşuluna `world` eklendi, `Invoke-GodotTest 'world'` bloğu. Yalnız satır ekleme.
- `CLAUDE.md` hâlâ `World.Placeholders.cs`'i anıyor; dosya bu dalda silindi (görev buydu). CLAUDE.md'ye dokunmadım.
- Başka akış dosyası değişmedi. `WorldShots` yalnız `Capa.Select`, `Hud.Hint` ve `Director.SpeedScale`'i çağırır.

## (d) Gevşetilen bot adımları

Yok. Bot `RESULT ok credits=210 eggs=1 kills=21 cards=3 maya=48 stations=3 harvests=4` (taban ile aynı). Kil artık göl kıyısında (Ocak'a ~60 m) olduğu için kil arayan botların yürüyüşü uzar; bu dalda kil adımı yok.

## (e) Üretim isteği

| Ne | Adet | Havuz | Tahmin (dry-run) | Onay durumu | Çıktı yolu |
|---|---|---|---|---|---|
| Ot ve yabani buğday yeniden üretimi (tek, kalın, kaynaşık gövde; ölçüt 3) | 2 model | Tripo API | 20 + 20 = 40 kredi | URETIM §6 M1 kararları: "ince saplı ot ve buğday ~80'e kadar" içinde | `.tools/tripo/ot_v2`, `.tools/tripo/bugday_v2` |
| Çalı ve çiçek yeniden üretimi (aynı neden) | 2 model | Tripo API | 20 + 20 = 40 kredi | **onay gerekir** (onaylı satırda yok) | `.tools/tripo/cali_v2`, `.tools/tripo/cicek_v2` |

Komut (ana oturumda): `powershell -NoProfile -ExecutionPolicy Bypass -File pipeline/gen_decor_v2.ps1 -Only ot,bugday -Run`; ardından `pipeline/import_decor.ps1 -Only ot,bugday -Suffix _v2` aynı `game/assets/models/<ad>/<ad>.glb` yoluna yazar, kod değişmez; ölçütten geçerse `biomes.json`'daki `placeholder` satırı silinir. Yine geçemezse SANAT-SES §9 son satırı: en çok dört deneme, sonra koddan kurulur. Diğer beş modelin tek kusuru ölçüt 4 (yukarıda): üretim isteği değil.

## (f) Belgeyle çelişen ya da belgenin sustuğu yerler

1. **Sulama deposu sayıları** (soruldu, cevap yok; kod yazılmadı): 100 L belgede, ama kare başına tüketim, gölden dolma hızı, başlangıç doluluğu yok. Önerim: dolu başlar, ıslatılan kare başına 5 L, su yüzeyinden 25 L/sn, dolarken "water" sesi pes.
2. **Verimler** (soruldu): tutam başına Lif/Buğday, kil ve bakır düğüm birimi, ağaç vuruşu başına odun belgede yok. Yer tutucu sayılar korundu (4 / 8 / `woodPerChop`).
3. **Düz ve dik kaya kazılır mı?** Belge susuyor; süs olarak bıraktım (kazılmaz, çarpışmalı).
4. **Kil yatağı**: belge model mi kod mu demiyor; kredi istemediği için C# yumruları (MeshKit) yazdım, Blender betiği değil.
5. **Yön**: belge kuzeyi tanımlamıyor. Krater +Z (kuzey), güneş doğudan (−X) doğar; azimut 0 kuzey. İlk açılış görüntüsünü bu belirliyor.
6. **İki ay**: SANAT-SES §6 "güneş ve iki ay" diyor, §2 tablosu tek ay konumu veriyor. İkinci ayın konumu birincisine göre sabit açıyla (kodda) çiziliyor.
7. **Üs yer imi**: SANAT-SES §9 "ağıl ve Gıdak" istiyor; ağıl ilkgün akışında, bu dalda yok. Üs görüntüleri ağılsız (kısmen).
8. **`--test=shots`**: CLAUDE.md `--shots=...` argümanını anıyor (MVP aracı); M1 görüntüleri için ayrı test adı ekledim, eskisi değişmedi.
9. **Sulama boş depoyla da püskürtüyor** (capa akışı; depo gelince birleşmede bağlanmalı).
10. **Kuşak etiketleri Deck çözünürlüğünde kesik** ("ılgam Tohum", "ebze Çorbas"): gecehud akışı.
11. **Ölçüt 4 denetimi** onaylı altın varlıkları da düşürüyor: varlik akışı.
12. **Birleşme dikişleri** (paylaşılan notlar `dunya-notes.md` §4): gecehud `ApplyClockLight()` → `SetDay/SetDawn/SetNormalNight`; ses göl kıyısı emitter'ı → `World.LakeCenter` / `ShoreRadius`.
