# Kabarma audio

## M1 layout (ses akışı)

- The sound list is `game/data/audio.json` (SANAT-SES §10.1, M1's 60 sounds). Each sound has 3 variants: `sfx/<id>_<n>.mp3` (n = 1..3). A missing variant falls back to the sound's M0 file (`m0`), otherwise it is silent.
- Music: `music/cayir_gunduz.mp3` (day), `music/kabarma_1.mp3`, `kabarma_2.mp3`, `kabarma_3.mp3` (Kabarma layers that add up: base, drums, zurna/lead; they start together and loop). Until they exist, layer 1 falls back to `music/kabarma_night.mp3`.
- Suna: `voice/<lang>/<line_id>.mp3` (`tr`, `en`). Line ids come from `game/data/scenario.json` (see `VoiceLines` in `src/Kabarma.Sim/AudioMix.cs`).
- Made with code, no credits: `pipeline/synth_ui.py` (the 5 UI sounds). Paid generation: `pipeline/eleven.py` with the request `pipeline/requests/m1_audio.json`. Checks: `pipeline/audio_qc.py`. Sources and licences: `pipeline/licenses.md`.

## MVP files (Genex)

All audio was generated with Genex on 2026-11-17. Total spent: **51 credits** (1 music track at 27, plus 12 SFX at 2 each). Nothing was retried.

The untrimmed originals are in `game/assets/genex/<job-id>/`. To re-download one, call `genex__asset wait <generationId>`, which doesn't bill.

## Music

| File | Duration | Generation id | Credits | Notes |
|---|---|---|---|---|
| music/kabarma_night.mp3 | 90.0 s | cmwk1jftd01mj3gt9inmo4qtb | 27 | "Space türkü" night battle track: davul, zurna and bağlama with analog synths, halay drive. Prompted as a seamless loop with no intro or outro. Level holds at about −14 dB RMS from the first bar to the last (no fades). Peak −0.2 dB. Unedited. Set loop on import. |

## Sound effects (`sfx/`)

| File | Duration | Peak | Generation id | Credits | Post-processing |
|---|---|---|---|---|---|
| beam_loop.mp3 | 2.00 s | −8.1 dB | cmwk1ji4f01mq3gt92wlclpzs | 2 | Generated with loop on. Unedited. Set loop on import. |
| pickup.mp3 | 0.48 s | −1.6 dB | cmwk1jixd01mw3gt9rdtzqukz | 2 | None. 0.48 s was the shortest the generator gave. |
| place.mp3 | 0.60 s | −1.2 dB | cmwk1jjrx01n23gt983v6ctw7 | 2 | None |
| till.mp3 | 0.45 s | −5.3 dB | cmwk1jkov01rz30qknxxrirnr | 2 | Trimmed the silent tail, +12 dB (it was very quiet), fade-out |
| water.mp3 | 0.50 s | −2.7 dB | cmwk1jlkv01s730qkg7v45jz9 | 2 | Trimmed 0.5 s of silent tail, fade-out |
| swell.mp3 | 1.15 s | −5.0 dB | cmwk1jmh601sd30qkgbty4t8y | 2 | Trimmed 50 ms of leading silence |
| pop.mp3 | 0.50 s | −0.9 dB | cmwk1jn9p01nh3gt9syvx7c7b | 2 | Trimmed the tail, fade-out |
| nailgun.mp3 | 0.30 s | −1.4 dB | cmwk1jo5b01np3gt9ftg9kdil | 2 | Trimmed to 0.3 s, fade-out |
| hit.mp3 | 0.18 s | −0.8 dB | cmwk1joz301nv3gt9q4eucyro | 2 | Trimmed 0.1 s of leading silence and the tail |
| levelup.mp3 | 0.85 s | −6.7 dB | cmwk1jpui01o13gt9fhwjhae5 | 2 | Trimmed the tail, fade-out |
| siren.mp3 | 2.90 s | −4.0 dB | cmwk1jqtx01o73gt9f8r4ki22 | 2 | Trimmed 0.1 s of leading silence |
| rocket.mp3 | 1.67 s | −3.4 dB | cmwk1jrts01sw30qkx1psyhwx | 2 | Trimmed leading silence and 0.7 s of silent tail, +5 dB. The bright, bell-like burst at about 1.0–1.2 s is probably the "ka-ching" (not checked by ear). |

The edited SFX were re-encoded with ffmpeg/libmp3lame (VBR q2). Durations and peaks above were measured with ffprobe and ffmpeg volumedetect.
