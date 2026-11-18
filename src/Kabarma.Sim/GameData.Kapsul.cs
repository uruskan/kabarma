namespace Kabarma.Sim;

// Inis kapsulunun parcali gorunusu: kapsul.json (gorsel-model akisi). Oyun kurali degil; Pod.Model.cs okur.
// Govde res://assets/models/kapsul/kapsul.glb'de kalir (Pod.cs); kapak-rampa, inis ayaklari, Kargo Rampasi pisti ve
// kargo roketi buradaki yollardan gelir. Dosya yoksa o parca eski gorunusunde kalir.

public sealed class KapsulLegDef
{
    /// <summary>Ayak baglanti noktasi (Pod yerel ekseni, Y yukari, +Z kapak yonu), metre.</summary>
    public float[] Pos { get; set; } = Array.Empty<float>();
    /// <summary>Y etrafinda donus (derece); ayak modelinin +X'i disari baksin.</summary>
    public float Yaw { get; set; }
    /// <summary>Disa egim (derece).</summary>
    public float Tilt { get; set; }
}

public sealed class KapsulFile
{
    /// <summary>Govdeye Pod.cs'nin -90 derecesine ek Y donusu (derece): modelin kapi boslugu +Z'ye (kapak, rampa) baksin.</summary>
    public float BodyYaw { get; set; }
    /// <summary>Kapak-rampa modeli (yere yatik uretilir; mentese kenari modelin -X ucunda).</summary>
    public string Hatch { get; set; } = "";
    /// <summary>Mentese (Pod yerel): kapagin alt kenari.</summary>
    public float[] Hinge { get; set; } = Array.Empty<float>();
    /// <summary>Kapali durus icin modele uygulanan donus (derece, X Y Z): yatik plakayi dik kapiya cevirir.</summary>
    public float[] HatchClosedRotation { get; set; } = Array.Empty<float>();
    /// <summary>Kapak modelinin boyu (m): uretilen model bu yukseklige olceklenir (0 = olcekleme yok).</summary>
    public float HatchLength { get; set; }
    public string Leg { get; set; } = "";
    /// <summary>Ayak modelinin boyu (m), 0 = olcekleme yok.</summary>
    public float LegLength { get; set; }
    public List<KapsulLegDef> Legs { get; set; } = new();
    /// <summary>Kargo Rampasi pisti (eski MeshKit yerine).</summary>
    public string Pad { get; set; } = "";
    /// <summary>Kargo roketi (eski MeshKit yerine).</summary>
    public string Rocket { get; set; } = "";
}

public sealed partial class GameData
{
    [DataFile("kapsul.json")]
    public KapsulFile Kapsul { get; private set; } = new();

    /// <summary>Yalniz kapsul.json (Pod butun GameData'ya erismeden okur).</summary>
    public static KapsulFile ParseKapsul(string json) => (KapsulFile)Parse(typeof(KapsulFile), _ => json, new[] { "kapsul.json" });
}
