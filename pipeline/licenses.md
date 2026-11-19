# Ses varlıklarının kaynak ve lisans kaydı

URETIM §9 "Servis şartları": bütün varlıklar ücretli planlarda üretilir (ticari kullanım hakkı için). Her servis için üretim tarihi ve plan kaydı burada tutulur. Bu dosya ses varlıklarını kapsar (ses akışı, M1). Model ve görseller kendi akışlarında eklenir.

Her üretimden sonra bu tabloya bir satır eklenir. `pipeline/eleven.py` her çağrıyı ayrıca `progress/m1/ses/eleven_ledger.json` defterine yazar (tür, miktar, tarih, çıktı yolu). Anahtar hiçbir yere yazılmaz.

## Servisler ve şartları

| Kaynak | Ne üretti | Plan | Ticari kullanım | Yükümlülük | Doğrulama |
|---|---|---|---|---|---|
| Genex (`@genex-ai/cli-demo` 1.43.4, ses kanalı) | M0'ın 12 sesi ve 1 Kabarma müziği | Sahibin Genex hesabı, kredili (1 kredi = 0,01 $) | Genex aracının kendi belgesi: indirilen dosyalar "yours to ship" (`.claude/skills/genex-tool-audio/SKILL.md`) | Genex'in arkasındaki ses sağlayıcısının şartları Genex sözleşmesinde. Belgede ayrıntı yok | ⚠️ Genex kullanım şartlarının ses bölümü okunmadı. Çıkıştan önce okunacak |
| ElevenLabs API (doğrudan, sahibin anahtarı) | M1: 55 ses efekti × 3 sürüm, 2 müzik (çayır gündüzü; Kabarma ve 3 katmanı), Suna'nın sesi (tasarım) ve 30 TR + 30 EN replik | Üretim günü hangi plandaysa o satıra yazılır. URETIM §9'a göre ücretli plan | Ücretli planlarda ticari kullanım. Müzik Free–Pro planlarda "bireysel kullanım" diye geçiyor (URETIM §2.1 madde 3) | **Müzik:** oyun geliri 500.000 $'ı (ya da geliştiricinin yıllık oyun geliri 1.000.000 $'ı) aşınca 60 gün içinde stüdyo planına geçilir; eşik çıkış günü kontrol listesinde (URETIM §9) | ⚠️ Şart metni ElevenLabs'in şartlar sayfasından üretim günü yeniden okunup tarihiyle buraya yazılacak |
| `pipeline/synth_ui.py` (kendi kodumuz, Python standart kütüphanesi) | 5 arayüz sesi × 3 sürüm: `ui_odak`, `ui_onay`, `ui_hata`, `ui_baslik_carki`, `telsiz_mandali` | Yok (kredi yok) | Projenin kendi eseri; üçüncü taraf örnek ya da kayıt kullanılmadı | Yok | ✅ Yöntem sahibin Oduncu projesindeki `tools/sfx/gen_sfx.py` ile aynı; kod Kabarma için yeniden yazıldı |
| ffmpeg 9.0.2 (gyan.dev derlemesi) | Kırpma, düzey, kodlama (libmp3lame), ölçüm | — | Araç olarak kullanılır, oyunla dağıtılmaz. Çıktı dosyaları ffmpeg lisansına bağlı değildir | Yok | — |

## Üretim kayıtları

