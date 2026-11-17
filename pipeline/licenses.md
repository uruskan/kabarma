# Ses varlıklarının kaynak ve lisans kaydı

URETIM §9 "Servis şartları": bütün varlıklar ücretli planlarda üretilir (ticari kullanım hakkı için). Her servis için üretim tarihi ve plan kaydı burada tutulur. Bu dosya ses varlıklarını kapsar (ses akışı, M1). Model ve görseller kendi akışlarında eklenir.

Her üretimden sonra bu tabloya bir satır eklenir. `pipeline/eleven.py` her çağrıyı ayrıca `progress/m1/ses/eleven_ledger.json` defterine yazar (tür, miktar, tarih, çıktı yolu). Anahtar hiçbir yere yazılmaz.

## Servisler ve şartları

| Kaynak | Ne üretti | Plan | Ticari kullanım | Yükümlülük | Doğrulama |
|---|---|---|---|---|---|
| Genex (`@genex-ai/cli-demo` 1.43.4, ses kanalı) | M0'ın 12 sesi ve 1 Kabarma müziği | Sahibin Genex hesabı, kredili (1 kredi = 0,01 $) | Genex aracının kendi belgesi: indirilen dosyalar "yours to ship" (`.claude/skills/genex-tool-audio/SKILL.md`) | Genex'in arkasındaki ses sağlayıcısının şartları Genex sözleşmesinde. Belgede ayrıntı yok | ⚠️ Genex kullanım şartlarının ses bölümü okunmadı. Çıkıştan önce okunacak |
| ElevenLabs API (doğrudan, sahibin anahtarı) | M1: 55 ses efekti × 3 sürüm, 2 müzik (çayır gündüzü; Kabarma ve 3 katmanı), Suna'nın sesi (tasarım) ve 30 TR + 30 EN replik | Üretim günü hangi plandaysa o satıra yazılır. URETIM §9'a göre ücretli plan | Ücretli planlarda ticari kullanım. Müzik Free–Pro planlarda "bireysel kullanım" diye geçiyor (URETIM §2.1 madde 3) | **Müzik:** oyun geliri 500.000 $'ı (ya da geliştiricinin yıllık oyun geliri 1.000.000 $'ı) aşınca 60 gün içinde stüdyo planına geçilir; eşik çıkış günü kontrol listesinde (URETIM §9) | ⚠️ Şart metni ElevenLabs'in şartlar sayfasından üretim günü yeniden okunup tarihiyle buraya yazılacak |
| `pipeline/synth_ui.py` (kendi kodumuz, Python standart kütüphanesi) | 5 arayüz sesi × 3 sürüm: `ui_odak`, `ui_onay`, `ui_hata`, `ui_baslik_carki`, `telsiz_mandali` | Yok (kredi yok) | Projenin kendi eseri; üçüncü taraf örnek ya da kayıt kullanılmadı | Yok | ✅ Yöntem sahibin Oduncu projesindeki `tools/sfx/gen_sfx.py` ile aynı; kod Kabarma için yeniden yazıldı |
| ffmpeg 9.0.2 (gyan.dev derlemesi) | Kırpma, düzey, kodlama (libmp3lame), ölçüm | — | Araç olarak kullanılır, oyunla dağıtılmaz. Çıktı dosyaları ffmpeg lisansına bağlı değildir | Yok | — |

## Üretim kayıtları

| Tarih | Kaynak ve plan | Dosyalar | Miktar | Not |
|---|---|---|---|---|
| 17 Kasım 2026 | Genex (MVP) | `game/assets/audio/sfx/{beam_loop,pickup,place,till,water,swell,pop,nailgun,hit,levelup,siren,rocket}.mp3`, `game/assets/audio/music/kabarma_night.mp3` | 51 Genex kredisi (12 × 2 + 27) | Ayrıntı ve üretim kimlikleri: `game/assets/audio/README.md`. M1'de bu 12 ses `audio.json`'da `m0` yedeği; yerlerine 3'er ElevenLabs sürümü gelecek |
| 17 Kasım 2026 | `pipeline/synth_ui.py` (kredi yok) | `game/assets/audio/sfx/{ui_odak,ui_onay,ui_hata,ui_baslik_carki,telsiz_mandali}_{1,2,3}.mp3` | 15 dosya | Betikli denetimden geçti (`progress/m1/ses/audio_qc.md`). Dinleme onayı sahipte; onaylanana dek `placeholder: true` |
| — | ElevenLabs | — | — | Henüz üretim yok. İstek: `pipeline/requests/m1_audio.json`. Kuru koşu tahmini: `progress/m1/ses/eleven_dry_run.txt` |

## AI beyanı (URETIM §9)

Steam içerik anketinde: ses efektleri, müzik ve Suna'nın seslendirmesi önceden üretilmiştir (Genex, ElevenLabs). Arayüz sesleri kodla sentezlenmiştir. Oyun sırasında çalışan üretken model yoktur.
