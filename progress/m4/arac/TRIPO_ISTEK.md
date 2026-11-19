# Arac akisi: uretim istegi (kredi harcanmadi; ana oturum Tripo web'de uretir)

Bugun 6 arac ve 10 eklenti **koddan yer tutucu govdelerle** calisir (`game/scripts/world/VehicleBodies.cs`; `vehicles.json` her satirda `placeholder`). Gercek modeller Tripo web H3.1 (is basina 55), `low poly` sinirini 2 000 000 tut, renk adlarini duz yaz. Her model +Z ileri, taban z=0, tekerlekler ayri dugum olarak (arac) ya da tek parca (eklenti); 2-6k ucgen (`tripo_prop`, pipeline/build_bitki.py). Oncelik yukaridan asagi.

## Araclar

| id | istem | boy (m) |
|---|---|---|
| traktor | small red farm tractor, big rear wheels, open cab with roof, exhaust pipe, tow hitch at the back, stylized game vehicle | 3.6 uzun |
| kamyon | blue flatbed cargo truck, wooden crate bed for six crates, rounded cab, stylized game vehicle | 5.5 uzun |
| bicerdover | yellow combine harvester with wide front cutting header and reel, glass cab on top, stylized | 6 uzun |
| hava_yastikli | white hovercraft with black rubber skirt, four seats, rear propeller cage, stylized | 4 uzun |
| zeplin | small cargo airship, cream balloon envelope, wooden gondola with six seats, tail fins, two side propellers, stylized | 12 uzun |
| cekirge | (mevcut yer tutucu) olive ATV, two seats, small cargo rack, stylized | 2 uzun |

## Eklentiler (hepsi 1-3 m, araca takilir)

romork (small wooden two-wheel farm trailer) · pulluk (three-blade plough, steel) · ekim_makinesi (seed drill with three tubes and hopper) · su_tankeri (blue water tank trailer, horizontal cylinder) · vinc (small crane arm with hook on a post) · kepce (front loader bucket) · zirh_tampon (steel bull bar bumper) · arac_kulesi (round gun turret mount, no gun) · arac_projektoru (round spotlight on a bracket) · balon (small hot-air balloon canopy with ropes).

## Not

Zeplin Hangari modeli (placeable `zeplin_hangari`) zincir akisinin yer tutucusu; arac akisi yalnizca islevini ekledi. Araclarin sesi (motor, pervane) ses akisinin isi; bugun sessiz.
