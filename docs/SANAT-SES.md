# Kabarma: Sanat ve Ses

> Hedef: ekran görüntüsüne bakan biri bunu bir stüdyonun yaptığını düşünmeli. Bu belge o hedefi ölçülebilir kurallara çevirir. Varlıkların nasıl üretildiği [URETIM.md](URETIM.md)'de.

## 1. Görsel dil

Beş kural. Bir varlık bunlardan birini bozuyorsa oyuna girmez.

> **17 Kasım 2026 güncellemesi (sahibinin kararı):** "Aşırı yüksek poligon gerekmez ama flash oyun gibi de durmasın; bütçeye göre ayarla." Dokusuz, palete oturtulmuş düşük poligon bırakıldı. Kural 1 ve 3 kalır; kural 2 aşağıdaki gibi değişti; bütçeler §4'te yükseldi.

1. **Büyük, okunaklı biçimler.** İnce çubuk ve sivri diken yok; en ince parça, nesnenin en uzun kenarının %6'sından kalın olur. Ayrıntı vardır ama siluet önce gelir.
2. **Doku var, palet yön verir.** Modelin kendi dokusu korunur (Tripo doku v3.5, gerekirse PBR). Palet artık yüzeyi zorlamaz; istemde renk yönü olarak verilir ve ışık, sis, renk düzeltmesiyle sahne bütünlenir. İstemde "cartoon / flat colours" kullanılmaz; "stylised, hand-painted, grounded materials" kullanılır.
3. **Yumuşak gölgeleme, net siluet.** Yüzler pürüzsüz gölgelenir; sert kenar yalnızca biçimin kırıldığı yerde. Her nesne 64 piksellik küçük resimde tanınır.
4. **Derinlik sisle kurulur.** Her manzarada en az üç katman okunur: ön plan, orta plan, sisli ufuk. Uzak katmanlar gökyüzü rengine yaklaşır (Firewatch ilkesi). Saf siyah ve saf beyaz kullanılmaz.
5. **Işıyan tek şey Maya'dır.** Işıma (emission) yalnızca Maya'ya, kabarmış canlılara, çalışan makine göstergelerine ve gece lambalarına aittir. Böylece Kabarma başladığında ekran kendiliğinden değişir.

## 2. Paletler

Her biyomun altı ana rengi ve dört gün saati için gökyüzü geçişi var. Varlıklar yalnızca kendi biyom paletini ve ortak nötr paleti kullanır. Değerler başlangıçtır; M1 diliminde ekran görüntüsüyle ayarlanır ve `data/biomes.json`'a yazılır.

| Biyom | Zemin | Bitki | Vurgu | Gölge | Sis ve ufuk | Gökyüzü (öğle → gün batımı) |
|---|---|---|---|---|---|---|
| Altın Çayır | `#E2B54A` | `#7FA650` | `#F26B3A` | `#5A4A6B` | `#F3D9A4` | `#8CC8E8` → `#F58A5B` |
| Sis Korusu | `#3F6B5C` | `#2E8C7A` | `#F2C14E` | `#1F3044` | `#A9C9C4` | `#9FC5C9` → `#E0926B` |
| Işıl Sazlık | `#4A4F7A` | `#3FB7A6` | `#D96BD0` | `#22203F` | `#8E8FC4` | `#7FA8D8` → `#B56BC9` |
| Kızıl Kanyon | `#C8553D` | `#D9A441` | `#3FA7B5` | `#5B2A3A` | `#EDB48A` | `#F1C27D` → `#E0563F` |
| Ayaz Yaylası | `#DCE8F2` | `#7FB8C9` | `#5B6EE0` | `#3A4A78` | `#C7D8EC` | `#A8CDEA` → `#F0A6B8` |
| Göbek | `#3A2E4F` | `#8A5BD1` | `#5FF0C8` | `#150F26` | `#6B4FA0` | `#2A1F4A` → `#5FF0C8` |

