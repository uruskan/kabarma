using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Savas sozlesmesinin Godot yuzu (GDD §16.1, TEKNIK §3.1): silahla, kuleyle, tuzakla ya da alan etkisiyle vurulabilen
/// her sey. Kural Sim'de (Combat.Apply); hedef kendi CombatBody'sini tasir ve isabetin sonucunu gosterir.
/// </summary>
public interface ICombatTarget
{
    Vector3 Center { get; }
    float Radius { get; }
    bool Alive { get; }
    /// <summary>Zayif nokta (kovan, maya kesesi): Capa nisani govdeden once secer, vurus x2 (combat.json weakPointMult).</summary>
    bool IsWeakPoint { get; }
    /// <summary>Saglik ve durum govdesi; alan etkileri (yanik sicramasi, sok zinciri, patlama) bunu kullanir. Null = alan almaz.</summary>
    CombatBody Body { get; }
    /// <summary>Isabet: hedef kendi kuralini uygular (Ata asamasi, kovan ...) ve sonucu doner. from: isabetin geldigi nokta.</summary>
    CombatResult TakeHit(CombatHit hit, Vector3 from);
}
