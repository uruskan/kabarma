# Kayıt: durum denetimi (M2, `kayit` akışı, 18 Kasım 2026)

Main `f38521f` üstünde. Soru: bir oyunu geri kurmak için gereken değişken durum nerede, `Sim`'de mi Godot düğümünde mi, kayıtta nasıl tutulacak. Sözleşme TEKNIK §9.1'de; bu belge her akışın neyi bağlayacağını söyler.

**Kısaltmalar:** S = `src/Kabarma.Sim`, G = `game/scripts`. "Bölüm" = `[SaveSection]` (Sim) ya da `Director.Save.Add(...)` (Godot).

## Genel bulgular

1. Kodda tek geri yükleme yöntemi `WaterTank.Restore` vardı. Diğer `Sim` sınıfları durumu `private set` / `internal set` / özel koleksiyonlarda tutuyor. Her sınıfa **sessiz** bir `Load`/`Restore` gerekiyor (olay yaymaz, sayaç ilerletmez).
2. Sim'in değiştiricileri olay yayıyor ve Godot onlara bağlı: `Backpack.Changed → Known, Belt`, `Farm.Planted → Orders`, `Scenario.StepChanged → Suna, OnStepStart`. Yüklemeyi bu yollardan "yeniden oynatmak" sayaçları iki kez ilerletir. Bu yüzden yükleme sırası şöyle: önce dünyadakiler kurulur (`Spawn`), sonra sistem durumu (`State`), en son ilerleme (`Progress`).
3. Arazi yüksekliği hiç değişmiyor (tohum + göl oyması). **Kaydedilecek arazi farkı yok.** Sürülmüş toprak ve temizlenen çim, tarla karelerinden ve yerleştirilmiş nesnelerden yeniden türetilir.
4. Gece (`KabarmaNight`) özel Rng, kart rütbeleri, Öz ve olay abonelikleri taşıyor. Kartlar şafakta bitiyor. **Karar:** gece sürerken (Alacakaranlık, Gece) kayıt alınmaz. Otomatik kayıt şafağa ya da gündüze kayar; çıkışta gece sürüyorsa gecenin başındaki kayıt kalır. Gece sayacı (`NightsStarted`) ve son rapor saklanır.
5. Elde tutulan nesne, itilen araba ve yere düşmüş oyuncu kayıt anında bırakılır ya da beklenir. Kayıtta nesne başvurusu yoktur.

## Sistem sistem

