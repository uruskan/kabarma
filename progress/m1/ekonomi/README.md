# M1 ekonomi akışı: kanıt

Dal `claude/m1-ekonomi-cd7c7a`. Kredi harcanmadı; modeller kodla ve Blender betiğiyle (`pipeline/build_placeables.py`).

| Dosya | Ne |
|---|---|
| `test.log` | `tools/test.ps1 -BotTimeout 900 -Log progress/m1/ekonomi/test.log`: derleme, Sim, bot, buildtest, ekonomi |
| `ekonomi_test.log` | `--test=ekonomi` (game/scripts/tests/EconomyTest.cs): istasyonlar, El Arabası, kasa ve rampa, söküm ve onarım, uyku ve yemek |
| `buildtest.log` | `--test=build` satırları: 26 şeklin (28 parça) hepsi oturuyor (`SHAPES placed=28/28`) |
| `bot_chain.log` | Bot zincirinin ekonomi satırları: Tezgâh → Ocakbaşı ve Sebze Çorbası → Taş Fırın → Bakır Külçe → Çivi → Paketleme → kasa → rampa → 60 sn sonra ödeme |
| `ekonomi_istasyonlar.png` | Tezgâh, Ocakbaşı, Taş Fırın, Paketleme Tezgâhı, Sandık, Meşale (pencereli `--test=ekonomi`) |
| `ekonomi_el_arabasi.png` | El Arabası, kasasında kargo kasası |
| `ekonomi_rampa.png` | Kargo Rampası'nda 6 kasa (2 × 3) |
| `ekonomi_uyku_tulumu.png` | Uyku Tulumu: doğma noktası ve uyku |
| `build_all_shapes.png` | Ahşap yapı setinin yeni şekilleri (Makine Platformu 3 boy, Kule Tabanı, Sur, Üçgen Temel, Kazık Temel, çatı köşesi …) |

Sahibin yargısına kalanlar (his, dinleme, 30 dk oynama, RTX 2070S FPS, görsel ölçüt) burada ölçülmedi. Video yok.
