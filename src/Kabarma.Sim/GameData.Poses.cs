namespace Kabarma.Sim;

// Poz kutuphanesi: poses.json (URETIM §2.2 E.3). Ornekleme: Poses.cs.

public sealed class PosesFile
{
    public Dictionary<string, BodyPlanDef> Plans { get; set; } = new();
}

/// <summary>Bir govde plani (kus, iki ayak, dort ayak ...): roller, konum ofsetinin kemigi ve 8 klip.</summary>
public sealed class BodyPlanDef
{
    public List<string> Roles { get; set; } = new();
    public string MoveRole { get; set; } = "";
    public Dictionary<string, PoseClipDef> Clips { get; set; } = new();
}

/// <summary>Anahtar pozlar: rol -> [t, x, y, z] (derece, karakter ekseni); move -> [t, x, y, z] (iskelet boyu).</summary>
public sealed class PoseClipDef
{
    public float Length { get; set; }
    public bool Loop { get; set; }
    public string Mirror { get; set; } = "none";
    public Dictionary<string, List<List<float>>> Tracks { get; set; } = new();
    public List<List<float>> Move { get; set; } = new();
}

public sealed partial class GameData
{
    [DataFile("poses.json")]
    public PosesFile Poses { get; private set; } = new();

    public BodyPlanDef Plan(string id) => Poses.Plans[id];
}
