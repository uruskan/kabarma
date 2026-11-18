namespace Kabarma.Sim;

// Akis alanli yol bulma: flowfield.json (TEKNIK 8, 8.1). Kurallar FlowField*.cs, Godot katmani game/scripts/ai/.

public sealed class FlowFieldFile
{
    /// <summary>Pencere kenari, hucre (1 m).</summary>
    public int GridSize { get; set; } = 160;
    /// <summary>Acik duz hucreye girme maliyeti; capraz x1,4.</summary>
    public int OpenCost { get; set; } = 10;
    public float SlopeCostPerDegree { get; set; } = 0.25f;
    public float MaxSlopeDegrees { get; set; } = 42f;
    public int WaterCost { get; set; } = 40;
    /// <summary>Iz turu -> kirma maliyeti (acik hucre maliyetine eklenir).</summary>
    public Dictionary<string, int> BreakCost { get; set; } = new();
    /// <summary>Yapi parcasi turu -> iz turu ("walk", "ignore" ya da BreakCost anahtari).</summary>
    public Dictionary<string, string> PieceKinds { get; set; } = new();
    public float BlockHeight { get; set; } = 1.5f;
    public int MaxRecomputesPerTick { get; set; } = 2;
    public float RescanSeconds { get; set; } = 0.5f;

    public const string Walk = "walk", Ignore = "ignore";

    /// <summary>Parca turunun izi: "walk", "ignore" ya da bir engel turu. Bilinmeyen tur duvar sayilir.</summary>
    public string StampOf(string pieceKind) => PieceKinds.TryGetValue(pieceKind ?? "", out var s) ? s : "wall";

    /// <summary>Engel turunun kirma maliyeti; tabloda yoksa duvarinki.</summary>
    public int BreakOf(string stampKind) => BreakCost.TryGetValue(stampKind, out var c) ? c : BreakCost.GetValueOrDefault("wall", 400);
}

public sealed partial class GameData
{
    [DataFile("flowfield.json")]
    public FlowFieldFile FlowField { get; private set; } = new();
}
