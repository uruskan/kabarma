# Üretim isteği: görsel yenileme (gorsel-model akışı, 18 Kasım 2026)

Bu dal **hiç kredi harcamadı**. Aşağıdaki üretimleri ana oturum, sahibin o tutara onayıyla çalıştırır.

- Betik: `pipeline/gen_gorsel.py` (varsayılan `--dry-run`, kredi harcamaz). Üretim: `python pipeline/gen_gorsel.py --run <ad>`.
- Tahmin kaydı: `progress/gorsel/model/gen_gorsel_dry_run.log` (tripo CLI `--dry-run --json`, ham JSON dahil). CLI parçalama adımını tahmine katmıyor; M0'da ölçülen 40 kredi elle eklendi.
- Ortak ayar: `--model tripo-v3.1 -p texture_quality=detailed -p texture_version=v3.5 -p pbr=true` (pbr=true kabul edildi: yeni modeller normal + metal/pürüzlülük haritalarıyla gelir).
- Çıktı (git dışı): `C:/Users/uruskan/Documents/GitHub/kabarma/.tools/tripo/<ad>_v3/`.
- Yeni stil cümlesi (her istemin sonunda; eski "toy-like, chunky soft, no thin spikes" dili bırakıldı): *Semi-stylised high-end game asset, mature and believable, grounded PBR materials with real wear: chipped paint on edges, grime in crevices, scorch and rust streaks, crisp bevelled hard-surface detail with panel seams, bolts and rivets, strong readable silhouette. Not cute, not toy-like, not cartoon, no plastic look. Muted off-white, cobalt blue and safety-yellow accents for a frontier farming co-op on a warm golden-meadow moon.*

## Liste ve kredi

Tripo API bakiyesi 310. Sıra öncelik sırasıdır; kesme çizgisi bakiyenin içinde kalan son satırın altındadır.

| # | Ad | Neden | Kaynak | Tahmin | Birikimli | Hedef dosya |
|---|---|---|---|---|---|---|
| 1 | `kapsul_govde` | Sahibin 1 numaralı şikâyeti: oyuncak balon kapsül. İlk kare, Kargo Rampası | metin (ya da agy referansı) | 30 | 30 | `kapsul/kapsul.glb` (`body`, `base`) |
| 2 | `kapsul_kapak` | Kapak = rampa; ayrı model: temiz menteşe pivotu | metin | 30 | 60 | `kapsul/kapsul_kapak.glb` |
| 3 | `kapsul_ayak` | İniş ayağı (4 örnek, kodla yerleşir) | metin | 30 | 90 | `kapsul/kapsul_ayak.glb` |
| 4 | `capa` | Ekranda her an oyuncunun elinde; tabanca oyuncak | metin | 30 | 120 | `capa/capa.glb` |
| 5 | `ocak` | Üssün kalbi; mavi kubbe ve topak biçim | metin | 30 | 150 | `ocak/ocak.glb` |
| 6 | `tezgah` | Mavi oyuncak çerçeve, silueti koltuk | metin | 30 | 180 | `tezgah/tezgah.glb` |
| 7 | `paketleme` | Dağınık oyuncak; işlevi okunmuyor | metin | 30 | 210 | `paketleme/paketleme.glb` |
| 8 | `el_arabasi` | İnce boru çerçeve, oyuncak tekne; teker ayrı parça | metin + parçalama | 30 + 40 = 70 | **280** | `el_arabasi/el_arabasi.glb` |
| | **kesme çizgisi (310)** | | | | | |
| 9 | `tas_firin` | Kabarcık biçim; kapak ve baca ayrı parça | metin + parçalama | 30 + 40 = 70 | 350 | `tas_firin/tas_firin.glb` |

