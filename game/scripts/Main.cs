using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

public partial class Main : Node3D
{
    public override void _Ready()
    {
        var data = GameData.Load(f => FileAccess.GetFileAsString("res://data/" + f));
        GD.Print($"BOOT Kabarma C# ok, crops={data.Crops.Crops.Count} cards={data.Cards.Cards.Count}");
        GetTree().Quit();
    }
}
