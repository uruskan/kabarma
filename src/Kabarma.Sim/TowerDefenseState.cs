using System.Numerics;

namespace Kabarma.Sim;

/// <summary>Kurulu kuleler ve tuzaklar (GDD §16.4). Kayit bolumu "defense"; Godot dugumleri bu kimliklerden kurulur.</summary>
public sealed class DefenseState
{
    public List<TowerState> Towers { get; set; } = new();
    public List<TrapState> Traps { get; set; } = new();
    public int NextId { get; set; } = 1;

    public TowerState AddTower(TowerDef d, Vector3 at, float yaw)
    {
        var t = new TowerState { Id = NextId++, Def = d.Id, Pos = new[] { at.X, at.Y, at.Z }, Yaw = yaw, Hp = d.Hp };
        Towers.Add(t);
        return t;
    }

    public TrapState AddTrap(TrapDef d, Vector3 at, float yaw)
    {
        var t = new TrapState { Id = NextId++, Def = d.Id, Pos = new[] { at.X, at.Y, at.Z }, Yaw = yaw, Hp = d.Hp };
        Traps.Add(t);
        return t;
    }

    public bool Remove(int id) => Towers.RemoveAll(t => t.Id == id) + Traps.RemoveAll(t => t.Id == id) > 0;

    /// <summary>Ayni turden, tol icindeki kayitli yapi (kayittan ya da agdan donen dugum kendi durumunu bulur).</summary>
    public int FindAt(string def, Vector3 at, float tol = 0.3f)
    {
        foreach (var t in Towers) if (t.Def == def && Vector3.Distance(t.At, at) < tol) return t.Id;
        foreach (var t in Traps) if (t.Def == def && Vector3.Distance(t.At, at) < tol) return t.Id;
        return 0;
    }

    public TowerState? Tower(int id) => Towers.FirstOrDefault(t => t.Id == id);
    public TrapState? Trap(int id) => Traps.FirstOrDefault(t => t.Id == id);
}

public sealed partial class GameState
{
    public DefenseState Defense { get; private set; } = new();

    [SaveSection("defense", 1, Order = SaveOrder.Spawn, Covers = new[] { nameof(Defense) })]
    DefenseState DefenseSection
    {
        get => Defense;
        set { value ??= new DefenseState(); Defense.Towers = value.Towers; Defense.Traps = value.Traps; Defense.NextId = value.NextId; } // yerinde doldurur
    }
}
