# Kabarma: Ana Plan

> Çalışma adı: **Kabarma** (TR) / **Moonswell** (EN).
> Bu belge 16 Kasım 2026'da yazıldı. Oyunun kendisi henüz yazılmadı; burada v1.0'ın tam planı var.
> Sayıların hepsi ayarlanabilir başlangıç değeridir. Tasarım kararları ise kesindir.

## 0. Belge haritası

| Belge | İçerik |
|---|---|
| **PLAN.md** (bu belge) | Oyun nedir, neden tutar, kilitli kararlar, v1.0'ın tanımı, riskler |
| [GDD.md](GDD.md) | Bütün sistemlerin kuralları ve formülleri |
| [ICERIK.md](ICERIK.md) | Adıyla sanıyla bütün içerik: biyomlar, yaratıklar, ekinler, aletler, kartlar, makineler, tarifler, iş emirleri ve basamak zincirinin doğrulaması |
| [SANAT-SES.md](SANAT-SES.md) | Görsel dil, animasyon, efekt, arayüz, ses ve müzik |
| [TEKNIK.md](TEKNIK.md) | Mimari, ağ, kayıt, dünya üretimi, yapay zekâ, performans, test |
| [URETIM.md](URETIM.md) | AI üretim hattı, kalite kapıları, kredi bütçesi, kilometre taşları, çıkış planı |

## 1. Oyun

### 1.1 Mağaza metni

> **Vahşi bir ayda çiftlik kur. Birkaç gecede bir gökyüzü tutuşur ve her şey kabarır.**
>
> Şalgamların fıçı kadar olur. Tavuğun ata döner. Tarlana dadanan tavşanlar plaj topu gibi şişip saldırır. Elindeki alet çatırdamaya başlar ve sen de onlarla birlikte güçlenirsin.
>
> Gündüzleri tek başına ya da 8 kişiye kadar arkadaşlarınla ev kur, tarla sür, hayvan besle, makine bağla, yeni topraklar keşfet. Kabarma gecesinde hasadını savun, sabah ayakta kalanı sat. Sonra daha büyüğünü kur.

### 1.2 Tek cümle

Sakin günlerde derin bir çiftlik ve üs kurma oyunu; birkaç gecede bir, aynı üssün içinde oynanan bir güçlenme koşusu.

### 1.3 Tek fiil: kabart

Oyundaki her şeyin iki hâli var: normal hâli ve **kabarmış** hâli. Bu, içerik eklemenin de kuralı. Oyuna yeni bir ekin, hayvan, düşman, alet ya da araç girdiğinde tek soru sorulur: "Bunun kabarmış hâli ne yapar?"

Oyuncunun bu fiille ilişkisi oyun boyunca değişir:
1. **Başta katlanırsın.** Kabarma gelir, tarlanı korumaya çalışırsın.
2. **Ortada yararlanırsın.** Neyi ekersen onu kabartır ve onu çekersin; tarlanı ve savunmanı buna göre kurarsın.
3. **Sonda yönetirsin.** Maya şırıngasıyla tek bir bitkiyi, hayvanı, kuleyi, aracı ya da kendini istediğin an kabartırsın; en sonunda Kabartma Odası ile bunu otomatiğe bağlarsın.

### 1.4 İlk 10 dakika

| Dakika | Ne oluyor |
|---|---|
| 0:00 | İniş kapsülünün kapağı altın sarısı bir çayıra açılıyor. Ufukta, ayın ortasındaki kraterden göğe yükselen ışık sütunu. Telsizde Suna: "Kooperatife hoş geldin, üye 4417. Tarla senin, borç bizim." |
| 0:30 | Tetiğe basıyorsun. Çapa'nın ışını kayayı kemiriyor, parçalar "pıt pıt" sırt çantana çekiliyor. Çantandaki yuvalar gözünün önünde doluyor. |
| 2:00 | Ocak'ı yere koyuyorsun. Çevresine ılık bir ışık kubbesi yayılıyor: tapun. İlk tezgâhı kuruyorsun. |
| 4:00 | Toprak başlığıyla 3×3 tarla açıyor, şalgam ekiyor, hortumla suluyorsun. Filizler anında baş veriyor. |
| 6:00 | Bir Gıdak geçiyor. Önüne buğday atıyorsun, peşinden az önce çaktığın ağıla giriyor ve yumurtluyor. |
| 8:00 | Hava kararıyor. Suna: "Küçük bir ayrıntı kaldı. Bu ayın bir huyu var." Gökyüzü dalgalanıyor. Dokuz şalgamın fıçı boyuna şişiyor. Üç Zıpzıp plaj topu gibi kabarıp çite yükleniyor. Çapa'n çatırdıyor, ilk kartını seçiyorsun: "Çatal". Sayılar havada uçuşuyor. Gıdak'ın midilli boyuna gelip birini çitin üstünden tekmeliyor. |
| 10:00 | Şafak. Fıçı boyundaki şalgamı çekim ışınıyla, iniş kapsülünden bozma Kargo Rampası'na taşıyorsun. Roket kalkıyor: "+120 Kredi". Defter açılıyor: 240 iş emri. Sıradaki: "5 kabarık ürün yetiştir." |

