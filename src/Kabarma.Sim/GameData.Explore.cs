namespace Kabarma.Sim;

// Dunya M2 (dunya2 akisi): biyom haritasi, dogma tablolari, kesif noktalari, harita ve mancinik. biomes.json icinde
// (TEKNIK §7 "M2 dunyasi", ICERIK §17, GDD §21). Vektorler [x, z]. Kurallar BiomeMap, SpawnTable, PoiPlacer,
// Exploration ve Catapult siniflarinda.

/// <summary>
/// Biyom haritasi (TEKNIK §7.1, M2 ara dunyasi): Ocak'tan krater isik sutununa dogru olculen uzaklik (s),
/// gurultuyle bukulmus siniri gecince ikinci biyom baslar. Gecis bandi Blend metre.
/// </summary>
public sealed class BiomeMapDef
{
    /// <summary>Biyomlar krater yonunde sirayla (ilk: baslangic biyomu).</summary>
    public List<string> Order { get; set; } = new();
    /// <summary>Siradaki biyomun basladigi s uzakligi (m); Order.Count - 1 eleman.</summary>
    public List<float> Boundaries { get; set; } = new();
    /// <summary>Gecis bandinin genisligi (m): agirlik bu bantta 0'dan 1'e yumusakca cikar.</summary>
    public float Blend { get; set; } = 45f;
    /// <summary>Sinir bukulmesi: gurultu genligi (m) ve frekansi (1/m).</summary>
    public float WarpAmp { get; set; } = 30f;
    public float WarpFrequency { get; set; } = 0.008f;
    public int Seed { get; set; } = 91;
    /// <summary>M4 (1800 m dunya): derinlik zinciri yalniz bu biyomlar arasindadir (bos = Order'in hepsi). Order kademe sirasiyla altı biyomdur.</summary>
    public List<string> Home { get; set; } = new();
    /// <summary>M4: krater (Gobek) ve cevresindeki aciksal dilimler. Crater.Radius 0 ise yalniz derinlik zinciri (M2 dunyasi).</summary>
    public CraterDef Crater { get; set; } = new();
    public List<SliceDef> Slices { get; set; } = new();
}

/// <summary>Krater: Gobek biyomu (daire), cevresinde sirt (yukseklik). Merkez dunya konumu [x, z].</summary>
public sealed class CraterDef
{
    public string Biome { get; set; } = "gobek";
    public List<float> Center { get; set; } = new();
    public float Radius { get; set; }
    /// <summary>Gobek agirliginin dustugu band (m).</summary>
    public float Blend { get; set; } = 50f;
    /// <summary>Krater sirti: Radius*RimStart ile Radius*RimEnd arasinda RimHeight (m) yukselen cos tumsegi.</summary>
    public float RimHeight { get; set; }
    public float RimStart { get; set; } = 0.9f;
    public float RimEnd { get; set; } = 1.45f;
}

/// <summary>Krater cevresindeki aciksal dilim: merkez ve yarim genislik (derece; 0 = +X, 90 = +Z). Biome "home" = derinlik zinciri.</summary>
public sealed class SliceDef
{
    public string Biome { get; set; } = "";
    public float Center { get; set; }
    public float HalfWidth { get; set; } = 40f;
}

