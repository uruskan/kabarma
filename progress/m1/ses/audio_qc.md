# Ses denetimi (pipeline/audio_qc.py)

198 dosya · 193 gecti · 5 KALDI · 94 UYARI

Tonlama (key) tespiti yok; tempo kestirimi yalnizca muzikte. Kulak yargisi sahibin dinleme turunda.

| Dosya | Tur | Sure (sn) | Gercek tepe (dBTP) | LUFS-I | En yuksek anlik | Bas/son sessizlik (sn) | Dikis | Sonuc |
|---|---|---|---|---|---|---|---|---|
| game/assets/audio/music/cayir_gunduz.mp3 | music_day | 176.66 | -1.5 | -15.4 | -11.6 | 0.01 / 0.124 |  | gecti |
| game/assets/audio/music/kabarma_1.mp3 | music_layer | 120.03 | -5.0 | -19.5 | -17.8 | 0.037 / 0.001 | 25.31 dB / 0.05x | gecti |
| game/assets/audio/music/kabarma_2.mp3 | music_layer | 120.03 | -1.1 | -15.6 | -14.1 | 0.006 / 0.001 | 9.24 dB / 0.09x | gecti |
| game/assets/audio/music/kabarma_3.mp3 | music_layer | 120.03 | -3.0 | -17.9 | -13.7 | 0.007 / 0.001 | 9.13 dB / 0.02x | gecti |
| game/assets/audio/music/kabarma_full.mp3 | music_kabarma | 120.03 | -1.4 | -13.2 | -11.5 | 0.007 / 0.003 | 2.47 dB / 0.02x | gecti |
| game/assets/audio/music/kabarma_night.mp3 | music_kabarma | 90.02 | -0.2 | -11.9 | -10.9 | 0.007 / 0.0 | 5.32 dB / 0.22x | KALDI tepe: gercek tepe -0.2 dBTP > -1.0; UYARI yukseklik: -11.9 LUFS, secilen aralik -20.0..-12.0 |
| game/assets/audio/sfx/adim_ot_1.mp3 | sfx | 0.48 | -5.2 | -22.8 | -22.8 | 0.005 / 0.071 |  | gecti |
| game/assets/audio/sfx/adim_ot_2.mp3 | sfx | 0.48 | -8.6 | -26.9 | -26.9 | 0.006 / 0.065 |  | UYARI yukseklik: en yuksek anlik -26.9 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/adim_ot_3.mp3 | sfx | 0.45 | -19.1 | -35.6 | -35.6 | 0.011 / 0.043 |  | UYARI yukseklik: en yuksek anlik -35.6 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/adim_tahta_1.mp3 | sfx | 0.48 | -7.4 | -23.1 | -23.1 | 0.0 / 0.148 |  | gecti |
| game/assets/audio/sfx/adim_tahta_2.mp3 | sfx | 0.48 | -5.3 | -22.7 | -22.7 | 0.0 / 0.168 |  | gecti |
| game/assets/audio/sfx/adim_tahta_3.mp3 | sfx | 0.48 | -7.7 | -23.0 | -23.0 | 0.0 / 0.134 |  | gecti |
| game/assets/audio/sfx/adim_toprak_1.mp3 | sfx | 0.48 | -5.3 | -21.0 | -21.0 | 0.009 / 0.038 |  | gecti |
| game/assets/audio/sfx/adim_toprak_2.mp3 | sfx | 0.48 | -6.6 | -23.0 | -23.0 | 0.03 / 0.06 |  | gecti |
| game/assets/audio/sfx/adim_toprak_3.mp3 | sfx | 0.48 | -4.0 | -21.8 | -21.8 | 0.0 / 0.07 |  | gecti |
| game/assets/audio/sfx/ahsap_kapi_1.mp3 | sfx | 0.80 | -3.7 | -16.7 | -15.4 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/ahsap_kapi_2.mp3 | sfx | 0.74 | -1.4 | -22.9 | -22.7 | 0.01 / 0.1 |  | UYARI spektrum: agirlik merkezi 749 Hz, grup 'yapi' ortancasi 2597 Hz |
| game/assets/audio/sfx/ahsap_kapi_3.mp3 | sfx | 0.48 | -1.9 | -18.4 | -18.4 | 0.0 / 0.1 |  | gecti |
| game/assets/audio/sfx/ates_citirti_1.mp3 | loop | 5.00 | -7.6 | -30.7 | -26.8 | 0.0 / 0.0 | 4.62 dB / 0.38x | UYARI yukseklik: en yuksek anlik -26.8 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/ates_citirti_2.mp3 | loop | 5.00 | -1.0 | -31.9 | -24.0 | 0.0 / 0.0 | 3.39 dB / 0.11x | gecti |
| game/assets/audio/sfx/ates_citirti_3.mp3 | loop | 5.00 | -8.5 | -31.2 | -27.4 | 0.0 / 0.0 | 2.45 dB / 0.13x | UYARI yukseklik: en yuksek anlik -27.4 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/atis_civi_1.mp3 | sfx | 0.48 | -4.1 | -19.0 | -19.0 | 0.0 / 0.227 |  | gecti |
| game/assets/audio/sfx/atis_civi_2.mp3 | sfx | 0.48 | -2.8 | -17.1 | -17.1 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/atis_civi_3.mp3 | sfx | 0.30 | -5.2 | None | None | 0.0 / 0.1 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -25.1 dBFS |
| game/assets/audio/sfx/atis_tirpan_1.mp3 | sfx | 0.48 | -5.4 | -17.6 | -17.6 | 0.0 / 0.034 |  | gecti |
| game/assets/audio/sfx/atis_tirpan_2.mp3 | sfx | 0.48 | -2.2 | -14.8 | -14.8 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/atis_tirpan_3.mp3 | sfx | 0.48 | -1.1 | -13.6 | -13.6 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/beam_loop.mp3 | loop | 2.00 | -8.0 | -16.5 | -16.1 | 0.0 / 0.0 | 0.53 dB / 0.42x | gecti |
| game/assets/audio/sfx/bicme_supurme_1.mp3 | sfx | 0.55 | -10.4 | -21.6 | -21.6 | 0.004 / 0.062 |  | gecti |
| game/assets/audio/sfx/bicme_supurme_2.mp3 | sfx | 0.54 | -13.8 | -23.4 | -23.3 | 0.01 / 0.017 |  | gecti |
| game/assets/audio/sfx/bicme_supurme_3.mp3 | sfx | 0.49 | -20.7 | -31.0 | -31.0 | 0.004 / 0.095 |  | UYARI yukseklik: en yuksek anlik -31.0 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/bitki_darbe_1.mp3 | sfx | 0.48 | -2.7 | -21.7 | -21.7 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/bitki_darbe_2.mp3 | sfx | 0.48 | -3.1 | -21.4 | -21.4 | 0.018 / 0.041 |  | gecti |
| game/assets/audio/sfx/bitki_darbe_3.mp3 | sfx | 0.48 | -10.0 | -22.3 | -22.3 | 0.024 / 0.0 |  | gecti |
| game/assets/audio/sfx/cekim_isini_1.mp3 | loop | 3.00 | -4.5 | -17.1 | -16.5 | 0.0 / 0.0 | 0.33 dB / 0.19x | UYARI spektrum: agirlik merkezi 1714 Hz, grup 'capa' ortancasi 5065 Hz |
| game/assets/audio/sfx/cekim_isini_2.mp3 | loop | 3.00 | -2.4 | -12.2 | -11.6 | 0.0 / 0.0 | 0.45 dB / 1.21x | UYARI spektrum: agirlik merkezi 817 Hz, grup 'capa' ortancasi 5065 Hz |
| game/assets/audio/sfx/cekim_isini_3.mp3 | loop | 3.00 | -1.3 | -11.9 | -11.2 | 0.0 / 0.0 | 0.15 dB / 1.18x | gecti |
| game/assets/audio/sfx/cephane_bitti_1.mp3 | sfx | 0.41 | -1.8 | -20.9 | -20.9 | 0.006 / 0.069 |  | gecti |
| game/assets/audio/sfx/cephane_bitti_2.mp3 | sfx | 0.48 | -3.6 | -18.8 | -18.8 | 0.007 / 0.235 |  | gecti |
| game/assets/audio/sfx/cephane_bitti_3.mp3 | sfx | 0.31 | -1.5 | None | None | 0.01 / 0.092 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.8 dBFS |
| game/assets/audio/sfx/dalga_girisi_1.mp3 | sfx | 1.81 | -1.7 | -17.0 | -15.0 | 0.0 / 0.1 |  | UYARI spektrum: agirlik merkezi 448 Hz, grup 'kabarma' ortancasi 3909 Hz |
| game/assets/audio/sfx/dalga_girisi_2.mp3 | sfx | 1.65 | -2.4 | -17.6 | -13.9 | 0.0 / 0.1 |  | UYARI spektrum: agirlik merkezi 341 Hz, grup 'kabarma' ortancasi 3909 Hz |
| game/assets/audio/sfx/dalga_girisi_3.mp3 | sfx | 1.48 | -3.6 | -16.7 | -14.3 | 0.01 / 0.1 |  | UYARI spektrum: agirlik merkezi 465 Hz, grup 'kabarma' ortancasi 3909 Hz |
| game/assets/audio/sfx/donma_citirti_1.mp3 | sfx | 0.80 | -2.6 | -20.7 | -18.6 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/donma_citirti_2.mp3 | sfx | 0.80 | -6.6 | -27.3 | -24.9 | 0.0 / 0.004 |  | gecti |
| game/assets/audio/sfx/donma_citirti_3.mp3 | sfx | 0.80 | -3.4 | -21.7 | -20.1 | 0.0 / 0.001 |  | gecti |
| game/assets/audio/sfx/el_arabasi_teker_1.mp3 | loop | 3.00 | -4.3 | -21.2 | -18.1 | 0.0 / 0.0 | 2.01 dB / 0.53x | gecti |
| game/assets/audio/sfx/el_arabasi_teker_2.mp3 | loop | 2.50 | -1.5 | -20.1 | -15.5 | 0.0 / 0.0 | 6.58 dB / 0.21x | gecti |
| game/assets/audio/sfx/el_arabasi_teker_3.mp3 | loop | 3.00 | -1.4 | -23.0 | -18.3 | 0.0 / 0.0 | 1.31 dB / 2.04x | gecti |
| game/assets/audio/sfx/filiz_cikisi_1.mp3 | sfx | 0.62 | -6.0 | -27.8 | -24.9 | 0.0 / 0.099 |  | gecti |
| game/assets/audio/sfx/filiz_cikisi_2.mp3 | sfx | 0.64 | -7.7 | -26.1 | -25.1 | 0.0 / 0.1 |  | gecti |
| game/assets/audio/sfx/filiz_cikisi_3.mp3 | sfx | 0.80 | -9.8 | -30.1 | -26.7 | 0.029 / 0.202 |  | UYARI yukseklik: en yuksek anlik -26.7 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/firin_kapagi_1.mp3 | sfx | 0.80 | -2.7 | -16.6 | -14.0 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/firin_kapagi_2.mp3 | sfx | 0.80 | -5.8 | -18.4 | -17.6 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/firin_kapagi_3.mp3 | sfx | 0.80 | -1.4 | -18.2 | -16.7 | 0.0 / 0.065 |  | gecti |
| game/assets/audio/sfx/firlatma_1.mp3 | sfx | 0.68 | -1.5 | -15.3 | -12.3 | 0.024 / 0.0 |  | UYARI spektrum: agirlik merkezi 227 Hz, grup 'capa' ortancasi 5065 Hz |
| game/assets/audio/sfx/firlatma_2.mp3 | sfx | 0.65 | -3.1 | -23.0 | -20.3 | 0.011 / 0.0 |  | UYARI spektrum: agirlik merkezi 1080 Hz, grup 'capa' ortancasi 5065 Hz |
| game/assets/audio/sfx/firlatma_3.mp3 | sfx | 0.64 | -10.8 | -25.6 | -23.3 | 0.01 / 0.0 |  | UYARI spektrum: agirlik merkezi 268 Hz, grup 'capa' ortancasi 5065 Hz |
| game/assets/audio/sfx/gidak_gidaklama_1.mp3 | sfx | 0.56 | -3.0 | -16.7 | -16.5 | 0.0 / 0.1 |  | UYARI spektrum: agirlik merkezi 6500 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gidak_gidaklama_2.mp3 | sfx | 0.55 | -9.2 | -19.2 | -19.0 | 0.01 / 0.1 |  | UYARI spektrum: agirlik merkezi 7724 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gidak_gidaklama_3.mp3 | sfx | 0.81 | -8.9 | -18.1 | -16.9 | 0.01 / 0.1 |  | UYARI spektrum: agirlik merkezi 8044 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gidak_tekme_1.mp3 | sfx | 0.60 | -17.9 | -34.5 | -30.8 | 0.0 / 0.194 |  | UYARI yukseklik: en yuksek anlik -30.8 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/gidak_tekme_2.mp3 | sfx | 0.60 | -4.7 | -22.1 | -20.0 | 0.0 / 0.172 |  | UYARI spektrum: agirlik merkezi 527 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gidak_tekme_3.mp3 | sfx | 0.60 | -4.0 | -24.0 | -21.4 | 0.0 / 0.216 |  | gecti |
| game/assets/audio/sfx/gidak_yumurtlama_1.mp3 | sfx | 0.89 | -4.9 | -28.4 | -25.6 | 0.011 / 0.091 |  | UYARI spektrum: agirlik merkezi 10655 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gidak_yumurtlama_2.mp3 | sfx | 0.94 | -10.9 | -28.4 | -25.4 | 0.01 / 0.05 |  | UYARI spektrum: agirlik merkezi 10372 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gidak_yumurtlama_3.mp3 | sfx | 0.92 | -18.7 | -32.9 | -30.7 | 0.01 / 0.046 |  | UYARI yukseklik: en yuksek anlik -30.7 LUFS, secilen aralik -26.0..-10.0; UYARI spektrum: agirlik merkezi 9695 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/gokyuzu_dalga_1.mp3 | loop | 8.00 | -14.6 | -28.8 | -25.2 | 0.0 / 0.0 | 2.63 dB / 0.86x | gecti |
| game/assets/audio/sfx/gokyuzu_dalga_2.mp3 | loop | 8.00 | -1.1 | -12.5 | -9.1 | 0.0 / 0.0 | 1.48 dB / 0.27x | UYARI yukseklik: en yuksek anlik -9.1 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/gokyuzu_dalga_3.mp3 | loop | 8.00 | -18.2 | -32.0 | -28.1 | 0.0 / 0.0 | 1.38 dB / 1.75x | UYARI yukseklik: en yuksek anlik -28.1 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/hasat_cekme_1.mp3 | sfx | 0.68 | -6.1 | -22.3 | -21.2 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/hasat_cekme_2.mp3 | sfx | 0.68 | -8.4 | -25.5 | -25.5 | 0.0 / 0.103 |  | gecti |
| game/assets/audio/sfx/hasat_cekme_3.mp3 | sfx | 0.68 | -12.3 | -31.3 | -31.2 | 0.012 / 0.165 |  | UYARI yukseklik: en yuksek anlik -31.2 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/hit.mp3 | sfx | 0.18 | -0.7 | None | None | 0.011 / 0.03 |  | KALDI tepe: gercek tepe -0.7 dBTP > -1.0; UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -16.5 dBFS |
| game/assets/audio/sfx/inis_1.mp3 | sfx | 0.44 | -2.8 | -18.9 | -18.9 | 0.0 / 0.136 |  | gecti |
| game/assets/audio/sfx/inis_2.mp3 | sfx | 0.31 | -9.4 | None | None | 0.009 / 0.112 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -26.4 dBFS |
| game/assets/audio/sfx/inis_3.mp3 | sfx | 0.60 | -3.0 | -20.4 | -18.0 | 0.0 / 0.168 |  | UYARI spektrum: agirlik merkezi 1063 Hz, grup 'oyuncu' ortancasi 3355 Hz |
| game/assets/audio/sfx/isabet_civi_1.mp3 | sfx | 0.48 | -1.0 | -18.9 | -18.9 | 0.0 / 0.0 |  | UYARI spektrum: agirlik merkezi 216 Hz, grup 'silah' ortancasi 3181 Hz |
| game/assets/audio/sfx/isabet_civi_2.mp3 | sfx | 0.48 | -25.4 | -40.1 | -40.1 | 0.0 / 0.237 |  | UYARI yukseklik: en yuksek anlik -40.1 LUFS, secilen aralik -26.0..-10.0; UYARI spektrum: agirlik merkezi 980 Hz, grup 'silah' ortancasi 3181 Hz |
| game/assets/audio/sfx/isabet_civi_3.mp3 | sfx | 0.48 | -4.5 | -22.4 | -22.4 | 0.0 / 0.129 |  | UYARI spektrum: agirlik merkezi 646 Hz, grup 'silah' ortancasi 3181 Hz |
| game/assets/audio/sfx/isabet_kesme_1.mp3 | sfx | 0.48 | -2.0 | -21.3 | -21.3 | 0.0 / 0.135 |  | gecti |
| game/assets/audio/sfx/isabet_kesme_2.mp3 | sfx | 0.48 | -5.4 | -22.0 | -22.0 | 0.0 / 0.001 |  | gecti |
| game/assets/audio/sfx/isabet_kesme_3.mp3 | sfx | 0.48 | -1.1 | -25.6 | -25.6 | 0.0 / 0.148 |  | gecti |
| game/assets/audio/sfx/isabet_kritik_1.mp3 | sfx | 0.56 | -1.5 | -16.9 | -15.1 | 0.0 / 0.101 |  | gecti |
| game/assets/audio/sfx/isabet_kritik_2.mp3 | sfx | 0.60 | -1.4 | -17.1 | -14.9 | 0.0 / 0.118 |  | gecti |
| game/assets/audio/sfx/isabet_kritik_3.mp3 | sfx | 0.60 | -1.6 | -15.3 | -12.3 | 0.0 / 0.081 |  | gecti |
| game/assets/audio/sfx/isabet_zayif_1.mp3 | sfx | 0.45 | -5.5 | -20.5 | -20.5 | 0.0 / 0.1 |  | UYARI spektrum: agirlik merkezi 469 Hz, grup 'silah' ortancasi 3181 Hz |
| game/assets/audio/sfx/isabet_zayif_2.mp3 | sfx | 0.60 | -4.6 | -17.7 | -15.9 | 0.0 / 0.091 |  | UYARI spektrum: agirlik merkezi 546 Hz, grup 'silah' ortancasi 3181 Hz |
| game/assets/audio/sfx/isabet_zayif_3.mp3 | sfx | 0.60 | -1.4 | -18.8 | -15.8 | 0.006 / 0.119 |  | UYARI spektrum: agirlik merkezi 1222 Hz, grup 'silah' ortancasi 3181 Hz |
| game/assets/audio/sfx/kabarik_gumleme_1.mp3 | sfx | 0.80 | -5.3 | -20.9 | -18.7 | 0.0 / 0.239 |  | UYARI spektrum: agirlik merkezi 781 Hz, grup 'tarim' ortancasi 4831 Hz |
| game/assets/audio/sfx/kabarik_gumleme_2.mp3 | sfx | 0.80 | -2.7 | -21.7 | -17.9 | 0.0 / 0.168 |  | UYARI spektrum: agirlik merkezi 1792 Hz, grup 'tarim' ortancasi 4831 Hz |
| game/assets/audio/sfx/kabarik_gumleme_3.mp3 | sfx | 0.80 | -2.9 | -20.9 | -17.0 | 0.0 / 0.139 |  | UYARI spektrum: agirlik merkezi 435 Hz, grup 'tarim' ortancasi 4831 Hz |
| game/assets/audio/sfx/kabarmis_patlama_1.mp3 | sfx | 0.35 | -1.4 | None | None | 0.011 / 0.1 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.1 dBFS |
| game/assets/audio/sfx/kabarmis_patlama_2.mp3 | sfx | 0.50 | -2.0 | -21.1 | -21.1 | 0.0 / 0.1 |  | gecti |
| game/assets/audio/sfx/kabarmis_patlama_3.mp3 | sfx | 0.44 | -1.4 | -20.6 | -20.6 | 0.0 / 0.136 |  | UYARI spektrum: agirlik merkezi 603 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/kargo_roketi_1.mp3 | sfx | 1.81 | -2.2 | -16.7 | -13.2 | 0.01 / 0.1 |  | gecti |
| game/assets/audio/sfx/kargo_roketi_2.mp3 | sfx | 1.73 | -2.5 | -17.2 | -14.2 | 0.01 / 0.1 |  | gecti |
| game/assets/audio/sfx/kargo_roketi_3.mp3 | sfx | 2.00 | -2.4 | -15.3 | -13.2 | 0.01 / 0.233 |  | gecti |
| game/assets/audio/sfx/kart_secimi_1.mp3 | sfx | 0.60 | -4.0 | -22.0 | -21.8 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/kart_secimi_2.mp3 | sfx | 0.60 | -4.9 | -20.8 | -20.6 | 0.0 / 0.224 |  | gecti |
| game/assets/audio/sfx/kart_secimi_3.mp3 | sfx | 0.39 | -9.8 | None | None | 0.0 / 0.1 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -27.9 dBFS |
| game/assets/audio/sfx/kaya_kemirme_1.mp3 | loop | 3.00 | -5.2 | -15.6 | -15.0 | 0.0 / 0.0 | 6.86 dB / 0.31x | gecti |
| game/assets/audio/sfx/kaya_kemirme_2.mp3 | loop | 3.00 | -19.5 | -33.4 | -32.9 | 0.0 / 0.0 | 3.42 dB / 0.33x | UYARI yukseklik: en yuksek anlik -32.9 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/kaya_kemirme_3.mp3 | loop | 3.00 | -6.8 | -17.7 | -16.5 | 0.0 / 0.0 | 0.34 dB / 1.73x | gecti |
| game/assets/audio/sfx/kazma_isini_1.mp3 | loop | 3.00 | -14.3 | -23.1 | -22.2 | 0.0 / 0.0 | 1.09 dB / 0.26x | gecti |
| game/assets/audio/sfx/kazma_isini_2.mp3 | loop | 3.00 | -32.0 | -40.4 | -40.2 | 0.0 / 0.0 | 0.15 dB / 0.47x | UYARI yukseklik: en yuksek anlik -40.2 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/kazma_isini_3.mp3 | loop | 3.00 | -9.2 | -19.3 | -19.1 | 0.0 / 0.0 | 0.31 dB / 0.55x | gecti |
| game/assets/audio/sfx/levelup.mp3 | sfx | 0.85 | -6.7 | -15.6 | -13.8 | 0.0 / 0.098 |  | gecti |
| game/assets/audio/sfx/nailgun.mp3 | sfx | 0.30 | -1.1 | None | None | 0.0 / 0.044 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.1 dBFS |
| game/assets/audio/sfx/ocak_yanma_1.mp3 | sfx | 2.00 | -3.0 | -16.3 | -13.5 | 0.01 / 0.215 |  | gecti |
| game/assets/audio/sfx/ocak_yanma_2.mp3 | sfx | 1.63 | -5.0 | -19.2 | -16.3 | 0.01 / 0.1 |  | gecti |
| game/assets/audio/sfx/ocak_yanma_3.mp3 | sfx | 1.69 | -6.3 | -21.3 | -18.1 | 0.01 / 0.1 |  | gecti |
| game/assets/audio/sfx/ortam_cayir_gunduz_1.mp3 | loop | 20.00 | -28.7 | -42.3 | -37.0 | 0.0 / 0.0 | 1.75 dB / 0.19x | UYARI yukseklik: en yuksek anlik -37.0 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/ortam_cayir_gunduz_2.mp3 | loop | 20.00 | -27.1 | -40.9 | -36.8 | 0.0 / 0.0 | 0.7 dB / 0.41x | UYARI yukseklik: en yuksek anlik -36.8 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/ortam_cayir_gunduz_3.mp3 | loop | 20.00 | -35.0 | -45.7 | -43.1 | 0.001 / 0.001 | 0.79 dB / 0.19x | UYARI yukseklik: en yuksek anlik -43.1 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/ortam_gol_kiyisi_1.mp3 | loop | 20.00 | -14.7 | -40.9 | -33.2 | 0.0 / 0.0 | 1.39 dB / 0.41x | UYARI yukseklik: en yuksek anlik -33.2 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/ortam_gol_kiyisi_2.mp3 | loop | 20.00 | -11.1 | -36.4 | -29.3 | 0.0 / 0.0 | 0.78 dB / 0.54x | UYARI yukseklik: en yuksek anlik -29.3 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/ortam_gol_kiyisi_3.mp3 | loop | 20.00 | -12.2 | -36.2 | -29.4 | 0.0 / 0.0 | 0.84 dB / 0.66x | UYARI yukseklik: en yuksek anlik -29.4 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/oyuncu_aci_1.mp3 | sfx | 0.48 | -1.0 | -10.2 | -10.2 | 0.028 / 0.105 |  | UYARI spektrum: agirlik merkezi 1250 Hz, grup 'oyuncu' ortancasi 3355 Hz |
| game/assets/audio/sfx/oyuncu_aci_2.mp3 | sfx | 0.48 | -1.5 | -11.2 | -11.2 | 0.0 / 0.127 |  | UYARI spektrum: agirlik merkezi 742 Hz, grup 'oyuncu' ortancasi 3355 Hz |
| game/assets/audio/sfx/oyuncu_aci_3.mp3 | sfx | 0.41 | -1.6 | -11.7 | -11.6 | 0.01 / 0.1 |  | UYARI spektrum: agirlik merkezi 680 Hz, grup 'oyuncu' ortancasi 3355 Hz |
| game/assets/audio/sfx/parca_darbe_1.mp3 | sfx | 0.48 | -1.5 | -19.1 | -19.1 | 0.027 / 0.132 |  | gecti |
| game/assets/audio/sfx/parca_darbe_2.mp3 | sfx | 0.48 | -1.4 | -16.7 | -16.7 | 0.0 / 0.199 |  | gecti |
| game/assets/audio/sfx/parca_darbe_3.mp3 | sfx | 0.48 | -4.4 | -18.3 | -18.3 | 0.0 / 0.138 |  | gecti |
| game/assets/audio/sfx/parca_emilis_1.mp3 | sfx | 0.41 | -4.9 | -18.4 | -18.4 | 0.009 / 0.055 |  | gecti |
| game/assets/audio/sfx/parca_emilis_2.mp3 | sfx | 0.41 | -6.4 | -19.1 | -19.1 | 0.007 / 0.073 |  | gecti |
| game/assets/audio/sfx/parca_emilis_3.mp3 | sfx | 0.48 | -3.7 | -16.3 | -16.3 | 0.0 / 0.14 |  | gecti |
| game/assets/audio/sfx/parca_sacilma_1.mp3 | sfx | 0.68 | -13.9 | -27.2 | -25.3 | 0.0 / 0.064 |  | gecti |
| game/assets/audio/sfx/parca_sacilma_2.mp3 | sfx | 0.68 | -11.6 | -27.6 | -24.5 | 0.0 / 0.001 |  | gecti |
| game/assets/audio/sfx/parca_sacilma_3.mp3 | sfx | 0.68 | -17.3 | -34.7 | -32.1 | 0.0 / 0.113 |  | UYARI yukseklik: en yuksek anlik -32.1 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/parca_yerlesme_1.mp3 | sfx | 0.60 | -1.9 | -29.0 | -26.5 | 0.0 / 0.074 |  | UYARI yukseklik: en yuksek anlik -26.5 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/parca_yerlesme_2.mp3 | sfx | 0.60 | -1.5 | -22.4 | -20.8 | 0.001 / 0.061 |  | gecti |
| game/assets/audio/sfx/parca_yerlesme_3.mp3 | sfx | 0.60 | -1.7 | -28.0 | -28.0 | 0.0 / 0.129 |  | UYARI yukseklik: en yuksek anlik -28.0 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/pickup.mp3 | sfx | 0.48 | -1.6 | -19.5 | -19.5 | 0.04 / 0.033 |  | KALDI bas_sessizlik: 0.040 sn > 0.03 |
| game/assets/audio/sfx/place.mp3 | sfx | 0.60 | -1.2 | -18.4 | -15.0 | 0.03 / 0.0 |  | gecti |
| game/assets/audio/sfx/pop.mp3 | sfx | 0.50 | -0.9 | -12.5 | -11.7 | 0.006 / 0.035 |  | KALDI tepe: gercek tepe -0.9 dBTP > -1.0 |
| game/assets/audio/sfx/rocket.mp3 | sfx | 1.67 | -3.2 | -18.6 | -16.2 | 0.001 / 0.061 |  | gecti |
| game/assets/audio/sfx/safak_sonme_1.mp3 | sfx | 2.48 | -4.3 | -13.6 | -11.1 | 0.0 / 0.037 |  | gecti |
| game/assets/audio/sfx/safak_sonme_2.mp3 | sfx | 2.48 | -19.3 | -35.9 | -29.8 | 0.008 / 0.106 |  | UYARI yukseklik: en yuksek anlik -29.8 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/safak_sonme_3.mp3 | sfx | 2.46 | -3.6 | -17.2 | -12.5 | 0.01 / 0.089 |  | gecti |
| game/assets/audio/sfx/seviye_atlama_1.mp3 | sfx | 1.00 | -10.8 | -28.5 | -25.8 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/seviye_atlama_2.mp3 | sfx | 1.00 | -6.5 | -18.8 | -18.1 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/seviye_atlama_3.mp3 | sfx | 1.00 | -2.1 | -15.1 | -13.6 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/siren.mp3 | sfx | 2.90 | -4.0 | -9.0 | -7.9 | 0.011 / 0.087 |  | UYARI yukseklik: en yuksek anlik -7.9 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/siren_1.mp3 | sfx | 3.00 | -1.5 | -8.6 | -7.9 | 0.027 / 0.086 |  | UYARI yukseklik: en yuksek anlik -7.9 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/siren_2.mp3 | sfx | 2.94 | -4.8 | -7.3 | -6.4 | 0.01 / 0.034 |  | UYARI yukseklik: en yuksek anlik -6.4 LUFS, secilen aralik -26.0..-10.0; UYARI spektrum: agirlik merkezi 1511 Hz, grup 'kabarma' ortancasi 3909 Hz |
| game/assets/audio/sfx/siren_3.mp3 | sfx | 2.78 | -4.2 | -11.5 | -9.5 | 0.01 / 0.099 |  | UYARI yukseklik: en yuksek anlik -9.5 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/sisme_1.mp3 | sfx | 1.20 | -2.2 | -13.8 | -11.4 | 0.027 / 0.0 |  | gecti |
| game/assets/audio/sfx/sisme_2.mp3 | sfx | 1.16 | -6.8 | -18.7 | -16.1 | 0.01 / 0.004 |  | gecti |
| game/assets/audio/sfx/sisme_3.mp3 | sfx | 1.19 | -2.3 | -12.5 | -10.3 | 0.01 / 0.009 |  | gecti |
| game/assets/audio/sfx/sokum_1.mp3 | sfx | 0.66 | -1.5 | -17.0 | -15.5 | 0.01 / 0.013 |  | gecti |
| game/assets/audio/sfx/sokum_2.mp3 | sfx | 0.68 | -1.6 | -15.6 | -15.1 | 0.0 / 0.055 |  | gecti |
| game/assets/audio/sfx/sokum_3.mp3 | sfx | 0.67 | -1.1 | -16.8 | -15.9 | 0.0 / 0.1 |  | gecti |
| game/assets/audio/sfx/sulama_1.mp3 | loop | 3.00 | -10.1 | -18.4 | -18.2 | 0.0 / 0.0 | 0.22 dB / 0.33x | gecti |
| game/assets/audio/sfx/sulama_2.mp3 | loop | 3.00 | -26.5 | -35.1 | -34.7 | 0.0 / 0.0 | 0.78 dB / 1.22x | UYARI yukseklik: en yuksek anlik -34.7 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/sulama_3.mp3 | loop | 3.00 | -12.0 | -19.4 | -19.1 | 0.0 / 0.0 | 0.33 dB / 1.11x | gecti |
| game/assets/audio/sfx/swell.mp3 | sfx | 1.15 | -5.0 | -14.8 | -12.4 | 0.01 / 0.0 |  | gecti |
| game/assets/audio/sfx/telsiz_mandali_1.mp3 | ui | 0.10 | -3.5 | None | None | 0.0 / 0.002 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.8 dBFS; UYARI spektrum: agirlik merkezi 7498 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/telsiz_mandali_2.mp3 | ui | 0.08 | -2.5 | None | None | 0.0 / 0.002 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -21.3 dBFS; UYARI spektrum: agirlik merkezi 7125 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/telsiz_mandali_3.mp3 | ui | 0.12 | -2.7 | None | None | 0.0 / 0.001 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -22.6 dBFS; UYARI spektrum: agirlik merkezi 7442 Hz, grup 'arayuz' ortancasi 1529 Hz |
| game/assets/audio/sfx/tezgah_calisma_1.mp3 | loop | 4.00 | -5.6 | -21.8 | -19.2 | 0.0 / 0.0 | 6.19 dB / 0.02x | gecti |
| game/assets/audio/sfx/tezgah_calisma_2.mp3 | loop | 4.00 | -2.7 | -19.2 | -17.7 | 0.0 / 0.0 | 13.56 dB / 0.29x | gecti |
| game/assets/audio/sfx/tezgah_calisma_3.mp3 | loop | 3.50 | -2.0 | -22.9 | -21.0 | 0.0 / 0.0 | 15.87 dB / 3.0x | gecti |
| game/assets/audio/sfx/till.mp3 | sfx | 0.45 | -5.1 | -18.6 | -18.6 | 0.0 / 0.034 |  | gecti |
| game/assets/audio/sfx/tohum_ekme_1.mp3 | sfx | 0.19 | -26.5 | None | None | 0.0 / 0.1 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -46.8 dBFS |
| game/assets/audio/sfx/tohum_ekme_2.mp3 | sfx | 0.28 | -35.7 | None | None | 0.01 / 0.1 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -54.2 dBFS |
| game/assets/audio/sfx/tohum_ekme_3.mp3 | sfx | 0.22 | -42.5 | None | None | 0.009 / 0.1 |  | UYARI yukseklik: 400 ms'den kisa: LUFS olculemez, en yuksek 100 ms RMS -56.9 dBFS |
| game/assets/audio/sfx/toprak_surme_1.mp3 | sfx | 0.60 | -10.9 | -30.2 | -28.1 | 0.0 / 0.103 |  | UYARI yukseklik: en yuksek anlik -28.1 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/toprak_surme_2.mp3 | sfx | 0.60 | -16.2 | -34.7 | -32.9 | 0.0 / 0.105 |  | UYARI yukseklik: en yuksek anlik -32.9 LUFS, secilen aralik -26.0..-10.0 |
| game/assets/audio/sfx/toprak_surme_3.mp3 | sfx | 0.60 | -1.3 | -25.3 | -23.7 | 0.0 / 0.0 |  | gecti |
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
| game/assets/audio/sfx/water.mp3 | loop | 0.50 | -2.7 | -13.6 | -13.2 | 0.0 / 0.097 | 15.06 dB / 0.06x | KALDI sure: 0.50 sn, beklenen 1.0-30.0 |
| game/assets/audio/sfx/yanik_tutusma_1.mp3 | sfx | 0.68 | -1.1 | -16.9 | -14.9 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/yanik_tutusma_2.mp3 | sfx | 0.68 | -9.9 | -29.8 | -25.9 | 0.0 / 0.135 |  | gecti |
| game/assets/audio/sfx/yanik_tutusma_3.mp3 | sfx | 0.68 | -1.2 | -16.1 | -15.1 | 0.0 / 0.0 |  | gecti |
| game/assets/audio/sfx/yeme_1.mp3 | sfx | 0.73 | -3.4 | -19.9 | -18.6 | 0.01 / 0.037 |  | gecti |
| game/assets/audio/sfx/yeme_2.mp3 | sfx | 0.77 | -8.7 | -22.1 | -21.3 | 0.012 / 0.095 |  | gecti |
| game/assets/audio/sfx/yeme_3.mp3 | sfx | 0.74 | -5.9 | -19.3 | -18.6 | 0.01 / 0.082 |  | gecti |
| game/assets/audio/sfx/zipzip_aci_1.mp3 | sfx | 0.48 | -1.0 | -10.2 | -10.2 | 0.0 / 0.06 |  | gecti |
| game/assets/audio/sfx/zipzip_aci_2.mp3 | sfx | 0.48 | -1.5 | -13.4 | -13.4 | 0.0 / 0.043 |  | gecti |
| game/assets/audio/sfx/zipzip_aci_3.mp3 | sfx | 0.48 | -1.5 | -13.1 | -13.1 | 0.003 / 0.076 |  | gecti |
| game/assets/audio/sfx/zipzip_kemirme_1.mp3 | sfx | 0.80 | -6.7 | -25.6 | -23.4 | 0.005 / 0.001 |  | UYARI spektrum: agirlik merkezi 8240 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/zipzip_kemirme_2.mp3 | sfx | 0.80 | -2.8 | -20.9 | -18.7 | 0.0 / 0.0 |  | UYARI spektrum: agirlik merkezi 6569 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/zipzip_kemirme_3.mp3 | sfx | 0.80 | -4.4 | -22.8 | -19.8 | 0.004 / 0.0 |  | UYARI spektrum: agirlik merkezi 6936 Hz, grup 'yaratik' ortancasi 2338 Hz |
| game/assets/audio/sfx/zipzip_sicrama_1.mp3 | sfx | 0.48 | -1.5 | -24.3 | -24.3 | 0.0 / 0.208 |  | gecti |
| game/assets/audio/sfx/zipzip_sicrama_2.mp3 | sfx | 0.48 | -1.3 | -23.7 | -23.7 | 0.0 / 0.233 |  | gecti |
| game/assets/audio/sfx/zipzip_sicrama_3.mp3 | sfx | 0.48 | -1.5 | -23.2 | -23.2 | 0.0 / 0.195 |  | gecti |

Tempo kestirimi: game/assets/audio/music/cayir_gunduz.mp3: 166.8 BPM, game/assets/audio/music/kabarma_1.mp3: 59.7 BPM, game/assets/audio/music/kabarma_2.mp3: 119.9 BPM, game/assets/audio/music/kabarma_3.mp3: 79.9 BPM, game/assets/audio/music/kabarma_full.mp3: 119.9 BPM, game/assets/audio/music/kabarma_night.mp3: 119.9 BPM (veri: Kabarma 120 BPM).

Kabarma katmanlari: ornek sayilari kabarma_1.mp3 5293440, kabarma_2.mp3 5293440, kabarma_3.mp3 5293440; toplam: dikis 2.91 dB (sesin kendi oynamasi 5.27 dB), atlama 0.01x, tempo 119.9 BPM.
Zarf kaymasi (yalniz bilgi; farkli enstrumanlarda yaniltici): kabarma_2.mp3: 120.0 ms, kabarma_3.mp3: -130.0 ms
