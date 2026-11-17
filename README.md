# Kabarma (Moonswell)

Vahşi bir ayda çiftlik kur. Birkaç gecede bir gökyüzü tutuşur ve her şey kabarır.

Üçüncü şahıs, 1–8 kişilik hayatta kalma ve üs kurma oyunu.

## Durum: MVP oynanabilir (17 Kasım 2026)

Planın "ilk 10 dakika"sının ([PLAN.md](docs/PLAN.md) §1.4) küçültülmüş hâli, tek oyuncu: Kazma ile taş, Ocak, 3×3 şalgam tarlası, sulama, Gıdak'a buğday, Küçük Kabarma gecesi (şişen şalgamlar, 3 dalga kabarmış Zıpzıp, Tırpan Işını, 3 kart), şafakta Kargo Rampası'na satış (+120 Kredi) ve Hasat Raporu. Godot 4.7.2 .NET (C#), kurallar `src/Kabarma.Sim` içinde, sayılar `game/data/*.json` içinde. Genex harcaması: 150 kredi (karakter 64, Gıdak 35, müzik 27, 12 ses 24).

- **Oyna:** `OYNA.bat` (derler ve açar). **Botu izle:** `IZLE.bat`.
- **Tuşlar:** WASD yürü · Shift koş · Space zıpla · fare nişan · sol tık Çapa'yı kullan · 1–6 başlık (Kazma, Toprak, Sulama, Çekim, İnşa, Tırpan Işını) · E Gıdak'a buğday · Z/X/C kart seç · Enter raporu kapat · Esc fareyi bırak.
- **Testler:** `dotnet test src/Kabarma.Sim.Tests` · baştan sona bot: `tools/run.sh bot -- --bot --fast=4 --quit_after=300 --mute` (`RESULT ok` yazar).

## Belgeler

| Belge | İçerik |
|---|---|
| [docs/PLAN.md](docs/PLAN.md) | Oyun nedir, kilitli kararlar, v1.0'ın tanımı, riskler. Buradan başla. |
| [docs/GDD.md](docs/GDD.md) | Bütün sistemlerin kuralları |
| [docs/ICERIK.md](docs/ICERIK.md) | Adıyla bütün içerik ve basamak zincirinin doğrulaması |
| [docs/SANAT-SES.md](docs/SANAT-SES.md) | Görsel dil, animasyon, efekt, arayüz, ses, müzik |
| [docs/TEKNIK.md](docs/TEKNIK.md) | Mimari, ağ, kayıt, dünya üretimi, test |
| [docs/URETIM.md](docs/URETIM.md) | AI üretim hattı, kredi bütçesi, kilometre taşları, çıkış planı |