İlk gece senaryolu, kısa bir Küçük Kabarma'dır (3 dakika). Oyunun vaadi ilk 10 dakikada gösterilir; normal takvim ondan sonra başlar.

### 1.5 Bir Kabarma gecesi (oyunun ortası)

Çan çalındı. Ekip iki gruba ayrıldı. İki kişi evde: biri biçerdöverle tarlaya dalan kabarmış domuzları biçiyor, diğeri çatıdaki sapan kulesine karpuz taşıyor. Diğer iki kişi kamyonla Holding kampına gidiyor, çünkü kampın kalkanı sadece bu gece açık. Dönüşte kasada ev boyunda bir balkabağı var. Arkadan Holding'in kamyoneti kovalıyor. Evdeki oyuncunun 14. seviyede aldığı "Omlet Yağmuru" kartı sayesinde gökten yumurta yağıyor. Şafakta rapor çıkıyor: 3.400 Maya, 11 kabarık ürün, en çok ezilen oyuncu ödülü.

### 1.6 Referans oyunlardan ne alındı

Her satır, o oyunda elle tutulur bir şeye karşılık geliyor.

| Oyun | Ondaki somut şey | Bizdeki karşılığı |
|---|---|---|
| Astroneer | Sırtında yuvaları görünen çanta; ışınla kazıp emen alet; elle takılan kablolar; yuvarlak hatlı, doygun renkli düşük poligon | Sırt çantası ve Çapa aynı mantıkla çalışır; kablolar elle takılır; görsel dil bu çizgide |
| Firewatch | Kat kat sisli tepe siluetleri; telsizdeki ses; gözetleme kuleleri | Atmosfer ilkeleri; telsiz karakteri Suna; haritayı açan Seyir Kuleleri |
| Rust | Temel, duvar, tavan parçalarının birbirine oturması; çekiçle yerinde malzeme yükseltme; alanı sahiplenen dolap | Yapı sistemi ve Ocak'ın tapu alanı |
| Valheim | Boss'un bir sonraki basamağı açması; açlık yerine yemek güçlendirmesi; konfor; kullandıkça gelişen beceri; araziyi düzleme | Atalar, üç yemek yuvası, Dinlenmiş etkisi, Ustalık, Toprak başlığı |
| Subsistence | Seninle birlikte üs kurup büyüyen, sana baskın yapan, senin de baskın yaptığın AI rakipler; üssün güç ekonomisi | Holding kampları ve güç şebekesi |
| V Rising | Kale kalbi; baskının sadece belirli saatlerde açık olması; boss'tan güç kazanma | Ocak; "tapular yalnızca Kabarma'da askıda" kuralı; Kadim Taşlar ve Atalardan gelen güçler |
| Megabonk | Koşu içinde seviye atlayıp üç karttan birini seçmek ve saçma derecede güçlenmek; 240 görevlik liste; açılan karakterler | Kabarma kartları, 240 iş emri, 12 üye tipi |
| How to Fish | Doğadaki hayvanın karşılık vermesi; gösterişli öldürmeye çarpan; nadir renkli türlerin çarka atılması; tokatla diriltme; martının leşi kapması | Kabaran yaratıklar, Gösteri çarpanı, Alaca türler ve Tohum Çarkı, şok tokadı, yük çalan Leşçi |

## 2. Sütunlar

Tasarımda bir karar zorlaştığında bu beş cümleye bakılır.

