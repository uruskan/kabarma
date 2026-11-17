# gecehud: Rust tarzı arayüz (M1 kararı 20)

Belge: SANAT-SES §8.1 "Görsel dil: Rust tarzı arayüz", GDD §4 başlık değiştirme satırı, URETIM §6 M1 kararı 20.
Başvuru: Hollow-Universe (`Common/Ui.cs`, `App/UiScaler.cs`, `UI/GroundUi.*.cs`), kod uyarlandı; palet sıcak griye çekildi.
Rust'ın kendi varlıkları (ikon, doku, logo) kullanılmadı: ikonlar çizgiyle çizilir, eşya ikonları yer tutucudur.

## Klasörler

| Klasör | İçerik |
|---|---|
| `once_<boy>/` | Değişiklikten önceki arayüz (aynı bot koşusu, aynı anlar) |
| `sonra_<boy>/` | Rust tarzı arayüz |

Boylar: 1920×1080 (pencere), 2560×1440 (tam ekran; pencere görev çubuğuyla 1421'e kırpılıyordu), 1280×720 (pencere).

| Dosya | Ekran |
|---|---|
| `gunduz_pano.png` | Gündüz HUD: kemer, Çapa başlığı yuvası, can/dayanıklılık, pusula, Defter adımı, nişangâh istemi, toplanan eşya |
| `gunduz_pano_defter.png` | Gündüz HUD, Defter adımı değiştiği an |
| `cark.png` | Başlık çarkı (Q basılı) |
| `envanter.png` | Envanter (Tab): kuşam, çanta, kemer, ayrıntı |
| `istasyon.png` | İstasyon paneli envanter şablonunda (yalnız `sonra_`; önceki arayüzde istasyon görüntüsü alınmamıştı) |
| `kabarma_pano_gece1.png` | Kabarma HUD: dalga, Gösteri, Öz, kart şeridi |
| `kart_teklifi_gece1.png` | Kart teklifi |
| `hasat_raporu_gece1.png` | Hasat raporu |

Ölçek: 1600×900 tasarım tuvali; 1080p'de 1, 1440p'de 1,35, 720p'de 0,8 (Sim `UiScale`, `UiTests`). Arayüz gerdirilmez,
her ölçekte gerçek pikselde çizilir. Ctrl + artı / eksi / 0 oyuncu tercihi (`user://ui.cfg`).

Görüntü komutu (her boy için ayrı koşu):

```
--test=bot --fast=3 --quit_after --mute --bot_card_delay=3 --gecehud_shots=ui/sonra_<boy> --hudopen=wheel:150,bag:292,station:297
```

2560×1440 tam ekran koşusunda bot 306 sn sınırına dek son adıma varamadı (`RESULT fail ... step=yapi`). Görüntü koşusu
`--quit_after` ile kesilir. Olası neden (doğrulanmadı): `--hudopen` envanter ve istasyon görüntüsü için botu `yapi`
adımındayken bekletti, ya da bu boyda kare süresi uzun. Bütün görüntüler ondan önce alındı. Oyunun doğruluğu `test.log`'daki
tam koşuda (`tools/test.ps1`) sınanır.

Görsel yargı (Rust'a benzerlik, okunurluk, his) **sahibin onayını bekliyor**.