- **Ortak nötr palet** (makine, yapı, araç): kırık beyaz `#EDE6D6`, kooperatif mavisi `#3E78B2`, uyarı sarısı `#F2B632`, koyu gri `#3B3F4A`, ahşap `#B07A4A`, Holding kırmızısı `#C8372D`.
- **Maya paleti:** turkuaz `#5FF0C8` ile eflatun `#D45BFF` arasında geçiş. Kabarma gecesinde gökyüzü, zemin damarları ve kabarmış her şey bu iki renge döner.
- **Gece** karanlık değil mavidir: en koyu değer `#1A2238`. Ay ışığı ve aurora her zaman okunaklı bir taban ışık verir.

## 3. Karakter

- **Gövde:** "Tulumlu". Boy 1,6 m; kafa gövdenin üçte biri; eller ve botlar büyük; boyun yok. Tek iskelet, bütün üye tipleri ve Holding birimleri için.
- **Vizör** bir ekrandır: gözler ve ağız iki boyutlu ifadelerdir. İfade duruma göre değişir (şaşkın, yorgun, mutlu, korkmuş); konuşurken ağız ses düzeyine göre oynar. Yüz kemiği ve dudak eşlemesi gerekmez.
- **Sırt çantası** yuvaları ve içindekileri gösterir; Çapa'nın takılı başlıkları Çapa'nın üstünde görünür.
- **Kozmetik** parçalar aynı iskelete takılır: kask, vizör yüzü, tulum deseni (renk bölgeleri), çanta süsü, Çapa kaplaması.

### 3.1 Animasyon

Üç katman üst üste çalışır:

| Katman | Kaynak | İçerik |
|---|---|---|
| Klipler | AI ile üretilir, iskelete aktarılır | Aşağıdaki liste |
| Kodla | IK ve yaylar | Çapa'yı tutan kolun hedefe dönmesi, geri tepme, ayakların zemine oturması, baş ve gövdenin bakış yönüne dönmesi, çanta ve süslerin salınımı, koşarken eğilme |
| Fizik | Ragdoll | Yere serilme, fırlatılma, bayılma; kalkış klibine harmanlanır |

**Klip listesi (53):** bekleme (2) · sekiz yönlü yürüme ve koşu (16) · depar · zıplama, havada, iniş (3) · düşme · merdiven tırmanma · suda yürüme · süzülme · paraşüt · ağır yük taşıma · el arabası itme · oturma · sürücü koltuğu · binek üstü · uyuma · yeme · hayvan okşama · diz çöküp ekme · kol çevirme · şok tokadı · baygın sürünme · önden ve arkadan kalkış (2) · 12 jest.

Birinci şahıs kol animasyonu, yakın dövüş kombosu ve silaha özel doldurma animasyonu yoktur; tasarım bunları gerektirmez.

## 4. Yaratıklar

- **Biçim:** Her türün tek cümlelik bir silueti var (Gıdak: armut gövde, kısa kanat, tepelik; Pofuduk: bacaklı yün yumağı). Gözler büyük ve basit. Tür adı siluetten tahmin edilebilmeli.
- **Kabarma görüntüsü** bütün türlerde aynı shader ile kurulur:
  1. Gövde normal yönünde şişer (2,5–4 kat boy).
  2. Yüzey yay gibi titrer; her adımda ve darbede esner.
  3. Maya rengi damarlar ışır.
  4. Zayıf nokta olarak parlayan maya kesesi belirir.
  5. Ölünce balon gibi söner ya da patlar; Maya ve ganimet saçılır.
