# Üretim isteği (varlık akışı, 18 Kasım 2026)

Bu dal **hiç kredi harcamadı**. Aşağıdakiler ücretsiz onarımdan (`pipeline/clean_asset.py`) sonra SANAT-SES §9'un betikle ölçülen
ölçütlerinden geçemeyen altın varlıklardır. Üretimi ana oturum, sahibin onayladığı sınır içinde başlatır.

- Betik: `pipeline/gen_retry.py` (varsayılan `--dry-run`, kredi harcamaz). Üretim: `python pipeline/gen_retry.py --run <ad>` (yalnız sahibin o tutara onayıyla).
- Tahmin: `progress/m1/varlik/log/gen_retry_dry_run.log` (tripo CLI `--dry-run --json`). CLI parçalama adımını tahmine katmıyor; M0'da ölçülen 40 kredi elle eklendi.
- Çıktı yolu (sabit, git dışı): `C:/Users/uruskan/Documents/GitHub/kabarma/.tools/tripo/<ad>_v2/`.
- Sonra: `pipeline/golden_manifest.json`'da o varlığın `in` yolu yeni `model.glb`'ye çevrilir, `python pipeline/run_golden.py <ad>` çalışır, `game/data/assets.json` durumu kendiliğinden güncellenir. El Arabası'nda parça adları (`--rename`) yeni parçalamaya göre yeniden eşlenmeli.
- SANAT-SES §9: en fazla dört deneme; yine geçemezse koddan kurulur.

| Varlık | Neden (ölçüt) | Adet | Havuz | Tahmin (bir deneme) | En kötü (4 deneme) | Çıktı |
|---|---|---|---|---|---|---|
| El Arabası (`el_arabasi`) | 3 kalınlık: çubuk alanı %6,6 (izin %2; sınır 6% × 1,81 m = 11 cm): ince boru çerçeve, tutamak kolları, bacaklar | 1 model + parçalama | Tripo API (`tripo-cli`) | 30 + 40 = **70** | 280 | `.tools/tripo/el_arabasi_v2/` |
| Çayır ağacı (`agac`) | 3 kalınlık: %5,0 (sınır 25 cm): sivri kök uçları, taçta ince yaprak pulları | 1 model | Tripo API ya da Tripo web (kaynağı web "HD Model" idi) | **30** (API) | 120 | `.tools/tripo/agac_v2/` |
| Çapa (`capa`) | 3 kalınlık: %2,1 (izin %2, sınırda): nozul halkasının ince kenarı, üstte ince gösterge çubuğu | 1 model | Tripo API | **30** | 120 | `.tools/tripo/capa_v2/` |
| **Toplam** | | 3 | | **130** | **520** | |

Havuz notu: sahibin 17 Kasım onayı Tripo API için ≤175 kredilik belirli bir listeydi; bu üç istek o listede yok, ayrı onay ister.
Tripo web havuzu (3.200) tarayıcı oturumu ister; alt oturum kullanmadı.

İstemler `pipeline/gen_retry.py` içinde (`REQUESTS`); stil cümlesi `gen_golden.sh` ile aynı. Ana fikir her birinde aynı:
ince çubuk, diken ve boru yerine "kalın, yuvarlatılmış, bilek kalınlığında" parçalar.

## Ölçüt 6–8'de kalanlar (sahibin yargısına bağlı, istem yazılmadı)

Ölçüt 6–8'i ben puanladım (`game/data/assets.json`, gerekçeler `progress/m1/varlik/puanlar.md`). Bunlarda 8'in altında kalan
varlıklar için yeniden üretim, sahip puanı onaylarsa gündeme gelir; kredi tahmini yine model başına 30 (Tripo API, parçalamasız).
Ayrıntı ve öneriler `puanlar.md`'de.
