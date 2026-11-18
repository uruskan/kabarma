# kartlar (M3) kanıtı

- `kartlar_test.log`: `--test=kartlar`, pencereli koşu, sonuç 13/13 OK.
  - Bu bir gerçek gecedir; tuşlar gerçek girdi olarak basılır.
  - U ile yenileme, K + X ile sürgün, Z ile seçim yapılır.
  - Sınama (bot değil) üç kartı Grant ile verir ve oyundaki karşılıklarını ölçer: Yumurta Topu düşmana vurur, Kabuk hasarı emer, Cam Top can tavanını 100'den 60'a indirir.
  - Şafakta kartlar söner ve can tavanı tabana döner.
- `kart_teklifi_surgun.png`: sürgün kipindeki kart teklifi. Görünenler:
  - üst şerit: yenileme hakkı (1), sürgün kipi, yuva (0/12);
  - kartların sınıf ve ekip etiketi;
  - kırmızı çerçeve: sürgün edilebilir kartlar.
- `kart_defteri.png`: Kart Defteri. 24/72 kart açık (20 baştan açık kart ve ebeveynleri baştan açık 4 aşı), sınıf başına sayılar, jeton.
- `test_full.log`: tam `tools/test.ps1`. Üç adım kırmızı, sorumluluğu doğrulanmadı:
  - `world`: Sis Korusu süsleri, ağaçlar ve Seyir Kulesi modeli (dunya2 içeriği).
  - `save`: bot ilk günü 1500 sn'de bitirmedi; `gidak` adımında kaldı. Aynı koşuda `bot` adımı 239 sn'de geçti; makinede ~15 Godot vardı.
  - `input_pad`: `barinak` adımında takılma. Oyun kolu botu daha önce de takılıyordu.
- Kartlar Sim'de 72 senaryo testiyle denetlenir (`CardScenarioTests.cs`), kurallar ayrıca `CardRulesTests.cs`'te.
- Aşı ve 5. rütbe ilk gecenin 3 seviyesiyle dürüstçe oyunda görülemez. KS gecelerini kabarma3 akışı getiriyor.
