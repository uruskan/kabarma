namespace Kabarma.Sim;

// Araclar: vehicles.json (ICERIK §14, GDD §20). M2: Cekirge (ATV). El Arabasi ekonomi akisinda (economy.json cart).

public sealed class VehicleDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public int Seats { get; set; } = 1;
    public int Slots { get; set; }
    /// <summary>Ileri en yuksek hiz, geri hiz (m/sn), ivme ve fren (m/sn²).</summary>
    public float MaxSpeed { get; set; }
    public float ReverseSpeed { get; set; }
    public float Accel { get; set; }
    public float Brake { get; set; }
    /// <summary>Surtunme yavaslamasi (gaz yokken, m/sn²).</summary>
    public float Coast { get; set; } = 2f;
    /// <summary>Donus hizi (rad/sn) TurnAtSpeed m/sn'de; daha yavasta orantili azalir.</summary>
    public float TurnRate { get; set; }
    public float TurnAtSpeed { get; set; } = 4f;
    /// <summary>Yakit esyasi ve depo: TankUnits birim, birim basina gazda gecen sure (sn). GDD §20: dolu depo ~1 saat.</summary>
    public string FuelItem { get; set; } = "";
    public int TankUnits { get; set; }
    public float UnitSeconds { get; set; }
    /// <summary>Bu egimden dik yokusta FlipSpeed'ten hizli gidilirse devrilir (derece, m/sn).</summary>
    public float FlipSlopeDeg { get; set; } = 35f;
    public float FlipSpeed { get; set; } = 3f;
    public float Hp { get; set; } = 100f;
    /// <summary>Tarif (belge: ICERIK §20). Girdileri main'de olunca recipes.json'a satir olarak eklenir.</summary>
    public Dictionary<string, int> Recipe { get; set; } = new();
    public string Placeholder { get; set; } = "";
}

public sealed class VehiclesFile
{
    public List<VehicleDef> Vehicles { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("vehicles.json")]
    public VehiclesFile Vehicles { get; private set; } = new();

    public VehicleDef Vehicle(string id) => Vehicles.Vehicles.First(v => v.Id == id);
}
