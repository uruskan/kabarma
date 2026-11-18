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

public sealed class HoldingFile
{
    public HoldingCampDef Camp { get; set; } = new();
    public List<HoldingUnitDef> Units { get; set; } = new();
    public HoldingCaravanDef Caravan { get; set; } = new();
    public HoldingRaidDef Raid { get; set; } = new();
    public HoldingHusumetDef Husumet { get; set; } = new();
    public Dictionary<string, HoldingLootDef> Loot { get; set; } = new();

    public HoldingUnitDef Unit(string id) => Units.First(u => u.Id == id);
}

public sealed partial class GameData
{
    [DataFile("holding.json")]
    public HoldingFile Holding { get; private set; } = new();
}
