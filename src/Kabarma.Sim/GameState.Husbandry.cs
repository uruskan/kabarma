namespace Kabarma.Sim;

public sealed partial class GameState
{
    Ranch? _ranch;

    /// <summary>
    /// Hayvancilik (GDD 9.1-9.4): Coop'un hayvanlari, Yemlik ve Folluk'u uzerine ihtiyac, mutluluk, gen, ureme, rol,
    /// kacma; Suluk, Kasinma Fircasi, Ari Kovani, Kulucka. Kurucuya dokunmamak icin ilk eriste kurulur.
    /// </summary>
    public Ranch Ranch => _ranch ??= new Ranch(Coop, Data.Pen, Data.Husbandry, Data.Calendar, Seed);
}
