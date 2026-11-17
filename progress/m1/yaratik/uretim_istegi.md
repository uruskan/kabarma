# Yaratik akisi: uretim istegi (kredi harcanmadi)

Alt oturum kredi harcamaz (CLAUDE.md). Asagidaki is ana oturumun onayiyla, onun calistirmasi icindir.

## 1. Zipzip yurume klibi (tek istek): TAMAMLANDI, 10 kredi

**Durum (2026-11-18):** ana oturum uretti: gorev `446094f2-9317-4d25-ac9b-4f1f2d25ac89`, 10 kredi (Tripo API,
bakiye 310), `animate_in_place=true`. Ham cikti
`.tools/tripo/zipzip/anim-walk/tripo-out/zipzip-walk-retarget-446094f2/model.glb`. `pipeline/merge_clips.py --only walk`
ile `zipzip_5k.glb`'ye eklendi (cikti klipleri hurt, idle, jump, run, walk; eslesmeyen kemik yok:
`zipzip_5k_walk_merge.report.json`). Veride `clips.walk: "glb:walk"`, `placeholder: []`. Zipzip'in 8 yuvasindan 5'i
artik Tripo klibi (idle, walk, run, attack1=jump, hit=hurt), 3'u poz (attack2, eat, rest). Asagisi istegin ilk hali.

| Alan | Deger |
|---|---|
| Ne | Zipzip'in var olan Tripo iskeletine `preset:walk` animasyonu (animate_retarget) |
| Adet | 1 klip |
| Havuz | Tripo API (tripo-cli). 2026-11-17'de onaylanan 175 kredilik tavanin icinden |
| Tahmini kredi | **~10** (onceki retarget-4b2662d5: 4 klip = 40 kredi, "billed per animation") |
| Girdi | iskelet gorevi `ffa5acf2-146b-4485-b2ae-75fedd639433` (`.tools/tripo/zipzip.json`, animate_rig, 55 kredi harcanmisti) |
| Ham cikti | `.tools/tripo/zipzip/anim-walk/` altinda gorev klasoru (`model.glb`, tam cozunurluk) |
| Oyuna giren cikti | `game/assets/models/zipzip/zipzip_5k.glb` (ayni dosya; yurume klibi eklenir, ucgen sayisi degismez: 4830) |
| Yer tutucu isareti | `game/data/creatures.json` -> `zipzip.rig.placeholder: ["walk"]`, `clips.walk: "pose"` |

Su an yurume, poz kutuphanesinin iki ayakli planindan (`game/data/poses.json` -> `biped.walk`) koddan uretiliyor; oyun
yer tutucuyla calisiyor, istek zorunlu degil. Diger yedi yuva: idle/run/attack1(jump)/hit(hurt) Tripo klibi,
attack2/eat/rest poz.

Ilk dort klip `pipeline/gen_golden.sh` satir 53 ile `--animate-in-place` (kok hareketi yok) uretildi; yurume de
ayni bicimde istenir, yoksa yerinde oynayan idle/run yaninda kok hareketli bir walk gelir.

### Kuru calistirma (cevrimdisi, kredisiz)

```
tripo make ffa5acf2-146b-4485-b2ae-75fedd639433 --then "retarget:animation=preset:walk,animate_in_place=true" --dry-run --json
```

Sonuc (2026-11-18): `valid=true`, `errors=[]`, adim `animate_retarget`
`{"animation":"preset:walk","animate_in_place":true}`, `generation_credits_estimate=null` ("cost not estimated (not
zero credits)"; "retarget 1 animation(s): billed per animation"). Uyari: "chain legality unverified: upstream task
type unknown (no local history ...)": gorev `.tools/tripo/zipzip.json`'da animate_rig olarak kayitli; gercek kosu
bunu API ile denetler. (`--then "retarget:preset:walk:animate_in_place=true"` bicimi gecersiz: "retarget requires
animation".)

### Calistirma (ana oturum, onaydan sonra)

Kuru calistirmasi yapilan bicim (onerilen):

```
cd C:\Users\uruskan\Documents\GitHub\kabarma\.tools\tripo
tripo make ffa5acf2-146b-4485-b2ae-75fedd639433 --then "retarget:animation=preset:walk,animate_in_place=true" --out zipzip\anim-walk --name zipzip-walk --json --yes --no-open > zipzip.walk.json 2> zipzip.walk.err
```

gen_golden.sh'nin bicimi (`tripo anim retarget <rig> --animation preset:walk --animate-in-place ...`) ayni istegi
yapar ama `--dry-run` desteklemez.

Sonra kredisiz birlestirme (Blender 5.3; `pipeline/merge_clips.py`, bu dalda, var olan kliplerle sinandi):

```
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b -P pipeline/merge_clips.py -- --base game/assets/models/zipzip/zipzip_5k.glb --clips C:\Users\uruskan\Documents\GitHub\kabarma\.tools\tripo\zipzip\anim-walk\<gorev>\model.glb --only walk --out game/assets/models/zipzip/zipzip_5k.glb
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
