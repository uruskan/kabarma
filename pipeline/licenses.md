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

## Zincir istasyonları (M2, yer tutucu)

Araştırma Masası, Kömür Ocağı, Demirci Ocağı, Bıçkı, Değirmen, Dokuma Tezgâhı, Kasap Tezgâhı, Kuluçka, Araç Atölyesi, Oyma İskelesi: **koddan** (`game/scripts/world/Station.Visuals.cs`, `MeshKit` yastık kutu ve silindir). Üçüncü taraf içerik yok, kredi harcanmadı. Gerçek model gelince `placeables.json` `model` alanı yer tutucunun yerini alır.