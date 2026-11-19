# Denge notu (M6 denge turu)

Yöntem: `src/Kabarma.Sim/BalanceReport.cs` başsız koşucudur. Veriden eğrileri hesaplar (Kabarma KS 1–50, yaratık/Ata/Azman canı, başlık ve güç hasarı, üretim süreleri, ekin geliri, tohum paketi, çark). `BalanceTests` aynı eğrileri denetler (tekdüze artış, sıçrama yok, hedef bant). Tabloyu görmek için `dotnet test src/Kabarma.Sim.Tests --filter Dump_tables`; çıktı `%TEMP%/denge_dump.txt`. Sayılar `game/data/*.json`'dadır; bu belge neden değiştiklerini yazar. Performans ölçümü değildir, oynanış dengesidir.

Hedefler: ICERIK/GDD'de olanlar oradan (KS 1–10 ilk denemede geçme %60–80, çark beklenen değeri 0,85); yoksa endüstri mantığı: artış tekdüze, bir basamakta ≤ ×1,45 (toplam dalga canı), geçme oranı KS 11–50 boyunca %50–90 (tek oyuncu), ekip %50–95.

## Eğri (tek oyuncu, önerilen donanım)

| KS | can çarpanı | hasar çarpanı | dalga düşmanı | toplam dalga canı | Maya çarpanı | geçme (1 oyuncu) |
|---|---|---|---|---|---|---|
| 1 | 1,0 | 1,0 | 141 | 13,7 B | 1,15 | 0,83 |
| 10 | 3,5 | 2,0 | 385 | 129 B | 2,5 | 0,73 |
| 20 | 9,1 | 3,6 | 649 | 618 B | 4,0 | 0,57 |
| 30 | 18,8 | 5,8 | 933 | 1,8 Mn | 5,5 | 0,60 |
| 40 | 30,6 | 8,6 | 1221 | 3,8 Mn | 7,0 | 0,67 |
| 50 | 49,8 | 12,8 | 1462 | 7,6 Mn | 8,5 | 0,77 |

(B: bin, Mn: milyon. Örnek gürültüsü ±0,08.)

## Değişiklikler (bir satır neden)

| Dosya, alan | Önce → sonra | Neden |
|---|---|---|
| `kabarma.json` `hpGrowthLate` / `hpGrowthEnd`, `lateFromKs` 10, `endFromKs` 25 | tek us 1,15 → 1,15 / 1,10 (KS 11–25) / 1,05 (26+) | Tek üstel eğri KS 50'de can çarpanı 942 yapıyordu (toplam can 1,4 × 10⁸); karşılığı olan gerçekçi donanım büyümesi yok, geçme oranı KS 18'den sonra %100'e yapışıyordu. Erken eğri (KS 1–10) M3 kabulüne bağlı, dokunulmadı. |
| `kabarma.json` `damageGrowthLate` / `damageGrowthEnd` | 1,08 → 1,06 / 1,04 | Hasar can eğrisiyle birlikte yumuşar; KS 50'de ×43 yerine ×12,8. |
| `kabarma.json` `balance.gearPerKsLate` 0,16, `gearPerKsEnd` 0,08 | 0,28 sabit → 0,28 / 0,16 / 0,08 | Önerilen donanım modeli can eğrisini izler: düşman sayısı artışı yavaşladıkça gereken donanım büyümesi de düşer. Üç uçlu parametreler tarama (hpLate × gearLate) ile bulundu, geçme ≈ %55–75. |
| `kabarma.json` `balance.baseDps` | 31 → 32 | Kanyon yaratıklarının canı artınca KS 4 geçme oranı %58'e düştü (M3 bandı %60–80). |
| `kabarma.json` `playerFactor` / `playerExponent` | 0,6 / 0,85 → 1,0 / 1,0 | Eski çarpanla 4 oyuncu 2,3× düşmana karşı 4× hasar uyguluyor, geçme %100'dü. Şimdi her ek oyuncu bir oyuncu kadar düşman; ekip geçme %63–90 (biraz rahat, ama sabit geçmez). |
| `creatures.json` kosar 80, lesci 85, kivrik 115, dehliz 175, kizgin 195 (kanyon) | 60/65/95/150/170 | Kanyon (T4) ortalama canı Sazlık'tan (T3) düşüktü; artık 130 > 126. |
| `azman.json` kizgin 2000, surubasi 2300, hamur 2800 | 1700 / 1900 / 2400 | Azman canı biyom sırasıyla tekdüze artmıyordu (Kızgın < Kıskaç). Hepsi hâlâ aynı biyomun Ata'sının altında (T1 hariç, orada Azman zırhlı ve Ata'ya yakın). |
| `weapons.json` kusam başlıkları damage | alev 5→13, havan 60→150, ark 18→23, tohum 8→13, ayaz 3→22, yercekimi 70→230, maya_topu 80→440 | Yeni başlıklar başlangıç silahı Çivi Tabancası'ndan (92 sn-hasar) zayıftı; artık T3 ≈ 130, T4 ≈ 190, T5 ≈ 225, T6 ≈ 330 (tek hedef eşdeğeri). |
| `crops.json` maya_flower basePrice 130, leaven_mushroom 150 | 90 / 110 | Gelir Göbek'te (T6) 50 Kredi/gün ile Ayaz'ın (55) altındaydı; şimdi 70. |
| `recipes.json` seconds (T3–T6, yemek hariç) | T3 7,7→9,2 · T4 9,8→11,3 · T5 12,7→13,7 · T6 12,7→16 sn ortalama | Üretim süresi T2'den T3'e düşüyor, T5–T6 eşitti; basamak başına artan eğri (5 → 8 → 9,2 → 11,3 → 13,7 → 16). Oran 0,5 sn'ye yuvarlandı. |

## Değişmeyenler (denetlendi)

- Ata canı: 1400 → 1800 → 2600 → 3400 → 4200 → 7000 (basamak başına ≤ ×1,67, Ana Maya son patron).
- Yaratık hasarı biyom basamağıyla artıyor (7, 7, 8, 8,8, 10).
- Tohum Çarkı beklenen değer / bedel 0,85 (hedef 0,85).
- Tohum paketi getirisi (ekin geliri / tohum bedeli) 1,0–2,5 (hiçbiri zarar ettirmez, hiçbiri 4'ü geçmez).

## Bilinen sınırlar

- `WaveBalance` önerilen donanımı modeller (ortalama oyuncu + beceri sapması); gerçek oyun donanımı (Çapa Mk, çip, kart, kule) ayrı ayrı modellenmedi. Sayılar gerçek oyunda bot ve sahip oyunuyla yeniden doğrulanmalı.
- Ekonomi: Kredi akışı (kasa satışı, borsa doygunluğu) için tek bir "Kredi/gün" modeli yok; yalnız ekin geliri ve katalog/çark bedelleri denetlendi.
- Göbek'te tek yaratık (Hamurcuk) olduğu için yaratık eğrisi T1–T5'te sınanır.
- KS 1–10 bandı (M3 kabulü) `WaveBalanceTests` ile ayrıca korunur.