# Kabarma: İlerleme günlüğü

> URETIM §7: ne doğrulandı, ne kısmen doğrulandı, ne yapılmadı. En yeni kayıt en üstte.
> Durum: ✅ doğrulandı · ⚠️ kısmen / doğrulanmadı · ❌ yok · 👤 sahibin onayını bekliyor.

## Yol haritası (canlı; ana oturum günceller)

Sahibin kararı 24 (18 Kasım 2026): ana oturum M2'den M7'ye sormadan ilerler; akışlar CLAUDE.md "M2 akışları" tablosunda. Sıra URETIM §6: M2 çekirdek sandbox → M3 Kabarma ve savaş → M4 dünya ve içerik → M5 çok oyunculu → M6 cila → M7 çıkış.

### Durum (18 Kasım 2026, öğleden sonra)

- **M1:** main `9dd245a` sonrası. Teslimlerin çoğu main'de (Çapa elde, kemer ve Rust Üretim paneli, İnşa Planı, El Arabası iki elle, Suna'nın sesi, 75 ikon). Açık: `input_kbm`/`input_pad` testleri Taş Fırın ve Çivi adımında kırmızı (girdi); Taş Fırın ve ağaç modelleri (Tripo web, sahibin girişi); El Arabası LOD, çarpışma kabuğu ve rapor eski modeli anlatıyor, üçgen 14.747 > 14.000 (varlik); `kazma_isini_2` ~20 dB sessiz (ses); kapsül rampası pedin 0,4 m üstünde. 👤 his, 30 dk oynama, FPS, Megascans lisansı, ElevenLabs'teki 6 tasarlanmış ses.
- **Dışa aktarma:** Windows .NET şablonları kurulu; `export/Kabarma.zip` (353 MB, `2577bd8`) ayrı klasörde açılıp `--test=camera` 73/73 geçti.
- **M2:** akışların çoğu main'de (bcb5e4a). Kalan: kayit (12 commit dalda), hayvan, tarim; bot/input testleri `hasat` adımında kırmızı (tarim düzeltiyor). Kabul: 4 bot istemciyle senkron (`--test=coop` geçti), kayıt-yükleme farkı 0 (kayit), SimRunner T3 hedefi (ölçülmedi), 👤 sahip ve arkadaşı Steam'de 2 saat.
- **M3:** main'de (b64d3c4): savas, kabarma3, kartlar, savunma, gucler, holding, yolbulma, cip, oyuk, savas-hayvan. Kırmızı: `input_kbm`/`input_pad` (inceleniyor), `--test=world` (M4 biyom ilk iş). KS 10'da 200 düşman ~33 ms: Optimizasyon listesinde.
- **M4:** başladı (18 Kasım gece); akışlar CLAUDE.md "M4 akışları", aynı anda en çok 3, kimlikler `docs/kimlikler-m4.md`.

### M2 akışları

| Akış | Model | Durum |
|---|---|---|
| kayit | Opus high | dalda, main'e alınıyor |
| ortak | Opus high | main'de (2151474): coop ve steam testleri geçti → M3 yolbulma |
| yapi | Opus high | main'de (71e7aa5); bot `yukselt` adımı hasat yüzünden doğrulanmadı → M3 savunma |
| tarim | Opus medium | main'de; bot `hasat` kırığını düzeltiyor |
| hayvan | Opus medium | dalda, bitiyor → sonra M3 savas-hayvan |
| yaratik2 | Opus medium | main'de (68105e2) → M3 kabarma3 |
| boss | Opus medium | main'de (db010d2) → M3 savas |
| guc | Opus medium | main'de (96abc5c) → M3 gucler |
| zincir | Opus medium | main'de → M3 kartlar |
| ekonomi2 | Opus medium | main'de (51399ce) → M3 holding |
| ilerleme | Sonnet 5.5 | main'de (4b1c001) → M3 cip |
| dunya2 | Opus medium | main'de (79c94b1) → M3 oyuk |
| bitki | Opus medium | çizgi film ağaçlar → F:\Assets PBR ağaç ve bitki |
| girdi, varlik (M1) | — | main'de, arşivlendi |

### Kredi kuyruğu (sahibe tek listede sorulur)

