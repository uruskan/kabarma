using Godot;

namespace Kabarma.Game;

/// <summary>F4 (coop_panel): Ortak oyun paneli; tek oyuncuda da calisir. Kolda panel ana menuden acilir (kayit akisi).</summary>
public partial class NetHotkeys : Node
{
    readonly Director _d;
    public NetHotkeys(Director d) { _d = d; }
    public NetHotkeys() { }

    public override void _UnhandledInput(InputEvent e)
    {
        if (InputMap.HasAction("coop_panel") && e is InputEventKey && e.IsActionPressed("coop_panel")) CoopPanel.Open(_d, _d);
    }
}
