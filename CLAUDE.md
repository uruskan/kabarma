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