- **Kesme çizgisinin üstü: 280 kredi** (bakiyede 30 kalır). **Tam liste: 350 kredi.**
- En kötü durum (SANAT-SES §9: en fazla 4 deneme): kapsülün üç parçası için 4 deneme = 360; tüm liste için 1.400. Öneri: kapsülde agy referansıyla tek deneme, beğenilmezse yalnız gövde yeniden.
- **Risk:** metinden 3B istemlerdeki "door NOT included / legs NOT included" çoğu zaman yok sayılır. Gövde kapak ya da ayakla birleşik gelirse çare parçalamadır (+40): kesme çizgisinin üstü 280 → 320 olur ve 310'u aşar. O durumda önerim `paketleme`yi (30) sonraya bırakmak: 290. Gövdede agy referansı (kapısız, ayaksız çizilmiş) bu riski azaltır.
- Kapsül tek model + parçalama (30 + 40 = 70) yerine üç ayrı model (90) öneriliyor: parçalama kapağı ve ayakları güvenilir ayırmıyor; ayrı model temiz pivot ve döndürülebilir kapak verir. 20 kredi fark.
- Sırada olmayan ama düşünülmesi gereken (kesme çizgisinin altında, tahmin edilmedi): Çayır ağacı `agac` ve cevherli kaya `kaya` (Tripo web, havuz 3.200). Oyun içinde "bebek" hissini en çok bu ikisi veriyor (lolipop taçlar, mor topak kaya ve sarı yumrular). Bu dalda yalnız albedo derecelendirmesi aldılar. Yaratıklar (Zıpzıp, Gıdak, Pofuduk) kimliğin parçası; yaratik akışına bırakıldı.

## agy referans istemleri (çalıştırılmadı)

Bu oturum `agy`'yi çalıştıramadı: çağrı Claude Code izin sınıflandırıcısında reddedildi. Ana oturum, sahibin izniyle, referansı üretip `progress/gorsel/model/ref/<ad>.png` olarak koyarsa `gen_gorsel.py` o varlığı metin yerine görselden üretir (image-to-model; **kredisi tahmin edilmedi**: referans dosyası olmadığı için dry-run metinle koştu; görsel girişin bedeli ilk dry-run'da görülmeli). Önerilen yalnız kapsül gövdesi ve Ocak için:

```
agy -p "Generate ONE image with your image generation tool and save it as kapsul_govde.png in the current directory. Do not run shell commands. Concept art reference for an image-to-3D generator: a single rugged industrial planetary landing capsule hull, three-quarter front view, plain light grey studio background, soft even light, whole object in frame, no people, no text. Squat truncated-cone body about 3 m tall, heavy scorched ablative heat-shield ring at the bottom, off-white painted steel hull plates with panel seams, rivets and chipped worn paint, muted cobalt-blue and safety-yellow bands, a large rectangular door opening on the front (empty recessed frame, door not included), small round porthole, recessed grab handles, RCS thruster quads, short antenna mast, stencilled hazard chevrons, four leg mount brackets near the base (legs not included). Mature semi-stylised survival-game look (Astroneer polish, Rust/Valheim material seriousness). Not cute, not toy-like."
```

```
agy -p "Generate ONE image with your image generation tool and save it as ocak.png in the current directory. Do not run shell commands. Concept art reference for an image-to-3D generator: a compact frontier hearth furnace about 2 m tall, three-quarter front view, plain light grey studio background, whole object in frame, no text. Heavy stone and mortar base, cast-iron firebox with a big glowing fire door on the front, riveted dark steel hood, tall flue pipe with spark arrestor cap, brass pressure gauge and valve wheel on the side, small cobalt-blue co-op badge plate, soot around the door. Mature semi-stylised survival-game look. Not cute, not toy-like."
```

## Oturtma (yeni model gelince)

Ortak adımlar, her model için:

1. `pipeline/golden_manifest.json` → o varlığın `"in"` yolunu `.tools/tripo/<ad>_v3/.../model.glb` yap (yeni kapsül parçaları için satır ekle: `kapsul_kapak`, `kapsul_ayak`, `"class": "makine"`).
2. `python pipeline/run_golden.py <ad>`: temizlik, boy, taban, LOD, kabuk, ikon, `assets.json`.
3. Malzeme: yeni model `pbr=true` ile kendi normal ve metal/pürüzlülük haritasını getirir. `pipeline/pbr_manifest.json`'da o varlığa `"keepMaps": true` yaz ve `pbr_bake.py --only <ad>` çalıştır (yalnız albedo derecelendirmesi). Ya da satırı sil ve `materials.json`'daki kaydını kaldır. Unutulursa zarar yok: `MaterialUplift` yalnız tam malzeme adıyla eşleşir; Tripo adı görev kimliğini taşıdığından eski haritalar yeni modele takılmaz, yalnız `UPLIFT ... yuzeyi yok` uyarısı çıkar.
4. `& $G --headless --path game --import`, sonra `--test=lineup --lineup_only=<ad> --lineup_tag=yeni`.

