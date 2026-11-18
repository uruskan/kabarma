namespace Kabarma.Sim;

/// <summary>GameState parcasi: el uretim kuyrugu (GDD 5 Uretim paneli; URETIM 6 M1 karari 23).</summary>
public sealed partial class GameState
{
    CraftQueue? _craft;

    /// <summary>Ilk eriside kurulur (craft.json craftQueueMax); kilitli tarifler Known'a bakar.</summary>
    public CraftQueue Craft => _craft ??= new CraftQueue(Data.Tool.CraftQueueMax) { Known = Known };
}
