# M3 gucler: oyuncu güçleri, Kadim Taş, Maya Feneri, Tatbikat

Kural: GDD §17, §17.1 (yeni), §14.6, §14.7 (M3 kuralı, yeni); ICERIK §7 (M3 sayıları, yeni). Veri: `game/data/abilities.json`.
Sim: `src/Kabarma.Sim/Ability*.cs`, `KadimTas.cs`, `Tatbikat.cs`, `GameData.Abilities.cs`; testler `AbilityTests.cs`.
Godot: `game/scripts/abilities/`.

## Ne var

- **Beş güç:**
  - Atılma: 7 m, ilk 0,35 sn hasar yok.
  - Kalkan Balonu: 3 m kubbe, 4 sn. Dışarıdan gelen hasar geçmez ve düşman içeri giremez.
  - Şok Dalgası: 6 m alanda 25 hasar ve itme. Kabarma düşmanına, hayalete ve gündüz yabanisine işler.
  - Yem Hologramı: 8 sn. Hayaletler ona saldırır, Kabarma düşmanı ona itilir.
  - Şifa Sisi: 5 m, 6 sn, saniyede 8 can.
- **İki ulti:**
  - Sürü Çağrısı: dört hayalet hayvan (yer tutucu) 25 m koşar, her biri 40 hasar verir.
  - Dev Mod: 20 sn, boy ×2,5, alınan hasar −%50, ezme hasarı.
  - Ultiler Ata'dan gelir; kaynak `AtaProgress.Ultis`.
- **Yuvalar:** 1 etkin yuva açık. 2. yuvanın tuşu bağlı ama kilitli (Kurbağa Ağa, M4). 1 ulti yuvası var. Yuva tamamlanmış bir Kadim Taş'ın E menüsünde değiştirilir.
- **Şarj:** 100. Son harcamadan 1,5 sn sonra saniyede 5 dolar.
- **Maya ölçeri:** 100. Kabarma'da öldürme başına 2 (elit 6). Ata dövüşünde canının %1'i başına 2.
- **Beş Kadim Taş ve denemeleri:**
  - parkur: halkalardan sırayla geçilir.
  - koru: Taşın Gıdak'ı 120 sn korunur.
  - dalga: üç ya da dört dalga temizlenir.
  - gizlen: gözcü konilerine görünmeden hedefe varılır.
  - Şifa denemesi: "kimse yere düşmesin".
  - Kaybedince taş 10 sn dinlenir. Kazanınca güç ekibin tamamına açılır.
- **Maya Feneri:** kurulur. Her fener günde bir kez yanar. KS, açılmış en yüksek KS'nin yarısıdır. Bedel 3 Maya Kesesi, 5 Bakır Tel, 2 Reçine; ilk Maya Kesesi alınınca açılır.
- **Tatbikat:** Ocak'ın E menüsünden başlar, KS seçilir. Hayalet dalgalar gelir, hasar gerçektir. Gece sayacı, Kredi ve kart ilerlemez. Bitince yapılar başlangıç canına onarılır.
- **Kayıt ve ortak oyun:**
  - Kayıt bölümü `abilities` (sürüm 1): açılan güçler, yerel oyuncunun donanımı, Kadim Taş ilerlemesi, fenerler.
  - Ortak oyunda `abilities` bölümü dünya durumunu taşır.
  - `abil` isteğinde host, istemcinin Şarj ve beklemesini kendi aynasında denetleyip etkiyi kurar. Fener `fener` isteğiyle yanar.
- **Tuşlar:**

  | | Klavye | Kol |
  |---|---|---|
  | Güç 1 | Alt | DPad ← |
  | Güç 2 | CapsLock | DPad → |
  | Ulti | Ctrl | R3 |

  Kart teklifi açıkken güç tuşları susar, çünkü kart seçimi aynı yön tuşlarını kullanır.

## Geçici arayüzler (işaretli, sözleşmeler main'e girince değişecek)

