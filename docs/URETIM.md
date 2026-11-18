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
2. 53 klip: döngülü yürüyüş, akrobasi ve jestler düz yazıyla hareket üretimi ya da insansı klip kütüphanesinden; yerinde duran ve üst gövdesi sabit klipler E'deki poz yöntemiyle kodla. M1'de 30'u ([SANAT-SES.md](SANAT-SES.md) §3.1).
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

### 2.3.1 Deneme sonuçları (17 Kasım 2026)

Ara karar: sahibi dokusuz, palete oturtulmuş görünümü istemedi ([SANAT-SES.md](SANAT-SES.md) §1 güncellemesi). Hat iki kipte çalışıyor; denemeler dokulu kipte yeniden işlendi ve oyuna öyle girdi.

| # | Deneme | Sonuç | Ölçülen |
|---|---|---|---|
| 1 | Statik: kaya, çayır ağacı, sulama kovası (Tripo web, H3.1 "HD Model") | ⚠️ Üçü de oyunda. Kaya 2,0 M → 3.879 üçgen; ağaç 1,9 M → 6.788 (bütün ağaçlar tek MultiMesh); kova 1,9 M → 2.908; hepsi 1K doku. Web modelleri API'den görünmüyor (görev bulunamadı); yalnızca tarayıcıdan dışa aktarılabiliyor. Ölçüt 4 (bütünlük), varlık raporlarına göre: ağaç 0 manifold dışı kenar (geçti), kaya 13, kova 83 (kaldı); gözle görünmüyor. | 3 × 55 web kredisi, 0 yeniden deneme |
| 2 | Makine: Taş Fırın (Tripo API H3.1 + anlamsal parçalama v2) | ⚠️ 6 parçaya bölündü ama çıktıdaki düğümler `base, body, door, door_handle, part_4, part_5`: baca ve pano adlı parça yok. Kapak menteşesinden kodla açılıyor; baca "puf"u henüz yok. `--rename` ile yeniden işlenecek (kredi yok). Not: Tripo modeli +X'e bakıyor. | 20 + 40 = 60 API kredisi, 0 yeniden deneme |
| 3 | Oyuncu + klipler | ⚠️ MVP'de Genex (Meshy) ile: 1,6 m, 6 klip (bekleme, yürüme ×2, koşu ×2, zıplama) kaymadan oynuyor. Kol IK'si kodda var (`TwoBoneIK3D`) ama doğrulanmadı. Birleştirilen 15 oyuncu klibinin 8'i boş (birleştirme hatası). | 64 Genex kredisi |
| 4 | Yaratık: Pofuduk (dört ayak) ve Gıdak (kuş) | Pofuduk ✅: Tripo dört ayak iskeleti v2.5 ve hazır yürüme klibi; kemikler adımda 83°'ye kadar dönüyor. Bacaklar yünün içinde kaldığı için adım uzaktan zor seçiliyor; tasarım notu: istemde daha uzun bacak. Gıdak ⚠️: Tripo'da kuş için hazır klip yok (belgedeki gibi); şimdilik kodla paytak yürüyüş. Kodla yazılan poz kütüphanesi (§2.2 E) henüz yazılmadı. | Pofuduk 20 + 25 + 10 = 55 API kredisi; Gıdak 35 Genex kredisi |
| 5 | 10 ses efekti + 1 döngü | ⚠️ MVP'de Genex ses kanalıyla 12 ses üretildi (11 efekt + ışın döngüsü). Denetim betiği yok: süre ve tepe düzeyi elle ffprobe ile ölçüldü (§2.2 H.3'ün betiği yazılmadı). Dinleme turu sahibinde. | 24 Genex kredisi |
| 6 | 1 müzik, 3 katman | ⚠️ Tek katmanlı 90 sn Kabarma parçası (Genex). Katman ayrıştırma denenmedi. | 27 Genex kredisi |
| 7 | Suna: 3 aday ses, 10 replik | ⏸ Yapılmadı: sahibinin ElevenLabs anahtarı her kullanımda ayrı onay istiyor; onay alınmadı. | — |

**Öğrenilenler:** Tripo API'de H3.1 metinden model 20 kredi, web stüdyosunda "HD Model" 55 kredi: toplu üretim API'de çok daha ucuz. Web kredileri önemli tek modeller için. Yapay zekâ dokularındaki pişmiş gölge palete oturtmayı bozuyor; dokulu yönde bu sorun yok. Karmaşık şekiller (2 M üçgen) Blender'da tek geçişte 4 bin üçgene iniyor, yaklaşık 50 sn.

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

**Durum (17 Kasım 2026):** 11 altın varlık + 5 ek varlık üretildi ([stil/README.md](stil/README.md)); El Arabası dışında hepsi oyunda (El Arabası henüz kullanılmıyor). Yapı köşesi Blender betiğiyle üretiliyor. 13 varlık raporunun 12'si betikli ölçüt 4'ten (bütünlük) kalıyor; yani "ölçütlerden geçer" koşulu henüz sağlanmadı. Tripo API harcaması bu turda 385 kredi (8 model, 2 iskelet, 4 klip, 2 parçalama), deneme çarpanı 1,0: hiçbiri yeniden üretilmedi.

