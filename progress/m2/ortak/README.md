# Ortak oyun (M2, ortak akışı) — kanıt

## Koşular (18 Kasım 2026)

- `--test=coop` (aynı makinede başlıksız host + 3 ENet istemcisi; biri 35. sn'de sonradan katılır): `coop_host.log`, `coop_client_1..3.log`
  `COOP RESULT ok players=4 max_diff_mm=2.536 pieces=4 foundations=4 late_join=1 plots=26 revives=1 voice_frames=25 creatures_spawned=26 rejected=0 corrections=0`
  - Botlar oyuncu yolundan kazar, odun biçer, İnşa Planı ile temel kurar, tarla sürer (istemcide istekler host'a gider).
  - Ölçütler: her avatar sahibinin konumundan < 1 cm (en kötü 2,5 mm); çanta, kaya, tutam, yerleştirilebilir, yapı, el üretim kuyruğu ve sandık özetleri host'la aynı; 16 kayıt bölümü köprüden her istemciye uygulandı (sonradan katılan dahil); her bot temel kurdu ve en az 4 taş kazdı; host'ta en az 4 tarla karesi.
  - Tatbikat: host bayılır (ortak oyunda 30 sn), Bot 1 şok tokadıyla kaldırır (`revives=1`); Bot 2 sinüs sesi konuşur, host ve diğer istemciler 25/25 çerçeve duyar.
  - Sonradan doğan yaratıklar (çayır Zıpzıpları, yabani sürüler) yalnız host'ta doğar; istemcilerde kukla sayısı host'unkiyle aynı (26).
- `--test=steam` (tek süreç, Steam açık, uygulama 480): `steam.log` (Steam kimliği satırı çıkarıldı)
  `STEAM RESULT ok lobby=… data=1 members=1 listen=True app=480`

## Bilinen eksikler

- Gece dalgası ve kartlar: dalga host'ta doğar, istemciye kukla gelir, vuruşlar host'a gider; ama Kabarma gecesinin durumu (dalga sayacı, kart seçimi, Öz) her süreçte yerel işler, eşitlenmez. Yaratıklar yalnız host oyuncusunu hedefler.
- İstemcide yalnız yerelde değişen çanta host'tan geri alınır. Host'a bağlı olanlar: kazma, biçme, yerleştirme, söküm, onarım, sürme/ekme/sulama, el ve istasyon üretimi, sandık, yemek, güç kablosu (guc akışı). Bağlı olmayanlar: yumurta toplama, Gıdak'a buğday, silah mermisi (çivi), kargo kasası, el arabası yuvaları, araştırma.
- İstemcinin istasyon ürünü host'ta onun çantasına girer; yük ürünü (kasa) host'ta doğar.
- İki makinede Steam ile 2 saatlik oyun: sahibin onayını bekliyor.
