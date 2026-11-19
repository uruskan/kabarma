namespace Kabarma.Sim;

// holding.json: Bereket Holding (GDD §19, §19.4.1; ICERIK §3.7). holding akisi (M3).

public sealed class HoldingCompositionDef
{
    public int GuardsBase { get; set; } = 2;
    public float GuardsPerLevel { get; set; } = 1;
    public int WorkersBase { get; set; } = 2;
    public float WorkersPerLevel { get; set; } = 0.5f;
    public float TowersPerLevel { get; set; } = 0.5f;
    public float WallsPerLevel { get; set; } = 1f / 3f;
}

public sealed class HoldingStructureDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Hp { get; set; }
    public float HpPerLevel { get; set; }
    /// <summary>Kamp merkezine gore [x, z].</summary>
    public List<float> Offset { get; set; } = new();
    /// <summary>Sube Cekirdegi: oteki yapilar yikilmadan vurulamaz.</summary>
    public bool Core { get; set; }
}

public sealed class HoldingCampDef
{
    public List<float> Anchor { get; set; } = new();
    public float MinDistance { get; set; } = 250;
    public float PerdeRadius { get; set; } = 26;
    public float PerdePush { get; set; } = 7;
    public int LevelEveryDays { get; set; } = 5;
    public int CapPerOcakTier { get; set; } = 2;
    public int MaxLevel { get; set; } = 10;
    public int FirstRealNight { get; set; } = 2;
    public int RebuildDays { get; set; } = 3;
    public float VeinRadius { get; set; } = 150;
    public int VeinHitsPerWorkerDay { get; set; } = 2;
    /// <summary>Gunduz kampa vurulunca bekcilerin ofkesi (sn).</summary>
    public float ProvokeSeconds { get; set; } = 30;
    public HoldingCompositionDef Composition { get; set; } = new();
    public List<HoldingStructureDef> Structures { get; set; } = new();
}

public sealed class HoldingUnitDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>worker, guard, elite, mechanic, boss.</summary>
    public string Role { get; set; } = "";
    public float Hp { get; set; }
    public float Speed { get; set; }
    public float Damage { get; set; }
    public float Range { get; set; }
    public float FireSeconds { get; set; }
    public float AggroRange { get; set; }
    /// <summary>Hasar azaltma orani (0-1).</summary>
    public float Armor { get; set; }
    public bool Flees { get; set; }
    public string Weapon { get; set; } = "";
    public string Tint { get; set; } = "";
    /// <summary>Oynadigi kilometre tasi (bos: M3). Mudur M5.</summary>
    public string Milestone { get; set; } = "";
}

public sealed class HoldingCaravanDef
{
    public float DepartTime { get; set; } = 0.27f;
    public List<float> Depot { get; set; } = new();
    public float Speed { get; set; } = 3.4f;
    public float WaitSeconds { get; set; } = 20;
    public int GuardsBase { get; set; } = 2;
    public int GuardsMax { get; set; } = 4;
    public float VehicleHp { get; set; } = 300;
    public Dictionary<string, int> Cargo { get; set; } = new();
    public int CreditsPerLevel { get; set; } = 120;
}

public sealed class HoldingRaidDef
{
    public float Threshold { get; set; } = 50;
    public int BaseCount { get; set; } = 2;
    public int TruckFromLevel { get; set; } = 5;
    public int Carriers { get; set; } = 2;
    public float CarrySpeedMult { get; set; } = 0.5f;
    public float EntryOffset { get; set; } = 8;
    /// <summary>Tapu yaricapi: baskin ekibi Ocak'a bu kadar yaklasinca 'girdi' sayilir (m).</summary>
    public float ClaimRadius { get; set; } = 30;
    /// <summary>Baskin ekibinin Ocak'tan, kamp yonunde dogdugu uzaklik (m).</summary>
    public float SpawnDistance { get; set; } = 70;
}

public sealed class HoldingHusumetDef
{
    public float Start { get; set; }
    public float Max { get; set; } = 100;
    public float Daily { get; set; } = 2;
    public float ExportPerThousand { get; set; } = 1;
    public float ExportMax { get; set; } = 3;
    public float Ambush { get; set; } = 15;
    public float NightRaid { get; set; } = 25;
    public float TheyRaided { get; set; } = -40;
}

public sealed class HoldingLootDef
{
    /// <summary>Kredi (× kamp seviyesi).</summary>
    public int Credits { get; set; }
    public Dictionary<string, int> Items { get; set; } = new();
    /// <summary>Sistemi olmayan odul: Kooperatif deposunda sayilir.</summary>
    public string Token { get; set; } = "";
}