## 5. Kredi bütçesi

Birim fiyatlar `npx genex --help` çıktısından (16 Kasım 2026). Yeniden deneme çarpanları varsayımdır; M0 ölçümüyle düzeltilir.

| Sınıf | Adet | Birim (kredi) | Deneme çarpanı | Toplam (kredi) |
|---|---|---|---|---|
| Statik modeller (bitki ve kaya 96, damar 25, yerleştirilebilir 72, ekin 48, dünya eşyası 30, başlık 18, kozmetik 24, keşif noktası 40, Holding 15) | 368 | 40 (görsel + model) | 2,5 | 36.800 |
| Parçalı modeller (makine ve parça 73, kule ve tuzak 16, araç ve eklenti 17) | 106 | 86 (+ parçalara ayırma) | 2,5 | 22.800 |
| Yaratıklar (27 tür, Ata parçaları 12, Oyuk koruyucusu 4) | 43 | 81 (+ kemik + yürüyüş) | 3 | 10.400 |
| Oyuncu gövdesi ve 53 klip | 1 | 2.500 | 2 | 5.000 |
| Görseller (kart 72, logo, kapsül, menü, konsept föyleri) | 400 | 5 | 1 | 2.000 |
| Ses efektleri (900 ses × 4 varyasyon) | 3.600 | 2 | 1,5 | 10.800 |
| Müzik (40 parça, 20 geçiş sesi) | 60 | 27 | 2,5 | 4.100 |
| Seslendirme (doğrudan ElevenLabs: yaklaşık 1.550 replik, 125.000 karakter) | — | — | 2 | 2.000 |
| **Ara toplam** | | | | **93.900** |
| Beklenmeyen (%30) | | | | 28.200 |
| **Toplam** | | | | **yaklaşık 122.000 kredi ≈ 1.220 $** |

Buna ek olarak: ElevenLabs aboneliği (üretim aylarında), Steam uygulama ücreti (100 $). Harcama taşlara yayılır: M0 yaklaşık %2, M1 %8, M2–M4 %70, M6 %20. Her taşın başında o taşın harcama tavanını söylerim; onayın olmadan üretim başlatmam.

### 5.1 Ölçülen birim fiyatlarla yeniden hesap (17 Kasım 2026, dokulu yön)

Birimler M0'da ödenen gerçek tutarlar. Tripo API kredisi, Tripo web kredisi ve Genex kredisi ayrı havuzlardır; dolar karşılığı havuza göre değişir. Deneme çarpanı M0'da 1,0 çıktı (5 denemenin hiçbiri yeniden üretilmedi), ama örnek küçük olduğu için 1,5–2 alındı.

| Sınıf | Adet | Yol ve birim | Çarpan | Toplam |
|---|---|---|---|---|
| Statik modeller | 368 | Tripo API H3.1 20 + ayrıntılı doku ~10 = 30 | 1,5 | 16.560 Tripo |
| Parçalı modeller | 106 | H3.1 20 + parçalama 40 = 60 | 1,5 | 9.540 Tripo |
| Yaratıklar | 43 | H3.1 20 + iskelet 25 + yürüme 10 = 55 | 2 | 4.730 Tripo |
| Oyuncu gövdesi ve klipler | 1 | Genex karakter 64 + ek klipler | 2 | 5.000 Genex |
| Görseller | 400 | 5 | 1 | 2.000 Genex |
| Ses efektleri | 3.600 | 2 | 1,5 | 10.800 Genex ya da ElevenLabs (anahtar her kullanımda onaylı) |
| Müzik | 60 | 27 | 2,5 | 4.100 aynı |
| Seslendirme | — | ElevenLabs | 2 | 2.000 aynı |
| **Ara toplam** | | | | **30.830 Tripo + 23.900 ses/görsel** |
| Beklenmeyen (%30) | | | | 16.420 |
| **Toplam** | | | | **yaklaşık 71.000 kredi** (eski hesap 122.000) |

Elde (17 Kasım 2026): Tripo API 320 (9 süs modeline 180 kredi harcandı: turuncu ağaç, eğrelti, ot, çiçek, yabani buğday, çalı, dik kaya, düz kaya, bakır damarı; üretildi, henüz içe aktarılmadı) · Tripo web 3.035 (doğrulanmadı) · Genex 2 (23 Kasım 2026'da 200'e dolar).

## 6. Kilometre taşları

Sıra sabittir: önce doğrulama, sonra görünüş ve his, sonra sistemler az içerikle, sonra içerik, sonra çok oyunculu genişleme, sonra cila. Her taş oynanabilir bir derlemeyle biter.

### M0: Doğrulama

**Teslim:** Çalışan boş C# projesi, `Sim` iskeleti, Steam ve ENet taşıması, Blender hattı, yedi hat denemesi, güncellenmiş bütçe.