| Tarih | Kaynak ve plan | Dosyalar | Miktar | Not |
|---|---|---|---|---|
| 17 Kasım 2026 | Genex (MVP) | `game/assets/audio/sfx/{beam_loop,pickup,place,till,water,swell,pop,nailgun,hit,levelup,siren,rocket}.mp3`, `game/assets/audio/music/kabarma_night.mp3` | 51 Genex kredisi (12 × 2 + 27) | Ayrıntı ve üretim kimlikleri: `game/assets/audio/README.md`. M1'de bu 12 ses `audio.json`'da `m0` yedeği; yerlerine 3'er ElevenLabs sürümü gelecek |
| 17 Kasım 2026 | `pipeline/synth_ui.py` (kredi yok) | `game/assets/audio/sfx/{ui_odak,ui_onay,ui_hata,ui_baslik_carki,telsiz_mandali}_{1,2,3}.mp3` | 15 dosya | Betikli denetimden geçti (`progress/m1/ses/audio_qc.md`). Dinleme onayı sahipte; onaylanana dek `placeholder: true` |
| 18 Kasım 2026, 00:28–00:34 | ElevenLabs API, `eleven_text_to_sound_v3`. Plan adı: ⚠️ sahipten bekleniyor | `game/assets/audio/sfx/{id}_{1,2,3}.mp3`, 55 ses (arayüz grubu hariç) | 165 üretim, 329,7 sn (defter) | Ana oturum koştu. İşleme (18 Kasım, kredisiz): 2 tepe indirildi, 2 döngü çapraz geçişle onarıldı; hepsi betikli denetimden geçti, `placeholder: false`. Dinleme sahipte |
| 18 Kasım 2026 | ElevenLabs API, `music_v2_5` + katman ayrıştırma. Plan adı: ⚠️ sahipten bekleniyor | `game/assets/audio/music/cayir_gunduz.mp3` (180 sn), `kabarma_full.mp3` (120 sn), `kabarma_{1,2,3}.mp3` (ayrıştırma) | 420 sn (180 + 120 + ayrıştırma 120) / 750 | Kökler: bass, drums, other sesli; guitar, piano, vocals boş. Katmanlar kredisiz yeniden toplandı: 1 bas, 2 davul, 3 other; köklerin baştaki 1.152 örneklik gecikmesi kırpıldı (RAPOR (e)). `cayir_gunduz` sondaki sessizlik kırpıldı, `kabarma_full` tepe indirildi. Müzik şartı: Free–Pro'da "bireysel kullanım", plan adı önemli |
| 18 Kasım 2026 | ElevenLabs API, `eleven_ttv_v3` (ses tasarımı). Plan adı: ⚠️ sahipten bekleniyor | `progress/m1/ses/suna_adaylari/aday_{1,2,3}.mp3` | 1 çağrı, 142 karakter | Suna'nın 3 aday sesi; sahip seçecek, sonra `--only save-voice`. Replikler (30 TR + 30 EN) henüz üretilmedi |

## Koddan yaratık gövdeleri (yaratik2, 18 Kasım 2026)

`game/data/bodies.json` → `game/scripts/actors/CreatureBody.cs`: Homur, Sinsi, Dallı, Bozuk, Hu, Vıcık, Fındıkçı gövdeleri; Çekirge ve Vızvız sürü üyeleri; Vızvız kovanı (10 model). Projenin kendi kodu, Godot ilkel mesh'lerinden kurulur; üçüncü taraf model, doku ya da `F:\Assets` kullanılmadı, kredi yok. Hepsi işaretli yer tutucu (`placeholder` alanı); Tripo üretim isteği `pipeline/requests/yaratik2.json`.

## AI beyanı (URETIM §9)

Steam içerik anketinde: ses efektleri, müzik ve Suna'nın seslendirmesi önceden üretilmiştir (Genex, ElevenLabs). Arayüz sesleri kodla sentezlenmiştir. Oyun sırasında çalışan üretken model yoktur.

## Sahibin varlık kütüphanesinden alınan modeller (F:\Assets)

gorsel-model akışı, 18 Kasım 2026. Kaynak dosyalar yalnızca okundu; `F:\Assets` içinde hiçbir şey değiştirilmedi. `pipeline/lib_import.py` seçili nesneyi tek mesh yapar, sadeleştirir, ölçekler, tabanı y=0'a koyar, dokuları 1024 px'e küçültür ve GLB yazar. `.blend.zip` kaynakları önce oturumun geçici klasörüne açıldı.

| Oyundaki dosya | Kaynak (F:\Assets) | Lisans | Yükümlülük | Not |
|---|---|---|---|---|
| `game/assets/models/lib/kaya/kaya.glb` (kazılan taş kayası, `biomes.json` nodes `kaya`) | `boulder_01_4k.blend.zip` → `boulder_01_LOD2` (Poly Haven "Boulder 01") | CC0 1.0 | Yok (atıf gerekmez) | 5.500 üçgen, 1,1 m; diff/nor_gl/rough 1K |
| `game/assets/models/lib/kaya_dik/kaya_dik.glb` (süs, dik kaya) | aynı `boulder_01_LOD2`, dik çevrilmiş | CC0 1.0 | Yok | 4.500 üçgen, 2,4 m |
| `game/assets/models/lib/kaya_duz/kaya_duz.glb` (süs, düz kaya) | `rock_moss_set_02_4k.gltf` → `rock_moss_set_02_rock10` (Poly Haven "Rock Moss Set 02") | CC0 1.0 | Yok | 3.500 üçgen, 0,7 m |

