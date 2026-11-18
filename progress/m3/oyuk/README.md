# M3 oyuk kanıtı

- `test.log`: `tools/test.ps1 -Only build,oyuk,world` (oyuk: `--test=oyuk`, world: M2 16 nokta + 6 Oyuk).
- `test-tam.log`: commit öncesi tam `tools/test.ps1`.
- `shots/` (pencereli `--test=oyuk --oyuk_shots=...`, oyuncu kamerası):
  - `kostebek_ustte.png`: Köstebek Bey delikten çıkmış, Sulama öncesi.
  - `kostebek_sikisti.png`: bütün delikler tıkalı, ortada sıkışmış ve sersem.
  - `koru_oyuk_agzi.png`: Sis Korusu'ndaki Oyuk ağzı (yüzey).
  - `yarasa_toplu.png`: karanlıkta toplu yumak.
  - `yarasa_dagildi.png`: kandil yanınca dağılmış yumak, ana yarasa görünür.
- Sınama kurulumu (`OyukTest.cs` başında yazılı): su deposu Sim'den doldurulur (göl yolculuğu yerine); Çekim'le tutulan kaya deliğin üstüne taşınır (nişan yerine); silah isabeti geçici savaş köprüsünün hedefine doğrudan verilir.
- Görünüş, his ve zorluk **sahibin onayını bekliyor**.
