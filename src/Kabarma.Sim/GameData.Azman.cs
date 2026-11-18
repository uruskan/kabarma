namespace Kabarma.Sim;

// Azmanlar (ICERIK §4.2): azman.json. Kural Azman.cs / AzmanRules.cs; Godot katmani game/scripts/combat/Azman*.cs.

/// <summary>Uzun hucum (Azman Homur): sur yikar, oyuncuyu savurur; duvara toslayinca sersemler.</summary>
public sealed class AzmanChargeDef
{
    public float Speed { get; set; }
    public float Seconds { get; set; }
    public float Damage { get; set; }
    public float Knockback { get; set; }
    /// <summary>Degdigi her yapi parcasina hasar ve arama yaricapi (m).</summary>
    public float StructureDamage { get; set; }
    public float StructureRadius { get; set; } = 2f;
    /// <summary>Yapiya toslayinca sersemleme (sn).</summary>
    public float WallStun { get; set; }
}

/// <summary>Sirttaki kucuk Homurlar: can orani At esiklerinin altina indikce Count / At.Count kadar duser.</summary>
public sealed class AzmanRidersDef
{
    public int Count { get; set; }
    public List<float> At { get; set; } = new();
    public string Creature { get; set; } = "";
    public float Scale { get; set; } = 1f;
    public float Hp { get; set; }
    public float Damage { get; set; }
    public float Speed { get; set; }
}

/// <summary>Agac sokup firlatma (Azman Bozuk).</summary>
public sealed class AzmanUprootDef
{
    /// <summary>Bu yaricapta agac arar; yoksa yerden kaya koparir.</summary>
    public float TreeReach { get; set; }
    /// <summary>Sokme suresi (sn).</summary>
    public float Pull { get; set; }
    public float ThrowSpeed { get; set; }
    public float Damage { get; set; }
    public float Radius { get; set; }
    public float Knockback { get; set; }
    public float StructureDamage { get; set; }
    /// <summary>Yerde kalan kutuk Cekim'le Azman'a firlatilinca hasar (Darbe, zirhsiz) ve cozulme suresi.</summary>
    public float LogDamage { get; set; }
    public float LogSeconds { get; set; }
}

public sealed class AzmanRewardDef
{
    public float Oz { get; set; }
    public List<AtaRewardItemDef> Items { get; set; } = new();
}

public sealed class AzmanDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Biome { get; set; } = "";
    /// <summary>Govdenin geldigi yabani tur (creatures.json): model, iskelet, profil.</summary>
    public string Base { get; set; } = "";
    /// <summary>URETIM §3 yer tutucu isareti.</summary>
    public string Placeholder { get; set; } = "";
    public float Scale { get; set; } = 1f;
    public float Radius { get; set; }
    public float Height { get; set; }
    public float Hp { get; set; }
    public float Armor { get; set; }
    public float Mass { get; set; } = 20f;
    public float MoveSpeed { get; set; }
    public float ContactDamage { get; set; }
    public float ContactEvery { get; set; } = 1.2f;
    /// <summary>Saldiri sirasi: "charge", "uproot".</summary>
    public List<string> Attacks { get; set; } = new();
    public float AttackEvery { get; set; }
    /// <summary>Inisten sonra ilk saldiriya kadar (sn): Azman gelir gelmez hucum eder.</summary>
    public float FirstAttack { get; set; } = 2f;
    public float Telegraph { get; set; }
    public AzmanChargeDef? Charge { get; set; }
    public AzmanRidersDef? Riders { get; set; }
    public AzmanUprootDef? Uproot { get; set; }
    public AzmanRewardDef Reward { get; set; } = new();
}

public sealed class AzmanFile
{
    /// <summary>Her Every. KS'nin Doruk asamasi (ICERIK §4.2).</summary>
    public int Every { get; set; } = 5;
    public float HpPerKs { get; set; }
    public float TeamHpPerExtra { get; set; } = 0.5f;
    public float ArriveSeconds { get; set; } = 2f;
    public List<AzmanDef> Azmanlar { get; set; } = new();

    public AzmanDef Get(string id) => Azmanlar.FirstOrDefault(a => a.Id == id) ?? throw new KeyNotFoundException($"azman.json: '{id}' yok");
    public AzmanDef? ForBiome(string biome) => Azmanlar.FirstOrDefault(a => a.Biome == biome);
}

public sealed partial class GameData
{
    [DataFile("azman.json")]
    public AzmanFile Azman { get; private set; } = new();
}
