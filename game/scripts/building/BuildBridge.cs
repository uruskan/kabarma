using System.Collections.Generic;
using Godot;

namespace Kabarma.Game;

/// <summary>
/// Insa basligi ile yapi seti arasindaki kopru. Yapi seti (BuildSystem) yoksa null doner ve
/// Insa basligi yalnizca istasyonlari yerlestirir. Seti bulunca parca kimliklerini Insa listesine ekler.
/// </summary>
public partial class BuildBridge : Node
{
    readonly Director _d;
    public readonly List<string> PieceIds = new();
    readonly HashSet<string> _set = new();

    BuildBridge(Director d) { _d = d; }

    public static BuildBridge TryCreate(Director d)
    {
        // Yapi seti henuz projede degil: koprü bos kalir.
        if (!FileAccess.FileExists("res://assets/models/build/wood/sockets.json")) return null;
        var b = new BuildBridge(d) { Name = "BuildBridge" };
        d.AddChild(b);
        b.Init();
        return b;
    }

    partial void InitImpl();
    partial void TickImpl(Player p, Vector3 aim, bool place);
    partial void SelectImpl(string id);
    partial void RotateImpl();
    partial void HideImpl();

    void Init() => InitImpl();
    public bool Handles(string id) => _set.Contains(id);
    public void Tick(Player p, Vector3 aim, bool place) => TickImpl(p, aim, place);
    public void Select(string id) { if (Handles(id)) SelectImpl(id); }
    public void Rotate() => RotateImpl();
    public void Hide() => HideImpl();

    internal void Register(string id) { if (_set.Add(id)) PieceIds.Add(id); }
}