Denenip **kullanılmayanlar:** `SourceWorld/nodes/stone_node.glb` ve `metal_node.glb` (PROVENANCE: sahibin kendi yaptığı ResourceNodes; lisans sorunu yok). Çakıl yığını yapısı sadeleştirmede kırıklı parçalara dönüştü; ayrıca biçimleri Rust'ın kaynak düğümlerine çok benziyor. PSG Furnace, Workbench ve StarterKit parçaları aynı benzerlik nedeniyle alınmadı.

⚠️ **Lisansı doğrulanmamış olanlar (kullanılmadı):** `SourceWorld/rocks/*` ve `SourceWorld/vegetation/dwarf_birch_*`. Bunlar Fab Megascans; PROVENANCE yalnız "owned_in_local_fab_catalog" diyor. Fab Standard License metni okunmadı. `SourceWorld/birch/five_birch_trees_pack_lowpoly_lods.glb` CC-BY 4.0 (LOLIPOP, Fab): kullanılırsa oyun içi künyede atıf gerekir.

## Doku kaynakları (arazi)

Sahibin varlık kitaplığından (`F:\Assets`, yalnız okundu) kopyalanıp `pipeline/import_terrain_scans.py` ile 2K/1K paketli dokuya çevrildi. Çıktı: `game/assets/textures/terrain/`.

| Katman | Kaynak klasör | Lisans | Not |
|---|---|---|---|
| `cimen` (2K) | `F:\Assets\leafy_grass_4k` (Poly Haven, Charlotte Baglioni) | CC0 1.0 | Klasördeki json lisansı yazıyor |
| `toprak` (2K), `tarla` (1K, üstüne koddan eğatlar) | `F:\Assets\forest_floor_wgyhdgy_4k` (Megascans) | Fab Standart Lisans (Megascans) | Klasörde lisans alanı yok; ana oturumun bildirdiği lisans. Sahip doğrulamalı |
| `kaya` (2K) | `F:\Assets\cracked_nordic_coastal_rock_ukohaj0mw_4k` (Megascans) | Fab Standart Lisans (Megascans) | Aynı not |
| `kum` (1K) | `F:\Assets\beach_sand_uinladeo_4k` (Megascans) | Fab Standart Lisans (Megascans) | Aynı not |

## Hayvan akışı (M2) yer tutucuları

Hepsi koddan, kredisiz ve lisans sorunu yok. Gerçekleri yaratik2 akışının aktörleri ve modelleri; onlar gelince yer tutucular kalkar.

| Varlık | Kaynak | Lisans | Not |
|---|---|---|---|
| Vızvız gövdesi ve modeli olmayan tür | `game/scripts/husbandry/TameablePlaceholder.cs` (MeshKit küre/yastık) | Proje kodu | YER TUTUCU; diğer türler yaratik2'nin WildCreature aktörüyle gelir |
| Suluk, Kaşınma Fırçası, Arı Kovanı | `pipeline/hayvan_props_kit.py` (Blender, yapı kitinin kalas dili, ortak ahşap atlası) | Proje kodu | Kredisiz; 396–660 üçgen |
| Gübre yığını | `RanchSystem.PilesSync` (MeshKit küre) | Proje kodu | YER TUTUCU |
| Savaş Ağılı Bayrağı, Ahır Zili, Bağlama Direği, Eyerlik (M3) | `pipeline/hayvan_m3_kit.py` (Blender, yapı kitinin kalas dili, ortak ahşap atlası) | Proje kodu | Kredisiz; 264–440 üçgen |

## Model yer tutucuları (dunya2, M2)

