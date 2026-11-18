# Gorsel dunya: rapor (dal `claude/gorsel-dunya-4ec6e7`)

Kanit: `once/` (eski, 16 goruntu), `sonra/` (yeni, ayni kamera, 16 goruntu), yan yana: `once_sonra_ogle.png`,
`once_sonra_gunbatimi.png`, `once_sonra_sabah.png`, `once_sonra_kabarma_gecesi.png` (satirlar: kapsul, us, gol, tepe;
sol eski, sag yeni). Goruntuler `--test=shots --shots_quick` (bot yok, us bos), `yuksek` on ayari.

## Ne degisti

- **Arazi** (`terrain.gdshader`, `World.Ground.cs`): bes dosenebilir PBR katmani (cimen, toprak, kaya, kum, tarla).
  Dokular sahibin kitapligindaki taramalardan (`F:\Assets`, yalniz okundu): cimen Poly Haven `leafy_grass` (CC0),
  toprak/kaya/kum Megascans; tarla = toprak taramasi + koddan egatlar. `pipeline/import_terrain_scans.py` 2K (cimen,
  toprak, kaya) ve 1K (kum, tarla) paketler, renkleri Altin Cayir paletine ceker. Lisans satirlari
  `pipeline/licenses.md`, ayrinti `assets/textures/terrain/KAYNAK.md`. Koddan yedek: `pipeline/gen_terrain_textures.py`.
  Denetim dokusu: patikalar (`biomes.json` `paths`), aciklik, gol kiyisi kumu, surulmus tarla (`World.MarkSoil`,
  FarmView'dan). Katmanlar kendi yukseklikleriyle karisir; iki olcekli tekrar kirici; buyuk olcekte yesil/altin
  lekeler (biyom paletinden carpan); egimde uc duzlemli kaya; su kenarinda islaklik. Maya damarlari korundu.
- **Ot** (`GrassField.cs`, `grass_field.gdshader`): kameraya bagli iki halka (yakin sik, uzak seyrek LOD), uzaklikla
  sirayla seyrelir, esinti lekeleriyle ruzgar, oyuncu ayagi iter. Ot sikligi CPU'da (egim, su, patika, kum, tarla,
  aciklik, ciplak leke); yapi ve esya altindan kalkar (`World.ClearGrass`, Station ve Furniture).
- **Gok ve isik** (`sky_kabarma.gdshader`, `World.Sky.cs`, `biomes.json` times): pus bandi, gunes cevresi sacilma,
  isikli ve oz golgeli bulutlar, kucuk HDR gunes; AgX + kontrast/doygunluk; ince sis, hava perspektifi, gunes
  sacilmasi; dort saat yeniden ayarlandi (gunes 2.2-2.9, ortam 0.5-0.62). Maya sutunu yumusak isik demeti
  (`maya_beam.gdshader`), ufuk tepeleri orman lekeli (`far_hills.gdshader`).
- **Su** (`water.gdshader`): kirilma (ekran dokusu), derinlikle yutma, uc olcekli dalgacik, Fresnel yansima, kirik kopuk.
- **Kademeler** (`graphics.json`): dusuk / orta / yuksek / ultra. Yuksek: 4 bolmeli golge, SMAA, SSAO, ot 22/50 m.
  Ultra: PCSS yari golge, SSIL, SDFGI, hacimsel sis, SSR, TAA, ot 32/80 m.
- **Araclar**: `--shots_out/--shots_times/--shots_marks`, goruntu basina `gpu_ms`; `--test=gfxab` (ozellikleri tek
  tek kapatip kare suresi).

## Baska akislarin dosyalarina kucuk dokunuslar

`FarmView.cs` (Init'e bir satir: MarkSoil), `Station.cs` ve `Placeables.cs` (`_Ready`'ye birer satir: ertelenmis
ClearGrass), `Main.cs` (Tests'e `gfxab` satiri), `WorldShots.cs` (cikti klasoru, suzgec, GPU suresi).

## Acik kalanlar

- **Varsayilan kademenin (`yuksek`) kare hizi dogrulanmadi.** Olcum sirasinda GPU baska islerle %100 doluydu (sahibin
  oyunu ve diger oturumlarin Godot'lari; bir ara benim takilan bir `gfxab` surecim de, kapattim). `us` yer iminde art
  arda: `dusuk` 9,8 ms, `yuksek` 23,8 ms (baslarken 2 baska Godot calisiyordu). Bu olcumden SONRA `yuksek`
  hafifletildi (PCSS kapali, SSIL ultraya, ot 22/50 m, golge 110 m, MSAA yerine SMAA) ama yeniden olculmedi. Butceyi
  tutmazsa yedek kademe `orta`. Bos
  makinede: `--test=perf` (4 yer imi) ve `--test=gfxab` (hangi ozellik ne kadar). 11,1 ms'yi asarsa ilk adaylar:
  uzak ot halkasi (`grassFar`), 4 bolmeli golge, `terrainDetail`.
- **Megascans lisansi dogrulanmali**: uc Megascans klasorunde (`forest_floor_wgyhdgy`, `cracked_nordic_coastal_rock`,
  `beach_sand_uinladeo`) lisans alani yok; ana oturumun bildirdigi "Fab Standart Lisans" yazildi. Sahip dogrulamali.
  Poly Haven cimen CC0 (json'da yaziyor).
- Arazi dizileri sikistirmasiz RGBA8 (2 dizi x 5 katman x 2048², mip ile ~220 MB VRAM). Bellek sorun olursa: dusuk
  kademede 1K'ya indirme ya da BPTC. Depoya ~115 MB PNG eklendi (LFS yok).
- `sonra/` koddan dokularla cekildi; taramali hali `tarama/` (4 yer imi x ogle, gun batimi; ayni kameralar). Genel
  renk ayni, yakinda daha dogal ayrinti, patika daha kahverengi.
- agy kullanilamadi (izin sinifinda reddedildi); gerek kalmadi.
- Agaclar, kayalar ve sus bitkileri hala toon/duz renkli (gorsel-model akisinin isi); yeni isikla birlikte en cok
  goze batan sey bunlar.
- Gun batiminda on plan biraz karanlik; `gunbatimi.ambient` 0.5 -> 0.6 denenebilir.
- `ultra` hic calisir gorulmedi: sahibin oyunu VRAM'in ~7/8 GB'ini tutarken goruntu suresi doldu (hata yok, yalniz
  cok yavas). Golge atlasi 8192'den 4096'ya indirildi. Bos makinede bir kez `--preset=ultra` ile bakilmali.
- Tarla katmani (`MarkSoil`), yapi altindan ot kalkmasi (`ClearGrass`) ve ayakla otun egilmesi oyunda gorulmedi
  (`--shots_quick` bos us, bot yok). 1 m tarla karesi 0,586 m denetim hucresine duser: kenari keskin kare degil,
  yumusak leke olur.
- `SANAT-SES.md` §2 gunduz renkleri ve `TEKNIK.md` §10 "Yuksek" satiri yeni degerlere guncellendi;
  `BiomesDataTests` buna gore (166/166 gecti). Gunes yukseklikleri ve sabah sisi x1,5 kurali korundu.
- `--headless --test=build` gecti (75 yerlestirme, 0 hata). `sonra/` goruntuleri gunes yukseklikleri belgedeki
  12/62/4 dereceye geri alinmadan once cekildi (13/58/5); fark kucuk.
- Oyun icinden kademe secimi yok (yalniz `--preset=` ve `graphics.json` `default`).
- Tam `tools/test.ps1` ve bot kosulmadi (yeni calisma modu); yalniz derleme ve goruntu kosulari.
