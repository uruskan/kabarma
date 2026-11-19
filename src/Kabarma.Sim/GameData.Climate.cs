namespace Kabarma.Sim;

// Biyom sozlesmesi (M4 biyom akisi): altı biyomun kimlikleri, kademeleri ve iklim tehlikeleri (GDD §3.5, ICERIK §1).
// Veri: data/climate.json. Durumsuz; kayda ve aga girmez.

/// <summary>Bir iklim tehlikesi: nerede, ne ile direnilir, direnc yetmezse ne olur (GDD §3.5).</summary>
public sealed class HazardDef
{
    public string Name { get; set; } = "";
    /// <summary>Direnc veren kusam modulleri (giyili esya kimlikleri).</summary>
    public List<string> Gear { get; set; } = new();
    /// <summary>Direnc veren yemekler (etkin yemek kimlikleri).</summary>
    public List<string> Foods { get; set; } = new();
    /// <summary>Direnc veren durumlar: "shelter" (kapali alan ya da isi kaynagi), "shade", "wet".</summary>
    public List<string> Conditions { get; set; } = new();
    /// <summary>Direnc yetmezse saniyede giden can (GDD §3.5: 1-3). Hicbir tehlike anında oldurmez.</summary>
    public float DrainPerSecond { get; set; } = 1f;
    /// <summary>Direnc yetmezse dayaniklilik yenilenmez.</summary>
    public bool BlocksStaminaRegen { get; set; } = true;
}

/// <summary>Biyomun bir tehlikeye maruz birakmasi: ne zaman ("day", "night", "always") ve hangi bolgede ("" = her yerde).</summary>
public sealed class BiomeHazardDef
{
    public string Hazard { get; set; } = "";
    public string When { get; set; } = "always";
    /// <summary>Bos: biyomun her yeri. Dolu: yalniz o bolge ("gas_pit": Sazlik gaz cukurlari).</summary>
    public string Zone { get; set; } = "";
}

public sealed class BiomeClimateDef
{
    /// <summary>Kademe (ICERIK §1 "T"): Cayir 1 ... Gobek 6.</summary>
    public int Tier { get; set; }
    public List<BiomeHazardDef> Hazards { get; set; } = new();
    /// <summary>Girisin onerilen donanimi (arayuz ipucu; zorunlu degil).</summary>
    public List<string> EntryGear { get; set; } = new();
}

public sealed class ClimateFile
{
    /// <summary>Altı biyomun kimligi, kademe sirasiyla (altin_cayir ... gobek).</summary>
    public List<string> BiomeIds { get; set; } = new();
    public Dictionary<string, HazardDef> Hazards { get; set; } = new();
    public Dictionary<string, BiomeClimateDef> Biomes { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("climate.json")]
    public ClimateFile Climate { get; private set; } = new();
}
