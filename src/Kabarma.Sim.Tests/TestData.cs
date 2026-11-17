using Kabarma.Sim;

namespace Kabarma.Sim.Tests;

/// <summary>
/// Ortak veri fiksturu: game/data/*.json test ciktisina kopyalanir (csproj). Load() her cagrida taze yukler;
/// testler ayni nesneyi paylasip birbirini etkilemez.
/// </summary>
public static class TestData
{
    public static string Dir => Path.Combine(AppContext.BaseDirectory, "data");
    public static string Read(string file) => File.ReadAllText(Path.Combine(Dir, file));
    public static GameData Load() => GameData.Load(Read);
}