1. **Her şey kabarır.** Yeni içerik, kabarmış hâli düşünülmeden eklenmez.
2. **Sakinlik gerçek, savaş randevulu.** Gündüz kimse seni zorla savaşa sokmaz. Kabarma'nın saati gökyüzünde yazar; istersen çanla öne alırsın, istersen dünyayı "yalnızca çanla" ayarına kurarsın.
3. **Ne ekersen onu çekersin.** Tarlan, ağılın ve makinelerin gece neyle savaşacağını ve ne kazanacağını belirler. Sistemler birbirinden bağımsız çalışmaz (§5).
4. **Elle tutulur, tıknaz, okunaklı.** Kaynak çantaya emilir, kablo takılır, dev ürün vinçle taşınır. Menü yerine dünya.
5. **Tek başına tam, arkadaşla kaos.** Her içerik tek oyuncuyla bitirilebilir. Arkadaş eklemek oyunu kolaylaştırmaktan çok karıştırır: ezilme, fırlatılma, yük kavgası.

## 3. Kurgu (kabuk)

Kurgu tek bu bölümde durur. Sistemler kurgudan bağımsız tasarlandı; kabuk değişirse aşağıdaki tablo yeniden adlandırılır, kurallar aynı kalır.

**Hikâye:** Ay Tarım Kooperatifi, verimli ama "huylu" bir aya üye yerleştiriyor. Sen üyesin. Ayın ortasındaki kraterden sızan **Maya**, birkaç gecede bir bütün canlıları ve Maya'ya bulanmış aletleri kabartıyor. Kooperatif bunu broşüre yazmamış. Rakip şirket **Bereket Holding** de aynı aya ekip indirdi ve senin hasadına göz dikti. Telsizdeki santral memuru **Suna** sana iş emirlerini okur, başına gelenlere kuru bir sesle yorum yapar.

**Ton:** Ciddiyetle oynanan saçmalık. Kimse şaka yaptığının farkında değildir. Suna asla gülmez.

| Kabuktaki ad | Altındaki sistem |
|---|---|
| Kabarma | Belirli aralıklarla gelen, zorluğu seçilebilen dalga olayı |
| Maya | Dalga olayından ve son bölgeden gelen üst basamak kaynağı |
| Ocak | Üs kalbi: tapu alanı, yeniden doğma, ortak depo bağlantısı, basamak kilidi |
| Çapa | Başlık takılan tek el aleti |
| Kabarma kartı | Dalga olayı boyunca geçerli, seviye atladıkça seçilen güçlendirme |
| Atalar | Biyom boss'ları |
| Bereket Holding | Seninle birlikte büyüyen rakip üsler (PvP dünyasında gerçek ekipler) |
| Kooperatif, Suna | Görev veren, satın alan, yorumlayan ses |
| Alaca | Nadir renk varyantı |

## 4. Kilitli kararlar

