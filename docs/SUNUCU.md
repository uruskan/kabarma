# Kabarma adanmış sunucu

Başsız (görüntüsüz) Godot sürecidir: oyuncu karakteri yoktur, dünya bir kayıt yuvasında yaşar. TEKNIK §5 ve §12; kurallar `src/Kabarma.Sim/ServerAdmin.cs`, süreç `game/scripts/server/`, ayarlar `game/data/server.json`.

## Başlatma

Windows (sürümlü Godot .NET konsol sürümüyle ya da dışa aktarılmış `Kabarma.exe` ile):

```powershell
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path game -- --server --port=27015 --name="Benim Sunucum" --password=gizli --admin_password=yonetici
```

Linux (dışa aktarılmış sunucu ya da Godot .NET ikilisiyle):

```bash
./KabarmaSunucu.x86_64 --headless -- --server --port=27015 --name="Benim Sunucum"
```

Bayraklar `server.json`'u ezer: `--port --name --password --admin_password --slot --max_players --server_dir`. Dünya `user://saves/<slot>` yuvasından yüklenir (yoksa yeni dünya) ve oraya yazılır: otomatik kayıt (`autosaveMinutes`), her şafakta ve kapanışta. Gece kayıt alınamaz; şafakta alınır.

Port: ENet UDP `port`. Steam sunucu tarayıcısı için `steam.listed` açıkken UDP `port + 1` sorgu portu da açık olmalıdır (Steam yoksa atlanır).

## Yönetim

Komutlar üç yerden gelir: konsol (stdin), komut dosyası (`server_commands.txt`, okununca boşaltılır; sonuç `server_results.log`) ve oyun içinden (F8 paneli; önce `giris <admin_password>`).

| Komut | İş |
|---|---|
| `yardim` | komutları listeler |
| `oyuncular` | numara, ad, adres, yönetici |
| `at <no|ad> [neden]` | oyuncuyu atar |
| `yasakla <no|ad> [dakika(0=kalıcı)] [neden]` | atar ve ad ile adresini yasaklar (çevrimdışı ad da olur) |
| `yasak_kaldir <ad>`, `yasaklar` | yasak listesi |
| `kaydet` | dünyayı hemen kaydeder (gece alınamaz) |
| `duyuru <metin>` | herkese ekran mesajı |
| `sifre [yeni|-]` | giriş şifresini gösterir, değiştirir, kaldırır |
| `durum` | oyuncu, gün, gece, kayıt sayısı |
| `kapat` | oyuncuları atar, kaydeder, kapanır |

Yasaklar ve şifre `server_runtime.json`'da kalıcıdır.

## Sunucu listesi

Ana menü → **Sunucular**: kayıtlı sunucular (`user://servers.json`, şifre saklanmaz), Steam sunucu tarayıcısı (Steam açıksa) ve IP ile doğrudan bağlantı (ENet).

## Linux dışa aktarma

`game/export_presets.cfg` içinde `Linux Sunucu` ön ayarı var. Dışa aktarmak için Godot'un **Linux .NET dışa aktarma şablonları** kurulu olmalıdır (Editor → Manage Export Templates; sürüm 4.7.2 mono). Şablon yoksa:

1. Godot sürümünün `Godot_v4.7.2-stable_mono_export_templates.tpz` dosyasını indir (godotengine.org/download/archive).
2. Editor → Project → Export → Manage Export Templates → Install From File; ya da `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` altına aç.
3. `Godot --headless --path game --export-release "Linux Sunucu"` (çıktı `export/linux-server/KabarmaSunucu.x86_64`).
4. Hedefe ikiliyi, `.pck` dosyasını ve `data_*` .NET klasörünü birlikte kopyala. Steam listesi için `libsteam_api.so` aynı klasörde olmalı.

Bu makinede Linux şablonu kurulu olmadığından dışa aktarma burada denenmedi; sunucu Windows'ta `--test=server` ile sınanır.