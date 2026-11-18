# bitki-pbr: ağaçlar ve süs bitkileri (18 Kasım 2026)

Çizgi film Tripo bitkileri, `F:\Assets`'teki dokulu (PBR) kütüphane modelleriyle değişti. Hat: `pipeline/build_bitki.py` (Blender 5.3, ayar `pipeline/bitki_manifest.json`), kayıt `pipeline/bitki_sync.py`. Lisanslar: `pipeline/licenses.md` "Bitkiler".

## Ne neyle değişti (yollar ve kimlikler aynı)

| Kimlik (biomes.json) | Eski | Yeni | LOD0 / LOD1 / LOD2 üçgen | Boy |
|---|---|---|---|---|
| `agac` | Tripo, düşük poligon yuvarlak taç | Poly Haven Jacaranda Tree: gerçek gövde + dallar, yaprak kümeleri karta pişirildi | 8.000 / 3.800 / 1.400 | 5,0 m |
| `turuncu_agac` | Tripo | Poly Haven Island Tree 01, yaprak tonu sonbahar turuncusu | 3.668 / 2.568 / 1.299 | 4,8 m |
| `cali` (Lif) | Tripo | Megascans Downy Willow | 5.000 / 2.200 / 800 | 1,1 m |
| `egrelti` | Tripo | Megascans Beech Fern | 1.098 / 550 / 220 | 0,7 m |
| `ot` (Lif) | Tripo | Megascans Cotton Grass (beyaz püsküllü; çimen katmanının üstünde okunur) | 3.600 / 1.998 / 700 | 0,9 m |
| `bugday` (Buğday) | Tripo | Megascans Dry Grass, altın tona çekildi | 3.103 / 1.957 / 700 | 1,0 m |
| `cicek` | Tripo | Poly Haven Dandelion 01, üç bitkilik öbek | 4.798 / 1.574 / 600 | 0,38 m |

Sis Korusu için hazır (oyunda değil): `res://assets/models/koknar/koknar.glb` (9 m köknar), `koknar_genc/koknar_genc.glb` (4,5 m), `kutuk/kutuk.glb` (Poly Haven kütük, 0,6 m). Koru'nun `egrelti` ve `koru_ot` satırları aynı yolları kullandığı için kendiliğinden yeni modele geçti.

## Oyunda
- `plant_wind.gdshader`: alfa makası (gölge de kesilir), pürüzlülük dokusu, yaprak arka ışığı (`BACKLIGHT`), ağaçta tac hacmi normalleri (arka yüzde çevrilmez). Eski modeller için davranış aynı (yeni değişkenler varsayılanda kapalı).
- `World.DecorLod.cs` (yeni partial): modelin yanında `_lod1.glb` / `_lod2.glb` varsa her 50 m parçaya LOD MultiMesh'leri; LOD0 < 48 m, LOD1 < 115 m, sonra LOD2 (parça merkezine). LOD0 düğüm adı aynı (`TreeChunksAt`, Biçme esnemesi çalışır). Biçme tutamlarına (harvest) LOD yok. `World.Decor.cs`'e iki satırlık kanca.
- Godot'un kendi LOD üretimi bu GLB'lerde kapalı (`generate_lods=false`): kart yaprakları bozuyor.
- Çarpışma: biomes.json'daki gövde silindiri aynı (ağaç 0,45 / 0,5 m; yeni gövde yarıçapı 0,35 / 0,43 m). `_hull.tres` yalnız gövde.
- Biçme verimleri, tutam kimlikleri, satır sırası ve sayıları değişmedi: yerleşim (tohumlu RNG) bit bit aynı.

## Kesilen ağaç ve çalı parçaları
Kodda düşen parça ya da kütük modeli yok: ağaç devrilmiyor (Capa.Bicme.cs "devrilme M2"), tutam küçülüp kayboluyor, kıymık `Fx.Burst` renk parçacığı. Değiştirilecek model olmadığı için oynanışa dokunulmadı; devrilme gelince `kutuk.glb` hazır.

