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

**Altın Çayır'ın dört gün saati** (M1'in ışık ve ekran görüntüsü saatleri; değerler veriye yazılır):

| Saat | Güneş yüksekliği | Gökyüzü tepe → ufuk | Güneş ışığı | Sis |
|---|---|---|---|---|
| Sabah | 12°, doğudan | `#5F98CF` → `#F4D2AE` | `#FFD6A6` | `#E9CDAE`, yoğunluk ×1,5 |
| Öğle | 62° | `#3E7DC2` → `#D7E2E2` | `#FFF5E4` | `#C9D6DA` |
| Gün batımı | 4°, batıdan | `#3A4A86` → `#F48A52` | `#FF8C4A` | `#C98466` |
| Kabarma gecesi | Güneş yok; ay 35° | `#1A2238` → `#5FF0C8` (aurora bandı) | Ay `#A9B8E8` | `#2A3358`; zemin damarları Maya paletinde |
| Normal gece | Güneş yok; ay 35° | `#1A2238` → `#2E3E66` | Ay `#A9B8E8` | `#2A3358` |

Gündüz renkleri 18 Kasım görsel düzeltmesinde (gorsel-dunya) daha derin gökyüzü ve ince sise çekildi; enerji ve ortam ışığı veride. M1'in 16 ekran görüntüsü ilk dört satırdandır; normal gece M1'de ışık olarak vardır ama görüntü setine girmez.

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

**Kaynak kuralı:** Döngülü yürüyüş, akrobasi ve jestler insansı klip kütüphanesinden ya da düz yazıyla üretilir. Yerinde duran ya da üst gövdesi sabit kalan klipler (ekme, yeme, uyuma, oturma, okşama, kol çevirme, itme, taşıma, sürücü, binek, paraşüt, süzülme, şok tokadı) yaratıklardaki poz yöntemiyle kodla yazılır ([URETIM.md](URETIM.md) §2.2 E).

**M1'in 30 klibi:**

| Kaynak | Klipler | Adet |
|---|---|---|
| Kütüphane ya da düz yazı | Bekleme (2), sekiz yönlü yürüme ve koşu (16), depar, zıplama-havada-iniş (3), düşme, önden ve arkadan kalkış (2) | 25 |
| Kodla poz | Diz çöküp ekme, yeme, uyuma, el arabası itme, suda yürüme (yürüme üstüne katman) | 5 |

**Sonraki taşlara kalan 23:** merdiven tırmanma, oturma, paraşüt, ağır yük taşıma, sürücü koltuğu, hayvan okşama, kol çevirme, şok tokadı, baygın sürünme → M2 · binek üstü → M3 · süzülme → Planör ya da Gıdak Kostümlü'nün geldiği taş, en geç M4 · 12 jest → M4.

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

