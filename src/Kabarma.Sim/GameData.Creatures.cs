namespace Kabarma.Sim;

// Yaratiklar: creatures.json.

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

public sealed partial class GameData
{
    [DataFile("creatures.json")]
    public CreaturesFile Creatures { get; private set; } = new();

    public CreatureDef Creature(string id) => Creatures.Creatures.First(c => c.Id == id);
}
