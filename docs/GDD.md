# Kabarma: Oyun Tasarımı

> Kurallar ve formüller burada, adlı içerik listeleri [ICERIK.md](ICERIK.md)'de.
> Bütün sayılar `data/` altındaki dosyalarda durur ve ayarlanabilir. Kısaltmalar: **T** = Ocak basamağı (1–6), **KS** = Kabarma seviyesi.

## 1. Döngüler

| Ölçek | Döngü |
|---|---|
| Saniye | Nişan al → ışın ya da atış → parça ve sayı fırlar → çantaya emilir |
| Dakika | Topla → üret → kur → ek ya da besle → sat |
| Gün (30 dk) | Sabah hasat ve bakım → gündüz keşif ya da inşa → akşam dönüş → gece dinlenme ya da Küçük Kabarma |
| Kabarma (varsayılan 4 günde bir) | Hazırlık → gece: dalgalar ve kart yığını → şafak: hasat, rapor, satış → onarım ve yükseltme |
| Basamak (saatler) | Yeni biyom → yeni malzeme, ekin, hayvan → adak → Ata → Ocak yükselir → yeni istasyonlar ve daha yüksek KS |

## 2. Zaman ve takvim

- Bir oyun günü 30 gerçek dakikadır: 22 dk gündüz, 8 dk gece. Dünya ayarıyla ×0,5 ile ×2 arasında değişir.
- Bütün oyuncular yataktaysa gece atlanır. Kabarma gecesi atlanamaz.
- **Kabarma takvimi:** varsayılan her 4. gece. Ayar: 2–7 gün ya da "yalnızca çanla". Gökyüzündeki Maya halkası kalan süreyi gösterir; son gün halka tamamdır ve Suna sabah anons eder.
- **Çan:** Ocak'taki çan çalınırsa bir sonraki gün batımında Kabarma başlar ve takvim sıfırlanır. Çan çalınırken KS seçilir.
- Kabarma gecesi 14 dakikadır.

## 3. Oyuncu

### 3.1 Nitelikler

| Nitelik | Taban | Ne artırır |
|---|---|---|
| Can | 100 | Yemek, zırh takımı, kartlar |
| Dayanıklılık | 100 | Yemek, Ustalık |
| Şarj (kuşam enerjisi) | 100 | Kuşam modülleri |
| Taşıma gücü (Çekim) | 80 kg × Çapa çarpanı | Çapa basamağı, çip |

### 3.2 Hareket

Yürüme 3,5 m/sn, koşu 6, depar 8,5 (saniyede 8 dayanıklılık). Depar bırakıldıktan 1 sn sonra dayanıklılık saniyede 15 dolar; sıfıra inerse depar, dayanıklılık 20'ye çıkana kadar kapanır. Zıplama 1,3 m. Merdiven ve rampa tırmanılır. Zıpkın başlığı kendini çeker. Planör, çift zıplama ve roket zıplama kuşam modülü ya da güçtür. Yuvarlanma yok; kaçınma Atılma gücüyle yapılır.

Gövde fiziklidir: araç ya da 150 kg'dan ağır bir nesne çarparsa oyuncu 5 saniyeliğine yere serilir (ragdoll). Bu hasar vermez.

### 3.3 Yemek

Açlık ve susuzluk yok. Üç yemek yuvası var. Her yemek 15–25 dakika boyunca Can, Dayanıklılık ve çoğu zaman özel bir etki verir. Aynı yemek iki yuvaya konamaz. Yemek bozulmaz. Liste: [ICERIK.md](ICERIK.md) §15.

### 3.4 Konfor ve dinlenme

Yatağın 10 m çevresindeki farklı mobilya ve süs türlerinin sayısı konfor puanıdır (1–20). Uyuyunca **Dinlenmiş** etkisi gelir: süre `8 + konfor` dakika, dayanıklılık yenilenmesi +%25, Ustalık kazanımı +%10.

### 3.5 Tehlikeler

| Tehlike | Nerede | Direnç kaynağı |
|---|---|---|
| Soğuk | Ayaz Yaylası, yüksek rakımda gece | Termal Astar, Acılı Ezme ve Kar Çayı, ısı kaynağı, kapalı alan |
| Sıcak | Kızıl Kanyon gündüzü | Serin Yelek, Ayran ve Karpuz Peynir, gölge, ıslak olmak |
| Zehirli gaz | Sazlık çukurları, bazı Oyuklar | Filtre Maskesi |
| Maya yanığı | Göbek | Maya Yalıtımı, Ekşi Maya Ekmeği |

Direnç yetmiyorsa dayanıklılık yenilenmez ve saniyede 1–3 can gider. Hiçbir tehlike anında öldürmez.

### 3.6 Bayılma, ölüm, doğma

- **Ortak oyun:** Can sıfırlanınca oyuncu 30 saniye baygın kalır. Bir arkadaşı şok tokadıyla (E basılı, 1,5 sn) %30 canla kaldırır. Süre dolarsa ölür.
- **Tek oyuncu:** Gecede bir kez "ikinci nefes": can sıfırlanınca %40 canla kalkılır.
- **Ölüm:** Çanta, ölünen yerde emanet kasası olarak kalır ve haritada işaretlenir; kaybolmaz. Çapa ve giyili kuşam oyuncuda kalır. Oyuncu yatağında, yatak yoksa Ocak'ta doğar. Ustalık kaybı yok.
- **Kabarma sırasında ölüm:** 10 sn + ölüm başına 5 sn sonra yatakta doğulur. Kartlar kalır; dolmakta olan seviye çubuğunun yarısı ve Gösteri çarpanı sıfırlanır.

## 4. Çapa

Oyuncunun ana aleti. Bütün işler ve silahlar başlık olarak takılır. Çapa kemerdeki bir eşyadır (başta 1. yuvada); 1–8 ile seçilir, başka yuva seçilince elden iner (sahibin kararı, 18 Kasım 2026; [URETIM.md](URETIM.md) §6 M1 kararı 23). Karakter Çapa'yı hep aynı pozda tutar; hedefe kol IK'sıyla döner.

- **Elde tutulan öbür eşyalar** (kemerden): yerleştirilebilir (hayalet görünür, sol tık kurar, R döndürür), İnşa Planı (yapı parçaları, aşağıda §7), yemek (sol tık yer), tohum, Meşale. Boş yuva seçilirse eller boştur; E ile etkileşim her durumda çalışır.

| Çapa | Gereken Ocak | Güç çarpanı | Silah yuvası | Başlık başına çip yuvası |
|---|---|---|---|---|
| Mk.I | T1 | 1,0 | 2 | 1 |
| Mk.II | T2 | 1,5 | 3 | 1 |
| Mk.III | T3 | 2,2 | 3 | 2 |
| Mk.IV | T4 | 3,2 | 4 | 2 |
| Mk.V | T5 | 4,5 | 4 | 3 |
| Mk.VI | T6 | 6,5 | 5 | 4 |

- **İş başlıkları** (5): Kazma, Biçme, Çekim, Toprak, Sulama. Hepsi iniş kapsülünden çıkar, yuva harcamaz ve iş çarkından seçilir. Çapa basamağı ve çipler onları güçlendirir. (İnşa başlığı kalktı; yerine İnşa Planı eşyası, §7.)
- **Silah başlıkları** (12): [ICERIK.md](ICERIK.md) §6. Aynı anda silah yuvası kadarı takılı durur; Alet Askısı'nda değiştirilir.
- Başlık değiştirme: yalnız çark (Q basılı; kol: sol tampon basılı). Sayı tuşları 1–8 kemeri seçer, başlık seçmez (sahibin kararı, 18 Kasım 2026; [SANAT-SES.md](SANAT-SES.md) §8.1).
- **Tarayıcı** başlık değildir; her zaman açılabilen bir moddur. İlk kez taranan her tür, bitki ve maden Veri verir ve Ansiklopedi'ye yazılır.
- **Cephane:** Çivi, saçma, havan mermisi, biyoyakıt ve tohum kullanan başlıklar gündüz cephane harcar. Kabarma sırasında Çapa aşırı yüklüdür ve cephane sınırsızdır; özel cephane takılıysa etkisi sürer.
- **Çipler:** Başlığa takılan kalıcı geliştirmeler. 24 tür, 4 nadirlik. Çipler ganimetten ve üretimden gelir; en üst nadirlik yalnızca Holding parçasıyla yapılır.

**Vuruş hissi ölçütleri** (hepsi F1 ayar panelinde): vuruş duraklaması 30–80 ms; kamera sarsıntısı; hasar sayıları; geri tepme yayı; vurulan hedefte ezilip esneme; üç katmanlı ses (atış, isabet, parça). Girişten ekrandaki ilk tepkiye kadar geçen süre en fazla 50 ms.

## 5. Envanter ve yük

