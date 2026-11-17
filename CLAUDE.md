@AGENTS.md

# Kabarma: proje kuralları (her oturum için geçerli)

Bu bölüm sahibin kurallarıdır ve yukarıdaki Genex sözleşmesiyle çeliştiği yerde **bu bölüm geçerlidir**. Özellikle Genex sözleşmesinin 4. maddesi ("izin sormadan üret") bu projede geçerli değildir.

## Belgeler tek kaynaktır

`docs/PLAN.md` (buradan başla) · `docs/GDD.md` (kurallar) · `docs/ICERIK.md` (içerik) · `docs/SANAT-SES.md` (görsel, ses) · `docs/TEKNIK.md` (mimari, test) · `docs/URETIM.md` (hat, bütçe, kilometre taşları) · `docs/stil/README.md` (stil föyü) · `docs/PROGRESS.md` (ilerleme günlüğü).

- Sıra: **önce belge, sonra veri, sonra kod** (URETIM §7). Belgeyle çelişen bir şey gerekiyorsa koda geçmeden önce dur ve raporla.
- Kurallar motordan bağımsız `src/Kabarma.Sim` içinde ve testli; sayılar `game/data/*.json` içinde, kodda sihirli sayı yok.
- Kilometre taşı sırası sabittir (URETIM §6). Şu an **M1** (görünüş ve his dilimi). M2'ye ait iş, sahip M1 kapısını açıkça geçmeden yapılmaz.
- "Bitti" = PLAN §7.2'nin altı koşulu. Raporda her koşulu ayrı yaz: geçti / kısmen / yok. Sahibin yargısına kalanları (his, dinleme, 30 dk oynama, RTX 2070S FPS) asla "geçti" diye yazma; "sahibin onayını bekliyor" yaz.
- Yer tutucu varlıklar `data/` içinde işaretlenir (URETIM §3).

## Para ve dış dünya

- **Kredi harcayan hiçbir üretimi sahibin o tutara açık onayı olmadan başlatma** (Genex, Tripo API, Tripo web, ElevenLabs). Önce ne, kaç adet, hangi havuz, kaç kredi; sonra onay. Alt oturumlar ve alt ajanlar **hiç kredi harcamaz**; ihtiyacı listeleyip ana oturuma bildirir.
- `.env` içindeki ElevenLabs anahtarı her kullanımda ayrı onay ister. Alt oturumlar `.env`'i okumaz. Anahtar hiçbir çıktıya yazılmaz.
- `git push`, yayınlama, mağaza işlemleri yalnızca sahibin açık onayıyla.
- Sahibin kaydına dokunma; testler ayrı kayıt yuvasında koşar.

## Komutlar (PowerShell)

`.tools/` git'te yok; worktree'lerde bu yollar ana çalışma kopyasından kullanılır:

- Godot (konsol): `C:\Users\uruskan\Documents\GitHub\kabarma\.tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe`
- Blender 5.3: `C:\Program Files\Blender Foundation\Blender 5.3\blender.exe` (betikler `pipeline/`)
- Derle: `dotnet build game/Kabarma.Game.csproj -nologo -v q`
- Sim testleri: `dotnet test src/Kabarma.Sim.Tests -nologo -v q`
- Yeni worktree'de ilk iş içe aktarma: `& $G --headless --path game --import`
- Baştan sona bot (ilk 10 dakika): `& $G --headless --path game -- --bot --fast=4 --quit_after=400 --mute` → çıktıda `RESULT ok` olmalı. Uzun komutları arka planda çalıştır.
- Ekran görüntüsü: `--shots=...` argümanı (bkz. `tools/run.sh`, `game/scripts/Main.cs`).

## Paralel çalışma

- Çok oturum aynı anda çalışır. Şu dosyalar sıcaktır, mümkünse dokunma; dokunman gerekiyorsa küçük, yerel değişiklik yap ve raporda say: `game/scripts/Director.cs`, `game/scripts/ui/Hud.cs`, `game/scripts/actors/Capa.cs`, `game/scripts/Bot.cs`, `game/scripts/world/World.cs`, `src/Kabarma.Sim/GameData.cs`, `src/Kabarma.Sim.Tests/SimTests.cs`, `game/data/*.json`.
- Yeni işi yeni dosyalara koy (partial class, yeni test dosyası, yeni veri dosyası).
- Bitince: derleme temiz, Sim testleri yeşil, bot `RESULT ok`. Kendi dalına commit et, push etme.
- Kanıt `progress/m1/` altına (ekran görüntüsü, log).
