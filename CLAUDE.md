@AGENTS.md

# Kabarma: proje kuralları (her oturum için geçerli)

Bu bölüm sahibin kurallarıdır ve yukarıdaki Genex sözleşmesiyle çeliştiği yerde **bu bölüm geçerlidir**. Özellikle Genex sözleşmesinin 4. maddesi ("izin sormadan üret") bu projede geçerli değildir.

## Belgeler tek kaynaktır

`docs/PLAN.md` (buradan başla) · `docs/GDD.md` (kurallar) · `docs/ICERIK.md` (içerik) · `docs/SANAT-SES.md` (görsel, ses) · `docs/TEKNIK.md` (mimari, test) · `docs/URETIM.md` (hat, bütçe, kilometre taşları) · `docs/stil/README.md` (stil föyü) · `docs/PROGRESS.md` (ilerleme günlüğü).

- Sıra: **önce belge, sonra veri, sonra kod** (URETIM §7). Belgeyle çelişen bir şey gerekiyorsa koda geçmeden önce dur ve raporla.
- Kurallar motordan bağımsız `src/Kabarma.Sim` içinde ve testli; sayılar `game/data/*.json` içinde, kodda sihirli sayı yok.
- Kilometre taşı sırası sabittir (URETIM §6). Şu an **M1** (görünüş ve his dilimi). M2'ye ait iş, sahip M1 kapısını açıkça geçmeden yapılmaz.
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

- Aynı anda birkaç oturum, her biri kendi worktree dalında çalışır. **Dallar main'e birleştirilmez**; sahip kendisi inceleyip birleştirecek. Bu yüzden her dal kendi başına derlenmeli ve yeşil olmalı; başka bir akışın henüz yazmadığı koda dayanma.
- Dosya sahipliği aşağıda. Başka akışın dosyasına dokunman gerekiyorsa küçük, yerel değişiklik yap ve raporda say. Ortak noktalar (`Bot` kurucusundaki `Register` listesi, `WireScenario`, `Events.cs`, `Main.Tests`, `tools/test.ps1`, `Capa.Process` içindeki başlık anahtarı, `Hud`/`Director` `_Ready`/`_Process`) yalnızca **satır ekleyerek** değişir; var olan satırı yeniden düzenleme.
- Yeni işi yeni dosyalara koy (partial class, yeni test dosyası, yeni veri dosyası, yeni `pipeline/*.py`).
- Kredi gerektiren üretim (model, iskelet, klip, ses, müzik, görsel) alt oturumda **çalıştırılmaz**: istemleri ve betiği hazırla, dry-run tahminini çıkar, ana oturuma "üretim isteği" olarak bildir.
- Bitince: `tools/test.ps1` yeşil. Kendi dalına commit et, push etme. Kanıt `progress/m1/<akış>/` altına (ekran görüntüsü, log). Raporun sonunda PLAN §7.2 koşullarını teslim başına yaz.

| Akış | Sahip olduğu dosyalar ve işler |
|---|---|
| dunya | `World*.cs`, gökyüzü/su/bitki shader'ları, `data/biomes.json`, süs modelleri (`cali`, `bugday`, `cicek`, `ot`, `turuncu_agac`, `egrelti`, `kaya_duz`, `kaya_dik`, `bakir`), kil yatağı, kaynak düğümleri (`Props.cs` içindeki kaya/kaynak türleri), Sulama deposu ve su yüzeyi (`Capa.Sulama.cs`), Biçme'nin ot/çalı/yabani buğday verimi, yer imleri, 16 görüntü aracı, FPS ölçümü |
| varlik | `pipeline/clean_asset.py`, `style_sheet.py`, `gen_golden.sh`, altın varlık klasörleri (`agac`, `kaya`, `kova`, `salgam`, `tezgah`, `tas_firin`, `el_arabasi`, `ocak`, `kapsul`, `paketleme`, `capa`), `data/assets.json`, `docs/stil/` |
| oyuncu | `Player*.cs`, `PlayerRig.cs`, `Visor.cs`, `pipeline/merge_player.py`, `assets/models/player/`, dayanıklılık (Sim + `Hud.Stamina.cs`) |
| capa | `Capa*.cs` (Sulama deposu ve İnşa hariç), `Feel.cs`, `data/heads.json`, `weapons.json`, `feel.json`, vuruş efektleri |
| yaratik | `Creatures.cs` animasyon ve şişme kısmı, `PoseLibrary.cs`, `Pofuduk.cs`, `Swellable.cs`, şişme shader'ı, `gidak`/`zipzip`/`pofuduk` modelleri, `data/creatures.json` |
| ilkgun | `data/scenario.json`, `Director.Scenario.cs`, yeni bot adımları, ağıl, kapı/kapı kanadı açılma, Ahşap Kapı, Yemlik, Folluk, Gıdak'ın ağıl davranışı, yumurta eşyası, Zıpzıp'ın çit hücumu ve çit hasarı, Suna'nın 30 repliği (TR + EN) |
| ekonomi | `recipes.json`, `placeables.json`, `foods.json`, `prices.json`, `Station.cs`, El Arabası, kargo kasası, Kargo Rampası ve kapsül (`Props.cs` içindeki `Pod`), Ocakbaşı, Sandık, Uyku Tulumu, Meşale, söküm ve onarım (`BuildBridge*.cs`), `BuildTest.cs`, yığın boyları, yemek etkileri |
| gecehud | `Night.cs`, `cards.json`, `kabarma.json`, `Hud*.cs` (`Hud.Stamina.cs` ve `Hud.Voice.cs` hariç), `Director.Night.cs`, Defter ve iş emirleri (`orders.json`), envanter arayüzü, pusula |
| girdi | `Main.cs` girdi eşlemesi, oyun kolu, `Director.Input.cs`, girdi glifleri, gerçek girdi sürücüsü testi, `tools/test.ps1`, `game/scripts/tests/`, Sim kıyaslama testi |
| ses | `Sfx.cs`, ses kanalları, `data/audio.json`, müzik durum makinesi, `Hud.Voice.cs`, `pipeline/audio_qc.py`, `pipeline/eleven.py`, `pipeline/licenses.md` |
