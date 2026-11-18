namespace Kabarma.Sim;

/// <summary>Mancinik duragi: kurulu mancinik (yerlestirilebilir) ya da Seyir Kulesi'nin dibindeki hazir durak.</summary>
public sealed class CatapultStop
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    /// <summary>Kullanilabilir mi (kule duragi: kule tirmanilinca acilir).</summary>
    public bool Open { get; set; } = true;
}

/// <summary>Ucus plani: baslangic, inis noktasi, sure, yay tepesi. Konum Sample(t) ile.</summary>
public readonly record struct CatapultFlight(float X0, float Y0, float Z0, float X1, float Y1, float Z1, float Seconds, float Apex)
{
    /// <summary>t saniyedeki konum: yatayda dogrusal, dikeyde iki ucu baglayan parabol + tepe.</summary>
    public (float x, float y, float z) Sample(float t)
    {
        float u = Seconds <= 0 ? 1f : Math.Clamp(t / Seconds, 0f, 1f);
        float y = Y0 + (Y1 - Y0) * u + 4f * Apex * u * (1f - u);
        return (X0 + (X1 - X0) * u, y, Z0 + (Z1 - Z0) * u);
    }
}

/// <summary>
/// Mancinik kurali (GDD §21, ICERIK §17): acik bir duraktan acik bir duraga firlatir; parasutle inilir. Yuk nesnesi
/// (el arabasi, kargo kasasi, tasinan ekin) varken firlatmaz. Durumsuz; ucus sayaci Exploration.Flights.
/// </summary>
public static class Catapult
{
    public enum Refusal { None, NoStop, Closed, Same, TooClose, Carrying }

    public static Refusal Check(CatapultDef def, CatapultStop? from, CatapultStop? to, bool carrying)
    {
        if (from == null || to == null) return Refusal.NoStop;
        if (!from.Open || !to.Open) return Refusal.Closed;
        if (from.Id == to.Id) return Refusal.Same;
        if (carrying) return Refusal.Carrying;
        if (Distance(from, to) < def.MinDistance) return Refusal.TooClose;
        return Refusal.None;
    }

    public static float Distance(CatapultStop a, CatapultStop b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>Ucus: durak a'dan, b'nin LandOffset yanina (a'dan gelis yonunun berisine). groundAt inis yuksekligi.</summary>
    public static CatapultFlight Plan(CatapultDef def, CatapultStop a, CatapultStop b, Func<float, float, float> groundAt)
    {
        float d = Distance(a, b);
        float dx = d > 0 ? (b.X - a.X) / d : 0, dz = d > 0 ? (b.Z - a.Z) / d : 0;
        float lx = b.X - dx * def.LandOffset, lz = b.Z - dz * def.LandOffset;
        float seconds = Math.Min(def.FlightMax, def.FlightBase + d * def.FlightPerMeter);
        float apex = Math.Max(def.ApexMin, d * def.ApexRatio);
        return new CatapultFlight(a.X, a.Y + 1.5f, a.Z, lx, groundAt(lx, lz), lz, seconds, apex);
    }

    public static string Text(Refusal r) => r switch
    {
        Refusal.NoStop => "Yakında mancınık durağı yok",
        Refusal.Closed => "Durak kapalı: önce Seyir Kulesi'ne tırman",
        Refusal.Same => "Aynı durak",
        Refusal.TooClose => "Durak çok yakın",
        Refusal.Carrying => "Yükle fırlatılmaz: arabayı ya da kasayı bırak",
        _ => "",
    };
}