| Tarih | Kaynak | Dosyalar | Not |
|---|---|---|---|
| 18 Kasım 2026 | `pipeline/gen_kesif.py` (kendi kodumuz, Blender 5.3 ilkel parçalar; kredi yok, üçüncü taraf varlık yok) | `game/assets/models/koru/{koru_agac,dev_mantar,kutuk,demir,recine,guhercile}.glb`, `game/assets/models/kesif/{seyir_kulesi,yabani_bahce,yuva,enkaz}.glb`, `game/assets/models/placeables/mancinik.glb` | Dokusuz düz renk; `biomes.json`'da `placeholder` işaretli. `F:\Assets`'teki orman varlıkları (Polyhaven, Fab köknar paketleri) fotogerçekçi olduğu için stile uymadı, kullanılmadı. Yerlerine Tripo üretim isteği raporda |
| 18 Kasım 2026 (oyuk, M3) | `pipeline/gen_kesif.py` `oyuk_agzi` ve koddan (`game/scripts/world/OyukRoom.cs`, `OyukGuardians.cs`, MeshKit) | `game/assets/models/kesif/oyuk_agzi.glb`; Oyuk odası, delikler, tıkaç kayası, sandık, kandil, Köstebek Bey, Yarasa Yumağı | Projenin kendi eseri, kredi yok. **Yer tutucu** (`oyuk.json` ve `biomes.json` `placeholder`) |

## Yapı kiti malzemeleri: Kerpiç-Taş, Çelik Sac, Mayalı Beton (M2, 18 Kasım 2026)

`pipeline/build_kit_materials.py` kodla üretir; kredi harcanmadı (Genex, Tripo, ElevenLabs yok). Geometri ahşap kitinden (`pipeline/build_kit.py`, değiştirilmedi) modül olarak alınır; 28 parçanın sınırları, yuvaları, çarpışma kutuları ve üçgen sayıları ahşap `sockets.json` ile betikte birebir denetlenir. Doku yapısı (tuğla, blok, kiremit, oluk, perçin, panel, derz) numpy ile yordamsal. Yüzey tanesi ve pas deseni aşağıdaki taramalardan gelir: `F:\Assets`'ten yalnız okundu, 1024'lük atlasa küçültüldü ve yüksek geçiren ayrıntı olarak palet renklerinin üstüne karıştırıldı. Çıktı: `game/assets/models/build/{kerpic_tas,celik_sac,mayali_beton}/`. **Yer tutucu:** dokular sahip ve görsel akış onaylayana dek son kalite sayılmaz.

| Malzeme | Kaynak (F:\Assets) | Lisans | Not |
|---|---|---|---|
| Kerpiç-Taş (taş blok ve kerpiç tanesi, kiremit) | `PSG_Compound_Utilities_R02\Textures\Source\rock_boulder_dry\rock_boulder_dry_diff_4k.jpg` (Poly Haven "Rock Boulder Dry") | CC0 1.0 | Lisans: aynı paketin `Reports\Texture_sources.json` dosyası (polyhaven.com/a/rock_boulder_dry) |
| Çelik Sac (duvar sacı ve kiriş boyası, aşınma) | `PSG_Compound_Utilities_R02\Textures\Source\blue_metal_plate\blue_metal_plate_diff_4k.jpg` (Poly Haven "Blue Metal Plate") | CC0 1.0 | Aynı kaynak json |
| Çelik Sac (çatı sacı pas ve kir deseni) | `PSG_Compound_Utilities_R02\Textures\Source\worn_corrugated_iron\worn_corrugated_iron_diff_4k.jpg` (Poly Haven "Worn Corrugated Iron") | CC0 1.0 | Aynı kaynak json |
| Mayalı Beton (beton yüzey tanesi) | `PSG_Compound_Utilities_R01\Textures\Source\concrete_wall_001\concrete_wall_001_diff_4k.jpg` (Poly Haven "Concrete Wall 001") | CC0 1.0 | Lisans: `PSG_Compound_Utilities_R01\Reports\Texture_sources.json` |

Demir karoları (cıvata, kayış) ahşap kitinin yordamsal `metal_tile` işlevinden gelir. Megascans ve lisansı yazılı olmayan klasörler kullanılmadı.

## Atalar: koddan yer tutucular (boss akışı, M2)

18 Kasım 2026. Kredi harcanmadı; dış kaynak kullanılmadı. Hepsi `atalar.json` içinde `placeholder` alanıyla işaretli (URETIM §3).

