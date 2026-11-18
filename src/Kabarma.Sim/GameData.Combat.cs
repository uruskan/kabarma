namespace Kabarma.Sim;

// Savas (GDD §16.1): combat.json. Kural Combat.cs / Status.cs.

public sealed class DamageTypeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    /// <summary>Bu turun isabeti ekledigi durum ("burn", "freeze", "shock", "poison", "swell"; bos = yok).</summary>
    public string Status { get; set; } = "";
}

public sealed class BurnDef
{
    public float Seconds { get; set; } = 3f;
    /// <summary>Saniyedeki yanik hasari = isabet hasari × DpsOfHit (GDD: %20).</summary>
    public float DpsOfHit { get; set; } = 0.2f;
    public float MinDps { get; set; } = 1f;
    /// <summary>Yanik her SpreadEvery sn'de SpreadRadius icindeki SpreadTargets hedefe sicrar.</summary>
    public float SpreadEvery { get; set; } = 1f;
    public float SpreadRadius { get; set; } = 2.5f;
    public int SpreadTargets { get; set; } = 1;
}

public sealed class FreezeDef
{
    public float Seconds { get; set; } = 2.5f;
    /// <summary>Hiz kaybi (GDD: %40).</summary>
    public float Slow { get; set; } = 0.4f;
    /// <summary>Bu kadar yigin kilitler (LockSeconds boyunca hareket ve saldiri yok).</summary>
    public int LockStacks { get; set; } = 3;
    public float LockSeconds { get; set; } = 2f;
}

public sealed class ShockDef
{
    public float StunSeconds { get; set; } = 0.4f;
    public float ChainRadius { get; set; } = 4.5f;
    public int ChainTargets { get; set; } = 2;
    /// <summary>Sicrayan hasar = isabet × ChainFalloff; sicrayan isabet yeniden sicramaz.</summary>
    public float ChainFalloff { get; set; } = 0.6f;
}

public sealed class PoisonDef
{
    public float Seconds { get; set; } = 8f;
    public float DpsOfHit { get; set; } = 0.08f;
    public float MinDps { get; set; } = 0.5f;
    /// <summary>Zehir yigilir (saniyelik hasar toplanir), en cok MaxDps.</summary>
    public float MaxDps { get; set; } = 40f;
}

public sealed class SwellRuleDef
{
    /// <summary>Kabarik artisi = Maya hasari / azami can × PerMaxHp.</summary>
    public float PerMaxHp { get; set; } = 3f;
    /// <summary>Patlama esigi: eklenen boy (1 = %200 boy).</summary>
    public float BurstAt { get; set; } = 1f;
    public float BurstRadius { get; set; } = 3.5f;
    /// <summary>Patlamanin cevreye Darbe hasari ve hedefin kendine hasari: azami canin payi.</summary>
    public float BurstOfMaxHp { get; set; } = 0.5f;
    public float SelfOfMaxHp { get; set; } = 0.5f;
}

public sealed class KnockDef
{
    public float MinMass { get; set; } = 0.5f;
    public float MaxSpeed { get; set; } = 18f;
}

public sealed class CombatFile
{
    /// <summary>GDD §16.1: azaltma = zirh / (zirh + ArmorK).</summary>
    public float ArmorK { get; set; } = 100f;
    public float WeakPointMult { get; set; } = 2f;
    public bool FriendlyFire { get; set; }
    /// <summary>Surekli hasarin (yanik, zehir) islenme araligi (sn).</summary>
    public float TickSeconds { get; set; } = 0.5f;
    public List<DamageTypeDef> Types { get; set; } = new();
    public BurnDef Burn { get; set; } = new();
    public FreezeDef Freeze { get; set; } = new();
    public ShockDef Shock { get; set; } = new();
    public PoisonDef Poison { get; set; } = new();
    public SwellRuleDef Swell { get; set; } = new();
    public KnockDef Knock { get; set; } = new();
    /// <summary>Capa basligi kimligi -> hasar turu kimligi.</summary>
    public Dictionary<string, string> WeaponTypes { get; set; } = new();
    /// <summary>Profil (yaratik/Ata/Azman kimligi) -> hasar turu kimligi -> carpan.</summary>
    public Dictionary<string, Dictionary<string, float>> Profiles { get; set; } = new();

    /// <summary>Profilin bu tura carpani (yazilmayan 1).</summary>
    public float Mult(string profile, DamageType type) =>
        !string.IsNullOrEmpty(profile) && Profiles.TryGetValue(profile, out var p) && p.TryGetValue(Combat.Id(type), out var m) ? m : 1f;

    /// <summary>Capa basliginin hasar turu (yazilmayan Darbe).</summary>
    public DamageType WeaponType(string weaponId) =>
        WeaponTypes.TryGetValue(weaponId, out var t) ? Combat.Parse(t) : DamageType.Darbe;
}

public sealed partial class GameData
{
    [DataFile("combat.json")]
    public CombatFile Combat { get; private set; } = new();
}
