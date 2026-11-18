# Gorsel dunya: rapor (dal `claude/gorsel-dunya-4ec6e7`)

Kanit: `once/` (eski, 16 goruntu), `sonra/` (yeni, ayni kamera, 16 goruntu), yan yana: `once_sonra_ogle.png`,
`once_sonra_gunbatimi.png`, `once_sonra_sabah.png`, `once_sonra_kabarma_gecesi.png` (satirlar: kapsul, us, gol, tepe;
sol eski, sag yeni). Goruntuler `--test=shots --shots_quick` (bot yok, us bos), `yuksek` on ayari.

## Ne degisti

- **Arazi** (`terrain.gdshader`, `World.Ground.cs`): bes dosenebilir PBR katmani (cimen, toprak, kaya, kum, tarla),
  koddan uretildi (`pipeline/gen_terrain_textures.py`, kredi yok; lisans notu `assets/textures/terrain/KAYNAK.md`).
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

- **Performans olculemedi.** Olcum sirasinda GPU baska islerle %100 doluydu (sahibin oyunu ve diger oturumlarin
  Godot'lari; bir ara benim takilan bir `gfxab` surecim de, kapattim). Tek temiz olcum: `us` yer imi `dusuk` 9,8 ms.
  `yuksek` sonra hafifletildi (PCSS kapali, SSIL ultraya, ot 22/50 m) ama temiz makinede yeniden olculmedi. Bos
  makinede: `--test=perf` (4 yer imi) ve `--test=gfxab` (hangi ozellik ne kadar). 11,1 ms'yi asarsa ilk adaylar:
  uzak ot halkasi (`grassFar`), 4 bolmeli golge, `terrainDetail`.
- **agy kullanilamadi**: komut izin sinifinda reddedildi. Dokular koddan; gercek fotograf tabanli dokular ayni dosya
  adlari ve kanal duzeniyle konabilir (KAYNAK.md). Uretim istegi gerekirse: 5 dosenebilir arazi dokusu.
- Agaclar, kayalar ve sus bitkileri hala toon/duz renkli (gorsel-model akisinin isi); yeni isikla birlikte en cok
  goze batan sey bunlar.
- `biomes.json` isik degerleri SANAT-SES §2 tablosundan ayrildi (ogle azimutu 165, enerjiler); belge guncellenmeli.
- Gun batiminda on plan biraz karanlik; `gunbatimi.ambient` 0.5 -> 0.6 denenebilir.
- Oyun icinden kademe secimi yok (yalniz `--preset=` ve `graphics.json` `default`).
- Tam `tools/test.ps1` ve bot kosulmadi (yeni calisma modu); yalniz derleme ve goruntu kosulari.