| Oyundaki varlık | Kaynak | Lisans | Not |
|---|---|---|---|
| Koca Pofuduk (`game/scripts/boss/KocaPofuduk.cs`) | Projenin `pofuduk.glb` modeli (M0, Tripo; iskelet ve klipler) Ata boyuna ölçeklenir, üstüne koddan süs (yün taç, bakır çıngıraklı tasma, ışıyan göz, yün zırhı) | Pofuduk satırıyla aynı (Tripo, sahibin hesabı) | Üretim isteği: Ata sınıfı model (30.000 üçgen), dört ayak iskeleti, 6 klip |
| Boynuzlu Dede (`game/scripts/boss/BoynuzluDede.cs`) | Koddan parça model (`MeshKit`: gövde, bacaklar, boyun, baş, sakal, dallı boynuz, fidanlar, iki kovan) | Projenin kendi eseri | Üretim isteği: Ata sınıfı model, dört ayak iskeleti, 6 klip |
| Ata Taşı ve sunak, arena sütunları, yardımcılar (Yün Yumağı = ölçekli Pofuduk; Koru Bekçisi = koddan fındık), boynuz parçası, yün yumağı, kök halkası (`game/scripts/boss/`) | Koddan (`MeshKit`, Godot ilkel şekilleri) | Projenin kendi eseri | Ata Taşı ICERIK §17 keşif noktası modeli olarak yeniden üretilebilir |
| Ata sesleri | `audio.json`'daki mevcut sesler (sisme, kabarik_gumleme, firlatma, kaya_kemirme, parca_darbe, kabarmis_patlama, gokyuzu_dalga ...) düşük perdeyle | Yukarıdaki ElevenLabs satırı | Üretim isteği: her Ata'ya kendi teması (SANAT-SES §5 "Atalar 6") ve 6–8 kendi sesi |

## Zincir istasyonları (M2, yer tutucu)

Araştırma Masası, Kömür Ocağı, Demirci Ocağı, Bıçkı, Değirmen, Dokuma Tezgâhı, Kasap Tezgâhı, Kuluçka, Araç Atölyesi, Oyma İskelesi: **koddan** (`game/scripts/world/Station.Visuals.cs`, `MeshKit` yastık kutu ve silindir). Üçüncü taraf içerik yok, kredi harcanmadı. Gerçek model gelince `placeables.json` `model` alanı yer tutucunun yerini alır.

## tarim (M2): ekin ve Korkuluk yer tutuculari

| Varlik | Kaynak | Lisans | Not |
|---|---|---|---|
| Cayir ve Koru'nun 10 ekini (Bugday, Kabak, Aycicegi, Lifotu, Domates, Sapkali Mantar, Bogurtlen, Eksilik, Findik, Sarimsak) | Koddan: `game/scripts/world/FarmView.Meshes.cs` (`CropVisuals`, MeshKit ilkelleri) | Projenin kendi kodu | YER TUTUCU; uretim istegi: ekin basina 1 Tripo modeli |
| Korkuluk | Koddan: `KorkulukVisual` (ayni dosya) | Projenin kendi kodu | YER TUTUCU |
| Alaca gorunusu | Toon golgelendiricinin `tint` degeri | Projenin kendi kodu | Benekli, Altin, Aurora |
## Güç ve sinyal parçaları (guc akışı, M2)

Rüzgâr Gülü, Hayvan Çarkı, Direk, Şalter, Elektrik Lambası, Düğme, Basınç Plakası ve kablolar koddan kurulur (`game/scripts/power/PowerVisuals.cs`, MeshKit; **yer tutucu**). Dış kaynak, kredi ve lisans yok. Altın varlık gelince `placeables.json` satırına `model` yazılır ve `PlaceableVisuals.Make` içindeki guc satırı kaldırılır. Akü Mk1'in görünüşü de koddan.

## Savunma yapıları (M3, yer tutucu)

Çivi Kulesi, Sapan Kulesi, Saçma Kulesi, Diken Tarlası, Yapışkan Zemin, Fırlatma Pedi ve Çukur Kapağı (`game/assets/models/defense/*.glb`) **koddan** üretildi: `pipeline/build_defense.py` (Blender, yapı kitinin `build_kit.py` kalas dili ve projenin kendi ortak ahşap atlası). Üçüncü taraf içerik yok, `F:\Assets` dokusu kullanılmadı, kredi harcanmadı. **Yer tutucu**; `defense.json` içinde `placeholder: true`.

