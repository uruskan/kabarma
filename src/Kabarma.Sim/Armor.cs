namespace Kabarma.Sim;

// kusam (M4): zirh takimlari, kusam modulleri ve Capa Mk (ICERIK 16). Takim, modul ve Mk "uzerinde" sayilir: ilgili esya cantadaysa
// giyilir (arayuz gerekmez); en yuksek zirh takimi, o takimin yuvasi kadar modul, en yuksek Mk. Duz ve kayda gerek yok (canta kaydi).

public sealed class ArmorSetDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public float Armor { get; set; }
    public int ModuleSlots { get; set; }
    public Dictionary<string, int> Cost { get; set; } = new();
}

public sealed class ArmorModuleDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    /// <summary>Etki anahtari -> deger (heatResist, backpackSlots, shield ...).</summary>
    public Dictionary<string, float> Effects { get; set; } = new();
    public Dictionary<string, int> Cost { get; set; } = new();
}

public sealed class CapaMkDef
{
    public string Id { get; set; } = "";
    public int Mk { get; set; }
    public int Tier { get; set; }
    /// <summary>Baslik basina cip yuvasi (GDD 4 / ICERIK 6.3.1).</summary>
    public int ChipSlots { get; set; }
    public Dictionary<string, int> Cost { get; set; } = new();
}

public sealed class ArmorFile
{
    public List<ArmorSetDef> Sets { get; set; } = new();
    public List<ArmorModuleDef> Modules { get; set; } = new();
    public List<CapaMkDef> CapaMk { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("armor.json")]
    public ArmorFile Armor { get; private set; } = new();
}

/// <summary>Zirh kurallari: canta icerigine gore giyilen takim, moduller, Mk ve gelen hasar.</summary>
public static class ArmorRules
{
    /// <summary>Cantadaki en yuksek zirhli takim (yoksa null: Kooperatif Tulumu 0 zirh).</summary>
    public static ArmorSetDef? Worn(ArmorFile f, Func<string, int> count) =>
        f.Sets.Where(s => count(s.Id) > 0).OrderByDescending(s => s.Armor).FirstOrDefault();

    public static float ArmorPoints(ArmorFile f, Func<string, int> count) => Worn(f, count)?.Armor ?? 0f;

    public static int ModuleSlots(ArmorFile f, Func<string, int> count, int tulumSlots = 3) => Worn(f, count)?.ModuleSlots ?? tulumSlots;

    /// <summary>Cantadaki moduller, yuva sayisina kadar (veri sirasiyla).</summary>
    public static List<ArmorModuleDef> ActiveModules(ArmorFile f, Func<string, int> count, int tulumSlots = 3) =>
        f.Modules.Where(m => count(m.Id) > 0).Take(ModuleSlots(f, count, tulumSlots)).ToList();

    /// <summary>Etkin modullerin bir etkisi toplami (ornek: shield, backpackSlots).</summary>
    public static float Effect(ArmorFile f, Func<string, int> count, string key, int tulumSlots = 3) =>
        ActiveModules(f, count, tulumSlots).Sum(m => m.Effects.TryGetValue(key, out var v) ? v : 0f);

    /// <summary>Capa Mk: cantadaki en yuksek (Mk.I varsayilan).</summary>
    public static CapaMkDef? Mk(ArmorFile f, Func<string, int> count) =>
        f.CapaMk.Where(m => count(m.Id) > 0).OrderByDescending(m => m.Mk).FirstOrDefault();

    public static int ChipSlots(ArmorFile f, Func<string, int> count, int mk1Slots = 1) => Mk(f, count)?.ChipSlots ?? mk1Slots;

    /// <summary>Gelen hasar: zirh puani GDD 16.1 (Waves.AfterArmor), sonra kalkan jeneratoru (shield) once tuketilir. Donus: (hasar, kalan kalkan).</summary>
    public static (float damage, float shield) Absorb(float dmg, float armor, float shield)
    {
        dmg = Waves.AfterArmor(dmg, armor);
        float eaten = MathF.Min(shield, dmg);
        return (dmg - eaten, shield - eaten);
    }
}