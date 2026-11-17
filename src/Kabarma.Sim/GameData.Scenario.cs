namespace Kabarma.Sim;

// Ilk 10 dakikanin kurgusu: scenario.json (PLAN 1.4).

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

public sealed partial class GameData
{
    [DataFile("scenario.json")]
    public ScenarioFile Scenario { get; private set; } = new();
}