- **Sırt çantası:** 20 yuva; Çapa basamağıyla 24, 30, 36, 42, 48. **Kemer:** 8 yuva; seçili yuvadaki eşya eldedir. Başlangıçta 1. yuvada Çapa, 2. yuvada İnşa Planı.
- **Üretim paneli (Rust tarzı):** Tab envanterinin yanında. Bütün tarifler listelenir; arama ve kategori var. El tarifleri her yerde, istasyon tarifleri yalnız o istasyonun yakınında üretilir (uzaktaysa satır "Tezgâh gerekir" der). Üretim süreli bir kuyruğa girer; kuyruk ekranın sağ altında görünür, iptal edilince malzeme geri döner. İstasyon menüsü (E) aynı paneli o istasyonun tarifleriyle açar.
- **Eksik malzeme:** Tarif satırında eksik malzeme kırmızıdır ve nereden geldiğini yazar (örnek: "Kil: göl kıyısındaki kil yatakları, Kazma"). "Pusulada göster" en yakın kaynağı pusulaya işaretler.
- **Yerleştirilebilirler** (Tezgâh, Taş Fırın, Ocakbaşı, Paketleme, Sandık, Uyku Tulumu, Meşale, Yemlik, Folluk, Ahşap Kapı, Kargo Rampası) üretilince envantere eşya olarak girer; kemerden seçilip kurulur. M1'de elde: Tezgâh 5 sn, Meşale 1 sn, Uyku Tulumu, Sandık, Yemlik, Folluk, Ahşap Kapı 3'er sn; Tezgâh'ta: Taş Fırın 6 sn, Ocakbaşı ve Paketleme Tezgâhı 5'er sn. Bedeller ICERIK §10.2 ve §20'deki gibi. Ocak kapsülden gelir (başlangıçta çantada, kemerden kurulur). Kargo Rampası üretilmez: ilk günden kapsül rampaya döner.
- Yığın: kaynak 50, ara ürün 20, yemek 10.
- **Yük nesneleri** çantaya girmez; dünyada fiziksel durur: kabarık ürün, kargo kasası, büyük leş, kuluçkalık yumurta (yuvadan alınan; Gıdak yumurtası sıradan eşyadır, çantaya girer), paketlenmiş makine ve araç, Ata ganimeti. Çekim başlığıyla, birkaç kişiyle, hayvanla, vinçle ya da araçla taşınır.
- **Ambardan üretim:** Tapu içinde Ocak'a bağlı bütün depolardaki malzeme tezgâhlarda doğrudan kullanılır. Kural (M2): istasyon tarifinin malzemesi ve yakıtı önce çantadan, eksik kalan tapu yarıçapındaki (§6) Sandıklardan alınır; ürün çantaya, çanta doluysa aynı Sandıklara girer, ikisi de doluysa iş bekler. El tarifleri yalnız çantadan. İptal edilen işin malzemesi aynı yolla geri döner.
- **İstasyon kuyruğu:** Her istasyonun kendi kuyruğu vardır (en çok 8 iş, farklı tarifler sıra bekler); oyuncu uzaklaşsa da işler.
- **Envanter düzeni:** Sandık menüsünde "Çantayı sırala" (aynı eşyanın yarım yığınlarını birleştirir, türe göre dizer) ve "Aynılarını koy" (Sandık'ta zaten bulunan eşyaların hepsini çantadan Sandık'a taşır, Rust'taki hızlı yığma).

## 6. Ocak ve tapu

Ocak üssün kalbidir. İşlevleri: tapu alanı, doğma noktası, Çan, Maya Kazanı, ambar bağlantısı, basamak yükseltme, Mühür rafı.

| Basamak | Açan | Tapu yarıçapı | Tapu direği | Karakol | KS tavanı | Hayvan sınırı |
|---|---|---|---|---|---|---|
| T1 | Başlangıç | 40 m | 3 | 1 | 5 | 8 |
| T2 | Çayır Çekirdeği | 60 m | 6 | 2 | 10 | 14 |
| T3 | Koru Çekirdeği | 80 m | 9 | 3 | 15 | 20 |
| T4 | Sazlık Çekirdeği | 110 m | 12 | 4 | 20 | 28 |
| T5 | Kanyon Çekirdeği | 140 m | 16 | 6 | 25 | 36 |
| T6 | Ayaz Çekirdeği | 180 m | sınırsız | 8 | sınırsız | 48 |

- **Tapu direği:** 25 m yarıçaplı ek alan; mevcut tapuya değmek zorundadır.
- **Karakol Ocağı:** Uzakta 30 m'lik mini tapu. Yatak, depo, mancınık ve teleferik konur. Kabarma karakolları etkilemez; o gece mühürlüdürler.
- **Taşıma:** Ocak sökülüp başka yere kurulabilir (1 oyun günü bekleme). Eski yapılar yerinde kalır.
- **Maya Kazanı:** Kabarma'da kazanılan Maya burada birikir. Düşmanların ikinci hedefidir.
- **Mühür rafı:** Her Ata kalıcı bir ekip bonusu (Mühür) verir.

## 7. Yapı

- **Izgara:** 3 m'lik kare; kat yüksekliği 3 m; üçgen parçalar var. İlk temel serbest döner (15° adım), sonrakiler yuvaya oturur.
- **Tapu dışı:** Kurma tapu dışında da serbesttir (köprü, gözetleme yeri, yol); söküm iadesi orada düşüktür (aşağıda).
- **Bedel:** Parça bedelleri [ICERIK.md](ICERIK.md) §10.1'de. Yapı parçaları (temel, duvar, kapı kasası, tavan, merdiven) **İnşa Planı** eşyasıyla kurulur: plan eldeyken sağ tık basılı parça çarkını açar, sol tık kurar, bedel çantadan düşer. Yerleştirilebilirler önce üretilir, sonra kemerden kurulur (§5).
- **Malzemeler:**

| Malzeme | Basamak | Parça canı | En fazla yatay açıklık |
|---|---|---|---|
| Ahşap | T1 | 400 | 3 parça |
| Kerpiç-Taş | T2 | 900 | 4 parça |
| Çelik Sac | T3 | 2.000 | 6 parça |
| Mayalı Beton | T6 | 5.000 | 8 parça |
| Cam (ışık geçirir) | T4 | 300 | 3 parça |

- **Taşıma dengesi:** Her parça en yakın temele olan destek mesafesini hesaplar. Açıklık aşılırsa parça yerleşmez. Desteği yıkılan parçalar fizik nesnesi olarak düşer ve altındakini yere serer.
  - **Açıklık:** Araziye oturan parça 0. Alttaki parçanın üstüne dikilen düşey parça (duvar, sütun) aynı açıklıkta kalır; yatay parça (zemin, çatı, kiriş) ve yandan ya da üstten asılan düşey parça +1. Açıklık parçanın kendi malzemesinin sınırını (tablo) aşamaz.
  - **Yük:** Her parçanın ağırlığı = şekil birimi (ICERIK §10.1 Odun adedi) × malzemenin birim ağırlığı. Parça kendi ağırlığını ve üstündekilerin payını taşır; pay, onu en kısa yoldan taşıyan parçalara eşit bölünür. Araziye oturan parça yükü toprağa verir (sınırsız). Yük malzemenin taşıma sınırını aşamaz.
  - Açıklığı ya da yükü aşacak parça yerleşmez ve ağırlığı alttakini aşacak yükseltme yapılmaz. Söküm ya da yükseltme sonrası desteği kalmayan ya da aşırı yüklenen parça çöker; üstündekiler zincirle düşer. Sayılar `data/build.json`'da.
  - **Hayalet rengi:** yeşilden sarıya parçanın zorlanması (açıklık ve yük oranının büyüğü); kırmızı yerleşmez.
- **Yerinde yükseltme:** İnşa Planı eldeyken parçaya bakıp E basılı tutulur (yükselt, onar, sök çarkı). Yükselt bir üst malzemeye geçer (Ahşap → Kerpiç-Taş → Çelik Sac → Mayalı Beton; geri yok). "Aradaki fark" şöyle ödenir: yeni malzemenin bedeli çantadan düşer, eski malzemenin söküm iadesi (aşağıdaki oranla) çantaya döner. Can, yeni malzemenin canına hasar oranı korunarak geçer. Malzemeye basamak kilidi yok: kilit malzemenin kendisidir (Kerpiç Taş Fırın'da, Çelik Yüksek Fırın'da).
- **Onarım:** Hasar oranında malzemenin %25'i (parçanın şu anki malzemesiyle). **Söküm:** tapu içinde %100, dışında %50 iade (M2'den itibaren kurma tapu dışında da açık). Söküm yüzünden çöken parçaların iadesi de aynı kuralla döner. M1: sökülen yerleştirilebilir (Tezgâh, Sandık vb.) eşyanın kendisi olarak çantaya döner; tapu dışı %50 kuralı yerleştirilebilirde M2'de (sahibin onayını bekliyor).
- **Boyama:** 16 renk, parça başına.
- **Kapalı alan:** Üstü ve çevresi kapalı hacim ısıyı, konforu ve yağmuru etkiler.
- **Plan modu:** Parçalar hayalet olarak bırakılır; ekip üyeleri malzemeyi getirince tamamlanır. İnşa Planı eldeyken G plan modunu açar ve kapatır. Plan kipinde sol tık bedel ödemeden mavi bir plan hayaleti bırakır (açıklık, plan parçaları kurulmuş sayılarak denetlenir). Plan hayaletine bakıp sol tık çantadaki uygun malzemeyi koyar; bedel dolunca parça kurulur. Altı henüz kurulmamış plan "önce altını bitir" der ve malzemeyi bekletir. Plan hayaletini Sök çarkı iz bırakmadan kaldırır, konan malzeme geri döner. Plan hayaleti çarpışmaz (oyuncu ve yaratık geçer).
- **Plan Defteri:** Kurulu bir bölge plan olarak kaydedilir ve başka yere hayalet olarak konur. 12 yuva. İnşa Planı eldeyken B defteri açar. "Kaydet" bakılan parçanın bağlı yapısını (kurulu ve plan, en fazla 200 parça) seçili yuvaya yazar. Bir yuva seçilince yapının tamamı hayalet olarak nişanı izler, R döndürür, sol tık plan olarak bırakır. Yerine oturamayan parça atlanır ve sayısı yazılır. Kayıt şekli, malzemeyi, ilk temele göre yeri ve yönü tutar.
- **Dev ürün oyma:** L ve XL kabarık Kabak, Karpuz ve Mantar, yanına kurulan Oyma İskelesi ile kalıcı yapıya dönüşür (Kabak Ev, Karpuz Havuz, Mantar Kule). Konfor puanı verir.

