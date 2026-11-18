# yaratik2 kanıtı (18 Kasım 2026)

- `y2_dizi_normal_003.50.png`: `--creatureview=wild --row=normal`. Soldan: Gıdak, Pofuduk, Homur, Sinsi (kaya arkasında), Dallı, Hu, Vıcık, Bozuk, Vızvız (kovan + arılar), Fındıkçı; Çekirge sürüsü Homur'un önünde.
- `y2_dizi_sisik_003.50.png`: aynı dizi `--row=swollen` (şişme shader'ı, maya kesesi, sürülerde şişik üye sayısı).
- `y2_bozuk_00{2,6}.00.png`, `y2_cekirge_003.00.png`, `y2_vizviz_003.00.png`, `y2_hu_003.00.png`, `y2_dalli_003.00.png`, `y2_homur_003.00.png`: `--creatureview=wildday --species=<id>`, oyuncunun önünde 3'lük grup, gündüz davranışı.
- `y2_wild_003.50.png`, `y2_scale_003.50.png`: ilk Homur dilimi (ölçek ve şişme).

Testler: `--test=creatures` → `RESULT ok creatures=10 wild=11 clips=80 posed=74`; Sim `WildTests` (12) ve `VehicleTests` (3).
Gövdeler koddan yer tutucu (`data/bodies.json`); Tripo isteği `pipeline/requests/yaratik2.json`.
