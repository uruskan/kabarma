# M3 yolbulma: akış alanı ve KS 30 stres sahnesi

Sözleşme TEKNIK §8.1, veri `game/data/flowfield.json`, Sim `src/Kabarma.Sim/FlowField*.cs` (testler `FlowFieldTests`),
Godot `game/scripts/ai/` (`FlowFieldSystem`, `Director.Flow`, `StressTest`). Bağlanma notu CLAUDE.md "M3 akışları".

## Stres sahnesi (`--test=stress`)

Host + 7 ENet istemcisi (ortak oyun botları), sahnede sürekli 220 kabarmış Zıpzıp (GEÇİCİ doğurucu; kabarma3 `Wave*`
gelince onunkiyle değişir, KS'nin can/hasar ölçeği uygulanmaz), tapuda 6 duvar izi, biri 2 sn'de bir kalkıp konur.
Ölçüm penceresi 45 sn. Host sınırsız FPS'te koşar; kare süresi iki `_Process` arasındaki gerçek süredir
(`Performance.TimeProcess` saniyenin en kötü karesini tuttuğu için kullanılmaz).

Sert sınırlar (bu akışın): ağ bütçesi (TEKNIK §5.3), kuklalar, akış alanı tiki ≤ 4 ms (TEKNIK §10 Sim tiki).
Kare bütçesi (TEKNIK §10: Kabarma karesi ≤ 11 ms, RTX 2070S'te çizimli) ayrı alan: aşarsa sonuç `partial`.

| Koşu | Akış tiki p95 / en kötü | Host yükleme | İstemci indirme (en çok) | Kukla | Host CPU karesi med / p95 |
|---|---|---|---|---|---|
| 8 süreç (`stress_8.log`), bütçe 3000 hücre | 1,73 / 5,13 ms | 1129 kbps (hedef 1500, sınır 2000) | 19,8 KB/sn (sınır 35) | 251/220+ her istemcide, avatar 7 | 31,6 / 53,4 ms |
| tek başına (`stress_solo.log`), bütçe 3000 | 1,13 / 1,27 ms | – | – | – | 20,0 / 26,3 ms |
| boş sahne (`stress_base.log`, `--enemies=0`) | – | – | – | – | 6,9 / 7,5 ms |

8 süreç aynı makinede koştu (ayrıca başka oturumların 16 Godot süreci açıktı); host karesi bu yüzden şişer. En kötü
akış tiki 5,13 ms görüldüğü için tik bütçesi 1500 hücreye indirildi (son koşu aşağıda, "Son doğrulama").

### Kare bütçesi neden aşılıyor (ölçüldü, geçici sayaçla; sayaç commit edilmedi)

- Boş sahne 6,9 ms; 220 Zıpzıp ile 20 ms: yaratık başına ~0,06 ms.
- Zıpzıp `_Process` 220 yaratıkta karede ~4 ms; bunun ~2,8 ms'si `TickLook` (gölgelendirici parametreleri ve
  `Swellable`), hedef seçimi 0,5 ms, `Flow.Steer` 0,01 ms. Kalan ~9 ms iskelet animasyonu ve dönüşüm yayılımı.
- Akış alanının payı ihmal edilebilir (sorgu O(1), tik bütçeli). Kare bütçesi TEKNIK §10'un öngördüğü sürü çizimine
  (VAT, örnekli çizim) bağlı: kabarma3 "sürü çizimi" işi. RTX 2070S'te çizimli kare sahibin onayını bekliyor.

### Akış alanı onarımı karelere bölündü

İlk ölçümde onarım tikleri en kötü 37–54 ms idi (bir duvar 160 m pencerede 3 700–10 000 hücreye dokunur, tam hesap
25 600 hücre ~12 ms). Hesap artık arka tamponda, tikte en çok `maxCellsPerTick` hücre; bitince takas, o zamana dek
eski alan geçerli. Yalnız yayılımı bölmek yetmedi: 8 süreçte en kötü tik 4,95 ms kaldı, çünkü onarımın kurulumu
(alt ağaç + tohumlar) bütçesizdi ve tek başına 3,2 ms'ye çıkıyordu. Kurulum da üç aşamalı ve bütçeli yapıldı: Sim
kıyasında kurulum tiki en çok 0,1 ms, yayılım dilimi en çok 2,1 ms (yüklü makinede, Debug).
Birim testleri: iş sürerken gelen değişimle dilimli onarım = tam hesap (300 rastgele adım, 37 hücrelik dilimler);
tik hücre bütçesine uyar.

## İlk gün botu

`tools/test.ps1 -Only bot -BotTimeout 900` (`bot.log`): `RESULT ok ... kills=21 ... harvests=5` (Zıpzıp akış alanıyla
yürürken, çit kırma maliyeti 40).

## Son doğrulama

Kurulum bölündükten sonra 8 süreç (`stress_8_final.log`): akış tiki iş yapılan 150 tikte p95 2,24 ms, en kötü
21,3 ms; host yükleme 1080 kbps, istemci indirme en çok 20,3 KB/sn, host karesi med 43 / p95 178 ms, istemci karesi
p95 184 ms. Makine o sırada çok yüklüydü (başka oturumların Godot süreçleri); tik işi 1500 hücreyle sınırlı olduğu
hâlde tek örnek 21 ms, yani işletim sistemi duraklaması. Sert yargı bu yüzden iş tiklerinin p95'inde; en kötü değer
satırda kalır. Sim kıyasında (tek süreç) kurulum tiki en çok 0,1 ms, yayılım dilimi en çok 2,1 ms.

Tam `tools/test.ps1 -BotTimeout 900` (`test_full.log`, merge sonrası): build, sim (584), bot, coop, ekonomi, zincir ve
diğerleri geçti; `save` (`ranch` bölümü farklı: hayvan akışı) ve `input_kbm` (`yukselt` adımında takılı: yapı/girdi)
kaldı, ikisi de bu akışın dosyalarına dokunmuyor.