**Kabul:**
- [ ] [TEKNIK.md](TEKNIK.md) §1.2'deki sekiz adım geçti ya da yedeği seçildi ve belgeye yazıldı.
  *Durum (17 Kasım 2026): 3, 5, 6, 8 geçti; 1 ve 2 kısmen (Windows'a dışa aktarma ve 1.000 gün ölçümü yok); 4 (Linux) yedeği yazılı, doğrulanmadı; 7 aşağıda.*
- [ ] §2.3'teki yedi deneme geçti ya da yedeği seçildi.
  *Durum: hiçbiri tam geçmedi; 1, 2, 3, 5, 6 ⚠️; 4'te Pofuduk'un iskeleti ve yürüyüşü oturdu, Gıdak iskeleti, poz kütüphanesi ve ortak şişme shader'ı açık; 7 yapılmadı (§2.3.1).*
- [ ] Bütçe tablosu ölçülen deneme oranlarıyla güncellendi; sen onayladın.
  *Durum: §5.1 yazıldı; sahibin onayı kayıtlı değil.*

### M1: Görünüş ve his dilimi

Tek oyuncu, Altın Çayır'ın 300 × 300 m'lik bir köşesi. Amaç tek: oyunun bir stüdyo işi gibi durduğunu ve ana fiilin zevkli olduğunu kanıtlamak. Genişlik yok.

**Teslim:**
- 12 altın varlık ve stil föyü.
- Arazi, gökyüzü, sis, su; 8 bitki, 4 kaya.
- Oyuncu: 30 klip (M1 alt kümesi; kalan 23'ü onları ilk kullanan taşta, [SANAT-SES.md](SANAT-SES.md) §3.1), IK ve yay katmanları, ragdoll, vizör ifadeleri.
- Çapa: altı iş başlığı, Tırpan Işını, Çivi Tabancası; vuruş hissi katmanları; F1 ayar paneli.
- Gıdak ve Zıpzıp, normal ve kabarmış.
- Ahşap yapı seti (26 şekil), 6 yerleştirilebilir, Ocak.
- Şalgam tarlası (büyüme, sulama, kabarma).
- Tezgâh, Ocakbaşı, Taş Fırın, Paketleme Tezgâhı, Kargo Rampası, El Arabası.
- İlk gece senaryosu: Küçük Kabarma, 10 kart, hasar sayıları, Hasat Raporu.
- HUD (gündüz ve Kabarma), envanter, Defter'in ilk sayfası.
- 60 ses efekti, 2 müzik parçası (çayır gündüzü; 3 katmanlı Kabarma), Suna'dan 30 replik (Türkçe ve İngilizce).

**M1 kapsamı ayrıntısı:**

| Kalem | M1'de | Kural ve liste |
|---|---|---|
| Kaynaklar | Taş, Odun, Lif, Kil, Bakır Cevheri, Bakır Külçe, Şalgam ve tohumu, Buğday, Yumurta, Çivi. Kazma ile: Kil göl kıyısındaki kil yataklarından (koddan yumru, kredi yok), Bakır Cevheri bakır damarından. Biçme ile: Lif ot ve çalıdan, Buğday yabani buğdaydan; kapsülden ayrıca 10 Buğday. Buğday ekimi M2. Haritada Ocak'a 60–90 m'de bir göl. | [ICERIK.md](ICERIK.md) §2, §20 |
| Bitki ve kaya | 8 bitki: çayır ağacı ve Şalgam (altın) + 6 süs modeli. 4 kaya: cevherli kaya (altın) + dik kaya, düz kaya, bakır damarı. Süs modelleri üretildi, içe aktarılacak (§5.1). | — |
| Yerleştirilebilir (6) | Ahşap Kapı, Sandık, Uyku Tulumu, Meşale, Yemlik, Folluk. Modelleri yapı kitinin Blender betiğinden (kalas dili, kredi yok). Korkuluk M2'de (gündüz zararlısıyla birlikte). | [ICERIK.md](ICERIK.md) §10.2 |
| Yapı | 26 Ahşap şekli kitte ve kurulabilir; sur ve kule tabanının savaş işlevi M3. İnşa: yerleştir, sök (%100 iade), onar (hasar oranında %25). Yükseltme, boyama, plan modu M2. Kurma yalnızca tapu içinde; tapu dışı M2. Hasarla yıkılma yok: parça canı %10'da durur (yıkılma M3). | [ICERIK.md](ICERIK.md) §10.1, [GDD.md](GDD.md) §7 |
| İstasyonlar (5) | Tezgâh, Ocakbaşı, Taş Fırın, Paketleme Tezgâhı, Kargo Rampası; Ocak ve rampa kapsülden. Tarifler: Tezgâh → Çivi Tabancası, Çivi, El Arabası · Taş Fırın → Bakır Külçe · Ocakbaşı → 3 yemek · Paketleme → kargo kasası. Yakıt: iş başına 1 Odun. Kerpiç, Bakır Tel M2. Ocakbaşı modeli koddan (kaya modelinden taş halka + ızgara + ateş; kredi yok). | [ICERIK.md](ICERIK.md) §11, §20 |
| Yemek (3) | Közde Şalgam, Haşlanmış Yumurta, Sebze Çorbası (2 Şalgam); üç yemek yuvası | [ICERIK.md](ICERIK.md) §15, [GDD.md](GDD.md) §3.3 |
| Gıdak ve ağıl | Ağıl kuralı GDD §9.1; Gıdak başına 4 m². Buğdayla güven, izler, kapıdan girince kayıt. Yemlik'te Buğday varken 3 dakikada bir Folluk'a (yoksa yere) yumurta. Kaçma, mutluluk, üreme M2. | [GDD.md](GDD.md) §9, §24 |
| Zıpzıp | Gündüz çayırda 2–3'lük ürkek gruplar; olgun ekine gelir ([GDD.md](GDD.md) §8.1). Gece dalga. | — |
| Çapa | Kazma: taş, kil, bakır · Biçme: alan etkili; ağaç vuruş başına Odun verir, devrilmez (devrilme, kütük, yenilenme M2); ot ve çalı Lif, yabani buğday Buğday verir, geri gelmez; olgun şalgamı hasat eder · Çekim: tutar, taşır, fırlatır; 80 kg sınırı; arkadaş tutma M2 · Toprak: yalnız tarla sürme; yükseltme, alçaltma, düzleme, yol M2 · Sulama: 100 L depo, su yüzeyinden dolar; küçük düşmanı iter; yangın söndürme M3 · İnşa: yukarıdaki "Yapı" · Tırpan Işını: baştan · Çivi Tabancası: yarı otomatik, gündüz Çivi harcar, Kabarma'da sınırsız; ilk Bakır Külçe'de tarifi açılır · Tarayıcı M2 | [ICERIK.md](ICERIK.md) §6, [GDD.md](GDD.md) §4 |
| Kartlar (10) | Çatal, Seken, Tez El, İri Tane, Kıvılcım, Kırağı, Keskin Göz, Tokmak, Mıknatıs, Çevik (hepsi §8.3'ün baştan açık 20'sinden). İlk teklifte Çatal her zaman var. | [ICERIK.md](ICERIK.md) §8.3, [GDD.md](GDD.md) §24 |
| Kargo | GDD §13 olduğu gibi: kasa 20 yığın, 25 kg yük nesnesi, değeri içindekilerin toplamı; rampa 1–6 kasa ya da 1 kabarık ürün; ödeme 60 sn (ilk satış anında). Fiyat = taban × kalite; Borsa ve doygunluk M2. | [GDD.md](GDD.md) §13 |
| El Arabası | Tezgâh'ta. E ile tutamağa geçilir, yürüme hızının %80'iyle itilir; 1 yük nesnesi (en fazla 200 kg) ya da 6 yuva, ikisi birden değil; devrilince E ile düzeltilir. | [ICERIK.md](ICERIK.md) §14 |
| HUD | Gündüz: kemer, can ve dayanıklılık, pusula, etkin adımın tek satırı. Kabarma: + dalga sayacı, Öz çubuğu, Gösteri, kart şeridi. Dayanıklılık sistemi var. | [SANAT-SES.md](SANAT-SES.md) §8.1, [GDD.md](GDD.md) §3.2 |
| Takvim | İlk geceden sonra gün döngüsü işler; normal gece Kabarma'sızdır. Takvim ve Çan M3. Küçük Kabarma F1 panelinden yeniden başlatılır (his denemesi için). | [GDD.md](GDD.md) §2 |
| İlk gün | 9 adım + ilk 10 dakikadan sonra 7 adım | [GDD.md](GDD.md) §24 |
| Ses ve Suna | 60 ses (12'si M0'dan), varyasyon 3 · 30 replik × 2 dil, tuş adı yok | [SANAT-SES.md](SANAT-SES.md) §10.1, §10.3 |
| Görüntü ve performans | 4 gün saati, 4 yer imi; "yüksek" ön ayar ve ölçüm sahnesi | [SANAT-SES.md](SANAT-SES.md) §2, §9, [TEKNIK.md](TEKNIK.md) §10 |
| [PLAN.md](PLAN.md) §7.2 koşul 4 ve 5 | Ortak oyun yarısı ve kayıt "M2'de" raporlanır; klavye-fare ve oyun kolu M1'de zorunlu. Yeni `Sim` durumu kayda hazır, Godot katmanında yeni statik yok. | [PLAN.md](PLAN.md) §7.2, [TEKNIK.md](TEKNIK.md) §9 |

**Kabul:**
- [ ] İlk 10 dakika senaryosu ([PLAN.md](PLAN.md) §1.4, adımlar [GDD.md](GDD.md) §24) hem botla hem insan giriş yoluyla hatasız oynanıyor.
- [ ] Bütün varlıklar sekiz ölçütten geçti.
- [ ] 16 ekran görüntüsü (4 yer imi × 4 gün saati, [SANAT-SES.md](SANAT-SES.md) §2 ve §9) ekran görüntüsü ölçütlerinden geçti.
- [ ] His ölçütleri aralıkta (girişten tepkiye ≤ 50 ms).
- [ ] RTX 2070S'te 1080p yüksek ayarda ≥ 90 FPS ([TEKNIK.md](TEKNIK.md) §10'daki ön ayar ve sahneler).
- [ ] **Sen** 30 dakika oynuyorsun ve iki soruya "evet" diyorsun: "Stüdyo işi gibi duruyor mu?" ve "Ateş etmek ve kabartmak zevkli mi?" İkisi de "evet" olmadan M2 başlamaz.

### M1 kararları (17 Kasım 2026)

Belgelerin sustuğu ya da çeliştiği yerler için verilen kararlar. Sahibin tek yerden gözden geçirmesi için.

| # | Karar | Değişen yer |
|---|---|---|
| 1 | 6 yerleştirilebilir: Ahşap Kapı, Sandık, Uyku Tulumu, Meşale, Yemlik, Folluk; bedelleri yazıldı. Korkuluk M2. | ICERIK §10.2 |
| 2 | 3 yemek: Közde Şalgam, Haşlanmış Yumurta, Sebze Çorbası. Sebze Çorbası, Kabak Çorbası'nın yerini aldı (2 Şalgam ya da 2 Kabak; değerler aynı); yemek sayısı 32'de kaldı. Bu bir ad değişikliğidir ("adlar kesindir", ICERIK başı): sahibin onayına; seçeneği Kabak Çorbası'nı korumak ve M1'i iki yemekle bırakmak. Pişiren istasyon Ocakbaşı. Gıdak yumurtası çanta eşyasıdır; yük nesnesi olan kuluçkalık yumurtadır. | ICERIK §15, GDD §5 |
| 3 | Ahşap parça bedelleri tabloya yazıldı. Kurma tapu dışında da serbest; tapu dışında söküm %50 iade. M1'de kurma yalnız tapu içinde (tapu dışı M2); söküm ve onarım var, hasarla yıkılma M3. | ICERIK §10.1, GDD §7, bu bölüm |
| 4 | M1 belgeye uyar, sadeleştirme yok: Tezgâh 10 Odun + 5 Taş, Taş Fırın 15 Taş + 5 Kil, Çivi Bakır Külçe'den, Çivi Tabancası ilk Bakır Külçe'de. M1 dünyasına Kil ve Bakır girer. Ocakbaşı M1'e girer (M1 istasyonu 4 → 5). Ocakbaşı, Paketleme Tezgâhı, El Arabası, Çivi ve Çivi Tabancası bedelleri yazıldı. | ICERIK §2.3, §20; §6 içerik dağılımı |
| 5 | Koddaki 10 kart onaylandı; ilk teklifte Çatal; ilk gecede üye tipinin başlangıç kartı verilmez. | GDD §24 |
| 6 | Çapa başlıklarının M1 kapsamı yazıldı (yukarıdaki tablo). | bu bölüm |
| 7 | 4 gün saati: sabah, öğle, gün batımı, Kabarma gecesi; normal gecenin ışığı da yazıldı ama 16 görüntüye girmez. 4 yer imi: iniş kapsülü, üs, göl kıyısı, tepe. "Yüksek" ön ayar ve ≥ 90 FPS ölçüm sahneleri. | SANAT-SES §2, §9; TEKNIK §10 |
| 8 | M1'in 60 sesi listelendi; 12'si M0'dan. | SANAT-SES §10.1 |
| 9 | Suna'nın 30 repliği Türkçe ve İngilizce; seslendirilen replikte tuş adı yok, tuş ekranda glifle. | SANAT-SES §10.3, GDD §24 |
| 10 | **Plan değişikliği:** Oyuncu M1'de 53 değil 30 klip: 25 kütüphane ya da düz yazı, 5 kodla poz. Kalan 23 klip onları ilk kullanan taşa kaydı (9'u M2, 1'i M3, 13'ü en geç M4). | SANAT-SES §3.1, §2.2 D, M1 Teslim |
| 11 | HUD'da pusula var; dayanıklılık var (depar 8/sn, yenilenme kuralı yazıldı); Defter takibi pusulanın altında tek satır. | SANAT-SES §8.1, GDD §3.2 |
| 12 | PLAN §7.2'nin 4. (ortak oyun yarısı) ve 5. koşulu M1'de "M2'de" raporlanır; karşılığında kayda hazır `Sim` durumu, yeni statik yok. | PLAN §7.2, TEKNIK §9 |
| 13 | Ağıl tanımı (kapalı halka, ≥ 1 hücre); ilk günün 9 adımı: Tezgâh ve ağıl geceden önce; öğretici kuralları (gece hedefi, ilk yumurta, ilk satış); MVP'nin gece sonrası adımları "ilk 10 dakikadan sonra" olarak kaldı ve yeniden sıralandı. | GDD §9.1, §24 |
| 14 | Kargo M1'de GDD §13 olduğu gibi; kasa 25 kg ve değeri içeriği; ilk satış anında öder. | GDD §13, §24 |
| 15 | El Arabası: Tezgâh'ta 12 Odun + 4 Taş; 1 yük nesnesi (≤ 200 kg) ya da 6 yuva; itilir. | ICERIK §14, §20 |
| 16 | Bayat bilgiler düzeltildi: hat denemeleri, altın varlık durumu, eldeki krediler, M0 durumları, test sayısı, dışa aktarma; Zıpzıp küçük sınıfta, 5.000 üçgene indirilir. | §2.3.1, §4, §5.1, M0; TEKNIK §1.3; SANAT-SES §4; stil/README |
| 17 | Gündüz Zıpzıp grupları M1'de olgun ekine gelir, yanında durup koklar, oyuncu yaklaşınca kaçar; ekini **yemez**. Ekin kaybı (GDD §8.1 gündüz zararlısı) korkulukla birlikte M2'de açılır (`creatures.json` `dayEats`). | bu bölüm; GDD §8.1 |
| 18 | İlk günün ilk 10 dakikadan sonraki adımları (GDD §24) veride ön koşul taşır (gereken tarif ya da yerleştirilebilir kimliği). Ön koşulu oyunda olmayan adım atlanır ve görünür biçimde raporlanır; sıkı kipte atlama hatadır. Paralel dallar birleştikten sonraki doğrulama sıkı kipte yapılır. | bu bölüm; GDD §24 |
| 19 | Öz, öldürülen düşmanın yerine parçacık olarak düşer (değeri öldürme anında: tür × Gösteri) ve 4 m toplama yarıçapında oyuncuya geçer; Mıknatıs yarıçapı +%40 büyütür. Dalga sonunda ya da şafakta yerde kalan Öz oyuncuya uçar: Öz kaybolmaz, Mıknatıs yalnız seviye atlamayı öne çeker. | bu bölüm; GDD §8; ICERIK §8.3 |
| 20 | **Sahibin kararı:** arayüz Rust tarzı (yerleşim, oran, his; kod ve çizim özgün). Çapa başlıkları ve silahları yalnız Q çarkından seçilir; 1–8 kemeri seçer. | SANAT-SES §8.1; GDD §4 |
| 21 | **Sahibin kararı:** birinci şahıs yok. V, normal üçüncü şahıs ile Astroneer tarzı yakın omuz kipi arasında geçer; normal kipte fare tekerleği yakınlaştırır. Tutulan her nesne el soketine oturur, eller IK ile kavrar. | PLAN §4 Kamera, §8 |
| 22 | **Sahibin kararı (18 Kasım):** M1 kapısı kalktı; oyun bütünüyle hızla yapılır, sahip sonunda oynayıp düzeltme ister. Dallar ana oturumca main'e birleştirilir. Görünüş olgun ve cilalı (Astroneer üstü); kaynak: `F:\Assets` kitaplığı, Tripo (bakiyenin tamamına kadar onaylı), agy. | bu bölüm |
| 23 | **Sahibin kararı (18 Kasım):** Astroneer mantığına Rust mantığı eklenir. Çapa kemerdeki bir eşyadır (1–8 ile seçilir); elde başka eşya ya da hiçbir şey olabilir. Üretim Çapa'dan yapılmaz: Tab envanterindeki Üretim panelinden (el üretimi ve kuyruk) ve istasyon menülerinden yapılır. Üretilen yerleştirilebilir envantere girer, kemerden seçilip kurulur. Yapı parçaları ayrı bir İnşa Planı eşyasıyla kurulur; İnşa başlığı kalkar (Çapa'da 5 iş başlığı). Eksik malzeme satırı nereden bulunacağını söyler ve pusulada gösterilebilir. | GDD §4, §5, §7; PLAN §3, §4 |
| 24 | **Sormadan ilerle ve model kuralı (sahibin kararı, 18 Kasım 2026):** ana oturum M2'den M7'ye taşları sahibe "devam edeyim mi" diye sormadan sırayla yapar; gerekirse 20'ye kadar paralel oturum açar. Sahibe yalnız kredi tutarı, `git push`/yayın ve sahibin dosyasını silmek sorulur; kredi istekleri tek listede toplanır. Sahibin yargısına kalan kabul maddeleri "sahibin onayını bekliyor" yazılır ve iş durmaz. Model: kolay işler Sonnet 5.5; varsayılan Opus 5.5 medium; yalnız en zor işler (ağ ve ortak oyun, kayıt, yapı dengesi, yol bulma) Opus 5.5 high; danışman Fable. Yol haritası ve kredi kuyruğu `docs/PROGRESS.md`'de. | bu bölüm, CLAUDE.md |
| 25 | **Akışlar main'e kendisi alır, bitenler kapanır (sahibin kararı, 18 Kasım 2026):** worktree + ana oturumun sırayla birleştirmesi yerine her akış `tools/integrate.ps1` ile kendi dalını main'e alır (main'i dala birleştir, derleme ve Sim testleri, main'i atomik ilerlet; çatışmayı kodu bilen akış çözer). Worktree'ler kalır: aynı klasörde çok sayıda Godot ve derleme koşusu birbirini bozar. Ana çalışma kopyası main'i detached tutar. İşi biten ve main'e girmiş oturumları ana oturum arşivler. `.gitattributes`: `*.import` ve `*.uid` LF (Godot içe aktarmasının yarattığı sahte değişiklikler biter). | CLAUDE.md, `tools/integrate.ps1` |
| — | Takvim: ilk geceden sonra Kabarma yok; Küçük Kabarma F1'den yeniden oynanır. | bu bölüm |
| — | Koddan varlıklar: Ocakbaşı (kaya modelinden halka + ızgara), 6 yerleştirilebilir (yapı kitinin kalas dili) ve kil yatağı §2.2 A yolu yerine Blender betiğinden, kredisiz. [SANAT-SES.md](SANAT-SES.md) §9'dan geçemeyen Tripo API ile üretilir (yaklaşık 30'ar kredi, onaylı; elde 320). | bu bölüm |