- **Ölüm animasyonu yoktur.** Kabarmış yaratık patlar; gündüz avlanan yaratık ragdoll olur.
- **Animasyon:** Tür başına sekiz klip (bekleme, yürüme, koşu, iki saldırı, darbe alma, yeme, dinlenme). Yürüyüş hazır klipten gelir; diğerleri gövde planı başına bir kez kodla yazılmış poz eğrilerinden üretilir ve o planı kullanan bütün türlere aktarılır ([URETIM.md](URETIM.md) §2.2). Üstüne kodla ayak IK'sı, bakış ve esneme eklenir.
- **Sürüler** tek mesh'in örneklenmesiyle çizilir; hareketleri köşe animasyon dokusundan gelir.
- **Atalar** birkaç parçadan kurulur (Çıyan Paşa boğumları, Dede'nin boynuzundaki koru); parçalar kodla hareket eder. Ana Maya şekilsiz bir shader kütlesidir.

**Poligon bütçeleri (17 Kasım 2026, dokulu yön):** oyuncu 10.000–15.000 üçgen · küçük yaratık 5.000 · orta 8.000 · büyük 12.000 · Ata 30.000 · eşya ve kaya 2.000–6.000 · ağaç 5.000–8.000 · makine 8.000–15.000 · araç 12.000–20.000 · yapı parçası 200–1.500. Doku: eşya 1K, yaratık ve makine 1–2K, oyuncu ve Ata 2K. Eski bütçe (oyuncu 8.000, küçük yaratık 2.000 …) dokusuz yön içindi.

## 5. Yapılar, makineler, araçlar

- **Yapı parçaları** koddan üretilen bir kittir; yuvalar milimetrik oturur. Ahşap: kalın kalas ve kiriş. Kerpiç-Taş: yuvarlak köşeli bloklar. Çelik Sac: perçinli paneller. Mayalı Beton: kabarmış, yastık gibi yüzeyler ve ışıyan derzler.
- **Makineler** "oyuncak" gibi okunur: iri düğmeler, dönen bir parça, dolan bir gösterge, çalışırken çıkan buhar ya da kıvılcım. Giriş ve çıkış ağızları renk kodludur (giriş mavi, çıkış sarı). Soketler fiziksel fiştir; kablo elle takılır ve gergin durur.
- **Araçlar** tıknazdır: büyük teker, kısa gövde, görünür süspansiyon. Eklentiler yuvaya oturunca tık sesi ve küçük bir sıçrama verir.
- **Hareketli parçalar** kodla döner; makine modeli üretimde adlı parçalara ayrılır.

## 6. Dünya

- **Arazi:** köşe rengi eğime, yüksekliğe ve biyoma göre; biyom geçişleri 30–60 m'de harmanlanır. Tarla sürülen yer koyu toprak rengine döner.
- **Bitki örtüsü:** biyom başına 8–12 bitki ve 4–6 kaya biçimi. Taçlar rüzgârda köşe shader'ıyla salınır. Ot, oyuncu yakınında örneklenmiş tutamlardır.
- **Su:** stilize, düz renkli, kıyıda köpük çizgisi; derinliğe göre renk.
- **Gökyüzü:** palet bantları, güneş ve iki ay, stilize bulut, gece yıldızlar. Maya halkası takvimi gösterir. Krater ışık sütunu her yerden görünür.
- **Hava:** yağmur perdesi, sis yoğunluğu, kum ve kar parçacıkları; ıslanan zemin koyulaşır.

## 7. Efektler

Hepsi shader ve parçacıkla, kodda yazılır. Liste:

| Grup | Efektler |
|---|---|
| Çapa (18 başlık) | Her başlığın ışını ya da namlu efekti, isabet efekti ve iz efekti; emilen parçaların çantaya uçuşu |
| Vuruş | Hasar sayıları (kritik büyük ve sarı), vuruş duraklaması, sarsıntı, hedefin ezilip esnemesi, parça saçılması |
| Durumlar | Yanık alevi, donma buzu, şok kıvılcımı, zehir köpüğü, kabarık şişmesi ve patlaması |
| Kabarma | Gökyüzü dalgası, zemin damarları, ekinin şişme sekansı, hayvanın şişme sekansı, dalga giriş işareti, Kazan'a akan Maya, şafakta sönme |
| Kartlar | 60 kartın 32'sinin kendi görseli (yörünge silahları, beden değişimleri, 12 aşı); diğerleri ortak "güçlenme" parlaması ve ikon |
| Güçler | 12 güç ve 4 ulti için ayrı efekt |
| Savunma | 8 kulenin atışı, 8 tuzağın tetiklenmesi |
| Tarım | Filiz çıkışı, sulama damlaları, olgunluk parıltısı, Alaca pırıltısı, hasat patlaması |
| Hayvan | Kalp (mutluluk), güven çubuğu, uyku Z'leri, ürün çıkışı |
| Mühendislik | Kablo akımı, soket kıvılcımı, makine buharı, bant üstünde ürün, drone izi, aşırı yük uyarısı |
| Yapı | Hayalet önizleme, yerleşme tozu, yükseltme dalgası, çökme tozu |
| Araç | Egzoz, toz izi, sıçrayan çamur ve su, far |
| Ekonomi | Kargo roketi kalkışı, kredi yağmuru, çark ışıkları |
| Dünya | Mancınık izi, paraşüt, Seyir Kulesi'nden harita açılışı, Kadim Taş ışıması, Ata gelişi |
| Hava | Yağmur, şimşek, sis, kum fırtınası, tipi, sıcak dalgası titremesi |

## 8. Arayüz

### 8.1 İlkeler

- **Önce dünya, sonra pano.** Çantanın doluluğu sırtta, makinenin durumu makinenin üstünde, takvim gökyüzünde görünür. Pano yalnızca dünyada gösterilemeyen şeyi taşır.
- **Gündüz sade, gece yoğun.** Gündüz ekranda yalnızca kemer, can ve dayanıklılık, pusula durur. Kabarma'da dalga sayacı, Öz çubuğu, Gösteri çarpanı ve kart şeridi eklenir.
- **İkonlar modelden çekilir.** Her eşyanın ikonu kendi 3B modelinin aynı ışık ve açıyla alınmış görüntüsüdür; böylece ikon seti kendiliğinden tutarlıdır.
- **Yazı tipi:** arayüz için yuvarlak hatlı bir sans, sayılar için sabit genişlikli; ikisi de açık lisanslı ve Türkçe, Kiril, Çince, Japonca, Korece kapsamı olan ailelerden.
- **Her panel** klavye-fare ve oyun koluyla tam kullanılır; ipucu ikonları son kullanılan aygıta göre değişir.

### 8.2 Ekranlar

Ana menü · dünya kurma ve ayarları · sunucu listesi · karakter ve Gardırop · oyun içi HUD (gündüz, Kabarma, araç, binek) · başlık çarkı · inşa çarkı ve Plan Defteri · envanter ve kuşam · istasyon panelleri (ortak şablon) · Harman Yeri (gen okuma) · hayvan kartı (ihtiyaçlar, genler, rol) · güç ağı görünümü · sinyal bağlama görünümü · harita · Defter (iş emirleri, Ansiklopedi, kartlar ve Nişanlar, Ustalık, koleksiyon) · Borsa Tahtası · Katalog · Tohum Çarkı · Çan (KS seçimi, tüzük maddeleri) · kart seçimi şeridi · Hasat Raporu · Ata can çubuğu · Holding ilişkileri · fotoğraf modu · ayarlar · F1 ayar paneli (geliştirme ve his ayarı).

### 8.3 Erişilebilirlik ve ayarlar

Tuş ve kol eşlemesi değiştirilebilir · basılı tut ya da aç-kapa seçimi · arayüz ve altyazı boyutu · üç renk körlüğü paleti (nadirlik ve hasar renkleri biçimle de ayrılır) · kamera sarsıntısı, vuruş duraklaması ve ekran parlaması sürgüleri · hareket bulanıklığı kapalı gelir · örümcek korkusu kipi (Kıvrık ve Kıskaç yuvarlak hatlı hâllerine döner) · nişan yardımı düzeyi · Suna sıklığı · ses kanalları ayrı ayrı · görüş alanı · kare hızı sınırı · grafik ön ayarları (düşük, orta, yüksek, Deck).

## 9. Görsel kalite ölçütleri

Her varlık ve her taşın ekran görüntüsü seti bu ölçütlerden geçer. İlk beşi betikle ölçülür; son üçünü ben görüntüye bakarak puanlarım (10 üzerinden, geçme 8).

| # | Ölçüt | Geçme koşulu | Nasıl ölçülür |
|---|---|---|---|
| 1 | Palet / doku | Dokulu yönde: doku var, 4K değil, bütçe çözünürlüğünde (§4). Eski yön: yüzeyin %95'i palete yakın (ΔE < 8) | Betik |
| 2 | Poligon | Sınıf bütçesinin içinde (§4) | Betik |
| 3 | Kalınlık | En ince parça en uzun kenarın %6'sından kalın | Betik: kalınlık örneklemesi |
| 4 | Bütünlük | Delik yok, havada kalan parça yok, ters yüz yok | Betik: mesh denetimi |
| 5 | Ölçek ve eksen | 1,6 m'lik karaktere göre doğru boy; taban merkezde, ileri yön doğru | Betik |
| 6 | Siluet | 64 piksellik siyah silueti doğru sınıfla eşleşiyor | Ben; referans föyle karşılaştırma |
| 7 | Dil uyumu | Referans varlıkların yanında aynı elden çıkmış duruyor | Ben; dört ışık koşulunda yan yana görüntü |
| 8 | Okunaklılık | Oyun kamerası mesafesinde işlevi anlaşılıyor (giriş, çıkış, tehlike, etkileşim) | Ben; oyun içi görüntü |

**Ekran görüntüsü ölçütleri:** üç derinlik katmanı okunuyor · gökyüzü geçişi ve sis var · saf siyah bölge yok · arayüz 1080p'de ve Deck çözünürlüğünde okunuyor · ışıyan yüzey gündüz ekranın %5'ini geçmiyor.

Ölçütten geçemeyen varlık en fazla dört kez yeniden üretilir; yine geçemezse koddan kurulur (Oduncu'daki kit yöntemi) ve aynı ölçütlerden geçirilir.

## 10. Ses

### 10.1 Ses efektleri

Her sesin 3–5 varyasyonu olur; çalarken perde ±%8, düzey ±2 dB oynar.

| Grup | Ses sayısı (varyasyon öncesi) |
|---|---|
| Çapa: 18 başlığın atışı, döngüsü, isabeti, boşta sesi | 90 |
| Malzeme darbeleri ve kırılmalar | 40 |
| Ayak sesleri: 6 zemin × yürüme, koşu, iniş | 60 |
| Yapı: yerleştirme, yükseltme, onarım, çökme | 30 |
| Makineler: 43 makinenin çalışma döngüsü ve tek sesleri | 86 |
| Güç ve sinyal: fiş takma, şalter, vınlama, siren | 24 |
| Araçlar: 7 araç × motor katmanları, korna, kapı, çarpma | 42 |
| Tarım: sürme, ekme, sulama, hasat, şişme | 30 |
| Hayvanların ses dışı sesleri: yeme, adım, kırkım, sağım | 20 |
| Yaratık sesleri: 27 tür × bekleme, uyarı, saldırı, acı, kabarma, patlama | 162 |
| Atalar ve Azmanlar | 72 |
| Kabarma: siren, gökyüzü, dalga girişi, Kazan, şafak | 30 |
| Kartlar ve seviye atlama | 20 |
| Güçler ve ultiler | 32 |
| Kuleler ve tuzaklar | 48 |
| Fiziksel şamata: çarpma, fırlatma, ezilme, tokat, mancınık, paraşüt | 40 |
| Arayüz | 50 |
| Ortam döngüleri: 6 bölge × gündüz ve gece, 8 hava durumu | 20 |
| **Toplam** | **yaklaşık 900** |

- **Motorlar ve döngüler:** birkaç devirde üretilmiş döngüler devire göre çapraz geçişle çalınır.
- **Kanallar:** Ana / Efekt / Ortam / Müzik / Suna / Sesli sohbet / Arayüz. Suna konuşurken müzik 4 dB kısılır.
- **Mekân:** biyoma ve kapalı alana göre yankı; uzaklıkla alçak geçiren süzgeç.
- **Düzey hedefi:** ana çıkışta tepe −1 dBTP; Suna, efektlerin ortalamasının 3 dB üstünde.

### 10.2 Müzik

**Kimlik: "uzay türküsü".** Bağlama, kaval ve davul gibi halk çalgılarının sıcak analog synth'lerle karışımı. Gündüz sakin ve geniş; Kabarma'da davul ve zurna tempoyu alır, oyun halaya döner.

| Kullanım | Parça | Not |
|---|---|---|
| Biyom gezme, gündüz ve gece | 12 | 6 bölge × 2; seyrek, uzun aralıklı |
| Üs teması | 2 | Tapu içinde, konfor yükseldikçe çalgı eklenir |
| Kabarma | 3 set × 3 yoğunluk katmanı | Aynı ton ve tempo; yoğunluk dalga numarasına ve Gösteri çarpanına göre vuruş sınırında geçer |
| Atalar | 6 | Her Ata'nın kendi teması |
| Azman, Holding baskını, Küçük Kabarma | 3 | |
| Ana menü, şafak ve rapor | 2 | |
| Radyo (üsteki süs eşyası) | 6 | Kısa çalgısal parçalar |
| Geçiş sesleri (stinger) | 20 | Seviye atlama, Alaca, Ata yenildi, basamak açıldı |

Müzik durumdan duruma en az iki saniyelik geçişle değişir; aynı parça art arda çalmaz; sessizlik de bir durumdur (gezme müziği zamanın yarısında susar ve ortam sesi kalır).

### 10.3 Suna ve sesler

- **Suna:** orta yaşlı, sakin, kuru bir santral memuru. Asla bağırmaz, asla gülmez; en büyük felakette bile tutanak okur gibi konuşur. Ses, tek bir tasarlanmış sentetik sestir; Türkçe ve İngilizce aynı sesle üretilir.
- **Telsiz süzgeci** oyunda uygulanır: bant geçiren süzgeç, hafif doyum, başta ve sonda mandal sesi. Bu hem kimliği kurar hem sentetik sesin pürüzlerini örter.
- **Replik dağılımı (dil başına yaklaşık 700):**

| Grup | Adet |
|---|---|
| İlk gün ve öğretici | 60 |
| İş emri okuma (48 zincirin açılışı ve bitişi) | 96 |
| Kabarma anonsları (takvim, aşamalar, sonuç) | 80 |
| Ölüm, bayılma, ezilme | 60 |
| İlk kez yapılanlar | 80 |
| Alaca, rekor, çark | 80 |
| Holding | 50 |
| Atalar | 48 |
| Hava ve saat | 40 |
| Boş zaman | 80 |
| Ortak oyun şamatası | 30 |

- **Holding hoparlörü:** kamplarda duyulan kurumsal erkek sesi, 60 replik ("Değerli çalışanlarımız, hasat hedefimizin gerisindeyiz.").
- **Oyuncu karakteri konuşmaz;** kısa nidalar çıkarır (zıplama, darbe, sevinme): 24 ses.
- **Altyazı** her replikte vardır; arayüzün 10 dilinin hepsinde.

## 11. Mağaza ve tanıtım görselleri

- **Kapsül:** oyun içi sahneden alınan ana görsel (kabarmış bir Gıdak'ın üstünde oyuncu, arkada aurora ve dev şalgam) + logo. Logo biçimi: harfleri kabarmış, yastık gibi bir yazı. Logo görsel üretimiyle çıkarılır, ardından vektöre çevrilip temizlenir.
- **Denetim:** logo 64 piksel yükseklikte okunuyor; kapsül Steam'in bütün kapsül boyutlarına otomatik kırpıldığında ana öğe ve logo çerçevede kalıyor; son seçim senin onayınla.
- **Ekran görüntüleri:** her biyomdan bir manzara, bir üs, bir Kabarma anı, bir Ata, bir otomasyon hattı, bir ortak oyun şamatası. Hepsi oyun içi fotoğraf moduyla.
- **Fragman:** oyun içi kamera aracıyla çekilir. Akış: sakin sabah (10 sn) → Suna'nın "bir huyu var" repliği → Kabarma (30 sn) → üs ve otomasyon (15 sn) → ortak oyun şamatası (15 sn) → logo.
