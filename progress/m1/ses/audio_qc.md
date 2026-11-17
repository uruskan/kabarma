# Ses denetimi (pipeline/audio_qc.py)

28 dosya · 23 gecti · 7 KALDI · 22 UYARI

Tonlama (key) tespiti yok; tempo kestirimi yalnizca muzikte. Kulak yargisi sahibin dinleme turunda.

| Dosya | Tur | Sure (sn) | Gercek tepe (dBTP) | LUFS-I | En yuksek anlik | Bas/son sessizlik (sn) | Dikis | Sonuc |
|---|---|---|---|---|---|---|---|---|
| game/assets/audio/music/kabarma_night.mp3 | music_kabarma | 90.02 | -0.2 | -11.9 | -10.9 | 0.007 / 0.0 | 5.32 dB / 0.22x | KALDI tepe: gercek tepe -0.2 dBTP > -1.0; KALDI dikis_duzey: bas/son duzey farki 5.32 dB; UYARI yukseklik: -11.9 LUFS, secilen aralik -20.0..-12.0 |
| game/assets/audio/sfx/beam_loop.mp3 | loop | 2.00 | -8.0 | -16.5 | -16.1 | 0.0 / 0.0 | 0.53 dB / 0.42x | gecti |
| game/assets/audio/sfx/hit.mp3 | sfx | 0.18 | -0.7 | None | None | 0.011 / 0.03 |  | KALDI tepe: gercek tepe -0.7 dBTP > -1.0; UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -16.5 dBFS |
| game/assets/audio/sfx/levelup.mp3 | sfx | 0.85 | -6.7 | -15.6 | -13.8 | 0.0 / 0.098 |  | gecti |
| game/assets/audio/sfx/nailgun.mp3 | sfx | 0.30 | -1.1 | None | None | 0.0 / 0.044 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.1 dBFS |
| game/assets/audio/sfx/pickup.mp3 | sfx | 0.48 | -1.6 | -19.5 | -19.5 | 0.04 / 0.033 |  | KALDI bas_sessizlik: 0.040 sn > 0.03 |
| game/assets/audio/sfx/place.mp3 | sfx | 0.60 | -1.2 | -18.4 | -15.0 | 0.03 / 0.0 |  | gecti |
| game/assets/audio/sfx/pop.mp3 | sfx | 0.50 | -0.9 | -12.5 | -11.7 | 0.006 / 0.035 |  | KALDI tepe: gercek tepe -0.9 dBTP > -1.0 |
| game/assets/audio/sfx/rocket.mp3 | sfx | 1.67 | -3.2 | -18.6 | -16.2 | 0.001 / 0.061 |  | gecti |
| game/assets/audio/sfx/siren.mp3 | sfx | 2.90 | -4.0 | -9.0 | -7.9 | 0.011 / 0.087 |  | UYARI yukseklik: en yuksek anlik -7.9 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/swell.mp3 | sfx | 1.15 | -5.0 | -14.8 | -12.4 | 0.01 / 0.0 |  | gecti |
| game/assets/audio/sfx/telsiz_mandali_1.mp3 | ui | 0.10 | -3.5 | None | None | 0.0 / 0.002 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.8 dBFS; UYARI spektrum: agirlik merkezi 7498 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/telsiz_mandali_2.mp3 | ui | 0.08 | -2.5 | None | None | 0.0 / 0.002 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -21.3 dBFS; UYARI spektrum: agirlik merkezi 7125 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/telsiz_mandali_3.mp3 | ui | 0.12 | -2.7 | None | None | 0.0 / 0.001 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.6 dBFS; UYARI spektrum: agirlik merkezi 7442 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/till.mp3 | sfx | 0.45 | -5.1 | -18.6 | -18.6 | 0.0 / 0.034 |  | gecti |
| game/assets/audio/sfx/ui_baslik_carki_1.mp3 | ui | 0.09 | -3.2 | None | None | 0.0 / 0.0 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -16.3 dBFS; UYARI spektrum: agirlik merkezi 5577 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/ui_baslik_carki_2.mp3 | ui | 0.09 | -3.2 | None | None | 0.0 / 0.001 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -16.4 dBFS; UYARI spektrum: agirlik merkezi 4683 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/ui_baslik_carki_3.mp3 | ui | 0.09 | -3.0 | None | None | 0.0 / 0.0 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -15.7 dBFS; UYARI spektrum: agirlik merkezi 6122 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/ui_hata_1.mp3 | ui | 0.22 | -3.0 | None | None | 0.0 / 0.0 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -10.4 dBFS |
| game/assets/audio/sfx/ui_hata_2.mp3 | ui | 0.22 | -3.0 | None | None | 0.0 / 0.0 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -10.3 dBFS |
| game/assets/audio/sfx/ui_hata_3.mp3 | ui | 0.22 | -3.0 | None | None | 0.0 / 0.0 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -10.4 dBFS |
| game/assets/audio/sfx/ui_odak_1.mp3 | ui | 0.07 | -3.0 | None | None | 0.0 / 0.001 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -14.7 dBFS |
| game/assets/audio/sfx/ui_odak_2.mp3 | ui | 0.07 | -3.1 | None | None | 0.0 / 0.001 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -14.6 dBFS |
| game/assets/audio/sfx/ui_odak_3.mp3 | ui | 0.07 | -3.0 | None | None | 0.0 / 0.001 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -14.9 dBFS |
| game/assets/audio/sfx/ui_onay_1.mp3 | ui | 0.42 | -3.0 | -15.2 | -15.2 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/ui_onay_2.mp3 | ui | 0.42 | -3.0 | -14.5 | -14.5 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/ui_onay_3.mp3 | ui | 0.42 | -3.0 | -15.2 | -15.2 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/water.mp3 | loop | 0.50 | -2.7 | -13.6 | -13.2 | 0.0 / 0.097 | 26.19 dB / 0.06x | KALDI sure: 0.50 sn, beklenen 1.0-30.0; KALDI dikis_duzey: bas/son duzey farki 26.19 dB |

Tempo kestirimi: game/assets/audio/music/kabarma_night.mp3: 119.9 BPM (veri: Kabarma 120 BPM).