- **Kabarma başlatma (kabarma3, `Wave*`):** `IKabarmaStarter` ve `LocalKabarmaRequest` (`Tatbikat.cs`) geçici.
  - Fener şimdilik Director'ın Küçük Kabarma'sını açar. 40 m, 4 dk ve kart sınırı kabarma3 sözleşmesiyle uygulanacak.
  - Tatbikat dalgalarını şimdilik bu akışın `TrialGhost` hayaletleri yapar: GDD §14.3 sayısı ×0,25, 3 dalga.
  - `HighestKs` şimdilik `kabarma.json` firstNight.ks.
- **Hasar (savas, `Combat*`):** güçler `Zipzip.Hit`, `TrialGhost.Hit` ve `Wildlife.ProjectileHit` ile vurur. Player.Hurt'a tek satır süzgeç eklendi (Atılma, Kalkan, Dev Mod). Savaş arayüzü gelince ortak hasar girişine bağlanacak.
- **Maya Kesesi düşmesi:** kabarmış yaratık kesesiyle ölünce çantaya 1 kese girer (`TatbikatSystem.OnKilled`). Ganimet savas/kabarma3'e geçince silinecek.
- **Elit öldürme:** elit önekleri gelene kadar bütün öldürmeler sıradan sayılır (ölçere 2).

## Yer tutucular (kredi harcanmadı)

Hepsi koddan, düz renk ve yarı saydam malzemeyle:

- Kadim Taş monoliti ve runeleri
- parkur halkaları
- gözcü direkleri ve görüş konileri
- Taşın Gıdak'ı
- hayalet düşman
- Yem Hologramı
- Kalkan ve Şifa kubbeleri
- Sürü Çağrısı'nın hayalet hayvanları
- Maya Feneri

## Üretim isteği (ana oturum karar verir; çalıştırılmadı)

- **Model (Tripo):**
  - Kadim Taş: oyulmuş monolit, rune yüzeyi ayrı malzeme yuvası.
  - Maya Feneri: taş ayak, ahşap direk, bakır taç, ışıyan küre ayrı düğüm.
  - Gözcü totemi: göz düğümü ayrı.
- **Efekt ve ses (SANAT-SES §"Güçler ve ultiler"):**
  - 5 güç ve 2 ulti için kısa ses (ElevenLabs sfx).
  - Kadim Taş ışıması için bir ses.
- Bu oturumda kredi tahmini çıkarılmadı (dry-run koşulmadı).

## Başka akışların dosyaları (yalnız satır eklendi)

- `Director.cs`: `InitAbilities();`
- `Director.Input.cs`: `if (InteractAbilities()) return;`
- `Capa.Insa.cs`: `PlaceAbility` satırı.
- `NetSync.World.cs`: `HostPlaceItem` içine `PlaceAbility` satırı.
- `Player.cs` Hurt: süzgeç satırı.
- `Capa.Weapons.cs`: hayalet vuruşu, 2 satır ve 1 alan.
- `Main.cs`: Tests satırı `gucler`.
- `tools/test.ps1`: `-Only gucler` satırı.
- `ItemNames.cs`: `maya_feneri`, `maya_kesesi`.
- `input.json`: `ability_1`, `ability_2`, `ulti`.
- `recipes.json`, `placeables.json`: `maya_feneri`.
- `craft.json` sources: `maya_kesesi`, `resin`.
- `research.json` rawSources: `maya_kesesi`, `resin`.

## Açık işler

- **Kadim Taş yerleri:** `abilities.json`'da sabit. dunya akışı isterse `biomes.json` pois'e taşır; şimdi taşımak WorldTest'in 16 POI sayısını bozar.
- **Kol bağlaması:** DPad ←/→ kartlarla, R3 kamera kipiyle paylaşılıyor. girdi akışı akor ya da bağlam kuralı koyarsa değişir.
- **Ortak oyunda denemeler:**
  - Deneme host oyuncusu içindir; ekip denemesi M5'te.
  - İstemcinin ölçeri host'ta tutulmuyor (M5).
  - İstemcinin kendi donanımı kayda girmiyor (karakter dosyası, kayit akışı).
- **ICERIK §12 M3 sütunu:** Kabarma Sensörü ve siren (elektrik) hiçbir M3 akışında yok.
