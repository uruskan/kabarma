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
    /// <summary>Urun cantaya degil, dunyaya yuk nesnesi olarak cikar (kargo kasasi, El Arabasi; GDD §5).</summary>
    public bool Load { get; set; }
    /// <summary>Tarif bu esya ilk kez alininca acilir (GDD §12 Kesif); null: bastan acik.</summary>
    public string? UnlockOn { get; set; }
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
    /// <summary>Is basina yakan yakit turleri (ilk bulunan harcanir) ve adedi (ICERIK §20: iş başına 1).</summary>
    public List<string> Fuel { get; set; } = new();
    public int FuelPerJob { get; set; }
    /// <summary>Depo yuvasi (Sandik 20).</summary>
    public int Slots { get; set; }
    /// <summary>Gece isigi yaricapi, m (Mesale 6).</summary>
    public float LightRange { get; set; }
    /// <summary>Dogma noktasi ve uyku yeri (Uyku Tulumu).</summary>
    public bool Spawn { get; set; }
}

public sealed class FoodDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float MaxHp { get; set; }
    /// <summary>Suresince azami Dayaniklilik'a eklenir (ICERIK §15 "Day.").</summary>
    public float Stamina { get; set; }
    public float Regen { get; set; }
    public float Seconds { get; set; }
}

public sealed class PlayerDef
{
    public float MaxHp { get; set; }
    public float WalkSpeed { get; set; }
    public float SprintSpeed { get; set; }
    public float Jump { get; set; }
    public StaminaDef Stamina { get; set; } = new();
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
    public SulamaDef Sulama { get; set; } = new();
    public Dictionary<string, int> Prices { get; set; } = new();
    public int BackpackSlots { get; set; }
    public int StackResource { get; set; }
    public Dictionary<string, int> StartItems { get; set; } = new();
    // Ekonomi: yigin boylari (GDD §5), yemek yuvalari (GDD §3.3), fiyat kurallari (GDD §13, ICERIK §18).
    public int StackIntermediate { get; set; }
    public int StackFood { get; set; }
    public List<string> Intermediates { get; set; } = new();
    public int FoodSlots { get; set; } = 3;
    public List<string> Processed { get; set; } = new();
    public float ProcessedMult { get; set; } = 1f;
    public List<float> Quality { get; set; } = new() { 1f };

    /// <summary>Esya basina yigin boyu: yemek 10, ara urun 20, kalan her sey (kaynak) 50 (GDD §5).</summary>
    public int StackOf(string id)
    {
        if (StackFood > 0 && Foods.Any(f => f.Id == id)) return StackFood;
        if (StackIntermediate > 0 && Intermediates.Contains(id)) return StackIntermediate;
        return StackResource;
    }
}

public sealed partial class GameData
{
    [DataFile("tool.json", "heads.json", "weapons.json", "recipes.json", "placeables.json", "foods.json", "prices.json")]
    public ToolFile Tool { get; private set; } = new();
}