/// <summary>
/// Kamp yeri (GDD §19.1): Slot 1 ilk kamp (camp.anchor, Kabarma ile), 2. kamp Ocak T3, 3. kamp T4, Genel Merkez T5 (kind "hq").
/// Biome: kamp bu biyomun icinde olmali; Anchor bossa biyomun tabanindan ve agirligindan cozulur (HoldingSites).
/// </summary>
public sealed class HoldingSiteDef
{
    public int Slot { get; set; }
    /// <summary>"camp" ya da "hq" (Genel Merkez).</summary>
    public string Kind { get; set; } = "camp";
    public string Biome { get; set; } = "";
    public int OcakTier { get; set; } = 1;
    public List<float> Anchor { get; set; } = new();
    /// <summary>Kervanin teslim noktasi, kampa gore [x, z]; bos ise caravan.depot.</summary>
    public List<float> DepotOffset { get; set; } = new();
    /// <summary>Sabit seviye (Genel Merkez 10); 0 = 1. seviyeden baslar.</summary>
    public int Level { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Muduru'n zirhli kamyonu (avlu, 1. asama): govde zirhli, arkadaki motor kapagi zayif nokta; Mekanikler onarir.</summary>
public sealed class MudurTruckDef
{
    public float Hp { get; set; } = 1800;
    /// <summary>Govdeye (kasa) gelen hasarin dusen orani; motor kapagi zirhsiz ve MotorMult kat.</summary>
    public float BodyArmor { get; set; } = 0.7f;
    public float MotorMult { get; set; } = 3f;
    public int Mechanics { get; set; } = 2;
    public float RepairPerMechanic { get; set; } = 15f;
    public float CannonDamage { get; set; } = 9;
    public float CannonEvery { get; set; } = 1.5f;
    public float Speed { get; set; } = 6;
    public float RamDamage { get; set; } = 28;
}

/// <summary>Muduru'n yaya hali (yonetim kati, 2. asama) ve kalan can; 3. asamada zeplin.</summary>
public sealed class MudurBossDef
{
    public float Hp { get; set; } = 2200;
    /// <summary>Can orani bunun altina inince cati asamasi (zeplin) baslar.</summary>
    public float RoofAt { get; set; } = 0.4f;
    public int Generators { get; set; } = 4;
    public float GeneratorHp { get; set; } = 160;
    public int Speakers { get; set; } = 2;
    public float SpeakerHp { get; set; } = 90;
    public float CallEvery { get; set; } = 14;
    public int CallCount { get; set; } = 2;
    /// <summary>Hoparlor calisirken Holding birimlerinin hiz carpani.</summary>
    public float SpeechSpeedMult { get; set; } = 1.4f;
    public float MeleeDamage { get; set; } = 18;
    public float FireDamage { get; set; } = 10;
    public float Speed { get; set; } = 3.4f;
}

public sealed class MudurRoofDef
{
    public int Balloons { get; set; } = 6;
    public float BalloonHp { get; set; } = 70;
    /// <summary>Balonlarin yerden yuksekligi (m): ayakta vurulur, zipkinla tutunmak gerekmez (M4 sadeligi).</summary>
    public float Altitude { get; set; } = 7;
}

public sealed class MudurRewardDef
{
    public Dictionary<string, int> Items { get; set; } = new();
    /// <summary>Acilanlar: "head:yercekimi", "plan:efsane_cip", "cosmetic:kask_holding_muduru", "cosmetic:kaplama_holding".</summary>
    public List<string> Unlocks { get; set; } = new();
    public int Credits { get; set; }
}

/// <summary>Genel Merkez ve Mudur (ICERIK §4.4, GDD §19.5).</summary>
public sealed class HoldingMudurDef
{
    public float TeamHpPerExtra { get; set; } = 0.5f;
    public MudurTruckDef Truck { get; set; } = new();
    public MudurBossDef Boss { get; set; } = new();
    public MudurRoofDef Roof { get; set; } = new();
    public MudurRewardDef Reward { get; set; } = new();
    /// <summary>Avlu yaricapi (m): kamyon bu halkada doner.</summary>
    public float YardRadius { get; set; } = 20;
}

public sealed class HoldingFile
{
    public List<HoldingSiteDef> Sites { get; set; } = new();
    public HoldingMudurDef Mudur { get; set; } = new();
    public HoldingCampDef Camp { get; set; } = new();
    public List<HoldingUnitDef> Units { get; set; } = new();
    public HoldingCaravanDef Caravan { get; set; } = new();
    public HoldingRaidDef Raid { get; set; } = new();
    public HoldingHusumetDef Husumet { get; set; } = new();
    public Dictionary<string, HoldingLootDef> Loot { get; set; } = new();

    public HoldingUnitDef Unit(string id) => Units.First(u => u.Id == id);
    public HoldingSiteDef? Site(int slot) => Sites.FirstOrDefault(s => s.Slot == slot);
}

public sealed partial class GameData
{
    [DataFile("holding.json")]
    public HoldingFile Holding { get; private set; } = new();
}
