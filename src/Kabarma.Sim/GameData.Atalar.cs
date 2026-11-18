namespace Kabarma.Sim;

// Atalar (boss): atalar.json (GDD §18, §18.1; ICERIK §4.1). M2: Koca Pofuduk ve Boynuzlu Dede.
// Kural Ata.cs / AtaRules.cs; Godot katmani game/scripts/boss/.

/// <summary>Adagin bir bileseni. Swollen: kabarik urun (yuk nesnesi), Cekim'le sunaga birakilir; degilse canta esyasi.</summary>
public sealed class AtaOfferingDef
{
    public string Item { get; set; } = "";
    public int Count { get; set; }
    public bool Swollen { get; set; }
}

/// <summary>Arena: dunyadaki yeri. Vektorler [x, z]; Ocak (0,0,0). Sutunlar ve tas merkeze goredir.</summary>
public sealed class AtaArenaDef
{
    public List<float> Center { get; set; } = new();
    public float Radius { get; set; }
    /// <summary>Ata Tasi ve sunak: merkeze gore [x, z].</summary>
    public List<float> Stone { get; set; } = new();
    /// <summary>Sunaga bu kadar yakin oyuncu adak sunabilir (m).</summary>
    public float AltarReach { get; set; } = 3.5f;
    /// <summary>Tas sutunlar (merkeze gore [x, z]); Koca Pofuduk'un hucumu bunlara toslayinca sersemler.</summary>
    public List<List<float>> Pillars { get; set; } = new();
    public float PillarRadius { get; set; } = 1f;
    /// <summary>Oyuncu bu yaricapin disina cikarsa Ata merkeze doner (m).</summary>
    public float Leash { get; set; } = 30f;
}

/// <summary>Bir asama. Asama, can orani HpUntil'in altina inene dek surer (son asamada 0).</summary>
public sealed class AtaPhaseDef
{
    public string Name { get; set; } = "";
    public float HpUntil { get; set; }
    public float MoveSpeed { get; set; }
    /// <summary>Govdeye degme hasari ve aralik (sn).</summary>
    public float ContactDamage { get; set; }
    /// <summary>Saldiri turu: "charge" (hucum), "volley" (yumak/fidan atisi), "rootwave" (kok halkasi); bos = yok.</summary>
    public List<string> Attacks { get; set; } = new();
    public float AttackEvery { get; set; } = 4f;
    /// <summary>Saldiridan once sezdirme suresi (sn): oyuncu kacabilir.</summary>
    public float Telegraph { get; set; } = 0.9f;
    public float AttackDamage { get; set; }
    public float ChargeSpeed { get; set; }
    public float ChargeSeconds { get; set; } = 1.4f;
    /// <summary>Yumak / mermi: adet, hiz (m/s), oyuncuyu itme (m/s).</summary>
    public int VolleyCount { get; set; }
    /// <summary>Tek yumagin hasari (hucumun AttackDamage'indan ayri).</summary>
    public float VolleyDamage { get; set; }
    public float VolleySpeed { get; set; }
    public float Knockback { get; set; }
    /// <summary>Kok halkasi: yayilma hizi (m/s), en buyuk yaricap, kalinlik (m).</summary>
    public float WaveSpeed { get; set; }
    public float WaveRadius { get; set; }
    public float WaveWidth { get; set; } = 1.2f;
    /// <summary>Yardimci yaratik: her AddEvery sn'de AddCount tane, ayni anda en cok AddMax.</summary>
    public int AddCount { get; set; }
    public float AddEvery { get; set; }
    public int AddMax { get; set; }
    /// <summary>Asama basinda zirh (0..1: silah hasarindan dusen pay). Bicme vurusu ShearPerHit, yanik saniyede BurnPerSecond dusurur.</summary>
    public float Armor { get; set; }
    public float ShearPerHit { get; set; }
    public float BurnPerSecond { get; set; }
    /// <summary>Silah hasari carpani (Dede asama 3: boynuz parcalari asil hasar).</summary>
    public float WeaponMult { get; set; } = 1f;
    /// <summary>Zayif nokta (kovan) sayisi ve cani; kirilan kovan StunSeconds sersemletir.</summary>
    public int WeakPoints { get; set; }
    public float WeakPointHp { get; set; }
    /// <summary>Sutuna toslayinca ya da kovan kirilinca sersemleme suresi ve o surede alinan hasar carpani.</summary>
    public float StunSeconds { get; set; }
    public float StunMult { get; set; } = 1f;
    /// <summary>Firlatilan boynuz parcasi: dusen parca sayisi ve isabet hasari.</summary>
    public int Shards { get; set; }
    public float ShardDamage { get; set; }
    /// <summary>Sis yogunlugu carpani (Dede asama 1); 1 = degismez.</summary>
    public float Fog { get; set; } = 1f;
}

