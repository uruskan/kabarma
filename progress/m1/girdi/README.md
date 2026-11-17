# M1 girdi akışı: kanıt

Dal `claude/m1-girdi-bcdafe` (main'e birleştirilmedi). Tarih 2026-11-18.

| Dosya | Ne |
|---|---|
| `test.log` | `tools/test.ps1` tam koşu: derleme, Sim, kıyaslama, bot, inşa, girdi (klavye-fare + oyun kolu) |
| `input_kbm.log`, `input_pad.log` | `--test=input` koşularının süzülmüş satırları (STEP, QUEUED, INPUT, RESULT) |
| `sim_bench.txt` | 1.000 oyun günü Sim kıyaslaması (Release, 3×3 tarla; TEKNIK §1.2 adım 2) |
| `sim_bench_10x10.txt` | Aynı kıyaslama, isteğe bağlı 10×10 tarla ölçeği |
| `export.log` | Windows dışa aktarma denemesi (`export_presets.cfg`): .NET şablonu eksik |

## Teslimler

1. **Girdi eşlemesi veriden.** `game/data/input.json` → `GameData.Input` (Sim) → `Controls` düğümü (InputMap). Her eylemin klavye-fare ve kol bağlaması var; sağ çubuk bakışı, tetikler, LB başlık çarkı. Oyuncu F2 / Görünüm ile açılan panelde yeniden atar; değişiklikler `user://input.json`'a yazılır (sınamalar sahibin dosyasına dokunmaz: `--input_user=` ayrı dosya).
2. **Glif.** `{act:eylem}` yer tutucusu son kullanılan aygıta göre `[E]` ya da `Ⓧ` olur (`Controls.Expand`, `Hud.G`). Seslendirme için `InputText.Strip`. Yer tutucuların bilinen eylem olduğu Sim testinde (`InputDataTests`) denetlenir.
3. **Gerçek girdi sürücüsü** `--test=input --device=kbm|pad`. Botun kararları (senaryo + adım kaydı, sıra kodda yok) `Input.ParseInputEvent` ile sentetik olaya döner, oyun gerçek giriş yolundan işler. Zincirin tamamı (mine → … → yapi → end) iki aygıtla da geçiyor. Açılışta yeniden atama denetimi (`INPUT probe ok`), adım işleyicisi oyunu doğrudan değiştirirse `RESULT fail bypass`.
4. **Sim kıyaslaması** 1.000 oyun günü: bkz. `sim_bench.txt`.
5. **Windows dışa aktarma**: `game/export_presets.cfg` hazır; dışa aktarma .NET şablonu (`export_templates/4.7.2.stable.mono/`) olmadığı için durdu. Yüklü olan yalnızca .NET'siz `4.7.2.stable`. İndirilmedi.
6. **Sınama koşucusu**: `Main.Tests["input"]`, `tools/test.ps1` adımları `bench` ve `input`.

## `--test=input` ne denetler

- Açılışta: her eylemin o aygıtta bağlaması var (`Preflight`); yeniden atama (`interact` → F / R3, bas, eylem geldi mi, son aygıt ve glif değişti mi, varsayılana dön, kalıcı dosya temizlendi mi: `INPUT probe ok`); sekiz başlığın hepsi seçilebiliyor mu (klavye 1–8 tuşları ve Q + fare çarkı, kol LB + sağ çubuk çarkı: `INPUT heads ok`). Senaryo bazı başlıkları adım başında kendisi taktığı için zincir hepsine basmaz; tarama bunu kapatır.
- Zincir: senaryonun bütün adımları (mine … yapi), botun kararlarıyla, yalnızca sentetik olayla. Adım başına 240 oyun sn sınırı (`RESULT fail input stuck`), işleyici oyunu doğrudan değiştirirse `RESULT fail bypass`.
- Kanıt satırı: `INPUT summary device= events= taps= wheels= menuNavs= mouseMoves= sidesteps= actions=`.

## Kararlılık (bu dalda görülen ve giderilen takılmalar)

| Belirti | Neden | Çözüm |
|---|---|---|
| Kol: bakış durdu (çubuk −1 gönderildi, eylem 0 okudu) | Sıfırlama bütün eylemlerin InputMap olaylarını silip yeniden kuruyordu; silme basılı eylemi bırakır | `Controls.ResetAll` yalnızca değişen eylemleri kurar (basılı tutan oyuncu için de doğru) |
| Kol: nişan salınıyor | Saniye başına kazanç + başlıksız `--fast=4` kare süresi oynaklığı (2–100 ms) | Kare başına kapatılan hata oranı; son 8 karenin en uzun bakış adımıyla (`Director.LookDt`) |
| Kol: sür/ek adımında takılma | Bot her kareye sabit 0,2 oyun sn ateş veriyordu (doğrudan kipte nişan anında) | `Bot.Day.cs`: kare sürülünce/ekilince sonrakine geç, 3 sn yedek |
| Çift `QUEUED civi` | Hasat Raporu istasyon menüsünün üstünde açıkken tek Ⓐ/Enter hem raporu kapatıp hem menüde satın alıyordu | `Director.Input.cs`: rapor açıkken Ⓐ/Enter menüye gitmez |
| Hasat/satış/kazma: kamera ışını Ocak'a ya da ağaca çarpıyor | Doğrudan kipte nişan engelin içinden geçer | Sürücü görüş engelinde kısa yan adım atar (`sidesteps=`), bot adımına dokunmaz |
| Kol çarkı taramada hep aynı başlık | Tarama içindeki çark işleminde nişan sürücüsü sağ çubuğu ezmişti | İşlem bakışı sahiplenir (`OwnsLook`); çark açılmadan çubuk bırakılır |

Tekrarlar (sür/ek düzeltmesinden sonra): klavye-fare 7/7 yeşil, kol 6/7. Tek kol takılması satışta Ocak engeliydi: basış nişan oturunca hemen harcandığı için yan adım tetiklenmiyordu; sürücü artık yakın zamanda istenen basışı da "iş yapmak istiyor" sayar. Bu düzeltmeden sonraki tam koşu (`test.log`) yeşil, ancak o koşuda satışta engel çıkmadı (tek yan adım yine hasatta, `yanadim=1`): düzeltmenin bir takılmayı açtığı henüz görülmedi. Ağaç engeli (paketle, kazma) için de yan adımın engeli açtığı bir koşu yok; bu iki durum gözlem altında. Kol koşusu yük altında daha yavaş (~80 sn gerçek, ~300 oyun sn); `--quit_after=900` üst sınır, zincir bitince test kendisi çıkar.

Kıyaslama: 1.000 gün 3×3 tarlada 1,76 sn (ölçüt 10 sn, geçti). İsteğe bağlı 10×10 ölçeği 12,0 sn (ölçütü geçmiyor), toplam 38 GB ayırma (3×3'te 3,4 GB); nedeni profillenmedi (`sim_bench_10x10.txt`).

## Sınanmayanlar

- Olayla hiç basılmayan eylemler: `build_prev`, `build_rotate`, `card_2`, `card_3`, `jump`, `ui_up`, `controls_panel` (bağlamaları her iki aygıtta var: `InputDataTests` durağan denetimi).
- Yeniden atama paneli arayüzü (yakalama kipi, gezinme) çalıştırılmadı; yalnızca `Controls.Rebind` / `ResetAll` açılış denetiminde.
- Kaydedilmiş `user://input.json`'un sonraki açılışta yüklenmesi sınanmadı.
- Kemer (`Alt+1–8`) ham tuş: kolda yolu yok (Ⓨ çanta menüsünden), yeniden atanamaz.
- Gerçek kol donanımı, pencereli fare kilidi, kol hissi (ölü bölge, eğri, hız): sahibin onayını bekliyor. Başlıksız fare bakışı (`Player.cs`) yalnızca sınama yolu.
- Ekran görüntüsü yok (başlıksız koşu): glif ve panel görünüşü sahibin onayını bekliyor.
