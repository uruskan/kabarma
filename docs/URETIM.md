# Kabarma: Üretim Hattı ve Yol Haritası

> Oyunun tamamı AI ile üretilir: kod, tasarım ve kalite kontrol bende; modeller, animasyonlar, sesler ve müzik AI servislerinden gelir ve benim betiklerimden geçer.
> Servis bilgileri 16 Kasım 2026'da resmi belge ve fiyat sayfalarından okundu; hiçbiri henüz bu proje için gerçek çağrıyla denenmedi. Denemeler M0'da (§2.3).

## 1. Kim ne yapar

| Kim | Ne |
|---|---|
| Ben | Tasarım, kod, veri, üretim betikleri, istem (prompt) yazımı, kalite denetimi, testler, replik ve çeviri metinleri, mağaza metinleri |
| AI servisleri | 3B model, kemiklendirme, animasyon klibi, görsel, ses efekti, müzik, seslendirme |
| Sen | Kredi bütçesi onayı; her taşın sonunda oynayıp his onayı; sesleri ve müziği dinleyip onay; Steam hesabı; test grubunu toplamak; yayın kararları |

**Yapamadığım iki şey ve çaresi:**
- **Sesi duyamam.** Sesleri ve müziği dalga biçimi, spektrogram, düzey, süre ve (replikler için) yazıya geri çevirme ile teknik olarak denetlerim. Kulağa hoş gelip gelmediğine sen karar verirsin; her taşta 10 dakikalık bir dinleme turu var.
- **Hissi yargılayamam.** Tepki süresini, vuruş duraklamasını, kare süresini ölçerim. "Zevkli mi" sorusunun cevabı her taşın sonunda senden gelir.

## 2. Üretim hattı

### 2.1 Servisler

