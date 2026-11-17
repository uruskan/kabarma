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
    // Koc Gidak tekmesi: Zipzip citin ustunden yay cizerek ucar (PLAN §1.4, KickArc).
    public float KickApex { get; set; }
    public float KickDistance { get; set; }
    public float KickFlightSeconds { get; set; }
    public float KickClearAt { get; set; }
    public float KickClearHeight { get; set; }
    public float KickLandStun { get; set; }
    public float KickSpinTurns { get; set; }
    public RigDef? Rig { get; set; }
    public SwellDef Swell { get; set; } = new();
    public DayHerdDef? Day { get; set; }
}

/// <summary>Model, govde plani, kemik eslemesi ve 8 klip yuvasinin kaynagi (SANAT-SES §4, URETIM §2.2 E).</summary>
public sealed class RigDef
{
    public string Plan { get; set; } = "";
    public string Model { get; set; } = "";
    public float Height { get; set; }
    public float Yaw { get; set; }
    public int TriBudget { get; set; }
    public Dictionary<string, string> Bones { get; set; } = new();
    public Dictionary<string, string> Clips { get; set; } = new();
    public List<string> Placeholder { get; set; } = new();
    public Dictionary<string, List<float>> RestFix { get; set; } = new();
    /// <summary>Ragdoll kemikleri: rol -> kapsul yaricapi (iskelet boyunun orani).</summary>
    public Dictionary<string, float> Ragdoll { get; set; } = new();

    /// <summary>Klip yuvasinin kaynagi: "pose" ya da "glb:&lt;ad&gt;". Yazilmamis yuva poz kutuphanesinden gelir.</summary>
    public string ClipSource(string slot) => Clips.TryGetValue(slot, out var s) ? s : "pose";
}

/// <summary>Kabarma gorunusu: ortak sisme shader'inin tur basina ayarlari (SANAT-SES §4).</summary>
public sealed class SwellDef
{
    public float Push { get; set; }
    public float Wobble { get; set; }
    public float Glow { get; set; } = 1f;
    public float VeinScale { get; set; } = 3f;
    public float Seconds { get; set; } = 1f;
    public float StepJiggle { get; set; }
    public float HitJiggle { get; set; }
}

/// <summary>Gunduz surusu (URETIM §6 M1 "Zipzip" satiri, GDD §8.1).</summary>
public sealed class DayHerdDef
{
    public int Groups { get; set; }
    public int GroupMin { get; set; }
    public int GroupMax { get; set; }
    public float SpawnMin { get; set; }
    public float SpawnMax { get; set; }
    public float WanderRadius { get; set; }
    public float WalkSpeedMult { get; set; }
    public float FleeRadius { get; set; }
    public float FleeSpeedMult { get; set; }
    public float FleeSeconds { get; set; }
    public float CropSense { get; set; }
    public float CropStop { get; set; }
    public bool Eats { get; set; }
    public float RespawnSeconds { get; set; }
    public float RagdollSeconds { get; set; }
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
