# Kabarma: Teknik Tasarım

> Bu belgede "doğrulandı" yazanlar bu makinede (Ryzen 5 3600, RTX 2070 SUPER, Windows 11) çalıştırılıp görüldü. "Hedef" ve "M0'da doğrulanacak" yazanlar henüz ölçülmedi.

## 1. Araç zinciri

### 1.1 Bugünkü durum (16 Kasım 2026)

| Bileşen | Durum | Kaynak |
|---|---|---|
| Godot 4.7.2-stable (standart sürüm), Jolt fizik | ✅ Doğrulandı | Oduncu projesi |
| Başsız (headless) test, çok süreçli host-istemci senkronu, Movie Maker ile video | ✅ Doğrulandı | Oduncu: 0,27 mm senkron farkı, 60/60 senaryo |
| GodotSteam GDExtension 4.24 (Steamworks 1.65) | ✅ GDScript'ten doğrulandı | Oduncu |
| Python 3.14, Node.js, ffmpeg 9.0.2 | ✅ Kurulu | |
| Genex CLI (model, kemik, animasyon, doku, ses, müzik, görsel) | ✅ Kurulu (oduncu klasöründe) | `npx genex --help` |
| .NET 8 SDK | ✅ Kuruldu (8.0.428), 17 Kasım 2026 | `dotnet --list-sdks` |
| Godot 4.7.2 .NET | ✅ `.tools/` altında; C# projesi derleniyor, başsız ve pencereli çalışıyor | MVP + M0 |
| Blender 5.3 | ✅ Kurulu; başsız `bpy` hattı çalışıyor | `pipeline/clean_asset.py` |
| Steamworks.NET 2025.165.0 (SDK 1.65) | ✅ `lib/steamworks-2025.165.0/` | NuGet'teki 2024.8.0, SDK 1.65 `steam_api64.dll` ile uyuşmuyor (giriş noktası hatası); GitHub Standalone sürümü kullanılır |
| Tripo CLI 0.11.2 | ✅ Oturum açık (API kredisi ayrı havuz) | `tripo balance` |

Dış kaynaklardan teyit edilenler (16 Kasım 2026): Godot'un son kararlı sürümü 4.7.2; GodotSharp 4.7.2 .NET 8'i hedefliyor; C# projeleri Windows, Linux ve macOS'a dışa aktarılıyor, web'e aktarılmıyor. GodotSteam'in C# sürümü yok. Steamworks.NET 2025.165.0 (SDK 1.65) Networking Sockets, Networking Messages ve Matchmaking'i içeriyor. Godot `--headless` ve adanmış sunucu dışa aktarma kipini destekliyor; bunun .NET projesinde Linux'ta çalıştığı belgede açıkça yazmıyor.

### 1.2 M0 doğrulama adımları

Her adımın geçme ölçütü ve yedeği var. M0 bitmeden M1 başlamaz.

| # | Adım | Geçme ölçütü | Yedek |
|---|---|---|---|
| 1 | .NET 8 SDK ve Godot 4.7.2 .NET kurulumu; boş C# projesi | Başsız çalışıyor, Windows'a dışa aktarılıyor, .exe açılıyor | GDScript (Oduncu'daki zincir). `Sim` fikri korunur, test hızı düşer. |
| 2 | `Kabarma.Sim` kütüphanesi Godot projesinden çağrılıyor; `dotnet test` çalışıyor | 1.000 oyun günü 10 saniyenin altında simüle ediliyor | — |
| 3 | Steamworks.NET ile Steam başlatma, lobi, Networking Sockets üzerinden iki süreç arası mesaj | Uygulama kimliği 480 ile yerelde mesaj gidip geliyor | GodotSteam GDExtension'ı C#'tan `ClassDB` çağrılarıyla kullanmak |
| 4 | C# projesinin Linux başsız sunucu derlemesi | Sunucu açılıyor, Windows istemci ENet ile bağlanıyor | Adanmış sunucu yalnızca Windows; Linux, Proton ile |
| 5 | C# ile Oduncu'daki senkron testinin eşleniği | Son durum farkı 1 cm'nin altında | — |
| 6 | Blender kurulumu ve başsız `bpy` betiği: GLB al, sadeleştir, köşe rengine çevir, GLB ver | Betik komut satırından uçtan uca çalışıyor | Sadeleştirme ve renklendirmeyi Godot içe aktarma betiğinde yapmak |
| 7 | Üretim hattı denemeleri | [URETIM.md](URETIM.md) §2.3 | Aynı yerde |
| 8 | Sürü çizimi: köşe animasyon dokusuyla 2.000 örnek | RTX 2070S'te 2 ms'nin altında | Sürü üyelerini animasyonsuz, yay-salınımlı çizmek |

