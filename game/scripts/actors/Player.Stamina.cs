using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Depar ve dayaniklilik (GDD §3.2, URETIM §6 M1 karari 11). Kural Sim'de (Kabarma.Sim.Stamina, GameState.Stamina);
/// burada her fizik karesinde islenir. Bot da ayni kurala tabidir (MoveOverride ile depar istese de).
/// </summary>
public partial class Player
{
    bool _sprinting;

    /// <summary>Dayaniklilik durumu (pano okur). Director yoksa (yalniz sinamada) null.</summary>
    public Stamina Stamina => (GetParent() as Director)?.State?.Stamina;

    /// <summary>Depar istegi -> bu karede depar atiliyor mu (dayaniklilik yetiyorsa).</summary>
    bool SprintGate(bool want, float dt)
    {
        var s = Stamina;
        // Duvara dayanip depar tusuna basmak dayaniklilik yemez: yalniz gercekten kosarken (onceki karenin hizi).
        if (want && s != null && new Godot.Vector2(_prevVel.X, _prevVel.Z).Length() < s.Def.MinSpeed) want = false;
        _sprinting = s == null ? want : s.Tick(dt, want);
        return _sprinting;
    }
}
