using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Director'un kayit defteri (TEKNIK 9.1): Sim bolumleri (GameState [SaveSection]) + Godot katmaninin bolumleri.
/// Godot'ta durum tutan bir sistem kendi kurulumunda bir satirla baglanir:
/// <code>d.Save.Add(SaveSection.Json("pen.gates", 1, CaptureGates, ApplyGates, SaveOrder.Spawn));</code>
/// Bolum Godot tipi yazmaz (Vector3 yerine float[]); dunyadaki ornekler EntityRecord ve State.Ids ile anilir.
/// Statik degil: her Director (her oyun) kendi defterini kurar.
/// </summary>
public partial class Director
{
    SaveRegistry _save;

    /// <summary>Ilk eriside Sim bolumleriyle kurulur (State hazir olmali).</summary>
    public SaveRegistry Save => _save ??= new SaveRegistry().AddRange(State.SaveSections());
}