**Bu kararların kredi isteyenleri ve sahibin onayları (17 Kasım 2026):**
- **ElevenLabs (doğrudan, sahibin anahtarı) — M1 ses paketi onaylandı**, şu üst sınırlarla: Suna için 3 aday ses · 30 TR + 30 EN replik, en fazla 7.200 karakter, geri çevirme denetimiyle · 60 ses × 3–4 sürüm, en fazla ~19 dk · 2 müzik (çayır gündüzü ~3 dk, 3 katmanlı Kabarma ~2 dk), denemelerle en fazla ~12,5 dk. Sınır aşılacaksa yeniden sorulur. Genex ses kanalı kullanılmaz (bakiye yetmez).
- **Tripo API — 175 krediye kadar onaylandı** (elde 320): Gıdak kuş iskeleti (Blender denemesi tutmazsa ~55), ince saplı ot ve buğdayın yeniden üretimi (~80'e kadar), gerekirse 8. bitki (~30), Zıpzıp yürüyüş klibi (~10). Her üretimden önce `--dry-run` tahmini yazılır.
- **Onay bekleyen:** eksik oyuncu klipleri (kütüphane bağlama ~6 × 11, `--locomotion` ~3 × 14; Genex, 23 Kasım'da 200'e dolar). Tripo web kredisi tarayıcı oturumu olmadığı için kullanılamıyor.
- Ocakbaşı, 6 yerleştirilebilir ve kil yatağı koddan: kredi yok.

### M2: Çekirdek sandbox

Bütün kalıcı sistemlerin kuralları, T1–T2 içeriğiyle; ardından ortak oyun.

**Teslim:** yapı (taşıma dengesi, yükseltme, plan modu, Plan Defteri) · tarım (genler, çaprazlama, Alaca, zararlılar) · hayvancılık (Çayır ve Koru'nun sekiz evcil türü: Gıdak, Pofuduk, Homur, Dallı, Hu, Vıcık, Bozuk, Vızvız; roller, üreme) · Çayır ve Koru'nun 12 yaratığının tamamı · güç ağı ve temel sinyal · T1–T2 üretim zinciri ve araştırma · envanter, ambardan üretim · ekonomi (borsa, katalog, çark) · iş emirleri altyapısı ve ilk 12 zincir · Ustalık · kayıt ve yükleme · Sis Korusu · Koca Pofuduk ve Boynuzlu Dede · harita, Seyir Kulesi, mancınık · Çekirge · ortak oyun 1–4 (ENet ve Steam), sonradan katılma, sesli sohbet, şok tokadı.

**Kabul:**
- [ ] Birim, içerik ve senaryo testleri yeşil.
- [ ] 4 bot istemciyle senkron farkı < 1 cm; kayıt-yükleme sonrası dünya aynı.
- [ ] `SimRunner` oyuncu modeli başlangıçtan T3'e hedef sürede ulaşıyor (±%25).
- [ ] **Sen ve bir arkadaşın** iki ayrı makinede Steam üzerinden 2 saat oynuyorsunuz.

### M3: Kabarma ve savaş

Bu taşta dünya hâlâ Altın Çayır ve Sis Korusu'dur (T1–T2); savaş sistemlerinin tamamı bu içerikle kurulur.

**Teslim:** tam Kabarma akışı (oyunda KS 1–10; stres sahnesinde KS 30), dalga bileşimi ve "ne ekersen onu çekersin" · 72 kart, aşılar, Nişan · Gösteri çarpanı · Tatbikat, Maya Feneri · T1–T2 kuleleri (Çivi, Sapan, Saçma) ve tuzakları (Diken Tarlası, Yapışkan Zemin, Fırlatma Pedi, Çukur Kapağı) · savaş hayvanları ve binek · 24 çip · ilk beş güç ve Kadim Taş denemeleri, iki ulti · Holding: beş birim, ilk kamp (seviye 1–4), kervan, baskın ve karşı baskın · Azman Homur ve Azman Bozuk · Köstebek Bey ve Yarasa Yumağı · akış alanlı yol bulma.

**Kabul:**
- [ ] KS 10 gerçek içerikle ve KS 30 stres sahnesi, 8 bot: kare süresi ve ağ bütçesi içinde ([TEKNIK.md](TEKNIK.md) §5.3, §10).
- [ ] Denge simülasyonunda önerilen donanımla ilk denemede geçme oranı %60–80.
- [ ] Her kartın ve her aşının senaryo testi var.
- [ ] **Sen** tek başına üç, arkadaşlarınla bir Kabarma gecesi oynuyorsun: "gece güçlenmesi zevkli" onayı.

### M4: Dünya ve içerik

**Teslim:** Işıl Sazlık, Kızıl Kanyon, Ayaz Yaylası, Göbek · T3–T6 zinciri · kalan 15 yaratık, 13 ekin, 25 istasyon ve makine, otomasyon, drone ve teleferik · sera, Aşı Tezgâhı, Kabartma Odası · Traktör, Kamyon, Biçerdöver, Hava Yastıklı, Zeplin ve 10 eklenti · Kurbağa Ağa, Çıyan Paşa, Buz Tosunu, Ana Maya; Kabarık Atalar; dört Azman; üç Oyuk koruyucusu · ikinci ve üçüncü kamp, Genel Merkez ve Müdür · kalan 7 silah başlığı, 7 güç, 2 ulti · kalan 5 kule ve 4 tuzak · kalan 18 yemek · kalan 24 iş emri zinciri · 11 üye tipi, 72 kozmetik · KS 11 ve üstü, tüzük maddeleri, Yeni Ay · kalan biyomların keşif noktaları.

Her içerik bölümünün hangi taşta geldiği aşağıdaki tabloda.

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

### İçerik dağılımı

[ICERIK.md](ICERIK.md)'deki her bölümün hangi taşta tamamlandığı. M5 ve M6 yeni oynanış içeriği eklemez; M6 replikleri, dilleri ve son ses-müzik hâlini getirir.

| ICERIK bölümü | M1 | M2 | M3 | M4 |
|---|---|---|---|---|
| §1 Biyomlar | Çayır'ın bir köşesi | Altın Çayır, Sis Korusu | — | Sazlık, Kanyon, Ayaz, Göbek |
| §2 Kaynaklar (67) | T1'in çekirdeği | T1–T2'nin tamamı | Maya, Maya Kesesi, Holding Parçası | T3–T6 |
| §3 Yaratıklar (27 + 5) | Gıdak, Zıpzıp | Çayır ve Koru'nun kalan 10 türü | 5 Holding birimi | Sazlık 5, Kanyon 5, Ayaz 4, Göbek 1 |
| §4 Atalar, Azmanlar, koruyucular | — | Koca Pofuduk, Boynuzlu Dede | Azman Homur, Azman Bozuk, Köstebek Bey, Yarasa Yumağı | 4 Ata, 4 Azman, 3 koruyucu, Müdür, Kabarık Atalar |
| §5 Ekinler (24) | Şalgam | Çayır ve Koru'nun 11 ekini | — | 13 ekin, sera |
| §6 Başlıklar ve çipler | 6 iş başlığı, Tırpan, Çivi | Sapan, Saçma, Zıpkın | 24 çip | Alev, Havan, Ark, Tohum Makinalısı, Ayaz Lazeri, Yerçekimi, Maya Topu |
| §7 Güçler ve ultiler | — | — | 5 güç, 2 ulti | 7 güç, 2 ulti |
| §8 Kartlar ve tüzük | 10 kart | — | 72 kart, Nişan | 12 tüzük maddesi |
| §9 Üye tipleri, kozmetik | Çırak | — | — | 11 üye tipi, 72 kozmetik |
| §10 Yapı | Ahşap seti (26 şekil), 6 yerleştirilebilir | Kerpiç-Taş; T1–T2 yerleştirilebilirleri | Sur, sur kapısı, kule tabanı | Çelik Sac, Cam, Mayalı Beton; kalan yerleştirilebilirler |
| §11 İstasyonlar (43) | 5 (Tezgâh, Ocakbaşı, Taş Fırın, Paketleme, Kargo Rampası) | T1–T2'nin 18'i | — | T3–T6 ve otomasyon: 25 |
| §12 Güç, sinyal, taşıma (43) | — | T2 üreticileri, Akü Mk1, kablo, direk, şalter, düğme, basınç plakası | Kabarma Sensörü, siren | Kalanlar |
| §13 Kuleler, tuzaklar | — | — | 3 kule, 4 tuzak | 5 kule, 4 tuzak |
| §14 Araçlar | El Arabası | Çekirge | — | 5 araç, 10 eklenti |
| §15 Yemekler (32) | 3 (Közde Şalgam, Haşlanmış Yumurta, Sebze Çorbası) | T1–T2'nin 14'ü | — | 18 |
| §16 Zırh ve kuşam | — | 3 takım, 6 modül, Çapa Mk.II | — | 4 takım, 12 modül, Çapa Mk.III–VI |
| §17 Keşif noktaları | — | Seyir Kulesi, Ata Taşı, Yabani Bahçe, Yuva, Enkaz | Kadim Taş, Oyuk, Holding Kampı | Zengin Yatak, Genel Merkez, kalan biyomların noktaları |
| §18 İş emirleri (48 zincir) | Çıraklık'ın ilk üç adımı | 12 zincir | Kabarma ile Av ve Atalar: 12 zincir | 24 zincir, başarımlar, katalog ve çark tabloları |
| §19 Ustalık | — | 8 dal, 80 özellik | — | — |

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