## 8. Tarım

### 8.1 Tarla

- Toprak başlığı 1 × 1 m'lik kareleri sürer (eğim 15°'den az olmalı).
- Her karenin iki değeri var: **Bereket** (0–100) ve **Nem** (0–100).
- Evreler: Tohum → Filiz → Genç → Olgun. Kabarma yalnızca Olgun ekini kabartır.

### 8.2 Büyüme

`hız = taban × f(Nem) × f(Bereket) × f(Işık) × f(Sıcaklık) × (1 + 0,04 × H)`

| Etken | Kural |
|---|---|
| Nem | 20'nin altı: durur. 20–40: ×0,5. 40–80: ×1. 80 üstü: ×0,8 (su seven ekinlerde ×1). |
| Bereket | `0,5 + Bereket / 200` |
| Işık | Açık hava ya da büyütme lambası ×1; karanlık ×0. Mantarlar tersidir. |
| Sıcaklık | Ekinin biyom bandında ×1, dışında ×0,25 (yavaş ama büyür). Altın Çayır ve Sis Korusu aynı banttır. Sera bandı düzeltir. |

- **Hasat verimi:** `taban × (1 + 0,08 × V) × kalite`. Kalite ★1–3; Bereket 70 ve üstü, Nem hiç 20'nin altına düşmediyse ★3.
- **Bereket:** Her hasat −8 (kök ekinleri −10, Katran Fasulyesi +5). Kompost +30, gübre +20, kimyasal gübre +50. Boş kare günde +3. Bir karede art arda farklı aile ekmek +%10 verim.
- **Su:** Hortum, yağmur, yağmurlama (4 m yarıçap, boru ve su kaynağı ister), damla hattı.
- **Gündüz zararlıları:** Zıpzıp, Fındıkçı ve küçük Çekirge grupları olgun ekine gelir. Korkuluk (10 m), çit, bekçi hayvan ya da kule önler.
  - Zararlı ekini bir lokmada yemez. Her ısırık Zarar'ı `0,10 × (1 − 0,05 × D)` artırır. Zarar 1'e varınca ekin yenmiş olur. Hasat verimi `× (1 − Zarar)` olur.
  - Sarımsak, kendi karesine ve 8 komşu kareye zararlı getirmez.
- **Özel ekinler** ([ICERIK.md](ICERIK.md) §5):
  - Mantar yalnız karanlıkta büyür (gece ya da çatı altı).
  - Böğürtlen ve Fındık kalıcı çalıdır. Hasattan sonra Genç evreye döner ve "sonra günde bir / iki günde bir" kuralıyla yeniden olgunlaşır.
  - Buğdayın ürünü `wheat`, Lifotu'nun ürünü `fiber` kaynağıdır.