| Servis | Bu projedeki işi | Belgelerden teyit edilen |
|---|---|---|
| **Genex CLI** (oduncu klasöründe kurulu; M0'da bu repoya kurulur) | Varsayılan yol: tek hesap, tek sayaç | Model 35 kr · adlı parçalara ayırma 46 · kemiklendirme 29 (yedi gövde planı: iki ayak, dört ayak, altı ayak, sekiz ayak, kuş, sürüngen, su canlısı) · hazır klip aktarma 12/klip · karakter (kemikli, tek adım) 64 · düz yazıyla hareket 46/klip · görsel 5 · döşenebilir doku 6 · ses efekti 2 · müzik 27 · tek replik 12. 1 kredi = 0,01 $. |
| **Tripo API** | İnsansı olmayan gövdelerin kemiklendirilmesi; düşük poligon üretim | Yedi gövde planı ve "diğer" için otomatik kemik. Düşük poligon: `smart_low_poly` (500–20.000 üçgen) ve P serisi. Parça üretimi ve dörtgen topoloji var. Hazır klip: insansıda 101 (eski iskelet) ya da 11 (yeni iskelet); diğer gövdelerde yalnızca tek yürüyüş klibi, kuşta hiç yok. 1 kredi = 0,01 $: metinden model 10–20, görselden 20–30, düşük poligon +10, parça +20, kemik 25, klip aktarma 10. |
| **Meshy API** | İnsansı karakter ve klip kütüphanesi için yedek; düşük poligon üretim için ikinci kaynak | Düşük poligon modelleri `meshy-t2` (100–15.000 üçgen) ve `meshy-t2.5` (100–30.000). Stilize doku modeli `meshy-n1`. Kemiklendirme yalnızca insansı. Hazır animasyon kütüphanesi geniş ama tamamı insansı. Model 5–20 kredi, kemik 5, klip 3. API için Pro ve üstü plan gerekir. Eski `lowpoly` parametresi 30 Kasım 2026'dan sonra çalışmıyor. |
| **ElevenLabs API** | Suna ve Holding sesi; ses efekti ve müzik için doğrudan yol | Konuşma: `eleven_v4` ve `eleven_v3`; Türkçe destekli; ses etiketleri var. Ses tasarımı (tarifle yeni ses) API'de var. Ses efekti: 0,5–30 sn, döngü seçeneği. Müzik: yalnızca çalgısal üretim, bölüm planı, katman ayrıştırma. API fiyatları: konuşma 1.000 karakter 0,08 $, ses efekti dakikası 0,12 $, müzik dakikası 0,15 $. Varsayılan ses efekti ve müzik modelleri 2027 başında kaldırılıyor; `eleven_text_to_sound_v3` ve `music_v2_5` sabitlenecek. |
| **Blender** (başsız, Python) | Temizleme, sadeleştirme, palete oturtma, kit üretimi, poz kütüphanesi | Bu makinede kurulu değil; M0'da kurulur |

**Araştırmadan çıkan üç sonuç plana işlendi:**
1. İnsansı olmayan gövdeyi kemiklendiren belgelenmiş tek servis Tripo (Genex'in yedi gövde planı aynı listeyi sunuyor). Bu gövdeler için hazır klip neredeyse yok. Bu yüzden yaratık klipleri servisten değil, benim yazdığım **poz kütüphanesinden** gelir (§2.2 E).
2. Hazır klip zenginliği yalnızca insansıda var. Oyuncu ve Holding birimleri tek insansı iskeleti paylaşır; klip işi bir kez yapılır.
3. ElevenLabs müzik lisansı Free–Pro planlarda "bireysel kullanım" diye geçiyor ve oyun geliri 500.000 $'ı (ya da geliştiricinin yıllık oyun geliri 1.000.000 $'ı) aşınca 60 gün içinde stüdyo planına geçmeyi şart koşuyor. Bu, §9'da bir yükümlülük olarak duruyor.

### 2.2 Varlık sınıfına göre adımlar

**A. Statik varlıklar** (eşya, bitki, kaya, süs, başlık, kozmetik)
1. İstem ve referans: stil föyündeki altın varlıklardan ikisi görsel referans olarak verilir (§4).
2. Konsept görseli üretilir.
3. Görselden düşük poligon model.
4. Blender betiği: birleştir, sadeleştir (sınıf bütçesine), dokuyu at, renkleri en yakın palet rengine oturt, köşeleri pahla, ölçekle, ekseni ve tabanı düzelt, iki LOD ve çarpışma kabuğu üret.
5. Betikli denetim (ölçüt 1–5) ve döner tabla görüntüsü.
6. Görsel puanlama (ölçüt 6–8). Geçemeyen yeniden üretilir, en fazla dört deneme.
7. Godot içe aktarma, ikon çekimi, varlık kartı (boyut, üçgen, palet kullanımı).

**B. Makineler, kuleler, araçlar**
A'daki adımlar + adlı parçalara ayırma. Dönen, açılan, dolan parçalar kodla hareket eder. Soket, giriş ve çıkış ağzı konumları varlık kartına yazılır.
*Yedek:* Blender Python ile hazır parçalardan (silindir, kutu, boru, çark, düğme) kurma.

**C. Yapı kiti**
Tamamen koddan. 26 şekil × 4 malzeme ve cam seti, parametreli bir Blender betiğiyle üretilir; yuva noktaları betikten gelir. AI modeli kullanılmaz, çünkü parçaların milimetrik oturması gerekir.

**D. Oyuncu karakteri**
1. Konsept ve kemikli insansı model.
2. 53 klip: düz yazıyla hareket üretimi ya da insansı klip kütüphanesinden seçim.
3. Blender betiği: kök hareketini temizle, döngü başını ve sonunu eşle, tıknaz orana göre el ve ayak hedeflerini düzelt.
4. Godot animasyon kitaplığı; üstüne kodla IK ve yay katmanları.
*Yedek:* Oduncu'da çalışan tamamen prosedürel gövde (yay, IK, ragdoll).

**E. Yaratıklar**
1. Konsept, model, gövde planına göre kemik.
2. Yürüyüş klibi hazır kütüphaneden (kuş gövdesinde kodla).
3. **Poz kütüphanesi:** Her gövde planı için sekiz klip, kemik dönüş eğrileri olarak bir kez kodla yazılır (anahtar pozlar ve yumuşatma). Betik bu eğrileri o planı kullanan her türün iskeletine, kemik uzunluklarına göre aktarır. Yedi plan × sekiz klip = 56 eğri seti; 27 türün hepsini besler.
4. Kodla katmanlar: ayak IK'sı, bakış, esneme, şişme shader'ı.
5. Sürü türlerinde yürüyüş köşe animasyon dokusuna pişirilir.

**F. Arazi, gökyüzü, su, efektler, arayüz:** kod ve shader.

**G. İki boyutlu görseller**
- Eşya ikonları modelden çekilir.
- Kart görselleri (72), logo, kapsül ve menü görselleri: görsel üretimi, ardından düzenleme. Vizör yüzleri kodla çizilir.

**H. Ses efektleri**
1. İstem: ses listesindeki her satır için kaynak, malzeme, mesafe, süre.
2. Her ses için 3–5 varyasyon.
3. Betikli denetim: süre, tepe düzeyi, baştaki ve sondaki sessizliğin kırpılması, döngülerde dikiş noktası, spektrum kıyaslaması (aynı grubun sesleri benzer aralıkta mı).
4. Düzey eşitleme ve Godot ses listesine kayıt.
5. Dinleme turu sende.

**I. Müzik**
1. Parça başına bölüm planı; ton ve tempo sabit; yalnızca çalgısal.
2. Kabarma setlerinde üç yoğunluk katmanı: aynı plandan üretilir, katman ayrıştırmayla bölünür.
3. Betikli denetim: tempo ve ton tespiti, katmanların vuruş hizası, döngü dikişi.
4. Dinleme turu sende.

**J. Seslendirme**
1. Suna'nın sesi tarifle tasarlanır; üç aday üretilir, sen birini seçersin.
2. Replikleri Türkçe ve İngilizce ayrı ayrı yazarım (çeviri değil, uyarlama).
3. Üretim; ardından her replik yazıya geri çevrilip metinle karşılaştırılır (yanlış okunan kelimeyi yakalamak için).
4. Telsiz süzgeci oyunda uygulanır.

**K. Çeviri**
10 dil. Her dil için: çeviri, geri çeviriyle anlam denetimi, terim sözlüğüne uyum denetimi, sözde çeviriyle taşma testi.

### 2.3 M0 hat denemeleri

| # | Deneme | Geçme ölçütü | Yedek |
|---|---|---|---|
| 1 | Üç statik varlık (sulama kovası, çayır ağacı, kaya) | Üçü de en fazla dört denemede [SANAT-SES.md](SANAT-SES.md) §9'dan geçiyor | Koddan kit (Oduncu yöntemi) |
| 2 | Bir makine (Taş Fırın) | Parçalara ayrılıyor; kapak ve baca kodla hareket ediyor | Blender'da hazır parçalardan kurma |
| 3 | Oyuncu gövdesi + 6 klip | Tıknaz gövdede yürüyüş, koşu, zıplama ve bir jest kayma ve çökme olmadan oynuyor; Çapa kolu IK ile hedefe dönüyor | Tam prosedürel gövde |
| 4 | İki yaratık: Pofuduk (dört ayak), Gıdak (kuş) | Kemik oturuyor; yürüyüş ve kodla yazılmış iki klip oynuyor; şişme shader'ı çalışıyor | Yaratığı katı parçalardan kurup tamamen kodla oynatmak |
| 5 | 10 ses efekti ve 1 döngü | Betikli denetimden geçiyor; sen dinleyip onaylıyorsun | Doğrudan ElevenLabs; son yedek Oduncu'daki sentez betikleri |
| 6 | 1 müzik parçası, 3 katman | Katmanlar hizalı; sen dinleyip onaylıyorsun | Katmansız üç ayrı parça ve çapraz geçiş |
| 7 | Suna: 3 aday ses, 10 replik TR ve EN | Geri çevirme hatasız; sen sesi seçiyorsun | Genex'in hazır ses kadrosu |

M0'ın sonunda gerçek yeniden deneme oranları ölçülür ve §5'teki bütçe tablosu güncellenir.

## 3. Kalite kapıları

| Düzey | Kapı |
|---|---|
| Varlık | [SANAT-SES.md](SANAT-SES.md) §9'daki sekiz ölçüt. Geçemeyen varlık oyuna girmez. |
| Özellik | [PLAN.md](PLAN.md) §7.2'deki altı koşul |
| Kilometre taşı | Bu belgedeki kabul ölçütleri ve senin onayın |
| Sürüm | §8'deki çıkış kapısı |

"Yer tutucu" etiketi taşıyan her varlık `data/` içinde işaretlidir; çıkış kapısında bu sayı sıfır olmak zorundadır.

## 4. Stil kilidi

M1'in ilk işi **12 altın varlık** üretmektir: bir karakter, iki yaratık, iki bitki, bir kaya, iki makine, bir araç, bir yapı köşesi, bir eşya, bir kabarık ürün. Bunlar ölçütlerden geçer ve sen onaylarsın. Ondan sonra üretilen her varlık:
- istemde bu altın varlıklardan ikisini görsel referans alır,
- puanlamada onların yanında, aynı ışıkta görüntülenir.

Stil föyü `docs/stil/` altında durur: paletler, altın varlıkların döner tabla görüntüleri, doğru ve yanlış örnekler.

## 5. Kredi bütçesi

Birim fiyatlar `npx genex --help` çıktısından (16 Kasım 2026). Yeniden deneme çarpanları varsayımdır; M0 ölçümüyle düzeltilir.

| Sınıf | Adet | Birim (kredi) | Deneme çarpanı | Toplam (kredi) |
|---|---|---|---|---|
| Statik modeller (bitki ve kaya 96, damar 25, yerleştirilebilir 72, ekin 48, dünya eşyası 30, başlık 18, kozmetik 24, keşif noktası 40, Holding 15) | 368 | 40 (görsel + model) | 2,5 | 36.800 |
| Parçalı modeller (makine ve parça 73, kule ve tuzak 16, araç ve eklenti 17) | 106 | 86 (+ parçalara ayırma) | 2,5 | 22.800 |
| Yaratıklar (27 tür, Ata parçaları 12) | 39 | 81 (+ kemik + yürüyüş) | 3 | 9.500 |
| Oyuncu gövdesi ve 53 klip | 1 | 2.500 | 2 | 5.000 |
| Görseller (kart 72, logo, kapsül, menü, konsept föyleri) | 400 | 5 | 1 | 2.000 |
| Ses efektleri (900 ses × 4 varyasyon) | 3.600 | 2 | 1,5 | 10.800 |
| Müzik (40 parça, 20 geçiş sesi) | 60 | 27 | 2,5 | 4.100 |
| Seslendirme (doğrudan ElevenLabs: yaklaşık 1.550 replik, 125.000 karakter) | — | — | 2 | 2.000 |
| **Ara toplam** | | | | **93.000** |
| Beklenmeyen (%30) | | | | 27.900 |
| **Toplam** | | | | **yaklaşık 121.000 kredi ≈ 1.200 $** |

Buna ek olarak: ElevenLabs aboneliği (üretim aylarında), Steam uygulama ücreti (100 $). Harcama taşlara yayılır: M0 yaklaşık %2, M1 %8, M2–M4 %70, M6 %20. Her taşın başında o taşın harcama tavanını söylerim; onayın olmadan üretim başlatmam.

## 6. Kilometre taşları

Sıra sabittir: önce doğrulama, sonra görünüş ve his, sonra sistemler az içerikle, sonra içerik, sonra çok oyunculu genişleme, sonra cila. Her taş oynanabilir bir derlemeyle biter.

### M0: Doğrulama

**Teslim:** Çalışan boş C# projesi, `Sim` iskeleti, Steam ve ENet taşıması, Blender hattı, yedi hat denemesi, güncellenmiş bütçe.

**Kabul:**
- [ ] [TEKNIK.md](TEKNIK.md) §1.2'deki sekiz adım geçti ya da yedeği seçildi ve belgeye yazıldı.
- [ ] §2.3'teki yedi deneme geçti ya da yedeği seçildi.
- [ ] Bütçe tablosu ölçülen deneme oranlarıyla güncellendi; sen onayladın.

### M1: Görünüş ve his dilimi

Tek oyuncu, Altın Çayır'ın 300 × 300 m'lik bir köşesi. Amaç tek: oyunun bir stüdyo işi gibi durduğunu ve ana fiilin zevkli olduğunu kanıtlamak. Genişlik yok.

**Teslim:**
- 12 altın varlık ve stil föyü.
- Arazi, gökyüzü, sis, su; 8 bitki, 4 kaya.
- Oyuncu: 53 klip, IK ve yay katmanları, ragdoll, vizör ifadeleri.
- Çapa: altı iş başlığı, Tırpan Işını, Çivi Tabancası; vuruş hissi katmanları; F1 ayar paneli.
- Gıdak ve Zıpzıp, normal ve kabarmış.
- Ahşap yapı seti (26 şekil), 6 yerleştirilebilir, Ocak.
- Şalgam tarlası (büyüme, sulama, kabarma).
- Tezgâh, Taş Fırın, Paketleme Tezgâhı, Kargo Rampası, El Arabası.
- İlk gece senaryosu: Küçük Kabarma, 10 kart, hasar sayıları, Hasat Raporu.
- HUD (gündüz ve Kabarma), envanter, Defter'in ilk sayfası.
- 60 ses efekti, 2 müzik parçası (çayır gündüzü; 3 katmanlı Kabarma), Suna'dan 30 replik.

**Kabul:**
- [ ] İlk 10 dakika senaryosu ([PLAN.md](PLAN.md) §1.4) hem botla hem insan giriş yoluyla hatasız oynanıyor.
- [ ] Bütün varlıklar sekiz ölçütten geçti.
- [ ] 16 ekran görüntüsü (4 yer imi × 4 gün saati) ekran görüntüsü ölçütlerinden geçti.
- [ ] His ölçütleri aralıkta (girişten tepkiye ≤ 50 ms).
- [ ] RTX 2070S'te 1080p yüksek ayarda ≥ 90 FPS.
- [ ] **Sen** 30 dakika oynuyorsun ve iki soruya "evet" diyorsun: "Stüdyo işi gibi duruyor mu?" ve "Ateş etmek ve kabartmak zevkli mi?" İkisi de "evet" olmadan M2 başlamaz.

### M2: Çekirdek sandbox

Bütün kalıcı sistemlerin kuralları, T1–T2 içeriğiyle; ardından ortak oyun.

**Teslim:** yapı (taşıma dengesi, yükseltme, plan modu, Plan Defteri) · tarım (genler, çaprazlama, Alaca, zararlılar) · hayvancılık (Gıdak, Pofuduk, Homur, Vızvız; roller, üreme) · güç ağı ve temel sinyal · T1–T2 üretim zinciri ve araştırma · envanter, ambardan üretim · ekonomi (borsa, katalog, çark) · iş emirleri altyapısı ve ilk 12 zincir · Ustalık · kayıt ve yükleme · Sis Korusu · Koca Pofuduk ve Boynuzlu Dede · harita, Seyir Kulesi, mancınık · Çekirge · ortak oyun 1–4 (ENet ve Steam), sonradan katılma, sesli sohbet, şok tokadı.

**Kabul:**
- [ ] Birim, içerik ve senaryo testleri yeşil.
- [ ] 4 bot istemciyle senkron farkı < 1 cm; kayıt-yükleme sonrası dünya aynı.
- [ ] `SimRunner` oyuncu modeli başlangıçtan T3'e hedef sürede ulaşıyor (±%25).
- [ ] **Sen ve bir arkadaşın** iki ayrı makinede Steam üzerinden 2 saat oynuyorsunuz.

### M3: Kabarma ve savaş

**Teslim:** tam Kabarma akışı (KS 1–15), dalga bileşimi ve "ne ekersen onu çekersin" · 72 kart, aşılar, Nişan · Gösteri çarpanı · Tatbikat, Maya Feneri · T1–T3 kuleleri ve tuzakları · savaş hayvanları ve binek · T1–T3 silah başlıkları, çipler · ilk beş güç ve Kadim Taş denemeleri · Holding: iki kamp (seviye 1–6), kervan, baskın ve karşı baskın · Azman Homur ve Azman Bozuk · akış alanlı yol bulma.

**Kabul:**
- [ ] KS 15, 8 bot: kare süresi ve ağ bütçesi içinde ([TEKNIK.md](TEKNIK.md) §5.3, §10).
- [ ] Denge simülasyonunda önerilen donanımla ilk denemede geçme oranı %60–80.
- [ ] Her kartın ve her aşının senaryo testi var.
- [ ] **Sen** tek başına üç, arkadaşlarınla bir Kabarma gecesi oynuyorsun: "gece güçlenmesi zevkli" onayı.

### M4: Dünya ve içerik

**Teslim:** Işıl Sazlık, Kızıl Kanyon, Ayaz Yaylası, Göbek · T3–T6 zinciri · kalan 17 yaratık, 13 ekin, makineler, otomasyon, drone ve teleferik · sera, Aşı Tezgâhı, Kabartma Odası · Traktör, Kamyon, Biçerdöver, Hava Yastıklı, Zeplin ve eklentiler · Kurbağa Ağa, Çıyan Paşa, Buz Tosunu, Ana Maya; Kabarık Atalar · üçüncü kamp ve Genel Merkez · kalan silah başlıkları, güçler, ultiler · 32 yemek · 48 iş emri zincirinin tamamı · 12 üye tipi, 72 kozmetik · KS 16 ve üstü, tüzük maddeleri, Yeni Ay · Oyuklar ve bütün keşif noktaları.

**Kabul:**
- [ ] `DataCheck`: içerik adetleri [PLAN.md](PLAN.md) §7.1 ile birebir.
- [ ] `RecipeGraph`: başlangıçtan Kabartma Odası'na yol var.
- [ ] Hızlandırılmış bot, başlangıçtan Ana Maya'ya kopmadan gidiyor.
- [ ] Her biyomun ekran görüntüsü seti ölçütlerden geçti.
- [ ] **Sen** her biyomu en az 30 dakika oynuyorsun.

### M5: Çok oyunculu tamamı

**Teslim:** 8 oyunculu ortak oyun · adanmış sunucu (Windows ve Linux) · sunucu listesi · Çekişme dünyası ve on kuralı · dünyayı devret · telsiz kanalı · sunucu yönetim komutları.

**Kabul:**
- [ ] 8 botla 60 dakika, 16 botla 30 dakika: 0 hata, bütçe içinde.
- [ ] Çekişme kurallarının her biri için senaryo testi (gündüz dokunulmazlık, Açık Silo, çevrimdışı koruma, enkaz planı).
- [ ] **6–8 kişilik grup** 2 saat ortak oyun; iki ekip bir Çekişme gecesi oynuyor.

### M6: Cila

**Teslim:** öğretici ve Suna'nın bütün replikleri (TR ve EN) · bütün ses ve müziğin son hâli, miks · arayüz cilası, erişilebilirlik ayarları · 10 dil · başarımlar, Steam Cloud, zengin durum bilgisi · fotoğraf modu · performans turu (asgari donanım, Deck) · denge turu (simülasyon ve oynama testi verisi) · hata avı.

**Kabul:** §8'deki çıkış kapısının tamamı.

### M7: Çıkış

**Teslim:** mağaza sayfası, kapsüller, fragman · demo (ilk iki saat: T1, ilk Kabarma'lar, Koca Pofuduk) · Steam Playtest · içerik üreticisi anahtar listesi · çıkış günü kontrol listesi · ilk hafta yama düzeni.

### Büyüklük ve süre

Göreli büyüklük: M0 < M1 < M5 < M2 ≈ M3 ≈ M6 < M4. Takvim veremiyorum: hızı oturumların uzunluğu, deneme oranları ve senin onay dönüşlerin belirliyor. M1 bittiğinde ölçülen hızla kalan taşlar için bir tahmin yazarım.

## 7. Çalışma düzeni

- Tablo değişecekse önce belge, sonra veri, sonra kod güncellenir.
- İlerleme günlüğü `docs/PROGRESS.md`'de tutulur: ne doğrulandı, ne kısmen doğrulandı, ne yapılmadı.
- Her özellik bittiğinde `progress/` altına kısa video; `IZLE.bat` her an güncel hâli gösterir.
- Kredi harcayan her üretim öncesinde tutar söylenir ve onay beklenir.
- Uzak depoya gönderme (push) ve yayınlama senin açık onayınla yapılır.
- Testler ayrı kayıt yuvalarında koşar; senin kaydına dokunulmaz.

## 8. Çıkış kapısı (v1.0)

- [ ] İçerik adetleri [PLAN.md](PLAN.md) §7.1 ile birebir; yer tutucu varlık sayısı 0.
- [ ] Bütün test katmanları yeşil ([TEKNIK.md](TEKNIK.md) §11).
- [ ] Hızlandırılmış bot geçişi: tek oyuncu ve 4 kişilik ekip, başlangıçtan Ana Maya'ya.
- [ ] 8 botla 60 dakika ve 200 oyun günlük kayıtla 0 hata.
- [ ] Performans bütçeleri üç donanım sınıfında tutuyor.
- [ ] Ses kapsamı: sesi olmayan oyun olayı 0 (olay listesi ile ses listesi otomatik karşılaştırılır).
- [ ] 10 dilde eksik anahtar 0, taşan metin 0.
- [ ] Oyun koluyla, fareye dokunmadan menüden Ana Maya'ya gidilebiliyor.
- [ ] Oynama testi: en az 20 yeni oyuncu. İlk Kabarma'ya ulaşma oranı ≥ %85; ilk 30 dakikada bırakma ≤ %20; "arkadaşına önerir misin" ortalaması 10 üzerinden ≥ 8.
- [ ] Bilinen çökme 0; bilinen ilerleme kilidi 0.
- [ ] Senin son onayın.

## 9. Çıkış planı ve yükümlülükler

| Konu | Plan |
|---|---|
| Fiyat | 14,99 $; Steam'in bölgesel fiyat önerileri; çıkışta %10 indirim |
| Mağaza sayfası | M4 bittiğinde açılır; istek listesi çıkışa kadar birikir |
| Demo | M7'de; bir Steam Next Fest dönemine denk getirilir |
| Dil | 10 arayüz dili, 2 seslendirme |
| AI beyanı | Steam içerik anketinde üretilmiş içerik beyan edilir: modeller, sesler, müzik, seslendirme önceden üretilmiştir; oyun sırasında çalışan üretken model yoktur |
| Müzik lisansı | ElevenLabs şartları gereği oyun geliri 500.000 $'ı aşarsa 60 gün içinde stüdyo planına geçilir; bu eşik çıkış günü kontrol listesinde durur |
| Servis şartları | Bütün varlıklar ücretli planlarda üretilir (ticari kullanım hakkı için); her servis için üretim tarihi ve plan kaydı `pipeline/licenses.md`'de tutulur |
| Ad | "Moonswell", "Swelltide" ve "Kabarma" için 16 Kasım 2026'da Steam aramasında oyun çıkmadı. Marka sicili sorgulanmadı; mağaza sayfası açılmadan sorgulanır |
| Yaş derecelendirmesi | Steam anketi; içerik: çizgi film şiddeti, kan yok |
| Çıkış sonrası ilk hafta | Her gün bir yama penceresi; geri bildirim Steam forumundan toplanır |
