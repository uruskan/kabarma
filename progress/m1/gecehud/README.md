# gecehud kanıtları (M1)

Görüntüler olay anında alınır (`--gecehud_shots`, `Director.Calendar.cs` `DebugShot`). Yeniden üretmek için (pencereli, ~3 dk):

```
& $G --path game -- --test=bot --fast=3 --quit_after=520 --mute --bot_card_delay=3 --gecehud_shots --hudopen=bag:292,defter:297 --replay_at=303 --clock=0.718
```

- `--bot_card_delay=3`: bot kart şeridini 3 oyun saniyesi açık tutar (yalnız görüntü için; varsayılan 0,6).
- `--hudopen`: envanteri ve Defter'i belirtilen saniyede açar. `--replay_at`: F1 düğmesinin yaptığını yapar. `--clock`: takvim başlayınca saati günün o kesrine alır.
- Rapor görüntüsünde bot görüntü alınana dek durur (raporu ertesi karede kapatmasın); oyun durumuna dokunulmaz.

| Dosya | Ne gösterir |
|---|---|
| `gunduz_pano.png` | Gündüz panosu: üstte pusula (Ocak ◆ ve etkin hedef ▲, uzaklıkla), altında etkin adımın tek satırı; sol altta can (altında dayanıklılık çubuğunun yeri ayrıldı, çubuğu oyuncu akışı çizer); altta kemer. Defter ilk geceden önce kapalı: iş emri ilerlemesi sessizce sayılır. |
| `kart_teklifi_gece1.png` | İlk kart teklifi: Çatal her zaman ilk sırada; [Z]/[X]/[C], nadirlik, rütbe noktaları, 20 sn otomatik seçim çubuğu. |
| `kabarma_pano_gece1.png` | Kabarma panosu (SANAT-SES 8.1): dalga sayacı ve noktaları, Gösteri ×, Öz çubuğu, seviye satırı, kart şeridi. |
| `hasat_raporu_gece1.png` | İlk gecenin Hasat Raporu (kabarık ürün satıldıktan sonra, PLAN 1.4): alanlar, şafakta sönen kartlar, "Defter açıldı" ve sıradaki iş emri. |
| `normal_gece.png` | Takvimin ilk normal gecesi (Gün 2 akşamı): "GECE", Kabarma yok, uyku ipucu. Işık yer tutucudur: gün batımı tonunda kalır (normal gece ışığı dunya akışının). |
| `gunduz_pano_defter.png` | Defter açıldıktan sonra adım satırının altında Defter takibi (Şalgam Baronu 1/5). |
| `envanter.png` | Envanter ızgarası: çanta, bilgi bölmesi, kemer satırı (1–8 ile atanır), 3 yemek yuvası, Kredi. |
| `defter.png` | Defter'in ilk sayfası: gün ve gün parçası, iş emri sayıları, açık zincirler ve adımları, zincir ödülleri. |
| `kart_teklifi_gece2.png`, `kabarma_pano_gece2.png` | F1'den yeniden başlatılan Küçük Kabarma (saat durur). |
| `hasat_raporu_gece2.png` | Yeniden oynanan gecenin raporu, şafakta (GDD 14.1). Bot senaryoyu bitirdiği için bu gecede ateş etmez: kabarıkları Gıdak'ın tekmesi söndürür, bu yüzden "Verilen hasar" 0'dır (oyuncu hasarı sayılır). |

Öz (URETIM 6 M1 kararı 19): öldürülen kabarığın yerine mor tane olarak düşer, 4 m içinde toplanır (Mıknatıs ×1,4), dalga sonunda ve şafakta kalanı oyuncuya uçar. Kabarma panosunda "yerde N Öz" yazar. Bot yürüyerek toplar (bot koşusunda 21 Öz'ün 18'i elle, 3'ü dalga sonunda).

Gösteri bütün görüntülerde ×1,0: bot ilk gecede kabarıkları tek tek söndürüyor, 3 saniyede 5 öldürme serisine ulaşmıyor. Kural Sim testinde sınanıyor (`OrdersTests.Gosteri_rises_on_a_kill_streak...`).

`test.log`: `tools/test.ps1` tam koşusu (derleme, Sim testleri, bot, inşa, gece testi). Gece testi kol yolunu gerçek `InputEventJoypadButton` olaylarıyla sınar (d-pad, LB/RB, X). Kolun A/B düğmeleri, `bag` ve `card_1..3` bu dalda bağlı değil (girdi akışı).