## Kabarma kartları (kartlar akışı, M3, yer tutucu)

72 kartın (60 kart, 12 aşı) ikonu yok.

- Arayüz kart sınıfının rengini şerit olarak çizer (Hud.Cards.cs): Çapa, Yörünge, Beden, Çiftlik, Risk, Aşı.
- Yörünge silahları ve patlamalar Fx.Burst parçacığıyla çizilir; kodla yapıldı, kredi harcanmadı.
- Üretim isteği: 72 kart ikonu, Genex image ya da Tripo web. Sınıf başına tek stil.
## Bitkiler (bitki-pbr, 18 Kasım 2026)

Ağaçlar ve süs bitkileri, çizgi film Tripo modellerinin yerine dokulu (PBR) kütüphane modelleri. `F:\Assets` yalnız okundu; zip'ler ana kopyanın `.tools/bitki_src/` klasörüne (git dışı) açıldı. Dönüşüm `pipeline/build_bitki.py` (Blender 5.3, ayarlar `pipeline/bitki_manifest.json`), kayıt `pipeline/bitki_sync.py`. Kredi harcanmadı. Poly Haven lisansları 18 Kasım'da varlık sayfalarından doğrulandı (hepsi CC0; polyhaven.com/license).

| Oyundaki dosya | Kaynak (F:\Assets) | Lisans | Yükümlülük | Not |
|---|---|---|---|---|
| `game/assets/models/agac/agac*.glb` (çayır ağacı) | `verdant_trail.zip` → `jacaranda_tree_trunk`, `_branches` ve yaprak kümeleri (Poly Haven "Jacaranda Tree", Rico Cilliers, Rob Tuytel) | CC0 1.0 | Yok | Yaprak kümeleri karta pişirildi; 8.000 / 3.800 / 1.400 üçgen. Arşivde lisans dosyası yok; model Poly Haven kataloğunda |
| `game/assets/models/turuncu_agac/turuncu_agac*.glb` | `verdant_trail.zip` → `island_tree_01_a` (Poly Haven "Island Tree 01", Rico Cilliers, Rob Tuytel) | CC0 1.0 | Yok | Yaprak tonu sonbahar turuncusuna kaydırıldı |
| `game/assets/models/cicek/cicek*.glb` | `dandelion_01_4k.blend.zip` (Poly Haven "Dandelion 01") | CC0 1.0 | Yok | Üç bitkilik öbek |
| `game/assets/models/kutuk/kutuk*.glb` (oyunda değil; Sis Korusu için hazır) | `tree_stump_02_4k.blend.zip` (Poly Haven "Tree Stump 02", Rob Tuytel) | CC0 1.0 | Yok | |
| `game/assets/models/cali/cali*.glb` | `Fab\Quixel-Megascans\Plants\Downy-Willow-…\downy_willow_wknlfiuqx_high.zip` → VarF LOD2 (Megascans "Downy Willow") | Fab Standart Lisans (Megascans) | Ham kaynak dağıtılamaz; türev oyunda kullanılabilir | ⚠️ Sahip doğrulamalı (PROVENANCE: "owned_in_local_fab_catalog") |
| `game/assets/models/egrelti/egrelti*.glb` | `SourceWorld\vegetation\fern_a.glb` (Megascans "Beech Fern" VarA LOD2) | Fab Standart Lisans (Megascans) | Aynı | ⚠️ Sahip doğrulamalı |
| `game/assets/models/ot/ot*.glb` (Lif tutamı) | `cotton_grass_wlykecdia_ue_high` → VarA + VarF LOD1 (Megascans "Cotton Grass") | Fab Standart Lisans (Megascans) | Aynı | ⚠️ Sahip doğrulamalı |
| `game/assets/models/bugday/bugday*.glb` (yabani buğday) | `dry_grass_tbbqejqr_ue_high` → VarA + VarB LOD1 (Megascans "Dry Grass") | Fab Standart Lisans (Megascans) | Aynı | ⚠️ Sahip doğrulamalı; altın tona çekildi |
| `game/assets/models/koknar/koknar*.glb`, `koknar_genc/koknar_genc*.glb` (oyunda değil; Sis Korusu için hazır) | `SourceWorld\vegetation\fir_01.glb`, `fir_04.glb` (Fab "Fir trees pack (9 models + LODs and billboards)", `christmass_trees_pack.blend`) | Fab Standart Lisans (Megascans değil) | Aynı | ⚠️ Sahip doğrulamalı (PROVENANCE: Fab sahiplik denetimi geçti) |

