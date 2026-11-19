@AGENTS.md

# Kabarma: proje kuralları (her oturum için geçerli)

Bu bölüm sahibin kurallarıdır ve yukarıdaki Genex sözleşmesiyle çeliştiği yerde **bu bölüm geçerlidir**. Özellikle Genex sözleşmesinin 4. maddesi ("izin sormadan üret") bu projede geçerli değildir.

## Belgeler tek kaynaktır

`docs/PLAN.md` (buradan başla) · `docs/GDD.md` (kurallar) · `docs/ICERIK.md` (içerik) · `docs/SANAT-SES.md` (görsel, ses) · `docs/TEKNIK.md` (mimari, test) · `docs/URETIM.md` (hat, bütçe, kilometre taşları) · `docs/stil/README.md` (stil föyü) · `docs/PROGRESS.md` (ilerleme günlüğü).

- Sıra: **önce belge, sonra veri, sonra kod** (URETIM §7). Belgeyle çelişen bir şey gerekiyorsa koda geçmeden önce dur ve raporla.
- Kurallar motordan bağımsız `src/Kabarma.Sim` içinde ve testli; sayılar `game/data/*.json` içinde, kodda sihirli sayı yok.
- Kilometre taşı sırası URETIM §6'dadır, ama **M1 kapısı kalktı** (sahibin kararı, 18 Kasım 2026, URETIM M1 kararı 22): oyunun tamamı hızla yapılır, sahip sonda oynar ve düzeltme ister. Efor yalnız medium/high (xhigh ve üstü yasak); test hafif, raporlar kısa.
- **Sormadan ilerle (sahibin kararı, 18 Kasım 2026):** ana oturum taşları (M2 → M7) sahibe "devam edeyim mi" diye sormadan sırayla yapar; sahip sonda oynar. Sorulan yalnız şunlar: kredi tutarı, `git push`/yayın, sahibin dosyasını silmek. Model (sahibin kararı, 19 Kasım 2026, kullanım limiti): **varsayılan Sonnet 5.5**, akışlar ve alt ajanlar dahil; **Opus 5.5 medium** yalnız Sonnet'in takıldığı zor tasarım işinde; **Opus 5.5 high** yalnız ağ/ortak oyun, kayıt, yapı dengesi, yol bulma. Danışman (advisor) Fable kalır.
- "Bitti" = PLAN §7.2'nin altı koşulu. Raporda her koşulu ayrı yaz: geçti / kısmen / yok / M2'de (ortak oyun yarısı ve kayıt, URETIM §6 M1 kapsamı). Sahibin yargısına kalanları (his, dinleme, 30 dk oynama, RTX 2070S FPS) asla "geçti" diye yazma; "sahibin onayını bekliyor" yaz.
- M1 kapsamı ve kararları: `docs/URETIM.md` §6 "M1 kapsamı ayrıntısı" ve "M1 kararları" tabloları. Önce bunları oku.
- Yer tutucu varlıklar `data/` içinde işaretlenir (URETIM §3).

## Para ve dış dünya

- **Kredi harcayan hiçbir üretimi sahibin o tutara açık onayı olmadan başlatma** (Genex, Tripo API, Tripo web, ElevenLabs). Önce ne, kaç adet, hangi havuz, kaç kredi; sonra onay. Alt oturumlar ve alt ajanlar **hiç kredi harcamaz**; ihtiyacı listeleyip ana oturuma bildirir. **İstisna (sahibin kararı, 19 Kasım 2026):** onaylı Tripo web bakiyesi içindeki tarayıcı üretimini ana oturumun başlattığı bir Sonnet alt ajanı yapar; ana oturum adedi ve tavanı verir, harcamayı izler.
- `.env` içindeki ElevenLabs anahtarı her kullanımda ayrı onay ister. Alt oturumlar `.env`'i okumaz. Anahtar hiçbir çıktıya yazılmaz.
- `git push`, yayınlama, mağaza işlemleri yalnızca sahibin açık onayıyla.
- Sahibin kaydına dokunma; testler ayrı kayıt yuvasında koşar.

## Komutlar (PowerShell)

`.tools/` git'te yok; worktree'lerde bu yollar ana çalışma kopyasından kullanılır:

- Godot (konsol): `C:\Users\uruskan\Documents\GitHub\kabarma\.tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`
- Blender 5.3: `C:\Program Files\Blender Foundation\Blender 5.3\blender.exe` (betikler `pipeline/`)
- Derle: `dotnet build game/Kabarma.Game.csproj -nologo -v q`
- Sim testleri: `dotnet test src/Kabarma.Sim.Tests -nologo -v q`
- Yeni worktree'de ilk iş içe aktarma: `& $G --headless --path game --import`
- **Hepsi bir arada:** `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1` (derleme, Sim testleri, bot, inşa testi; hata varsa çıkış kodu 1). `-Only`, `-BotTimeout`, `-BotArgs`, `-Log` seçenekleri var. Uzun komutları arka planda çalıştır.
- Godot testleri: `& $G --headless --path game -- --test=bot|build|netsync|swarm` (başarısızlıkta çıkış kodu ≠ 0). Bot `RESULT ok ...` yazar.
- Ekran görüntüsü: `--shots=...` argümanı (bkz. `tools/run.sh`, `game/scripts/Main.cs`).
- Nasıl eklenir: senaryo adımı → `game/data/scenario.json` + `Director.Scenario.cs` `WireScenario` içinde bir satır; bot adımı → yeni `game/scripts/Bot.Xxx.cs` (`RegisterXxx`) + `Bot` kurucusunda bir satır; olay → `game/scripts/autoload/Events.cs`; veri ailesi → `src/Kabarma.Sim/GameData.Xxx.cs` + `[DataFile("xxx.json")]` (bilinmeyen anahtar testi otomatik); Godot testi → `Main.Tests` sözlüğüne bir satır + `tools/test.ps1`.

## Paralel çalışma