- **Gün birimi:** Ekin süreleri (olgunlaşma, kalıcı çalının yenilenmesi, boş karenin +3 Bereketi, yabani bitkinin yeniden bitmesi) **büyüme günü** ile sayılır. Büyüme günü `crops.json growDaySeconds` değeridir (şimdilik 37,5 sn; Şalgam'ın 2 günü 75 sn). Kabarık ürünün sönmesi takvim gününe bağlıdır (§2).
- **Yabani bitki:** Tapu dışında kendiliğinden biten olgun ekindir. Biçmeyle ürün ve yabani tohum verir. Kabarmaz. Hasattan 2 büyüme günü sonra yeniden biter.

### 8.3 Genler ve ıslah

Her tohumun dört geni var (1–10): **V** verim, **H** hız, **D** dayanıklılık (nem ve sıcaklık toleransı, zararlı hasarı), **M** Maya eğilimi (kabarma boyu). Yabani tohum 1–3 ile gelir.

- **Çaprazlama:** Aynı aileden iki bitki en fazla 2 m arayla ekiliyse ve 12 m içinde Vızvız kovanı varsa, hasat edilen tohumun her geni ebeveyn ortalamasıdır, üstüne mutasyon: %70 değişmez, %20 +1, %8 −1, %2 +2.
- **Aşı Tezgâhı (T5):** Bir tohumdaki seçilen tek geni başka tohuma aktarır; kaynak tohum harcanır.
- **Alaca:** Her hasatta Benekli 1/40, Altın 1/250, Aurora 1/2.000. Alaca tohum %50 olasılıkla Alaca verir. Satışta ×3, ×10, ×50; kabarık hâlinin kendine özgü görünüşü olur.
  - Zar ekimde atılır. Böylece bitki büyürken ve kabarıkken Alaca olduğu görünür; olasılık her yetişen bitki için aynıdır.
  - Alaca hasadın ürünü ayrı eşyadır (`<ekin>_benekli`, `_altin`, `_aurora`). Tohumları Alaca tohumdur.
- **Kalıtım (çaprazlama yokken):** Hasat edilen her tohum, bitkinin genlerini alır ve üstüne aynı mutasyon zarı atılır.
- **Yuvarlama ve sınır:** Ebeveyn ortalaması buçukluysa (ör. 2,5) yarı yarıya aşağı ya da yukarı yuvarlanır. Mutasyondan sonra gen 1–10 arasında kalır.
- **D'nin etkisi:**
  - Nem eşikleri (20 ve 40) `0,5 × (D − 1)` aşağı iner. Kalite yıldızı için 20 eşiği değişmez.
  - Bant dışı hız `0,25 + 0,03 × (D − 1)` olur.
  - Zararlı ısırığı yukarıdaki gibidir.
- **Tohum soyu:** Çantada tohum yalnız adet ve kimlikle durur. Genleri ve Alaca'sı tapunun **Tohum Kaydı**'nda tutulur.
  - Ekerken o türün en iyi kaydı kullanılır. Alaca kayıt önce gelir, sonra gen toplamı en yüksek olan.
  - Kaydı olmayan tohum (kapsülden, satın alınmış ya da başka yoldan gelen) ekinin taban genleriyle ekilir.
  - Tohumu olmayan kayıt bekler; ilk tohum geldiğinde kullanılır.
- **Islah notları:** Defter'in Islah sayfası her türün en iyi genlerini, son mutasyonları ve Alaca buluşlarını yazar.

### 8.4 Kabarma'da tarla

- Tapu içindeki Olgun ekinler gecenin ilk iki dakikasında kabarır.
- **Boy puanı** = `M + KS / 5 + kart bonusu`. 6'nın altı S, 6–9 M, 10–13 L, 14 ve üstü XL.
- Kabarık ürün yük nesnesidir: S 40 kg, M 150 kg, L 600 kg, XL 2.500 kg. Satış değeri normalin 8, 12, 16, 24 katıdır.
- Şafaktan sonra 1 gün içinde toplanmayan kabarık ürün söner ve iki kat normal ürüne döner.
- **Ne ekersen onu çekersin:** Her ekin ailesi belirli yaratık ailelerini çeker ([ICERIK.md](ICERIK.md) §5). Dalgaların yarısı buna göre kurulur (§14).

### 8.5 Sera

Cam parçalarla kapatılmış hacme Sera Kontrol Ünitesi konunca içerideki sıcaklık bandı seçilir. Böylece her biyomun ekini her yerde yetişir. Ünite güç ister.

## 9. Hayvancılık

### 9.1 Evcilleştirme

- **Otçullar ve küçükler:** Sevdiği yem atılır. Her yiyişte güven çubuğu dolar; koşmak ve ateş etmek çubuğu sıfırlar. Dolunca oyuncuyu izler; ağıla girince kayıt olur.
- **Ağıl:** Çit, bahçe kapısı, duvar ya da kapı parçalarından kapalı bir halka; en az 1 hücre (9 m²), çatı gerekmez. Kapılar E ile açılıp kapanır; açık kapı geçilir. Halkanın içindeki hayvan kayıt olur. Tür başına gereken alan veridedir (Gıdak 4 m²).
- **Yırtıcılar ve büyükler:** Önce canı %25'in altına indirilip Çekim ya da Zıpkın ile tutulur, sonra beslenir.
- **Yumurtadan:** Hu, Koşar, Paytak ve Kızgın yuvadan alınan yumurtayla Kuluçka'da çıkar. Yavru evcil doğar.
- **Kovan:** Vızvız ağıla girmez. Sevdiği yemle güvenen sürü bir Arı Kovanı'na yerleşir ve orada kayıt olur.
- Tür başına yol, yem sayısı ve alan `husbandry.json`'da. Homur, Dallı ve Vıcık otçul yolundan evcilleşir; Bozuk yırtıcı yolundan.
- **Kaçma:** Mutluluğu eşiğin altında kalan hayvan, bir süre sonra açık kapıdan ağılı terk eder. Kaydı düşer, güveni yarıya iner; yeniden yemle kazanılır. Eşik ve süre veride.

### 9.2 İhtiyaçlar ve mutluluk

Yemlik (türe göre yem), suluk, çatı, tür başına alan. Mutluluk (0–100) karşılanan ihtiyaçlardan, okşamaktan ve gübreyi toplamaktan gelir. Mutluluk ürün kalitesini ve üremeyi belirler.

- Mutluluk bir hedefe doğru günde belli bir hızla kayar. Hedef, karşılanan ihtiyaçların ağırlıkları toplamıdır.
- Okşama günde bir kez ek puan verir. Ağılda biriken her toplanmamış gübre puan düşürür.
- Ürün yalnız Yemlik'te türün yemi varken çıkar; ihtiyaçlar ürünü durdurmaz. Mutluluk miktarı (kalite çarpanı) ve sıklığı değiştirir, U geni de sıklığı.
- Ağırlıklar, aralıklar ve eşikler `husbandry.json`'da.

### 9.3 Genler ve üreme

Dört gen (1–10): **B** boyut (can, yük), **U** uysallık (komut dinleme, ürün sıklığı), **V** ürün verimi, **K** kabarma gücü (kabarık hâlin canı ve hasarı). Kalıtım ve Alaca kuralları tarımdakiyle aynı. İki yetişkin, 60 ve üstü mutluluk ve boş yer varsa 2–4 günde yavru gelir.

- **Genin hesabı:** Tarımdaki çaprazlamayla aynıdır (§8.3). Her gen ebeveyn ortalamasıdır; buçuk yarı yarıya aşağı ya da yukarı yuvarlanır. Üstüne mutasyon gelir, sonuç 1–10'a kırpılır.
- **Alaca:** Yavru doğarken ve Kuluçka'dan çıkarken §8.3'teki zar atılır. Alaca ebeveyn Alaca'sını aynı olasılıkla aktarır.
- **Yavru:** Evcil ve kayıtlı doğar. Yetişkin olana kadar (gün sayısı veride) ürün vermez, üremez.
- **Yer:** Ağıl alanı, içindeki hayvanların tür alanları toplamıdır; yavru da yer kaplar.

### 9.4 Roller

Her hayvana bir rol atanır:

| Rol | Ne yapar |
|---|---|
| Üretici | Ahırda ürün verir |
| Bekçi | Tapuda devriye gezer, gündüz zararlılarını kovalar |
| Binek | Sürülür |
| Yük | Heybe taşır (8–24 yuva) ya da araba çeker |
| İşçi | Türe özgü iş yapar ([ICERIK.md](ICERIK.md) §3): cevher eşeler, kaya kırar, kompost yapar, elektrik üretir, fırın olur, harita açar |
| Savaşçı | Savaş Ağılı'nda bekler; Kabarma'da kabarıp savaşır |

Komutlar: izle, bekle, işaretlediğime saldır, dön. Uysallığı düşük hayvan bazen dinlemez.

- Türün alabileceği roller `husbandry.json`'da ([ICERIK.md](ICERIK.md) §3'ten).
- **M2 ekleri** (hayvan akışının kararı, ICERIK §3'te yazmıyor):
  - Homur, Dallı, Hu ve Bozuk'a Üretici rolü de verildi, çünkü ICERIK §2.2'deki Deri, Tüy ve Kürk onlardan gelir.
  - Hayvan kalıcı ölmediği için (§9.5) Deri ve Kürk av yerine uzun aralıklı ürün olarak gelir.
  - Bozuk'a Bekçi rolü de verildi.
  - Vızvız kovanda Yemlik'siz ürün verir; çiçeği kendisi bulur.
- Dinleme olasılığı U geniyle artar.
- B geni can, heybe ve binek hızına eklenir.

### 9.5 Kabarma'da hayvanlar

- Savaş Ağılı'ndakiler kabarır ve savaşır. Canı biten bayılır, sabah ahırda uyanır ve bir gün yorgundur.
- Ahırdakiler saklanır. Ahır yıkılırsa kaçışırlar; sabah mutlulukları yarıya iner.
- Hayvan kalıcı olarak ölmez (Sert Ay ayarında ölür).

## 10. Ustalık

Sekiz dal: **Kazı, Hasat, Çiftçilik, Hayvancılık, Mühendislik, Aşçılık, Savaş, Sürüş.** Her dal 1–50.

- Yaparak gelişir. `n. seviye eşiği = 100 × n^1,6`.
- Her seviye ilgili işe +%1 hız ya da verim.
- 10, 20, 30, 40 ve 50. seviyelerde iki özellikten biri seçilir. Toplam 80 özellik: [ICERIK.md](ICERIK.md) §19.
- **Eşik toplam XP'dir:** 1. seviye 100 XP, 10. seviye ~3.981, 50. seviye ~52.300 (`100 × n^1,6`, aşağı yuvarlanır). XP kaynakları ve miktarları `mastery.json`.
- Seçimler kredi karşılığı sıfırlanır: dalın seçimleri `1.000 × (dalın seviyesi / 10)` Kredi; XP ve seviye kalır. Defter'in Ustalık sekmesinde yapılır.
- Ustalık karaktere aittir ve dünyalar arasında taşınır.

## 11. Mühendislik

### 11.1 Güç

- Birimler: watt (W) ve watt-saat (Wh).
- Üreticiler, aküler ve tüketiciler kabloyla aynı ağa bağlanır. Kablo iki soket arasına elle çekilir (en fazla 12 m). Direkte 4 soket var.
- Her tikte ağ hesabı: üretim talebi karşılamazsa akü devreye girer; o da yetmezse tüketiciler öncelik sırasına göre (1–5, oyuncu ayarlar) kapanır.
- Şalter ağı böler. Parçalar: [ICERIK.md](ICERIK.md) §12.

### 11.2 Sinyal

Ayrı, ince kabloyla taşınan 0–100 arası sayı.

| Tür | Parçalar |
|---|---|
| Kaynak | Düğme, şalter, basınç plakası, hareket sensörü (oyuncu, hayvan, düşman süzgeci), gün-gece sensörü, Kabarma sensörü (aşama ve dalga), depo sensörü, güç sensörü, nem sensörü, sayaç, zamanlayıcı |
| Kapı | VE, VEYA, DEĞİL, karşılaştırıcı, bellek |
| Alıcı | Kapı, sur kapısı, kule (aç, kapat, hedef seçimi), yağmurlama, lamba, siren, bant, kol, tuzak, fırlatma pedi, Kabartma Odası |

### 11.3 Taşıma

Bant (eğim en fazla 30°, saniyede 2 nesne), ayırıcı, birleştirici, kol, boru ve pompa (su, yakıt, süt), depolar, Akıllı Depo (istek listesi), Drone İstasyonu, teleferik (karakollar arası yük hattı).

### 11.4 Otomasyon

Hasat Robotu, Ekim Robotu, otomatik yemlik, Sağım-Kırkım Kolu, Yumurta Toplayıcı, Kompostör, Montaj Makinesi (seçilen tarifi kendi üretir), Paketleyici, otomatik Kargo Rampası, Sondaj Kulesi (Zengin Yatak üstünde). Son halka: **Kabartma Odası**, Maya harcayarak tek bir tarla karesini ya da hayvanı gündüz kabartır.

## 12. İçerik nasıl açılır

| Kanal | Ne açar |
|---|---|
| Ocak basamağı | İstasyonlar, Çapa basamağı, KS tavanı |
| Keşif | Yeni bir malzeme ilk alındığında onu kullanan temel tarifler |
| Araştırma | Tarama, iş emirleri ve enkaz veri küplerinden gelen **Veri** ile Araştırma Masası'nda makine, başlık, parça ve araç tarifleri. Her kalemin Veri bedeli ICERIK'te yazıyor. |
| Katalog | **Kredi** ile tohum, yumurta, araç gövdesi, süs, kozmetik |
| Ganimet | Çipler, kart jetonları, güçler (Kadim Taş), Holding planları |

**Araştırma kuralı (M2):** Araştırma Araştırma Masası'nın yanında yapılır: Üretim panelinde araştırmayla kilitli satırın düğmesi "ARAŞTIR · n VERİ" olur. Bedel kalemin basamağına göre sabittir ([ICERIK.md](ICERIK.md) §0). Ağaçta önceki düğüm gerekir ([ICERIK.md](ICERIK.md) §11.1); Ocak basamağı gerekmez, ilerlemeyi malzeme (ör. Koru'nun demiri) ve Veri sınırlar. Açılma sütunu "Ata" olan istasyon Ocak o basamağa çıkınca açılır. **Tarayıcı** (§4): bakılan yaratık, bitki ya da maden türü ilk kez taranınca Veri verir (yaratık 15, bitki 8, maden 6, ağaç 5); Ansiklopedi'ye yazılır.

