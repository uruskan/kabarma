namespace Kabarma.Sim;

// Hayvancilik verisi: husbandry.json (GDD 9.1-9.4, ICERIK 3.1-3.2, 2.2, 10.2). Kurallar Husbandry*.cs'te.

public sealed class HappinessTier
{
    /// <summary>Bu mutluluktan baslar (0-100).</summary>
    public float Min { get; set; }
    /// <summary>Urunun kalitesi (★1-3, Inventory).</summary>
    public int Stars { get; set; } = 1;
    /// <summary>Urun miktari carpani.</summary>
    public float Yield { get; set; } = 1f;
    /// <summary>Urun sikligi carpani (buyuk = daha sik).</summary>
    public float Speed { get; set; } = 1f;
}

public sealed class HappinessDef
{
    public float Start { get; set; } = 50;
    /// <summary>Hicbir ihtiyac karsilanmasa da hedefin tabani.</summary>
    public float Base { get; set; }
    /// <summary>Mutluluk hedefe gunde en fazla bu kadar yaklasir.</summary>
    public float RatePerDay { get; set; } = 60;
    /// <summary>Ihtiyac -> hedefe katkisi: food, water, roof, space, brush (GDD 9.2).</summary>
    public Dictionary<string, float> Weights { get; set; } = new();
    /// <summary>Okşamanin o gunluk hedefe eki (GDD 9.2).</summary>
    public float PetBonus { get; set; }
    public int PetsPerDay { get; set; } = 1;
    /// <summary>Agildaki her toplanmamis gubre yiginin hedeften dusurdugu, en cok manurePenaltyMax.</summary>
    public float ManurePenalty { get; set; }
    public float ManurePenaltyMax { get; set; }
    public List<HappinessTier> Tiers { get; set; } = new();
}

public sealed class MutationStep
{
    public float P { get; set; }
    public int Delta { get; set; }
}

public sealed class AnimalGenesDef
{
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 10;
    /// <summary>Yabani hayvanin genleri bu aralikta (GDD 8.3'teki yabani tohum gibi 1-3).</summary>
    public int WildMin { get; set; } = 1;
    public int WildMax { get; set; } = 3;
    /// <summary>GDD 8.3: %70 degismez, %20 +1, %8 -1, %2 +2.</summary>
    public List<MutationStep> Mutation { get; set; } = new();
    /// <summary>U (uysallik) her puani urun sikligina (U-1) x bu kadar ekler.</summary>
    public float USpeedPerPoint { get; set; }
    /// <summary>V (verim) her puani urun miktarina (V-1) x bu kadar ekler.</summary>
    public float VYieldPerPoint { get; set; }
    /// <summary>B (boyut): cana, heybeye ve binek hizina (B-1) x bu kadar.</summary>
    public float BHpPerPoint { get; set; }
    public int BPackPerPoint { get; set; }
    public float BSpeedPerPoint { get; set; }
    /// <summary>Komutu dinleme olasiligi: obeyBase + U x obeyPerPoint (en cok 1).</summary>
    public float ObeyBase { get; set; }
    public float ObeyPerPoint { get; set; }
}

public sealed class BreedingDef
{
    public float MinHappiness { get; set; } = 60;
    public float MinDays { get; set; } = 2;
    public float MaxDays { get; set; } = 4;
    /// <summary>Yavru bu kadar gunde yetiskin olur.</summary>
    public float AdultDays { get; set; } = 3;
}

public sealed class EscapeDef
{
    /// <summary>Mutluluk bunun altinda...</summary>
    public float Below { get; set; } = 20;
    /// <summary>...bu kadar saniye kalirsa hayvan agildan cikmak ister.</summary>
    public float Seconds { get; set; } = 240;
    /// <summary>Kacan hayvanin kalan guveni (0-1).</summary>
    public float TrustAfter { get; set; } = 0.5f;
}

public sealed class ManureDef
{
    public string Item { get; set; } = "gubre";
    public float Seconds { get; set; } = 600;
    /// <summary>Yeni gubre bu uzakliktaki yigina eklenir, m.</summary>
    public float PileRadius { get; set; } = 2f;
}

public sealed class SubdueDef
{
    /// <summary>Can bu oranin altina inince tutulabilir (GDD 9.1: %25).</summary>
    public float HpFraction { get; set; } = 0.25f;
    /// <summary>Tutan basliklar (GDD 9.1: Cekim, Zipkin).</summary>
    public List<string> Tools { get; set; } = new();
}

