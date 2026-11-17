namespace Kabarma.Sim;

// Dunya ve biyomlar: biomes.json (TEKNIK §13 "biomes", SANAT-SES §2 ve §6, URETIM §6 M1 kapsami).
// Arazi, gol, bitki ortusu, kaynak dugumleri ve verimleri, gun saatlerinin isigi, yer imleri.
// Vektorler [x, z] (yatay) ya da [x, y, z] dizisidir. Renkler "RRGGBB".

/// <summary>Arazi: boyut, gurultu, ussun duzlugu, tepeler (GDD 8.1: tarla egimi 15 dereceden az).</summary>
public sealed class TerrainDef
{
    public int Size { get; set; } = 300;
    public int Seed { get; set; } = 4417;
    public float NoiseFrequency { get; set; } = 0.012f;
    public float NoiseAmp { get; set; } = 9f;
    public float DetailAmp { get; set; } = 0.8f;
    public float BaseFlatInner { get; set; } = 18f;
    public float BaseFlatOuter { get; set; } = 42f;
    /// <summary>Uzak cayirin yuvarlak tepeleri: dusuk frekansli gurultu, bu mesafeler arasinda acilir.</summary>
    public float HillAmp { get; set; }
    public float HillFrequency { get; set; }
    public float HillStart { get; set; }
    public float HillEnd { get; set; }
    /// <summary>Dunya kenarinda yukselen sirt (ufuk katmanina gecis).</summary>
    public float EdgeStart { get; set; }
    public float EdgeHeight { get; set; }
    /// <summary>En yuksek tepe (yer imi "tepe"): merkez [x, z], yukseklik, yaricap.</summary>
    public List<float> Peak { get; set; } = new();
    public float PeakHeight { get; set; }
    public float PeakRadius { get; set; }
}

/// <summary>Gol (URETIM 6 M1: Ocak'a 60-90 m). Sig: oyuncu icinde yurur (SANAT-SES 3.1 "suda yurume").</summary>
public sealed class LakeDef
{
    public List<float> Center { get; set; } = new();
    public float Radius { get; set; }
    public float ShoreNoise { get; set; }
    public float Depth { get; set; }
    /// <summary>Ussun Ocak'ina uzaklik siniri (URETIM 6 M1), dunya sinamasi denetler.</summary>
    public List<float> OcakDistance { get; set; } = new();
    public string Shallow { get; set; } = "";
    public string Deep { get; set; } = "";
    public string Foam { get; set; } = "";
    public float FoamWidth { get; set; }
    public float Clarity { get; set; }
}

/// <summary>Yer imi (SANAT-SES §9): sabit kamera konumu ve bakis yonu; oyun kamerasi yuksekliginde.</summary>
public sealed class BookmarkDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>[x, z] goz konumu; yukseklik zemin + EyeHeight.</summary>
    public List<float> Eye { get; set; } = new();
    public float EyeHeight { get; set; } = 2.5f;
    /// <summary>[x, y, z] bakilan nokta; y zemine gore.</summary>
    public List<float> Target { get; set; } = new();
    public float Fov { get; set; } = 68f;
    /// <summary>"ocak": goz ve hedef Ocak'in gercek konumuna gore (us oyuncuyla kurulur; yeri oyundan oyuna kayar).</summary>
    public string Anchor { get; set; } = "";
}

public sealed class WorldDef
{
    public string Biome { get; set; } = "";
    public TerrainDef Terrain { get; set; } = new();
    public LakeDef Lake { get; set; } = new();
    /// <summary>Krater isik sutunu [x, z] ve ufuk tepe katmanlarinin uzakliklari.</summary>
    public List<float> Pillar { get; set; } = new();
    public List<float> HorizonRings { get; set; } = new();
    /// <summary>Yukseklik sisi [baslangic yuksekligi m, yogunluk]: vadilerde birikir, tepeden bakista sis katmanlari (SANAT-SES §9 tepe).</summary>
    public List<float> HeightFog { get; set; } = new() { 1f, 0.015f };
    /// <summary>Bitki konmayan aciklik [x, z, yaricap]: inis kapsulu, ussun tarlasi ve istasyonlari.</summary>
    public List<List<float>> Clearings { get; set; } = new();
    public List<BookmarkDef> Bookmarks { get; set; } = new();
}