**Altın yaratıkların sınıfı:** Zıpzıp küçük sınıftadır (kabarmamış hâli küçük zararlı, ağırlık 1). 5.764 üçgen bütçeyi %15 aşıyor; hat 5.000'e indirecek (kredi yok). İskelet ağırlıklarının sadeleştirmeden sağlam çıktığı doğrulanacak; bozulursa yeniden kemiklendirilir (Tripo, 25 kredi, onaylı). Pofuduk orta sınıftadır (5.576, bütçe içinde).

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
- **Gündüz sade, gece yoğun.** Gündüz ekranda yalnızca kemer, can ve dayanıklılık, pusula durur. Pusula Ocak'ı ve etkin hedefi işaretler; altında Defter'in etkin adımı tek satır durur (tuşlar glifle). Kabarma'da dalga sayacı, Öz çubuğu, Gösteri çarpanı ve kart şeridi eklenir.
- **İkonlar modelden çekilir.** Her eşyanın ikonu kendi 3B modelinin aynı ışık ve açıyla alınmış görüntüsüdür; böylece ikon seti kendiliğinden tutarlıdır.
- **Yazı tipi:** arayüz için yuvarlak hatlı bir sans, sayılar için sabit genişlikli; ikisi de açık lisanslı ve Türkçe, Kiril, Çince, Japonca, Korece kapsamı olan ailelerden.
- **Her panel** klavye-fare ve oyun koluyla tam kullanılır; ipucu ikonları son kullanılan aygıta göre değişir.
- **Görsel dil: Rust tarzı arayüz (sahibin kararı, 18 Kasım 2026).** Yerleşim, oran ve his Rust'ın arayüzüne yakın; kod, ikon ve çizimler özgün (Rust'ın varlıkları kullanılmaz). Başvuru: sahibin açık kaynak Godot C# projesi [Hollow-Universe](https://github.com/uruskan/Hollow-Universe): `game/scripts/Common/Ui.cs` (tasarım sistemi: palet, yazı rampası, tema, keskin ölçekleme), `game/scripts/App/UiScaler.cs`, `game/scripts/UI/GroundUi.*.cs` (HUD, envanter, çark, üretim, ayarlar), `docs/DESIGN.md` UI-01…UI-06, Barlow yazı tipleri (OFL). Yerel kopya (yalnız bu dosyalar, git dışı): `.tools/ref/Hollow-Universe/`. Kurallar:
  - Koyu, yarı saydam, sıcak gri paneller; açık kenarlı yuvalar; büyük harf, kalın, hafif aralıklı dar yazı (Barlow Condensed, OFL, Türkçe kapsamlı); seçili yuva mavi-beyaz.
  - **Kemer (hotbar)** alt ortada, 8 yuva, yuva numarası, adet ve dayanıklılık çubuğu; 1–8 tuşları kemeri seçer.
  - **Can ve dayanıklılık** sağ altta, ikonlu yatay çubuklar ve sağa yaslı sayılar; Çivi Tabancası tutulurken cephane bunların üstünde.
  - **Pusula** üst ortada şerit; Ocak ve etkin hedef işaretli. Defter'in etkin adımı pusulanın altında tek satır.
  - **Etkileşim istemi** nişangâhın yanında (glif + fiil + nesne adı); toplanan eşya sağda birikip sönen satırlar.
  - **Envanter (Tab):** solda kuşam yuvaları, ortada 20 yuvalı çanta ızgarası ve altında kemer, sağda seçili eşyanın ayrıntısı ya da açılan kabın (Sandık, istasyon) paneli; sürükle-bırak, Shift+tık hızlı aktarım.
  - **Başlık çarkı:** Çapa'nın 6 iş başlığı ve silahları **yalnız** çarktan seçilir (Q basılı; kolda sol tampon basılı). Sayı tuşları başlık seçmez. Çark, Rust'ın inşa çarkı gibi krem halka üstünde kızıl-turuncu seçim dilimi.
  - Kabarma'da dalga sayacı, Öz çubuğu, Gösteri ve kart şeridi aynı panel dilinde eklenir.

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

**M1 yer imleri** (Altın Çayır köşesi). Her yer imi sabit kamera konumu ve bakış yönüdür, veride durur; oyun kamerası yüksekliğinde, arayüz açık. 16 görüntü = 4 yer imi × §2'deki 4 saat; Kabarma gecesi görüntüleri ilk gecenin 2. dalgasında alınır.

| # | Yer imi | Kadrajda |
|---|---|---|
| 1 | İniş kapsülü | Kapsül (Kargo Rampası) ön planda, çayır, ufukta krater ışık sütunu |
| 2 | Üs | Ocak, 3×3 tarla, ağıl ve Gıdak; arkada istasyonlar |
| 3 | Göl kıyısı | Su ve köpük çizgisi, kil yatakları, ağaçlar, sisli tepeler |
| 4 | Tepe | En yüksek tepeden üsse ve kratere; üç sis katmanı |

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

**M1'in 60 sesi** (↻ döngü; *M0* işaretli 12'si M0'da üretildi. M1'de her sesin 3 varyasyonu olur.)

