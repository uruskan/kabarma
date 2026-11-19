namespace Kabarma.Sim;

// Araclar ve eklentiler (ICERIK §14, GDD §20): vehicles.json. M2: Cekirge; M4 arac akisi: Traktor, Kamyon, Bicerdover,
// Hava Yastikli, Zeplin ve 10 eklenti. Kural Vehicle.cs / Vehicle.Attach.cs (durumsuz, Sim'de).

public sealed class VehicleDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public int Seats { get; set; } = 1;
    public int Slots { get; set; }
    /// <summary>"ground" (tekerlekli), "hover" (hava yastikli: su, bataklik, kar ve kumda gider), "air" (ucar).</summary>
    public string Kind { get; set; } = "ground";
    /// <summary>Ileri en yuksek hiz, geri hiz (m/sn), ivme ve fren (m/sn²).</summary>
    public float MaxSpeed { get; set; }
    public float ReverseSpeed { get; set; }
    public float Accel { get; set; }
    public float Brake { get; set; }
    /// <summary>Surtunme yavaslamasi (gaz yokken, m/sn²).</summary>
    public float Coast { get; set; } = 2f;
    /// <summary>Donus hizi (rad/sn) TurnAtSpeed m/sn'de; daha yavasta orantili azalir.</summary>
    public float TurnRate { get; set; }
    public float TurnAtSpeed { get; set; } = 4f;
    /// <summary>Birincil yakit esyasi ve depo: TankUnits birim, birim basina gazda gecen sure (sn). GDD §20: dolu depo ~1 saat.</summary>
    public string FuelItem { get; set; } = "";
    public int TankUnits { get; set; }
    public float UnitSeconds { get; set; }
    /// <summary>Kabul edilen yakitlar ve verim carpani (ICERIK §14 "Yakit": Bitkisel Yag, Biyoyakit iki kat, aku). Bos: yalniz FuelItem.</summary>
    public Dictionary<string, float> Fuels { get; set; } = new();
    /// <summary>Bu egimden dik yokusta FlipSpeed'ten hizli gidilirse devrilir (derece, m/sn). Hava araci devrilmez.</summary>
    public float FlipSlopeDeg { get; set; } = 35f;
    public float FlipSpeed { get; set; } = 3f;
    public float Hp { get; set; } = 100f;
    /// <summary>Zemin turune gore hiz carpani ("water", "swamp", "snow", "sand"); yoksa 1.</summary>
    public Dictionary<string, float> Surface { get; set; } = new();
    /// <summary>Eklenti yuvalari: cekme demiri ("hitch") ve govde ("body").</summary>
    public int Hitches { get; set; }
    public int BodyMounts { get; set; }
    /// <summary>Kasa (kargo kasasi) tasima kapasitesi; Zeplin icin altina asilan kasa sayisi.</summary>
    public int CargoCrates { get; set; }
    /// <summary>Hava araci: en cok yukseklik (m), tirmanis hizi (m/sn), yerden havalandigi yukseklik (m).</summary>
    public float MaxAltitude { get; set; }
    public float ClimbRate { get; set; }
    public float TakeoffAltitude { get; set; } = 1.5f;
    /// <summary>Yerdeyken (havalanmadan) en yuksek hiz (hava araci).</summary>
    public float TaxiSpeed { get; set; } = 2f;
    /// <summary>Bicerdover: oniindeki bu yaricapta ekini biçer (m) ve saniyede biçme sayisi.</summary>
    public float HarvestRadius { get; set; }
    public float HarvestPerSecond { get; set; }
    /// <summary>Uretim istasyonu ("arac_atolyesi" ya da "zeplin_hangari") ve tarif (recipes.json arac satirlariyla ayni).</summary>
    public string Station { get; set; } = "arac_atolyesi";
    public Dictionary<string, int> Recipe { get; set; } = new();
    public string Placeholder { get; set; } = "";
}

/// <summary>Eklenti (ICERIK §14): "hitch" (cekme demiri) ya da "body" (govde) yuvasina takilir; Params sayilari etkiyi verir.</summary>
public sealed class AttachmentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    /// <summary>"hitch" ya da "body".</summary>
    public string Mount { get; set; } = "body";
    /// <summary>Takilabilecek araclar; "*" hepsi.</summary>
    public List<string> Fits { get; set; } = new();
    /// <summary>Etki sayilari: crates, speedMult, armorHp, ramDamage, till, sow, water, waterUnits, width, lift, reach, scoop, gunnerSeat, light, glide.</summary>
    public Dictionary<string, float> Params { get; set; } = new();
    public Dictionary<string, int> Recipe { get; set; } = new();
    public string Station { get; set; } = "arac_atolyesi";
    public string Placeholder { get; set; } = "";
}

public sealed class VehiclesFile
{
    public List<VehicleDef> Vehicles { get; set; } = new();
    public List<AttachmentDef> Attachments { get; set; } = new();
    /// <summary>Genel sayilar (ortak oyunda arac girdisi gonderme sikligi, dusme hasari).</summary>
    public VehicleRulesDef Rules { get; set; } = new();

    public AttachmentDef? Attachment(string id) => Attachments.FirstOrDefault(a => a.Id == id);
}

public sealed class VehicleRulesDef
{
    /// <summary>Bundan hizli (m/sn) inen arac hasar alir; asan her m/sn icin DamagePerSpeed can.</summary>
    public float FallDamageSpeed { get; set; } = 9f;
    public float DamagePerSpeed { get; set; } = 8f;
    /// <summary>Ortak oyun: istemci surucu girdisini saniyede bu kadar gonderir.</summary>
    public float NetInputHz { get; set; } = 15f;
    /// <summary>Yakit doldurma ve binme mesafesi (m).</summary>
    public float ReachMeters { get; set; } = 2.6f;
    /// <summary>Zeplin Hangari: bu yaricapta inen zeplin parkli sayilir (m).</summary>
    public float HangarParkRadius { get; set; } = 14f;
    /// <summary>Hasarsiz carpma hizi (m/sn): Tampon'suz araclar yaratiga carparsa itme; Tampon'lu hasar verir.</summary>
    public float RamMinSpeed { get; set; } = 4f;
}

public sealed partial class GameData
{
    [DataFile("vehicles.json")]
    public VehiclesFile Vehicles { get; private set; } = new();

    public VehicleDef Vehicle(string id) => Vehicles.Vehicles.First(v => v.Id == id);
    public AttachmentDef? Attachment(string id) => Vehicles.Attachments.FirstOrDefault(a => a.Id == id);
}