- Aynı anda birkaç oturum, her biri kendi worktree dalında çalışır. **Her akış kendi dalını main'e kendisi alır** (sahibin kararı 25, 18 Kasım 2026; eski "ana oturum birleştirir" kuralının yerine): iş bitince ya da sağlam bir ara noktada, her şey commit edilmişken `powershell -NoProfile -ExecutionPolicy Bypass -File tools/integrate.ps1` koşar. Betik güncel main'i dala birleştirir, derleme ve Sim testlerini koşar, main'i atomik olarak dalın ucuna ilerletir; çatışmayı akış çözer. Ana çalışma kopyası main'i detached tutar, içinde `git checkout main` yapılmaz. İşe başlarken de güncel main'i kendi dalına birleştir. Her dal kendi başına derlenmeli ve yeşil olmalı; başka bir akışın henüz yazmadığı koda dayanma.
- Dosya sahipliği aşağıda. Başka akışın dosyasına dokunman gerekiyorsa küçük, yerel değişiklik yap ve raporda say. Ortak noktalar (`Bot` kurucusundaki `Register` listesi, `WireScenario`, `Events.cs`, `ItemNames.cs`, `Main.Tests`, `tools/test.ps1`, `Capa.Process` içindeki başlık anahtarı, `Hud`/`Director`/`World` `_Ready`/`_Process`, `data/*.json` dizilerine yeni öğe) yalnızca **satır ekleyerek** değişir; var olan satırı yeniden düzenleme.
- **Ortak kimlikler** (değiştirme, yeniden adlandırma; yeni kimlik satır eklenerek gelir): kaynak `stone`, `wood`, `fiber`, `clay`, `copper_ore`, `copper_ingot`, `wheat`, `egg` (çanta eşyası), `turnip`, `turnip_seed`, `civi` · ürün `civi_tabancasi`, `el_arabasi`, `kargo_kasasi`, `kozde_salgam`, `haslanmis_yumurta`, `sebze_corbasi` · yerleştirilebilir/istasyon `ocak`, `tezgah`, `tas_firin`, `paketleme`, `ocakbasi`, `kargo_rampasi`, `ahsap_kapi`, `sandik`, `uyku_tulumu`, `mesale`, `yemlik`, `folluk`. Türkçe adları `game/scripts/ui/ItemNames.cs`.
- **Yer tutucu kaynaklar:** `World.Placeholders.cs` kil, bakır, lif ve yabani buğday için geçici düğümler kurar (Kazma ve Biçme ile toplanır). Diğer akışlar bunları gerçek oyun yolundan toplar; dunya akışı gerçekleriyle değiştirir.
- **Testleri hafif tut (sahibin kararı, 18 Kasım):** ara koşularda yalnız `tools/test.ps1 -Only build,sim` ve gerekirse kendi yeni Godot testin tek başına. Tam `tools/test.ps1` (bot dahil) yalnız commit'ten hemen önce bir kez. Aynı koşuyu kanıt için tekrarlama; ekran görüntüsü koşusu en sonda bir kez.
- **Makineyi yorma, arkanı topla (sahibin isteği, 18 Kasım 2026: makine donuyor, disk doluyor):**
  - `tools/test.ps1` bütün oturumlar için aynı anda en çok 2 Godot koşturur (`KABARMA_GODOT_MAX`); fazlası sırada bekler.
  - Godot'u `& $G` ile doğrudan yalnız ekran görüntüsü için ve tek başına aç.
  - İş bitince kendi geçicilerini sil: TEMP'te açtığın git worktree'leri (`git worktree remove`), büyük log ve ham çıktılar, `__pycache__`.
  - Kanıt `progress/` altında, ham model ve büyük dosya `.tools/` altında kalır.
  - Akış bitince her şey commit'lenmiş ve main'e alınmış olsun ki oturum arşivlenip worktree'si silinebilsin.
- **Optimizasyon en sona (sahibin kararı, 18 Kasım 2026):**
  - Bütçe ölçümü, profil çıkarma, stres ya da FPS koşusu ve optimizasyon şimdi yapılmaz.
  - Bir şey bütçeyi aşıyorsa `docs/PROGRESS.md` "Optimizasyon listesi"ne tek satır yaz ve özellik işine devam et.
  - Stres testleri (KS 30 gibi) olağan koşuya girmez.
  - Kararlar sektörün olağan yoluna ve mantığa göre verilir.
- **Otomatik koşular sessiz (sahibin kararı, 18 Kasım 2026):** `--test`, `--bot`, `--shots`, `--quit_after` gibi her otomatik koşuda `Main.cs` ana kanalı susturur (sahip çalışırken müzik dinliyor). Sesi gerçekten duymak gereken bir koşu `--ses` ekler. Pencereli koşularda ayrıca `--audio-driver Dummy` kullanılabilir.
- **Ayak işi Sonnet'e (sahibin kararı, 18 Kasım 2026):** test koşuları (`tools/test.ps1`, Godot testleri), ekran görüntüsü koşuları, log taraması ve `tools/integrate.ps1` koşusu Opus oturumunun kendisinde değil, bir **Sonnet alt ajanında** yapılır (Agent aracı, `model: "sonnet"`). Alt ajana komutu, worktree yolunu ve neye bakacağını ver; o koşar, logu okur ve kısa özet döner (geçti/kaldı, ilk hata satırları, ekran görüntüsü yolları). Uzun koşuyu alt ajan arka planda koşturur. Kod düzeltmesi Opus'ta kalır.
- **Botu dürüst tut:** kaynak oyun yolundan gelir; bot için eşya verme, adım silme ya da atlama yok. Gevşettiğin her bot adımını raporda yaz. Aynı anda çok oturum Godot koşturduğu için bot zaman aşımı olabilir: hata ayıklamadan önce `-BotTimeout` büyütüp yeniden koş.
- Yeni işi yeni dosyalara koy (partial class, yeni test dosyası, yeni veri dosyası, yeni `pipeline/*.py`).
- Kredi gerektiren üretim (model, iskelet, klip, ses, müzik, görsel) alt oturumda **çalıştırılmaz**: istemleri ve betiği hazırla, dry-run tahminini çıkar, ana oturuma "üretim isteği" olarak bildir.
- Bitince: `tools/test.ps1` yeşil. Kendi dalına commit et, `tools/integrate.ps1` ile main'e al, push etme. Kanıt `progress/m1/<akış>/` altına (ekran görüntüsü, log). Raporun sonunda PLAN §7.2 koşullarını teslim başına yaz.

