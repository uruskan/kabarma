# zincir (M2) kanıtı

- `zincir_test.log`: `--test=zincir`. Bot ilk günü Tezgâh'a kadar oynar. Sonra Taş Fırın'ı kurar, 4 Bakır Külçe eritir ve 8 Bakır Tel çeker. Araştırma Masası'nı kurar, Tarayıcı ile 4 tür tarar (27 Veri). Dokuma Tezgâhı'nı araştırır, kurar ve Kumaş dokur. Sonuç 8/8 OK, oyun içi süre yaklaşık 200 sn.
- `zincir_istasyonlar.png`: Araştırma Masası ve Dokuma Tezgâhı (koddan yer tutucu). Sağ altta toplanan eşyalar görünür, Kumaş dahil.
- `zincir_arastirma_paneli.png`: Üretim paneli, Araştırma kategorisi. Satırlarda bedel ve öncül düğüm yazar. Düğme "VERİ 2/25", solda Veri, Ocak basamağı ve tapudaki Sandık sayısı var.
- `zincir_dokuma_menusu.png`: Dokuma Tezgâhı'nın E menüsü (Kumaş: 3 Lif).
- `test_full.log`: commit öncesi tam `tools/test.ps1` koşusu. Kalan üç kırmızı başka akışlardan:
  - `ekonomi`: Borsa çarpanı, ekonomi2.
  - `input_kbm` ve `input_pad`: `civi` adımında takılma.
    - Görünen düzenek: bot Taş Fırın'da E'ye basıyor, menü daha yakın olan Tezgâh'ınki açılıyor. 0. satır Çivi oluyor, külçe yetmiyor, döngü sürüyor.
    - zincir'den önceki koşular da aynı biçimde `firin` adımında takılıyordu.
    - Sorumluluk doğrulanmadı: boss, gecehud ve ilerleme dalları zincir'in ilk commit'ini de içeriyor.
  - `world`: zincir düzeltti, ayrı koşuda yeşil.
