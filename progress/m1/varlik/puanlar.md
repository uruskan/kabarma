# Ölçüt 6–8 puanları (varlık akışı, 18 Kasım 2026)

SANAT-SES §9: "son üçünü görüntüye bakarak puanlarım (10 üzerinden, geçme 8)". Puanları bu oturum verdi; **hepsi sahibin onayını bekliyor**.
Bir varlık sekiz ölçütten de geçse bile `game/data/assets.json`'da durumu `onay_bekliyor` olur; `altin` yalnız sahip onaylayınca (`ownerApproved: true`).

Kanıt:
- 6 Siluet: `kart/<ad>.png` sol alt, 64 px siyah siluet (ikon açısı: önden 35°, üstten). "Doğru sınıfla eşleşiyor mu": referans föyündeki aynı sınıf varlıklarla.
- 7 Dil uyumu: `docs/stil/altin_varliklar_4isik.png` (SANAT-SES §2'nin dört saati, yan yana) ve `docs/stil/altin_varliklar.png` (stüdyo).
- 8 Okunaklılık: `oyun/` altındaki oyun içi görüntüler (bot oyunu, `--test=bot --shots=...`, oyun kamerası; her dosyanın adı varlık ve oyun saniyesi). El Arabası oyunda yok, puanlanmadı. Bot kadrajı rastgele: dunya akışının yer imi görüntüleri (SANAT-SES §9, 16 görüntü) gelince yeniden bakılmalı.
- Siluet açısı belgede yazmıyor; ikon açısı kullanıldı. Kova ve Çapa yandan bakınca daha iyi okunur: açıyı sahip seçerse puan değişebilir.

| Varlık | 1 doku | 2 poligon | 3 kalınlık | 4 bütünlük | 5 ölçek | 6 siluet | 7 dil uyumu | 8 okunaklılık | Durum |
|---|---|---|---|---|---|---|---|---|---|
| Çayır ağacı | geçti | geçti | **kaldı** | geçti | geçti | 9/10 | 8/10 | 9/10 | yer tutucu |
| Cevherli kaya | geçti | geçti | geçti | geçti | geçti | 8/10 | 8/10 | 8/10 | sahibin onayını bekliyor |
| Sulama kovası | geçti | geçti | geçti | geçti | geçti | **7/10** | 8/10 | 8/10 | yer tutucu |
| Şalgam (ekin ve kabarık ürün) | geçti | geçti | geçti | geçti | geçti | 9/10 | 9/10 | 8/10 | sahibin onayını bekliyor |
| Tezgâh | geçti | geçti | geçti | geçti | geçti | **7/10** | 8/10 | 8/10 | yer tutucu |
| Taş Fırın | geçti | geçti | geçti | geçti | geçti | 8/10 | 8/10 | 8/10 | sahibin onayını bekliyor |
| El Arabası | geçti | geçti | **kaldı** | geçti | geçti | 9/10 | **6/10** | puanlanmadı | yer tutucu |
| Ocak | geçti | geçti | geçti | geçti | geçti | 8/10 | **7/10** | **7/10** | yer tutucu |
| İniş kapsülü (Kargo Rampası) | geçti | geçti | geçti | geçti | geçti | 8/10 | **7/10** | 8/10 | yer tutucu |
| Paketleme Tezgâhı | geçti | geçti | geçti | geçti | geçti | 8/10 | 8/10 | **7/10** | yer tutucu |
| Çapa | geçti | geçti | **kaldı** | geçti | geçti | **6/10** | 8/10 | **7/10** | yer tutucu |

**Ölçüt 3 yorumu:** "geçti" sütunu levhaları (yaprak, kapak, pano) saymayan, çubuk/diken alanına %2 pay veren ölçüye göre. Levhaları da sayan harfiyen okumada Şalgam (levha %41) ve Taş Fırın (%10) da kalır; sekiz ölçütten geçen yalnız Kaya olur. Bu yorum kod yazılmadan önce sorulmadı; sahip onaylamalı.

## Gözlemler

- **El Arabası**: siluet çok iyi, ama ince boru çerçeve ve gerçekçi metal tekne diğerlerinin "kalın, yumuşak, el boyaması" dilinden ayrılıyor (ölçüt 3 ile aynı kök). Oyunda yok: ölçüt 8 puanlanamaz.
- **Ocak**: değer aralığı çok koyu (dokunun %48'i `dark_grey`, %34'ü `shadow`); gün batımında ve Kabarma gecesinde neredeyse siyah bir kütle. SANAT-SES §9 ekran görüntüsü ölçütü "saf siyah bölge yok" ile çatışma riski. Ateş ağzı gündüz bile çoğu açıdan görünmüyor, işlevi bacadan okunuyor.
- **Kapsül**: kapak penceresinin içinde kaynağın kendi açık renkli levhaları (yırtık kâğıt gibi). Onarımın dolgusu değil (denendi: `--dark_fill`).
- **Sulama kovası**: 64 px silueti kutu ve kısa emzik; sap dolu kütleye karışıyor. Yan siluette okunur, ikon açısında zayıf.
- **Tezgâh**: 64 px silueti koltuk gibi (yüksek arka pano + oturak). Mengene ve aletler ancak yakından.
- **Çapa**: ikon açısında siluet yumru; yandan bakınca tabanca biçimi net. Oyunda elde küçük; işlevini başlık adı (HUD) söylüyor.
- **Şalgam, kaya, ağaç**: dört ışıkta da en tutarlı üçlü; ağaç oyunda Biçme hedefi olarak çok net.
