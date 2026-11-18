using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Gunduz savasi (GDD §16.3, savas akisi): yabani yaratik ortak savas sozlesmesine baglanir. Can WildState.Hp'de kalir
/// (kayit "wildlife" bolumuyle); savas govdesi her isabette ondan kurulur ve durumlari (yanik, donma, sok, zehir)
/// tasir. Tur zayifligi ve direnci combat.json profiles'tan (tur kimligi). Durum zamani CombatSystem'de.
/// </summary>
public partial class WildCreature : ICombatTarget
{
    CombatBody _cbody;

    /// <summary>Savas govdesi: can WildState'ten esitlenir.</summary>
    public CombatBody Body
    {
        get
        {
            _cbody ??= CombatBody.Of(Def.Hp, CombatTeam.Wild, Def.Id, mass: Mathf.Max(0.5f, (Def.Rig?.Height ?? 0.8f) * 1.5f));
            _cbody.Hp = State.Hp;
            return _cbody;
        }
    }

    public Vector3 Center => GlobalPosition + Vector3.Up * (Def.Rig?.Height ?? 0.8f) * 0.5f;
    public float Radius => (Def.Rig?.Height ?? 0.8f) * 0.45f;
    bool ICombatTarget.Alive => !Dead && !Display && IsInsideTree();
    public bool IsWeakPoint => false;

    /// <summary>Isabet: azaltma ve durum Sim'de; canin dusmesi ve tepki (darbe klibi, kacis/ofke) eski yoldan (Hit).</summary>
    public CombatResult TakeHit(CombatHit hit, Vector3 from)
    {
        if (Dead) return default;
        var b = Body;
        float before = b.Hp;
        var r = Combat.Apply(GetDirector()?.Data.Combat ?? new CombatFile(), b, hit);
        b.Hp = before; // can WildState'te: Hit dusurur
        Hit(r.Damage);
        return r;
    }

    /// <summary>Durum zamani (CombatSystem her kare): surekli hasar cani dusurur. Islenen hasar.</summary>
    public float TickCombat(CombatFile data, float dt)
    {
        if (_cbody == null || _cbody.Statuses.Count == 0 || Dead) return 0;
        var b = Body;
        float dealt = Combat.Tick(data, b, dt);
        if (dealt > 0) State.Hp = b.Hp;
        return dealt;
    }

    /// <summary>Durumlarin hiz carpani (donma yavaslatir; kilit ve sersem durdurur).</summary>
    public float CombatSpeedMult(CombatFile data) => _cbody == null ? 1f : Status.SpeedMult(data, _cbody);

    Director _dir;
    Director GetDirector() => _dir ??= GetTree()?.Root.FindChild("Director", true, false) as Director;
}
