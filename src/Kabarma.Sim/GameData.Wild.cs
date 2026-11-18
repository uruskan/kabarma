namespace Kabarma.Sim;

// Yabani yaratiklarin gunduz tavri (GDD §16.2 "Gunduz tavri", ICERIK §3.8), sururler (TEKNIK §6 "Sururler"),
// koddan govdeler (bodies.json; Tripo modeli gelene kadar isaretli yer tutucu, URETIM §2.2 E yedegi) ve
// biyom dogma satirlari dunya2'nin semasinda (biomes.json spawns.creatures, GameData.Explore.cs CreatureSpawnDef).

/// <summary>Tavir: ICERIK §3.8. shy urkek, calm sakin, raider yagmaci, predator yirtici, nocturnal gececi, swarm suru.</summary>
public static class WildStance
{
    public const string Shy = "shy", Calm = "calm", Raider = "raider", Predator = "predator", Nocturnal = "nocturnal", Swarm = "swarm";
    public static readonly string[] All = { Shy, Calm, Raider, Predator, Nocturnal, Swarm };
}

/// <summary>Bir turun yabani davranisi (creatures.json "wild"). Mesafeler m, sureler sn, hizlar turun Speed'inin carpani.</summary>
public sealed class WildDef
{
    public string Stance { get; set; } = WildStance.Calm;
    /// <summary>"day", "night" ya da "always": disarida oldugu saatler. Disinda uyur ya da sahneden cekilir.</summary>
    public string Active { get; set; } = "day";
    public float WanderRadius { get; set; } = 6f;
    public float WalkSpeedMult { get; set; } = 0.5f;
    public float RunSpeedMult { get; set; } = 1f;
    /// <summary>Oyuncu bu kadar yakinsa basini kaldirip bakar (ürkek), uyarir (yirtici).</summary>
    public float AlertRadius { get; set; }
    /// <summary>Oyuncu bu kadar yakinsa kacar (0: yaklasinca kacmaz).</summary>
    public float FleeRadius { get; set; }
    public float FleeSeconds { get; set; } = 3f;
    /// <summary>Vurulunca (ya da yakinda ates edilince) kacar mi.</summary>
    public bool FleeOnHit { get; set; } = true;
    /// <summary>Hedef: "" yok, "crop" olgun ekin, "grain" olgun tahil, "seed" ekili tohum, "coop" kumes/yumurta, "flower" cicek.</summary>
    public string Target { get; set; } = "";
    public float TargetSense { get; set; }
    public float TargetStop { get; set; } = 1.2f;
    /// <summary>Hedefin yaninda kalma suresi; dolunca olay bildirilir (calma/yeme) ve kacar.</summary>
    public float TargetSeconds { get; set; } = 6f;
    /// <summary>Yirtici: hucum yaricapi, vurus menzili, vurus araligi, gunduz vurus hasari (M2: kucuk), uyari suresi, kovalamanin birakildigi uzaklik.</summary>
    public float ChargeRadius { get; set; }
    public float AttackRange { get; set; } = 1.6f;
    public float AttackInterval { get; set; } = 1.6f;
    public float AttackDamage { get; set; }
    public float WarnSeconds { get; set; } = 1.2f;
    public float GiveUpRadius { get; set; } = 22f;
    public float PauseMin { get; set; } = 2f;
    public float PauseMax { get; set; } = 5f;
    public float IdleShare { get; set; } = 0.4f;
    public float GrazeShare { get; set; } = 0.4f;
    public float RetargetMin { get; set; } = 4f;
    public float RetargetMax { get; set; } = 9f;
    /// <summary>Gececi turun gunduz uyudugu yer ("tree", "bush"); gorunus icin.</summary>
    public string Roost { get; set; } = "";
    /// <summary>Arkasinda iz birakir (Vicik): izin omru (sn), 0 yok.</summary>
    public float TrailSeconds { get; set; }
    /// <summary>Uyanikken yururken yerden yukseklik (Hu suzulur), m; 0 yerde.</summary>
    public float FlyHeight { get; set; }
    /// <summary>Gunduz sahnesinde kabarmis olabilir mi (gorunus sinamasi disinda hayir; Kabarma gecesi dalga olarak gelir).</summary>
    public bool DaySwell { get; set; }
}

