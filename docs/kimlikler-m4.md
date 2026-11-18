# M4 kanonik kimlik listesi

Kaynak: `refs/heads/main` (docs/ICERIK.md, URETIM.md §6 M4 ve "İçerik dağılımı", PLAN.md §7.1; game/data/*.json; ItemNames.cs), 18 Kasım 2026.
Biçim: ASCII snake_case, Türkçe harfler katlanır. `var` = main'de bu kimlikle zaten var (dosya belirtilmiş), dokunulmaz. Boş "not" = yeni.
Sözleşme: yeni kimlik main'de yoksa burada yazılanı kullanın; ayrı kimlik uydurmayın. Listede olmayan bir kimlik gerekirse bu dosyaya satır ekleyin.

**Ana oturumun kararları (aşağıdaki belirsizlikler için):**
- **Ekinler İngilizce kalır.** crops.json'daki 11 ekin İngilizce olduğu için yeni 13 ekin de İngilizce (`rice`, `reed`, `glow_lily` …); Türkçe ad `ItemNames.cs`'te. Bu, `kar_cayi` ekin/yemek çakışmasını da çözer.
- **Reçine `resin`'dir.** `vehicles.json`'daki `recine` yazımını arac akışı `resin`'e çevirir.
- **Çakışan adlar ayrılır:** yaratık `suluk_surusu`, araç eklentisi `arac_projektoru`, iş emri zincirleri `merdiven_zinciri` ve `kabarik_ata_zinciri`. `cekirge` iki ayrı tabloda (yaratık ve araç) kalır, yeniden adlandırılmaz.
- **Metaller ve cam:** `titan_ore` ve `titan_ingot` (metal kalıbı). Cam, build.json'a ayrı malzeme olarak değil, dört parçalık ayrı set olarak gelir (`cam_duvar`, `cam_cati`, `cam_zemin`, `cam_kapi`).
- **Eksikler:**
  - Zırh modüllerinin toplamı 18'dir. Main'de yalnız `filtre_maskesi` var; eksiklerin hepsini kusam yapar.
  - M3 payından eksik kalan `kabarma_sensoru` ve `siren`'i tasima ekler.
  - Main'de eksik istasyonlar (`sirt_yazicisi`, `tabaklama_askisi`, `harman_yeri`) zincir4'ün.
  - M2 yemeklerinin 11'i tarim4'ün.
  - `kemik` (kaynak) biyom'un.
- **Ekin sayısı:** PLAN'daki 24 ekin, M2'nin 11 ekinine Şalgam dahil sayılarak tutar.
- **Yeni Ay:** içerik GDD §14.8'de; gece4 yapar.

## 0. Adet kontrolü (listelenen / URETIM M4 ve PLAN §7.1) ve belirsizlikler

| Grup | Listelenen | Beklenen | Durum |
|---|---|---|---|
| 1 Biyom | 4 yeni (6 toplam) | 4 (PLAN: 5 + Göbek = 6) | tamam |
| 2 Kaynak T3–T6 | 15 ham + 3 hayvansal (kemik, kitin, zehir_kesesi) + 16 ara/cephane/şırınga | "T3–T6", PLAN toplam 67 | tamam; ICERIK M4 için sayı vermiyor, ayrım benim (kemik T1 ama main'de yok) |
| 3 Yaratık | 15 (5+5+4+1) | 15 | tamam |
| 4 Ata/Azman/koruyucu/Müdür | 4 Ata, 4 Azman, 3 koruyucu, 1 Müdür, 6 Kabarık Ata | 4/4/3/1 | tamam; Kabarık Ata sayısı ve kimliği ICERIK/GDD'de yok (GDD §18: "aynı adakla yeniden çağrılır"), 6 türetildi |
| 5 Ekin | 13 | 13 | tamam. UYARI: 1 (M1) + 11 (M2) + 13 = 25, PLAN 24. ICERIK #1–11 Şalgam'ı içeriyor; "M2 11 ekin" Şalgam dahil sayılmalı |
| 6 Silah başlığı | 7 (hepsi `var`) | 7 | tamam |
| 7 Güç / ulti | 7 / 2 | 7 / 2 | tamam |
| 8 Tüzük | 12 | 12 | tamam |
| 9 Üye / kozmetik | 11 (+Çırak) / 72 (6×12) | 11 / 72 | tamam; 72'nin dışında kalan 6 zincir ödülü kozmetiği var (bkz. 9) |
| 10 Yapı | 3 malzeme (2'si `var`) + cam seti 4 + 72 yerleştirilebilirin tamamı | 3 + kalanlar | tamam; build.json'da cam malzemesi YOK (4 malzeme: wood, kerpic_tas, celik_sac, mayali_beton) |
| 11 İstasyon | 25 | 25 | tamam (43 − 18) |
| 12 Güç/sinyal/taşıma | 43 listeli, 33 yeni | "kalanlar" | tamam; M3'ün `kabarma_sensoru` ve `siren`i main'de yok (power.json 8 parça), onlar gelmediyse M4 payı 35 |
| 13 Kule / tuzak | 5 / 4 | 5 / 4 | tamam |
| 14 Araç / eklenti | 5 / 10 | 5 / 10 | tamam |
| 15 Yemek | 18 | 18 | tamam; main'de yalnız 3 yemek var, M2'nin 11 yemeği henüz yok |
| 16 Zırh | 4 takım, 12 modül, Çapa Mk.III–VI (4) | 4 / 12 / 4 | modül: 18 listeli ama M2'nin hangi 6'sı olduğu ICERIK'te yok; main'de yalnız `filtre_maskesi` var, 12 payı belirsiz |
| 17 Keşif | 9 tür (hepsi `var`), `genel_merkez` | Zengin Yatak, GM, kalan biyomlar | ICERIK yeni biyomlar için nokta sayısı vermiyor (yalnız toplamlar) |
| 18 İş emri | 24 zincir | 24 | tamam (#13–24 + #37–48) |

Belirsizlikler / çakışmalar:
- **Ekin kimlikleri:** main'deki 11 ekin İngilizce (`turnip, wheat, pumpkin, sunflower, flax, tomato, mushroom, blackberry, sourberry, hazelnut, garlic`; ItemNames'te `_seed`, `_benekli`, `_altin`, `_aurora` türevleri). Bu liste de 13 yeni ekin için İngilizce öneriyor (Türkçe katlanmış alternatif "not" sütununda). Tarım akışı karar versin; ikisi karışmasın.
- **`resin` / `recine`:** ItemNames, recipes.json (`maya_feneri`) ve ICERIK `resin` kullanıyor; vehicles.json (`recipe.recine`, `fuelItem: bitkisel_yag`) `recine` yazmış. Kanonik `resin`; vehicles.json düzeltilmeli.
- **`cekirge`:** hem Çekirge Sürüsü yaratığı (creatures.json) hem Çekirge ATV (vehicles.json). Ayrı tablo ama ikisi de item/tarif kimliği olursa çakışır.
- **`suluk`:** Sülük (Sülük Sürüsü) katlanınca `suluk` = Suluk yerleştirilebiliri. Yaratık `suluk_surusu` olmalı.
- **`projektor`:** hem Işık yerleştirilebiliri (§10.2) hem araç eklentisi. Eklenti `arac_projektoru`.
- **`merdiven`:** İş emri zinciri #26 Merdiven ile yapı şekli `merdiven`; zincir `merdiven_zinciri` olmalı (M3 payı). Benzer: zincir `coban` / üye `coban` (ayrı alan, sorun yok).
- **`silo`:** Silo (depo) ile holding.json yapı kimliği `silo` aynı ad, ayrı alan.
- **`kar_cayi`:** hem ekin (#21) hem yemek (#26). İngilizce ekin kimliğiyle (`snow_tea`) çakışmaz; Türkçe seçilirse çakışır.
- **Kozmetik:** 72 listenin dışında zincir ödülü kozmetikler: `duduk_kaplama`, `altin_eyer`, `altin_drone`, `planor_deseni`, `altin_cark`, `altin_cerceve` (ICERIK'te bölümleri yok). Main'de ilk ikisi zaten orders.json'da.
- **Yeni Ay:** URETIM M4'e koyuyor; ICERIK yalnız Ana Maya ödülü olarak anıyor, içeriği GDD §14.8/§22.
- **Kadim Taş / güç kimlikleri:** main'de `kadim_atilma, kadim_kalkan, kadim_sok, kadim_yem, kadim_sifa` kısa adlı; 7 yenisinin kısa adı seçimdir (grup 7).
- ICERIK §14 başlığı "Araçlar (7)", URETIM "5 araç" (El Arabası + Çekirge M1/M2): tutarlı. §11'in 43 satırı sayıldı: 10+8+10+9+4+2 = 43.
- `kemik` (§2 #32) ve T1 istasyonları `sirt_yazicisi`, `tabaklama_askisi`, `harman_yeri` main'de yok (M1/M2 payı, burada yalnız not).
- `maya` (§2 #26 Maya) için main'de bir eşya kimliği doğrulanamadı; `maya_kesesi` var (ItemNames).

## 1. Biyomlar (ICERIK §1)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| altin_cayir | Altın Çayır | §1 | var (biomes.json) |
| sis_korusu | Sis Korusu | §1 | var (biomes.json) |
| isil_sazlik | Işıl Sazlık | §1 | T3, Ata: Kurbağa Ağa |
| kizil_kanyon | Kızıl Kanyon | §1 | T4, Ata: Çıyan Paşa |
| ayaz_yaylasi | Ayaz Yaylası | §1 | T5, Ata: Buz Tosunu |
| gobek | Göbek | §1 | T6 krater, Ata: Ana Maya (`gobek` GDD'de geçiyor) |

Bant kimlikleri (crops.json `band`: `cayir`, `koru`): yeni `sazlik`, `kanyon`, `ayaz`, `gobek`. Çekirdek eşyaları (research.json `tierCores`): `cayir_cekirdegi` T2 var, `koru_cekirdegi` T3 var, `sazlik_cekirdegi` T4, `kanyon_cekirdegi` T5, `ayaz_cekirdegi` T6 (üçü research.json'da yazılı, ItemNames'te adı yok).

## 2. Kaynaklar T3–T6 (ICERIK §2)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| turba | Turba | §2.1 #11 | Sazlık |
| kukurt | Kükürt | §2.1 #12 | Sazlık |
| kaucuk_sutu | Kauçuk Sütü | §2.1 #13 | Sazlık |
| balcik | Balçık | §2.1 #14 | Sazlık |
| isil_yosun | Işıl Yosun | §2.1 #15 | Sazlık |
| kum | Kum | §2.1 #16 | Kanyon |
| kuvars | Kuvars | §2.1 #17 | Kanyon |
| titan_ore | Titan Cevheri | §2.1 #18 | Kanyon; `copper_ore`/`iron_ore` kalıbı (alt: `titan_cevheri`) |
| katran | Katran | §2.1 #19 | Kanyon |
| tuz | Tuz | §2.1 #20 | Kanyon |
| buz | Buz | §2.1 #21 | Ayaz |
| ayaz_kristali | Ayaz Kristali | §2.1 #22 | Ayaz, Billur |
| ham_maya | Ham Maya | §2.1 #23 | Göbek |
| obsidyen | Obsidyen | §2.1 #24 | Göbek |
| isil_cevher | Işıl Cevher | §2.1 #25 | Göbek |
| maya | Maya | §2.1 #26 | M3; kimlik main'de doğrulanmadı |
| holding_parcasi | Holding Parçası | §2.1 #27 | var (ItemNames) |
| maya_kesesi | Maya Kesesi | §2.2 #36 | var (ItemNames, recipes.json) |
| kemik | Kemik | §2.2 #32 | T1 ama main'de yok |
| kitin | Kitin | §2.2 #33 | Kıskaç, Kıvrık, Çekirge |
| zehir_kesesi | Zehir Kesesi | §2.2 #35 | Kıvrık, Tükürgen |
| celik | Çelik | §2.3 #50 | var (ItemNames, build.json girdisi) |
| kaucuk | Kauçuk | §2.3 #51 | T3, Pres |
| barut | Barut | §2.3 #52 | T3, Kimya Tezgâhı |
| kimyasal_gubre | Kimyasal Gübre | §2.3 #54 | T3 |
| bitkisel_yag | Bitkisel Yağ | §2.3 #55 | T2 yakıt; vehicles.json `fuelItem` var, eşya yok |
| biyoyakit | Biyoyakıt | §2.3 #56 | T3, cephane de |
| cam | Cam | §2.3 #58 | T4 |
| devre | Devre | §2.3 #59 | T4 |
| plastik | Plastik | §2.3 #60 | T4 |
| titan_ingot | Titan Külçe | §2.3 #61 | T4; `copper_ingot`/`iron_ingot` kalıbı (alt: `titan_kulce`) |
| termal_kumas | Termal Kumaş | §2.3 #62 | T4 |
| kristal_lens | Kristal Lens | §2.3 #63 | T5 |
| aku_hucresi | Akü Hücresi | §2.3 #64 | T5 |
| mayali_alasim | Mayalı Alaşım | §2.3 #66 | T6 |
| mayali_beton | Mayalı Beton | §2.3 #67 | var (ItemNames, build.json) |
| reaktor_cubugu | Reaktör Çubuğu | §2.3 #65 | T6 |
| havan_mermisi | Havan Mermisi | §2.3 cephane | T3 |
| maya_siringasi | Maya Şırıngası | §20 | T5 |

Var olan ve bu gruba girmeyen T1–T2: `stone, wood, fiber, clay, copper_ore, scrap, hard_wood, resin, iron_ore, saltpeter, yun, deri, tuy, sut, kurk, bal, balmumu, gubre, sumuk, et, kompost, brick, charcoal, copper_ingot, copper_wire, iron_ingot, plank, cloth, flour, civi, sacma`.

**zincir4 eklemeleri (19 Kasım 2026):** `ark_eki` (Yüksek Fırın'ın ark eki, Tezgâh'ta 10 Çelik + 4 Devre; ilk alınınca `titan_ingot` tarifi açılır, harcanmaz), `kabartma_plani` (Ana Maya'nın ödülü: Kabartma Odası tarifini açar; ata4 düşürür, `research.json` `rawSources` T6 `by: ata4`), tarif girdisi olarak T1–T2 ham kaynak satırları `sunflower`, `saltpeter`, `yun`, `kompost` (`rawSources`'a eklendi). Ham kaynak kimlikleri bu bölümdekilerdir (`titan_ore` vb.); ara ürün tarifinin kimliği ürünün kimliğidir (`celik`, `cam`, `devre` …). Biyom akışı: `rawSources` `by: biyom` satırları (turba, kukurt, kaucuk_sutu, balcik, isil_yosun T3; kum, kuvars, titan_ore, katran, tuz T4; buz, ayaz_kristali T5; ham_maya, obsidyen, isil_cevher T6) düğümlerinin eşya kimliğidir; `craft.json` `sources` hedefi `rock:<kimlik>`.

## 3. Yaratıklar (ICERIK §3)

Main creatures.json (var): `zipzip, gidak, pofuduk, homur, cekirge, sinsi, dalli, hu, vicik, bozuk, vizviz, findikci`.

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| vrak | Vrak | §3.3 | Sazlık |
| kiskac | Kıskaç | §3.3 | Sazlık |
| suluk_surusu | Sülük Sürüsü | §3.3 | Sazlık; `suluk` (yerleştirilebilir) ile çakışmasın |
| tukurgen | Tükürgen | §3.3 | Sazlık |
| cizbiz | Cızbız | §3.3 | Sazlık |
| kosar | Koşar | §3.4 | Kanyon |
| kivrik | Kıvrık | §3.4 | Kanyon |
| kizgin | Kızgın | §3.4 | Kanyon |
| lesci | Leşçi | §3.4 | Kanyon |
| dehliz | Dehliz | §3.4 | Kanyon |
| tosun | Tosun | §3.5 | Ayaz |
| ayaz_kurdu | Ayaz Kurdu | §3.5 | Ayaz |
| paytak | Paytak | §3.5 | Ayaz |
| billur | Billur | §3.5 | Ayaz |
| hamurcuk | Hamurcuk | §3.6 | Göbek |

Holding birimleri (§3.7), hepsi var (holding.json `units`): `isci, bekci, ustabasi, mekanik, mudur`.

## 4. Atalar, Azmanlar, koruyucular, Müdür (ICERIK §4)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| koca_pofuduk | Koca Pofuduk | §4.1 | var (atalar.json) |
| boynuzlu_dede | Boynuzlu Dede | §4.1 | var (atalar.json) |
| kurbaga_aga | Kurbağa Ağa | §4.1 | Sazlık |
| ciyan_pasa | Çıyan Paşa | §4.1 | Kanyon |
| buz_tosunu | Buz Tosunu | §4.1 | Ayaz |
| ana_maya | Ana Maya | §4.1 | Göbek; başarım `maya_hakimi` stat'ı `ana_maya` zaten var |
| kabarik_koca_pofuduk, kabarik_boynuzlu_dede, kabarik_kurbaga_aga, kabarik_ciyan_pasa, kabarik_buz_tosunu, kabarik_ana_maya | Kabarık Ata (her Ata için) | GDD §18, ICERIK §2 #26 | önerilen kalıp `kabarik_<ata>`; ICERIK adlandırmıyor |
| azman_homur | Azman Homur | §4.2 | var (azman.json) |
| azman_bozuk | Azman Bozuk | §4.2 | var (azman.json) |
| azman_kiskac | Azman Kıskaç | §4.2 | Sazlık |
| azman_kizgin | Azman Kızgın | §4.2 | Kanyon |
| surubasi | Sürübaşı | §4.2 | Ayaz (adında "Azman" yok) |
| azman_hamur | Azman Hamur | §4.2 | Göbek |
| kostebek_bey | Köstebek Bey | §4.3 | var (oyuk.json) |
| yarasa_yumagi | Yarasa Yumağı | §4.3 | var (oyuk.json) |
| camur_devi | Çamur Devi | §4.3 | Sazlık |
| kuvars_akrebi | Kuvars Akrebi | §4.3 | Kanyon |
| buz_aynasi | Buz Aynası | §4.3 | Ayaz |
| mudur | Müdür | §4.4 | var (holding.json `units`); Genel Merkez boss'u |

Ata ödül kimlikleri (var olan örüntü: `ata_yunu`, `dede_boynuzu`, `azman_postu`, `azman_boynuzu`):

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| aga_derisi | Ağa Derisi | §4.1 | Kurbağa Ağa ödülü |
| pasa_kabugu | Paşa Kabuğu | §4.1 | Çıyan Paşa ödülü |
| tosun_disi | Tosun Dişi | §4.1 | Buz Tosunu ödülü |
| islak_zemin | Mühür: Islak Zemin | §4.1 | seals; var: `bereketli_toprak`, `derin_kok` (atalar.json) |
| sarsilmaz | Mühür: Sarsılmaz | §4.1 | |
| kalin_post | Mühür: Kalın Post | §4.1 | |
| eksi_maya | Mühür: Ekşi Maya | §4.1 | |
| dev_sinek_yemi | Adak: Dev Sinek Yemi | §4.1 | Kurbağa Ağa (adak adı atalar.json'da `offeringName`) |
| sismik_davul | Adak: Sismik Davul | §4.1 | Çıyan Paşa |
| tuz_yalagi | Adak: Tuz Yalağı | §4.1 | Buz Tosunu |
| eksi_maya_kazani | Adak: Ekşi Maya Kazanı | §4.1 | Ana Maya |

Ödül kozmetikleri/planları: Müdür → `yercekimi` başlığı (var), Holding Müdürü kaskı `kask_holding_muduru`, Holding kaplaması `kaplama_holding`; Ana Maya → `kabartma_odasi` tarifi, `yeni_ay`, Aurora seti (`kask_aurora`, `tulum_aurora`, `kaplama_aurora`).

## 5. Ekinler #12–#24 (ICERIK §5)

Önerilen kimlik İngilizce (main'deki 11 ekinle uyum); alternatif Türkçe katlanmış ad "not"ta. Tohum `<kimlik>_seed`, Alaca `<kimlik>_benekli|_altin|_aurora`.

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| rice | Pirinç | §5 #12 | Sazlık, Tahıl; alt `pirinc` |
| reed | Kamış | §5 #13 | Sazlık, Lif; alt `kamis` |
| glow_lily | Işıl Nilüfer | §5 #14 | Sazlık, Çiçek; alt `isil_nilufer` |
| chili | Acı Biber | §5 #15 | Sazlık, Baharat; alt `aci_biber` |
| corn | Mısır | §5 #16 | Kanyon, Tahıl; alt `misir` |
| watermelon | Karpuz | §5 #17 | Kanyon, Kabakgil; alt `karpuz` |
| prickly_pear | Kaktüs İnciri | §5 #18 | Kanyon, Meyve; alt `kaktus_inciri` |
| tar_bean | Katran Fasulyesi | §5 #19 | Kanyon, Yağ; alt `katran_fasulyesi` |
| frost_beet | Ayaz Pancarı | §5 #20 | Ayaz, Kök; alt `ayaz_pancari` |
| snow_tea | Kar Çayı | §5 #21 | Ayaz, Baharat; alt `kar_cayi` (yemekle çakışır) |
| ice_grape | Buz Üzümü | §5 #22 | Ayaz, Meyve kalıcı asma; alt `buz_uzumu` |
| maya_flower | Maya Çiçeği | §5 #23 | Göbek, Çiçek; alt `maya_cicegi` |
| leaven_mushroom | Ekşi Maya Mantarı | §5 #24 | Göbek, Mantar; alt `eksi_maya_mantari` |

Var (crops.json): `turnip, wheat, pumpkin, sunflower, flax, tomato, mushroom, blackberry, sourberry, hazelnut, garlic`.
Aile kimlikleri (crops.json `family`, var): `root, grain, gourd, oil, fiber, fruit, mushroom, spice`; yeni gerekirse `flower` (Çiçek ailesi, main'de hiçbir ekin yok).
Sera: ayrı kimlik yok; istasyon `sera_kontrol_unitesi` (grup 11), yapı `cam_*` (grup 10).

## 6. Silah başlıkları (ICERIK §6.2)

Hepsi var (chips.json `weaponHeads`; ekonomik/davranış verisi weapons.json'da henüz yalnız ilk ikisi).

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| alev_puskurtecu | Alev Püskürteci | §6.2 #6 | var (chips.json) |
| havan | Havan | §6.2 #7 | var (chips.json) |
| ark | Ark | §6.2 #8 | var (chips.json) |
| tohum_makinalisi | Tohum Makinalısı | §6.2 #9 | var (chips.json) |
| ayaz_lazeri | Ayaz Lazeri | §6.2 #10 | var (chips.json) |
| yercekimi | Yerçekimi | §6.2 #11 | var (chips.json); ödül: Müdür |
| maya_topu | Maya Topu | §6.2 #12 | var (chips.json) |

Önceki: `tirpan_isini, civi_tabancasi, sapan, sacma_borusu, zipkin` (var, chips.json). İş başlıkları (var): `kazma, bicme, cekim, toprak, sulama, insa_plani`.

## 7. Güçler ve ultiler (ICERIK §7)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| atilma, kalkan_balonu, sok_dalgasi, yem_hologrami, sifa_sisi | #1–5 | §7 | var (abilities.json) |
| zaman_bali | Zaman Balı | §7 #6 | Sazlık |
| cekim_kuyusu | Çekim Kuyusu | §7 #7 | Sazlık |
| roket_ziplama | Roket Zıplama | §7 #8 | Kanyon |
| alev_cemberi | Alev Çemberi | §7 #9 | Kanyon |
| isinlan | Işınlan | §7 #10 | Kanyon |
| buz_duvari | Buz Duvarı | §7 #11 | Ayaz |
| gorunmez_pelerin | Görünmez Pelerin | §7 #12 | Ayaz |
| suru_cagrisi | Sürü Çağrısı | §7 ulti | var (abilities.json) |
| dev_mod | Dev Mod | §7 ulti | var (abilities.json) |
| yorunge_sulamasi | Yörünge Sulaması | §7 ulti | Çıyan Paşa |
| zaman_durdu | Zaman Durdu | §7 ulti | Buz Tosunu |

Kadim Taş (abilities.json `stones`, var): `kadim_atilma, kadim_kalkan, kadim_sok, kadim_yem, kadim_sifa`. Önerilen yeniler (aynı kısa ad örüntüsü): `kadim_bal` (Zaman Balı), `kadim_kuyu` (Çekim Kuyusu), `kadim_roket`, `kadim_alev`, `kadim_isin`, `kadim_duvar`, `kadim_pelerin`. Ulti kaynağı = Ata (`ata:<kimlik>` kalıbı).

## 8. Tüzük maddeleri (ICERIK §8.4)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| sis | Sis | §8.4 #1 | |
| ac_suru | Aç Sürü | §8.4 #2 | |
| kalin_kabuk | Kalın Kabuk | §8.4 #3 | |
| cift_azman | Çift Azman | §8.4 #4 | |
| kirik_can | Kırık Çan | §8.4 #5 | |
| saganak | Sağanak | §8.4 #6 | |
| holding_seferberligi | Holding Seferberliği | §8.4 #7 | |
| cimri_ay | Cimri Ay | §8.4 #8 | |
| uzun_gece | Uzun Gece | §8.4 #9 | |
| kor_kule | Kör Kule | §8.4 #10 | |
| gocmen_suru | Göçmen Sürü | §8.4 #11 | |
| ters_ruzgar | Ters Rüzgâr | §8.4 #12 | |

`yeni_ay` (Yeni Ay, tüzük maddelerinin baştan seçildiği yeni dünya modu, GDD §14.8): ICERIK'te tanımsız. Kartlar (72, `var` cards.json) ve aşılar bu grubun dışında.

## 9. Üye tipleri ve kozmetik (ICERIK §9)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| cirak | Çırak | §9 #1 | M1, baştan; main'de veri yok |
| coban | Çoban | §9 #2 | var (orders.json grant `member:coban`) |
| makinist | Makinist | §9 #3 | |
| asci | Aşçı | §9 #4 | |
| kacak | Kaçak | §9 #5 | |
| pehlivan | Pehlivan | §9 #6 | |
| arici | Arıcı | §9 #7 | var (orders.json `member:arici`) |
| muhtar | Muhtar | §9 #8 | |
| nine | Nine | §9 #9 | |
| robot_kahya | Robot Kâhya | §9 #10 | |
| gidak_kostumlu | Gıdak Kostümlü | §9 #11 | var (orders.json `member:gidak_kostumlu`) |
| holding_firarisi | Holding Firarisi | §9 #12 | |

Kozmetik: 6 kategori × 12 = 72. Kimlik kalıbı `<önek>_<ad>` (main: `kask_sera_cami`, orders.json).

| önek | Kategori | Adet | Parçalar |
|---|---|---|---|
| kask_ | Kask | 12 | standart, hasir_sapkali, kasketli, takkeli, sera_cami (var), dalgic, arici_tulu, kaynakci, boynuzlu, yumurta_kabugu, aurora, holding_muduru |
| vizor_ | Vizör yüzü | 12 | nokta_goz, uykulu, saskin, kizgin, tek_kas, kalp, yildiz, piksel, biyikli, gozluklu, aglamakli, bos_ekran |
| tulum_ | Tulum deseni | 12 | kooperatif_mavisi, camurlu, ekose, cizgili, benekli, altin, kamuflaj, sazlik_moru, kanyon_kizili, ayaz_beyazi, aurora, holding_kirmizisi |
| canta_ | Çanta süsü | 12 | tencere, tava, surahi, fener, kurek, gidak_pelusu, bayrak, ruzgar_firildagi, saksi, caydanlik, ata_rozeti, maya_kavanozu |
| kaplama_ | Çapa kaplaması | 12 | fabrika, pasli, krom, ahsap_sapli, sari_siyah, cicekli, altin, kristal, kemik, aurora, holding, eksi_maya |
| jest_ | Jest | 12 | el_salla, alkis, halay, zafer, omuz_silk, otur, uzan, parmak_salla, cay_ic, islik, reverans, gobek_at |

Not: ICERIK §9 listesindeki "Kasketli, Takkeli" vb. kısa sıra yukarıdadır. Listede olmayan zincir ödülleri: bkz. §0.

## 10. Yapı (ICERIK §10)

Malzemeler (build.json): `wood` var, `kerpic_tas` var, `celik_sac` var, `mayali_beton` var; cam seti ayrı:

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| cam_duvar | Cam Duvar | §10.1 | cam seti, T4 |
| cam_cati | Cam Çatı | §10.1 | |
| cam_zemin | Cam Zemin | §10.1 | |
| cam_kapi | Cam Kapı | §10.1 | |

Şekiller (26, ASCII kimlikleri placeables.json `buildCosts` içinde var): `kare_temel, ucgen_temel, kazik_temel, duvar, yarim_duvar, alcak_duvar, kapi_kasasi, pencereli_duvar, ucgen_duvar, kare_zemin, ucgen_zemin, merdiven, rampa, egimli_cati, cati_kosesi, cati_tepesi, duz_cati, sutun, kiris, parmaklik, cit, bahce_kapisi, sur_duvari, sur_kapisi, kule_tabani, makine_platformu_(kucuk|orta|buyuk)`.

Yerleştirilebilirler (72 = 6+8+8+6+10+8+8+18). `var` = placeables.json.

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| ahsap_kapi | Ahşap Kapı | §10.2 Kapı | var |
| celik_kapi | Çelik Kapı | | |
| cift_kanat_kapi | Çift Kanat Kapı | | |
| garaj_kapisi | Garaj Kapısı | | |
| tavan_kapagi | Tavan Kapağı | | |
| el_merdiveni | El Merdiveni | | |
| sandik | Sandık | §10.2 Depo | var |
| ambar | Ambar | | 60 yuva |
| silo | Silo | | tek türden 2.000 |
| sivi_tanki | Sıvı Tankı | | |
| akilli_depo | Akıllı Depo | | |
| tohum_dolabi | Tohum Dolabı | | |
| alet_askisi | Alet Askısı | | |
| kupa_rafi | Kupa Rafı | | |
| uyku_tulumu | Uyku Tulumu | §10.2 Yaşam | var |
| yatak | Yatak | | |
| ranza | Ranza | | |
| sedir | Sedir | | |
| masa | Masa | | |
| sandalye | Sandalye | | |
| hali | Halı | | |
| soba | Soba | | |
| mesale | Meşale | §10.2 Işık | var |
| fener | Fener | | |
| elektrik_lambasi | Elektrik Lambası | | var |
| projektor | Projektör | | araç eklentisi `arac_projektoru` |
| nilufer_saksisi | Nilüfer Saksısı | | |
| sokak_lambasi | Sokak Lambası | | |
| korkuluk | Korkuluk | §10.2 Tarım | var |
| yagmurlama | Yağmurlama | | |
| damla_hatti | Damla Hattı | | |
| buyutme_lambasi | Büyütme Lambası | | |
| kompost_kasasi | Kompost Kasası | | |
| saksi | Saksı | | |
| yukseltilmis_yatak | Yükseltilmiş Yatak | | |
| ari_kovani | Arı Kovanı | | var (kart `ari_kovani` ayrı tablo) |
| sirik | Sırık | | |
| tarla_tabelasi | Tarla Tabelası | | |
| yemlik, suluk, folluk, savas_agili_bayragi, ahir_zili, baglama_diregi, kasinma_fircasi, eyerlik | Hayvan (8) | §10.2 | hepsi var |
| mancinik | Mancınık | §10.2 Hizmet | var |
| harita_masasi | Harita Masası | | |
| is_emri_panosu | İş Emri Panosu | | |
| borsa_tahtasi | Borsa Tahtası | | |
| katalog_terminali | Katalog Terminali | | |
| tohum_carki | Tohum Çarkı | | |
| gardirop | Gardırop | | |
| tabela | Tabela | | |
| saksi_cicegi | Saksı Çiçeği | §10.2 Süs | |
| duvar_halisi | Duvar Halısı | | |
| tablo | Tablo | | oyuncu fotoğrafı |
| ekip_bayragi | Ekip Bayrağı | | |
| ata_heykeli | Ata Heykeli | | |
| ruzgar_cani | Rüzgâr Çanı | | |
| kus_evi | Kuş Evi | | |
| gidak_cucesi | Gıdak Cücesi | | |
| cesme | Çeşme | | |
| hamak | Hamak | | |
| salincak | Salıncak | | |
| kamp_atesi | Kamp Ateşi | | |
| raf | Raf | | |
| kitaplik | Kitaplık | | |
| saat | Saat | | |
| radyo | Radyo | | |
| semaver | Semaver | | orders.json item grant var (`item:semaver`) |
| kilim | Kilim | | |

Ek (placeables.json'da olup §10.2 sayımına girmeyen, var): `ocak, maya_feneri` ve istasyonlar.

## 11. İstasyonlar ve makineler T3–T6 (ICERIK §11)

25 yeni (T3 10, T4 9, T5 4, T6 2).

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| yuksek_firin | Yüksek Fırın | §11 | T3, Ata (Ocak T3); Titan için "ark eki" (kimliği yok, belirsiz) |
| kimya_tezgahi | Kimya Tezgâhı | §11 | T3 |
| pres | Pres | §11 | T3 |
| mutfak | Mutfak | §11 | T3 |
| damitici | Damıtıcı | §11 | T3 |
| tursu_ficisi | Turşu Fıçısı | §11 | T3 |
| hasat_robotu | Hasat Robotu | §11 | T3 otomasyon |
| otomatik_yemlik | Otomatik Yemlik | §11 | T3 |
| yumurta_toplayici | Yumurta Toplayıcı | §11 | T3 |
| kompostor | Kompostör | §11 | T3 |
| cam_ocagi | Cam Ocağı | §11 | T4, Ata (Ocak T4) |
| elektronik_tezgahi | Elektronik Tezgâhı | §11 | T4 |
| montaj_makinesi | Montaj Makinesi | §11 | T4 |
| rafineri | Rafineri | §11 | T4 |
| sera_kontrol_unitesi | Sera Kontrol Ünitesi | §11 | T4, sera |
| ekim_robotu | Ekim Robotu | §11 | T4 |
| sagim_kirkim_kolu | Sağım-Kırkım Kolu | §11 | T4 |
| paketleyici | Paketleyici | §11 | T4 |
| sondaj_kulesi | Sondaj Kulesi | §11 | T4, Zengin Yatak |
| kristal_kesici | Kristal Kesici | §11 | T5, Ata (Ocak T5) |
| batarya_tezgahi | Batarya Tezgâhı | §11 | T5 |
| asi_tezgahi | Aşı Tezgâhı | §11 | T5, gen aktarma |
| zeplin_hangari | Zeplin Hangarı | §11 | T5 |
| maya_dokumhanesi | Maya Dökümhanesi | §11 | T6, Ata (Ocak T6) |
| kabartma_odasi | Kabartma Odası | §11 | T6, Ana Maya tarifi |

Var (T1–T2, placeables.json): `tezgah, tas_firin, komur_ocagi, ocakbasi, paketleme, arastirma_masasi, demirci_ocagi, bicki, degirmen, dokuma_tezgahi, kasap_tezgahi, kulucka, arac_atolyesi, oyma_iskelesi` (+ `kargo_rampasi` ItemNames). Main'de yok (M1/M2 payı): `sirt_yazicisi, tabaklama_askisi, harman_yeri`.

## 12. Güç, sinyal ve taşıma (ICERIK §12; 43)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| hayvan_carki | Hayvan Çarkı | §12 Üretici | var |
| ruzgar_gulu | Rüzgâr Gülü | | var |
| su_carki | Su Çarkı | | T3 |
| biyoyakit_jeneratoru | Biyoyakıt Jeneratörü | | T3 |
| gunes_paneli | Güneş Paneli | | T4 |
| cizbiz_tanki | Cızbız Tankı | | T3 |
| kabarma_toplayicisi | Kabarma Toplayıcısı | | T4 |
| maya_reaktoru | Maya Reaktörü | | T6 |
| aku_mk1 | Akü Mk1 | §12 Akü | var |
| aku_mk2 | Akü Mk2 | | T3 |
| aku_mk3 | Akü Mk3 | | T4 |
| aku_mk4 | Akü Mk4 | | T5 |
| kablo | Kablo | §12 Dağıtım | var (eşya) |
| direk | Direk | | var |
| salter | Şalter | | var |
| sigorta_kutusu | Sigorta Kutusu | | M4'e atanmış |
| dugme | Düğme | §12 Sinyal | var |
| salter_kolu | Şalter Kolu | | |
| basinc_plakasi | Basınç Plakası | | var |
| hareket_sensoru | Hareket Sensörü | | |
| gun_gece_sensoru | Gün-Gece Sensörü | | |
| kabarma_sensoru | Kabarma Sensörü | | M3 payı; main'de yok |
| depo_sensoru | Depo Sensörü | | |
| guc_sensoru | Güç Sensörü | | |
| nem_sensoru | Nem Sensörü | | |
| sayac | Sayaç | | |
| zamanlayici | Zamanlayıcı | | |
| kapi_ve | VE | §12 Kapı | |
| kapi_veya | VEYA | | |
| kapi_degil | DEĞİL | | |
| karsilastirici | Karşılaştırıcı | | |
| bellek | Bellek | | |
| siren | Siren | §12 Uyarı | M3 payı; main'de yok |
| sinyal_lambasi | Sinyal Lambası | | |
| bant | Bant | §12 Taşıma | T3 |
| ayirici | Ayırıcı | | |
| birlestirici | Birleştirici | | |
| tasima_kolu | Kol | | adı yalnız "Kol", `tasima_kolu` önerilir; T3 |
| boru | Boru | | |
| pompa | Pompa | | |
| drone_istasyonu | Drone İstasyonu | | T5, 6 drone, 80 m |
| teleferik_istasyonu | Teleferik İstasyonu | | T5 |
| teleferik_diregi | Teleferik Direği | | T5 |

Drone biriminin kendisi: `drone`. Kapı grubu adlarındaki `kapi_` öneki "VE/VEYA" gibi kısa adların çakışmasını önler.

## 13. Kuleler ve tuzaklar (ICERIK §13)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| civi_kulesi, sapan_kulesi, sacma_kulesi | T1–T2 kuleleri | §13 | var (defense.json) |
| alev_kulesi | Alev Kulesi | §13 | T3 |
| havan_kulesi | Havan Kulesi | §13 | T3 |
| ark_bobini | Ark Bobini | §13 | T4, 120 W |
| lazer_kulesi | Lazer Kulesi | §13 | T5, 200 W |
| maya_topu_kulesi | Maya Topu Kulesi | §13 | T6, 300 W |
| diken_tarlasi, yapiskan_zemin, firlatma_pedi, cukur_kapagi | T1–T2 tuzakları | §13 | var (defense.json) |
| elektrikli_cit | Elektrikli Çit | §13 | T3 |
| mayin | Mayın | §13 | T3 |
| yag_hendegi | Yağ Hendeği | §13 | T3 |
| dondurucu_puskurtucu | Dondurucu Püskürtücü | §13 | T5 |

## 14. Araçlar ve eklentiler (ICERIK §14)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| el_arabasi | El Arabası | §14 | var (recipes.json) |
| cekirge | Çekirge (ATV) | §14 | var (vehicles.json); yaratık `cekirge` ile aynı ad |
| traktor | Traktör | §14 | T3 |
| kamyon | Kamyon | §14 | T3 |
| bicerdover | Biçerdöver | §14 | T4 |
| hava_yastikli | Hava Yastıklı | §14 | T4 |
| zeplin | Zeplin | §14 | T5 |
| romork | Römork | §14 eklenti | |
| pulluk | Pulluk | | |
| ekim_makinesi | Ekim Makinesi | | |
| su_tankeri | Su Tankeri | | |
| vinc | Vinç | | |
| kepce | Kepçe | | |
| zirh_tampon | Zırh ve Tampon | | tek eklenti |
| arac_kulesi | Araç Kulesi | | |
| arac_projektoru | Projektör | | `projektor` yerleştirilebiliriyle çakışmasın |
| balon | Balon | | |

Yakıt: `bitkisel_yag` (T2), `biyoyakit` (T3), akü (T4).

## 15. Yemekler #15–#32 (ICERIK §15)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| pilav | Pilav | §15 #15 | T3 |
| acili_ezme | Acılı Ezme | #16 | T3 |
| tursu | Turşu | #17 | T3 |
| sucuk | Sucuk | #18 | T3 |
| lahmacun | Lahmacun | #19 | T3 |
| bal_kaymak | Bal Kaymak | #20 | T3 |
| misir_ekmegi | Mısır Ekmeği | #21 | T4 |
| karpuz_peynir | Karpuz Peynir | #22 | T4 |
| kaktus_serbeti | Kaktüs Şerbeti | #23 | T4 |
| kozde_misir | Közde Mısır | #24 | T4 |
| guvec | Güveç | #25 | T4 |
| kar_cayi | Kar Çayı | #26 | T5; ekin `snow_tea` ile karışmasın |
| pekmez | Pekmez | #27 | T5 |
| pancar_tatlisi | Pancar Tatlısı | #28 | T5 |
| baklava | Baklava | #29 | T5 |
| eksi_maya_ekmegi | Ekşi Maya Ekmeği | #30 | T6 |
| maya_corbasi | Maya Çorbası | #31 | T6 |
| kooperatif_ziyafeti | Kooperatif Ziyafeti | #32 | T6 |

Var (foods.json): `kozde_salgam, haslanmis_yumurta, sebze_corbasi`. M2'ye ait ama main'de yok (ICERIK kimlikleri): `menemen, bazlama, izgara_et, salgam_suyu, mantar_sote, bogurtlen_receli, gozleme, sarimsakli_yogurt, findik_ezmesi, kavurma, ayran`.

## 16. Zırh ve kuşam (ICERIK §16)

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| kooperatif_tulumu | Kooperatif Tulumu | §16 | T0, baştan |
| deri_yelek | Deri Yelek | | T1 (M2) |
| demir_plaka | Demir Plaka | | T2 (M2) |
| celik_zirh | Çelik Zırh | | T3 |
| titan_zirh | Titan Zırh | | T4 |
| kristal_zirh | Kristal Zırh | | T5 |
| mayali_zirh | Mayalı Zırh | | T6 |

Modüller (18): `filtre_maskesi` (var, recipes.json), `serin_yelek`, `termal_astar`, `maya_yalitimi`, `planor`, `cift_ziplama_tabani`, `genis_canta`, `kemer_uzatmasi`, `aku_paketi`, `hizli_sarj`, `miknatis_eldiveni`, `far`, `yonlu_parasut`, `amortisor`, `yuzgec`, `hayvan_dudugu`, `tamir_kiti`, `kalkan_jeneratoru`. M2'nin hangi 6'sı olduğu belgede yok (ICERIK §20: Filtre Maskesi T2, Serin Yelek T3, Termal Astar T4, Maya Yalıtımı T5); 12 modül payı bu yüzden net değil.

Çapa: `capa_mk2` (atalar.json `unlocks` içinde anılıyor), `capa_mk3` (anılıyor), `capa_mk4`, `capa_mk5`, `capa_mk6` (M4 = Mk.III–VI; Mk.III ve ilgili tarif kimlikleri main'de yalnız `unlocks` dizesinde, recipes.json'da yok).

## 17. Keşif noktaları (ICERIK §17)

Tür kimlikleri (biomes.json `world.poiTypes`, hepsi var): `seyir_kulesi, kadim_tas, ata_tasi, oyuk, enkaz, holding_kampi, yabani_bahce, yuva, zengin_yatak`.

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| zengin_yatak | Zengin Yatak | §17 | var (poiTypes, M4 işaretli); 14 adet, biyom dağılımı yok |
| genel_merkez | Genel Merkez | §17 | ayrı tür değil, "Holding Kampı 3 + GM"; chips.json kaynak anahtarı `holding:genel_merkez` var; `holding:kamp` var |
| seyir_kulesi, ata_tasi, yabani_bahce, yuva, enkaz, oyuk | M2/M3 türleri | | var; Çayır ve Koru payları main'de |

Kalan biyomlar için ICERIK yalnız toplam verir (Seyir Kulesi 18, Kadim Taş 12, Ata Taşı 6, Oyuk 15, Enkaz ~30, Yabani Bahçe 10, Yuva ~20, Holding Kampı 3 + GM). Tablodan çıkan türetmeler (ICERIK yazmıyor): Ata Taşı Sazlık/Kanyon/Ayaz/Göbek 1'er (= 6 − 2 = 4); Oyuk Sazlık/Kanyon/Ayaz 3'er (= 15 − 6 = 9; Göbek'te Oyuk yok); Kadim Taş Sazlık 2, Kanyon 3, Ayaz 2 (§7 tablosundan); Seyir Kulesi 16, Yabani Bahçe 8, Yuva ~16, Enkaz ~24 kalan biyomlara bölüşülecek (bölüşüm belgede yok, boşluk).

## 18. İş emri zincirleri #13–#24 ve #37–#48 (ICERIK §18; kimlik yalnız)

Var (orders.json, #1–12): `ciraklik, salgam_baronu, yesil_parmak, islahci, alaca_bahce, devler_ligi, coban, suru_sahibi, mandira, cins_hayvan, suvari, savas_agili`. M3 payı (#25–36, main'de yok, bilgi için): `gece_bekcisi, merdiven_zinciri, sovmen, kartbaz, asici, mayaci, avci, ata_yolu, kabarik_ata_zinciri, azman_kiran, holding_belasi, silahsor` (`merdiven` ve `kabarik_ata` varlık kimlikleriyle çakışmasın diye son ek).

| kimlik | Türkçe ad | ICERIK § | not |
|---|---|---|---|
| usta | Usta | §18 #13 | Yapı |
| elektrikci | Elektrikçi | #14 | |
| otomasyoncu | Otomasyoncu | #15 | |
| kalfa | Kalfa | #16 | |
| mantikci | Mantıkçı | #17 | |
| mimar | Mimar | #18 | |
| gozcu | Gözcü | #19 | Keşif |
| kasif | Kâşif | #20 | |
| magaraci | Mağaracı | #21 | |
| ansiklopedist | Ansiklopedist | #22 | |
| kadim_yol | Kadim Yol | #23 | |
| enkazci | Enkazcı | #24 | |
| tuccar | Tüccar | #37 | Ticaret |
| borsaci | Borsacı | #38 | |
| cesitci | Çeşitçi | #39 | |
| carkci | Çarkçı | #40 | |
| gurme | Gurme | #41 | |
| koleksiyoncu | Koleksiyoncu | #42 | |
| can_dostu | Can Dostu | #43 | Ekip |
| ucan_uye | Uçan Üye | #44 | |
| ezilenler_kulubu | Ezilenler Kulübü | #45 | |
| nakliyeci | Nakliyeci | #46 | |
| sofor | Şoför | #47 | |
| fotografci | Fotoğrafçı | #48 | |

Başarım kimliği kalıbı: `chain_<zincir>` (achievements.json `chainPrefix`); ek başarımlar var: `ilk_gece`, `maya_hakimi`.
