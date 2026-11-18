using System.Linq;
using Godot;
using Kabarma.Sim;

namespace Kabarma.Game;

/// <summary>
/// Capa, Ata baglantisi (boss akisi, GDD §18.1): silah isabeti Ata hedeflerine (govde, kovan, yardimci) ve Bicme ile
/// kirkma. Mermiler (Capa.Weapons.cs) ve Mow (Capa.Bicme.cs) birer satirla buraya baglanir.
/// </summary>
public partial class Capa
{
    /// <summary>
    /// Mermi bir Ata/Azman hedefine degdi (savas sozlesmesi): kritik ve zayif nokta zari (gece Night.RollHit, gunduz
    /// silahin kendi kritik sansi), hasar turu ve kart durumlari CombatSystem.WeaponHit'ten; kural hedefin TakeHit'inde.
    /// </summary>
    internal void AtaWeaponHit(string feel, WeaponDef w, CombatStats s, IAtaTarget t, Vector3 at, Vector3 dir, Color color)
    {
        var night = _d.Night;
        float weakMult = _d.Data.Combat.WeakPointMult;
        float dmg; bool crit;
        if (night != null) (dmg, crit) = night.RollHit(w, t.IsWeakPoint, weakMult);
        else { crit = GD.Randf() < w.CritChance; dmg = Kabarma.Sim.Combat.Raw(w.Damage, crit, w.CritMult, t.IsWeakPoint, weakMult); }
        var hit = _d.Savas.WeaponHit(w, dmg, crit, t.IsWeakPoint, w.Knockback * s.KnockbackMult);
        _d.Savas.Hit(t, hit, at - dir);
        FeelHit(feel, at, crit || t.IsWeakPoint ? Palette.Warning : color, crit, t.IsWeakPoint);
    }

    /// <summary>Bicme vurusu Ata'ya yetisti mi: yetistiyse kirkar (Koca Pofuduk asama 2). Kirkilan Ata sayisi.</summary>
    int MowAta(Vector3 aim)
    {
        var sys = _d.Atalar;
        if (sys?.Actor == null || !sys.Actor.Alive) return 0;
        var a = sys.Actor;
        var c = a.Center;
        if (new Vector2(c.X - aim.X, c.Z - aim.Z).Length() > _t.MowRadius + a.Radius) return 0;
        if (!sys.Shear(aim)) return 0;
        FeelHit("bicme_tutam", aim + Vector3.Up * 0.6f, Palette.OffWhite, squash: a.Visual);
        return 1;
    }
}