## Ölçüm (öğle, 60 sn; GPU başka oturumların Godot koşularıyla dolu, kirli)
| Yer imi | Önce fps / ms / çizim / ilkel | Sonra |
|---|---|---|
| kapsül | 158,9 / 6,29 / 319 / 3,42 M | 159,2 / 6,28 / 373 / 4,10 M |
| üs | 143,8 / 6,96 / 359 / 3,65 M | 142,6 / 7,01 / 429 / 4,31 M |
| göl | 145,3 / 6,88 / 177 / 2,81 M | 129,7 / 7,71 / 209 / 3,37 M (p99 25,7 ms, kirli) |
| tepe | 124,6 / 8,02 / 358 / 3,22 M | 119,2 / 8,39 / 437 / 3,56 M |
VRAM 840 → 979 MB. Hepsi TEKNIK §10 bütçesinde (≤ 8,3 ms sınırda: tepe 8,39, kirli ölçüm); çizim ≤ 2.000. Kanıt `sonra/perf/`.

## Sahip kararı (18 Kasım 2026)
Sahip bu gerçekçi ağaçları beğenmedi (oyunun stilize kaya ve arazisine uymuyor). Ana oturum Tripo web ile stilize ama ayrıntılı ağaçlar üretecek; o zamana dek bu modeller kalır. Hat hazır: `bitki_manifest.json` `kind: "stylized"` (şablon `ornek_tripo_agac`), isteğe bağlı yaprak kartı kabuğu (`leaf_shell`). Ayrıntı `pipeline/README.md` "Bitkiler".

## Tripo ağaçları (18 Kasım 2026, ikinci tur)
Sahip PBR kütüphane ağaçlarını beğenmedi; ana oturum Tripo web (H3.1) ile altı ağaç üretti (ham GLB ~1,9 M üçgen, 4K, yapraklar ve gövde tek kaynaşmış mesh). Hat `pipeline/build_bitki.py` `kind: "tripo_tree"`:
- Yüzler doku rengine göre yaprak / odun ayrılır (`leaf_rule`: varsayılan yeşil kuralı ya da ton aralığı + doygunluk).
- Odun (gövde, dallar, kökler) seyreltilir, yeni UV açılır, albedo + normal + pürüzlülük yüksek poligondan pişirilir.
- Yapraklar konum + yönle 900 kümeye ayrılır; her küme kendi düzleminden tek bir 4K atlasa pişirilir ve o düzlemde alfa makaslı bir kart olur. Kalın, yuvarlak Tripo yaprak görünüşü dokuda kalır (erimiş topak ya da delik yok). LOD2'de en büyük kümeler büyütülerek kalır.
- Seçici ton kaydırma (`hue_map`): koru ağacının camgöbeği yaprakları (165–215°) orman yeşiline (≈132°), doygunluk ×0,62; yosun ve kabuk aynı.

| Satır (biomes.json) | Kaynak | LOD0 / LOD1 / LOD2 üçgen | Boy |
|---|---|---|---|
| `agac` | meşe | 5.400 / 3.100 / 1.100 | 5,0 m |
| `agac_hus` (yeni, Çayır listesinin sonunda) | huş, yaprak meşeye yaklaştırıldı | 5.400 / 3.100 / 1.100 | 5,2 m |
| `turuncu_agac` | turuncu ağaç | NOT sonrası rapora bakın | 4,8 m |
| `cali` | çalı (Biçme Lif) | rapora bakın | 1,2 m |
| `koru_agac` | yosunlu kadim ağaç, ton düzeltildi | 5.400 / 3.100 / 1.100 | 8,0 m |
| `koru_ladin` (yeni, Koru listesinin sonunda) | uzun ladin | rapora bakın | 12 m |

Yeni satırlar listelerin sonunda: öncekilerin yerleşimi (tohumlu RNG) değişmez. Önceki kütüphane ağaçlarının ayarları manifestte `disabled` (`agac_jakaranda`, `turuncu_agac_pbr`, `cali_pbr`).