| Akış | Sahip olduğu dosyalar ve işler |
|---|---|
| dunya | `World*.cs` (`World.Placeholders.cs` dahil: gerçeğiyle değiştirir), `Rock.cs`, gökyüzü/su/bitki shader'ları, `data/biomes.json`, süs modelleri (`cali`, `bugday`, `cicek`, `ot`, `turuncu_agac`, `egrelti`, `kaya_duz`, `kaya_dik`, `bakir`), kil yatağı, bakır damarı, Sulama deposu ve su yüzeyi (`Capa.Sulama.cs`), Biçme'nin ot/çalı/yabani buğday verimi, 4 gün saatinin ışığı, yer imleri, 16 görüntü aracı, "yüksek" ön ayar ve FPS ölçümü |
| varlik | `pipeline/clean_asset.py`, `style_sheet.py`, `gen_golden.sh`, altın varlık klasörleri (`agac`, `kaya`, `kova`, `salgam`, `tezgah`, `tas_firin`, `el_arabasi`, `ocak`, `kapsul`, `paketleme`, `capa`), `data/assets.json`, `data/palette.json`, `docs/stil/` |
| oyuncu | `Player*.cs`, `PlayerRig.cs`, `Visor.cs`, `pipeline/merge_player.py`, `assets/models/player/`, `tool.json` içindeki `player`, dayanıklılık (Sim + `Hud.Stamina.cs`) |
| capa | `Capa*.cs` (Sulama deposu ve İnşa hariç), `Feel.cs` ve F1 paneli, `data/heads.json`, `weapons.json`, `feel.json`, vuruş efektleri, girişten tepkiye ölçüm; tarla küçük kuralları (`Farm.cs`, `FarmView.cs`, `crops.json`: eğim sınırı, kalite çarpanları, kabarık ürünün sönmesi) |
| yaratik | `Zipzip.cs` ve `Gidak.cs` (görünüş, animasyon, şişme, Kabarma davranışı; Gıdak'ın Zıpzıp'ı çitin üstünden tekmelemesi; Zıpzıp'ın gündüz 2–3'lük ürkek grupları), `PoseLibrary.cs`, `Pofuduk.cs`, `Swellable.cs`, şişme shader'ı, `gidak`/`zipzip`/`pofuduk` modelleri, `data/creatures.json` |
| ilkgun | `data/scenario.json`, `Director.Scenario.cs`, ilk günün bot adımları, ağıl, kapı/kapı kanadı açılma, Ahşap Kapı, Yemlik, Folluk, Gıdak'ın ağıl davranışı (yeni `Gidak.Pen.cs` partial), yumurtanın düşmesi ve Folluk'a girmesi, Zıpzıp'ın çit hücumu ve çit hasarı (yeni `Zipzip.Fence.cs` partial), kapsül kapağının açılışı (yeni `Pod.Hatch.cs` partial), Suna'nın 30 repliği (TR + EN, metin) |
| ekonomi | `recipes.json`, `placeables.json`, `foods.json`, `prices.json`, `tool.json` içindeki çanta ve yığın boyları, `Station.cs`, `Ocak.cs`, `Pod.cs` (rampaya dönüşme), El Arabası, kargo kasası, Kargo Rampası, Ocakbaşı, Sandık, Uyku Tulumu (`Events.RaiseSlept`), Meşale, söküm ve onarım (`BuildBridge*.cs`), `BuildTest.cs`, yemek etkileri |
| gecehud | `Night.cs`, `cards.json`, `kabarma.json`, `Hud*.cs` (`Hud.Stamina.cs` ve `Hud.Voice.cs` hariç), `Director.Night.cs`, gün döngüsü ve takvim (ilk geceden sonra; `Events.Slept` ile normal gecenin atlanması), F1'den Küçük Kabarma'yı yeniden başlatma, Defter ve iş emirleri (`orders.json`), envanter arayüzü, pusula |
| girdi | `Main.cs` girdi eşlemesi, oyun kolu, `Director.Input.cs`, girdi glifleri, gerçek girdi sürücüsü testi, `tools/test.ps1`, `game/scripts/tests/`, Sim kıyaslama testi, `export_presets.cfg` (Windows dışa aktarma) |
| ses | `Sfx.cs`, ses kanalları, `data/audio.json`, müzik durum makinesi, `Hud.Voice.cs`, `pipeline/audio_qc.py`, `pipeline/eleven.py`, `pipeline/licenses.md` |

## M2 akışları (18 Kasım 2026)

M1 tablosu M1 akışlarınındır; M2 akışları aşağıdakilerdir. Kurallar aynı: kendi dalında çalış, başta main'i birleştir, ara noktalarda ve sonda `tools/integrate.ps1` ile main'e al, kredi harcama (üretim isteğini `docs/PROGRESS.md` "Kredi kuyruğu"na yazdırmak için ana oturuma bildir), yer tutucuyu işaretle, `Sim`'e giren her durum düz ve serileştirilebilir (TEKNIK §9). Ortak dosyalara yalnız satır ekle. Aynı anda çok oturum Godot koşturduğu için bot ve Godot testlerinde `-BotTimeout 900` kullan.

