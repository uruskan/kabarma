namespace Kabarma.Sim;

// members.json (12 uye tipi, ICERIK §9) ve cosmetics.json (72 kozmetik parca, ICERIK §9). Oynanisa etkisi yalniz uye pasifindedir (GDD 23).

public sealed class MemberDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Pasifin okunur metni (ICERIK §9).</summary>
    public string Passive { get; set; } = "";
    /// <summary>Pasif etkiler: anahtar -> deger. Carpan anahtarlari 0,10 = +%10; bayraklar 1. Anahtar listesi ve baglandiklari yer docs notunda.</summary>
    public Dictionary<string, float> Effects { get; set; } = new();
    /// <summary>Kabarma'ya basladigi kart (cards.json kimligi; bos: rastgele Siradan kart).</summary>
    public string StartCard { get; set; } = "";
    public string Unlock { get; set; } = "";
    /// <summary>Acan is emri zinciri ve adimi (orders.json grants kind=member). "start": baştan acik.</summary>
    public string Chain { get; set; } = "";
    public int Step { get; set; }
    /// <summary>Gorunus: tulum rengi (kimlik tulumu yoksa) ve kisa not; uye tipinin kendi modeli yer tutucudur.</summary>
    public string Look { get; set; } = "";
    /// <summary>Pasifin oyunda baglandigi yerler (anahtar listesi); bos: etkisi ilgili sistem gelince baglanir.</summary>
    public List<string> LiveKeys { get; set; } = new();
}

public sealed class MemberFile
{
    public string DefaultMember { get; set; } = "cirak";
    public List<MemberDef> Members { get; set; } = new();
    public MemberDef? Member(string id) => Members.FirstOrDefault(m => m.Id == id);
}

public sealed class CosmeticDef
{
    public string Id { get; set; } = "";
    /// <summary>kask | vizor | tulum | canta | capa | jest.</summary>
    public string Slot { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>start | catalog | order | wheel | hq (Genel Merkez).</summary>
    public string Source { get; set; } = "catalog";
    public int Price { get; set; }
    /// <summary>Gorunus tarifi: slot'a gore (kind, renkler, desen, klip...). Kod cizilir (CosmeticView).</summary>
    public Dictionary<string, string> Look { get; set; } = new();
}

public sealed class CosmeticFile
{
    public List<CosmeticDef> Cosmetics { get; set; } = new();
    public List<string> Slots { get; set; } = new() { "kask", "vizor", "tulum", "canta", "capa", "jest" };
    public CosmeticDef? Get(string id) => Cosmetics.FirstOrDefault(c => c.Id == id);
}

public sealed partial class GameData
{
    [DataFile("members.json")]
    public MemberFile Members { get; private set; } = new();

    [DataFile("cosmetics.json")]
    public CosmeticFile Cosmetics { get; private set; } = new();
}
