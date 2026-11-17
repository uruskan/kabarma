# Üretim isteği: oyuncu klipleri (M1)

Akış: oyuncu · dal `claude/m1-oyuncu-7059e8` · 17 Kasım 2026.
**Hiçbiri çalıştırılmadı.** Alt oturum kredi harcamaz; aşağıdakiler sahibin onayını bekliyor.

## Durum

M1'in 30 oyuncu klibi `game/data/player_anims.json` içinde (`m1: true`):

| | Adet | Klipler |
|---|---|---|
| Elde (kütüphane, birleştirilmiş `player.glb`) | 7 | `idle`, `walk`, `run`, `sprint`, `jump_start` / `jump_air` / `jump_land` (üçü `jump` klibinden kodla kesilir) |
| Kodla poz ve katman | 5 | `plant`, `eat`, `sleep`, `push_cart`, `wade` |
| **Eksik, yer tutucuyla oynuyor** | 18 | aşağıda |

Eksik klipler oyunu durdurmaz: her biri veride `"placeholder": true` ve `"fallback"` taşır, oyun en yakın klibi (gerekirse ters oynatarak) ya da kodla pozu kullanır. `PlayerAnimsTests` her yer tutucunun gerçek bir klibe çözüldüğünü ve bir `request` notu olduğunu sınar.

## İstek 1: kütüphane bağlama (4 klip)

| Klip | Şimdiki yer tutucu | Önerilen kütüphane klibi (ücretsiz aramadan) |
|---|---|---|
| `idle_2` | `idle` | 338 Short_Breathe_and_Look_Around (yedek 336 Long_Breathe_and_Look_Around) |
| `fall` | `jump_air` | 502 Fall1 (önizlemeyle 502–505 arasından döngüye uygun olan) |
| `getup_front` | `getup_front_pose` (kodla) | 344–353 Stand_Up1..10 arasından yüzüstü kalkan (önizlemeyle) |
| `getup_back` | `getup_back_pose` (kodla) | 271 Wake_Up_and_Look_Up ya da Stand_Up serisinden sırtüstü kalkan |

- **Havuz:** Genex (Meshy kütüphanesi, karakter `cmwki6h6s038n3gt9vgvkv9gy`).
- **Tahmini kredi:** 4 × ~6 = **~24** (URETIM §6 "Onay bekleyen" birimi ~6). Önizleme seçimi ücretsiz (`animations search`).
- **Komut** (seçilen kimliklerle, tek parti):
  ```powershell
  npx genex character animate cmwki6h6s038n3gt9vgvkv9gy --action 338 --action 502 --action <yuzustu> --action <sirtustu> --no-wait
  npx genex wait <id>
  ```
- **Çıktı yolu:** `game/assets/genex/<wait-id>/` (ana çalışma kopyası; git'te değil).

## İstek 2: sekiz yönlü yürüme ve koşu (14 klip)

`walk_b`, `walk_l`, `walk_r`, `walk_fl`, `walk_fr`, `walk_bl`, `walk_br`, `run_b`, `run_l`, `run_r`, `run_fl`, `run_fr`, `run_bl`, `run_br`. Şimdi hepsi `walk` ya da `run` ile oynuyor (geri yönler ters oynatılır); nişan alırken yan ve geri yürüyüşte ayak kayması görünür.

- **Havuz:** Genex.
- **Tahmini kredi:** `--locomotion` 16 klip üretir (ileri yürüme ve koşu dahil, ikisi elde). Birim ~3: **~42–48**.
- **Komut:** `npx genex character animate cmwki6h6s038n3gt9vgvkv9gy --locomotion --no-wait`. Sunucu ücretlendirmeden önce planı (rota ve klip sayısı) yazar; Meshy iskeletinde bu lane'in kemik adlarının tuttuğu o planda doğrulanmalı.
- **Daha ucuz yedek:** kütüphanede bazı yönler hazır (679 Walk_Backward_inplace, 607 BackRight_Run_inplace). Yalnız geri yönler alınırsa 4 × ~6 = ~24.
- **Çıktı yolu:** `game/assets/genex/<wait-id>/`.

## Toplam

**~66–72 Genex kredisi** (İstek 1 + İstek 2). Genex bakiyesi 2; 23 Kasım 2026'da 200'e dolar (URETIM §5).

## Klip gelince (kredisiz, betikle)

1. `player_anims.json` içinde ilgili satırda `action` alanına gelen klibin adını yaz, `placeholder` ve `fallback` alanlarını sil.
2. `merge_player.py` komutunu yeni klasörü `--clips` listesine ekleyerek yeniden çalıştır (komut `game/assets/models/player/README.md` içinde). Betik eksik klipte ve boş klipte çıkış kodu 1 verir.
3. `dotnet test src/Kabarma.Sim.Tests` (`PlayerAnimsTests`) ve `tools/test.ps1`.
