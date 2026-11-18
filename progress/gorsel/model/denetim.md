# Model denetimi (gorsel-model akışı, 18 Kasım 2026)

Her model aynı stüdyo ışığında, oyunun kurduğu yolla (sınıfıyla ya da `Assets.Load`), yanında oyuncu modeliyle (SANAT-SES §3: 1,6 m):
`--test=lineup` → `once/` (bu dalın başı) ve `sonra/`. Temas föyleri `once/sheet.png`, `sonra/sheet.png`; seçili 10 model yan yana `karsilastirma.png`; oyun içi çift `oyun/oyun_once.png` ile `oyun/oyun_sonra.png` (aynı kare, `--no_uplift` ile; yalnız çayır ve Çapa görünüyor, kapsül ve üs bu karede yok).

**Ortak kök neden:** Tripo API modellerinin hepsinde yalnız renk dokusu var; malzeme roughness 0,9, metal 0. Her yüzey aynı tebeşir ya da kil gibi ışık alıyor: "plastik oyuncak". Buna doygun mavi, sarı ve mor palet ile yuvarlak, şişkin biçimler ekleniyor.

Kusur kodları: **D** düz renk / malzeme ayrımı yok · **P** plastik, tebeşir ışık · **B** oyuncak biçim (şişkin, balon) · **A** düşük ayrıntı · **K** koddan çizilmiş ilkel · **Ö** ölçek ya da oran.

## Sıralama (en kötüden) ve yapılan

