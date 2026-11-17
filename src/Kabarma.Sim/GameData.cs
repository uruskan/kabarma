using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kabarma.Sim;

// Veri dosyalarinin (game/data/*.json) C# karsiliklari. Sayilar burada degil, veride durur.

public sealed class Genes
{
    public int V { get; set; } = 1;
    public int H { get; set; } = 1;
    public int D { get; set; } = 1;
    public int M { get; set; } = 1;
}

public sealed class CropDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Family { get; set; } = "";
    public float GrowSeconds { get; set; }
    public int BasePrice { get; set; }
    public int BaseYield { get; set; } = 1;
    public int SeedReturn { get; set; }
    public Genes SeedGenes { get; set; } = new();
    public float MoistureDrainPerSecond { get; set; }
    public float WaterPerShot { get; set; }
}

public sealed class MoistureRules
{
    public float StopBelow { get; set; }
    public float HalfBelow { get; set; }
    public float WetAbove { get; set; }
    public float WetFactor { get; set; }
}

public sealed class SizeClassDef
{
    public string Id { get; set; } = "";
    public float MinScore { get; set; }
    public int Kg { get; set; }
    public int PriceMult { get; set; }
    public float Scale { get; set; }
}

public sealed class CropsFile
{
    public List<CropDef> Crops { get; set; } = new();
    public MoistureRules Moisture { get; set; } = new();
    public float StartFertility { get; set; }
    public List<float> QualityMult { get; set; } = new() { 1f, 1f, 1f };
    public float HarvestFertilityLoss { get; set; } = 10f;
    public List<SizeClassDef> SizeClasses { get; set; } = new();
}

public sealed class CreatureDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Archetype { get; set; } = "";
    public int Weight { get; set; }
    public float Hp { get; set; }
    public float Damage { get; set; }
    public float Speed { get; set; }
    public float SwollenScale { get; set; }
    public float WeakPointMult { get; set; } = 1f;
    public float EatSeconds { get; set; }
    public float KickInterval { get; set; }
    public float KickRange { get; set; }
    public float SwollenSpeedMult { get; set; } = 1f;
}

public sealed class CreaturesFile
{
    public List<CreatureDef> Creatures { get; set; } = new();
}

public sealed class CardDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Rarity { get; set; } = "S";
    public string Text { get; set; } = "";
    public string Stat { get; set; } = "";
    public float PerRank { get; set; }
}

public sealed class CardsFile
{
    public Dictionary<string, int> RarityWeights { get; set; } = new();
    public int MaxRank { get; set; }
    public int OfferSize { get; set; }
    public float AutoPickSeconds { get; set; }
    public float SlowMoScale { get; set; }
    public List<CardDef> Cards { get; set; } = new();
}

public sealed class WaveDef
{
    public int Count { get; set; }
    public float Seconds { get; set; }
    public float GapAfter { get; set; }
}

public sealed class NightDef
{
    public int Ks { get; set; }
    public float AlarmSeconds { get; set; }
    public float SwellSeconds { get; set; }
    public List<WaveDef> Waves { get; set; } = new();
    public int MaxCardLevels { get; set; }
    public string Enemy { get; set; } = "";
}

public sealed class GosteriDef
{
    public float Min { get; set; }
    public float Max { get; set; }
    public int StreakKills { get; set; }
    public float StreakWindow { get; set; }
    public float StreakBonus { get; set; }
    public float DecayPerSecond { get; set; }
}

public sealed class KabarmaFile
{
    public NightDef FirstNight { get; set; } = new();
    public float LevelBase { get; set; }
    public float LevelExponent { get; set; }
    public GosteriDef Gosteri { get; set; } = new();
    public float OzPerWeight { get; set; } = 1f;
    public float MayaPerWeight { get; set; }
    public float MayaKsFactor { get; set; }
    public float HpGrowth { get; set; }
    public float DamageGrowth { get; set; }
}