Varlığa özel:

| Ad | `run_golden` ayarı | Oyun bağlaması |
|---|---|---|
| `kapsul_govde` | `height 3.0, tris 14000, class makine`; `--parts` yok (tek gövde) ya da Tripo `body/base` ayrımı | `Pod.cs` aynı yolu yükler (`kapsul.glb`, −90° Y). **Yeni gövdede `hatch` düğümü olmamalı**: varsa `Pod._Ready` onu menteşeye takar ve ayrı kapak takılmaz. Çarpışma silindiri (`Pod.cs` r 1,35 / h 2,6) 3 m gövdeye uyar; gerekirse ekonomi akışı. |
| `kapsul_kapak` | `height` yerine en uzun kenar 1,6 m (`data/kapsul.json` `hatchLength` zaten ölçekler); `tris 5000`, `class makine` | `Pod.Model.cs` `_EnterTree`: menteşe `hinge` [0, 0.5, 1.28]; model yatık üretilir, menteşe kenarı −X. `hatchClosedRotation` [0, 90, 90] yatık plakayı dik kapıya çevirir. **Model ters gelirse** (boyalı yüz içeride) Y'yi tersle: [0, -90, 90] (Godot YXZ sırası; menteşe altta kalır). Bu dal yer tutucu bir kirişle denendi: kapalıyken dik, rampa açılınca +Z'ye yatıyor (`bagla_deneme/kapak_deneme_*.png`). Açılış animasyonu (`Pod.Hatch.cs`, `Pod.cs` rampa) değişmeden çalışır. |
| `kapsul_ayak` | `tris 3000`, `class makine`, `forward +X` | `data/kapsul.json` `legs`: dört üst bağlantı (Pod yerel, ±0,95 / 0,97 / ±0,95; yaw, 28° dışa eğim), `legLength` 1,1 m; ayak bağlantıdan aşağı sarkar, tabanı yere değer (y = 1,1 × cos 28°). Yer tutucu modelle denendi (`bagla_deneme/kapsul.png`). Ayaklar gövde yeni geldiğinde bağlantı braketlerine göre konum ayarı ister (yalnız veri). |
| `capa` | eskisi gibi (`golden_manifest` satırı) | Değişiklik yok (`Player.cs` aynı yolu yükler). Elde yön ve tutma noktası oyuncu akışında. |
| `ocak` | eskisi gibi | Değişiklik yok (`Ocak.cs`). Ateş ağzı önde olmalı: ışık `(0, 1.2, 0.9)`. |
| `tezgah`, `paketleme` | eskisi gibi | Değişiklik yok (`placeables.json` `model`). |
| `el_arabasi` | `--parts --rename` yeni parçalamaya göre yeniden eşlenir: `body`, `wheel`, `frame_l/r`, `grip_l/r`, `fork`; boş düğümler `wheel_pivot`, `handle_l/r`, `handle`, `load_slot` (bkz. varlik notu) | `ElArabasi.cs` düğüm adlarıyla çalışır; adlar korunursa kod değişmez. |
| `tas_firin` | `--parts --rename` → `body, base, door, door_handle, chimney, panel`; `--pivots chimney=bottom` | `TasFirin` (Pofuduk.cs) değişmeden çalışır. |

## Bu dalda kredisiz yapılanlar (yeniden üretim gelene kadar)

- 21 modele pişirilmiş PBR (`pipeline/pbr_bake.py`, `game/data/materials.json`). 18'inde normal, ORM ve albedo; Tripo web'den gelen 3'ünde (kaya, ağaç, kova) yalnız albedo.
- Koddan çizilen 3 ilkel varlık Blender modeline çevrildi (`pipeline/build_props.py`): Ocakbaşı, Kargo Rampası pisti, kargo roketi.
- Kapsülün parçalı bağlaması hazır (`Pod.Model.cs`, `data/kapsul.json`); kapak ve ayak dosyaları gelince kendiliğinden takılır.
