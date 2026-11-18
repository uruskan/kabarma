# tarim (M2): kanıt

Dal `claude/m2-tarim-66b764`. Kaynaklar: GDD §8.1–8.3, ICERIK §5 (#1–#11), §10.2.

## Ne yapıldı
- **Belge:** GDD §8.2–8.3'e eksik sayılar ve kararlar eklendi:
  - zararlı ısırığı ve D genine bağlı Zarar;
  - Sarımsak koruması;
  - mantar, kalıcı çalı, büyüme günü;
  - yabani bitki;
  - Alaca zarının ekimde atılması, Alaca ürünün eşya kimliği;
  - çaprazlama yokken kalıtım, yuvarlama ve sınır;
  - D'nin etkisi;
  - Tohum Kaydı;
  - Islah notları.

  ICERIK §10.2'ye Korkuluk bedeli eklendi.
- **Veri:** `crops.json` içinde Çayır ve Koru'nun 11 ekini ve `genetics` bölümü. Korkuluk için `placeables.json` ve `recipes.json` satırları.
- **Sim:**
  - `Genetics.cs`: mutasyon, kalıtım, çaprazlama, Alaca, Tohum Kaydı, Islah notları.
  - `Farm.Genetics.cs`: zararlı, Korkuluk ve Sarımsak, yabani bitki, `HarvestFull`.
  - `Farm.cs`: büyümede D, mantar ve kalıcı çalı; nadas ve nöbet.
  - `Genetics.Save.cs`: kayıt bölümü `farm.genetics`.
- **Godot:**
  - `FarmView.Meshes.cs`: 10 ekinin ve Korkuluk'un koddan modeli (YER TUTUCU). Alaca, toon gölgelendiricinin `tint` değeriyle görünür.
  - `FarmView.Tarim.cs`: tohum seçimi (T / DPad aşağı), yabani bitkiler (YER TUTUCU), Korkuluk ve Arı Kovanı taraması, hasat geri bildirimi.
  - `FarmCard.cs`: tohum ve ekin bilgi kartı.
  - `Hud.DefterIslah.cs`: Defter'de Islah notları.
  - `Bot.Tarim.cs` ve `--test=tarim`.
- **Testler:**
  - `GeneticsTests`: istatistik; mutasyon 200 bin, Alaca 400 bin çekiliş; sınırlar; çaprazlama ortalaması.
  - `FarmM2Tests`: 11 ekinin evreleri ve hasadı, zararlı, Korkuluk, Sarımsak, nöbet, nadas, kalıcı çalı, mantar, yabani bitki, D. Ayrıca 100 büyüme günlük tarla simülasyonu: kendi kendine yeter, gen toplamı seçilimle artar, Alaca bulunur, çaprazlama olur.
  - `GeneticsSaveTests`: bayt bayt aynı gidiş-dönüş.

## Üretim isteği (kredi harcanmadı)
| Ne | Adet | Hat | İstem (özet) | Tahmin |
|---|---|---|---|---|
| Ekin modeli: Buğday, Kabak, Ayçiçeği, Lifotu, Domates, Şapkalı Mantar, Böğürtlen, Ekşilik, Fındık, Sarımsak | 10 | Tripo API (şalgamın yolu: `pipeline/clean_asset.py`, stil föyü) | "stylized chunky low-poly {ekin}, soft rounded forms, vertex-color look, single mature plant, game asset, no ground" | Şalgam hattının fiyatıyla ekin başına ~30 kredi: **~300 Tripo kredisi**. Doğru sayı için önce `--dry-run` |
| Korkuluk | 1 | Tripo API | "stylized farm scarecrow, wooden post, sack head, straw hat, chunky" | ~30 kredi |
| (isteğe bağlı) Alaca dokuları: Benekli, Altın, Aurora | 3 | Genex image `--transparent` | Benek ve parıltı kaplaması | 3 görsel |

Yer tutucuların kaynağı `pipeline/licenses.md` "tarim (M2)" tablosunda.
