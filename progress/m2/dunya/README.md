# M2 dunya2 kanıtı

- `world.log`: `--test=world` (M1 bölümleri + M2 bölümü: Sis Korusu, geçiş, 16 keşif noktası, sis/huzme, Koru ışığı).
- `explore.log`: `--test=explore` (yürüyerek harita, Seyir Kulesi tırmanma/inme, mancınık uçuşu ve paraşüt, yük reddi, Yuva, Enkaz yenilenmesi, M haritası).
- `shots/`: 6 yer imi × 4 gün saati (`--test=shots --shots_quick`, bot yok, üs boş): M1'in dört yer imi + `gecis` (Çayır→Koru) + `koru` (Koru içi).
- `perf/noon.txt`: öğle ölçümü, 6 yer imi × 60 sn (`--test=perf --perf=noon --preset=yuksek`), RTX 2070S, aynı anda 8 Godot oturumu. En düşük kapsul 73,5 FPS, Koru 85,4, geçiş 94,2. GPU paylaşıldığı için sayılar **sahibin onayını bekliyor**. (JSON yazımı mutlak `--perf_out` yolunda düştü; düzeltildi, sayılar logdan.)
- `bot-ara.log`: birleşme öncesi bot (geçti, harvests=5).
- `test-tam.log`: commit öncesi tam `tools/test.ps1` (main birleşmesinden sonra): 3 FAIL (bot, input_kbm, input_pad), üçü de `hasat` adımında. Aynı bot temiz main 71e7aa5'te de `hasat`'ta kalıyor: `bot-main-71e7aa5.log`. Diğer 20 adım geçti (world, explore, boss dahil).
- `bot-son.log`: son birleşmeden (main c6d42b2, tarım yabani biçme kuralı) sonra bot **geçti** (harvests=5, skipped=-). input_kbm/pad aynı adımda kalmıştı, yeniden koşulmadı.
- FPS sayıları son iki main birleşmesinden önce ölçüldü.
- Huzme hatası (görüntüde bulundu): saydam huzme kartlarına ortam sisi ekleniyordu, sert kenarlı levha gibi görünüyordu; `fog_disabled` ile düzeldi, gece huzme yok.