## 13. Ekonomi

- **Kredi kaynakları:** kargo satışı, iş emri ödülü, Holding silosu, enkaz kasaları.
- **Kargo:** Ürünler Paketleme Tezgâhı'nda kargo kasasına konur (20 yığın). Kasa yük nesnesidir (25 kg); değeri içindekilerin satış değerinin toplamıdır. Kargo Rampası 1–6 kasa ya da bir kabarık ürün alır ve fırlatır; ödeme 60 saniye sonra gelir.
- **Fiyat:** `taban × kalite (1,0 / 1,3 / 1,7) × Alaca (3 / 10 / 50) × Borsa × doygunluk`. Taban fiyat, katalog fiyatı ve ödül kuralları: [ICERIK.md](ICERIK.md) §18.
- **Borsa:** 12 kategori. Her kategorinin çarpanı günde ±0,15 oynar (artı 1,0'a doğru %10 çekim), 0,6 ile 1,6 arasında kalır. Her gün bir kategori "sıcak"tır (×2); ×2 bu sınırın üstüne biner, yani Borsa çarpanı 0,6 ile 3,2 arasındadır. Borsa Kargo Rampası'nın terminalindedir; her kategorinin son 30 günlük çarpanı görünür.
- **Borsanın açılışı:** İlk Kabarma'nın şafağına kadar bütün çarpanlar 1,0, sıcak kategori yok, doygunluk tam; öğretici satışlar (ilk kabarık şalgam 120 Kredi) nötr fiyattandır. Fiyatlar takvimin ilk gün değişiminde (ilk şafaktan sonraki sabah) oynamaya başlar.
- **Doygunluk:** Aynı kategoriden satılan her kasa o kategoriyi %3 düşürür (taban %40); her sabah 0,15 eklenerek geri gelir (en fazla 1,0). Karışık kasa içindeki her kategoriyi bir kez düşürür; kabarık ürün kendi kategorisini bir kez düşürür. Tek ürüne yüklenmek cezalanır.
- **Fiyat kilidi:** Kasanın ve kabarık ürünün değeri fırlatma anındaki fiyatla belirlenir; 60 saniyelik ödeme gecikmesinde fiyat değişmez.
- **Katalog:** Kargo Rampası terminalinden Krediyle alınır. Küçük kalemler (tohum, yumurta, süs) gökten kapsülle Ocak'ın yanına iner (20 sn); araç gövdesi Kargo Rampası'na iner (40 sn). Kapsül E ile açılır, içi çantaya geçer; sığmayan kapsülde kalır.
- **Tohum Çarkı:** Bir çevirme 500 Kredi ya da 3 Alaca ürün; günün ilk çevirmesi bedava (takvim günü başına bir). Ödüller: nadir tohum, yumurta, çip, kart jetonu, kozmetik, kredi. Beklenen değer harcananın %85'i (ödüllerin Kredi karşılığı ICERIK §18). Sistemi henüz olmayan ödüller (çip, kart jetonu, kozmetik, yumurta, Alaca tohum) Kooperatif deposunda sayılır ve sistemleri gelince oradan alınır.
- **Cüzdan ekipçe ortaktır.**
- **İş emirleri:** 48 zincir, her biri 5 adım. Suna okur, Defter'de durur.

## 14. Kabarma

### 14.1 Akış

| Aşama | Süre | Ne olur |
|---|---|---|
| Alaca | 2 dk | Siren, gökyüzü dalgalanır, ekinler ışımaya başlar. Hazırlık anı. |
| Dalgalar 1–8 | Her biri 75 sn + 15 sn ara | Ekinler ilk iki dakikada kabarır. Dalgalar tapu sınırından girer. |
| Doruk | 2 dk | Her 5. KS'de Azman; diğerlerinde elit dalga |
| Şafak | — | Kalan düşmanlar söner ve kaçar. Hasat Raporu açılır. |

### 14.2 Kabaranlar

Tapudaki olgun ekinler; Savaş Ağılı'ndaki hayvanlar; çevredeki yabani yaratıklar (dalga olarak gelirler); oyuncuların Çapa'sı (kartlar açılır, cephane sınırsız olur); Holding kampları (kalkanları iner, Husumet yüksekse baskın ekibi çıkar).

### 14.3 Dalga kuralları

- **Sayı:** dalga başına toplam düşman `(30 + 8 × KS) × oyuncu çarpanı`. Oyuncu çarpanı `1 + 0,6 × (n − 1)^0,85`. Aynı anda sahnede en fazla 220 düşman bulunur; fazlası sırayla girer.
- **Güç:** can `1,15^(KS−1)`, hasar `1,08^(KS−1)`.
- **Elit oranı:** `min(%40, %2 × KS)`. Elit önekleri: Zırhlı, Hızlı, Bölünen, Patlayan, Şifacı, Mıknatıs, Dev.
- **Bileşim:** %50 ekili ailelerin çektiği türler (ekili alan oranında), %30 biyomun yerlileri, %20 komşu biyom. Tapuda hiç olgun ekin yoksa tamamı yerli gelir, sayı ×0,6 olur ve Maya kazancı yarıya iner.
- **Hedef önceliği:** kendi ailesinin ekini → Maya Kazanı → kendisine saldıran. Yoldaki yapıyı kırarlar.
- **Sürüler** (Çekirge, Sülük, Hamurcuk öbekleri) tek varlık sayılır ve tek can havuzu taşır.

### 14.4 Kazanç ve kayıp

- **Maya** = öldürülen her düşmanın Maya değeri × `(1 + 0,15 × KS)` × Gösteri çarpanı. Kazan'da birikir.
- **Kazan devrilirse** Kabarma o an biter ve o gecenin Maya'sının yarısı gider. Ekinler kabarık kalır. Kazan sabah kendini onarır.
- **Bozgun:** Bütün oyuncular aynı anda yerdeyse Kazan %25 hasar alır, herkes yatağında doğar.
- **Yenen ekin** kaybedilir. Yıkılan yapı onarılır.
- **İlk geçiş ödülü:** Bir KS ilk kez geçildiğinde bir üst KS açılır; ekip 1 kart jetonu ve kredi primi alır.

### 14.5 Gösteri çarpanı

×1,0 ile ×5,0 arası; ekranın üst ortasında durur. Artıranlar: 3 saniyede 5 öldürme, havadaki düşmanı öldürme, çevreyle öldürme (fırlatma pedi, araç, biçerdöver, düşen yapı), hayvanının öldürmesi, kıl payı kurtulma, hasar almadan seri. Zamanla düşer; ölünce sıfırlanır.

### 14.6 Küçük Kabarma

Maya Feneri (T2, günde bir kez kullanılır) 40 m yarıçapta 4 dakikalık bir Kabarma başlatır. Yarıçaptaki olgun ekinler ve evcil hayvanlar kabarır; KS seçilenin yarısıdır, en fazla 3 kart seviyesi verir. Ana tapunun dışında, karakollarda da çalışır. Kullanımı: ıslah denemesi, hayvan eğitimi ve başka bir biyomun ekinini kendi yerinde, tam hızda yetiştirip orada kabartmak (Ata adakları için önemli, [ICERIK.md](ICERIK.md) §20).

### 14.7 Tatbikat

Ocak'tan başlatılır. Seçilen KS'nin dalgaları hayalet olarak gelir; hasar gerçektir ama onarım bedavadır ve ödül yoktur. Savunma düzenini sınamak için.

### 14.8 Tüzük maddeleri (KS 26 ve üstü)

26'dan sonra her seviye 12 maddelik havuzdan birini kalıcı olarak ekler ([ICERIK.md](ICERIK.md) §8.4). 37'den sonra maddeler ikinci kez eklenip şiddetlenir.

## 15. Kabarma kartları

- **Gece seviyesi:** Öldürmeler Öz verir (türün Öz değeri × Gösteri). `n. seviye eşiği = 20 × n^1,5`. Tipik bir gecede 18–26 seviye atlanır.
- **Seçim:** Her seviyede açık havuzdan 3 kart gelir. Oyun durmaz; kartlar ekranın altında belirir ve tek tuşla seçilir. 20 saniyede seçilmezse ilki alınır. Tek oyuncuda seçim sırasında zaman %30'a yavaşlar.
- **Nadirlik ağırlıkları:** Sıradan 60, Nadir 28, Destansı 10, Efsane 2.
- **Rütbe:** Aynı kart 5 kez alınabilir. En fazla 12 farklı kart taşınır.
- **Aşı:** İki belirli kart 5. rütbeye gelince birleşik aşı kartı sunulur; iki yuvayı tek yuvaya indirir.
- **Yenileme ve sürgün:** Gecede 2 yenileme, 2 sürgün.
- **Ekip kartları** (Çiftlik sınıfı) bütün ekibi etkiler; aynı kart farklı oyunculardan yığılmaz, en yüksek rütbe geçerlidir.
- **Ortak oyunda** her oyuncu kendi Öz'ünü toplar, kendi seviyesini atlar ve kendi üç kartından seçer; seçim sırasında zaman yavaşlamaz.
- Şafakta bütün kartlar söner.
- **Nişan:** Bir kartı geceyi 5. rütbede bitirmek 1 Nişan puanı verir. 3, 8, 15, 25, 40 puanda Nişan 1–5 olur: her seviye kartın etkisini %4 artırır; Nişan 5'te kart geceye 2. rütbeden başlar.
- **Açılma:** 20 kart baştan açıktır. 40 kart iş emri zincirleri, üye tipleri, Atalar ve kart jetonlarıyla açılır ([ICERIK.md](ICERIK.md) §8.3). 12 aşı kartı, iki ebeveyni açık olunca kendiliğinden açılır.

## 16. Savaş

### 16.1 Hasar ve durumlar

Hasar türleri: Darbe, Kesme, Ateş, Buz, Şok, Zehir, Maya. Her yaratığın zayıflığı ve direnci var.

| Durum | Etki |
|---|---|
| Yanık | 3 sn boyunca saniyede hasarın %20'si; yakındakine sıçrar |
| Donma | %40 yavaşlatır; yığılınca 2 sn kilitler |
| Şok | Yakındaki hedeflere sıçrar, kısa sersemletir |
| Zehir | Uzun süre az hasar; iyileşmeyi keser |
| Kabarık | Maya hasarı hedefi şişirir; %200'e varınca patlar ve alan hasarı verir |
| İtme | Kütleye göre savurur |

- **Zırh:** `azaltma = zırh / (zırh + 100)`.
- **Zayıf nokta:** Kabarmış yaratıklarda parlayan maya kesesi; vuruş ×2.
- **Dost ateşi** kapalıdır (dünya ayarı). İtme, fırlatma ve ezilme her zaman açıktır.

### 16.2 Düşman davranışları

Dokuz arketip: Koşucu, Tank, Sıçrayan, Menzilli, Uçan, Sürü, Kazıcı, Hırsız (ürünü kapıp kaçar), Yıkıcı (yapıya öncelik verir). Her tür bir ya da iki arketip taşır.

**Gündüz tavrı:** Kabarmamış yabani yaratık arketipini gündüz bir tavırla taşır: ürkek (yaklaşınca kaçar), sakin (otlar, vurulunca kaçar), yağmacı (ekine ya da tohuma gider, kovulunca kaçar), yırtıcı (yakına geleni uyarır, sonra saldırır), gececi (gündüz uyur), sürü (tek varlık, hedefe akar). Tür başına tavır ve eşikler [ICERIK.md](ICERIK.md) §3.8'de ve `data/creatures.json` `wild` alanında. Gündüz saldırısı M2'de uyarı ve küçük hasardır; tam savaş M3.

### 16.3 Gündüz savaşı

Yabani yaratıklar, açık arazideki Holding ekipleri ve kervanları, Oyuk koruyucuları ve Atalar. Kart yoktur; güç kalıcı donanımdan gelir: Çapa basamağı, başlıklar, çipler, zırh, güçler, yemek, hayvan.

### 16.4 Savunma yapıları

8 kule ve 8 tuzak ([ICERIK.md](ICERIK.md) §13). Kuleler güç ya da cephane ister; sinyalle açılıp kapanır ve hedef önceliği seçilir (en yakın, en güçlü, uçan, hırsız).

**M3 kuralları (T1–T2: Çivi, Sapan, Saçma; Diken Tarlası, Yapışkan Zemin, Fırlatma Pedi, Çukur Kapağı).** Sayılar `data/defense.json`'da.
- **Kurulum:** Kule ve tuzak Tezgâh'ta üretilen bir eşyadır; kemerden seçilip hayaletle kurulur, İnşa Planı'nın Sök kipiyle sökülür (eşya ve içindeki cephane geri döner). Tapu dışında da kurulur.
- **Cephane:** Kulenin bir deposu var. Kuleye bakıp {act:interact} basınca çantadaki uygun cephane depo dolana dek aktarılır. Atış başına bir cephane gider; depo boşalınca kule durur. Sapan Taş ya da ürün atar; kabarık ürünün özelliği M4'te.
- **Hedef:** Kule menzilindeki canlı düşmanlardan önceliğe göre seçer: en yakın, en güçlü (canı en yüksek), uçan, hırsız. Aradığı tür yoksa en yakına döner. {act:interact} basılı tutulunca öncelik sırayla değişir; sıranın sonu "kapalı"dır (sinyal bağlantısı M4'te güç ağıyla). Çivi tek hedefe hızlı ve öne nişanlı atar, Sapan kavisle alana düşürür, Saçma yakın bir koniye saçar.
- **Bakım:** Her atış ve her tetiklenme aşınma yazar. Can sıfırlanınca yapı durur ve "bakım ister" der. Can yarının altındaysa {act:interact} önce onarır (tuzakta her zaman onarır; Çukur Kapağı'nda yalnız bozuksa): bedel tarifin %25'i (yapı onarımıyla aynı oran), can tamamlanır. Gece dalgasının yapılara saldırısı kabarma3 akışında.
- **Tuzaklar:** Diken Tarlası üstündekine saniyede hasar verir ve yavaşlatır. Yapışkan Zemin girene yapışır, birkaç saniye tutar; aynı hedef kısa süre yeniden tutulmaz. Fırlatma Pedi basanı (oyuncu dahil) bakış yönünde fırlatır, sonra kurulması için bekler. Çukur Kapağı {act:interact} ile açılır ve kapanır (sinyal M4); açıkken üstüne gelen düşman çukura düşer, çıkamaz ve kapak kapanana dek tutulur. Çukurun sığası vardır.

## 17. Güçler

- İki etkin güç yuvası ve bir ulti yuvası. İkinci etkin yuva Kurbağa Ağa'nın ödülüdür.
- Etkin güçler Şarj harcar (25–50) ve 8–30 saniye bekleme süresi vardır.
- **Ulti** Maya ölçeriyle çalışır. Ölçer yalnızca Kabarma'da ve Ata dövüşünde dolar.
- 12 etkin güç Kadim Taş denemelerinden, 4 ulti Atalardan gelir ([ICERIK.md](ICERIK.md) §7).

## 18. Atalar

- Her biyomda bir **Ata Taşı** var. Adağın bileşenleri taşın önündeki sunağa tek tek konur; tamamlanınca Ata gelir. Kabarık ürün yük nesnesi olduğu için sunağa taşımak işin parçasıdır.
- Adaklar bilerek başka sistemlerden geçer: kabarık ürün (tarım ve Kabarma), bal (hayvancılık), güç isteyen makine (mühendislik).
- Dövüş açık arenadadır, üç aşamalıdır. Ata'nın canı ekip boyuna göre `1 + 0,5 × (n − 1)` ile çarpılır.
- **Ödül:** Çekirdek (Ocak basamağı), Mühür (kalıcı ekip bonusu), bir ulti ya da özel ödül, ganimet.
- **Kabarık Ata:** Yenilen Ata aynı adak ve `KS × 50` Maya ile yeniden çağrılır; gücü seçilen KS'ye göre ölçeklenir, üst basamak çip malzemesi verir.
- Ayrıntılar: [ICERIK.md](ICERIK.md) §4.

### 18.1 M2 uygulaması (Koca Pofuduk, Boynuzlu Dede)

Sayılar `data/atalar.json`'da; kural `src/Kabarma.Sim/Ata*.cs`, Godot katmanı `game/scripts/boss/`.

- **Bulunma ve çağırma:** Arena, biyomun Ata Taşı keşif noktasındadır (`biomes.json` `pois` ve `spawns.atalar`). Nokta henüz arazinin dışındaysa `atalar.json` yedek yeri kullanılır; Sis Korusu'nun noktası bu durumdadır. Ata Taşı ve sunağı arenanın kenarında durur. Adak sunağa tek tek konur. Çanta eşyası etkileşimle sunulur; kabarık ürün Çekim'le sunağa bırakılır. Adak tamamlanınca Ata arenanın ortasına iner. Yenilen Ata aynı adakla yeniden çağrılmaz; Kabarık Ata M4'te gelir.
- **Aşamalar:** Can oranı aşama eşiğinin altına inince sonraki aşama başlar. Geçişte Ata 1,5 saniye hasar almaz ve kabarır.
- **Kaçış:** Oyuncu arenadan 20 saniye uzak kalırsa Ata çekilir ve dövüş biter. Adak geri gelmez.
- **Koca Pofuduk:**
  - **Aşama 1:** Sezdirip hücum eder; yün yumağı atar. Yumak oyuncuyu sektirir.
  - **Aşama 2:** Yün zırhı hasarı azaltır. Biçme vuruşu zırhı kırkar; yanık da zırhı yer. Yumaklardan küçük Pofuduklar çıkar.
  - **Aşama 3:** Çıplak ve hızlıdır. Hücumda arenanın taş sütununa toslarsa sersemler; sersemken aldığı hasar artar.
- **Boynuzlu Dede:**
  - **Aşama 1:** Dallardan koruyucular düşer; sis koyulaşır.
  - **Aşama 2:** Yerden kök dalgası halkaları yayılır; zıplayan oyuncuya değmez. Her dalgadan sonra boynuzdaki iki kovan birkaç saniye ışır. Işıyan kovan zayıf noktadır: kırılan kovan Dede'yi sersemletir.
  - **Aşama 3:** Boynuz kırılır ve parçaları yere düşer. Parçalar Çekim'le alınıp Dede'ye fırlatılınca büyük hasar verir. Silah hasarı azalır.
- **Kural:** Özel mekanikler dövüşü hızlandırır, kapı değildir. Her aşama Tırpan Işını ya da Çivi Tabancası ile tek başına bitirilebilir.
- **Ödül:** Çekirdek ve ganimet çantaya girer. Mühür, ulti ve açılan içerik Sim'deki Ata ilerlemesine yazılır. Mühür etkisinin oranını tarım ve yapı kuralları buradan okur.
- **M2 sınaması:** Adak bileşenlerinin çoğu henüz başka akışlarda: Yün, Demir Külçe, Reçine, kabarık mantar. Bu yüzden `--test=boss` botu Ata Taşı'na yürür ve Ata'yı F1 geliştirici çağırmasıyla indirir. Adak kuralı Sim testlerinde sınanır. Dövüşte eşya, can ya da hasar verilmez.

## 19. Bereket Holding (rakipler)

### 19.1 Kamplar

| Kamp | Ne zaman kurulur |
|---|---|
| 1. kamp | İlk gerçek Kabarma'nın ertesi günü, Ocak'tan en az 600 m uzağa |
| 2. kamp | Ocak T3 |
| 3. kamp | Ocak T4 |
| Genel Merkez | Ocak T5; krater eteğinde sabit yerde |

- **Seviye 1–10.** Baskın yemeyen kamp her 5 günde bir seviye atlar. Tavan `Ocak basamağı × 2`, en fazla 10.
- Kamp oyuncunun kullandığı yapı parçalarından kurulur ve seviye atladıkça gözle görülür biçimde büyür: seviye başına `2 + L` bekçi, `2 + L/2` işçi, `L/2` kule, `L/3` sur katmanı.

### 19.2 Gündüz

- İşçiler 150 m içindeki damarları toplar; o damarlar daha çabuk tükenir.
- Günde bir **kervan** yola çıkar (bir araç, 2–4 bekçi). Açık arazide pusu kurulabilir.
- Kamp tapusunda **Perde** vardır: içeride kimse kimseye hasar veremez; izinsiz giren kibarca dışarı itilir.

### 19.3 Husumet (0–100)

| Olay | Değişim |
|---|---|
| Her gün | +2 |
| İhracat gelirin (günlük, bin kredi başına +1) | en fazla +3 |
| Kervan pususu | +15 |
| Kampa gece baskını | +25 |
| Onlar sana baskın yapınca | −40 |

Husumet 50 ve üstündeyse o kamp Kabarma'da baskın ekibi gönderir (`L/2 + 2` birim, 4. seviyeden sonra kamyonetli). Hedefleri kabarık ürünü çalıp kamyonetle kaçırmak, ikinci hedefleri kulelere sabotajdır.

### 19.4 Senin baskının

Kabarma gecesi Perde iner. Hedefler:

| Hedef | Ganimet |
|---|---|
| Silo | Kabarık ürün, kredi kasaları |
| Atölye | Holding parçası (en üst çiplerin tek kaynağı), planlar |
| Ahır | Nadir cins hayvanlar |
| Şube Çekirdeği | Yok edilirse büyük ödül; kamp 3 gün sonra 1. seviyeden yeniden kurulur, Husumet sıfırlanır |

Şafakta Perde geri gelir ve içeride kalan oyuncu dışarı fırlatılır; elindeki ganimet onda kalır. Evde kalmak ile baskına gitmek arasındaki seçim, ekibi bölmenin ya da savunmayı otomatikleştirmenin gerekçesidir.

### 19.5 Genel Merkez

10 seviyeli, iç mekânlı sabit üs. Sonunda Müdür var. Yalnızca Kabarma gecesi girilir. En üst çip planları ve Holding kozmetikleri buradan gelir.

## 20. Araçlar

- Araç Atölyesi'nde gövde basılır, eklentiler gövdenin yuvalarına takılır.
- Yakıt: T2'de Bitkisel Yağ (Çekirge), T3'ten biyoyakıt (iki kat verim), T4'ten akü ([ICERIK.md](ICERIK.md) §14). Dolu depo yaklaşık bir saat sürüş verir.
- Araç hasar alır ve onarılır; devrilen araç E ile düzeltilir.
- Kabarma'da araç silahtır: ezme ve biçme Gösteri çarpanını artırır.
- Liste: [ICERIK.md](ICERIK.md) §14.

## 21. Dünya

- **Yapı:** Tek ada. Ortada Göbek krateri ve göğe yükselen ışık sütunu; her yerden görünür. Beş biyom kraterin çevresinde dilim dilim dizilir; başlangıç güney kıyısındaki Altın Çayır'dır. Adanın kenarı Sis Denizi'ne inen yarlardır.
- **Geçişler** yumuşaktır: bir biyoma girmek serbest, ayakta kalmak donanım ister. Göbek'in kapısı Ocak T6 ile açılır.
- **Keşif noktaları** (9 tür): Seyir Kulesi, Kadim Taş, Ata Taşı, Oyuk, Enkaz, Holding Kampı, Yabani Bahçe, Yuva, Zengin Yatak ([ICERIK.md](ICERIK.md) §17).
- **Harita:** Gezilen yer açılır. Seyir Kulesi'ne tırmanmak 600 m yarıçapı açar.
- **Hızlı seyahat:** **Mancınık.** Ocak, karakollar ve Seyir Kuleleri arasında oyuncuyu fırlatır; paraşütle inilir. Yük nesnesi taşımaz.
- **Hava:** açık, bulutlu, yağmur, fırtına, sis, kum fırtınası (Kanyon), tipi (Ayaz), sıcak dalgası. Yağmur tarlayı sular; fırtına rüzgâr gülünü iki kat çalıştırır.
- **Yenilenme:** Sıradan damarlar ve ağaçlar 5–10 günde yenilenir. Zengin Yataklar bitmez ama yalnızca Sondaj Kulesi ile işletilir.

## 22. Çok oyunculu

### 22.1 Ortak oyun

- 1–8 oyuncu. Kayıt host'ta. Oyun sürerken katılınır ve çıkılır.
- **Ekipçe ortak:** Ocak, ambar, kredi, araştırma, basamak, Holding ilişkileri.
- **Kişisel:** çanta, Ustalık, kart Nişanları, Ansiklopedi, kozmetik.
- Karakter (Ustalık, Nişan, Ansiklopedi, kozmetik) dünyalar arasında taşınır. Çanta dünyada kalır.
- **Dünyayı devret:** Host, dünya kaydını oyun içinden bir ekip üyesine gönderir; host çevrimdışıyken ekip oynamaya devam eder.
- Yakınlık sesli sohbeti, uzak mesafe için telsiz tuşu, işaret çarkı, 12 jest.
- Düşman sayısı ve Ata canı ekip boyuna göre ölçeklenir.

### 22.2 Çekişme (PvP dünyası)

Ayrı bir dünya türüdür; ortak oyun dünyalarında PvP yoktur.

1. 2–4 ekip, ekip başına 1–4 oyuncu. Her ekibin kendi Ocak'ı vardır; Ocaklar arası en az 500 m.
2. **Gündüz tapu dokunulmazdır:** tapu içinde oyuncu, hayvan ve yapı hasar almaz.
3. **Açık arazi:** Sunucu ayarı. Kapalı (varsayılan) ya da açık; açıksa öldüren, ölenin çantasından rastgele 3 yığın alır.
4. **Kabarma** sunucu takvimine göre herkese aynı anda gelir ve tapular askıya alınır.
5. **Yağmalanabilenler:** tarladaki kabarık ürün ve Açık Silo. Her ekibin o geceki Maya'sının %30'u ve ambarda "ihracata hazır" işaretli mallar Açık Silo'da durur. Geri kalan ambar ve hayvanlar yağmalanamaz.
6. **Yapı hasarı:** Kabarma'da yapılar yıkılabilir. Yıkılan parça enkaz planı olarak kalır ve malzemenin %50'siyle tek tuşla yeniden kurulur. Ocak yok edilemez. Kazanı devrilen ekip için gece biter ve tapusu mühürlenir.
7. **Çevrimdışı koruma:** Ekipten kimse çevrimiçi değilse tapu mühürlüdür ve ekin kabarmaz.
8. **Sezon tablosu:** toplanan Maya + ihracat. Sunucu yöneticisi sezon süresini seçer (7, 14, 30 gün ya da süresiz). Sezon sonunda tablo dondurulur.
9. Holding kampları sunucu ayarıyla açılır ya da kapanır.
10. Listen-server'da iki ekip (en fazla 8 oyuncu), adanmış sunucuda dört ekip (16 oyuncu).

## 23. Üye tipleri ve kozmetik

- 12 oynanabilir üye tipi. Her birinin görünüşü, bir pasifi ve Kabarma'ya başladığı bir kartı var. İş emri zincirleriyle açılır ([ICERIK.md](ICERIK.md) §9).
- Kozmetik parçalar: kask, vizör yüzü, tulum deseni, çanta süsü, Çapa kaplaması, jest. Katalogdan, iş emirlerinden, çarktan ve Genel Merkez'den gelir. Oynanışa etkisi yoktur.
- Vizör bir ekrandır: göz ve ağız ifadeleri duruma göre değişir; konuşurken ağız ses düzeyine göre oynar.

## 24. Öğretme ve Suna

- **İlk gün:** Suna dokuz adımda yönlendirir ([PLAN.md](PLAN.md) §1.4):

| # | Adım | Hedef | Yaklaşık dakika |
|---|---|---|---|
| 1 | Taş | Kazma ile 10 Taş | 0:30 |
| 2 | Odun | Biçme ile 25 Odun (Tezgâh 10 + ağıl 13 + 2) | 1:15 |
| 3 | Ocak | Ocak'ı kur; tapu yayılır | 2:00 |
| 4 | Tezgâh | Tezgâh'ı kur (10 Odun + 5 Taş) | 2:00 |
| 5 | Tarla | 3×3 sür, 9 Şalgam ek, sula | 4:00 |
| 6 | Ağıl | En az 1×2 hücrelik kapalı ağıl: 5 Çit + 1 Bahçe Kapısı (13 Odun) | 5:00 |
| 7 | Gıdak | Buğday at; izleyen Gıdak'ı açık kapıdan ağıla sok | 6:00 |
| 8 | Küçük Kabarma | 3 dakika, 3 dalga Zıpzıp; ilk kart | 8:00 |
| 9 | Kargo | Kabarık şalgamı Çekim ile Kargo Rampası'na taşı | 10:00 |

- **Öğretici kuralları (yalnız ilk gün):**
  - İlk dokuz şalgam öğretici hızında büyür (veride); sonraki ekimler §8.2 hızında.
  - İlk Gıdak tek buğdayla güvenir; ağıla ilk girişinde hemen bir yumurta bırakır.
  - Gece 7. adım bitince başlar; ekinin olgun olması gerekmez. Küçük Kabarma evresine bakmadan bütün ekinleri ve ağıldaki bütün hayvanları kabartır.
  - Dalgalar ağılın bulunduğu yönden girer; Zıpzıp'ın hedefi tarladaki ekindir ve yoldaki çite yüklenir (§14.3). İlk gecede yapı canı %10'un altına inmez.
  - Üye tipinin başlangıç kartı (§23) ilk gecede verilmez; ilk seviyede sunulan üç karttan biri her zaman Çatal'dır.
  - İlk satışın ödemesi anında gelir (normalde 60 sn, §13).
- **İlk 10 dakikadan sonra** öğretici yedi adımla sürer; Çıraklık zincirine bağlanır:

| # | Adım | Hedef |
|---|---|---|
| 10 | Hasat | Biçme ile 5 şalgam (ürün ve tohum) |
| 11 | Yemek | Ocakbaşı kur, bir yemek pişir ve ye |
| 12 | Bakır ve kil | Bakır damarından Bakır Cevheri, göl kıyısından Kil |
| 13 | Taş Fırın | Taş Fırın kur, Bakır Külçe erit |
| 14 | Çivi | Tezgâh'ta Çivi Tabancası ve Çivi yap |
| 15 | Kasa | Paketleme Tezgâhı kur, kasa doldur; Tezgâh'ta El Arabası yap, kasayı onunla rampaya götür, sat |
| 16 | Barınak | Temel, duvar, kapı kasası ve Ahşap Kapı, çatı; içine Uyku Tulumu |
| 17 | Katalog | Kargo Rampası terminalinden Katalog'u aç, satıştan gelen Krediyle Şalgam Tohumu paketi al; kapsül Ocak'ın yanına iner, E ile aç (M2, §13) |
| 18 | Yükseltme (M2) | Taş Fırın'da Kerpiç (2 Kil + 1 Lif) yap; İnşa Planı eldeyken barınağın bir duvarına bakıp E basılı → Yükselt: Kerpiç-Taş (2 Kerpiç + 2 Taş, Odun iadesi). Suna repliği yok, yalnız Defter satırı. |

- **Suna'nın seslendirilen replikleri tuş adı içermez.** Tuşlar değiştirilebilir; yönlendirme ekrandaki adım satırında son kullanılan aygıtın glifiyle yazılır ([SANAT-SES.md](SANAT-SES.md) §10.3).
- **Çıraklık zinciri:** Defter'deki ilk zincir ilk iki saati taşır.
- **Her yeni sistem** ilk karşılaşmada tek cümlelik ipucu ve Ansiklopedi sayfası açar.
- **Suna** yaklaşık 700 replik söyler: iş emirleri, olay yorumları (ölüm, ilk kez yapılanlar, Alaca, rekor), Kabarma anonsları, ipuçları. Aynı replik 20 dakika içinde tekrarlanmaz. Ayarlardan sıklığı azaltılır ya da kapatılır.

## 25. Dünya ayarları

| Ön ayar | Fark |
|---|---|
| Sakin Ay | Kabarma yalnızca çanla; düşman hasarı %60; Holding kapalı |
| Kooperatif Standardı | Varsayılan |
| Sert Ay | Hayvan ölümü kalıcı; emanet kasası 20 dakikada kaybolur; Husumet ×1,5 |

Ayrı sürgüler: gün uzunluğu, Kabarma aralığı, büyüme hızı, düşman canı ve hasarı, kaynak bolluğu, Holding, dost ateşi. **Yeni Ay** kurarken tüzük maddeleri (§14.8) baştan seçilebilir.

## 26. Denge hedefleri

`Sim` ile hızlandırılmış oyuncu modeli şu hedeflere ±%25 uymalı ([TEKNIK.md](TEKNIK.md) §11):

| Geçiş | Tek oyuncu hedef süre |
|---|---|
| Başlangıç → T2 | 4–6 saat |
| T2 → T3 | 8–10 saat |
| T3 → T4 | 12–15 saat |
| T4 → T5 | 15–20 saat |
| T5 → T6 | 20–25 saat |
| T6 → Ana Maya | 10–15 saat |

- Dört kişilik ekipte süreler ×0,6.
- Önerilen donanımla bir KS'yi ilk denemede geçme oranı %70.
- Hiçbir üretim basamağında 10 dakikadan uzun zorunlu bekleme yok.
- Her 20 dakikada en az bir yeni şey açılır (tarif, kart, iş emri adımı, Ansiklopedi sayfası).
