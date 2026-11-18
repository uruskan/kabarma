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
    /// <summary>Mermi bir Ata hedefine degdi: Zipzip'le ayni hasar zarlari ve isabet hissi.</summary>
    internal void AtaWeaponHit(string feel, WeaponDef w, CombatStats s, IAtaTarget t, Vector3 at, Vector3 dir, Color color)
    {
        var night = _d.Night;
        float dmg; bool crit;
        if (night != null) (dmg, crit) = night.RollHit(w, t.IsWeakPoint, 1.5f);
        else { dmg = w.Damage; crit = false; }
        t.TakeWeapon(dmg, crit, dir with { Y = 0 }, w.Knockback * s.KnockbackMult);
        if (night != null && night.RollBurn()) t.Burn(w.BurnSeconds);
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
