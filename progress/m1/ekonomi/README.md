# M1 ekonomi akışı: kanıt

Dal `claude/m1-ekonomi-cd7c7a`. Kredi harcanmadı; modeller kodla ve Blender betiğiyle (`pipeline/build_placeables.py`).

| Dosya | Ne |
|---|---|
| `test.log` | `tools/test.ps1 -BotTimeout 900 -Log progress/m1/ekonomi/test.log`: derleme, Sim, bot, buildtest, ekonomi |
| `ekonomi_test.log` | `--test=ekonomi` (game/scripts/tests/EconomyTest.cs): istasyonlar, El Arabası, kasa ve rampa, söküm ve onarım, uyku ve yemek |
| `buildtest.log` | `--test=build` özeti: 26 şeklin (28 parça) hepsi oturuyor (`SHAPES placed=28/28`) |
| `bot_chain.log` | Bot zincirinin ekonomi satırları: Tezgâh → Ocakbaşı ve Sebze Çorbası → Taş Fırın → Bakır Külçe → Çivi → Paketleme → kasa → rampa → 60 sn sonra ödeme |
| `*.png` | Başlıklı koşu ekran görüntüleri (varsa): `--test=ekonomi` pencereli çalışınca yazılır |

Sahibin yargısına kalanlar (his, dinleme, 30 dk oynama, RTX 2070S FPS) burada ölçülmedi.
