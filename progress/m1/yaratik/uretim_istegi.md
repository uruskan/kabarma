# Yaratik akisi: uretim istegi (kredi harcanmadi)

Alt oturum kredi harcamaz (CLAUDE.md). Asagidaki is ana oturumun onayiyla, onun calistirmasi icindir.

## 1. Zipzip yurume klibi (tek istek)

| Alan | Deger |
|---|---|
| Ne | Zipzip'in var olan Tripo iskeletine `preset:walk` animasyonu (animate_retarget) |
| Adet | 1 klip |
| Havuz | Tripo API (tripo-cli). 2026-11-17'de onaylanan 175 kredilik tavanin icinden |
| Tahmini kredi | **~10** (onceki retarget-4b2662d5: 4 klip = 40 kredi, "billed per animation") |
| Girdi | iskelet gorevi `ffa5acf2-146b-4485-b2ae-75fedd639433` (`.tools/tripo/zipzip.json`, animate_rig, 55 kredi harcanmisti) |
| Ham cikti | `.tools/tripo/zipzip/anim-walk/tripo-out/` (`model.glb`, tam cozunurluk) |
| Oyuna giren cikti | `game/assets/models/zipzip/zipzip_5k.glb` (ayni dosya; yurume klibi eklenir, ucgen sayisi degismez: 4830) |
| Yer tutucu isareti | `game/data/creatures.json` -> `zipzip.rig.placeholder: ["walk"]`, `clips.walk: "pose"` |

Su an yurume, poz kutuphanesinin iki ayakli planindan (`game/data/poses.json` -> `biped.walk`) koddan uretiliyor; oyun
yer tutucuyla calisiyor, istek zorunlu degil. Diger yedi yuva: idle/run/attack1(jump)/hit(hurt) Tripo klibi,
attack2/eat/rest poz.

### Kuru calistirma (cevrimdisi, kredisiz)

```
tripo make ffa5acf2-146b-4485-b2ae-75fedd639433 --then "retarget:preset:walk" --dry-run --json
```

Sonuc (2026-11-17): `valid=true`, `errors=[]`, adim `animate_retarget` `{"animation":"preset:walk"}`,
`generation_credits_estimate=null` ("cost not estimated (not zero credits)"; "retarget 1 animation(s): billed per
animation"). Uyari: "chain legality unverified: upstream task type unknown (no local history ...)". Gorev
`.tools/tripo/` gecmisinde var; gercek kosu bunu API ile denetler.

### Calistirma (ana oturum, onaydan sonra)

```
cd C:\Users\uruskan\Documents\GitHub\kabarma\.tools\tripo
tripo anim retarget ffa5acf2-146b-4485-b2ae-75fedd639433 --animation preset:walk --out zipzip\anim-walk\tripo-out --name zipzip-walk --json > zipzip.walk.json 2> zipzip.walk.err
```

Sonra kredisiz birlestirme (Blender 5.3; `pipeline/merge_clips.py`, bu dalda, var olan kliplerle sinandi):

```
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b -P pipeline/merge_clips.py -- --base game/assets/models/zipzip/zipzip_5k.glb --clips C:\Users\uruskan\Documents\GitHub\kabarma\.tools\tripo\zipzip\anim-walk\tripo-out\<gorev>\model.glb --only walk --out game/assets/models/zipzip/zipzip_5k.glb
```

Ve veride iki satir: `zipzip.rig.clips.walk: "glb:walk"`, `placeholder: []`. Denetim:
`tools/test.ps1 -Only creatures` (8 yuva, kemik hareketi, ucgen butcesi).

Birlestirme sinamasi (2026-11-17, kredisiz): `--only run --rename walk_test` -> cikti klipleri
`hurt, idle, jump, run, walk_test`, `pass=true`; `--only run` (ayni ad) -> `run` degistirildi, `pass=true`.

## 2. Gidak iskeleti: istek yok

`pipeline/rig_bird.py` (Blender, kredisiz) kus iskeletini kurdu: `game/assets/models/gidak/gidak_rig.glb`, 13 kemik,
3748 kose, agirliksiz kose 0, yanlis bolge 0 (`gidak_rig.glb.report.json`). Tripo kus iskeleti (~55 kredi) gerekmedi.
Gidak'in 8 klibi poz kutuphanesinin kus planindan.

## 3. Pofuduk: istek yok

Var olan Tripo dort ayakli iskeleti ve `walk` klibi kullanildi; diger yedi yuva poz.