| İstek | Hat | Adet | Tahmin | Durum |
|---|---|---|---|---|
| Suna M6 replikleri (ogretici), 12 adet, ElevenLabs, sahibin onayi. Kimlik, metin (TR ve EN) ve ton: `game/data/suna.json` `lines` (ornek: `tut_biyom_sis_korusu`, `tut_otomasyon_ilk`); dosya `assets/audio/voice/{dil}/{kimlik}.mp3` | ElevenLabs | 12 × 2 dil | — | sahibin onayi bekliyor (her kullanimda ayri onay; ses yokken altyazi calisir) |
| Ağaçlar (meşe, huş, turuncu, koru, ladin, çalı) | Tripo web | 7 iş + 1 yeniden doku | 405 | üretildi (18 Kasım). Ham dosyalar `.tools/tripo_web/agaclar/`; bitki oturumu hattan geçirip bağlıyor |
| Dalga 1: dev mantar, kütük, demir, reçine, güherçile, yuva, enkaz, oyuk ağzı | Tripo web | 8 iş + 1 yeniden doku | 460 | üretildi (18 Kasım). Ham dosyalar `.tools/tripo_web/dalga1/`, istemler `istemler.md`; bitki bağlıyor |
| Homur (yaratık denemesi) | Tripo web | 1 iş + iskelet | 55 + ? | model üretildi, iskelet ve yaratik2 doğrulaması bekliyor; öteki 6 tür ve Atalar bundan sonra |
| Taş Fırın, kuleler, tuzaklar, istasyonlar | Tripo web | — | — | sırada. Hareketli parçası olanlar (kule başı, kapak, mancınık kolu) önce parçalama planı ister |

### İstasyon işlevleri: sonraki akışlara bırakılanlar (tasima, 19 Kasım 2026; `automation.json` `functions`, `AutomationTests`)

| İstasyon | İşlev (ICERIK §11) | Sonraki akış | Durum |
|---|---|---|---|
| Kabartma Odası | tek kareyi ya da hayvanı gündüz kabartır (50 Maya, 800 W) | gece4 | ❌ kurulur ve bedeli ödenir, çalışma yok; `Maya` (`MayaVault`) ve Kabarma başlatma (`StartKabarma`) hazır |
| Zeplin Hangarı | Zeplin üretimi | arac | ❌ kurulur, tarifi yok; Zeplin ve eklentiler arac akışında |
| Mutfak, Aşı Tezgâhı | T2–T6 yemek; gen aktarma | tarim4 | ❌ tarim4'ün işi (tarifleri `mutfak`, `asi_tezgahi`) |

Kale (19 Kasım 2026) açıkları: kapı varyantları (Çelik Kapı, Çift Kanat, Garaj, Tavan Kapağı, El Merdiveni) kurulur ama Ahşap Kapı gibi açılmaz (kapı kasası davranışı yok); Gardırop kozmetik akışını bekler (dolap); Cam seti modeli ahşap geometrisinin saydam boyası (yer tutucu); kuleler ve yeni 56 parçanın modelleri koddan yer tutucu (Tripo istemleri ana oturumda); Havan Kulesi kabarık Fındık özelliğini taşımaz (yalnız cephane).