public sealed class WeaponDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Damage { get; set; }
    public float FireInterval { get; set; }
    public float Range { get; set; }
    public float Speed { get; set; }
    public float Width { get; set; }
    public float SpreadDegrees { get; set; }
    public float CritChance { get; set; }
    public float CritMult { get; set; }
    public float Knockback { get; set; }
    public float BurnDps { get; set; }
    public float BurnSeconds { get; set; }
    public float FreezeSlow { get; set; }
    public float FreezeSeconds { get; set; }
    public string? Ammo { get; set; }
    public int AmmoPerShot { get; set; }
}

public sealed class RecipeDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Station { get; set; } = "";
    public Dictionary<string, int> In { get; set; } = new();
    public int Out { get; set; } = 1;
    public float Seconds { get; set; }
}

public sealed class PlaceableDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, int> Cost { get; set; } = new();
    public string? Station { get; set; }
    public string? Model { get; set; }
    public bool Once { get; set; }
    public float Radius { get; set; } = 1f;
}

public sealed class FoodDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float MaxHp { get; set; }
    public float Regen { get; set; }
    public float Seconds { get; set; }
}

public sealed class PlayerDef
{
    public float MaxHp { get; set; }
    public float WalkSpeed { get; set; }
    public float SprintSpeed { get; set; }
    public float Jump { get; set; }
}

public sealed class ToolFile
{
    public List<string> Heads { get; set; } = new();
    public float MineRate { get; set; }
    public float MineRange { get; set; }
    public float TillRange { get; set; }
    public float WaterRange { get; set; }
    public float CarryRange { get; set; }
    public float BuildRange { get; set; }
    public PlayerDef Player { get; set; } = new();
    public WeaponDef Weapon { get; set; } = new();
    public WeaponDef Nailgun { get; set; } = new();
    public float MowRadius { get; set; } = 1.5f;
    public float MowRange { get; set; } = 5f;
    public List<RecipeDef> Recipes { get; set; } = new();
    public List<FoodDef> Foods { get; set; } = new();
    public List<PlaceableDef> Placeables { get; set; } = new();
    public Dictionary<string, int> Prices { get; set; } = new();
    public int BackpackSlots { get; set; }
    public int StackResource { get; set; }
    public Dictionary<string, int> StartItems { get; set; } = new();
}

public sealed class StepDef
{
    public string Id { get; set; } = "";
    public int Target { get; set; }
    public string Goal { get; set; } = "";
    public string? Start { get; set; }
    public string Done { get; set; } = "";
}

public sealed class ScenarioFile
{
    public List<string> Intro { get; set; } = new();
    public List<StepDef> Steps { get; set; } = new();
    public Dictionary<string, string> NightLines { get; set; } = new();
    public string NextOrder { get; set; } = "";
    public int OrdersTotal { get; set; }
}

public sealed class GameData
{
    public CropsFile Crops { get; init; } = new();
    public CreaturesFile Creatures { get; init; } = new();
    public CardsFile Cards { get; init; } = new();
    public KabarmaFile Kabarma { get; init; } = new();
    public ToolFile Tool { get; init; } = new();
    public ScenarioFile Scenario { get; init; } = new();

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    static T Parse<T>(Func<string, string> read, string file) =>
        JsonSerializer.Deserialize<T>(read(file), Options) ?? throw new InvalidDataException($"{file} okunamadi");

    /// <param name="read">Dosya adini (ornegin "crops.json") metne ceviren okuyucu. Godot ve testler kendi okuyucusunu verir.</param>
    public static GameData Load(Func<string, string> read) => new()
    {
        Crops = Parse<CropsFile>(read, "crops.json"),
        Creatures = Parse<CreaturesFile>(read, "creatures.json"),
        Cards = Parse<CardsFile>(read, "cards.json"),
        Kabarma = Parse<KabarmaFile>(read, "kabarma.json"),
        Tool = Parse<ToolFile>(read, "tool.json"),
        Scenario = Parse<ScenarioFile>(read, "scenario.json"),
    };

    public CropDef Crop(string id) => Crops.Crops.First(c => c.Id == id);
    public CreatureDef Creature(string id) => Creatures.Creatures.First(c => c.Id == id);
}