| Konu | Karar | Gerekçe |
|---|---|---|
| Kamera | Üçüncü şahıs, birinci şahıs modu yok. İki kip, V ile geçilir (sahibin kararı, 18 Kasım 2026): **normal** (karakterin arkasında ve üstünde; fare tekerleğiyle yakınlaştırma) ve **yakın omuz** (Astroneer'deki gibi kamera omzun hemen arkasında ve yanında; kafa ve omuz ekranın kenarında görünür, nişan neredeyse birinci şahıs gibi hisseder). | Karakter ve kozmetik hep görünür; yakın omuz kipi nişan ve inşa için. El ve kol animasyonu birinci şahıs için ayrıca gerekmez. |
| Elde tutulan şey | Tek alet (Çapa), tek tutuş pozu, 18 başlık | Her yeni silah yeni animasyon istemez; iş ışına, efekte ve sese düşer. |
| Animasyon | Gövde ve yaratıklar: AI ile kemiklendirme ve hazır klipler + kodla katmanlar (IK, yay, şişme). Ölüm ve ezilme: ragdoll. | Tek zayıf nokta birinci şahıs animasyonuydu; o tasarımda yok. |
| Motor | Godot 4.7.2 (.NET sürümü), C# | Her şey metinden yazılır; derleyici hataları çalıştırmadan yakalar. Godot bu makinede Oduncu ile doğrulandı; .NET sürümü M0'da doğrulanacak ([TEKNIK.md](TEKNIK.md) §1). Rust motoru (Bevy) seçilmedi: fizik, animasyon, ses, arayüz ve Steam katmanları Godot'ta hazır ve denenmiş; Bevy'de her biri ayrı risk olurdu. |
| Kod yapısı | Motordan bağımsız `Sim` kütüphanesi (kurallar) + Godot katmanı (fizik, görüntü, ses) | Ekonomi, tarım, güç ve dalga dengesi saniyeler içinde binlerce oyun günü simüle edilerek test edilir. |
| Sunucu | Host yetkili. Ortak oyunda oyunculardan biri host; kalıcı dünyalar için başsız (headless) sunucu derlemesi. | Fizik komedisi için iki tarafta aynı fizik motoru gerekir; ayrı bir özel sunucu yazmak bu yüzden seçilmedi. |
| Oyuncu sayısı | Ortak oyun 1–8. Adanmış sunucu 16'ya kadar (Çekişme: 4 ekip × 4). | Kabarma'da 220 etkin düşmanla ağ bütçesi buna göre hesaplandı. |
| Dünya | Tohumdan üretilen, sınırlı, tek parça ada (yaklaşık 5 × 5 km), yükleme ekranı yok | Tek ortak mekân; "sonsuzluk" haritada değil üste, dalga seviyesinde ve ıslahta (§6). |
| Arazi | Yükseklik haritası + Valheim tarzı düzleme, yükseltme, kazma. Voksel tünel yok. | Ağda farkları göndermek ucuz ve sağlam. Mağaralar hazır iç mekân. |
| Görsel | Stilize ama dokulu: AI modelinin dokusu korunur, orta poligon bütçesi ([SANAT-SES.md](SANAT-SES.md) §4), Firewatch atmosferi. Flash oyunu ya da çizgi film gibi durmaz. | 17 Kasım 2026, sahibinin kararı: M0 denemelerinde dokusuz, palete oturtulmuş düşük poligon "flash oyun / çizgi film" gibi durdu. Eski kural (dokusuz, köşe rengi) bırakıldı; tutarlılık artık istem dili, doku sürümü ve ortak ışıkla sağlanır. |
| Para | Gerçek parayla satış yok. Çark ve kasa oyun içi krediyle çalışır. | |
| Diller | Arayüz: 10 dil (Türkçe, İngilizce, Almanca, Fransızca, İspanyolca, Brezilya Portekizcesi, Rusça, Basitleştirilmiş Çince, Japonca, Korece). Seslendirme: Türkçe ve İngilizce. | |
| Giriş | Klavye-fare ve oyun kolu, ilk günden. Steam Deck hedefi. | |
| Platform | Windows ve Linux (Steam). | |

## 5. Neden Valheim ve Subsistence'tan derin

Derinlik sistem sayısından gelmez, sistemlerin birbirini beslemesinden gelir. Aşağıdaki her sistem en az iki başka sistemi besliyor. Ayrıntılar [GDD.md](GDD.md)'de.

| Sistem | Beslediği sistemler ve nasıl |
|---|---|
| **Tarım** | *Kabarma:* ekin ailesi hangi düşmanın geleceğini belirler. *Savaş:* tohumlar Tohum Makinalısı'nın mermisi, ürünler sapan kulesinin cephanesi. *Hayvancılık:* yem. *Mühendislik:* biyoyakıt, yağ, lif. *Atalar:* çağırma adağı kabarık üründür. *Ekonomi:* ihracat. *Yapı:* dev ürünler oyulup ev olur. |
| **Hayvancılık** | *Tarım:* gübre, arıyla tozlaşma ve çaprazlama. *Savaş:* kabaran hayvanlar binek, bekçi ve yük hayvanı olur. *Mühendislik:* hayvan çarkı ve Cızbız tankı elektrik üretir, Kızgın canlı fırındır, Kıskaç kaya kırar. *Keşif:* Hu harita açar, Homur cevher bulur. *Ekonomi:* yün, süt, yumurta, Alaca türler. |
| **Mühendislik** | *Tarım:* sulama, sera iklimi, hasat ve ekim robotu. *Hayvancılık:* yemlik, sağım, kuluçka. *Savaş:* kuleler, tuzaklar, sensörle çalışan kapılar. *Kabarma:* toplayıcı o gece aküleri doldurur; Kabartma Odası olayı otomatikleştirir. *Araçlar:* atölye ve yakıt. |
| **Yapı** | *Savaş:* sur, kapı, kule tabanı; düşman yol bulurken duvar kırmayı hesaba katar. *Konfor:* Dinlenmiş etkisi. *Tarım ve hayvancılık:* sera, ağıl, ahır. *Mühendislik:* makine platformları, kablo direkleri. |
| **Kabarma** | *İlerleme:* Maya ve kabarık ürün olmadan basamak atlanmaz. *Tarım:* kabarık ürün. *Hayvancılık:* kabarık parçalar ve savaş hayvanı deneyimi. *Ekonomi:* en değerli mallar. *Kartlar:* Nişan ilerlemesi. |
| **Keşif** | *İlerleme:* her biyom yeni malzeme, ekin, hayvan ve Ata. *Güçler:* Kadim Taşlar. *Mühendislik:* enkazlardan plan parçaları. *Hareket:* Seyir Kuleleri ve mancınık ağı. |
| **Holding (rakipler)** | *Savaş:* gündüz kervan pususu, gece baskın ve karşı baskın. *Ekonomi:* onların silosu. *İlerleme:* en iyi çipler yalnızca Holding parçasından yapılır. *Kaynak:* aynı madenlere onlar da gelir. |
| **Ekonomi** | *İlerleme:* katalogdan tohum, yumurta, araç gövdesi, süs. *Çark:* nadir tohum, çip ve kart. *İş emirleri:* yönlendirme ve ödül. |

**Aynı soruya üç oyunun cevabı:**

| Soru | Valheim | Subsistence | Kabarma |
|---|---|---|---|
| Baskında ne gelir? | Rastgele olay | Rakip avcılar | Ne ektiysen onun düşmanı, artı rakip ekip |
| Tarım ne işe yarar? | Yemek | Yemek | Yemek, cephane, yakıt, adak, düşman seçimi, yapı malzemesi |
| Hayvan ne işe yarar? | Et, deri | Et, yumurta | Ürün, binek, bekçi, elektrik, fırın, madenci, kâşif |
| Otomasyon | Yok | Sulama borusu, teleferik | Bant, kol, drone, robot; sonunda Kabarma'nın kendisi |
| Islah | Yok | Yok | Bitkide ve hayvanda dört gen, çaprazlama, Alaca türler |
| Savaşta güçlenme | Ekipman | Ekipman | Ekipman + o geceye özel kart yığını |

## 6. Oyun saatleri

| Saat | Oyuncu ne yapıyor |
|---|---|
| 1 | Ocak kuruldu, ilk tarla ve ağıl var, Küçük Kabarma atlatıldı, ilk kargo satıldı. |
| 10 | Bakır aletler, ahşap ev, 30 karelik tarla, 6 hayvan. İkinci ya da üçüncü Kabarma. İlk Ata (Koca Pofuduk) yenildi, Sis Korusu açıldı. İlk Holding kampı ufukta belirdi. |
| 50 | Çelik basamağı. Traktör ve kamyon. Rüzgâr, su çarkı ve akülerle şebeke. Kuleli surlar. Kabarma seviyesi 12–15. Savaş hayvanı ıslahı başladı. Holding kampına ilk gece baskını yapıldı. Üç Ata yenildi. |
| 100 | Göbek'e girildi, Ana Maya yenildi. Dronelu depo, hasat ve ekim robotları. Maya şırıngası. |
| 200 | Aşağıdaki "sonsuz" hedeflerin içinde. |

**200. saatte oyunda duran hedefler** (hepsi v1.0'da var):
1. **Kabarma merdiveni:** Seviyenin üst sınırı yok. 26. seviyeden sonra her seviye yeni bir "tüzük maddesi" (zorlaştırıcı kural) ekler. Ekip rekoru Steam sıralamasına yazılır.
2. **Islah:** 24 ekin ve 16 hayvanda dört genin 10'a çıkarılması; her türün üç Alaca varyantı. Toplam 120 koleksiyon satırı.
3. **Tam otomasyon:** Ekimden kargoya el değmeyen çiftlik; Kabartma Odası ile gündüz kabarık ürün üretimi.
4. **Kart Nişanları:** 72 kartın her biri beş Nişan seviyesi.
5. **Holding Genel Merkezi:** Yalnızca Kabarma gecesinde girilen, 10. seviyeye kadar büyüyen son baskın hedefi.
6. **Kabarık Atalar:** Her Ata, Kabarma seviyesine bağlı güçte yeniden çağrılır; üst basamak çipleri buradan gelir.
7. **240 iş emri, 12 üye tipi, 50 başarım.**
8. **Yeni Ay:** Karakterini koruyup tüzük maddeleri seçerek yeni tohumda dünya açmak.
9. **Çekişme dünyaları:** Gerçek rakip ekiplerle aynı adada.
10. **Sınırsız üs:** 6. basamak Ocak'ta tapu direği sayısı sınırsızdır.

## 7. v1.0'ın tanımı

v1.0, aşağıdaki içeriğin tamamının oynanabilir, dengelenmiş, seslendirilmiş ve cilalanmış olduğu sürümdür. Erken erişim yolu ve kesme listesi yok.

### 7.1 İçerik sayıları

| Kalem | Adet | Liste |
|---|---|---|
| Biyom | 5 + Göbek krateri | [ICERIK.md](ICERIK.md) §1 |
| Kaynak ve ara ürün | 67 | §2 |
| Yaratık türü | 27 (16'sı evcilleşir) + 5 Holding birimi | §3 |
| Ata (boss) / Azman (gece mini boss'u) | 6 / 6 | §4 |
| Oyuk koruyucusu / Holding boss'u | 5 / 1 (Müdür) | §4 |
| Ekin | 24 | §5 |
| Çapa başlığı | 18 (6 iş, 12 silah) | §6 |
| Çip | 24 × 4 nadirlik | §6 |
| Güç / Ulti | 12 / 4 | §7 |
| Kabarma kartı / tüzük maddesi | 72 (60 + 12 aşı) / 12 | §8 |
| Üye tipi (karakter) / kozmetik parça | 12 / 72 | §9 |
| Yapı parçası | 26 şekil × 4 malzeme + cam seti | §10 |
| Yerleştirilebilir (kapı, depo, mobilya, süs, ışık) | 72 | §10 |
| İstasyon ve makine | 43 | §11 |
| Güç, sinyal ve taşıma parçası | 43 | §12 |
| Kule ve tuzak | 8 / 8 | §13 |
| Araç / eklenti | 7 / 10 | §14 |
| Yemek tarifi | 32 | §15 |
| Zırh takımı / kuşam modülü | 7 / 18 | §16 |
| Keşif noktası türü | 9 | §17 |
| Borsa kategorisi | 12 | §18 |
| İş emri | 240 (48 zincir × 5 adım) | §18 |
| Başarım | 50 | §18 |
| Ustalık | 8 dal × 50 seviye, 80 özellik | §19 |

### 7.2 Kalite çıtası

Bir özellik ancak şu altısı tamamsa "bitti" sayılır:
1. Kuralı GDD'de yazıyor, sayıları veri dosyasında.
2. Otomatik testi geçiyor (birim, simülasyon ya da senaryo).
3. Sesi, efekti ve arayüz geri bildirimi var.
4. Klavye-fare ve oyun koluyla, tek oyuncuda ve 4 kişilik ortak oyunda çalışıyor.
5. Kaydedilip yüklenince aynı duruyor.
6. Ekran görüntüsü ve kısa videosu görsel ölçütleri geçiyor ([SANAT-SES.md](SANAT-SES.md) §9).

**M1'de 4. ve 5. koşul:** Ortak oyun ve kayıt M2'nin teslimidir; M1'deki hiçbir özellik bunları karşılayamaz. M1'de bir özellik 4. koşulun ortak oyun yarısını ve 5. koşulu "M2'de" diye raporlar; 4. koşulun klavye-fare ve oyun kolu yarısı M1'de de zorunludur. Karşılığında yeni `Sim` durumu kayda hazır yazılır ve Godot katmanına yeni statik durum eklenmez ([TEKNIK.md](TEKNIK.md) §9).

Oyunun tamamı için çıkış kapısı [URETIM.md](URETIM.md) §8'de.

## 8. Tasarım sınırları

Bunlar eksik değil, verilmiş kararlardır.

- Birinci şahıs kamera yok; en yakın görünüş Astroneer tarzı yakın omuz kipidir (bkz. §4 Kamera).
- Voksel tünel kazma yok; arazi yükseklik haritasıdır.
- Açlıktan ölme yok; yemek güç verir.
- Gündüz zorla baskın yok; savaşın saati bellidir.
- Kalıcı kayıp yok: yapı onarılır, hayvan "bayılır" ve ahırda uyanır, ölen oyuncu çantasını yerden alır.
- Eşleştirme ve sıralı PvP yok; PvP dünyaları arkadaş daveti ve sunucu listesiyle kurulur.
- Gerçek parayla satış yok.

## 9. Riskler

| Risk | Olasılık | Önlem |
|---|---|---|
| Görsel "AI işi" ya da "programcı işi" duruyor | Orta–yüksek | İlk kilometre taşı sadece görünüş ve his dilimi; genişliğe ondan önce geçilmez. Her varlık bütçe, siluet ve dil uyumu kontrolünden geçer; geçemeyen yeniden üretilir ya da koddan kurulur ([URETIM.md](URETIM.md) §3). |
| **Yaratık animasyonu tek bir yönteme dayanıyor** | Orta–yüksek | Planın en zayıf noktası. Servislerde insansı olmayan gövdeler için yalnızca yürüyüş klibi var; 27 türün kalan bütün klipleri benim kodla yazacağım poz kütüphanesinden gelecek ([URETIM.md](URETIM.md) §2.2 E) ve bu yöntem henüz denenmedi. M0'da iki gövde planıyla sınanır, M1'de sen ekranda görüp onaylarsın. Yedek: yaratıkları katı parçalardan kurup tamamen kodla oynatmak. Bedeli açık: hayvanlar yumuşak gövdeli canlı gibi değil, kurmalı oyuncak gibi durur; Pofuduk'un yünü, Vrak'ın sıçrayışı gibi sevimlilik veren hareketler kaybolur ve gündüz hayvancılığı görsel olarak zayıflar. Şişme, titreme ve patlama shader olduğu için bundan etkilenmez; yani Kabarma gecesi görüntüsünü korur, gündüz kaybeder. |
| Hazır insansı klipler tıknaz gövdeye oturmuyor | Orta | M0'da oyuncu gövdesiyle denenir. Yedek: Oduncu'da çalışan tamamen prosedürel gövde (yay, IK, ragdoll). |
| C# araç zinciri bu makinede kurulu değil | Kesin | M0'ın ilk işi kurulum ve doğrulama. Başarısız olursa yedek GDScript'tir; `Sim` kütüphanesi fikri GDScript'te de korunur ama test hızı düşer. |
| Kabarma'da yüzlerce düşmanla ağ ve kare süresi | Orta | Sürüler tek ağ varlığıdır; etkin düşman tavanı 220; M3'te 8 istemcili bot testiyle ölçülür ([TEKNIK.md](TEKNIK.md) §5). |
| Rakip AI'nın üs içinde yol bulması | Orta | Izgara üstünde, duvar kırma maliyetini hesaba katan yol bulma; baskın hedefi öncelikle açık tarla ve silo. |
| Denge: 60'tan fazla sistemin sayıları | Yüksek | Sayılar veri dosyasında; `Sim` ile hızlandırılmış oyuncu modeli; her kilometre taşında hedef eğriye uyum ölçülür ([TEKNIK.md](TEKNIK.md) §11). |
| "His" yargısı | Yüksek | Ben ölçülebilir olanı ölçerim (tepki süresi, vuruş duraklaması, kare süresi); his onayını her taşın sonunda oynayarak sen verirsin. |
| Kapsam | Yüksek | Genişlik sırası sabit: önce dilim, sonra sistemler az içerikle, sonra içerik. Hiçbir sistem içeriği beklemez; içerik veriyle eklenir. |
| AI servislerinin fiyatı ya da yeteneği değişir | Orta | Hat, sağlayıcıdan bağımsız adımlara bölünmüş; her adımın en az bir yedeği var ([URETIM.md](URETIM.md) §2). |

## 10. Senden gerekenler

| Ne | Ne zaman |
|---|---|
| Kredi bütçesi onayı (yaklaşık tutar [URETIM.md](URETIM.md) §5'te) | M0'dan önce. Onay olmadan üretim yapmam. |
| Her taşın sonunda oynayıp his onayı | M1'den itibaren |
| İkinci bilgisayar ve ikinci Steam hesabı, sonra 4–8 kişilik test grubu | M2 ve M5 |
| Steamworks hesabı ve uygulama kimliği | M5'ten önce |
| Mağazadaki son ad için marka kontrolü ve onay | M6'dan önce |

## 11. Satış üzerine

Hiçbir plan satışı garanti etmez; bu plan elimdeki en güçlü kanca, en geniş içerik yüzeyi ve AI ile gerçekten bitirilebilecek kapsamın kesişimidir, ve ilk gerçek sınavı M1 dilimini oynadığında vereceğin tepkidir.
