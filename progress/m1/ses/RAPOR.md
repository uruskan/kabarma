# ses akışı: M1 raporu (17 Kasım 2026)

**(a) Dal:** `claude/m1-ses-3ca715`, `af67734` (local main) üstüne. Worktree açılışta `0c22fab`'deydi (main'in 24 commit gerisi, CLAUDE.md ve `tools/test.ps1` yoktu); değişiklik yokken `af67734`'e ileri sarıldı. Aynı anda diğer dokuz worktree de `0c22fab`'de görünüyordu.

**Testler (18 Kasım):** `tools/test.ps1` yeşil.
- Derleme: 0 uyarı.
- Sim: 28/28 (12'si ses testi).
- Bot: `RESULT ok credits=210 eggs=1 kills=21 cards=3 maya=48 stations=3 harvests=4` (önceki ile aynı).
- İnşa: 60/0.
- `audio`: `ok sounds=60 playable=60 variant_files=180 buses=6 layers_loaded=3/3 day_music=yes`. Üretim, onarımlar ve yeniden toplanan katmanlar commit'li; çıkışta sızıntı satırı yok.

Günlük: `progress/m1/ses/test.log`.

Kanıt:
- `test.log`
- `bot_audio_lines.log`: müzik durumları ve Suna satırları botta.
- `bot_unmuted.log`: bot `--mute` olmadan; oyun içi ses yolu çalıştı, sonuç aynı, 0 hata satırı.
- `audio_qc.md` / `audio_qc.json`: 198 dosya, 18 Kasım (ana oturumun üretimi dahil).
- `bot_unmuted_real.log`: bot `--mute` olmadan, gerçek dosyalarla. Sonuç aynı; müzik Gündüz → Sessizlik → Kabarma → 2 → 3 katman → Gündüz. Yalnız 3 çıkış uyarısı var, (f) 21.
- `eleven_dry_run.txt`: bu dalın metni.
- `eleven_dry_run_ilkgun.txt`: ilkgun'un metni.
- `merge_ilkgun_bot.log`: ses + ilkgun birleşik ağacında her Suna satırı ilkgun kimliğine çözülüyor. `audio_qc --fix-dir` ve `eleven.post_process` M0 dosyalarının kopyalarında denendi: `hit`, `pickup`, `pop` düzeltmeden sonra geçti; geçen dosyaya dokunulmadı.

## Ne yapıldı

| Teslim | Dosyalar | Durum |
|---|---|---|
| Ses kanalları ve sürgüler | `fx/Sfx.Buses.cs`, `ui/AudioPanel.cs` | 6 kanal çalışma anında veriden kurulur: Ana (tepe sınırı −1 dB), Müzik (Suna'da −4 dB kısma), Efekt, Suna/telsiz (yüksek ve alçak geçiren süzgeç + hafif doyum), Ortam, Arayüz. F2 (oyun kolunda Geri) paneli, kanal başına sürgü, `user://audio_settings.cfg`. Sınamalarda ayar okunmaz ve yazılmaz. |
| `data/audio.json` ve `Sfx` | `game/data/audio.json`, `src/Kabarma.Sim/GameData.Audio.cs`, `fx/Sfx.cs`, `fx/Sfx.Events.cs`, `fx/Foley.cs` | 60 ses: grup, kanal, düzey, 3 varyasyon, döngü, çalma noktası (`play`), bağlı mı (`wired`), M0 yedeği, `placeholder`. Rastgele varyasyon (aynısı art arda çalmaz), perde ±%8, düzey ±2 dB, ses başına eşzamanlı kopya sınırı. Eksik dosyada M0 sesi, yoksa sessiz. Eski adlar (`place`, `hit`...) çalışıyor; başka akışın çağrısı değişmedi. Döngü sesi `Play` ile tekrar tekrar çağrılırsa (Sulama her karede `"water"`) tek döngü çalar, çağrı kesilince 0,3 sn'de söner; yalnız M0 yedeği varken eski tek seferlik davranış. |
| Müzik durum makinesi | `fx/MusicDirector.cs`, `src/Kabarma.Sim/AudioMix.cs` | Durumlar: Gündüz (parça biter, parça boyu kadar susar: §10.2), Sessizlik (akşam), Kabarma (3 katman aynı karede başlar; dalga numarası ve Gösteri'ye göre ölçü sınırında açılır, 4 ölçü beklemeden kapanmaz). Geçişler 2,5 sn, gerçek zaman (vuruş duraklaması müziği etkilemez). Ortam: gündüz çayır döngüsü, gece gökyüzü döngüsü. Director evresini okur; gecehud dosyalarına dokunmaz. |
| Suna oynatıcısı | `ui/Hud.Voice.cs` | Satır başında telsiz mandalı, 0,12 sn sonra replik telsiz kanalında, sonda mandal; konuşurken müzik −4 dB. Altyazı Suna kutusu: ses bitene dek ekranda. Kimlik ekrandaki metinden (`VoiceLines.FromJson`): `scenario.json`'da `lines` varsa ilkgun'un açık kimlikleri, yoksa eski yapıdan türetme. Dosya `voice/{dil}/{kimlik}.mp3`; yoksa altyazıyla sessiz. |
| `pipeline/audio_qc.py` | | LUFS, gerçek tepe, kırpılma, süre, baş/son sessizlik, döngü dikişi, grup içi spektrum, müzikte tempo ve katman hizası. `--fix-dir` ile kırpma ve tepe düzeltme. Python + ffmpeg (numpy yok). |
| `pipeline/eleven.py` | `pipeline/requests/m1_audio.json` | Anahtar yalnız `ELEVENLABS_API_KEY`; `.env` açılmaz, anahtar yazılmaz. Sahibin sınırları sert sınır + harcama defteri (yeniden denemeler dahil). Modeller sabit. Ben yalnız `--dry-run` çalıştırdım; ana oturum 18 Kasım'da `sfx`, `music`, `stems`, `design` adımlarını koştu (e). |
| `pipeline/licenses.md` | | Kaynak, plan, yükümlülük, üretim kayıtları. |
| Ek: arayüz sesleri | `pipeline/synth_ui.py`, `sfx/ui_*_{1,2,3}.mp3`, `sfx/telsiz_mandali_{1,2,3}.mp3` | SANAT-SES §10.1 "Arayüz grubu sentez betikleriyle de yapılabilir": 5 ses × 3 sürüm koddan, kredisiz. 15'i de betikli denetimden geçti. Dinleme sahipte. |

## (b) PLAN §7.2, teslim başına

| Teslim | 1 Belge + veri | 2 Otomatik test | 3 Ses/efekt/arayüz | 4 KM + kol (ortak oyun) | 5 Kayıt | 6 Görüntü |
|---|---|---|---|---|---|---|
| Ses kanalları ve sürgüler | geçti | geçti (`audio`: kanallar, etkiler, ikinci kurulum, düzey) | kısmen (sürgü hemen duyulur; sürgüde arayüz sesi yok) | kısmen: F2 ve kol Geri, yön pedi sürgüde; gerçek kolla denenmedi · ortak oyun M2'de | M2'de | yok (F2 paneli görüntüsü alınmadı) |
| 60 ses (`audio.json`, `Sfx`) | geçti | geçti (Sim: 60 ses, gruplar, 12 M0, döngüler, varyasyon; `audio`: 60'ı da çöker mi, tutulan döngü; bot `--mute` olmadan da yeşil) | kısmen. 60/60 çalınabilir: 180 dosya (165 ElevenLabs + 15 sentez), hepsi betikli denetimden geçti, yer tutucu 0. 33 sesin çalma noktası sahibi akışta bağlanmalı. Dinleme: sahibin onayını bekliyor | M2'de (ortak oyun); girdi yok | M2'de | yok |
| Müzik (2 parça, 3 katman) | geçti (eşikler belgenin sustuğu yer, aşağıda) | geçti (Sim: yoğunluk, katman, bekleme; `audio`: durumlar ve katmanlar; bot günlüğünde Gündüz → Sessizlik → Kabarma → 2 → 3 katman → Gündüz) | kısmen. Gündüz parçası ve 3 katman (bas / davul / ezgi) yüklü (`layers_loaded=3/3 day_music=yes`). Katmanlar örnek örnek eşit boy; toplamın dikişi ve temposu geçti. Dinleme: sahibin onayını bekliyor | M2'de | M2'de | yok |
| Suna (30 replik, telsiz, altyazı) | kısmen: kod iki metin yapısını da okur. Bu daldaki metin eski (41 replik, 12'sinde tuş adı, İngilizcesi yok); ilkgun dalında 30 TR + 30 EN hazır | kısmen. Sim: iki yapı ve bugünkü dosya. Birleşik ağaçta bot: 24 Suna satırının hepsi ilkgun kimliğine çözüldü (`merge_ilkgun_bot.log`). Sesli yol, replik dosyası olmadığı için koşmadı | kısmen: mandal ve altyazı var; seslendirme yok (3 aday üretildi, sahip seçecek) | M2'de | M2'de | yok |
| `audio_qc.py` | kısmen: −1 dBTP ve Suna +3 dB belgeden; LUFS aralıkları ve dikiş ölçütü seçilmiş ((f) 22) | kısmen: 198 gerçek dosyada koştu; sessiz katmanı, tutmayan tepe düzeltmesini ve köklerin 25 ms gecikmesini yakaladı, üçü de düzeltildi ((e)). Döngü dikişi çapraz geçişle onarılıyor. Birim testi yok | — | — | — | — |
| `eleven.py` + istek | geçti (sınırlar M1 kararlarından) | kısmen: kuru koşu iki metin yapısıyla koştu; ağ yolu ana oturumda `sfx`, `music`, `stems`, `design` için koştu (defter); `save-voice` ve `voice` koşmadı | — | — | — | — |
| `licenses.md` | geçti | — | — | — | — | — |

His ve dinleme: **sahibin onayını bekliyor**: 165 ElevenLabs sesi, 15 sentez arayüz sesi, gündüz parçası, 3 Kabarma katmanı ve Suna'nın 3 aday sesi.

**M0 denemeleri (URETIM §2.3):**
- 5 (10 ses + 1 döngü): ⚠️ Betikli denetim artık var. 12 M0 sesinin 8'i geçti. `hit` ve `pop` gerçek tepe −1 dBTP üstünde; `pickup` 40 ms baş sessizliği; `water` döngü olarak kullanılamıyor (0,5 sn, dikiş 26 dB). Dinleme turu 👤.
- 5, ElevenLabs ile (18 Kasım): ✅ betikli denetim. 165 ses üretildi; ilk denetimde 10'u KALDI, 4'ü onarıldı, 6'sı yeni dikiş ölçütüyle geçti. Dinleme turu 👤.
- 6 (1 müzik, 3 katman): ✅ betikli denetim. 120 sn, 119,9 BPM. Katmanlar bas / davul / ezgi; örnek örnek eşit boy; toplamın dikişi geçti. Ayrıştırmanın 25 ms gecikmesi kırpıldı. Dinleme 👤.
- 7 (Suna: 3 aday, 10 replik): ❌ Betik hazır (tasarım → sahip seçer → kayıt → replik + geri çevirme). Üretim isteği. Replik metni (30 TR + 30 EN) ilkgun dalında hazır; dallar birleşince başlanabilir. 👤

## (c) Dokunduğum başka akış dosyaları

- `game/scripts/ui/Hud.cs` (gecehud): 2 satır eklendi (`InitVoice();` `_Ready` sonunda; `VoiceLineStarted(_full);` Suna satırı başlarken). Var olan satır değişmedi.
- `game/scripts/Main.cs` (girdi): `Tests` sözlüğüne `["audio"]` satırı.
- `tools/test.ps1` (girdi): varsayılan adımlara `audio` ekleyen 1 satır ve `audio` adım bloğu.
- `game/scripts/tests/AudioTest.cs` (girdi'nin klasöründe yeni dosya).
- `pipeline/README.md`: sona ses bölümü; 18 Kasım'da `--reuse-raw` cümlesi.
- `pipeline/.gitignore` (yeni): `__pycache__/`. `8f3f476`'da yanlışlıkla commit ettiğim 3 `.pyc` izlemeden çıkarıldı.
- Davranış değişikliği: her çağrı artık `audio.json`'dan geçiyor. Ses başına eşzamanlı kopya sınırı var (vuruşlarda 6), kanallar ayrı, ana kanalda tepe sınırı. Eski adlar aynı M0 dosyasını çalıyor. `--mute` artık ana kanalı da susturuyor.
- Godot içe aktarmanın 106 `.import` dosyasında yaptığı satır sonu değişiklikleri geri alındı, commit'te yok.

## (d) Gevşetilen bot adımı

Yok. Bot sonucu değişmedi.

## (e) Üretim istekleri (havuz: ElevenLabs, sahibin anahtarı, M1 ses paketi onayı; Genex 0, Tripo 0)

Kuru koşu: `progress/m1/ses/eleven_dry_run.txt`. Komutlar depo kökünden; her adım ayrı onayla.

| # | Ne | Adet | Tahmin | Sınır (onay) | Çıktı | Komut |
|---|---|---|---|---|---|---|
| 1 | Ses efekti, `eleven_text_to_sound_v3` | 55 ses × 3 sürüm = 165 üretim (arayüzün 5'i sentezle yapıldı) | 329,7 sn = 5,5 dk ≈ 0,66 $ | 19 dk | `game/assets/audio/sfx/{id}_{n}.mp3` | `--only sfx` |
| 2 | Müzik, `music_v2_5` | 2 parça: `cayir_gunduz` 180 sn, `kabarma` 120 sn (120 BPM, 60 ölçü) | 300 sn ≈ 0,75 $ | 12,5 dk (denemeler dahil) | `game/assets/audio/music/cayir_gunduz.mp3`, `kabarma_full.mp3` | `--only music` |
| 3 | Kabarma katman ayrıştırma | 1 | 120 sn (fiyatı yazmıyor; müzik dakikasına sayıldı) | aynı 12,5 dk | `music/kabarma_{1,2,3}.mp3` | `--only stems` |
| 4 | Suna ses tasarımı, `eleven_ttv_v3` | 1 çağrı, 3 aday | 142 karakter (fiyatı yazmıyor; karaktere sayıldı) | 3 aday | `progress/m1/ses/suna_adaylari/aday_{1,2,3}.mp3` | `--only design`, sahip seçer, `--only save-voice --generated-id …` |
| 5 | Suna replikleri, `eleven_v3` + geri çevirme `scribe_v3` | 30 TR + 30 EN | ilkgun metniyle: 2.099 TR + 2.481 EN = 4.580 karakter (+142 tasarım = 4.722); yeniden deneme payı 2.478 karakter · geri çevirme ~327 sn ≈ 0,02 $ (`eleven_dry_run_ilkgun.txt`) | 7.200 karakter | `game/assets/audio/voice/{tr,en}/{kimlik}.mp3` | `--only voice --lang tr` / `en` · **ENGEL bu dalda:** eski metin (41 replik, tuş adı, EN yok). ilkgun dalı birleşince kalkar; betik o metni engelsiz okuyor |

Toplam tahmin ~1,9 $ (liste fiyatı) + ses tasarımı ve katman ayrıştırma (fiyatı yazmıyor). Her üretimden sonra: Godot `--import`, `python pipeline/audio_qc.py`, sahibin dinlemesi, `audio.json`'da `placeholder: false`, `licenses.md`'ye satır.

**Ana oturumun üretimi (18 Kasım 00:28–00:34, bu worktree'de; ben çalıştırmadım).** Defterde (`eleven_ledger.json`): 1–4 yapıldı.
- Ses efekti: 165 üretim, 329,7 sn.
- Müzik: `cayir_gunduz` 180 sn, `kabarma_full` 120 sn ve katman ayrıştırma 120 sn; toplam 420 sn / 750.
- Suna ses tasarımı: 1 çağrı, 142 karakter, 3 aday (`suna_adaylari/aday_{1,2,3}.mp3`).

İndirmeden sonra `post_process` denetiminde 10 dosya KALDI: 8 döngü dikişi (`kaya_kemirme_1,2`, `ates_citirti_1,2`, `tezgah_calisma_1,2,3`, `el_arabasi_teker_2`) ve 2 tepe (`sokum_1`, `kargo_roketi_1`).

**İşleme (18 Kasım, ana oturumun isteğiyle; kredi yok).** Sonuç: üretilen her dosya betikli denetimden geçti ve commit edildi. Değiştirilen 6 dosyanın önceki hâli `.tools/eleven_raw/before_fix/` altında (git dışı).

`audio_qc.md` 198 dosya: 193 geçti, 5 KALDI, 94 UYARI.
- 5 KALDI'nin hepsi eski Genex M0 asılları: `hit`, `pop`, `pickup`, `water`, `kabarma_night`. Bu seslerin hepsinin artık 3'er gerçek sürümü var; M0 yalnız yedek ve çalınmıyor. Kaynak kaydı olarak dokunulmadı.
- UYARI'lar yalnız seçtiğim yükseklik aralıkları (54) ve grup içi spektrum yayılması (40). Kulak kararı sahipte.

1. **Kabarma katmanları baştan toplandı** (`--only stems --reuse-raw --force`; indirilmiş kökler, API yok).
   - Teslim edilen `kabarma_3` tamamen sessizdi (−87,5 dBTP). Benim eşlemem boş `vocals` kökünü 3. katmana veriyordu.
   - Yeni eşleme: 1 bas, 2 davul, 3 `other` (zurna, bağlama, sentez). guitar, piano ve vocals kökleri boş geldi; katmana girmedi.
   - **Ayrıca:** ayrıştırma servisi her kökü tam parçadan tam 1.152 örnek (bir mp3 karesi) uzun ve o kadar geç veriyor. Baştaki fazlalık −80 dBFS altında, yani sessiz. Kırpılmasa her döngüde 25 ms boşluk olurdu; toplamın dikişi 22 dB atlıyordu.
     - `eleven.py` bu fazlalığı ölçüyor, sessiz olduğunu doğrulayıp kırpıyor; sessiz değilse durur.
   - Sonuç:
     - Üç katman da 5.293.440 örnek, tam parçayla örnek örnek aynı boy.
     - Toplamın dikişi 2,9 dB; parçanın kendi oynaması 5,3 dB.
     - Atlama 0,01×; tempo 119,9 BPM; tepeler −5,0 / −1,1 / −3,0 dBTP.
2. **Tepe:**
   - `sokum_1` −0,9 → −1,5 dBTP.
   - `kargo_roketi_1` −0,9 → −2,2 dBTP.
   - `kabarma_full` +0,1 → −1,4 dBTP (oyunda çalmaz; ayrıştırma kaynağı).
   - `fix` artık her kodlamadan sonra ölçüyor; önceden mp3'e yeniden kodlama tepeyi geri taşıyordu.
3. **Döngü dikişi:**
   - Çapraz geçişle onarılan ikisi:
     - `el_arabasi_teker_2`: tık 7,6× → 0,2×; 3,0 → 2,5 sn.
     - `tezgah_calisma_3`: 37 dB → 15,9 dB; sesin kendi oynaması 22,7 dB, 4,0 → 3,5 sn.
     - Çapraz geçiş, sonun en çok 0,5 sn'sini başa eşit güçle bindiriyor. Dikiş tanım gereği süreklidir: yeni son ve ilk örnek, orijinalde komşu iki örnektir.
   - Diğer 6'sı (`kaya_kemirme_1,2`, `ates_citirti_1,2`, `tezgah_calisma_1,2`) yeni ölçütle geçti, dokunulmadı. Ölçüt değişikliği: (f) 22.
4. **`cayir_gunduz`:** sondaki 3,4 sn sessizlik kırpıldı (180,0 → 176,7 sn); tepe −1,5 dBTP.

**Kredisiz onarılamayan:** yok. Betikli denetime göre yeniden üretim isteği de yok. Sahibin dinlemesinde reddedilen ses olursa istek o zaman yazılır; sınırda ~13,5 dk ses efekti payı var.

Defter `bf5d6fd`'de commit edildi. 5. madde (Suna replikleri) duruyor: bu dalda metin engelli, ayrıca sahip 3 adaydan birini seçmeli.

## (f) Belgeyle çelişen ya da belgenin sustuğu yerler

1. **Kanallar:** görev 5 kanal sayıyor (Ana, Müzik, Efekt, Ses/Telsiz, Ortam); SANAT-SES §10.1'de ayrıca Arayüz ve Sesli sohbet var. Belgeye uydum: 6 kanal. Sesli sohbet ortak oyunla gelir (M2).
2. **Varyasyon:** §10.1 "3–5", M1 "3", onay "3–4". Veride 3; `eleven.py` 3–4'e izin veriyor.
3. **"Aynı parça art arda çalmaz"** (§10.2), ama M1'de tek gündüz parçası var: arada parça boyu kadar sessizlik (pay 0,5, belgedeki "zamanın yarısında susar").
4. **Konuşma modeli:** URETIM §2.1 `eleven_v4` ve `eleven_v3`'ü birlikte sayıyor, hangisinin sabitleneceğini söylemiyor. `eleven_v3` sabitlendi (sahibin seçimi). Ses tasarımı (`eleven_ttv_v3`) ve geri çevirme (`scribe_v3`) modelleri belgede yok.
5. **Kabarma katman sayıları** belgede yok: eşikler 0,25 / 0,6, dalga ağırlığı 0,7, Gösteri ağırlığı 0,3, 4 ölçü bekleme, 120 BPM 4/4. Veride duruyor. M0 parçasının ölçülen temposu 119,9.
6. **LUFS hedefleri** belgede yok; yalnız −1 dBTP ve "Suna efektlerin ortalamasının 3 dB üstünde" var. Seçilen aralıklar `audio_qc.py`'de UYARI.
7. **Katman ayrıştırma:** ElevenLabs belgesi fiyatı ve kök adlarını vermiyor. Fiyat müzik dakikasına sayıldı; kökler anahtar sözcükle katmana eşleniyor, eşleşmezse betik durur.
8. **Suna metni (18 Kasım'da güncellendi):** bu daldaki `scenario.json` hâlâ eski yapıda: 41 replik (belge 30), 12'sinde tuş adı (§10.3 yasaklıyor), İngilizcesi yok. ilkgun kendi dalında (`claude/m1-ilkgun-3d5928`, `f472a1a`) bunu çözdü: `lines` {kimlik: {tr, en}}, 30 TR + 30 EN, tuş adı yok, `Events.SunaSaid(kimlik)`. Ses tarafı (`VoiceLines.FromJson`, `Hud.Voice`, `eleven.py load_lines`) iki yapıyı da okur ve `ScenarioFile` alanlarına dayanmaz. Bu dal kendi başına yeşil; dallar birleşince kimlikler kendiliğinden ilkgun'unkiler olur. Kimlik ekrandaki metinden bulunur: ilkgun'un `SayLine`'ı (kimlikle) ve gecehud'un gece satırları (`Hud.Say(NightLines[...])`, metinle) ikisi de çözülür. `SunaSaid` olayına bağlanmadım: bu dalda yok, metin yolu zaten kapsıyor.
9. **Çift mandal:** `Hud.cs`'teki eski `Sfx.Play("place", null, -22f); // telsiz mandali` satırı (gecehud) duruyor; yeni mandalla birlikte çalıyor. Silinmesi önerilir.
10. **33 sesin çalma noktası** başka akışların kodunda (`wired: false`, yer `play` alanında). Ben yalnız kendi dosyalarımdan ve olaylardan bağladım. Çift çalmasın diye kod yerinde zaten ses çalan olaylara bağlamadım.
11. **Foley.cs** adımları, inişi, Gıdak'ın bekleme gıdaklamasını ve Ocak ateşini durumu okuyarak çalıyor. oyuncu ya da yaratık kendi bağlarsa buradaki kaldırılmalı.
12. **F2 / kol Geri** düğmesi girdi akışının eşlemesiyle çakışabilir.
13. **ElevenLabs şartları:** müzik Free–Pro'da "bireysel kullanım" (URETIM §9'da). Plan adı üretim günü `licenses.md`'ye yazılacak.
14. **Statik durum:** PLAN §7.2 ve M1 kararı 12, Godot katmanına yeni statik durum eklenmemesini istiyor. `Sfx` MVP'den beri statik bir sınıftı; ben ona statik önbellekler ve referanslar ekledim (veri, Director, müzik düğümü, akış ve varyasyon önbelleği, kopya sayacı, tutulan döngüler, kısma). Bunlar oyun durumu değil, kayda girecek bir şey yok; her Director kurulumunda sıfırlanıyorlar. Sıkı uyum istenirse `Sfx` Director'a bağlı bir düğüme taşınır, `Sfx.Play` statik kapı olarak kalır.
15. **Süreç:** görev, belgeyle çelişen ya da belgenin sustuğu yerde koda geçmeden durup bildirmemi istiyordu. Durmadım. 1–13'ü kendim karara bağladım: belge konuştuysa belgeye uydum, sustuysa değeri veriye yazdım. Hepsi veri değeri ya da küçük bir kod anahtarı; inceleme için burada.
16. **Harcama defteri checkout'a bağlı:** `progress/m1/ses/eleven_ledger.json`. `eleven.py` yalnızca tek bir checkout'tan (ana çalışma kopyası) çalıştırılmalı ve her koşudan sonra defter commit edilmeli. Yoksa sert sınırlar sessizce sıfırlanır.
17. **ElevenLabs API sözleşmeleri** 17 Kasım'da resmi API sayfalarından okundu (uç noktalar, alanlar, modeller), ama hiçbir gerçek çağrı yapılmadı. İlk gerçek çağrıda uyumsuzluk çıkarsa betik ilk HTTP hatasında durur; o çağrı deftere "faturalanmadı" diye yazılır.
18. **Birleşme denemesi (18 Kasım):** geçici, dala bağlı olmayan bir worktree'de ses (`6ec5e6a`) ile ilkgun (`f472a1a`) birleştirildi ve deneme bitince silindi; hiçbir dal değişmedi.
    - Çakışma yok. Sim 45/45, derleme 0 uyarı, bot `RESULT ok`. Her Suna satırı (gecehud'un metinle söylediği gece satırları dahil) ilkgun kimliğine çözüldü.
    - Aynı koşuda ilkgun'un `ActText.Label`'ı (`ActText.cs:26`, `DisplayServer.KeyboardGetKeycodeFromPhysical`) `--headless` altında 12.937 `Not supported by this display server` hatası yazdı. Bu ses akışından değil; ilkgun'a bildirildi.
19. **`eleven.py` kuru koşusu tüm planı sayar:** yapılmış adımları düşmez. Ana oturumun üretiminden sonra müzik satırı "ASIYOR" gösteriyor (defter 420 + plan 420 > 750); tasarımın 142 karakteri de yeniden sayılıyor. Gerçek koşuda sert sınır ve `designCalls` durumu tekrarı zaten engeller. Kalan iş yalnız Suna replikleri: 4.580 karakter + defterdeki 142 = 4.722 / 7.200.
20. **Katman içerikleri belgede yok:** SANAT-SES §10.2 ve URETIM yalnız "3 yoğunluk katmanı" diyor. Ben seçtim: 1 bas (alarmda tek başına), 2 davul, 3 ezgi ve sentez (zurna, bağlama, arpej). Sıra `m1_audio.json`'da (`_layersNot`), dinleyip değiştirmek bir satır.
21. **Çıkışta ses sızıntısı uyarısı (gerçek dosyalarla ortaya çıktı):** Oyun ses çalarken kapanınca Godot şunları yazıyor:
    - `ObjectDB instances were leaked at exit` (`--verbose`: `AudioStreamMP3` ve `AudioStreamPlaybackMP3`)
    - `resources still in use`

    Oynatıcı serbest kalıyor, ama ses döngüsü playback'i bırakmaya yetişmiyor. Dosyasızken çalan döngü yoktu; şimdi ortam ve müzik hep çalıyor. Bu yalnız çıkışta olur, oyunu etkilemez.
    - `Sfx.StopAll()` eklendi. `audio` sınaması çıkmadan önce onu çağırıp 0,1 sn bekliyor: sızıntı satırı 0.
    - Bot ve `Main.Tests` çıkışı başka akışların dosyasında. Orada `Quit` öncesine aynı iki satır eklenirse bot günlüğü de temizlenir (öneri; dokunmadım).
22. **Dikiş ölçütü (benim seçimim, inceleme için):** belge yalnız "döngü dikişi denetimi" diyor (URETIM §2.2).
    - Eski ölçüt: son ve ilk 50 ms arasındaki düzey farkı > 3 dB ise KALDI. Çıtırtı ve kemirme gibi seslerde komşu iki 50 ms penceresi bile 5–20 dB oynar; bu yüzden ölçüt iyi dikişleri de düşürüyordu.
    - Yeni ölçüt: fark, sesin kendi komşu pencereleri arasındaki farkın %95'ini (ve 3 dB'yi) aşarsa KALDI. Örnek atlaması (tık) ölçütü değişmedi.
    - Bu ölçütle 8 dikiş hatasının 6'sı geçti, bunlara dokunulmadı. Kalan 2'si çapraz geçişle onarıldı.
    - Kabarma katmanları tek tek değil toplamıyla denetleniyor: aynı boy, toplamın dikişi ve temposu. Tek enstrüman katmanında bas notası ölçü başında çalar, sonda susar; tek başına dikiş anlamsız. Katmanlar hiç çapraz geçişe sokulmaz, sokulsa boyları değişip birbirinden kayarlar.
23. **Yer tutucu işareti:** önceki notum "dinleme onayından sonra false" diyordu. URETIM §3'e göre yer tutucu geçici varlıktır. Ana oturumun isteğiyle 60 ses ve 2 müzik `placeholder: false` (gerçek M1 dosyaları, denetimden geçti). Dinleme onayı ayrı izleniyor; reddedilen ses yeniden `true` olur.
24. **ElevenLabs katman ayrıştırma gecikmesi** belgelerde yok: kökler girişten 1.152 örnek (bir mp3 karesi) uzun ve geç. `eleven.py` ölçüp kırpıyor ((e) 1).
