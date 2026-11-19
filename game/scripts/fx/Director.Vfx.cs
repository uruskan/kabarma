using Godot;

namespace Kabarma.Game;

/// <summary>Director, efekt kitapligi (fx/Vfx.cs, VfxSystem.cs, data/vfx.json; M4 efekt akisi).</summary>
public partial class Director
{
    public VfxSystem VfxSys { get; private set; }

    /// <summary>Director._Ready'den bir kez (Fx.Root atandiktan ve Combat kurulduktan sonra).</summary>
    void InitVfx()
    {
        Vfx.Init(this);
        VfxSys = new VfxSystem(this);
        AddChild(VfxSys);
    }
}
