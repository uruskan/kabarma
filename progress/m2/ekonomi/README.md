# M2 ekonomi2: Borsa, Katalog, Tohum Çarkı (18 Kasım 2026)

## Ne var
- **Borsa** (`src/Kabarma.Sim/Market.cs`, `game/data/market.json`): 12 kategori, günlük ±0,15 + 1,0'a %10 çekim, 0,6–1,6; günün sıcak kategorisi ×2; doygunluk kasa başına −0,03 (taban 0,4), her sabah +0,15; 30 günlük geçmiş. İlk Kabarma şafağına kadar nötr (öğretici 120 Kredi korunur). Rastgelelik durumsuz (tohum + gün + kategori).
- **Fiyat**: `Pricing.Unit = taban × kalite × Borsa × doygunluk`; kasa ve kabarık ürün fırlatma anında kilitlenir.
- **Katalog** (`Catalog.cs`, `catalog.json`): Şalgam Tohumu paketi (15 × 20 = 300, kapsülle 20 sn, Ocak'ın yanına iner, E ile açılır), El Arabası gövdesi (400, rampaya 40 sn). `requires` ile sistemi gelmeyen kalemler gizli (yumurta, plan, kozmetik satır ekleyerek gelir).
- **Tohum Çarkı** (`Wheel.cs`, `wheel.json`): günün ilki bedava, sonra 500 Kredi; 8 ödül, beklenen değer 425 = %85. Sistemi olmayan ödüller (çip, kozmetik, kart jetonu, yumurta, Alaca tohum) Kooperatif deposunda sayılır.
- **Kayıt**: `market` bölümü (`GameState.Market.Save.cs`), gidiş-dönüş testi.
- **Arayüz**: Kargo Rampası panelinin sonuna Borsa / Katalog / Tohum Çarkı satırları; sol sütunda Rust dilinde panel (`Hud.Market.cs`).
- **Bot**: senaryonun 17. adımı `katalog` (`Bot.Market.cs`): satıştan gelen Krediyle rampadan tohum paketini alır, kapsülü açar.

## Testler
- Sim `MarketTests` (16): 200 gün × 3 tohum satış baskısıyla sınırlar, NaN yok, geçmiş sınırlı; doygunluk; fiyat kilidi; katalog fiyatları ICERIK formülünden; çark beklenen değeri %85 ± 1 ve 40.000 çekilişte ağırlıklar; kayıt gidiş-dönüşü.
- Godot `--test=borsa` (`MarketTest.cs`): 25 denetim (rampa paneli, Borsa, katalog, kapsül, El Arabası rampaya, çark).
- Bot: `katalog` adımı (tam zincir).

Görüntüler: `borsa_rampa.png`, `borsa.png`, `katalog.png`, `kapsul.png`, `cark.png` (pencereli `--test=borsa`).