| Grup | Adet | Sesler |
|---|---|---|
| Çapa iş başlıkları | 9 | kazma ışını ↻ *M0* · kaya kemirme ↻ (taş, kil, bakır) · parça emilişi "pıt" *M0* · biçme süpürmesi · bitki darbesi (ağaç, çalı, ot) · çekim ışını ↻ · fırlatma · toprak sürme *M0* · sulama püskürtmesi ↻ *M0* (depo dolumunda aynı ses pes çalar) |
| Silahlar, vuruşun üç katmanı ([GDD.md](GDD.md) §4) | 8 | atış: Tırpan Işını · atış: Çivi Tabancası *M0* · isabet: kesme *M0* · isabet: çivi saplanması · isabet: kritik · isabet: zayıf nokta · parça: kauçuk ve Maya kırıntısı saçılması · cephane bitti tıkı |
| Oyuncu | 6 | adım: ot · adım: toprak · adım: tahta · iniş (ağır varyasyonu yere serilme) · acı nidası · yeme |
| Gıdak ve Zıpzıp | 7 | Gıdak gıdaklaması (ürkme varyasyonlu) · yumurtlama · Koç Gıdak tekmesi · Zıpzıp sıçraması · Zıpzıp kemirmesi · Zıpzıp acısı · kabarmış yaratık patlaması *M0* |
| Tarım | 4 | tohum ekme · filiz çıkışı · hasat çekme · kabarık ürün gümlemesi (yük nesnesi yere ya da arabaya) |
| Yapı | 4 | parça yerleşmesi *M0* (kısa varyasyonu onarım) · söküm · parçaya darbe (çite yüklenme) · ahşap kapı ve kapak (kapı, bahçe kapısı, Sandık, Folluk) |
| İstasyonlar ve araç | 6 | Ocak yanması ve tapu kubbesi · ateş çıtırtısı ↻ (Ocak, Ocakbaşı, Taş Fırın) · tezgâh çalışması ↻ (Tezgâh, Paketleme) · Taş Fırın kapağı · kargo roketi ve kredi *M0* · El Arabası tekerleği ↻ |
| Kabarma | 5 | siren *M0* · gökyüzü dalgalanması ↻ · şişme *M0* (ekin ve canlı; perde boya göre) · dalga girişi · şafakta sönme |
| Kartlar ve seviye | 4 | seviye atlama (Çapa çatırdaması) *M0* · kart seçimi · Yanık tutuşması · Donma çıtırtısı |
| Arayüz | 5 | odak · onay (üretim bitti, adım tamam, rapor açıldı) · hata (yetmiyor, geçersiz yer) · başlık çarkı · telsiz mandalı (Suna'nın başı ve sonu) |
| Ortam | 2 | çayır gündüzü ↻ · göl kıyısı ↻ (Kabarma gecesini gökyüzü döngüsü ve müzik taşır) |
| **Toplam** | **60** | |

Arayüz grubu Oduncu'daki sentez betikleriyle de yapılabilir (kredi yok).

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

- **Seslendirilen replik tuş adı içermez.** Tuşlar değiştirilebildiği için (§8.3) "E'ye bas", "(6)", "Tab ile" gibi yönlendirmeler ekrandaki adım satırına, son kullanılan aygıtın glifiyle yazılır; Suna yalnızca ne yapılacağını söyler. Aynı kural altyazıya da uyar: altyazı seslendirilen metnin aynısıdır.
- **M1: 30 replik, Türkçe ve İngilizce (60 kayıt).** Giriş 2 · ilk günün dokuz adımı 14 · Küçük Kabarma ve şafak 6 · ilk 10 dakikadan sonraki yedi adım 7 · sıradaki iş emri 1 ([GDD.md](GDD.md) §24). Adımların hedef satırları seslendirilmez, yalnızca ekranda durur.
- **Holding hoparlörü:** kamplarda duyulan kurumsal erkek sesi, 60 replik ("Değerli çalışanlarımız, hasat hedefimizin gerisindeyiz.").
- **Oyuncu karakteri konuşmaz;** kısa nidalar çıkarır (zıplama, darbe, sevinme): 24 ses.
- **Altyazı** her replikte vardır; arayüzün 10 dilinin hepsinde.

## 11. Mağaza ve tanıtım görselleri

- **Kapsül:** oyun içi sahneden alınan ana görsel (kabarmış bir Gıdak'ın üstünde oyuncu, arkada aurora ve dev şalgam) + logo. Logo biçimi: harfleri kabarmış, yastık gibi bir yazı. Logo görsel üretimiyle çıkarılır, ardından vektöre çevrilip temizlenir.
- **Denetim:** logo 64 piksel yükseklikte okunuyor; kapsül Steam'in bütün kapsül boyutlarına otomatik kırpıldığında ana öğe ve logo çerçevede kalıyor; son seçim senin onayınla.
- **Ekran görüntüleri:** her biyomdan bir manzara, bir üs, bir Kabarma anı, bir Ata, bir otomasyon hattı, bir ortak oyun şamatası. Hepsi oyun içi fotoğraf moduyla.
- **Fragman:** oyun içi kamera aracıyla çekilir. Akış: sakin sabah (10 sn) → Suna'nın "bir huyu var" repliği → Kabarma (30 sn) → üs ve otomasyon (15 sn) → ortak oyun şamatası (15 sn) → logo.