public sealed class AtaRewardItemDef
{
    public string Item { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Odul (GDD §18): Cekirdek (Ocak basamagi), Muhur (kalici ekip bonusu), ulti, ganimet; acilanlar (GDD §12).</summary>
public sealed class AtaRewardDef
{
    public List<AtaRewardItemDef> Items { get; set; } = new();
    public string Seal { get; set; } = "";
    public string Ulti { get; set; } = "";
    /// <summary>Acilan icerik kimlikleri ("biome:sis_korusu", "recipe:capa_mk2" ...); ilgili akislar Sim'den sorar.</summary>
    public List<string> Unlocks { get; set; } = new();
}

/// <summary>Kalici ekip bonusu: Effect anahtari kurallarda carpan olarak okunur (AtaProgress.SealMult).</summary>
public sealed class SealDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Effect { get; set; } = "";
    public float Mult { get; set; } = 1f;
}

/// <summary>Yardimci yaratik (yer tutucu: gercegi yaratik2 akisinda gelince Creature alanina tur kimligi yazilir).</summary>
public sealed class AtaAddDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Hp { get; set; }
    public float Speed { get; set; }
    public float Damage { get; set; }
    public float Scale { get; set; } = 1f;
    public string Look { get; set; } = "";
    public string Placeholder { get; set; } = "";
}

public sealed class AtaDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Biome { get; set; } = "";
    /// <summary>URETIM §3 yer tutucu isareti (neden). Bos = gecti.</summary>
    public string Placeholder { get; set; } = "";
    public AtaArenaDef Arena { get; set; } = new();
    public string OfferingName { get; set; } = "";
    public List<AtaOfferingDef> Offering { get; set; } = new();
    public float Hp { get; set; }
    /// <summary>Govde: carpisma yaricapi, boy (m), gorunus olcegi.</summary>
    public float Radius { get; set; }
    public float Height { get; set; }
    public float Scale { get; set; } = 1f;
    public string Add { get; set; } = "";
    public List<AtaPhaseDef> Phases { get; set; } = new();
    public AtaRewardDef Reward { get; set; } = new();
}

public sealed class AtalarFile
{
    /// <summary>GDD §18: Ata cani ekip boyuna gore 1 + TeamHpPerExtra × (n − 1).</summary>
    public float TeamHpPerExtra { get; set; } = 0.5f;
    /// <summary>Asama gecisinde hasar almama suresi (sn).</summary>
    public float PhaseShiftSeconds { get; set; } = 1.5f;
    /// <summary>Ata'nin inisi (sn): bu surede hasar almaz, saldirmaz.</summary>
    public float ArriveSeconds { get; set; } = 2.5f;
    public List<AtaDef> Atalar { get; set; } = new();
    public List<AtaAddDef> Adds { get; set; } = new();
    public List<SealDef> Seals { get; set; } = new();

    public AtaDef Get(string id) => Atalar.FirstOrDefault(a => a.Id == id) ?? throw new KeyNotFoundException($"atalar.json: '{id}' yok");
    public AtaAddDef? AddOf(string id) => Adds.FirstOrDefault(a => a.Id == id);
    public SealDef? SealOf(string id) => Seals.FirstOrDefault(s => s.Id == id);
}

public sealed partial class GameData
{
    [DataFile("atalar.json")]
    public AtalarFile Atalar { get; private set; } = new();
}
