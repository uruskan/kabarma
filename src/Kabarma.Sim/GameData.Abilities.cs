namespace Kabarma.Sim;

// Oyuncu gucleri, ultiler, Kadim Tas denemeleri, Maya Feneri ve Tatbikat: abilities.json (GDD 14.6, 14.7, 17, 17.1;
// ICERIK 7). Elektrik agi (power.json, Power*) ile karistirma.

/// <summary>Etkin guc ya da ulti. Kullanilmayan alan 0 kalir.</summary>
public sealed class AbilityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>active (Sarj ve bekleme) ya da ulti (Maya olceri).</summary>
    public string Kind { get; set; } = "active";
    public float Charge { get; set; }
    public float Cooldown { get; set; }
    public float Distance { get; set; }
    public float Duration { get; set; }
    /// <summary>Atilma: kullanimdan sonra hasar alinmayan sure.</summary>
    public float Invulnerable { get; set; }
    public float Radius { get; set; }
    public float Damage { get; set; }
    public float Knock { get; set; }
    public string DamageType { get; set; } = "";
    /// <summary>Yem Hologrami'nin cani.</summary>
    public float Hp { get; set; }
    /// <summary>Sifa Sisi: saniyede verilen can.</summary>
    public float Heal { get; set; }
    /// <summary>Suru Cagrisi: evcil hayvan yoksa kosan hayalet hayvan (YER TUTUCU).</summary>
    public int GhostAnimals { get; set; }
    /// <summary>Dev Mod: boy carpani.</summary>
    public float Scale { get; set; }
    /// <summary>Dev Mod: hiz carpani.</summary>
    public float Speed { get; set; }
    /// <summary>Dev Mod: alinan hasar carpani.</summary>
    public float DamageTaken { get; set; } = 1f;

    public bool IsUlti => Kind == "ulti";
}

public sealed class ChargeDef
{
    public float Max { get; set; } = 100f;
    public float RegenPerSecond { get; set; } = 5f;
    public float RegenDelay { get; set; } = 1.5f;
}

public sealed class UltiMeterDef
{
    public float Max { get; set; } = 100f;
    public float PerKill { get; set; }
    public float PerEliteKill { get; set; }
    /// <summary>Ata'nin caninin her %1'i icin.</summary>
    public float PerAtaPercent { get; set; }
}

/// <summary>Kadim Tas ve denemesi. Konumlar dunya metresi; halka, hedef ve gozcu konumlari tasa goreli.</summary>
public sealed class KadimTasDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Biome { get; set; } = "";
    public string Ability { get; set; } = "";
    /// <summary>parkur, koru, dalga, gizlen.</summary>
    public string Trial { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Seconds { get; set; }
    /// <summary>parkur: halkalar [dx, dz, yerden yukseklik], sirayla.</summary>
    public List<float[]> Rings { get; set; } = new();
    public float RingRadius { get; set; } = 2f;
    /// <summary>koru: korunan Gidak'in cani; her 'every' sn'de 'perWave' hayalet.</summary>
    public float GuardHp { get; set; }
    public float Every { get; set; }
    public int PerWave { get; set; }
    /// <summary>dalga: dalga basina hayalet sayisi.</summary>
    public List<int> Waves { get; set; } = new();
    /// <summary>dalga: ekipten biri yere duserse kayip (Sifa Sisi). Oteki denemelerde yere dusen deneyen kaybeder.</summary>
    public bool NoDown { get; set; }
    /// <summary>gizlen: hedef [dx, dz]; gozculer [dx, dz, bakis derecesi (0 = +Z, saat yonu)].</summary>
    public float[] Goal { get; set; } = new float[2];
    public List<float[]> Watchers { get; set; } = new();
    public float Sight { get; set; }
    public float Fov { get; set; }
    public float SeenLimit { get; set; }
    public float GoalRadius { get; set; } = 2f;
}

public sealed class GhostDef
{
    public float Hp { get; set; } = 30f;
    public float Damage { get; set; } = 6f;
    public float Speed { get; set; } = 3.6f;
    public float AttackSeconds { get; set; } = 1.3f;
    public float Reach { get; set; } = 1.1f;
}

public sealed class TrialDef
{
    public float RetrySeconds { get; set; } = 10f;
    public float SpawnRadius { get; set; } = 14f;
    /// <summary>Deneyen tastan bu kadar uzaklasirsa deneme biter.</summary>
    public float LeaveRadius { get; set; } = 40f;
    public GhostDef Ghost { get; set; } = new();
}

public sealed class FenerDef
{
    public string Id { get; set; } = "maya_feneri";
    public float Radius { get; set; } = 40f;
    public float Seconds { get; set; } = 240f;
    public float KsFactor { get; set; } = 0.5f;
    public int MaxCardLevels { get; set; } = 3;
    public int UsesPerDay { get; set; } = 1;
}

public sealed class TatbikatDef
{
    public bool RepairFree { get; set; } = true;
    public bool Rewards { get; set; }
    /// <summary>GECICI (kabarma3 dalga arayuzu gelene kadar): hayalet dalga sayisi, GDD 14.3 sayisinin carpani, dogma halkasi.</summary>
    public int Waves { get; set; } = 3;
    public float CountScale { get; set; } = 0.25f;
    public float SpawnMin { get; set; } = 22f;
    public float SpawnMax { get; set; } = 28f;
}

public sealed class AbilitiesFile
{
    public ChargeDef Charge { get; set; } = new();
    /// <summary>Acik etkin yuva (M3: 1; ikincisi Kurbaga Aga ile, M4).</summary>
    public int ActiveSlots { get; set; } = 1;
    public int ActiveSlotsMax { get; set; } = 2;
    public UltiMeterDef UltiMeter { get; set; } = new();
    public List<AbilityDef> Abilities { get; set; } = new();
    public List<KadimTasDef> Stones { get; set; } = new();
    public TrialDef Trial { get; set; } = new();
    public FenerDef Fener { get; set; } = new();
    public TatbikatDef Tatbikat { get; set; } = new();

    public AbilityDef? Def(string id) => Abilities.FirstOrDefault(a => a.Id == id);
    public KadimTasDef? Stone(string id) => Stones.FirstOrDefault(s => s.Id == id);
}

public sealed partial class GameData
{
    [DataFile("abilities.json")]
    public AbilitiesFile Abilities { get; private set; } = new();
}