| Sistem | Durum | Yer | S/G | Kayıtta | Sim'e taşınmalı mı |
|---|---|---|---|---|---|
| Oyuncu | konum, yaw, kamera eğimi, `Hp`, `RespawnPoint` | `Player.cs:15-32` | G | `player` bölümü (Godot), oyuncu kimliğine göre liste | Hayır (fizik gövdesi Godot'ta). `RespawnPoint` açıkça saklanır |
| Oyuncu | çanta, kemer, seçili yuva, su deposu | `Inventory.cs`, `WaterTank.cs` | S | `player.pack` (**bağlandı**) | Zaten Sim |
| Oyuncu | dayanıklılık `Value/Exhausted/SinceSprint` | `Stamina.cs:28-38` | S | `player.pack`'e eklenir | Zaten Sim; `Restore` gerekli |
| Oyuncu | yemek etkileri `_active` (FoodDef başvurusu) | `Crafting.cs:108` | S | kimlik + kalan süre | Zaten Sim; `Restore(id, left)` |
| Oyuncu | Çapa başlığı `Capa.Current` | `Capa.cs:25` | G | `player` bölümü | Hayır; sessiz seçici gerekli (`Select` ses çalar) |
| Oyuncu | açık silahlar `CapaState.Unlocked` | `CapaState.cs` | S | `capa` | Zaten Sim |
| Dünya | kaya `Stones`, aşınma; tükenen kaya kendini siler | `Rock.cs:11-18`, adı `{düğüm}_{tohum}` | G | `world.rocks`: ad → kalan taş | Evet, M2 dunya2 kaynak/yenilenme kuralıyla; şimdilik ad anahtarıyla Godot bölümü |
| Dünya | tutam `Tuft.Left` | `World.Resources.cs:26` | G | `world.tufts`: sıra → kalan | Aynı |
| Dünya | ağaç | `Capa.Bicme.cs` | — | yok (sonsuz, durumsuz) | — |
| Dünya | yeniden doğma sayacı | — | — | henüz yok (GDD §21: 5–10 gün); dunya2 eklerken bölüm yazar | Evet (Sim) |
| Yapı | parçalar `_pieces`: `Def.Id`, dönüşüm, `Hp`, `_serial` | `BuildSystem.cs:90-94` | G | yapi akışının `build` bölümü devraldı (kayit'ın `build.pieces`'ı kaldırıldı; ağıl kapıları Sim kimliğiyle) | Durum Sim'e (`Structure*.cs`, yapi akışı); kurulum `PlaceAt` ile, `Hp` sonra |
| Yerleştirilebilir | istasyonlar, Ocak, mobilya (Sandık, Uyku Tulumu, Meşale) | `Director.Menus.cs:10,62`, `Director.Economy.cs:45` | G | `placeables`: `EntityRecord` | Hayır; `Place*`'e "yükleniyor" bayrağı (olay, ses, efekt, `RespawnPoint` yok) |
| Yerleştirilebilir | kapsül rampası `Pod.IsRamp` | `Pod.cs:11` | G | `placeables` içinde parametre | Hayır |
| İstasyon | kuyruklar: tür başına tek `StationQueue` (`Active`, `Progress`, `Queued`, `_fuel`) | `Crafting.cs:13-63` | S | `stations` | Zaten Sim; `Restore` gerekli |
| İstasyon | el üretim kuyruğu `_jobs`, `Blocked` | `Crafting.Queue.cs:59-63` | S | `craft` | Zaten Sim; `Restore` gerekli |
| Kap | Sandık `Furniture.Storage` | `Placeables.cs:202` | G düğüm, S veri | `placeables` kaydında yuvalar | Envanter Sim'de; sahibi Godot düğümü |
| Kap | El Arabası `Slots`, `Load` (kabarık ürün başvurusu) | `ElArabasi.cs:17-20` | G | `carts`: kayıt + yuvalar | Hayır; yüklü ürün kimlik + boyla |
| Kap | kargo kasası `CargoCrate` (`Id`, `Items`), rampada / arabada | `CargoCrateNode.cs:16-20` | G | `crates` | Hayır |
| Kap | yerdeki kabarık ürün `SwollenCrop` | `FarmView.cs:~215` | G | `swollen`: ürün + boy + dönüşüm | Hayır |
| Tarla | `_plots`: X, Y, Crop, Genes, Growth, Moisture, Fertility, NeverDry, Swollen (boy kimliği), Eaten, Harvested, SinceDawn, Deflated | `Farm.cs:6-32` | S | `farm` | Zaten Sim; `Farm.Load` + `FarmView.Rebuild` |
| Ağıl | `Coop.Animals` (tür, öğretici, güven, kayıt, yumurta sayacı) | `Coop.cs:8-21,134` | S | `pen.animals` | Zaten Sim; `Gidak._animal` bağlantısı yeniden kurulur |
| Ağıl | Yemlik `Feeder.Amount`, Folluk `Nest.Eggs` | `Coop.cs:135-136` | S | ilgili `PenProp`'un kaydında | Evet: Sim'de konuma ya da örnek kimliğine bağlanmalı (hayvan akışı). Şimdilik `PenProp` kimliğiyle |
| Ağıl | kapı `PenProp.DoorOpen`, bahçe kapısı `Gate.Open`, yerdeki yumurta | `PenProp.cs:18`, `PenSystem.cs:39-46` | G | `pen.props`, `pen.gates`, `pen.eggs` | Hayır |
| Ağıl | Gıdak: `Fed`, görünürlük, konum | `Gidak.cs:19-23` | G | `creatures.gidak` | Görünürlük senaryo adımından türetilir |
| Yaratık | gündüz Zıpzıp sürüleri, `_rng` `GD.Randi()`'den | `ZipzipMeadow.cs:11-40` | G | **kaydedilmez**: yükleyince yeniden doğar | Rastgelelik tohumsuz: yaratik2'ye not |
| Yaratık | gece düşmanları `Zipzip.All` (statik liste) | `Zipzip.cs:20` | G | gece kaydı yok (genel bulgu 4) | — |
| Yaratık | Pofuduk | `Pofuduk.cs:17-20` | G | konum yeter | — |
| Takvim | `Running`, `Day`, `Seconds`, `NightsSkipped`, `Paused` | `Calendar.cs:15-21` | S | `calendar` | Zaten Sim; sessiz `Restore` (`Start` Morning yayar) |
| Gece | `NightsStarted`, `_salesAtNightStart`, `LastReport` | `GameState.Night.cs:10-17` | S | `night` | Zaten Sim |
| Gece | `Director.Phase`, `_actT`, `_reportShown`, `_replay` | `Director.cs:35-37`, `Director.Night.cs:13-17` | G | `director` | Evre Sim'e taşınabilir; şimdilik Godot bölümü |
| Senaryo | `StepIndex`, `Progress` | `Scenario.cs:7-8` | S | `scenario` | Zaten Sim; sessiz `Restore` |
| Senaryo | `_wateredPlots`, `_madeThisStep`, `_placedIds`, `Harvests` | `Director.Scenario.cs:15-17,150` | G | `scenario.director` | Evet, ilk fırsatta Sim'e (Scenario sayacı); şimdilik Godot bölümü |
| Suna | kapak girişi `Pod.IntroDone` | `Pod.Hatch.cs:15` | G | `director` | Yüklemede giriş atlanır. Söylenen replikler adımdan türetilir |
| Defter | `DefterOpen`, zincir `Opened`, `Counts`, `_done` | `Orders.cs:7-52` | S | `orders` | Zaten Sim; `Restore` gerekli |
| Kartlar | `_ranks`, `Offer`, `PendingLevels` | `Night.cs:75-137` | S | şafakta biter; yalnız `LastReport.CardsAtDawn` | — |
| Ekonomi | `Credits`, `Sales` | `Scenario.cs:63-66` | S | `core` (**bağlandı**) | Zaten Sim |
| Ekonomi | `Cargo`: `Open`, `_closed`, `_pad`, `_pending` (ödeme sayaçları), `_serial`, `FirstSaleDone` | `Economy.cs:66-84` | S | `cargo` | Zaten Sim; `Restore` gerekli |
| Rastgele | `Rng._s` | `Inventory.cs:6` | S | `core` (**bağlandı**, `Rng.State` eklendi) | — |

## Godot statikleri

| Statik | Yer | Yükleme / yeni oyunda sıfırlanır mı |
|---|---|---|
| `Events.*` (20 olay) | `autoload/Events.cs` | Evet; `Events.Clear()` zaten `Director._Ready`/`_ExitTree`'de |
| `Zipzip.All` | `Zipzip.cs:20` | Evet (`Clear`) |
| `Fx.Root` | `Fx.cs:9` | Evet (yeni Director atar; eskisi bırakılır) |
| `Sfx._root/_d/_a/_beam/_beamId/_music`, `Live`, `Held`, `LastVariant`, `_duck` | `Sfx.cs:19-29,194`, `Sfx.Buses.cs:16` | Evet: eski düğüm başvuruları temizlenir. `Streams`, `VariantFiles`, `Warned` önbellek |
| `MaterialUplift._done` | `MaterialUplift.cs:16` | Evet (örnek kimlikleri) |
| `UiTheme.ScaleChanged` | `UiTheme.cs:20` | Eski Hud abonelikleri sızmasın diye abonelikler çözülür |
| `Main.Args`, `Sfx.Muted`, `UiTheme` ölçeği, yazı tipleri, `ItemIcons`, `MeshKit`, `Swellable`, `Assets`, `World` doku dizileri, `Pod._kapsul`, glifler | çeşitli | Hayır (komut satırı, ayar ya da önbellek) |

Sıfırlama tek yerde: `game/scripts/save/GameStatics.cs` (`GameStatics.Reset()`), yeni oyun ve yükleme öncesi.

## Bağlantı durumu (kayit akışı, 2. teslim)

| Bölüm | Kapsam | Sıra | Yer |
|---|---|---|---|
| `world.settings` | GDD §25 dünya ayarı | Spawn−50 | `GameState.Save.cs` |
| `build` + `build.view` | yapı parçaları (malzeme, plan, can): yapi akışının Sim bölümü; düğümler `RebuildFromGraph` ile (AfterRestore) | Spawn | `StructurePlanBook.cs`, `building/BuildBridge.Yapi.cs` (yapi) |
| `placeables` | Ocak, istasyonlar, mobilya, Sandık içi | Spawn+10 | aynı |
| `pen.props` | Yemlik yemi, Folluk yumurtası, Ahşap Kapı | Spawn+20 | aynı |
| `pen.ground` | açık bahçe kapıları, yerdeki yumurta | Spawn+30 | aynı |
| `world.resources` | kaya (ad → kalan), tutam verimi | Spawn+40 | aynı |
| `loads` | El Arabası (yuvalar, yük), kargo kasası (rampa, araba), yerdeki kabarık ürün | Spawn+50 | aynı |
| `farm`, `pen.animals`, `stations`, `craft`, `cargo`, `capa`, `calendar`, `night`, `player.pack` | Sim sistemleri | State | `GameState.Save*.cs` |
| `player` | konum, bakış, can, doğma noktası, Çapa başlığı | State+10 | `save/Director.SaveSections.cs` |
| `creatures` | Gıdak: görünürlük, konum, yem; hayvan kaydına bağ | State+20 | aynı |
| `core`, `scenario`, `orders` | kredi, satış, Known, Rng, kimlikler; senaryo; Defter | Progress | `GameState.Save*.cs` |
| `director` | evre, rapor, senaryo yardımcı sayaçları, hasat, rampa | Progress+10 | `save/Director.SaveSections.cs` |

Kaydedilmeyenler ve nedeni: gündüz Zıpzıp sürüleri (yeniden doğar), gece düşmanları ve kartlar (gece kaydı yok), Pofuduk (sabit yuva), Hasat Raporu (bir kez gösterilir).

## Belge notu

CLAUDE.md `World.Placeholders.cs`'ten söz ediyor, ama bu dosya main'de yok. Yer tutucu düğümler `World.Resources.cs` içinde (dunya akışı).