/// <summary>Yaratik dogma satiri (yaratik2 ve hayvan akislari satir ekler). Suru: Group araliginda uye.</summary>
public sealed class CreatureSpawnDef
{
    /// <summary>creatures.json kimligi (yaratik akisi turu eklemeden once de satir durabilir; aktor yoksa atlanir).</summary>
    public string Creature { get; set; } = "";
    /// <summary>"day", "night" ya da "any" (Kabarma gecesinin dalgalari ayri: kabarma.json).</summary>
    public string Time { get; set; } = "day";
    /// <summary>Ayni biyomda ayni anda secilme agirligi.</summary>
    public float Weight { get; set; } = 1f;
    /// <summary>Suru boyu [en az, en cok].</summary>
    public List<int> Group { get; set; } = new() { 1, 1 };
    /// <summary>Bu turun biyomda ayni anda en cok suru sayisi.</summary>
    public int MaxGroups { get; set; } = 1;
    /// <summary>Ocak'a uzaklik araligi [en az, en cok] (m): ussun icine dogmaz.</summary>
    public List<float> BaseDistance { get; set; } = new() { 40f, 1000f };
    /// <summary>Dogma yerinde biyomun en az agirligi (0..1): 0,6 = biyomun icinde, gecis bandinin derininde degil.</summary>
    public float MinWeight { get; set; } = 0.6f;
    /// <summary>Suru olunce ya da cekilince yenisinin gelmesi icin sure (oyun saniyesi).</summary>
    public float Respawn { get; set; } = 120f;
    /// <summary>Ogle ve gece disinda: yalnizca bu kesif noktasi turunun cevresinde (bos = biyomun her yeri). Ornek "yuva".</summary>
    public string NearPoi { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>Ata dogma satiri (boss akisi satir ekler): Ata hangi kesif noktasinda cagrilir, arena ne kadar.</summary>
public sealed class AtaSpawnDef
{
    /// <summary>atalar.json kimligi (boss akisi).</summary>
    public string Ata { get; set; } = "";
    /// <summary>Kesif noktasi turu: Ata Tasi.</summary>
    public string Site { get; set; } = "ata_tasi";
    /// <summary>Arena yaricapi (m): bu alanda yapi kurulmaz, kesif noktasi duzlestirmesi bunu kapsar.</summary>
    public float ArenaRadius { get; set; } = 28f;
    public string Note { get; set; } = "";
}

public sealed class SpawnsDef
{
    public List<CreatureSpawnDef> Creatures { get; set; } = new();
    public List<AtaSpawnDef> Atalar { get; set; } = new();
}

/// <summary>Kesif noktasi turu (ICERIK §17): ad, gorunus, ne verdigi, yerlesim kurali disinda kalan ortak sayilar.</summary>
public sealed class PoiTypeDef
{
    public string Name { get; set; } = "";
    /// <summary>Geldigi tas: "M2" oyunda calisir; daha sonraki taslar yalniz isaret olarak yerlesir.</summary>
    public string Milestone { get; set; } = "M2";
    /// <summary>Tirmaninca acilan harita yaricapi (m, GDD §21: 600). 0 = acmaz.</summary>
    public float Reveal { get; set; }
    /// <summary>Dibinde mancinik duragi var (Seyir Kulesi).</summary>
    public bool Catapult { get; set; }
    /// <summary>Ilk gorulunce haritaya isaret dusen yaricap (m).</summary>
    public float DiscoverRadius { get; set; } = 40f;
    /// <summary>Arazi duzlestirme yaricapi (m) (TEKNIK §7.2 "kesif noktasi duzlestirmesi").</summary>
    public float Flatten { get; set; } = 6f;
    /// <summary>Etkilesim: Capa basligi ("kazma", "bicme") ya da "e" (el). Bos = etkilesim yok (yalniz isaret).</summary>
    public string Use { get; set; } = "";
    /// <summary>Etkilesimde alinan esyalar (biyoma gore LootByBiome bunu ezer).</summary>
    public Dictionary<string, int> Loot { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> LootByBiome { get; set; } = new();
    /// <summary>Bosaldiktan sonra yenilenme (oyun gunu); 0 = yenilenmez.</summary>
    public float RespawnDays { get; set; }
    /// <summary>Vurus basina alinan birim (Kazma/Bicme ile toplanan noktalarda).</summary>
    public int PerHit { get; set; } = 1;
    /// <summary>Harita isaretinin rengi "RRGGBB".</summary>
    public string Color { get; set; } = "";
    public string Model { get; set; } = "";
    public string Placeholder { get; set; } = "";
}

/// <summary>Biyomdaki kesif noktasi kurali (TEKNIK §7.3 kuralli Poisson): adet, asgari aralik, Ocak'a uzaklik.</summary>
public sealed class PoiRuleDef
{
    public string Type { get; set; } = "";
    public int Count { get; set; } = 1;
    /// <summary>Ayni turden iki nokta arasi en az (m).</summary>
    public float MinSpacing { get; set; } = 80f;
    /// <summary>Ocak'a uzaklik [en az, en cok] (m).</summary>
    public List<float> BaseDistance { get; set; } = new() { 60f, 1000f };
    /// <summary>Noktada biyomun en az agirligi.</summary>
    public float MinWeight { get; set; } = 0.75f;
    /// <summary>Elle konmus yerler [x, z] (once bunlar, sonra tohumdan kalanlar).</summary>
    public List<List<float>> Fixed { get; set; } = new();
}

/// <summary>Harita (GDD §21): gezilen yer acilir. Hucre boyu, oyuncunun cevresinde acilan yaricap, M tusu paneli.</summary>
public sealed class MapDef
{
    /// <summary>Kesif izgarasinin hucresi (m).</summary>
    public float Cell { get; set; } = 8f;
    /// <summary>Oyuncunun cevresinde surekli acilan yaricap (m).</summary>
    public float WalkReveal { get; set; } = 40f;
    /// <summary>Haritanin dokusu (px, kare).</summary>
    public int TexSize { get; set; } = 512;
    /// <summary>Acilmamis alanin rengi ve saydamligi.</summary>
    public string Fog { get; set; } = "2B2A33";
}

/// <summary>Mancinik (GDD §21): duraktan duraga firlatir, parasutle inilir. Yuk nesnesi tasimaz.</summary>
public sealed class CatapultDef
{
    /// <summary>Ucus suresi = Base + mesafe * PerMeter (sn), en cok Max.</summary>
    public float FlightBase { get; set; } = 2.5f;
    public float FlightPerMeter { get; set; } = 0.012f;
    public float FlightMax { get; set; } = 9f;
    /// <summary>Yay tepesinin yuksekligi = mesafe * ApexRatio (en az ApexMin m).</summary>
    public float ApexRatio { get; set; } = 0.22f;
    public float ApexMin { get; set; } = 25f;
    /// <summary>Parasutun acildigi yukseklik (zeminden m) ve inis hizi (m/sn).</summary>
    public float ChuteHeight { get; set; } = 18f;
    public float ChuteSpeed { get; set; } = 4f;
    /// <summary>Hedef duragin bu kadar yanina inilir (m): mancinigin ustune dusmez.</summary>
    public float LandOffset { get; set; } = 6f;
    /// <summary>Iki durak arasi en az mesafe (m): yakin duraga firlatmaz.</summary>
    public float MinDistance { get; set; } = 30f;
    /// <summary>Kurulu mancinik (yerlestirilebilir) ile Seyir Kulesi duragi arasinda kullanma yaricapi (m).</summary>
    public float UseRadius { get; set; } = 3f;
}
