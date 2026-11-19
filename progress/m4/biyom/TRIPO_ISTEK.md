# Biyom akışı: üretim isteği (kredi harcanmadı; ana oturum Tripo web'de üretir)

Bugün dört yeni biyom **mevcut modellerin tonlanmış hâliyle** çalışır (`biomes.json` `decor.tint`, `nodes.tint`; her satırda `placeholder` işareti). Aşağıdakiler gerçek modellerdir. Tripo web H3.1 (iş başına 55), `low poly` sınırı 2 000 000, renk adı düz ("blue-green" değil "forest green"); ölçü ve işlem kuralları Dalga 1 gibi (`pipeline/build_bitki.py`, `tripo_prop` / `tripo_tree`). Öncelik sırası: yukarıdan aşağı.

## Dekor (öncelik 1: biyomun karakterini taşıyanlar)

| id | biyom | istem | boy |
|---|---|---|---|
| sazlik_kamis | Sazlık | tall reed clump, thin glowing violet stems with turquoise tips, wetland, stylized game prop | 2.2 m |
| sazlik_mantar | Sazlık | glowing turquoise bioluminescent mushroom cluster, swamp, stylized | 1.6 m |
| kanyon_mesa | Kanyon | red sandstone mesa rock pillar with layered strata, desert canyon, stylized | 6 m |
| kanyon_kaktus | Kanyon | tall desert cactus with two arms, dry, stylized | 3 m |
| ayaz_ladin | Ayaz | snow covered pine tree, heavy white snow on branches, stylized | 8 m |
| ayaz_buz_kristali | Ayaz | tall pointed ice crystal cluster, pale blue, translucent look, stylized | 3 m |
| gobek_hamur_kaya | Göbek | puffy dough-like cream rock formation, soft rounded risen bread shape, stylized | 4 m |
| gobek_maya_kristali | Göbek | glowing violet crystal cluster growing from cream stone, magical, stylized | 2.4 m |

## Kaynak düğümleri (öncelik 2; bugün kaya/kil/demir modelleri ton farkıyla)

turba (dark peat block), kükürt (yellow sulfur crystals on grey rock), kauçuk sütü (white latex oozing vine knot), balçık (grey-green sticky clay mound), ışıl yosun (glowing turquoise moss mound), kum (sand pile), kuvars (white quartz crystal cluster), titan cevheri (blue-grey metallic ore rock), katran (black tar blob), tuz (white salt crystal block), buz (blue ice block), ayaz kristali (cyan crystal cluster), ham maya (violet bubbling dough blob), obsidyen (black glassy rock shard), ışıl cevher (teal glowing ore rock), kemik yığını (pile of bleached animal bones). Her biri yaklaşık 1 m, `tripo_prop` (2–6k üçgen), mevcut düğümlerle aynı ölçü (`nodes.size` 0.8–1.4).

## Keşif noktası (öncelik 3)

zengin_yatak (rich ore deposit with an empty drill platform, rocky mound with glowing ore veins, stylized, 3 m). Bugün yer tutucu: demir damarı modeli.

## Not

Su yüzeyleri (Sazlık sığ sular, Ayaz donmuş gölleri, Kanyon katran gölcükleri, Göbek Maya gölleri) model değil, shader ve zemin işi: bugün yalnız zemin tonu + düğümler; gerçek su dünya akışının sonraki işi.
