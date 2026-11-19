namespace Kabarma.Sim;

// Oyuklar (M3, oyuk akisi; ICERIK §4.3 ve §17 "M3: Oyuklar", TEKNIK §7.5). Kurallar Oyuk*.cs siniflarinda. Kademe dizileri
// (tier) birinci, ikinci, ucuncu Oyuk icindir; dizi kisaysa son eleman kullanilir.

/// <summary>Ic mekan odasi: oynanabilir karenin disinda, arazinin Depth metre altinda.</summary>
public sealed class OyukInteriorDef
{
    /// <summary>Oda tabaninin yuksekligi (m).</summary>
    public float Depth { get; set; } = -500f;
    /// <summary>Ilk odanin xz merkezi ve odalar arasi aralik (m): oynanabilir karenin disi.</summary>
    public List<float> Origin { get; set; } = new() { 1000f, 0f };
    public float Spacing { get; set; } = 120f;
    /// <summary>Taban yaricapi ve tavan yuksekligi (m), koruyucuya gore ayri.</summary>
    public float Radius { get; set; } = 16f;
    public float Height { get; set; } = 9f;
    /// <summary>Cikis kapisi ve giriste oyuncunun dogdugu yer, oda merkezine gore [x, z].</summary>
    public List<float> Door { get; set; } = new() { 0f, 14f };
    /// <summary>Kapi ve yuzeydeki agiz etkilesim yaricapi (m).</summary>
    public float DoorReach { get; set; } = 2.5f;
    /// <summary>Icerideki ortam isigi (0..1) ve odanin kendi isiklarinin gucu.</summary>
    public float Ambient { get; set; } = 0.06f;
    public string WallColor { get; set; } = "4A3B30";
    public string FloorColor { get; set; } = "6B5240";
}