public sealed class TroughDef
{
    public int Capacity { get; set; } = 20;
    /// <summary>Her hayvanin gunde ictigi su.</summary>
    public float PerAnimalPerDay { get; set; } = 2;
    /// <summary>Yagmur gelene kadar Suluk her safakta dolar (ICERIK 10.2).</summary>
    public bool RefillAtDawn { get; set; } = true;
}

public sealed class IncubatorDef
{
    public float Seconds { get; set; } = 600;
}

public sealed class NamedId
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class AnimalProductDef
{
    public string Item { get; set; } = "";
    public float Seconds { get; set; }
    /// <summary>Her urunde Yemlik'ten alinan yem (0: yalniz yem bulunmasi yeter).</summary>
    public int Food { get; set; }
    public int Amount { get; set; } = 1;
    /// <summary>Folluk'a (yoksa yere) gider; digerleri hayvanin ustunde birikir ve E ile alinir.</summary>
    public bool Nest { get; set; }
}

public sealed class WeightedItem
{
    public string Item { get; set; } = "";
    public float Weight { get; set; } = 1;
}

/// <summary>Iscinin ture ozgu isi (GDD 9.4): dig (eseler), compost (gubreden kompost), scout (harita acar, isaretler).</summary>
public sealed class AnimalWorkDef
{
    public string Kind { get; set; } = "";
    public float Seconds { get; set; }
    public List<WeightedItem> Yield { get; set; } = new();
    public string In { get; set; } = "";
    public string Out { get; set; } = "";
    public float Radius { get; set; }
}

public sealed class SpeciesDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Evcillestirme yolu (GDD 9.1): feed (yemle guven), subdue (can %25 alti, tut, sonra yem), egg (Kulucka'dan).</summary>
    public string Tame { get; set; } = "feed";
    /// <summary>Evi: pen (agil) ya da hive (Ari Kovani).</summary>
    public string Home { get; set; } = "pen";
    /// <summary>Sevdigi yem (ICERIK 3): guven ve Yemlik.</summary>
    public string Food { get; set; } = "";
    public int FoodToTrust { get; set; } = 1;
    /// <summary>Agilda kapladigi alan, m² (GDD 9.1).</summary>
    public float Area { get; set; }
    /// <summary>Kulucka'ya konan yumurta esyasi (tame = egg).</summary>
    public string Egg { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public List<AnimalProductDef> Products { get; set; } = new();
    public AnimalWorkDef? Work { get; set; }
    /// <summary>Yuk rolunde heybe yuvasi (GDD 9.4: 8-24; Dalli 16).</summary>
    public int Pack { get; set; }
    /// <summary>Vizviz: bu yaricapta capraz tozlasma (GDD 8.3: 12 m).</summary>
    public float PollinateRadius { get; set; }
    /// <summary>Yer tutucu govdenin rengi, boyu (m) ve yurume hizi (m/sn).</summary>
    public string Color { get; set; } = "#ffffff";
    public float Size { get; set; } = 1f;
    public float Speed { get; set; } = 1.5f;
}

public sealed partial class HusbandryFile
{
    public HappinessDef Happiness { get; set; } = new();
    public AnimalGenesDef Genes { get; set; } = new();
    public BreedingDef Breeding { get; set; } = new();
    public EscapeDef Escape { get; set; } = new();
    public ManureDef Manure { get; set; } = new();
    public SubdueDef Subdue { get; set; } = new();
    public TroughDef Trough { get; set; } = new();
    public IncubatorDef Incubator { get; set; } = new();
    /// <summary>Kasinma Fircasi bu yaricaptaki hayvanlara mutluluk verir, m.</summary>
    public float BrushRadius { get; set; } = 6;
    /// <summary>Okşama uzanimi, m.</summary>
    public float PetRange { get; set; } = 2.2f;
    public List<NamedId> Roles { get; set; } = new();
    public List<NamedId> Commands { get; set; } = new();
    public List<SpeciesDef> Species { get; set; } = new();

    public SpeciesDef? SpeciesOf(string id) => Species.FirstOrDefault(s => s.Id == id);
}

public sealed partial class GameData
{
    [DataFile("husbandry.json")]
    public HusbandryFile Husbandry { get; private set; } = new();
}