**Ortak oyuna bağlanma (ortak akışı, `game/scripts/net/NetSync.cs`):** Tek oyuncuda `_d.Net` null'dur; bütün kancalar `_d.Net?.X() == true` biçimindedir. (1) Sistemin durumunu `_d.Net?.Register("kimlik", perPeer, (w, peer) => yaz, r => uzlaştır)` ile ya da bir `INetSection` ile kaydet: yazım kanonik olsun (sıralı, yuva düzeni yok), okuma eksik olanı kursun, fazlayı kaldırsın; host değişeni 5 Hz'te, tamamını katılmada gönderir, 10 sn'de bir özetleri karşılaştırır. (2) Dünyayı ya da çantayı değiştiren her eylemin başına tek satır `if (_d.Net?.Forward("istek", w => ...) == true) return;` ekle; host'ta `_d.Net.On("istek", (actor, r) => ...)` içinde `NetActor`'ün (kendi `Bag`'i, son konumu) menzilini ve bedelini doğrula, sonra sistemin genel girişini çağır; bu yüzden yeni girişler `_d.Player` / `_d.State.Backpack` yerine aktörü ya da çantayı parametre alsın. (3) Yaratık düğümü `NetSync.CreatureGroup` grubuna girer (host'ta yapay zekâ, istemcide kukla); hedef seçimi için bütün oyuncular `_d.Net.Players()`. Oyun sırasında doğan yaratık yalnız host'ta doğar (doğurucunun başına `if (_d.NetClient) return;`); türü `NetSync.Spawns.cs` `KindOf`/`MakePuppet`'e birer satırla eklenir. Çantayı değiştiren her yeni eylem (2)'deki gibi host'a gitmeli: istemcinin çantası host'ta durur, yalnız yerelde değişen çanta geri alınır.

**Kayıt sözleşmesi:** `kayit` akışı önce Godot düğümlerinde duran durumu denetler ve bir anlık görüntü sözleşmesi (bölüm arayüzü) yazar; onu `tools/integrate.ps1` ile erken main'e alır. O birleşene kadar akışlar durumlarını düz `Sim` sınıflarında tutar; birleşince kendi bölümlerini satır ekleyerek bağlar. Birleşmemiş koda dayanma. **Nasıl bağlanılır (TEKNIK §9.1, birleşti):** `Sim` durumu için kendi partial dosyanda `GameState` üzerine `[SaveSection("kimlik", 1, Order = SaveOrder.State, Covers = new[] { nameof(Xxx) })]` işaretli bir özellik yaz: get düz bir DTO üretir, set onu **sessizce** uygular (olay yaymaz, `Add`/`Plant` gibi sayaç ilerleten yolları kullanmaz). Godot düğümündeki durum için kurulumda `d.Save.Add(SaveSection.Json("kimlik", 1, Capture, Apply, SaveOrder.Spawn))` ekle; nesne yerine `State.Ids.New()` kimliği ve `EntityRecord` kullan, Godot tipi yazma. `GameState`'e eklenen her özellik bir bölümce kapsanmazsa `SaveContractTests` kırılır; sürüm artınca `[SaveMigration("kimlik", eski)]` adımı ve testi zorunlu. Örnekler `GameState.Save.cs` (`core`, `player.pack`), denetim `progress/m2/kayit/durum_denetimi.md`.


| Akış | Model | Sahip olduğu dosyalar ve işler |
|---|---|---|
| kayit | Opus high | `src/Kabarma.Sim/Save*.cs`, `GameState.Save.cs`, `game/scripts/save/`, `data/save.json`; kayıt ve yükleme, yuvalar, otomatik kayıt (5 dk, şafak, çıkış), 5 yedek, göçler, karakter dosyası; Godot statiklerinin temizlenmesi |
| ortak | Opus high | `src/Kabarma.Sim/Net*.cs`, `game/scripts/net/`, `data/net.json`; Steam (Steamworks.NET) ve ENet taşıması, lobi ve davet, host yetkili çoğaltma (oyuncu, yaratık, yapı, arazi farkı, envanter), sonradan katılma (kayıt anlık görüntüsüyle), sesli sohbet, şok tokadı, `--test=coop` |
| yapi | Opus high | `game/scripts/building/` (`BuildBridge*.cs`'e yalnız satır ekler), `src/Kabarma.Sim/Structure*.cs`; taşıma dengesi, 4 malzemeye yükseltme, plan modu, Plan Defteri, `data/build.json`, yapı parçası modelleri (`pipeline/` betiği) |
| tarim | Opus medium | `Farm*.cs`, `FarmView*.cs`, `crops.json`, `src/Kabarma.Sim/Genetics*.cs`; bitki genleri, çaprazlama, Alaca, zararlılar, T1–T2 ekinleri |
| hayvan | Opus medium | `Pen*.cs`, `Coop.cs` (Sim), `Gidak.Pen.cs`, `src/Kabarma.Sim/Husbandry*.cs`; evcilleştirme, ihtiyaç ve mutluluk, hayvan genleri ve üreme, roller; 8 evcil türün kuralı ve ağıl davranışı |
| yaratik2 | Opus medium | Çayır ve Koru'nun 12 yaratığının görünüşü ve vahşi davranışı (`Zipzip*.cs`, `Pofuduk*.cs`, yeni yaratık aktörleri), Çekirge, `PoseLibrary.cs`, `Swellable.cs`, `creatures.json` (satır ekleyerek). Evcil türlerin kuralı hayvan'da |
| boss | Opus medium | Koca Pofuduk ve Boynuzlu Dede: `src/Kabarma.Sim/Ata*.cs`, `game/scripts/boss/`, `data/atalar.json`, karşılaşma alanları |
| guc | Opus medium | `src/Kabarma.Sim/Power*.cs`, `Signal*.cs`, `game/scripts/power/`, `data/power.json`; güç ağı ve temel sinyal, T1–T2 parçaları (ICERIK §12) |
| zincir | Opus medium | T1–T2 üretim zinciri ve araştırma, yeni istasyonlar (ICERIK §11), ambardan üretim, envanter; `Station*.cs`, `Crafting*.cs`, `src/Kabarma.Sim/Research*.cs`, `data/research.json`, `recipes.json` ve `placeables.json` (satır ekleyerek) |
| ekonomi2 | Opus medium | borsa, katalog, çark (GDD §13, ICERIK §18): `Economy*.cs`, `economy.json`, `prices.json`, `src/Kabarma.Sim/Market*.cs`, `Catalog*.cs`, `Wheel*.cs`, yeni `Hud.Market*.cs` |
| ilerleme | Opus medium | iş emirleri altyapısı ve ilk 12 zincir (`Orders*.cs`, `orders.json`), Ustalık (`src/Kabarma.Sim/Mastery*.cs`, `data/mastery.json`, `Hud.Mastery*.cs`) |
| dunya2 | Opus medium | Sis Korusu biyomu, harita ve Seyir Kulesi, mancınık, T1–T2 keşif noktaları; `World*.cs`, `biomes.json`, `Hud.Map*.cs` |
| girdi, varlik, ses | M1 oturumları | M1'deki sahiplikleri sürer (girdi testleri, varlık hattı, ses) |

**Güç bağlanma notu (guc):** Güç isteyen istasyon yalnız `research.json` `watts` tablosuna satır ekler (istasyon kimliği → W; tabloda olmayan güç istemez). Kurulunca `PowerSystem` ona kendiliğinden bir soket açar ve `StationQueue.PowerOk`'u bağlar: sıra yalnız kabloyla beslenirken ilerler, iş yokken talep yok. İstasyon kodu değişmez; etiket için `_d.Power?.PortOf(this)?.Status` (sözleşme `game/scripts/power/PowerPort.cs`, durum `GameState.Power`, kayıt bölümü `power`).

## M3 akışları (18 Kasım 2026)

M3 (URETIM §6): tam Kabarma ve savaş, dünya hâlâ Altın Çayır + Sis Korusu (T1–T2). Kurallar M2 ile aynı (kendi dal, `tools/integrate.ps1`, ayak işi Sonnet alt ajanında, otomatik koşular sessiz, kredi yok, Sim düz ve kayda/ağa bağlı). M2 oturumları yeniden kullanılır; her akış önce main'i birleştirir.

**Sözleşmeler (erken main'e alınır):** `savas` ortak hasar, sağlık ve durum arayüzünü (`Combat*`), `kabarma3` dalga ve Kabaran arayüzünü (`Wave*`), `yolbulma` akış alanı sorgusunu (`FlowField*`) ilk iş olarak yazar ve main'e alır, CLAUDE.md'ye iki satırlık bağlanma notu ekler. O birleşene kadar ötekiler yerel, işaretli bir geçici arayüz kullanır.

**Akış alanına bağlanma (yolbulma, birleşti; TEKNIK §8.1):** Yön için `_d.Flow?.Steer(pos, "crop" | "crop:<aile>" | "kazan" | kendi anahtarın)` (null: pencere dışı ya da yol yok, doğrudan yürü); kırılacak engel için `_d.Flow.Sample(key, pos)` → `Blocked`, `Blocker` (sahip kimliği; `FlowFieldSystem.OwnerNode(id)` yapı parçasını verir), `BlockerKind`; hasarı veren sensin. Kendi hedef kümen için `_d.Flow.SetGoals(key, noktalar)`; kendi engelin (tuzak, kule, kapı; tür `flowfield.json` `breakCost` anahtarı, açık kapı `doorOpen`) için `_d.Flow.StampBox(kimlik, tür, merkez, yarıX, yarıZ, yön)` / `Unstamp(kimlik)`; Holding için `_d.Flow.FindPath(a, b)` (kırma maliyetli A*). Alan yalnız host'ta işler, türetilmiş veridir (kayda ve ağa bölüm gerekmez).

| Akış (oturum) | Model | Sahip olduğu dosyalar ve işler |
|---|---|---|
| savas (boss) | Opus medium | GDD §16.1 hasar ve durumlar, §16.3 gündüz savaşı: `src/Kabarma.Sim/Combat*.cs`, `Status*.cs`, `data/combat.json`, `game/scripts/combat/`; Azman Homur ve Azman Bozuk (ICERIK §4.2): `Azman*.cs`, `data/azman.json` |
| kabarma3 (yaratik2) | Opus medium | GDD §14.1–14.5 ve §14.6: tam Kabarma akışı, Kabaranlar, dalga kuralları ve bileşimi, "ne ekersen onu çekersin", KS 1–10 (stres sahnesi KS 30), kazanç ve kayıp, Gösteri çarpanı: `Night*.cs`, `Director.Night.cs`, `kabarma.json`, `src/Kabarma.Sim/Wave*.cs`, `Kabaran*.cs`, sürü çizimi |
| kartlar (zincir) | Opus medium | GDD §15, ICERIK §8: 72 kart (60 + 12 aşı), baştan açık 20, Nişan; `cards.json`, `src/Kabarma.Sim/Card*.cs`, `Asi*.cs`, `Nisan*.cs`, kart arayüzü (`Hud.Night*.cs`'e satır, yeni `Hud.Cards*.cs`); her kartın ve aşının senaryo testi |
| savunma (yapi) | Opus medium | GDD §16.4, ICERIK §13: T1–T2 kuleleri (Çivi, Sapan, Saçma) ve tuzakları (Diken Tarlası, Yapışkan Zemin, Fırlatma Pedi, Çukur Kapağı); `src/Kabarma.Sim/Tower*.cs`, `Trap*.cs`, `game/scripts/defense/`, `data/defense.json` |
| gucler (guc) | Opus medium | GDD §17, ICERIK §7: ilk beş güç, Kadim Taş denemeleri, iki ulti; GDD §14.7 Tatbikat ve Maya Feneri (§14 Kabarma başlatma, `kabarma3` sözleşmesiyle); `src/Kabarma.Sim/Ability*.cs`, `KadimTas*.cs`, `Tatbikat*.cs`, `data/abilities.json` |
| holding (ekonomi2) | Opus medium | GDD §19, ICERIK §3.7: beş Holding birimi, ilk kamp (seviye 1–4), kervan, baskın ve karşı baskın, Husumet; `src/Kabarma.Sim/Holding*.cs`, `game/scripts/holding/`, `data/holding.json` |
| yolbulma (ortak) | Opus high | TEKNIK §8 akış alanlı yol bulma (sürü ve Holding birimleri için), KS 30 stres sahnesi 8 botla kare süresi ve ağ bütçesi (TEKNIK §5.3, §10); `src/Kabarma.Sim/FlowField*.cs`, `game/scripts/ai/`, `--test=stress` |
| cip (ilerleme) | Sonnet 5.5 | ICERIK §6.3: 24 çip × 4 nadirlik, Çapa'ya takma, düşme kaynakları; `src/Kabarma.Sim/Chip*.cs`, `data/chips.json`, `Hud.Chips*.cs` |
| savas-hayvan (hayvan) | Opus medium | GDD §9.5 Kabarma'da hayvanlar, savaş hayvanları ve binek; `Husbandry.Combat*.cs`, `Mount*.cs` (M2 işi bittikten sonra) |
| oyuk (dunya2) | Opus medium | Köstebek Bey ve Yarasa Yumağı (ICERIK §4.3) ve Oyuk alanları; `Oyuk*.cs`, `data/oyuk.json`, `World*.cs`'e satır |

**Kabarma bağlanma notu (kabarma3):** Başlatma `State.StartKabarma(State.KabarmaSpecFor(KabarmaKind.Full|Small|Drill, ks, oyuncu, x, z))` (Çan/takvim, Maya Feneri, Tatbikat); plan `Waves.Compose` (`src/Kabarma.Sim/Wave.cs`), gece `KabarmaNight` (`Plan`, `TakeDue`, `Kazan`, `DamageKazan`, `ReportGosteri(GosteriEvent)`, `CreditKill`, olaylar `DorukStarted`/`Ended(KabarmaEnd)`), kalıcı `State.Kabarma` (kayıt bölümü `kabarma`).
Gece düşmanını bulmak ve vurmak için yalnız `Kabarans.Near(nokta, yarıçap)` ve `Kabarans.Hit(k, hasar, kaynak, itme, saldıran, çevre, gece)` (`game/scripts/night/Kabarans.cs`, aktör `IKabaran`); vuruş geçici, savaş sözleşmesi gelince yalnız `Kabarans.Hit` değişir. Azman Doruk'ta `WaveSlot.Azman` yerinden doğar (savaş).

## M4 akışları (18 Kasım 2026)

M4 (URETIM §6): dört yeni biyom, T3–T6 ve kalan içerik. Kurallar M3 ile aynı: kendi dal, başta ve sonda `tools/integrate.ps1`, ayak işi Sonnet alt ajanında, otomatik koşular sessiz, kredi yok, Sim düz ve kayda/ağa bağlı, optimizasyon yok. Ek kurallar:

- **Aynı anda en çok 5 akış** (makine yükü; 19 Kasım 2026'da 3'ten 5'e çıktı, Godot hâlâ en çok 2). Bir akış main'e alınıp arşivlenince ana oturum sıradakini başlatır; sıra tablodaki "Sıra" sütunu. Sonraki akışa dayanma; gerekeni kendi dosyanda küçük, işaretli bir geçiciyle çöz.
- **Ortak kimlikler: `docs/kimlikler-m4.md`.** Biyom, kaynak, yaratık, ekin, istasyon, araç, yemek ve öteki içerik kimlikleri oradan alınır. Yeni kimlik satır eklenerek gelir; var olan kimlik yeniden adlandırılmaz. Listede olmayan bir şey gerekirse satırı ekle ve raporda say.
- **Modeller:** yeni yaratık, Ata, araç, istasyon ve kozmetik modelleri önce yer tutucudur (`data/assets.json`'da işaretli, URETIM §3). Kredi isteyen üretimi listele; ana oturum Tripo web'de üretir.
- **Adetler:** içerik adetleri PLAN §7.1 ile birebir olmalı. Akış kendi bölümünü bitirince `DataCheckTests` sayısını günceller.
- **Kaba hata bakışı (sahibin kararı, 19 Kasım 2026):** büyük bir akış main'e alınınca ana oturum bir Opus high alt ajanına akışın farkını yalnız kaba hatalar için kısaca okutur: çökme, eksik kayıt ya da ağ kancası, yanlış veri, belgeyle çelişki. Test koşusu ve düzeltme yok; bulunanı akışın kendisi (Sonnet) düzeltir.

| Akış | Sıra | Model | Sahip olduğu dosyalar ve işler |
|---|---|---|---|
| biyom | 1 | Opus medium | **Önce** kırmızı `--test=world`'ü yeşile çevir. ICERIK §1 Işıl Sazlık, Kızıl Kanyon, Ayaz Yaylası, Göbek: arazi, ışık, iklim, giriş donanımı. §2 T3–T6 kaynak düğümleri ve toplama verimi. §17 Zengin Yatak ve dört biyomun keşif noktaları. Her biyomun ekran görüntüsü seti. Dosyalar: `World*.cs`, `biomes.json`, `Hud.Map*.cs`, biyom shader'ları |
| zincir4 | 1 | Opus medium | ICERIK §11 T3–T6 istasyon ve makineleri (Aşı Tezgâhı tarim4'te), Kabartma Odası, §2 T3–T6 işleme tarifleri, T3–T6 araştırması. `RecipeGraph`'te başlangıçtan Kabartma Odası'na yol olmalı. Dosyalar: `Station*.cs`, `Crafting*.cs`, `Research*.cs`, `recipes.json`, `placeables.json`, `research.json` |
| yaratik4 (yaratik2) | 1 | Opus medium | ICERIK §3 yeni biyomların 15 yaratığı (Sazlık 5, Kanyon 5, Ayaz 4, Göbek 1): görünüş, vahşi davranış, Kabaran hâli. Dosyalar: `game/scripts/actors/` (yeni aktörler), `CreatureRules.cs`, `PoseLibrary.cs`, `creatures.json` (satır ekleyerek) |
| ata4 | 2 | Opus medium | ICERIK §4: Kurbağa Ağa, Çıyan Paşa, Buz Tosunu, Ana Maya, Kabarık Atalar, dört Azman, üç Oyuk koruyucusu ve karşılaşma alanları. Dosyalar: `Ata*.cs`, `game/scripts/boss/`, `atalar.json`, `Azman*.cs`, `azman.json`, `Oyuk*.cs`, `oyuk.json` |
| tarim4 | 3 | Opus medium | ICERIK §5 13 ekin ve sera, Aşı Tezgâhı (gen aktarma), §15 kalan 18 yemek. Dosyalar: `Farm*.cs`, `FarmView*.cs`, `crops.json`, `Genetics*.cs`, `foods.json` |
| tasima | 4 | Opus medium | ICERIK §12 kalan güç, sinyal ve taşıma parçaları: otomasyon, drone, teleferik. Dosyalar: `Power*.cs`, `Signal*.cs`, `game/scripts/power/`, `power.json`, yeni `Drone*.cs` ve `Teleferik*.cs` |
| kusam | 5 | Opus medium | ICERIK §6 yedi silah başlığı, §7 yedi güç ve iki ulti, §16 dört takım, 12 modül, Çapa Mk.III–VI. Dosyalar: `heads.json`, `weapons.json`, `abilities.json`, `Ability*.cs`, yeni `Armor*.cs` |
| kale | 6 | Opus medium | ICERIK §13 beş kule ve dört tuzak; §10 Çelik Sac, Cam, Mayalı Beton ve kalan yerleştirilebilirler. Dosyalar: `Tower*.cs`, `Trap*.cs`, `game/scripts/defense/`, `defense.json`, `build.json`, `game/scripts/building/`, `Structure*.cs` |
| arac | 7 | Opus medium | ICERIK §14 Traktör, Kamyon, Biçerdöver, Hava Yastıklı, Zeplin ve 10 eklenti. Dosyalar: `Vehicle*.cs`, `Director.Vehicles.cs`, `vehicles.json` |
| holding4 | 8 | Opus medium | ICERIK §3.7 ve §4.4: ikinci ve üçüncü kamp, Genel Merkez ve Müdür. Dosyalar: `Holding*.cs`, `game/scripts/holding/`, `holding.json` |
| gece4 | 9 | Opus medium | GDD §14, ICERIK §8.4: KS 11 ve üstü, 12 tüzük maddesi, Yeni Ay. Dosyalar: `Night*.cs`, `Wave*.cs`, `kabarma.json`, yeni `Tuzuk*.cs` |
| dolap | 10 | Opus medium | ICERIK §9 kalan 11 üye tipi (pasif, başlangıç kartı, açılma yolu) ve 72 kozmetik (kask, vizör yüzü, tulum deseni, çanta süsü, Çapa kaplaması, jest). Dosyalar: yeni `Member*.cs`, `Cosmetic*.cs`, `members.json`, `cosmetics.json` |
| ilerleme4 | 11 | Sonnet 5.5 | ICERIK §18 kalan 24 iş emri zinciri, başarımlar, katalog ve çark tabloları. Dosyalar: `Orders*.cs`, `orders.json`, `achievements.json`, `catalog.json`, `wheel.json` |
| kabul | 12 | Opus medium | M4 kabulü: `DataCheckTests` adetleri PLAN §7.1 ile birebir; hızlandırılmış bot başlangıçtan Ana Maya'ya; biyom ekran görüntüsü setleri; kalan kırmızı testler. Dosyalar: yeni `Bot.AnaMaya*.cs` |

**Ata bağlanma notu (ata4, M4):** Yeni Atalar (`kurbaga_aga`, `ciyan_pasa`, `buz_tosunu`, `ana_maya`) `atalar.json`'da; arenaları `arena.temporary` ile **geçici işaretli** (biyom akışı Ata Taşı noktalarını koyana dek `AtaSystem` dünyayı kurmaz; gerçek nokta çözülünce `AtaArena.Resolve` kendiliğinden kurar; test `AtaSystem.Place(id)` ile kurar). Arena donanımı `arena.props` (merkeze göre: nilüfer, duvar, varil, ısınma noktası, davul, göl). Kabarık Ata: yenilen Ata aynı adak (`kabarik_<ata>` kaydı) + `KS × 50` `maya`; `AtaRules.SummonKabarik`, yenilgide `RewardKabarik` (yalnız `kabarik_oz`), olay/çip kaynağı `kabarik_<ata>`. Azman kimliği `azman_<tür>` ya da `surubasi`; Doruk'ta biyomun Azman'ı (`Data.ForBiome`). Yeni Oyuk koruyucuları `oyuk.json` `kind`: `camur`, `akrep`, `ayna`; biyom Oyuk noktaları gelince `OyukRules.Oyuks` odayı kendisi kurar, o güne dek `OyukSystem.AddTemporaryRoom(guardianId, tier)`. Azman Kızgın'ın yangını `AzmanSystem.FireSpots()` ile okunur, söndürme `AzmanSystem.Douse(aim, dt)` (Sulama hattı çağırır; yağmurlama da çağırmalı); tarla bitkisine yangın hasarı tarim akışının bağlanma noktasıdır. Test: `--test=ata4` (`tools/test.ps1 -Only ata4`). Ortak oyun (`AtaSystem.Net.cs`): `ata.state` bölümü Ata ilerlemesini ve dövüşü (can, evre, konum) istemciye taşır; adak ve Kabarık çağırma `ata.offer` ile host'ta aktörün çantasından (Maya dahil), vuruşlar `ata.hit`/`ata.weak`/`ata.shear` ile host'a gider, ödül her aktörün çantasına verilir; istemcide Ata kuklası yalnız yürür ve temas hasarı verir (saldırı efektleri, yardımcılar ve Müdür host'ta: bilinen boşluk). Azman söndürme `azman.douse`. `--test=coop` bir Ata adak+dövüş adımını sınar.

| biyom | 1 | Sonnet 5.5 | **Önce** kırmızı `--test=world`'ü yeşile çevir. ICERIK §1 Işıl Sazlık, Kızıl Kanyon, Ayaz Yaylası, Göbek: arazi, ışık, iklim, giriş donanımı. §2 T3–T6 kaynak düğümleri ve toplama verimi. §17 Zengin Yatak ve dört biyomun keşif noktaları. Her biyomun ekran görüntüsü seti. Dosyalar: `World*.cs`, `biomes.json`, `Hud.Map*.cs`, biyom shader'ları |
| zincir4 | 1 | Sonnet 5.5 | ICERIK §11 T3–T6 istasyon ve makineleri (Aşı Tezgâhı tarim4'te), Kabartma Odası, §2 T3–T6 işleme tarifleri, T3–T6 araştırması. `RecipeGraph`'te başlangıçtan Kabartma Odası'na yol olmalı. Dosyalar: `Station*.cs`, `Crafting*.cs`, `Research*.cs`, `recipes.json`, `placeables.json`, `research.json` |
| yaratik4 (yaratik2) | 1 | Sonnet 5.5 | ICERIK §3 yeni biyomların 15 yaratığı (Sazlık 5, Kanyon 5, Ayaz 4, Göbek 1): görünüş, vahşi davranış, Kabaran hâli. Dosyalar: `game/scripts/actors/` (yeni aktörler), `CreatureRules.cs`, `PoseLibrary.cs`, `creatures.json` (satır ekleyerek) |
| ata4 | 2 | Sonnet 5.5 | ICERIK §4: Kurbağa Ağa, Çıyan Paşa, Buz Tosunu, Ana Maya, Kabarık Atalar, dört Azman, üç Oyuk koruyucusu ve karşılaşma alanları. Dosyalar: `Ata*.cs`, `game/scripts/boss/`, `atalar.json`, `Azman*.cs`, `azman.json`, `Oyuk*.cs`, `oyuk.json` |
| tarim4 | 3 | Sonnet 5.5 | ICERIK §5 13 ekin ve sera, Aşı Tezgâhı (gen aktarma), §15 kalan 18 yemek. Dosyalar: `Farm*.cs`, `FarmView*.cs`, `crops.json`, `Genetics*.cs`, `foods.json` |
| tasima | 4 | Sonnet 5.5 | ICERIK §12 kalan güç, sinyal ve taşıma parçaları: otomasyon, drone, teleferik. Dosyalar: `Power*.cs`, `Signal*.cs`, `game/scripts/power/`, `power.json`, yeni `Drone*.cs` ve `Teleferik*.cs` |
| kusam | 5 | Sonnet 5.5 | ICERIK §6 yedi silah başlığı, §7 yedi güç ve iki ulti, §16 dört takım, 12 modül, Çapa Mk.III–VI. Dosyalar: `heads.json`, `weapons.json`, `abilities.json`, `Ability*.cs`, yeni `Armor*.cs` |
| efekt | 6 | Sonnet 5.5 | SANAT-SES §7 efekt geçişi (sahibin isteği, 19 Kasım 2026): Çapa başlıklarının namlu, isabet ve iz efekti; vuruş (hasar sayısı, sarsıntı, parça saçılması); durumlar (yanık, donma, şok, zehir, kabarık); yaratık ve Ata saldırılarının görünür işaretleri; güçler ve ultiler; kule atışı, tuzak tetiklenmesi; tarım, hayvan, mühendislik efektleri. Hepsi shader ve parçacıkla kodda; ortak bir efekt kitaplığı. Dosyalar: `game/scripts/fx/`, yeni `Vfx*.cs`, `data/vfx.json`; sistemlerin dosyalarına yalnız efekt çağrısı satırı |
| kale | 7 | Sonnet 5.5 | ICERIK §13 beş kule ve dört tuzak; §10 Çelik Sac, Cam, Mayalı Beton ve kalan yerleştirilebilirler. Dosyalar: `Tower*.cs`, `Trap*.cs`, `game/scripts/defense/`, `defense.json`, `build.json`, `game/scripts/building/`, `Structure*.cs` |
| arac | 8 | Sonnet 5.5 | ICERIK §14 Traktör, Kamyon, Biçerdöver, Hava Yastıklı, Zeplin ve 10 eklenti. Dosyalar: `Vehicle*.cs`, `Director.Vehicles.cs`, `vehicles.json` |
| holding4 | 9 | Sonnet 5.5 | ICERIK §3.7 ve §4.4: ikinci ve üçüncü kamp, Genel Merkez ve Müdür. Dosyalar: `Holding*.cs`, `game/scripts/holding/`, `holding.json` |
| gece4 | 10 | Sonnet 5.5 | GDD §14, ICERIK §8.4: KS 11 ve üstü, 12 tüzük maddesi, Yeni Ay. Dosyalar: `Night*.cs`, `Wave*.cs`, `kabarma.json`, yeni `Tuzuk*.cs` |
| dolap | 11 | Sonnet 5.5 | ICERIK §9 kalan 11 üye tipi (pasif, başlangıç kartı, açılma yolu) ve 72 kozmetik (kask, vizör yüzü, tulum deseni, çanta süsü, Çapa kaplaması, jest). Dosyalar: yeni `Member*.cs`, `Cosmetic*.cs`, `members.json`, `cosmetics.json` |
| ilerleme4 | 12 | Sonnet 5.5 | ICERIK §18 kalan 24 iş emri zinciri, başarımlar, katalog ve çark tabloları. Dosyalar: `Orders*.cs`, `orders.json`, `achievements.json`, `catalog.json`, `wheel.json` |
| kabul | 13 | Sonnet 5.5 | M4 kabulü: `DataCheckTests` adetleri PLAN §7.1 ile birebir; hızlandırılmış bot başlangıçtan Ana Maya'ya; biyom ekran görüntüsü setleri; kalan kırmızı testler. Dosyalar: yeni `Bot.AnaMaya*.cs` |

**Efekt bağlanma notu (efekt, M4):** Yeni sistem görsel efekti `Vfx.Play("kimlik", konum[, yön, renk])` ile çağırır; tarifi `game/data/vfx.json` `effects` içine satır ekleyerek yazar (bilinmeyen kimlik sessiz geçer). Capa başlığı `head.<başlık>.muzzle|impact|trail` (yoksa `head.default.*`), güç/ulti `power.<kimlik>` (yoksa `power.default`; kusam akışı yeni güçler için yalnız bu satırları ekler, kod gerekmez), `Vfx.Telegraph(konum, yarıçap, süre, tehlike)` saldırı işareti, `Vfx.Loop/Stop` sürekli aura. Durum auraları ve olay efektleri `VfxSystem`'de merkezi; sistem dosyalarına tek satırlık `Vfx.*` çağrısı girer.

**Holding bağlanma notu (holding4, M4):** `holding.json` `sites`: 2. kamp Işıl Sazlık (Ocak T3), 3. kamp Kızıl Kanyon (T4), Genel Merkez (`kind: "hq"`, T5, krater eteği, seviye sabit 10); yerler `HoldingSites.Resolve` ile biyomdan çözülür (`HoldingSystem.SitePos`), kervan teslimi `DepotOf`. Genel Merkez yalnız Kabarma gecesi girilir (Perde), baskın ve kervanı yoktur; Çekirdeği düşünce Müdür gelir (`HoldingState.Mudur`, `MudurRules`; asamalar avlu, yönetim katı, çatı), safakta kaçar ve GM yeniden kurulur; yenilince `HoldingState.Unlocks`: `head:yercekimi`, `plan:efsane_cip`, `cosmetic:kask_holding_muduru`, `cosmetic:kaplama_holding` (ilgili akışlar Sim'den sorar). Müdür parçaları `IAtaTarget` (`MudurPart`, `Atalar.Targets`). GECİCİ: kamyon gövdesi `HoldingMudur.cs` `BuildTruck` (araç akışının aracı gelince onun gövdesi takılır). Test: `--test=holding`.

**Tam test nöbetçide (19 Kasım 2026, Godot kuyruğu tıkandığı için):** akışlar commit öncesi tam `test.ps1` koşmaz. Yalnız `-Only build,sim` ve kendi değiştirdiği alanın Godot testlerini koşar, sonra `tools/integrate.ps1`. Tam koşuyu main üzerinde `nobetci` oturumu düzenli koşar; kırmızıyı ayırır ve ilgili akışa bildirir.

## M5 akışları (19 Kasım 2026)

M5 (URETIM §6): çok oyunculunun tamamı. Kurallar M4 ile aynı (kendi dal, `tools/integrate.ps1`, Sonnet varsayılan, testler hafif, ölçüm yok, kredi yok, sahip uyurken soru yok). M4'ün son akışlarıyla (dolap, ilerleme4, kabul) aynı anda yürür; toplam en çok 5 akış. Ağ kalıbı M2 "Ortak oyuna bağlanma" notudur; her akış bitince Opus high kaba hata bakışı yapılır.

| Akış | Sıra | Model | Sahip olduğu dosyalar ve işler |
|---|---|---|---|
| sunucu | 1 | Sonnet 5.5 | TEKNIK §5, §12: adanmış sunucu (Windows ve Linux, başsız Godot, dışa aktarma ön ayarı), sunucu yönetim komutları (atma, yasaklama, kaydet, duyuru, şifre), sunucu listesi (Steam sunucu tarayıcısı ve ENet doğrudan bağlantı). Dosyalar: yeni `game/scripts/server/`, `data/server.json`, `export_presets.cfg`'ye satır |
| cekisme | 1 | Sonnet 5.5 | GDD §22.2: Çekişme dünyası ve on kuralı (gündüz dokunulmazlık, Açık Silo, çevrimdışı koruma, enkaz planı ve ötekiler); her kural için senaryo testi. Dosyalar: yeni `src/Kabarma.Sim/Rivalry*.cs`, `game/scripts/rivalry/`, `data/rivalry.json` |
| sekiz | 2 | Sonnet 5.5 | GDD §22.1, TEKNIK §5.4: 8 oyuncuya çıkış (ilgi alanı, 5 Hz bütçesi içinde kalan yazım), dünyayı devretme (host ayrılınca), telsiz kanalı (sesli sohbetin uzak kanalı), 8 botluk `--test=coop`. Dosyalar: `game/scripts/net/`, `src/Kabarma.Sim/Net*.cs`, `data/net.json` |
**Çekişme bağlanma notu (cekisme, M5):** Dünya ayarı `State.Rivalry` (`RivalryState`, kayıt bölümü `rivalry`, ağ bölümü `rivalry`); kurallar `RivalryRules` (`src/Kabarma.Sim/RivalryRules.cs`, sayılar `rivalry.json`). Kurmak: `_d.Riv.Start(dedicated, takımlar)`. Hasar vermeden önce karar: `_d.Riv.Verdict(konum, saldıranTakım, şekil, oyuncuMu)` ya da yapı için `_d.Riv.RaidPiece(parça, hasar, takım)` (host; yıkılan parça enkaz planı olur, `Rebuild` bedelin %50'si). `Enabled` false iken hepsi no-op. Acik Silo: `LootSilo`; ekin kabarması `Farm.SwellFilter` ile çevrimdışı takımda durur. Test: `RivalryTests` (10 kural) ve `--test=coop` Çekişme adımı. Bilinen boşluk: oyuncu-oyuncu silah yolu yok (kural 3 yalnız kapı ve yağma olarak hazır); Kabarma tek ortak simülasyon, takıma özel Kazan `ToppleKazan` ile dışarıdan bildirilir.

**Sunucu bağlanma notu (sunucu, M5):** Adanmış sunucu `Godot --headless --path game -- --server` (belge `docs/SUNUCU.md`, ayarlar `data/server.json`, kod `game/scripts/server/`). `ServerRunner` host oyunu kurar (`NetSync.Dedicated`: oyuncu karakteri gizli, `Players()`/`PlayerCount`te sayılmaz, roster adı `[Sunucu] ...`), kayıt yuvasından yükler ve yazar, katılma denetimi `NetSync.JoinGate` (`ServerAdmin.CheckJoin`: yasak, şifre, ad), şifre Hello ad alanında `ad\u001fşifre`. Yönetim `ServerAdmin.Execute` (konsol, `server_commands.txt`, oyun içi F8 + `giris <admin_password>`; `srv.cmd`/`srv.reply` ve duyuru `srv.msg` bölümleri `ServerNet`). Yeni yönetim komutu `ServerAdmin.Execute`'e bir `case`. Sunucu listesi ana menüde "Sunucular" (`ServerListPanel`, Steam tarayıcısı `SteamServerBrowser`, sunucu kaydı `SteamListing`). Test: `--test=server`.

## M6 akışları (19 Kasım 2026)

M6 (URETIM §6): cila. Yeni oynanış içeriği yok. Kurallar M5 ile aynı. Kredi ya da ElevenLabs isteyen her şey (ses, müzik, seslendirme, model) yalnız istek listesi olarak PROGRESS "Kredi kuyruğu"na yazılır; anahtar her kullanımda sahibin onayını ister. Performans turu en sondadır (sahibin kararı).

| Akış | Model | Sahip olduğu dosyalar ve işler |
|---|---|---|
| arayuz | Sonnet 5.5 | Arayüz cilası (bütün paneller tek görsel dil, Rust tarzı), erişilebilirlik ayarları (yazı boyu, renk körlüğü, sarsıntı kapatma, tuş yeniden atama, altyazı), fotoğraf modu. Dosyalar: `Hud*.cs` (yalnız cila), yeni `Settings*.cs`, `PhotoMode*.cs`, `data/ui.json` |
| dil | Sonnet 5.5 | 10 dil (TEKNIK §12): metin anahtarlarına geçiş, `ItemNames` ve bütün arayüz metinleri çeviri tablosunda, TR ve EN tam; öteki 8 dil makine çevirisi + işaret. Dosyalar: yeni `game/locale/`, `Loc*.cs` |
| ogretici | Sonnet 5.5 | Öğretici ve Suna'nın bütün replikleri (TR ve EN metin; ses kaydı kredi kuyruğuna). Dosyalar: `scenario.json`, `Director.Scenario.cs`, yeni `data/suna.json` |
| steam | Sonnet 5.5 | Başarımlar (Steam), Steam Cloud, zengin durum bilgisi; Steam yokken sessizce geçer. Dosyalar: yeni `game/scripts/steam/`, `achievements.json`'a satır |
| denge | Sonnet 5.5 high | Simülasyonla denge turu (ekonomi, Kabarma KS eğrisi, yaratık ve Ata canları); sayılar `data/*.json`'da, gerekçe `docs/` altında bir denge notunda |
