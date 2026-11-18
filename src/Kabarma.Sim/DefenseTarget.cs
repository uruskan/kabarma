using System.Numerics;

namespace Kabarma.Sim;

/// <summary>
/// GECICI yerel arayuz (savunma akisi, M3): kulelerin ve tuzaklarin gordugu hedef. Savas akisinin hasar ve durum
/// sozlesmesi (Combat*) ve kabarma3'un dalga sozlesmesi (Wave*) main'e girince bunun yerine onlarin hedef kaydi kullanilir;
/// o zamana dek Godot katmani Zipzip ve yabani yaratiklardan bu duz kaydi doldurur (game/scripts/defense/DefenseTargets.cs).
/// </summary>
public readonly record struct DefenseTarget(int Id, Vector3 Pos, Vector3 Vel, float Radius, float Hp, float MaxHp,
    bool Flying = false, bool Thief = false, bool IsPlayer = false);

/// <summary>GECICI: hasar uygulama (savas akisinin Combat sozlesmesi gelene dek). Zirh GDD §16.1: azaltma = zirh / (zirh + 100).</summary>
public static class DefenseHit
{
    public static float Apply(float damage, float armor = 0f) => damage * (1f - Math.Max(0f, armor) / (Math.Max(0f, armor) + 100f));
}