/// <summary>Gun saati isigi (SANAT-SES §2 tablosu). Aci derece; azimut 0 kuzey (+Z), 90 dogu (-X) (World.Dir).</summary>
public sealed class TimeOfDayDef
{
    public string Name { get; set; } = "";
    public float LightElevation { get; set; }
    public float LightAzimuth { get; set; }
    public string LightColor { get; set; } = "";
    public float LightEnergy { get; set; }
    public float SunDisk { get; set; }
    public float MoonDisk { get; set; }
    public float MoonElevation { get; set; }
    public float MoonAzimuth { get; set; }
    public string SkyTop { get; set; } = "";
    public string SkyHorizon { get; set; } = "";
    public string Fog { get; set; } = "";
    public float FogDensity { get; set; }
    public float Ambient { get; set; }
    public float Stars { get; set; }
    public float Aurora { get; set; }
    public float Clouds { get; set; }
    public string CloudColor { get; set; } = "";
    /// <summary>Zemindeki Maya damarlarinin isimasi (Kabarma gecesi).</summary>
    public float Veins { get; set; }
}

/// <summary>Bitki ortusu ve sus kayasi: model, adet, olcek, yerlestirme kurali; Bicme verimi varsa kaynaktir.</summary>
public sealed class DecorDef
{
    public string Id { get; set; } = "";
    public string Model { get; set; } = "";
    public int Count { get; set; }
    public List<float> Scale { get; set; } = new() { 1f, 1f };
    public float MinDistance { get; set; }
    public float MaxDistance { get; set; } = 1000f;
    /// <summary>Gol kiyisindan en az bu kadar uzak (m); negatifse yalniz kiyiya bu kadar yakin.</summary>
    public float LakeClear { get; set; } = 2f;
    public float Wind { get; set; }
    /// <summary>Bu uzakliktan sonra cizilmez (m); 0 = sinirsiz. Sis zaten ortuyor.</summary>
    public float ViewDistance { get; set; }
    public bool Tree { get; set; }
    public bool Shadow { get; set; } = true;
    public float Sink { get; set; }
    public float CollideRadius { get; set; }
    public float CollideHeight { get; set; }
    /// <summary>Bicme ile toplanan esya ("fiber", "wheat"); bos ise sus. Geri gelmez (URETIM 6 M1, Capa).</summary>
    public string Harvest { get; set; } = "";
    public int Yield { get; set; }
    /// <summary>URETIM §3 "yer tutucu" isareti: SANAT-SES §9'dan gecemeyen varlik (neden). Bos = gecti.</summary>
    public string Placeholder { get; set; } = "";
}

/// <summary>Elle konmus kume: ussun yakinindaki Lif ve Bugday kumeleri gibi.</summary>
public sealed class PatchDef
{
    public string Decor { get; set; } = "";
    public List<float> Center { get; set; } = new();
    public float Radius { get; set; }
    public int Count { get; set; }
}

/// <summary>Kazma ile toplanan dugum (Rock): tas, kil yatagi, bakir damari.</summary>
public sealed class NodeDef
{
    public string Id { get; set; } = "";
    public string Item { get; set; } = "";
    public string Model { get; set; } = "";
    /// <summary>Dugum basina birim [en az, en cok].</summary>
    public List<int> Units { get; set; } = new();
    public List<float> Size { get; set; } = new() { 1f, 1f };
    public List<List<float>> Positions { get; set; } = new();
    /// <summary>Gol kiyisina dizilen dugum sayisi ve kiyidan uzaklik (m).</summary>
    public int ShoreCount { get; set; }
    public float ShoreOffset { get; set; }
    public List<float> ShoreArc { get; set; } = new();
    public string Placeholder { get; set; } = "";
}

/// <summary>Ot tutamlari: oyuncu yakininda orneklenir (SANAT-SES §6); gorunme yaricapi on ayardan.</summary>
public sealed class GrassDef
{
    public float Chunk { get; set; } = 16f;
    public int PerChunk { get; set; }
    public List<float> Height { get; set; } = new();
    public float BaseClear { get; set; }
    public float Wind { get; set; }
}

public sealed class BiomeDef
{
    public string Name { get; set; } = "";
    /// <summary>Alti ana renk + ogle ve gunbatimi gokyuzu (SANAT-SES §2).</summary>
    public Dictionary<string, string> Palette { get; set; } = new();
    /// <summary>sabah, ogle, gunbatimi, kabarma_gecesi, normal_gece.</summary>
    public Dictionary<string, TimeOfDayDef> Times { get; set; } = new();
    public List<DecorDef> Decor { get; set; } = new();
    public List<PatchDef> Patches { get; set; } = new();
    public List<NodeDef> Nodes { get; set; } = new();
    public GrassDef Grass { get; set; } = new();
}

public sealed class BiomesFile
{
    public WorldDef World { get; set; } = new();
    public Dictionary<string, BiomeDef> Biomes { get; set; } = new();

    public BiomeDef Current => Biomes[World.Biome];
}

public sealed partial class GameData
{
    [DataFile("biomes.json")]
    public BiomesFile Biomes { get; private set; } = new();
}
