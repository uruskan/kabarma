using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Gecis uyumu (savas, M3): M2'deki IAtaTarget.TakeWeapon(dmg, crit, dir, knock) cagrilari (oyuk akisinin testi gibi)
/// savas sozlesmesine (ICombatTarget.TakeHit) iner: Darbe, oyuncu takimi. Yeni kod TakeHit'i dogrudan cagirir.
/// </summary>
public static class AtaTargetCompat
{
    public static CombatResult TakeWeapon(this IAtaTarget t, float dmg, bool crit, Vector3 dir, float knock) =>
        t.TakeHit(new CombatHit(dmg, DamageType.Darbe, CombatTeam.Player) { Crit = crit, Knock = knock }, t.Center - dir);
}