Kabul akışı için açık: `rawSources` `via: pending:biyom` (T3–T6 ham kaynak düğümleri) ve `pending:ata4` (Ana Maya'nın `kabartma_plani`); biyom ve ata4 birleşince bu satırlar silinir, `AutomationTests.Raw_sources_need_an_in_game_producer_or_are_listed_as_pending` listeyi sıfıra indirmeyi bekler. Sondaj Kulesi Zengin Yatak düğümü yerleşene kadar boşta (Sim kuralı sınandı, dünya sınaması yok).

### Optimizasyon listesi (sahibin kararı: en sona; ölçüm ve iyileştirme şimdi yapılmaz)

| Bulgu | Nerede | Not |
|---|---|---|
| KS 10'da 200 düşmanla kare ~33 ms (bütçe üstü) | gece, sürü | Şüphe (ölçülmedi): şişme shader'ı parametreleri her karede her yaratığın kendi materyaline yazılıyor; çizimler toplanamıyor |
| 1800 m dünyada kurulum ~16 → ~40 sn (başsız `--test=world`, makine yoğunken); TEKNIK §10 ≤ 15 sn hedefi | World.cs yükseklik ızgarası (3,2 M hücre), World.Ground denetim dokusu 2048², dekor ve çarpışma gövdeleri | Şüphe (ölçülmedi): ızgara tek iş parçacığında; başsız testlerde yarı çözünürlük ya da yalnız gereken bölge; çarpışma gövdeleri ağaç başına bir düğüm |

## 17 Kasım 2026, akşam: M1 eksik denetimi

Dokuz ayrı denetim, M1 teslim listesinin (URETIM §6 M1) her satırını ve açık M0 denemelerini belgelere karşı kodla, veriyle ve kanıtla karşılaştırdı. Kod değişmedi. Bu kayıt o denetimin özetidir.

### Temel

- Commit `3857464`: önceki çalışanın yarım işi (PlayerRig, ragdoll, odun ve barınak adımları) + Genex CLI kurulumu (`@genex-ai/cli-demo` 1.43.4, URETIM §2.1) + `CLAUDE.md` proje kuralları.
- ✅ Derleme temiz. Sim testleri 11/11. Bot baştan sona `RESULT ok credits=210 eggs=1 kills=21 cards=3 maya=48 stations=3 harvests=4`.
- ✅ Temiz bir worktree'de derleme, 16 sn içe aktarma ve bot `RESULT ok`. Git'e girmeyen `game/assets/genex/` ve `.env` oyun için gerekmiyor.
- Bakiye: Tripo API 320 · Genex 2 (23 Kasım'da 200'e dolar) · Tripo web görülemedi (tarayıcıda oturum yok).

### M0 denemeleri (URETIM §2.3)

| # | Deneme | Durum | Not |
|---|---|---|---|
| 1 | Üç statik varlık | ⚠️ | Ağaç betik ölçütlerinden geçiyor; kaya (13) ve kova (83 manifold dışı kenar) ölçüt 4'te kalıyor. Ölçüt 6–8 puanlanmadı. |
| 2 | Taş Fırın parçaları | ⚠️ | Kapak açılıyor; dosyada `chimney`/`panel` düğümü yok, baca hareketi çalışmıyor. `--rename` ile yeniden işlenecek (0 kredi). |
| 3 | Oyuncu + klipler | ⚠️ | Kol IK kodu var, görüntüyle doğrulanmadı. `player.glb` içindeki 15 klibin 8'i boş (birleştirme hatası); yürürken `crouch_walk` seçiliyor. |
| 4 | Pofuduk, Gıdak | ❌ | Kodla yazılan poz kütüphanesi yok; Gıdak'ın iskeleti yok; Gıdak ve Pofuduk ortak şişme shader'ını kullanmıyor. |
| 5 | 10 ses + 1 döngü | ⚠️ 👤 | 12 dosya var; betikli denetim betiği yok; dinleme turu sahibinde. |
| 6 | 1 müzik, 3 katman | ⚠️ 👤 | Tek katman; katman ayrıştırma denenmedi. |
| 7 | Suna sesi | ❌ 👤 | ElevenLabs her kullanımda sahibin onayını istiyor. |

TEKNIK §1.2'nin sekiz adımı: Steam, ağ senkronu, Blender, sürü ✅ (deneme düzeyinde). Windows dışa aktarımı denenmedi (`export_presets.cfg` yok). 1.000 oyun günü kıyaslaması yok.

### M1 teslim listesi

| Teslim | Durum | Ana eksikler |
|---|---|---|
| 12 altın varlık ve stil föyü | ⚠️ 👤 | 13 rapordan 12'si betik ölçütünde kalıyor; ölçüt 6–8 puanı ve sahibin onayı yok; LOD, çarpışma kabuğu, ikon, varlık kartı adımları hatta yok; El Arabası oyunda kullanılmıyor; yer tutucu işareti `data/` içinde yok. |
| Arazi, gökyüzü, sis, su; 8 bitki, 4 kaya | ⚠️ | Su yok. Dünya 160 m (hedef 300 m). Parası ödenmiş 9 Tripo modeli (6 bitki, 3 kaya) içe aktarılmadı ve yalnızca git dışı `.tools/` içinde. Ay, bulut, yıldız, rüzgâr salınımı, `biomes.json` yok. |
| Oyuncu: 53 klip, IK ve yay, ragdoll, vizör | ⚠️ | 4 klip çalışıyor, 5 ücretli klip boş, 41 klip yok. Ayak IK, gövde dönüşü, çanta salınımı yok. Ragdoll'da Çapa havada kalıyor, kalkış yok. Vizör ifadeleri hiç yok. |
| Çapa: 6 iş başlığı, Tırpan, Çivi; vuruş hissi; F1 paneli | ⚠️ | Vuruş duraklaması kodda yazılanın yaklaşık 10 katı sürüyor (hata). 50 ms gecikme ölçümü yalnız silahlarda ve otomatik değil. Çivi tam otomatik ateşliyor. F1 paneli 4 sürgü, kaydetmiyor. Birçok sayı kodda. |
| Gıdak ve Zıpzıp, normal ve kabarmış | ⚠️ | Poz kütüphanesi yok; Zıpzıp 8 klibin 4'ü; ağıl yok; çit düşmanı durdurmuyor; tekme yere yapışık, "çitin üstünden" yok; oyunda normal Zıpzıp görünmüyor. |
| Ahşap yapı seti, 6 yerleştirilebilir, Ocak | ⚠️ | 26 şeklin hepsi var; 13'ünün oturması test edilmedi. 6 yerleştirilebilirin hiçbiri yok. Kapılar açılmıyor, söküm yok. |
| Şalgam tarlası | ✅ | Küçük kurallar eksik: eğim sınırı, kalite çarpanları, kabarık ürünün sönmesi. |
| Tezgâh, Taş Fırın, Paketleme, Kargo Rampası, El Arabası | ⚠️ | El Arabası'nın oynanışı yok. Kasa fiziksel yük değil; rampa ödemeyi anında yapıyor. Maliyet ve tarifler ICERIK §20'den sapıyor. |
| İlk gece: Küçük Kabarma, 10 kart, hasar sayıları, Hasat Raporu | ⚠️ | Çevik ve Mıknatıs oyunda etkisiz. Kart etkileri şafakta sıfırlanmıyor (hata). İlk kart Çatal değil, rastgele. |
| HUD, envanter, Defter'in ilk sayfası | ⚠️ | Dayanıklılık ve pusula yok; envanter metin menüsü; Defter ekranı ve iş emri verisi yok. |
| 60 ses, 2 müzik, Suna'dan 30 replik | ❌ 👤 | 12 ses (tek sürüm), 1 müzik (tek katman), 0 seslendirme. Ses kanalları, telsiz süzgeci, denetim betiği yok. |

PLAN §1.4 ilk 10 dakika: Kapak açılışı, ağıl, çit hücumu, çitin üstünden tekme, Çapa çatırtısı ve ilk kart Çatal eksik. Tezgâh gecenin sonrasına kaymış.

### M1 kabul ölçütleri

| Ölçüt | Durum |
|---|---|
| İlk 10 dakika botla hatasız | ✅ (bot girişi oyuncu girdisini atlıyor) |
| İlk 10 dakika insan girdisiyle hatasız | ❌ Gerçek girdi sürücüsü yok; oyun koluyla bitirilemiyor (sağ çubukla bakış yok) |
| Bütün varlıklar sekiz ölçütten geçti | ❌ |
| 16 ekran görüntüsü (4 yer imi × 4 gün saati) | ❌ Araç yok |
| Girişten tepkiye ≤ 50 ms | ⚠️ 👤 Ölçüm var, otomatik kayıt yok |
| RTX 2070S, 1080p yüksek, ≥ 90 FPS | 👤 Ölçüm aracı yok; "yüksek" ön ayarı tanımsız |
| Sahip 30 dakika oynar, iki "evet" | 👤 |

### PLAN §7.2 notu

Ortak oyun (koşul 4) ve kayıt (koşul 5) M2 teslimidir. M1'deki hiçbir özellik bu iki koşulu karşılayamaz; raporlarda "M2'de" yazılır.

### Sıradaki

1. Dikiş refaktörü: sıcak dosyaları partial dosyalara bölmek, bot adım kaydı, olay yolu, veri bölme, tek test koşucusu ve gerçek çıkış kodları. Davranış değişmez.
2. Belge kararları: M1'de belgelerin sustuğu yerler (6 yerleştirilebilir, 3 yemek, 60 ses listesi, 4 gün saati ve yer imleri, kartlar, başlık kapsamı) ve eskimiş bilgiler.
3. Ardından dokuz paralel akış: dünya, varlık hattı, oyuncu, Çapa ve his, yaratıklar, yapı-tarla-istasyon, gece-HUD-ilk 10 dakika, teknik ve girdi, ses kodu. Kredi gerektiren üretim sahibin onayıyla ayrıca yapılır.

- 2026-11-19 oyuncu2: yeni oyuncu modeli (Tripo, Mixamo iskeleti) player.glb; govde ~13,6k + canta 3k ucgen (butce 8k: optimizasyon listesine). Klipler Meshy'den yeniden hedeflendi; ayrinti game/assets/models/player/README.md.

- 2026-11-19 dolap: 12 uye tipi (pasifler: members.json liveKeys baglananlar), 72 kozmetik (kodla cizilen kask, vizor yuzu, tulum deseni golgelendiricisi, canta susu, Capa kaplamasi, jest klibi), karakter dosyasinda kayit, K dolap menusu, ortak oyunda gorunum. Canta artik gogse agirlikli ayri mesh (Godot kemik-cocugu yana kaydiriyordu).

- 2026-11-19 ilerleme4: 24 is emri zinciri (#13-24, #37-48; 36/48), olcu kaynaklari (9 bagli, 15 sistem bekliyor), zincir odulleri dolaba ve kartlara bagli, katalog (23 tohum, 36 kozmetik), cark kozmetik odulu.

- 2026-11-19 arayuz (M6): ayarlar sayfasi (arayuz boyu, renk korlugu suzgeci, sarsinti, altyazi ve boyu, tus atamalari), fotograf modu (P / Esc menusu), eski paneller tek Rust dilinde (UiPanels, Hud.Box). Renk korlugu: Machado tabanli daltonizasyon (gorsel dogrulama yalniz sahibin gozunde).

- 2026-11-19 ogretici (M6): data/suna.json (12 replik TR ve EN, ceviri anahtari suna.<kimlik>, ton), Director.Ogretici.cs (8 sistem tetikleyicisi, bir kez, kayitta saklanir), SayLine suna.json'a duser. Ses: Kredi kuyrugunda.
