# guc akışı (M2): güç ağı ve temel sinyal

Kanıtlar:

- `guc_gunduz.png`, `guc_yakin.png`, `guc_gece.png`: `--test=powershots` (pencereli). Rüzgâr Gülü → Direk → Şalter → Elektrik Lambası, Direk → ikinci lamba, Hayvan Çarkı → Direk; mavi ince teller sinyal: Basınç Plakası → Şalter, Düğme → lamba. Gece görüntüsünde iki lamba yanıyor (plaka basılı, düğmeye basıldı).
- `guc_panel.png`: lambaya E: ayar paneli (Kapat, Öncelik 1–5, Kablolarını sök, Sök; altında ağ dengesi).
- `guc_kablo.png`: Kablo elde, Direk soketi seçili, Basınç Plakası'na nişan: önizleme kırmızı, ipucu nedeni yazıyor ("Güç soketi yok (sinyal teli için R)"), uzunluk 6.4/12 m. Kablo ikonu yer tutucu (yazı).
- `power_bot.log`: `--test=power` bot koşusunun güç bölümü (ilk gün zinciri bittikten sonra). Bot oyun yolundan: bakır kazar, külçe eritir, Bakır Tel, Kablo, Rüzgâr Gülü, Elektrik Lambası, Düğme üretir; kemerden kurar; Kablo eldeyken soketlere sol tıkla kablo çeker (lamba yanar), R ile sinyal teline geçip Düğme'yi lambaya bağlar (lamba söner), Düğme'ye E basar (lamba yanar).
- Sim: `src/Kabarma.Sim.Tests/PowerTests.cs` (13 test: veri, rüzgâr, 12 m ve soket sınırı, öncelikle kapanma, istasyon talebi, akü dolma/boşalma, şalter ağı böler, düğme ve plaka sinyali, Hayvan Çarkı, JSON gidiş-dönüş, oyun saati).