## Tripo ağaçları (bitki-pbr, 18 Kasım 2026)

Sahip kütüphane ağaçlarını beğenmedi; ana oturum Tripo web (H3.1, sahibin hesabı, web havuzu) ile stilize yarı gerçekçi ağaçlar üretti. Ham GLB'ler ana kopyanın `.tools/tripo_web/agaclar/` klasöründe (git dışı, salt okunur). Dönüşüm `pipeline/build_bitki.py` `tripo_tree`: yaprak/odun renge göre ayrılır, odun seyreltilip yeniden pişirilir, yaprak kümeleri karta pişirilir. Kredi ana oturumun kaydında; bu akış kredi harcamadı.

| Oyundaki dosya | Kaynak | Lisans | Not |
|---|---|---|---|
| `game/assets/models/agac/agac*.glb` | `kabarma_agac_mese.glb` (meşe) | Tripo, sahibin hesabı (üretilen varlık sahibin) | Çayır ağacı |
| `game/assets/models/agac_hus/agac_hus*.glb` | `kabarma_agac_hus.glb` (huş) | Aynı | Çayır ikinci ağaç; yaprak tonu meşeye yaklaştırıldı |
| `game/assets/models/turuncu_agac/turuncu_agac*.glb` | `kabarma_turuncu_agac.glb` | Aynı | |
| `game/assets/models/cali/cali*.glb` | `kabarma_cali.glb` | Aynı | Biçme Lif tutamı |
| `game/assets/models/koru/koru_agac*.glb` | `kabarma_koru_agac.glb` | Aynı | Sis Korusu; camgöbeği yaprak seçici ton kaydırmayla orman yeşiline (ICERIK §2) |
| `game/assets/models/koru_ladin/koru_ladin*.glb` | `kabarma_koru_ladin.glb` (uzun ladin) | Aynı | Sis Korusu ikinci ağaç |

### Dalga 1 statik modeller (Tripo web, 18 Kasım 2026)
Ham GLB'ler `.tools/tripo_web/dalga1/` (git dışı). Dönüşüm `build_bitki.py` `tripo_prop`: kaynaklar seyreltilir, yeni UV açılır, doku yüksek poligondan pişirilir (1K). Lisans: Tripo, sahibin hesabı (üretilen varlık sahibin).

| Oyundaki dosya | Kaynak | Not |
|---|---|---|
| `game/assets/models/koru/dev_mantar*.glb` | `kabarma_dev_mantar.glb` | 2,5 m |
| `game/assets/models/koru/kutuk*.glb` | `kabarma_kutuk.glb` | 3 m uzun |
| `game/assets/models/koru/recine.glb` | `kabarma_recine.glb` | 0,9 m |
| `game/assets/models/koru/guhercile.glb` | `kabarma_guhercile.glb` | 0,8 m |
| `game/assets/models/koru/demir.glb` | `kabarma_demir.glb` | 1 m |
| `game/assets/models/kesif/yuva.glb` | `kabarma_yuva.glb` | 1 m çap |
| `game/assets/models/kesif/enkaz.glb` | `kabarma_enkaz.glb` | 2,5 m |
| `game/assets/models/kesif/oyuk_agzi.glb` | `kabarma_oyuk_agzi.glb` | 4 m |

## Istasyon yer tutuculari (zincir4, M4)
| 19 Kasim 2026 | 25 T3-T6 istasyon ve 3 T1 istasyon (Sirt Yazicisi, Tabaklama Askisi, Harman Yeri) | `game/scripts/world/Station.Visuals4.cs` (MeshKit yastik kutu, silindir, kure) | Proje kodu | YER TUTUCU; kredi harcanmadi, Tripo istemleri raporda |
