# M4 kabul (URETIM §6) — durum

## 1. İçerik adetleri (PLAN §7.1)
`src/Kabarma.Sim.Tests/AcceptanceTests.cs` veri dosyalarından sayar: biyom 6, yaratık 27 + 5 Holding, Ata 6, Azman 6, Oyuk koruyucusu 5, Müdür, ekin 24, Çapa başlığı 6 iş + 12 silah, çip 24 × 4, güç 12 / ulti 4, kart 72, tüzük 12, istasyon ve makine 43, güç/sinyal/taşıma 43, kule 8 / tuzak 8, araç 6 + El Arabası / eklenti 10, yemek 32, zırh 7 / modül 18, keşif türü 9, borsa 12, Ustalık 8 dal / 80, yerleştirilebilir 72 (+Ocak), yapı kiti 26 şekil + cam.
**Henüz sayılamayan (akışlar birleşmedi):** üye tipi 12 ve kozmetik 72 (dolap), iş emri 240 ve başarım 50 (ilerleme4). Yaratık "16'sı evcilleşir" için `husbandry.json` bugün 8 tür taşır (hayvan akışı).
**Kaynak 67:** ICERIK §2 satırlarının hepsinin veri karşılığı yok: `zehir_kesesi` (Kıvrık, Tükürgen düşüşü) için yaratık düşüş tablosu yok; kitin yalnız Azman Kıskaç ödülünde.

## 2. RecipeGraph
T6'da ulaşılamayan tarif 0, Kabartma Odası ulaşılıyor (zincir4 testi). Bu akış: `research.json` `pending:*` kaynakları kalktı (`node:<biyom>`, `ata:ana_maya`, `azman:azman_kiskac`, `mayavault`); her biri bir testle gerçek veriye bağlı. Ana Maya artık `kabartma_plani` verir (daha önce hiçbir şey vermiyordu: Kabartma Odası oyunda kilitli kalıyordu). Dört yeni Ata'nın arenası Ata Taşı'nda, GECİCİ işaretleri kalktı; yeni biyomlarda Oyuk noktaları (3 × 3) eklendi.

## 3. Ana Maya botu (`--test=anamaya`)
İlk gün zinciri sonra altı biyomu 3× zamanla yürür (3339 m, kopma yok): Koru Demir, Sazlık Turba, Kanyon Kum, Ayaz Kristali, Göbek Ham Maya gerçek Kazma ile kazıldı; Ana Maya Ata Taşı'na varıldı. **Gevşetmeler:** zaman 3×; yaratıkla dövüşülmez, can 40 altına inince yenilenir (bu koşuda 0 kez); **Ata dövüşleri, T2–T6 üretim zinciri ve Kabarma geceleri bu botta yok** (kendi testleri: boss, ata4, zincir, kabarma). Başlangıçtan Ana Maya'ya tam zincirli (eşyasız) bot yazılmadı.

## 4. Görüntüler
`progress/m4/kabul/`: gecis, koru, sazlik, kanyon, ayaz, gobek × öğle, gün batımı, Kabarma gecesi (18 görüntü). Ölçütler (SANAT-SES §9 görsel puanları) **sahibin onayını bekliyor**.

## 5. Sahibin 30 dakikası
Sahibin onayını bekliyor.
