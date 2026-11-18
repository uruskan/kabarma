@AGENTS.md

# Kabarma: proje kuralları (her oturum için geçerli)

Bu bölüm sahibin kurallarıdır ve yukarıdaki Genex sözleşmesiyle çeliştiği yerde **bu bölüm geçerlidir**. Özellikle Genex sözleşmesinin 4. maddesi ("izin sormadan üret") bu projede geçerli değildir.

## Belgeler tek kaynaktır

`docs/PLAN.md` (buradan başla) · `docs/GDD.md` (kurallar) · `docs/ICERIK.md` (içerik) · `docs/SANAT-SES.md` (görsel, ses) · `docs/TEKNIK.md` (mimari, test) · `docs/URETIM.md` (hat, bütçe, kilometre taşları) · `docs/stil/README.md` (stil föyü) · `docs/PROGRESS.md` (ilerleme günlüğü).

- Sıra: **önce belge, sonra veri, sonra kod** (URETIM §7). Belgeyle çelişen bir şey gerekiyorsa koda geçmeden önce dur ve raporla.
- Kurallar motordan bağımsız `src/Kabarma.Sim` içinde ve testli; sayılar `game/data/*.json` içinde, kodda sihirli sayı yok.
- Kilometre taşı sırası URETIM §6'dadır, ama **M1 kapısı kalktı** (sahibin kararı, 18 Kasım 2026, URETIM M1 kararı 22): oyunun tamamı hızla yapılır, sahip sonda oynar ve düzeltme ister. Efor yalnız medium/high (xhigh ve üstü yasak); test hafif, raporlar kısa.
- **Sormadan ilerle (sahibin kararı, 18 Kasım 2026):** ana oturum taşları (M2 → M7) sahibe "devam edeyim mi" diye sormadan sırayla yapar; sahip sonda oynar. Sorulan yalnız şunlar: kredi tutarı, `git push`/yayın, sahibin dosyasını silmek. Model: kolay işler (belge, veri girişi, ikon, küçük düzeltme, test) **Sonnet 5.5**; varsayılan **Opus 5.5 medium**; yalnız en zor işler (ağ/ortak oyun, kayıt, yapı dengesi, yol bulma) **Opus 5.5 high**. Danışman (advisor) Fable kalır.
- "Bitti" = PLAN §7.2'nin altı koşulu. Raporda her koşulu ayrı yaz: geçti / kısmen / yok / M2'de (ortak oyun yarısı ve kayıt, URETIM §6 M1 kapsamı). Sahibin yargısına kalanları (his, dinleme, 30 dk oynama, RTX 2070S FPS) asla "geçti" diye yazma; "sahibin onayını bekliyor" yaz.
- M1 kapsamı ve kararları: `docs/URETIM.md` §6 "M1 kapsamı ayrıntısı" ve "M1 kararları" tabloları. Önce bunları oku.
- Yer tutucu varlıklar `data/` içinde işaretlenir (URETIM §3).

## Para ve dış dünya

- **Kredi harcayan hiçbir üretimi sahibin o tutara açık onayı olmadan başlatma** (Genex, Tripo API, Tripo web, ElevenLabs). Önce ne, kaç adet, hangi havuz, kaç kredi; sonra onay. Alt oturumlar ve alt ajanlar **hiç kredi harcamaz**; ihtiyacı listeleyip ana oturuma bildirir.
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

**Ortak oyuna bağlanma (ortak akışı, `game/scripts/net/NetSync.cs`):** Tek oyuncuda `_d.Net` null'dur; bütün kancalar `_d.Net?.X() == true` biçimindedir. (1) Sistemin durumunu `_d.Net?.Register("kimlik", perPeer, (w, peer) => yaz, r => uzlaştır)` ile ya da bir `INetSection` ile kaydet: yazım kanonik olsun (sıralı, yuva düzeni yok), okuma eksik olanı kursun, fazlayı kaldırsın; host değişeni 5 Hz'te, tamamını katılmada gönderir, 10 sn'de bir özetleri karşılaştırır. (2) Dünyayı ya da çantayı değiştiren her eylemin başına tek satır `if (_d.Net?.Forward("istek", w => ...) == true) return;` ekle; host'ta `_d.Net.On("istek", (actor, r) => ...)` içinde `NetActor`'ün (kendi `Bag`'i, son konumu) menzilini ve bedelini doğrula, sonra sistemin genel girişini çağır; bu yüzden yeni girişler `_d.Player` / `_d.State.Backpack` yerine aktörü ya da çantayı parametre alsın. (3) Yaratık düğümü `NetSync.CreatureGroup` grubuna girer (host'ta yapay zekâ, istemcide kukla); hedef seçimi için bütün oyuncular `_d.Net.Players()`.

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