### 1.3 M0 sonuçları (17 Kasım 2026)

| # | Adım | Sonuç | Kanıt |
|---|---|---|---|
| 1 | .NET 8 SDK + Godot .NET; boş C# projesi | ⚠️ Kısmen. Başsız çalışıyor. Windows'a dışa aktarma denenmedi (`export_presets.cfg` yok), .exe açılışı doğrulanmadı. | MVP, `tools/run.sh` |
| 2 | `Kabarma.Sim` Godot'tan çağrılıyor; `dotnet test` | ⚠️ Kısmen: çağrı ve `dotnet test` çalışıyor (11 test). 1.000 oyun günü ölçümü henüz yazılmadı. | `src/Kabarma.Sim.Tests` |
| 3 | Steamworks.NET: başlatma, lobi, Networking Sockets ile iki süreç | ✅ Geçti: app 480, özel lobi kuruldu, iki süreç arasında 20/20 mesaj (ortalama 31 ms, döngüdeki bekleme dahil). Aynı hesapta iki süreç için Steam ağ sertifikasının hazır olmasını beklemek gerekiyor. | `progress/m0/steam_check.log`, `tools/spikes/SteamSpike` |
| 4 | C# Linux başsız sunucu | ⏸ Bu makinede doğrulanamadı: WSL ve Linux makine yok. Windows'ta C# başsız host çalışıyor (adım 5). Yedek, belgedeki gibi: adanmış sunucu önce Windows; Linux ikinci bir makinede ya da WSL kurulunca denenecek. | — |
| 5 | C# ile senkron testi (ENet, host yetkili) | ✅ Geçti: 24 fizik cismi, 600 tik, 600 anlık görüntü, 0 boşluk, son durum farkı 0,000 mm. Not: yerel döngü; paket kaybı benzetimi yok. | `progress/m0/netsync_check.log`, `game/scripts/tests/NetSyncTest.cs` |
| 6 | Blender başsız hattı | ✅ Geçti; iki kip: palet (eski yön) ve dokulu (yeni yön) | `pipeline/README.md` |
| 7 | Üretim hattı denemeleri | [URETIM.md](URETIM.md) §2.3.1 | |
| 8 | Sürü çizimi: 2.000 örnek, köşe animasyon dokusu | ✅ Geçti: 1080p'de sürünün GPU maliyeti 1,28 ms (açık 3,69 / kapalı 2,41 ms, medyan), sınır 2 ms | `progress/m0/swarm_check.log`, `progress/m0/swarm.png` |

## 2. Proje yapısı

