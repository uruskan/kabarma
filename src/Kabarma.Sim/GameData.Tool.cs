namespace Kabarma.Sim;

// Capa ve cevresi (GDD 4). Bellekte tek ToolFile (Data.Tool), diskte TEKNIK §13 duzeninde birkac dosya:
//   heads.json      basliklar, menziller, hizlar, woodPerChop
//   weapons.json    weapon (Tirpan Isini), nailgun (Civi Tabancasi)
//   recipes.json    recipes
//   placeables.json placeables, buildCosts
//   foods.json      foods
//   prices.json     prices (sira satis menusundeki siradir)
//   tool.json       player, backpackSlots, stackResource, startItems

public sealed partial class WeaponDef
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

public sealed partial class ToolFile
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
    public Dictionary<string, int> BuildCosts { get; set; } = new();
    public int WoodPerChop { get; set; } = 2;
    public Dictionary<string, int> Prices { get; set; } = new();
    public int BackpackSlots { get; set; }
    public int StackResource { get; set; }
    public Dictionary<string, int> StartItems { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("tool.json", "heads.json", "weapons.json", "recipes.json", "placeables.json", "foods.json", "prices.json")]
    public ToolFile Tool { get; private set; } = new();
}