| # | Model | Kusur | Bu dalda (kredisiz) | Kalan / istek |
|---|---|---|---|---|
| 1 | İniş kapsülü `kapsul` | B, P, D | PBR pişirme: boya kenarında metal, alttan is ve toprak, gerçek AO; iç kabuk UV çakışması ayıklandı | Kapak-rampa ve iniş ayağı **Blender modeli** (`props/kapsul_kapak.glb`, `props/kapsul_ayak.glb`); gövde Tripo Ultra (50 kredi). Bağlama hazır (`Pod.Model.cs`, `data/kapsul.json`) |
| 2 | Kargo Rampası pisti (Pod `_pad`) | K | **Blender modeli** `props/rampa_pisti.glb`: çelik çerçeve, baklava sac, uyarı şeridi, köşe direkleri, cıvatalar | yok |
| 3 | Kargo roketi (Pod `_rocket`) | K | **Blender modeli** `props/kargo_roketi.glb`: panel derzleri, RCS, kanatlar, motor çanı, yanık izi | yok |
| 4 | Ocakbaşı | K | **Blender modeli** `props/ocakbasi.glb`: taş halka, kül, kömürleşmiş odun, dökme demir ızgara, emaye tencere | yok |
| 5 | Çapa `capa` | B, P | PBR: boya gri-metal, kenar aşınması | Yeniden üretim (30). Ekranda hep görünür |
| 6 | Ocak `ocak` | B, D | PBR: taş, is, metal baca; mavi kubbe boyalı sac | Yeniden üretim (30) |
| 7 | Tezgâh `tezgah` | B, P | PBR: ahşap tane, boyalı çerçevede atma | Yeniden üretim (30) |
| 8 | Paketleme `paketleme` | B, A | PBR | Yeniden üretim (30) |
| 9 | El Arabası `el_arabasi` | P, ince çerçeve | PBR: boyalı tekne, lastik, ahşap sap | Yeniden üretim + parçalama (70) |
| 10 | Taş Fırın `tas_firin` | B | PBR | Yeniden üretim + parçalama (70), kesme çizgisinin altında |
| 11 | Cevherli kaya `kaya` | B, D (mor topak, sarı yumru) | **Kütüphaneden değiştirildi:** Poly Haven `boulder_01` (CC0), `lib/kaya/kaya.glb` (`biomes.json` nodes) | yok |
| 12 | Çayır ağacı `agac` | B (lolipop taç) | Yalnız albedo: doygunluk 0,8, gövdede AO | Tripo web'de yeniden üretim önerisi (tahmin yok); oyun içinde "bebek" hissini en çok veren |
| 13 | Sulama kovası `kova` | B, P | Yalnız albedo | — |
| 14 | Turuncu ağaç, çalı, ot, buğday, çiçek, eğrelti | P | PBR (bitki sınıfı, hafif AO) | — (dunya akışı) |
| 15 | Kaya (düz, dik), bakır, kil yatağı | P | Düz ve dik kaya **kütüphaneden** (Poly Haven CC0 `rock_moss_set_02`, `boulder_01`; `lib/`); bakır ve kil PBR (bakır cevheri metal, kil). Sahibin `stone_node`/`metal_node`'u denendi: sadeleştirmede kırıldı ve Rust'a çok benziyor | — |
| 16 | Şalgam `salgam` | — | PBR (hafif) | — |
| 17 | Ahşap yapı kiti (28 parça), Sandık, Uyku Tulumu, Meşale, Kargo Kasası, Ahşap Kapı, Yemlik, Folluk | — | Dokunulmadı: zaten yordamsal ahşap albedo + normal + rm (`build_kit.py`); bu dildeki en olgun varlıklar, ölçü çubuğu | — |
| 18 | Yaratıklar (Zıpzıp, Gıdak, Pofuduk) | B (kimlik gereği sevimli) | Dokunulmadı (yaratik akışı; `Swellable` normal'i devralır) | Kimlik kararı sahibin; istek yazılmadı |
| 19 | Oyuncu | — | Dokunulmadı (oyuncu akışı) | — |

## Ölçek ve oran

Bütün modeller oyuncunun yanında denetlendi: **ölçek hatası bulunmadı**, değişiklik yapılmadı. Kapsül 2,8 m, Ocak 2,0 m, Tezgâh 1,5 m, El Arabası 0,85 m, Çapa 0,35 m (elde), kova 0,55 m, ağaç 4,2 m, kapı 2,3 m; hepsi 1,6 m'lik oyuncuya göre makul. `once/` föyünde Gıdak ve iki temel parçası boş görünüyordu: vitrin hatası (Gıdak ham GLB'den yüklenmişti, temeller y=0'ın altındaydı), oyun hatası değil; vitrin düzeltildi.

## Koddan çizilen ilkel şekiller (MeshKit) taraması

`new MeshKit()` geçen her yer:

| Yer | Durum |
|---|---|
| `Pod.cs` pist ve roket | **Değiştirildi** (Blender modeli, `Pod.Model.cs`) |
| `Placeables.cs` OcakbasiVisual | **Değiştirildi** (Blender modeli) |
| `Pod.cs` gövde/kapak, `Ocak.cs`, `Placeables.cs` (kutu, Meşale), `ElArabasi.cs`, `CargoCrateNode.cs`, `Gidak.cs`, `Zipzip.cs`, `Rock.cs`, `Player.cs` (gövde, Çapa), `FarmView.cs` (şalgam) | Yalnız model dosyası yoksa devreye giren yedek; oyunda görünmüyor |
| `FarmView.cs` PlotView toprak parseli | **Açık, canlı:** 3×3 tarlanın yastık biçimli toprak tümsekleri. capa akışının dosyası; flaş için canlı toon malzemesi kullanıyor. Öneri: Blender'da sürülmüş toprak + ahşap kenar tahtası modeli, malzeme parametreleri korunarak |
| `PenSystem.cs`, `Gidak.cs` yumurta | **Açık, canlı:** düz beyaz küre; küçük. ilkgun/yaratik akışı |
| `Director.Oz.cs` Öz tanesi, `Fx.cs` uçan parça | Efekt (ışıyan Maya tanesi); model sayılmadı |
| `World.Sky.cs` ufuk tepeleri | gorsel-dunya akışı |
| `SwarmTest.cs` | Sınama |