```
kabarma/
  Kabarma.sln
  src/
    Kabarma.Sim/         net8.0 kütüphane, Godot bağımlılığı yok: oyunun kuralları
    Kabarma.Sim.Tests/   xUnit
    (protokol: mesajlar, anlık görüntü kodlayıcı; Godot bağımlılığı yok → ayrı proje yerine src/Kabarma.Sim/Net*.cs, 18 Kasım 2026)
    Kabarma.Tools/       konsol: SimRunner, DataCheck, BalanceReport, RecipeGraph
  game/                  Godot projesi (C#)
    project.godot  Kabarma.Game.csproj (Sim ve Net'e başvurur)
    autoload/      Platform, Session, Content, Audio, Settings, Events
    world/  player/  creatures/  building/  farming/  machines/  vehicles/
    combat/  fx/  ui/  net/  ai/
    assets/        models/ audio/ shaders/ fonts/ icons/
    data/          *.json: bütün içerik ve denge tabloları
    i18n/          strings.csv
  pipeline/        Python ve Blender betikleri: üretim, temizleme, kalite kontrol
  tests/           başsız Godot senaryoları, ortak oyun betikleri, botlar
  docs/  progress/  build/
```

**Kod kuralları:** Tanımlayıcılar İngilizce, yorumlar Türkçe. Sihirli sayı yok; her sayı `data/` altında. Olaylar tek bir olay veri yolundan geçer. `Sim` içinde `Godot` ad alanı kullanılmaz; bunu bir derleme testi denetler.

## 3. İş bölümü: Sim ve Godot

