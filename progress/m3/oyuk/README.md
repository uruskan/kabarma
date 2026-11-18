# M3 oyuk kanıtı

- `test-tam.log`: main birleşmeden önce tam `tools/test.ps1`. 24 adım geçti, boss bir kez kaldı (Boynuzlu Dede'de oyuncu öldü). Boss tek başına iki kez yeniden koşuldu, ikisi de geçti (`boss-1.log`, `boss-2.log`): yük altında kararsız.
- `test-son.log`: main birleştikten sonra `-Only build,sim,oyuk,world`. Hepsi geçti (607 Sim testi).
- `integrate.log`: main'e alma.
- `shots/` (pencereli `--test=oyuk --oyuk_shots=...`, oyuncu kamerası, `AimOverride`):
  - `kostebek_ustte.png`: Köstebek Bey delikten çıkmış, Sulama öncesi (aydınlık oda, duvar fenerleri).
  - `kostebek_sikisti.png`: bütün delikler tıkalı, ortada sıkışmış ve sersem.
  - `koru_oyuk_agzi.png`: Sis Korusu'ndaki Oyuk ağzı (yüzey). Yer tutucu model ağızdan çok kaya yığınına benziyor (üretim isteğinde).
  - `yarasa_toplu.png`: karanlık oda, yumak tavanda (çok karanlık, mekaniğin kendisi).
  - `yarasa_dagildi.png`: kandil yanmış, sıcak ışık halkası. Yarasalar loşta ancak seçiliyor.
- Sınama kurulumu (`OyukTest.cs` başında yazılı):
  - Su deposu Sim'den doldurulur (göl yolculuğu yerine).
  - Çekim'le tutulan kaya deliğin üstüne taşınır (nişan yerine).
  - Silah isabeti geçici savaş köprüsünün hedefine doğrudan verilir (`TakeWeapon`). Silahın gerçek atış yolu yalnız hedef sorgusuyla sınandı.
- Görünüş, his ve zorluk **sahibin onayını bekliyor**.