/// <summary>Suru turu (TEKNIK §6: merkez, yaricap, uye sayisi, can havuzu, tohum). Uyeler tohumdan hesaplanir, fizik yok.</summary>
public sealed class SwarmDef
{
    public int Members { get; set; } = 40;
    public float Radius { get; set; } = 2.5f;
    /// <summary>Uyenin boyu (m) ve yerden ortalama yuksekligi.</summary>
    public float MemberSize { get; set; } = 0.12f;
    public float Height { get; set; } = 0.6f;
    /// <summary>Uyelerin merkez etrafinda donme hizi (tur/sn) ve dalgalanma genligi (m).</summary>
    public float Orbit { get; set; } = 0.3f;
    public float Bob { get; set; } = 0.15f;
    public float HpPool { get; set; } = 80f;
    /// <summary>Vurulunca dagilma suresi ve dagilmis yaricap carpani.</summary>
    public float ScatterSeconds { get; set; } = 3f;
    public float ScatterMult { get; set; } = 3f;
    /// <summary>Kabarmis hali: uye carpani, yaricap carpani (ICERIK §3.1 Bulut: dev suru).</summary>
    public float SwollenMembers { get; set; } = 2f;
    public float SwollenRadius { get; set; } = 2.2f;
    /// <summary>Kovanli suru (Vizviz): kovan doğma yerinde durur, suru kovan cevresinde gezer.</summary>
    public bool Hive { get; set; }
    public float HiveRange { get; set; } = 10f;
    /// <summary>Kovana bu kadar yaklasana sokar (gunduz, M2: kucuk hasar), hasar ve aralik.</summary>
    public float GuardRadius { get; set; }
    public float StingDamage { get; set; }
    public float StingInterval { get; set; } = 1.5f;
}

/// <summary>Koddan govde (bodies.json): kemikler (model uzayinda, dinlenme donusu yok) ve kemige bagli kati parcalar.</summary>
public sealed class BodyBoneDef
{
    public string Name { get; set; } = "";
    public string Parent { get; set; } = "";
    /// <summary>[x, y, z] model uzayinda (m); +X sol, +Y yukari, +Z ileri.</summary>
    public List<float> Pos { get; set; } = new();
}

public sealed class BodyPartDef
{
    public string Bone { get; set; } = "";
    /// <summary>sphere, pillow, capsule, cylinder, cone.</summary>
    public string Shape { get; set; } = "sphere";
    /// <summary>[x, y, z] boy (m). Kure ve yastik icin uc eksen; kapsul ve silindir icin [cap, boy, cap].</summary>
    public List<float> Size { get; set; } = new();
    public List<float> Pos { get; set; } = new();
    /// <summary>[x, y, z] derece (YXZ).</summary>
    public List<float> Rot { get; set; } = new();
    /// <summary>"RRGGBB" ya da govdenin renk adi (colors).</summary>
    public string Color { get; set; } = "";
    public float Round { get; set; } = 0.4f;
    /// <summary>Sol (+X) parca sag tarafa da aynalanir.</summary>
    public bool Mirror { get; set; }
}

public sealed class BodyDef
{
    public Dictionary<string, string> Colors { get; set; } = new();
    public List<BodyBoneDef> Bones { get; set; } = new();
    public List<BodyPartDef> Parts { get; set; } = new();
    /// <summary>Kabarinca gorunen Maya kesesi (SANAT-SES §4.4): kemik, konum, yaricap.</summary>
    public string SacBone { get; set; } = "";
    public List<float> SacPos { get; set; } = new();
    public float SacRadius { get; set; } = 0.1f;
    /// <summary>URETIM §3 yer tutucu isareti (neden). Bos: yer tutucu degil.</summary>
    public string Placeholder { get; set; } = "";
}

public sealed class BodiesFile
{
    public Dictionary<string, BodyDef> Bodies { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("bodies.json")]
    public BodiesFile Bodies { get; private set; } = new();

    public BodyDef? Body(string id) => Bodies.Bodies.TryGetValue(id, out var b) ? b : null;
}