| `Kabarma.Sim` (motordan bağımsız) | Godot katmanı (host'ta yetkili) |
|---|---|
| Takvim, gün döngüsü, hava | Karakter hareketi ve çarpışması |
| Dünya üretimi: yükseklik haritası, biyom haritası, keşif noktası yerleşimi | Arazi mesh'i ve çarpışma şekli |
| Arazi farkları | Mermi ve ışın isabet sorguları |
| Tarım: büyüme, nem, bereket, genler, kabarma boyu | Yaratık yürüyüşü, animasyon, IK |
| Hayvan ihtiyaçları, mutluluk, üreme, genler | Fizik nesneleri: yük, araç, ragdoll, çöken yapı |
| Güç ağı, sinyal ağı | Görüntü, efekt, ses, arayüz |
| Bant, kol, drone lojistiğinin mantığı | Bant üstündeki nesnelerin görsel hareketi |
| Tarifler, envanter, ambar | |
| Ekonomi, borsa, katalog, çark | |
| İş emirleri, araştırma, Ustalık | |
| Kabarma yönetmeni: zamanlama, dalga bileşimi, ölçekleme | Dalgadaki yaratıkların sahneye konması |
| Kart sistemi: seçim, rütbe, aşı, etkilerin sayısal karşılığı | Kart efektlerinin görüntüsü |
| Hasar hesabı (Godot'tan gelen isabet olayına göre) | |
| Holding'in stratejik AI'ı: kamp büyümesi, Husumet, kervan ve baskın planı | Holding birimlerinin çatışma davranışı |
| Yol bulma: ızgara A*, akış alanları | |
| Kayıt ve yükleme, sürüm göçü | |

- `Sim` komut alır (oyuncu eylemleri, isabet olayları, konum özetleri) ve olay yayar. Aynı tohum ve aynı komut günlüğü aynı sonucu verir; bu, tekrar oynatma testlerinin temelidir.
- `Sim` saniyede 20 tik çalışır. Yavaş sistemler (tarım, güç, ekonomi) saniyede 2 alt adımda güncellenir.
- Adanmış sunucu, aynı Godot projesinin başsız derlemesidir. Ayrı bir özel sunucu yazılmıyor, çünkü yük, araç ve ragdoll fiziği iki tarafta da aynı motoru ister.
- `Kabarma.Tools/SimRunner`, Godot olmadan binlerce oyun gününü simüle eder; denge testleri bununla koşar.

## 4. Varlık modeli

- Ağdaki her nesnenin bir `net_id`'si ve bir tanımlayıcısı vardır: `{tür, içerik kimliği, parametreler, dönüşüm}`. Tanımlayıcı nesneyi iki tarafta da aynı kurmaya yeter (Oduncu'da kanıtlandı).
- **Bitki örtüsü** kaydedilmez, tohumdan yeniden üretilir; yalnızca farklar tutulur (kesildi, hasarlı).
- **Ekinler** tarla karesi verisidir (`Sim`); görüntüsü örneklenmiş mesh'tir, büyüme ve şişme değeri örnek parametresiyle gider.
- **Sürüler** (Çekirge, Sülük, Hamurcuk öbekleri, Vızvız) tek ağ varlığıdır: merkez, yarıçap, üye sayısı, can havuzu ve bir tohum. Üyelerin hareketi istemcide aynı tohumla hesaplanır.
- **Yapılar** olayla senkronlanır (kuruldu, hasar aldı, yıkıldı); sürekli snapshot'a girmez.

## 5. Ağ

### 5.1 Taşıma

Kendi ince mesaj katmanımız (`src/Kabarma.Sim/Net*.cs`: ikili, sürümlü; `net.json` `protocol`). Godot'un üst düzey RPC'si kullanılmaz. `ITransport` arayüzünün (`game/scripts/net/`) iki uygulaması var:

| Taşıma | Kullanım |
|---|---|
| ENet | Geliştirme, yerel ağ, adanmış sunucuya doğrudan bağlanma |
| Steam Networking Sockets (Steamworks.NET) | Arkadaş daveti, lobi, relay; port açmak gerekmez |

### 5.2 Yetki

| Şey | Yetkili |
|---|---|
| Oyuncunun kendi hareketi | Sahibi olan istemci; host hız ve konum tutarlılığını denetler |
| Atış | İstemci efekti anında oynatır; isabete host karar verir (gecikme telafisi: yaratık konumları 200 ms'ye kadar geri sarılır) |
| Yaratıklar, Holding birimleri, dalgalar, Atalar | Host |
| Fizik nesneleri | Host; tutulan nesnede tutan istemciye geçici yetki |
| Araçlar | Sürücü istemci; host yansıtır |
| `Sim`'deki her şey (tarım, güç, ekonomi, kartlar) | Host |
| Efekt ve ses | Yerel |

### 5.3 Güncelleme sıklığı ve bütçe

| Veri | Boyut ve sıklık |
|---|---|
| Oyuncu | 16 B, 20 Hz (konum 3 × 24 bit, metrede 256 adım: 3,9 mm) |
| Yaratık (120 m içinde) | 13 B, 10 Hz; daha uzakta 2 Hz (10 B'de 16 bitlik konum 1 cm ölçütünü tutmuyordu; 24 bite çıktı, 18 Kasım 2026) |
| Sürü | 14 B, 5 Hz |
| Fizik nesnesi | 15 B, uyanıkken 20 Hz, uzakta 5 Hz (Oduncu biçimi) |
| `Sim` durumu | Değişince fark; 10 sn'de bir özet doğrulaması |

**En kötü durum hesabı (8 oyuncu, Kabarma, 220 etkin düşman):** yaratıklar yaklaşık 22 KB/sn, oyuncular 2,6, fizik 8, olaylar 2; istemci başına yaklaşık 35 KB/sn. Host'un 7 istemciye yüklemesi yaklaşık 245 KB/sn, yani 2 Mbps. Uzak yaratıkların 2 Hz'e düşmesiyle hedef 1,5 Mbps. 16 oyunculu adanmış sunucuda yaklaşık 4,2 Mbps. Bu değerler M3'te 8, M5'te 16 bot istemciyle ölçülür.

### 5.4 Katılma, ayrılma, devir

- **Sonradan katılma:** tam varlık listesi, `Sim` durumu, arazi ve yapı farkları sıkıştırılmış gönderilir; istemci "hazır" deyince oyuna girer.
- **Host ayrılırsa** oturum biter. Host çıkarken "dünyayı devret" seçeneği sunulur: kayıt bir ekip üyesine aktarılır ve o host olarak yeniden açar.
- **Ses:** Steam ses yakalama → sıkıştırılmış veri → alıcıda çözme → oyuncu başına konumlu ses kaynağı. Telsiz tuşu konumsuz kanala geçirir.

### 5.5 Hile

Host ya da sunucu yetkilidir. İstemci istekleri mesafe, bekleme süresi ve envanter açısından doğrulanır. Çekişme sunucularında hareket denetimi sıkılaştırılır. Çekirdek düzeyinde hile koruması kullanılmaz.

## 6. Fizik

- Jolt. Katmanlar: dünya, yapı, oyuncu, yaratık, yük nesnesi, araç, mermi, tetikleyici, ragdoll.
- **Yaratıklar** fizik gövdesi değil, kapsül süpürmesiyle hareket eden hafif karakterlerdir. Oyunculardan 150 m uzaktakiler yalnızca ızgara üstünde mantıksal olarak ilerler.
- **Sürü üyeleri** fizik kullanmaz.
- **Tavanlar:** aynı anda uyanık fizik nesnesi 300 (Oduncu'da 300 nesnede en kötü 2,4 ms ölçüldü); çöken yapı parçası 40; ragdoll 12.
- **Ezilme:** Oduncu'daki model. Host karar verir; istemci oyuncularda kapsül 15 cm küçültülmüş sayılır.
- **Taşıma:** eklem yerine kuvvet tabanlı yay; birden fazla tutanın kuvveti toplanır.
- **Araçlar:** `VehicleBody3D`; römork ve vinç eklemle. Güvenlik ağı: araziden düşen gövde yüzeye geri konur.

## 7. Dünya üretimi

1. **Tohum → biyom haritası.** Merkezde krater; çevresinde beş açısal dilim, sınırları gürültüyle bükülmüş. Başlangıç dilimi güneyde.
2. **Yükseklik haritası.** 5 × 5 km, 1 m örnekleme. Biyoma göre gürültü katmanları, krater çanağı, kıyı yarları, nehir ve göl oyması, keşif noktası düzleştirmesi.
3. **Keşif noktaları.** Kurallı Poisson dağılımı: tür başına asgari mesafe, biyom ve eğim şartı ([ICERIK.md](ICERIK.md) §17).
4. **Damarlar ve bitki örtüsü.** Tohumdan; parça (chunk) başına üretilir.
5. **İç mekânlar** (Oyuklar, Genel Merkez'in içi) hazır sahnelerdir; arazinin 500 m altına yerleştirilir ve kapıdan geçişle girilir. Yükleme ekranı yoktur.

**Akış:** 64 m'lik parçalar. Oyuncuya 400 m içinde tam ayrıntı, 1,5 km'ye kadar iki kademe düşük ayrıntı, ötesinde biyom başına tek siluet mesh'i. Kat kat ufuk görüntüsü bu siluetlerle ve yükseklik sisiyle kurulur.

**Arazi farkları** parça başına seyrek tutulur; kayda ve ağa yalnızca farklar gider.

## 8. Yapay zekâ

- **Izgara:** 1 m'lik maliyet ızgarası; eğim, su ve yapı ayak izinden türetilir. Duvar hücreleri kapalı değil, "kırma maliyetli"dir.
- **Akış alanları:** Her hedef türü için (ekin aileleri, Kazan) bir akış alanı. Yapı değişince etkilenen bölge yeniden hesaplanır. Dalgadaki yaratık yönünü alandan okur; yaratık sayısı maliyeti artırmaz.
- **Holding ekipleri:** kırma maliyetli A* ve diziliş.
- **Uçanlar** ızgarayı yok sayar. **Kazıcılar** çıkış noktası seçer.
- **Davranış:** dokuz arketip için veriyle sürülen durum makineleri; tür başına parametreler `data/creatures.json`'da.
- **Evcil hayvan işleri:** basit görev kuyruğu (yeme git, işe git, ahıra dön).
- **Botlar:** oyuncuyla aynı giriş arayüzünü kullanır; testler gerçek kod yolundan geçer.

## 9. Kayıt

- **Biçim:** JSON bildirim dosyası (sürüm, tohum, oyun günü, ekip) + ikili bölümler (arazi farkları, tarla, yapılar, varlıklar).
- **Yazma:** önce geçici dosyaya, sonra yeniden adlandırarak. Beş döngülü yedek.
- **Ne zaman:** 5 dakikada bir, her şafakta ve çıkışta (pencere kapatma dahil).
- **Göç:** her sürüm artışı için bir göç fonksiyonu ve birim testi zorunlu.
- **Karakter** ayrı dosyadadır (Ustalık, Nişan, Ansiklopedi, kozmetik) ve Steam Cloud ile eşlenir.
- **Hedef boyut:** dünya kaydı en fazla 30 MB.
- **Kayda hazır yazım (M1'den itibaren):** Kayıt ve ortak oyun M2'de gelir; M1'de yazılan kod onlar için yeniden yazılmasın diye: `Sim`'e giren her yeni durum düz, serileştirilebilir sınıftır (Godot tipi, düğüm başvurusu ya da olay aboneliği tutmaz; kimlikle başvurur). Godot katmanına yeni statik durum eklenmez: statikler kayıt yüklenince sıfırlanmaz ve aynı süreçte koşan host ile istemciyi birbirine karıştırır ([PLAN.md](PLAN.md) §7.2).

## 10. Performans bütçeleri

| Kalem | Önerilen (RTX 2070S, 1080p yüksek) | Asgari (GTX 1050, 8 GB, 1080p düşük) | Steam Deck (800p) |
|---|---|---|---|
| Kare süresi, gündüz | ≤ 8,3 ms | ≤ 22 ms | ≤ 25 ms |
| Kare süresi, Kabarma | ≤ 11 ms | ≤ 28 ms | ≤ 33 ms |
| `Sim` tiki | ≤ 4 ms | ≤ 8 ms | ≤ 8 ms |
| Fizik | ≤ 4 ms | ≤ 6 ms | ≤ 6 ms |
| Çizim çağrısı | ≤ 2.000 | ≤ 1.200 | ≤ 1.200 |
| Bellek | ≤ 4 GB | ≤ 3 GB | ≤ 3 GB |
| Dünya yükleme | ≤ 15 sn | ≤ 30 sn | ≤ 30 sn |

**Çizim:** Forward+. Bitki örtüsü, ekinler ve yapı parçaları örneklenmiş (MultiMesh). Sürü üyeleri köşe animasyon dokusuyla tek çizim çağrısında. Gölge iki bölümlü; SDFGI kapalı; hafif SSAO ve glow; yükseklik ve mesafe sisi. Düşük ayarda gölge mesafesi ve bitki yoğunluğu yarıya iner.

**"Yüksek" ön ayarı** (önerilen donanım sütunu ve M1'in ≥ 90 FPS ölçümü): 1920×1080, çözünürlük ölçeği 1,0 · MSAA yok, SMAA · yönlü gölge 4 bölüm, 4096 atlas, 110 m · SSAO açık (yarım çözünürlük) · glow açık · SDFGI, VoxelGI, SSIL, SSR ve hacimsel sis kapalı · yükseklik ve mesafe sisi açık · bitki yoğunluğu %100, ot yakın halka 22 m, uzak halka 50 m · dikey eşitleme kapalı, kare sınırı yok. Kademeler `graphics.json`: düşük, orta, yüksek, ultra (18 Kasım görsel düzeltmesi).

**M1 ölçümü:** dört yer iminin her birinde ([SANAT-SES.md](SANAT-SES.md) §9) öğle saatinde 60 sn ve ilk gecenin son dalgası boyunca; her birinde ortalama kare süresi ≤ 11,1 ms (≥ 90 FPS). Gündüzün 8,3 ms bütçesi M6 performans turunun hedefidir.

## 11. Test ve doğrulama

| Katman | Ne sınanır | Nasıl |
|---|---|---|
| **Birim (`dotnet test`)** | Formüller (GDD örnekleriyle birebir), güç ve sinyal ağı, gen dağılımları, borsa, kart etkileri, kayıt göçleri | Saniyeler içinde, her değişiklikte |
| **İçerik denetimi** | Benzersiz kimlikler, geçerli başvurular, çeviri anahtarları; içerik adetleri [PLAN.md](PLAN.md) §7.1 ile aynı | `DataCheck` |
| **Tarif grafı** | Başlangıçtan Kabartma Odası'na yol var; hiçbir zorunlu girdi sonraki basamağa bağlı değil ([ICERIK.md](ICERIK.md) §20) | `RecipeGraph`; bulunamazsa derleme kırılır |
| **Denge simülasyonu** | [GDD.md](GDD.md) §26 hedefleri: basamak süreleri, KS geçme oranı, kredi eğrisi, "20 dakikada bir yeni şey" | `SimRunner` içinde oyuncu modeli; çatışma katsayıları Godot koşularından ayarlanır |
| **Senaryo (başsız Godot)** | Her özellik için bir senaryo: tarla, evcilleştirme, her başlık, her makine, her kule, her araç, her Ata, Kabarma KS 1–30, Holding baskını, Çekişme kuralları | `--test=<ad>`; sıfır olmayan çıkış kodu hata |
| **Ortak oyun** | Senkron farkı < 1 cm; sonradan katılma; host devri; 8 ve 16 bot | Çok süreçli betikler |
| **İnsan giriş yolu** | Menüden oyuna, gerçek giriş olaylarıyla; klavye-fare ve oyun kolu eşlemeleri | Arayüz sürücüsü |
| **Dayanıklılık** | 60 dakikalık 8 botlu oturumda 0 hata; 200 oyun günlük kayıt yüklenip aynı kalıyor | Gecelik |
| **Performans** | Kıyas sahneleri: 5 biyom, büyük üs, Kabarma KS 25 | Kare kare ölçüm günlüğü; bütçe aşılırsa hata |
| **Görsel** | Biyom başına 4 gün saati × yer imi; her varlığın döner tabla görüntüsü | Ben ölçütlere göre incelerim ([SANAT-SES.md](SANAT-SES.md) §9), taş sonunda sen onaylarsın |
| **His ölçütleri** | Girişten ilk tepkiye ≤ 50 ms; vuruş duraklaması ve sarsıntı değerleri aralıkta | Otomatik ölçüm |
| **Yerelleştirme** | Eksik anahtar, taşan metin (sözde çeviri), yazı tipi kapsamı | Otomatik + ekran görüntüsü |

**Senin izlemen için:** `IZLE.bat` (bot oynar, serbest kamera) ve `progress/` altında her özelliğin kısa videosu. Oduncu'daki düzen.

## 12. Build ve dağıtım

- `tools/build.ps1`: Windows ve Linux istemci, Linux ve Windows adanmış sunucu.
- Sürüm `version.txt` içinde (semver + build numarası); kayıtlara yazılır.
- Steam: istemci deposu ve ayrı "Adanmış Sunucu" aracı; `steamcmd` betikleri. Giriş bilgilerini sen girersin, ben saklamam.
- Dallar: `default` (çıkışa kadar gizli), `beta`, `playtest`.
- Hata raporu: oyun içi "Hata bildir" düğmesi günlükleri ve son kaydı bir zip'e koyup klasörü açar.

## 13. Veri

Bütün içerik `data/*.json` dosyalarındadır ve şemayla doğrulanır: `resources`, `recipes`, `creatures`, `crops`, `heads`, `chips`, `powers`, `cards`, `characters`, `pieces`, `placeables`, `machines`, `power_parts`, `defenses`, `vehicles`, `foods`, `gear`, `pois`, `quests`, `mastery`, `waves`, `holding`, `economy`, `biomes`. Geliştirme sırasında dosya değişince oyun yeniden yükler. Yeni içerik eklemek kod değişikliği gerektirmez; yalnızca yeni bir davranış gerektiriyorsa kod yazılır.
