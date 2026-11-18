# holding (M3) kanıtı: Bereket Holding

Bu klasör `--test=holding --fast=2` testinin pencereli koşusundan çıktı. Sonuç: `HOLDING RESULT ok=27 fail=0`.

- `kamp_gunduz.png`: ilk gerçek Kabarma'nın ertesi sabahı kurulan 1. seviye kamp.
  - Silo, Atölye, Ahır ve Şube Çekirdeği görünür.
  - Gündüz Perde'si ve tabela da görünür.
  - Ocak'tan uzaklık 256 m.
- `kervan.png`: kervan aracı ve iki bekçi depoya giderken, pusudan hemen önce.
- `baskin.png`: Holding'in gece baskın ekibi tapuda. Taşıyıcı kabarık ürünü kapmış.
- `kamp_yikik.png`: Silo, Atölye, Ahır ve Çekirdek düşmüş, kamp yıkık. 3 gün sonra yeniden kurulur.

## Testin denetledikleri

- **Kuruluş:** kamp ilk gerçek Kabarma'dan önce yoktur. Ertesi sabah, Ocak'tan en az 250 m uzakta kurulur.
- **Perde:** gündüz oyuncuyu dışarı iter ve mermiyi durdurur.
- **Kervan:** sabah 0,27'de yola çıkar ve depoya doğru ilerler.
- **Pusu:** önce korumalar düşer, sonra araç. Ganimet (bakır cevheri, taş, Kredi) çantaya geçer ve Husumet +15 olur.
- **Onların baskını (gece, Husumet 60):**
  - Perde iner, ekip gelir ve tapuya girer; Husumet −40 olur.
  - Taşıyıcı ürünü kapar. Taşıyıcı düşerse ürün yere düşer.
  - Baskıncı oyuncuya ateş eder.
- **Senin baskının:**
  - Çekirdek, öteki binalar ayaktayken kilitlidir. İlk isabet Husumet +25 yapar.
  - Ganimet: Silo'dan Kredi, Atölye'den Holding Planı ve Holding Parçası, Ahır'dan nadir hayvan (depoya).
  - Çekirdek düşünce kamp yıkılır ve birimler kalkar.
- **Şafak:** Perde kalkar, baskın dağılır, yıkık kampın yeniden kurulacağı gün yazılır.

## Sınırlar

- **Test kısayolları:** Sim olayları doğrudan çağrılır: gece ve şafak, kuruluş sabahı, kervanın Perde dışına taşınması. Mermiler gerçek girişten gelir (`Director.HoldingProjectileHit`).
- **Tarla yolu Godot'da sınanmadı:** testteki ürün yerde duran bir kabarık üründür. Tarladan söküp kapma (`PlotView.Lift`) aynı kodla çalışır ama bu testte denenmedi.
- **Yer tutucular:** birimler oyuncu modelinin üstüne kırmızı boyayla çizilir. Binalar ve kervan aracı MeshKit gövdeleridir. Gerçek modeller üretim isteğinde.
