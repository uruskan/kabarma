namespace Kabarma.Game;

/// <summary>
/// Director: ortak savas (GDD §16.1, §16.3) ve Azmanlar (ICERIK §4.2). CombatSystem hedef kaydi ve alan etkileri;
/// AzmanSystem Azman Homur ve Azman Bozuk. Ornekler Director'da (statik yok, TEKNIK §9).
/// </summary>
public partial class Director
{
    public CombatSystem Savas { get; private set; }

    void InitSavas()
    {
        Savas = new CombatSystem(this) { Name = "Savas" };
        AddChild(Savas);
    }
}