/// <summary>Koruyucu satiri (Kostebek Bey, Yarasa Yumagi; M4'te uc tane daha).</summary>
public sealed class OyukGuardianDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    /// <summary>Bu koruyucunun tuttugu biyom (biomes.json kimligi).</summary>
    public string Biome { get; set; } = "";
    /// <summary>"kostebek", "yarasa", "camur", "akrep" ya da "ayna": hangi kural sinifi (MoleFight, BatKnotFight, MudFight, ScorpionFight, MirrorFight).</summary>
    public string Kind { get; set; } = "";
    public List<float> Hp { get; set; } = new() { 300f };
    /// <summary>Oyuncuya vurusu (kostebek cikis carpmasi, yarasa dalisi).</summary>
    public List<float> Damage { get; set; } = new() { 10f };
    /// <summary>Vurusun yaricapi (m).</summary>
    public float HitRadius { get; set; } = 2f;
    /// <summary>Savunmasiz (sersem, dagilmis) haldeyken hasar carpani.</summary>
    public float WeakMult { get; set; } = 2f;
    public float Radius { get; set; } = 1.2f;
    public string Color { get; set; } = "8A6A50";
    public string Placeholder { get; set; } = "";

    // ---- Kostebek ----
    public List<int> Holes { get; set; } = new() { 5 };
    /// <summary>Deliklerin oda merkezinden uzakligi (m).</summary>
    public float HoleRing { get; set; } = 9f;
    public List<float> HiddenSeconds { get; set; } = new() { 2.5f };
    public List<float> SurfaceSeconds { get; set; } = new() { 3f };
    public float StunSeconds { get; set; } = 4f;
    /// <summary>Delik bu kadar litre suyla islanir; islaklik WetSeconds surer.</summary>
    public float FloodLiters { get; set; } = 6f;
    public float WetSeconds { get; set; } = 8f;
    /// <summary>Tikac kayasi deligin bu kadar yakinina birakilirsa tikar (m).</summary>
    public float PlugRadius { get; set; } = 1.3f;
    public int PlugRocks { get; set; } = 7;
    public float PlugKg { get; set; } = 40f;
    /// <summary>Butun delikler tikaninca: sikisip ortada kalir, aralikla oyuncuya hamle eder.</summary>
    public float TrappedStunSeconds { get; set; } = 6f;
    public float TrappedLungeEvery { get; set; } = 3f;

    // ---- Yarasa ----
    public List<int> Bats { get; set; } = new() { 24 };
    /// <summary>Isik kaynagi yumaga bu yaricapta (m) ise dagilir.</summary>
    public float LightRadius { get; set; } = 7f;
    public List<float> RegroupSeconds { get; set; } = new() { 4f };
    public List<float> DiveEvery { get; set; } = new() { 3f };
    /// <summary>Kandil sayisi ve E ile yakilinca kac saniye yanar (0: sonmez).</summary>
    public int Lamps { get; set; } = 3;
    public float LampSeconds { get; set; } = 0f;
    /// <summary>Yumagin tavandan asagi sarktigi yukseklik (m, tabandan).</summary>
    public float HangHeight { get; set; } = 7.5f;
    /// <summary>Odanin ortam isigi (0: Interior.Ambient) ve duvar fenerleri (adet; karanlik yalniz Yarasa'nin mekanigi).</summary>
    public float Ambient { get; set; } = 0f;
    public int WallLights { get; set; } = 0;
    /// <summary>Oda tavani (m); 0 ise Interior.Height.</summary>
    public float RoomHeight { get; set; } = 0f;

    // ---- Camur Devi (M4, ata4) ----
    /// <summary>Yuruyus hizi (m/s), vurus araligi (sn), islakken canlanma (can/sn): kademe dizileri.</summary>
    public List<float> MoveSpeed { get; set; } = new() { 2.2f };
    public List<float> SlamEvery { get; set; } = new() { 3.4f };
    public List<float> RegenPerSecond { get; set; } = new() { 12f };
    /// <summary>Kuruma: ates/yanma hasarinin her puani DryPerHeat sn kuruluk ekler; DryNeeded sn dolunca kurur ve DryHold sn kuru kalir.</summary>
    public float DryNeeded { get; set; } = 3f;
    public float DryHold { get; set; } = 7f;
    public float DryPerHeat { get; set; } = 0.06f;
    /// <summary>Gol (oda merkezine gore [x, z], yaricap): kuruyan dev oraya yurur ve girince yeniden islanir.</summary>
    public List<float> Pool { get; set; } = new() { 0f, -8f };
    public float PoolRadius { get; set; } = 3f;
    // ---- Kuvars Akrebi (M4, ata4) ----
    /// <summary>Catlak suresi (sn); agir vurus esigi (Havan: bu hasar ve ustu catlatir); sarkit sayisi, halka yaricapi, dusme gecikmesi (sn) ve etki yaricapi.</summary>
    public float CrackSeconds { get; set; } = 7f;
    public float CrackThreshold { get; set; } = 60f;
    public int Stalactites { get; set; } = 4;
    public float StalactiteRing { get; set; } = 8f;
    public float StalactiteRadius { get; set; } = 2.6f;
    public float FallSeconds { get; set; } = 0.9f;
    /// <summary>Kuyruk (zehir) atisi araligi (sn, kademe) ve menzili (m).</summary>
    public List<float> TailEvery { get; set; } = new() { 3.2f };
    public float TailRange { get; set; } = 16f;
    // ---- Buz Aynasi (M4, ata4) ----
    /// <summary>Kopya cikarma araligi (sn), ayni anda en cok kopya, kopya cani ve vurus araligi (sn): kademe dizileri.</summary>
    public List<float> CopyEvery { get; set; } = new() { 6f };
    public List<int> CopyMax { get; set; } = new() { 2 };
    public List<float> CopyHp { get; set; } = new() { 40f };
    public float CopyReach { get; set; } = 2.4f;
    public float CopyAttackEvery { get; set; } = 1.6f;
    public float CopySpeed { get; set; } = 3.2f;

    /// <summary>Ilk acilista sandiktan cikanlar: esya → kademe dizisi.</summary>
    public Dictionary<string, List<int>> Rewards { get; set; } = new();
    public int Chests { get; set; } = 2;

    public static T Tier<T>(List<T> list, int tier) => list.Count == 0 ? default! : list[Math.Clamp(tier, 0, list.Count - 1)];
}

public sealed class OyukFile
{
    public OyukInteriorDef Interior { get; set; } = new();
    public List<OyukGuardianDef> Guardians { get; set; } = new();

    public OyukGuardianDef? ForBiome(string biome) => Guardians.FirstOrDefault(g => g.Biome == biome);
    public OyukGuardianDef Get(string id) => Guardians.FirstOrDefault(g => g.Id == id) ?? throw new KeyNotFoundException($"oyuk.json: '{id}' yok");
}

public sealed partial class GameData
{
    [DataFile("oyuk.json")]
    public OyukFile Oyuk { get; private set; } = new();
}
